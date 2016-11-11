using System;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK.Menu.Values;
using Moon_Walk_Evade.Utils;
using SharpDX;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    class ZileanQ : CircularSkillshot
    {
        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new ZileanQ { OwnSpellData = OwnSpellData };
            if (debug)
            {
                bool isProjectile = EvadeMenu.DebugMenu["isProjectile"].Cast<CheckBox>().CurrentValue;
                var newDebugInst = new ZileanQ
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

        private int CreationTime;
        public override void OnCreateUnsafe(GameObject obj)
        {
            var mis = obj as MissileClient;
            if (mis != null)
            {
                CreationTime = Environment.TickCount;
            }
            base.OnCreateUnsafe(obj);
        }

        public override Vector3 FixedEndPosition
        {
            get
            {
                if (Missile == null || Environment.TickCount - CreationTime <= 750 || true)
                    return base.FixedEndPosition;

                /*Could be collected afterwards*/
                return ObjectManager.Get<Obj_GeneralParticleEmitter>().First(x => x.Name.Equals("Zilean_Base_Q_Detonate_Audio.troy")).Position;
            }
        }
    }
}
