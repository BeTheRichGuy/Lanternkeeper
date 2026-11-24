using System;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Screen HUD on the GUI canvas — Diablo II's bottom panel in bone and wax (희승 11-22, 시안 7): on the left the heart
    /// in its ribcage (life is the blood in it, the heart beats faster as it drains; click to kindle the lantern), in the
    /// middle the bone bar with the four ring skills (cooldown sweep, practice badge, glyph), the class resource and the
    /// experience groove, on the right the cup of molten wax (the resonance gauge); the last three drops over the heart. Top centre: where the hero is and how long to the next level. Bottom right:
    /// icon menu, names only on hover. Replaces the text-and-dots corner of <see cref="HudView"/> and the text
    /// <see cref="MenuBar"/>; the boss bar, zone lines and event prompts stay there.
    /// </summary>
    public sealed class HudBar
    {
        const float BarW = 512f, BarH = 128f, Slot = 56f, Orb = 128f;
        /// <summary>The bone bar's four slots and grooves, in UI px (the 256×64 art drawn at ×2).</summary>
        static readonly float[] SlotX = { 136f, 198f, 260f, 323f };
        const float SlotY = 45f, SlotH = 52f, HeatX = 136f, HeatW = 243f, XpX = 70f, XpY = 104f, XpW = 364f;
        readonly UiKit _ui;
        readonly SimWorld _world;
        readonly RectTransform _root, _bar, _menu, _top, _navLayer;

        // lantern
        readonly RectTransform _lifeOrb, _waxOrb;
        readonly Image _blood, _heart, _igniteGlow, _wax;
        readonly Text _igniteText, _lifeText, _waxText;
        bool _hoverWax, _hoverHeat;
        readonly Image[] _todo = new Image[16];
        float _todoAt;

        /// <summary>
        /// Plan D3, every second: 가방 gets a dot when a bag piece is ▲ or ⇄ for the hero (auto-equip leaves those to the
        /// player), 스킬 트리 when points are left and 자동 분배 is off. Opening the window answers why.
        /// </summary>
        void RenderTodo()
        {
            if (Time.unscaledTime < _todoAt) return;
            _todoAt = Time.unscaledTime + 1f;
            var h = _world.Hero;
            bool bag = false;
            foreach (var it in _world.Bag.Items)
            {
                var a = HeroStats.Compare(h, it).Arrow;
                if (a == GearArrow.Up || a == GearArrow.BigUp || a == GearArrow.Trade) { bag = true; break; }
            }
            bool tree = !h.TreeAuto && SkillTree.Points(h) > SkillTree.Spent(h);
            for (int i = 0; i < Menu.Length && i < _todo.Length; i++)
                if (_todo[i] != null) _todo[i].enabled = (i == 0 && bag) || (i == 2 && tree);
        }
        Image _heatBack;
        int _lifeKey = -1, _waxKey = -1;
        static Sprite _square;
        float _hpShown = 1f, _xpShown;
        int _igniteKey = -1;

        // skills
        readonly Image[] _frame = new Image[4], _icon = new Image[4], _cd = new Image[4], _sigil = new Image[4], _lvBar = new Image[4];
        readonly Text[] _lv = new Text[4];
        readonly string[] _shown = new string[4];
        readonly Sigil[] _sigilShown = { (Sigil)(-1), (Sigil)(-1), (Sigil)(-1), (Sigil)(-1) };
        readonly int[] _lvShown = { -1, -1, -1, -1 };
        readonly Image _heat, _xp;
        float _gaugeShown;

        // drops
        readonly SlotView[] _drops = new SlotView[3];
        readonly Item[] _dropShown = new Item[3];
        readonly ItemTooltip _tip;

        // hover card (skills, lantern, menu)
        readonly RectTransform _card;
        readonly Text _cardText;
        int _hoverSkill = -1, _hoverMenu = -1;
        bool _hoverVial;
        string _cardKey;

        // boss bar under the top line (the drawn horned frame, a red fill and a pale damage trail)
        const float BossW = 360f;
        readonly RectTransform _boss;
        readonly CanvasGroup _bossGroup;
        readonly Image _bossFill, _bossTrail;
        readonly Text _bossName;
        float _bossShown = 1f, _bossTrailShown = 1f, _bossAlpha;
        int _bossId = -1;

        // top line
        readonly Text _where, _eta, _goal;
        long _whereKey = -1;
        float _etaTimer;
        double _xpRate; // smoothed experience per second
        long _lastXpTotal = -1;
        float _lastXpAt;

        public Action<int> OpenMenu;

        /// <summary>Menu buttons: label, key, icon (one drawn set, 11-21; the label's first letter if a piece is missing).</summary>
        // UX plan E1/D3: one set of names, and a two-letter label under each icon (a ring = skills, a letter = story read
        // as riddles, UX audit #21).
        static readonly (string label, string key, string icon, string tag)[] Menu =
        {
            // The tag is the window's own title (loop 2: 스킬/트리/야영/기록/도움 against titles 스킬 배치/스킬 트리/…).
            ("가방", "I", "ui/ui3_menu_bag", "가방"), ("스킬 배치", "R", "ui/ui3_menu_ring", "스킬 배치"), ("스킬 트리", "T", "ui/ui3_menu_tree", "스킬 트리"),
            ("야영지", "Y", "ui/ui3_menu_camp", "야영지"), ("도감", "J", "ui/ui3_menu_codex", "도감"), ("이야기", "U", "ui/ui3_menu_mail", "이야기"),
            ("알림 기록", "G", "ui/ui3_menu_journal", "알림 기록"), ("설정", "Esc", "ui/ui3_menu_settings", "설정"), ("도움말", "H", "ui/ui3_menu_help", "도움말"), ("능력치", "C", "ui/ui3_menu_char", "능력치"),
        };

        public HudBar(UiKit ui, SimWorld world)
        {
            _ui = ui;
            _world = world;
            _root = UiKit.Rect(ui.Root, "HUD", 0f, 0f, 0f, 0f);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f); // the hover card places itself from the centre
            _root.SetAsFirstSibling(); // under every window

            // ---------- bottom panel: heart, bone bar, wax ----------
            _bar = UiKit.Rect(_root, "bar", 0f, 0f, BarW, BarH);
            _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 0f);
            _bar.pivot = new Vector2(0.5f, 0f);
            _bar.anchoredPosition = Vector2.zero;
            var barArt = ui.Sprite(_bar, "ui/hud_bone_bar", 0f, 0f, BarW, BarH); // 256×64 at ×2
            barArt.preserveAspect = false;
            barArt.raycastTarget = true; // clicks on the bar never reach the world
            if (_square == null) _square = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));

            // the heart orb, left of the bar; its window is 88 across, under the ribs
            _lifeOrb = UiKit.Rect(_bar, "life orb", -Orb - 2f, 0f, Orb, Orb);
            _igniteGlow = UiKit.Fill(_lifeOrb, "ignite glow", -18f, -18f, Orb + 36f, Orb + 36f, new Color(1f, 0.8f, 0.45f, 0f));
            _igniteGlow.sprite = UiKit.Glow(ui, 64);
            var lifeBack = UiKit.Fill(_lifeOrb, "life back", 20f, 22f, 88f, 88f, new Color(0.07f, 0.01f, 0.02f, 1f));
            lifeBack.sprite = UiKit.Disc(ui, 88, 0);
            _blood = UiKit.Fill(_lifeOrb, "blood", 20f, 22f, 88f, 88f, new Color(0.6f, 0.04f, 0.06f, 1f));
            _blood.sprite = UiKit.Disc(ui, 88, 0);
            _blood.type = Image.Type.Filled;
            _blood.fillMethod = Image.FillMethod.Vertical;
            _blood.fillOrigin = (int)Image.OriginVertical.Bottom;
            _heart = ui.Sprite(_lifeOrb, "ui/hud_heart", 32f, 34f, 64f, 64f);
            var ribs = ui.Sprite(_lifeOrb, "ui/hud_heart_frame", 0f, 0f, Orb, Orb);
            ribs.raycastTarget = true;
            var lp = ribs.gameObject.AddComponent<UiPointer>();
            lp.Enter = () => _hoverVial = true;
            lp.Exit = () => _hoverVial = false;
            lp.LeftClick = () => _world.Ignite();
            _lifeText = UiKit.Label(_lifeOrb, "", -24f, -14f, Orb + 48f, 12f, 12, TextAnchor.UpperCenter, new Color(1f, 0.72f, 0.68f));
            // Above the drop icons, apart from the life number: right over "720 / 720" it read as that number's gauge (loop 3).
            // Over the "최근 획득" row, not on it (critique 6: the three drops read as part of the ignite line).
            _igniteText = UiKit.Label(_lifeOrb, "", -24f, -104f, Orb + 48f, 14f, 12, TextAnchor.UpperCenter, UiKit.Dim);

            // the wax cup, right of the bar: its molten wax is the resonance gauge
            _waxOrb = UiKit.Rect(_bar, "wax orb", BarW + 2f, 0f, Orb, Orb);
            // The wax takes the cup's own opening, read off the frame art (희승 11-23: 공명 게이지가 딱 안 들어맞는다 —
            // an ellipse left gaps at the sides and the bottom).
            var opening = CupOpening(ui.Catalog.Get("ui/hud_wax_frame"), out var box);
            var waxBack = UiKit.Fill(_waxOrb, "wax back", box.x, box.y, box.width, box.height, new Color(0.06f, 0.03f, 0.02f, 1f));
            waxBack.sprite = opening;
            _wax = UiKit.Fill(_waxOrb, "wax", box.x, box.y, box.width, box.height, new Color(1f, 0.55f, 0.15f, 1f));
            _wax.sprite = opening;
            _wax.type = Image.Type.Filled;
            _wax.fillMethod = Image.FillMethod.Vertical;
            _wax.fillOrigin = (int)Image.OriginVertical.Bottom;
            var cup = ui.Sprite(_waxOrb, "ui/hud_wax_frame", 0f, 0f, Orb, Orb);
            cup.raycastTarget = true;
            var wp = cup.gameObject.AddComponent<UiPointer>();
            wp.Enter = () => _hoverWax = true;
            wp.Exit = () => _hoverWax = false;
            _waxText = UiKit.Label(_waxOrb, "", -24f, -14f, Orb + 48f, 12f, 12, TextAnchor.UpperCenter, new Color(1f, 0.8f, 0.5f));

            // the class resource: a thin line over the four slots
            // Plan F4: 4 px (was 2) and a hover line on what it does.
            var heatBack = UiKit.Fill(_bar, "heat back", HeatX, SlotY - 7f, HeatW, 5f, new Color(0f, 0f, 0f, 0.6f));
            heatBack.raycastTarget = true;
            var hp2 = heatBack.gameObject.AddComponent<UiPointer>();
            hp2.Enter = () => _hoverHeat = true;
            hp2.Exit = () => _hoverHeat = false;
            _heatBack = heatBack;
            _heat = UiKit.Fill(_bar, "heat", HeatX, SlotY - 6f, HeatW, 4f, Color.white);
            // The bar's name, as the class card says it (loop 4: "자원 열기" on the card, an unnamed line here).
            // 11 px and readable ink (critique 6 P0: at 8 px in the 명조 set it was an unreadable smudge).
            // Centred over the bar, clear of the corner candles (critique 7 P0: at the bar's right end the wax hid it).
            // (critique 8 P0: no spot over the bar is free of the frame art — the name lives in the gauge's hover card.)

            for (int i = 0; i < 4; i++)
            {
                float x = SlotX[i], y = SlotY;
                int slot = i;
                _icon[i] = ui.Sprite(_bar, "ui/dot", x + 6f, y + 4f, 44f, 44f);
                _icon[i].enabled = false; // an empty slot stays empty until a skill shows (null == null never triggers the first update)
                _cd[i] = UiKit.Fill(_bar, "cooldown", x + 6f, y + 4f, 44f, 44f, new Color(0f, 0f, 0f, 0.66f));
                _cd[i].sprite = _square;
                _cd[i].type = Image.Type.Filled;
                _cd[i].fillMethod = Image.FillMethod.Radial360;
                _cd[i].fillOrigin = (int)Image.Origin360.Top;
                _cd[i].fillClockwise = false;
                // the slot's own edge lights gold for an evolved skill (clear otherwise); it is also the hover target
                _frame[i] = ui.Sprite(_bar, "ui/ui2_cell_hover", x, y, Slot, SlotH, sliced: true);
                _frame[i].preserveAspect = false;
                _frame[i].color = Color.clear;
                _frame[i].raycastTarget = true;
                _sigil[i] = ui.Sprite(_bar, "ui/dot", x + Slot - 15f, y - 3f, 14f, 14f);
                _lvBar[i] = UiKit.Fill(_bar, "practice", x + 6f, y + SlotH - 3f, 0f, 2f, new Color(0.6f, 0.8f, 1f, 0.9f));
                _lv[i] = UiKit.Label(_bar, "", x, y + SlotH - 19f, Slot - 7f, 14f, 11, TextAnchor.LowerRight, UiKit.Ink); // readable (critique 7 P0: 8 px on the corner art)
                _lv[i].gameObject.AddComponent<Outline>().effectColor = Color.black;
                var hit = _frame[i].gameObject.AddComponent<UiPointer>();
                hit.Enter = () => _hoverSkill = slot;
                hit.Exit = () => { if (_hoverSkill == slot) _hoverSkill = -1; };
            }

            // last three drops, over the heart, named (critique 6: three unnamed squares)
            _dropsLabel = UiKit.Label(_bar, "최근 획득", -Orb + 8f, -82f, 120f, 14f, 11, TextAnchor.UpperLeft, UiKit.Dim);
            _tip = new ItemTooltip(ui, "hud drop tip");
            for (int i = 0; i < 3; i++)
            {
                var v = _drops[i] = new SlotView(ui, _bar, -Orb + 8f + i * 34f, -66f, 30f, 30f, SlotStyle.Cell, "ui/ui2_cell", "ui/ui2_cell_hover");
                v.Pointer.Enter += () => { if (v.Item != null) { _tip.Show(v.Item, "최근 획득"); _tip.PlaceBeside(v.Root); } };
                v.Pointer.Exit += () => _tip.Hide();
            }

            // experience in the groove along the bar's foot
            _xp = UiKit.Fill(_bar, "xp", XpX, XpY, 0f, 5f, new Color(1f, 0.74f, 0.32f, 0.95f));

            // ---------- top centre ----------
            _top = UiKit.Rect(_root, "where", 0f, 0f, 320f, 30f);
            _top.anchorMin = _top.anchorMax = new Vector2(0.5f, 1f);
            _top.pivot = new Vector2(0.5f, 1f);
            _top.anchoredPosition = new Vector2(0f, -4f);
            _where = UiKit.Label(_top, "", 0f, 0f, 320f, 14f, 12, TextAnchor.UpperCenter, UiKit.Ink);
            _eta = UiKit.Label(_top, "", 0f, 15f, 320f, 12f, 8, TextAnchor.UpperCenter, UiKit.Dim);
            // UX plan D1: the next goal, always one line, under the level ETA.
            _goal = UiKit.Label(_top, "", -160f, 28f, 640f, 16f, 12, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.4f)); // as big as the level line (loop 1: the smallest text on screen)

            // ---------- boss bar ----------
            _boss = UiKit.Rect(_root, "boss", 0f, 0f, BossW, 44f);
            _boss.anchorMin = _boss.anchorMax = new Vector2(0.5f, 1f);
            _boss.pivot = new Vector2(0.5f, 1f);
            _boss.anchoredPosition = new Vector2(0f, -54f); // under the 다음: line (critique 5 P0: the boss name sat on it)
            _bossGroup = _boss.gameObject.AddComponent<CanvasGroup>();
            _bossGroup.blocksRaycasts = false;
            _bossName = UiKit.Label(_boss, "", 0f, 0f, BossW, 16f, 12, TextAnchor.UpperCenter, new Color(1f, 0.8f, 0.7f), title: true);
            // The frame's groove (fill_rect 27..113 × 8..14 of 128×24) is see-through: the fill sits behind it.
            UiKit.Fill(_boss, "boss back", 27f, 26f, BossW - 42f, 7f, new Color(0.08f, 0.06f, 0.08f, 0.9f));
            _bossTrail = UiKit.Fill(_boss, "boss trail", 27f, 26f, BossW - 42f, 7f, new Color(0.95f, 0.85f, 0.6f, 0.85f));
            _bossFill = UiKit.Fill(_boss, "boss fill", 27f, 26f, BossW - 42f, 7f, new Color(0.72f, 0.12f, 0.12f));
            if (ui.Catalog.Has(UiFrame.BossBar))
            {
                var bossFrame = ui.Sprite(_boss, UiFrame.BossBar, 0f, 18f, BossW, 24f, sliced: true);
                bossFrame.preserveAspect = false;
            }
            _bossGroup.alpha = 0f;

            // ---------- right-edge menu ----------
            // A column down the right edge, names to its left (loop 3 P0): as a row over the wax cup it sat where the bag
            // and skill windows reach, and either the windows cut it or it covered their buttons. No window reaches x 1196.
            const float B = 30f, G = 2f, TagW = 40f;
            float mh = Menu.Length * (B + G) - G;
            _navLayer = UiKit.Rect(ui.Root, "HUD menu", 0f, 0f, 0f, 0f);
            _navLayer.anchorMin = Vector2.zero; _navLayer.anchorMax = Vector2.one;
            _navLayer.offsetMin = _navLayer.offsetMax = Vector2.zero;
            _menu = UiKit.Rect(_navLayer, "menu", 0f, 0f, TagW + 4f + B, mh);
            _menu.anchorMin = _menu.anchorMax = new Vector2(1f, 0.5f);
            _menu.pivot = new Vector2(1f, 0.5f);
            _menu.anchoredPosition = new Vector2(-6f, -76f); // under the minimap's hunt board, above the wax cup
            for (int i = 0; i < Menu.Length; i++)
            {
                int index = i;
                var back = ui.Sprite(_menu, "ui/ui2_cell", TagW + 4f, i * (B + G), B, B, sliced: true);
                back.preserveAspect = false;
                back.raycastTarget = true;
                var hover = ui.Sprite(back.transform, "ui/ui2_cell_hover", 0f, 0f, B, B, sliced: true);
                hover.preserveAspect = false;
                hover.enabled = false;
                string icon = Menu[i].icon;
                if (icon != null && ui.Catalog.Has(icon)) ui.Sprite(back.transform, icon, -1f, -1f, 32f, 32f); // 32 px art 1:1, its empty margin over the edge
                else UiKit.Label(back.transform, Menu[i].label.Substring(0, 1), 0f, 0f, B, B, 12, TextAnchor.MiddleCenter, UiKit.Gold, title: true);
                // The key as a small badge on the icon's top-right corner, the name alone underneath (loop 2: "J 이야기 U 기록"
                // read the keys as part of the next name).
                float kw = 4f + 5f * Menu[i].key.Length;
                UiKit.Fill(back.transform, "key back", B - kw + 1f, -2f, kw, 10f, new Color(0f, 0f, 0f, 0.85f));
                UiKit.Label(back.transform, Menu[i].key, B - kw + 1f, -2f, kw, 10f, 8, TextAnchor.UpperCenter, UiKit.Gold);
                var tag = UiKit.Label(_menu, Menu[i].tag, 0f, i * (B + G) + 9f, TagW, 12f, 8, TextAnchor.UpperRight, UiKit.Dim);
                tag.horizontalOverflow = HorizontalWrapMode.Overflow;
                // A dark plate under the name (critique 6 P0: the names floated unreadably over the monsters).
                float tw = Mathf.Ceil(tag.preferredWidth) + 8f;
                var plate = UiKit.Fill(_menu, "tag back", TagW - tw + 4f, i * (B + G) + 7f, tw, 16f, new Color(0.03f, 0.025f, 0.03f, 0.75f));
                plate.transform.SetSiblingIndex(tag.transform.GetSiblingIndex());
                var p = back.gameObject.AddComponent<UiPointer>();
                p.Enter = () => { hover.enabled = true; _hoverMenu = index; };
                p.Exit = () => { hover.enabled = false; if (_hoverMenu == index) _hoverMenu = -1; };
                p.LeftClick = () => OpenMenu?.Invoke(index);
                // Plan D3: an orange dot on a menu with something to do now (a better piece in the bag, tree points left).
                _todo[i] = UiKit.Fill(back.transform, "todo", -3f, B - 6f, 9f, 9f, new Color(1f, 0.6f, 0.2f, 1f)); // bottom-left: clear of the hunt board above (critique 8 P0)
                _todo[i].sprite = UiKit.Disc(ui, 9, 0);
                _todo[i].enabled = false;
            }

            // ---------- hover card ----------
            _card = UiKit.Rect(_root, "hud card", 0f, 0f, 200f, 40f);
            _card.anchorMin = _card.anchorMax = new Vector2(0f, 0f);
            _card.pivot = new Vector2(0.5f, 0f);
            var cb = _card.gameObject.AddComponent<Image>();
            cb.color = new Color(0.04f, 0.035f, 0.05f, 0.95f);
            cb.raycastTarget = false;
            var frame = ui.Sprite(_card, ui.Catalog.Has("ui/ui_tooltip_frame") ? "ui/ui_tooltip_frame" : UiFrame.Window, -2f, -2f, 204f, 44f, sliced: true);
            frame.preserveAspect = false;
            frame.rectTransform.anchorMin = Vector2.zero; frame.rectTransform.anchorMax = Vector2.one;
            frame.rectTransform.offsetMin = new Vector2(-2f, -2f); frame.rectTransform.offsetMax = new Vector2(2f, 2f);
            _cardText = UiKit.Label(_card, "", 8f, 6f, 184f, 30f, 8, TextAnchor.UpperLeft, UiKit.Ink);
            _card.gameObject.SetActive(false);
        }

        public bool Visible { get => _root.gameObject.activeSelf; set { _root.gameObject.SetActive(value); _navLayer.gameObject.SetActive(value); } }

        /// <summary>
        /// While a window is open the orbs' side lines step back (critique 6 P0: window edges cut "점화 가능 [S", "785 / 7"
        /// in half). The orbs and the skill bar stay.
        /// </summary>
        Text _dropsLabel;
        bool _windowOpen;

        public void SetWindowOpen(bool open)
        {
            _windowOpen = open;
            _igniteText.enabled = _lifeText.enabled = _waxText.enabled = _dropsLabel.enabled = !open;
            foreach (var d in _drops) if (d.Root.gameObject.activeSelf == open) d.Root.gameObject.SetActive(!open); // the icons too (critique 7 P0)
        }

        public void Render(float dt)
        {
            if (!Visible) return;
            if (_navLayer.GetSiblingIndex() != _navLayer.parent.childCount - 1) _navLayer.SetAsLastSibling(); // over any window opened since
            var h = _world.Hero;
            float k = 1f - Mathf.Exp(-dt / 0.12f);
            // Pulses step eight times a second: the HUD shares the canvas with the windows, and a colour change every
            // frame would rebuild all of it every frame.
            float t = Mathf.Floor(Time.unscaledTime * 8f) / 8f;

            // life as the blood in the heart's cage; the heart beats faster as it drains, and low life pulses
            _hpShown = Mathf.Lerp(_hpShown, Mathf.Clamp01(h.Stats.Hp / h.Stats.MaxHp), k);
            _blood.fillAmount = _hpShown;
            _blood.color = _hpShown < 0.3f ? new Color(0.6f + 0.3f * Mathf.Abs(Mathf.Sin(t * 6f)), 0.04f, 0.06f) : new Color(0.6f, 0.04f, 0.06f);
            float bpm = Mathf.Lerp(150f, 60f, _hpShown), beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * bpm / 60f * Mathf.PI)), 8f);
            float hs = 1f + 0.08f * beat;
            _heart.rectTransform.localScale = new Vector3(hs, hs, 1f);
            _heart.color = Color.Lerp(new Color(0.45f, 0.35f, 0.35f), Color.white, 0.4f + 0.6f * _hpShown);
            int lifeKey = Mathf.CeilToInt(h.Stats.Hp) * 100000 + Mathf.CeilToInt(h.Stats.MaxHp);
            if (lifeKey != _lifeKey) { _lifeKey = lifeKey; _lifeText.text = Mathf.CeilToInt(h.Stats.Hp) + " / " + Mathf.CeilToInt(h.Stats.MaxHp); }

            // kindling: ready = breathing glow, burning = bright, recharging = a bar that fills
            bool on = _world.Ignited;
            float cdLeft = _world.IgniteCooldownLeft;
            _igniteGlow.color = new Color(1f, 0.8f, 0.45f, on ? 0.65f + 0.2f * Mathf.Sin(t * 10f) : cdLeft <= 0f ? 0.25f + 0.2f * Mathf.Sin(t * 3f) : 0f);
            int secs = Mathf.CeilToInt(on ? _world.IgniteLeft : cdLeft);
            int igniteKey = (on ? 100000 : cdLeft > 0f ? 0 : 200000) + secs;
            if (igniteKey != _igniteKey)
            {
                _igniteKey = igniteKey;
                // The key with the word (loop 2 P0: "점화 가능" said neither how nor what).
                _igniteText.text = on ? $"점화 중 {secs}초 · 피해 +15%" : cdLeft > 0f ? $"점화 {secs / 60}:{secs % 60:00}" : "점화 가능 [Space]";
            }
            _igniteText.color = on ? UiKit.Gold : cdLeft > 0f ? UiKit.Dim : new Color(1f, 0.8f, 0.45f, 0.75f + 0.25f * Mathf.Sin(t * 4f));

            RenderSkills(h, k, t);
            RenderDrops();
            RenderTodo();

            float xpTarget = Mathf.Clamp01((float)h.Xp / h.XpToNext);
            _xpShown = xpTarget < _xpShown - 0.5f ? xpTarget : Mathf.Lerp(_xpShown, xpTarget, k); // a level-up wraps at once
            _xp.rectTransform.sizeDelta = new Vector2(Mathf.Round(XpW * _xpShown), 5f);

            RenderTop(h, dt);
            RenderBoss(dt, k);
            RenderCard();
        }

        void RenderSkills(Hero h, float k, float t)
        {
            var ring = h.Ring;
            var cat = _ui.Catalog;
            for (int i = 0; i < 4; i++)
            {
                var s = ring.Slots[i];
                if (s?.Id != _shown[i])
                {
                    _shown[i] = s?.Id;
                    _icon[i].enabled = s != null;
                    if (s != null)
                    {
                        string key = cat.Has("icon/skill_" + s.Id) ? "icon/skill_" + s.Id : "icon/skill_" + s.BaseId;
                        _icon[i].sprite = cat.Has(key) ? cat.Get(key) : UiKit.Disc(_ui, 16, 0);
                        _icon[i].color = cat.Has(key) ? Color.white : FxView.ElementColor(s.Elem);
                    }
                    _frame[i].color = s != null && s.IsEvolution ? new Color(1f, 0.72f, 0.4f) : Color.clear;
                }
                if (ring.Sigils[i] != _sigilShown[i])
                {
                    _sigilShown[i] = ring.Sigils[i];
                    string sk = "icon/sigil_" + ring.Sigils[i].ToString().ToLowerInvariant();
                    _sigil[i].enabled = ring.Sigils[i] != Sigil.None && cat.Has(sk);
                    if (_sigil[i].enabled) _sigil[i].sprite = cat.Get(sk);
                }
                int uses = s != null && h.SkillUses.TryGetValue(s.BaseId, out int u) ? u : 0;
                int lv = s == null ? 0 : SkillBook.SkillLevel(uses);
                if (lv != _lvShown[i])
                {
                    _lvShown[i] = lv;
                    _lv[i].text = lv > 0 ? lv.ToString() : "";
                    _lv[i].color = lv >= SkillBook.CopyEvery ? UiKit.Gold : new Color(0.85f, 0.9f, 1f);
                }
                float next = lv <= 0 || lv >= SkillBook.MaxSkillLevel ? 1f
                    : (uses - SkillBook.UsesFor(lv)) / (float)Mathf.Max(1, SkillBook.UsesFor(lv + 1) - SkillBook.UsesFor(lv));
                _lvBar[i].rectTransform.sizeDelta = new Vector2(44f * (s == null ? 0f : Mathf.Clamp01(next)), 2f);
                float cd = s == null || s.Cooldown <= 0f ? 0f : Mathf.Clamp01((float)ring.CooldownLeft(i, _world.Time) / s.Cooldown);
                _cd[i].enabled = cd > 0.001f;
                _cd[i].fillAmount = cd;
            }
            _gaugeShown = Mathf.Lerp(_gaugeShown, h.Gauge / SimWorld.GaugeMax, k);
            float g = Mathf.Clamp01(_gaugeShown);
            float full = g >= 0.999f ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 8f)) : 1f;
            _wax.fillAmount = g;
            // molten wax, tinged toward the element the next great resonance will be
            var tint = Color.Lerp(new Color(1f, 0.48f, 0.1f), FxView.ElementColor(Elements.Next(h.LastResonanceMark)), 0.15f) * full;
            tint.a = 1f;
            _wax.color = tint;
            int waxKey = Mathf.FloorToInt(g * 100f);
            if (waxKey != _waxKey) { _waxKey = waxKey; _waxText.text = "대공명 " + waxKey + "%"; } // the gauge toward 대공명 (critique 7: "공명" here and "공명 0개" in the ring meant two things)
            _heat.rectTransform.sizeDelta = new Vector2(Mathf.Round(HeatW * Mathf.Clamp01(h.Heat / SimWorld.HeatMax)), 4f);
            _heat.color = h.ClassId == "oathknight" ? new Color(1f, 0.85f, 0.4f, 0.9f) : h.ClassId == "windhunter" ? new Color(0.55f, 0.95f, 0.75f, 0.9f)
                : h.ClassId == "starseer" ? new Color(0.65f, 0.6f, 1f, 0.9f) : new Color(1f, 0.45f, 0.15f, 0.9f);
        }

        void RenderDrops()
        {
            for (int i = 0; i < 3; i++)
            {
                var item = i < _world.RecentDrops.Count ? _world.RecentDrops[i] : null;
                if (item == _dropShown[i]) continue;
                _dropShown[i] = item;
                if (item == null) _drops[i].Clear();
                else _drops[i].Show(item, null);
                // A new drop pushed the row along under the pointer: the tooltip follows the slot's new item.
                if (_drops[i].Hovering) { if (item != null) { _tip.Show(item, "최근 획득"); _tip.PlaceBeside(_drops[i].Root); } else _tip.Hide(); }
            }
            foreach (var d in _drops) d.Tick(Time.unscaledTime);
        }

        void RenderTop(Hero h, float dt)
        {
            long key = h.Level * 10000L + _world.FloorIndex + h.EmberLevel * 1000000L + h.WickLevel * 10000000000L + (_world.FastDescent ? 7L : 0L)
                + (_world.FloorLock ? 1L << 40 : 0L) + (_world.CodexFirst ? 1L << 41 : 0L) + (_world.TopLoop ? 1L << 42 : 0L);
            if (key != _whereKey)
            {
                _whereKey = key;
                var act = _world.ActNow;
                string where = _world.IsActBossFloor ? "막 " + act.Numeral + " · " + act.Name + " · 보스"
                    : "막 " + act.Numeral + " · " + act.Name + " · " + (_world.FloorInAct + 1) + " / " + SimWorld.ActLength + "층"; // one way of saying where, as the goal line counts it (loop 2)
                int lap = _world.ActLoop - (EnemyRoster.Acts.Length - 1);
                if (lap > 0) where += " · " + (lap + 1) + "회차";
                string lv = "Lv " + h.Level + (h.EmberLevel > 0 ? " <color=#ffb060>+" + h.EmberLevel + "</color>" : "");
                if (h.WickLevel > 0) lv += " <color=#ffe7a0>심지 " + h.WickLevel + "</color>";
                string mode = _world.FloorLock ? "  <color=#9fc8ff>층 고정</color>" : _world.CodexFirst ? "  <color=#9fc8ff>도감 우선</color>" : _world.TopLoop ? "  <color=#9fc8ff>최상층 반복</color>" : "";
                _where.text = lv + "   " + where + (_world.FastDescent ? "  <color=#9fd28a>고속 진행 ×" + _world.RepeatMul + "</color>" : "") + mode;
            }

            // Next goal: the next level, timed from the experience of the last minute or so.
            long total = (long)h.Level * 1000000000L + h.Xp;
            float now = Time.unscaledTime;
            if (_lastXpTotal < 0 || total < _lastXpTotal) { _lastXpTotal = total; _lastXpAt = now; }
            _etaTimer += dt;
            if (_etaTimer < 1f) return;
            _etaTimer = 0f;
            float span = now - _lastXpAt;
            if (span >= 1f)
            {
                double gained = total - _lastXpTotal;
                if (gained >= 1000000000L) gained = h.Xp; // a level-up: count what came after it
                double rate = gained / span;
                _xpRate = _xpRate <= 0 ? rate : _xpRate + (rate - _xpRate) * 0.1;
                _lastXpTotal = total; _lastXpAt = now;
            }
            string goal = _world.NextGoal();
            if (_goal.text != goal) _goal.text = goal;
            if (h.Level >= SimWorld.LevelCap) { _eta.text = _world.PendingEmber > 0 ? "다음 환생 시 잔불 +" + _world.PendingEmber : ""; return; }
            double left = h.XpToNext - h.Xp;
            _eta.text = _xpRate > 0.01 ? "Lv " + (h.Level + 1) + "까지 약 " + Duration(left / _xpRate) : "";
        }

        void RenderBoss(float dt, float k)
        {
            Enemy boss = null;
            foreach (var e in _world.Enemies) if (e.Boss && e.Aggro && e.Alive) { boss = e; break; }
            _bossAlpha = Mathf.MoveTowards(_bossAlpha, boss != null ? 1f : 0f, dt / 0.3f);
            float a = Mathf.Round(_bossAlpha * 8f) / 8f;
            if (!Mathf.Approximately(_bossGroup.alpha, a)) _bossGroup.alpha = a;
            if (boss == null)
            {
                if (_bossAlpha <= 0f) { _bossShown = _bossTrailShown = 1f; _bossId = -1; }
                return;
            }
            if (boss.Id != _bossId)
            {
                _bossId = boss.Id;
                _bossName.text = string.IsNullOrEmpty(boss.Def.Name) ? "보스" : boss.Def.Name;
                _bossShown = _bossTrailShown = Mathf.Clamp01(boss.Stats.Hp / boss.Stats.MaxHp);
            }
            float frac = Mathf.Clamp01(boss.Stats.Hp / boss.Stats.MaxHp);
            _bossShown = Mathf.Lerp(_bossShown, frac, k);
            _bossTrailShown = Mathf.MoveTowards(_bossTrailShown, frac, dt * 0.35f); // the pale trail drains after the hit
            float w = BossW - 42f;
            var fs = new Vector2(Mathf.Round(w * _bossShown), 7f);
            var ts = new Vector2(Mathf.Round(w * Mathf.Max(_bossShown, _bossTrailShown)), 7f);
            if (_bossFill.rectTransform.sizeDelta != fs) _bossFill.rectTransform.sizeDelta = fs;
            if (_bossTrail.rectTransform.sizeDelta != ts) _bossTrail.rectTransform.sizeDelta = ts;
        }

        static string Duration(double seconds)
        {
            if (seconds < 60) return Mathf.Max(1, (int)seconds) + "초";
            if (seconds < 3600) return (int)(seconds / 60) + "분";
            int hrs = (int)(seconds / 3600), mins = (int)(seconds % 3600 / 60);
            return mins > 0 ? hrs + "시간 " + mins + "분" : hrs + "시간";
        }

        void RenderCard()
        {
            string key = null, text = null;
            RectTransform anchor = null;
            var h = _world.Hero;
            // Under an open window no HUD card (critique 8 P0: a skill card leaked out below the codex).
            var hs = _hoverSkill >= 0 && !_windowOpen ? h.Ring.Slots[_hoverSkill] : null;
            if (hs != null)
            {
                h.SkillUses.TryGetValue(hs.BaseId, out int uses);
                int lv = SkillBook.SkillLevel(uses);
                key = "s" + _hoverSkill + hs.Id + uses;
                if (key != _cardKey)
                {
                    // The shared skill card, brief (UX plan B2): what it does, its numbers, the resonance it sets up, 숙련.
                    var conv = h.Ring.ModsFor(_hoverSkill).Convert;
                    text = SkillCard.Build(h, hs, brief: true, elem: conv, sigil: h.Ring.Sigils[_hoverSkill]);
                }
                anchor = _frame[_hoverSkill].rectTransform;
            }
            else if (_windowOpen) { }
            else if (_hoverVial)
            {
                key = "v" + Mathf.CeilToInt(h.Stats.Hp) + (_world.Ignited ? 1 : 0) + Mathf.CeilToInt(_world.IgniteCooldownLeft);
                if (key != _cardKey)
                    text = "<b>생명력</b>  " + Mathf.CeilToInt(h.Stats.Hp) + " / " + Mathf.CeilToInt(h.Stats.MaxHp)
                        + "\n<color=#8a8178>" + (_world.Ignited ? "점화 중: 더 밝고 넓게 비춘다" : _world.IgniteCooldownLeft > 0f ? "재사용 대기 중" : "클릭 또는 Space: 등불 점화") + "</color>";
                anchor = _lifeOrb;
            }
            else if (_hoverWax)
            {
                key = "w" + _waxKey;
                if (key != _cardKey)
                    text = "<b>대공명</b>  " + _waxKey + "%\n<color=#8a8178>원소가 서로 터질 때마다 촛농이 차오르고, 가득 차면 대공명이 터진다</color>";
                anchor = _waxOrb;
            }
            else if (_hoverHeat)
            {
                key = "h";
                if (key != _cardKey)
                    text = "<b>" + ClassSelectPanel.ResourceOf(h.ClassId) + "</b>  직업 자원\n<color=#8a8178>싸울수록 찹니다. 가득 차면 다음 스킬이 위력 1.5배 · 범위 +30%로 나갑니다</color>";
                anchor = _heatBack.rectTransform;
            }
            // The menu needs no card: each button shows its name beside it and its key on a badge (loop 3).
            if (key == null) { if (_cardKey != null) { _cardKey = null; _card.gameObject.SetActive(false); } return; }
            if (key == _cardKey) return;
            _cardKey = key;
            _cardText.text = text;
            float w = Mathf.Min(hs != null ? 340f : 220f, _cardText.preferredWidth + 2f); // a skill card carries sentences
            _cardText.rectTransform.sizeDelta = new Vector2(w, 30f);
            float hgt = _cardText.preferredHeight + 12f;
            _card.sizeDelta = new Vector2(w + 16f, hgt);
            // Above the hovered thing, kept on screen.
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var top = new Vector2((corners[1].x + corners[2].x) / 2f, corners[1].y);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, top, null, out var local);
            var size = _root.rect.size;
            float x = Mathf.Clamp(local.x + size.x / 2f, _card.sizeDelta.x / 2f + 4f, size.x - _card.sizeDelta.x / 2f - 4f);
            _card.anchoredPosition = new Vector2(x, local.y + size.y / 2f + 6f);
            _card.gameObject.SetActive(true);
        }

        /// <summary>
        /// The see-through pixels of the cup reachable from its middle — the opening the wax fills — as a white mask
        /// cropped to them, and where that crop sits in the frame (top-down, frame pixels = HUD units at 128 across).
        /// </summary>
        static Sprite CupOpening(Sprite frame, out Rect box)
        {
            var tr = frame.textureRect;
            int w = (int)tr.width, h = (int)tr.height, ox = (int)tr.x, oy = (int)tr.y, tw = frame.texture.width;
            var px = frame.texture.GetPixels32();
            var inside = new bool[w * h];
            var todo = new System.Collections.Generic.Stack<Vector2Int>();
            todo.Push(new Vector2Int(w / 2, h / 2));
            int x0 = w, x1 = -1, y0 = h, y1 = -1;
            while (todo.Count > 0)
            {
                var p = todo.Pop();
                if (p.x < 0 || p.y < 0 || p.x >= w || p.y >= h || inside[p.y * w + p.x] || px[(oy + p.y) * tw + ox + p.x].a > 40) continue;
                inside[p.y * w + p.x] = true;
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
                todo.Push(new Vector2Int(p.x + 1, p.y)); todo.Push(new Vector2Int(p.x - 1, p.y));
                todo.Push(new Vector2Int(p.x, p.y + 1)); todo.Push(new Vector2Int(p.x, p.y - 1));
            }
            int mw = x1 - x0 + 1, mh = y1 - y0 + 1;
            var mask = new Texture2D(mw, mh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[mw * mh];
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                    cols[y * mw + x] = inside[(y + y0) * w + x + x0] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            mask.SetPixels32(cols);
            mask.Apply();
            float s = Orb / w;
            box = new Rect(x0 * s, (h - 1 - y1) * s, mw * s, mh * s);
            return Sprite.Create(mask, new Rect(0f, 0f, mw, mh), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
