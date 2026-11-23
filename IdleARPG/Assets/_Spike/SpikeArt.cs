// 버리는 프로토타입(스파이크). 코드로 그린 임시 도트 스프라이트.
using UnityEngine;

namespace Spike
{
    public static class SpikeArt
    {
        public const float PPU = 32f;

        static Material _mat;
        public static Material Mat => _mat != null ? _mat
            : _mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(new Color32[w * h]);
            return t;
        }

        static Sprite Make(Texture2D t, Vector2 pivot)
        {
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, PPU, 0, SpriteMeshType.FullRect);
        }

        // 2:1 아이소 바닥 타일 32x16
        public static Sprite Tile(Color top, Color edge, int seed)
        {
            var t = NewTex(32, 16);
            var rng = new System.Random(seed);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Mathf.Abs(x - 15.5f) / 16f + Mathf.Abs(y - 7.5f) / 8f;
                    if (d > 1f) continue;
                    Color c = d > 0.86f ? edge : top;
                    if (rng.NextDouble() < 0.07) c = Color.Lerp(c, edge, 0.6f);
                    t.SetPixel(x, y, c);
                }
            return Make(t, new Vector2(0.5f, 0.5f));
        }

        // 머리 + 몸통 + 다리 실루엣. normal과 피격용 흰 실루엣을 함께 돌려준다.
        public static (Sprite normal, Sprite white) Figure(int w, int h, Color body, Color outline, Color eye)
        {
            var mask = new bool[w, h];
            float hx = (w - 1) / 2f, hy = h - 1 - w * 0.25f, hr = w * 0.24f;
            float bx = (w - 1) / 2f, by = h * 0.42f, brx = w * 0.40f, bry = h * 0.28f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool head = (x - hx) * (x - hx) + (y - hy) * (y - hy) <= hr * hr;
                    float ex = (x - bx) / brx, ey = (y - by) / bry;
                    bool torso = ex * ex + ey * ey <= 1f;
                    bool legs = y >= 1 && y < h * 0.22f && (Mathf.Abs(x - (w * 0.33f)) < 1.2f || Mathf.Abs(x - (w * 0.66f)) < 1.2f);
                    mask[x, y] = head || torso || legs;
                }

            var n = NewTex(w, h);
            var wt = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (mask[x, y])
                    {
                        float shade = y < by ? 0.8f : 1f;
                        n.SetPixel(x, y, new Color(body.r * shade, body.g * shade, body.b * shade, 1));
                        wt.SetPixel(x, y, Color.white);
                    }
                    else if (Neighbor(mask, x, y, w, h))
                    {
                        n.SetPixel(x, y, outline);
                        wt.SetPixel(x, y, Color.white);
                    }
                }
            int eyY = Mathf.RoundToInt(hy);
            n.SetPixel(Mathf.RoundToInt(hx - 1), eyY, eye);
            n.SetPixel(Mathf.RoundToInt(hx + 1), eyY, eye);
            var pivot = new Vector2(0.5f, 0f);
            return (Make(n, pivot), Make(wt, pivot));
        }

        static bool Neighbor(bool[,] m, int x, int y, int w, int h)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && m[nx, ny]) return true;
                }
            return false;
        }

        // 아이소 타원 고리 64x32 (화면에서 2x1 유닛)
        public static Sprite Ring()
        {
            var t = NewTex(64, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 64; x++)
                {
                    float ex = (x - 31.5f) / 32f, ey = (y - 15.5f) / 16f;
                    float d = Mathf.Sqrt(ex * ex + ey * ey);
                    if (d <= 1f && d >= 0.8f) t.SetPixel(x, y, Color.white);
                }
            return Make(t, new Vector2(0.5f, 0.5f));
        }

        // 부드러운 타원 원판 64x32
        public static Sprite Disc()
        {
            var t = NewTex(64, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 64; x++)
                {
                    float ex = (x - 31.5f) / 32f, ey = (y - 15.5f) / 16f;
                    float d = Mathf.Sqrt(ex * ex + ey * ey);
                    if (d <= 1f) t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1.2f - d)));
                }
            return Make(t, new Vector2(0.5f, 0.5f));
        }

        public static Sprite Pixel()
        {
            var t = NewTex(2, 2);
            t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            return Make(t, new Vector2(0.5f, 0.5f));
        }

        // 아이템 빛기둥 2x32, 위로 갈수록 투명
        public static Sprite Beam()
        {
            var t = NewTex(2, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 2; x++)
                    t.SetPixel(x, y, new Color(1, 1, 1, 1f - y / 32f));
            return Make(t, new Vector2(0.5f, 0f));
        }

        // 등불 시야: 가운데는 투명, 바깥은 어둠. 256x128 (8x4 유닛)
        public static Sprite Darkness()
        {
            var t = NewTex(256, 128);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 256; x++)
                {
                    float ex = (x - 127.5f) / 128f, ey = (y - 63.5f) / 64f;
                    float d = Mathf.Sqrt(ex * ex + ey * ey);
                    float a = Mathf.SmoothStep(0f, 0.9f, Mathf.InverseLerp(0.10f, 0.30f, d));
                    t.SetPixel(x, y, new Color(0.01f, 0.01f, 0.03f, a));
                }
            t.filterMode = FilterMode.Bilinear;
            return Make(t, new Vector2(0.5f, 0.5f));
        }
    }
}
