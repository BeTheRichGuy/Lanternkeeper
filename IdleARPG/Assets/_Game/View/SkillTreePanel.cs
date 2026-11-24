using System;
using System.Collections.Generic;
using System.Text;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// The skill tree (희승 11-24: "한 탭에서는 하나의 기능만"). Four tabs under the title: one per branch, each only
    /// that branch's skill rows (skill, its two 변형, its evolution, the name beside the skill), and 「특성」 with three
    /// headed sections: the ultimate choice, the keystones, the 갈래 강화. Owned nodes wear a gold ring and check, takeable ones carry a
    /// "+" and breathe, locked ones stand at 60 % with a lock. The card on the right shows the node pointed at, else the
    /// one last clicked, else the tab's first takeable (or owned) node; the legend waits behind "?". Top bar: points,
    /// 자동 분배, reset. T toggles it.
    /// </summary>
    public sealed class SkillTreePanel
    {
        const float W = 880f, H = 500f, TreeW = 596f, CardX = 608f, CardW = W - CardX - 12f;
        const float BodyTop = 60f, BodyBottom = H - 32f; // under the tab rule; the status line sits below
        const int TraitTab = 3;
        readonly UiKit _ui;
        readonly SimWorld _world;
        readonly RectTransform _win, _treeLayer;
        readonly Text _title, _points, _autoLabel, _cardName, _cardBody, _footer;
        readonly Image _cardIcon, _cardWell;
        readonly GameObject _legend;
        // Labels Refresh rewrites: a row's name follows its evolution, its second line says what the skill needs.
        readonly List<(Text name, Text status, TreeNode node)> _rowNames = new List<(Text, Text, TreeNode)>();
        readonly Text[] _branchPoints = new Text[3], _passiveCount = new Text[3];
        // 특성 tab: each keystone's name and state line, and the gold edge of the chosen ultimate's card.
        readonly List<(Text name, Text status, TreeNode node)> _keyLabels = new List<(Text, Text, TreeNode)>();
        readonly List<(GameObject edge, TreeNode node)> _ultEdges = new List<(GameObject, TreeNode)>();
        Text _resetLabel;
        float _resetAskedAt = -10f;
        public bool Visible { get; private set; }

        sealed class NodeView
        {
            public TreeNode Node;
            public Image Frame, Icon, Glow, Ring;
            public Text Mark, Plus;
            public GameObject Holder, Lock, Check;
            public float Size;
        }

        readonly List<NodeView> _nodes = new List<NodeView>();
        readonly Dictionary<TreeNode, NodeView> _viewOf = new Dictionary<TreeNode, NodeView>();
        readonly List<(TreeNode child, Image line)> _edges = new List<(TreeNode, Image)>();
        string _classId;
        int _stamp = -1;
        TreeNode _hovered, _selected;
        readonly Color[] _branchColor = new Color[3];
        readonly Dictionary<string, Vector2> _pos = new Dictionary<string, Vector2>();
        readonly Text[] _tabText = new Text[4];
        readonly Image[] _tabBar = new Image[4];
        readonly RectTransform[] _tabRoots = new RectTransform[4];
        Image _selRing;
        int _tab;

        public SkillTreePanel(UiKit ui, SimWorld world)
        {
            _ui = ui;
            _world = world;
            _win = ui.Window("Skill Tree", W, H, "스킬 트리", Toggle, out _title);

            // ---------- top bar: the four tabs left, points · 자동 분배 · 초기화 right (as RingPanel's tab row) ----------
            for (int t = 0; t < 4; t++)
            {
                int tab = t;
                var btn = ui.Button(_win, t == TraitTab ? "특성" : "", 16f + t * 108f, 32f, 104f, 22f, () => ShowTab(tab));
                _tabText[t] = btn.GetComponentInChildren<Text>();
                _tabBar[t] = UiKit.Fill(_win, "tab bar", 16f + t * 108f, 52f, 104f, 3f, UiKit.Gold);
            }
            UiKit.Fill(_win, "tab rule", 12f, 54f, W - 24f, 1f, UiKit.Line);
            _points = UiKit.Label(_win, "", 452f, 36f, 214f, 14f, 12, TextAnchor.UpperRight, UiKit.Gold);
            _points.horizontalOverflow = HorizontalWrapMode.Overflow;
            var auto = ui.Button(_win, "", 676f, 33f, 116f, 19f, () => { _world.SetTreeAuto(!_world.Hero.TreeAuto); Changed(); });
            _autoLabel = auto.GetComponentInChildren<Text>();
            // UX plan B6③: a reset is undone by nothing - the first click asks, a second within 3 s does it.
            var reset = ui.Button(_win, "초기화", 798f, 33f, 66f, 19f, null);
            var resetLabel = reset.GetComponentInChildren<Text>();
            reset.onClick.AddListener(() =>
            {
                if (Time.unscaledTime - _resetAskedAt < 3f) { _world.ResetTree(); Changed(); _resetAskedAt = -10f; if (resetLabel != null) resetLabel.text = "초기화"; return; }
                _resetAskedAt = Time.unscaledTime;
                if (resetLabel != null) resetLabel.text = "<color=#ff8a70>정말?</color>";
            });
            _resetLabel = resetLabel;

            UiKit.Fill(_win, "tree back", 8f, BodyTop, TreeW - 8f, BodyBottom - BodyTop, new Color(0f, 0f, 0f, 0.35f));
            _treeLayer = UiKit.Rect(_win, "tree", 0f, 0f, TreeW, H);
            // One line under the tree: what the player can do here now (critique 6: "자동 분배: 켬" and 0 points said nothing).
            _footer = UiKit.Label(_win, "", 16f, H - 25f, TreeW - 24f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);

            // ---------- card (right): the node pointed at, on the same burnt parchment as the skill-placement card ----------
            var paper = ui.Sprite(_win, "ui/ui2_parchment", CardX, BodyTop, CardW, BodyBottom - BodyTop, sliced: true);
            paper.preserveAspect = false;
            paper.color = new Color(0.42f, 0.36f, 0.32f, 1f);
            _cardWell = UiKit.Fill(_win, "icon well", CardX + 12f, 72f, 72f, 72f, new Color(0.05f, 0.04f, 0.04f, 0.85f));
            _cardIcon = ui.Sprite(_win, "icon/item_rune", CardX + 16f, 76f, 64f, 64f);
            _cardName = UiKit.Label(_win, "", CardX + 94f, 74f, CardW - 94f - 30f, 40f, 14, TextAnchor.UpperLeft, UiKit.Gold, title: true);
            _cardBody = UiKit.Label(_win, "", CardX + 12f, 156f, CardW - 24f, 290f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            _cardBody.lineSpacing = 1.12f;

            // The legend behind "?" (critique 6 P2: it sat inside the card, mixed with the description).
            var help = ui.Button(_win, "?", CardX + CardW - 28f, 66f, 20f, 18f, null);
            var legend = UiKit.Rect(_win, "legend", CardX + 8f, BodyBottom - 128f, CardW - 16f, 120f);
            var lb = legend.gameObject.AddComponent<Image>();
            lb.color = new Color(0.04f, 0.035f, 0.05f, 0.97f);
            lb.raycastTarget = false;
            UiKit.Fill(legend, "edge", 0f, 0f, CardW - 16f, 1f, UiKit.Line);
            UiKit.Label(legend, "<color=#ffcf5a>금빛으로 빛나는 칸</color>  배운 칸\n<color=#9fd28a>+</color> 붙은 칸  지금 배울 수 있음 (클릭)\n<color=#8a8178>어둡고 자물쇠</color>  아직 잠김\n큰 원 스킬 · 마름모 변형 · 가시 원 진화\n<color=#8a8178>진화는 그 스킬 숙련 " + SkillTree.EvoMastery + "부터</color>",
                10f, 8f, CardW - 36f, 104f, 12, TextAnchor.UpperLeft, UiKit.Ink).lineSpacing = 1.15f;
            _legend = legend.gameObject;
            _legend.SetActive(false);
            help.onClick.AddListener(() => _legend.SetActive(!_legend.activeSelf));
            SetVisible(false);
        }

        /// <summary>Raised after a click changes the tree, so the game saves soon.</summary>
        public Action OnChanged;

        void Changed()
        {
            _stamp = -1;
            OnChanged?.Invoke();
        }

        public void Toggle() => SetVisible(!Visible);

        void SetVisible(bool v)
        {
            Visible = v;
            _win.gameObject.SetActive(v);
            if (v) _win.SetAsLastSibling();
            _stamp = -1;
        }

        // ---------- build the tree once per class ----------

        Vector2 Pos(TreeNode n) => _pos.TryGetValue(n.Id, out var p) ? p : new Vector2(-1000f, -1000f);

        // Branch tabs: rows of skill · name · two 변형 · evolution.
        const float RowsTop = 84f, SkillX = 60f, NameX = 96f, Mod0X = 334f, Mod1X = 400f, EvoX = 484f;
        // 특성 tab (critique 7: three systems crowded into one row per branch): three headed sections, top to bottom —
        // 궁극기 (two cards side by side), 핵심 특성 (a row per branch, I · II · III in fixed columns), 갈래 강화 (a column
        // per branch: its pips and n / m). Rows run x 20–580; every row keeps 8 px inside its plate.
        const float RowX = 20f, RowW = 560f;
        const float UltHeadY = 64f, UltTop = 84f, UltH = 60f, UltW = 274f, UltGap = 12f;
        const float KeyHeadY = 152f, KeyTop = 172f, KeyRowH = 64f, KeyRowStep = 66f, KeyCellX = 132f, KeyCellStep = 148f;
        const float PassHeadY = 376f, PassTop = 394f, PassH = 70f, PassColW = 182f, PassColStep = 189f;
        readonly Dictionary<string, Vector2> _dims = new Dictionary<string, Vector2>(); // 갈래 강화 pips: (drawn size, hit)
        static readonly Color Plate = new Color(1f, 0.85f, 0.6f, 0.012f); // a row's faint plate (0.035 read as an olive box)

        static bool InTab(TreeNode n, int tab) => tab == TraitTab
            ? n.Kind == TreeNodeKind.Ultimate || n.Kind == TreeNodeKind.Key || n.Kind == TreeNodeKind.Passive
            : n.Branch == tab && (n.Kind == TreeNodeKind.Skill || n.Kind == TreeNodeKind.Mod || n.Kind == TreeNodeKind.Evolution);

        static int TabOf(TreeNode n)
        {
            for (int t = 0; t < 4; t++) if (InTab(n, t)) return t;
            return -1;
        }

        /// <summary>Where each node stands in its tab (centres, UI px from the window's top-left).</summary>
        void Layout(TreeDef t)
        {
            _pos.Clear();
            _dims.Clear();
            for (int b = 0; b < 3; b++)
            {
                int rows = 0, passives = 0;
                foreach (var n in t.Nodes)
                {
                    if (n.Branch != b) continue;
                    if (n.Kind == TreeNodeKind.Skill) rows++;
                    else if (n.Kind == TreeNodeKind.Passive) passives++;
                }
                float rowH = Mathf.Min(76f, (BodyBottom - 6f - RowsTop) / Mathf.Max(1, rows));
                // Pips share their column, left-aligned: 28 px while five fit, smaller for six, so the owned ring
                // (+2 a side) never meets the next pip.
                float pipStep = Mathf.Min(36f, (PassColW - 12f) / Mathf.Max(1, passives));
                float pipSize = Mathf.Min(28f, Mathf.Floor(pipStep - 6f));
                int row = 0, passive = 0;
                foreach (var n in t.Nodes)
                {
                    if (n.Branch != b) continue;
                    switch (n.Kind)
                    {
                        case TreeNodeKind.Skill:
                            float y = RowsTop + rowH * (row + 0.5f);
                            _pos[n.Id] = new Vector2(SkillX, y);
                            foreach (var m in t.Nodes)
                            {
                                if (m.Parent != n.Id) continue;
                                if (m.Kind == TreeNodeKind.Mod) _pos[m.Id] = new Vector2(m.Id.EndsWith(":1", StringComparison.Ordinal) ? Mod1X : Mod0X, y);
                                else if (m.Kind == TreeNodeKind.Evolution) _pos[m.Id] = new Vector2(EvoX, y);
                            }
                            row++;
                            break;
                        case TreeNodeKind.Passive:
                            _pos[n.Id] = new Vector2(RowX + b * PassColStep + 6f + pipStep * (passive++ + 0.5f), PassTop + 46f);
                            _dims[n.Id] = new Vector2(pipSize, pipStep);
                            break;
                        case TreeNodeKind.Key:
                            _pos[n.Id] = new Vector2(KeyCellX + n.KeyIndex * KeyCellStep + 26f, KeyTop + b * KeyRowStep + KeyRowH / 2f);
                            break;
                    }
                }
            }
            int u = 0;
            foreach (var n in t.Nodes)
                if (n.Kind == TreeNodeKind.Ultimate) _pos[n.Id] = new Vector2(RowX + u++ * (UltW + UltGap) + 32f, UltTop + UltH / 2f);
        }

        void ShowTab(int t)
        {
            _tab = t;
            for (int i = 0; i < 4; i++)
            {
                if (_tabRoots[i] != null) _tabRoots[i].gameObject.SetActive(i == t);
                _tabText[i].color = i == t ? UiKit.Gold : UiKit.Dim;
                _tabBar[i].enabled = i == t;
            }
            if (_selected != null && !InTab(_selected, t)) _selected = null;
            _hovered = null;
            ShowCard();
        }

        void Build()
        {
            foreach (var r in _tabRoots) if (r != null) UnityEngine.Object.Destroy(r.gameObject);
            _rowNames.Clear();
            _keyLabels.Clear();
            _ultEdges.Clear();
            _nodes.Clear();
            _viewOf.Clear();
            _edges.Clear();
            _hovered = _selected = null;
            _classId = _world.Hero.ClassId;
            var t = SkillTree.For(_classId);
            Layout(t);
            for (int i = 0; i < 4; i++) _tabRoots[i] = UiKit.Rect(_treeLayer, "tab " + i, 0f, 0f, TreeW, H);
            for (int b = 0; b < 3; b++) _tabText[b].text = Talents.BranchName(_classId, b);

            // Branch colour = the most common element among its skills.
            for (int b = 0; b < 3; b++)
            {
                var count = new Dictionary<Element, int>();
                foreach (var n in t.Nodes)
                    if (n.Branch == b && n.Kind == TreeNodeKind.Skill)
                    {
                        var e = SkillBook.Get(n.SkillId).Elem;
                        count.TryGetValue(e, out int c);
                        count[e] = c + 1;
                    }
                Element best = Element.None;
                int bc = -1;
                foreach (var kv in count) if (kv.Value > bc) { bc = kv.Value; best = kv.Key; }
                _branchColor[b] = FxView.ElementColor(best);
            }

            // ---------- branch tabs: column heads, a plate per row, the name and what it needs beside the skill ----------
            for (int b = 0; b < 3; b++)
            {
                var root = _tabRoots[b];
                UiKit.Label(root, "스킬", NameX, 66f, 120f, 12f, 11, TextAnchor.UpperLeft, UiKit.Dim);
                UiKit.Label(root, "변형", Mod0X - 32f, 66f, Mod1X - Mod0X + 64f, 12f, 11, TextAnchor.UpperCenter, UiKit.Dim);
                UiKit.Label(root, "진화", EvoX - 40f, 66f, 80f, 12f, 11, TextAnchor.UpperCenter, UiKit.Dim);
            }
            foreach (var n in t.Nodes)
            {
                if (n.Kind != TreeNodeKind.Skill) continue;
                var root = _tabRoots[n.Branch];
                var p = Pos(n);
                UiKit.Fill(root, "row", 20f, p.y - 33f, TreeW - 36f, 70f, Plate);
                var nm = UiKit.Label(root, "", NameX, p.y - 17f, 210f, 16f, 13, TextAnchor.UpperLeft, UiKit.Ink);
                nm.horizontalOverflow = HorizontalWrapMode.Overflow;
                var st = UiKit.Label(root, "", NameX, p.y + 3f, 210f, 14f, 11, TextAnchor.UpperLeft, UiKit.Dim);
                st.horizontalOverflow = HorizontalWrapMode.Overflow;
                _rowNames.Add((nm, st, n));
            }
            // What each 변형 does, under it (they were anonymous diamonds).
            foreach (var n in t.Nodes)
            {
                if (n.Kind != TreeNodeKind.Mod) continue;
                var p = Pos(n);
                UiKit.Label(_tabRoots[n.Branch], ModShort(n.Mod), p.x - 32f, p.y + 23f, 64f, 12f, 10, TextAnchor.UpperCenter, UiKit.Dim);
            }

            // ---------- 특성 tab: 궁극기 · 핵심 특성 · 갈래 강화, each under its own head ----------
            var trait = _tabRoots[TraitTab];
            Head(trait, UltHeadY, "궁극기", "통합 레벨 " + SkillBook.UltimateLevel + "부터 · 둘 중 하나만 고릅니다");
            foreach (var n in t.Nodes)
            {
                if (n.Kind != TreeNodeKind.Ultimate) continue;
                float x = Pos(n).x - 32f;
                UiKit.Fill(trait, "card", x, UltTop, UltW, UltH, Plate);
                // The chosen one wears a gold edge (critique 7: "배움" alone did not read as the pick).
                var edge = UiKit.Rect(trait, "chosen", x, UltTop, UltW, UltH);
                foreach (var r in new[] { new Rect(0f, 0f, UltW, 2f), new Rect(0f, UltH - 2f, UltW, 2f), new Rect(0f, 0f, 2f, UltH), new Rect(UltW - 2f, 0f, 2f, UltH) })
                    UiKit.Fill(edge, "edge", r.x, r.y, r.width, r.height, new Color(1f, 0.81f, 0.35f, 0.85f));
                _ultEdges.Add((edge.gameObject, n));
                var nm = UiKit.Label(trait, "", x + 64f, UltTop + 9f, 110f, 16f, 13, TextAnchor.UpperLeft, UiKit.Ink);
                nm.horizontalOverflow = HorizontalWrapMode.Overflow;
                var st = UiKit.Label(trait, "", x + UltW - 100f, UltTop + 10f, 90f, 14f, 11, TextAnchor.UpperRight, UiKit.Dim);
                st.horizontalOverflow = HorizontalWrapMode.Overflow;
                _rowNames.Add((nm, st, n));
                UiKit.Label(trait, UltimateLine(n.SkillId), x + 64f, UltTop + 32f, UltW - 74f, 14f, 11, TextAnchor.UpperLeft, UiKit.Dim);
            }

            Head(trait, KeyHeadY, "핵심 특성", "갈래 포인트 " + SkillTree.KeyThreshold[0] + " · " + SkillTree.KeyThreshold[1] + " · " + SkillTree.KeyThreshold[2]
                + "에서 I · II · III이 열립니다 · 하나에 " + SkillTree.KeyCost + "포인트");
            Head(trait, PassHeadY, "갈래 강화", "한 칸에 1포인트 · 그 갈래 능력치 +" + SkillTree.PassiveWeight + "단계");
            for (int b = 0; b < 3; b++)
            {
                var bc = Color.Lerp(_branchColor[b], Color.white, 0.35f);
                float y = KeyTop + b * KeyRowStep;
                UiKit.Fill(trait, "row", RowX, y, RowW, KeyRowH, Plate);
                UiKit.Label(trait, Talents.BranchName(_classId, b), 32f, y + 14f, 96f, 16f, 13, TextAnchor.UpperLeft, bc);
                _branchPoints[b] = UiKit.Label(trait, "", 32f, y + 34f, 96f, 14f, 11, TextAnchor.UpperLeft, UiKit.Dim);
                _branchPoints[b].horizontalOverflow = HorizontalWrapMode.Overflow;

                float px = RowX + b * PassColStep;
                UiKit.Fill(trait, "column", px, PassTop, PassColW, PassH, Plate);
                UiKit.Label(trait, Talents.BranchName(_classId, b), px + 10f, PassTop + 7f, 110f, 16f, 13, TextAnchor.UpperLeft, bc);
                _passiveCount[b] = UiKit.Label(trait, "", px + PassColW - 70f, PassTop + 8f, 60f, 14f, 11, TextAnchor.UpperRight, UiKit.Dim);
            }
            foreach (var n in t.Nodes)
            {
                if (n.Kind != TreeNodeKind.Key) continue;
                var p = Pos(n);
                float tx = p.x + 35f, top = p.y - KeyRowH / 2f;
                var nm = UiKit.Label(trait, Talents.KeystoneName(n.Branch, n.KeyIndex), tx, top + 14f, KeyCellStep - 66f, 16f, 12, TextAnchor.UpperLeft, UiKit.Ink);
                nm.horizontalOverflow = HorizontalWrapMode.Overflow;
                var st = UiKit.Label(trait, "", tx, top + 34f, KeyCellStep - 66f, 14f, 11, TextAnchor.UpperLeft, UiKit.Dim);
                st.horizontalOverflow = HorizontalWrapMode.Overflow;
                _keyLabels.Add((nm, st, n));
            }

            // Trunk lines (skill to the next skill, under the nodes).
            foreach (var n in t.Nodes)
            {
                if (n.Kind != TreeNodeKind.Skill || n.Parent == null) continue;
                var parent = t.Get(n.Parent);
                if (parent == null || parent.Kind != TreeNodeKind.Skill) continue;
                var a = Pos(parent);
                var d = Pos(n) - a;
                var line = UiKit.Fill(_tabRoots[n.Branch], "edge", 0f, 0f, 1f, 1f, Color.clear);
                var rt = line.rectTransform;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(a.x, -a.y);
                rt.sizeDelta = new Vector2(d.magnitude, 4f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg);
                _edges.Add((n, line));
            }
            // Ultimates first, so the 특성 tab's card opens on them (DefaultNode walks this order).
            var order = new List<TreeNode>();
            foreach (var n in t.Nodes) if (n.Kind == TreeNodeKind.Ultimate) order.Add(n);
            foreach (var n in t.Nodes) if (n.Kind != TreeNodeKind.Ultimate) order.Add(n);
            foreach (var n in order)
            {
                int tab = TabOf(n);
                if (tab < 0) continue; // the root lantern has no place in the tabs
                var nv = MakeNode(n, _tabRoots[tab]);
                _nodes.Add(nv);
                _viewOf[n] = nv;
            }
            // The node the card describes wears a thin light ring.
            _selRing = UiKit.Fill(_tabRoots[0], "selected", 0f, 0f, 10f, 10f, new Color(1f, 0.96f, 0.86f, 0.9f));
            _selRing.sprite = UiKit.Disc(_ui, 64, 2);
            ShowTab(_tab);
        }

        NodeView MakeNode(TreeNode n, RectTransform parent)
        {
            // Drawn frames at 1:1 (skill 44, evolution 52→48, keystone 56→48) and the small ones at ×2 (변형 20→40,
            // passive 16→32); every node takes clicks over at least 40 px (critique 6: a 12 px diamond read as no button).
            // 갈래 강화 pips share a column, so each takes clicks over its own step only.
            float size = n.Kind == TreeNodeKind.Key || n.Kind == TreeNodeKind.Evolution ? 48f : n.Kind == TreeNodeKind.Mod ? 40f
                : n.Kind == TreeNodeKind.Passive ? 32f : 44f;
            float hit = Mathf.Max(size, 40f);
            if (_dims.TryGetValue(n.Id, out var dim)) { size = dim.x; hit = Mathf.Max(size, dim.y); }
            float off = (hit - size) / 2f;
            var p = Pos(n);
            var holder = UiKit.Rect(parent, n.Id, p.x - hit / 2f, p.y - hit / 2f, hit, hit);
            var hitArea = holder.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            var nv = new NodeView { Node = n, Holder = holder.gameObject, Size = size };
            nv.Glow = UiKit.Fill(holder, "glow", off - 10f, off - 10f, size + 20f, size + 20f, Color.clear);
            nv.Glow.sprite = UiKit.Glow(_ui, 64); // soft halo, not a flat disc
            // Owned: a gold ring round the frame (critique 7 P0 #7: a bought 변형 or 진화 looked like one not bought).
            float rp = n.Kind == TreeNodeKind.Passive ? 2f : 3f;
            nv.Ring = UiKit.Fill(holder, "owned ring", off - rp, off - rp, size + 2f * rp, size + 2f * rp, UiKit.Gold);
            nv.Ring.sprite = UiKit.Disc(_ui, 32, 2);
            nv.Frame =UiKit.Rect(holder, "frame", off, off, size, size).gameObject.AddComponent<Image>();
            nv.Frame.sprite = FrameFor(n);
            nv.Frame.raycastTarget = false;
            if (n.Kind == TreeNodeKind.Skill || n.Kind == TreeNodeKind.Evolution || n.Kind == TreeNodeKind.Ultimate)
            {
                float io = off + Mathf.Floor((size - 32f) / 2f);
                nv.Icon = _ui.Sprite(holder, IconFor(n), io, io, 32f, 32f);
            }
            if (n.Kind == TreeNodeKind.Key)
                nv.Mark = UiKit.Label(holder, Roman(n.KeyIndex), off, off, size, size, 12, TextAnchor.MiddleCenter, UiKit.Gold);
            // "+" = takeable now; the lock = closed for a reason other than points.
            nv.Plus = UiKit.Label(holder, "+", hit - 11f, -5f, 16f, 16f, 14, TextAnchor.MiddleCenter, UiKit.Good);
            nv.Lock = MakeLock(holder, hit - 10f, hit - 12f);
            // ...and a gold check where a takeable node shows its "+": three states, three marks, one corner each.
            // A 갈래 강화 pip keeps the ring alone (a check on each crowded the next pip).
            if (n.Kind != TreeNodeKind.Passive) nv.Check = MakeCheck(holder, hit - 12f, -3f);
            var ptr = holder.gameObject.AddComponent<UiPointer>();
            ptr.Enter += () => { _hovered = n; ShowCard(); };
            ptr.Exit += () => { if (_hovered == n) { _hovered = null; ShowCard(); } };
            ptr.LeftClick += () => { _selected = n; if (_world.TakeTreeNode(n.Id)) Changed(); else ShowCard(); };
            return nv;
        }

        /// <summary>A small lock drawn from rectangles (no lock art, and the font may not carry the glyph) — as the codex's.</summary>
        static GameObject MakeLock(RectTransform holder, float x, float y)
        {
            var l = UiKit.Rect(holder, "lock", x, y, 11f, 13f);
            var c = new Color(0.86f, 0.82f, 0.74f);
            UiKit.Fill(l, "lock back", 0f, 4f, 11f, 9f, new Color(0f, 0f, 0f, 0.75f));
            UiKit.Fill(l, "lock shackle", 3f, 0f, 5f, 1f, c);
            UiKit.Fill(l, "lock shackle", 3f, 0f, 1f, 5f, c);
            UiKit.Fill(l, "lock shackle", 7f, 0f, 1f, 5f, c);
            UiKit.Fill(l, "lock body", 1f, 5f, 9f, 7f, c);
            return l.gameObject;
        }

        /// <summary>A gold coin with a dark check, drawn from a disc and two strokes (the font may not carry ✓).</summary>
        GameObject MakeCheck(RectTransform holder, float x, float y)
        {
            var c = UiKit.Rect(holder, "owned", x, y, 15f, 15f);
            var coin = c.gameObject.AddComponent<Image>();
            coin.sprite = UiKit.Disc(_ui, 16, 0);
            coin.color = UiKit.Gold;
            coin.raycastTarget = false;
            var ink = new Color(0.16f, 0.1f, 0.03f);
            Stroke(c, new Vector2(3.5f, 7.5f), new Vector2(6.5f, 10.5f), ink);
            Stroke(c, new Vector2(6f, 10.5f), new Vector2(11.5f, 4.5f), ink);
            return c.gameObject;
        }

        static void Stroke(RectTransform parent, Vector2 a, Vector2 b, Color col)
        {
            var d = b - a;
            var rt = UiKit.Fill(parent, "stroke", 0f, 0f, 1f, 1f, col).rectTransform;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(a.x, -a.y);
            rt.sizeDelta = new Vector2(d.magnitude + 1f, 2f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg);
        }

        Sprite FrameFor(TreeNode n)
        {
            string key = n.Kind == TreeNodeKind.Key ? "ui/ui2_node_key" : n.Kind == TreeNodeKind.Mod ? "ui/ui2_node_mod"
                : n.Kind == TreeNodeKind.Passive ? "ui/ui2_node_passive" : n.Kind == TreeNodeKind.Evolution ? "ui/ui2_node_evo" : "ui/ui2_node_skill";
            if (_ui.Catalog.Has(key)) return _ui.Catalog.Get(key);
            // Until the drawn frames arrive: crisp procedural shapes.
            switch (n.Kind)
            {
                case TreeNodeKind.Mod: return UiKit.Diamond(_ui, 16);
                case TreeNodeKind.Passive: return UiKit.Disc(_ui, 12, 0);
                case TreeNodeKind.Key: return UiKit.Star(_ui, 40);
                default: return UiKit.Disc(_ui, 40, 3);
            }
        }

        string IconFor(TreeNode n)
        {
            var s = SkillBook.Get(n.SkillId);
            string id = n.Kind == TreeNodeKind.Evolution && s.EvolveTo != null ? s.EvolveTo : s.Id;
            return _ui.Catalog.Has("icon/skill_" + id) ? "icon/skill_" + id : "icon/skill_" + s.BaseId;
        }

        static string Roman(int k) => k == 0 ? "I" : k == 1 ? "II" : "III";

        /// <summary>The 변형 in a word and a number, to fit under its node (the card spells it out).</summary>
        static string ModShort(TreeMod m) => m == TreeMod.Copy ? "분신 +1" : m == TreeMod.Area ? "범위 +20%" : m == TreeMod.Cooldown ? "대기 −15%" : "위력 +20%";

        /// <summary>A 특성 section head: the name in gold, what it is about beside it, a rule over it.</summary>
        static void Head(RectTransform parent, float y, string name, string about)
        {
            if (y > UltHeadY) UiKit.Fill(parent, "rule", RowX - 4f, y - 4f, RowW + 8f, 1f, UiKit.Line);
            UiKit.Label(parent, name + "  <color=#8a8178><size=11>" + about + "</size></color>", 24f, y, RowW - 8f, 16f, 13, TextAnchor.UpperLeft, UiKit.Gold)
                .horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // What each ultimate does, in one line for its card (as the cut-in's lines; the card on the right spells it out).
        static readonly Dictionary<string, string> UltLines = new Dictionary<string, string>
        {
            { "forge_judgment", "일대를 태우고 공명을 채웁니다" },
            { "undying_flame", "8초간 사망 연쇄가 약해지지 않습니다" },
            { "grand_judgment", "일대에 벼락을 내리고 공명을 채웁니다" },
            { "sanctuary", "8초간 덜 맞고, 맞으면 되받아칩니다" },
            { "thunderstorm", "8초간 벼락이 잇따라 떨어집니다" },
            { "hunt_time", "8초간 두 배로 빠르게 몰아칩니다" },
            { "star_collapse", "일대를 무너뜨리고 공명을 채웁니다" },
            { "eternal_moment", "6초간 고리가 쉬지 않고 돕니다" },
        };

        static string UltimateLine(string id)
        {
            if (UltLines.TryGetValue(id, out var line)) return line;
            string text = GameText.Skill(id) ?? "";
            int dot = text.IndexOf(". ", StringComparison.Ordinal);
            return dot > 0 ? text.Substring(0, dot + 1) : text;
        }

        // ---------- per frame ----------

        public void Render()
        {
            if (!Visible) return;
            var h = _world.Hero;
            if (_classId != h.ClassId) Build();
            float t = Time.unscaledTime;
            int stamp = h.TreeOrder.Count * 7919 + SkillTree.Points(h) * 31 + (h.TreeAuto ? 1 : 0);
            foreach (var id in h.TreeOrder) stamp = unchecked(stamp * 31 + id.GetHashCode());
            foreach (var kv in h.SkillUses) stamp = unchecked(stamp * 17 + SkillBook.SkillLevel(kv.Value));
            if (stamp != _stamp) { _stamp = stamp; Refresh(); }
            if (_resetAskedAt > 0f && Time.unscaledTime - _resetAskedAt > 3f) { _resetAskedAt = -10f; _resetLabel.text = "초기화"; }
            // Takeable nodes breathe softly (four steps a second keeps the canvas calm).
            float pulse = 0.18f + 0.14f * Mathf.Sin(Mathf.Floor(t * 4f) / 4f * 4f);
            foreach (var nv in _nodes)
                if (!SkillTree.Has(h, nv.Node.Id) && SkillTree.Blocker(h, nv.Node) == null)
                    nv.Glow.color = new Color(1f, 0.9f, 0.6f, pulse);
        }

        void Refresh()
        {
            var h = _world.Hero;
            int points = SkillTree.Points(h), spent = SkillTree.Spent(h), left = Math.Max(0, points - spent);
            _title.text = "스킬 트리 · " + SkillBook.ClassName(h.ClassId);
            _points.text = "남은 포인트 " + (left > 0 ? "<color=#9fd28a>" + left + "</color>" : "<color=#8a8178>0</color>");
            _autoLabel.text = h.TreeAuto ? "자동 분배: 켬" : "자동 분배: 끔";
            _autoLabel.color = h.TreeAuto ? UiKit.Good : UiKit.Dim;
            _footer.text = h.TreeAuto ? "자동 분배가 포인트를 넣고 있습니다. 칸을 직접 누르면 자동 분배가 꺼집니다."
                : left > 0 ? "<color=#9fd28a>+</color> 표시가 있는 칸을 누르면 포인트 1을 넣어 배웁니다."
                : "레벨이 오를 때마다 포인트 1을 받습니다.";

            var tree = SkillTree.For(h.ClassId);
            foreach (var (label, status, n) in _rowNames)
            {
                var def = SkillBook.Get(n.SkillId);
                bool evolved = def.EvolveTo != null && h.Evolved.Contains(def.BaseId);
                label.text = evolved ? def.Name + "  <color=#ffcf5a>→ " + SkillBook.Get(def.EvolveTo).Name + "</color>" : def.Name;
                label.color = SkillTree.Has(h, n.Id) ? UiKit.Ink : UiKit.Dim;
                status.text = Status(h, n);
            }
            for (int b = 0; b < 3; b++)
            {
                if (_branchPoints[b] == null) continue;
                int have = 0, all = 0;
                foreach (var n in tree.Nodes)
                    if (n.Kind == TreeNodeKind.Passive && n.Branch == b) { all++; if (SkillTree.Has(h, n.Id)) have++; }
                _branchPoints[b].text = "갈래 포인트 " + SkillTree.BranchPoints(h, b);
                _passiveCount[b].text = (have == all ? "<color=#ffcf5a>" : have > 0 ? "<color=#e8e0d0>" : "") + have + " / " + all + (have > 0 ? "</color>" : "");
            }
            foreach (var (label, status, n) in _keyLabels)
            {
                label.color = SkillTree.Has(h, n.Id) ? UiKit.Ink : UiKit.Dim;
                status.text = Status(h, n);
            }
            foreach (var (edge, n) in _ultEdges) edge.SetActive(SkillTree.Has(h, n.Id));

            foreach (var (child, line) in _edges)
            {
                var c = _branchColor[child.Branch];
                line.color = SkillTree.Has(h, child.Id) ? new Color(c.r, c.g, c.b, 0.95f) : new Color(0.3f, 0.26f, 0.22f, 0.9f);
            }
            foreach (var nv in _nodes)
            {
                var n = nv.Node;
                bool taken = SkillTree.Has(h, n.Id);
                string block = taken ? null : SkillTree.Blocker(h, n);
                bool open = !taken && block == null;
                // Owned: full colour and a gold halo. Takeable: near full, "+", breathing. Locked: 60 %, a lock.
                var dim = new Color(0.6f, 0.6f, 0.6f);
                nv.Frame.color = taken ? Color.white : open ? new Color(0.92f, 0.9f, 0.86f) : dim;
                if (nv.Icon != null) nv.Icon.color = taken ? Color.white : open ? new Color(0.85f, 0.85f, 0.85f) : dim;
                // The ring and check carry "owned" now; the halo stays faint (a pip's crowded its neighbours and name).
                nv.Glow.color = taken && n.Kind != TreeNodeKind.Passive ? new Color(1f, 0.78f, 0.36f, 0.35f) : Color.clear;
                if (nv.Mark != null) nv.Mark.color = taken ? UiKit.Gold : open ? UiKit.Ink : new Color(0.55f, 0.52f, 0.48f);
                nv.Plus.enabled = open;
                // A keystone short of its 갈래 points is locked even when points are short too (its line says "갈래 9P 필요").
                bool gated = n.Kind == TreeNodeKind.Key && SkillTree.BranchPoints(h, n.Branch) < SkillTree.KeyThreshold[n.KeyIndex];
                nv.Lock.SetActive(!taken && !open && (block != "포인트 부족" || gated));
                nv.Ring.enabled = taken;
                if (nv.Check != null) nv.Check.SetActive(taken);
            }
            ShowCard();
        }

        /// <summary>The short line beside a skill or ultimate: owned, takeable, or what it waits for.</summary>
        static string Status(Hero h, TreeNode n)
        {
            bool ult = n.Kind == TreeNodeKind.Ultimate;
            if (SkillTree.Has(h, n.Id)) return ult ? "<color=#ffcf5a>선택함</color>" : "<color=#9fd28a>배움</color>";
            string block = SkillTree.Blocker(h, n);
            if (block == null) return ult ? "<color=#ffd38a>+ 고를 수 있음</color>" : "<color=#ffd38a>+ 배울 수 있음</color>";
            // The other one was picked: "포인트 부족" here read as a contradiction (critique 7).
            if (ult) foreach (var id in h.TreeNodes) if (id.StartsWith("u:", StringComparison.Ordinal)) return "<color=#8a8178>선택 안 함</color>";
            if (n.Kind == TreeNodeKind.Key)
            {
                int at = SkillTree.KeyThreshold[n.KeyIndex];
                if (SkillTree.BranchPoints(h, n.Branch) < at) return "<color=#d8907a>갈래 " + at + "P 필요</color>";
                return block == "선행 노드 필요" ? "<color=#d8907a>선행 스킬 필요</color>" : "<color=#8a8178>" + block + "</color>";
            }
            int need = n.Kind == TreeNodeKind.Ultimate ? SkillBook.UltimateLevel : SkillBook.Get(n.SkillId).Unlock;
            if (need > h.IntegratedLevel) return "<color=#d8907a>필요 Lv " + need + "</color>";
            if (block == "선행 노드 필요") return "<color=#d8907a>윗줄 스킬을 먼저</color>";
            if (block == "궁극기는 하나만 선택 가능") return "<color=#8a8178>다른 궁극기를 골랐음</color>";
            return "<color=#8a8178>" + block + "</color>";
        }

        /// <summary>The card shows the node pointed at, else the one clicked last, else this tab's first takeable (or owned) node.</summary>
        void ShowCard()
        {
            if (_classId == null) return;
            var n = _hovered ?? _selected ?? DefaultNode();
            if (n == null) return;
            Card(n);
            if (_selRing != null && _viewOf.TryGetValue(n, out var nv))
            {
                var rt = _selRing.rectTransform;
                rt.SetParent(nv.Holder.transform, false);
                rt.SetSiblingIndex(1); // over the glow, under the frame
                float hit = Mathf.Max(nv.Size, 40f), s = nv.Size + 4f;
                rt.anchoredPosition = new Vector2((hit - s) / 2f, -(hit - s) / 2f);
                rt.sizeDelta = new Vector2(s, s);
            }
        }

        TreeNode DefaultNode()
        {
            var h = _world.Hero;
            TreeNode owned = null, first = null;
            foreach (var nv in _nodes)
            {
                var n = nv.Node;
                if (!InTab(n, _tab)) continue;
                if (first == null) first = n;
                if (SkillTree.Has(h, n.Id)) { if (owned == null) owned = n; }
                else if (SkillTree.Blocker(h, n) == null) return n;
            }
            return owned ?? first;
        }

        void Card(TreeNode n)
        {
            var h = _world.Hero;
            var sb = new StringBuilder();
            string name;
            bool icon = true;
            switch (n.Kind)
            {
                case TreeNodeKind.Skill:
                {
                    var s = SkillBook.Get(n.SkillId);
                    name = s.Name;
                    // UX plan B2/B5: the shared skill card - what it does first, then the numbers (two ultimates read alike before).
                    sb.Append(SkillCard.Build(h, s, title: false));
                    sb.Append("\n<color=#8a8178>배우면 스킬 배치에 넣을 수 있습니다. 통합 레벨 ").Append(s.Unlock).Append("부터.</color>");
                    break;
                }
                case TreeNodeKind.Mod:
                    name = "변형 · " + SkillBook.Get(n.SkillId).Name;
                    sb.Append("<color=#ffd38a>").Append(SkillTree.ModText(n.Mod)).Append("</color>\n<color=#8a8178>이 스킬에만 걸립니다.</color>");
                    icon = false;
                    break;
                case TreeNodeKind.Passive:
                    name = "갈래 강화 · " + Talents.BranchName(h.ClassId, n.Branch);
                    sb.Append(n.Branch == 0 ? "피해·원소 피해·치명타·공격 속도" : n.Branch == 1 ? "생명력·받는 피해 감소·생명력 재생" : "공명 피해·연쇄 범위·공명 게이지")
                      .Append("\n<color=#8a8178>이 갈래 능력치 +").Append(SkillTree.PassiveWeight).Append("단계</color>");
                    icon = false;
                    break;
                case TreeNodeKind.Key:
                    name = "핵심 특성 · " + Talents.KeystoneName(n.Branch, n.KeyIndex);
                    sb.Append("<color=#ffd38a>").Append(Talents.KeystoneText(n.Branch, n.KeyIndex)).Append("</color>")
                      .Append("\n<color=#8a8178>갈래 포인트 ").Append(SkillTree.BranchPoints(h, n.Branch)).Append(" / ").Append(SkillTree.KeyThreshold[n.KeyIndex]).Append("</color>");
                    icon = false;
                    break;
                case TreeNodeKind.Evolution:
                {
                    var s = SkillBook.Get(n.SkillId);
                    var evo = s.EvolveTo != null ? SkillBook.Get(s.EvolveTo) : null;
                    name = "진화 · " + (evo != null ? evo.Name : s.Name);
                    sb.Append(s.Name).Append(Josa(s.Name, "이", "가")).Append(" 진화합니다.");
                    string change = evo != null ? GameText.Skill(evo.Id) : null;
                    if (!string.IsNullOrEmpty(change)) sb.Append("\n<color=#e8e0d0>").Append(change).Append("</color>");
                    sb.Append("\n<color=#a8d0ff>숙련 ").Append(SkillTree.MasteryOf(h, s.Id)).Append(" / ").Append(SkillTree.EvoMastery).Append("</color>");
                    break;
                }
                default:
                {
                    var s = SkillBook.Get(n.SkillId);
                    name = "궁극기 · " + s.Name;
                    // What it does, like any skill (loop 1: both ultimates said the same charge sentence and nothing else).
                    sb.Append(SkillCard.Build(h, s, title: false))
                      .Append("\n<color=#8a8178>스킬이 한 바퀴 돌 때마다 차오릅니다. 둘 중 하나만 고를 수 있습니다.</color>");
                    break;
                }
            }
            bool taken = SkillTree.Has(h, n.Id);
            string block = taken ? null : SkillTree.Blocker(h, n);
            if (block == "선행 노드 필요")
            {
                // Name what comes first (the 특성 tab's nodes hang off skills that live in the branch tabs).
                var parent = SkillTree.For(h.ClassId).Get(n.Parent);
                if (parent != null && parent.Kind == TreeNodeKind.Skill)
                {
                    string pn = SkillBook.Get(parent.SkillId).Name;
                    block = "먼저 「" + pn + "」" + Josa(pn, "을", "를") + " 배워야 합니다";
                }
            }
            sb.Append("\n\n").Append(taken ? "<color=#9fd28a>배움</color>" : block == null ? "<color=#ffd38a>클릭하면 배웁니다 · " + n.Cost + "포인트</color>" : "<color=#ff8a7a>" + block + "</color>");
            if (!taken && h.TreeAuto && block == null) sb.Append("\n<color=#8a8178>직접 찍으면 자동 분배가 꺼집니다</color>");
            _cardName.text = name;
            _cardBody.text = sb.ToString();
            _cardIcon.enabled = icon;
            _cardWell.enabled = icon;
            if (icon) _cardIcon.sprite = _ui.Catalog.Get(IconFor(n));
            // Without an icon the name and text move up into its place.
            _cardName.rectTransform.anchoredPosition = new Vector2(icon ? CardX + 94f : CardX + 12f, -74f);
            _cardName.rectTransform.sizeDelta = new Vector2(icon ? CardW - 124f : CardW - 42f, 40f);
            _cardBody.rectTransform.anchoredPosition = new Vector2(CardX + 12f, icon ? -156f : -100f);
        }

        /// <summary>The Korean particle by the last syllable: 받침 → <paramref name="with"/> (이/을/은), none → <paramref name="without"/>.</summary>
        static string Josa(string word, string with, string without)
        {
            if (string.IsNullOrEmpty(word)) return without;
            char c = word[word.Length - 1];
            return c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 != 0 ? with : without;
        }

        static string ElementName(Element e)
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

        static string RoleName(SkillRole r)
        {
            switch (r)
            {
                case SkillRole.Opener: return "개시 스킬";
                case SkillRole.Extender: return "연계 스킬";
                case SkillRole.Finisher: return "종결 스킬";
                case SkillRole.Defense: return "방어";
                case SkillRole.Ultimate: return "궁극기";
                default: return "특수";
            }
        }
    }
}
