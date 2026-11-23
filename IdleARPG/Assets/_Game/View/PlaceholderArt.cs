using System.Collections.Generic;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>Procedural stand-in sprites so every key renders before real art exists. PPU 64, point filtered.</summary>
    public static class PlaceholderArt
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string key, int index = 0)
        {
            string cacheKey = key.StartsWith("tile/") ? key + "#" + (index & 3) : key;
            // Domain reload is off: the cache outlives play sessions while Unity destroys runtime textures on exit,
            // so a cached sprite can be a destroyed object (== null). Rebuild it then.
            if (Cache.TryGetValue(cacheKey, out var s) && s != null) return s;
            s = Make(key, index);
            Cache[cacheKey] = s;
            return s;
        }

        static Sprite Make(string key, int index)
        {
            switch (key)
            {
                case "tile/floor": return Diamond(new Color32(58, 58, 66, 255), new Color32(40, 40, 48, 255), index);
                case "tile/wall": return Block(new Color32(70, 66, 74, 255), new Color32(44, 40, 50, 255));
                // Rings grow to whole rooms late in the game (area, copies): a fine 256 px ring keeps them a thin line
                // instead of a stair-stepped band of huge pixels. Still one world unit across.
                case "fx/ring": return Ring(128, 5, 256f);
                case "fx/disc": return Disc(16);
                case "ui/dot": return Square(2);
                case "ui/lantern": return Lantern();
                case "fx/beam": return Beam();
            }
            if (key.StartsWith("icon/")) return Gem(Tint(key));
            if (key.StartsWith("hero/")) return Figure(new Color32(96, 88, 84, 255), new Color32(255, 140, 50, 255), 40);
            if (key == "enemy/gravelord") return Figure(new Color32(120, 110, 140, 255), new Color32(170, 230, 255, 255), 56);
            if (key.StartsWith("enemy/")) return Figure(Tint(key), new Color32(200, 255, 200, 255), 30);
            return Square(8);
        }

        static Color32 Tint(string key)
        {
            int h = key.GetHashCode();
            return new Color32((byte)(80 + (h & 63)), (byte)(80 + ((h >> 6) & 63)), (byte)(70 + ((h >> 12) & 63)), 255);
        }

        static Texture2D Tex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color32[w * h];
            t.SetPixels32(clear);
            return t;
        }

        static Sprite Finish(Texture2D t, Vector2 pivot)
        {
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, IsoProjection.PPU);
        }

        static Sprite Diamond(Color32 top, Color32 edge, int seed)
        {
            var t = Tex(64, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - 32f) / 32f, dy = Mathf.Abs(y + 0.5f - 16f) / 16f;
                if (dx + dy > 1f) continue;
                bool rim = dx + dy > 0.92f;
                bool speck = ((x * 7 + y * 13 + seed * 31) % 17) == 0;
                t.SetPixel(x, y, rim ? edge : speck ? (Color)edge : top);
            }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        static Sprite Block(Color32 top, Color32 side)
        {
            // A cube: top diamond centred at y=48, two side faces hanging 32px below its lower edges.
            var t = Tex(64, 64);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float cx = Mathf.Abs(x + 0.5f - 32f) / 32f;
                if (cx > 1f) continue;
                float py = y + 0.5f;
                float topLow = 48f - 16f * (1f - cx), topHigh = 48f + 16f * (1f - cx);
                if (py >= topLow && py <= topHigh) { t.SetPixel(x, y, top); continue; }
                if (py < topLow && py >= topLow - 32f)
                    t.SetPixel(x, y, x < 32 ? (Color)side : (Color)side * 0.75f);
            }
            return Finish(t, new Vector2(0.5f, 0.25f));
        }

        static Sprite Figure(Color32 body, Color32 glow, int height)
        {
            var t = Tex(64, 64);
            int w = Mathf.Max(10, height / 3), x0 = 32 - w / 2;
            for (int y = 2; y < 2 + height; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                bool edge = x == x0 || x == x0 + w - 1 || y == 2 || y == 1 + height;
                t.SetPixel(x, y, edge ? new Color32(16, 14, 18, 255) : body);
            }
            t.SetPixel(32 - 2, height - 6, glow);
            t.SetPixel(32 + 2, height - 6, glow);
            return Finish(t, new Vector2(0.5f, 2f / 64f));
        }

        static Sprite Ring(int r, int thick, float ppu = 0f)
        {
            var t = Tex(r * 2, r * 2);
            for (int y = 0; y < r * 2; y++)
            for (int x = 0; x < r * 2; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                if (d <= r && d >= r - thick) t.SetPixel(x, y, Color.white);
            }
            if (ppu > 0f) { t.Apply(); return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu); }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        static Sprite Disc(int r)
        {
            var t = Tex(r * 2, r * 2);
            for (int y = 0; y < r * 2; y++)
            for (int x = 0; x < r * 2; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                if (d <= 1f) t.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - d));
            }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        static Sprite Square(int n)
        {
            var t = Tex(n, n);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) t.SetPixel(x, y, Color.white);
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Vertical light shaft for item drops: bright core, soft edges, fades toward the top.</summary>
        static Sprite Beam()
        {
            var t = Tex(4, 64);
            for (int y = 0; y < 64; y++)
            {
                float a = 1f - y / 64f;
                t.SetPixel(0, y, new Color(1f, 1f, 1f, a * 0.35f));
                t.SetPixel(1, y, new Color(1f, 1f, 1f, a));
                t.SetPixel(2, y, new Color(1f, 1f, 1f, a));
                t.SetPixel(3, y, new Color(1f, 1f, 1f, a * 0.35f));
            }
            return Finish(t, new Vector2(0.5f, 0f));
        }

        /// <summary>Stand-in item icon until the slot has PixelLab art: a faceted gem.</summary>
        static Sprite Gem(Color32 c)
        {
            var t = Tex(16, 16);
            var dark = new Color32((byte)(c.r / 2), (byte)(c.g / 2), (byte)(c.b / 2), 255);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d = Mathf.Abs(x + 0.5f - 8f) + Mathf.Abs(y + 0.5f - 8f);
                if (d > 7f) continue;
                t.SetPixel(x, y, d > 6f ? (Color)dark : x + y > 16 ? (Color)c : Color.Lerp(c, Color.white, 0.35f));
            }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        static Sprite Lantern()
        {
            var t = Tex(8, 10);
            var brass = new Color32(180, 130, 60, 255);
            var flame = new Color32(255, 190, 90, 255);
            for (int x = 2; x < 6; x++) { t.SetPixel(x, 0, brass); t.SetPixel(x, 8, brass); }
            for (int y = 1; y < 8; y++) { t.SetPixel(1, y, brass); t.SetPixel(6, y, brass); }
            for (int y = 2; y < 7; y++) for (int x = 2; x < 6; x++) t.SetPixel(x, y, flame);
            t.SetPixel(3, 9, brass); t.SetPixel(4, 9, brass);
            return Finish(t, new Vector2(0.5f, 0.5f));
        }
    }
}
