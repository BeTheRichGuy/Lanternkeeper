using System;
using System.Collections.Generic;
using System.Text;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 스킬 배치 (GDD §5.1; 희승 11-22: laid out like the skill bar it fills). Left: the four slots in a row, in cast order
    /// as on the bone bar, the basic attack under them; each link between neighbours is coloured by the resonance the
    /// order makes (bright for a forward resonance, grey for none) and named over it, the last slot's link back to the
    /// first named under the row; each slot shows its glyph, 숙련 level and a bar to the next one.
    /// Right: the chosen slot in detail (practice, copies, evolution two-of-three), then the skills and glyphs to put in it
    /// — hovering one previews the resonances it would make with its neighbours — and the recommended rings.
    /// Keys still work: R, 1–4, Q/E, A/D, P.
    /// </summary>
    public sealed class RingPanel
    {
        const float W = 880f, H = 500f, SlotSize = 44f; // the drawn round skill frame at its own size
        /// <summary>The basic attack's place, under the row of slots.</summary>
        static readonly Vector2 Center = new Vector2(218f, 330f);
        const float RowY = 150f;

        readonly UiKit _ui;
        readonly SimWorld _world;
        readonly RectTransform _win;
        readonly Text _title;
        public bool Visible { get; private set; }
        int _slot;
        int _stamp = -1;

        // the ring
        readonly Image[] _frames = new Image[4], _icons = new Image[4], _sigils = new Image[4], _select = new Image[4];
        const string FrameKey = "ui/ui2_node_skill";
        readonly Image[] _barBack = new Image[4], _barFill = new Image[4];
        readonly Text[] _levels = new Text[4];
        readonly Image[] _links = new Image[4];
        readonly Text[] _linkNames = new Text[4];
        readonly Image _basicIcon;
        // detail
        readonly Image _detailIcon;
        readonly Text _detailName, _detailBody;
        readonly Image _detailBarBack, _detailBarFill;
        // pickers
        readonly RectTransform _skillGrid, _sigilGrid;
        readonly List<(Image frame, Image icon, Text badge, UiPointer ptr)> _skillCells = new List<(Image, Image, Text, UiPointer)>();
        readonly List<(Image frame, Image icon, UiPointer ptr)> _sigilCells = new List<(Image, Image, UiPointer)>();
        readonly Text _preview, _sigilLabel;
        readonly List<Button> _presets = new List<Button>();
        List<SkillDef> _available = new List<SkillDef>();
        static readonly Sigil[] AllSigils = (Sigil[])Enum.GetValues(typeof(Sigil));

        public RingPanel(UiKit ui, SimWorld world)
        {
            _ui = ui;
            _world = world;
            _win = ui.Window("Ring Panel", W, H, "스킬 배치", Toggle, out _title, "ui/ui2_window_iron");

            // ---------- ring ----------
            // A pool of lantern light under the ring (same language as the reliquary and the tree).
            var halo = UiKit.Fill(_win, "light", Center.x - 200f, RowY - 110f, 400f, 300f, new Color(1f, 0.72f, 0.4f, 0.24f));
            halo.sprite = UiKit.Glow(ui, 64);
            for (int i = 0; i < 4; i++)
            {
                _links[i] = UiKit.Fill(_win, "link", 0f, 0f, 1f, 2f, Color.clear);
                _linkNames[i] = UiKit.Label(_win, "", 0f, 0f, 90f, 14f, 12, TextAnchor.MiddleCenter, UiKit.Dim);
            }
            var basicFrame = ui.Sprite(_win, FrameKey, Center.x - 22f, Center.y - 22f, 44f, 44f);
            basicFrame.preserveAspect = false;
            basicFrame.color = new Color(0.75f, 0.72f, 0.68f);
            _basicIcon = ui.Sprite(_win, "icon/skill_ember_slash", Center.x - 16f, Center.y - 16f, 32f, 32f);
            UiKit.Label(_win, "기본 공격", Center.x - 40f, Center.y + 24f, 80f, 14f, 12, TextAnchor.UpperCenter, UiKit.Dim);
            for (int i = 0; i < 4; i++)
            {
                var p = SlotPos(i);
                int slot = i;
                // The picked slot glows from behind (a soft gold pool) instead of a square outline.
                _select[i] = UiKit.Fill(_win, "sel", p.x - 18f, p.y - 18f, SlotSize + 36f, SlotSize + 36f, Color.clear);
                _select[i].sprite = UiKit.Glow(ui, 64);
                // Round frame: the square icon sits in a round mask under it so its corners never poke through.
                var mask = UiKit.Fill(_win, "icon mask", p.x + 6f, p.y + 6f, 32f, 32f, Color.white);
                mask.sprite = UiKit.Disc(ui, 32, 0);
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                _frames[i] = ui.Sprite(_win, FrameKey, p.x, p.y, SlotSize, SlotSize);
                _frames[i].preserveAspect = false;
                _frames[i].raycastTarget = true;
                var ptr = _frames[i].gameObject.AddComponent<UiPointer>();
                ptr.LeftClick += () => SelectSlot(slot);
                _icons[i] = ui.Sprite(mask.transform, "icon/skill_ember_slash", 0f, 0f, 32f, 32f);
                _sigils[i] = ui.Sprite(_win, "icon/sigil_chain", p.x + SlotSize - 10f, p.y - 6f, 16f, 16f);
                _barBack[i] = UiKit.Fill(_win, "bar", p.x, p.y + SlotSize + 2f, SlotSize, 2f, new Color(0f, 0f, 0f, 0.6f));
                _barFill[i] = UiKit.Fill(_win, "bar fill", p.x, p.y + SlotSize + 2f, 0f, 2f, new Color(0.66f, 0.82f, 1f, 0.9f));
                _levels[i] = UiKit.Label(_win, "", p.x - 48f, p.y + SlotSize + 5f, SlotSize + 96f, 14f, 12, TextAnchor.UpperCenter, new Color(0.66f, 0.82f, 1f));
            }

            // ---------- detail ----------
            const float dx = 452f, dw = W - dx - 16f;
            // The skill card on burnt parchment, like the reliquary's stat card.
            var card = ui.Sprite(_win, "ui/ui2_parchment", dx - 12f, 30f, dw + 24f, 158f, sliced: true);
            card.preserveAspect = false;
            card.color = new Color(0.42f, 0.36f, 0.32f, 1f);
            var dframe = UiKit.Fill(_win, "icon well", dx, 40f, 72f, 72f, new Color(0.05f, 0.04f, 0.04f, 0.85f));
            _detailIcon = ui.Sprite(_win, "icon/skill_ember_slash", dx + 4f, 44f, 64f, 64f); // 32 px art at ×2
            _detailName = UiKit.Label(_win, "", dx + 84f, 42f, dw - 84f, 16f, 14, TextAnchor.UpperLeft, UiKit.Gold, title: true);
            _detailBody = UiKit.Label(_win, "", dx + 84f, 62f, dw - 84f, 118f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            _detailBody.lineSpacing = 1.12f;
            _detailBarBack = UiKit.Fill(_win, "bar", dx, 118f, 72f, 3f, new Color(0f, 0f, 0f, 0.6f));
            _detailBarFill = UiKit.Fill(_win, "bar fill", dx, 118f, 0f, 3f, new Color(0.66f, 0.82f, 1f, 0.95f));

            // ---------- pickers ----------
            UiKit.Label(_win, "이 칸에 배치할 스킬", dx, 192f, dw, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _skillGrid = UiKit.Rect(_win, "skills", dx, 208f, dw, 80f);
            _preview = UiKit.Label(_win, "", dx, 292f, dw, 30f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            _sigilLabel = UiKit.Label(_win, "", dx, 326f, dw, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _sigilGrid = UiKit.Rect(_win, "sigils", dx, 342f, dw, 80f);
            for (int i = 0; i < AllSigils.Length; i++)
            {
                var g = AllSigils[i];
                var frame = UiKit.Fill(_sigilGrid, "sigil", (i % 11) * 36f, (i / 11) * 36f, 34f, 34f, new Color(0.08f, 0.07f, 0.07f, 0.95f));
                frame.raycastTarget = true;
                var icon = ui.Sprite(frame.transform, SigilIcon(g), 1f, 1f, 32f, 32f); // 16 px glyphs at ×2
                var ptr = frame.gameObject.AddComponent<UiPointer>();
                ptr.LeftClick += () => { _world.SetCustomSigil(_slot, g); Changed(); };
                ptr.Enter += () => _preview.text = g == Sigil.None ? "문양 제거" : "<color=#a8c4ff>◈ " + Sigils.Name(g) + "</color>";
                ptr.Exit += () => _preview.text = "";
                _sigilCells.Add((frame, icon, ptr));
            }

            UiKit.Label(_win, "추천 배치", 16f, H - 52f, 200f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            var presets = SkillBook.PresetsFor(world.Hero.ClassId);
            for (int i = 0; i < presets.Length; i++)
            {
                int idx = i;
                _presets.Add(ui.Button(_win, presets[i].Name, 16f + i * 140f, H - 34f, 134f, 20f, () => { _world.SetPreset(idx); Changed(); }));
            }
            // 고리 시험 (재기획 §5.6): every order of these skills hunted 40 s from this moment, best first.
            ui.Button(_win, "순서 시험", W - 16f - 140f, H - 34f, 140f, 20f, RunTrial);
            // 갈래별 스킬 세트 (희승 11-22): a card of that branch switches back to what is kept here.
            ui.Button(_win, "이 갈래 세트로 저장", W - 16f - 140f, H - 58f, 140f, 20f, () => { _world.SaveBranchSet(); Changed(); });
            _trial = UiKit.Rect(_win, "trial", W - 16f - 330f, H - 40f - 200f, 330f, 200f);
            var tc = _trial.gameObject.AddComponent<Canvas>();
            tc.overrideSorting = true;
            tc.sortingOrder = ui.Canvas.sortingOrder + 5;
            _trial.gameObject.AddComponent<GraphicRaycaster>();
            var tb = _trial.gameObject.AddComponent<Image>();
            tb.color = new Color(0.04f, 0.035f, 0.05f, 1f);
            _trial.gameObject.SetActive(false);
            SetVisible(false);
        }

        RectTransform _trial;
        readonly List<GameObject> _trialBits = new List<GameObject>();

        /// <summary>Runs the trial (about a second: 24 orders × 40 s of deterministic sim) and lists the five best.</summary>
        void RunTrial()
        {
            foreach (var g in _trialBits) UnityEngine.Object.Destroy(g);
            _trialBits.Clear();
            var results = RingTrial.Run(_world);
            var cur = new string[4]; // the ring in use, read only (the trial must not switch a preset ring to custom)
            for (int i = 0; i < 4; i++) cur[i] = _world.Hero.Ring.Slots[i]?.BaseId;
            string now = string.Join("|", cur);
            double baseDmg = 0;
            foreach (var r in results) if (string.Join("|", r.Order) == now) baseDmg = r.Damage;
            bool hasBase = baseDmg > 0; // the order in use was not among the trial's: show the ranks without a gain
            _trialBits.Add(UiKit.Label(_trial, "<color=#ffcf5a>순서 시험</color>  <color=#8a8178>순서 " + results.Count + "가지 · 각 40초</color>", 10f, 8f, 310f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink).gameObject);
            for (int k = 0; k < results.Count && k < 5; k++)
            {
                var r = results[k];
                float y = 30f + k * 32f;
                for (int i = 0; i < 4; i++)
                {
                    var sk = r.Order[i] == null ? null : SkillBook.Get(r.Order[i]);
                    var icon = _ui.Sprite(_trial, sk != null && _ui.Catalog.Has("icon/skill_" + sk.Id) ? "icon/skill_" + sk.Id : "ui/dot", 10f + i * 30f, y, 26f, 26f);
                    if (sk == null) icon.color = new Color(0.2f, 0.2f, 0.2f);
                    _trialBits.Add(icon.gameObject);
                }
                double gain = hasBase ? r.Damage / baseDmg - 1.0 : 0.0;
                bool current = string.Join("|", r.Order) == now;
                string g = current ? "<color=#8a8178>현재 순서</color>" : !hasBase ? "<color=#8a8178>" + (k + 1) + "위</color>"
                    : (gain >= 0 ? "<color=#9fd28a>피해 +" : "<color=#d08a6a>피해 ") + (gain * 100).ToString("0") + "%</color>";
                _trialBits.Add(UiKit.Label(_trial, g, 134f, y + 6f, 100f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink).gameObject);
                if (!current)
                {
                    var order = r.Order;
                    _trialBits.Add(_ui.Button(_trial, "적용", 250f, y + 3f, 68f, 20f, () => { _world.SetCustomOrder(order); _trial.gameObject.SetActive(false); Changed(); }).gameObject);
                }
            }
            _trialBits.Add(_ui.Button(_trial, "닫기", 250f, 8f, 68f, 18f, () => _trial.gameObject.SetActive(false)).gameObject);
            _trial.gameObject.SetActive(true);
        }

        static Vector2 SlotPos(int i)
        {
            // left to right in cast order, like the bone bar
            return new Vector2(40f + i * 104f, RowY);
        }

        public void Toggle() => SetVisible(!Visible);

        void SetVisible(bool v)
        {
            Visible = v;
            _win.gameObject.SetActive(v);
            if (_trial != null && !v) _trial.gameObject.SetActive(false);
            if (v) _win.SetAsLastSibling();
            _stamp = -1;
        }

        public void SelectSlot(int slot) { _slot = Math.Max(0, Math.Min(3, slot)); _stamp = -1; }

        /// <summary>Raised after a click changes the ring, so the game saves soon (the keys already do).</summary>
        public Action OnChanged;

        void Changed()
        {
            if (_trial != null) _trial.gameObject.SetActive(false); // its orders were for the ring before this change
            _stamp = -1;
            OnChanged?.Invoke();
        }

        /// <summary>Q/E: next or previous unlocked skill for the selected slot.</summary>
        public void CycleSkill(int step)
        {
            var h = _world.Hero;
            var list = SkillBook.Available(h.ClassId, h.IntegratedLevel, TreeSkills(h));
            if (list.Count == 0 || _slot >= SkillBook.SlotsAt(h.IntegratedLevel)) return;
            string cur = h.Ring.Slots[_slot]?.BaseId;
            int i = list.FindIndex(s => s.Id == cur);
            i = ((i < 0 ? 0 : i + step) % list.Count + list.Count) % list.Count;
            _world.SetCustomSkill(_slot, list[i].Id);
            _stamp = -1;
        }

        /// <summary>A/D: next or previous glyph (from Lv12).</summary>
        public void CycleSigil(int step)
        {
            var h = _world.Hero;
            if (h.IntegratedLevel < SkillBook.SigilLevel) return;
            int n = AllSigils.Length;
            int cur = (int)h.Ring.Sigils[_slot];
            _world.SetCustomSigil(_slot, (Sigil)(((cur + step) % n + n) % n));
            _stamp = -1;
        }

        // ---------- render ----------

        public void Render()
        {
            if (!Visible) return;
            var h = _world.Hero;
            var ring = h.Ring;
            int stamp = _slot * 1000003 + h.IntegratedLevel * 7919 + (h.UseCustomRing ? 1 : 0) + h.Preset * 13;
            for (int i = 0; i < 4; i++)
            {
                stamp = stamp * 31 + (ring.Slots[i]?.Id.GetHashCode() ?? 0) + (int)ring.Sigils[i] * 7;
                var s = ring.Slots[i];
                if (s != null && h.SkillUses.TryGetValue(s.BaseId, out int u)) stamp = stamp * 17 + SkillBook.SkillLevel(u);
            }
            if (stamp == _stamp) return;
            _stamp = stamp;
            Rebuild();
        }

        void Rebuild()
        {
            var h = _world.Hero;
            var ring = h.Ring;
            int open = SkillBook.SlotsAt(h.IntegratedLevel);
            _title.text = "스킬 배치 · " + SkillBook.ClassName(h.ClassId)
                + (h.UseCustomRing ? "  <color=#9a948c>직접 배치</color>" : "  <color=#9a948c>추천: " + SkillBook.PresetsFor(h.ClassId)[h.Preset].Name + "</color>");
            // Basic attacks have no drawn icon of their own: the class weapon stands in.
            var basic = SkillBook.BasicFor(h.ClassId);
            _basicIcon.sprite = _ui.Catalog.Get(_ui.Catalog.Has("icon/skill_" + basic.Id) ? "icon/skill_" + basic.Id : "icon/item_sword");

            for (int i = 0; i < 4; i++)
            {
                var s = i < open ? ring.Slots[i] : null;
                bool locked = i >= open;
                // One drawn frame; evolutions burn orange-gold, empty and locked slots go dim.
                _frames[i].color = locked ? new Color(0.4f, 0.38f, 0.36f, 0.8f) : s == null ? new Color(0.7f, 0.66f, 0.62f) : s.IsEvolution ? new Color(1f, 0.72f, 0.4f) : Color.white;
                _icons[i].enabled = s != null;
                if (s != null) _icons[i].sprite = _ui.Catalog.Get(SkillIcon(s));
                var g = locked ? Sigil.None : ring.Sigils[i];
                _sigils[i].enabled = g != Sigil.None;
                if (g != Sigil.None) _sigils[i].sprite = _ui.Catalog.Get(SigilIcon(g));
                _select[i].color = i == _slot ? new Color(1f, 0.8f, 0.4f, 0.6f) : Color.clear;
                if (locked) { _levels[i].text = "<color=#6a645c>Lv " + (i == 1 ? 2 : i == 2 ? 4 : 8) + "에서 개방</color>"; SetBar(_barFill[i], 0f, SlotSize); continue; }
                if (s == null) { _levels[i].text = "<color=#6a645c>비어 있음</color>"; SetBar(_barFill[i], 0f, SlotSize); continue; }
                h.SkillUses.TryGetValue(s.BaseId, out int uses);
                int lv = SkillBook.SkillLevel(uses);
                _levels[i].text = "숙련 " + lv + (SkillBook.LevelCopies(lv) > 0 ? "  <color=#ffd38a>분신 " + SkillBook.LevelCopies(lv) + "</color>" : "");
                SetBar(_barFill[i], Progress(uses, lv), SlotSize);
            }

            // Links, clockwise: slot i marks, slot i+1 hits.
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                var a = SlotPos(i) + Vector2.one * (SlotSize / 2f);
                var b = SlotPos(j) + Vector2.one * (SlotSize / 2f);
                var dir = (b - a).normalized;
                var from = a + dir * (SlotSize * 0.62f);
                var to = b - dir * (SlotSize * 0.62f);
                var res = i < open && j < open && ring.Slots[i] != null && ring.Slots[j] != null
                    ? Elements.Resolve(ElementOf(ring, i), ElementOf(ring, j)) : Resonance.None;
                bool forward = Elements.IsForward(res);
                var col = forward ? FxView.ElementColor(ElementOf(ring, j)) : new Color(0.45f, 0.42f, 0.38f, 0.5f);
                bool wrap = j == 0; // the last slot back to the first: named under the row, no line across it
                if (wrap) _links[i].color = Color.clear;
                else Line(_links[i], from, to, forward ? 2f : 1f, col);
                var mid = (from + to) / 2f;
                _linkNames[i].rectTransform.anchoredPosition = wrap ? new Vector2(Mathf.Round(Center.x - 45f), -Mathf.Round(RowY + SlotSize + 26f))
                    : new Vector2(Mathf.Round(mid.x - 45f), -Mathf.Round(mid.y - 26f));
                string name = res == Resonance.None ? "" : forward ? "<color=#cfe9b4>" + FxView.ResonanceName(res) + " ✓</color>"
                    : res == Resonance.Stack ? "중첩" : "약공명";
                _linkNames[i].text = j == 0 && name.Length > 0 ? "4 → 1  " + name : name;
            }

            // Detail of the chosen slot.
            var sel = _slot < open ? ring.Slots[_slot] : null;
            if (sel == null)
            {
                _detailIcon.enabled = false;
                _detailName.text = _slot < open ? (_slot + 1) + "번 칸 · 비어 있음" : (_slot + 1) + "번 칸 · 잠김";
                _detailBody.text = _slot < open ? "아래에서 스킬을 선택해 배치합니다." : "통합 레벨 " + (_slot == 1 ? 2 : _slot == 2 ? 4 : 8) + "에서 개방됩니다.";
                SetBar(_detailBarFill, 0f, 72f);
            }
            else
            {
                _detailIcon.enabled = true;
                _detailIcon.sprite = _ui.Catalog.Get(SkillIcon(sel));
                _detailName.text = (_slot + 1) + "번 칸 · " + sel.Name;
                h.SkillUses.TryGetValue(sel.BaseId, out int uses);
                int lv = SkillBook.SkillLevel(uses);
                var sb = new StringBuilder();
                var elem = ElementOf(ring, _slot);
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(FxView.ElementColor(elem))).Append(">").Append(ElementName(elem)).Append("</color>")
                  .Append("  ·  재사용 대기시간 ").Append(sel.Cooldown.ToString("0.#")).Append("초");
                if (ring.Sigils[_slot] != Sigil.None) sb.Append("  ·  <color=#a8c4ff>◈ ").Append(Sigils.Name(ring.Sigils[_slot])).Append("</color>");
                sb.Append("\n<color=#a8d0ff>숙련 ").Append(lv).Append("</color>  위력 +").Append(((SkillBook.LevelPower(lv) - 1f) * 100f).ToString("0"))
                  .Append("%  범위 +").Append(((SkillBook.LevelArea(lv) - 1f) * 100f).ToString("0")).Append("%  분신 ").Append(SkillBook.LevelCopies(lv));
                if (lv < SkillBook.MaxSkillLevel)
                    sb.Append("\n<color=#8a8178>다음 숙련까지 ").Append((SkillBook.UsesFor(lv + 1) - uses).ToString("N0")).Append("회 · 분신은 숙련 ")
                      .Append((SkillBook.LevelCopies(lv) + 1) * SkillBook.CopyEvery).Append("에서</color>");
                if (sel.EvolveTo != null && !h.Evolved.Contains(sel.BaseId))
                {
                    bool byUses = uses >= sel.EvolveUses, bySigil = ring.ModsFor(_slot).Has(sel.EvolveSigil), byEssence = h.Has(sel.EvolveEssence);
                    string Mark(bool ok) => ok ? "<color=#9fd28a>✓</color>" : "<color=#6a645c>·</color>";
                    sb.Append("\n<color=#ffb060>진화 → ").Append(SkillBook.Get(sel.EvolveTo).Name).Append("</color> <color=#8a8178>(셋 중 둘)</color>")
                      .Append("\n").Append(Mark(byUses)).Append(" 사용 ").Append(Math.Min(uses, sel.EvolveUses).ToString("N0")).Append('/').Append(sel.EvolveUses.ToString("N0"))
                      .Append("   ").Append(Mark(bySigil)).Append(" 문양 ").Append(sel.EvolveSigilName)
                      .Append("   ").Append(Mark(byEssence)).Append(" 정수");
                }
                else if (sel.IsEvolution) sb.Append("\n<color=#ffb060>진화한 스킬</color>");
                _detailBody.text = sb.ToString();
                SetBar(_detailBarFill, Progress(uses, lv), 72f);
            }

            // Skill picker: only skills taken in 기억의 나무 can go in the ring.
            _available = SkillBook.Available(h.ClassId, h.IntegratedLevel, TreeSkills(h));
            while (_skillCells.Count < _available.Count) AddSkillCell();
            for (int i = 0; i < _skillCells.Count; i++)
            {
                var c = _skillCells[i];
                bool on = i < _available.Count;
                c.frame.gameObject.SetActive(on);
                if (!on) continue;
                var s = _available[i];
                c.icon.sprite = _ui.Catalog.Get(SkillIcon(s));
                int at = -1;
                for (int k = 0; k < 4; k++) if (ring.Slots[k] != null && ring.Slots[k].BaseId == s.BaseId) at = k;
                c.badge.text = at >= 0 ? (at + 1).ToString() : "";
                c.frame.color = at == _slot ? new Color(0.35f, 0.27f, 0.12f, 0.95f) : new Color(0.08f, 0.07f, 0.07f, 0.95f);
            }
            bool sigilsOpen = h.IntegratedLevel >= SkillBook.SigilLevel;
            _sigilLabel.text = sigilsOpen ? "보조 문양 (해당 칸과 다음 칸에 적용)" : "보조 문양 · 통합 레벨 " + SkillBook.SigilLevel + "에서 개방";
            _sigilGrid.gameObject.SetActive(sigilsOpen);
            for (int i = 0; i < _sigilCells.Count; i++)
                _sigilCells[i].frame.color = AllSigils[i] == ring.Sigils[_slot] ? new Color(0.2f, 0.25f, 0.4f, 0.95f) : new Color(0.08f, 0.07f, 0.07f, 0.95f);
            for (int i = 0; i < _presets.Count; i++)
                _presets[i].GetComponentInChildren<Text>().color = !h.UseCustomRing && h.Preset == i ? UiKit.Gold : UiKit.Dim;
        }

        void AddSkillCell()
        {
            int i = _skillCells.Count;
            var frame = UiKit.Fill(_skillGrid, "skill", (i % 11) * 38f, (i / 11) * 38f, 36f, 36f, new Color(0.08f, 0.07f, 0.07f, 0.95f));
            frame.raycastTarget = true;
            var icon = _ui.Sprite(frame.transform, "icon/skill_ember_slash", 2f, 2f, 32f, 32f);
            var badge = UiKit.Label(frame.transform, "", 0f, 0f, 34f, 12f, 12, TextAnchor.UpperRight, UiKit.Gold);
            var ptr = frame.gameObject.AddComponent<UiPointer>();
            ptr.LeftClick += () =>
            {
                if (i >= _available.Count || _slot >= SkillBook.SlotsAt(_world.Hero.IntegratedLevel)) return;
                _world.SetCustomSkill(_slot, _available[i].Id);
                Changed();
            };
            ptr.Enter += () => { if (i < _available.Count) _preview.text = Preview(_available[i]); };
            ptr.Exit += () => _preview.text = "";
            _skillCells.Add((frame, icon, badge, ptr));
        }

        /// <summary>"넣으면": the skill's name and the resonances it would make with the slot before and after.</summary>
        string Preview(SkillDef s)
        {
            var h = _world.Hero;
            var ring = h.Ring;
            int open = SkillBook.SlotsAt(h.IntegratedLevel);
            var sb = new StringBuilder();
            sb.Append("<color=#f2e6cf>").Append(s.Name).Append("</color>  <color=#8a8178>").Append(ElementName(s.Elem))
              .Append(" · 재사용 대기시간 ").Append(s.Cooldown.ToString("0.#")).Append("초</color>");
            if (_slot >= open) return sb.ToString();
            var conv = ring.ModsFor(_slot).Convert;
            var e = conv != Element.None ? conv : s.Elem;
            int prev = (_slot + 3) % 4, next = (_slot + 1) % 4;
            sb.Append("\n넣으면  ");
            sb.Append(Pair(prev < open ? ring.Slots[prev] : null, prev, e, true)).Append("   ");
            sb.Append(Pair(next < open ? ring.Slots[next] : null, next, e, false));
            return sb.ToString();
        }

        string Pair(SkillDef other, int otherSlot, Element mine, bool otherFirst)
        {
            if (other == null) return "";
            var eo = ElementOf(_world.Hero.Ring, otherSlot);
            var res = otherFirst ? Elements.Resolve(eo, mine) : Elements.Resolve(mine, eo);
            string arrow = otherFirst ? ElementName(eo) + "→" + ElementName(mine) : ElementName(mine) + "→" + ElementName(eo);
            if (Elements.IsForward(res)) return "<color=#9fd28a>" + arrow + " " + FxView.ResonanceName(res) + " ✓</color>";
            return "<color=#6a645c>" + arrow + " —</color>";
        }

        // ---------- helpers ----------

        static HashSet<string> TreeSkills(Hero h)
        {
            var set = new HashSet<string>();
            foreach (var id in h.TreeNodes) if (id.StartsWith("s:", StringComparison.Ordinal)) set.Add(id.Substring(2));
            return set;
        }

        static float Progress(int uses, int lv)
        {
            if (lv >= SkillBook.MaxSkillLevel) return 1f;
            int a = SkillBook.UsesFor(lv), b = SkillBook.UsesFor(lv + 1);
            return b > a ? Mathf.Clamp01((uses - a) / (float)(b - a)) : 1f;
        }

        static void SetBar(Image fill, float frac, float width)
        {
            var rt = fill.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Round(width * Mathf.Clamp01(frac)), rt.sizeDelta.y);
        }

        static void Line(Image img, Vector2 from, Vector2 to, float thick, Color c)
        {
            var rt = img.rectTransform;
            var d = to - from;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(from.x, -from.y);
            rt.sizeDelta = new Vector2(d.magnitude, thick);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg);
            img.color = c;
        }

        static Element ElementOf(SkillRing ring, int slot)
        {
            var s = ring.Slots[slot];
            if (s == null) return Element.None;
            var conv = ring.ModsFor(slot).Convert;
            return conv != Element.None ? conv : s.Elem;
        }

        string SkillIcon(SkillDef s) => _ui.Catalog.Has("icon/skill_" + s.Id) ? "icon/skill_" + s.Id : "icon/skill_" + s.BaseId;

        static string SigilIcon(Sigil g) => g == Sigil.None ? "icon/item_rune" : "icon/sigil_" + g.ToString().ToLowerInvariant();

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
    }
}
