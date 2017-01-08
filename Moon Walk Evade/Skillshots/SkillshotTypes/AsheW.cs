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
    public class AsheW : EvadeSkillshot
    {
        public AsheW()
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
        private bool CollisionChecked;
        private Vector2[] CollisionPoints;

        public MissileClient Missile => OwnSpellData.IsPerpendicular ? null : SpawnObject as MissileClient;

        public Vector3 FixedStartPosition
        {
            get
            {
                bool debugMode = EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;

                if (debugMode)
                    return Debug.GlobalStartPos;
                return _FixedStartPos;
            }
        }

        public Vector3 CurrentPosition
        {
            get
            {
                float speed = OwnSpellData.MissileSpeed;
                float timeElapsed = Environment.TickCount - TimeDetected - OwnSpellData.Delay;
                float traveledDist = speed * timeElapsed / 1000;
                return _FixedStartPos.Extend(_FixedEndPos, traveledDist).To3D();
            }
        }

        public Vector3 FixedEndPosition
        {
            get
            {

                bool debugMode = EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue;
                if (debugMode)
                    return Debug.GlobalEndPos;

                return _FixedEndPos;
            }
        }

        public override Vector3 GetCurrentPosition()
        {
            return CurrentPosition;
        }

        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new AsheW { OwnSpellData = OwnSpellData };
            if (debug)
            {
                var newDebugInst = new AsheW
                {
                    OwnSpellData = OwnSpellData,
                    _FixedStartPos = Debug.GlobalStartPos,
                    _FixedEndPos = Debug.GlobalEndPos,
                    IsValid = true,
                    IsActive = true,
                    TimeDetected = Environment.TickCount,
                    SpawnObject = null
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
            if (Environment.TickCount > TimeDetected + OwnSpellData.Delay + 1000)
            {
                IsValid = false;
                return;
            }

            if (!CollisionChecked)
            {
                CollisionPoints = this.GetCollisionPoints();
                CollisionChecked = true;
            }
        }

        public Geometry.Polygon ToSimplePolygon()
        {
            var poly = new Geometry.Polygon();
            poly.Add(FixedStartPosition);
            poly.Points.AddRange(GetEdgePoints());
            return poly;
        }

        public override void OnDraw()
        {
            if (!IsValid)
            {
                return;
            }

            if ((MoonWalkEvade.DrawingType)EvadeMenu.DrawMenu["drawType"].Cast<Slider>().CurrentValue == MoonWalkEvade.DrawingType.Fast)
            {
                ToSimplePolygon().Draw(Color.White);
            }
            else ToPolygon().Draw(Color.White, 3);
        }

        Vector2[] GetBeginEdgePoints(Vector2[] edges)
        {
            if (FixedStartPosition.Distance(CurrentPosition) <= 50)
                return new [] {CurrentPosition.To2D()};

            var endEdges = edges;

            Vector2 direction = (FixedEndPosition - FixedStartPosition).To2D();
            var perpVecStart = CurrentPosition.To2D() + direction.Normalized().Perpendicular();
            var perpVecEnd = CurrentPosition.To2D() + direction.Normalized().Perpendicular()*1500;

            //right side is not the same?
            var perpVecStart2 = CurrentPosition.To2D() + direction.Normalized().Perpendicular2();
            var perpVecEnd2 = CurrentPosition.To2D() + direction.Normalized().Perpendicular2() * 1500;


            Geometry.Polygon.Line leftEdgeLine = new Geometry.Polygon.Line(FixedStartPosition.To2D(), endEdges[1]);
            Geometry.Polygon.Line rightEdgeLine = new Geometry.Polygon.Line(FixedStartPosition.To2D(), endEdges[0]);

            var inters = leftEdgeLine.GetIntersectionPointsWithLineSegment(perpVecStart, perpVecEnd);
            var inters2 = rightEdgeLine.GetIntersectionPointsWithLineSegment(perpVecStart2, perpVecEnd2);
            Vector2 p1 = Vector2.Zero, p2 = Vector2.Zero;



            if (inters.Any())
            {
                var closestInter = inters.OrderBy(x => x.Distance(CurrentPosition)).First();
                p2 = closestInter;
            }
            if (inters2.Any())
            {
                var closestInter = inters2.OrderBy(x => x.Distance(CurrentPosition)).First();
                p1 = closestInter;
            }

            if (!p1.IsZero && !p2.IsZero)
                return new[] {p1, p2};


            return new[] { CurrentPosition.To2D() };
        }

        Vector2 RotateAroundPoint(Vector2 start, Vector2 end, float theta)
        {
            float px = end.X, py = end.Y;
            float ox = start.X, oy = start.Y;

            float x = (float)Math.Cos(theta) * (px - ox) - (float)Math.Sin(theta) * (py - oy) + ox;
            float y = (float)Math.Sin(theta) * (px - ox) + (float)Math.Cos(theta) * (py - oy) + oy;
            return new Vector2(x, y);
        }

        public Vector2[] GetEdgePoints()
        {
            float segmentAngleStep = 4.8f;
            float sidewardsRotationAngle = segmentAngleStep * 5;

            Vector2 rightEdge = RotateAroundPoint(FixedStartPosition.To2D(), FixedEndPosition.To2D(), 
                -sidewardsRotationAngle * (float)Math.PI/180);
            Vector2 leftEdge = RotateAroundPoint(FixedStartPosition.To2D(), FixedEndPosition.To2D(), sidewardsRotationAngle *
                (float)Math.PI / 180);

            return new[] {rightEdge, leftEdge};
        }

        IEnumerable<Vector2> OrderCollisionPointsHorizontally(Vector2 rightEdgePoint)
        {
            if (!CollisionPoints.Any())
                return CollisionPoints;

            List<Vector2> detailedEdgeLine = new List<Vector2>();
            for (float i = 1; i >= 0; i-=.1f)
            {
                detailedEdgeLine.Add(FixedStartPosition.To2D() + (rightEdgePoint - FixedStartPosition.To2D())*i);
            }

            return CollisionPoints.OrderBy(cp => detailedEdgeLine.OrderBy(p => p.Distance(cp)).First().Distance(cp));
        }

        class CollisionInfo
        {
            public bool BehindStartLine;
            public Vector2 New_RightCollPointOnStartLine, New_LeftCollPointOnStartLine;
        }

        CollisionInfo AreCollisionPointsBehindBegin(Vector2 rightCollP, Vector2 leftCollP, Vector2 rightBeginP, Vector2 leftBeginP)
        {
            var btweenCollP = rightCollP + (leftCollP - rightCollP)*.5f;
            var extendedRight = FixedStartPosition.Extend(rightCollP, 2000);
            var extendedLeft = FixedStartPosition.Extend(leftCollP, 2000);

            var intersectionsRight = new Geometry.Polygon.Line(FixedStartPosition.To2D(), extendedRight).
                GetIntersectionPointsWithLineSegment(rightBeginP, leftBeginP);
            var intersectionsleft = new Geometry.Polygon.Line(FixedStartPosition.To2D(), extendedLeft).
                GetIntersectionPointsWithLineSegment(rightBeginP, leftBeginP);

            bool behind =
                !new Geometry.Polygon.Line(rightBeginP, leftBeginP).IsIntersectingWithLineSegment(
                    FixedStartPosition.To2D(),
                    btweenCollP);

            CollisionInfo info = new CollisionInfo {BehindStartLine = behind};

            if (intersectionsRight.Any() && intersectionsleft.Any())
            {
                info.New_RightCollPointOnStartLine = intersectionsRight[0];
                info.New_LeftCollPointOnStartLine = intersectionsleft[0];
            }


            return info;
        }

        public override Geometry.Polygon ToPolygon()
        {
            Vector2[] edges = GetEdgePoints();
            Vector2 rightEdge = edges[0];
            Vector2 leftEdge = edges[1];

            var beginPoints = GetBeginEdgePoints(edges);
            Vector2 rightBeginPoint = beginPoints[0];
            Vector2 leftBeginPoint = beginPoints.Length == 1 ? Vector2.Zero : beginPoints[1];

            if (leftBeginPoint.IsZero)
                return new Geometry.Polygon();

            var baseTriangle = new Geometry.Polygon();
            baseTriangle.Points.AddRange(new List<Vector2> { FixedStartPosition.To2D(), rightEdge, leftEdge });

            var advancedTriangle = new Geometry.Polygon();
            advancedTriangle.Points.AddRange(new List<Vector2> { FixedStartPosition.To2D(), rightEdge });

            var dummyTriangle = advancedTriangle;

            if (CollisionPoints.Any())
            {
                foreach (var collisionPoint in OrderCollisionPointsHorizontally(rightEdge))
                {
                    var dir = collisionPoint - FixedStartPosition.To2D();
                    var leftColl = FixedStartPosition.To2D() + dir + dir.Perpendicular().Normalized() * 25;
                    var rightColl = FixedStartPosition.To2D() + dir + dir.Perpendicular2().Normalized() * 25;

                    var backToLineRight = FixedStartPosition.Extend(rightColl, FixedEndPosition.Distance(FixedStartPosition));
                    var backToLineLeft = FixedStartPosition.Extend(leftColl, FixedEndPosition.Distance(FixedStartPosition));

                    var earlyCollCheck_Left = backToLineLeft.Extend(leftColl, FixedEndPosition.Distance(FixedStartPosition));
                    var earlyCollCheck_Right = backToLineRight.Extend(rightColl, FixedEndPosition.Distance(FixedStartPosition));

                    Geometry.Polygon earlyCollisionRectangle = new Geometry.Polygon();
                    earlyCollisionRectangle.Points.AddRange(new List<Vector2>
                    {
                        leftColl, earlyCollCheck_Left, earlyCollCheck_Right, rightColl
                    });
                    bool EarlyCollision =
                        CollisionPoints.Any(x => x != collisionPoint && earlyCollisionRectangle.IsInside(x));

                    Func<Vector2, bool> outsideDummy = point => dummyTriangle.Points.Count < 3 || dummyTriangle.IsOutside(point);

                    if (baseTriangle.IsInside(rightColl) && baseTriangle.IsInside(leftColl)
                        && outsideDummy(rightColl) && outsideDummy(leftColl) && !EarlyCollision &&
                        backToLineLeft.Distance(backToLineRight) >= OwnSpellData.Radius * 2)
                    {
                        CollisionInfo info = AreCollisionPointsBehindBegin(rightColl, leftColl, rightBeginPoint,
                            leftBeginPoint);

                        if (!info.BehindStartLine)
                        {
                            dummyTriangle.Points.Add(backToLineRight);
                            advancedTriangle.Points.Add(backToLineRight);

                            dummyTriangle.Points.Add(rightColl);
                            advancedTriangle.Points.Add(rightColl);


                            dummyTriangle.Points.Add(leftColl);
                            advancedTriangle.Points.Add(leftColl);

                            dummyTriangle.Points.Add(backToLineLeft);
                            advancedTriangle.Points.Add(backToLineLeft);
                        }
                        else //collision points behind startLine
                        {
                            leftColl = info.New_LeftCollPointOnStartLine;
                            rightColl = info.New_RightCollPointOnStartLine;

                            backToLineRight = FixedStartPosition.Extend(rightColl, FixedEndPosition.Distance(FixedStartPosition));
                            backToLineLeft = FixedStartPosition.Extend(leftColl, FixedEndPosition.Distance(FixedStartPosition));

                            dummyTriangle.Points.Add(backToLineRight);
                            advancedTriangle.Points.Add(backToLineRight);

                            dummyTriangle.Points.Add(rightColl);
                            advancedTriangle.Points.Add(rightColl);


                            dummyTriangle.Points.Add(leftColl);
                            advancedTriangle.Points.Add(leftColl);

                            dummyTriangle.Points.Add(backToLineLeft);
                            advancedTriangle.Points.Add(backToLineLeft);
                        }
                    }

                }
            }

            advancedTriangle.Points.Add(leftEdge);
            advancedTriangle.Points.RemoveAt(0);
            advancedTriangle.Points.Insert(0, rightBeginPoint);
            advancedTriangle.Points.Insert(0, leftBeginPoint);

            return advancedTriangle;
        }

        public override int GetAvailableTime(Vector2 pos)
        {
            if (Missile == null)
            {
                return Math.Max(0, OwnSpellData.Delay - (Environment.TickCount - TimeDetected) - Game.Ping);
            }

            var proj = pos.ProjectOn(CurrentPosition.To2D(), FixedEndPosition.To2D());
            if (!proj.IsOnSegment)
                return short.MaxValue;

            var dest = proj.SegmentPoint;
            var InsidePath = Player.Instance.GetPath(dest.To3D(), true).Where(segment => ToPolygon().IsInside(segment));
            var point = InsidePath.OrderBy(x => x.Distance(CurrentPosition)).FirstOrDefault();

            if (point == default(Vector3))
                return short.MaxValue;

            float skillDist = point.Distance(CurrentPosition);
            return Math.Max(0, (int)(skillDist/OwnSpellData.MissileSpeed*1000));
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
                return _FixedStartPos.To2D();//Missile not even created

            float dist = OwnSpellData.MissileSpeed / 1000f * extraTime;
            return CurrentPosition.Extend(Missile.EndPosition, dist);
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
                Vector3 skillDir = FixedEndPosition - CurrentPosition;

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
                            Math.Pow(h, 2) * j * k - 1000 * a * b * c * d * Math.Pow(h, 2) + 1000 * a * Math.Pow(d, 2) * f * g * h) /
                            (1000 * Math.Pow(d, 2) * Math.Pow(g, 2) * Math.Pow(p, 2) - 2000 * c * d * g * h * n * p +
                            1000 * Math.Pow(c, 2) * Math.Pow(h, 2) * Math.Pow(n, 2) + 1000 * Math.Pow(d, 2) * Math.Pow(g, 2) * Math.Pow(l, 2) -
                            2000 * c * d * g * h * j * l + 1000 * Math.Pow(c, 2) * Math.Pow(h, 2) * Math.Pow(j, 2) + 1000 * Math.Pow(b, 2) *
                            Math.Pow(c, 2) * Math.Pow(h, 2) - 2000 * b * c * d * f * g * h + 1000 * Math.Pow(d, 2) * Math.Pow(f, 2) *
                            Math.Pow(g, 2));

                Vector3 myPosition = path[0].To3D() + (float)t * pathDir * c / pathDir.Length();
                Vector3 misPosition = CurrentPosition + (float)t * skillDir * g / skillDir.Length();

                bool valid = myPosition.Distance(Player.Instance) <= Player.Instance.Distance(path[1]) &&
                    misPosition.Distance(CurrentPosition) <= CurrentPosition.Distance(FixedEndPosition) && t >= 0;

                if (!valid && t >= 0)
                {
                    /*t out of skill range => set t to skillshot maxrange*/
                    if (misPosition.Distance(CurrentPosition) > CurrentPosition.Distance(FixedEndPosition))
                    {
                        t = CurrentPosition.Distance(FixedEndPosition) / OwnSpellData.MissileSpeed + r / 1000;

                        myPosition = path[0].To3D() + (float)t * pathDir * c / pathDir.Length();
                        //misPosition = FixedEndPosition;

                        return ToPolygon().IsOutside(myPosition.To2D());
                    }

                    /*t out of path range*/
                    if (myPosition.Distance(Player.Instance) > Player.Instance.Distance(path[1]))
                    {
                        return ToPolygon().IsOutside(path[1]);
                    }
                }

                //timeNeeded = 1337;
                return !valid || ToPolygon().IsOutside(myPosition.To2D());
            }

            var timeToExplode = TimeDetected + OwnSpellData.Delay - Environment.TickCount;
            if (timeToExplode <= 0)
            {
                //timeNeeded = -9;
                return IsSafe();
            }

            var myPositionWhenExplodes = path.PositionAfter(timeToExplode, speed, delay + timeOffset);

            bool b1 = IsSafe(myPositionWhenExplodes);
            //timeNeeded = b ? -103 : Player.Instance.WalkingTime(allIntersections[0].Point) + timeOffset + delay - timeToExplode;
            //timeNeeded = -12345;

            return b1;
        }
    }
}