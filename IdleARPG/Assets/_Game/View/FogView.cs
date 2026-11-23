using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Floor fog (docs/research/2026-11-21-modern-pixel-rendering.md §3.2 대기): two soft layers drifting across the floor
    /// at different speeds, drawn lit — so the fog only shows where a lantern or a prop light reaches it, and the air
    /// itself seems to catch the light. Each act has its own colour, density and drift.
    /// </summary>
    public sealed class FogView
    {
        const int Tex = 128;
        const float TileWorld = 6f; // one noise tile spans 6 world units (384 art px)
        const int Order = -30000 + 10; // over the floor and its decals, under ground effects and units

        readonly SpriteRenderer[] _layers = new SpriteRenderer[2];
        Color _color = Color.clear;
        Vector2 _speed1, _speed2;

        /// <summary>Per act (§3.2): fog colour, alpha of both layers together, drift speed of each layer (units/s).</summary>
        static readonly (Color c, float a, Vector2 s1, Vector2 s2)[] Acts =
        {
            (Hex(0xD8C8A8), 0.08f, new Vector2(0.05f, 0.01f), new Vector2(-0.03f, 0.012f)),
            (Hex(0xA8C8A0), 0.12f, new Vector2(0.02f, 0.006f), new Vector2(-0.012f, 0.004f)),
            (Hex(0xC89090), 0.07f, new Vector2(0.015f, 0.004f), new Vector2(-0.01f, 0.006f)),
            (Hex(0xC8E0F0), 0.12f, new Vector2(0.03f, -0.008f), new Vector2(-0.02f, 0.01f)),
            (Hex(0x403028), 0.10f, new Vector2(0.01f, 0.04f), new Vector2(-0.008f, 0.06f)), // the forge's smoke rises
            (Hex(0x8080B0), 0.06f, new Vector2(0.01f, 0.003f), new Vector2(-0.006f, 0.004f)),
        };

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        public FogView(Transform root, Material litMaterial)
        {
            var noise = MakeNoise();
            for (int i = 0; i < 2; i++)
            {
                var sr = new GameObject("fog " + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root, false);
                sr.sharedMaterial = litMaterial;
                sr.sprite = noise;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.sortingOrder = Order + i;
                _layers[i] = sr;
            }
        }

        /// <summary>Tileable value noise, two octaves, soft blobs: alpha carries the density, colour is white.</summary>
        static Sprite MakeNoise()
        {
            var rng = new System.Random(1123);
            float[] Grid(int n) { var g = new float[n * n]; for (int i = 0; i < g.Length; i++) g[i] = (float)rng.NextDouble(); return g; }
            float Sample(float[] g, int n, float u, float v)
            {
                float x = u * n, y = v * n;
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
                float At(int a, int b) => g[((b % n + n) % n) * n + ((a % n + n) % n)];
                return Mathf.Lerp(Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), fx), Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), fx), fy);
            }
            var g4 = Grid(4);
            var g8 = Grid(8);
            var tex = new Texture2D(Tex, Tex, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[Tex * Tex];
            for (int y = 0; y < Tex; y++)
                for (int x = 0; x < Tex; x++)
                {
                    float u = (x + 0.5f) / Tex, v = (y + 0.5f) / Tex;
                    float n = Sample(g4, 4, u, v) * 0.65f + Sample(g8, 8, u, v) * 0.35f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.8f, n));
                    px[y * Tex + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Tex, Tex), new Vector2(0.5f, 0.5f), Tex / TileWorld, 0, SpriteMeshType.FullRect);
        }

        public void SetAct(int act)
        {
            var a = Acts[Mathf.Clamp(act, 0, Acts.Length - 1)];
            _color = new Color(a.c.r, a.c.g, a.c.b, a.a * 0.5f); // half the total on each layer
            _speed1 = a.s1;
            _speed2 = a.s2;
        }

        /// <summary>Covers the view around <paramref name="center"/>; each layer slides by its drift, wrapped to one tile.</summary>
        public void Update(Vector3 center, Vector2 viewSize, float time)
        {
            var size = viewSize + new Vector2(TileWorld * 4f, TileWorld * 4f); // the grid snap and drift move a layer up to ~1.5 tiles
            for (int i = 0; i < 2; i++)
            {
                var sr = _layers[i];
                var drift = (i == 0 ? _speed1 : _speed2) * time;
                // Anchor to the tile grid so the pattern stays put in the world while the camera moves, then drift.
                float ox = Mathf.Repeat(drift.x, TileWorld), oy = Mathf.Repeat(drift.y, TileWorld);
                var snapped = new Vector3(Mathf.Floor(center.x / TileWorld) * TileWorld, Mathf.Floor(center.y / TileWorld) * TileWorld, 0f);
                sr.transform.localPosition = snapped + new Vector3(ox + i * 2.3f, oy + i * 1.7f, 0f);
                if (sr.size != size) sr.size = size;
                if (sr.color != _color) sr.color = _color;
            }
        }
    }
}
