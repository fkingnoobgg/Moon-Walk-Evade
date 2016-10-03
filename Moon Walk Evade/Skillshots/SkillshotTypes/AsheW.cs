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
                bool debugMode = EvadeMenu.HotkeysMenu["debugMode"].Cast<KeyBind>().CurrentValue;

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

                bool debugMode = EvadeMenu.HotkeysMenu["debugMode"].Cast<KeyBind>().CurrentValue;
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

        public override Geometry.Polygon ToRealPolygon()
        {
            return ToPolygon();
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
            float segmentAngleStep = 4.62f;
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

        public override Geometry.Polygon ToPolygon(float extrawidth = 0)
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
            //return Math.Max(0, OwnSpellData.Delay - (Environment.TickCount - TimeDetected));
            var dist1 =
                Math.Abs((FixedEndPosition.Y - CurrentPosition.Y) * pos.X - (FixedEndPosition.X - CurrentPosition.X) * pos.Y +
                         FixedEndPosition.X * CurrentPosition.Y - FixedEndPosition.Y * CurrentPosition.X) / CurrentPosition.Distance(FixedEndPosition);

            var actualDist = Math.Sqrt(CurrentPosition.Distance(pos).Pow() - dist1.Pow());

            var time = OwnSpellData.MissileSpeed > 0 ? (int)(actualDist / OwnSpellData.MissileSpeed * 1000) : 0;

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
            if (Missile == null)
                return _FixedStartPos.To2D();//Missile not even created

            float dist = OwnSpellData.MissileSpeed / 1000f * extraTime;
            return CurrentPosition.Extend(Missile.EndPosition, dist);
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
                        var enterIntersectionProjection = enterIntersection.Point.ProjectOn(Missile.StartPosition.To2D(), FixedEndPosition.To2D()).SegmentPoint;

                        //Intersection with no exit point.
                        if (i == allIntersections.Count - 1)
                        {
                            var missilePositionOnIntersection = GetMissilePosition(enterIntersection.Time - timeOffset);
                            return
                                FixedEndPosition.Distance(missilePositionOnIntersection) + 50 <=
                                FixedEndPosition.Distance(enterIntersectionProjection) &&
                                ObjectManager.Player.MoveSpeed < OwnSpellData.MissileSpeed;
                        }


                        var exitIntersection = allIntersections[i + 1];
                        var exitIntersectionProjection = exitIntersection.Point.ProjectOn(Missile.StartPosition.To2D(), FixedEndPosition.To2D()).SegmentPoint;

                        var missilePosOnEnter = GetMissilePosition(enterIntersection.Time - timeOffset);
                        var missilePosOnExit = GetMissilePosition(exitIntersection.Time + timeOffset);

                        //Missile didnt pass.
                        if (missilePosOnEnter.Distance(FixedEndPosition) + 50 > enterIntersectionProjection.Distance(FixedEndPosition))
                        {
                            if (missilePosOnExit.Distance(FixedEndPosition) <= exitIntersectionProjection.Distance(FixedEndPosition))
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
                    var exitIntersectionProjection = exitIntersection.Point.ProjectOn(FixedStartPosition.To2D(), FixedEndPosition.To2D()).SegmentPoint;

                    var missilePosOnExit = GetMissilePosition(exitIntersection.Time + timeOffset);
                    if (missilePosOnExit.Distance(FixedEndPosition) <= exitIntersectionProjection.Distance(FixedEndPosition))
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