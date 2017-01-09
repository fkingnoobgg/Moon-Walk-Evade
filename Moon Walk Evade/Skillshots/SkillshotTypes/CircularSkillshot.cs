using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using Moon_Walk_Evade.Evading;
using Moon_Walk_Evade.Utils;
using SharpDX;
using Color = System.Drawing.Color;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    public class CircularSkillshot : EvadeSkillshot
    {
        public CircularSkillshot()
        {
            Caster = null;
            SpawnObject = null;
            SData = null;
            OwnSpellData = null;
            Team = GameObjectTeam.Unknown;
            IsValid = true;
            TimeDetected = Environment.TickCount;
        }

        public Vector3 FixedStartPosition { get; set; }

        public virtual Vector3 FixedEndPosition { get; set; }

        public MissileClient Missile => SpawnObject as MissileClient;

        private bool _missileDeleted;


        public override Vector3 GetCurrentPosition()
        {
            return FixedEndPosition;
        }

        /// <summary>
        /// Creates an existing Class Object unlike the DataBase contains
        /// </summary>
        /// <returns></returns>
        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new CircularSkillshot { OwnSpellData = OwnSpellData };
            if (debug)
            {
                bool isProjectile = EvadeMenu.DebugMenu["isProjectile"].Cast<CheckBox>().CurrentValue;
                var newDebugInst = new CircularSkillshot
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

        public override void OnCreateUnsafe(GameObject obj)
        {
            if (Missile == null && CastArgs != null)
            {
                FixedEndPosition = CastArgs.End;
            }
            else if (Missile != null)
            {
                FixedEndPosition = Missile.EndPosition;
            }
        }

        public override void OnCreateObject(GameObject obj)
        {
            var missile = obj as MissileClient;

            if (SpawnObject == null && missile != null)
            {
                if (missile.SData.Name == OwnSpellData.ObjectCreationName && missile.SpellCaster.Index == Caster.Index)
                {
                    // Force skillshot to be removed
                    IsValid = false;
                }
            }
        }

        public override bool OnDeleteMissile(GameObject obj)
        {
            int delaytime = TimeDetected + OwnSpellData.Delay + OwnSpellData.ExtraExistingTime - Environment.TickCount;
            if (Missile != null && obj.Index == Missile.Index && !string.IsNullOrEmpty(OwnSpellData.ToggleParticleName))
            {
                if (OwnSpellData.ExtraExistingTime == 0)
                    _missileDeleted = true;
                else Core.DelayAction(() => _missileDeleted = true, delaytime);
                return false;
            }

            if (OwnSpellData.ExtraExistingTime == 0)
                return true;
            else Core.DelayAction(() => IsValid = false, delaytime);

            return false;
        }

        public override void OnDeleteObject(GameObject obj)
        {
            if (Missile != null && _missileDeleted && !string.IsNullOrEmpty(OwnSpellData.ToggleParticleName))
            {
                var r = new Regex(OwnSpellData.ToggleParticleName);
                if (r.Match(obj.Name).Success && obj.Distance(FixedEndPosition, true) <= 100 * 100)
                {
                    IsValid = false;
                }
            }
        }

        /// <summary>
        /// check if still valid
        /// </summary>
        public override void OnTick()
        {
            if (Missile == null)
            {
                if (Environment.TickCount > TimeDetected + OwnSpellData.Delay + 250 + OwnSpellData.ExtraExistingTime)
                    IsValid = false;
            }
            else if (Missile != null)
            {
                if (Environment.TickCount > TimeDetected + 6000)
                    IsValid = false;
            }
        }

        public override void OnDraw()
        {
            if (!IsValid)
            {
                return;
            }

            //if (Missile != null && !_missileDeleted && OwnSpellData.ChampionName == "Lux")
            //    new Geometry.Polygon.Circle(FixedEndPosition,
            //        FixedStartPosition.To2D().Distance(Missile.Position.To2D()) / (FixedStartPosition.To2D().Distance(FixedEndPosition.To2D())) * OwnSpellData.Radius).DrawPolygon(
            //            Color.DodgerBlue);


            float radius = OwnSpellData.Radius;

            new Circle(new ColorBGRA(), radius, 3) { Color = Color.White }.Draw(FixedEndPosition);
            bool fancy = (MoonWalkEvade.DrawingType)EvadeMenu.DrawMenu["drawType"].Cast<Slider>().CurrentValue == MoonWalkEvade.DrawingType.Fancy;
            if (Environment.TickCount < TimeDetected + OwnSpellData.Delay + OwnSpellData.ExtraExistingTime && fancy)
            {
                float dt = Environment.TickCount - TimeDetected;
                radius *= dt / (OwnSpellData.Delay + OwnSpellData.ExtraExistingTime);
                new Circle(new ColorBGRA(), radius, 2) { Color = Color.CornflowerBlue }.Draw(FixedEndPosition);
            }
        }

        public override Geometry.Polygon ToPolygon()
        {
            return new Geometry.Polygon.Circle(FixedEndPosition, OwnSpellData.Radius);
        }

        private Geometry.Polygon ToDetailedPolygon()
        {
            Geometry.Polygon poly = new Geometry.Polygon();
            for (int i = 0; i < 360; i += 10)
            {
                /*bounding radius not included*/
                poly.Points.Add(PointOnCircle(OwnSpellData.Radius + Player.Instance.BoundingRadius * 2, i, FixedEndPosition.To2D()));
            }
            return poly;
        }

        Vector2 PointOnCircle(float radius, float angleInDegrees, Vector2 origin)
        {
            float x = origin.X + (float)(radius * System.Math.Cos(angleInDegrees * Math.PI / 180));
            float y = origin.Y + (float)(radius * System.Math.Sin(angleInDegrees * Math.PI / 180));

            return new Vector2(x, y);
        }

        public override int GetAvailableTime(Vector2 pos)
        {
            if (Missile == null)
            {
                return Math.Max(0, OwnSpellData.Delay - (Environment.TickCount - TimeDetected));
            }

            if (!_missileDeleted)
            {
                return (int)(Missile.Position.To2D().Distance(FixedEndPosition.To2D()) / OwnSpellData.MissileSpeed * 1000);
            }

            return -1;
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
            return FixedEndPosition.To2D();
        }

        public override bool IsSafePath(Vector2[] path, int timeOffset = 0, int speed = -1, int delay = 0)
        {
            if (path.Length <= 1) //lastissue = playerpos
            {
                if (!Player.Instance.IsRecalling())
                    return IsSafe();

                if (IsSafe())
                    return true;

                float timeLeft = (Player.Instance.GetBuff("recall").EndTime - Game.Time) * 1000;
                return GetAvailableTime(Player.Instance.Position.To2D()) > timeLeft;
            }

            timeOffset += Game.Ping;

            speed = speed == -1 ? (int)ObjectManager.Player.MoveSpeed : speed;

            var timeToExplode = TimeDetected + OwnSpellData.Delay - Environment.TickCount;
            if (timeToExplode <= 0)
            {
                bool noIntersections = MyUtils.GetLineCircleIntersectionPoints(FixedEndPosition.To2D(), OwnSpellData.Radius, path[0],
                        path[1]).Length == 0;
                return ToPolygon().IsOutside(Player.Instance.Position.To2D()) && noIntersections;
            }

            var myPositionWhenExplodesWithOffset = path.PositionAfter(timeToExplode, speed, delay + timeOffset);
            return IsSafe(myPositionWhenExplodesWithOffset);
        }
    }
}