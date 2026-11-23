using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    public enum SlotStyle { Socket, Cell }

    /// <summary>
    /// One item place (재기획 §5.3, 성물함 아트 11-21). A <see cref="SlotStyle.Socket"/> is a worn-gear holder with its own
    /// carved shape (arch, shield, ring, teardrop, medallion): rarity glows behind the icon and in a gem under the frame,
    /// an empty one shows its part as a faint silhouette. A <see cref="SlotStyle.Cell"/> is a bag recess: rarity washes
    /// the inside and rare or better also edges it. Both carry ★ count, socket dots, lock, ▲ upgrade, a "new" bar and a
    /// hover light. Frames come from the drawn UI kit (ui/ui2_*), with plain stand-ins if a piece is missing.
    /// </summary>
    public sealed class SlotView
    {
        public readonly RectTransform Root;
        public readonly UiPointer Pointer;
        public Item Item { get; private set; }

        readonly UiKit _ui;
        readonly SlotStyle _style;
        readonly string _frameKey, _hoverKey;
        readonly Image _glow, _wash, _frame, _icon, _gem, _hover, _newBar, _lockBody, _lockLoop;
        readonly Image[] _border = new Image[4];
        readonly Image[] _sockets = new Image[3];
        readonly Text _stars, _badge;
        bool _isNew, _hovering;

        public SlotView(UiKit ui, Transform parent, float x, float y, float w, float h, SlotStyle style, string frameKey, string hoverKey = null,
            bool slicedFrame = false, float iconSize = 32f)
        {
            _ui = ui;
            _style = style;
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
            {
                _border[0] = UiKit.Fill(Root, "b", 2f, 2f, w - 4f, 1f, Color.clear);
                _border[1] = UiKit.Fill(Root, "b", 2f, h - 3f, w - 4f, 1f, Color.clear);
                _border[2] = UiKit.Fill(Root, "b", 2f, 2f, 1f, h - 4f, Color.clear);
                _border[3] = UiKit.Fill(Root, "b", w - 3f, 2f, 1f, h - 4f, Color.clear);
            }
            _gem = ui.Sprite(Root, "ui/ui2_gem", Mathf.Floor((w - 10f) / 2f), h - 7f, 10f, 10f);
            _gem.color = Color.clear;
            for (int i = 0; i < 3; i++) _sockets[i] = UiKit.Fill(Root, "socket", w - 7f, iy + 2f + i * 5f, 3f, 3f, Color.clear);
            _stars = UiKit.Label(Root, "", io - 2f, iy + 20f, 36f, 12f, 12, TextAnchor.LowerLeft, new Color(1f, 0.81f, 0.35f));
            _badge = UiKit.Label(Root, "", io, iy - 3f, 34f, 12f, 12, TextAnchor.UpperRight, UiKit.Good);
            _lockBody = UiKit.Fill(Root, "lock", io + 1f, iy + 4f, 6f, 4f, Color.clear);
            _lockLoop = UiKit.Fill(Root, "lock loop", io + 2f, iy + 1f, 4f, 3f, Color.clear);
            _newBar = UiKit.Fill(Root, "new", io + 2f, iy + 33f, 28f, 1f, Color.clear);
            _hover = UiKit.Fill(Root, "hover", io - 2f, iy - 2f, sz + 4f, sz + 4f, Color.clear);
            Pointer = Root.gameObject.AddComponent<UiPointer>();
            Pointer.Enter += () => SetHover(true);
            Pointer.Exit += () => SetHover(false);
        }

        void SetHover(bool on)
        {
            _hovering = on;
            _hover.color = on ? new Color(1f, 0.95f, 0.85f, 0.12f) : Color.clear;
            if (_hoverKey != null && _frame != null) _frame.sprite = _ui.Catalog.Get(on ? _hoverKey : _frameKey);
        }

        /// <summary>Shows <paramref name="item"/>, or the faint <paramref name="emptyIcon"/> (sockets) when null.</summary>
        public void Show(Item item, string emptyIcon, bool upgrade = false, bool isNew = false, bool usable = true)
        {
            Item = item;
            _isNew = isNew && item != null;
            if (item == null)
            {
                _icon.enabled = emptyIcon != null;
                if (emptyIcon != null) { _icon.sprite = _ui.Catalog.Get(emptyIcon); _icon.color = new Color(0f, 0f, 0f, 0.5f); }
                _glow.color = _wash.color = _gem.color = Color.clear;
                if (_style == SlotStyle.Cell) foreach (var b in _border) b.color = Color.clear;
                foreach (var s in _sockets) s.color = Color.clear;
                _stars.text = _badge.text = "";
                _lockBody.color = _lockLoop.color = _newBar.color = Color.clear;
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
            _wash.color = _style == SlotStyle.Cell ? new Color(rc.r, rc.g, rc.b, common ? 0.04f : 0.14f) : Color.clear;
            if (_style == SlotStyle.Cell)
                foreach (var b in _border) b.color = item.Rarity >= Rarity.Rare ? new Color(rc.r, rc.g, rc.b, 0.85f) : Color.clear;
            _gem.color = _style == SlotStyle.Socket ? (common ? new Color(0.55f, 0.55f, 0.55f, 1f) : rc) : Color.clear;
            for (int i = 0; i < 3; i++)
                _sockets[i].color = i >= item.Sockets ? Color.clear
                    : i < item.RuneSlots.Count ? new Color(0.66f, 0.77f, 1f, 1f) : new Color(0.25f, 0.25f, 0.3f, 1f);
            int stars = item.GreaterCount;
            _stars.text = stars <= 0 ? "" : stars == 1 ? "★" : "★" + stars;
            _badge.text = upgrade ? "▲" : "";
            _lockBody.color = item.Locked ? new Color(1f, 0.83f, 0.45f, 1f) : Color.clear;
            _lockLoop.color = item.Locked ? new Color(1f, 0.83f, 0.45f, 0.7f) : Color.clear;
            if (!_isNew) _newBar.color = Color.clear;
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

        /// <summary>The "new" bar breathes while the panel is open, in four steps a second so the canvas is not
        /// rebuilt every frame (review 11-21).</summary>
        public void Tick(float t)
        {
            if (!_isNew) return;
            float stepped = Mathf.Floor(t * 4f) / 4f;
            _newBar.color = new Color(1f, 0.9f, 0.6f, 0.45f + 0.45f * Mathf.Sin(stepped * 5f));
        }

        public bool Hovering => _hovering;
    }
}
