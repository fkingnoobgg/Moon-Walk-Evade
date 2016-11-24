using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using Moon_Walk_Evade.Utils;
using SharpDX;
using Color = System.Drawing.Color;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    class VeigarE : EvadeSkillshot
    {
        public VeigarE()
        {
            Caster = null;
            SpawnObject = null;
            SData = null;
            OwnSpellData = null;
            Team = GameObjectTeam.Unknown;
            IsValid = true;
            TimeDetected = Environment.TickCount;
        }

        public Vector3 StartPosition { get; set; }

        public Vector3 EndPosition { get; set; }

        public MissileClient Missile => SpawnObject as MissileClient;

        private bool _missileDeleted;


        public override Vector3 GetCurrentPosition()
        {
            return EndPosition;
        }

        /// <summary>
        /// Creates an existing Class Object unlike the DataBase contains
        /// </summary>
        /// <returns></returns>
        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new VeigarE { OwnSpellData = OwnSpellData };
            if (debug)
            {
                bool isProjectile = EvadeMenu.DebugMenu["isProjectile"].Cast<CheckBox>().CurrentValue;
                var newDebugInst = new VeigarE
                {
                    OwnSpellData = OwnSpellData,
                    StartPosition = Debug.GlobalStartPos,
                    EndPosition = Debug.GlobalEndPos,
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
            EndPosition = Missile?.EndPosition ?? CastArgs.End;
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
            if (Missile != null && obj.Index == Missile.Index && !string.IsNullOrEmpty(OwnSpellData.ToggleParticleName))
            {
                _missileDeleted = true;
                return false;
            }

            return true;
        }

        public override void OnDeleteObject(GameObject obj)
        {
            if (Missile != null && _missileDeleted && !string.IsNullOrEmpty(OwnSpellData.ToggleParticleName))
            {
                var r = new Regex(OwnSpellData.ToggleParticleName);
                if (r.Match(obj.Name).Success && obj.Distance(EndPosition, true) <= 100 * 100)
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
                if (Environment.TickCount > TimeDetected + OwnSpellData.Delay + 3000)
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

            if (Missile != null && !_missileDeleted)
            {
                new Geometry.Polygon.Circle(EndPosition,
                    StartPosition.To2D().Distance(Missile.Position.To2D()) / (StartPosition.To2D().Distance(EndPosition.To2D())) * OwnSpellData.Radius).DrawPolygon(
                        Color.DodgerBlue);
            }

            ToPolygon().Draw(Color.White);
        }

        public override Geometry.Polygon ToInnerPolygon()
        {
            float extrawidth = -20;
            return new Geometry.Polygon.Circle(EndPosition, OwnSpellData.RingRadius + extrawidth);
        }

        public override Geometry.Polygon ToOuterPolygon()
        {
            float extrawidth = 20;
            return new Geometry.Polygon.Circle(EndPosition, OwnSpellData.RingRadius + OwnSpellData.Radius + extrawidth);
        }

        Vector2 PointOnCircle(float radius, float angleInDegrees, Vector2 origin)
        {
            float x = origin.X + (float)(radius * Math.Cos(angleInDegrees * Math.PI / 180));
            float y = origin.Y + (float)(radius * Math.Sin(angleInDegrees * Math.PI / 180));

            return new Vector2(x, y);
        }

        public override Geometry.Polygon ToPolygon()
        {
            float extrawidth = -20;

            List<Vector2> points = new List<Vector2>();
            int i = 0;
            for (; i <= 360; i += 20)
            {
                points.Add(PointOnCircle(OwnSpellData.RingRadius + extrawidth, i, EndPosition.To2D()));
            }

            extrawidth = 20;
            i -= 20;
            for (; i >= 0; i -= 20)
            {
                points.Add(PointOnCircle(OwnSpellData.RingRadius + OwnSpellData.Radius + extrawidth, i, EndPosition.To2D()));
            }
            var poly = new Geometry.Polygon();
            poly.Points.AddRange(points);

            return poly;
        }

        public override int GetAvailableTime(Vector2 pos)
        {
            return Math.Max(0, OwnSpellData.Delay - (Environment.TickCount - TimeDetected));
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
            return EndPosition.To2D();
        }

        public override bool IsSafePath(Vector2[] path, int timeOffset = 0, int speed = -1, int delay = 0)
        {
            if (path.Length <= 1) //lastissue = playerpos
            {
                if (!Player.Instance.IsRecalling())
                    return IsSafe();

                float timeLeft = (Player.Instance.GetBuff("recall").EndTime - Game.Time) * 1000;
                return GetAvailableTime(Player.Instance.Position.To2D()) > timeLeft;
            }

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
