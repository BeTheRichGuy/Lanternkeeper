using System;
using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Return summary (GDD §13, 재기획 §7.2 귀환 전리품 개봉식) on the GUI canvas: how long the lantern burned alone, then
    /// the best finds opened one by one (a dark cell, then the piece in a burst of its rarity's light), and the tallies
    /// rolling up — kills, experience, levels, ash, practice, wick. The first key or click opens everything at once; the
    /// next closes it (or it closes by itself after 25 s).
    /// </summary>
    public sealed class OfflinePanel
    {
        const float W = 560f, H = 380f, Reveal = 0.55f, Roll = 1.2f;
        readonly RectTransform _win, _veil;
        readonly Text _away, _tallies, _foot, _more;
        readonly SlotView[] _cells = new SlotView[5];
        readonly Image[] _burst = new Image[5];
        readonly ItemTooltip _tip;
        readonly List<Item> _best = new List<Item>();
        OfflineReport _r;
        float _shownFor, _skipAt = -1f;
        int _opened, _talliesKey = -1;
        bool _closing;
        public bool Visible { get; private set; }

        // ---------- 결정 카드 (재기획 §7.3): choices that came up while away, the recommended one ready on a button ----------
        readonly UiKit _ui;
        readonly RectTransform _cards;
        readonly List<GameObject> _cardBits = new List<GameObject>();
        /// <summary>Raised after a card's action changed the world (the game saves soon).</summary>
        public Action OnChanged;
        /// <summary>The cards are showing: clicks go to them, not to closing the summary.</summary>
        public bool CardsUp => Visible && _cards.gameObject.activeSelf;

        public OfflinePanel(UiKit ui)
        {
            _ui = ui;
            // A dark veil under the window: while it is up, clicks never reach the HUD or the world.
            _veil = UiKit.Rect(ui.Root, "Offline Veil", 0f, 0f, 0f, 0f);
            _veil.anchorMin = Vector2.zero; _veil.anchorMax = Vector2.one;
            _veil.offsetMin = _veil.offsetMax = Vector2.zero;
            _veil.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            _win = ui.Window("Offline Panel", W, H, "등불이 혼자 타오르는 동안", null, out _, "ui/ui2_window_iron");
            var x0 = _win.Find("button ×");
            if (x0 != null) x0.gameObject.SetActive(false); // any key or click closes it; no corner X
            _away = UiKit.Label(_win, "", 20f, 32f, W - 40f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            var pool = UiKit.Fill(_win, "light", W / 2f - 220f, 40f, 440f, 140f, new Color(1f, 0.72f, 0.4f, 0.18f));
            pool.sprite = UiKit.Glow(ui, 64);
            _tip = new ItemTooltip(ui, "Offline Tooltip");
            for (int i = 0; i < 5; i++)
            {
                float x = W / 2f - (5 * 72f - 16f) / 2f + i * 72f;
                _burst[i] = UiKit.Fill(_win, "burst", x - 30f, 50f, 116f, 116f, Color.clear);
                _burst[i].sprite = UiKit.Glow(ui, 64);
                var sv = _cells[i] = new SlotView(ui, _win, x, 80f, 56f, 56f, SlotStyle.Socket, "ui/ui2_socket_medallion");
                sv.Pointer.Enter += () => { if (sv.Item != null) { _tip.Show(sv.Item, "가져온 전리품"); _tip.PlaceBeside(sv.Root); } };
                sv.Pointer.Exit += () => _tip.Hide();
            }
            _more = UiKit.Label(_win, "", 20f, 146f, W - 40f, 14f, 12, TextAnchor.UpperCenter, UiKit.Dim);
            UiKit.Fill(_win, "rule", 20f, 168f, W - 40f, 1f, UiKit.Line);
            _tallies = UiKit.Label(_win, "", 40f, 178f, W - 80f, 110f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            _tallies.lineSpacing = 1.25f;
            _foot = UiKit.Label(_win, "", 20f, H - 22f, W - 40f, 14f, 12, TextAnchor.UpperCenter, UiKit.Dim);
            _cards = UiKit.Rect(_win, "cards", 20f, 252f, W - 40f, 96f);
            Hide();
        }

        public void Show(OfflineReport r, SimWorld world)
        {
            _r = r;
            BuildCards(world);
            var t = TimeSpan.FromSeconds(r.Seconds);
            _away.text = Duration(t) + (r.Seconds > r.CappedSeconds ? "  <color=#6f6860>(기름 저장고 한도 " + Duration(TimeSpan.FromSeconds(r.CappedSeconds)) + "까지만)</color>" : "")
                + "  ·  도달한 최고 층에서 계속 사냥했습니다";
            // The best finds: legendaries first (rarest, most ★ first), then new equips, five at most.
            _best.Clear();
            var all = new List<Item>(r.Legendaries);
            all.Sort((a, b) => b.Rarity != a.Rarity ? b.Rarity.CompareTo(a.Rarity) : b.GreaterCount.CompareTo(a.GreaterCount));
            foreach (var it in all) if (_best.Count < 5 && !_best.Contains(it)) _best.Add(it);
            foreach (var it in r.Equipped) if (_best.Count < 5 && !_best.Contains(it)) _best.Add(it);
            var distinct = new HashSet<Item>(r.Legendaries);
            distinct.UnionWith(r.Equipped); // a legendary still worn is in both lists
            int extra = distinct.Count - _best.Count;
            _more.text = _best.Count == 0 ? "주목할 만한 장비는 없었습니다" : extra > 0 ? "외 " + extra + "개" : "";
            for (int i = 0; i < 5; i++)
            {
                _cells[i].Root.gameObject.SetActive(i < _best.Count);
                _cells[i].Show(null, "icon/item_rune"); // closed: a faint shape until it opens
                _burst[i].color = Color.clear;
            }
            _opened = 0;
            _talliesKey = -1;
            _shownFor = 0f;
            _skipAt = -1f;
            _closing = false;
            _foot.text = "클릭하거나 아무 키나 누르기 · 한 번에 개방";
            Visible = true;
            _veil.gameObject.SetActive(true);
            _veil.SetAsLastSibling();
            _win.gameObject.SetActive(true);
            _win.SetAsLastSibling();
        }

        /// <summary>Up to three cards; doing nothing costs nothing (재기획 §7.3).</summary>
        void BuildCards(SimWorld w)
        {
            foreach (var g in _cardBits) UnityEngine.Object.Destroy(g);
            _cardBits.Clear();
            var cards = new List<(string title, string body, string button, Action act)>();
            // A better ring order (고리 시험 from this moment).
            List<RingTrial.Result> trial = null;
            if (SkillBook.SlotsAt(w.Hero.IntegratedLevel) > 1)
            {
                try { trial = RingTrial.Run(w); }
                catch (Exception e) { Debug.LogWarning("[Lanternkeeper] ring trial on return failed: " + e.Message); }
            }
            if (trial != null && trial.Count > 1)
            {
                var cur = new string[4];
                for (int i = 0; i < 4; i++) cur[i] = w.Hero.Ring.Slots[i]?.BaseId;
                string now = string.Join("|", cur);
                double baseDmg = 0;
                foreach (var t in trial) if (string.Join("|", t.Order) == now) baseDmg = t.Damage;
                var best = trial[0];
                double gain = baseDmg > 0 ? best.Damage / baseDmg - 1.0 : 0.0;
                if (gain >= 0.08 && string.Join("|", best.Order) != now)
                    cards.Add(("스킬 순서", "변경 시 피해 <color=#9fd28a>+" + (gain * 100).ToString("0") + "%</color>\n<color=#8a8178>순서 시험, 40초 기준</color>", "적용", () => w.SetCustomOrder(best.Order)));
            }
            // Points left in the tree with auto off.
            int left = SkillTree.Points(w.Hero) - SkillTree.Spent(w.Hero);
            if (!w.Hero.TreeAuto && left > 0)
                cards.Add(("스킬 트리", "남은 포인트 <color=#ffcf5a>" + left + "</color>\n<color=#8a8178>자동 분배가 꺼져 있다</color>", "자동 분배", () => w.SetTreeAuto(true)));
            // The bag overflowed.
            if (w.Bag.Items.Count > Bag.Capacity)
                cards.Add(("가방 초과", "<color=#ffb070>" + w.Bag.Items.Count + " / " + Bag.Capacity + "</color>\n<color=#8a8178>잠금 빼고 약한 것부터</color>", "일괄 분해", () => w.SalvageAll()));
            float cw = (W - 40f - 2 * 8f) / 3f;
            for (int i = 0; i < cards.Count && i < 3; i++)
            {
                var c = cards[i];
                float x = i * (cw + 8f);
                var back = UiKit.Fill(_cards, "card", x, 0f, cw, 90f, new Color(0.075f, 0.068f, 0.062f, 1f));
                _cardBits.Add(back.gameObject);
                var bar = UiKit.Fill(back.transform, "bar", 0f, 0f, cw, 2f, UiKit.Gold);
                UiKit.Label(back.transform, "<color=#ffcf5a>" + c.title + "</color>", 8f, 8f, cw - 16f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink);
                UiKit.Label(back.transform, c.body, 8f, 26f, cw - 16f, 34f, 12, TextAnchor.UpperLeft, UiKit.Ink);
                Button b = null;
                b = _ui.Button(back.transform, c.button, 8f, 64f, cw - 16f, 20f, () =>
                {
                    c.act();
                    b.interactable = false;
                    b.GetComponentInChildren<Text>().text = "완료";
                    bar.color = UiKit.Good;
                    OnChanged?.Invoke();
                });
            }
            _cards.gameObject.SetActive(false); // they come up once the finds are open
        }

        void Hide()
        {
            Visible = false;
            _tip.Hide();
            foreach (var c in _cells) c.ClearHover();
            _win.gameObject.SetActive(false);
            _veil.gameObject.SetActive(false);
        }

        static string Duration(TimeSpan t) =>
            t.TotalHours >= 1 ? $"{(int)t.TotalHours}시간 {t.Minutes}분" : $"{Math.Max(1, t.Minutes)}분";

        public void Update(float dt, bool anyInput)
        {
            if (!Visible) return;
            _shownFor += Mathf.Min(dt, 0.1f);
            float end = Mathf.Max(0.4f + _best.Count * Reveal, Roll + 0.2f);
            if (anyInput && _shownFor > 0.3f)
            {
                // The first press opens everything; a later one (after a beat) closes.
                if (_shownFor < end && _skipAt < 0f) { _skipAt = _shownFor = end; }
                else if (_skipAt < 0f || _shownFor - _skipAt > 0.3f) { Hide(); return; }
            }
            if (_shownFor > 25f) { Hide(); return; }

            // Open the finds one by one; each opens in a burst of its rarity's light that fades.
            for (int i = 0; i < _best.Count; i++)
            {
                float at = 0.4f + i * Reveal;
                if (i >= _opened && _shownFor >= at) { _cells[i].Show(_best[i], null); _opened = i + 1; }
                float k = _shownFor - at;
                var rc = ItemStyle.Color(_best[i].Rarity);
                float glow = k < 0f ? 0f : Mathf.Round(Mathf.Lerp(0.9f, 0.25f, Mathf.Clamp01(k / 0.5f)) * 10f) / 10f;
                var c = new Color(rc.r, rc.g, rc.b, glow);
                if (_burst[i].color != c) _burst[i].color = c;
            }
            // Tallies roll up over the first second, then hold.
            float roll = Mathf.Clamp01(_shownFor / Roll);
            int key = Mathf.RoundToInt(roll * 30f);
            if (key != _talliesKey)
            {
                _talliesKey = key;
                var r = _r;
                float e = 1f - (1f - roll) * (1f - roll);
                string lv = r.LevelAfter > r.LevelBefore ? "   <color=#ffd070>Lv " + r.LevelBefore + " → " + r.LevelAfter + "</color>" : "";
                _tallies.text =
                    "처치  <color=#ffcf5a>" + Mathf.RoundToInt(r.Kills * e).ToString("N0") + "</color>        경험치  <color=#ffcf5a>+" + ((long)(r.XpGained * e)).ToString("N0") + "</color>" + lv + "\n"
                    + "획득 장비  <color=#ffcf5a>" + Mathf.RoundToInt(r.ItemsKept * e) + "</color>        잿 파편  <color=#ffcf5a>+" + Mathf.RoundToInt(r.AshGained * e).ToString("N0") + "</color>\n"
                    + (r.SkillLevelsGained > 0 ? "<color=#a8d0ff>스킬 숙련  +" + r.SkillLevelsGained + "</color>        " : "")
                    + (r.WickLevelsGained > 0 ? "<color=#ffe7a0>심지  +" + r.WickLevelsGained + "</color>" : "");
            }
            if (!_closing && _shownFor >= end)
            {
                _closing = true;
                _foot.text = _cardBits.Count > 0 ? "카드를 넘기지 않아도 불이익이 없습니다 · 아무 키나 누르면 닫힙니다" : "아무 키나 누르면 닫힙니다";
                _cards.gameObject.SetActive(_cardBits.Count > 0);
            }
        }
    }
}
