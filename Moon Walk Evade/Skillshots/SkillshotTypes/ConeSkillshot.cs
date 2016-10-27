using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using Moon_Walk_Evade.Utils;
using SharpDX;
using Color = System.Drawing.Color;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    class ConeSkillshot : EvadeSkillshot
    {
        public ConeSkillshot()
        {
            Caster = null;
            SpawnObject = null;
            SData = null;
            OwnSpellData = null;
            Team = GameObjectTeam.Unknown;
            IsValid = true;
            TimeDetected = Environment.TickCount;
        }

        public Vector3 _FixedStartPos;
        public Vector3 _FixedEndPos;

        public MissileClient Missile => OwnSpellData.IsPerpendicular ? null : SpawnObject as MissileClient;

        public Vector3 FixedStartPos
        {
            get
            {
                bool debugMode = EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;

                if (debugMode)
                    return Debug.GlobalStartPos;

                if (Missile == null)
                    return _FixedStartPos;

                return Missile.StartPosition;
            }
        }

        public Vector3 CurrentPos
        {
            get
            {
                bool debugMode = EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;
                if (Missile == null)
                {
                    if (debugMode)
                        return Debug.GlobalStartPos;
                    return _FixedStartPos;
                }


                if (debugMode)//Simulate Position
                {
                    float speed = OwnSpellData.MissileSpeed;
                    float timeElapsed = Environment.TickCount - TimeDetected - OwnSpellData.Delay;
                    float traveledDist = speed * timeElapsed / 1000;
                    return Debug.GlobalStartPos.Extend(Debug.GlobalEndPos, traveledDist).To3D();
                }

                return Missile.Position;
            }
        }

        public Vector3 FixedEndPos
        {
            get
            {
                bool debugMode = EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;
                if (debugMode)
                    return Debug.GlobalEndPos;

                if (Missile == null)
                    return _FixedEndPos;

                return Missile.StartPosition.ExtendVector3(Missile.EndPosition, OwnSpellData.Range);
            }
        }

        public override Vector3 GetCurrentPosition()
        {
            return CurrentPos;
        }

        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new ConeSkillshot { OwnSpellData = OwnSpellData };
            if (debug)
            {
                bool isProjectile = EvadeMenu.DebugMenu["isProjectile"].Cast<CheckBox>().CurrentValue;
                var newDebugInst = new ConeSkillshot
                {
                    OwnSpellData = OwnSpellData,
                    _FixedStartPos = Debug.GlobalStartPos,
                    _FixedEndPos = Debug.GlobalEndPos,
                    IsValid = true,
                    IsActive = true,
                    TimeDetected = Environment.TickCount,
                    SpawnObject = isProjectile ? new MissileClient() : null
                };
                return newDebugInst;
            }
            return newInstance;
        }

        public override void OnCreateObject(GameObject obj)
        {
            var missile = obj as MissileClient;

            if (SpawnObject == null && missile != null)
            {
                if (missile.SData.Name == OwnSpellData.ObjectCreationName && missile.SpellCaster.Index == Caster.Index)
                {
                    IsValid = false;
                }
            }
        }

        public override void OnSpellDetection(Obj_AI_Base sender)
        {
            _FixedStartPos = Caster.ServerPosition;
            _FixedEndPos = _FixedStartPos.ExtendVector3(CastArgs.End, OwnSpellData.Range);
        }

        public override void OnTick()
        {
            if (Missile == null)
            {
                if (Environment.TickCount > TimeDetected + OwnSpellData.Delay + 250)
                {
                    IsValid = false;
                }
            }
            else if (Missile != null)
            {
                if (Environment.TickCount > TimeDetected + 6000)
                {
                    IsValid = false;
                }
            }

            if (EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue)
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
            if (!EvadeMenu.DrawMenu["drawDangerPolygon"].Cast<CheckBox>().CurrentValue)
            {
                ToPolygon().Draw(Color.White);
            }
        }

        Vector2 RotateAroundPoint(Vector2 start, Vector2 end, float theta)
        {
            float px = end.X, py = end.Y;
            float ox = start.X, oy = start.Y;

            float x = (float)Math.Cos(theta) * (px - ox) - (float)Math.Sin(theta) * (py - oy) + ox;
            float y = (float)Math.Sin(theta) * (px - ox) + (float)Math.Cos(theta) * (py - oy) + oy;
            return new Vector2(x, y);
        }

        Vector2[] GetBeginEdgePoints(Vector2[] edges)
        {
            var endEdges = edges;

            Vector2 direction = (FixedEndPos - FixedStartPos).To2D();
            var perpVecStart = CurrentPos.To2D() + direction.Normalized().Perpendicular();
            var perpVecEnd = CurrentPos.To2D() + direction.Normalized().Perpendicular() * 1500;

            //right side is not the same?
            var perpVecStart2 = CurrentPos.To2D() + direction.Normalized().Perpendicular2();
            var perpVecEnd2 = CurrentPos.To2D() + direction.Normalized().Perpendicular2() * 1500;


            Geometry.Polygon.Line leftEdgeLine = new Geometry.Polygon.Line(FixedStartPos.To2D(), endEdges[1]);
            Geometry.Polygon.Line rightEdgeLine = new Geometry.Polygon.Line(FixedStartPos.To2D(), endEdges[0]);

            var inters = leftEdgeLine.GetIntersectionPointsWithLineSegment(perpVecStart, perpVecEnd);
            var inters2 = rightEdgeLine.GetIntersectionPointsWithLineSegment(perpVecStart2, perpVecEnd2);
            Vector2 p1 = Vector2.Zero, p2 = Vector2.Zero;



            if (inters.Any())
            {
                var closestInter = inters.OrderBy(x => x.Distance(CurrentPos)).First();
                p2 = closestInter;
            }
            if (inters2.Any())
            {
                var closestInter = inters2.OrderBy(x => x.Distance(CurrentPos)).First();
                p1 = closestInter;
            }

            if (!p1.IsZero && !p2.IsZero)
                return new[] { p1, p2 };


            return new[] { CurrentPos.To2D(),  CurrentPos.To2D() };
        }

        public override Geometry.Polygon ToPolygon()
        {
            List<Vector2> coneSegemnts = new List<Vector2>();
            for (float i = -OwnSpellData.ConeAngle / 2f; i <= OwnSpellData.ConeAngle / 2f; i++)
            {
                coneSegemnts.Add(RotateAroundPoint(FixedStartPos.To2D(), FixedEndPos.To2D(), i * (float)Math.PI / 180));
            }

            if (Missile != null)
            {
                var beginPoints = GetBeginEdgePoints(new[] { coneSegemnts.First(), coneSegemnts.Last() });
                coneSegemnts.Insert(0, beginPoints[0]);
                coneSegemnts.Insert(0, beginPoints[1]);
            }
            else
                coneSegemnts.Insert(0, FixedStartPos.To2D());

            Geometry.Polygon polygon = new Geometry.Polygon();
            polygon.Points.AddRange(coneSegemnts);

            return polygon;
        }

        public override Geometry.Polygon ToExactPolygon(float extrawidth = 0)
        {
            var poly = ToPolygon();
            var poly2 = new Geometry.Polygon();

            for (int i = 0; i < poly.Points.Count; i++)
            {
                var p = poly.Points[i];
                var nextP = poly.Points[i == poly.Points.Count - 1 ? 0 : i + 1];

                if (p.Distance(nextP) > 50)
                {
                    int steps = (int)Math.Floor(p.Distance(nextP) / 50);

                    float extendDist = 50;
                    for (int step = 0; step < steps; step++)
                    {
                        poly2.Points.Add(p.Extend(nextP, extendDist));
                        extendDist += 50;
                    }

                    if (p.Distance(nextP) % 50 != 0)
                        poly2.Points.Add(nextP);
                }
                else
                {
                    poly2.Points.Add(nextP);
                }
            }
            return poly2;
        }

        public override int GetAvailableTime(Vector2 pos)
        {
            var dist1 =
                Math.Abs((FixedEndPos.Y - CurrentPos.Y) * pos.X - (FixedEndPos.X - CurrentPos.X) * pos.Y +
                         FixedEndPos.X * CurrentPos.Y - FixedEndPos.Y * CurrentPos.X) / CurrentPos.Distance(FixedEndPos);

            var actualDist = Math.Sqrt(CurrentPos.Distance(pos).Pow() - dist1.Pow());

            var time = OwnSpellData.MissileSpeed > 0 ? (int)(actualDist / OwnSpellData.MissileSpeed * 1000) : 0;

            if (Missile == null)
            {
                return Math.Max(0, OwnSpellData.Delay - (Environment.TickCount - TimeDetected));
            }

            return time;
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
            return FixedEndPos.To2D();
        }

        public override bool IsSafePath(Vector2[] path, int timeOffset = 0, int speed = -1, int delay = 0, [CallerMemberName] string caller = null)
        {
            if (path.Length <= 1) //lastissue = playerpos
                return IsSafe();

            timeOffset -= 40;

            speed = speed == -1 ? (int)ObjectManager.Player.MoveSpeed : speed;

            var allIntersections = new List<FoundIntersection>();
            var segmentIntersections = new List<FoundIntersection>();
            var polygon = ToExactPolygon();

            var from = path[0];
            var to = path[1];

            for (var j = 0; j <= polygon.Points.Count - 1; j++)
            {
                var sideStart = polygon.Points[j];
                var sideEnd = polygon.Points[j == polygon.Points.Count - 1 ? 0 : j + 1];

                var intersection = from.Intersection(to, sideStart, sideEnd);

                if (intersection.Intersects)
                {
                    segmentIntersections.Add(
                        new FoundIntersection(intersection.Point.Distance(from), (int)(intersection.Point.Distance(from) * 1000 / speed) + delay,
                            intersection.Point, from));
                }
            }

            var sortedList = segmentIntersections.OrderBy(o => o.Distance).ToList();
            allIntersections.AddRange(sortedList);

            //Skillshot with missile.
            if (SpawnObject != null)
            {
                var debug = EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;
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
                        var enterIntersectionProjection = enterIntersection.Point.ProjectOn(MissileStartPosition, FixedEndPos.To2D()).SegmentPoint;

                        //Intersection with no exit point.
                        if (i == allIntersections.Count - 1)
                        {
                            var missilePositionOnIntersection = GetMissilePosition(enterIntersection.Time - timeOffset);
                            bool safe = FixedEndPos.Distance(missilePositionOnIntersection) + 50 <=
                                        FixedEndPos.Distance(enterIntersectionProjection) &&
                                        ObjectManager.Player.MoveSpeed < OwnSpellData.MissileSpeed;
                            return safe;
                        }

                        var exitIntersection = allIntersections[i + 1];
                        var exitIntersectionProjection = exitIntersection.Point.ProjectOn(MissileStartPosition, FixedEndPos.To2D()).SegmentPoint;

                        var missilePosOnEnter = GetMissilePosition(enterIntersection.Time - timeOffset);
                        var missilePosOnExit = GetMissilePosition(exitIntersection.Time + timeOffset);

                        //Missile didnt pass.
                        if (missilePosOnEnter.Distance(FixedEndPos) > enterIntersectionProjection.Distance(FixedEndPos))
                        {
                            if (missilePosOnExit.Distance(FixedEndPos) <= exitIntersectionProjection.Distance(FixedEndPos))
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
                    var exitIntersectionProjection = exitIntersection.Point.ProjectOn(MissileStartPosition, FixedEndPos.To2D()).SegmentPoint;

                    var missilePosOnExit = GetMissilePosition(exitIntersection.Time + timeOffset);
                    return missilePosOnExit.Distance(FixedEndPos) > exitIntersectionProjection.Distance(FixedEndPos);
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
