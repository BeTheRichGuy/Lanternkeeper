using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 도감 (GDD §12.5, 재기획 §5.9) on the GUI canvas, three tabs. 진화: a card per tree skill with its evolution, the
    /// practice bar to 10 and whether the evolution node is taken, plus the resonance mastery tiers. 적: a portrait grid
    /// per act (the monster walks in place; never-killed ones are black silhouettes), kills and medal gems, lore on hover.
    /// 도전 과제: a grid of goals, gold when done. J opens and closes it.
    /// </summary>
    public sealed class CodexPanel
    {
        const float W = 880f, H = 480f, Top = 58f;
        readonly UiKit _ui;
        readonly SimWorld _world;
        readonly RectTransform _win, _page, _actRow;
        readonly Text[] _tabText = new Text[3];
        readonly Text[] _actText;
        readonly Text _info;
        readonly List<GameObject> _bits = new List<GameObject>();
        readonly List<(Image img, string key)> _walkers = new List<(Image, string)>();
        public bool Visible { get; private set; }

        int _tab, _act, _stamp = -1;
        float _refresh, _walkT = -1f;

        static readonly string[] TabNames = { "진화", "적", "도전 과제" };
        // Opaque dark card colours: the GUI blends in linear space, so even 3% white over the near-black body reads mid-grey.
        static readonly Color Card = new Color(0.075f, 0.068f, 0.062f, 0.95f), CardLit = new Color(0.17f, 0.12f, 0.06f, 0.95f), CardBoss = new Color(0.12f, 0.075f, 0.065f, 0.95f);
        const string InfoHint = "J 닫기";

        public CodexPanel(UiKit ui, SimWorld world)
        {
            _ui = ui;
            _world = world;
            _win = ui.Window("Codex Panel", W, H, "도감", Close, out _, "ui/ui2_window_iron");
            for (int i = 0; i < 3; i++)
            {
                int tab = i;
                _tabText[i] = ui.Button(_win, TabNames[i], 16f + i * 92f, 30f, 88f, 20f, () => { _tab = tab; _stamp = -1; }).GetComponentInChildren<Text>();
            }
            // act picker for the enemy tab
            _actRow = UiKit.Rect(_win, "acts", 312f, 30f, EnemyRoster.Acts.Length * 52f, 20f);
            _actText = new Text[EnemyRoster.Acts.Length];
            for (int a = 0; a < EnemyRoster.Acts.Length; a++)
            {
                int act = a;
                _actText[a] = ui.Button(_actRow, "막 " + EnemyRoster.Acts[a].Numeral, a * 52f, 0f, 48f, 20f, () => { _act = act; _stamp = -1; }).GetComponentInChildren<Text>();
            }
            _page = UiKit.Rect(_win, "page", 16f, Top, W - 32f, H - Top - 28f);
            _info = UiKit.Label(_win, InfoHint, 16f, H - 22f, W - 32f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            SetVisible(false);
        }

        /// <summary>J: open on the evolution tab, or close.</summary>
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
            if (stamp == _stamp && (_refresh > 0f || _tab == 2)) return; // goals change only through the stamp
            _stamp = stamp;
            _refresh = 2f; // kills and practice tick up while it is open
            Rebuild();
        }

        T Keep<T>(T c) where T : Component { _bits.Add(c.gameObject); return c; }

        void Rebuild()
        {
            foreach (var g in _bits) Object.Destroy(g);
            _bits.Clear();
            _walkers.Clear();
            _info.text = InfoHint;
            for (int i = 0; i < 3; i++) _tabText[i].color = i == _tab ? UiKit.Gold : UiKit.Dim;
            _actRow.gameObject.SetActive(_tab == 1);
            for (int a = 0; a < _actText.Length; a++) _actText[a].color = a == _act ? UiKit.Gold : UiKit.Dim;
            if (_tab == 0) Evolutions();
            else if (_tab == 1) Enemies();
            else Achievements();
        }

        // ---------- 진화 ----------

        void Evolutions()
        {
            var h = _world.Hero;
            var cards = new List<TreeNode>();
            foreach (var n in SkillTree.For(h.ClassId).Nodes) if (n.Kind == TreeNodeKind.Evolution) cards.Add(n);
            int done = 0;
            foreach (var n in cards) if (SkillTree.Has(h, n.Id)) done++;
            Keep(UiKit.Label(_page, "스킬 트리에서 진화 칸을 찍으면 스킬이 진화한다 · 숙련 " + SkillTree.EvoMastery + " 필요   <color=#ffcf5a>" + done + " / " + cards.Count + "</color>",
                0f, 0f, W - 32f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim));
            const float cw = 276f, ch = 54f;
            for (int i = 0; i < cards.Count; i++)
            {
                var node = cards[i];
                var skill = SkillBook.Get(node.SkillId);
                if (skill == null) continue;
                var evo = skill.EvolveTo != null ? SkillBook.Get(skill.EvolveTo) : null;
                float x = (i % 3) * (cw + 6f), y = 20f + (i / 3) * (ch + 4f);
                bool taken = SkillTree.Has(h, node.Id);
                int mastery = SkillTree.MasteryOf(h, node.SkillId);
                var card = Keep(UiKit.Fill(_page, "evo card", x, y, cw, ch, taken ? CardLit : Card));
                card.raycastTarget = true;
                Keep(UiKit.Fill(_page, "edge", x, y + ch - 1f, cw, 1f, taken ? new Color(1f, 0.8f, 0.4f, 0.5f) : UiKit.Line));
                Icon("icon/skill_" + skill.Id, x + 6f, y + 11f, taken ? Color.white : new Color(0.8f, 0.8f, 0.8f));
                Keep(UiKit.Label(_page, "→", x + 40f, y + 18f, 14f, 14f, 12, TextAnchor.UpperCenter, UiKit.Dim));
                Icon("icon/skill_" + (evo?.Id ?? skill.Id), x + 54f, y + 11f, taken ? Color.white : new Color(0f, 0f, 0f, 0.75f));
                string evoName = evo?.Name ?? skill.Name;
                Keep(UiKit.Label(_page, (taken ? "<color=#ffcf5a>" + evoName + "</color>" : evoName) + "  <color=#8a8178>" + skill.Name + "</color>",
                    x + 92f, y + 6f, cw - 96f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink));
                // practice bar to the evolution threshold
                float k = Mathf.Clamp01(mastery / (float)SkillTree.EvoMastery);
                Keep(UiKit.Fill(_page, "bar back", x + 92f, y + 26f, 120f, 3f, new Color(0f, 0f, 0f, 0.6f)));
                Keep(UiKit.Fill(_page, "bar", x + 92f, y + 26f, 120f * k, 3f, k >= 1f ? UiKit.Gold : new Color(0.6f, 0.8f, 1f)));
                string state = taken ? "<color=#9fd28a>진화 완료</color>" : mastery >= SkillTree.EvoMastery ? "<color=#ffcf5a>트리에서 습득 가능</color>" : "숙련 " + mastery + " / " + SkillTree.EvoMastery;
                Keep(UiKit.Label(_page, state, x + 92f, y + 32f, cw - 96f, 12f, 8, TextAnchor.UpperLeft, UiKit.Dim));
                var p = card.gameObject.AddComponent<UiPointer>();
                p.Enter = () => _info.text = "<color=#ffcf5a>" + evoName + "</color>  " + skill.Name + "의 진화 · 숙련 " + mastery + " / " + SkillTree.EvoMastery;
                p.Exit = () => _info.text = InfoHint;
            }
            // resonance mastery along the bottom
            float ry = 20f + ((cards.Count + 2) / 3) * (ch + 4f) + 6f;
            Keep(UiKit.Label(_page, "공명 숙련  <color=#8a8178>300 · 2,000 · 10,000회</color>", 0f, ry, 400f, 14f, 12, TextAnchor.UpperLeft, UiKit.Gold));
            var res = new[] { Resonance.Shatter, Resonance.Overload, Resonance.Plague, Resonance.Devour, Resonance.Collapse };
            for (int i = 0; i < res.Length; i++)
            {
                int n = h.ResonanceCounts[(int)res[i]];
                int tier = Elements.MasteryTier(n);
                Keep(UiKit.Label(_page, (tier > 0 ? "<color=#ffcf5a>" : "<color=#cfc6b8>") + FxView.ResonanceName(res[i]) + " " + (tier == 0 ? "—" : tier == 1 ? "I" : tier == 2 ? "II" : "III")
                    + "</color>  <color=#8a8178>" + n.ToString("N0") + "</color>", i * 168f, ry + 18f, 164f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink));
            }
        }

        void Icon(string key, float x, float y, Color tint)
        {
            var img = Keep(_ui.Sprite(_page, _ui.Catalog.Has(key) ? key : "ui/dot", x, y, 32f, 32f));
            img.color = tint;
        }

        // ---------- 적 ----------

        void Enemies()
        {
            var act = EnemyRoster.Acts[_act];
            var defs = new List<EnemyDef>(act.Ranks) { act.Elite, act.Boss };
            Keep(UiKit.Label(_page, "막 " + act.Numeral + " · " + act.Name + "   <color=#8a8178>메달: 100 · 1,000 · 10,000 처치, 메달당 해당 적에게 주는 피해 +1%</color>",
                0f, 0f, W - 32f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink));
            const float cw = 206f, ch = 182f;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                float x = (i % 4) * (cw + 4f), y = 20f + (i / 4) * (ch + 4f);
                int kills = _world.KillsOf(d.Id);
                int medal = SimWorld.MedalOf(kills);
                bool known = kills > 0, boss = d == act.Boss, elite = d == act.Elite;
                var card = Keep(UiKit.Fill(_page, "enemy card", x, y, cw, ch, boss ? CardBoss : Card));
                card.raycastTarget = true;
                var pool = Keep(UiKit.Fill(_page, "pool", x + 23f, y + 6f, 160f, 140f, new Color(1f, 0.72f, 0.4f, known ? 0.16f : 0.05f)));
                pool.sprite = UiKit.Glow(_ui, 64);
                string key = WorldView.EnemyArt(_ui.Catalog, d.Id, out var tint, out _);
                var img = Keep(_ui.Sprite(_page, _ui.Catalog.Has(key) ? key : "ui/dot", x + 39f, y + 8f, 128f, 128f));
                img.color = known ? tint : new Color(0f, 0f, 0f, 0.9f);
                if (known) _walkers.Add((img, key));
                string name = known ? d.Name : "???";
                Keep(UiKit.Label(_page, (boss ? "<color=#ff9a7a>" : elite ? "<color=#ffcf5a>" : "") + name + (boss || elite ? "</color>" : ""),
                    x + 4f, y + 140f, cw - 8f, 14f, 12, TextAnchor.UpperCenter, UiKit.Ink));
                Keep(UiKit.Label(_page, known ? kills.ToString("N0") + " 처치" : "만난 적 없음", x + 4f, y + 158f, cw - 8f, 12f, 8, TextAnchor.UpperCenter, UiKit.Dim));
                if (medal > 0)
                {
                    var gem = Keep(_ui.Sprite(_page, "ui/ui2_gem", x + cw - 22f, y + 8f, 14f, 14f));
                    gem.color = medal == 3 ? new Color(1f, 0.83f, 0.4f) : medal == 2 ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.82f, 0.55f, 0.36f);
                }
                if (boss || elite) Keep(UiKit.Label(_page, boss ? "보스" : "정예", x + 6f, y + 6f, 40f, 12f, 8, TextAnchor.UpperLeft, boss ? new Color(1f, 0.6f, 0.48f) : UiKit.Gold));
                var p = card.gameObject.AddComponent<UiPointer>();
                string lore = known ? d.Lore : "아직 처치하지 못했다.";
                p.Enter = () => _info.text = "<color=#e8dcc8>" + name + "</color>  <i>" + lore + "</i>";
                p.Exit = () => _info.text = InfoHint;
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
                0f, 0f, W - 32f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink));
            const float cw = 208f, ch = 40f;
            for (int i = 0; i < all.Length; i++)
            {
                var a = all[i];
                bool ok = h.Achieved.Contains(a.Id);
                float x = (i % 4) * (cw + 4f), y = 20f + (i / 4) * (ch + 4f);
                Keep(UiKit.Fill(_page, "goal", x, y, cw, ch, ok ? CardLit : Card));
                var mark = Keep(UiKit.Fill(_page, "mark", x + 8f, y + 12f, 14f, 14f, ok ? UiKit.Gold : new Color(0.3f, 0.28f, 0.26f)));
                mark.sprite = UiKit.Diamond(_ui, 14);
                Keep(UiKit.Label(_page, ok ? "<color=#ffcf5a>" + a.Name + "</color>" : a.Name, x + 28f, y + 4f, cw - 32f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink));
                Keep(UiKit.Label(_page, a.Text + (a.Lantern > 0 ? " · 등불 색 " + SimWorld.LanternColors[a.Lantern].name : ""), x + 28f, y + 22f, cw - 32f, 12f, 8, TextAnchor.UpperLeft, UiKit.Dim));
            }
        }
    }
}
