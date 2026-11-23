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
    /// sockets, locks, ▲ upgrades and a "new" bar; hover shows the tooltip with a compare line and the worn piece beside it.
    /// Click equips, Shift+click locks, right click salvages (twice for legendary, ★ or unique pieces). Tab toggles it,
    /// F steps 자동 분해.
    /// </summary>
    public sealed class GearPanel
    {
        const float W = 420f, H = 590f, Cell = 36f, Pitch = 38f;
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
        readonly ItemTooltip _tip, _compare;
        readonly HashSet<int> _seen = new HashSet<int>();
        public bool Visible { get; private set; }

        // ---------- 룬 보관함 (재기획 §5.7) ----------
        RectTransform _bagView, _runePage;
        bool _runeTab;
        Text _bagTabText, _runeTabText;
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
            // Two tabs: the bag (with the doll) and the rune hoard share the left side.
            var bagTab = _ui.Button(_win, "가방", tx, ty, 104f, 19f, () => ShowRunes(false));
            var runeTab = _ui.Button(_win, "룬 보관함", tx + 108f, ty, 112f, 19f, () => ShowRunes(true));
            _bagTabText = bagTab.GetComponentInChildren<Text>();
            _runeTabText = runeTab.GetComponentInChildren<Text>();

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

        const string RuneHint = "룬을 클릭해 집은 뒤 장비를 클릭하면 빈 소켓에 꽂힙니다 · 룬어 재료가 다 모이면 빈 장비에 저절로 꽂힙니다";
        const string HandHint = "클릭: 장착 · Shift+클릭: 잠금 · 우클릭: 분해";

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
            _win = ui.Window("Gear Panel", W, H, "소지품", Toggle, out _title, "ui/ui2_window_iron");
            _win.anchoredPosition = new Vector2(232f, UiKit.Lift(H)); // the right half; the character window takes the left

            // ---------- the paper doll (Diablo II's places; no off-hand here, so the hero stands where a shield would) ----------
            const float ox = 30f, oy = 40f, cw = 76f;
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
            const float bx = 18f, by = oy + 256f;
            const float bw = Cols * Pitch - 2f;
            var chest = ui.Sprite(_win, "ui/ui2_chest", bx - 10f, by - 10f, bw + 20f, Pitch * Rows + 18f, sliced: true);
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

            // the purse at the foot, as Diablo's gold
            float py = by + Pitch * Rows + 12f;
            var purse = ui.Sprite(_win, "icon/item_gold", bx, py - 4f, 24f, 24f);
            purse.preserveAspect = true;
            _ash = UiKit.Label(_win, "", bx + 28f, py, 220f, 14f, 12, TextAnchor.UpperLeft, UiKit.Gold);

            // ---------- under the bag: count, 자동 분해, the hand buttons ----------
            _bagCount = UiKit.Label(_win, "", W - 170f, py, 152f, 14f, 12, TextAnchor.UpperRight, UiKit.Ink);
            float jy = py + 24f;
            UiKit.Label(_win, "자동 분해", bx, jy + 3f, 80f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            for (int i = 0; i < _junk.Length; i++)
            {
                int level = i - 1;
                _junk[i] = ui.Button(_win, JunkNames[i], bx + 76f + i * 76f, jy, 72f, 19f, () => { _world.AutoSalvage = level; Changed(); });
            }
            UiKit.Label(_win, "<color=#8a8178>줍는 즉시 잿 파편으로 · 전설 이상 · ★ · 소켓 2칸 이상은 늘 지킴</color>", bx, jy + 22f, W - 36f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            float hy = jy + 44f;
            BuildRunePage(bx, oy, bw, bx, hy);
            ui.Button(_win, "정렬", bx + 224f, hy, 72f, 19f, SortBag);
            var salvage = ui.Button(_win, "일괄 분해", bx + 300f, hy, 84f, 19f, SalvageAll);
            _salvageAllLabel = salvage.GetComponentInChildren<Text>();
            _hint = UiKit.Label(_win, HandHint, bx, H - 22f, W - 36f, 14f, 12, TextAnchor.UpperCenter, new Color(0.62f, 0.58f, 0.52f));

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

        public void Toggle() => SetVisible(!Visible);

        void SetVisible(bool v)
        {
            if (Visible && !v) foreach (var it in _world.Bag.Items) _seen.Add(it.Uid); // closing marks the bag as seen
            Visible = v;
            _win.gameObject.SetActive(v);
            if (!v)
            {
                if (_held != Rune.None) Release();
                _tip.Hide();
                _compare.Hide();
                _hovered = null;
                foreach (var c in _cells) c.ClearHover();
                foreach (var s in _spots) s.ClearHover();
            }
            else _win.SetAsLastSibling();
            _stamp = -1;
        }

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
            _title.text = "소지품 · " + SkillBook.ClassName(h.ClassId) + "  Lv " + h.Level
                + (h.EmberLevel > 0 ? " <color=#9a948c>+" + h.EmberLevel + "</color>" : "");

            foreach (var spot in DollOrder) _spots[(int)spot].Show(h.Gear.Spots[(int)spot], EmptyIcon(spot));

            var bag = _world.Bag.Items;
            _bagCount.text = "가방  " + bag.Count + " / " + Bag.Capacity;
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
                bool usable = it.ReqLevel <= h.IntegratedLevel;
                bool up = usable && HeroStats.Upgrade(h, it, _world.SpotFor(it), out _) > 1.001f;
                cell.Show(it, null, up, !_seen.Contains(it.Uid), usable);
            }

            for (int i = 0; i < _junk.Length; i++)
                _junk[i].GetComponentInChildren<Text>().color = _world.AutoSalvage == i - 1 ? UiKit.Gold : UiKit.Dim;
            _ash.text = _world.Bag.AshShards.ToString("N0") + "  <color=#8a8178>잿 파편</color>";
            if (_runeTab) RebuildRunes();
            _bagTabText.color = _runeTab ? UiKit.Dim : UiKit.Gold;
            _runeTabText.color = _runeTab ? UiKit.Gold : UiKit.Dim;
        }

        void AddCell()
        {
            int i = _cells.Count;
            var cell = new SlotView(_ui, _bagContent, (i % Cols) * Pitch, (i / Cols) * Pitch, Cell, Cell, SlotStyle.Cell, "ui/ui2_cell", "ui/ui2_cell_hover");
            cell.Pointer.Enter += () => Hover(cell, worn: false);
            cell.Pointer.Exit += () => Unhover(cell);
            cell.Pointer.LeftClick += () => ClickBag(cell);
            cell.Pointer.RightClick += () => SalvageOne(cell);
            _cells.Add(cell);
        }

        // ---------- hand actions ----------

        void ClickBag(SlotView cell)
        {
            var it = cell.Item;
            if (it == null) return;
            if (_held != Rune.None) { SocketHeld(cell); return; }
            if (Shift) _world.ToggleLock(it.Uid);
            else if (!_world.EquipFromBag(it.Uid)) { Hover(cell, false, "<color=#ff6b61>요구 레벨 " + it.ReqLevel + "에 미달합니다</color>"); return; }
            _seen.Add(it.Uid);
            Changed();
        }

        void SalvageOne(SlotView cell)
        {
            if (_held != Rune.None) { Release(); return; } // right click puts the rune back
            var it = cell.Item;
            if (it == null) return;
            if (it.Locked) { Hover(cell, false, "<color=#ffd38a>잠긴 장비는 분해되지 않습니다</color>"); return; }
            bool precious = it.Rarity >= Rarity.Legendary || it.GreaterCount > 0;
            if (precious && (_salvageArmedUid != it.Uid || Time.unscaledTime - _salvageArmedAt > 1.5f))
            {
                _salvageArmedUid = it.Uid;
                _salvageArmedAt = Time.unscaledTime;
                Hover(cell, false, "<color=#ffb070>한 번 더 우클릭하면 분해됩니다</color>");
                return;
            }
            _salvageArmedUid = -1;
            if (_world.SalvageFromBag(it.Uid)) { _tip.Hide(); _compare.Hide(); Changed(); }
        }

        void SalvageAll()
        {
            int n = 0;
            foreach (var it in _world.Bag.Items) if (!it.Locked && it.Rarity < Rarity.Legendary && it.GreaterCount == 0) n++;
            if (n == 0) { _salvageAllLabel.text = "분해할 장비 없음"; _salvageAllArmedAt = Time.unscaledTime; return; }
            if (Time.unscaledTime - _salvageAllArmedAt > 3f)
            {
                _salvageAllArmedAt = Time.unscaledTime;
                _salvageAllLabel.text = "한 번 더: " + n + "개 분해 (전설·★·잠금 제외)";
                return;
            }
            _world.SalvageAll();
            _salvageAllArmedAt = -10f;
            _salvageAllLabel.text = "일괄 분해";
            Changed();
        }

        void SortBag()
        {
            _world.Bag.Items.Sort((a, b) =>
            {
                int c = b.Rarity.CompareTo(a.Rarity);
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
                foot.Append("장착 중");
                if (it.Locked) foot.Append(" · 잠김");
                _tip.Show(it, note ?? foot.ToString(), it.Locked ? "잠김" : null);
                _tip.PlaceBeside(cell.Root);
                _compare.Hide();
                return;
            }
            Item wornThere = null;
            if (it.ReqLevel > h.IntegratedLevel)
                foot.Append("<color=#ff6b61>요구 레벨 ").Append(it.ReqLevel).Append(" (현재 ").Append(h.IntegratedLevel).Append(")</color>");
            else
            {
                var spot = _world.SpotFor(it);
                wornThere = h.Gear.Spots[(int)spot];
                float score = HeroStats.Upgrade(h, it, spot, out float dps);
                // One line, compare first: "is this better?" before the details (재기획 §5.5). The hand controls live
                // once at the foot of the window, not in every tooltip.
                foot.Append("초당 피해 ").Append(Delta(dps)).Append("   종합 ").Append(Delta(score - 1f));
            }
            if (note != null) foot.Insert(0, note + "\n");
            _tip.Show(it, foot.ToString(), it.Locked ? "잠김" : null);
            _tip.PlaceBeside(cell.Root, preferLeft: true);
            if (wornThere != null)
            {
                _compare.Show(wornThere, "<color=#8a8178>현재 장착 중</color>");
                _compare.PlaceNextTo(_tip);
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

        static string Delta(float frac)
        {
            float pct = frac * 100f;
            if (Mathf.Abs(pct) < 0.05f) return "<color=#8a8178>±0%</color>";
            return pct > 0f ? "<color=#73f280>▲ +" + pct.ToString("0.#") + "%</color>" : "<color=#ff6b61>▼ " + pct.ToString("0.#") + "%</color>";
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
