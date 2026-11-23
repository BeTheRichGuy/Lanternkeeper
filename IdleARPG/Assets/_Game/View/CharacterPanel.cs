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
        const float W = 420f, H = 590f; // as tall as the inventory beside it
        readonly SimWorld _world;
        readonly RectTransform _win;
        readonly Text _title, _head, _dps, _hp, _left, _leftValues, _right, _rightValues;
        readonly Image _xp;
        int _stamp = -1;
        public bool Visible { get; private set; }

        public CharacterPanel(UiKit ui, SimWorld world)
        {
            _world = world;
            _win = ui.Window("Character Panel", W, H, "캐릭터", Toggle, out _title, "ui/ui2_window_iron");
            _win.anchoredPosition = new Vector2(-232f, UiKit.Lift(H)); // the left half; the inventory takes the right

            _head = UiKit.Label(_win, "", 16f, 34f, W - 32f, 30f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Fill(_win, "xp back", 16f, 68f, W - 32f, 4f, new Color(0f, 0f, 0f, 0.7f));
            _xp = UiKit.Fill(_win, "xp", 16f, 68f, 0f, 4f, new Color(1f, 0.74f, 0.32f, 0.95f));

            // the two numbers a hunter asks first, big
            UiKit.Fill(_win, "card", 12f, 80f, W - 24f, 58f, new Color(0f, 0f, 0f, 0.3f));
            UiKit.Label(_win, "초당 피해", 24f, 84f, 180f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _dps = UiKit.Label(_win, "", 24f, 100f, 180f, 32f, 28, TextAnchor.UpperLeft, UiKit.Gold, title: true);
            UiKit.Label(_win, "생명력", W / 2f + 8f, 84f, 180f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _hp = UiKit.Label(_win, "", W / 2f + 8f, 100f, 180f, 32f, 28, TextAnchor.UpperLeft, new Color(1f, 0.55f, 0.5f), title: true);

            // two columns of blocks
            float cw = (W - 48f) / 2f;
            _left = UiKit.Label(_win, "", 16f, 148f, cw, 340f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _leftValues = UiKit.Label(_win, "", 16f, 148f, cw, 340f, 12, TextAnchor.UpperRight, UiKit.Ink);
            _right = UiKit.Label(_win, "", 32f + cw, 148f, cw, 340f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _rightValues = UiKit.Label(_win, "", 32f + cw, 148f, cw, 340f, 12, TextAnchor.UpperRight, UiKit.Ink);
            foreach (var t in new[] { _left, _leftValues, _right, _rightValues }) t.lineSpacing = 1.05f;
            UiKit.Fill(_win, "column rule", W / 2f, 150f, 1f, 330f, UiKit.Line);
            Show(false);
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
            int stamp = unchecked(h.Level * 7919 + h.Xp + Mathf.RoundToInt(h.Stats.MaxHp) * 31 + Mathf.RoundToInt(h.Stats.Damage * 10f) * 17
                + h.EmberLevel * 13 + h.WickLevel * 11 + h.Rebirths * 5 + Mathf.RoundToInt(h.Stats.AttackSpeed * 100f) + h.Augments.Count * 101);
            if (stamp == _stamp) return;
            _stamp = stamp;
            var d = h.Derived;

            _title.text = "캐릭터 · " + SkillBook.ClassName(h.ClassId);
            _head.text = "<color=#ffcf5a>" + SkillBook.ClassName(h.ClassId) + "</color>   레벨 " + h.Level
                + (h.EmberLevel > 0 ? "  <color=#9a948c>+" + h.EmberLevel + " 잔불</color>" : "")
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
            Row(l, lv, "기본 피해", h.Stats.Damage.ToString("0.#"), true);
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
            Row(l, lv, "화염 피해", Pct(d.EmberPct), d.EmberPct > 0f);
            Row(l, lv, "냉기 피해", Pct(d.FrostPct), d.FrostPct > 0f);
            Row(l, lv, "번개 피해", Pct(d.ShockPct), d.ShockPct > 0f);

            Head(r, rv, "스킬");
            Row(r, rv, "스킬 범위", Pct(d.AreaPct), d.AreaPct > 0f);
            Row(r, rv, "사거리", Pct(d.RangePct), d.RangePct > 0f);
            Row(r, rv, "투사체 및 분신", "+" + d.Projectiles, d.Projectiles > 0);
            Row(r, rv, "재사용 대기시간", d.CooldownCut > 0f ? "−" + (d.CooldownCut * 100f).ToString("0") + "%" : "—", d.CooldownCut > 0f);
            Row(r, rv, "공명 피해", Pct(d.ResonancePct), d.ResonancePct > 0f);
            Row(r, rv, "연쇄 상한", _world.EffectiveChainCap.ToString(), true);
            Gap(r, rv);
            Head(r, rv, "그 밖");
            Row(r, rv, "이동 속도", h.Stats.MoveSpeed.ToString("0.#"), true);
            Row(r, rv, "경험치 획득", Pct(d.XpPct), d.XpPct > 0f);
            Row(r, rv, "마법 아이템 발견", Pct(d.MagicFindPct / 100f), d.MagicFindPct > 0f);
            Gap(r, rv);
            Head(r, rv, "성장");
            Row(r, rv, "통합 레벨", h.IntegratedLevel.ToString(), true);
            Row(r, rv, "잔불 레벨", h.EmberLevel.ToString(), h.EmberLevel > 0);
            Row(r, rv, "심지 레벨", h.WickLevel.ToString(), h.WickLevel > 0);
            Row(r, rv, "환생", h.Rebirths + "회", h.Rebirths > 0);
            Gap(r, rv);
            Head(r, rv, "증강 카드 (" + h.Augments.Count + " / " + Augments.SlotsAt(h.Level) + ")");
            foreach (var id in h.Augments)
            {
                var a = Augments.Get(id);
                if (a == null) continue;
                string col = a.Tier == AugTier.Prism ? "#d8b4ff" : a.Tier == AugTier.Gold ? "#ffcf5a" : "#cdd6e6";
                r.Append("<color=" + col + ">" + a.Name + "</color>").Append('\n');
                rv.Append("<color=#8a8178>" + Augments.TierName(a.Tier) + "</color>").Append('\n');
            }

            _left.text = l.ToString().TrimEnd('\n');
            _leftValues.text = lv.ToString().TrimEnd('\n');
            _right.text = r.ToString().TrimEnd('\n');
            _rightValues.text = rv.ToString().TrimEnd('\n');
        }
    }
}
