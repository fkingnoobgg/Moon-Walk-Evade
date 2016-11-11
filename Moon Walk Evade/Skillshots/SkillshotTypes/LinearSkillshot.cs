using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public override Vector3 FixedStartPosition { get; set; }
        public override Vector3 FixedEndPosition { get; set; }
        private bool DoesCollide;
        private Vector2 LastCollisionPos;

        public MissileClient Missile => OwnSpellData.IsPerpendicular ? null : SpawnObject as MissileClient;

        bool debugMode => EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;
        public virtual Vector3 CurrentPosition
        {
            get
            {
                
                if (Missile == null)
                {
                    if (debugMode)
                        return Debug.GlobalStartPos;

                    return FixedStartPosition;
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

        public virtual Vector3 EndPosition
        {
            get
            {
                if (debugMode)
                    return Debug.GlobalEndPos;

                if (Missile == null)
                {
                    return FixedEndPosition;
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
                    FixedStartPosition = Debug.GlobalStartPos,
                    FixedEndPosition = Debug.GlobalEndPos,
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

            if (SpawnObject == null && missile != null && !debugMode)
            {
                if (missile.SData.Name == OwnSpellData.ObjectCreationName && missile.SpellCaster.Index == Caster.Index)
                {
                    IsValid = false;
                }
            }

            if (missile != null) //missle
            {
                FixedEndPosition = missile.EndPosition;
                FixedStartPosition = missile.StartPosition;

                Vector2 collision = this.GetCollisionPoint();
                DoesCollide = !collision.IsZero;
                var projection = collision.ProjectOn(missile.StartPosition.To2D(), missile.EndPosition.To2D());
                if (projection.IsOnSegment)
                    LastCollisionPos = projection.SegmentPoint;
            }
        }

        public override void OnSpellDetection(Obj_AI_Base sender)
        {
            if (!OwnSpellData.IsPerpendicular)
            {
                FixedStartPosition = Caster.ServerPosition;
                FixedEndPosition = FixedStartPosition.ExtendVector3(CastArgs.End, OwnSpellData.Range + 100);
            }
            else
            {
                OwnSpellData.Direction = (CastArgs.End - CastArgs.Start).To2D().Normalized();

                var direction = OwnSpellData.Direction;
                FixedStartPosition = (CastArgs.End.To2D() - direction.Perpendicular() * OwnSpellData.SecondaryRadius).To3D();

                FixedEndPosition = (CastArgs.End.To2D() + direction.Perpendicular() * OwnSpellData.SecondaryRadius).To3D();
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

            Utils.Utils.Draw3DRect(CurrentPosition, EndPosition, OwnSpellData.Radius * 2, Color.White, 3);
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

            var dest = proj.SegmentPoint;
            var InsidePath = Player.Instance.GetPath(dest.To3D(), true).Where(segment => ToPolygon().IsInside(segment));
            var point = InsidePath.OrderBy(x => x.Distance(CurrentPosition)).FirstOrDefault();

            if (point == default(Vector3))
                return short.MaxValue;

            float skillDist = point.Distance(CurrentPosition);
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
                return FixedStartPosition.To2D();//Missile not even created

            float dist = OwnSpellData.MissileSpeed / 1000f * extraTime;
            if (dist > CurrentPosition.Distance(EndPosition))
                dist = CurrentPosition.Distance(EndPosition);


            return CurrentPosition.Extend(EndPosition, dist);
        }
    }
}