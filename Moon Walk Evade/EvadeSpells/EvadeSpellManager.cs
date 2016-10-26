using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using Moon_Walk_Evade.Evading;
using Moon_Walk_Evade.Utils;
using SharpDX;

namespace Moon_Walk_Evade.EvadeSpells
{
    public static class EvadeSpellManager
    {
        public static bool ProcessFlash(MoonWalkEvade moonWalkEvade)
        {
            var castPos = GetBlinkCastPos(moonWalkEvade, Player.Instance.ServerPosition.To2D(), 425);
            var slot = GetFlashSpellSlot();
            if (!castPos.IsZero && slot != SpellSlot.Unknown && Player.Instance.Spellbook.GetSpell(slot).IsReady)
            {
                Player.Instance.Spellbook.CastSpell(slot, castPos.To3D());
                return true;
            }

            return false;
        }

        public static SpellSlot GetFlashSpellSlot()
        {
            return Player.Instance.GetSpellSlotFromName("summonerflash");
        }


        public static Vector2 GetBlinkCastPos(MoonWalkEvade moonWalkMoonWalkEvade, Vector2 center, float maxRange)
        {
            var polygons = moonWalkMoonWalkEvade.ClippedPolygons.Where(p => p.IsInside(center)).ToArray();
            var segments = new List<Vector2[]>();

            foreach (var pol in polygons)
            {
                for (var i = 0; i < pol.Points.Count; i++)
                {
                    var start = pol.Points[i];
                    var end = i == pol.Points.Count - 1 ? pol.Points[0] : pol.Points[i + 1];

                    var intersections =
                        Utils.Utils.GetLineCircleIntersectionPoints(center, maxRange, start, end)
                            .Where(p => p.IsInLineSegment(start, end))
                            .ToList();

                    if (intersections.Count == 0)
                    {
                        if (start.Distance(center, true) < maxRange.Pow() &&
                            end.Distance(center, true) < maxRange.Pow())
                        {
                            intersections = new[] { start, end }.ToList();
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else if (intersections.Count == 1)
                    {
                        intersections.Add(center.Distance(start, true) > center.Distance(end, true)
                            ? end
                            : start);
                    }

                    segments.Add(intersections.ToArray());
                }
            }

            if (!segments.Any())
            {
                return Vector2.Zero;
            }

            const int maxdist = 2000;
            const int division = 30;
            var points = new List<Vector2>();

            foreach (var segment in segments)
            {
                var dist = segment[0].Distance(segment[1]);
                if (dist > maxdist)
                {
                    segment[0] = segment[0].Extend(segment[1], dist / 2 - maxdist / 2f);
                    segment[1] = segment[1].Extend(segment[1], dist / 2 - maxdist / 2f);
                    dist = maxdist;
                }

                var step = maxdist / division;
                var count = dist / step;

                for (var i = 0; i < count; i++)
                {
                    var point = segment[0].Extend(segment[1], i * step);
                    if (!point.IsWall())
                    {
                        points.Add(point);
                    }
                }
            }

            if (!points.Any())
            {
                return Vector2.Zero;
            }

            var evadePoint = points.Where(x => moonWalkMoonWalkEvade.IsPointSafe(x) && !x.IsWall()).OrderBy(x => x.Distance(Game.CursorPos)).
                FirstOrDefault();
            return evadePoint;
        }

        public static bool TryEvadeSpell(int TimeAvailable, MoonWalkEvade moonWalkMoonWalkEvadeInstance)
        {
            IEnumerable<EvadeSpellData> evadeSpells = EvadeMenu.MenuEvadeSpells.Where(evadeSpell =>
            {
                var item = EvadeMenu.EvadeSpellMenu[evadeSpell.SpellName + "/enable"];
                // ReSharper disable once SimplifyConditionalTernaryExpression
                // ReSharper disable once MergeConditionalExpression
                return item != null ? item.Cast<CheckBox>().CurrentValue : false;
            });
            foreach (EvadeSpellData evadeSpell in evadeSpells)
            {
                int dangerValue =
                        EvadeMenu.MenuEvadeSpells.First(x => x.SpellName == evadeSpell.SpellName).DangerValue;
                if (moonWalkMoonWalkEvadeInstance.GetDangerValue() < dangerValue)
                    continue;

                bool isReady = !evadeSpell.isItem ? Player.Instance.Spellbook.GetSpell(evadeSpell.Slot).IsReady &&
                           Player.Instance.Spellbook.GetSpell(evadeSpell.Slot).IsLearned &&
                           Player.Instance.Spellbook.GetSpell(evadeSpell.Slot).SData.Mana <= Player.Instance.Mana : Item.CanUseItem(evadeSpell.itemID);

                if (!isReady)
                    continue;

                //dash
                if (evadeSpell.Range != 0)
                {
                    var evadePos = GetBlinkCastPos(moonWalkMoonWalkEvadeInstance, Player.Instance.Position.To2D(), evadeSpell.Range);
                    float castTime = evadeSpell.Delay;
                    if (TimeAvailable > castTime && !evadePos.IsZero && moonWalkMoonWalkEvadeInstance.IsPointSafe(evadePos))
                    {
                        if (IsDashSafe(evadeSpell.Slot, evadePos, moonWalkMoonWalkEvadeInstance))
                        {
                            CastEvadeSpell(evadeSpell, evadePos);
                            return true;
                        }
                    }
                }

                //speed buff (spell or item NOT)
                if (evadeSpell.EvadeType == EvadeType.MovementSpeedBuff && !evadeSpell.isItem)
                {
                    var playerPos = Player.Instance.Position.To2D();

                    float speed = Player.Instance.MoveSpeed;
                    speed += speed * evadeSpell.speedArray[Player.Instance.Spellbook.GetSpell(evadeSpell.Slot).Level - 1] / 100;
                    float maxTime = TimeAvailable - evadeSpell.Delay;
                    float maxTravelDist = speed * (maxTime / 1000);

                    var evadePoints = moonWalkMoonWalkEvadeInstance.GetEvadePoints();

                    var evadePoint = evadePoints.OrderBy(x => !x.IsUnderTurret()).ThenBy(p => p.Distance(Game.CursorPos)).FirstOrDefault();
                    if (evadePoint != default(Vector2))
                    {
                        CastEvadeSpell(evadeSpell, evadeSpell.isItem ? Vector2.Zero : evadePoint);
                        return true;
                    }
                }

                //items
                if (evadeSpell.isItem && evadeSpell.EvadeType != EvadeType.MovementSpeedBuff && 
                    Player.Instance.GetSpellSlotFromName(evadeSpell.SpellName) != SpellSlot.Unknown)
                {
                    if (TimeAvailable >= evadeSpell.Delay)
                    {
                        CastEvadeSpell(evadeSpell, Vector2.Zero);
                        return true;
                    }
                }
            }

            return false;
        }

        private static void CastEvadeSpell(EvadeSpellData evadeSpell, Vector2 evadePos)
        {
            bool isItem = evadePos.IsZero;

            if (isItem)
            {
                Item.UseItem(evadeSpell.itemID);
                return;
            }

            switch (evadeSpell.CastType)
            {
                case CastType.Position:
                    if (!evadeSpell.isReversed)
                        Player.Instance.Spellbook.CastSpell(evadeSpell.Slot, evadePos.To3D());
                    else
                        Player.Instance.Spellbook.CastSpell(evadeSpell.Slot,
                            evadePos.Extend(Player.Instance, evadePos.Distance(Player.Instance) + evadeSpell.Range).To3D());
                    break;
                case CastType.Self:
                    Player.Instance.Spellbook.CastSpell(evadeSpell.Slot, Player.Instance);
                    break;
            }
        }

        public static bool IsDashSafe(SpellSlot slot, Vector2 endPos, MoonWalkEvade evadeInstance)
        {
            var evadeSpell =
                EvadeSpellDatabase.Spells.FirstOrDefault(
                    x => x.ChampionName == Player.Instance.ChampionName && x.Slot == slot);
            if (evadeSpell == null)
                return false;

            return evadeInstance.IsPathSafeEx(Player.Instance.GetPath(endPos.To3D()).ToVector2(), (int) evadeSpell.Speed,
                (int) evadeSpell.Delay);
        }


        public static bool IsDashSpell(SpellSlot slot) => 
            EvadeSpellDatabase.Spells.Any(x => x.ChampionName == Player.Instance.ChampionName && x.Slot == slot && x.EvadeType == EvadeType.Dash);
    }
}