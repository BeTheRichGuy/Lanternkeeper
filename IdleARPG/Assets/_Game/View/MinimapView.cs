using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Automap in a box in the top-right corner, centred on the hero, on the same isometric diamond as the world: only what the lantern has
    /// lit is shown (a fog-of-war reveal around the hero), room edges bright like D2's automap lines, the boss room red,
    /// event and treasure rooms gold, the hero a lantern dot. Z toggles it (saved).
    /// </summary>
    public sealed class MinimapView
    {
        const int RevealRadius = 9;
        // A box in the top-right corner, centred on the hero; the map is clipped to it (HUD units, before UI scale).
        const float BoxW = 2.8f, BoxH = 1.7f, K = 0.085f;

        readonly SimWorld _world;
        readonly Transform _root, _frame;
        readonly SpriteRenderer _map, _hero, _back;
        Texture2D _tex;
        Color32[] _px;
        bool[] _seen;
        Floor _builtFor;
        bool _dirty;
        float _halfW, _halfH, _ui = 1f;
        public bool Visible { get; private set; }
        /// <summary>Clicked open: the same map, much bigger, in the middle of the screen (click again to shrink).</summary>
        public bool Large { get; private set; }

        public void ToggleLarge() { Large = !Large; Place(); }

        /// <summary>True when the screen point is on the map box.</summary>
        public bool Hit(Camera cam, Vector2 screen)
        {
            if (!_root.gameObject.activeInHierarchy) return false;
            var p = WorldPresenter.ScreenToWorld(cam, screen);
            var b = _back.bounds;
            return p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
        }

        static readonly Color32 None = new Color32(0, 0, 0, 0);
        static readonly Color32 FloorC = new Color32(105, 92, 76, 110);
        static readonly Color32 Edge = new Color32(214, 190, 140, 235);
        static readonly Color32 BossEdge = new Color32(230, 90, 70, 245);
        static readonly Color32 GoldEdge = new Color32(240, 200, 110, 245);

        public MinimapView(Transform camera, SimWorld world, SpriteCatalog catalog, Material material)
        {
            _world = world;
            _root = new GameObject("Minimap").transform;
            _root.SetParent(camera, false);
            const float dot = 0.03125f;
            _back = new GameObject("back").AddComponent<SpriteRenderer>();
            _back.transform.SetParent(_root, false);
            _back.sharedMaterial = material;
            _back.sprite = catalog.Get("ui/dot");
            _back.transform.localScale = new Vector3(BoxW / dot, BoxH / dot, 1f);
            _back.color = new Color(0.02f, 0.02f, 0.03f, 0.72f);
            _back.sortingOrder = 32039;
            // 재기획 §5.8: the map sits in a drawn frame instead of floating loose in the corner.
            // Drawn behind the map: only its border shows outside the clip box.
            UiFrame.Make(_root, catalog, material, catalog.Has("ui/ui2_window_iron") ? "ui/ui2_window_iron" : UiFrame.Tooltip, new Vector2(BoxW + 0.16f, BoxH + 0.16f), Vector3.zero, 32038);
            var mask = new GameObject("clip").AddComponent<SpriteMask>();
            mask.transform.SetParent(_root, false);
            mask.sprite = catalog.Get("ui/dot");
            mask.transform.localScale = _back.transform.localScale;
            mask.isCustomRangeActive = true;
            mask.frontSortingOrder = 32042;
            mask.backSortingOrder = 32039;
            _frame = new GameObject("iso").transform;
            _frame.SetParent(_root, false);
            // ToWorld(x, y) = ((x − y)/2, −(x + y)/4): a 45° turn, then (√2/2, −√2/4), then the map scale K.
            _frame.localScale = new Vector3(0.70710678f * K, -0.35355339f * K, 1f);
            _map = new GameObject("map").AddComponent<SpriteRenderer>();
            _map.transform.SetParent(_frame, false);
            _map.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _map.sharedMaterial = material;
            _map.sortingOrder = 32040;
            _map.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            _hero = new GameObject("hero").AddComponent<SpriteRenderer>();
            _hero.transform.SetParent(_map.transform, false);
            _hero.sharedMaterial = material;
            _hero.sprite = catalog.Get("ui/dot");
            _hero.color = new Color(1f, 0.8f, 0.45f);
            _hero.sortingOrder = 32041;
            _hero.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            try { Visible = PlayerPrefs.GetInt("minimap", 1) == 1; } catch { Visible = true; }
            _root.gameObject.SetActive(Visible);
        }

        bool _hidden;

        public void Toggle()
        {
            Visible = !Visible;
            PlayerPrefs.SetInt("minimap", Visible ? 1 : 0);
            if (!Visible && Large) { Large = false; Place(); } // hidden: back to the corner next time
            _root.gameObject.SetActive(Visible && !_hidden);
        }

        /// <summary>Hidden behind full-screen choices without touching the saved toggle.</summary>
        public void Hide(bool hidden)
        {
            _hidden = hidden;
            if (hidden && Large) { Large = false; Place(); }
            _root.gameObject.SetActive(Visible && !hidden);
        }

        public void Layout(float halfW, float halfH, float ui)
        {
            _halfW = halfW; _halfH = halfH; _ui = ui;
            Place();
        }

        void Place()
        {
            _back.color = new Color(0.02f, 0.02f, 0.03f, Large ? 0.82f : 0.45f); // the big map sits over the fight: darker behind it
            if (Large)
            {
                _root.localScale = Vector3.one * _ui * 2.6f;
                _root.localPosition = new Vector3(0f, 0.2f * _ui, 9.5f);
                return;
            }
            _root.localScale = Vector3.one * _ui;
            _root.localPosition = new Vector3(_halfW - (0.3f + BoxW / 2f) * _ui, _halfH - (0.3f + BoxH / 2f) * _ui, 9.5f);
        }

        void Build()
        {
            var f = _world.Floor;
            _builtFor = f;
            if (_tex == null || _tex.width != f.Width || _tex.height != f.Height)
            {
                if (_map.sprite != null) Object.Destroy(_map.sprite);
                if (_tex != null) Object.Destroy(_tex);
                _tex = new Texture2D(f.Width, f.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                _px = new Color32[f.Width * f.Height];
                _seen = new bool[f.Width * f.Height];
                _map.sprite = Sprite.Create(_tex, new Rect(0, 0, f.Width, f.Height), Vector2.zero, 1f);
            }
            System.Array.Clear(_seen, 0, _seen.Length);
            for (int i = 0; i < _px.Length; i++) _px[i] = None;
            _dirty = true;
        }

        Color32 CellColor(Floor f, int x, int y)
        {
            bool edge = !f.IsWalkable(x + 1, y) || !f.IsWalkable(x - 1, y) || !f.IsWalkable(x, y + 1) || !f.IsWalkable(x, y - 1);
            if (!edge) return FloorC;
            var room = f.RoomAt(new Vec2(x + 0.5f, y + 0.5f));
            if (room == null) return Edge;
            return room.Kind == RoomKind.Boss ? BossEdge : room.Kind == RoomKind.Event || room.Kind == RoomKind.Treasure ? GoldEdge : Edge;
        }

        /// <summary>Once per Sim tick: light the cells around the hero.</summary>
        public void OnTick()
        {
            var f = _world.Floor;
            if (f == null) return;
            if (f != _builtFor) Build();
            int hx = (int)_world.Hero.Pos.X, hy = (int)_world.Hero.Pos.Y;
            for (int y = hy - RevealRadius; y <= hy + RevealRadius; y++)
            for (int x = hx - RevealRadius; x <= hx + RevealRadius; x++)
            {
                if (x < 0 || y < 0 || x >= f.Width || y >= f.Height) continue;
                int i = y * f.Width + x;
                if (_seen[i] || (x - hx) * (x - hx) + (y - hy) * (y - hy) > RevealRadius * RevealRadius || !f.IsWalkable(x, y)) continue;
                _seen[i] = true;
                _px[i] = CellColor(f, x, y);
                _dirty = true;
            }
        }

        public void Render(Vec2 heroPos)
        {
            if (!Visible || _tex == null) return;
            if (_dirty) { _tex.SetPixels32(_px); _tex.Apply(false); _dirty = false; }
            // The dot lives in the map's pixel space (1 unit = 1 cell); 2.5 cells wide so it reads at this scale.
            _hero.transform.localPosition = new Vector3(heroPos.X, heroPos.Y, 0f);
            // Keep the hero in the middle of the box: shift the map by the hero's own (scaled) world position.
            var w = IsoProjection.ToWorld(heroPos);
            _frame.localPosition = new Vector3(-w.x * K, -w.y * K, 0f);
            _hero.transform.localScale = Vector3.one * (2.5f / (_hero.sprite != null ? _hero.sprite.bounds.size.x : 1f));
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            _hero.color = new Color(1f, 0.8f * pulse + 0.15f, 0.45f, 1f);
        }
    }
}
