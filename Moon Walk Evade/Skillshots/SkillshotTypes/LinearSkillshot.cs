using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EloBuddy;
using EloBuddy.Sandbox;
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
                bool debugMode = EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;
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

                bool debugMode = EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;
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
                bool isProjectile = EvadeMenu.DebugMenu["isProjectile"].Cast<CheckBox>().CurrentValue;
                var newDebugInst = new LinearSkillshot
                {
                    OwnSpellData = OwnSpellData,
                    FixedStartPos = Debug.GlobalStartPos,
                    FixedEndPos = Debug.GlobalEndPos,
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

            bool debugMode = EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;
            if (SpawnObject == null && missile != null && !debugMode)
            {
                if (missile.SData.Name == OwnSpellData.ObjectCreationName && missile.SpellCaster.Index == Caster.Index)
                {
                    IsValid = false;
                }
            }
        }

        public override void OnCreateUnsafe(GameObject obj)
        {
            var missile = obj as MissileClient;
            if (missile != null) //missle
            {
                Vector2 collision = this.GetCollisionPoint();
                DoesCollide = !collision.IsZero;
                LastCollisionPos = collision;
            }
        }

        public override void OnSpellDetection(Obj_AI_Base sender)
        {
            if (!OwnSpellData.IsPerpendicular)
            {
                FixedStartPos = Caster.ServerPosition;
                FixedEndPos = FixedStartPos.ExtendVector3(CastArgs.End, OwnSpellData.Range + 100);
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
            var debug = EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;
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

           
            MyUtils.Draw3DRect(CurrentPosition, EndPosition, OwnSpellData.Radius * 2, Color.White, 3);
        }

        public override Geometry.Polygon ToPolygon()
        {
            float extrawidth = 0;
            if (OwnSpellData.AddHitbox || true)
            {
                extrawidth += Player.Instance.BoundingRadius * 1.7f;
            }

            return new Geometry.Polygon.Rectangle(CurrentPosition, EndPosition.ExtendVector3(CurrentPosition, -extrawidth), OwnSpellData.Radius + extrawidth);
        }

        public override int GetAvailableTime(Vector2 pos)
        {
            if (Missile == null)
            {
                return Math.Max(0, OwnSpellData.Delay - (Environment.TickCount - TimeDetected) - Game.Ping);
            }

            var proj = pos.ProjectOn(CurrentPosition.To2D(), EndPosition.To2D());
            if (!proj.IsOnSegment)
                return short.MaxValue;

            float skillDist = proj.SegmentPoint.Distance(CurrentPosition) - OwnSpellData.Radius - Player.Instance.BoundingRadius;
            return Math.Max(0, (int)(skillDist / OwnSpellData.MissileSpeed * 1000));
        }

        public override bool IsFromFow()
        {
            return Missile != null && !Missile.SpellCaster.IsVisible;
        }

        public override bool IsSafe(Vector2? p = null)
        {
            return ToPolygon().IsOutside(p ?? Player.Instance.Position.To2D());
        }

        /*ping attened from caller*/
        public override Vector2 GetMissilePosition(int extraTime)
        {
            if (Missile == null)
                return FixedStartPos.To2D();//Missile not even created

            float dist = OwnSpellData.MissileSpeed / 1000f * extraTime;
            if (dist > CurrentPosition.Distance(EndPosition))
                dist = CurrentPosition.Distance(EndPosition);


            return CurrentPosition.Extend(EndPosition, dist);
        }

        public override bool IsSafePath(Vector2[] path, int timeOffset = 0, int speed = -1, int delay = 0)
        {
            timeOffset += Game.Ping;
            speed = speed == -1 ? (int)Player.Instance.MoveSpeed : speed;
            if (path.Length <= 1) //lastissue = playerpos
            {
                //timeNeeded = -11;
                if (!Player.Instance.IsRecalling())
                    return IsSafe();

                if (IsSafe())
                    return true;

                float timeLeft = (Player.Instance.GetBuff("recall").EndTime - Game.Time) * 1000;
                return GetAvailableTime(Player.Instance.Position.To2D()) > timeLeft;
            }

            //Skillshot with missile.
            if (!string.IsNullOrEmpty(OwnSpellData.ObjectCreationName))
            {
                float r = Missile == null ? TimeDetected + OwnSpellData.Delay - Environment.TickCount : 0;
                r -= timeOffset;

                Vector3 pathDir = path[1].To3D() - path[0].To3D();
                Vector3 skillDir = EndPosition - CurrentPosition;
                
                float a = path[0].X;
                float w = path[0].Y;
                float m = path[0].To3D().Z;

                float v = CurrentPosition.X;
                float k = CurrentPosition.Y;
                float o = CurrentPosition.Z;

                float b = pathDir.X;
                float j = pathDir.Y;
                float n = pathDir.Z;

                float f = skillDir.X;
                float l = skillDir.Y;
                float p = skillDir.Z;

                float c = speed;
                float d = pathDir.Length();
                
                float g = OwnSpellData.MissileSpeed;
                float h = skillDir.Length();

                /*nullstelle d/dt - min distance*/
                double t = ((1000 * Math.Pow(d, 2) * g * h * l - 1000 * c * d * Math.Pow(h, 2) * j) * w + (1000 * b * c * d * 
                            Math.Pow(h, 2) - 1000 * Math.Pow(d, 2) * f * g * h) * v + (c * d * g * h * n * p - Math.Pow(c, 2) * 
                            Math.Pow(h, 2) * Math.Pow(n, 2) + c * d * g * h * j * l - Math.Pow(c, 2) * Math.Pow(h, 2) * Math.Pow(j, 2) -
                            Math.Pow(b, 2) * Math.Pow(c, 2) * Math.Pow(h, 2) + b * c * d * f * g * h) * r + (1000 * Math.Pow(d, 2) * g * 
                            h * m - 1000 * Math.Pow(d, 2) * g * h * o) * p + 1000 * c * d * Math.Pow(h, 2) * n * o - 1000 * c * d * 
                            Math.Pow(h, 2) * m * n - 1000 * Math.Pow(d, 2) * g * h * k * l + 1000 * c * d *
                            Math.Pow(h, 2) * j * k - 1000 * a * b * c * d * Math.Pow(h, 2) + 1000 * a * Math.Pow(d, 2) * f * g * h)/
                            (1000 * Math.Pow(d, 2) * Math.Pow(g, 2) * Math.Pow(p, 2) - 2000 * c * d * g * h * n * p +
                            1000 * Math.Pow(c, 2) * Math.Pow(h, 2) * Math.Pow(n, 2) + 1000 * Math.Pow(d, 2) * Math.Pow(g, 2) * Math.Pow(l, 2) -
                            2000 * c * d * g * h * j * l + 1000 * Math.Pow(c, 2) * Math.Pow(h, 2) * Math.Pow(j, 2) + 1000 * Math.Pow(b, 2) *
                            Math.Pow(c, 2) * Math.Pow(h, 2) - 2000 * b * c * d * f * g * h + 1000 * Math.Pow(d, 2) * Math.Pow(f, 2) * 
                            Math.Pow(g, 2));
                
                Vector3 myPosition = path[0].To3D() + (float)t * pathDir * c / pathDir.Length();
                Vector3 misPosition = CurrentPosition + (float)t * skillDir * g / skillDir.Length();

                bool valid = myPosition.Distance(Player.Instance) <= Player.Instance.Distance(path[1]) &&
                    misPosition.Distance(CurrentPosition) <= CurrentPosition.Distance(EndPosition) && t >= 0;

                if (!valid && t >= 0)
                {
                    /*t out of skill range => set t to skillshot maxrange*/
                    if (misPosition.Distance(CurrentPosition) > CurrentPosition.Distance(EndPosition))
                    {
                        t = CurrentPosition.Distance(EndPosition)/OwnSpellData.MissileSpeed + r/1000;

                        myPosition = path[0].To3D() + (float) t*pathDir*c/pathDir.Length();
                        misPosition = EndPosition;

                        return myPosition.Distance(misPosition) > OwnSpellData.Radius + Player.Instance.BoundingRadius;
                    }

                    /*t out of path range*/
                    if (myPosition.Distance(Player.Instance) > Player.Instance.Distance(path[1]))
                    {
                        t = path[0].Distance(path[1]) / speed;

                        myPosition = path[1].To3D();
                        misPosition = CurrentPosition + (float)t * skillDir * g / skillDir.Length();
                        bool pathEndSafe = myPosition.Distance(misPosition) > OwnSpellData.Radius + Player.Instance.BoundingRadius;

                        return pathEndSafe && ToPolygon().IsOutside(path[1]);
                    }
                }

                return !valid || myPosition.Distance(misPosition) > OwnSpellData.Radius + Player.Instance.BoundingRadius;
            }

            var timeToExplode = TimeDetected + OwnSpellData.Delay - Environment.TickCount;
            if (timeToExplode <= 0)
            {
                bool intersects;
                MyUtils.GetLinesIntersectionPoint(CurrentPosition.To2D(), EndPosition.To2D(), path[0], path[1], out intersects);
                return ToPolygon().IsOutside(Player.Instance.Position.To2D()) && !intersects;
            }

            var myPositionWhenExplodes = path.PositionAfter(timeToExplode, speed, delay + timeOffset);

            bool b1 = IsSafe(myPositionWhenExplodes);
            return b1;
        }
    }
}