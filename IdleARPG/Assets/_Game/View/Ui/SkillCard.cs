using System.Text;
using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// One skill card for every screen (UX plan B2; 희승 11-23 "스킬들도 각각 설명이 없고"): the HUD hover, the ring panel's
    /// detail, the skill tree card and the codex all print the same first lines for the same skill —
    ///   name in its element colour  [원소] [역할] [형태]
    ///   what it does (GameText, one or two sentences)
    ///   numbers: 공격력의 N% (now about X) · cooldown · reach
    ///   the mark it leaves → which element after it sets off which resonance
    ///   숙련, its bonuses and the next copy
    ///   evolution (what changes) and the slot's glyph
    /// <c>brief</c> keeps the first four for the HUD.
    /// </summary>
    public static class SkillCard
    {
        /// <summary>The element colours of the UX plan §3.0(나) — names, chips and numbers only.</summary>
        public static string ElementHex(Element e)
        {
            switch (e)
            {
                case Element.Ember: return "#FF8A4A";
                case Element.Frost: return "#8FD4FF";
                case Element.Shock: return "#FFE36A";
                case Element.Rot: return "#9BD15A";
                case Element.Void: return "#B78CFF";
                default: return "#E8E0D0";
            }
        }

        public static string ElementName(Element e)
        {
            switch (e)
            {
                case Element.Ember: return "잿불";
                case Element.Frost: return "서리";
                case Element.Shock: return "전격";
                case Element.Rot: return "부패";
                case Element.Void: return "공허";
                default: return "무속성";
            }
        }

        /// <summary>The coloured element word.</summary>
        public static string Elem(Element e) => "<color=" + ElementHex(e) + ">" + ElementName(e) + "</color>";

        /// <summary>The short label before " — " in a GameText line ("개시 스킬 — …" → "개시 스킬").</summary>
        public static string Head(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int i = text.IndexOf(" — ");
            return i > 0 ? text.Substring(0, i) : text;
        }

        /// <summary>The explanation after " — ".</summary>
        public static string Tail(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int i = text.IndexOf(" — ");
            return i > 0 ? text.Substring(i + 3) : text;
        }

        const string Body = "#E8E0D0", Dim = "#8A8178", Practice = "#A8D0FF", Evo = "#FFB060", Glyph = "#A8C4FF";

        /// <summary>
        /// The card text. <paramref name="elem"/> is the element the skill casts in this slot (a glyph may convert it;
        /// <see cref="Element.None"/> takes the skill's own). <paramref name="sigil"/> is the slot's glyph, or None.
        /// </summary>
        public static string Build(Hero h, SkillDef s, bool brief = false, Element elem = Element.None, Sigil sigil = Sigil.None, bool title = true)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            var e = elem != Element.None ? elem : s.Elem;
            if (title)
            {
                sb.Append("<color=").Append(ElementHex(e)).Append(">").Append(s.Name).Append("</color>  ");
                Chips(sb, s, e);
                sb.Append('\n');
            }
            string what = GameText.Skill(s.Id) ?? GameText.Skill(s.BaseId);
            if (!string.IsNullOrEmpty(what)) sb.Append("<color=").Append(Body).Append(">").Append(what).Append("</color>\n");

            // Numbers: the multiplier and what it comes to now, then the cooldown and the reach.
            h.SkillUses.TryGetValue(s.BaseId, out int uses);
            int lv = SkillBook.SkillLevel(uses);
            sb.Append("<color=").Append(Dim).Append(">");
            if (s.Power > 0f)
            {
                float now = h.Stats.Damage * s.Power * SkillBook.LevelPower(lv);
                sb.Append("공격력의 ").Append((s.Power * 100f).ToString("0")).Append("%");
                if (now >= 1f) sb.Append(" (지금 약 ").Append(DamageNumbers.Short(now)).Append(")");
                sb.Append(" · ");
            }
            if (s.Cooldown > 0f) sb.Append("재사용 ").Append(s.Cooldown.ToString("0.#")).Append("초");
            if (s.Radius > 0.1f) sb.Append(" · 범위 ").Append(s.Radius.ToString("0.#")).Append("칸");
            sb.Append("</color>");

            // The resonance this skill sets up: its mark, and the element that sets it off.
            if (e != Element.None)
            {
                var next = Elements.Next(e);
                var res = Elements.Forward(e);
                sb.Append("\n<color=").Append(Dim).Append(">남기는 각인 </color>").Append(Elem(e))
                  .Append("<color=").Append(Dim).Append("> → 다음 칸이 </color>").Append(Elem(next))
                  .Append("<color=").Append(Dim).Append(">면 </color><color=#cfe9b4>").Append(Head(GameText.Resonance(res))).Append("</color>");
            }
            if (brief)
            {
                sb.Append("\n<color=").Append(Practice).Append(">숙련 ").Append(lv).Append("</color><color=").Append(Dim).Append("> · 위력 +")
                  .Append(((SkillBook.LevelPower(lv) - 1f) * 100f).ToString("0")).Append("% · 분신 ").Append(SkillBook.LevelCopies(lv)).Append("</color>");
                return sb.ToString();
            }

            // 숙련: what it gives now and when the next copy comes.
            sb.Append("\n<color=").Append(Practice).Append(">숙련 ").Append(lv).Append("</color>  위력 +").Append(((SkillBook.LevelPower(lv) - 1f) * 100f).ToString("0"))
              .Append("% · 범위 +").Append(((SkillBook.LevelArea(lv) - 1f) * 100f).ToString("0")).Append("% · 분신 ").Append(SkillBook.LevelCopies(lv));
            if (SkillBook.LevelCopies(lv) > 0) // what 분신 means, where the number is (loop 2)
                sb.Append("<color=").Append(Dim).Append("> (다른 적에게 ").Append(SkillBook.LevelCopies(lv)).Append("번 더)</color>");
            if (lv < SkillBook.MaxSkillLevel)
                sb.Append("\n<color=").Append(Dim).Append(">다음 숙련까지 ").Append((SkillBook.UsesFor(lv + 1) - uses).ToString("N0")).Append("회 · 분신 +1은 숙련 ")
                  .Append((SkillBook.LevelCopies(lv) + 1) * SkillBook.CopyEvery).Append("</color>");

            // Evolution: what it becomes and what changes.
            if (s.IsEvolution)
            {
                var from = SkillBook.Get(s.BaseId); // what it grew from (loop 2: the old name was nowhere)
                sb.Append("\n<color=").Append(Evo).Append(">진화한 스킬").Append(from != null && from != s ? " (원래 " + from.Name + ")" : "").Append("</color>");
            }
            else if (s.EvolveTo != null)
            {
                var evo = SkillBook.Get(s.EvolveTo);
                if (evo != null)
                {
                    sb.Append("\n<color=").Append(Evo).Append(">진화 → ").Append(evo.Name).Append("</color>");
                    string change = GameText.Skill(evo.Id);
                    if (!string.IsNullOrEmpty(change)) sb.Append("<color=").Append(Dim).Append(">: ").Append(change).Append("</color>");
                }
            }
            if (sigil != Sigil.None)
                sb.Append("\n<color=").Append(Glyph).Append(">보조 문양 ◈ ").Append(GameText.Sigil(sigil)).Append("</color>"); // read as part of the evolution before (loop 2)
            return sb.ToString();
        }

        /// <summary>[원소] [역할] [형태] in dim brackets, the element in its colour.</summary>
        static void Chips(StringBuilder sb, SkillDef s, Element e)
        {
            sb.Append("<color=").Append(Dim).Append(">[</color>").Append(Elem(e)).Append("<color=").Append(Dim).Append(">] [")
              .Append(Head(GameText.Role(s.Role)).Replace(" 스킬", "")).Append("] [").Append(Head(GameText.Shape(s.Shape))).Append("]</color>");
        }
    }
}
