using System;
using System.Collections.Generic;
using System.Text;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 장비 창 (희승 11-22: Diablo II's inventory) — the paper doll on top (weapon left, helm and armour in the middle, the
    /// hero where the shield would hang, gloves, rings, belt and boots below), a 10 × 4 bag under it and the purse at the
    /// foot, 자동 분해 and the hand buttons under it; the numbers live in the character window (C) on the left half, this
    /// window on the right half, as in Diablo II. Bag cells carry rarity, ★,
    /// sockets, locks, the compare arrow (▲▲ ▲ ▼ ◆) and a "new" bar; hover shows the tooltip with the answer line, each
    /// stat's change and the worn piece beside it.
    /// Click and right click equip (Diablo's habit), Shift+click locks, Ctrl+click salvages (twice for legendary, ★ or
    /// unique pieces; Z takes it back for five seconds). Tab toggles it,
    /// F steps 자동 분해.
    /// The head (UX plan A2, A6) says what the game does on its own here: the 자동 장착 switch with one sentence on why
    /// the bag holds no ▲, 직전 교체 되돌리기 for its last swap, and "?" for the rarity legend.
    /// </summary>
    public sealed class GearPanel
    {
        // 희승 11-24 "너무 따닥따닥 붙어있고 … 자동 분해는 설정창 아이콘 달고 설정창 열어서": one job per window part — tabs on top,
        // the doll, the bag with room between its cells, one foot line; 자동 장착·자동 분해 behind the ⚙ button, the hand
        // controls and the rarity legend behind "?".
        const float W = 440f, H = 570f, Cell = 36f, Pitch = 40f;
        const int Cols = Bag.Cols, Rows = Bag.Capacity / Bag.Cols;

        readonly UiKit _ui;
        readonly SimWorld _world;
        readonly RectTransform _win, _bagContent;
        readonly ScrollRect _scroll;
        readonly Text _title, _bagCount, _ash;
        readonly Image _hero, _plinth;
        readonly SlotView[] _spots = new SlotView[9];
        readonly List<SlotView> _cells = new List<SlotView>();
        readonly RectTransform _doll;
        // 자동 분해 (희승 11-22): off, or every drop up to common / magic / rare goes to ash as it falls.
        readonly Button[] _junk = new Button[4];
        static readonly string[] JunkNames = { "끔", "일반", "마법 이하", "희귀 이하" };
        readonly Text _salvageAllLabel;
        readonly Text _hint;
        readonly Text _undoLabel;
        readonly GameObject _undoButton;
        readonly Button[] _autoEquip = new Button[2];
        RectTransform _legend, _settings;
        /// <summary>When a one-off message in the hint line gives way to the hand controls again (0 = none up).</summary>
        float _hintUntil;
        readonly ItemTooltip _tip, _compare;
        readonly HashSet<int> _seen = new HashSet<int>();
        public bool Visible { get; private set; }

        // ---------- 룬 보관함 (재기획 §5.7) ----------
        RectTransform _bagView, _runePage;
        bool _runeTab;
        Text _bagTabText, _runeTabText;
        Image _bagTabBar, _runeTabBar;
        RectTransform _bagHead;
        readonly Image[] _runeIcon = new Image[Runes.Count];
        readonly Text[] _runeCount = new Text[Runes.Count];
        readonly Text[] _wordName = new Text[Runes.Words.Length];
        readonly Image[][] _wordChip = new Image[Runes.Words.Length][];
        readonly Text[][] _wordChipText = new Text[Runes.Words.Length][];
        Text _runeInfo;

        /// <summary>Each rune's colour (its element where it has one).</summary>
        static Color RuneColor(Rune r)
        {
            switch (r)
            {
                case Rune.Ash: return new Color(1f, 0.55f, 0.25f);
                case Rune.Frost: return new Color(0.6f, 0.85f, 1f);
                case Rune.Thunder: return new Color(1f, 0.92f, 0.4f);
                case Rune.Rot: return new Color(0.6f, 0.9f, 0.35f);
                case Rune.Void: return new Color(0.72f, 0.5f, 1f);
                case Rune.Lantern: return new Color(1f, 0.88f, 0.62f);
                case Rune.Oath: return new Color(1f, 0.8f, 0.35f);
                case Rune.Blood: return new Color(0.95f, 0.3f, 0.3f);
                case Rune.Star: return new Color(0.8f, 0.78f, 1f);
                case Rune.Wind: return new Color(0.5f, 0.95f, 0.8f);
                case Rune.Bone: return new Color(0.92f, 0.88f, 0.78f);
                default: return new Color(0.62f, 0.68f, 0.8f); // 문
            }
        }

        void BuildRunePage(float bx, float by, float bw, float tx, float ty)
        {
            // Two tabs under the title (11-24: they sat at the foot among the action buttons and read as two more buttons):
            // 장비 (doll and bag) and 룬 보관함, the open one marked by a gold bar under it, a rule across the window under both.
            var bagTab = _ui.Button(_win, "장비", tx, ty, 96f, 22f, () => ShowRunes(false));
            var runeTab = _ui.Button(_win, "룬 보관함", tx + 100f, ty, 110f, 22f, () => ShowRunes(true));
            _bagTabText = bagTab.GetComponentInChildren<Text>();
            _runeTabText = runeTab.GetComponentInChildren<Text>();
            UiKit.Fill(_win, "tab rule", 12f, ty + 22f, W - 24f, 1f, UiKit.Line);
            _bagTabBar = UiKit.Fill(_win, "tab bar", tx, ty + 20f, 96f, 3f, UiKit.Gold);
            _runeTabBar = UiKit.Fill(_win, "tab bar", tx + 100f, ty + 20f, 110f, 3f, UiKit.Gold);

            _runePage = UiKit.Rect(_win, "rune page", bx, by, bw + 2f, Pitch * 7f);
            var back = _runePage.gameObject.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.001f);
            // Twelve runes, 6 x 2: what one adds, how many are in the hoard; never-found ones are dark silhouettes.
            for (int i = 1; i < Runes.Count; i++)
            {
                int col = (i - 1) % 6, row = (i - 1) / 6;
                float x = col * 50f + 4f, y = row * 52f;
                var r = (Rune)i;
                int index = i;
                var cell = _ui.Sprite(_runePage, "ui/ui2_cell", x, y, 40f, 40f, sliced: true);
                cell.preserveAspect = false;
                cell.raycastTarget = true;
                _runeIcon[i] = _ui.Sprite(_runePage, "icon/item_rune", x + 4f, y + 4f, 32f, 32f);
                UiKit.Label(_runePage, Runes.Name(r), x - 4f, y + 40f, 48f, 10f, 8, TextAnchor.UpperCenter, UiKit.Dim);
                _runeCount[i] = UiKit.Label(_runePage, "", x, y + 27f, 37f, 12f, 8, TextAnchor.LowerRight, UiKit.Ink);
                var p = cell.gameObject.AddComponent<UiPointer>();
                p.Enter = () =>
                {
                    var (stat, value) = Runes.StatOf(r);
                    _runeInfo.text = "<color=#" + ColorUtility.ToHtmlStringRGB(RuneColor(r)) + ">" + Runes.Name(r) + " 룬</color>  "
                        + ItemStyle.Line(new AffixRoll { Stat = stat, Value = value }, false) + "  <color=#8a8178>보유 " + _world.Bag.RuneCounts[index] + "</color>";
                };
                p.Exit = () => _runeInfo.text = RuneHint;
                p.LeftClick = () => Hold(r);
            }
            UiKit.Fill(_runePage, "rule", 0f, 104f, bw, 1f, UiKit.Line);
            UiKit.Label(_runePage, "룬어", 0f, 107f, 100f, 12f, 12, TextAnchor.UpperLeft, UiKit.Gold, title: true);
            // Ten runewords in two columns: name, then the recipe as chips lit by what the hoard already holds.
            for (int w = 0; w < Runes.Words.Length; w++)
            {
                var def = Runes.Words[w];
                float x = (w % 2) * 152f, y = 124f + (w / 2) * 24f;
                _wordName[w] = UiKit.Label(_runePage, "", x, y, 150f, 10f, 8, TextAnchor.UpperLeft, UiKit.Dim);
                _wordChip[w] = new Image[def.Recipe.Length];
                _wordChipText[w] = new Text[def.Recipe.Length];
                for (int c = 0; c < def.Recipe.Length; c++)
                {
                    var chip = UiKit.Fill(_runePage, "chip", x + c * 14f, y + 11f, 12f, 12f, Color.white);
                    chip.sprite = UiKit.Disc(_ui, 12, 0);
                    _wordChip[w][c] = chip;
                    _wordChipText[w][c] = UiKit.Label(chip.transform, Runes.Name(def.Recipe[c]).Substring(0, 1), 0f, 0f, 12f, 12f, 8, TextAnchor.MiddleCenter, Color.black);
                    _wordChipText[w][c].GetComponent<Shadow>().enabled = false;
                }
                int captured = w;
                var hit = UiKit.Rect(_runePage, "word hit", x, y, 150f, 24f).gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var wp = hit.gameObject.AddComponent<UiPointer>();
                wp.Enter = () => _runeInfo.text = "<color=#ffcf5a>" + Runes.Words[captured].Name + "</color>  " + Runes.Words[captured].Text
                    + "  <color=#8a8178>소켓 " + Runes.Words[captured].Recipe.Length + "칸 장비</color>";
                wp.Exit = () => _runeInfo.text = RuneHint;
            }
            _runeInfo = UiKit.Label(_runePage, RuneHint, 0f, Pitch * 7f - 12f, bw, 12f, 8, TextAnchor.UpperLeft, UiKit.Dim);
            _runePage.gameObject.SetActive(false);
        }

        const string RuneHint = "룬을 클릭해 집은 뒤 장비를 클릭하면 빈 소켓에 꽂힙니다";
        /// <summary>The hand controls, told once in the "?" box (11-24: a line at the window's foot every time it opened).</summary>
        const string HandControls = "클릭 · 우클릭: 장착 · Shift+클릭: 잠금 · Ctrl+클릭: 분해 · Z: 분해 되돌리기";
        /// <summary>The foot line is empty unless it has something to say (a held rune, a refusal, an undo).</summary>
        const string HandHint = "";

        /// <summary>A rune picked from the hoard, waiting for a piece of gear to be clicked (Diablo: the rune on the cursor).</summary>
        Rune _held = Rune.None;

        void Hold(Rune r)
        {
            if (_world.Bag.RuneCounts[(int)r] <= 0) { _runeInfo.text = Runes.Name(r) + " 룬이 없습니다"; return; }
            _held = r;
            ShowRunes(false);
            _hint.text = "<color=#" + ColorUtility.ToHtmlStringRGB(RuneColor(r)) + ">" + Runes.Name(r) + " 룬</color>을 꽂을 장비를 클릭하세요 · 우클릭: 취소";
        }

        void Release()
        {
            _held = Rune.None;
            _hint.text = HandHint;
        }

        void SocketHeld(SlotView cell)
        {
            var it = cell.Item;
            if (it == null) return;
            if (it.Sockets == 0) { Hover(cell, Array.IndexOf(_spots, cell) >= 0, "<color=#ffb070>소켓이 없는 장비입니다</color>"); return; }
            if (it.RuneSlots.Count >= it.Sockets) { Hover(cell, Array.IndexOf(_spots, cell) >= 0, "<color=#ffb070>소켓이 모두 찼습니다</color>"); return; }
            if (!_world.SocketRune(it, _held)) return;
            Release();
            Changed();
            Hover(cell, Array.IndexOf(_spots, cell) >= 0);
        }

        void ShowRunes(bool on)
        {
            _runeTab = on;
            _bagView.gameObject.SetActive(!on);
            _doll.gameObject.SetActive(!on);
            _bagHead.gameObject.SetActive(!on);
            _runePage.gameObject.SetActive(on);
            if (on) { _tip.Hide(); _compare.Hide(); _hovered = null; foreach (var c in _cells) c.ClearHover(); }
            _runeInfo.text = RuneHint;
            _stamp = -1;
        }

        void RebuildRunes()
        {
            var counts = _world.Bag.RuneCounts;
            var h = _world.Hero;
            for (int i = 1; i < Runes.Count; i++)
            {
                int n = counts[i];
                // Seen = in the hoard now, or part of a runeword already found.
                bool seen = n > 0;
                if (!seen) foreach (var w in Runes.Words) if (h.RunewordsFound.Contains(w.Id) && Array.IndexOf(w.Recipe, (Rune)i) >= 0) seen = true;
                var c = RuneColor((Rune)i);
                _runeIcon[i].color = seen ? Color.Lerp(Color.white, c, 0.55f) : new Color(0f, 0f, 0f, 0.7f);
                _runeCount[i].text = n > 0 ? "×" + n : "";
            }
            var equipped = new HashSet<string>();
            foreach (var it in h.Gear.Spots) if (it?.Runeword != null) equipped.Add(it.Runeword.Id);
            for (int w = 0; w < Runes.Words.Length; w++)
            {
                var def = Runes.Words[w];
                bool worn = equipped.Contains(def.Id);
                var left = (int[])counts.Clone();
                int have = 0;
                for (int c = 0; c < def.Recipe.Length; c++)
                {
                    int r = (int)def.Recipe[c];
                    bool lit = worn || left[r] > 0;
                    if (!worn && left[r] > 0) left[r]--;
                    if (lit) have++;
                    var col = RuneColor(def.Recipe[c]);
                    _wordChip[w][c].color = lit ? col : new Color(col.r * 0.25f, col.g * 0.25f, col.b * 0.25f, 0.9f);
                    _wordChipText[w][c].color = lit ? new Color(0.05f, 0.04f, 0.05f) : new Color(col.r, col.g, col.b, 0.6f);
                }
                bool found = h.RunewordsFound.Contains(def.Id);
                string state = worn ? "  <color=#9fd28a>장착 중</color>" : "  <color=#8a8178>(" + have + "/" + def.Recipe.Length + ")</color>";
                _wordName[w].text = (found ? "<color=#ffcf5a>" + def.Name + "</color>" : def.Name) + state;
            }
        }

        int _stamp = -1;
        SlotView _hovered;
        int _salvageArmedUid = -1;
        float _salvageArmedAt, _salvageAllArmedAt = -10f;

        static readonly EquipSpot[] DollOrder =
        {
            EquipSpot.Helm, EquipSpot.Weapon, EquipSpot.Gloves, EquipSpot.Boots, EquipSpot.Amulet,
            EquipSpot.Ring1, EquipSpot.Ring2, EquipSpot.Armor, EquipSpot.Belt,
        };

        static readonly string[] FilterNames = { "입문", "일반", "엄격", "엔드게임", "직업 추천", "★ 사냥", "수동" };

        public GearPanel(UiKit ui, SimWorld world)
        {
            _ui = ui;
            _world = world;
            _win = ui.Window("Gear Panel", W, H, "가방", Toggle, out _title, "ui/ui2_window_iron");
            _win.anchoredPosition = new Vector2(232f, UiKit.Lift(H)); // the right half; the character window takes the left

            // ---------- the head: ⚙ (자동 장착 · 자동 분해) and ? (등급 · 조작), left of the window's × ----------
            var gear = ui.Button(_win, "", W - 76f, 5f, 20f, 18f, ToggleSettings);
            gear.GetComponentInChildren<Text>().text = "";
            var gearIcon = ui.Sprite(gear.transform, "ui/ui3_menu_settings", 2f, 1f, 16f, 16f);
            gearIcon.raycastTarget = false;
            Tip(gear.gameObject, "설정: 자동 장착 · 자동 분해");
            var help = ui.Button(_win, "?", W - 52f, 5f, 20f, 18f, ToggleLegend);
            Tip(help.gameObject, "등급 색 · 화살표 · 조작법");

            // ---------- the paper doll (Diablo II's places; no off-hand here, so the hero stands where a shield would) ----------
            const float ox = 40f, oy = 70f, cw = 76f; // under the 장비 | 룬 보관함 tabs
            _doll = UiKit.Rect(_win, "doll", 0f, 0f, W, H);
            var at = new Dictionary<EquipSpot, Rect>
            {
                [EquipSpot.Weapon] = new Rect(ox, oy, cw, 152f),
                [EquipSpot.Helm] = new Rect(ox + 140f, oy, cw, cw),
                [EquipSpot.Amulet] = new Rect(ox + 226f, oy + 38f, 38f, 38f),
                [EquipSpot.Armor] = new Rect(ox + 140f, oy + 84f, cw, 112f),
                [EquipSpot.Gloves] = new Rect(ox, oy + 164f, cw, cw),
                [EquipSpot.Ring1] = new Rect(ox + 92f, oy + 204f, 38f, 38f),
                [EquipSpot.Belt] = new Rect(ox + 140f, oy + 204f, cw, 38f),
                [EquipSpot.Ring2] = new Rect(ox + 226f, oy + 204f, 38f, 38f),
                [EquipSpot.Boots] = new Rect(ox + 274f, oy + 164f, cw, cw),
            };
            var standBox = ui.Sprite(_doll, "ui/ui2_cell", ox + 274f, oy, cw, 152f, sliced: true);
            standBox.preserveAspect = false;
            var pool = UiKit.Fill(_doll, "light pool", ox + 262f, oy + 8f, 100f, 136f, new Color(1f, 0.74f, 0.42f, 0.3f));
            pool.sprite = UiKit.Glow(ui, 64);
            _plinth = ui.Sprite(_doll, "ui/ui2_plinth", ox + 278f, oy + 112f, 68f, 26f);
            _plinth.preserveAspect = false;
            _hero = ui.Sprite(_doll, "hero/" + world.Hero.ClassId, ox + 274f, oy + 30f, cw, cw);
            _hero.preserveAspect = false;
            foreach (var spot in DollOrder)
            {
                var r = at[spot];
                float icon = Mathf.Min(r.width, r.height) >= 70f ? 64f : 32f; // 32 px art at ×2 where the place is big
                var sv = new SlotView(ui, _doll, r.x, r.y, r.width, r.height, SlotStyle.Socket, "ui/ui2_cell", "ui/ui2_cell_hover", slicedFrame: true, iconSize: icon);
                _spots[(int)spot] = sv;
                var captured = sv;
                sv.Pointer.Enter += () => Hover(captured, worn: true);
                sv.Pointer.Exit += () => Unhover(captured);
                sv.Pointer.LeftClick += () =>
                {
                    if (_held != Rune.None) { SocketHeld(captured); return; }
                    if (Shift && captured.Item != null) { _world.ToggleLock(captured.Item.Uid); Changed(); }
                };
                sv.Pointer.RightClick += () => { if (_held != Rune.None) Release(); };
            }

            // ---------- the bag: ten across, four down, every place drawn (empty ones too) ----------
            const float bw = Cols * Pitch - 4f, bx = (W - bw) / 2f, by = oy + 278f;
            _bagHead = UiKit.Rect(_win, "bag head", 0f, 0f, W, H); // what the 룬 보관함 tab hides with the doll and the bag
            // Over the bag: its count on the left, its two actions on the right; 직전 교체 되돌리기 only while there is one.
            _bagCount = UiKit.Label(_bagHead, "", bx, by - 26f, 120f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            var undo = ui.Button(_bagHead, "", bx + 96f, by - 28f, UndoW, 19f, Undo);
            _undoButton = undo.gameObject;
            _undoLabel = undo.GetComponentInChildren<Text>();
            var undoHover = undo.gameObject.AddComponent<UiPointer>();
            undoHover.Enter = () => { if (_held == Rune.None) { _hint.text = UndoPreview(); _hintUntil = 0f; } };
            undoHover.Exit = () => { if (_held == Rune.None && _hintUntil <= 0f) _hint.text = HandHint; };
            var sort = ui.Button(_bagHead, "정렬", bx + bw - 140f, by - 28f, 52f, 19f, SortBag);
            Tip(sort.gameObject, "좋은 것부터: ▲▲ · ▲ · ⇄ · 등급 순");
            var salvage = ui.Button(_bagHead, "일괄 분해", bx + bw - 84f, by - 28f, 84f, 19f, SalvageAll);
            // 일괄 분해 gets a red frame because it destroys. Only this button's four edge strips are recoloured, not UiKit.
            foreach (var edge in salvage.GetComponentsInChildren<Image>()) if (edge.gameObject.name == "edge") edge.color = new Color(0.9f, 0.32f, 0.28f, 0.95f);
            _salvageAllLabel = salvage.GetComponentInChildren<Text>();
            Tip(salvage.gameObject, "잠근 것 · ★ · 전설 이상을 뺀 나머지를 잿 파편으로 (확인을 한 번 더 받음)");
            var chest = ui.Sprite(_bagHead, "ui/ui2_chest", bx - 10f, by - 10f, bw + 20f, Pitch * Rows + 18f, sliced: true);
            chest.preserveAspect = false;
            var view = UiKit.Rect(_win, "bag view", bx, by, bw + 2f, Pitch * Rows);
            var viewImg = view.gameObject.AddComponent<Image>();
            viewImg.color = new Color(0f, 0f, 0f, 0.001f);
            // Its own canvas: a breathing "new" bar or a drop dirties only the bag, not the whole window (review 11-21).
            view.gameObject.AddComponent<Canvas>();
            view.gameObject.AddComponent<GraphicRaycaster>();
            _bagContent = UiKit.Rect(view, "bag content", 0f, 0f, bw, Pitch * Rows);
            _scroll = view.gameObject.AddComponent<ScrollRect>(); // no scrolling past forty; kept so the wheel does nothing odd
            _scroll.content = _bagContent;
            _scroll.viewport = view;
            _scroll.horizontal = _scroll.vertical = false;
            _bagView = view;
            for (int i = 0; i < Bag.Capacity; i++) AddCell();

            // the purse at the foot, as Diablo's gold; the one message line under it
            float py = by + Pitch * Rows + 14f;
            var purse = ui.Sprite(_win, "icon/item_gold", bx, py - 4f, 24f, 24f);
            purse.preserveAspect = true;
            _ash = UiKit.Label(_win, "", bx + 28f, py, 220f, 14f, 12, TextAnchor.UpperLeft, UiKit.Gold);
            _hint = UiKit.Label(_win, HandHint, bx, H - 24f, bw, 14f, 12, TextAnchor.UpperCenter, new Color(0.78f, 0.72f, 0.62f));
            BuildRunePage(bx, oy, bw, bx, 34f);
            BuildSettings();

            _tip = new ItemTooltip(ui, "Item Tooltip");
            _compare = new ItemTooltip(ui, "Compare Tooltip");
            foreach (var it in world.Bag.Items) _seen.Add(it.Uid);
            SetVisible(false);
        }

        static bool Shift
        {
            get
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.shiftKey.isPressed;
            }
        }

        static bool Ctrl
        {
            get
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.ctrlKey.isPressed;
            }
        }

        public void Toggle() => SetVisible(!Visible);

        void SetVisible(bool v)
        {
            if (Visible && !v) foreach (var it in _world.Bag.Items) _seen.Add(it.Uid); // closing marks the bag as seen
            Visible = v;
            _win.gameObject.SetActive(v);
            if (!v)
            {
                if (_held != Rune.None) Release();
                if (_legend != null) _legend.gameObject.SetActive(false);
                _settings.gameObject.SetActive(false);
                if (_confirm != null) _confirm.gameObject.SetActive(false);
                _tip.Hide();
                _compare.Hide();
                _hovered = null;
                foreach (var c in _cells) c.ClearHover();
                foreach (var s in _spots) s.ClearHover();
            }
            else _win.SetAsLastSibling();
            _stamp = -1;
        }

        /// <summary>The name of an 자동 분해 step (−1 off … <see cref="SimWorld.AutoSalvageMax"/>).</summary>
        public static string SalvageName(int step) => JunkNames[Mathf.Clamp(step + 1, 0, JunkNames.Length - 1)];

        /// <summary>F: the next 자동 분해 step (끔 → 일반 → 마법 이하 → 희귀 이하 → 끔).</summary>
        public void CycleFilter()
        {
            _world.AutoSalvage = _world.AutoSalvage >= SimWorld.AutoSalvageMax ? -1 : _world.AutoSalvage + 1;
            Changed();
        }

        /// <summary>Raised after a hand change (equip, lock, salvage, sort, filter) so the game saves soon.</summary>
        public Action OnChanged;

        void Changed()
        {
            _stamp = -1;
            OnChanged?.Invoke();
        }

        // ---------- per frame ----------

        public void Render()
        {
            if (!Visible) return;
            float t = Time.unscaledTime;
            // The hero breathes on the plinth (facing the viewer).
            _hero.sprite = _ui.Catalog.GetAnim("hero/" + _world.Hero.ClassId, "idle", 0, t, true, 1f, out _);
            foreach (var c in _cells) if (c.Root.gameObject.activeSelf) c.Tick(t);
            if (_salvageAllArmedAt > 0f && t - _salvageAllArmedAt > 3f) { _salvageAllArmedAt = -10f; _salvageAllLabel.text = "일괄 분해"; }
            if (_hintUntil > 0f && t > _hintUntil) { _hintUntil = 0f; if (_held == Rune.None) _hint.text = HandHint; }
            int stamp = Stamp();
            if (stamp == _stamp) return;
            _stamp = stamp;
            Rebuild();
            if (_hovered != null) Hover(_hovered, Array.IndexOf(_spots, _hovered) >= 0); // a drop or swap under the cursor
        }

        int Stamp()
        {
            unchecked
            {
                var h = _world.Hero;
                int s = 17;
                foreach (var it in _world.Bag.Items) s = s * 31 + it.Uid * (it.Locked ? 3 : 1) + it.RuneSlots.Count * 7 + it.Masterwork;
                foreach (var it in h.Gear.Spots)
                    s = s * 31 + (it == null ? 0 : it.Uid * (it.Locked ? 3 : 1) + it.RuneSlots.Count * 7 + it.Masterwork);
                // Level, ember, talents and runewords move the numbers without touching an item (review 11-21).
                s = s * 31 + h.Level;
                s = s * 31 + h.EmberLevel;
                s = s * 31 + Mathf.RoundToInt(h.Stats.MaxHp);
                s = s * 31 + Mathf.RoundToInt(h.Stats.Damage * 10f);
                s = s * 31 + h.IntegratedLevel;
                s = s * 31 + h.WickLevel;
                s = s * 31 + _world.AutoSalvage;
                s = s * 31 + (_world.AutoEquip ? 1 : 0);
                s = s * 31 + (_world.CanUndoAutoEquip ? 1 : 0);
                s = s * 31 + _world.Bag.AshShards;
                s = s * 31 + (_runeTab ? 1 : 0);
                foreach (int n in _world.Bag.RuneCounts) s = s * 31 + n;
                s = s * 31 + h.RunewordsFound.Count;
                return s;
            }
        }

        void Rebuild()
        {
            var h = _world.Hero;
            _title.text = "가방"; // the class and level are the HUD's and the 능력치 window's (11-24: one job per line)
            _undoButton.SetActive(_world.CanUndoAutoEquip && !_runeTab);
            if (_world.CanUndoAutoEquip) _undoLabel.text = UndoButtonText();

            foreach (var spot in DollOrder) _spots[(int)spot].Show(h.Gear.Spots[(int)spot], EmptyIcon(spot));

            var bag = _world.Bag.Items;
            bool nearFull = bag.Count >= NearFull;
            _bagCount.text = "가방  " + bag.Count + " / " + Bag.Capacity; // orange from 36: the ⚙ box says what a full bag does
            _bagCount.color = nearFull ? Warn : UiKit.Ink;
            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                if (i >= bag.Count)
                {
                    // An emptied place gets no pointer exit: forget its item, and drop the tooltip if it was the one hovered.
                    cell.Show(null, null);
                    cell.Clear();
                    if (_hovered == cell) { _hovered = null; _tip.Hide(); _compare.Hide(); }
                    continue;
                }
                var it = bag[i];
                // The same compare the tooltip and auto-equip use (UX plan §3.0 다): a cell's arrow never disagrees with them.
                var arrow = HeroStats.Compare(h, it).Arrow;
                // Only what is worth a look gets a mark, as in Diablo IV (critique 5: a red ▼ on all forty cells was noise):
                // ▲▲ ▲ better, ⇄ a trade; weaker and the same carry none.
                var mark = arrow == GearArrow.Down ? GearArrow.Same : arrow;
                cell.Show(it, null, mark, !_seen.Contains(it.Uid), usable: arrow != GearArrow.Locked, showSame: false);
            }

            // The ⚙ box: the chosen step of each choice gets a bright body and a ✓, not only brighter text (UX plan A7 ⑥).
            for (int i = 0; i < _junk.Length; i++) Choice(_junk[i], JunkNames[i], _world.AutoSalvage == i - 1);
            Choice(_autoEquip[0], "켬", _world.AutoEquip);
            Choice(_autoEquip[1], "끔", !_world.AutoEquip);
            _ash.text = _world.Bag.AshShards.ToString("N0") + "  <color=#8a8178>잿 파편</color>";
            if (_runeTab) RebuildRunes();
            _bagTabText.color = _runeTab ? UiKit.Dim : UiKit.Gold;
            _runeTabText.color = _runeTab ? UiKit.Gold : UiKit.Dim;
            _bagTabBar.enabled = !_runeTab;
            _runeTabBar.enabled = _runeTab;
        }

        void AddCell()
        {
            int i = _cells.Count;
            var cell = new SlotView(_ui, _bagContent, (i % Cols) * Pitch, (i / Cols) * Pitch, Cell, Cell, SlotStyle.Cell, "ui/ui2_cell", "ui/ui2_cell_hover");
            cell.Pointer.Enter += () => Hover(cell, worn: false);
            cell.Pointer.Exit += () => Unhover(cell);
            cell.Pointer.LeftClick += () => ClickBag(cell);
            cell.Pointer.RightClick += () => RightClickBag(cell);
            _cells.Add(cell);
        }

        // ---------- hand actions ----------

        void ClickBag(SlotView cell)
        {
            var it = cell.Item;
            if (it == null) return;
            if (_held != Rune.None) { SocketHeld(cell); return; }
            if (Ctrl) { SalvageOne(cell); return; }
            if (Shift) _world.ToggleLock(it.Uid);
            else if (!EquipCell(cell)) return;
            _seen.Add(it.Uid);
            Changed();
        }

        bool EquipCell(SlotView cell)
        {
            var it = cell.Item;
            if (_world.EquipFromBag(it.Uid)) return true;
            Hover(cell, false, "<color=#ff6b61>요구 레벨 " + it.ReqLevel + "에 미달합니다</color>");
            return false;
        }

        /// <summary>Right click equips like a click (UX plan A7; it used to salvage with no way back); with a rune held it puts the rune back.</summary>
        void RightClickBag(SlotView cell)
        {
            if (_held != Rune.None) { Release(); return; }
            var it = cell.Item;
            if (it == null || !EquipCell(cell)) return;
            _seen.Add(it.Uid);
            Changed();
        }

        /// <summary>Raised after a hand salvage so the game can post the 5 s notice (Z takes it back).</summary>
        public Action<Item> OnSalvaged;
        /// <summary>A plain line for the player (undo refusals), shown as a notice.</summary>
        public Action<string> OnNotice;
        float _salvagedAt = -10f;

        /// <summary>
        /// Z within five seconds of a hand salvage puts the piece back. True when the key was used (also on a refusal,
        /// which says why), so the minimap toggle that shares Z stays put.
        /// </summary>
        public bool TryUndoSalvage()
        {
            if (_world.LastSalvaged == null || Time.unscaledTime - _salvagedAt > 5f) return false;
            var it = _world.LastSalvaged;
            if (!_world.UndoSalvage())
            {
                OnNotice?.Invoke(_world.Bag.Items.Count >= Bag.Capacity ? "되돌릴 수 없습니다 · 가방이 가득 찼습니다" : "되돌릴 수 없습니다 · 잿 파편을 이미 썼습니다");
                _salvagedAt = -10f;
                return true;
            }
            _salvagedAt = -10f;
            _seen.Add(it.Uid); // it comes back as seen, not as a new find
            OnNotice?.Invoke("되돌렸습니다 · " + it.Name);
            Changed();
            return true;
        }

        void SalvageOne(SlotView cell)
        {
            var it = cell.Item;
            if (it == null) return;
            if (it.Locked) { Hover(cell, false, "<color=#ffd38a>잠긴 장비는 분해되지 않습니다</color>"); return; }
            bool precious = it.Rarity >= Rarity.Legendary || it.GreaterCount > 0;
            if (precious && (_salvageArmedUid != it.Uid || Time.unscaledTime - _salvageArmedAt > 1.5f))
            {
                _salvageArmedUid = it.Uid;
                _salvageArmedAt = Time.unscaledTime;
                Hover(cell, false, "<color=#ffb070>한 번 더 Ctrl+클릭하면 분해됩니다</color>");
                return;
            }
            _salvageArmedUid = -1;
            if (_world.SalvageFromBag(it.Uid))
            {
                _tip.Hide();
                _compare.Hide();
                _salvagedAt = Time.unscaledTime;
                OnSalvaged?.Invoke(it);
                Changed();
            }
        }

        /// <summary>The 일괄 분해 button: asks first (UX plan A7 ④); the box's own 분해 button is the second click.</summary>
        void SalvageAll()
        {
            var counts = _world.BulkSalvageCounts();
            int n = 0;
            foreach (int c in counts) n += c;
            if (n == 0) { _salvageAllLabel.text = "분해할 장비 없음"; _salvageAllArmedAt = Time.unscaledTime; return; }
            if (_confirm == null) BuildConfirm();
            var sb = new StringBuilder("<color=#ffd38a>일괄 분해 · " + n + "개</color>\n");
            int lines = 1;
            for (int r = 0; r < counts.Length; r++)
            {
                if (counts[r] == 0) continue;
                sb.Append("<color=").Append(ItemStyle.Hex((Rarity)r)).Append('>').Append(ItemStyle.RarityName((Rarity)r)).Append("</color>  ").Append(counts[r]).Append("개\n");
                lines++;
            }
            sb.Append("<color=#8a8178>잠근 것 · ★ 있는 것 · 전설 이상은 남깁니다</color>");
            lines++;
            // Legendary or better is never in the set today; if the rule ever lets one in, the box says so in red.
            if (counts[(int)Rarity.Legendary] + counts[(int)Rarity.Unique] > 0)
            {
                sb.Append("\n<color=#FF5A4D>전설 이상이 섞여 있습니다. 되돌릴 수 없습니다</color>");
                lines++;
            }
            _confirmText.text = sb.ToString();
            float textH = lines * 15f;
            float boxH = 12f + textH + 8f + 20f + 12f;
            _confirm.sizeDelta = new Vector2(ConfirmW, boxH);
            _confirmEdges[0].rectTransform.anchoredPosition = new Vector2(0f, -(boxH - 1f));
            _confirmEdges[1].rectTransform.sizeDelta = _confirmEdges[2].rectTransform.sizeDelta = new Vector2(1f, boxH);
            _confirmText.rectTransform.sizeDelta = new Vector2(ConfirmW - 24f, textH);
            foreach (var b in _confirmButtons) b.anchoredPosition = new Vector2(b.anchoredPosition.x, -(12f + textH + 8f));
            _confirm.gameObject.SetActive(true);
            _confirm.SetAsLastSibling();
            _tip.Hide();
            _compare.Hide();
        }

        const float ConfirmW = 290f;
        RectTransform _confirm;
        Text _confirmText;
        readonly List<RectTransform> _confirmButtons = new List<RectTransform>();
        readonly Image[] _confirmEdges = new Image[3]; // bottom, left, right

        void BuildConfirm()
        {
            _confirm = UiKit.Rect(_win, "salvage confirm", (W - ConfirmW) / 2f, 330f, ConfirmW, 120f);
            var back = _confirm.gameObject.AddComponent<Image>();
            back.color = new Color(0.035f, 0.032f, 0.045f, 1f); // opaque, and it takes the clicks meant for the bag under it
            var edge = new Color(1f, 0.83f, 0.5f, 0.45f);
            // bottom, left, right edges are resized to the text each time the box opens
            UiKit.Fill(_confirm, "edge", 0f, 0f, ConfirmW, 1f, edge);
            for (int i = 0; i < 3; i++) _confirmEdges[i] = UiKit.Fill(_confirm, "edge", i == 2 ? ConfirmW - 1f : 0f, 0f, i == 0 ? ConfirmW : 1f, 1f, edge);
            _confirmText = UiKit.Label(_confirm, "", 12f, 12f, ConfirmW - 24f, 60f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            _confirmText.lineSpacing = 1.15f;
            var yes = _ui.Button(_confirm, "분해", 12f, 0f, 128f, 20f, () =>
            {
                _world.SalvageAll();
                _confirm.gameObject.SetActive(false);
                Changed();
            });
            var no = _ui.Button(_confirm, "취소", ConfirmW - 140f, 0f, 128f, 20f, () => _confirm.gameObject.SetActive(false));
            _confirmButtons.Add((RectTransform)yes.transform);
            _confirmButtons.Add((RectTransform)no.transform);
            _confirm.gameObject.SetActive(false);
        }

        void SortBag()
        {
            // ▲▲ ▲ first, then ⇄, then the rest (UX plan A8); the same compare the cell arrows use. Ranked once per item.
            var h = _world.Hero;
            var rank = new Dictionary<int, int>();
            foreach (var it in _world.Bag.Items)
            {
                var arrow = HeroStats.Compare(h, it).Arrow;
                rank[it.Uid] = arrow == GearArrow.BigUp ? 0 : arrow == GearArrow.Up ? 1 : arrow == GearArrow.Trade ? 2 : 3;
            }
            _world.Bag.Items.Sort((a, b) =>
            {
                int c = rank[a.Uid].CompareTo(rank[b.Uid]);
                if (c != 0) return c;
                c = b.Rarity.CompareTo(a.Rarity);
                if (c != 0) return c;
                c = b.GreaterCount.CompareTo(a.GreaterCount);
                if (c != 0) return c;
                c = a.Slot.CompareTo(b.Slot);
                if (c != 0) return c;
                c = b.Ilvl.CompareTo(a.Ilvl);
                return c != 0 ? c : a.Uid.CompareTo(b.Uid);
            });
            Changed();
        }

        // ---------- tooltips ----------

        void Hover(SlotView cell, bool worn, string note = null)
        {
            _hovered = cell;
            var it = cell.Item;
            if (it == null) { _tip.Hide(); _compare.Hide(); return; }
            var h = _world.Hero;
            var foot = new StringBuilder();
            if (worn)
            {
                // The band says "착용 중" and the corner says 잠김: no foot line left to repeat them.
                _tip.Show(it, note ?? "", it.Locked ? "잠김" : null, band: "착용 중");
                _tip.PlaceLeftOf(_win);
                _compare.Hide();
                return;
            }
            // "Is this better?" is the tooltip's own answer line now, under the name (UX plan A4); the foot keeps only
            // what the hand just did. The hand controls live once at the foot of the window, not in every tooltip.
            var cmp = HeroStats.Compare(h, it);
            var wornThere = h.Gear.Spots[(int)cmp.Spot];
            if (cmp.Arrow == GearArrow.Locked) foot.Append("<color=#8a8178>지금 통합 레벨 ").Append(h.IntegratedLevel).Append("</color>");
            if (note != null) foot.Insert(0, foot.Length > 0 ? note + "\n" : note);
            _tip.Show(it, foot.ToString(), it.Locked ? "잠김" : null, cmp, wornThere, band: "가방");
            // Fixed left of the window (UX loop 2 P0-1): beside the cell they covered the bag, the orb and the skill bar.
            _tip.PlaceLeftOf(_win);
            if (wornThere != null)
            {
                _compare.Show(wornThere, "", band: "착용 중"); // the band at the top says which one is worn (loop 2 #8)
                _compare.PlaceLeftOrBelow(_tip);
            }
            else _compare.Hide();
        }

        void Unhover(SlotView cell)
        {
            if (_hovered != cell) return;
            _hovered = null;
            _tip.Hide();
            _compare.Hide();
        }

        // ---------- the head: auto-equip, its undo, the legend (UX plan A2, A6) ----------

        /// <summary>The bag count turns orange from here (four places left).</summary>
        const int NearFull = Bag.Capacity - 4;
        static readonly Color Warn = new Color(1f, 0.69f, 0.44f);
        /// <summary>A hover line in the foot for a small control (what ⚙, ?, 정렬, 일괄 분해 do) — no extra text on the window.</summary>
        void Tip(GameObject go, string text)
        {
            var p = go.GetComponent<UiPointer>() ?? go.AddComponent<UiPointer>();
            p.Enter += () => { if (_held == Rune.None) { _hint.text = text; _hintUntil = 0f; } };
            p.Exit += () => { if (_held == Rune.None && _hintUntil <= 0f) _hint.text = HandHint; };
        }

        /// <summary>One option of a choice row: the chosen one bright with a ✓.</summary>
        static void Choice(Button b, string name, bool on)
        {
            var label = b.GetComponentInChildren<Text>();
            label.text = on ? "✓ " + name : name;
            label.color = on ? Color.white : UiKit.Dim;
            var cb = b.colors;
            cb.normalColor = cb.selectedColor = on ? new Color(0.42f, 0.31f, 0.12f, 1f) : new Color(0.09f, 0.08f, 0.075f, 0.95f);
            cb.highlightedColor = on ? new Color(0.52f, 0.39f, 0.16f, 1f) : new Color(0.2f, 0.16f, 0.11f, 0.98f);
            b.colors = cb;
        }

        void ToggleSettings()
        {
            bool on = !_settings.gameObject.activeSelf;
            _settings.gameObject.SetActive(on);
            if (on)
            {
                if (_legend != null) _legend.gameObject.SetActive(false);
                _settings.SetAsLastSibling();
                _tip.Hide(); _compare.Hide(); _hovered = null;
            }
        }

        /// <summary>
        /// The ⚙ box (11-24): what the bag does on its own, one setting a block — a title, the choice, one plain sentence.
        /// Laid over the doll, opaque; × or ⚙ again closes it.
        /// </summary>
        void BuildSettings()
        {
            const float sw = W - 40f, pad = 16f, sh = 284f; // down to the bag's head: no doll pips peeking under it
            _settings = UiKit.Rect(_win, "gear settings", 20f, 34f, sw, sh);
            var back = _settings.gameObject.AddComponent<Image>();
            back.color = new Color(0.045f, 0.04f, 0.05f, 1f);
            var edge = new Color(1f, 0.83f, 0.5f, 0.45f);
            UiKit.Fill(_settings, "edge", 0f, 0f, sw, 1f, edge);
            UiKit.Fill(_settings, "edge", 0f, sh - 1f, sw, 1f, edge);
            UiKit.Fill(_settings, "edge", 0f, 0f, 1f, sh, edge);
            UiKit.Fill(_settings, "edge", sw - 1f, 0f, 1f, sh, edge);
            UiKit.Label(_settings, "가방 설정", pad, 12f, 200f, 16f, 16, TextAnchor.UpperLeft, UiKit.Gold, title: true);
            _ui.Button(_settings, "×", sw - 30f, 10f, 20f, 18f, ToggleSettings).GetComponentInChildren<Text>().fontSize = 12;

            float y = 46f;
            UiKit.Label(_settings, "자동 장착", pad, y, 120f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink, title: true);
            _autoEquip[0] = _ui.Button(_settings, "켬", sw - pad - 152f, y - 3f, 74f, 20f, () => { _world.AutoEquip = true; Changed(); });
            _autoEquip[1] = _ui.Button(_settings, "끔", sw - pad - 74f, y - 3f, 74f, 20f, () => { _world.AutoEquip = false; Changed(); });
            UiKit.Label(_settings, "더 좋은 장비(<color=#5BD66B>▲</color>)를 주우면 바로 낍니다.",
                pad, y + 24f, sw - pad * 2f, 16f, 12, TextAnchor.UpperLeft, UiKit.Dim);

            y += 54f;
            UiKit.Fill(_settings, "rule", pad, y - 10f, sw - pad * 2f, 1f, UiKit.Line);
            UiKit.Label(_settings, "자동 분해", pad, y, 120f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink, title: true);
            float bw = (sw - pad * 2f - 12f) / 4f;
            for (int i = 0; i < _junk.Length; i++)
            {
                int level = i - 1;
                _junk[i] = _ui.Button(_settings, JunkNames[i], pad + i * (bw + 4f), y + 22f, bw, 20f, () => { _world.AutoSalvage = level; Changed(); });
            }
            UiKit.Label(_settings, "고른 등급 이하는 줍자마자 잿 파편이 됩니다. [F]로도 바꿉니다.\n"
                + "<color=#c8c0a8>전설 이상 · ★ · 소켓 2칸 이상 · 잠근 장비는 지킵니다.</color>",
                pad, y + 50f, sw - pad * 2f, 34f, 12, TextAnchor.UpperLeft, UiKit.Dim);

            y += 100f;
            UiKit.Fill(_settings, "rule", pad, y - 10f, sw - pad * 2f, 1f, UiKit.Line);
            UiKit.Label(_settings, "가방이 가득 차면", pad, y, 200f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink, title: true);
            UiKit.Label(_settings, "가장 약한 장비부터 분해해 자리를 냅니다.\n아끼는 장비는 Shift+클릭으로 잠그세요.",
                pad, y + 20f, sw - pad * 2f, 34f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _settings.gameObject.SetActive(false);
        }

        /// <summary>A one-off line where the hand controls stand; they come back after a few seconds.</summary>
        void Say(string text)
        {
            _hint.text = text;
            _hintUntil = Time.unscaledTime + 4f;
        }

        void Undo()
        {
            var s = _world.LastAutoSwap;
            if (!_world.UndoAutoEquip()) { Say("되돌릴 자동 교체가 없습니다"); return; }
            Say("되돌렸습니다 · <color=" + ItemStyle.Hex(s.New.Rarity) + ">" + s.New.Name + "</color> — 이제 자동으로 끼지 않습니다");
            Changed();
        }

        const float UndoW = 150f;

        /// <summary>"되돌리기: 새 → 옛" on the button itself, the two names cut with … until the line fits (loop 2 #17).</summary>
        string UndoButtonText()
        {
            var s = _world.LastAutoSwap;
            if (s == null) return "직전 교체 되돌리기";
            string now = s.New.Name, was = s.Old != null ? s.Old.Name : "빈 칸";
            string text = "되돌리기: " + now + " → " + was;
            for (int n = Mathf.Max(now.Length, was.Length); n >= 2; n--)
            {
                text = "되돌리기: " + Cut(now, n) + " → " + Cut(was, n);
                _undoLabel.text = text; // preferredWidth reads the label's own text
                if (_undoLabel.preferredWidth <= UndoW - 10f) break;
            }
            return text;
        }

        static string Cut(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        /// <summary>What the undo button would do, shown in the hint line while the pointer is on it.</summary>
        string UndoPreview()
        {
            var s = _world.LastAutoSwap;
            if (s == null || !_world.CanUndoAutoEquip) return "되돌릴 자동 교체가 없습니다";
            string line = "되돌리기: " + s.New.Name + " → " + (s.Old != null ? s.Old.Name + " 다시 착용" : "빈 칸");
            // The old piece went to ash on the spot (자동 분해): undo buys it back for what it gave.
            if (s.Old != null && !_world.Bag.Items.Contains(s.Old)) line += " · 잿 파편 " + s.AshPaid + " 반환";
            return line;
        }

        void ToggleLegend()
        {
            if (_legend == null) BuildLegend();
            else _legend.gameObject.SetActive(!_legend.gameObject.activeSelf);
            if (_legend.gameObject.activeSelf) { _settings.gameObject.SetActive(false); _legend.SetAsLastSibling(); _tip.Hide(); _compare.Hide(); _hovered = null; }
        }

        /// <summary>
        /// The rarity legend (UX plan A2, audit #3: five colours and no word on their order). Highest on top: a swatch,
        /// the ◆ count, the name and what the rarity carries (GameText), then the sentence that sends the reader to the
        /// arrows and what each arrow means. Laid over the doll, built on first use; a click on it closes it.
        /// </summary>
        void BuildLegend()
        {
            const float lw = W - 20f, pad = 10f;
            _legend = UiKit.Rect(_win, "legend", 10f, 28f, lw, 100f);
            var back = _legend.gameObject.AddComponent<Image>();
            back.color = new Color(0.035f, 0.032f, 0.045f, 1f); // opaque, and it takes the clicks meant for the doll under it
            _legend.gameObject.AddComponent<UiPointer>().LeftClick = ToggleLegend;
            UiKit.Label(_legend, "장비 등급", pad, 8f, 200f, 14f, 12, TextAnchor.UpperLeft, UiKit.Gold, title: true);
            UiKit.Label(_legend, "클릭: 닫기", lw - 110f, 8f, 100f, 14f, 12, TextAnchor.UpperRight, UiKit.Dim);
            float y = 28f;
            for (int r = (int)Rarity.Unique; r >= (int)Rarity.Common; r--)
            {
                var rarity = (Rarity)r;
                UiKit.Fill(_legend, "swatch", pad, y + 2f, 10f, 10f, ItemStyle.Color(rarity));
                UiKit.Label(_legend, ItemStyle.Tag(rarity), pad + 16f, y, 100f, 14f);
                // GameText opens each line with the rarity's name and a dash; the tag beside it already says the name.
                string text = GameText.Rarity(rarity);
                int cut = text.IndexOf(" — ", StringComparison.Ordinal);
                var what = UiKit.Label(_legend, cut >= 0 ? text.Substring(cut + 3) : text, pad + 118f, y, lw - pad * 2f - 118f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink);
                y += Mathf.Max(14f, Mathf.Ceil(what.preferredHeight)) + 5f;
            }
            UiKit.Fill(_legend, "rule", pad, y, lw - pad * 2f, 1f, UiKit.Line);
            y += 6f;
            var foot = UiKit.Label(_legend,
                "위로 갈수록 드물고 옵션이 많습니다. 내게 더 좋은지는 <color=#5BD66B>▲</color>가 알려 줍니다.\n"
                + "<color=#5BD66B>▲▲</color> 전투력 +10% 이상 · <color=#5BD66B>▲</color> +1% 이상 · 표시 없음: 지금 장비보다 약하거나 비슷함\n"
                + "<color=#FFD24D>⇄</color> 교환: 공격과 생존 중 하나는 늘고 하나는 줍니다. 자동으로 끼지 않으니 직접 고르세요.\n"
                + "<color=#8A8178>지금 장비·레벨 기준 예상치입니다. 스킬 구성과 트리 효과는 넣지 않았습니다.</color>\n"
                + "<color=#ffd38a>조작</color>  " + HandControls,
                pad, y, lw - pad * 2f, 14f);
            foot.lineSpacing = 1.15f;
            y += Mathf.Ceil(foot.preferredHeight) + 8f;
            _legend.sizeDelta = new Vector2(lw, y);
            var edge = new Color(1f, 0.83f, 0.5f, 0.45f);
            UiKit.Fill(_legend, "edge", 0f, 0f, lw, 1f, edge);
            UiKit.Fill(_legend, "edge", 0f, y - 1f, lw, 1f, edge);
            UiKit.Fill(_legend, "edge", 0f, 0f, 1f, y, edge);
            UiKit.Fill(_legend, "edge", lw - 1f, 0f, 1f, y, edge);
        }

        static string EmptyIcon(EquipSpot s)
        {
            switch (s)
            {
                case EquipSpot.Weapon: return "icon/item_sword";
                case EquipSpot.Helm: return "icon/item_helm";
                case EquipSpot.Armor: return "icon/item_armor";
                case EquipSpot.Gloves: return "icon/item_gloves";
                case EquipSpot.Boots: return "icon/item_boots";
                case EquipSpot.Belt: return "icon/item_belt";
                case EquipSpot.Ring1:
                case EquipSpot.Ring2: return "icon/item_ring";
                default: return "icon/item_amulet";
            }
        }

    }
}
