using System;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Rendering;
using Moon_Walk_Evade.Properties;
using Moon_Walk_Evade.Skillshots;
using SharpDX;
using Collision = Moon_Walk_Evade.Evading.Collision;
using Debug = Moon_Walk_Evade.Utils.Debug;

namespace Moon_Walk_Evade
{
    internal static class Program
    {
        public static bool DeveloperMode = false;

        private static SpellDetector _spellDetector;
        private static Sprite introImg;

        private static void Main(string[] args)
        {
            Loading.OnLoadingComplete += delegate
            {
                _spellDetector = new SpellDetector(DeveloperMode ? DetectionTeam.AnyTeam : DetectionTeam.EnemyTeam);
                EvadeMenu.CreateMenu();
                new Evading.MoonWalkEvade(_spellDetector);


                Collision.Init();
                Debug.Init(ref _spellDetector);
                
                Core.DelayAction(() =>
                {
                    introImg = new Sprite(TextureLoader.BitmapToTexture(Resources.moonWalkTransparent));
                    Chat.Print(
                        "<b><font size='20' color='#4B0082'>Moon Walk Evade</font><font size='20' color='#FFA07A'> Loaded</font></b>");

                    Drawing.OnDraw += DrawingOnOnDraw;
                    Core.DelayAction(() =>
                    {
                        Drawing.OnDraw -= DrawingOnOnDraw;
                    }, 7000);
                }, 2000);
            };
            
        }

        private static int drawTick;
        private static void DrawingOnOnDraw(EventArgs args)
        {
            if (drawTick == 0)
                drawTick = Environment.TickCount;

            int timeElapsed = Environment.TickCount - drawTick;
            introImg.CenterRef = new Vector2(Drawing.Width / 2f, Drawing.Height / 2f).To3D();

            int dt = 300;
            if (timeElapsed <= dt)
            introImg.Scale = new Vector2(timeElapsed* 1f/ dt, timeElapsed* 1f/ dt);
            introImg.Draw(new Vector2(Drawing.Width / 2f - 1415 / 2f, Drawing.Height / 2f - 240 / 2f));
        }
    }
}
