using System;
using System.Collections.Generic;
using System.Text;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// The skill tree laid out as Diablo II's (희승 11-22; the rules of 기억의 나무 are unchanged): one tab per branch;
    /// in a tab the branch's skills stand in rows from top to bottom, each row with its required level on the left and
    /// its two 변형 and its evolution to the right, a line linking each skill to the next; on the right the two ultimates,
    /// the branch's three keystones and its 갈래 강화. Taken nodes light in the branch colour, takeable ones
    /// breathe, locked ones stay dark. Hover explains a node; click takes it (자동 분배 goes off). Top: points, 자동 분배,
    /// reset. Right: the card and the ring in use. T toggles it.
    /// </summary>
    public sealed class SkillTreePanel
    {
        const float W = 880f, H = 500f, TreeW = 600f;
        readonly UiKit _ui;
        readonly SimWorld _world;
        readonly RectTransform _win, _treeLayer;
        readonly Text _title, _points, _autoLabel, _cardName, _cardBody;
        // Labels Refresh rewrites (loop 1 P1-13): a row's name follows its evolution, the heads count what is taken.
        readonly List<(Text label, string skillId)> _rowNames = new List<(Text, string)>();
        readonly Text[] _passiveHeads = new Text[3];
        Text _ultHead;
        Text _resetLabel;
        float _resetAskedAt = -10f;
        readonly Image _cardIcon, _cardFrame;
        readonly Image[] _ringIcons = new Image[4];
        public bool Visible { get; private set; }

        sealed class NodeView
        {
            public TreeNode Node;
            public Image Frame, Icon, Glow;
            public Text Mark;
            public GameObject Holder;
        }

        readonly List<NodeView> _nodes = new List<NodeView>();
        readonly List<(TreeNode child, Image line)> _edges = new List<(TreeNode, Image)>();
        string _classId;
        int _stamp = -1;
        TreeNode _hovered;
        readonly Color[] _branchColor = new Color[3];
        readonly Dictionary<string, Vector2> _pos = new Dictionary<string, Vector2>();
        readonly Button[] _tabs = new Button[3];
        readonly List<GameObject> _rowBits = new List<GameObject>();
        int _tab;

        public SkillTreePanel(UiKit ui, SimWorld world)
        {
            _ui = ui;
            _world = world;
            _win = ui.Window("Skill Tree", W, H, "스킬 트리", Toggle, out _title);
            UiKit.Fill(_win, "tree back", 8f, 30f, TreeW - 8f, H - 38f, new Color(0f, 0f, 0f, 0.35f));
            _treeLayer = UiKit.Rect(_win, "tree", 0f, 0f, TreeW, H);

            // Top bar: points, auto growth, reset.
            _points = UiKit.Label(_win, "", 16f, 34f, 300f, 14f, 12, TextAnchor.UpperLeft, UiKit.Gold);
            var auto = ui.Button(_win, "", 330f, 32f, 140f, 18f, () => { _world.SetTreeAuto(!_world.Hero.TreeAuto); Changed(); });
            _autoLabel = auto.GetComponentInChildren<Text>();
            // UX plan B6③: a reset is undone by nothing - the first click asks, a second within 3 s does it.
            var reset = ui.Button(_win, "초기화", 476f, 32f, 70f, 18f, null);
            var resetLabel = reset.GetComponentInChildren<Text>();
            reset.onClick.AddListener(() =>
            {
                if (Time.unscaledTime - _resetAskedAt < 3f) { _world.ResetTree(); Changed(); _resetAskedAt = -10f; if (resetLabel != null) resetLabel.text = "초기화"; return; }
                _resetAskedAt = Time.unscaledTime;
                if (resetLabel != null) resetLabel.text = "<color=#ff8a70>정말?</color>";
            });
            _resetLabel = resetLabel;
            // one tab per branch, as Diablo II's three skill tabs
            for (int b = 0; b < 3; b++)
            {
                int tab = b;
                _tabs[b] = ui.Button(_win, "", 16f + b * 194f, 56f, 188f, 22f, () => { _tab = tab; ShowTab(); });
            }

            // Detail card (right).
            const float cx = TreeW + 12f, cw = W - cx - 14f;
            UiKit.Fill(_win, "card", cx - 4f, 32f, cw + 8f, 300f, new Color(1f, 0.85f, 0.6f, 0.05f));
            _cardFrame = ui.Sprite(_win, "ui/ui_slot_rare", cx, 40f, 72f, 72f);
            _cardFrame.preserveAspect = false;
            _cardIcon = ui.Sprite(_win, "icon/item_rune", cx + 4f, 44f, 64f, 64f);
            _cardName = UiKit.Label(_win, "", cx + 80f, 42f, cw - 80f, 40f, 12, TextAnchor.UpperLeft, UiKit.Gold);
            _cardBody = UiKit.Label(_win, "", cx, 120f, cw, 200f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            _cardBody.lineSpacing = 1.12f;

            // The ring in use.
            UiKit.Label(_win, "현재 스킬 배치", cx, H - 92f, cw, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            for (int i = 0; i < 4; i++)
            {
                var f = ui.Sprite(_win, "ui/ui_slot_common", cx + i * 58f, H - 76f, 52f, 52f);
                f.preserveAspect = false;
                _ringIcons[i] = ui.Sprite(_win, "icon/item_rune", cx + i * 58f + 10f, H - 66f, 32f, 32f);
            }
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

        const float RowTop = 112f, RowH = 54f, SkillX = 92f, ModX = 152f, EvoX = 214f;

        /// <summary>Where each node of a branch stands in its tab (centres, UI px from the window's top-left).</summary>
        void Layout(TreeDef t)
        {
            _pos.Clear();
            for (int b = 0; b < 3; b++)
            {
                int row = 0, passive = 0;
                foreach (var n in t.Nodes)
                {
                    if (n.Branch != b) continue;
                    switch (n.Kind)
                    {
                        case TreeNodeKind.Skill:
                            float y = RowTop + row * RowH;
                            _pos[n.Id] = new Vector2(SkillX, y);
                            foreach (var m in t.Nodes)
                            {
                                if (m.Parent != n.Id) continue;
                                if (m.Kind == TreeNodeKind.Mod) _pos[m.Id] = new Vector2(ModX + (m.Id.EndsWith(":1", StringComparison.Ordinal) ? 26f : 0f), y);
                                else if (m.Kind == TreeNodeKind.Evolution) _pos[m.Id] = new Vector2(EvoX, y);
                            }
                            row++;
                            break;
                        case TreeNodeKind.Passive:
                            _pos[n.Id] = new Vector2(452f + passive++ * 24f, 336f);
                            break;
                        case TreeNodeKind.Key:
                            _pos[n.Id] = new Vector2(462f + n.KeyIndex * 56f, 246f);
                            break;
                    }
                }
            }
            int u = 0;
            foreach (var n in t.Nodes) if (n.Kind == TreeNodeKind.Ultimate) _pos[n.Id] = new Vector2(470f + u++ * 60f, 124f);
        }

        /// <summary>Only the open tab's nodes, lines and row labels show; the ultimates always do.</summary>
        void ShowTab()
        {
            foreach (var nv in _nodes)
                nv.Holder.SetActive(nv.Node.Kind == TreeNodeKind.Ultimate || (nv.Node.Branch == _tab && nv.Node.Kind != TreeNodeKind.Root));
            foreach (var (child, line) in _edges) line.gameObject.SetActive(child.Branch == _tab);
            foreach (var go in _rowBits) go.SetActive(!go.name.Contains("#") || go.name.EndsWith("#" + _tab, StringComparison.Ordinal));
            for (int b = 0; b < 3; b++) _tabs[b].GetComponentInChildren<Text>().color = b == _tab ? UiKit.Gold : UiKit.Dim;
        }

        void Build()
        {
            // Node objects go with their holders (Render destroys those on a class change); edges are separate.
            foreach (var e in _edges) UnityEngine.Object.Destroy(e.line.gameObject);
            foreach (var go in _rowBits) UnityEngine.Object.Destroy(go);
            _rowBits.Clear();
            _rowNames.Clear();
            _nodes.Clear();
            _edges.Clear();
            _classId = _world.Hero.ClassId;
            var t = SkillTree.For(_classId);
            Layout(t);
            for (int b = 0; b < 3; b++) _tabs[b].GetComponentInChildren<Text>().text = Talents.BranchName(_classId, b);
            // row labels: the level each skill opens at (Diablo II's 1 · 6 · 12 ... down the left), the foot's headings
            foreach (var n in t.Nodes)
            {
                if (n.Kind != TreeNodeKind.Skill) continue;
                var pp = Pos(n);
                var lv = UiKit.Label(_treeLayer, "Lv " + SkillBook.Get(n.SkillId).Unlock, 14f, pp.y - 6f, 48f, 12f, 8, TextAnchor.UpperRight, UiKit.Dim);
                lv.gameObject.name = "row#" + n.Branch;
                _rowBits.Add(lv.gameObject);
                var nm = UiKit.Label(_treeLayer, SkillBook.Get(n.SkillId).Name, EvoX + 30f, pp.y - 6f, 196f, 12f, 12, TextAnchor.UpperLeft, UiKit.Ink);
                nm.horizontalOverflow = HorizontalWrapMode.Overflow;
                nm.gameObject.name = "row#" + n.Branch;
                _rowBits.Add(nm.gameObject);
                _rowNames.Add((nm, n.SkillId));
            }
            for (int b = 0; b < 3; b++)
            {
                var head = _passiveHeads[b] = UiKit.Label(_treeLayer, "갈래 강화", 440f, 306f, 160f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
                head.gameObject.name = "row#" + b;
                _rowBits.Add(head.gameObject);
                // Spelled as a condition, in a readable colour (loop 2: "갈래 포인트 4 · 9 · 15" was near-invisible grey).
                // One line that fits (critique 5 P0: the long sentence wrapped under the I node).
                var keys = UiKit.Label(_treeLayer, "핵심 특성  <color=#c8c0a8>" + SkillTree.KeyThreshold[0] + " · " + SkillTree.KeyThreshold[1] + " · " + SkillTree.KeyThreshold[2] + "포인트</color>",
                    440f, 210f, 220f, 16f, 12, TextAnchor.UpperLeft, UiKit.Dim);
                keys.horizontalOverflow = HorizontalWrapMode.Overflow;
                keys.gameObject.name = "row#" + b;
                _rowBits.Add(keys.gameObject);
            }
            var ult = _ultHead = UiKit.Label(_treeLayer, "궁극기  <color=#6f6860>Lv" + SkillBook.UltimateLevel + " · 하나만</color>", 440f, 84f, 160f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            ult.horizontalOverflow = HorizontalWrapMode.Overflow;
            _rowBits.Add(ult.gameObject);
            ult.gameObject.name = "ult";
            // Names under the right-hand nodes (loop 2: keystones and the second ultimate were pictures only).
            foreach (var n in t.Nodes)
            {
                if (n.Kind != TreeNodeKind.Key && n.Kind != TreeNodeKind.Ultimate) continue;
                var pp = Pos(n);
                string label = n.Kind == TreeNodeKind.Key ? Talents.KeystoneName(n.Branch, n.KeyIndex) : SkillBook.Get(n.SkillId).Name;
                var under = UiKit.Label(_treeLayer, label, pp.x - 30f, pp.y + 22f, 60f, 20f, 8, TextAnchor.UpperCenter, UiKit.Dim);
                under.gameObject.name = n.Kind == TreeNodeKind.Key ? "row#" + n.Branch : "ult name";
                _rowBits.Add(under.gameObject);
            }

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

            // Edges first (under the nodes).
            foreach (var n in t.Nodes)
            {
                if (n.Parent == null) continue;
                if (n.Kind == TreeNodeKind.Ultimate || n.Kind == TreeNodeKind.Passive || n.Kind == TreeNodeKind.Key) continue;
                var p = t.Get(n.Parent);
                if (p == null || p.Kind == TreeNodeKind.Root || p.Kind == TreeNodeKind.Passive) continue;
                var line = UiKit.Fill(_treeLayer, "edge", 0f, 0f, 1f, 1f, Color.clear);
                var a = Pos(p);
                var b = Pos(n);
                var d = b - a;
                var rt = line.rectTransform;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(a.x, -a.y);
                bool trunk = n.Kind == TreeNodeKind.Skill || n.Kind == TreeNodeKind.Ultimate;
                rt.sizeDelta = new Vector2(d.magnitude, trunk ? 4f : 2f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg);
                _edges.Add((n, line));
            }
            foreach (var n in t.Nodes) _nodes.Add(MakeNode(n));
            ShowTab();
        }

        NodeView MakeNode(TreeNode n)
        {
            // Drawn frame sizes (art 1:1): skill 44, evolution 52, keystone 56, 변형 20, passive 16, root lantern 48×64.
            bool drawnRoot = n.Kind == TreeNodeKind.Root && _ui.Catalog.Has("ui/ui2_tree_lantern");
            float size = n.Kind == TreeNodeKind.Root ? 48f : n.Kind == TreeNodeKind.Key ? 48f : n.Kind == TreeNodeKind.Mod ? 20f
                : n.Kind == TreeNodeKind.Passive ? 16f : n.Kind == TreeNodeKind.Evolution ? 48f : 44f;
            float height = drawnRoot ? 64f : size;
            var p = Pos(n);
            var holder = UiKit.Rect(_treeLayer, n.Id, p.x - size / 2f, p.y - height / 2f, size, height);
            var nv = new NodeView { Node = n, Holder = holder.gameObject };
            nv.Glow = UiKit.Fill(holder, "glow", -10f, (height - size) / 2f - 10f, size + 20f, size + 20f, Color.clear);
            nv.Glow.sprite = UiKit.Glow(_ui, 64); // soft halo, not a flat disc
            var frameImg = UiKit.Rect(holder, "frame", 0f, 0f, size, height).gameObject.AddComponent<Image>();
            nv.Frame = frameImg;
            nv.Frame.sprite = FrameFor(n);
            nv.Frame.raycastTarget = true;
            // The drawn lantern is the root itself; other kinds that stand for a skill carry its icon.
            if (n.Kind == TreeNodeKind.Skill || n.Kind == TreeNodeKind.Evolution || n.Kind == TreeNodeKind.Ultimate || (n.Kind == TreeNodeKind.Root && !drawnRoot))
            {
                float io = Mathf.Floor((size - 32f) / 2f);
                nv.Icon = _ui.Sprite(holder, IconFor(n), io, io, 32f, 32f);
            }
            if (n.Kind == TreeNodeKind.Key)
                nv.Mark = UiKit.Label(holder, Roman(n.KeyIndex), 0f, 0f, size, size, 12, TextAnchor.MiddleCenter, UiKit.Gold);
            var ptr = frameImg.gameObject.AddComponent<UiPointer>();
            ptr.Enter += () => { _hovered = n; Card(n); };
            ptr.Exit += () => { if (_hovered == n) _hovered = null; };
            ptr.LeftClick += () => { if (_world.TakeTreeNode(n.Id)) Changed(); else Card(n); };
            // The node's root object holds glow, frame and icon; destroying the frame's parent removes them all.
            nv.Frame = frameImg;
            _holders.Add(holder.gameObject);
            return nv;
        }

        readonly List<GameObject> _holders = new List<GameObject>();

        Sprite FrameFor(TreeNode n)
        {
            string key = n.Kind == TreeNodeKind.Root ? "ui/ui2_tree_lantern" : n.Kind == TreeNodeKind.Key ? "ui/ui2_node_key"
                : n.Kind == TreeNodeKind.Mod ? "ui/ui2_node_mod" : n.Kind == TreeNodeKind.Passive ? "ui/ui2_node_passive"
                : n.Kind == TreeNodeKind.Evolution ? "ui/ui2_node_evo" : "ui/ui2_node_skill";
            if (_ui.Catalog.Has(key)) return _ui.Catalog.Get(key);
            // Until the drawn frames arrive: crisp procedural shapes.
            switch (n.Kind)
            {
                case TreeNodeKind.Mod: return UiKit.Diamond(_ui, 16);
                case TreeNodeKind.Passive: return UiKit.Disc(_ui, 12, 0);
                case TreeNodeKind.Key: return UiKit.Star(_ui, 40);
                case TreeNodeKind.Root: return UiKit.Disc(_ui, 48, 3);
                default: return UiKit.Disc(_ui, 40, 3);
            }
        }

        string IconFor(TreeNode n)
        {
            if (n.Kind == TreeNodeKind.Root)
            {
                var basic = SkillBook.BasicFor(_classId);
                return _ui.Catalog.Has("icon/skill_" + basic.Id) ? "icon/skill_" + basic.Id : "icon/item_sword";
            }
            var s = SkillBook.Get(n.SkillId);
            string id = n.Kind == TreeNodeKind.Evolution && s.EvolveTo != null ? s.EvolveTo : s.Id;
            return _ui.Catalog.Has("icon/skill_" + id) ? "icon/skill_" + id : "icon/skill_" + s.BaseId;
        }

        static string Roman(int k) => k == 0 ? "I" : k == 1 ? "II" : "III";

        // ---------- per frame ----------

        public void Render()
        {
            if (!Visible) return;
            var h = _world.Hero;
            if (_classId != h.ClassId)
            {
                foreach (var go in _holders) UnityEngine.Object.Destroy(go);
                _holders.Clear();
                Build();
            }
            float t = Time.unscaledTime;
            int stamp = h.TreeOrder.Count * 7919 + SkillTree.Points(h) * 31 + (h.TreeAuto ? 1 : 0);
            foreach (var id in h.TreeOrder) stamp = unchecked(stamp * 31 + id.GetHashCode());
            foreach (var kv in h.SkillUses) stamp = unchecked(stamp * 17 + SkillBook.SkillLevel(kv.Value));
            if (stamp != _stamp) { _stamp = stamp; Refresh(); }
            // Takeable nodes breathe (four steps a second keeps the canvas calm).
            float pulse = 0.3f + 0.2f * Mathf.Sin(Mathf.Floor(t * 4f) / 4f * 4f);
            foreach (var nv in _nodes)
                if (!SkillTree.Has(h, nv.Node.Id) && SkillTree.Blocker(h, nv.Node) == null)
                    nv.Glow.color = new Color(1f, 0.85f, 0.5f, pulse);
        }

        void Refresh()
        {
            var h = _world.Hero;
            int points = SkillTree.Points(h), spent = SkillTree.Spent(h);
            _title.text = "스킬 트리 · " + SkillBook.ClassName(h.ClassId);
            // "남은 포인트 0 (받은 50)": 50 / 50 read as either all spent or all left (loop 2).
            _points.text = "남은 포인트 " + (points > spent ? "<color=#9fd28a>" + (points - spent) + "</color>" : "0")
                + "  <color=#8a8178>(받은 " + points + " = 레벨 " + h.IntegratedLevel + " + 처음 " + SkillTree.StartBonus + ")</color>";
            _points.horizontalOverflow = HorizontalWrapMode.Overflow;
            var tree = SkillTree.For(h.ClassId);
            foreach (var (label, id) in _rowNames)
            {
                var def = SkillBook.Get(id);
                bool evolved = def.EvolveTo != null && h.Evolved.Contains(def.BaseId);
                label.text = evolved ? def.Name + "  <color=#ffcf5a>→ " + SkillBook.Get(def.EvolveTo).Name + "</color>" : def.Name;
            }
            for (int b = 0; b < 3; b++)
            {
                if (_passiveHeads[b] == null) continue;
                int have = 0, all = 0;
                foreach (var n in tree.Nodes)
                    if (n.Kind == TreeNodeKind.Passive && n.Branch == b) { all++; if (SkillTree.Has(h, n.Id)) have++; }
                _passiveHeads[b].text = "갈래 강화  <color=#6f6860>" + have + " / " + all + "</color>";
            }
            if (_ultHead != null)
            {
                string chosen = null;
                foreach (var n in tree.Nodes)
                    if (n.Kind == TreeNodeKind.Ultimate && SkillTree.Has(h, n.Id)) chosen = SkillBook.Get(n.SkillId).Name;
                _ultHead.text = chosen != null ? "궁극기  <color=#ffcf5a>" + chosen + "</color>" : "궁극기  <color=#6f6860>Lv" + SkillBook.UltimateLevel + " · 하나만</color>"; // one line: the card starts at 600
            }
            _autoLabel.text = h.TreeAuto ? "자동 분배: 켬" : "자동 분배: 끔";
            _autoLabel.color = h.TreeAuto ? UiKit.Good : UiKit.Dim;

            foreach (var (child, line) in _edges)
            {
                bool lit = SkillTree.Has(h, child.Id);
                var c = child.Branch >= 0 ? _branchColor[child.Branch] : UiKit.Gold;
                line.color = lit ? new Color(c.r, c.g, c.b, 0.95f) : new Color(0.24f, 0.2f, 0.17f, 0.9f);
            }
            foreach (var nv in _nodes)
            {
                var n = nv.Node;
                bool taken = SkillTree.Has(h, n.Id);
                bool open = !taken && SkillTree.Blocker(h, n) == null;
                var c = n.Branch >= 0 ? _branchColor[n.Branch] : UiKit.Gold;
                nv.Frame.color = taken ? Color.white : open ? new Color(0.85f, 0.82f, 0.78f) : new Color(0.35f, 0.33f, 0.32f);
                if (nv.Icon != null) nv.Icon.color = taken ? Color.white : open ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.25f, 0.25f, 0.25f);
                nv.Glow.color = taken ? new Color(c.r, c.g, c.b, n.Kind == TreeNodeKind.Passive || n.Kind == TreeNodeKind.Mod ? 0.85f : 0.7f) : Color.clear;
                if (nv.Mark != null) nv.Mark.color = taken ? UiKit.Gold : new Color(0.5f, 0.47f, 0.42f);
            }
            for (int i = 0; i < 4; i++)
            {
                var s = h.Ring.Slots[i];
                _ringIcons[i].enabled = s != null;
                if (s != null) _ringIcons[i].sprite = _ui.Catalog.Get(_ui.Catalog.Has("icon/skill_" + s.Id) ? "icon/skill_" + s.Id : "icon/skill_" + s.BaseId);
            }
            if (_hovered != null) Card(_hovered);
            else
            {
                // Nothing pointed at: how to read the tree.
                // Critique 5: the card is for the node pointed at; with none, only the three things a reader needs.
                _cardName.text = "칸에 마우스를 올려 보세요";
                _cardBody.text = "<color=#8a8178>설명이 여기 나옵니다. 클릭하면 포인트 1을 넣습니다.</color>\n\n"
                    + "<color=#ffcf5a>●</color> 큰 칸  스킬\n<color=#ffcf5a>·</color> 작은 칸  그 스킬의 변형\n<color=#ffcf5a>◆</color> 오른쪽 칸  진화 (숙련 " + SkillTree.EvoMastery + ")";
                _cardIcon.enabled = false;
                _cardFrame.enabled = false;
            }
        }

        void Card(TreeNode n)
        {
            var h = _world.Hero;
            var sb = new StringBuilder();
            string name;
            bool icon = true;
            switch (n.Kind)
            {
                case TreeNodeKind.Root:
                    name = "등불 · 기본 공격";
                    sb.Append("모든 가지가 이곳에서 자라납니다.");
                    break;
                case TreeNodeKind.Skill:
                {
                    var s = SkillBook.Get(n.SkillId);
                    name = s.Name;
                    // UX plan B2/B5: the shared skill card - what it does first, then the numbers (two ultimates read alike before).
                    sb.Append(SkillCard.Build(h, s, title: false));
                    sb.Append("\n<color=#8a8178>열면 스킬 칸에 넣을 수 있습니다. 통합 레벨 ").Append(s.Unlock).Append("부터.</color>");
                    break;
                }
                case TreeNodeKind.Mod:
                    name = "변형 · " + SkillBook.Get(n.SkillId).Name;
                    sb.Append("<color=#ffd38a>").Append(SkillTree.ModText(n.Mod)).Append("</color>\n<color=#8a8178>이 스킬에만 걸린다.</color>");
                    icon = false;
                    break;
                case TreeNodeKind.Passive:
                    name = "갈래 위력 · " + Talents.BranchName(h.ClassId, n.Branch);
                    sb.Append(n.Branch == 0 ? "피해·원소 피해·치명타·공격 속도" : n.Branch == 1 ? "생명력·받는 피해 감소·생명력 재생" : "공명 피해·연쇄 범위·공명 게이지")
                      .Append("\n<color=#8a8178>이 갈래 능력치 +").Append(SkillTree.PassiveWeight).Append("단계</color>");
                    icon = false;
                    break;
                case TreeNodeKind.Key:
                    name = "핵심 · " + Talents.KeystoneName(n.Branch, n.KeyIndex);
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
            sb.Append("\n\n").Append(taken ? "<color=#9fd28a>습득함</color>" : block == null ? "<color=#ffd38a>클릭하여 습득 · " + n.Cost + "포인트</color>" : "<color=#ff8a7a>" + block + "</color>");
            if (!taken && h.TreeAuto && block == null) sb.Append("\n<color=#6f6a64>찍으면 자동 분배가 꺼진다</color>");
            _cardName.text = name;
            _cardBody.text = sb.ToString();
            _cardIcon.enabled = icon;
            _cardFrame.enabled = icon;
            if (icon) _cardIcon.sprite = _ui.Catalog.Get(IconFor(n));
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
