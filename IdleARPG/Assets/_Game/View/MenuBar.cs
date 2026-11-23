using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Bottom-right menu buttons (arpg-ui-ux-patterns.md #1): every panel opens with the mouse as well as its key, and
    /// each button shows its key so the keys are learned by using it.
    /// </summary>
    public sealed class MenuBar
    {
        public static readonly (string label, string key)[] Buttons =
        {
            ("소지품", "I"), ("스킬 배치", "R"), ("스킬 트리", "T"), ("야영지", "Y"), ("도감", "J"), ("기록", "U"), ("일지", "G"), ("설정", "Esc"), ("도움말", "H"),
        };

        readonly Transform _root;
        readonly SpriteRenderer[] _back = new SpriteRenderer[Buttons.Length];
        readonly TextMesh[] _text = new TextMesh[Buttons.Length];
        int _hover = -1;
        bool _hidden;

        /// <summary>The screen HUD's icon menu took over (재기획 §5.8): no buttons drawn, no hits.</summary>
        public bool Hidden { get => _hidden; set { _hidden = value; _root.gameObject.SetActive(!value); } }

        const float W = 0.62f, H = 0.3f, Gap = 0.05f;
        static readonly Color Idle = new Color(0.09f, 0.07f, 0.06f, 0.82f), Hover = new Color(0.32f, 0.22f, 0.12f, 0.95f);

        public MenuBar(Transform camera, SpriteCatalog catalog, Material material)
        {
            _root = new GameObject("Menu Bar").transform;
            _root.SetParent(camera, false);
            var font = UiFont.Body;
            for (int i = 0; i < Buttons.Length; i++)
            {
                var b = new GameObject("button").AddComponent<SpriteRenderer>();
                b.transform.SetParent(_root, false);
                b.sharedMaterial = material;
                b.sprite = catalog.Get("ui/dot");
                b.transform.localScale = new Vector3(W / 0.03125f, H / 0.03125f, 1f);
                b.color = Idle;
                b.sortingOrder = 32040;
                _back[i] = b;

                var go = new GameObject("label");
                go.transform.SetParent(_root, false);
                var tm = go.AddComponent<TextMesh>();
                tm.font = font;
                tm.fontSize = 48;
                tm.characterSize = 0.022f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.richText = true;
                tm.color = new Color(0.92f, 0.86f, 0.76f);
                tm.text = Buttons[i].label + " <color=#8a8178>" + Buttons[i].key + "</color>";
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                go.GetComponent<MeshRenderer>().sortingOrder = 32041;
                _text[i] = tm;
            }
        }

        /// <summary>A row along the bottom-right edge of the view (camera-local half extents).</summary>
        public void Layout(float halfW, float halfH, float ui)
        {
            _root.localPosition = new Vector3(halfW, -halfH, 9f);
            _root.localScale = Vector3.one * ui;
            float x = -0.12f - W / 2f;
            for (int i = Buttons.Length - 1; i >= 0; i--)
            {
                var p = new Vector3(x, 0.12f + H / 2f, 0f);
                _back[i].transform.localPosition = p;
                _text[i].transform.localPosition = p + new Vector3(0f, 0.005f, 0f);
                x -= W + Gap;
            }
        }

        /// <summary>The button under the screen point, or −1.</summary>
        public int Hit(Camera cam, Vector2 screen)
        {
            if (_hidden) return -1;
            var p = WorldPresenter.ScreenToWorld(cam, screen);
            for (int i = 0; i < _back.Length; i++)
            {
                var b = _back[i].bounds;
                if (p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y) return i;
            }
            return -1;
        }

        public void Hovered(int index)
        {
            if (index == _hover) return;
            _hover = index;
            for (int i = 0; i < _back.Length; i++) _back[i].color = i == index ? Hover : Idle;
        }
    }
}
