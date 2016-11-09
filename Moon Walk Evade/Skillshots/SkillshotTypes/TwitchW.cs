using System;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using Moon_Walk_Evade.Utils;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    class TwitchW : CircularSkillshot
    {
        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new TwitchW { OwnSpellData = OwnSpellData };
            if (debug)
            {
                bool isProjectile = EvadeMenu.DebugMenu["isProjectile"].Cast<CheckBox>().CurrentValue;
                var newDebugInst = new TwitchW
                {
                    OwnSpellData = OwnSpellData,
                    FixedStartPosition = Debug.GlobalStartPos,
                    FixedEndPosition = Debug.GlobalEndPos,
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
            base.OnCreateUnsafe(obj);
            if (Missile != null)
            {
                OwnSpellData.Delay =
                    (int) (Missile.Position.Distance(FixedEndPosition)/OwnSpellData.MissileSpeed*1000);
            }
        }
    }
}

