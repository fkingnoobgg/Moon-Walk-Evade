using System;
using EloBuddy;
using EloBuddy.SDK;
using Moon_Walk_Evade.Utils;
using Color = System.Drawing.Color;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    class LuxR : LinearSkillshot
    {
        public LuxR()
        {
            Caster = null;
            SpawnObject = null;
            SData = null;
            OwnSpellData = null;
            Team = GameObjectTeam.Unknown;
            IsValid = true;
            TimeDetected = Environment.TickCount;
        }

        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new LuxR { OwnSpellData = OwnSpellData };
            if (debug)
            {
                var newDebugInst = new LuxR
                {
                    OwnSpellData = OwnSpellData,
                    FixedStartPosition = Debug.GlobalStartPos,
                    FixedEndPosition = Debug.GlobalEndPos,
                    IsValid = true,
                    IsActive = true,
                    TimeDetected = Environment.TickCount - Game.Ping - 45,
                    SpawnObject = null
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

            Utils.Utils.Draw3DRect(Missile?.StartPosition ?? FixedStartPosition, Missile?.EndPosition ?? FixedEndPosition, OwnSpellData.Radius * 2, Color.White);
        }

        public override Geometry.Polygon ToPolygon()
        {
            float extrawidth = 0;
            if (OwnSpellData.AddHitbox)
            {
                extrawidth += Player.Instance.HitBoxRadius();
            }
            return new Geometry.Polygon.Rectangle(Missile?.StartPosition ?? FixedStartPosition, Missile?.EndPosition ?? FixedEndPosition, OwnSpellData.Radius + extrawidth);
        }
    }
}
