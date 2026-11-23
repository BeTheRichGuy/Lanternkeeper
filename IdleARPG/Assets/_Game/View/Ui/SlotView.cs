using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    public enum SlotStyle { Socket, Cell }

    /// <summary>
    /// One item place (재기획 §5.3, 성물함 아트 11-21). A <see cref="SlotStyle.Socket"/> is a worn-gear holder with its own
    /// carved shape (arch, shield, ring, teardrop, medallion): rarity glows behind the icon and counts in ◆ pips under
    /// the frame, an empty one shows its part as a faint silhouette. A <see cref="SlotStyle.Cell"/> is a bag recess:
    /// rarity washes the inside and edges it, each rarity its own edge (UX plan §3.0 가: common a quiet 1 px, magic and
    /// rare 1 px in their colour, legendary 2 px, unique a double line that glints every four seconds) so the order
    /// reads without colour too. Both carry ★ count, socket dots, lock, the compare arrow (▲▲ ▲ ▼ ◆), a "new" bar and
    /// a hover light. Frames come from the drawn UI kit (ui/ui2_*), with plain stand-ins if a piece is missing.
    /// </summary>
    public sealed class SlotView
    {
        public readonly RectTransform Root;
        public readonly UiPointer Pointer;
        public Item Item { get; private set; }

        readonly UiKit _ui;
        readonly SlotStyle _style;
        readonly string _frameKey, _hoverKey;
        readonly Image _glow, _wash, _frame, _icon, _hover, _newBack, _lockBody, _lockLoop;
        readonly Image[] _border = new Image[4], _inner = new Image[4];
        readonly Image[] _pips = new Image[5];
        readonly Image[] _sockets = new Image[3];
        readonly Image[] _pick = new Image[4]; // the white 2 px frame of the hovered place (tooltips no longer cover it)
        readonly Text _stars, _badge, _newText;
        readonly Outline _badgeOutline;
        readonly float _w, _h;
        bool _isNew, _hovering, _glints;

        public SlotView(UiKit ui, Transform parent, float x, float y, float w, float h, SlotStyle style, string frameKey, string hoverKey = null,
            bool slicedFrame = false, float iconSize = 32f)
        {
            _ui = ui;
            _style = style;
            _w = w;
            _h = h;
            _frameKey = ui.Catalog.Has(frameKey) ? frameKey : null;
            _hoverKey = hoverKey != null && ui.Catalog.Has(hoverKey) ? hoverKey : null;
            Root = UiKit.Rect(parent, "slot", x, y, w, h);
            var hit = Root.gameObject.AddComponent<Image>();
            hit.color = style == SlotStyle.Cell && _frameKey == null ? new Color(0.02f, 0.02f, 0.03f, 0.75f) : new Color(0f, 0f, 0f, 0.001f); // the pointer's target
            float sz = iconSize, io = Mathf.Floor((w - sz) / 2f), iy = Mathf.Floor((h - sz) / 2f);
            _glow = UiKit.Fill(Root, "glow", io - 6f, iy - 6f, sz + 12f, sz + 12f, Color.clear);
            _glow.sprite = UiKit.Glow(ui, 44); // soft: a flat disc covered the carved frame and every holder read as a coin
            _wash = UiKit.Fill(Root, "wash", 3f, 3f, w - 6f, h - 6f, Color.clear);
            _icon = ui.Sprite(Root, "icon/item_sword", io, iy, sz, sz);
            if (_frameKey != null)
            {
                _frame = ui.Sprite(Root, _frameKey, 0f, 0f, w, h, sliced: slicedFrame);
                _frame.preserveAspect = false;
            }
            else if (style == SlotStyle.Socket)
            {
                _frame = ui.Sprite(Root, "ui/ui_slot_common", 0f, 0f, w, h);
                _frame.preserveAspect = false;
            }
            if (style == SlotStyle.Cell)
                for (int i = 0; i < 4; i++)
                {
                    _border[i] = UiKit.Fill(Root, "b", 0f, 0f, 1f, 1f, Color.clear);
                    _inner[i] = UiKit.Fill(Root, "b2", 0f, 0f, 1f, 1f, Color.clear); // the unique's second line
                }
            // Worn places count their rarity in ◆ pips under the frame (UX plan A1: the gem there said it by colour alone).
            // Drawn 5 px diamonds: a text ◆ at this size would not land on whole pixels.
            // Bag cells carry them too, 3 px, bottom right inside the edge (loop 1: "가방 칸에는 마름모가 없고 보라색 칠뿐").
            int pip = style == SlotStyle.Socket ? 5 : 3;
            for (int i = 0; i < _pips.Length; i++)
            {
                _pips[i] = UiKit.Fill(Root, "pip", 0f, h - 3f, pip, pip, Color.clear); // sockets: astride the bottom edge, where the gem hung
                _pips[i].sprite = UiKit.Diamond(ui, pip);
            }
            for (int i = 0; i < 3; i++) _sockets[i] = UiKit.Fill(Root, "socket", w - 7f, iy + 2f + i * 5f, 3f, 3f, Color.clear);
            _stars = UiKit.Label(Root, "", io - 2f, iy + 20f, 36f, 12f, 12, TextAnchor.LowerLeft, new Color(1f, 0.81f, 0.35f));
            // 2 px inside the cell's right edge and a size smaller (critique 6: in the 명조 set ⇄ came from a wider fallback
            // font and stuck out of the cell).
            _badge = UiKit.Label(Root, "", io - 4f, iy - 2f, 32f, 12f, 11, TextAnchor.UpperRight, UiKit.Good);
            _badge.horizontalOverflow = HorizontalWrapMode.Overflow; // ▲▲ on a 30 px cell must not wrap under itself
            _badgeOutline = _badge.gameObject.AddComponent<Outline>(); // for ═: grey on the wash of a cell vanished (loop 2 #17)
            _badgeOutline.effectColor = Color.black;
            _badgeOutline.effectDistance = new Vector2(1f, -1f);
            _badgeOutline.enabled = false;
            _lockBody = UiKit.Fill(Root, "lock", io + 1f, iy + 4f, 6f, 4f, Color.clear);
            _lockLoop = UiKit.Fill(Root, "lock loop", io + 2f, iy + 1f, 4f, 3f, Color.clear);
            // "NEW" (UX plan A8): a 3-letter tag top left inside the cell, under the lock when there is one; replaces the blinking bar.
            _newBack = UiKit.Fill(Root, "new back", 3f, 2f, 17f, 8f, Color.clear);
            _newText = UiKit.Label(Root, "NEW", 3f, 2f, 17f, 8f, 8, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.55f));
            _newText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _newText.enabled = false;
            _hover = UiKit.Fill(Root, "hover", io - 2f, iy - 2f, sz + 4f, sz + 4f, Color.clear);
            for (int i = 0; i < 4; i++) _pick[i] = UiKit.Fill(Root, "pick", 0f, 0f, 1f, 1f, Color.clear);
            Pointer = Root.gameObject.AddComponent<UiPointer>();
            Pointer.Enter += () => SetHover(true);
            Pointer.Exit += () => SetHover(false);
        }

        void SetHover(bool on)
        {
            _hovering = on;
            _hover.color = on ? new Color(1f, 0.95f, 0.85f, 0.12f) : Color.clear;
            Edge(_pick, 0f, 2f, on && Item != null ? Color.white : Color.clear);
            if (_hoverKey != null && _frame != null) _frame.sprite = _ui.Catalog.Get(on ? _hoverKey : _frameKey);
        }

        /// <summary>One line of an edge at <paramref name="inset"/> from the cell's rim, <paramref name="thick"/> px wide.</summary>
        void Edge(Image[] sides, float inset, float thick, Color c)
        {
            float w = _w - inset * 2f, h = _h - inset * 2f;
            Place(sides[0], inset, inset, w, thick, c);
            Place(sides[1], inset, _h - inset - thick, w, thick, c);
            Place(sides[2], inset, inset, thick, h, c);
            Place(sides[3], _w - inset - thick, inset, thick, h, c);
        }

        static void Place(Image img, float x, float y, float w, float h, Color c)
        {
            img.rectTransform.anchoredPosition = new Vector2(x, -y);
            img.rectTransform.sizeDelta = new Vector2(w, h);
            img.color = c;
        }

        void Pips(int n, Color c)
        {
            bool cell = _style == SlotStyle.Cell;
            float step = cell ? 4f : 6f;
            float x = cell ? _w - 5f - (n * step - 1f) : Mathf.Floor((_w - (n * step - 1f)) / 2f);
            float y = cell ? _h - 8f : _h - 3f;
            for (int i = 0; i < _pips.Length; i++)
            {
                _pips[i].rectTransform.anchoredPosition = new Vector2(x + i * step, -y);
                _pips[i].color = i < n ? c : Color.clear;
            }
        }

        /// <summary>Shows <paramref name="item"/>, or the faint <paramref name="emptyIcon"/> (sockets) when null.</summary>
        /// <param name="arrow">The compare arrow in the top-right corner (UX plan §3.0 다); ═ and a level-locked piece show none.</param>
        public void Show(Item item, string emptyIcon, GearArrow arrow = GearArrow.Same, bool isNew = false, bool usable = true, bool showSame = false)
        {
            Item = item;
            _isNew = isNew && item != null;
            _glints = false;
            if (item == null)
            {
                _icon.enabled = emptyIcon != null;
                if (emptyIcon != null) { _icon.sprite = _ui.Catalog.Get(emptyIcon); _icon.color = new Color(0f, 0f, 0f, 0.5f); }
                _glow.color = _wash.color = Color.clear;
                Pips(0, Color.clear);
                if (_style == SlotStyle.Cell) for (int i = 0; i < 4; i++) _border[i].color = _inner[i].color = Color.clear;
                foreach (var s in _sockets) s.color = Color.clear;
                _stars.text = _badge.text = "";
                _lockBody.color = _lockLoop.color = Color.clear;
                SetNew(false, false);
                return;
            }
            var rc = ItemStyle.Color(item.Rarity);
            bool common = item.Rarity == Rarity.Common;
            _icon.enabled = true;
            _icon.sprite = _ui.Catalog.Get(ItemStyle.Icon(item));
            _icon.color = usable ? Color.white : new Color(0.55f, 0.35f, 0.35f, 1f); // above the 통합 레벨: dimmed red
            // Sockets glow softly behind the icon; bag cells keep only their wash and edge (a glow per cell turned the
            // whole chest one orange block).
            float glow = common ? 0f : item.Rarity >= Rarity.Legendary ? 0.55f : 0.4f;
            _glow.color = _style == SlotStyle.Socket ? new Color(rc.r, rc.g, rc.b, glow) : Color.clear;
            // Legendary and unique cells also light up inside (20%), the rest keep the faint wash.
            _wash.color = _style == SlotStyle.Cell ? new Color(rc.r, rc.g, rc.b, common ? 0.04f : item.Rarity >= Rarity.Legendary ? 0.2f : 0.14f) : Color.clear;
            if (_style == SlotStyle.Cell)
            {
                // Every rarity has an edge now: magic had none and vanished in the bag (audit #3).
                bool unique = item.Rarity == Rarity.Unique;
                Edge(_border, 2f, item.Rarity == Rarity.Legendary ? 2f : 1f, common ? ItemStyle.CommonEdge : rc);
                Edge(_inner, 4f, 1f, unique ? rc : Color.clear);
                _glints = unique;
            }
            Pips((int)item.Rarity + 1, rc);
            for (int i = 0; i < 3; i++)
                _sockets[i].color = i >= item.Sockets ? Color.clear
                    : i < item.RuneSlots.Count ? new Color(0.66f, 0.77f, 1f, 1f) : new Color(0.25f, 0.25f, 0.3f, 1f);
            int stars = item.GreaterCount;
            _stars.text = stars <= 0 ? "" : stars == 1 ? "★" : "★" + stars;
            // ═ shows too where the caller compares (the bag), faint (loop 1: a blank corner could mean weaker or the same).
            _badge.text = arrow == GearArrow.Same && !showSame ? "" : ItemStyle.ArrowGlyph(arrow);
            if (ColorUtility.TryParseHtmlString(ItemStyle.ArrowHex(arrow), out var ac))
                _badge.color = arrow == GearArrow.Same ? Color.white : arrow == GearArrow.Down ? new Color(ac.r, ac.g, ac.b, 0.7f) : ac; // ▼ is the common case: quieter, so ▲ and ⇄ lead; ═ must still be seen
            _badgeOutline.enabled = arrow == GearArrow.Same && _badge.text.Length > 0;
            _lockBody.color = item.Locked ? new Color(1f, 0.83f, 0.45f, 1f) : Color.clear;
            _lockLoop.color = item.Locked ? new Color(1f, 0.83f, 0.45f, 0.7f) : Color.clear;
            SetNew(_isNew, item.Locked);
        }

        void SetNew(bool on, bool locked)
        {
            float y = locked ? 9f : 2f; // the lock sits at y 3..8
            _newBack.rectTransform.anchoredPosition = _newText.rectTransform.anchoredPosition = new Vector2(3f, -y);
            _newBack.color = on ? new Color(0f, 0f, 0f, 0.75f) : Color.clear;
            _newText.enabled = on;
        }

        /// <summary>An unused pooled cell: forgets its item (so no tooltip can point at a salvaged piece) and its hover.</summary>
        public void Clear()
        {
            Item = null;
            _isNew = false;
            SetHover(false);
        }

        /// <summary>A hidden panel never gets the pointer's exit: drop the hover light by hand.</summary>
        public void ClearHover() => SetHover(false);

        /// <summary>A unique's inner line glints white for a quarter second every four seconds (the NEW tag is static).</summary>
        public void Tick(float t)
        {
            if (_glints)
            {
                var c = t % 4f < 0.25f ? Color.white : ItemStyle.Color(Rarity.Unique);
                if (_inner[0].color != c) foreach (var b in _inner) b.color = c;
            }
        }

        public bool Hovering => _hovering;
    }
}
