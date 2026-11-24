using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Screen-space GUI (2026-11-21 비주얼 개편 1단계): one overlay canvas laid out in 960×540 UI pixels and scaled by a
    /// whole number so the pixel frames and the Galmuri font stay crisp. Panels build themselves from these helpers:
    /// rects anchored top-left (y grows downward), sliced frames, icons and text.
    /// </summary>
    public sealed class UiKit
    {
        /// <summary>The panel area every window is laid out for (the Diablo II windows are up to 880×590).</summary>
        public const float RefWidth = 1280f, RefHeight = 720f;
        public readonly Canvas Canvas;
        public readonly RectTransform Root;
        public readonly SpriteCatalog Catalog;
        readonly CanvasScaler _scaler;

        public UiKit(SpriteCatalog catalog)
        {
            Catalog = catalog;
            var go = new GameObject("GUI", typeof(RectTransform));
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;
            Canvas.pixelPerfect = true;
            _scaler = go.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // The pixel art imports at 64 pixels per unit: matching it keeps 9-slice borders at their drawn size.
            _scaler.referencePixelsPerUnit = 64f;
            go.AddComponent<GraphicRaycaster>();
            Root = (RectTransform)go.transform;
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                // Added at run time the module can start without actions on some Input System versions: give it the
                // defaults explicitly so clicks and the wheel always reach the GUI (review 11-21).
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            UpdateScale();
        }

        /// <summary>
        /// Scale in half steps from the window size: 720p → ×1, 1080p → ×1.5, 1440p → ×2, 2160p → ×3, so the panel area
        /// is never under 1280×720. 점검 11-22: whole steps from 960×540 gave a 1080p full screen ×2 and a 960×540 area,
        /// and the inventory and character windows (590 tall) ran off it.
        /// </summary>
        public float Scale => _scaler.scaleFactor;

        public void UpdateScale()
        {
            float s = Mathf.Max(1f, Mathf.Floor(2f * Mathf.Min(Screen.height / RefHeight, Screen.width / RefWidth)) / 2f);
            if (!Mathf.Approximately(_scaler.scaleFactor, s)) _scaler.scaleFactor = s;
        }

        /// <summary>Raised by every GUI button press (GameBootstrap plays the click sound).</summary>
        public static Action Clicked;

        /// <summary>True while the pointer is over any GUI element (the world ignores clicks there).</summary>
        public static bool PointerOverGui => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>The panel area in UI pixels (the canvas size divided by the scale).</summary>
        public Vector2 Size => Root.rect.size;

        // ---------- builders ----------

        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A rect centred in its parent.</summary>
        public static RectTransform Centered(Transform parent, string name, float w, float h)
        {
            var rt = Rect(parent, name, 0f, 0f, w, h);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public Image Sprite(Transform parent, string key, float x, float y, float w, float h, bool sliced = false)
        {
            var img = Rect(parent, key, x, y, w, h).gameObject.AddComponent<Image>();
            img.sprite = Catalog.Get(key);
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.preserveAspect = !sliced;
            img.raycastTarget = false;
            return img;
        }

        public static Image Fill(Transform parent, string name, float x, float y, float w, float h, Color c)
        {
            var img = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Galmuri text. <paramref name="size"/> 12 is body, 16 the bold title size.</summary>
        public static Text Label(Transform parent, string text, float x, float y, float w, float h, int size = 12,
            TextAnchor align = TextAnchor.UpperLeft, Color? color = null, bool title = false)
        {
            var t = Rect(parent, "text", x, y, w, h).gameObject.AddComponent<Text>();
            t.font = title ? UiFont.Title : UiFont.Body;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Ink;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.05f;
            t.raycastTarget = false;
            t.text = text;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        /// <summary>A framed window: sliced ornate frame, dark body, title, close X (calls <paramref name="onClose"/>).</summary>
        /// <param name="frameKey">A drawn 9-slice frame (e.g. ui/ui2_window_iron); the ornate stone frame if null or missing.</param>
        public const string BoneFrame = "ui/ui4_window_bone";

        /// <summary>Where the top-centre HUD (level, ETA, the next goal) ends, in reference pixels.</summary>
        public const float TopHudBottom = 58f; // the 다음: line ends at 44; the frames' spikes stand ~14 px over the window

        /// <summary>
        /// How far a window of height <paramref name="h"/> sits above the centre: as in Diablo II the bottom panel stays in
        /// view, so a tall window rises until its bottom clears the bone bar (y 600 of 720) — but never over the top-centre
        /// HUD: a newcomer lost the 다음: line under every open window (loop 1). A tall one gives up the bar first.
        /// </summary>
        public static float Lift(float h)
        {
            float top = RefHeight / 2f - h / 2f, bottom = RefHeight / 2f + h / 2f;
            // 592: the bone frame hangs 8 px under the body, and that edge too stays off the bar (critique 8 P0).
            return Mathf.Max(0f, Mathf.Min(bottom - 592f, top - TopHudBottom)); // never over the level and 다음: lines (loop 1 P0-6)
        }

        public RectTransform Window(string name, float w, float h, string title, Action onClose, out Text titleText, string frameKey = null)
        {
            var win = Centered(Root, name, w, h);
            win.anchoredPosition = new Vector2(0f, Lift(h));
            var body = win.gameObject.AddComponent<Image>();
            body.color = new Color(0.035f, 0.032f, 0.045f, 1f); // opaque: HDR-bright damage numbers showed through even 2%
            body.raycastTarget = true; // the window swallows clicks meant for the world under it
            // 희승 11-22 (시안 7): every window in the bone-and-wax frame once it is drawn; the older frames stay as fallbacks.
            string key = Catalog.Has(BoneFrame) ? BoneFrame : frameKey != null && Catalog.Has(frameKey) ? frameKey : UiFrame.Window;
            bool bone = key == BoneFrame;
            // The bone frame's wax hangs from its top edge: it sits higher, so the drips fall over the window's rim rather
            // than over the title, and its edges repeat (stretched, the drips smeared into blocks).
            var frame = bone ? Sprite(win, key, -8f, -16f, w + 16f, h + 24f, sliced: true) : Sprite(win, key, -4f, -4f, w + 8f, h + 8f, sliced: true);
            frame.preserveAspect = false;
            if (bone) { frame.type = Image.Type.Tiled; frame.fillCenter = false; } // the art's inside is not quite empty
            Fill(win, "title band", 4f, 4f, w - 8f, 26f, new Color(1f, 0.85f, 0.55f, 0.06f));
            // Inside its band (critique 7 P0: the 고운바탕 title's taller glyphs ran past the band into the body).
            titleText = Label(win, title, 12f, 7f, w - 60f, 22f, 16, TextAnchor.UpperLeft, Gold, title: true);
            var close = Button(win, "×", w - 28f, 5f, 20f, 18f, onClose);
            close.GetComponentInChildren<Text>().fontSize = 12;
            return win;
        }

        /// <summary>
        /// A flat button: dark body, 1px bronze edge, hover lights it, press darkens it. The drawn ornate button art
        /// (18px end caps) only fits buttons 32px tall or more; under that its caps cover the label.
        /// </summary>
        public Button Button(Transform parent, string text, float x, float y, float w, float h, Action onClick)
        {
            var rt = Rect(parent, "button " + text, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            bool ornate = h >= 32f;
            if (ornate)
            {
                img.sprite = Catalog.Get("ui/ui_button_normal");
                img.type = Image.Type.Sliced;
            }
            else
            {
                img.color = Color.white; // tinted by the button's colour states below
                var edge = new Color(0.62f, 0.5f, 0.34f, 0.55f);
                Fill(rt, "edge", 0f, 0f, w, 1f, edge);
                Fill(rt, "edge", 0f, h - 1f, w, 1f, edge);
                Fill(rt, "edge", 0f, 0f, 1f, h, edge);
                Fill(rt, "edge", w - 1f, 0f, 1f, h, edge);
            }
            var b = rt.gameObject.AddComponent<Button>();
            if (ornate)
            {
                b.transition = Selectable.Transition.SpriteSwap;
                var st = b.spriteState;
                st.pressedSprite = Catalog.Get("ui/ui_button_pressed");
                st.highlightedSprite = Catalog.Get("ui/ui_button_normal");
                b.spriteState = st;
            }
            else
            {
                b.transition = Selectable.Transition.ColorTint;
                var cb = b.colors;
                cb.normalColor = new Color(0.09f, 0.08f, 0.075f, 0.95f);
                cb.highlightedColor = new Color(0.2f, 0.16f, 0.11f, 0.98f);
                cb.selectedColor = cb.normalColor;
                cb.pressedColor = new Color(0.05f, 0.045f, 0.04f, 1f);
                cb.fadeDuration = 0.06f;
                b.colors = cb;
            }
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => Clicked?.Invoke()); // the GUI's click sound
            if (onClick != null) b.onClick.AddListener(() => onClick());
            Label(rt, text, 0f, 0f, w, h, 12, TextAnchor.MiddleCenter);
            return b;
        }

        // ---------- palette ----------
        public static readonly Color Ink = new Color(0.9f, 0.87f, 0.8f);
        public static readonly Color Dim = new Color(0.58f, 0.55f, 0.5f);
        public static readonly Color Gold = new Color(1f, 0.83f, 0.5f);
        public static readonly Color Good = new Color(0.45f, 0.95f, 0.5f);
        public static readonly Color Bad = new Color(1f, 0.42f, 0.38f);
        public static readonly Color Line = new Color(1f, 0.85f, 0.6f, 0.16f);

        // ---------- procedural pixel shapes (stand-ins until drawn frames exist) ----------

        static readonly System.Collections.Generic.Dictionary<string, UnityEngine.Sprite> _shapes =
            new System.Collections.Generic.Dictionary<string, UnityEngine.Sprite>();

        /// <summary>A soft radial glow (bright centre fading to nothing at the edge, squared falloff): light pools and
        /// rarity glows. A flat disc stretched big read as a brown coin.</summary>
        public static UnityEngine.Sprite Glow(UiKit ui, int size = 64) =>
            Shape("glow" + size, size, (x, y, c) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                return a * a;
            }, FilterMode.Bilinear);

        /// <summary>A white disc of <paramref name="size"/> px; <paramref name="ring"/> &gt; 0 hollows it to a ring that
        /// thick, with a faint fill inside. Tinted by the Image colour.</summary>
        public static UnityEngine.Sprite Disc(UiKit ui, int size, int ring) =>
            Shape("disc" + size + "_" + ring, size, (x, y, c) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                if (d > c) return 0f;
                if (ring <= 0) return 1f;
                return d > c - ring ? 1f : 0.18f;
            });

        public static UnityEngine.Sprite Diamond(UiKit ui, int size) =>
            Shape("diamond" + size, size, (x, y, c) => Mathf.Abs(x + 0.5f - c) + Mathf.Abs(y + 0.5f - c) <= c ? 1f : 0f);

        /// <summary>A four-point star (keystones).</summary>
        public static UnityEngine.Sprite Star(UiKit ui, int size) =>
            Shape("star" + size, size, (x, y, c) =>
            {
                float dx = Mathf.Abs(x + 0.5f - c) / c, dy = Mathf.Abs(y + 0.5f - c) / c;
                // A diamond pinched in on its sides: |x|^0.5 + |y|^0.5 <= 1.
                return Mathf.Sqrt(dx) + Mathf.Sqrt(dy) <= 1f ? 1f : 0f;
            });

        static UnityEngine.Sprite Shape(string key, int size, Func<int, int, float, float> alpha, FilterMode filter = FilterMode.Point)
        {
            if (_shapes.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
            float c = size / 2f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * alpha(x, y, c)));
            tex.SetPixels32(px);
            tex.Apply();
            s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            _shapes[key] = s;
            return s;
        }
    }

    /// <summary>Pointer callbacks for a GUI element without writing a MonoBehaviour per use.</summary>
    public sealed class UiPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action Enter, Exit, LeftClick, RightClick;
        public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) LeftClick?.Invoke();
            else if (e.button == PointerEventData.InputButton.Right) RightClick?.Invoke();
        }
    }
}
