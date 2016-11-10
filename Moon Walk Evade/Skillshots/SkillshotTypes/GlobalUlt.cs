using System;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using Moon_Walk_Evade.Utils;
using SharpDX;
using Color = System.Drawing.Color;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    class GlobalUlt : LinearSkillshot
    {
        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new GlobalUlt { OwnSpellData = OwnSpellData };
            if (debug)
            {
                bool isProjectile = EvadeMenu.DebugMenu["isProjectile"].Cast<CheckBox>().CurrentValue;
                var newDebugInst = new GlobalUlt
                {
                    OwnSpellData = OwnSpellData,
                    FixedStartPos = Debug.GlobalStartPos,
                    FixedEndPos = Debug.GlobalEndPos,
                    IsValid = true,
                    IsActive = true,
                    TimeDetected = Environment.TickCount,
                    SpawnObject = isProjectile ? new MissileClient() : null
                };
                return newDebugInst;
            }
            return newInstance;
        }

        public override void OnDraw()
        {
            if (!IsValid)
            {
                return;
            }

            Utils.Utils.Draw3DRect(CurrentPosition, CurrentPosition.ExtendVector3(EndPosition, 300), OwnSpellData.Radius * 2, Color.White, 3);
        }
    }
}
