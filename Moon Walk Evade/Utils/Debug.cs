using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using Moon_Walk_Evade.Skillshots;
using Moon_Walk_Evade.Skillshots.SkillshotTypes;
using SharpDX;
using Color = System.Drawing.Color;

namespace Moon_Walk_Evade.Utils
{
    class HitReportInst
    {
        public int NeededTime { get; }

        public HitReportInst(int timeLeft, int neededTime, LinearSkillshot[] flyingSkillshots)
        {
            NeededTime = neededTime;
            TimeLeft = timeLeft;
            FlyingSkillshots = flyingSkillshots;
            InitTick = Environment.TickCount;
        }

        public int TimeLeft { get; }
        public LinearSkillshot[] FlyingSkillshots { get; }
        public int InitTick { get; }

        public bool Hit { get; set; }
        public bool Passed { get; set; }

        public bool Finished { get; set; }
    }

    static class Debug
    {
        public static Vector2 LastIssueOrderPos;
        public static bool TempBool { get; set; }
        public static Vector3 GlobalEndPos = Vector3.Zero, GlobalStartPos = Vector3.Zero;
        private static SpellDetector spellDetector;
        public static int LastCreationTick;
        

        static List<HitReportInst> hitReports = new List<HitReportInst>(); 

        /// <summary>
        /// The addon assumes that the evade time is too high => check that
        /// </summary>
        /// <param name="timeLeft"></param>
        /// <param name="neededTime"></param>
        /// <param name="flyingSkillshots"></param>
        public static void ReportHit(int timeLeft, int neededTime, List<EvadeSkillshot> flyingSkillshots)
        {
            hitReports.Add(new HitReportInst(timeLeft, neededTime, 
                flyingSkillshots.Where(x => x.SpawnObject != null).Select(x => (LinearSkillshot)x).ToArray()));
        }

        private static List<Vector2> DrawList = new List<Vector2>();
        private static List<Tuple<Vector2, Vector2>> DrawLineList = new List<Tuple<Vector2, Vector2>>();
        public static void AddDrawVector(this Vector3 v)
        {
            if (!DrawList.Contains(v.To2D()))
                DrawList.Add(v.To2D());
        }
        public static void AddDrawVector(this Vector2 v, int time = 10000)
        {
            if (!DrawList.Contains(v))
                DrawList.Add(v);

            if (time != short.MaxValue)
                Core.DelayAction(() => DrawList.Remove(v), time);
        }

        public static void AddDrawLine(this Vector2 v, Vector2? from = null, int time = 10000)
        {
            var wtc = Drawing.WorldToScreen(v.To3D());
            var wtcFrom = Drawing.WorldToScreen(from?.To3D() ?? Player.Instance.Position);
            var tuple = new Tuple<Vector2, Vector2>(wtcFrom, wtc);

            if (!DrawLineList.Contains(tuple))
                DrawLineList.Add(tuple);

            if (time != short.MaxValue)
                Core.DelayAction(() => DrawLineList.Remove(tuple), time);
        }

        public static void Init(ref SpellDetector detector)
        {
            spellDetector = detector;

            Game.OnWndProc += GameOnOnWndProc;
            Drawing.OnDraw += args =>
            {
                if (!GlobalEndPos.IsZero)
                    new Circle { Color = System.Drawing.Color.DodgerBlue, Radius = 100 }.Draw(GlobalEndPos);
                if (!GlobalStartPos.IsZero)
                    new Circle { Color = System.Drawing.Color.Red, Radius = 100 }.Draw(GlobalStartPos);

                if (DrawList.Count >= 5) DrawList.Clear();
                foreach (var vector2 in DrawList)
                {
                    new Circle { Color = System.Drawing.Color.BlueViolet, Radius = 50, BorderWidth = 20}.Draw(vector2.To3D());
                }

                if (DrawLineList.Count >= 5) DrawLineList.Clear();
                foreach (var tuple in DrawLineList)
                {
                    Drawing.DrawLine(tuple.Item1, tuple.Item2, 5, Color.Aqua);
                }
            };
            Game.OnUpdate += GameOnOnUpdate;
        }

        private static EvadeSkillshot lastKSkillshot;
        private static void GameOnOnUpdate(EventArgs args)
        {
            CheckReportedHitSkillshots();

            //CheckDebugSkillshotHit();
            CreateDebugSkillshot();
        }

        private static void CheckDebugSkillshotHit()
        {
            if (lastKSkillshot != null)
            {
                if (!lastKSkillshot.IsValid || !lastKSkillshot.IsActive)
                {
                    lastKSkillshot = null;
                    return;
                }

                var skillshot = lastKSkillshot as LinearSkillshot;
                if (skillshot != null)
                {
                    var skill = skillshot;
                    if (skill == null || skill.CurrentPosition == default(Vector3))
                        return;

                    if (skill.CurrentPosition.Distance(Player.Instance) <= skill.OwnSpellData.Radius + Player.Instance.BoundingRadius &&
                        Player.Instance.Position.To2D().ProjectOn(skill.CurrentPosition.To2D(), skill.EndPosition.To2D()).IsOnSegment && skill.Missile != null)
                    {
                        Chat.Print(Game.Time + "  Hit");
                        lastKSkillshot = null;
                    }
                }
            }
        }

        private static void CreateDebugSkillshot()
        {
            if (!EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue)
                return;

            if (GlobalStartPos.IsZero || GlobalEndPos.IsZero)
                return;

            if (Environment.TickCount - LastCreationTick < EvadeMenu.DebugMenu["debugModeIntervall"].Cast<Slider>().CurrentValue)
                return;

            LastCreationTick = Environment.TickCount;
            var skillshot =
                SkillshotDatabase.Database[EvadeMenu.DebugMenu["debugMissile"].Cast<Slider>().CurrentValue];
            if (skillshot.GetType() == typeof(CircularSkillshot) ||
                skillshot.GetType() == typeof(MultiCircleSkillshot))
                EvadeMenu.DebugMenu["isProjectile"].Cast<CheckBox>().CurrentValue = false;


            var nSkillshot = skillshot.NewInstance(true);
            spellDetector.AddSkillshot(nSkillshot);
            lastKSkillshot = nSkillshot;
        }

        /// <summary>
        /// Observes the skillshots that should normally hit when "not enough timeLeft" was calculated
        /// </summary>
        private static void CheckReportedHitSkillshots()
        {
            foreach (var report in hitReports)
            {
                bool hit = false;
                int dodgeCount = 0;
                foreach (var linearSkillshot in report.FlyingSkillshots)
                {
                    var sPos = linearSkillshot.CurrentPosition.To2D();
                    var ePos = linearSkillshot.EndPosition.To2D();
                    var playerPos = Player.Instance.Position.To2D();
                    var playerProjection = playerPos.ProjectOn(sPos, ePos).SegmentPoint;

                    if (sPos.Distance(playerPos) <= linearSkillshot.OwnSpellData.Radius + Player.Instance.BoundingRadius)
                    {
                        hit = true;
                        break;
                    }

                    if (playerProjection.Distance(ePos) - 50 > sPos.Distance(ePos))
                        dodgeCount++;
                }

                if (!hit && dodgeCount == report.FlyingSkillshots.Length)
                    report.Passed = true;
                else if (hit)
                    report.Hit = true;
            }

            hitReports.RemoveAll(report => report.Hit || report.Finished || Environment.TickCount - report.InitTick > report.TimeLeft + 1000);

            foreach (var report in hitReports.Where(report => report.Passed))
            {
                Chat.Print("Calculation Update | Dt: " + (report.NeededTime - report.TimeLeft) + "| T: " + report.TimeLeft);
                report.Finished = true;
            }
        }

        private static void GameOnOnWndProc(WndEventArgs args)
        {
            if (!EvadeMenu.DebugMenu["debugMode"].Cast<KeyBind>().CurrentValue)
                return;

            if (args.Msg == 0x0201)//mouse down
            {
                GlobalEndPos = Game.CursorPos;
            }

            if (args.Msg == 0x0202)
            {
                GlobalStartPos = Game.CursorPos;
            }
        }
        
    }
}
