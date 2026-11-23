using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>How items look and read: rarity colours (GDD §8.1), Korean labels, icon keys, affix lines.</summary>
    public static class ItemStyle
    {
        public static Color Color(Rarity r)
        {
            switch (r)
            {
                case Rarity.Magic: return new Color(0.45f, 0.62f, 1f);
                case Rarity.Rare: return new Color(1f, 0.9f, 0.35f);
                case Rarity.Legendary: return new Color(1f, 0.55f, 0.15f);
                case Rarity.Unique: return new Color(0.86f, 0.72f, 0.38f);
                default: return new Color(0.88f, 0.88f, 0.85f);
            }
        }

        public static string Hex(Rarity r) => "#" + ColorUtility.ToHtmlStringRGB(Color(r));

        public static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Magic: return "마법";
                case Rarity.Rare: return "희귀";
                case Rarity.Legendary: return "전설";
                case Rarity.Unique: return "고유";
                default: return "일반";
            }
        }

        public static string SpotName(EquipSpot s)
        {
            switch (s)
            {
                case EquipSpot.Weapon: return "무기";
                case EquipSpot.Helm: return "투구";
                case EquipSpot.Armor: return "갑옷";
                case EquipSpot.Gloves: return "장갑";
                case EquipSpot.Boots: return "장화";
                case EquipSpot.Belt: return "허리띠";
                case EquipSpot.Ring1: return "반지";
                case EquipSpot.Ring2: return "반지";
                default: return "목걸이";
            }
        }

        /// <summary>The base's own picture (`icon/item_<id>`, 희승 11-22 직업 전용 장비) once drawn, else its slot's.</summary>
        public static string Icon(Item item)
        {
            string own = "icon/item_" + item.BaseId;
            return HasSprite != null && HasSprite(own) ? own : ItemData.Base(item.BaseId).Icon;
        }

        /// <summary>Set by GameBootstrap from the sprite catalog.</summary>
        public static System.Func<string, bool> HasSprite;

        /// <summary>A drop worth a big signal: legendary or better, or any ★ 대옵션 line.</summary>
        public static bool IsJackpot(Item item) => item != null && (item.Rarity >= Rarity.Legendary || item.GreaterCount > 0);

        /// <summary>
        /// 희승 11-22 (Diablo II's tooltip colours): a stat's colour by what it does — magic blue for the plain ones, its
        /// element's colour for fire / frost / lightning, violet for what grows a build (area, copies, reach, cooldown,
        /// resonance, chain), green for experience and finds. ★ lines are gold whatever their stat.
        /// </summary>
        public static string StatHex(Stat s)
        {
            switch (s)
            {
                case Stat.EmberPct: return "#ff8a4a";
                case Stat.FrostPct: return "#8fd4ff";
                case Stat.ShockPct: return "#ffe36a";
                case Stat.AreaPct: case Stat.ProjectileCount: case Stat.RangePct: case Stat.CooldownPct:
                case Stat.ResonancePct: case Stat.ChainRadiusPct: return "#c9a2ff";
                case Stat.XpPct: case Stat.MagicFindPct: return "#8fe08f";
                default: return "#8fa8ff";
            }
        }

        /// <summary>The words of a stat line with no colour (the item's base line is plain white).</summary>
        public static string LineText(AffixRoll a, bool tier = false)
        {
            var def = ItemData.Affix(a.Stat);
            string label = def != null ? def.Label : a.Stat.ToString() + " +{0}";
            return string.Format(label, a.Value.ToString(a.Value % 1f == 0f ? "0" : "0.#")) + (tier && a.Tier > 0 ? $" [T{a.Tier}]" : "");
        }

        /// <param name="tier">False leaves the [Tn] tag off (the gear list is too narrow for it). Never cut it off afterwards:
        /// a line closes its colour after the tier, and a cut leaves the tag open and breaks the whole panel's rich text.</param>
        public static string Line(AffixRoll a, bool tier = true)
        {
            string line = LineText(a, tier);
            // ★ 대옵션 lines stand out in gold; the rest take their stat's colour.
            return a.Greater ? "<color=#ffcf5a>★ " + line + "</color>" : "<color=" + StatHex(a.Stat) + ">" + line + "</color>";
        }
    }
}
