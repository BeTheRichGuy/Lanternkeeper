using System.Text;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 캐릭터 창 (희승 11-22: Diablo II's character screen, C). On the left half of the screen, beside the inventory on the
    /// right: name, class, level and experience at the head, then the numbers in Diablo II's blocks — 공격, 방어, 원소,
    /// 스킬, 그 밖 — and the growth beyond level (잔불, 심지, 환생). Values the hero has built up read gold; untouched ones dim.
    /// </summary>
    public sealed class CharacterPanel
    {
        const float W = 420f, H = 540f; // critique 5 P0: at 590 its foot covered the life orb and the skill bar
        readonly SimWorld _world;
        readonly RectTransform _win;
        readonly Text _title, _head, _dps, _hp, _left, _leftValues, _right, _rightValues, _explain;
        string _hoverLine = "\u0001"; // never a real line: the first frame fills the bar
        readonly Image _xp;
        int _stamp = -1;
        public bool Visible { get; private set; }

        public CharacterPanel(UiKit ui, SimWorld world)
        {
            _world = world;
            _win = ui.Window("Character Panel", W, H, "능력치", Toggle, out _title, "ui/ui2_window_iron");
            _win.anchoredPosition = new Vector2(-232f, UiKit.Lift(H)); // the left half; the inventory takes the right

            _head = UiKit.Label(_win, "", 16f, 34f, W - 32f, 30f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Fill(_win, "xp back", 16f, 68f, W - 32f, 4f, new Color(0f, 0f, 0f, 0.7f));
            _xp = UiKit.Fill(_win, "xp", 16f, 68f, 0f, 4f, new Color(1f, 0.74f, 0.32f, 0.95f));

            // the two numbers a hunter asks first, big
            UiKit.Fill(_win, "card", 12f, 80f, W - 24f, 58f, new Color(0f, 0f, 0f, 0.3f));
            UiKit.Label(_win, "초당 피해 (예상)", 24f, 84f, 180f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _dps = UiKit.Label(_win, "", 24f, 100f, 180f, 32f, 28, TextAnchor.UpperLeft, UiKit.Gold, title: true);
            UiKit.Label(_win, "생명력", W / 2f + 8f, 84f, 180f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _hp = UiKit.Label(_win, "", W / 2f + 8f, 100f, 180f, 32f, 28, TextAnchor.UpperLeft, new Color(1f, 0.55f, 0.5f), title: true);

            // two columns of blocks
            float cw = (W - 48f) / 2f;
            // Labels in light beige: dark grey on the dark plate was hard to read (UX loop 2 #19).
            var labelInk = new Color(0.784f, 0.753f, 0.659f);
            _left = UiKit.Label(_win, "", 16f, 148f, cw, 340f, 12, TextAnchor.UpperLeft, labelInk);
            _left.raycastTarget = true;
            _leftValues = UiKit.Label(_win, "", 16f, 148f, cw, 340f, 12, TextAnchor.UpperRight, UiKit.Ink);
            _right = UiKit.Label(_win, "", 32f + cw, 148f, cw, 340f, 12, TextAnchor.UpperLeft, labelInk);
            _right.raycastTarget = true;
            _rightValues = UiKit.Label(_win, "", 32f + cw, 148f, cw, 340f, 12, TextAnchor.UpperRight, UiKit.Ink);
            // 0.95 (was 1.05): with five element lines (F2) and three cards the left column runs 22 lines in 340.
            foreach (var t in new[] { _left, _leftValues, _right, _rightValues }) t.lineSpacing = 0.95f;
            UiKit.Fill(_win, "column rule", W / 2f, 150f, 1f, 330f, UiKit.Line);
            // UX plan C2: every line explains itself - hover a line and its meaning shows in this bar (Diablo II's way).
            UiKit.Fill(_win, "explain back", 12f, H - 58f, W - 24f, 48f, new Color(0f, 0f, 0f, 0.35f));
            _explain = UiKit.Label(_win, "", 20f, H - 54f, W - 40f, 42f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            Show(false);
        }

        /// <summary>The label of the line of <paramref name="t"/> under a screen point (tags stripped), or null.</summary>
        static string LineUnder(Text t, Vector2 screen)
        {
            if (string.IsNullOrEmpty(t.text)) return null;
            var cam = t.canvas != null && t.canvas.renderMode != RenderMode.ScreenSpaceOverlay ? t.canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(t.rectTransform, screen, cam)) return null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(t.rectTransform, screen, cam, out var local);
            var gen = t.cachedTextGenerator;
            float y = local.y * t.pixelsPerUnit;
            var lines = t.text.Split('\n');
            for (int i = 0; i < gen.lineCount && i < lines.Length; i++)
            {
                var li = gen.lines[i];
                if (y <= li.topY && y > li.topY - li.height)
                {
                    string plain = System.Text.RegularExpressions.Regex.Replace(lines[i], "<[^>]+>", "").Trim();
                    return plain.Length > 0 ? plain : null;
                }
            }
            return null;
        }

        /// <summary>What a line of this panel means (GameText for the item stats, a plain sentence for the rest).</summary>
        static string Explain(string line, Hero h)
        {
            string Stat(Stat s) => SkillCard.Tail(GameText.Stat(s));
            switch (line)
            {
                case "공격력": return "모든 스킬 피해의 바탕입니다. 무기의 공격력과 레벨로 오릅니다.";
                case "피해 증가": return Stat(Sim.Stat.DamagePct);
                case "공격 속도": return Stat(Sim.Stat.AttackSpeedPct);
                case "치명타 확률": return Stat(Sim.Stat.CritChancePct);
                case "치명타 피해": return Stat(Sim.Stat.CritDamagePct);
                case "생명력": return Stat(Sim.Stat.MaxHp);
                case "받는 피해 감소": return Stat(Sim.Stat.DamageReductionPct);
                case "초당 생명력 회복": return Stat(Sim.Stat.RegenPct);
                case "잿불 피해": return Stat(Sim.Stat.EmberPct);
                case "서리 피해": return Stat(Sim.Stat.FrostPct);
                case "전격 피해": return Stat(Sim.Stat.ShockPct);
                case "부패 피해": return Stat(Sim.Stat.RotPct);
                case "공허 피해": return Stat(Sim.Stat.VoidPct);
                case "스킬 범위": return Stat(Sim.Stat.AreaPct);
                case "사거리": return Stat(Sim.Stat.RangePct);
                case "투사체 및 분신": return Stat(Sim.Stat.ProjectileCount);
                case "재사용 대기시간": return Stat(Sim.Stat.CooldownPct);
                case "공명 피해": return Stat(Sim.Stat.ResonancePct);
                case "사망 연쇄 횟수": return "적이 죽으며 옆의 적에게 터지는 사망 연쇄가 이어지는 최대 횟수입니다. 아이템 옵션 '사망 연쇄 최대 횟수'와 같은 값입니다.";
                case "이동 속도": return Stat(Sim.Stat.MoveSpeedPct);
                case "경험치 획득": return Stat(Sim.Stat.XpPct);
                case "마법 아이템 발견": return Stat(Sim.Stat.MagicFindPct);
                case "통합 레벨": return "레벨 + 잔불 레벨입니다. 스킬 칸·보조 문양·궁극기가 열리는 기준입니다.";
                case "잔불 레벨": return "환생할 때 쌓인 경험치가 바뀐 영구 레벨입니다. 환생해도 사라지지 않습니다.";
                case "심지 레벨": return "통합 레벨 100 뒤로 모든 캐릭터가 함께 올리는 끝없는 레벨입니다.";
                case "환생": return "레벨을 1로 되돌리는 대신 잔불 레벨을 얻은 횟수입니다.";
            }
            foreach (var id in h.Augments)
            {
                var a = Augments.Get(id);
                if (a != null && a.Name == line) return a.Text;
            }
            return null;
        }

        public void Toggle() => Show(!Visible);

        public void Show(bool v)
        {
            Visible = v;
            _win.gameObject.SetActive(v);
            if (v) _win.SetAsLastSibling();
            _stamp = -1;
        }

        public void Render()
        {
            if (!Visible) return;
            var h = _world.Hero;
            // The line under the pointer, explained in the bar at the bottom.
            var mouse = UnityEngine.InputSystem.Mouse.current;
            string line = mouse == null ? null : LineUnder(_left, mouse.position.ReadValue()) ?? LineUnder(_right, mouse.position.ReadValue());
            if (line != _hoverLine)
            {
                _hoverLine = line;
                string text = line != null ? Explain(line, h) : null;
                _explain.text = text != null ? "<color=#ffcf5a>" + line + "</color>  " + text : "<color=#8a8178>줄에 마우스를 올리면 뜻이 여기에 나옵니다.</color>";
            }
            int stamp = unchecked(h.Level * 7919 + h.Xp + Mathf.RoundToInt(h.Stats.MaxHp) * 31 + Mathf.RoundToInt(h.Stats.Damage * 10f) * 17
                + h.EmberLevel * 13 + h.WickLevel * 11 + h.Rebirths * 5 + Mathf.RoundToInt(h.Stats.AttackSpeed * 100f) + h.Augments.Count * 101);
            if (stamp == _stamp) return;
            _stamp = stamp;
            var d = h.Derived;

            _title.text = "능력치 · " + SkillBook.ClassName(h.ClassId);
            // 성장 in the head line (11-24: with five element lines the columns overflowed): 통합 레벨 when it differs, then
            // 잔불 · 심지 · 환생 when there are any.
            _head.text = "<color=#ffcf5a>" + SkillBook.ClassName(h.ClassId) + "</color>   레벨 " + h.Level
                + "<color=#9a948c>"
                + (h.IntegratedLevel != h.Level ? "  · 통합 " + h.IntegratedLevel : "")
                + (h.EmberLevel > 0 ? "  · 잔불 " + h.EmberLevel : "")
                + (h.WickLevel > 0 ? "  · 심지 " + h.WickLevel : "")
                + (h.Rebirths > 0 ? "  · 환생 " + h.Rebirths + "회" : "")
                + "</color>"
                + "\n<color=#8a8178>경험치 " + h.Xp.ToString("N0") + " / " + h.XpToNext.ToString("N0") + "</color>";
            _xp.rectTransform.sizeDelta = new Vector2(Mathf.Round((W - 32f) * Mathf.Clamp01((float)h.Xp / Mathf.Max(1, h.XpToNext))), 4f);
            _dps.text = HeroStats.Dps(h).ToString("N0");
            _hp.text = h.Stats.MaxHp.ToString("N0");

            var l = new StringBuilder(); var lv = new StringBuilder();
            var r = new StringBuilder(); var rv = new StringBuilder();
            void Head(StringBuilder a, StringBuilder b, string name) { a.Append("<color=#ffcf5a>").Append(name).Append("</color>\n"); b.Append('\n'); }
            void Row(StringBuilder a, StringBuilder b, string name, string value, bool built) { a.Append(name).Append('\n'); b.Append(built ? "<color=#f2e6cf>" + value + "</color>" : "<color=#6f6860>" + value + "</color>").Append('\n'); }
            void Gap(StringBuilder a, StringBuilder b) { a.Append('\n'); b.Append('\n'); }
            string Pct(float f) => (f >= 0f ? "+" : "") + (f * 100f).ToString("0") + "%";

            Head(l, lv, "공격");
            Row(l, lv, "공격력", h.Stats.Damage.ToString("0.#"), true);
            Row(l, lv, "피해 증가", Pct(d.DamagePct), d.DamagePct > 0f);
            Row(l, lv, "공격 속도", h.Stats.AttackSpeed.ToString("0.##") + "/초", true);
            Row(l, lv, "치명타 확률", (d.CritChance * 100f).ToString("0.#") + "%", d.CritChance > 0.05f);
            Row(l, lv, "치명타 피해", "×" + d.CritMult.ToString("0.##"), d.CritMult > 1.5f);
            Gap(l, lv);
            Head(l, lv, "방어");
            Row(l, lv, "생명력", h.Stats.MaxHp.ToString("N0"), true);
            Row(l, lv, "받는 피해 감소", (d.DamageReduction * 100f).ToString("0") + "%", d.DamageReduction > 0f);
            Row(l, lv, "초당 생명력 회복", (d.RegenPctPerSec * 100f).ToString("0.#") + "%", d.RegenPctPerSec > 0f);
            Gap(l, lv);
            Head(l, lv, "원소");
            // One set of element names everywhere (UX audit #8: items said 잿불/서리/전격, this panel 화염/냉기/번개).
            Row(l, lv, "잿불 피해", Pct(d.EmberPct), d.EmberPct > 0f);
            Row(l, lv, "서리 피해", Pct(d.FrostPct), d.FrostPct > 0f);
            Row(l, lv, "전격 피해", Pct(d.ShockPct), d.ShockPct > 0f);
            Row(l, lv, "부패 피해", Pct(d.RotPct), d.RotPct > 0f); // 11-24 F2: all five elements
            Row(l, lv, "공허 피해", Pct(d.VoidPct), d.VoidPct > 0f);

            Head(r, rv, "스킬");
            Row(r, rv, "스킬 범위", Pct(d.AreaPct), d.AreaPct > 0f);
            Row(r, rv, "사거리", Pct(d.RangePct), d.RangePct > 0f);
            Row(r, rv, "투사체 및 분신", "+" + d.Projectiles, d.Projectiles > 0);
            Row(r, rv, "재사용 대기시간", d.CooldownCut > 0f ? "−" + (d.CooldownCut * 100f).ToString("0") + "%" : "변화 없음", d.CooldownCut > 0f);
            Row(r, rv, "공명 피해", Pct(d.ResonancePct), d.ResonancePct > 0f);
            Row(r, rv, "사망 연쇄 횟수", _world.EffectiveChainCap.ToString(), true);
            Gap(r, rv);
            Head(r, rv, "그 밖");
            Row(r, rv, "이동 속도", h.Stats.MoveSpeed.ToString("0.#"), true);
            Row(r, rv, "경험치 획득", Pct(d.XpPct), d.XpPct > 0f);
            Row(r, rv, "마법 아이템 발견", Pct(d.MagicFindPct / 100f), d.MagicFindPct > 0f);
            Gap(r, rv);
            // The cards in the right column now that 성장 lives in the head line (the left one holds five element lines).
            Head(r, rv, "증강 카드 (" + h.Augments.Count + " / " + Augments.SlotsAt(h.Level) + ")");
            foreach (var id in h.Augments)
            {
                var a = Augments.Get(id);
                if (a == null) continue;
                string col = a.Tier == AugTier.Prism ? "#d8b4ff" : a.Tier == AugTier.Gold ? "#ffcf5a" : "#cdd6e6";
                r.Append("<color=" + col + ">" + a.Name + "</color>").Append('\n');
                rv.Append("<color=#8a8178>" + Augments.TierName(a.Tier) + " 등급</color>").Append('\n');
            }
            _left.text = l.ToString().TrimEnd('\n');
            _leftValues.text = lv.ToString().TrimEnd('\n');
            _right.text = r.ToString().TrimEnd('\n');
            _rightValues.text = rv.ToString().TrimEnd('\n');
        }
    }
}
