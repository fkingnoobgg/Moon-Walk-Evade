using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using Moon_Walk_Evade.Evading;
using Moon_Walk_Evade.Utils;
using SharpDX;
using Color = System.Drawing.Color;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    public class LinearSkillshot : EvadeSkillshot
    {
        public LinearSkillshot()
        {
            Caster = null;
            SpawnObject = null;
            SData = null;
            OwnSpellData = null;
            Team = GameObjectTeam.Unknown;
            IsValid = true;
            TimeDetected = Environment.TickCount;
        }

        public Vector3 FixedStartPos;
        public Vector3 FixedEndPos;
        private bool DoesCollide;
        private Vector2 LastCollisionPos;

        public MissileClient Missile => OwnSpellData.IsPerpendicular ? null : SpawnObject as MissileClient;

        public Vector3 CurrentPosition
        {
            get
            {
                bool debugMode = EvadeMenu.HotkeysMenu["debugMode"].Cast<KeyBind>().CurrentValue;
                if (Missile == null)
                {
                    if (debugMode)
                        return Debug.GlobalStartPos;
                    return FixedStartPos;
                }

                
                if (debugMode)//Simulate Position
                {
                    float speed = OwnSpellData.MissileSpeed;
                    float timeElapsed = Environment.TickCount - TimeDetected - OwnSpellData.Delay;
                    float traveledDist = speed * timeElapsed / 1000;
                    return Debug.GlobalStartPos.Extend(Debug.GlobalEndPos, traveledDist).To3D();
                }

                if (DoesCollide && Missile.Position.Distance(Missile.StartPosition) >= LastCollisionPos.Distance(Missile.StartPosition))
                    return LastCollisionPos.To3D();

                return Missile.Position;
            }
        }

        public Vector3 EndPosition
        {
            get
            {

                bool debugMode = EvadeMenu.HotkeysMenu["debugMode"].Cast<KeyBind>().CurrentValue;
                if (debugMode)
                    return Debug.GlobalEndPos;

                if (Missile == null)
                {
                    return FixedEndPos;
                }

                if (DoesCollide)
                    return LastCollisionPos.To3D();


                return Missile.StartPosition.ExtendVector3(Missile.EndPosition, OwnSpellData.Range + 100);
            }
        }

        public override Vector3 GetCurrentPosition()
        {
            return CurrentPosition;
        }

        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new LinearSkillshot { OwnSpellData = OwnSpellData };
            if (debug)
            {
                bool isProjectile = EvadeMenu.HotkeysMenu["isProjectile"].Cast<CheckBox>().CurrentValue;
                var newDebugInst = new LinearSkillshot
                {
                    OwnSpellData = OwnSpellData, FixedStartPos = Debug.GlobalStartPos,
                    FixedEndPos = Debug.GlobalEndPos, IsValid = true, IsActive = true, TimeDetected = Environment.TickCount-Game.Ping-45,
                    SpawnObject = isProjectile ? new MissileClient() : null
                };
                return newDebugInst;
            }
            return newInstance;
        }

        public override void OnCreateObject(GameObject obj)
        {
            var missile = obj as MissileClient;

            bool debugMode = EvadeMenu.HotkeysMenu["debugMode"].Cast<KeyBind>().CurrentValue;
            if (SpawnObject == null && missile != null && !debugMode)
            {
                if (missile.SData.Name == OwnSpellData.ObjectCreationName && missile.SpellCaster.Index == Caster.Index)
                {
                    IsValid = false;
                }
            }

            if (missile != null && !DoesCollide) //missle
            {
                Vector2 collision = this.GetCollisionPoint();
                DoesCollide = !collision.IsZero;
                LastCollisionPos = collision;

                //if (DoesCollide && !LastCollisionPos.ProjectOn(CurrentPosition.To2D(), FixedEndPosition.To2D()).IsOnSegment)
                //    DoesCollide = false;s
            }
        }

        public override void OnSpellDetection(Obj_AI_Base sender)
        {
            if (!OwnSpellData.IsPerpendicular)
            {
                FixedStartPos = Caster.ServerPosition;
                FixedEndPos = FixedStartPos.ExtendVector3(CastArgs.End, OwnSpellData.Range);
            }
            else
            {
                OwnSpellData.Direction = (CastArgs.End - CastArgs.Start).To2D().Normalized();

                var direction = OwnSpellData.Direction;
                FixedStartPos = (CastArgs.End.To2D() - direction.Perpendicular() * OwnSpellData.SecondaryRadius).To3D();

                FixedEndPos = (CastArgs.End.To2D() + direction.Perpendicular() * OwnSpellData.SecondaryRadius).To3D();
            }
        }

        public override void OnTick()
        {
            var debug = EvadeMenu.HotkeysMenu["debugMode"].Cast<KeyBind>().CurrentValue;
            if (Missile == null)
            {
                if (Environment.TickCount > TimeDetected + OwnSpellData.Delay + 250)
                {
                    IsValid = false;
                    return;
                }
            }
            else if (Missile != null && !debug)
            {
                if (Environment.TickCount > TimeDetected + 6000)
                {
                    IsValid = false;
                    return;
                }
            }

            if (debug)
            {
                float speed = OwnSpellData.MissileSpeed;
                float timeElapsed = Environment.TickCount - TimeDetected - OwnSpellData.Delay;
                float traveledDist = speed * timeElapsed / 1000;

                if (traveledDist >= Debug.GlobalStartPos.Distance(Debug.GlobalEndPos) - 50)
                {
                    IsValid = false;
                    return;
                }
            }
        }

        public override void OnDraw()
        {
            if (!IsValid)
            {
                return;
            }

            Utils.Utils.Draw3DRect(CurrentPosition, EndPosition, OwnSpellData.Radius * 2, Color.White);
        }

        public override Geometry.Polygon ToRealPolygon()
        {
            var halfWidth = OwnSpellData.Radius;
            var d1 = CurrentPosition.To2D();
            var d2 = EndPosition.To2D();
            var direction = (d1 - d2).Perpendicular().Normalized();

            Vector3[] points =
            {
                (d1 + direction*halfWidth).To3DPlayer(),
                (d1 - direction*halfWidth).To3DPlayer(),
                (d2 - direction*halfWidth).To3DPlayer(),
                (d2 + direction*halfWidth).To3DPlayer()
            };
            var p = new Geometry.Polygon();
            p.Points.AddRange(points.Select(x => x.To2D()).ToList());

            return p;
        }

        public override Geometry.Polygon ToPolygon(float extrawidth = 0)
        {
            if (OwnSpellData.AddHitbox)
            {
                extrawidth += Player.Instance.HitBoxRadius();
            }

            return new Geometry.Polygon.Rectangle(CurrentPosition, EndPosition.ExtendVector3(CurrentPosition, -extrawidth), OwnSpellData.Radius + extrawidth);
        }

        public override int GetAvailableTime(Vector2 pos)
        {
            if (Missile == null)
            {
                return Math.Max(0, OwnSpellData.Delay - (Environment.TickCount - TimeDetected));
            }

            var proj = pos.ProjectOn(CurrentPosition.To2D(), EndPosition.To2D());
            if (!proj.IsOnSegment)
                return short.MaxValue;

            var dest = proj.SegmentPoint;
            var InsidePath = Player.Instance.GetPath(dest.To3D(), true).Where(segment => ToPolygon().IsInside(segment));
            var point = InsidePath.OrderBy(x => x.Distance(CurrentPosition)).FirstOrDefault();

            if (point == default(Vector3))
                return short.MaxValue;

            float skillDist = point.Distance(CurrentPosition);
            return (int)(skillDist/OwnSpellData.MissileSpeed*1000);
        }

        public override bool IsFromFow()
        {
            return Missile != null && !Missile.SpellCaster.IsVisible;
        }

        public override bool IsSafe(Vector2? p = null)
        {
            return ToPolygon().IsOutside(p ?? Player.Instance.Position.To2D());
        }

        public override Vector2 GetMissilePosition(int extraTime)
        {
            if (Missile == null)
                return FixedStartPos.To2D();//Missile not even created

            float dist = OwnSpellData.MissileSpeed/1000f*extraTime;
            if (dist > CurrentPosition.Distance(EndPosition))
                dist = CurrentPosition.Distance(EndPosition);


            return CurrentPosition.Extend(EndPosition, dist);
        }

        public override bool IsSafePath(Vector2[] path, int timeOffset = 0, int speed = -1, int delay = 0)
        {
            var Distance = 0f;
            timeOffset += Game.Ping / 2;

            speed = speed == -1 ? (int)ObjectManager.Player.MoveSpeed : speed;

            var allIntersections = new List<FoundIntersection>();
            for (var i = 0; i <= path.Length - 2; i++)
            {
                var from = path[i];
                var to = path[i + 1];
                var segmentIntersections = new List<FoundIntersection>();
                var polygon = ToPolygon();

                for (var j = 0; j <= polygon.Points.Count - 1; j++)
                {
                    var sideStart = polygon.Points[j];
                    var sideEnd = polygon.Points[j == polygon.Points.Count - 1 ? 0 : j + 1];

                    var intersection = from.Intersection(to, sideStart, sideEnd);

                    if (intersection.Intersects)
                    {
                        segmentIntersections.Add(
                            new FoundIntersection(
                                Distance + intersection.Point.Distance(from),
                                (int)((Distance + intersection.Point.Distance(from)) * 1000 / speed),
                                intersection.Point, from));
                    }
                }

                var sortedList = segmentIntersections.OrderBy(o => o.Distance).ToList();
                allIntersections.AddRange(sortedList);

                Distance += from.Distance(to);
            }

            //Skillshot with missile.
            if (Missile != null)
            {
                var debug = EvadeMenu.HotkeysMenu["debugMode"].Cast<KeyBind>().CurrentValue;
                var MissileStartPosition = debug ? Debug.GlobalStartPos.To2D() : Missile.StartPosition.To2D();
                //Outside the skillshot
                if (IsSafe())
                {
                    //No intersections -> Safe
                    if (allIntersections.Count == 0)
                    {
                        return true;
                    }

                    for (var i = 0; i <= allIntersections.Count - 1; i = i + 2)
                    {
                        var enterIntersection = allIntersections[i];
                        var enterIntersectionProjection = enterIntersection.Point.ProjectOn(MissileStartPosition, EndPosition.To2D()).SegmentPoint;

                        //Intersection with no exit point.
                        if (i == allIntersections.Count - 1)
                        {
                            var missilePositionOnIntersection = GetMissilePosition(enterIntersection.Time - timeOffset);
                            bool safe = EndPosition.Distance(missilePositionOnIntersection) + 50 <=
                                        EndPosition.Distance(enterIntersectionProjection) &&
                                        ObjectManager.Player.MoveSpeed < OwnSpellData.MissileSpeed;
                            return safe;
                        }

                        var exitIntersection = allIntersections[i + 1];
                        var exitIntersectionProjection = exitIntersection.Point.ProjectOn(MissileStartPosition, EndPosition.To2D()).SegmentPoint;

                        var missilePosOnEnter = GetMissilePosition(enterIntersection.Time - timeOffset);
                        var missilePosOnExit = GetMissilePosition(exitIntersection.Time + timeOffset);

                        //Missile didnt pass.
                        if (missilePosOnEnter.Distance(EndPosition) + 50 > enterIntersectionProjection.Distance(EndPosition))
                        {
                            if (missilePosOnExit.Distance(EndPosition) <= exitIntersectionProjection.Distance(EndPosition))
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }

                //Inside the skillshot.
                if (allIntersections.Count == 0)
                {
                    return false;
                }

                if (allIntersections.Count > 0)
                {
                    //Check only for the exit point
                    var exitIntersection = allIntersections[0];
                    var exitIntersectionProjection = exitIntersection.Point.ProjectOn(FixedStartPos.To2D(), EndPosition.To2D()).SegmentPoint;

                    var missilePosOnExit = GetMissilePosition(exitIntersection.Time + timeOffset);
                    if (missilePosOnExit.Distance(EndPosition) <= exitIntersectionProjection.Distance(EndPosition))
                    {
                        return false;
                    }
                }
            }

            //No Missile
            if (allIntersections.Count == 0)
            {
                return IsSafe();
            }

            var timeToExplode = OwnSpellData.Delay + (Environment.TickCount - TimeDetected);

            var myPositionWhenExplodes = path.PositionAfter(timeToExplode, speed, delay);

            if (!IsSafe(myPositionWhenExplodes))
            {
                return false;
            }

            var myPositionWhenExplodesWithOffset = path.PositionAfter(timeToExplode, speed, timeOffset);

            return IsSafe(myPositionWhenExplodesWithOffset);
        }
    }
}