using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using Moon_Walk_Evade.EvadeSpells;
using Moon_Walk_Evade.Skillshots;
using Moon_Walk_Evade.Skillshots.SkillshotTypes;
using Moon_Walk_Evade.Utils;
using SharpDX;
using Color = System.Drawing.Color;

namespace Moon_Walk_Evade.Evading
{
    public class MoonWalkEvade
    {
        #region Properties

        public int ServerTimeBuffer
        {
            get { return EvadeMenu.MainMenu["serverTimeBuffer"].Cast<Slider>().CurrentValue; }
        }

        public bool EvadeEnabled
        {
            get { return EvadeMenu.HotkeysMenu["enableEvade"].Cast<KeyBind>().CurrentValue; }
        }

        public bool DodgeDangerousOnly
        {
            get { return EvadeMenu.HotkeysMenu["dodgeOnlyDangerousH"].Cast<KeyBind>().CurrentValue || EvadeMenu.HotkeysMenu["dodgeOnlyDangerousT"].Cast<KeyBind>().CurrentValue; }
        }

        public int ExtraEvadeRange
        {
            get { return EvadeMenu.MainMenu["extraEvadeRange"].Cast<Slider>().CurrentValue; }
        }

        public bool RandomizeExtraEvadeRange
        {
            get { return EvadeMenu.MainMenu["randomizeExtraEvadeRange"].Cast<CheckBox>().CurrentValue; }
        }

        public bool AllowRecalculateEvade
        {
            get { return EvadeMenu.MainMenu["recalculatePosition"].Cast<CheckBox>().CurrentValue; }
        }

        public bool RestorePosition
        {
            get { return EvadeMenu.MainMenu["moveToInitialPosition"].Cast<CheckBox>().CurrentValue; }
        }

        public bool DisableDrawings
        {
            get { return EvadeMenu.DrawMenu["disableAllDrawings"].Cast<CheckBox>().CurrentValue; }
        }

        public bool DrawEvadePoint
        {
            get { return EvadeMenu.DrawMenu["drawEvadePoint"].Cast<CheckBox>().CurrentValue; }
        }

        public bool DrawEvadeStatus
        {
            get { return EvadeMenu.DrawMenu["drawEvadeStatus"].Cast<CheckBox>().CurrentValue; }
        }

        public bool DrawDangerPolygon
        {
            get { return EvadeMenu.DrawMenu["drawDangerPolygon"].Cast<CheckBox>().CurrentValue; }
        }

        public int IssueOrderTickLimit
        {
            get { return 0 /*Game.Ping * 2*/; }
        }

        public enum StutterSearchType
        {
            MousePos, PlayerFaceDirection, FarestAway, 
        }
        public StutterSearchType AntiStutterSearchType => (StutterSearchType)EvadeMenu.MainMenu["stutterPointFindType"].Cast<Slider>().CurrentValue;

        private int _oldMovementDelay = -1;
        public int OldMovementDelay
        {
            get { return _oldMovementDelay; }
            private set
            {
                if (_oldMovementDelay == -1)
                    _oldMovementDelay = value;
            }
        }

        public bool manageOrbwalker => EvadeMenu.HotkeysMenu["manageMovementDeay"].Cast<CheckBox>().CurrentValue;

        #endregion

        #region Vars

        public SpellDetector SkillshotDetector { get; set; }
        public PathFinding PathFinding { get; private set; }

        public EvadeSkillshot[] Skillshots { get; private set; }
        public Geometry.Polygon[] Polygons { get; private set; }
        public List<Geometry.Polygon> ClippedPolygons { get; private set; }
        public Vector2 LastIssueOrderPos;

        private readonly Dictionary<EvadeSkillshot, Geometry.Polygon> _skillshotPolygonCache;

        private EvadeResult CurrentEvadeResult { get; set; }

        private Text StatusText;
        private int EvadeIssurOrderTime;

        #endregion

        public MoonWalkEvade(SpellDetector detector)
        {
            Skillshots = new EvadeSkillshot[] { };
            Polygons = new Geometry.Polygon[] { };
            ClippedPolygons = new List<Geometry.Polygon>();
            PathFinding = new PathFinding(this);
            StatusText = new Text("MoonWalkEvade Enabled", new Font("Euphemia", 10F, FontStyle.Bold)); //Calisto MT
            _skillshotPolygonCache = new Dictionary<EvadeSkillshot, Geometry.Polygon>();

            SkillshotDetector = detector;
            SkillshotDetector.OnUpdateSkillshots += OnUpdateSkillshots;
            SkillshotDetector.OnSkillshotActivation += OnSkillshotActivation;
            SkillshotDetector.OnSkillshotDetected += OnSkillshotDetected;
            SkillshotDetector.OnSkillshotDeleted += OnSkillshotDeleted;

            Player.OnIssueOrder += PlayerOnIssueOrder;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Dash.OnDash += OnDash;
            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
        }

        private void OnUpdateSkillshots(EvadeSkillshot skillshot, bool remove, bool isProcessSpell)
        {
            CacheSkillshots();
        }

        private void OnSkillshotActivation(EvadeSkillshot skillshot)
        {
            CacheSkillshots();
        }

        private void OnSkillshotDetected(EvadeSkillshot skillshot, bool isProcessSpell)
        {
            if (skillshot.ToPolygon().IsInside(Player.Instance))
            {
                CurrentEvadeResult = null;
            }
        }

        private void OnSkillshotDeleted(EvadeSkillshot skillshot)
        {
            if (RestorePosition && !SkillshotDetector.DetectedSkillshots.Any())
            {
                if (AutoPathing.IsPathing && Player.Instance.IsWalking())
                {
                    var destination = AutoPathing.Destination;
                    AutoPathing.StopPath();
                    Player.IssueOrder(GameObjectOrder.MoveTo, destination.To3DWorld(), false);
                }
                else if (CurrentEvadeResult != null && Player.Instance.IsMovingTowards(CurrentEvadeResult.EvadePoint))
                {
                    Player.IssueOrder(GameObjectOrder.MoveTo, LastIssueOrderPos.To3DWorld(), false);
                }
            }
        }

        
        private void OnUpdate(EventArgs args)
        {
            if (manageOrbwalker && Orbwalker.MovementDelay < 225)
            {
                OldMovementDelay = Orbwalker.MovementDelay;
                Orbwalker.MovementDelay = 225;
            }
            else if (!manageOrbwalker && OldMovementDelay != -1)
            {
                Orbwalker.MovementDelay = OldMovementDelay;
                _oldMovementDelay = -1;
            }

            if (!EvadeEnabled || Player.Instance.IsDead || Player.Instance.IsDashing())
            {
                CurrentEvadeResult = null;
                return;
            }

            CacheSkillshots();

            bool goodPath = IsPathSafe(Player.Instance.RealPath());
            if (!IsHeroInDanger() && goodPath && CurrentEvadeResult != null)
            {
                CurrentEvadeResult = null;
                return;
            }
            //if (CurrentEvadeResult != null && Player.Instance.Distance(CurrentEvadeResult.WalkPoint) <= 200)
            //    CurrentEvadeResult = null;

            if (CurrentEvadeResult == null)
                CheckEvade();

            if (CurrentEvadeResult != null) //&& CurrentEvadeResult.EvadePoint.Distance(hero) > hero.HitBoxRadius()
            {
                if (!CurrentEvadeResult.ShouldPreventStuttering)
                    MoveTo(CurrentEvadeResult.WalkPoint, false);
                else
                {
                    Vector2 newPoint = GetEvadePoints(CurrentEvadeResult.WalkPoint.To2D()).FirstOrDefault();
                    if (newPoint != default(Vector2))
                    {
                        CurrentEvadeResult.WalkPoint = newPoint.To3D();
                    }
                }
            }
        }

        private void CheckEvade()
        {
            var hero = Player.Instance;

            if (IsHeroInDanger(hero))
            {
                var evade = CalculateEvade(LastIssueOrderPos);
                if (evade.IsValid && evade.EnoughTime)
                {
                    CurrentEvadeResult = evade;
                }

                return;
            }

            bool goodPath = IsPathSafeEx(LastIssueOrderPos);
            if (!goodPath)
            {
                var evade = CalculateEvade(LastIssueOrderPos);

                if (evade.IsValid)
                {
                    evade.IsOutsideEvade = true;
                    CurrentEvadeResult = evade;
                }

                return;
            }

            CurrentEvadeResult = null;
        }

        private void PlayerOnIssueOrder(Obj_AI_Base sender, PlayerIssueOrderEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }

            if (args.Order == GameObjectOrder.AttackUnit)
            {
                LastIssueOrderPos =
                    (Player.Instance.Distance(args.Target, true) >
                     Player.Instance.GetAutoAttackRange(args.Target as AttackableUnit).Pow()
                        ? args.Target.Position
                        : Player.Instance.Position).To2D();
            }
            else
            {
                LastIssueOrderPos = (args.Target?.Position ?? args.TargetPosition).To2D();
            }

            if (CurrentEvadeResult != null)
            {
                args.Process = false;
                OnUpdate(null);
            }
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }

            if (args.SData.Name == "summonerflash")
            {
                CurrentEvadeResult = null;
            }
        }

        private void OnDash(Obj_AI_Base sender, Dash.DashEventArgs dashEventArgs)
        {
            if (!sender.IsMe || CurrentEvadeResult == null)
            {
                return;
            }

            CurrentEvadeResult = null;
            Player.IssueOrder(GameObjectOrder.MoveTo, LastIssueOrderPos.To3DWorld(), false);
        }

        private void OnDraw(EventArgs args)
        {
            if (DisableDrawings)
            {
                return;
            }

            if (DrawEvadePoint && CurrentEvadeResult != null)
            {
                if (CurrentEvadeResult.IsValid && CurrentEvadeResult.EnoughTime && !CurrentEvadeResult.Expired())
                {
                    Circle.Draw(new ColorBGRA(255, 0, 0, 255), Player.Instance.BoundingRadius, 25, CurrentEvadeResult.WalkPoint);
                }
            }

            if (DrawEvadeStatus)
            {
                StatusText.Color = EvadeEnabled ? Color.White : Color.Red;
                StatusText.TextValue = "MoonWalkEvade " + (EvadeEnabled ? "Enabled" : "Disabled");
                StatusText.Position = Player.Instance.Position.WorldToScreen() - new Vector2(StatusText.Bounding.Width / 2, -25);
                StatusText.Draw();
            }

            if (DrawDangerPolygon)
            {
                foreach (var pol in Geometry.ClipPolygons(SkillshotDetector.ActiveSkillshots.Select(c => c.ToPolygon())).ToPolygons())
                {
                    pol.DrawPolygon(Color.White, 3);
                }
            }

            //Utils.DrawPath(PathFinding.GetPath(Player.Instance.Position.To2D(), Game.CursorPos.To2D()), Color.Blue);
            //Utils.DrawPath(Player.Instance.GetPath(Game.CursorPos, true).ToVector2(), Color.Blue);
        }

        private void CacheSkillshots()
        {
            Skillshots =
                (DodgeDangerousOnly
                    ? SkillshotDetector.ActiveSkillshots.Where(c => c.OwnSpellData.IsDangerous)
                    : SkillshotDetector.ActiveSkillshots).ToArray();

            _skillshotPolygonCache.Clear();

            Polygons = Skillshots.Select(c =>
            {
                var pol = c.ToPolygon();
                _skillshotPolygonCache.Add(c, pol);

                return pol;
            }).ToArray();

            ClippedPolygons = Geometry.ClipPolygons(Polygons).ToPolygons();
        }

        public bool IsPointSafe(Vector2 point)
        {
            return !ClippedPolygons.Any(p => p.IsInside(point));
        }

        public bool IsPathSafe(Vector2[] path)
        {
            return IsPathSafeEx(path);
        }

        public bool IsPathSafe(Vector3[] path)
        {
            return IsPathSafe(path.ToVector2());
        }

        public bool IsHeroInDanger(AIHeroClient hero = null)
        {
            hero = hero ?? Player.Instance;
            return !IsPointSafe(hero.ServerPosition.To2D());
        }

        public int GetTimeAvailable(AIHeroClient hero = null)
        {
            hero = hero ?? Player.Instance;
            var skillshots = Skillshots.Where(c => _skillshotPolygonCache[c].IsInside(hero.Position)).ToArray();

            if (!skillshots.Any())
            {
                return short.MaxValue;
            }

            var times =
                skillshots.Select(c => c.GetAvailableTime(hero.ServerPosition.To2D()))
                    .Where(t => t > 0)
                    .OrderByDescending(t => t);

            return times.Any() ? times.Last() : short.MaxValue;
        }

        public int GetDangerValue(AIHeroClient hero = null)
        {
            hero = hero ?? Player.Instance;
            var skillshots = Skillshots.Where(c => _skillshotPolygonCache[c].IsInside(hero.Position)).ToArray();

            if (!skillshots.Any())
                return 0;

            var values = skillshots.Select(c => c.OwnSpellData.DangerValue).OrderByDescending(t => t);
            return values.Any() ? values.First() : 0;
        }

        public bool IsPathSafeEx(Vector2[] path, AIHeroClient hero = null)
        {
            return Skillshots.All(evadeSkillshot => evadeSkillshot.IsSafePath(path));
        }

        public bool IsPathSafeEx(Vector2 end, AIHeroClient hero = null)
        {
            hero = hero ?? Player.Instance;

            return IsPathSafeEx(hero.GetPath(end.To3DWorld(), true).ToVector2(), hero);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="awayFrom"></param>
        /// <param name="managed">Check for path safety</param>
        /// <returns></returns>
        public Vector2[] GetEvadePoints(Vector2? awayFrom = null, bool managed = true)
        {
            int posChecked = 0;
            int maxPosToCheck = 50;
            int posRadius = 50;
            int radiusIndex = 0;
            var heroPoint = Player.Instance.Position;

            var points = new List<Vector2>();

            while (posChecked < maxPosToCheck)
            {
                radiusIndex++;

                int curRadius = radiusIndex*2*posRadius;
                int curCircleChecks = (int)Math.Ceiling(2 * Math.PI * curRadius / (2 * (double)posRadius));

                for (int i = 1; i < curCircleChecks; i++)
                {
                    posChecked++;
                    var cRadians = 2 * Math.PI / (curCircleChecks - 1) * i; //check decimals
                    var pos = new Vector2((float)Math.Floor(heroPoint.X + curRadius * Math.Cos(cRadians)), (float)Math.Floor(heroPoint.Y + curRadius * Math.Sin(cRadians)));

                    points.Add(pos);
                }
            }

            if (!managed)
                return points.Where(p => IsPointSafe(p) && !p.IsWall()).ToArray();

            return !awayFrom.HasValue ? 
                points.Where(p => IsPointSafe(p) && IsPathSafeEx(p) && !p.IsWall()).ToArray() :
                points.Where(p => IsPointSafe(p) && IsPathSafeEx(p) && !p.IsWall() && p.Distance(awayFrom.Value) >= 225).OrderBy(
                p =>
                {
                    if (AntiStutterSearchType == StutterSearchType.PlayerFaceDirection)
                    {
                        var faceVec = 500*ObjectManager.Player.Direction.To2D().Perpendicular();
                        var destVec = p - Player.Instance.Position.To2D();
                        return faceVec.AngleBetween(destVec);
                    }

                    if (AntiStutterSearchType == StutterSearchType.FarestAway)
                        return -p.Distance(Player.Instance);

                    /*AntiStutterSearchType == StutterSearchType.MousePos*/
                    return p.Distance(Game.CursorPos);
                }).ToArray();
        }

        public Vector2 GetClosestEvadePoint(Vector2 from)
        {
            var polygons = ClippedPolygons.Where(p => p.IsInside(from)).ToArray();

            var polPoints =
                polygons.Select(pol => pol.ToDetailedPolygon())
                    .SelectMany(pol => pol.Points)
                    .OrderByDescending(p => p.Distance(from, true));

            return !polPoints.Any() ? Vector2.Zero : polPoints.Last();
        }

        int GetTimeUnitlOutOfDangerArea(Vector2 evadePoint)
        {
            IEnumerable<Vector2> inters =
                (from polygon in SkillshotDetector.ActiveSkillshots.Select(x => x.ToRealPolygon())
                 let intersections = polygon.GetIntersectionPointsWithLineSegment(Player.Instance.Position.To2D(), evadePoint)
                 .OrderBy(x => x.Distance(Player.Instance))
                 where intersections.Any()
                 select intersections.Last()).ToList();

            if (inters.Any())
            {
                var farest = inters.OrderBy(x => x.Distance(Player.Instance)).Last();
                return Player.Instance.WalkingTime(farest);
            }

            return int.MaxValue;
        }
        
        public EvadeResult CalculateEvade(Vector2 anchor, bool outside = false)
        {
            var playerPos = Player.Instance.ServerPosition.To2D();
            var maxTime = GetTimeAvailable();
            var time = Math.Max(0, maxTime - (Game.Ping + ServerTimeBuffer));

            // ReSharper disable once SimplifyConditionalTernaryExpression
            var points = GetEvadePoints();

            if (!points.Any() && !EvadeSpellManager.TryEvadeSpell(time, this))
            {
                var closestPoint = GetClosestEvadePoint(playerPos);
                /*no points => no evade spell => closest walk dist*/
                return new EvadeResult(this, closestPoint, anchor, maxTime, time, true);
            }

            var evadePoint = points.OrderBy(p => !p.IsUnderTurret()).ThenBy(p => p.Distance(Game.CursorPos)).FirstOrDefault();
            return new EvadeResult(this, evadePoint, anchor, maxTime, time, true);
        }

        public bool IsHeroPathSafe(EvadeResult evade, Vector3[] desiredPath, AIHeroClient hero = null)
        {
            hero = hero ?? Player.Instance;

            var path = (desiredPath ?? hero.RealPath()).ToVector2();
            return IsPathSafeEx(path, hero);
        }

        public bool MoveTo(Vector2 point, bool limit = true)
        {
            if (limit && EvadeIssurOrderTime + IssueOrderTickLimit >= Environment.TickCount)
            {
                return false;
            }

            EvadeIssurOrderTime = Environment.TickCount;
            Player.IssueOrder(GameObjectOrder.MoveTo, point.To3DWorld(), false);

            return true;
        }

        public bool MoveTo(Vector3 point, bool limit = true)
        {
            return MoveTo(point.To2D(), limit);
        }

        public class EvadeResult
        {
            private MoonWalkEvade _moonWalkEvade;


            public bool IsOutsideEvade { get; set; }

            public int StutterDistance => EvadeMenu.MainMenu["stutterDistanceTrigget"].Cast<Slider>().CurrentValue;
            public bool ShouldPreventStuttering => IsOutsideEvade && Player.Instance.Distance(WalkPoint) <= StutterDistance;



            private int ExtraRange { get; set; }

            public int Time { get; set; }
            public Vector2 PlayerPos { get; set; }
            public Vector2 EvadePoint { get; set; }
            public Vector2 AnchorPoint { get; set; }
            public int TimeAvailable { get; set; }
            public int TotalTimeAvailable { get; set; }
            public bool EnoughTime { get; set; }

            public bool IsValid
            {
                get { return !EvadePoint.IsZero; }
            }

            public Vector3 WalkPoint
            {
                get
                {
                    var walkPoint = EvadePoint.Extend(PlayerPos, -80);
                    var newPoint = walkPoint.Extend(PlayerPos, -ExtraRange);

                    if (_moonWalkEvade.IsPointSafe(newPoint))
                    {
                        return newPoint.To3DWorld();
                    }

                    return walkPoint.To3DWorld();
                }
                set { throw new NotImplementedException(); }
            }

            public EvadeResult(MoonWalkEvade moonWalkEvade, Vector2 evadePoint, Vector2 anchorPoint, int totalTimeAvailable,
                int timeAvailable,
                bool enoughTime)
            {
                _moonWalkEvade = moonWalkEvade;
                PlayerPos = Player.Instance.Position.To2D();
                Time = Environment.TickCount;

                EvadePoint = evadePoint;
                AnchorPoint = anchorPoint;
                TotalTimeAvailable = totalTimeAvailable;
                TimeAvailable = timeAvailable;
                EnoughTime = enoughTime;

                // extra moonWalkEvade range
                if (_moonWalkEvade.ExtraEvadeRange > 0)
                {
                    ExtraRange = (_moonWalkEvade.RandomizeExtraEvadeRange
                        ? Utils.Utils.Random.Next(_moonWalkEvade.ExtraEvadeRange / 3, _moonWalkEvade.ExtraEvadeRange)
                        : _moonWalkEvade.ExtraEvadeRange);
                }
            }

            public bool Expired(int time = 4000)
            {
                return Elapsed(time);
            }

            public bool Elapsed(int time)
            {
                return Elapsed() > time;
            }

            public int Elapsed()
            {
                return Environment.TickCount - Time;
            }
        }
    }
}