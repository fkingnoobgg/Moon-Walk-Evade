using EloBuddy;
using Moon_Walk_Evade.Utils;

namespace Moon_Walk_Evade.Skillshots.SkillshotTypes
{
    public class YasuoQ : LinearSkillshot
    {
        public override EvadeSkillshot NewInstance(bool debug = false)
        {
            var newInstance = new YasuoQ { OwnSpellData = OwnSpellData };
            return newInstance;
        }

        public override void OnSpellDetection(Obj_AI_Base sender)
        {
            FixedStartPos = Caster.ServerPosition;
            FixedEndPos = FixedStartPos.ExtendVector3(CastArgs.End, -OwnSpellData.Range);
        }
    }
}
