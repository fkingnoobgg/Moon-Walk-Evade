using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Events;
using Moon_Walk_Evade.Skillshots;
using Collision = Moon_Walk_Evade.Evading.Collision;
using Debug = Moon_Walk_Evade.Utils.Debug;

namespace Moon_Walk_Evade
{
    internal static class Program
    {
        public static bool DeveloperMode = false;

        private static SpellDetector _spellDetector;

        private static void Main(string[] args)
        {
            Loading.OnLoadingComplete += delegate
            {
                _spellDetector = new SpellDetector(DeveloperMode ? DetectionTeam.AnyTeam : DetectionTeam.EnemyTeam);
                EvadeMenu.CreateMenu();
                new Evading.MoonWalkEvade(_spellDetector);


                Collision.Init();
                Debug.Init(ref _spellDetector);
                
                Core.DelayAction(() => Chat.Print("<font size='24' color='#7FFFD4'>Wanna add more skillshots to evade? Watch forum page</font>"), 5000);
            };
        }
    }
}
