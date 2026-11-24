using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 도감 (GDD §12.5, 재기획 §5.9, plan E4) on the GUI canvas, four tabs and one job each (희승 11-24: "한 탭에서는 하나의
    /// 기능만"). 적: a portrait grid per act (the monster walks in place; never-killed ones are black silhouettes), kills and
    /// medal gems, lore on hover. 스킬 진화: a card per tree skill with its evolution, the practice bar to 10 and whether the
    /// evolution node is taken. 공명: the mastery tier of each resonance. 도전 과제: a grid of goals, gold when done.
    /// J opens it on 적 and closes it.
    /// </summary>
    public sealed class CodexPanel
    {
        const float W = 880f, H = 480f, Top = 62f, PageW = W - 32f;
        const int Tabs = 4;
        readonly UiKit _ui;
        readonly SimWorld _world;
        readonly RectTransform _win, _actRow;
        readonly RectTransform[] _roots = new RectTransform[Tabs]; // one container per tab, rebuilt only while shown
        readonly Text[] _tabText = new Text[Tabs];
        readonly Image[] _tabBar = new Image[Tabs];
        readonly Text[] _actText;
        readonly Text _info;
        readonly List<GameObject> _bits = new List<GameObject>();
        readonly List<(Image img, string key)> _walkers = new List<(Image, string)>();
        RectTransform _page; // the active tab's container
        public bool Visible { get; private set; }

        int _tab, _act, _stamp = -1;
        float _refresh, _walkT = -1f;
        string _hint = "";

        static readonly string[] TabNames = { "적", "스킬 진화", "공명", "도전 과제" };
        // Opaque dark card colours: the GUI blends in linear space, so even 3% white over the near-black body reads mid-grey.
        static readonly Color Card = new Color(0.075f, 0.068f, 0.062f, 0.95f), CardLit = new Color(0.17f, 0.12f, 0.06f, 0.95f), CardBoss = new Color(0.12f, 0.075f, 0.065f, 0.95f);

        public CodexPanel(UiKit ui, SimWorld world)
        {
            _ui = ui;
            _world = world;
            _win = ui.Window("Codex Panel", W, H, "도감", Close, out _, "ui/ui2_window_iron");
            // Same tab style as the ring panel: buttons under the title, a gold bar under the active one, a rule below.
            for (int i = 0; i < Tabs; i++)
            {
                int tab = i;
                _tabText[i] = ui.Button(_win, TabNames[i], 16f + i * 124f, 32f, 120f, 22f, () => { _tab = tab; _stamp = -1; }).GetComponentInChildren<Text>();
                _tabBar[i] = UiKit.Fill(_win, "tab bar", 16f + i * 124f, 52f, 120f, 3f, UiKit.Gold);
                _roots[i] = UiKit.Rect(_win, "tab " + TabNames[i], 16f, Top, PageW, H - Top - 28f);
            }
            UiKit.Fill(_win, "tab rule", 12f, 54f, W - 24f, 1f, UiKit.Line);
            // act picker, at the right of the enemy tab's header line
            int acts = EnemyRoster.Acts.Length;
            _actRow = UiKit.Rect(_roots[0], "acts", PageW - acts * 52f, 0f, acts * 52f, 20f);
            _actText = new Text[acts];
            for (int a = 0; a < acts; a++)
            {
                int act = a;
                _actText[a] = ui.Button(_actRow, "막 " + EnemyRoster.Acts[a].Numeral, a * 52f, 0f, 48f, 20f, () => { _act = act; _stamp = -1; }).GetComponentInChildren<Text>();
            }
            _info = UiKit.Label(_win, "", 16f, H - 22f, W - 130f, 16f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            UiKit.Label(_win, "(J) 닫기", W - 112f, H - 22f, 96f, 16f, 12, TextAnchor.UpperRight, UiKit.Dim);
            SetVisible(false);
        }

        /// <summary>J: open on the enemy tab, or close.</summary>
        public void Toggle() { if (!Visible) _tab = 0; SetVisible(!Visible); }

        /// <summary>Esc and other panels close it outright.</summary>
        public void Close() { if (Visible) SetVisible(false); }

        void SetVisible(bool v)
        {
            Visible = v;
            _win.gameObject.SetActive(v);
            if (v) { _win.SetAsLastSibling(); _act = Mathf.Min(_world.ActLoop, EnemyRoster.Acts.Length - 1); }
            _stamp = -1;
        }

        public void Render(float dt)
        {
            if (!Visible) return;
            // Monsters walk in place, eight frames a second.
            float t = Mathf.Floor(Time.unscaledTime * 8f) / 8f;
            if (t != _walkT)
            {
                _walkT = t;
                foreach (var (img, key) in _walkers) img.sprite = _ui.Catalog.GetAnim(key, "walk", 0, t, true, 0.7f, out _);
            }
            _refresh -= dt;
            var h = _world.Hero;
            int stamp = unchecked(_tab * 1000003 + _act * 10007 + h.Evolved.Count * 101 + h.Achieved.Count * 7 + h.TreeNodes.Count);
            if (stamp == _stamp && (_refresh > 0f || _tab == 3)) return; // goals change only through the stamp
            _stamp = stamp;
            _refresh = 2f; // kills, practice and resonance counts tick up while it is open
            Rebuild();
        }

        T Keep<T>(T c) where T : Component { _bits.Add(c.gameObject); return c; }

        void Rebuild()
        {
            foreach (var g in _bits) Object.Destroy(g);
            _bits.Clear();
            _walkers.Clear();
            _hint = "";
            _page = _roots[_tab];
            for (int i = 0; i < Tabs; i++)
            {
                _tabText[i].color = i == _tab ? UiKit.Gold : UiKit.Dim;
                _tabBar[i].enabled = i == _tab;
                _roots[i].gameObject.SetActive(i == _tab);
            }
            for (int a = 0; a < _actText.Length; a++) _actText[a].color = a == _act ? UiKit.Gold : UiKit.Dim;
            if (_tab == 0) Enemies();
            else if (_tab == 1) Evolutions();
            else if (_tab == 2) Resonances();
            else Achievements();
            _info.text = _hint;
        }

        // ---------- 적 ----------

        void Enemies()
        {
            var act = EnemyRoster.Acts[_act];
            var defs = new List<EnemyDef>(act.Ranks) { act.Elite, act.Boss };
            Keep(UiKit.Label(_page, "막 " + act.Numeral + " · " + act.Name, 0f, 2f, 520f, 16f, 12, TextAnchor.UpperLeft, UiKit.Ink));
            _hint = "메달: 100 · 1,000 · 10,000 처치, 메달당 해당 적에게 주는 피해 +1%";
            const float cw = 206f, ch = 178f;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                float x = (i % 4) * (cw + 8f), y = 26f + (i / 4) * (ch + 4f);
                int kills = _world.KillsOf(d.Id);
                int medal = SimWorld.MedalOf(kills);
                bool known = kills > 0, boss = d == act.Boss, elite = d == act.Elite;
                var card = Keep(UiKit.Fill(_page, "enemy card", x, y, cw, ch, boss ? CardBoss : Card));
                card.raycastTarget = true;
                var pool = Keep(UiKit.Fill(_page, "pool", x + 23f, y + 6f, 160f, 140f, new Color(1f, 0.72f, 0.4f, known ? 0.16f : 0.05f)));
                pool.sprite = UiKit.Glow(_ui, 64);
                string key = WorldView.EnemyArt(_ui.Catalog, d.Id, out var tint, out _);
                var img = Keep(_ui.Sprite(_page, _ui.Catalog.Has(key) ? key : "ui/dot", x + 39f, y + 6f, 128f, 128f));
                img.color = known ? tint : new Color(0f, 0f, 0f, 0.9f);
                if (known) _walkers.Add((img, key));
                string name = known ? d.Name : "???";
                Keep(UiKit.Label(_page, (boss ? "<color=#ff9a7a>" : elite ? "<color=#ffcf5a>" : "") + name + (boss || elite ? "</color>" : ""),
                    x + 4f, y + 138f, cw - 8f, 16f, 12, TextAnchor.UpperCenter, UiKit.Ink));
                Keep(UiKit.Label(_page, known ? kills.ToString("N0") + " 처치" : "만난 적 없음", x + 4f, y + 157f, cw - 8f, 16f, 12, TextAnchor.UpperCenter, UiKit.Dim));
                if (medal > 0)
                {
                    var gem = Keep(_ui.Sprite(_page, "ui/ui2_gem", x + cw - 22f, y + 8f, 14f, 14f));
                    gem.color = medal == 3 ? new Color(1f, 0.83f, 0.4f) : medal == 2 ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.82f, 0.55f, 0.36f);
                }
                if (boss || elite) Keep(UiKit.Label(_page, boss ? "보스" : "정예", x + 6f, y + 6f, 44f, 16f, 12, TextAnchor.UpperLeft, boss ? new Color(1f, 0.6f, 0.48f) : UiKit.Gold));
                var p = card.gameObject.AddComponent<UiPointer>();
                string lore = known ? d.Lore : "아직 처치하지 못했다.";
                p.Enter = () => _info.text = "<color=#e8dcc8>" + name + "</color>  <i>" + lore + "</i>";
                p.Exit = () => _info.text = _hint;
            }
        }

        // ---------- 스킬 진화 ----------

        void Evolutions()
        {
            var h = _world.Hero;
            var cards = new List<TreeNode>();
            foreach (var n in SkillTree.For(h.ClassId).Nodes) if (n.Kind == TreeNodeKind.Evolution) cards.Add(n);
            int done = 0;
            foreach (var n in cards) if (SkillTree.Has(h, n.Id)) done++;
            Keep(UiKit.Label(_page, "스킬 트리에서 진화 칸을 찍으면 스킬이 진화한다 · 숙련 " + SkillTree.EvoMastery + " 필요   <color=#ffcf5a>" + done + " / " + cards.Count + "</color>",
                0f, 2f, PageW, 16f, 12, TextAnchor.UpperLeft, UiKit.Dim));
            const float cw = 276f, ch = 56f;
            for (int i = 0; i < cards.Count; i++)
            {
                var node = cards[i];
                var skill = SkillBook.Get(node.SkillId);
                if (skill == null) continue;
                var evo = skill.EvolveTo != null ? SkillBook.Get(skill.EvolveTo) : null;
                float x = (i % 3) * (cw + 11f), y = 24f + (i / 3) * (ch + 4f);
                bool taken = SkillTree.Has(h, node.Id);
                int mastery = SkillTree.MasteryOf(h, node.SkillId);
                var card = Keep(UiKit.Fill(_page, "evo card", x, y, cw, ch, taken ? CardLit : Card));
                card.raycastTarget = true;
                Keep(UiKit.Fill(_page, "edge", x, y + ch - 1f, cw, 1f, taken ? new Color(1f, 0.8f, 0.4f, 0.5f) : UiKit.Line));
                Icon("icon/skill_" + skill.Id, x + 6f, y + 12f, taken ? Color.white : new Color(0.8f, 0.8f, 0.8f));
                Keep(UiKit.Label(_page, "→", x + 40f, y + 20f, 14f, 16f, 12, TextAnchor.UpperCenter, UiKit.Dim));
                // Not evolved yet: its own art (or the base skill's) dimmed with a small lock, in an evolution frame, never a white square.
                Icon("icon/skill_" + (evo?.Id ?? skill.Id), x + 54f, y + 12f, taken ? Color.white : new Color(0.42f, 0.42f, 0.42f), "icon/skill_" + skill.Id);
                Frame(x + 54f, y + 12f, 32f, 32f, taken ? new Color(1f, 0.8f, 0.4f, 0.9f) : new Color(0.55f, 0.5f, 0.44f, 0.6f));
                if (!taken) Lock(x + 54f + 32f - 11f, y + 12f + 32f - 12f);
                string evoName = evo?.Name ?? skill.Name;
                Keep(UiKit.Label(_page, (taken ? "<color=#ffcf5a>" + evoName + "</color>" : evoName) + "  <color=#8a8178>" + skill.Name + "</color>",
                    x + 94f, y + 6f, cw - 98f, 16f, 12, TextAnchor.UpperLeft, UiKit.Ink));
                // practice bar to the evolution threshold
                float k = Mathf.Clamp01(mastery / (float)SkillTree.EvoMastery);
                Keep(UiKit.Fill(_page, "bar back", x + 94f, y + 26f, 120f, 3f, new Color(0f, 0f, 0f, 0.6f)));
                Keep(UiKit.Fill(_page, "bar", x + 94f, y + 26f, 120f * k, 3f, k >= 1f ? UiKit.Gold : new Color(0.6f, 0.8f, 1f)));
                string state = taken ? "<color=#9fd28a>진화 완료</color>" : mastery >= SkillTree.EvoMastery ? "<color=#ffcf5a>트리에서 습득 가능</color>" : "숙련 " + mastery + " / " + SkillTree.EvoMastery;
                Keep(UiKit.Label(_page, state, x + 94f, y + 34f, cw - 98f, 16f, 12, TextAnchor.UpperLeft, UiKit.Dim));
                var p = card.gameObject.AddComponent<UiPointer>();
                p.Enter = () => _info.text = "<color=#ffcf5a>" + evoName + "</color>  " + skill.Name + "의 진화 · 숙련 " + mastery + " / " + SkillTree.EvoMastery;
                p.Exit = () => _info.text = _hint;
            }
        }

        void Frame(float x, float y, float w, float h, Color c)
        {
            Keep(UiKit.Fill(_page, "frame", x, y, w, 1f, c));
            Keep(UiKit.Fill(_page, "frame", x, y + h - 1f, w, 1f, c));
            Keep(UiKit.Fill(_page, "frame", x, y, 1f, h, c));
            Keep(UiKit.Fill(_page, "frame", x + w - 1f, y, 1f, h, c));
        }

        /// <summary>A small lock drawn from rectangles (no lock art, and the font may not carry the glyph).</summary>
        void Lock(float x, float y)
        {
            var c = new Color(0.86f, 0.82f, 0.74f);
            Keep(UiKit.Fill(_page, "lock shackle", x + 2f, y, 5f, 1f, c));
            Keep(UiKit.Fill(_page, "lock shackle", x + 2f, y, 1f, 4f, c));
            Keep(UiKit.Fill(_page, "lock shackle", x + 6f, y, 1f, 4f, c));
            Keep(UiKit.Fill(_page, "lock back", x - 1f, y + 4f, 11f, 8f, new Color(0f, 0f, 0f, 0.7f)));
            Keep(UiKit.Fill(_page, "lock body", x, y + 5f, 9f, 6f, c));
        }

        /// <summary>An evolution without its own art borrows its base skill's (a white square read as unfinished, UX audit #23).</summary>
        void Icon(string key, float x, float y, Color tint, string fallback = null)
        {
            string use = _ui.Catalog.Has(key) ? key : fallback != null && _ui.Catalog.Has(fallback) ? fallback : null;
            if (use == null) return;
            var img = Keep(_ui.Sprite(_page, use, x, y, 32f, 32f));
            img.color = tint;
        }

        // ---------- 공명 ----------

        void Resonances()
        {
            var h = _world.Hero;
            Keep(UiKit.Label(_page, "공명 숙련", 0f, 2f, 200f, 16f, 14, TextAnchor.UpperLeft, UiKit.Gold, title: true));
            Keep(UiKit.Label(_page, "횟수를 채우면 그 공명의 피해가 I단계 +15%, II단계부터 +30%", 0f, 22f, PageW, 16f, 12, TextAnchor.UpperLeft, UiKit.Dim));
            var res = new[] { Resonance.Shatter, Resonance.Overload, Resonance.Plague, Resonance.Devour, Resonance.Collapse };
            int[] steps = { 300, 2000, 10000 }; // Elements.MasteryTier's thresholds
            string[] roman = { "I", "II", "III" };
            const float rh = 56f, bx = 270f, bw = 420f;
            for (int i = 0; i < res.Length; i++)
            {
                float y = 48f + i * (rh + 6f);
                int n = h.ResonanceCounts[(int)res[i]];
                int tier = Elements.MasteryTier(n);
                Keep(UiKit.Fill(_page, "res card", 0f, y, PageW, rh, tier > 0 ? CardLit : Card));
                Keep(UiKit.Label(_page, (tier > 0 ? "<color=#ffcf5a>" : "<color=#cfc6b8>") + FxView.ResonanceName(res[i]) + "</color>",
                    16f, y + 8f, 240f, 18f, 14, TextAnchor.UpperLeft, UiKit.Ink));
                Keep(UiKit.Label(_page, tier == 0 ? "단계 없음" : roman[tier - 1] + "단계 · 피해 +" + (tier == 1 ? 15 : 30) + "%", 16f, y + 30f, 240f, 16f, 12, TextAnchor.UpperLeft,
                    tier > 0 ? UiKit.Gold : UiKit.Dim));
                float k = tier >= 3 ? 1f : Mathf.Clamp01(n / (float)steps[tier]);
                Keep(UiKit.Fill(_page, "bar back", bx, y + 16f, bw, 6f, new Color(0f, 0f, 0f, 0.6f)));
                Keep(UiKit.Fill(_page, "bar", bx, y + 16f, bw * k, 6f, tier >= 3 ? UiKit.Gold : new Color(0.6f, 0.8f, 1f)));
                Keep(UiKit.Label(_page, tier >= 3 ? "최대 단계" : n.ToString("N0") + " / " + steps[tier].ToString("N0"), bx, y + 28f, bw, 16f, 12, TextAnchor.UpperLeft, UiKit.Dim));
                if (tier < 3)
                    Keep(UiKit.Label(_page, "→ " + roman[tier] + "단계", bx + bw + 8f, y + 10f, PageW - bx - bw - 16f, 16f, 12, TextAnchor.UpperRight, UiKit.Ink));
            }
        }

        // ---------- 도전 과제 ----------

        void Achievements()
        {
            var h = _world.Hero;
            var all = SimWorld.Achievements;
            int done = 0;
            foreach (var a in all) if (h.Achieved.Contains(a.Id)) done++;
            Keep(UiKit.Label(_page, "도전 과제  <color=#ffcf5a>" + done + " / " + all.Length + "</color>   <color=#8a8178>등불 색 [X]: " + SimWorld.LanternColors[h.LanternColor].name + "</color>",
                0f, 2f, PageW, 16f, 12, TextAnchor.UpperLeft, UiKit.Ink));
            const float cw = 280f, ch = 42f;
            for (int i = 0; i < all.Length; i++)
            {
                var a = all[i];
                bool ok = h.Achieved.Contains(a.Id);
                float x = (i % 3) * (cw + 4f), y = 22f + (i / 3) * (ch + 3f);
                Keep(UiKit.Fill(_page, "goal", x, y, cw, ch, ok ? CardLit : Card));
                var mark = Keep(UiKit.Fill(_page, "mark", x + 8f, y + 14f, 14f, 14f, ok ? UiKit.Gold : new Color(0.3f, 0.28f, 0.26f)));
                mark.sprite = UiKit.Diamond(_ui, 14);
                Keep(UiKit.Label(_page, ok ? "<color=#ffcf5a>" + a.Name + "</color>" : a.Name, x + 28f, y + 3f, cw - 32f, 16f, 12, TextAnchor.UpperLeft, UiKit.Ink));
                Keep(UiKit.Label(_page, a.Text + (a.Lantern > 0 ? " · 등불 색 " + SimWorld.LanternColors[a.Lantern].name : ""), x + 28f, y + 22f, cw - 32f, 16f, 12, TextAnchor.UpperLeft, UiKit.Dim));
            }
        }
    }
}
