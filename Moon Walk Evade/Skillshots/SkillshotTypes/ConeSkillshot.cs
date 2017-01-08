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

        public Vector3 FixedStartPosition
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

        public Vector3 CurrentPosition
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

        public Vector3 FixedEndPosition
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
            return CurrentPosition;
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

        public Geometry.Polygon ToSimplePolygon()
        {
            var poly = new Geometry.Polygon();
            poly.Add(RotateAroundPoint(FixedStartPosition.To2D(), FixedEndPosition.To2D(), -OwnSpellData.ConeAngle/2f * (float)Math.PI / 180));
            poly.Add(RotateAroundPoint(FixedStartPosition.To2D(), FixedEndPosition.To2D(), +OwnSpellData.ConeAngle / 2f * (float)Math.PI / 180));
            poly.Add(FixedStartPosition);
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

            Vector2 direction = (FixedEndPosition - FixedStartPosition).To2D();
            var perpVecStart = CurrentPosition.To2D() + direction.Normalized().Perpendicular();
            var perpVecEnd = CurrentPosition.To2D() + direction.Normalized().Perpendicular() * 1500;

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
                return new[] { p1, p2 };


            return new[] { CurrentPosition.To2D(),  CurrentPosition.To2D() };
        }

        public override Geometry.Polygon ToPolygon()
        {
            List<Vector2> coneSegemnts = new List<Vector2>();
            for (float i = -OwnSpellData.ConeAngle / 2f; i <= OwnSpellData.ConeAngle / 2f; i++)
            {
                coneSegemnts.Add(RotateAroundPoint(FixedStartPosition.To2D(), FixedEndPosition.To2D(), i * (float)Math.PI / 180));
            }

            if (Missile != null)
            {
                var beginPoints = GetBeginEdgePoints(new[] { coneSegemnts.First(), coneSegemnts.Last() });
                coneSegemnts.Insert(0, beginPoints[0]);
                coneSegemnts.Insert(0, beginPoints[1]);
            }
            else
                coneSegemnts.Insert(0, FixedStartPosition.To2D());

            Geometry.Polygon polygon = new Geometry.Polygon();
            polygon.Points.AddRange(coneSegemnts);

            return polygon;
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

            //var dest = proj.SegmentPoint;
            //var InsidePath = Player.Instance.GetPath(dest.To3D(), true).Where(segment => ToPolygon().IsInside(segment));
            //var point = InsidePath.OrderBy(x => x.Distance(CurrentPosition)).FirstOrDefault();

            //if (point == default(Vector3))
            //    return short.MaxValue;

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

        public override Vector2 GetMissilePosition(int extraTime)
        {
            return FixedEndPosition.To2D();
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
