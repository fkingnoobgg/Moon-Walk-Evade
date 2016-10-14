﻿using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using Moon_Walk_Evade.Skillshots;
using Moon_Walk_Evade.Skillshots.SkillshotTypes;
using SharpDX;

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

        private static List<Vector2> DrawList = new List<Vector2>(5);
        public static void AddDrawVector(this Vector3 v)
        {
            if (!DrawList.Contains(v.To2D()))
                DrawList.Add(v.To2D());
        }
        public static void AddDrawVector(this Vector2 v)
        {
            if (!DrawList.Contains(v))
                DrawList.Add(v);
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
                    new Circle { Color = System.Drawing.Color.BlueViolet, Radius = 100 }.Draw(vector2.To3D());
                }
            };
            Game.OnUpdate += GameOnOnUpdate;
        }

        private static EvadeSkillshot lastKSkillshot;
        private static void GameOnOnUpdate(EventArgs args)
        {
            CheckReportedHitSkillshots();

            CheckDebugSkillshotHit();
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

                if (lastKSkillshot.GetType() == typeof(LinearSkillshot))
                {
                    var skill = (LinearSkillshot)lastKSkillshot;
                    if (skill.CurrentPosition.Distance(Player.Instance) <= Player.Instance.BoundingRadius &&
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
            if (!EvadeMenu.HotkeysMenu["debugMode"].Cast<KeyBind>().CurrentValue)
                return;

            if (GlobalStartPos.IsZero || GlobalEndPos.IsZero)
                return;

            if (Environment.TickCount - LastCreationTick < EvadeMenu.HotkeysMenu["debugModeIntervall"].Cast<Slider>().CurrentValue)
                return;

            LastCreationTick = Environment.TickCount;
            var skillshot =
                SkillshotDatabase.Database[EvadeMenu.HotkeysMenu["debugMissile"].Cast<Slider>().CurrentValue];
            if (skillshot.GetType() == typeof(CircularSkillshot) ||
                skillshot.GetType() == typeof(MultiCircleSkillshot))
                EvadeMenu.HotkeysMenu["isProjectile"].Cast<CheckBox>().CurrentValue = false;


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
            if (!EvadeMenu.HotkeysMenu["debugMode"].Cast<KeyBind>().CurrentValue)
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
