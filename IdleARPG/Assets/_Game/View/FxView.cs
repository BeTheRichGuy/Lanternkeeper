using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Pooled effects (craft floor: short-lived, palette-coloured, never Instantiate per event):
    /// blast rings, pixel sparks, ash on death, footstep dust, embers drifting in the lantern light,
    /// a level-up banner, and short labels. Same resonance in one tick collapses to one label, e.g. "과부하 ×8" (GDD §17.1).
    /// </summary>
    public sealed class FxView
    {
        sealed class Ring { public LineRenderer Lr; public Vec2 Pos; public float T, Life, R0, R1; public Color Color; }
        sealed class Particle { public SpriteRenderer Sr; public Vector3 P, V; public float T, Life, Size, Gravity; public Color Color; }
        sealed class Label { public TextMesh Text; public MeshRenderer Mr; public Vec2 Pos; public float T, Life, Rise, Pop, Stack; public Color Color; public string Key; public int Count; }

        const int FxOrder = 31000, LabelOrder = 32000;
        const float PixelSize = 1f / 64f;

        readonly Transform _root;
        readonly SpriteCatalog _catalog;
        readonly Material _material;
        readonly Font _font;
        readonly List<Ring> _rings = new List<Ring>();

        /// <summary>A boss's drawn danger: a pulsing rim at full size and a fill that grows until it lands.</summary>
        sealed class Warn { public SpriteRenderer Rim, Fill; public Vec2 Pos; public float R, T, Life; public Color Color; }
        readonly List<Warn> _warns = new List<Warn>();

        void AddWarn(Vec2 pos, float radius, float life, Color color)
        {
            var w = _warns.Find(x => x.T >= x.Life);
            if (w == null)
            {
                w = new Warn { Rim = NewSprite("warn rim", "fx/ring"), Fill = NewSprite("warn fill", "fx/ring") };
                // 재기획 §4.1: an enemy's warning is drawn twice — on the floor under everything (up to 0.45) and faintly
                // over everything (0.2), so danger shows through any amount of the hero's own effects.
                w.Fill.sortingOrder = VfxPlayer.GroundOrder + 2;
                _warns.Add(w);
            }
            w.Pos = pos; w.R = radius; w.T = 0f; w.Life = Mathf.Max(0.2f, life); w.Color = color;
            w.Rim.enabled = w.Fill.enabled = true;
        }

        void RenderWarns(float dt)
        {
            foreach (var w in _warns)
            {
                if (w.T >= w.Life) continue;
                w.T += dt;
                float k = Mathf.Clamp01(w.T / w.Life);
                if (k >= 1f) { w.Rim.enabled = w.Fill.enabled = false; continue; }
                var at = IsoProjection.ToWorld(w.Pos);
                w.Rim.transform.localPosition = w.Fill.transform.localPosition = at;
                w.Rim.transform.localScale = new Vector3(w.R * 1.414f, w.R * 0.707f, 1f);
                float f = w.R * k;
                w.Fill.transform.localScale = new Vector3(f * 1.414f, f * 0.707f, 1f);
                // Steady bright rim; the fill grows to it; right before it lands, one white flash.
                bool flash = k > 0.86f && k < 0.94f;
                var c = flash ? Color.white : w.Color;
                c.a = flash ? 0.6f : 0.2f;
                w.Rim.color = c;
                var cf = flash ? Color.white : w.Color; cf.a = flash ? 0.6f : 0.2f + 0.25f * k;
                w.Fill.color = cf;
            }
        }
        readonly List<Particle> _parts = new List<Particle>();
        readonly List<Label> _labels = new List<Label>();
        readonly Dictionary<Resonance, (int n, Vec2 sum)> _tickRes = new Dictionary<Resonance, (int, Vec2)>();
        int _tickChainMax;
        Vec2 _tickChainPos;
        float _stepTimer, _moteTimer, _dripTimer;

        /// <summary>Act of the floor on screen (set by WorldView) and which cells are floor, for the act's ambience.</summary>
        public int Act;
        public System.Func<Vec2, bool> Walkable;
        static readonly Color Water = new Color(0.55f, 0.85f, 0.9f);
        static readonly Color Spore = new Color(0.6f, 1f, 0.7f);
        Vec2 _hero;

        static readonly Color Ash = new Color(0.55f, 0.52f, 0.5f);
        static readonly Color EmberC = new Color(1f, 0.55f, 0.2f);
        static readonly Color Dust = new Color(0.45f, 0.42f, 0.4f);

        public FxView(Transform root, SpriteCatalog catalog, Material material)
        {
            _root = root;
            _catalog = catalog;
            _material = material;
            _font = UiFont.Body;
        }

        public static Color ElementColor(Element e)
        {
            switch (e)
            {
                case Element.Ember: return new Color(1f, 0.55f, 0.2f);
                case Element.Frost: return new Color(0.6f, 0.85f, 1f);
                case Element.Shock: return new Color(0.95f, 0.95f, 0.45f);
                case Element.Rot: return new Color(0.55f, 0.85f, 0.35f);
                case Element.Void: return new Color(0.7f, 0.45f, 1f);
                default: return new Color(0.92f, 0.9f, 0.85f);
            }
        }

        /// <summary>GDD §26.6 환생 독백: one line per rebirth, the forgetting growing heavier.</summary>
        public static string RebirthLine(int rebirths)
        {
            switch (rebirths)
            {
                case 1: return "다시 처음부터. 그래도 이 불씨는 기억한다.";
                case 2: return "잊은 얼굴이 하나 늘었다. 등불은 그만큼 밝아졌다.";
                case 3: return "이름을 부르면 대답하던 이가 있었는데.";
                case 4: return "몇 번째인지 세지 않기로 했다.";
                default: return "심지는 짧아지고, 불은 깊어진다.";
            }
        }

        public static string ResonanceName(Resonance r)
        {
            switch (r)
            {
                case Resonance.Shatter: return "파쇄";
                case Resonance.Plague: return "역병 전도";
                case Resonance.Overload: return "과부하";
                case Resonance.Devour: return "포식";
                case Resonance.Collapse: return "붕괴";
                case Resonance.Stack: return "중첩";
                default: return "";
            }
        }

        // ---------- events ----------

        /// <param name="vfxDrawn">A VFX game pack already shows this event: keep sparks and labels, drop the flat ring.</param>
        public void OnEvent(SimEvent ev, bool vfxDrawn = false)
        {
            switch (ev.Type)
            {
                case SimEventType.Cast when ev.Value > 1.6f:
                    // The flat line ring is gone: VfxDirector draws the shockwave in pixels now (11-23).
                    Burst(ev.Pos, 10, ElementColor(ev.Elem), 1.6f, 0.35f, 0.2f, 0.3f);
                    break;
                case SimEventType.Hit when !ev.FromChain && ev.SourceId != 0:
                {
                    var blow = BlowDirs.For(ev, _hero); // bits fly out behind the enemy, the way the blow went
                    ElementDebris(ev.Pos, ev.Elem, blow);
                    // 고어: a blow draws a little blood (a dozen spurts a tick at most, so a big pack stays readable).
                    if (GameSettings.Blood && ev.Value > 0f && _tickSpurts++ < 12) BloodSpray(ev.Pos, 3, 0.9f, blow);
                    break;
                }
                case SimEventType.Death:
                    _tickDeaths++;
                    // 고어 (희승 11-22): the fallen burst, blood flying on the way the killing blow went, and leave a stain.
                    if (GameSettings.Blood)
                    {
                        BloodSpray(ev.Pos, 16, 1.7f, BlowDirs.For(ev, _hero));
                        Stain(ev.Pos);
                    }
                    // 11-23: the killing element's burst and the ash are VfxDirector's now (pixel embers, shards, smoke, debris);
                    // drawn here too they stacked into a haze of white dots over every pile (review 2).
                    switch (Act)
                    {
                        case 0: Burst(ev.Pos, 4, new Color(0.9f, 0.76f, 0.5f), 0.9f, 0.6f, 0.15f, 1.2f); break;   // sand spills and falls
                        case 1: Burst(ev.Pos, 4, new Color(0.55f, 0.8f, 0.35f), 0.8f, 0.9f, 0.3f, 0.4f); break;   // leaves drift down
                        case 2: if (!GameSettings.Blood) Burst(ev.Pos, 8, new Color(0.75f, 0.12f, 0.15f), 1.1f, 0.5f, 0.3f, 2.2f); break; // blood drops (gore draws its own)
                        case 3: Burst(ev.Pos, 4, new Color(0.8f, 0.95f, 1f), 1.2f, 0.5f, 0.3f, 1.8f); break;      // ice shards
                        case 4: Burst(ev.Pos, 3, EmberC, 0.8f, 0.8f, 0.3f, -0.9f); break;                         // embers rise
                        default: Burst(ev.Pos, 3, new Color(0.78f, 0.7f, 1f), 0.6f, 1.0f, 0.3f, -0.5f); break;   // dead starlight
                    }
                    break;
                case SimEventType.ChainPop:
                    if (!vfxDrawn) SpawnRing(ev.Pos, 0.1f, ev.Value, 0.35f, ElementColor(ev.Elem));
                    Burst(ev.Pos, 8, ElementColor(ev.Elem), 2.2f, 0.35f, 0.2f, 0.4f);
                    if (ev.Depth + 1 > _tickChainMax) { _tickChainMax = ev.Depth + 1; _tickChainPos = ev.Pos; }
                    break;
                case SimEventType.Resonance:
                    _tickRes.TryGetValue(ev.Res, out var acc);
                    _tickRes[ev.Res] = (acc.n + 1, acc.sum + ev.Pos);
                    break;
                case SimEventType.Unlocked:
                    AddLabel(ev.Pos, ev.SkillId, new Color(0.7f, 0.95f, 0.8f), life: 2.6f, rise: 0.22f, pop: 0.35f, big: true);
                    break;
                case SimEventType.Drop when ev.Item != null && ev.Item.GreaterCount > 0:
                    // ★ 대옵션: gold star-dust bursts where it lands, one ring per star.
                    Burst(ev.Pos, 24 + 8 * ev.Item.GreaterCount, new Color(1f, 0.85f, 0.4f), 1.8f, 1.2f, 0.4f, -0.5f);
                    for (int i = 0; i < ev.Item.GreaterCount; i++) SpawnRing(ev.Pos, 0.1f, 1.2f + 0.5f * i, 0.6f + 0.15f * i, new Color(1f, 0.85f, 0.4f, 0.8f));
                    break;
                case SimEventType.RuneDrop:
                    AddLabel(ev.Pos, "룬 · " + Runes.Name((Rune)(int)ev.Value), new Color(0.75f, 0.85f, 1f), life: 1.4f, rise: 0.4f, pop: 0.2f, big: false);
                    break;
                case SimEventType.Runeword:
                    AddLabel(ev.Pos, "룬어 · " + ev.SkillId, new Color(1f, 0.85f, 0.5f), life: 2.4f, rise: 0.3f, pop: 0.4f, big: true);
                    break;
                case SimEventType.Telegraph:
                    // One grammar for every danger (d4 §6.2): the act's accent, a bright rim, a filling disc, one flash.
                    // 11-23: VfxDirector draws the danger zone in pixels now (PixelFx Warn); the sprite rings stay unused.
                    break;
                case SimEventType.BossCast:
                    AddLabel(ev.Pos, !string.IsNullOrEmpty(ev.Text) ? ev.Text : ev.SkillId == "abbot_prayer" ? "익사의 기도" : ev.SkillId == "queen_children" ? "아이들아" : ev.SkillId == "giant_star" ? "새 별을 벼려라" : ev.SkillId == "apostle_halo" ? "재의 후광" : "죽은 자여, 일어나라",
                        ev.SkillId == "abbot_prayer" ? new Color(0.6f, 0.9f, 1f) : new Color(0.75f, 1f, 0.7f),
                        life: 2f, rise: 0.3f, pop: 0.4f, big: true);
                    Burst(ev.Pos, 40, ev.SkillId == "abbot_prayer" ? new Color(0.5f, 0.85f, 1f) : new Color(0.6f, 0.9f, 0.55f), 2.2f, 1.3f, 0.4f, -0.8f);
                    break;
                case SimEventType.Rebirth:
                    AddLabel(ev.Pos, "환생 · 잔불 " + (int)ev.Value, new Color(1f, 0.7f, 0.35f), life: 3.2f, rise: 0.25f, pop: 0.5f, big: true);
                    AddLabel(ev.Pos, RebirthLine(ev.Depth), new Color(0.85f, 0.8f, 0.72f), life: 4.5f, rise: 0.15f, pop: 0f, big: false);
                    SpawnRing(ev.Pos, 0.2f, 5f, 1.2f, new Color(1f, 0.7f, 0.35f));
                    Burst(ev.Pos, 80, EmberC, 3.5f, 1.8f, 0.5f, -1.2f);
                    break;
                // 새 기록 · 도전 과제 · 도감 메달 · 야영지 개방 are told in the notice list on the left (ToastView): over the
                // hero, several at once (a boss kill) piled into one unreadable heap (점검 11-22).
                case SimEventType.Ignited:
                    AddLabel(ev.Pos, "등불 점화", new Color(1f, 0.88f, 0.55f), life: 2f, rise: 0.3f, pop: 0.45f, big: true);
                    SpawnRing(ev.Pos, 0.2f, 3.2f, 0.7f, new Color(1f, 0.82f, 0.45f));
                    Burst(ev.Pos, 48, new Color(1f, 0.75f, 0.35f), 3f, 1.4f, 0.6f, 1.6f);
                    break;
                case SimEventType.GrandResonance:
                    AddCountedLabel("grand", ev.Pos, "대공명 · " + ResonanceName(Elements.Forward(ev.Elem)), ElementColor(Elements.Next(ev.Elem)), life: 1.8f, rise: 0.3f, pop: 0.4f);
                    break;
                case SimEventType.WickLevelUp:
                    AddLabel(ev.Pos, "심지 " + (int)ev.Value, new Color(1f, 0.9f, 0.6f), life: 2.2f, rise: 0.3f, pop: 0.4f, big: true);
                    break;
                case SimEventType.SkillLevelUp when (int)ev.Value >= 2:
                    // 숙련: every CopyEvery levels the skill also gains a copy — say so.
                    AddLabel(ev.Pos, (ev.Text ?? "") + " 숙련 " + (int)ev.Value + ((int)ev.Value % SkillBook.CopyEvery == 0 ? " · 분신 +1" : ""),
                        (int)ev.Value % SkillBook.CopyEvery == 0 ? new Color(1f, 0.85f, 0.45f) : new Color(0.75f, 0.9f, 1f), life: 1.6f, rise: 0.35f, pop: 0.25f, big: (int)ev.Value % SkillBook.CopyEvery == 0);
                    break;
                case SimEventType.Evolved:
                    AddLabel(ev.Pos, "진화 · " + (SkillBook.Get(ev.SkillId)?.Name ?? ev.SkillId), new Color(1f, 0.78f, 0.35f), life: 2.6f, rise: 0.25f, pop: 0.45f, big: true);
                    break;
                case SimEventType.Mastery:
                    AddLabel(ev.Pos, $"{ResonanceName(ev.Res)} 숙련 {((int)ev.Value == 3 ? "III" : (int)ev.Value == 2 ? "II" : "I")}", new Color(0.8f, 0.85f, 1f), life: 2.2f, rise: 0.3f, pop: 0.3f, big: true);
                    break;
                case SimEventType.LevelUp:
                    AddLabel(ev.Pos, "LEVEL " + (int)ev.Value, new Color(1f, 0.85f, 0.4f), life: 1.6f, rise: 0.25f, pop: 0.35f, big: true);
                    SpawnRing(ev.Pos, 0.1f, 2.2f, 0.5f, new Color(1f, 0.8f, 0.4f));
                    Burst(ev.Pos, 36, new Color(1f, 0.78f, 0.35f), 2.4f, 1.1f, 0.5f, 1.4f);
                    break;
                // 장착 is told in the notice list (ToastView, worthwhile upgrades only): auto-equip swaps several pieces at
                // once and their names piled over the hero (점검 11-22).
                case SimEventType.Rested:
                    if (ev.TargetId == -1) AddLabel(ev.Pos, "불사", new Color(1f, 0.55f, 0.4f), life: 1.8f, rise: 0.35f, pop: 0.45f, big: true);
                    else AddLabel(ev.Pos, "숨을 고른다", new Color(0.85f, 0.9f, 0.8f), life: 1.6f, rise: 0.3f, pop: 0.2f, big: false);
                    Burst(ev.Pos, 16, new Color(1f, 0.8f, 0.5f), 0.8f, 1.2f, 0.2f, -0.5f);
                    break;
                case SimEventType.ChestOpened:
                    if (ev.SkillId == "hidden_cache")
                        AddLabel(ev.Pos, "등불이 숨겨진 보물을 비췄다", new Color(1f, 0.85f, 0.5f), life: 2.4f, rise: 0.3f, pop: 0.3f, big: false);
                    SpawnRing(ev.Pos, 0.1f, 1.4f, 0.4f, new Color(1f, 0.85f, 0.45f));
                    Burst(ev.Pos, 24, new Color(1f, 0.85f, 0.45f), 2f, 0.8f, 0.3f, 1.2f);
                    break;
                case SimEventType.HeroDied:
                    AddLabel(ev.Pos, "등불이 꺼졌다", new Color(0.8f, 0.8f, 0.9f), life: 2f, rise: 0.2f, pop: 0f, big: true);
                    break;
            }
        }

        int _tickDeaths, _tickSpurts;

        // ---------- 고어 (희승 11-22: 적이 쓰러질 때 피를 뿜도록) ----------
        static readonly Color[] BloodShades = { new Color(0.55f, 0.03f, 0.05f), new Color(0.74f, 0.07f, 0.08f), new Color(0.36f, 0.02f, 0.04f) };

        sealed class StainMark { public SpriteRenderer Sr; public float T, Life; }
        readonly List<StainMark> _stains = new List<StainMark>();
        const int MaxStains = 48;
        const float StainLife = 25f, StainFade = 6f;

        /// <summary>Drops thrown up and away from the hero, falling back under gravity.</summary>
        void BloodSpray(Vec2 at, int count, float force, Vector2 dir = default)
        {
            var origin = IsoProjection.ToWorld(at) + new Vector3(0f, 0.3f, 0f);
            Vector3 away = dir != Vector2.zero ? (Vector3)dir : IsoProjection.ToWorld(at) - IsoProjection.ToWorld(_hero);
            float baseAngle = away.sqrMagnitude > 1e-4f ? Mathf.Atan2(away.y, away.x) : Mathf.PI / 2f;
            for (int i = 0; i < count; i++)
            {
                float a = baseAngle + (Random.value - 0.5f) * 1.8f, s = force * (0.5f + Random.value);
                var v = new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s * 0.5f + force * (0.6f + Random.value * 0.8f), 0f);
                Emit(origin, v, 0.45f + Random.value * 0.4f, Random.value < 0.35f ? 2f : 1f, 4.2f, BloodShades[Random.Range(0, BloodShades.Length)]);
            }
        }

        /// <summary>A blood stain where one fell (the floor's own blood decals), fading after half a minute; the oldest goes first.</summary>
        void Stain(Vec2 at)
        {
            string key = "decal/decal_blood_" + (char)('a' + Random.Range(0, 4));
            if (!_catalog.Has(key)) return;
            var st = _stains.Find(x => x.T >= x.Life);
            if (st == null)
            {
                if (_stains.Count < MaxStains)
                {
                    st = new StainMark { Sr = NewSprite("blood stain", key) };
                    st.Sr.sortingOrder = VfxPlayer.GroundOrder - 1; // on the floor, under the hero's ground effects
                    _stains.Add(st);
                }
                else
                {
                    st = _stains[0];
                    foreach (var x in _stains) if (x.T > st.T) st = x;
                }
            }
            st.Sr.sprite = _catalog.Get(key);
            st.Sr.flipX = Random.value < 0.5f;
            st.Sr.transform.localPosition = IsoProjection.ToWorld(at) + new Vector3((Random.value - 0.5f) * 0.2f, (Random.value - 0.5f) * 0.1f, 0f);
            st.Sr.transform.localScale = Vector3.one * (1.1f + Random.value * 0.5f);
            st.Sr.enabled = true;
            st.T = 0f;
            st.Life = StainLife;
        }

        void RenderStains(float dt)
        {
            foreach (var st in _stains)
            {
                if (st.T >= st.Life) continue;
                st.T += dt;
                if (st.T >= st.Life || !GameSettings.Blood) { st.T = st.Life; st.Sr.enabled = false; continue; }
                float left = st.Life - st.T;
                st.Sr.color = new Color(0.85f, 0.8f, 0.8f, 0.9f * Mathf.Clamp01(left / StainFade));
            }
        }

        /// <summary>A new floor: the old floor's stains go.</summary>
        public void ClearStains()
        {
            foreach (var st in _stains) { st.T = st.Life; st.Sr.enabled = false; }
        }
        readonly int[] _deathWindow = new int[6]; // the last 0.6 s of kills, a tick a slot
        int _windowAt;
        float _massCooldown;

        /// <summary>Set for the tick a mass kill was called (WorldView shakes the screen a little).</summary>
        public bool MassKill { get; private set; }

        public void EndTick()
        {
            // 몰살: a dozen or more down within 0.6 s is the hunt at its best — say it once, big.
            _deathWindow[_windowAt] = _tickDeaths;
            _windowAt = (_windowAt + 1) % _deathWindow.Length;
            _tickDeaths = 0;
            _tickSpurts = 0;
            int recent = 0;
            foreach (int n in _deathWindow) recent += n;
            MassKill = false;
            _massCooldown -= 0.1f;
            if (recent >= 12 && _massCooldown <= 0f)
            {
                _massCooldown = 2.5f;
                MassKill = true;
                System.Array.Clear(_deathWindow, 0, _deathWindow.Length);
                AddLabel(_hero, (recent >= 30 ? "대학살" : "학살") + " ×" + recent, recent >= 30 ? new Color(1f, 0.9f, 0.55f) : new Color(1f, 0.72f, 0.4f),
                    life: 1.8f, rise: 0.35f, pop: 0.5f, big: true);
            }
            foreach (var kv in _tickRes)
            {
                // Only the named (forward) resonances get a word: 중첩 and the weak ones crowded every fight (점검 11-22).
                if (!Elements.IsForward(kv.Key)) continue;
                var (n, sum) = kv.Value;
                // One running line per resonance instead of a new line every tick (a late fight printed "중첩 ×113"
                // ten times a second over itself): while it is up the count adds and the line stays a little longer.
                string name = ResonanceName(kv.Key), key = "res:" + name;
                var live = _labels.Find(x => x.Key == key && x.T < x.Life);
                if (live != null)
                {
                    live.Count += n;
                    live.Text.text = $"{name} ×{live.Count}";
                    live.Life = Mathf.Max(live.Life, live.T + 0.6f);
                    continue;
                }
                var l = AddLabel(sum * (1f / n), n > 1 ? $"{name} ×{n}" : name, new Color(1f, 0.92f, 0.8f), life: 1.1f, rise: 0.5f, pop: 0.15f, big: false);
                l.Key = key; l.Count = n;
            }
            _tickRes.Clear();
            // The chain count once per tick: hotter and bigger the deeper it went (사망 전파 is the game's own thrill).
            if (_tickChainMax >= 2)
            {
                // One running chain line: while it is up a deeper chain raises it in place (review 11-23: "연쇄 2" lines
                // stacking every tick buried the fight under text).
                var chain = _labels.Find(x => x.Key == "chain" && x.T < x.Life);
                if (chain != null)
                {
                    if (_tickChainMax > chain.Count)
                    {
                        chain.Count = _tickChainMax;
                        chain.Text.text = _tickChainMax >= 5 ? $"연쇄 ×{_tickChainMax}!" : $"연쇄 {_tickChainMax}";
                        chain.Color = _tickChainMax >= 8 ? new Color(1f, 0.95f, 0.6f) : _tickChainMax >= 5 ? new Color(1f, 0.75f, 0.3f) : new Color(1f, 0.6f, 0.3f);
                    }
                    chain.Life = Mathf.Max(chain.Life, chain.T + 0.5f);
                }
                else
                {
                    var l = AddLabel(_tickChainPos, _tickChainMax >= 5 ? $"연쇄 ×{_tickChainMax}!" : $"연쇄 {_tickChainMax}",
                        _tickChainMax >= 8 ? new Color(1f, 0.95f, 0.6f) : _tickChainMax >= 5 ? new Color(1f, 0.75f, 0.3f) : new Color(1f, 0.6f, 0.3f),
                        life: 1.1f + 0.1f * Mathf.Min(_tickChainMax, 8), rise: 0.5f, pop: 0.2f + 0.04f * Mathf.Min(_tickChainMax, 8), big: _tickChainMax >= 5);
                    l.Key = "chain"; l.Count = _tickChainMax;
                }
            }
            _tickChainMax = 0;
        }

        // ---------- spawning ----------

        void SpawnRing(Vec2 pos, float r0, float r1, float life, Color color)
        {
            var fx = _rings.Find(f => f.T >= f.Life);
            if (fx == null)
            {
                fx = new Ring { Lr = NewRingLine() };
                _rings.Add(fx);
            }
            fx.Pos = pos; fx.T = 0f; fx.Life = life; fx.R0 = r0; fx.R1 = r1; fx.Color = color;
            fx.Lr.enabled = true;
        }

        const int RingPoints = 48;
        readonly Vector3[] _ringPts = new Vector3[RingPoints];

        /// <summary>
        /// 재기획 §4: rings are a thin line on the floor under the units (about 1.5 art px whatever the radius), not a
        /// thick sprite ring scaled up over everything — a big cast used to paint a fat white ellipse across the fight.
        /// </summary>
        LineRenderer NewRingLine()
        {
            var lr = new GameObject("ring").AddComponent<LineRenderer>();
            lr.transform.SetParent(_root, false);
            lr.sharedMaterial = _material;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = RingPoints;
            lr.widthMultiplier = 1.5f / 64f;
            lr.numCapVertices = 0;
            lr.sortingOrder = VfxPlayer.GroundOrder + 1;
            return lr;
        }

        /// <summary>
        /// 재기획 §4.6 원소 문법: each element's debris moves its own way, so a hit reads by motion as well as colour.
        /// Ember sparks float up, frost shards burst and fall, shock flicks fast and dies, rot drips slowly, void is drawn in.
        /// Physical chips fly away from the hero.
        /// </summary>
        void ElementDebris(Vec2 at, Element e, Vector2 dir)
        {
            var origin = IsoProjection.ToWorld(at) + new Vector3(0f, 0.4f, 0f);
            Vector3 away = dir != Vector2.zero ? (Vector3)dir : IsoProjection.ToWorld(at) - IsoProjection.ToWorld(_hero);
            away.z = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.right;
            switch (e)
            {
                case Element.Ember:
                    for (int i = 0; i < 5; i++)
                        Emit(origin + new Vector3(Random.Range(-0.12f, 0.12f), 0f, 0f), away * Random.Range(0.3f, 0.7f) + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.6f, 1.2f), 0f),
                            Random.Range(0.35f, 0.6f), Random.value < 0.3f ? 2f : 1f, -0.8f, Random.value < 0.35f ? new Color(1f, 0.95f, 0.7f) : new Color(1f, 0.55f, 0.2f));
                    break;
                case Element.Frost:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Mathf.Atan2(away.y, away.x) + Random.Range(-1.1f, 1.1f), s = Random.Range(0.8f, 1.6f); // a fan behind the enemy
                        Emit(origin, new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s * 0.6f + 0.6f, 0f), Random.Range(0.3f, 0.45f), Random.value < 0.4f ? 2f : 1f, 4f, new Color(0.75f, 0.92f, 1f));
                    }
                    break;
                case Element.Shock:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = Mathf.Atan2(away.y, away.x) + Random.Range(-1.2f, 1.2f);
                        Emit(origin, new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f, 0f) * 3f, 0.1f, 1f, 0f, new Color(1f, 0.98f, 0.75f));
                    }
                    break;
                case Element.Rot:
                    for (int i = 0; i < 3; i++)
                        Emit(origin + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0f, 0.1f), 0f), new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.1f, 0.3f), 0f),
                            Random.Range(0.6f, 0.9f), Random.value < 0.5f ? 2f : 1f, 1.4f, new Color(0.55f, 0.75f, 0.35f));
                    break;
                case Element.Void:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f + Random.value * 0.5f;
                        var off = new Vector3(Mathf.Cos(a) * 0.4f, Mathf.Sin(a) * 0.24f, 0f);
                        Emit(origin + off, -off / 0.14f, 0.14f, 1f, 0f, new Color(0.72f, 0.5f, 1f));
                    }
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                    {
                        var d = Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f)) * away;
                        Emit(origin, d * Random.Range(1f, 1.6f) + new Vector3(0f, 0.4f, 0f), 0.25f, 1f, 3f, new Color(0.9f, 0.86f, 0.78f));
                    }
                    break;
            }
        }

        /// <summary>Pixel particles: speed in world units/s, gravity &gt; 0 falls, &lt; 0 rises.</summary>
        void Burst(Vec2 at, int count, Color color, float speed, float life, float height, float gravity)
        {
            var origin = IsoProjection.ToWorld(at) + new Vector3(0f, height, 0f);
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f, s = speed * (0.4f + 0.6f * Random.value);
                var v = new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s * 0.6f + speed * 0.3f, 0f);
                Emit(origin, v, life * (0.7f + 0.6f * Random.value), Random.value < 0.3f ? 2f : 1f, gravity, color);
            }
        }

        void Emit(Vector3 p, Vector3 v, float life, float sizePx, float gravity, Color color)
        {
            var pt = _parts.Find(x => x.T >= x.Life);
            if (pt == null)
            {
                if (_parts.Count >= 400) return; // hard cap: the overlay must stay light
                pt = new Particle { Sr = NewSprite("px", "ui/dot") };
                _parts.Add(pt);
            }
            pt.P = p; pt.V = v; pt.T = 0f; pt.Life = life; pt.Size = sizePx; pt.Gravity = gravity; pt.Color = color;
            pt.Sr.enabled = true;
        }

        SpriteRenderer NewSprite(string name, string key)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(_root, false);
            sr.sharedMaterial = _material;
            sr.sprite = _catalog.Get(key);
            sr.sortingOrder = FxOrder;
            return sr;
        }

        /// <summary>
        /// A label that repeats fast late in the run (대공명 every few seconds): while one with this key is still up it
        /// counts up (×N) and restarts instead of stacking a new line on top.
        /// </summary>
        void AddCountedLabel(string key, Vec2 pos, string text, Color color, float life, float rise, float pop)
        {
            var live = _labels.Find(x => x.Key == key && x.T < x.Life);
            if (live != null)
            {
                live.Count++;
                live.Text.text = text + "  ×" + live.Count;
                live.Color = color;
                live.Pos = pos; // follows the hero, who walked on since the last one
                live.Life = Mathf.Max(live.Life, live.T + life); // stays up longer; no pop, no drop
                return;
            }
            var l = AddLabel(pos, text, color, life, rise, pop, big: true);
            l.Key = key; l.Count = 1;
        }

        Label AddLabel(Vec2 pos, string text, Color color, float life, float rise, float pop, bool big)
        {
            // At most eight words over the fight: the oldest small one makes room (review 11-23: late fights were buried
            // under 학살, 대공명, 연쇄 and rune lines all at once).
            int live = 0;
            Label oldest = null;
            foreach (var x in _labels)
                if (x.T < x.Life)
                {
                    live++;
                    if (x.Text.characterSize < 0.07f && (oldest == null || x.T > oldest.T)) oldest = x;
                }
            if (live >= 8 && oldest != null) { oldest.T = oldest.Life; oldest.Mr.enabled = false; }
            var l = _labels.Find(x => x.T >= x.Life);
            if (l == null)
            {
                var go = new GameObject("label");
                go.transform.SetParent(_root, false);
                var tm = go.AddComponent<TextMesh>();
                tm.font = _font;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = _font.material;
                mr.sortingOrder = LabelOrder;
                l = new Label { Text = tm, Mr = mr };
                _labels.Add(l);
            }
            l.Text.text = text;
            l.Text.characterSize = big ? 0.08f : 0.055f;
            l.Color = color;
            l.Pos = pos; l.T = 0f; l.Life = life; l.Rise = rise; l.Pop = pop; l.Key = null; l.Count = 0;
            // Stack labels that spawn close together so they never print over each other.
            // Each one below takes its own line height (a big line is ~0.4 tall: a fixed 0.28 let them overlap).
            float stack = 0f;
            foreach (var other in _labels)
                if (other != l && other.T < 0.6f && other.T < other.Life && Vec2.DistSq(other.Pos, pos) < 4f)
                    stack += other.Text.characterSize >= 0.08f ? 0.44f : 0.32f;
            l.Stack = stack;
            l.Mr.enabled = true;
            return l;
        }

        // ---------- per frame ----------

        public void Render(float dt, Vec2 heroPos, bool heroMoving)
        {
            RenderWarns(dt);
            RenderStains(dt);
            _hero = heroPos;
            var heroWorld = IsoProjection.ToWorld(heroPos);

            // Footstep dust while walking.
            if (heroMoving && (_stepTimer -= dt) <= 0f)
            {
                _stepTimer = 0.28f;
                for (int i = 0; i < 3; i++)
                    Emit(heroWorld + new Vector3((Random.value - 0.5f) * 0.25f, 0.02f, 0f),
                        new Vector3((Random.value - 0.5f) * 0.4f, 0.25f, 0f), 0.35f, 1f, 0.6f, Dust);
            }
            // Embers and ash drifting through the lantern light (ambient motes).
            if ((_moteTimer -= dt) <= 0f)
            {
                _moteTimer = 0.25f;
                var o = heroWorld + new Vector3((Random.value - 0.5f) * 6f, (Random.value - 0.5f) * 3f, 0f);
                // Each act its own air: sand dust, fireflies, red drips, snow, sparks, stardust.
                Color mote = Act == 0 ? (Random.value < 0.6f ? new Color(0.95f, 0.85f, 0.65f) : new Color(1f, 0.95f, 0.85f))
                    : Act == 1 ? (Random.value < 0.5f ? new Color(0.8f, 1f, 0.45f) : new Color(1f, 0.92f, 0.55f))
                    : Act == 2 ? (Random.value < 0.5f ? new Color(0.85f, 0.25f, 0.25f) : new Color(0.95f, 0.85f, 0.8f))
                    : Act == 3 ? new Color(0.92f, 0.97f, 1f)
                    : Act == 4 ? (Random.value < 0.6f ? EmberC : new Color(1f, 0.8f, 0.4f))
                    : (Random.value < 0.5f ? new Color(0.85f, 0.82f, 1f) : new Color(0.62f, 0.55f, 0.95f));
                // Snow falls, red motes sink, sand drifts sideways, the rest rises.
                var vel = Act == 3 ? new Vector3(0.1f + (Random.value - 0.5f) * 0.1f, -0.15f - Random.value * 0.1f, 0f)
                    : Act == 2 ? new Vector3((Random.value - 0.5f) * 0.05f, -0.06f - Random.value * 0.05f, 0f)
                    : Act == 0 ? new Vector3(0.18f + Random.value * 0.12f, 0.02f + Random.value * 0.04f, 0f)
                    : new Vector3((Random.value - 0.5f) * 0.12f, 0.12f + Random.value * 0.1f, 0f);
                Emit(o, vel, 3.5f, 1f, 0f, mote);
                // 밀림: fireflies drift up thick from the floor around the hero.
                if (Act == 1)
                    for (int k = 0; k < 3; k++)
                    {
                        // Only off real floor: spores rising out of the black void read wrong.
                        var cell = new Vec2(heroPos.X + (Random.value - 0.5f) * 16f, heroPos.Y + (Random.value - 0.5f) * 16f);
                        if (Walkable != null && !Walkable(cell)) continue;
                        Emit(IsoProjection.ToWorld(cell), new Vector3((Random.value - 0.5f) * 0.08f, 0.08f + Random.value * 0.12f, 0f),
                            4.5f, Random.value < 0.25f ? 2f : 1f, -0.02f, Random.value < 0.6f ? new Color(0.8f, 1f, 0.45f) : Spore);
                    }
            }
            // 핏덩이 소굴: drops fall from the flesh ceiling and ring out on the wet floor.
            if (Act == 2 && (_dripTimer -= dt) <= 0f)
            {
                _dripTimer = 0.35f + Random.value * 0.5f;
                var at = new Vec2(heroPos.X + (Random.value - 0.5f) * 14f, heroPos.Y + (Random.value - 0.5f) * 14f);
                if (Walkable == null || Walkable(at))
                {
                    var drop = new Color(0.75f, 0.18f, 0.2f);
                    SpawnRing(at, 0.04f, 0.3f + Random.value * 0.2f, 0.9f + Random.value * 0.5f, new Color(drop.r, drop.g, drop.b, 0.45f));
                    Burst(at, 3, drop, 0.5f, 0.35f, 0.05f, 2.5f);
                }
            }

            foreach (var fx in _rings)
            {
                if (fx.T >= fx.Life) continue;
                fx.T += dt;
                float k = Mathf.Clamp01(fx.T / fx.Life);
                if (k >= 1f) { fx.Lr.enabled = false; continue; }
                // Ring radius is in grid cells: squash vertically for the 2:1 ground plane (same size as the old
                // 0.5-unit ring sprite scaled r×1.414 by r×0.707).
                float r = Mathf.Lerp(fx.R0, fx.R1, 1f - (1f - k) * (1f - k));
                fx.Lr.transform.localPosition = IsoProjection.ToWorld(fx.Pos);
                for (int i = 0; i < RingPoints; i++)
                {
                    float a = i * Mathf.PI * 2f / RingPoints;
                    _ringPts[i] = new Vector3(Mathf.Cos(a) * r * 0.354f, Mathf.Sin(a) * r * 0.177f, 0f);
                }
                fx.Lr.SetPositions(_ringPts);
                var c = fx.Color; c.a = 0.7f * (1f - k);
                fx.Lr.startColor = c; fx.Lr.endColor = c;
            }

            foreach (var p in _parts)
            {
                if (p.T >= p.Life) continue;
                p.T += dt;
                if (p.T >= p.Life) { p.Sr.enabled = false; continue; }
                p.V.y -= p.Gravity * dt;
                p.V *= 1f - 1.5f * dt; // drag
                p.P += p.V * dt;
                float k = p.T / p.Life;
                // Snap to the pixel grid so particles stay crisp.
                var snapped = new Vector3(Mathf.Round(p.P.x / PixelSize) * PixelSize, Mathf.Round(p.P.y / PixelSize) * PixelSize, 0f);
                p.Sr.transform.localPosition = snapped;
                float size = p.Size * (k > 0.7f ? 1f - (k - 0.7f) / 0.3f * 0.5f : 1f);
                p.Sr.transform.localScale = Vector3.one * Mathf.Max(0.5f, size) * 0.5f; // dot sprite is 2 px
                var c = p.Color;
                c.a = 1f - k * k;
                p.Sr.color = c;
            }

            foreach (var l in _labels)
            {
                if (l.T >= l.Life) continue;
                l.T += dt;
                if (l.T >= l.Life) { l.Mr.enabled = false; continue; }
                var p = IsoProjection.ToWorld(l.Pos);
                float e = Mathf.Clamp01(l.T / 0.35f);
                float popScale = l.Pop > 0f ? OutBack(e) : 1f;
                l.Text.transform.localPosition = p + new Vector3(0f, 1.1f + l.Stack + Mathf.Min(l.T, 0.6f) * l.Rise, 0f);
                l.Text.transform.localScale = Vector3.one * Mathf.Max(0.01f, popScale);
                var c = l.Color; c.a = 1f - Mathf.Clamp01((l.T - (l.Life - 0.4f)) / 0.4f);
                l.Text.color = c;
            }
        }

        static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
