using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using Moon_Walk_Evade.Utils;
using SharpDX;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    class LuxR : LinearSkillshot
    {
        public override Geometry.Polygon ToPolygon()
        {
            float extrawidth = 0;
            if (OwnSpellData.AddHitbox)
            {
                extrawidth += Player.Instance.HitBoxRadius();
            }

            return new Geometry.Polygon.Rectangle(Missile?.StartPosition ?? FixedStartPos, Missile?.EndPosition ?? FixedEndPos, OwnSpellData.Radius + extrawidth);
        }

        public override bool IsSafe(Vector2? p = null)
        {
            return ToPolygon().IsOutside(p ?? Player.Instance.Position.To2D());
        }

        public override bool IsSafePath(Vector2[] path, int timeOffset = 0, int speed = -1, int delay = 0)
        {
            if (path.Length <= 1) //lastissue = playerpos
                return IsSafe();

            //timeOffset -= 40;

            speed = speed == -1 ? (int)ObjectManager.Player.MoveSpeed : speed;

            var allIntersections = new List<FoundIntersection>();
            var segmentIntersections = new List<FoundIntersection>();
            var polygon = ToPolygon();

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

            //No Missile
            if (allIntersections.Count == 0)
            {
                return IsSafe();
            }
            var timeToExplode = OwnSpellData.Delay + (Environment.TickCount - TimeDetected);

            var myPositionWhenExplodes = path.PositionAfter(timeToExplode, speed, delay + timeOffset);

            return IsSafe(myPositionWhenExplodes);
        }
    }
}
