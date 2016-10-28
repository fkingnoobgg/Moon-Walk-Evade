using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using SharpDX;
using Color = System.Drawing.Color;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    class MultiCircleSkillshot : EvadeSkillshot
    {
        public MultiCircleSkillshot()
        {
            Caster = null;
            SpawnObject = null;
            SData = null;
            OwnSpellData = null;
            Team = GameObjectTeam.Unknown;
            IsValid = true;
            TimeDetected = Environment.TickCount;
        }

        private float distance { get; set; }

        public Vector3 StartPosition { get; private set; }
        public Vector3 EndPosition { get; private set; }

        public Vector2 Direction { get; private set; }

        public MissileClient Missile => SpawnObject as MissileClient;

        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new MultiCircleSkillshot { OwnSpellData = OwnSpellData };
            return newInstance;
        }

        public override void OnCreateUnsafe(GameObject obj)
        {
            if (Missile == null)
            {
                StartPosition = Caster.Position;
                EndPosition = CastArgs.End.Distance(StartPosition) < 700
                    ? StartPosition.Extend(CastArgs.End, 700).To3D()
                    : EndPosition;
                distance = CastArgs.End.Distance(Caster.Position);
                Direction = (CastArgs.End.To2D() - Caster.Position.To2D()).Normalized();

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
                    //IsValid = false;
                }
            }
        }

        public override bool OnDeleteMissile(GameObject obj)
        {
            return false;
        }

        public override void OnDeleteObject(GameObject obj)
        {

        }

        public override Vector3 GetCurrentPosition()
        {
            return EndPosition;
        }

        public override void OnTick()
        {
            if (Environment.TickCount > TimeDetected + OwnSpellData.Delay + 5500)
                IsValid = false;
        }

        public override void OnDraw()
        {
            if (!IsValid)
            {
                return;
            }

            if (!EvadeMenu.DrawMenu["drawDangerPolygon"].Cast<CheckBox>().CurrentValue)
                ToPolygon().Draw(Color.White);
        }

        public override Geometry.Polygon ToPolygon()
        {
            var endPolygon = new Geometry.Polygon();
            List<Geometry.Polygon.Circle> circles = new List<Geometry.Polygon.Circle>(); 
            for (int i = -30; i <= 30; i+=10)
            {
                var rotatedDirection = Direction.Rotated(i*(float) Math.PI/180);
                var c = new Geometry.Polygon.Circle(StartPosition + rotatedDirection.To3D()*distance, OwnSpellData.Radius);
                circles.Add(c);
            }

            //var circlePointsList = circles.Select(x => x.Points.Where(circlePoint =>
            //     circles.Where(otherCircle => otherCircle != x).All(y => !y.IsInside(circlePoint))
            //)).ToList();
            var circlePointsList = circles.Select(x => x.Points);

            foreach (var circlePoints in circlePointsList)
            {
                foreach (var p in circlePoints)
                {
                    endPolygon.Add(p);
                }
            }

            return endPolygon;
        }

        Vector2 PointOnCircle(float radius, float angleInDegrees, Vector2 origin)
        {
            float x = origin.X + (float)(radius * System.Math.Cos(angleInDegrees * Math.PI / 180));
            float y = origin.Y + (float)(radius * System.Math.Sin(angleInDegrees * Math.PI / 180));

            return new Vector2(x, y);
        }

        Geometry.Polygon ToExactCircle(float radius, Vector2 origin)
        {
            Geometry.Polygon poly = new Geometry.Polygon();
            for (int i = 0; i < 360; i += 30)
            {
                poly.Points.Add(PointOnCircle(radius, i, origin));
            }
            return poly;
        }

        public override Geometry.Polygon ToExactPolygon(float extrawidth = 0)
        {
            var endPolygon = new Geometry.Polygon();
            List<Geometry.Polygon> circles = new List<Geometry.Polygon>();
            for (int i = -30; i <= 30; i += 10)
            {
                var rotatedDirection = Direction.Rotated(i * (float)Math.PI / 180);
                var c = ToExactCircle(OwnSpellData.Radius + extrawidth, StartPosition.To2D() + rotatedDirection * distance);
                circles.Add(c);
            }

            //var circlePointsList = circles.Select(x => x.Points.Where(circlePoint =>
            //     circles.Where(otherCircle => otherCircle != x).All(y => !y.IsInside(circlePoint))
            //)).ToList();
            var circlePointsList = circles.Select(x => x.Points);

            foreach (var circlePoints in circlePointsList)
            {
                foreach (var p in circlePoints)
                {
                    endPolygon.Add(p);
                }
            }

            return endPolygon;
        }

        public override int GetAvailableTime(Vector2 pos)
        {
            return OwnSpellData.Delay - (Environment.TickCount - TimeDetected);
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

            var timeToExplode = Environment.TickCount - TimeDetected + OwnSpellData.Delay;

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
