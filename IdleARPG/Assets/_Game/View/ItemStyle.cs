using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// How items look and read: rarity colours, Korean labels, icon keys, affix lines, the compare arrows. One meaning
    /// per colour (UX plan §3.0): a rarity colour only on the name, the cell edge and the ◆ pips; green and red only for
    /// gain and loss; element colours only on the element's own word; every stat line in the one body colour.
    /// </summary>
    public static class ItemStyle
    {
        // UX plan §3.0 (나): the colours that are not a rarity.
        public const string BodyHex = "#E8E0D0", GainHex = "#5BD66B", LossHex = "#FF5A4D", SameHex = "#8A8178",
            TradeHex = "#FFD24D", StarHex = "#FFCF5A";
        /// <summary>The edge of a common piece's bag cell: there, but quieter than any rarity.</summary>
        public static readonly Color CommonEdge = new Color32(0x5A, 0x56, 0x4E, 0xFF);

        /// <summary>
        /// UX plan §3.0 (가): Diablo's order (white, blue, yellow, orange) with the top rarity moved from dull gold to
        /// violet — yellow, orange and gold sat too close and the best one was the least seen (audit #3).
        /// </summary>
        public static Color Color(Rarity r)
        {
            switch (r)
            {
                case Rarity.Magic: return new Color32(0x6F, 0xA0, 0xFF, 0xFF);
                case Rarity.Rare: return new Color32(0xFF, 0xE4, 0x5C, 0xFF);
                case Rarity.Legendary: return new Color32(0xFF, 0x8C, 0x26, 0xFF);
                // 11-24: unique was purple, which Korean players read as *below* orange (Lost Ark 영웅 < 전설; WoW epic <
                // legendary). Diablo's own order ends in gold: 희귀 lemon yellow < 전설 orange < 고유 antique gold.
                case Rarity.Unique: return new Color32(0xE0, 0xBC, 0x74, 0xFF);
                default: return new Color32(0xC8, 0xC8, 0xC0, 0xFF);
            }
        }

        public static string Hex(Rarity r) => "#" + ColorUtility.ToHtmlStringRGB(Color(r));

        /// <summary>◆ to ◆◆◆◆◆: the rarity as a count, for whoever cannot tell the blue from the violet.</summary>
        public static string Pips(Rarity r) => new string('◆', (int)r + 1);

        /// <summary>"◆◆◆ 희귀" in the rarity's colour: the rarity always in shape and words beside the colour.</summary>
        public static string Tag(Rarity r) => "<color=" + Hex(r) + ">" + Pips(r) + " " + RarityName(r) + "</color>";

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
            // 11-24: a unique has its own picture (art/pixellab/icons/item_unique_<id>.png) once it is drawn.
            if (!string.IsNullOrEmpty(item.UniqueId) && HasSprite != null && HasSprite("icon/item_unique_" + item.UniqueId)) return "icon/item_unique_" + item.UniqueId;
            string own = "icon/item_" + item.BaseId;
            return HasSprite != null && HasSprite(own) ? own : ItemData.Base(item.BaseId).Icon;
        }

        /// <summary>Set by GameBootstrap from the sprite catalog.</summary>
        public static System.Func<string, bool> HasSprite;

        /// <summary>A drop worth a big signal: legendary or better, or any ★ 대옵션 line.</summary>
        public static bool IsJackpot(Item item) => item != null && (item.Rarity >= Rarity.Legendary || item.GreaterCount > 0);

        /// <summary>
        /// Where a stat line sits in a tooltip (UX plan A3): 0 공격, 1 방어, 2 스킬, 3 유틸. The kind of a stat is told by
        /// this order, not by a colour — colouring lines by stat (11-22) used the rarity colours for a second meaning.
        /// </summary>
        public static int Group(Stat s)
        {
            switch (s)
            {
                case Stat.AreaPct: case Stat.ProjectileCount: case Stat.RangePct: case Stat.CooldownPct:
                case Stat.ResonancePct: case Stat.ChainRadiusPct: return 2;
            }
            int g = GameText.StatGroup(s); // 0 공격, 1 방어, 2 유틸
            return g == 2 ? 3 : g;
        }

        /// <summary>An element stat's own word and colour (the only colour inside a stat line), or null.</summary>
        static (string word, string hex)? ElementWord(Stat s)
        {
            switch (s)
            {
                case Stat.EmberPct: return ("잿불", "#FF8A4A");
                case Stat.FrostPct: return ("서리", "#8FD4FF");
                case Stat.ShockPct: return ("전격", "#FFE36A");
                case Stat.RotPct: return ("부패", "#9BD15A"); // SkillCard's element colours
                case Stat.VoidPct: return ("공허", "#B78CFF");
                default: return null;
            }
        }

        /// <summary>The words of a stat line with no colour.</summary>
        public static string LineText(AffixRoll a, bool tier = false) => StatText(a.Stat, a.Value) + (tier && a.Tier > 0 ? $" [T{a.Tier}]" : "");

        /// <summary>"공격력 +14", "받는 피해 −3%": a stat and its amount in the affix's own words, no colour.</summary>
        public static string StatText(Stat s, float value)
        {
            var def = ItemData.Affix(s);
            string label = def != null ? def.Label : s.ToString() + " +{0}";
            return string.Format(label, value.ToString(value % 1f == 0f ? "0" : "0.#"));
        }

        /// <summary>
        /// A stat line in the body colour: only an element's word wears its element colour, and on a ★ line only the ★
        /// is gold (UX plan A3).
        /// </summary>
        /// <param name="tier">False leaves the [Tn] tag off (the gear list is too narrow for it). Never cut it off afterwards:
        /// a line closes its colour after the tier, and a cut leaves the tag open and breaks the whole panel's rich text.</param>
        public static string Line(AffixRoll a, bool tier = true)
        {
            string line = LineText(a, tier);
            string body = "<color=" + BodyHex + ">" + line + "</color>";
            var el = ElementWord(a.Stat);
            if (el.HasValue && line.StartsWith(el.Value.word, System.StringComparison.Ordinal))
                body = "<color=" + el.Value.hex + ">" + el.Value.word + "</color><color=" + BodyHex + ">" + line.Substring(el.Value.word.Length) + "</color>";
            return (a.Greater ? "<color=" + StarHex + ">★</color> " : "") + body;
        }

        // ---------- the compare arrows (UX plan §3.0 다): the same glyph, colour and words wherever gear is judged ----------

        /// <summary>▲▲ ▲ ▼ ⇄ ═; nothing for a piece above the hero's level (it gets "Lv N 필요", not an arrow).</summary>
        public static string ArrowGlyph(GearArrow a)
        {
            switch (a)
            {
                case GearArrow.BigUp: return "▲▲";
                case GearArrow.Up: return "▲";
                case GearArrow.Down: return "▼";
                case GearArrow.Trade: return "⇄"; // ◆ is the rarity pip only (loop 1: one glyph meant two things)
                case GearArrow.Same: return "═";
                default: return "";
            }
        }

        public static string ArrowHex(GearArrow a)
        {
            switch (a)
            {
                case GearArrow.BigUp: case GearArrow.Up: return GainHex;
                case GearArrow.Down: case GearArrow.Locked: return LossHex;
                case GearArrow.Trade: return TradeHex;
                default: return SameHex;
            }
        }

        /// <summary>A ratio (1 = same) as a signed, coloured change: "+12%" green, "−2%" red, "±0%" grey.</summary>
        public static string Pct(float ratio)
        {
            float pct = (ratio - 1f) * 100f;
            if (Mathf.Abs(pct) < 0.05f) return "<color=" + SameHex + ">±0%</color>";
            string n = Mathf.Abs(pct).ToString(Mathf.Abs(pct) < 10f ? "0.#" : "0");
            return pct > 0f ? "<color=" + GainHex + ">+" + n + "%</color>" : "<color=" + LossHex + ">−" + n + "%</color>";
        }

        /// <summary>
        /// The answer line of a tooltip (UX plan A4): "▲ 전투력 +12%   공격 +15% · 생존 −2%". A ◆ trade says 교환 in place
        /// of one number (its two halves are the point) but still shows the power ratio, so it ends in a verdict
        /// ("⇄ 교환 · 전투력 −3%  (공격 −33% · 생존 +17%)"); ═ says 비슷함.
        /// <paramref name="effectName"/> is the tooltip's own word for the legendary line (전설 효과 / 고유 효과).
        /// </summary>
        public static string Verdict(GearCompare c, string effectName = "전설 효과")
        {
            bool trade = c.Arrow == GearArrow.Trade;
            string head = trade ? "⇄ 교환 · 전투력" : c.Arrow == GearArrow.Same ? "═ 비슷함" : ArrowGlyph(c.Arrow) + " 전투력";
            string s = "<color=" + ArrowHex(c.Arrow) + ">" + head + "</color>";
            if (c.Arrow != GearArrow.Same) s += " " + Pct(c.Power);
            string halves = "<color=" + BodyHex + ">공격</color> " + Pct(c.Offence) + " <color=" + SameHex + ">·</color> <color=" + BodyHex + ">생존</color> " + Pct(c.Survival);
            if (c.Essences != 0)
                halves += " <color=" + SameHex + ">·</color> <color=" + BodyHex + ">" + effectName + "</color> <color=" + (c.Essences > 0 ? GainHex : LossHex) + ">"
                    + (c.Essences > 0 ? "+" : "−") + Mathf.Abs(c.Essences) + "</color>";
            return s + (trade ? "  (" + halves + ")" : "   " + halves);
        }
    }
}
