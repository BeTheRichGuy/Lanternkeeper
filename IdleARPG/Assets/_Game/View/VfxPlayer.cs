using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Plays PixelLab VFX game packs (imported as "vfx/&lt;id&gt;/front|behind|whole" clips) at sim events.
    /// Front layers sort just in front of a unit standing on the spot, behind layers just behind it.
    /// A per-tick budget keeps mass fights readable (spec §11: aggregate instead of stacking dozens of bursts).
    /// </summary>
    public sealed class VfxPlayer
    {
        sealed class Play
        {
            public SpriteRenderer Sr;
            public Sprite[] Frames;
            public float Fps, T, Until;
            public bool Loop, FollowHero;
            /// <summary>재기획 §4.1 L0: lies on the floor under every unit and settles to a faint alpha.</summary>
            public bool Ground;
            /// <summary>재기획 §4.1 L2: drawn over units, so it may stay bright only for its first quarter second.</summary>
            public bool FrontDecay;
            public Color Base;
            public int Bias;
            public Vector3 A, B;
            public float Travel;
            /// <summary>Hierarchy (audit 11-24): which sheet this is and where, so repeats on one spot can step back.</summary>
            public string Id;
            public Vector2 At;
            /// <summary>A wide sheet over the units: when its biggest frame shows, and how faint it settles after it.</summary>
            public float PeakAt, Settle = 1f;
            /// <summary>The same frames drawn again as pure light for the few frames round the peak (the bloom catches it).</summary>
            public SpriteRenderer Glow;
            public Color GlowTint;
        }

        readonly Transform _root;
        readonly SpriteCatalog _catalog;
        readonly Material _material;
        /// <summary>The sheet material (HDR hot core), for sheets drawn outside the player (status loops on units).</summary>
        public Material Material => _material;
        readonly List<Play> _live = new List<Play>();
        readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        int _spentThisTick;
        public int BudgetPerTick = 30; // copies (투사체·분신) and echoes multiply effects: room for them

        public VfxPlayer(Transform root, SpriteCatalog catalog, Material material)
        {
            _root = new GameObject("VFX").transform;
            _root.SetParent(root, false);
            _catalog = catalog;
            // 픽셀 VFX (희승 11-23): the sheets' near-white cores go past 1 so the bloom lights them (FxSprite).
            var fx = Resources.Load<Shader>("Shaders/FxSprite");
            _material = fx != null ? new Material(fx) : material;
        }

        public bool Has(string id) => _catalog.Has($"vfx/{id}/front") || _catalog.Has($"vfx/{id}/whole");

        public void BeginTick() { _spentThisTick = 0; _tickIds.Clear(); }

        // ---------- hierarchy (audit 11-24: 159 sheets at once, a 10-unit sheet opaque over every unit) ----------
        readonly Dictionary<string, int> _tickIds = new Dictionary<string, int>();
        readonly Dictionary<Sprite[], float> _peaks = new Dictionary<Sprite[], float>();
        readonly Stack<SpriteRenderer> _glowPool = new Stack<SpriteRenderer>();
        readonly MaterialPropertyBlock _glowBlock = new MaterialPropertyBlock();
        static readonly int TintId = Shader.PropertyToID("_Tint");
        Material _glowMat;
        /// <summary>A sheet this wide (world units) covers a pack: it gets the impact glow, then steps back.</summary>
        const float Wide = 2.5f;
        const float GlowLen = 0.2f;

        /// <summary>
        /// Seconds into the clip of the frame that covers most (the blow): the first frame whose sprite mesh (cut tight
        /// round the drawn pixels) reaches 90% of the largest. Sheets with no tight mesh: a third of the way in.
        /// </summary>
        float PeakTime(Sprite[] frames, float fps)
        {
            if (_peaks.TryGetValue(frames, out float at)) return at;
            var area = new float[frames.Length];
            float max = 0f, min = float.MaxValue;
            for (int i = 0; i < frames.Length; i++)
            {
                var v = frames[i].vertices;
                var tri = frames[i].triangles;
                float a = 0f;
                for (int k = 0; k + 2 < tri.Length; k += 3)
                {
                    Vector2 p0 = v[tri[k]], e1 = v[tri[k + 1]] - p0, e2 = v[tri[k + 2]] - p0;
                    a += Mathf.Abs(e1.x * e2.y - e1.y * e2.x) * 0.5f;
                }
                area[i] = a;
                max = Mathf.Max(max, a); min = Mathf.Min(min, a);
            }
            int peak = Mathf.RoundToInt(frames.Length * 0.35f);
            if (max - min > max * 0.05f)
                for (int i = 0; i < frames.Length; i++) if (area[i] >= max * 0.9f) { peak = i; break; }
            return _peaks[frames] = peak / Mathf.Max(1f, fps);
        }

        /// <summary>Live plays of the same sheet that began within the last half second on this spot.</summary>
        int Repeats(string id, Vector2 at, float reach)
        {
            int n = 0;
            foreach (var p in _live)
                if (p.Id == id && p.T < 0.5f && (p.At - at).sqrMagnitude < reach * reach) n++;
            return n;
        }

        SpriteRenderer NewGlow()
        {
            if (_glowPool.Count > 0) return _glowPool.Pop();
            if (_glowMat == null)
            {
                var shader = Resources.Load<Shader>("Shaders/AdditiveGlow");
                if (shader == null) return null;
                _glowMat = new Material(shader);
            }
            var sr = new GameObject("vfx glow").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(_root, false);
            sr.sharedMaterial = _glowMat;
            return sr;
        }

        void Release(Play p)
        {
            p.Sr.enabled = true; // a delayed leg that never showed must not go back to the pool hidden
            p.Sr.gameObject.SetActive(false);
            _pool.Push(p.Sr);
            if (p.Glow != null) { p.Glow.gameObject.SetActive(false); _glowPool.Push(p.Glow); p.Glow = null; }
        }

        /// <summary>
        /// Play effect <paramref name="id"/> anchored on grid point <paramref name="cell"/>.
        /// <paramref name="important"/> effects (ultimates, grand resonance, evolutions) ignore the budget.
        /// </summary>
        /// <summary>Just above the floor, its shadows and rings (WorldView.FloorOrder + 50/60): under every unit.</summary>
        public const int GroundOrder = -30000 + 70;

        /// <param name="ground">A field or aura: drawn under the units and faded to 0.35 after 0.3 s (재기획 §4.1 L0).</param>
        /// <summary>
        /// 재기획 §4.6: one material per element that redraws a sheet on that element's colour ramp (dark → mid → light), for
        /// skills a glyph converted — they show their new element, not the colour their sheet was painted in.
        /// </summary>
        static readonly (Color dark, Color mid, Color light)[] Ramps =
        {
            (Color.black, Color.gray, Color.white),                                                          // None (unused)
            (new Color(0.35f, 0.07f, 0.02f), new Color(1f, 0.45f, 0.12f), new Color(1f, 0.95f, 0.72f)),      // 잿불
            (new Color(0.04f, 0.14f, 0.32f), new Color(0.4f, 0.74f, 1f), new Color(0.9f, 0.98f, 1f)),        // 서리
            (new Color(0.24f, 0.18f, 0.02f), new Color(1f, 0.85f, 0.22f), new Color(1f, 1f, 0.86f)),         // 번개
            (new Color(0.07f, 0.16f, 0.04f), new Color(0.45f, 0.74f, 0.24f), new Color(0.85f, 1f, 0.62f)),   // 부패
            (new Color(0.1f, 0.03f, 0.2f), new Color(0.55f, 0.3f, 0.9f), new Color(0.92f, 0.82f, 1f)),       // 공허
        };
        readonly Material[] _rampMats = new Material[6];

        Material RampMaterial(Element e)
        {
            int i = Mathf.Clamp((int)e, 1, Ramps.Length - 1);
            if (_rampMats[i] != null) return _rampMats[i];
            var shader = Resources.Load<Shader>("Shaders/ElementRamp");
            if (shader == null) return _material;
            var m = new Material(shader);
            m.SetColor("_Dark", Ramps[i].dark);
            m.SetColor("_Mid", Ramps[i].mid);
            m.SetColor("_Light", Ramps[i].light);
            // The same five tones as the procedural pixel FX (PixelRamp), stepped: one palette across both.
            var five = PixelRamp.Of(e);
            for (int k = 0; k < 5; k++) m.SetColor("_C" + k, five.C[k]);
            return _rampMats[i] = m;
        }

        public bool Spawn(string id, Vec2 cell, float scale = 1f, Color? tint = null, float rotationDeg = 0f, bool flipX = false,
            float holdSeconds = 0f, bool important = false, Vector2 stretch = default, bool followHero = false, bool ground = false,
            Element recolor = Element.None)
        {
            if (!important && _spentThisTick >= BudgetPerTick) return false;
            var world = IsoProjection.ToWorld(cell);
            // Hierarchy: the sheet's drawn width decides how much room it may take.
            float alphaMul = 1f, width = 0f;
            foreach (var sizeLayer in new[] { "front", "whole", "behind" })
                if (_catalog.TryGetClip($"vfx/{id}/{sizeLayer}", out var sized, out _, out _)) { width = sized[0].bounds.size.x; break; }
            // One pixel density on screen: a sheet is drawn at a whole multiple of its art, like the units (1.5x put 3-px
            // texels beside their 2-px ones, 0.5x put 1-px sparks on them); a sheet already wide is never doubled.
            if (stretch == default) scale = width >= Wide ? 1f : Mathf.Max(1f, Mathf.Round(scale - 0.01f));
            float drawn = width * scale;
            bool wide = drawn >= Wide && stretch == default;
            var at2 = new Vector2(world.x, world.y);
            if (holdSeconds <= 0f && stretch == default)
            {
                _tickIds.TryGetValue(id, out int sameTick);
                if (sameTick >= (wide ? 2 : important ? 12 : 8)) return false;
                _tickIds[id] = sameTick + 1;
                // The first on a spot is the blow; a second within half a second is fainter, a third is not drawn.
                // Wide sheets keep to this even when "important": delayed blasts and evolved skills all come in as
                // important, and five ice fields over one pack made a wall (round 1 capture 040355).
                int again = Repeats(id, at2, Mathf.Max(0.6f, drawn * 0.5f));
                if (again >= 2 && (wide || !important)) return false;
                if (again >= 1) alphaMul = important && !wide ? 0.75f : 0.6f;
            }
            bool any = false;
            foreach (var (layer, bias) in new[] { ("behind", -2), ("front", 30), ("whole", 30) })
            {
                if (!_catalog.TryGetClip($"vfx/{id}/{layer}", out var frames, out float fps, out bool loop)) continue;
                var sr = _pool.Count > 0 ? _pool.Pop() : NewRenderer();
                sr.gameObject.SetActive(true);
                sr.sprite = frames[0];
                sr.sharedMaterial = recolor == Element.None ? _material : RampMaterial(recolor); // pooled: always set
                var tc = tint ?? Color.white;
                if (!important) tc.a *= GameSettings.FxAlpha; // 설정: 스킬 효과 강도
                tc.a *= alphaMul;
                if (ground) tc.a *= 0.75f; // a field is floor, never a lit slab: it starts under full strength and settles
                sr.color = tc;
                sr.flipX = flipX;
                sr.sortingOrder = ground ? GroundOrder : IsoProjection.SortOrder(world, bias);
                var t = sr.transform;
                t.localPosition = world;
                t.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);
                t.localScale = stretch == default ? new Vector3(scale, scale, 1f) : new Vector3(stretch.x, stretch.y, 1f);
                float length = frames.Length / Mathf.Max(1f, fps);
                bool frontDecay = !ground && !important && holdSeconds <= 0f && layer != "behind" && length > 0.25f;
                var play = new Play { Sr = sr, Frames = frames, Fps = fps, Loop = loop || holdSeconds > 0f, Until = holdSeconds > 0f ? holdSeconds : length, Base = sr.color,
                    FollowHero = followHero, Bias = bias, Ground = ground, FrontDecay = frontDecay && !wide, Id = id, At = at2 };
                // A wide sheet over the units: full for its blow, lit by a pass of pure light round it, then faint enough
                // that the pack under it reads (D2 draws its spells as light over the monsters, not paint).
                if (wide && !ground && holdSeconds <= 0f && layer != "behind")
                {
                    play.PeakAt = PeakTime(frames, fps);
                    play.Settle = important ? 0.6f : 0.45f;
                    play.Glow = NewGlow();
                    if (play.Glow != null)
                    {
                        var g = play.Glow;
                        g.gameObject.SetActive(true);
                        g.enabled = false;
                        g.flipX = flipX;
                        g.sortingOrder = sr.sortingOrder + 1;
                        g.transform.localPosition = t.localPosition;
                        g.transform.localRotation = t.localRotation;
                        g.transform.localScale = t.localScale;
                        var gt = recolor == Element.None ? Color.white : Ramps[Mathf.Clamp((int)recolor, 1, Ramps.Length - 1)].mid;
                        play.GlowTint = new Color(gt.r * tc.r, gt.g * tc.g, gt.b * tc.b, tc.a);
                    }
                }
                _live.Add(play);
                any = true;
            }
            if (any && !important) _spentThisTick++;
            return any;
        }

        /// <summary>A projectile (drawn pointing +x) flying from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public bool Projectile(string id, Vec2 from, Vec2 to, float seconds, Color? tint = null, float delay = 0f, Element recolor = Element.None)
        {
            if (!_catalog.TryGetClip($"vfx/{id}/whole", out var frames, out float fps, out _) &&
                !_catalog.TryGetClip($"vfx/{id}/front", out frames, out fps, out _)) return false;
            if (_spentThisTick >= BudgetPerTick) return false;
            var a = IsoProjection.ToWorld(from) + new Vector3(0f, 0.35f, 0f); // chest height
            var b = IsoProjection.ToWorld(to) + new Vector3(0f, 0.35f, 0f);
            var d = b - a;
            var sr = _pool.Count > 0 ? _pool.Pop() : NewRenderer();
            sr.gameObject.SetActive(true);
            sr.sprite = frames[0];
            sr.sharedMaterial = recolor == Element.None ? _material : RampMaterial(recolor); // pooled: always set
            sr.color = tint ?? Color.white;
            sr.flipX = false;
            sr.transform.localPosition = a;
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            sr.transform.localScale = Vector3.one;
            sr.sortingOrder = IsoProjection.SortOrder(a, 40);
            // A delayed leg (a ricochet after a bounce) waits hidden at its start until its turn.
            sr.enabled = delay <= 0f;
            _live.Add(new Play { Sr = sr, Frames = frames, Fps = fps, Loop = true, Until = seconds, Base = sr.color, Bias = 40, A = a, B = b, Travel = seconds, T = -delay });
            _spentThisTick++;
            return true;
        }

        /// <summary>A line effect (drawn along +x) stretched from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public bool Line(string id, Vec2 from, Vec2 to, Color? tint = null)
        {
            if (!_catalog.TryGetClip($"vfx/{id}/whole", out var frames, out _, out _) &&
                !_catalog.TryGetClip($"vfx/{id}/front", out frames, out _, out _)) return false;
            var a = IsoProjection.ToWorld(from);
            var b = IsoProjection.ToWorld(to);
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return false;
            float spriteLen = Mathf.Max(0.01f, frames[0].bounds.size.x);
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            // Anchor sits at the line's start (left edge). A line much longer than its sheet is laid as copies end to end
            // at an even pixel scale (11-23 review: one sheet stretched 3x along x had smeared, uneven pixels).
            int copies = Mathf.Max(1, Mathf.RoundToInt(len / spriteLen));
            if (copies == 1) return Spawn(id, from, 1f, tint, angle, false, 0f, false, new Vector2(len / spriteLen, 1f));
            int n = WorldPresenter.Whole;
            float sx = Mathf.Max(1, Mathf.Round(len / copies / spriteLen * n)) / n;
            var step = (to - from) * (1f / copies);
            bool any = false;
            for (int i = 0; i < copies; i++)
                any |= Spawn(id, new Vec2(from.X + step.X * i, from.Y + step.Y * i), 1f, tint, angle, false, 0f, i > 0, new Vector2(sx, 1f));
            return any;
        }

        SpriteRenderer NewRenderer()
        {
            var sr = new GameObject("vfx").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(_root, false);
            sr.sharedMaterial = _material;
            return sr;
        }

        public void Render(float dt, Vector3 heroWorld)
        {
            // Fields of the same drawing share one field's worth of alpha: four rot clouds on one spot read as one cloud,
            // not an opaque green slab (11-21 act III / VI screenshots).
            _groundCount.Clear();
            foreach (var g in _live)
                if (g.Ground && g.T >= 0f) _groundCount[g.Frames] = _groundCount.TryGetValue(g.Frames, out var c0) ? c0 + 1 : 1;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.T += dt;
                if (p.T >= p.Until)
                {
                    Release(p);
                    _live.RemoveAt(i);
                    continue;
                }
                if (p.T < 0f) continue;
                if (!p.Sr.enabled) p.Sr.enabled = true;
                int f = (int)(p.T * p.Fps);
                p.Sr.sprite = p.Frames[p.Loop ? f % p.Frames.Length : Mathf.Min(f, p.Frames.Length - 1)];
                if (p.Travel > 0f)
                {
                    var pos = Vector3.Lerp(p.A, p.B, p.T / p.Travel);
                    p.Sr.transform.localPosition = pos;
                    p.Sr.sortingOrder = IsoProjection.SortOrder(pos, p.Bias);
                }
                if (p.FollowHero)
                {
                    p.Sr.transform.localPosition = heroWorld;
                    p.Sr.sortingOrder = p.Ground ? GroundOrder : IsoProjection.SortOrder(heroWorld, p.Bias);
                }
                // Fade the last 15% so held loops and long clips leave gently.
                float k = p.T / p.Until;
                var c = p.Base;
                // 재기획 §4.1: a field settles to 0.35 after 0.3 s; an effect over the units dims after 0.25 s, so the units
                // under a blob of effects stay readable.
                if (p.Ground && p.T > 0.3f)
                {
                    int same = _groundCount.TryGetValue(p.Frames, out var n) ? n : 1;
                    float settle = Mathf.Clamp01((p.T - 0.3f) / 0.2f);
                    c.a *= Mathf.Lerp(1f, Mathf.Max(0.08f, 0.3f / same), settle);
                    // 재기획 §4.1: a settled field's fill is dark — the floor shows its element, not a glowing slab
                    // (act VI late game: the plague fields lit half the screen green).
                    float dark = Mathf.Lerp(1f, 0.55f, settle);
                    c.r *= dark; c.g *= dark; c.b *= dark;
                }
                // 11-23: 0.35 left most of every drawn effect faint after a quarter second (희승: 이펙트가 너무 단순해 보인다).
                if (p.FrontDecay && p.T > 0.25f) c.a *= Mathf.Lerp(1f, 0.6f, Mathf.Clamp01((p.T - 0.25f) / 0.2f));
                if (p.Settle < 1f)
                {
                    float since = p.T - p.PeakAt;
                    if (since > 0.12f) c.a *= Mathf.Lerp(1f, p.Settle, Mathf.Clamp01((since - 0.12f) / 0.2f));
                    if (p.Glow != null)
                    {
                        // Pure light for the blow: up in two frames before the peak, out over GlowLen after it.
                        float g = since < 0f ? Mathf.Clamp01(1f + since / 0.04f) : Mathf.Clamp01(1f - since / GlowLen);
                        p.Glow.enabled = g > 0.01f;
                        if (p.Glow.enabled)
                        {
                            p.Glow.sprite = p.Sr.sprite;
                            p.Glow.GetPropertyBlock(_glowBlock);
                            _glowBlock.SetColor(TintId, new Color(p.GlowTint.r, p.GlowTint.g, p.GlowTint.b, 0.55f * g * g * p.GlowTint.a));
                            p.Glow.SetPropertyBlock(_glowBlock);
                        }
                    }
                }
                if (k > 0.85f) c.a *= Mathf.Clamp01((1f - k) / 0.15f);
                p.Sr.color = c;
            }
        }

        readonly Dictionary<Sprite[], int> _groundCount = new Dictionary<Sprite[], int>();

        public void Clear()
        {
            foreach (var p in _live) Release(p);
            _live.Clear();
        }
    }
}
