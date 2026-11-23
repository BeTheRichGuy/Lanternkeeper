using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Real URP 2D lighting on a black background (희승, 2026-11-19: 전체화면이니 라이트를 써서 퀄리티 우선).
    /// A dim cold global light keeps the architecture faintly readable far away (a wide view should show more map),
    /// the hero's lantern is the warm key light, fire props light their corner of the room, and skills, chain pops
    /// and big drops flash briefly. Every local light is a sprite light squashed 2:1 so its pool lies flat on the
    /// isometric floor (point lights ignore transform scale and would stay circles).
    /// </summary>
    public sealed class LightRig
    {
        public const float Ground = 0.55f; // vertical squash of a light pool on the 2:1 floor
        // docs/research/act-visual-direction.md §3: the global light stays near-neutral (saturation <= 15%) and only its
        // strength changes per act; colour comes from local prop lights. The lantern is warm-white, not orange.
        static readonly Color Ambient = new Color(0.72f, 0.68f, 0.60f);
        static readonly Color LanternColor = new Color(1f, 0.92f, 0.81f);   // #FFEBCF

        // Per act (0 모래, 1 밀림, 2 핏덩이, 3 얼음, 4 용암, 5 심연): ambient colour, ambient strength, lantern strength.
        static readonly (Color c, float i, float lantern)[] ActLight =
        {
            (new Color(0.72f, 0.68f, 0.60f), 0.30f, 1.25f),  // #B8AE9A
            (new Color(0.56f, 0.63f, 0.54f), 0.22f, 1.70f),  // #8FA08A
            (new Color(0.66f, 0.60f, 0.61f), 0.26f, 1.90f),  // #A89A9C
            (new Color(0.60f, 0.67f, 0.73f), 0.20f, 1.70f),  // #9AAABB
            (new Color(0.64f, 0.61f, 0.60f), 0.14f, 1.60f),  // #A39C98
            (new Color(0.56f, 0.58f, 0.69f), 0.13f, 2.10f),  // #8E94B0
        };
        float _lanternBase = 1.5f;

        readonly Transform _root;
        readonly Sprite _falloff;
        readonly Light2D _lantern;
        readonly List<Prop> _props = new List<Prop>();
        int _propsUsed;
        readonly Flash[] _flashes = new Flash[24];
        sealed class Mover { public Light2D L; public Vector3 A, B; public float T, Travel, I, R; }
        readonly Mover[] _movers = new Mover[6];
        int _nextMover;
        int _nextFlash;

        sealed class Prop { public Light2D L; public SpriteRenderer Rays; public Color Color; public float I, R, Phase, Flicker; }
        sealed class Flash { public Light2D L; public float T, Life, I, R; }

        public LightRig(Transform root)
        {
            _root = new GameObject("Lights").transform;
            _root.SetParent(root, false);
            _falloff = MakeFalloff(128);

            var global = new GameObject("Ambient").AddComponent<Light2D>();
            global.transform.SetParent(_root, false);
            global.lightType = Light2D.LightType.Global;
            global.color = Ambient;
            _ambient = global;
            global.intensity = 0.3f;

            _lantern = NewLight("Lantern", LanternColor, 1.5f);
            for (int i = 0; i < _flashes.Length; i++)
            {
                var l = NewLight("Flash", Color.white, 0f);
                l.enabled = false;
                _flashes[i] = new Flash { L = l };
            }
            for (int i = 0; i < _movers.Length; i++)
            {
                var l = NewLight("Moving", Color.white, 0f);
                l.enabled = false;
                _movers[i] = new Mover { L = l };
            }

            // 빛의 파도: a ring of light that runs out across the map.
            _ring = MakeRing(128);
            for (int i = 0; i < _waves.Length; i++)
            {
                var l = new GameObject("Wave").AddComponent<Light2D>();
                l.transform.SetParent(_root, false);
                l.lightType = Light2D.LightType.Sprite;
                l.lightCookieSprite = _ring;
                l.enabled = false;
                _waves[i] = new WaveLight { L = l };
            }

            // 빛 산란: faint rays from the act's own fires (torches, braziers, lava), never round the hero — a halo on the
            // hero read as a saint (희승 11-22: "왜 내 캐릭터가 예수님처럼 빛나고 있는거지?").
            var glow = Resources.Load<Shader>("Shaders/AdditiveGlow");
            if (glow != null)
            {
                _glowMat = new Material(glow);
                _raySprite = MakeScatter(128, 7);
            }
        }

        Material _glowMat;
        Sprite _raySprite;

        readonly MaterialPropertyBlock _tintBlock = new MaterialPropertyBlock();
        static readonly int TintId = Shader.PropertyToID("_Tint");

        void Tint(SpriteRenderer sr, Color c)
        {
            sr.GetPropertyBlock(_tintBlock);
            _tintBlock.SetColor(TintId, c);
            sr.SetPropertyBlock(_tintBlock);
        }

        SpriteRenderer NewScatter(string name, Sprite sprite, Material mat)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(_root, false);
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            sr.sortingOrder = 30900; // over the units, under the spell effects and labels (FxView 31000+)
            return sr;
        }

        Light2D NewLight(string name, Color color, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light2D>();
            l.transform.SetParent(_root, false);
            l.lightType = Light2D.LightType.Sprite;
            l.lightCookieSprite = _falloff;
            l.color = color;
            l.intensity = intensity;
            return l;
        }

        /// <summary>White centre, smooth falloff to zero at the rim (a touch of plateau so the core reads as lit).</summary>
        static Sprite MakeFalloff(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float h = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - h) * (x + 0.5f - h) + (y + 0.5f - h) * (y + 0.5f - h)) / h;
                float v = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 1f, d));
                v = Mathf.Pow(v, 1.4f); // soft tail, broad bright core
                byte b = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(v));
                px[y * size + x] = new Color32(b, b, b, b);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            // PPU = size / 2 → the sprite is 2 units across, so transform scale = radius.
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f);
        }

        static void Shape(Light2D l, Vector3 world, float radius)
        {
            l.transform.localPosition = world;
            l.transform.localScale = new Vector3(radius, radius * Ground, 1f);
        }

        // ---------- per floor ----------

        public void ClearProps()
        {
            foreach (var p in _props) { p.L.enabled = false; if (p.Rays != null) p.Rays.enabled = false; }
            _propsUsed = 0;
        }

        /// <param name="flicker">0 = steady, 1 = lively fire.</param>
        public void AddProp(Vector3 world, Color color, float intensity, float radius, float flicker)
        {
            Prop p;
            if (_propsUsed < _props.Count) p = _props[_propsUsed];
            else { p = new Prop { L = NewLight("Prop", color, intensity) }; _props.Add(p); }
            _propsUsed++;
            p.L.enabled = true;
            p.L.color = color;
            p.I = intensity; p.R = radius; p.Flicker = flicker; p.Color = color;
            p.Phase = Mathf.Abs(world.x * 13.1f + world.y * 7.7f) % 10f;
            Shape(p.L, world, radius);
            if (_glowMat != null && flicker > 0f) // fires only: steady glows (crystals) keep no rays
            {
                if (p.Rays == null) p.Rays = NewScatter("Prop Rays", _raySprite, _glowMat);
                p.Rays.enabled = true;
                p.Rays.transform.localPosition = world;
                float s = Mathf.Clamp(radius * 0.4f, 1.2f, 2.6f);
                p.Rays.transform.localScale = new Vector3(s, s * 0.8f, 1f);
            }
            else if (p.Rays != null) p.Rays.enabled = false;
        }

        // ---------- transient ----------

        public void FlashAt(Vector3 world, Color color, float intensity, float radius, float life)
        {
            var f = _flashes[_nextFlash];
            _nextFlash = (_nextFlash + 1) % _flashes.Length;
            f.T = 0f; f.Life = life; f.I = intensity; f.R = radius;
            f.L.color = color;
            f.L.intensity = intensity;
            f.L.enabled = true;
            Shape(f.L, world, radius);
        }

        /// <summary>
        /// A light carried by a spell in flight (1123_pixel_vfx.md §3 layer 6: the bolt lights the floor it passes over),
        /// waiting <paramref name="delay"/> then running from <paramref name="a"/> to <paramref name="b"/> and going out.
        /// </summary>
        public void Moving(Vector3 a, Vector3 b, float delay, float travel, Color color, float intensity, float radius)
        {
            var m = _movers[_nextMover];
            _nextMover = (_nextMover + 1) % _movers.Length;
            m.A = a; m.B = b; m.T = -delay; m.Travel = Mathf.Max(0.05f, travel); m.I = intensity; m.R = radius;
            m.L.color = color;
            m.L.intensity = 0f;
            m.L.enabled = true;
            Shape(m.L, a, radius);
        }

        static readonly Color IgnitedColor = new Color(1f, 0.9f, 0.68f);

        /// <param name="ignite">0..1 eased 등불 점화 glow: the lantern burns brighter, whiter and much farther.</param>
        Light2D _ambient;

        /// <summary>막 II is colder and wetter: a teal ambient instead of the crypt's blue-grey.</summary>
        public void SetAct(int act)
        {
            var l = ActLight[Mathf.Clamp(act, 0, ActLight.Length - 1)];
            if (_ambient != null) { _ambient.color = l.c; _ambient.intensity = l.i; }
            _ambientBase = l.i;
            _lanternBase = l.lantern;
            _act = Mathf.Clamp(act, 0, ActLight.Length - 1);
        }

        // ---------- 빛의 파도 · 막별 빛의 행동 · 빛 산란 (기술 개발 트랙 1, 희승 11-22) ----------

        sealed class WaveLight { public Light2D L; public Vector3 At; public float T, Life, R, I; }
        readonly WaveLight[] _waves = new WaveLight[4];
        int _nextWave;
        readonly Sprite _ring;

        float _ambientBase = 0.3f, _ambientPulse;
        int _act;

        /// <summary>
        /// 빛의 파도: a ring of light runs out from <paramref name="world"/> to <paramref name="radius"/> over
        /// <paramref name="life"/> seconds, lighting the walls as it passes, while the whole map brightens by
        /// <paramref name="ambient"/> (0..1) and darkens back. The lantern's power shown on the map, not only on the hero.
        /// </summary>
        public void Wave(Vector3 world, Color color, float radius, float life, float intensity, float ambient)
        {
            var w = _waves[_nextWave];
            _nextWave = (_nextWave + 1) % _waves.Length;
            w.At = world; w.T = 0f; w.Life = life; w.R = radius; w.I = intensity;
            w.L.color = color;
            w.L.intensity = intensity;
            w.L.enabled = true;
            Shape(w.L, world, 0.5f);
            _ambientPulse = Mathf.Max(_ambientPulse, ambient);
        }

        /// <summary>How each act's fires behave (blind review 11-22: "막마다 빛의 행동 자체가 달라져야").</summary>
        float ActBehaviour(Prop p, float time)
        {
            float n = Mathf.PerlinNoise(time * 6f + p.Phase, p.Phase);
            switch (_act)
            {
                case 1: return 0.82f + 0.18f * Mathf.Sin(time * 1.3f + p.Phase);                       // 밀림: slow breath, bioluminescence
                case 2: return 0.7f + 0.65f * Heartbeat(time + p.Phase * 0.05f);                       // 핏덩이: a heartbeat
                case 3: return 0.92f + 0.08f * Mathf.PerlinNoise(time * 14f, p.Phase);                 // 얼음: a cold shimmer
                case 4: return (0.75f + 0.5f * n) * (Mathf.PerlinNoise(time * 11f, p.Phase + 5f) > 0.8f ? 1.7f : 1f); // 용암: sparks
                case 5: { float m = Mathf.PerlinNoise(time * 0.9f, p.Phase); return m > 0.63f ? 1.5f : 0.3f + 0.35f * m; } // 심연: irregular
                default: return 1f - p.Flicker * 0.25f + p.Flicker * 0.5f * n;                         // 사막: torch flicker
            }
        }

        /// <summary>Lub-dub at about 66 beats a minute: two quick swells, then rest.</summary>
        static float Heartbeat(float time)
        {
            float t = Mathf.Repeat(time * 1.1f, 1f);
            float a = Mathf.Exp(-t * 16f);
            float b = t > 0.24f ? 0.6f * Mathf.Exp(-(t - 0.24f) * 16f) : 0f;
            return a + b;
        }

        /// <summary>A soft ring (bright band near the rim, a faint wash inside): the cookie of a 빛의 파도.</summary>
        static Sprite MakeRing(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float h = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x + 0.5f - h) * (x + 0.5f - h) + (y + 0.5f - h) * (y + 0.5f - h)) / h;
                    float band = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.07f, 2f));
                    float wash = r < 0.86f ? 0.18f * r : 0f;
                    float v = Mathf.Clamp01(r > 1f ? 0f : band + wash);
                    byte b = (byte)Mathf.RoundToInt(255f * v);
                    px[y * size + x] = new Color32(b, b, b, b);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f);
        }

        /// <summary>Uneven soft rays (beams of lantern light caught in dust) round a bright core.</summary>
        static Sprite MakeScatter(int size, int seed)
        {
            var rng = new System.Random(seed);
            int rays = 13;
            var angle = new float[rays];
            var width = new float[rays];
            var reach = new float[rays];
            for (int i = 0; i < rays; i++)
            {
                angle[i] = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                width[i] = 0.05f + (float)rng.NextDouble() * 0.12f;
                reach[i] = 0.55f + (float)rng.NextDouble() * 0.45f;
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float h = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - h, dy = y + 0.5f - h;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / h;
                    float ang = Mathf.Atan2(dy, dx);
                    float v = 0f;
                    for (int i = 0; i < rays; i++)
                    {
                        float d = Mathf.DeltaAngle(ang * Mathf.Rad2Deg, angle[i] * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                        float across = Mathf.Exp(-(d * d) / (width[i] * width[i]));
                        float along = Mathf.Clamp01(1f - r / reach[i]);
                        v += across * along * along;
                    }
                    v = Mathf.Clamp01(v * 0.8f + Mathf.Pow(Mathf.Clamp01(1f - r), 3f) * 0.6f);
                    byte b = (byte)Mathf.RoundToInt(255f * v);
                    px[y * size + x] = new Color32(255, 255, 255, b);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f);
        }

        /// <summary>The lantern's colour (도전 과제 cosmetic, GDD §12.5); ignition still whitens it.</summary>
        public Color LanternTint = LanternColor;

        public void Update(Vector3 heroWorld, float dt, float time, float ignite = 0f)
        {
            // Lantern: a slow breath plus a faster flame jitter (hungrier while ignited).
            float breath = 0.94f + 0.06f * Mathf.PerlinNoise(time * (1.7f + ignite * 1.5f), 0.37f);
            float jitter = 0.97f + (0.03f + 0.04f * ignite) * Mathf.PerlinNoise(time * 9f, 3.1f);
            float beat = _act == 2 ? 0.93f + 0.14f * Heartbeat(time) : 1f; // 핏덩이 소굴: the lantern beats with the walls
            _lantern.intensity = Mathf.Lerp(_lanternBase, _lanternBase + 0.7f, ignite) * jitter * beat;
            _lantern.color = Color.Lerp(LanternTint, IgnitedColor, ignite);
            Shape(_lantern, heroWorld + new Vector3(0f, 0.35f, 0f), Mathf.Lerp(7.5f, 11.5f, ignite) * breath);

            for (int i = 0; i < _propsUsed; i++)
            {
                var p = _props[i];
                float n = Mathf.PerlinNoise(time * 6f + p.Phase, p.Phase);
                float act = ActBehaviour(p, time);
                p.L.intensity = p.I * act;
                float s = 1f + p.Flicker * 0.08f * (n - 0.5f);
                p.L.transform.localScale = new Vector3(p.R * s, p.R * s * Ground, 1f);
                if (p.Rays != null && p.Rays.enabled)
                {
                    // The fire's rays breathe with its flame (the act's own rhythm) and turn slowly.
                    p.Rays.transform.localEulerAngles = new Vector3(0f, 0f, time * 5f + p.Phase * 36f);
                    Tint(p.Rays, new Color(p.Color.r, p.Color.g, p.Color.b, 0.09f * Mathf.Clamp(act, 0f, 1.6f)));
                }
            }

            // The whole map breathes with a 빛의 파도, then settles back to the act's darkness.
            _ambientPulse = Mathf.Max(0f, _ambientPulse - dt * 0.7f);
            if (_ambient != null) _ambient.intensity = _ambientBase * (1f + 2.6f * _ambientPulse * _ambientPulse);
            foreach (var w in _waves)
            {
                if (!w.L.enabled) continue;
                w.T += dt;
                if (w.T >= w.Life) { w.L.enabled = false; continue; }
                float k = w.T / w.Life;
                float grow = 1f - (1f - k) * (1f - k) * (1f - k);
                w.L.intensity = w.I * Mathf.Pow(1f - k, 1.4f);
                Shape(w.L, w.At, Mathf.Max(0.5f, w.R * grow));
            }

            foreach (var f in _flashes)
            {
                if (!f.L.enabled) continue;
                f.T += dt;
                if (f.T >= f.Life) { f.L.enabled = false; continue; }
                float k = 1f - f.T / f.Life;
                f.L.intensity = f.I * k * k;
            }
            foreach (var m in _movers)
            {
                if (!m.L.enabled) continue;
                m.T += dt;
                if (m.T < 0f) continue;
                float k = m.T / m.Travel;
                if (k >= 1.25f) { m.L.enabled = false; continue; }
                m.L.intensity = m.I * (k <= 1f ? 1f : 1f - (k - 1f) / 0.25f);
                Shape(m.L, Vector3.Lerp(m.A, m.B, Mathf.Min(1f, k)), m.R);
            }
        }
    }
}
