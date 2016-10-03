﻿using EloBuddy.SDK.Events;
using Moon_Walk_Evade.Skillshots;
using Moon_Walk_Evade.Utils;
using Collision = Moon_Walk_Evade.Evading.Collision;

namespace Moon_Walk_Evade
{
    internal static class Program
    {
        public static bool DeveloperMode = true;

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
            };
        }
    }
}
