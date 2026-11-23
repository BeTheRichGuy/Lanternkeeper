using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Five tones per element, dark rim → white core (docs/research/1123_pixel_vfx.md §2: the hot core is what reads as
    /// "energy"; a hue shift down the ramp keeps it from looking flat).
    /// </summary>
    public sealed class PixelRamp
    {
        public readonly Color[] C;
        PixelRamp(params string[] hex)
        {
            C = new Color[hex.Length];
            for (int i = 0; i < hex.Length; i++) ColorUtility.TryParseHtmlString(hex[i], out C[i]);
        }

        public static readonly PixelRamp Ember = new PixelRamp("#5a1206", "#b5300c", "#ff7a1f", "#ffc24a", "#fff3c8");
        public static readonly PixelRamp Frost = new PixelRamp("#0b2350", "#2f6fc0", "#66bfff", "#bfeaff", "#f4fdff");
        public static readonly PixelRamp Shock = new PixelRamp("#3a2a05", "#b88a10", "#ffd83a", "#fff6a8", "#ffffff");
        public static readonly PixelRamp Rot = new PixelRamp("#142a08", "#3d6a17", "#74b83a", "#c8f27a", "#f2ffd0");
        public static readonly PixelRamp Void = new PixelRamp("#12051f", "#3f1a6e", "#8a4ae0", "#c9a2ff", "#f5ecff");
        public static readonly PixelRamp Holy = new PixelRamp("#4a3208", "#a8761c", "#f2c14e", "#ffe9a3", "#fffdf0");
        public static readonly PixelRamp Steel = new PixelRamp("#2b2622", "#6b625a", "#b8ada0", "#e8e0d2", "#ffffff");
        public static readonly PixelRamp Blood = new PixelRamp("#1e0203", "#4a0709", "#86100f", "#b42a1e", "#e0614a");
        /// <summary>Danger: every enemy warning, whatever the act (red reads as "move" everywhere).</summary>
        public static readonly PixelRamp Danger = new PixelRamp("#2a0505", "#7a0d0d", "#d0241a", "#ff6a3a", "#ffd2a0");
        public static readonly PixelRamp Ash = new PixelRamp("#141210", "#2c2824", "#4a443d", "#6e665c", "#948a7e");

        public static PixelRamp Of(Element e, PixelRamp none = null)
        {
            switch (e)
            {
                case Element.Ember: return Ember;
                case Element.Frost: return Frost;
                case Element.Shock: return Shock;
                case Element.Rot: return Rot;
                case Element.Void: return Void;
                default: return none ?? Steel;
            }
        }

        /// <summary>The tone at <paramref name="k"/> (0 dark .. 1 white), stepped, never blended (pixel art keeps its palette).</summary>
        public Color At(float k) => C[Mathf.Clamp(Mathf.FloorToInt(k * C.Length), 0, C.Length - 1)];
    }

    /// <summary>
    /// Which way a blow travels over the floor (희승 11-23: the bits behind a struck enemy fly "내가 때린 방향에 따라"):
    /// from where it came from (the hero, a blast's centre, an arrow's start, a chain's last link) through the target. The
    /// last blow on each enemy is kept so its death bursts the same way. World (screen) units, normalised; zero if unknown.
    /// </summary>
    public static class BlowDirs
    {
        static readonly Dictionary<int, Vector2> Last = new Dictionary<int, Vector2>();

        public static Vector2 For(in SimEvent ev, Vec2 hero)
        {
            if (ev.Type == SimEventType.Death) return Last.TryGetValue(ev.TargetId, out var k) ? k : Away(hero, ev.Pos);
            return Away(ev.From.X != 0f || ev.From.Y != 0f ? ev.From : hero, ev.Pos);
        }

        /// <summary>Keeps each enemy's last blow (call once per event, before the views see it).</summary>
        public static void Note(in SimEvent ev, Vec2 hero)
        {
            if (ev.Type == SimEventType.Hit && ev.TargetId != 0) { var d = For(ev, hero); if (d != Vector2.zero) Last[ev.TargetId] = d; }
        }

        public static void Forget(int id) => Last.Remove(id);
        public static void Clear() => Last.Clear();

        static Vector2 Away(Vec2 from, Vec2 to)
        {
            var a = IsoProjection.ToWorld(from); var b = IsoProjection.ToWorld(to);
            var d = new Vector2(b.x - a.x, b.y - a.y);
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector2.zero;
        }
    }

    public enum FxShape { Ring = 0, Slash = 1, Flare = 2, Blast = 3, Field = 4, Scorch = 5, Beam = 6, Sigil = 7, Warn = 8 }

    /// <summary>
    /// Procedural effect quads (PixelFx shader): each is computed per art pixel at its own size, placed on the art-pixel
    /// grid, and runs its 0..1 progress over its life.
    /// </summary>
    public sealed class PixelShapes
    {
        sealed class Shape
        {
            public SpriteRenderer Sr;
            public FxShape Kind;
            public float T, Life, Seed, Angle, Param, Boost, Add, Alpha;
            public Vector2 Px;
            public PixelRamp Ramp;
        }

        public const float ArtPx = 1f / IsoProjection.PPU;
        const int Max = 140;
        readonly Transform _root;
        readonly Material _material;
        readonly Sprite _quad;
        readonly List<Shape> _live = new List<Shape>();
        readonly Stack<Shape> _pool = new Stack<Shape>();
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        static readonly int IdKind = Shader.PropertyToID("_Kind"), IdT = Shader.PropertyToID("_T"), IdSeed = Shader.PropertyToID("_Seed"),
            IdPx = Shader.PropertyToID("_Px"), IdAngle = Shader.PropertyToID("_Angle"), IdBoost = Shader.PropertyToID("_Boost"),
            IdAdd = Shader.PropertyToID("_Add"), IdAlpha = Shader.PropertyToID("_Alpha"), IdParam = Shader.PropertyToID("_Param");
        static readonly int[] IdC = { Shader.PropertyToID("_C0"), Shader.PropertyToID("_C1"), Shader.PropertyToID("_C2"), Shader.PropertyToID("_C3"), Shader.PropertyToID("_C4") };

        public int Count => _live.Count;

        public PixelShapes(Transform parent)
        {
            _root = new GameObject("Pixel FX").transform;
            _root.SetParent(parent, false);
            _material = new Material(Resources.Load<Shader>("Shaders/PixelFx"));
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var white = new Color32[16];
            for (int i = 0; i < 16; i++) white[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(white);
            tex.Apply();
            _quad = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
        }

        public static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x / ArtPx) * ArtPx, Mathf.Round(p.y / ArtPx) * ArtPx, 0f);

        /// <param name="w">Width in world units (art pixels / 64).</param>
        /// <param name="h">Height in world units: half the width lays a round thing on the iso floor.</param>
        /// <param name="add">0 = painted over the scene, 1 = pure added light.</param>
        public void Add(FxShape kind, Vector3 world, float w, float h, float life, PixelRamp ramp, int order,
            float boost = 2.2f, float add = 0.4f, float angle = 0f, float param = 0f, float delay = 0f, float alpha = 1f)
        {
            if (_live.Count >= Max || life <= 0f) return;
            int pw = Mathf.Max(4, Even(Mathf.RoundToInt(w / ArtPx))), ph = Mathf.Max(4, Even(Mathf.RoundToInt(h / ArtPx)));
            var s = _pool.Count > 0 ? _pool.Pop() : new Shape { Sr = NewRenderer() };
            s.Sr.gameObject.SetActive(true);
            s.Sr.enabled = delay <= 0f;
            s.Sr.sortingOrder = order;
            var t = s.Sr.transform;
            t.localPosition = Snap(world);
            t.localScale = new Vector3(pw * ArtPx, ph * ArtPx, 1f);
            s.Kind = kind; s.T = -delay; s.Life = life; s.Seed = Random.value; s.Angle = angle; s.Param = param;
            s.Boost = boost; s.Add = add; s.Alpha = alpha; s.Px = new Vector2(pw, ph); s.Ramp = ramp;
            Apply(s, 0f);
            _live.Add(s);
        }

        static int Even(int v) => v + (v & 1);

        SpriteRenderer NewRenderer()
        {
            var sr = new GameObject("pfx").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(_root, false);
            sr.sprite = _quad;
            sr.sharedMaterial = _material;
            return sr;
        }

        void Apply(Shape s, float t)
        {
            _mpb.Clear();
            _mpb.SetFloat(IdKind, (int)s.Kind);
            _mpb.SetFloat(IdT, t);
            _mpb.SetFloat(IdSeed, s.Seed);
            _mpb.SetVector(IdPx, s.Px);
            _mpb.SetFloat(IdAngle, s.Angle);
            _mpb.SetFloat(IdBoost, s.Boost);
            _mpb.SetFloat(IdAdd, s.Add);
            _mpb.SetFloat(IdAlpha, s.Alpha);
            _mpb.SetFloat(IdParam, s.Param);
            for (int i = 0; i < 5; i++) _mpb.SetColor(IdC[i], s.Ramp.C[i]);
            s.Sr.SetPropertyBlock(_mpb);
        }

        public void Render(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var s = _live[i];
                s.T += dt;
                if (s.T >= s.Life)
                {
                    s.Sr.gameObject.SetActive(false);
                    _pool.Push(s);
                    _live.RemoveAt(i);
                    continue;
                }
                if (s.T < 0f) continue;
                if (!s.Sr.enabled) s.Sr.enabled = true;
                Apply(s, s.T / s.Life);
            }
        }

        public void Clear()
        {
            foreach (var s in _live) { s.Sr.gameObject.SetActive(false); _pool.Push(s); }
            _live.Clear();
        }
    }

    public enum PxKind : byte { Spark, Ember, Debris, Smoke, Shard, Glow, Edge }

    /// <summary>
    /// Art-pixel particles drawn as two meshes a frame (added light; painted smoke and debris). Positions are on the floor
    /// plane with a height above it, so debris arcs, lands and bounces like it would in the iso world.
    /// </summary>
    public sealed class PixelParticles
    {
        struct Pt
        {
            public PxKind Kind;
            public Vector2 G, V;     // floor position and velocity (world units)
            public float H, VH;      // height above the floor and its speed
            public float Grav, T, Life, Size, Seed;
            public PixelRamp Ramp;
            public byte Bounces;
        }

        const int Max = 4000; // a pixel bolt lays a particle per art pixel along its path
        const float Px = PixelShapes.ArtPx;
        readonly Pt[] _p = new Pt[Max];
        int _n;
        readonly Mesh _addMesh = new Mesh(), _paintMesh = new Mesh();
        readonly List<Vector3> _av = new List<Vector3>(), _pv = new List<Vector3>();
        readonly List<Color> _ac = new List<Color>(), _pc = new List<Color>();
        readonly List<int> _ai = new List<int>(), _pi = new List<int>();

        public int Count => _n;

        public PixelParticles(Transform parent, int paintOrder, int addOrder)
        {
            var shader = Resources.Load<Shader>("Shaders/PixelParticle");
            MakeLayer(parent, "pixel particles (paint)", _paintMesh, shader, UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, paintOrder);
            MakeLayer(parent, "pixel particles (light)", _addMesh, shader, UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.One, addOrder);
        }

        static void MakeLayer(Transform parent, string name, Mesh mesh, Shader shader, UnityEngine.Rendering.BlendMode src, UnityEngine.Rendering.BlendMode dst, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = new Material(shader);
            m.SetFloat("_Src", (float)src);
            m.SetFloat("_Dst", (float)dst);
            mr.sharedMaterial = m;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        public void Emit(PxKind kind, Vector2 floor, float height, Vector2 v, float vh, float life, float sizePx, PixelRamp ramp, float grav = 0f, float delay = 0f)
        {
            if (_n >= Max) return;
            _p[_n++] = new Pt { Kind = kind, G = floor, V = v, H = height, VH = vh, Grav = grav, T = -delay, Life = life, Size = sizePx, Seed = Random.value, Ramp = ramp };
        }

        public void Clear() => _n = 0;

        /// <summary>The hero's chest in world space: particles over the hero's body draw at half strength (review 11-23: keep
        /// the hero readable through the fight's own sparks).</summary>
        public Vector2 HeroChest;
        const float HeroClear = 0.42f;

        public void Render(float dt)
        {
            _av.Clear(); _ac.Clear(); _ai.Clear(); _pv.Clear(); _pc.Clear(); _pi.Clear();
            for (int i = _n - 1; i >= 0; i--)
            {
                ref var p = ref _p[i];
                p.T += dt;
                if (p.T >= p.Life) { _p[i] = _p[--_n]; continue; }
                if (p.T < 0f) continue;
                Step(ref p, dt);
                Draw(ref p);
            }
            Fill(_addMesh, _av, _ac, _ai);
            Fill(_paintMesh, _pv, _pc, _pi);
        }

        static void Fill(Mesh mesh, List<Vector3> v, List<Color> c, List<int> idx)
        {
            mesh.Clear();
            if (v.Count == 0) return;
            mesh.indexFormat = v.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(idx, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 10f));
        }

        void Step(ref Pt p, float dt)
        {
            float drag = p.Kind == PxKind.Spark ? 3.2f : p.Kind == PxKind.Smoke ? 1.8f : p.Kind == PxKind.Ember ? 1.2f : 0.6f;
            p.V *= Mathf.Max(0f, 1f - drag * dt);
            p.G += p.V * dt;
            p.VH -= p.Grav * dt;
            p.H += p.VH * dt;
            if (p.H < 0f && p.Grav > 0f)
            {
                if (p.Kind == PxKind.Debris && p.Bounces < 2 && p.VH < -0.6f) { p.H = 0f; p.VH = -p.VH * 0.32f; p.V *= 0.55f; p.Bounces++; }
                else { p.H = 0f; p.VH = 0f; p.V *= 0.2f; }
            }
            if (p.Kind == PxKind.Ember) p.G.x += Mathf.Sin(p.T * 9f + p.Seed * 20f) * 0.25f * dt; // a flicker of draught
        }

        void Draw(ref Pt p)
        {
            float k = p.T / p.Life;                     // 0 → 1 over life
            var at = new Vector2(p.G.x, p.G.y + p.H);
            int before = _ac.Count, beforeP = _pc.Count;
            switch (p.Kind)
            {
                case PxKind.Spark:
                {
                    // A white-hot head cooling down the ramp, with a trail of pixels back along its flight.
                    var col = Hot(p.Ramp, 1f - k * 1.1f, 2.6f);
                    var vel = new Vector2(p.V.x, p.V.y + p.VH);
                    float speed = vel.magnitude;
                    int trail = Mathf.Clamp(Mathf.RoundToInt(speed * 0.022f / Px), 0, 5);
                    var dir = speed > 1e-4f ? vel / speed : Vector2.zero;
                    Quad(_av, _ac, _ai, at, p.Size, col);
                    for (int j = 1; j <= trail; j++)
                    {
                        var tc = Hot(p.Ramp, 0.75f - k - j * 0.12f, 1.6f);
                        tc.a *= 1f - j / (trail + 1f);
                        Quad(_av, _ac, _ai, at - dir * (j * Px), 1f, tc);
                    }
                    break;
                }
                case PxKind.Ember:
                {
                    var col = Hot(p.Ramp, 0.85f - k * 0.7f, 1.8f);
                    col.a *= (0.65f + 0.35f * Mathf.Sin(p.T * 30f + p.Seed * 40f)) * (1f - k * k);
                    Quad(_av, _ac, _ai, at, p.Size, col);
                    break;
                }
                case PxKind.Edge:
                {
                    // The coloured skin around a bolt's white heart.
                    var col = p.Ramp.C[2];
                    col.a = 0.9f * (1f - k);
                    Quad(_av, _ac, _ai, at, p.Size, col);
                    break;
                }
                case PxKind.Glow:
                {
                    var col = Hot(p.Ramp, 0.5f, 1f);
                    col.a = 0.28f * (1f - k);
                    Disc(_av, _ac, _ai, at, Mathf.Max(1f, p.Size * (0.7f + 0.3f * k)), col);
                    break;
                }
                case PxKind.Debris:
                {
                    var col = p.Ramp.C[p.Seed < 0.5f ? 0 : 1];
                    col.a = 1f - Mathf.Clamp01((k - 0.75f) / 0.25f);
                    Quad(_pv, _pc, _pi, at, p.Size, col);
                    break;
                }
                case PxKind.Shard:
                {
                    var col = p.Ramp.C[p.Seed < 0.4f ? 4 : 3];
                    col.a = 1f - k * k;
                    bool tall = Mathf.Abs(p.VH) > Mathf.Abs(p.V.x);
                    QuadWH(_pv, _pc, _pi, at, tall ? 1f : p.Size, tall ? p.Size : 1f, col);
                    break;
                }
                case PxKind.Smoke:
                {
                    var col = Color.Lerp(p.Ramp.C[0], new Color(0.09f, 0.08f, 0.08f), 0.45f);
                    col.a = 0.3f * (1f - k) * Mathf.Clamp01(p.T / 0.08f); // 0.42 read as soft bokeh over the fight
                    Disc(_pv, _pc, _pi, at, Mathf.Max(1f, p.Size * (0.45f + 0.75f * k)), col);
                    break;
                }
            }
            if ((at - HeroChest).sqrMagnitude < HeroClear * HeroClear)
            {
                for (int i = before; i < _ac.Count; i++) { var c = _ac[i]; c.a *= 0.5f; _ac[i] = c; }
                for (int i = beforeP; i < _pc.Count; i++) { var c = _pc[i]; c.a *= 0.5f; _pc[i] = c; }
            }
        }

        static Color Hot(PixelRamp r, float k, float boost)
        {
            var c = r.At(Mathf.Clamp01(k));
            if (k >= 0.6f) { c.r *= boost; c.g *= boost; c.b *= boost; }
            c.a = 1f;
            return c;
        }

        static void Quad(List<Vector3> v, List<Color> c, List<int> idx, Vector2 at, float sizePx, Color col) =>
            QuadWH(v, c, idx, at, sizePx, sizePx, col);

        static void QuadWH(List<Vector3> v, List<Color> c, List<int> idx, Vector2 at, float wPx, float hPx, Color col)
        {
            int w = Mathf.Max(1, Mathf.RoundToInt(wPx)), h = Mathf.Max(1, Mathf.RoundToInt(hPx));
            float x0 = Mathf.Round(at.x / Px - w * 0.5f) * Px, y0 = Mathf.Round(at.y / Px - h * 0.5f) * Px;
            float x1 = x0 + w * Px, y1 = y0 + h * Px;
            int b = v.Count;
            v.Add(new Vector3(x0, y0, 0f)); v.Add(new Vector3(x1, y0, 0f)); v.Add(new Vector3(x1, y1, 0f)); v.Add(new Vector3(x0, y1, 0f));
            c.Add(col); c.Add(col); c.Add(col); c.Add(col);
            idx.Add(b); idx.Add(b + 1); idx.Add(b + 2); idx.Add(b); idx.Add(b + 2); idx.Add(b + 3);
        }

        /// <summary>A pixel disc, one strip per row (smoke puffs and light blobs stay pixel-round, not square).</summary>
        static void Disc(List<Vector3> v, List<Color> c, List<int> idx, Vector2 at, float rPx, Color col)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(rPx));
            float cx = Mathf.Round(at.x / Px) * Px, cy = Mathf.Round(at.y / Px) * Px;
            for (int y = -r; y < r; y++)
            {
                float yy = y + 0.5f;
                int half = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, r * r - yy * yy)));
                if (half <= 0) continue;
                float x0 = cx - half * Px, x1 = cx + half * Px, y0 = cy + y * Px, y1 = y0 + Px;
                int b = v.Count;
                v.Add(new Vector3(x0, y0, 0f)); v.Add(new Vector3(x1, y0, 0f)); v.Add(new Vector3(x1, y1, 0f)); v.Add(new Vector3(x0, y1, 0f));
                c.Add(col); c.Add(col); c.Add(col); c.Add(col);
                idx.Add(b); idx.Add(b + 1); idx.Add(b + 2); idx.Add(b); idx.Add(b + 2); idx.Add(b + 3);
            }
        }
    }

    /// <summary>
    /// 이펙트 레이어 감독 (1123_pixel_vfx.md §3): every blow, cast, death and resonance gets the layers a drawn flipbook
    /// cannot carry alone — a hot flare, a shockwave over the floor, sparks with trails, embers, debris that lands, smoke,
    /// a blast of fire — scaled by the blow's weight and budgeted per tick so a big pack stays readable.
    /// </summary>
    public sealed class VfxDirector
    {
        public readonly PixelShapes Shapes;
        public readonly PixelParticles Parts;
        readonly SimWorld _world;
        int _hits, _deaths, _casts, _shots, _resSheets, _tears, _splats;
        float _sigilReadyAt, _beamReadyAt;
        bool _drawn;

        /// <summary>The distortion pass, when there is one: (world centre, radius in cells, seconds, strength).</summary>
        public System.Action<Vector3, float, float, float> Ripple;

        /// <summary>The impact frame (two-tone screen for a few frames): (lit colour, seconds).</summary>
        public System.Action<Color, float> ImpactFrame;
        /// <summary>The zoom punch: amount (0.01 = 1% larger, easing back).</summary>
        public System.Action<float> ZoomKick;
        /// <summary>
        /// Plays a drawn sheet (pack id, cell, scale, mirrored, on the floor layer, recoloured into an element's ramp or
        /// None); false when it has no art or no budget.
        /// </summary>
        public System.Func<string, Vec2, float, bool, bool, Element, bool> Sheet;

        /// <summary>
        /// PVFX Foundry layers (CC0) over our own resonance sheets (희승 11-23: "공명 이펙트 … 너무 둔하고, 디테일이 떨어져"):
        /// our v2 sheets keep the shape, the PVFX ones add the weight - solid shaded ice, toxic cloud, a void that swallows.
        /// </summary>
        static string ResSheet(Resonance r) =>
            r == Resonance.Shatter ? "pvfx_frost_explosion" : r == Resonance.Overload ? "pvfx_static_burst" : r == Resonance.Plague ? "pvfx_toxic_explosion"
            : r == Resonance.Devour ? "pvfx_void_implosion" : r == Resonance.Collapse ? "pvfx_implosion" : null;

        /// <summary>The heavy-blow tear (PVFX crit-hit) in the element's painted variant; shock is recoloured into its ramp.</summary>
        static string CritSheet(Element e) =>
            e == Element.Frost ? "pvfx_crit_hit_frost" : e == Element.Rot ? "pvfx_crit_hit_venom" : e == Element.Void ? "pvfx_crit_hit_void" : "pvfx_crit_hit";

        /// <summary>
        /// The floor wave as a drawn sheet (희승 11-23: the flat yellow band under a skill "너무 둔하고, 디테일이 떨어져 보여"):
        /// PVFX Foundry's shockwave ring (CC0) in the element's own palette - a ring that throws up chips of floor and
        /// leaves a crack, instead of a shaded band. Null where no sheet suits (steel, ash): the band stays.
        /// </summary>
        static string RingSheet(PixelRamp r) =>
            r == PixelRamp.Ember ? "pvfx_ring_ember" : r == PixelRamp.Shock ? "pvfx_ring_solar" : r == PixelRamp.Rot ? "pvfx_ring_venom"
            : r == PixelRamp.Void ? "pvfx_ring_void" : r == PixelRamp.Frost ? "pvfx_ring_frost" : r == PixelRamp.Holy ? "pvfx_holy_nova" : null;

        /// <summary>World width of the sheet's ring at scale 1 (the PVFX ring spans 120 art pixels, the holy nova ~110).</summary>
        const float RingSheetWidth = 120f / 64f;

        /// <summary>
        /// The element's drawn floor ring, <paramref name="width"/> world units across; false where none suits. At most five
        /// in any 0.7 s: a drawn ring lingers 1.1 s (the flat band 0.4 s), and arrow rain landing in a boss room stacked
        /// 28 of them into one gold slab (editor check 11-23). Over the cap the caller draws nothing - its sparks and dust
        /// still mark the blow.
        /// </summary>
        bool SheetRing(Vec2 cell, float width, PixelRamp ramp, out bool capped)
        {
            capped = false;
            var sheet = RingSheet(ramp);
            if (sheet == null || Sheet == null) return false;
            int slot = 0;
            for (int i = 1; i < _ringAt.Length; i++) if (_ringAt[i] < _ringAt[slot]) slot = i;
            if (Time.time - _ringAt[slot] < 0.7f) { capped = true; return false; }
            // No more than twice the art size: at 2.7× its pixels turned to 3×3 blocks and its fading arcs to loose dashes.
            if (!Sheet(sheet, cell, Mathf.Min(2f, width / RingSheetWidth), false, true, Element.None)) return false;
            _ringAt[slot] = Time.time;
            return true;
        }

        readonly float[] _ringAt = { -9f, -9f, -9f, -9f, -9f };
        float _impactReadyAt;

        /// <summary>An impact frame at most every 4 s: rarer is what keeps it a punch.</summary>
        public void Punch(Color color, float seconds = 0.07f)
        {
            if (Time.unscaledTime < _impactReadyAt) return;
            _impactReadyAt = Time.unscaledTime + 4f;
            ImpactFrame?.Invoke(color, seconds);
        }

        public VfxDirector(Transform root, SimWorld world)
        {
            _world = world;
            Shapes = new PixelShapes(root);
            Parts = new PixelParticles(root, 30990, 31010);
        }

        public void BeginTick() { _hits = 0; _deaths = 0; _casts = 0; _shots = 0; _resSheets = 0; _tears = 0; _splats = 0; }

        // ---------- 림 라이트 (1122_pixel_lighting.md: directional coloured rim from the nearest light) ----------
        struct Glow { public Vector2 At; public Color C; public float R, T, Life; }
        readonly List<Glow> _glows = new List<Glow>();

        /// <summary>A short light the units around it catch on their rims (an explosion lights the pack it hits).</summary>
        void AddGlow(Vector3 world, Color c, float radius, float life)
        {
            if (_glows.Count >= 12) _glows.RemoveAt(0);
            _glows.Add(new Glow { At = new Vector2(world.x, world.y), C = c, R = radius, Life = life });
        }

        /// <summary>The strongest effect light on a unit standing at <paramref name="world"/>: colour, and its strength in a.</summary>
        public Color GlowAt(Vector3 world)
        {
            Color best = default;
            foreach (var g in _glows)
            {
                float d = Vector2.Distance(g.At, new Vector2(world.x, world.y));
                if (d >= g.R) continue;
                float k = (1f - d / g.R) * (1f - g.T / g.Life);
                if (k > best.a) best = new Color(g.C.r, g.C.g, g.C.b, k);
            }
            return best;
        }

        void TickGlows(float dt)
        {
            for (int i = _glows.Count - 1; i >= 0; i--)
            {
                var g = _glows[i];
                g.T += dt;
                if (g.T >= g.Life) _glows.RemoveAt(i); else _glows[i] = g;
            }
        }

        public void Render(float dt)
        {
            var hw = IsoProjection.ToWorld(_world.Hero.Pos);
            Parts.HeroChest = new Vector2(hw.x, hw.y + 0.4f);
            Statuses(dt);
            TickGlows(dt);
            Shapes.Render(dt);
            Parts.Render(dt);
        }

        float _statusAt;

        /// <summary>
        /// What is wrong with each enemy near the hero shows on its body (D2/D3: a burning, frozen or poisoned monster reads
        /// at a glance): embers rise off the burning, frost glints on the frozen, rot puffs off the poisoned, and a mote of
        /// its mark's colour hangs at the feet of a marked one (the resonance you are building toward).
        /// </summary>
        void Statuses(float dt)
        {
            if ((_statusAt -= dt) > 0f) return;
            _statusAt = 0.1f;
            var heroPos = _world.Hero.Pos;
            double now = _world.Time;
            int n = 0;
            foreach (var e in _world.Enemies)
            {
                if (!e.Alive || Vec2.DistSq(e.Pos, heroPos) > 13f * 13f) continue;
                if (n++ >= 48) break;
                var floor = Floor(e.Pos);
                float body = e.Boss ? 0.8f : 0.4f;
                if (e.BurnTicks > 0 && Random.value < 0.7f)
                    Parts.Emit(PxKind.Ember, floor + Random.insideUnitCircle * 0.15f, body * (0.5f + Random.value), Random.insideUnitCircle * 0.2f, 0.7f + Random.value * 0.6f, 0.5f + Random.value * 0.4f, Random.value < 0.25f ? 2f : 1f, PixelRamp.Ember, -0.5f);
                if (e.FreezeUntil > now && Random.value < 0.5f)
                    Parts.Emit(PxKind.Shard, floor + Random.insideUnitCircle * 0.18f, body * (0.3f + Random.value), Vector2.zero, 0.15f, 0.35f, 1f + Random.value * 2f, PixelRamp.Frost);
                if (e.DotTicks > 0 && Random.value < 0.35f)
                    Parts.Emit(PxKind.Smoke, floor + Random.insideUnitCircle * 0.15f, body * 0.6f, Random.insideUnitCircle * 0.1f, 0.25f, 0.8f, 3f + Random.value * 2f, PixelRamp.Rot);
                if (e.Mark != Element.None && Random.value < 0.3f)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    Parts.Emit(PxKind.Ember, floor + Iso(Mathf.Cos(a), Mathf.Sin(a)) * 0.7f, 0.04f, Vector2.zero, 0.35f, 0.55f, 1f, PixelRamp.Of(e.Mark), -0.1f);
                }
            }
        }

        public void Clear() { Shapes.Clear(); Parts.Clear(); BlowDirs.Clear(); }

        PixelRamp RampOf(Element e) => PixelRamp.Of(e, _world.Hero.ClassId == "oathknight" ? PixelRamp.Holy : PixelRamp.Steel);

        /// <summary>A grid-space direction as a floor-plane (world) direction.</summary>
        static Vector2 Iso(float gx, float gy) => new Vector2((gx - gy) * 0.5f, -(gx + gy) * 0.25f);

        static Vector2 Floor(Vec2 cell) { var w = IsoProjection.ToWorld(cell); return new Vector2(w.x, w.y); }

        /// <param name="drawn">A flipbook already draws this event (its own column of light, its own burst).</param>
        public void OnEvent(in SimEvent ev, bool drawn = false)
        {
            _drawn = drawn;
            int hero = _world.Hero.Id;
            switch (ev.Type)
            {
                case SimEventType.Hit when ev.TargetId != hero && !ev.FromChain:
                    Hit(ev, ev.SourceId == hero);
                    break;
                case SimEventType.Hit when ev.TargetId == hero && _hits < 14:
                    // The hero takes a blow: a small red star and a spurt (the blow must read, not cover the hero).
                    _hits++;
                    Burst(ev.Pos, PixelRamp.Blood, 0.5f, 3, 0.14f);
                    break;
                case SimEventType.Cast when ev.SourceId == hero && ev.Depth != 2:
                    Cast(ev);
                    break;
                case SimEventType.Cast when ev.SourceId == 0 && ev.SkillId != null:
                    Impact(ev); // a delayed blast landing (a meteor, a spear from above, a storm's bolt, an echo)
                    break;
                case SimEventType.Death when ev.TargetId != hero:
                    Death(ev);
                    break;
                case SimEventType.Resonance:
                    Burst(ev.Pos, RampOf(ev.Elem), 0.9f, 7, 0.22f);
                    // The PVFX layer under our sheet; overload's blue burst is repainted into the shock ramp (our overload is gold).
                    var res = ResSheet(ev.Res);
                    if (res != null && _resSheets++ < 4)
                        Sheet?.Invoke(res, ev.Pos, 1f, false, false, ev.Res == Resonance.Overload ? Element.Shock : Element.None);
                    break;
                case SimEventType.ChainPop when ev.Depth >= 2:
                    if (!SheetRing(ev.Pos, 1.2f, RampOf(ev.Elem), out var popCapped) && !popCapped)
                        Shapes.Add(FxShape.Ring, IsoProjection.ToWorld(ev.Pos), 1.2f, 0.6f, 0.32f, RampOf(ev.Elem), VfxPlayer.GroundOrder + 4, 2f, 0.5f);
                    Burst(ev.Pos, RampOf(ev.Elem), 0.6f, 4, 0.16f);
                    break;
                case SimEventType.GrandResonance:
                    // Late fights grand-resonate every few seconds: one ring and a blast, not the ultimate's double ring.
                    Nova(ev.Pos, Mathf.Min(3f, Mathf.Max(2.2f, ev.Value)), RampOf(ev.Elem), false, blast: ev.Elem == Element.Ember || ev.Elem == Element.Void);
                    break;
                case SimEventType.LevelUp:
                {
                    // A level: light falls on the hero, a circle opens under the feet, embers rise (idle satisfaction).
                    var feet = IsoProjection.ToWorld(_world.Hero.Pos);
                    Shapes.Add(FxShape.Beam, feet + new Vector3(0f, 2.05f, 0f), 0.9f, 4.2f, 0.7f, PixelRamp.Holy, IsoProjection.SortOrder(feet, 46), 1.8f, 0.6f);
                    Shapes.Add(FxShape.Sigil, feet, 2.2f, 1.1f, 1.2f, PixelRamp.Holy, VfxPlayer.GroundOrder + 5, 1.7f, 0.5f);
                    var f2 = new Vector2(feet.x, feet.y);
                    for (int i = 0; i < 24; i++)
                        Parts.Emit(PxKind.Ember, f2 + Iso(Random.value - 0.5f, Random.value - 0.5f) * 2.2f, 0.05f, Vector2.zero, 0.8f + Random.value * 1.4f, 1f + Random.value * 0.8f, Random.value < 0.3f ? 2f : 1f, PixelRamp.Holy, -0.4f, Random.value * 0.35f);
                    break;
                }
                case SimEventType.Telegraph:
                {
                    // An enemy's coming blow: the danger zone on the floor (under everything) that fills and flashes.
                    float r = Mathf.Max(0.6f, ev.Value);
                    Shapes.Add(FxShape.Warn, IsoProjection.ToWorld(ev.Pos), 1.414f * r * 1.05f, 0.707f * r * 1.05f, Mathf.Max(0.2f, ev.Depth * 0.1f), PixelRamp.Danger, VfxPlayer.GroundOrder + 6, 1.3f, 0.2f, alpha: 0.9f);
                    // …and faintly over everything, so it shows through the hero's own effects (재기획 §4.1).
                    Shapes.Add(FxShape.Warn, IsoProjection.ToWorld(ev.Pos), 1.414f * r * 1.05f, 0.707f * r * 1.05f, Mathf.Max(0.2f, ev.Depth * 0.1f), PixelRamp.Danger, 30980, 1.2f, 0.6f, alpha: 0.3f);
                    break;
                }
                case SimEventType.Slam:
                    Nova(ev.Pos, Mathf.Clamp(ev.Value, 1.5f, 4f), PixelRamp.Ash, false, debrisOnly: true);
                    break;
            }
        }

        // ---------- recipes ----------

        /// <summary>
        /// A lightning bolt between two points, drawn in pixels: a jagged path (midpoint displacement) with a branch or two,
        /// struck twice a few frames apart so it flickers, white at the heart cooling to the ramp.
        /// </summary>
        public void Bolt(Vec2 fromCell, Vec2 toCell, PixelRamp ramp, float delay = 0f)
        {
            var a = Floor(fromCell) + new Vector2(0f, 0.35f);
            var b = Floor(toCell) + new Vector2(0f, 0.35f);
            for (int strike = 0; strike < 2; strike++)
            {
                float d = delay + strike * 0.07f;
                var pts = Jag(a, b, 4);
                DrawPath(pts, ramp, d, 0.12f, 3f);
                // A branch splits off from a point along the bolt.
                if (Random.value < 0.7f && pts.Count > 4)
                {
                    var from = pts[Random.Range(2, pts.Count - 2)];
                    var dir = (b - a).normalized;
                    var side = new Vector2(-dir.y, dir.x) * (Random.value < 0.5f ? 1f : -1f);
                    DrawPath(Jag(from, from + (dir + side) * 0.5f * (b - a).magnitude * 0.3f, 2), ramp, d, 0.08f, 1f);
                }
            }
            Parts.Emit(PxKind.Glow, b - new Vector2(0f, 0.35f), 0.35f, Vector2.zero, 0f, 0.16f, 5f, ramp, 0f, delay);
        }

        static List<Vector2> Jag(Vector2 a, Vector2 b, int depth)
        {
            var pts = new List<Vector2> { a, b };
            float rough = (b - a).magnitude * 0.22f;
            for (int k = 0; k < depth; k++)
            {
                var next = new List<Vector2>(pts.Count * 2);
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var p = pts[i]; var q = pts[i + 1];
                    var n = new Vector2(-(q - p).y, (q - p).x).normalized;
                    next.Add(p);
                    next.Add((p + q) * 0.5f + n * (Random.value - 0.5f) * rough);
                }
                next.Add(pts[pts.Count - 1]);
                pts = next;
                rough *= 0.55f;
            }
            return pts;
        }

        /// <summary>A bolt's path: a white heart of <paramref name="sizePx"/> with a skin of the element's colour two pixels wider.</summary>
        void DrawPath(List<Vector2> pts, PixelRamp ramp, float delay, float life, float sizePx)
        {
            float step = PixelShapes.ArtPx * Mathf.Max(1f, sizePx - 1f);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var p = pts[i]; var q = pts[i + 1];
                int n = Mathf.Max(1, Mathf.CeilToInt((q - p).magnitude / step));
                for (int j = 0; j < n; j++)
                {
                    var at = Vector2.Lerp(p, q, j / (float)n);
                    Parts.Emit(PxKind.Edge, at, 0f, Vector2.zero, 0f, life * 1.2f, sizePx + 2f, ramp, 0f, delay);
                    Parts.Emit(PxKind.Spark, at, 0f, Vector2.zero, 0f, life, sizePx, ramp, 0f, delay);
                }
            }
        }

        /// <summary>
        /// A hero's projectile: a trail of hot pixels laid where it passes (each shown as the shot goes by), a small
        /// flash where it leaves the bow or hand.
        /// </summary>
        public void Shot(in SimEvent ev, float travel, float delay)
        {
            if (_shots++ >= 8) return;
            var def = string.IsNullOrEmpty(ev.SkillId) ? null : SkillBook.Get(ev.SkillId);
            var elem = ev.Elem != Element.None ? ev.Elem : def?.Elem ?? Element.None;
            var ramp = PixelRamp.Of(elem, _world.Hero.ClassId == "starseer" ? PixelRamp.Void : PixelRamp.Steel);
            var a = Floor(ev.From);
            var b = Floor(ev.Pos);
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            var dir = d / len;
            // A plain arrow keeps only the flash at the bow: white pixel dashes behind every arrow read as cheap (build 11-23).
            if (elem == Element.None && _world.Hero.ClassId != "starseer") { Parts.Emit(PxKind.Glow, a + dir * 0.2f, 0.35f, Vector2.zero, 0f, 0.1f, 2f, ramp, 0f, delay); return; }
            int n = Mathf.Clamp(Mathf.RoundToInt(len / 0.16f), 4, 14);
            const float chest = 0.35f;
            for (int i = 0; i < n; i++)
            {
                float f = (i + 0.5f) / n;
                var jitter = new Vector2(-dir.y, dir.x) * ((Random.value - 0.5f) * 0.05f);
                Parts.Emit(PxKind.Spark, a + d * f + jitter, chest, dir * 0.4f, 0f, 0.16f + Random.value * 0.12f, 1f, ramp, 0f, delay + f * travel);
                if (elem != Element.None && Random.value < 0.35f)
                    Parts.Emit(PxKind.Ember, a + d * f, chest, Random.insideUnitCircle * 0.3f, 0.3f, 0.4f + Random.value * 0.3f, 1f, ramp, -0.2f, delay + f * travel);
            }
            Parts.Emit(PxKind.Glow, a + dir * 0.2f, chest, Vector2.zero, 0f, 0.12f, 3f, ramp, 0f, delay);
        }

        void Hit(in SimEvent ev, bool byHero)
        {
            _hits++;
            var skill = string.IsNullOrEmpty(ev.SkillId) ? null : SkillBook.Get(ev.SkillId);
            int tier = skill != null && (skill.Role == SkillRole.Ultimate || skill.IsEvolution) ? 2
                : ev.Res != Resonance.None || (skill != null && skill.Role == SkillRole.Finisher) ? 1 : 0;
            if (!byHero) tier = 0;
            var ramp = RampOf(ev.Elem);
            var dir = BlowDirs.For(ev, _world.Hero.Pos); // the blow's way: everything thrown off flies on behind the enemy
            if (_hits > 14) { Sparks(ev.Pos, ramp, 2, 2.2f, 0.16f, dir); return; } // a crowd of hits: a spark each, nothing more
            float flare = tier == 2 ? 1.15f : tier == 1 ? 0.8f : 0.55f;
            Burst(ev.Pos, ramp, flare, tier == 2 ? 12 : tier == 1 ? 7 : 4, tier == 2 ? 0.24f : tier == 1 ? 0.18f : 0.14f,
                spikes: ev.Elem == Element.Shock ? 6f : 4f, dir: dir);
            int extra = tier == 2 ? 6 : tier == 1 ? 4 : 2;
            var floor = Floor(ev.Pos);
            // A heavy blow (a resonance, a finisher, an ultimate) tears through: PVFX's crit streak, mirrored to the blow's way.
            if (byHero && tier >= 1 && _tears++ < 3)
                Sheet?.Invoke(CritSheet(ev.Elem), ev.Pos, 1f, dir.x < 0f, false, ev.Elem == Element.Shock ? Element.Shock : Element.None);
            // A melee blow cuts across the body: a thin arc flashed over the struck enemy for a few frames.
            if (byHero && _hits <= 8 && HeroStats.IsMeleeClass(_world.Hero.ClassId))
            {
                var chest = IsoProjection.ToWorld(ev.Pos) + new Vector3(0f, 0.38f, 0f);
                float cut = tier == 2 ? 1.3f : tier == 1 ? 1.05f : 0.85f;
                // The arc bulges the way the blade went through (it was a random tilt).
                float cutAngle = dir != Vector2.zero ? Mathf.Atan2(dir.y, dir.x) + Random.Range(-0.5f, 0.5f)
                    : Random.Range(-0.9f, 0.9f) + (Random.value < 0.5f ? 0f : Mathf.PI);
                Shapes.Add(FxShape.Slash, chest, cut, cut, 0.13f, ramp, IsoProjection.SortOrder(chest, 44), 2f, 0.6f, cutAngle, 0.55f);
            }
            switch (ev.Elem)
            {
                case Element.Ember:
                    for (int i = 0; i < extra; i++)
                        Parts.Emit(PxKind.Ember, floor + Random.insideUnitCircle * 0.12f, 0.35f, dir * (0.4f + Random.value * 0.5f) + Random.insideUnitCircle * 0.3f, 0.6f + Random.value * 0.8f, 0.6f + Random.value * 0.6f, Random.value < 0.3f ? 2f : 1f, ramp, -0.4f);
                    break;
                case Element.Frost:
                    for (int i = 0; i < extra + 1; i++)
                    {
                        var d = Behind(dir, 0.75f);
                        Parts.Emit(PxKind.Shard, floor, 0.35f, d * (1.2f + Random.value * 1.4f), 1.2f + Random.value * 1.5f, 0.45f + Random.value * 0.3f, 2f + Random.value * 2f, ramp, 7f);
                    }
                    break;
                case Element.Rot:
                    for (int i = 0; i < Mathf.Max(1, extra / 2); i++)
                        Parts.Emit(PxKind.Smoke, floor + Random.insideUnitCircle * 0.15f, 0.25f, dir * 0.2f + Random.insideUnitCircle * 0.2f, 0.25f, 0.7f + Random.value * 0.4f, 4f + Random.value * 3f, ramp);
                    break;
                case Element.Void:
                    for (int i = 0; i < extra; i++)
                    {
                        // Drawn in, not thrown out: sparks start around the blow and fall into it.
                        var d = Random.insideUnitCircle.normalized * (0.35f + Random.value * 0.25f);
                        Parts.Emit(PxKind.Spark, floor + d, 0.35f, -d * 3.2f, 0f, 0.18f, 1f, ramp);
                    }
                    break;
            }
            if (tier == 2)
            {
                if (!SheetRing(ev.Pos, 1.6f, ramp, out var hitCapped) && !hitCapped)
                    Shapes.Add(FxShape.Ring, IsoProjection.ToWorld(ev.Pos), 1.6f, 0.8f, 0.35f, ramp, VfxPlayer.GroundOrder + 4, 2.2f, 0.5f);
                Debris(ev.Pos, PixelRamp.Ash, 5, 1.2f, dir: dir);
            }
            // 고어: a solid blow knocks a few dark-red chunks off, out the far side of the enemy.
            if (GameSettings.Blood && byHero && tier >= 1)
                Debris(ev.Pos, PixelRamp.Blood, 2 + tier, 1.1f, 0.35f, dir);
        }

        void Cast(in SimEvent ev)
        {
            var def = string.IsNullOrEmpty(ev.SkillId) ? null : SkillBook.Get(ev.SkillId);
            var elem = ev.Elem != Element.None ? ev.Elem : def?.Elem ?? Element.None;
            var ramp = RampOf(elem);
            bool big = def != null && (def.Role == SkillRole.Ultimate || def.IsEvolution);
            bool echo = ev.SkillId != null && ev.SkillId.EndsWith("_echo");
            // An ultimate opens a summoning circle under the hero, once in two seconds at most (on every evolved skill it
            // never left the late-game hero's feet, build 11-23).
            if (def != null && def.Role == SkillRole.Ultimate && !echo && Time.unscaledTime >= _sigilReadyAt)
            {
                Punch(ramp.C[3]);
                _sigilReadyAt = Time.unscaledTime + 2f;
                var feet = IsoProjection.ToWorld(_world.Hero.Pos);
                Shapes.Add(FxShape.Sigil, feet, 2.6f, 1.3f, 0.9f, ramp, VfxPlayer.GroundOrder + 5, 1.8f, 0.5f);
            }
            // A swing that cuts (melee classes): a smear of the blade's arc in front of the hero.
            if (def != null && HeroStats.IsMeleeClass(_world.Hero.ClassId) && (def.Shape == SkillShape.Cone || def.Shape == SkillShape.Single || def == SkillBook.Basic))
            {
                var f = _world.Hero.Facing;
                var fw = Iso(f.X, f.Y);
                float angle = Mathf.Atan2(fw.y, fw.x);
                float size = Mathf.Clamp((def.Radius > 0.1f ? def.Radius : 1.2f) * 1.25f, 1.1f, big ? 3.2f : 2.4f);
                var dir = fw.sqrMagnitude > 1e-6f ? fw.normalized : Vector2.right;
                var at = IsoProjection.ToWorld(_world.Hero.Pos) + new Vector3(dir.x, dir.y, 0f) * (size * 0.18f) + new Vector3(0f, 0.32f, 0f);
                Shapes.Add(FxShape.Slash, at, size, size * 0.8f, big ? 0.34f : 0.26f, ramp, IsoProjection.SortOrder(at, 40), 1.8f, 0.35f, angle, def.Shape == SkillShape.Cone ? 1.5f : 1.2f);
                if (def == SkillBook.Basic) return;
            }
            // A cast with an area: the shockwave over the floor, the blast, sparks, debris and smoke.
            if (ev.Value > 1.6f && def != null && def.Shape != SkillShape.Buff && def.Shape != SkillShape.Aura)
            {
                if (_casts++ >= 4) return;
                // The drawn ring stays near the hero's own size: a ring over the whole skill area (5-6 cells) swept the
                // whole screen every few seconds (1080p build 11-23). The area itself shows in the sparks and dust.
                float r = Mathf.Min(echo ? ev.Value * 0.7f : ev.Value, big ? 4.5f : 2.6f);
                // The floor ring is for a burst that spreads from a point (circle, leap, a fall, a pull); a fan or a line keeps
                // its sparks and dust but no ring (rings round every cast circled the late-game hero nonstop).
                bool burst = def.Shape == SkillShape.Circle || def.Shape == SkillShape.Leap || def.Shape == SkillShape.Delayed || def.Shape == SkillShape.Pull;
                Nova(ev.Pos, r, ramp, big, blast: elem == Element.Ember || elem == Element.Void || big, ring: burst && (elem != Element.None || big));
            }
        }

        /// <summary>Burnt ground is burnt: light and lightning scorch like fire; frost, rot and void mark the floor in their own.</summary>
        static PixelRamp ScorchOf(PixelRamp r) => r == PixelRamp.Frost || r == PixelRamp.Rot || r == PixelRamp.Void ? r : PixelRamp.Ember;

        static readonly string[] FromAbove = { "meteor", "spear", "bolt", "rain", "hammer", "judgment", "star", "comet", "bell" };

        void Impact(in SimEvent ev)
        {
            if (_casts++ >= 5) return;
            var def = SkillBook.Get(ev.SkillId);
            var elem = ev.Elem != Element.None ? ev.Elem : def?.Elem ?? Element.None;
            var ramp = RampOf(elem);
            bool above = def != null && def.Shape == SkillShape.Delayed;
            if (!above) foreach (var k in FromAbove) if (ev.SkillId.Contains(k)) { above = true; break; }
            var world = IsoProjection.ToWorld(ev.Pos);
            float r = Mathf.Clamp(ev.Value > 0.1f ? ev.Value : 1.4f, 0.8f, 3.2f);
            // The column only where no drawn sheet brings its own, and one in 0.15 s (a storm's five stacked into a wall).
            if (above && !_drawn && Time.unscaledTime >= _beamReadyAt)
            {
                _beamReadyAt = Time.unscaledTime + 0.15f;
                // A column of light falls onto the spot (a crooked one for lightning), the floor takes the blow.
                float bw = Mathf.Clamp(r * 0.55f, 0.45f, 1.3f), bh = 4.2f;
                Shapes.Add(FxShape.Beam, world + new Vector3(0f, bh * 0.5f - 0.05f, 0f), bw, bh, 0.36f, ramp, IsoProjection.SortOrder(world, 46), 1.45f, 0.55f,
                    param: elem == Element.Shock ? 1f : 0f);
            }
            Nova(ev.Pos, r, ramp, false, blast: elem == Element.Ember || elem == Element.Void, ring: elem != Element.None);
        }

        void Death(in SimEvent ev)
        {
            if (_deaths++ >= 8) return;
            var ramp = RampOf(ev.KillElem);
            var dir = BlowDirs.For(ev, _world.Hero.Pos); // the killing blow's way: the body bursts on along it
            var floor = Floor(ev.Pos);
            var world = IsoProjection.ToWorld(ev.Pos);
            switch (ev.KillElem)
            {
                case Element.Ember:
                    Shapes.Add(FxShape.Blast, world + new Vector3(0f, 0.3f, 0f), 1.05f, 1.0f, 0.42f, ramp, IsoProjection.SortOrder(world, 45), 1.8f, 0.3f);
                    for (int i = 0; i < 8; i++)
                        Parts.Emit(PxKind.Ember, floor + Random.insideUnitCircle * 0.2f, 0.3f, dir * (0.5f + Random.value * 0.6f) + Random.insideUnitCircle * 0.4f, 0.8f + Random.value * 1.2f, 0.8f + Random.value * 0.7f, Random.value < 0.3f ? 2f : 1f, ramp, -0.3f);
                    Smoke(ev.Pos, 3, 0.9f);
                    Shapes.Add(FxShape.Scorch, world, 0.9f, 0.45f, 3f, ramp, VfxPlayer.GroundOrder + 2, 1.6f, 0f, alpha: 0.55f);
                    break;
                case Element.Frost:
                    for (int i = 0; i < 14; i++)
                    {
                        var d = Behind(dir, 0.65f);
                        Parts.Emit(PxKind.Shard, floor, 0.3f + Random.value * 0.3f, d * (1.5f + Random.value * 2f), 1.5f + Random.value * 2f, 0.6f + Random.value * 0.4f, 2f + Random.value * 3f, ramp, 8f);
                    }
                    Burst(ev.Pos, ramp, 0.8f, 4, 0.18f, dir: dir);
                    break;
                case Element.Shock:
                    Burst(ev.Pos, ramp, 1.0f, 10, 0.2f, spikes: 6f, dir: dir);
                    break;
                case Element.Rot:
                    Shapes.Add(FxShape.Blast, world + new Vector3(0f, 0.2f, 0f), 0.9f, 0.7f, 0.55f, ramp, IsoProjection.SortOrder(world, 45), 1.05f, 0f);
                    for (int i = 0; i < 5; i++)
                        Parts.Emit(PxKind.Smoke, floor + Random.insideUnitCircle * 0.25f, 0.2f, Random.insideUnitCircle * 0.3f, 0.3f, 1f + Random.value * 0.6f, 5f + Random.value * 4f, ramp);
                    break;
                case Element.Void:
                    Shapes.Add(FxShape.Flare, world + new Vector3(0f, 0.35f, 0f), 1.0f, 1.0f, 0.3f, ramp, IsoProjection.SortOrder(world, 45), 2.6f, 0.7f, param: 8f);
                    for (int i = 0; i < 8; i++)
                    {
                        var d = Random.insideUnitCircle.normalized * (0.5f + Random.value * 0.3f);
                        Parts.Emit(PxKind.Spark, floor + d, 0.35f, -d * 3f, 0f, 0.22f, 1f, ramp);
                    }
                    break;
            }
            if (ev.KillElem == Element.None)
            {
                // No element: the body breaks with a puff of dust and grit.
                Smoke(ev.Pos, 2, 0.7f);
                Debris(ev.Pos, PixelRamp.Ash, 3, 1.1f, 0.3f, dir);
            }
            // 고어: the body bursts into chunks that bounce and lie a moment (thrown on the way the blow went).
            if (GameSettings.Blood) Debris(ev.Pos, PixelRamp.Blood, 6, 1.6f, 0.4f, dir);
            // …and a drawn splat (PVFX blood-splat sprays right from its root): thrown on past the body, a few a tick.
            if (GameSettings.Blood && _splats++ < 3)
                Sheet?.Invoke("pvfx_blood_splat", ev.Pos, 1f, dir.x < 0f, false, Element.None);
        }

        // ---------- building blocks ----------

        /// <summary>The flare of a blow with sparks thrown out and a blob of light under it.</summary>
        void Burst(Vec2 cell, PixelRamp ramp, float size, int sparks, float life, float spikes = 4f, Vector2 dir = default)
        {
            var world = IsoProjection.ToWorld(cell) + new Vector3(0f, 0.35f, 0f);
            Shapes.Add(FxShape.Flare, world, size, size, life, ramp, IsoProjection.SortOrder(world, 45), 1.7f, 0.6f, param: spikes);
            if (size >= 0.8f) AddGlow(world, ramp.C[3], 1.5f, 0.25f);
            var floor = Floor(cell);
            Sparks(cell, ramp, sparks, 2.6f + size * 2f, 0.2f + size * 0.12f, dir);
        }

        /// <summary>A direction thrown out behind a blow: within a fan around <paramref name="dir"/> (wider as spread grows),
        /// anywhere when the blow's way is unknown.</summary>
        static Vector2 Behind(Vector2 dir, float spread)
        {
            if (dir == Vector2.zero) return Random.insideUnitCircle.normalized;
            var d = (dir + Random.insideUnitCircle * spread).normalized;
            return d.sqrMagnitude > 1e-6f ? d : dir;
        }

        void Sparks(Vec2 cell, PixelRamp ramp, int n, float speed, float life, Vector2 dir = default)
        {
            var floor = Floor(cell);
            var away = dir;
            if (away == Vector2.zero) away = floor - Floor(_world.Hero.Pos);
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Random.insideUnitCircle.normalized;
            for (int i = 0; i < n; i++)
            {
                // Mostly on along the blow (out the far side of the enemy), a few anywhere.
                var d = (Random.value < 0.8f ? (away + Random.insideUnitCircle * 0.7f) : Random.insideUnitCircle).normalized;
                float s = speed * (0.5f + Random.value * 0.7f);
                Parts.Emit(PxKind.Spark, floor, 0.35f, new Vector2(d.x, d.y * 0.6f) * s, (Random.value * 1.6f + 0.4f) * speed * 0.4f, life * (0.7f + Random.value * 0.6f), 1f, ramp, 6f);
            }
        }

        void Debris(Vec2 cell, PixelRamp ramp, int n, float speed, float height = 0.15f, Vector2 dir = default)
        {
            var floor = Floor(cell);
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Mathf.PI * 2f, s = speed * (0.4f + Random.value * 0.8f);
                var v = Iso(Mathf.Cos(a), Mathf.Sin(a)) * s * 1.6f;
                if (dir != Vector2.zero) v = v * 0.45f + dir * s * 1.25f; // chunks fly on the way the blow went
                Parts.Emit(PxKind.Debris, floor, height, v, 1.6f + Random.value * 2.2f, 1.1f + Random.value * 0.6f, Random.value < 0.4f ? 3f : 2f, ramp, 9f);
            }
        }

        void Smoke(Vec2 cell, int n, float spread)
        {
            var floor = Floor(cell);
            for (int i = 0; i < n; i++)
                Parts.Emit(PxKind.Smoke, floor + Random.insideUnitCircle * 0.2f * spread, 0.2f, Random.insideUnitCircle * 0.3f * spread, 0.35f + Random.value * 0.25f, 1.1f + Random.value * 0.7f, 5f + Random.value * 4f, PixelRamp.Ash, 0f, Random.value * 0.15f);
        }

        /// <summary>An area cast: a shockwave the size of the area, a blast for fire and void, sparks, debris and smoke.</summary>
        void Nova(Vec2 cell, float radius, PixelRamp ramp, bool big, bool blast = false, bool debrisOnly = false, bool ring = true)
        {
            var world = IsoProjection.ToWorld(cell);
            float w = 1.414f * radius * 1.05f;
            if (ring)
            {
                bool drawnRing = SheetRing(cell, w, ramp, out var ringCapped);
                // An ultimate also gets the inner band under the drawn ring (two waves read as the heavier blow).
                if (!drawnRing && !ringCapped)
                    Shapes.Add(FxShape.Ring, world, w, w * 0.5f, big ? 0.6f : 0.4f, ramp, VfxPlayer.GroundOrder + 4, big ? 1.5f : 1.25f, 0.25f, alpha: big ? 1f : 0.8f);
                if (big) Shapes.Add(FxShape.Ring, world, w * 0.7f, w * 0.35f, 0.5f, ramp, VfxPlayer.GroundOrder + 4, 1.7f, 0.4f, delay: 0.08f, alpha: drawnRing ? 0.6f : 1f);
            }
            Ripple?.Invoke(world, radius, big ? 0.6f : 0.42f, big ? 1f : 0.6f);
            if (!debrisOnly) AddGlow(world, ramp.C[3], Mathf.Max(1.6f, w * 0.65f), big ? 0.5f : 0.38f);
            int n = Mathf.Clamp(Mathf.RoundToInt(radius * (big ? 14f : 8f)), 10, big ? 60 : 32);
            var floor = new Vector2(world.x, world.y);
            if (!debrisOnly)
            {
                if (blast)
                {
                    float bw = Mathf.Min(w * 0.75f, big ? 3.8f : 2.8f);
                    // Fire and light burn bright; rot and void stay murky (a glowing green ball read as a lamp, not plague).
                    bool murky = ramp == PixelRamp.Rot || ramp == PixelRamp.Void;
                    Shapes.Add(FxShape.Blast, world + new Vector3(0f, bw * 0.22f, 0f), bw, bw * 0.85f, big ? 0.6f : 0.45f, ramp, IsoProjection.SortOrder(world, 45), murky ? 1.1f : 1.8f, murky ? 0.05f : 0.3f);
                    // The blast leaves its mark: a burnt blot whose cracks glow and cool (decal layer, 1123_pixel_vfx.md §3).
                    Shapes.Add(FxShape.Scorch, world, bw * 0.8f, bw * 0.4f, big ? 5f : 3.5f, ScorchOf(ramp), VfxPlayer.GroundOrder + 2, 1.6f, 0f, alpha: 0.55f);
                    // The fire gives way to smoke rolling up off its top, and the burnt ground keeps spitting embers a while.
                    if (!murky)
                    {
                        for (int i = 0; i < (big ? 7 : 4); i++)
                            Parts.Emit(PxKind.Smoke, floor + Random.insideUnitCircle * bw * 0.15f, bw * (0.35f + Random.value * 0.25f), Random.insideUnitCircle * 0.25f, 0.5f + Random.value * 0.4f,
                                1.2f + Random.value * 0.8f, 5f + Random.value * 5f, PixelRamp.Ash, 0f, 0.18f + Random.value * 0.2f);
                        if (ScorchOf(ramp) == PixelRamp.Ember)
                            for (int i = 0; i < (big ? 14 : 8); i++)
                                Parts.Emit(PxKind.Ember, floor + Iso(Random.value - 0.5f, Random.value - 0.5f) * bw * 0.7f, 0.02f, Vector2.zero, 0.4f + Random.value * 0.6f,
                                    0.6f + Random.value * 0.5f, 1f, PixelRamp.Ember, -0.2f, 0.3f + Random.value * 1.5f);
                    }
                }
                for (int i = 0; i < n; i++)
                {
                    float a = Random.value * Mathf.PI * 2f, s = radius * (2f + Random.value * 2.2f);
                    Parts.Emit(PxKind.Spark, floor, 0.2f, Iso(Mathf.Cos(a), Mathf.Sin(a)) * s, 0.5f + Random.value * 2.5f, 0.3f + Random.value * 0.25f, 1f, ramp, 5f);
                }
                if (ramp == PixelRamp.Ember || big)
                    for (int i = 0; i < n / 2; i++)
                        Parts.Emit(PxKind.Ember, floor + Iso(Random.value - 0.5f, Random.value - 0.5f) * radius * 1.6f, 0.1f, Vector2.zero, 0.5f + Random.value, 0.9f + Random.value * 0.8f, Random.value < 0.25f ? 2f : 1f, ramp, -0.3f, Random.value * 0.2f);
            }
            Debris(cell, PixelRamp.Ash, Mathf.Clamp(Mathf.RoundToInt(radius * 3f), 4, 14), radius * 0.9f);
            // Dust rolls out along the ring's edge.
            int puffs = Mathf.Clamp(Mathf.RoundToInt(radius * 2.5f), 3, 10);
            for (int i = 0; i < puffs; i++)
            {
                float a = i / (float)puffs * Mathf.PI * 2f + Random.value * 0.4f;
                var d = Iso(Mathf.Cos(a), Mathf.Sin(a));
                Parts.Emit(PxKind.Smoke, floor + d * radius * 0.7f, 0.05f, d * radius * 0.6f, 0.15f, 0.9f + Random.value * 0.6f, 4f + Random.value * 4f, PixelRamp.Ash, 0f, 0.05f);
            }
        }
    }
}
