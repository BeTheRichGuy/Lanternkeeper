using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Draws a <see cref="SimWorld"/>: floor tiles, back walls, props, units and their animations.
    /// Lighting is real URP 2D lights (<see cref="LightRig"/>): world sprites use the lit material,
    /// FX and drop beams stay unlit so they glow in the dark.
    /// </summary>
    public sealed class WorldView
    {
        const int FloorOrder = -30000;

        readonly Transform _root;
        readonly SimWorld _world;
        readonly SpriteCatalog _catalog;
        readonly Material _material, _litMaterial, _floorMat, _wallMat;
        public readonly LightRig Lights;
        /// <summary>Floor fog per act (rendering report §3.2 대기), lit so it shows only where light reaches.</summary>
        public readonly FogView Fog;

        Floor _builtFor;
        Transform _tileRoot;
        readonly List<SpriteRenderer> _tilePool = new List<SpriteRenderer>();

        /// <summary>Temporary looks for ranks without their own art yet: reuse the ghoul, tinted and scaled.</summary>
        static readonly Dictionary<string, (Color tint, float scale)> StandIns = new Dictionary<string, (Color, float)>
        {
            { "villager", (new Color(0.80f, 0.78f, 0.74f), 0.95f) },
            { "skeleton", (new Color(0.88f, 0.86f, 0.78f), 0.95f) },
            { "archer",   (new Color(0.82f, 0.84f, 0.80f), 0.92f) },
            { "wraith",   (new Color(0.60f, 0.70f, 0.95f), 1.05f) },
            { "brute",    (new Color(0.92f, 0.62f, 0.55f), 1.25f) },
            { "grave_hound",       (new Color(0.55f, 0.5f, 0.45f), 0.8f) },
            { "candle_priest",     (new Color(0.75f, 0.6f, 0.95f), 1.0f) },
            { "bone_shieldbearer", (new Color(0.9f, 0.88f, 0.8f), 1.1f) },
            { "plague_rat_swarm",  (new Color(0.55f, 0.7f, 0.4f), 0.7f) },
            // 막 II 가라앉은 수도원: drowned teal until act2-assets lands.
            { "drowned_monk",   (new Color(0.62f, 0.85f, 0.85f), 1.0f) },
            { "mire_eel",       (new Color(0.45f, 0.7f, 0.6f), 0.75f) },
            { "choir_wraith",   (new Color(0.7f, 0.85f, 1f), 1.0f) },
            { "penitent",       (new Color(0.85f, 0.7f, 0.65f), 0.95f) },
            { "tide_acolyte",   (new Color(0.5f, 0.8f, 0.95f), 1.0f) },
            { "bell_ringer",    (new Color(0.7f, 0.75f, 0.7f), 1.2f) },
            { "abbey_guardian", (new Color(0.6f, 0.8f, 0.8f), 1.25f) },
            { "drowned_abbot",  (new Color(0.6f, 0.85f, 0.9f), 1f) },
            // 막 III 균사 왕국 — mossy green until act3-assets lands.
            { "spore_thrall",      (new Color(0.7f, 0.9f, 0.6f), 1.0f) },
            { "shroom_crawler",    (new Color(0.75f, 0.85f, 0.55f), 0.75f) },
            { "mycelium_weaver",   (new Color(0.8f, 0.95f, 0.75f), 1.0f) },
            { "bloated_carrier",   (new Color(0.65f, 0.8f, 0.5f), 1.25f) },
            { "fungal_stalker",    (new Color(0.6f, 0.85f, 0.65f), 1.0f) },
            { "lantern_moth",      (new Color(0.95f, 0.9f, 0.6f), 0.7f) },
            { "mycelial_colossus", (new Color(0.6f, 0.8f, 0.55f), 1.3f) },
            { "spore_queen",       (new Color(0.75f, 0.95f, 0.7f), 1f) },
            // 막 IV 녹은 대장간 — soot and ember until act4-assets lands.
            { "slag_worker",    (new Color(0.85f, 0.65f, 0.55f), 1.0f) },
            { "ember_imp",      (new Color(1f, 0.6f, 0.4f), 0.7f) },
            { "bellows_priest", (new Color(0.95f, 0.7f, 0.55f), 1.0f) },
            { "iron_golem",     (new Color(0.7f, 0.65f, 0.65f), 1.3f) },
            { "forge_hound",    (new Color(1f, 0.55f, 0.4f), 0.9f) },
            { "ash_wisp",       (new Color(1f, 0.75f, 0.5f), 0.7f) },
            { "guild_enforcer", (new Color(0.85f, 0.6f, 0.5f), 1.3f) },
            { "molten_giant",   (new Color(1f, 0.6f, 0.4f), 1.1f) },
            // 막 V 별 없는 심연 — starless indigo until act5-assets lands.
            { "fallen_pilgrim",    (new Color(0.75f, 0.72f, 0.95f), 1.0f) },
            { "void_crawler",      (new Color(0.65f, 0.55f, 0.9f), 0.75f) },
            { "starless_seer",     (new Color(0.8f, 0.75f, 1f), 1.0f) },
            { "husk_titan",        (new Color(0.7f, 0.7f, 0.85f), 1.3f) },
            { "abyss_stalker",     (new Color(0.6f, 0.6f, 0.95f), 0.95f) },
            { "star_shard",        (new Color(0.95f, 0.9f, 1f), 0.65f) },
            { "ash_herald",        (new Color(0.8f, 0.78f, 0.9f), 1.3f) },
            { "dead_star_apostle", (new Color(0.78f, 0.72f, 1f), 1.15f) },
            // 막 I 모래 — bleached sand until sand-assets lands.
            { "bandaged_wanderer", (new Color(0.96f, 0.88f, 0.72f), 1.0f) },
            { "scarab_swarm",      (new Color(0.75f, 0.8f, 0.55f), 0.75f) },
            { "shroud_wraith",     (new Color(0.98f, 0.92f, 0.8f), 1.0f) },
            { "sandstone_sentinel", (new Color(0.92f, 0.78f, 0.58f), 1.15f) },
            { "sand_wolf",         (new Color(0.9f, 0.78f, 0.6f), 0.9f) },
            { "sand_lurker",       (new Color(0.85f, 0.75f, 0.55f), 0.8f) },
            { "bronze_veiled_general", (new Color(0.95f, 0.75f, 0.5f), 1.3f) },
            { "sarcophagus_king",  (new Color(1f, 0.88f, 0.62f), 1.05f) },
            // 막 II 밀림 — leaf green.
            { "rootwalker",        (new Color(0.72f, 0.9f, 0.6f), 1.0f) },
            { "vine_hanger",       (new Color(0.6f, 0.85f, 0.5f), 0.8f) },
            { "flowerface_witch",  (new Color(0.95f, 0.8f, 0.9f), 1.0f) },
            { "spore_host",        (new Color(0.75f, 0.85f, 0.55f), 1.25f) },
            { "thorn_boar",        (new Color(0.75f, 0.7f, 0.5f), 0.95f) },
            { "leafmail_hunter",   (new Color(0.65f, 0.9f, 0.6f), 1.0f) },
            { "antler_knight",     (new Color(0.75f, 0.85f, 0.6f), 1.3f) },
            { "crowned_great_tree", (new Color(0.7f, 0.95f, 0.6f), 1.1f) },
            // 막 III 핏덩이 — monsters stay pale against the dark red floor.
            { "flesh_crawler",     (new Color(0.98f, 0.85f, 0.82f), 1.0f) },
            { "wailing_brood",     (new Color(1f, 0.9f, 0.88f), 0.75f) },
            { "sinew_summoner",    (new Color(0.95f, 0.88f, 0.85f), 1.0f) },
            { "hook_butcher",      (new Color(0.95f, 0.82f, 0.78f), 1.25f) },
            { "blood_hound",       (new Color(0.95f, 0.8f, 0.78f), 0.9f) },
            { "bone_puppet",       (new Color(1f, 0.96f, 0.9f), 0.95f) },
            { "fleshmail_warden",  (new Color(0.98f, 0.85f, 0.8f), 1.3f) },
            { "pulse_lord",        (new Color(1f, 0.88f, 0.86f), 1.05f) },
            // 막 IV 얼음 — frost blue.
            { "frost_wanderer",    (new Color(0.82f, 0.92f, 1f), 1.0f) },
            { "icicle_spider",     (new Color(0.75f, 0.9f, 1f), 0.8f) },
            { "frozen_choir",      (new Color(0.88f, 0.95f, 1f), 1.0f) },
            { "icemail_soldier",   (new Color(0.75f, 0.88f, 1f), 1.15f) },
            { "frost_bear",        (new Color(0.9f, 0.95f, 1f), 1.0f) },
            { "blizzard_wraith",   (new Color(0.9f, 0.97f, 1f), 1.0f) },
            { "frost_lord_knight", (new Color(0.78f, 0.9f, 1f), 1.3f) },
            { "giant_in_ice",      (new Color(0.8f, 0.93f, 1f), 1.1f) },
        };

        /// <summary>Stand-ins that borrow a closer silhouette than the ghoul.</summary>
        static readonly Dictionary<string, string> StandInBase = new Dictionary<string, string>
        {
            { "drowned_monk", "enemy/villager" }, { "choir_wraith", "enemy/wraith" }, { "tide_acolyte", "enemy/wraith" },
            { "bell_ringer", "enemy/brute" }, { "abbey_guardian", "enemy/brute" }, { "drowned_abbot", "enemy/gravelord" },
            { "spore_thrall", "enemy/drowned_monk" }, { "mycelium_weaver", "enemy/choir_wraith" }, { "bloated_carrier", "enemy/bell_ringer" },
            { "fungal_stalker", "enemy/penitent" }, { "shroom_crawler", "enemy/mire_eel" }, { "lantern_moth", "enemy/wraith" },
            { "mycelial_colossus", "enemy/abbey_guardian" }, { "spore_queen", "enemy/drowned_abbot" },
            { "slag_worker", "enemy/spore_thrall" }, { "bellows_priest", "enemy/mycelium_weaver" }, { "iron_golem", "enemy/bloated_carrier" },
            { "forge_hound", "enemy/fungal_stalker" }, { "ember_imp", "enemy/shroom_crawler" }, { "ash_wisp", "enemy/wraith" },
            { "guild_enforcer", "enemy/abbey_guardian" }, { "molten_giant", "enemy/gravelord" },
            { "fallen_pilgrim", "enemy/drowned_monk" }, { "starless_seer", "enemy/choir_wraith" }, { "husk_titan", "enemy/bloated_carrier" },
            { "abyss_stalker", "enemy/fungal_stalker" }, { "void_crawler", "enemy/shroom_crawler" }, { "star_shard", "enemy/lantern_moth" },
            { "ash_herald", "enemy/mycelial_colossus" }, { "dead_star_apostle", "enemy/spore_queen" },
            { "bandaged_wanderer", "enemy/villager" }, { "scarab_swarm", "enemy/plague_rat_swarm" }, { "shroud_wraith", "enemy/wraith" },
            { "sandstone_sentinel", "enemy/bone_shieldbearer" }, { "sand_wolf", "enemy/grave_hound" }, { "sand_lurker", "enemy/shroom_crawler" },
            { "bronze_veiled_general", "enemy/brute" }, { "sarcophagus_king", "enemy/gravelord" },
            { "rootwalker", "enemy/spore_thrall" }, { "vine_hanger", "enemy/mire_eel" }, { "flowerface_witch", "enemy/mycelium_weaver" },
            { "spore_host", "enemy/bloated_carrier" }, { "thorn_boar", "enemy/grave_hound" }, { "leafmail_hunter", "enemy/fungal_stalker" },
            { "antler_knight", "enemy/mycelial_colossus" }, { "crowned_great_tree", "enemy/spore_queen" },
            { "flesh_crawler", "enemy/ghoul" }, { "wailing_brood", "enemy/plague_rat_swarm" }, { "sinew_summoner", "enemy/candle_priest" },
            { "hook_butcher", "enemy/brute" }, { "blood_hound", "enemy/grave_hound" }, { "bone_puppet", "enemy/skeleton" },
            { "fleshmail_warden", "enemy/abbey_guardian" }, { "pulse_lord", "enemy/drowned_abbot" },
            { "frost_wanderer", "enemy/fallen_pilgrim" }, { "icicle_spider", "enemy/void_crawler" }, { "frozen_choir", "enemy/choir_wraith" },
            { "icemail_soldier", "enemy/bone_shieldbearer" }, { "frost_bear", "enemy/forge_hound" }, { "blizzard_wraith", "enemy/wraith" },
            { "frost_lord_knight", "enemy/guild_enforcer" }, { "giant_in_ice", "enemy/molten_giant" },
        };

        static readonly int FlashId = Shader.PropertyToID("_Flash"), DissolveId = Shader.PropertyToID("_Dissolve"), RimId = Shader.PropertyToID("_RimColor");

        sealed class UnitView
        {
            public SpriteRenderer Sr, Shadow, Ring;
            public string Key;
            public Color Tint = Color.white;
            public float BaseScale = 1f;
            public float Flash, Squash, Fade = 1f, DeadFor;
            /// <summary>타격감 (docs/research/1122_hit_feel.md): flash colour, knockback (view only), the killing element and the corpse's fling.</summary>
            public Color FlashCol = Color.white;
            public float FlashLen = 0.06f;
            public bool FlashSet;
            public MaterialPropertyBlock Mpb;
            public Vector3 Kick, Fling;
            public float KickT;
            public Element DeathElem;
            public bool DissolveSet, RimSet;
            /// <summary>Unscaled time before which a new hit does not flash again (재기획 §4.3).</summary>
            public float FlashReadyAt;
            /// <summary>재기획 §4.3 대상 국소 정지: seconds the unit's animation holds on the hit frame, jittering a pixel.</summary>
            public float Freeze;
            public bool Boss;
            /// <summary>The status worn (PVFX loop: burning, frozen, poisoned, stunned) and how long it has played.</summary>
            public SpriteRenderer Status;
            public string StatusId;
            public float StatusT;
            public string Anim = "idle";
            public float AnimT, AnimSpeed = 1f;
            public bool AnimLoop = true, AnimDone;
            public float Phase;

            public void Play(string anim, bool loop, float speed = 1f)
            {
                if (anim == Anim && loop && AnimLoop) return;
                Anim = anim; AnimLoop = loop; AnimSpeed = speed; AnimT = 0f; AnimDone = false;
            }
        }
        readonly Dictionary<int, UnitView> _units = new Dictionary<int, UnitView>();

        public readonly FxView Fx;
        public readonly VfxPlayer Vfx;
        /// <summary>Procedural pixel layers over every blow, cast and death (희승 11-23: VFX가 너무 단순해).</summary>
        public readonly VfxDirector Director;
        readonly DropView _drops;
        /// <summary>Camera shake strength (world units), decays every frame.</summary>
        public float Shake;
        /// <summary>0..1, set by a big blow and gone in a quarter second: the camera's bloom swells with it (GameBootstrap).</summary>
        public float BloomKick;

        /// <summary>
        /// Hit-stop: the big moments play in slow motion for a beat (grand resonance, an act boss falling, a deep
        /// chain). Only the screen slows; the Sim stays deterministic, it just gets fewer real seconds.
        /// </summary>
        public float SlowScale { get; private set; } = 1f;
        float _slowLeft;

        float _slowFrom = 1f;

        float _hitSlowReadyAt, _hitSlow = 1f, _hitSlowLeft;
        float _lastHeroCast = -10f, _castGap = 0.66f;

        /// <summary>
        /// 재기획 §4.3 등급별 타격: how long a struck unit freezes, and the world slow for the heaviest blows.
        /// Basic 2 frames, skill 3, finisher 4, ultimate/evolution 6 (world at 30% for 100 ms, at most every 2.5 s), resonance 2, chains 0.
        /// </summary>
        static float HitHold(SimEvent ev, int heroId, out float slow, out float slowFor)
        {
            const float F = 1f / 60f;
            slow = 1f; slowFor = 0f;
            if (ev.FromChain) return 0f;
            if (ev.Res != Resonance.None) return 2f * F;
            var skill = string.IsNullOrEmpty(ev.SkillId) ? null : SkillBook.Get(ev.SkillId);
            if (skill == null || ev.SourceId != heroId) return 2f * F;
            // Only an ultimate or evolution slows the whole screen; a finisher's weight stays on its target (희승: 궁수가
            // 멈칫멈칫한다 — finishers land every second or two and each one stopped the world).
            if (skill.Role == SkillRole.Ultimate || skill.IsEvolution) { slow = 0.3f; slowFor = 0.1f; return 6f * F; }
            if (skill.Role == SkillRole.Finisher) return 4f * F;
            return 3f * F;
        }

        public void SlowMo(float scale, float seconds)
        {
            if (scale < _slowFrom || _slowLeft <= 0f) { _slowFrom = scale; _slowLeft = seconds; }
            else _slowLeft = System.Math.Max(_slowLeft, seconds);
        }

        /// <summary>Real (unscaled) seconds; eases back to full speed over the last stretch.</summary>
        public void TickSlowMo(float realDt)
        {
            // Two channels, the slower wins: the long slows (ultimate cut-in, boss death, rebirth) and the hit-stop
            // beat of a finisher or ultimate blow, so one never stretches or cuts the other (review 11-21).
            float hit = 1f;
            if (_hitSlowLeft > 0f) { _hitSlowLeft -= realDt; hit = _hitSlow; }
            float main = 1f;
            if (_slowLeft > 0f)
            {
                _slowLeft -= realDt;
                main = _slowLeft < 0.12f ? Mathf.Lerp(1f, _slowFrom, Mathf.Max(0f, _slowLeft) / 0.12f) : _slowFrom;
            }
            SlowScale = Mathf.Min(main, hit);
        }
        /// <summary>True for the tick the floor was swapped (clear or death); camera code snaps instead of panning.</summary>
        public bool FloorChangedThisTick { get; private set; }
        bool _floorBuiltPending;

        public WorldView(Transform root, SimWorld world, SpriteCatalog catalog, Material material, Material litMaterial)
        {
            _root = root;
            _world = world;
            _catalog = catalog;
            _material = material;
            _litMaterial = litMaterial != null ? litMaterial : material;
            // Floors and walls take the act grade (재기획 §3, T4); props keep their colour — they carry the act's accents.
            var graded = Resources.Load<Shader>("Shaders/GradedLit");
            _floorMat = graded != null ? new Material(graded) : _litMaterial;
            _wallMat = graded != null ? new Material(graded) : _litMaterial;
            // Actors get the outline + top-rim sprite shader; elites and bosses a stronger rim (d4-visual-techniques §6.1, §6.3).
            var actor = Resources.Load<Shader>("Shaders/ActorSprite");
            _heroMat = actor != null ? new Material(actor) : material;
            _enemyMat = actor != null ? new Material(actor) : material;
            _eliteMat = actor != null ? new Material(actor) : material;
            _liftMat = actor != null ? new Material(actor) : material;
            if (_liftMat.HasProperty("_Lift")) _liftMat.SetFloat("_Lift", 0.22f);
            _liftEliteMat = actor != null ? new Material(actor) : material;
            if (_liftEliteMat.HasProperty("_Lift")) _liftEliteMat.SetFloat("_Lift", 0.22f);
            Lights = new LightRig(root);
            Fog = new FogView(root, _litMaterial);
            Fx = new FxView(root, catalog, material);
            Numbers = new DamageNumbers(root);
            Vfx = new VfxPlayer(root, catalog, material);
            Director = new VfxDirector(root, world);
            // Drawn sheets for the director (PVFX floor rings, tears, splats, resonance layers): whole-texel scale, under the
            // per-tick budget; floor waves on the ground layer, the rest sorted with the units.
            Director.Sheet = (id, cell, scale, flip, ground, recolor) =>
                Vfx.Spawn(id, cell, scale, null, 0f, flip, 0f, false, default, false, ground: ground, recolor: recolor);
            _drops = new DropView(root, catalog, material);
        }

        public Vector3 HeroWorldPos(float alpha) => IsoProjection.ToWorld(Lerp(_world.Hero.PrevPos, _world.Hero.Pos, alpha));

        static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        readonly Material _heroMat, _enemyMat, _eliteMat, _liftMat, _liftEliteMat;

        /// <summary>Monsters whose sheets came out darker than their act's floor (ice-assets report): drawn lifted.</summary>
        static readonly HashSet<string> DarkArt = new HashSet<string> { "icemail_soldier", "frost_lord_knight" };

        /// <summary>
        /// Each act's accent (d4-visual-techniques §6.2): telegraphs, elite rings and the monsters' rim. Not red on the flesh
        /// floor — there danger is warm white.
        /// </summary>
        public static readonly Color[] ActAccent =
        {
            new Color(1f, 0.79f, 0.29f), new Color(0.72f, 0.94f, 0.38f), new Color(1f, 0.95f, 0.85f),
            new Color(0.56f, 0.88f, 1f), new Color(1f, 0.70f, 0.28f), new Color(0.72f, 0.61f, 1f),
        };

        /// <summary>
        /// Albedo grade of floors / walls per act (docs/research/2026-11-21-modern-pixel-rendering.md §3.2 T4): saturation
        /// floor / wall, gamma, midtone colour and its share. The flesh act loses the most colour — its tiles are red.
        /// </summary>
        static readonly (float floorSat, float wallSat, float gamma, Color mix, float share)[] Grade =
        {
            (0.70f, 0.60f, 1.00f, new Color(0.78f, 0.72f, 0.60f), 0.10f), // #C8B898
            (0.65f, 0.55f, 1.05f, new Color(0.43f, 0.55f, 0.44f), 0.15f), // #6E8C70
            (0.50f, 0.42f, 1.08f, new Color(0.42f, 0.29f, 0.32f), 0.15f), // #6A4A52
            (0.60f, 0.55f, 1.05f, new Color(0.47f, 0.56f, 0.66f), 0.20f), // #7890A8
            (0.55f, 0.45f, 1.12f, new Color(0.29f, 0.25f, 0.24f), 0.15f), // #4A403C
            (0.55f, 0.45f, 1.15f, new Color(0.23f, 0.24f, 0.35f), 0.20f), // #3A3E5A
        };

        void SetGrade(int act)
        {
            var g = Grade[Mathf.Clamp(act, 0, Grade.Length - 1)];
            foreach (var (m, sat) in new[] { (_floorMat, g.floorSat), (_wallMat, g.wallSat) })
            {
                if (!m.HasProperty("_Sat")) continue;
                m.SetFloat("_Sat", sat);
                m.SetFloat("_Gamma", g.gamma);
                m.SetColor("_MixColor", g.mix);
                m.SetFloat("_Mix", g.share);
            }
        }

        void SetActorLook(int act)
        {
            var accent = ActAccent[Mathf.Clamp(act, 0, ActAccent.Length - 1)];
            if (_heroMat.HasProperty("_RimColor")) _heroMat.SetColor("_RimColor", new Color(1f, 0.92f, 0.81f, 0.5f));
            if (_enemyMat.HasProperty("_RimColor")) _enemyMat.SetColor("_RimColor", new Color(accent.r, accent.g, accent.b, 0.35f));
            if (_liftMat.HasProperty("_RimColor")) _liftMat.SetColor("_RimColor", new Color(accent.r, accent.g, accent.b, 0.35f));
            // A lifted elite keeps the elite marking (stronger rim, darker outline).
            if (_liftEliteMat.HasProperty("_RimColor")) _liftEliteMat.SetColor("_RimColor", new Color(accent.r, accent.g, accent.b, 0.65f));
            if (_liftEliteMat.HasProperty("_OutlineColor")) _liftEliteMat.SetColor("_OutlineColor", new Color(0.02f, 0.01f, 0.02f, 0.95f));
            if (_eliteMat.HasProperty("_RimColor")) _eliteMat.SetColor("_RimColor", new Color(accent.r, accent.g, accent.b, 0.65f));
            if (_eliteMat.HasProperty("_OutlineColor")) _eliteMat.SetColor("_OutlineColor", new Color(0.02f, 0.01f, 0.02f, 0.95f));
        }

        // ---------- ticks ----------

        public void OnTick()
        {
            // A floor can also change between ticks (rebirth from the camp) and be built by Render first:
            // BuildFloor leaves _floorBuiltPending so the next tick still counts as a floor change.
            if (_builtFor != _world.Floor) BuildFloor();
            FloorChangedThisTick = _floorBuiltPending;
            _floorBuiltPending = false;
            Vfx.BeginTick();
            Director.BeginTick();
            int heroId = _world.Hero.Id;
            foreach (var ev in _world.Events)
            {
                // Events from a tick that swapped floors carry old-floor positions; keep the notices that matter,
                // moved onto the hero.
                if (FloorChangedThisTick)
                {
                    if (ev.Type == SimEventType.HeroDied || ev.Type == SimEventType.Rebirth)
                    {
                        var moved = ev;
                        moved.Pos = _world.Hero.Pos;
                        if (ev.Type == SimEventType.Rebirth) { SlowMo(0.3f, 1f); VfxEvent(moved); }
                        Fx.OnEvent(moved);
                    }
                    continue;
                }
                switch (ev.Type)
                {
                    case SimEventType.Cast when ev.SourceId == heroId:
                        if (_units.TryGetValue(heroId, out var h))
                        {
                            var castDef = SkillBook.Get(ev.SkillId);
                            bool volley = castDef != null && castDef.Volley > 0;
                            if (ev.Depth == 2 && !volley) break; // a copy flying off on its own: no new swing
                            if (volley) { h.Play("attack", false, 3.5f); break; } // 속사: one quick draw per arrow
                            // 희승: 궁수가 멈칫멈칫한다 — every cast now restarts the swing, played just fast enough to end as the
                            // next cast comes (the gap between casts, smoothed), so shots never land mid-pose or after an idle beat.
                            float now = Time.time, gap = now - _lastHeroCast;
                            _lastHeroCast = now;
                            if (gap > 0.05f && gap < 1.5f) _castGap = Mathf.Lerp(_castGap, gap, 0.5f);
                            float baseLen = _catalog.Duration(h.Key, "attack", 1f);
                            float speed = baseLen > 0f ? Mathf.Clamp(baseLen / (_castGap * 0.95f), 1f, 3.5f) : 1.5f;
                            h.Play("attack", false, speed);
                        }
                        break;
                    case SimEventType.Hit when ev.TargetId == heroId:
                        if (_units.TryGetValue(heroId, out var hh))
                        {
                            // A blow on the hero: a short red tint at most every 0.25 s (a full white flash on every hit kept
                            // the late-game hero a flat pink silhouette, review 11-23).
                            if (Time.unscaledTime >= hh.FlashReadyAt)
                            {
                                hh.FlashLen = 0.08f; hh.Flash = 0.08f;
                                hh.FlashCol = new Color(1f, 0.25f, 0.2f, 0.55f);
                                hh.FlashReadyAt = Time.unscaledTime + 0.25f;
                            }
                            hh.Squash = 0.1f;
                        }
                        if (_units.TryGetValue(ev.SourceId, out var attacker))
                        {
                            attacker.Play("attack", false, 1.5f);
                            if (attacker.Boss) Shake = Mathf.Max(Shake, 0.05f);
                        }
                        break;
                    case SimEventType.Hit:
                        if (_units.TryGetValue(ev.TargetId, out var uv))
                        {
                            // 재기획 §4.3: two frames of flash, at most every 0.1 s per unit (Hades: 30 ms). At 0.12 s and
                            // eight hits a second the late-game packs stayed white and the hit signal was lost.
                            if (Time.unscaledTime >= uv.FlashReadyAt)
                            {
                                // 타격감 리서치 (1122_hit_feel.md): 40-60 ms, crits and heavy blows longer; tinted by the element.
                                bool heavy = ev.Res != Resonance.None; // a resonance blow flashes longer
                                uv.FlashLen = heavy ? 0.09f : 0.06f;
                                uv.Flash = uv.FlashLen;
                                uv.FlashCol = ev.Elem == Element.None ? Color.white : Color.Lerp(Color.white, FxView.ElementColor(ev.Elem), 0.45f);
                                uv.FlashReadyAt = Time.unscaledTime + 0.1f;
                            }
                            // Knockback (view only): the sprite gives a step the way the blow went (a blast's centre, an
                            // arrow's flight) and settles back.
                            if (!uv.Boss)
                            {
                                var away = BlowDirs.For(ev, _world.Hero.Pos);
                                if (away != Vector2.zero) { uv.Kick = (Vector3)away * 0.11f; uv.KickT = 0.12f; }
                            }
                            // 재기획 §4.3: the struck unit holds its frame 2-6 frames by the blow's weight; finishers and
                            // ultimates also slow the world for a beat (with a cooldown so a flurry never drags).
                            float hold = HitHold(ev, heroId, out float slow, out float slowFor);
                            uv.Freeze = Mathf.Max(uv.Freeze, hold);
                            uv.Squash = Mathf.Max(uv.Squash, 0.06f + hold);
                            if (slowFor > 0f && Time.unscaledTime >= _hitSlowReadyAt)
                            {
                                _hitSlow = slow;
                                _hitSlowLeft = slowFor;
                                _hitSlowReadyAt = Time.unscaledTime + 2.5f;
                            }
                            if (uv.Anim != "attack" && _catalog.HasAnim(uv.Key, "hurt")) uv.Play("hurt", false, 1.5f);
                        }
                        break;
                    case SimEventType.Death:
                        if (_units.TryGetValue(ev.TargetId, out var dk))
                        {
                            // The corpse is thrown along the blow, and dies by the element that killed it.
                            dk.DeathElem = ev.KillElem;
                            var away = IsoProjection.ToWorld(ev.Pos) - IsoProjection.ToWorld(_world.Hero.Pos);
                            dk.Fling = dk.Boss || away.sqrMagnitude < 1e-4f ? Vector3.zero : away.normalized * 0.32f;
                        }
                        if (_units.TryGetValue(ev.TargetId, out var dv) && dv.Boss)
                        {
                            Director.Punch(new Color(1f, 0.85f, 0.6f), 0.1f); // a boss's fall: the impact frame
                            Shake = Mathf.Max(Shake, 0.2f);
                            if (_world.IsActBossFloor) SlowMo(0.25f, 0.8f);
                        }
                        break;
                    case SimEventType.ChainPop when ev.Depth >= 5:
                        // 재기획 §4.3: only deep chains shake, and lightly (every pop from depth 3 kept the screen trembling).
                        Shake = Mathf.Max(Shake, 0.02f + 0.005f * ev.Depth);
                        if (ev.Depth >= 6) SlowMo(0.6f, 0.15f);
                        break;
                    case SimEventType.GrandResonance:
                        SlowMo(0.35f, 0.35f);
                        break;
                    case SimEventType.Rebirth:
                        SlowMo(0.3f, 1f);
                        Shake = Mathf.Max(Shake, 0.2f);
                        break;
                    case SimEventType.EventResolved:
                        OnEventResolved(ev);
                        break;
                    case SimEventType.Drop when ev.Item != null:
                        _drops.Spawn(ev.Item, ev.Pos);
                        if (ItemStyle.IsJackpot(ev.Item)) Shake = Mathf.Max(Shake, 0.06f);
                        break;
                }
                BlowDirs.Note(ev, _world.Hero.Pos); // each enemy's last blow, so its death bursts the same way
                LightEvent(ev);
                bool drawn = VfxEvent(ev);
                Fx.OnEvent(ev, drawn);
                Director.OnEvent(ev, drawn);
                if (ev.Type == SimEventType.Death) BlowDirs.Forget(ev.TargetId);
            }
            Fx.EndTick();
            if (Fx.MassKill) { Shake = Mathf.Max(Shake, 0.07f); Director.ZoomKick?.Invoke(0.012f); } // a pack falls: a small zoom punch
            if (!FloorChangedThisTick) Numbers.OnTick(_world, id => _units.TryGetValue(id, out var u) && u.Boss);
        }

        /// <summary>Floating damage numbers (V toggles).</summary>
        public DamageNumbers Numbers { get; private set; }

        int _flashFrame = -1, _flashesThisFrame, _soulFrame = -1, _souls;

        /// <summary>Short light flashes so spells and pops light up the room around them, not just themselves.</summary>
        void LightEvent(in SimEvent ev)
        {
            var at = IsoProjection.ToWorld(ev.Pos) + new Vector3(0f, 0.3f, 0f);
            // 재기획 §4.4: at most two routine light flashes a frame (late game lit every cast, pop and resonance, so the
            // dark acts kept blinking bright). Bosses, level-ups and drops below are not budgeted.
            if (Time.frameCount != _flashFrame) { _flashFrame = Time.frameCount; _flashesThisFrame = 0; }
            bool routine = ev.Type == SimEventType.Cast || ev.Type == SimEventType.ChainPop || ev.Type == SimEventType.Resonance;
            if (routine && _flashesThisFrame >= 2) return;
            switch (ev.Type)
            {
                case SimEventType.Cast when ev.Value > 1.6f:
                {
                    _flashesThisFrame++;
                    Lights.FlashAt(at, FxView.ElementColor(ev.Elem), 1.1f, Mathf.Clamp(ev.Value * 0.7f, 1.6f, 4.5f), 0.35f);
                    // An ultimate or a blast landing: a breath of added light over it and a swell of bloom (not on copies).
                    var castDef = ev.SkillId != null ? SkillBook.Get(ev.SkillId) : null;
                    bool landing = ev.SourceId == 0 && ev.SkillId != null && !ev.SkillId.EndsWith("_echo");
                    if (landing || (castDef != null && castDef.Role == SkillRole.Ultimate && ev.Depth != 2))
                    {
                        Lights.Glow(at, FxView.ElementColor(ev.Elem), Mathf.Clamp(ev.Value * 0.8f, 1.6f, 3.6f), landing ? 0.3f : 0.45f, 0.22f);
                        BloomKick = Mathf.Max(BloomKick, landing ? 0.5f : 1f);
                    }
                    break;
                }
                case SimEventType.ChainPop when ev.Depth >= 2:
                    _flashesThisFrame++;
                    Lights.FlashAt(at, FxView.ElementColor(ev.Elem), 0.8f + 0.1f * Mathf.Min(ev.Depth, 6), 1.5f + 0.25f * Mathf.Min(ev.Depth, 6), 0.4f);
                    break;
                case SimEventType.Resonance:
                    _flashesThisFrame++;
                    Lights.FlashAt(at, FxView.ElementColor(ev.Elem), 1.25f, 2.6f, 0.5f);
                    if (_flashesThisFrame == 1) Lights.Glow(at, FxView.ElementColor(ev.Elem), 1.3f, 0.28f, 0.16f);
                    break;
                case SimEventType.Death when _units.TryGetValue(ev.TargetId, out var dead) && dead.Boss:
                    Lights.FlashAt(at, new Color(1f, 0.9f, 0.75f), 1.6f, 6f, 1.1f);
                    Lights.Glow(at, new Color(1f, 0.9f, 0.75f), 4.5f, 0.6f, 0.35f);
                    BloomKick = 1f;
                    Lights.Wave(at, new Color(1f, 0.93f, 0.8f), 34f, 2.4f, 1.8f, 0.9f); // 빛의 파도: the dark lifts off the floor
                    break;
                case SimEventType.LevelUp:
                    Lights.FlashAt(at, new Color(1f, 0.85f, 0.45f), 1.4f, 4f, 1.2f);
                    Lights.Wave(at, new Color(1f, 0.82f, 0.45f), 12f, 1.2f, 1.1f, 0.25f);
                    break;
                case SimEventType.AugmentChosen when ev.Value >= 1f:
                    // A gold or prism card: the light of the pick runs out over the map.
                    bool prism = ev.Value >= 2f;
                    Lights.Wave(at, prism ? new Color(0.85f, 0.7f, 1f) : new Color(1f, 0.82f, 0.4f), prism ? 30f : 18f, prism ? 2.2f : 1.6f, prism ? 1.8f : 1.3f, prism ? 0.8f : 0.4f);
                    break;
                case SimEventType.ChestOpened:
                    Lights.FlashAt(at, new Color(1f, 0.8f, 0.4f), 1.0f, 2.6f, 0.9f);
                    break;
                case SimEventType.Drop when ev.Item != null && ev.Item.Rarity >= Rarity.Rare:
                    bool big = ItemStyle.IsJackpot(ev.Item);
                    Lights.FlashAt(at, ItemStyle.Color(ev.Item.Rarity), big ? 1.3f : 0.6f, big ? 2.6f : 1.4f, big ? 4f : 1.6f);
                    break;
            }
        }

        // ---------- VFX game packs (art/pixellab/vfx, spec §11) ----------

        /// <summary>Skill (or delayed blast) id → VFX pack id. Missing packs fall back to FxView's rings and sparks.</summary>
        static readonly Dictionary<string, string> SkillVfx = new Dictionary<string, string>
        {
            { "ember_slash", "ember_slash" }, { "evo_twin_ember", "evo_twin_ember" },
            { "ember_whirl", "ember_greatsword_whirl" }, { "evo_flame_vortex", "evo_flame_vortex" },
            { "frost_brand", "frost_brand" }, { "evo_glacial_brand", "frost_brand" },
            { "heat_absorb", "ember_heat_absorb" }, { "evo_ember_heart", "ember_heat_absorb" },
            { "storm_cleave", "storm_cleave" }, { "evo_thunder_cleave", "evo_thunder_cleave" },
            { "forge_strike", "forge_strike" }, { "evo_molten_execution", "forge_strike" },
            { "chain_hook", "chain_hook_pull" }, { "evo_abyss_chain", "chain_hook_pull" },
            { "thorn_mail", "ember_thorn_armor" }, { "evo_lava_mail", "ember_thorn_armor" },
            { "molten_heart", "molten_heart" }, { "evo_erupting_heart", "molten_heart" },
            { "frostfire_aura", "frostfire_aura" }, { "evo_eternal_frostfire", "frostfire_aura" },
            { "forge_judgment", "forge_judgment" }, { "undying_flame", "undying_flame" },
            { "fissure_burst", "fissure_burst" }, { "smith_hammer_drop", "smith_hammer" }, { "cinder_echo_burst", "cinder_echo" },
            { "elite_frost_burst", "frost_nova|res_shatter" },
            // 서약기사 (spec §9). "a|b": the first pack that exists.
            { "shield_bash", "shield_bash|hit_melee_spark" }, { "evo_rampart_bash", "evo_rampart_bash|shield_bash" },
            { "hallowed_aura", "hallowed_aura|oath_hallowed_aura" }, { "evo_pilgrim_light", "hallowed_aura|oath_hallowed_aura" },
            { "rallying_cry", "rallying_cry|oath_shield_taunt" }, { "evo_iron_cry", "rallying_cry|oath_shield_taunt" },
            { "judgment", "judgment" }, { "evo_double_judgment", "judgment" },
            { "oath_barrier", "oath_barrier|oath_barrier_counter" }, { "evo_retort_barrier", "oath_barrier|oath_barrier_counter" },
            { "consecrate", "consecrate|oath_hallowed_aura" }, { "evo_holy_ground", "consecrate|oath_hallowed_aura" },
            { "bell_of_judgment", "bell_of_judgment" }, { "evo_cathedral_bell", "evo_cathedral_bell|bell_of_judgment" },
            { "unbroken_vow", "unbroken_vow" }, { "evo_deathless_vow", "unbroken_vow" },
            { "warding_sigil", "warding_sigil" }, { "evo_sealing_sigil", "warding_sigil" },
            { "grand_judgment", "grand_judgment|grand_shock" }, { "sanctuary", "sanctuary|oath_barrier_counter" },
            { "oath_barrier_break", "oath_barrier_break|oath_barrier_counter" }, { "grace_echo_burst", "grace_echo_burst|oath_barrier_counter" },
            { "unbroken_vow_burst", "unbroken_vow_burst|grand_shock" }, { "heaven_spear", "heaven_spear|evo_spear_rain" },
            // 바람사냥꾼 (spec §9-1)
            { "whirling_blades", "whirling_blades|wind_whirling_blades" }, { "evo_blade_storm", "evo_blade_storm|whirling_blades|wind_whirling_blades" },
            { "shock_trap", "shock_trap|wind_shock_trap" }, { "evo_chain_trap", "shock_trap|wind_shock_trap" },
            { "storm_eye", "storm_eye" }, { "evo_typhoon_eye", "storm_eye" },
            { "rot_cloud", "rot_cloud" }, { "evo_plague_fog", "rot_cloud" },
            { "hunters_mark", "hunters_mark" }, { "evo_prey_brand", "hunters_mark" },
            { "shadow_decoy", "shadow_decoy" }, { "evo_shadow_legion", "shadow_decoy" },
            { "gale_volley", "gale_volley|wind_chain_arrow" }, { "evo_storm_volley", "gale_volley|wind_chain_arrow" },
            { "thunderstorm", "thunderstorm_bolt|grand_shock" }, { "hunt_time", "hunt_time" },
            { "arrow_rain_hit", "arrow_rain_hit|evo_steel_rain_hit|hit_melee_spark" }, { "thunderstorm_bolt", "thunderstorm_bolt|res_overload" },
            { "shadow_decoy_burst", "shadow_decoy_burst|res_collapse" }, { "viper_burst", "viper_burst|res_plague" },
            // 별점술사 (spec §9-2)
            { "meteor", "meteor|star_meteor" }, { "frost_nova", "frost_nova|res_shatter" }, { "evo_glacial_nebula", "frost_nova|res_shatter" },
            { "star_blessing", "star_blessing" }, { "evo_galaxy_blessing", "star_blessing" },
            { "black_hole", "black_hole|res_collapse" }, { "evo_event_horizon", "evo_event_horizon|black_hole|res_collapse" }, { "black_hole_burst", "black_hole_burst|grand_void" },
            { "time_stop", "time_stop|star_time_stop" }, { "evo_eternal_stop", "time_stop|star_time_stop" }, { "time_stop_release", "time_stop_release|grand_void" },
            { "rewind", "rewind|star_time_stop" }, { "evo_reverse_time", "rewind|star_time_stop" },
            { "nebula_burst", "nebula_burst|grand_ember" }, { "evo_great_nebula", "nebula_burst|grand_ember" },
            { "star_veil", "star_veil|oath_barrier_counter" }, { "evo_galaxy_veil", "star_veil|oath_barrier_counter" }, { "star_veil_break", "star_veil_break|res_shatter" },
            { "gravity_well", "gravity_well|res_collapse" }, { "evo_singularity", "evo_singularity|gravity_well|res_collapse" },
            { "supernova", "supernova|grand_ember" }, { "evo_hypernova", "evo_hypernova|supernova|grand_ember" },
            { "starfall_field", "starfall_field|star_blessing" }, { "evo_radiant_field", "starfall_field|star_blessing" },
            { "star_collapse", "star_collapse|grand_void" }, { "eternal_moment", "eternal_moment|star_time_stop" },
            // 대표 스킬 (11-21): drawn with existing packs; ForceRecolor repaints the ones borrowed from another element.
            { "blessed_hammer", "whirling_blades|wind_whirling_blades" }, { "evo_hammer_storm", "evo_blade_storm|whirling_blades|wind_whirling_blades" },
            { "zeal", "hit_melee_spark|shield_bash" }, { "evo_fanaticism", "hit_melee_spark|shield_bash" },
            { "combustion", "nebula_burst|grand_ember" }, { "evo_inferno", "evo_hypernova|supernova|grand_ember" },
            { "ice_shatter", "res_shatter|grand_frost" }, { "evo_glacier_break", "grand_frost|res_shatter" }, { "ice_shatter_burst", "res_shatter|frost_nova" },
            { "blizzard", "starfall_field|frost_nova" }, { "evo_whiteout", "starfall_field|frost_nova" },
            { "thunder_nova", "grand_shock|res_overload" }, { "evo_thunder_crown", "grand_shock|res_overload" },
        };

        /// <summary>Borrowed packs painted in another element: redrawn in the skill's colours.</summary>
        static readonly Dictionary<string, Element> ForceRecolor = new Dictionary<string, Element>
        {
            { "blessed_hammer", Element.Shock }, { "evo_hammer_storm", Element.Shock }, { "zeal", Element.Shock }, { "evo_fanaticism", Element.Shock },
            { "fireball", Element.Ember }, { "evo_great_fireball", Element.Ember }, { "ice_bolt", Element.Frost }, { "evo_ice_lance", Element.Frost },
            { "chain_lightning", Element.Shock }, { "evo_storm_chain", Element.Shock },
        };

        /// <summary>Projectile pack per shooting skill (Shot events from the hero); bone arrows stay the archers'.</summary>
        static readonly Dictionary<string, string> ShotVfx = new Dictionary<string, string>
        {
            { "shield_throw", "shield_throw|evo_returning_shield|proj_void_bolt" }, { "evo_returning_shield", "evo_returning_shield|shield_throw|proj_void_bolt" },
            { "bow_shot", "proj_storm_arrow" }, { "chain_arrow", "chain_arrow|proj_storm_arrow" }, { "evo_storm_arrow", "evo_storm_arrow|chain_arrow|proj_storm_arrow" },
            { "venom_arrow", "venom_arrow|proj_void_bolt" }, { "evo_plague_arrow", "venom_arrow|proj_void_bolt" },
            { "roll_shot", "chain_arrow|proj_storm_arrow" }, { "evo_triple_shot", "chain_arrow|proj_storm_arrow" },
            { "hunters_mark", "proj_void_bolt" }, { "evo_prey_brand", "proj_void_bolt" },
            { "star_orb", "proj_void_bolt" }, { "star_bolt", "star_bolt|proj_void_bolt" }, { "evo_twin_star", "star_bolt|proj_void_bolt" },
            { "ricochet_arrow", "chain_arrow|proj_storm_arrow" }, { "evo_ricochet_storm", "evo_storm_arrow|chain_arrow|proj_storm_arrow" },
            { "prism_star", "star_bolt|proj_void_bolt" }, { "evo_prism_nova", "star_bolt|proj_void_bolt" },
            { "gale_volley", "chain_arrow|proj_storm_arrow" }, { "evo_storm_volley", "evo_storm_arrow|chain_arrow|proj_storm_arrow" },
            { "fireball", "proj_void_bolt" }, { "evo_great_fireball", "proj_void_bolt" }, { "ice_bolt", "proj_void_bolt" }, { "evo_ice_lance", "proj_void_bolt" },
            { "chain_lightning", "proj_void_bolt" }, { "evo_storm_chain", "proj_void_bolt" },
        };

        /// <summary>Line skills drawn as one stretched beam from the hero toward the target.</summary>
        static readonly Dictionary<string, string> LineVfx = new Dictionary<string, string>
        {
            { "wind_path", "wind_path" }, { "evo_storm_path", "wind_path" }, { "void_lance", "void_lance" }, { "evo_abyss_lance", "void_lance" },
            { "comet_trail", "comet_trail" }, { "evo_twin_comet", "comet_trail" },
            { "ball_lightning", "wind_path" }, { "evo_twin_ball", "wind_path" },
        };

        string Pick(string ids)
        {
            foreach (var id in ids.Split('|')) if (Vfx.Has(id)) return id;
            return null;
        }

        static string ResVfx(Resonance r)
        {
            switch (r)
            {
                case Resonance.Shatter: return "res_shatter";
                case Resonance.Overload: return "res_overload";
                case Resonance.Plague: return "res_plague";
                case Resonance.Devour: return "res_devour";
                case Resonance.Collapse: return "res_collapse";
                default: return null;
            }
        }

        static string ElementName(Element e)
        {
            switch (e)
            {
                case Element.Frost: return "frost";
                case Element.Shock: return "shock";
                case Element.Rot: return "rot";
                case Element.Void: return "void";
                default: return "ember";
            }
        }

        Enemy FindEnemy(int id)
        {
            foreach (var e in _world.Enemies) if (e.Id == id) return e;
            return null;
        }

        static bool HasFrom(in SimEvent ev) => ev.From.X != 0f || ev.From.Y != 0f;

        /// <summary>Plays the event's VFX pack; true when one was drawn (so FxView skips its stand-in ring).</summary>
        bool VfxEvent(in SimEvent ev)
        {
            switch (ev.Type)
            {
                case SimEventType.Cast:
                {
                    // 축복의 망치: three golden hammers swing out around the paladin, a spiral turned a little each cast.
                    if ((ev.SkillId == "blessed_hammer" || ev.SkillId == "evo_hammer_storm") && ev.SourceId != 0 && Vfx.Has("smith_hammer"))
                    {
                        float turn = Time.time * 2.2f, r = Mathf.Max(1f, ev.Value * 0.7f);
                        for (int k = 0; k < 3; k++)
                        {
                            float a = turn + k * Mathf.PI * 2f / 3f;
                            var hammerAt = new Vec2(ev.Pos.X + Mathf.Cos(a) * r, ev.Pos.Y + Mathf.Sin(a) * r);
                            Vfx.Spawn("smith_hammer", hammerAt, 0.8f, null, 0f, false, 0f, false, default, false, false, Element.Shock); // 0.55 read faint (점검 11-22)
                        }
                        Vfx.Spawn(Pick("hallowed_aura|oath_hallowed_aura") ?? "hallowed_aura", ev.Pos, Mathf.Clamp(ev.Value / 2.6f, 0.8f, 1.4f), new Color(1f, 1f, 1f, 0.6f));
                        return true;
                    }
                    if (ev.SkillId != null && LineVfx.TryGetValue(ev.SkillId, out var lineId) && ev.SourceId != 0)
                    {
                        var lineDef = SkillBook.Get(ev.SkillId);
                        var tgt = FindEnemy(ev.TargetId);
                        if (tgt != null && lineDef != null)
                        {
                            var dir = tgt.Pos - ev.From;
                            if (dir.LengthSq > 1e-6f) return Vfx.Line(lineId, ev.From, ev.From + dir.Normalized * lineDef.Length);
                        }
                        break;
                    }
                    if (ev.SkillId == SkillBook.Basic.Id && ev.SourceId == _world.Hero.Id && HeroStats.IsMeleeClass(_world.Hero.ClassId))
                    {
                        // 휩쓸기: the basic swing shows its arc when it actually cuts more than one (sized to the arc).
                        var tgt = FindEnemy(ev.TargetId);
                        if (tgt == null) break;
                        float arc = 1.1f * (1f + _world.Hero.Derived.AreaPct);
                        bool more = false;
                        foreach (var e in _world.Enemies) if (e.Alive && e != tgt && Vec2.DistSq(e.Pos, tgt.Pos) <= arc * arc) { more = true; break; }
                        if (!more) break;
                        string arcFx = _world.Hero.ClassId == "oathknight" ? "storm_cleave" : "ember_slash";
                        return Vfx.Spawn(arcFx, tgt.Pos, arc / 1.4f, null, 0f, (_world.Hero.Facing.X - _world.Hero.Facing.Y) < 0f);
                    }
                    if (ev.SkillId == null) break;
                    // Copies over other packs ("<skill>_echo") look like the skill they copy when they have no art of their own.
                    if (!SkillVfx.TryGetValue(ev.SkillId, out var ids)
                        && !(ev.SkillId.EndsWith("_echo") && SkillVfx.TryGetValue(ev.SkillId.Substring(0, ev.SkillId.Length - 5), out ids))) break;
                    var id = Pick(ids);
                    if (id == null) break;
                    var def = SkillBook.Get(ev.SkillId);
                    bool follows = def != null && (def.Shape == SkillShape.Buff || def.Shape == SkillShape.Aura);
                    bool held = follows || (def != null && (def.Ground || def.TimeStopMul > 0f));
                    var at = ev.Pos;
                    if (def != null && def.Shape == SkillShape.Single)
                    {
                        var target = FindEnemy(ev.TargetId);
                        if (target != null) at = target.Pos;
                    }
                    bool facingLeft = (_world.Hero.Facing.X - _world.Hero.Facing.Y) < 0f; // screen x of the facing direction
                    float hold = held ? def.Duration : 0f;
                    bool big = ev.SourceId == 0 || (def != null && (def.Role == SkillRole.Ultimate || def.IsEvolution));
                    float scale = ev.Depth == 1 ? 1.25f : 1f; // 작열: the scorching version is bigger
                    // The effect grows with the area it hits (스킬 범위 %, 숙련, 문양): the art is drawn for the base
                    // radius, so a skill at twice its radius is drawn twice as big (competitive-analysis §5: hit = look).
                    // Echoes and copies ("<skill>_echo" blasts) size from the skill they echo.
                    bool echo = ev.SkillId.EndsWith("_echo");
                    var sizeDef = def ?? (echo ? SkillBook.Get(ev.SkillId.Substring(0, ev.SkillId.Length - 5)) : null);
                    // 재기획 §4.2: the drawing grows with the log of the area, capped at 1.6× (ultimates and the like 2×).
                    // It was linear up to 4×: pixels turned to 4×4 blocks and fields covered half the screen. The hit
                    // area itself is unchanged; growth shows as copies, rhythm and brightness instead.
                    if (sizeDef != null && sizeDef.Radius > 0.1f && ev.Value > 0f && !(sizeDef.Ranged && sizeDef.Shape == SkillShape.Single))
                    {
                        float ratio = Mathf.Max(0.01f, ev.Value / sizeDef.Radius);
                        scale *= Mathf.Clamp(1f + 0.5f * Mathf.Log(ratio, 2f), 0.8f, big ? 2f : 1.6f);
                        // 재기획 §4.2: where the capped drawing falls short of the real area, small impacts scatter over
                        // the outer band so the hit still reads as wide (at most four, only for the hero's own casts).
                        float drawnR = sizeDef.Radius * scale, realR = ev.Value;
                        if (!echo && ev.SourceId == _world.Hero.Id && realR > drawnR * 1.3f)
                        {
                            int n = Mathf.Min(4, 1 + (int)((realR - drawnR) / 1.2f));
                            var sparkTint = FxView.ElementColor(ev.Elem == Element.None ? sizeDef.Elem : ev.Elem);
                            for (int k = 0; k < n; k++)
                            {
                                float a = Random.value * Mathf.PI * 2f, d = Mathf.Lerp(drawnR, realR, 0.35f + 0.55f * Random.value);
                                Vfx.Spawn("hit_melee_spark", new Vec2(at.X + Mathf.Cos(a) * d, at.Y + Mathf.Sin(a) * d), 0.6f, sparkTint);
                            }
                        }
                    }
                    // Copies are smaller and fainter than the cast they copy, and never sit over the units (§4.4).
                    Color? tint = null;
                    if (echo)
                    {
                        if (GameSettings.EchoAlpha <= 0f) return true; // 설정: 분신 이펙트 끔 (the copy still hits)
                        scale *= 0.6f;
                        tint = new Color(1f, 1f, 1f, 0.55f * GameSettings.EchoAlpha);
                    }
                    // A glyph converted the skill's element: its sheet is redrawn in the new element's colours (§4.6).
                    var baseElem = sizeDef?.Elem ?? Element.None;
                    var recolor = ev.Elem != Element.None && baseElem != Element.None && ev.Elem != baseElem ? ev.Elem : Element.None;
                    if (recolor == Element.None && ev.SkillId != null && ForceRecolor.TryGetValue(ev.SkillId, out var forced)) recolor = forced;
                    return Vfx.Spawn(id, at, scale, tint, 0f, facingLeft, hold, big, default, follows, ground: held || echo, recolor: recolor);
                }
                case SimEventType.Resonance:
                {
                    var id = ResVfx(ev.Res);
                    if (id != null) Vfx.Spawn(id, ev.Pos, ev.FromChain ? 0.8f : 1f);
                    else if (ev.Res == Resonance.Spark) Vfx.Spawn("hit_melee_spark", ev.Pos, 0.8f, FxView.ElementColor(ev.Elem));
                    break;
                }
                case SimEventType.Rebirth:
                    Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), new Color(1f, 0.75f, 0.45f), 3f, 14f, 2f);
                    Lights.Wave(IsoProjection.ToWorld(ev.Pos), new Color(1f, 0.78f, 0.5f), 46f, 3f, 2.2f, 1f);
                    return Vfx.Spawn("grand_ember", ev.Pos, 1.6f, null, 0f, false, 0f, true) | Vfx.Spawn("sys_level_up", ev.Pos, 1.4f, new Color(1f, 0.7f, 0.4f), 0f, false, 0f, true);
                case SimEventType.Slam:
                {
                    bool tide = ev.SkillId == "abbot_tide";
                    if (ev.SkillId == "apostle_star" || ev.SkillId == "apostle_halo")
                    {
                        bool halo = ev.SkillId == "apostle_halo";
                        Shake = Mathf.Max(Shake, halo ? 0.22f : 0.1f);
                        Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), new Color(0.8f, 0.7f, 1f), halo ? 2.4f : 1.6f, ev.Value * 2.4f, 0.8f);
                        return halo ? Vfx.Spawn("black_hole_burst", ev.Pos, ev.Value / 2.2f) || Vfx.Spawn("grand_void", ev.Pos, ev.Value / 3f)
                                    : Vfx.Spawn("star_meteor", ev.Pos, ev.Value / 1.6f) || Vfx.Spawn("meteor", ev.Pos, ev.Value / 1.6f, new Color(0.8f, 0.7f, 1f));
                    }
                    if (ev.SkillId == "giant_pour" || ev.SkillId == "giant_star")
                    {
                        Shake = Mathf.Max(Shake, ev.SkillId == "giant_pour" ? 0.2f : 0.08f);
                        Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), new Color(1f, 0.55f, 0.2f), 2f, ev.Value * 2.4f, 0.7f);
                        return Vfx.Spawn("boss_grave_slam", ev.Pos, ev.Value / 2f, new Color(1f, 0.6f, 0.3f)) || Vfx.Spawn("grand_ember", ev.Pos, ev.Value / 3f);
                    }
                    if (ev.SkillId == "queen_spores")
                    {
                        Shake = Mathf.Max(Shake, 0.08f);
                        Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), new Color(0.55f, 1f, 0.6f), 1.4f, ev.Value * 2.2f, 0.6f);
                        return Vfx.Spawn("rot_cloud", ev.Pos, ev.Value / 1.5f, new Color(0.7f, 1f, 0.6f)) || Vfx.Spawn("res_plague", ev.Pos, ev.Value / 1.5f);
                    }
                    float scale = ev.Value / 2f;
                    bool drawn = tide
                        ? Vfx.Spawn("abbot_tide", ev.Pos, scale) || Vfx.Spawn("frost_nova", ev.Pos, scale, new Color(0.55f, 0.9f, 1f))
                        : Vfx.Spawn("boss_grave_slam", ev.Pos, scale) || Vfx.Spawn("fissure_burst", ev.Pos, scale);
                    Shake = Mathf.Max(Shake, tide ? 0.1f : 0.16f);
                    Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), tide ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.55f, 0.35f), 1.6f, ev.Value * 2.2f, 0.6f);
                    return drawn;
                }
                case SimEventType.BossCast:
                    if (ev.SkillId == "apostle_halo")
                    {
                        Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), new Color(0.8f, 0.7f, 1f), 1.6f, 6f, 1.2f);
                        return Vfx.Spawn("star_collapse", ev.Pos, 1.2f) || Vfx.Spawn("grand_void", ev.Pos, 1.2f);
                    }
                    if (ev.SkillId == "giant_star")
                    {
                        Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), new Color(1f, 0.6f, 0.25f), 1.6f, 5f, 1f);
                        return Vfx.Spawn("molten_heart", ev.Pos, 1.4f) || Vfx.Spawn("grand_ember", ev.Pos, 1.2f);
                    }
                    if (ev.SkillId == "queen_children")
                    {
                        Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), new Color(0.55f, 1f, 0.6f), 1.4f, 5f, 1.2f);
                        return Vfx.Spawn("res_plague", ev.Pos, 1.4f, new Color(0.7f, 1f, 0.6f));
                    }
                    if (ev.SkillId == "abbot_prayer")
                    {
                        Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), new Color(0.5f, 0.85f, 1f), 1.4f, 5f, 1.4f);
                        return Vfx.Spawn("hallowed_aura", ev.Pos, 1.1f, new Color(0.55f, 0.9f, 1f));
                    }
                    Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), new Color(0.6f, 1f, 0.6f), 1.4f, 5f, 1.2f);
                    return Vfx.Spawn("death_soul_release", ev.Pos, 1.4f, new Color(0.7f, 1f, 0.7f));
                case SimEventType.Ignited:
                {
                    // 등불 점화: the lantern flares — a warm nova, a white-gold flash and a jolt.
                    Vfx.Spawn("grand_ember", ev.Pos, 1.25f, new Color(1f, 0.85f, 0.55f), 0f, false, 0f, true);
                    Shake = Mathf.Max(Shake, 0.14f);
                    Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), new Color(1f, 0.92f, 0.7f), 2.6f, 12f, 1.6f);
                    Lights.Wave(IsoProjection.ToWorld(ev.Pos), new Color(1f, 0.88f, 0.6f), 26f, 1.9f, 1.6f, 0.6f); // 등불 점화 floods the floor
                    return true;
                }
                case SimEventType.GrandResonance:
                {
                    if (!Vfx.Spawn("grand_" + ElementName(ev.Elem), ev.Pos, 1f, null, 0f, false, 0f, true))
                        Vfx.Spawn("grand_ember", ev.Pos, 1f, FxView.ElementColor(Elements.Next(ev.Elem)), 0f, false, 0f, true);
                    Shake = Mathf.Max(Shake, 0.18f);
                    Lights.Glow(IsoProjection.ToWorld(ev.Pos) + new Vector3(0f, 0.3f, 0f), FxView.ElementColor(Elements.Next(ev.Elem)), 4f, 0.5f, 0.28f);
                    BloomKick = 1f;
                    Lights.FlashAt(IsoProjection.ToWorld(ev.Pos), FxView.ElementColor(Elements.Next(ev.Elem)), 1.8f, 7f, 1.3f);
                    Lights.Wave(IsoProjection.ToWorld(ev.Pos), FxView.ElementColor(Elements.Next(ev.Elem)), 18f, 1.5f, 1.3f, 0.4f);
                    break;
                }
                case SimEventType.ChainPop:
                    return Vfx.Spawn("chain_pop", ev.Pos, 0.7f + 0.06f * Mathf.Min(ev.Depth, 6), FxView.ElementColor(ev.Elem));
                case SimEventType.Hit when ev.FromChain && HasFrom(ev):
                {
                    var mark = FindEnemy(ev.TargetId)?.Mark ?? Element.Ember;
                    if (mark == Element.Ember || mark == Element.None) Vfx.Line("chain_ember_line", ev.From, ev.Pos);
                    else Vfx.Line("chain_line_neutral", ev.From, ev.Pos, FxView.ElementColor(mark));
                    break;
                }
                case SimEventType.Hit when ev.Res == Resonance.Overload && HasFrom(ev):
                    Vfx.Line("chain_line_neutral", ev.From, ev.Pos, FxView.ElementColor(Element.Shock));
                    break;
                case SimEventType.Hit when ev.SourceId == _world.Hero.Id && !ev.FromChain && ev.Res == Resonance.None:
                    // The sheet's chunks splash down-right: mirrored when the blow travels left on screen.
                    Vfx.Spawn("hit_melee_spark", ev.Pos, 0.7f, ev.Elem == Element.None ? (Color?)null : FxView.ElementColor(ev.Elem), 0f,
                        BlowDirs.For(ev, _world.Hero.Pos).x < 0f);
                    break;
                case SimEventType.Shot when ev.SourceId == _world.Hero.Id:
                {
                    // Hero projectiles: thrown shields, arrows, bolts (each ricochet leg flies on its own).
                    if (ev.SkillId == "shock_arc") { Director.Bolt(ev.From, ev.Pos, PixelRamp.Shock); break; } // 감전 전이: a pixel bolt (11-23)
                    if (ev.SkillId == "chain_lightning" || ev.SkillId == "evo_storm_chain") { Director.Bolt(ev.From, ev.Pos, PixelRamp.Shock, ev.Depth * 0.06f); break; }
                    var proj = ev.SkillId != null && ShotVfx.TryGetValue(ev.SkillId, out var pids) ? Pick(pids) : null;
                    bool ricochet = ev.SkillId != null && (SkillBook.Get(ev.SkillId)?.WallBounces ?? 0) > 0;
                    // 도탄 legs fly one after another (Depth = leg index), each a little slower so the bounce reads.
                    float travel = ricochet ? Mathf.Clamp(Vec2.Dist(ev.From, ev.Pos) * 0.04f, 0.06f, 0.35f) : Mathf.Clamp(Vec2.Dist(ev.From, ev.Pos) * 0.035f, 0.08f, 0.2f);
                    var shotColour = ev.SkillId != null && ForceRecolor.TryGetValue(ev.SkillId, out var sc) ? sc : Element.None;
                    // 갈래 화살 splits (Depth 1) leave once the first arrow has landed.
                    float legDelay = ricochet ? ev.Depth * 0.2f : ev.Depth > 0 ? 0.12f : 0f;
                    if (proj == null || !Vfx.Projectile(proj, ev.From, ev.Pos, travel, null, legDelay, shotColour))
                        Vfx.Line("chain_line_neutral", ev.From, ev.Pos, FxView.ElementColor(Element.Frost));
                    Director.Shot(ev, travel, legDelay);
                    // A spell in flight carries its light over the floor (plain arrows do not).
                    var shotElem = ev.Elem != Element.None ? ev.Elem : SkillBook.Get(ev.SkillId)?.Elem ?? Element.None;
                    if (shotElem != Element.None || _world.Hero.ClassId == "starseer")
                        Lights.Moving(IsoProjection.ToWorld(ev.From) + new Vector3(0f, 0.35f, 0f), IsoProjection.ToWorld(ev.Pos) + new Vector3(0f, 0.35f, 0f),
                            legDelay, travel, shotElem == Element.None ? new Color(0.75f, 0.6f, 1f) : FxView.ElementColor(shotElem), 1.1f, 1.8f);
                    break;
                }
                case SimEventType.Ground when ev.SkillId == "evo_meteor_burn":
                    // 유성 낙하: the crater keeps burning for the ground's life.
                    return Vfx.Spawn("evo_meteor_burn", ev.Pos, ev.Value / 2f, null, 0f, false, ev.Depth * 0.1f);
                case SimEventType.Ground when ev.SkillId == "viper":
                    Vfx.Spawn(Pick("viper_burst|res_plague") ?? "res_plague", ev.Pos, 1f);
                    break;
                case SimEventType.Shot when FindEnemy(ev.SourceId)?.Def.Id == "candle_priest":
                    // 촛불 사제: a void flame instead of an arrow.
                    if (!Vfx.Projectile("proj_void_bolt", ev.From, ev.Pos, 0.3f)) Vfx.Line("chain_line_neutral", ev.From, ev.Pos, FxView.ElementColor(Element.Void));
                    break;
                case SimEventType.Shot when FindEnemy(ev.SourceId)?.Def.Id == "tide_acolyte":
                    if (!Vfx.Projectile("proj_water_bolt", ev.From, ev.Pos, 0.3f) && !Vfx.Projectile("proj_void_bolt", ev.From, ev.Pos, 0.3f))
                        Vfx.Line("chain_line_neutral", ev.From, ev.Pos, FxView.ElementColor(Element.Frost));
                    break;
                case SimEventType.Shot when FindEnemy(ev.SourceId)?.Def.Id == "starless_seer":
                    if (!Vfx.Projectile("proj_void_bolt", ev.From, ev.Pos, 0.3f)) Vfx.Line("chain_line_neutral", ev.From, ev.Pos, FxView.ElementColor(Element.Void));
                    break;
                case SimEventType.Shot when FindEnemy(ev.SourceId)?.Def.Id == "bellows_priest":
                    // 풀무 사제: a gout of embers.
                    if (!Vfx.Projectile("proj_ember", ev.From, ev.Pos, 0.3f)) Vfx.Line("chain_ember_line", ev.From, ev.Pos, FxView.ElementColor(Element.Ember));
                    break;
                case SimEventType.Shot when FindEnemy(ev.SourceId)?.Def.Id == "mycelium_weaver":
                    // 균사 직조자: a gob of spores.
                    if (!Vfx.Projectile("proj_spore", ev.From, ev.Pos, 0.28f)) Vfx.Line("chain_line_neutral", ev.From, ev.Pos, FxView.ElementColor(Element.Rot));
                    break;
                case SimEventType.Shot when FindEnemy(ev.SourceId)?.Def.Id == "choir_wraith":
                    // 성가대 망령: a hymn that lands as a ring on the hero.
                    if (!Vfx.Spawn("hymn_ring", ev.Pos, 0.8f)) Vfx.Line("chain_line_neutral", ev.From, ev.Pos, new Color(0.75f, 0.9f, 1f));
                    break;
                case SimEventType.Shot:
                    if (!Vfx.Projectile("proj_bone_arrow", ev.From, ev.Pos, 0.22f)) Vfx.Line("chain_line_neutral", ev.From, ev.Pos, new Color(0.85f, 0.8f, 0.7f));
                    break;
                case SimEventType.Death:
                    // The soul leaves a body killed by no element (an element's death draws its own), three a tick at most:
                    // a pack's dozen white skulls at once read as noise (1080p build 11-23).
                    if (ev.KillElem == Element.None && Time.frameCount != _soulFrame | _souls < 3)
                    {
                        if (Time.frameCount != _soulFrame) { _soulFrame = Time.frameCount; _souls = 0; }
                        _souls++;
                        Vfx.Spawn("death_soul_release", ev.Pos, 0.8f, new Color(0.72f, 0.86f, 1f, 0.8f)); // pale blue: white is for the crit frame (review 11-23)
                    }
                    break;
                case SimEventType.Leap:
                    Vfx.Spawn(Pick(ev.SkillId == "holy_charge" || ev.SkillId == "evo_charge_of_light" ? "holy_charge_land|ash_leap_land"
                        : ev.SkillId == "roll_shot" || ev.SkillId == "evo_triple_shot" ? "roll_shot|hit_melee_spark"
                        : ev.SkillId == "evo_meteor_fall" ? "evo_meteor_fall|ash_leap_land" : "ash_leap_land") ?? "ash_leap_land", ev.Pos, 1f, null, 0f, false, 0f, true);
                    break;
                case SimEventType.Pull:
                    string pullLine = ev.SkillId == "martyr_chains" || ev.SkillId == "evo_confession_chains" ? "martyr_chains"
                        : ev.SkillId == "constellation" || ev.SkillId == "evo_great_constellation" ? "constellation_line" : "chain_hook";
                    if (!Vfx.Line(pullLine, ev.From, ev.Pos))
                        Vfx.Line("chain_line_neutral", ev.From, ev.Pos, FxView.ElementColor(Element.Void));
                    break;
                case SimEventType.LevelUp:
                case SimEventType.Evolved:
                    Vfx.Spawn("sys_level_up", _world.Hero.Pos, 1f, ev.Type == SimEventType.Evolved ? new Color(1f, 0.75f, 0.4f) : (Color?)null, 0f, false, 0f, true);
                    break;
                case SimEventType.Drop when ev.Item != null && ev.Item.Rarity >= Rarity.Legendary:
                    Vfx.Spawn("sys_legendary_drop", ev.Pos, 1f, null, 0f, false, 3f, true);
                    break;
            }
            return false;
        }

        // ---------- floor ----------

        void BuildFloor()
        {
            if (_tileRoot == null)
            {
                _tileRoot = new GameObject("Floor").transform;
                _tileRoot.SetParent(_root, false);
            }
            foreach (var u in _units.Values) Object.Destroy(u.Sr.gameObject);
            _eventProps.Clear();
            _units.Clear();
            Lights.ClearProps();
            _builtFor = _world.Floor;
            _floorBuiltPending = true;
            _drops.Clear();
            Vfx.Clear();
            Director.Clear();
            Fx.ClearStains(); // the old floor's blood stays with it
            _act = Mathf.Min(_world.ActLoop, EnemyRoster.Acts.Length - 1);
            Lights.SetAct(_act);
            Fog.SetAct(_act);
            SetActorLook(_act);
            SetGrade(_act);
            Fx.Act = _act;
            Fx.Walkable ??= p => _world.Floor.IsWalkable(p);

            int used = 0;
            var f = _world.Floor;
            var walls = new List<(int x, int y, WallShape shape, string key)>();
            for (int y = 0; y < f.Height; y++)
            for (int x = 0; x < f.Width; x++)
            {
                if (f.IsWalkable(x, y))
                    Place(ref used, "tile/floor", Hash(x, y, 0), x + 0.5f, y + 0.5f, FloorOrder);
                else
                {
                    var shape = ShapeOf(f, x, y);
                    if (shape == WallShape.None) continue;
                    var key = WallKey(shape, Hash(x, y, _world.FloorIndex + 1));
                    Place(ref used, key, 0, x + 0.5f, y + 0.5f, 0);
                    walls.Add((x, y, shape, key));
                }
            }
            PlaceProps(ref used, f);
            PlaceWallDecor(ref used, walls);
            for (int i = used; i < _tilePool.Count; i++) _tilePool[i].gameObject.SetActive(false);

        }

        // ---------- act themes (GDD §6.3) ----------

        /// <summary>Act index of the floor being shown: 0 모래, 1 밀림, 2 핏덩이, 3 얼음, 4 대장간, 5 심연.</summary>
        int _act;

        /// <summary>
        /// Folder suffix of each act's kit: tile{s}/floor, wall{s}/ (organic-kit names), prop{s}/, decal{s}/.
        /// The forge and the abyss keep the numbered kits they were delivered with.
        /// </summary>
        static readonly string[] ActSuffix = { "_sand", "_jungle", "_flesh", "_ice", "4", "5" };

        /// <summary>Mild wash for act I crypt pieces standing in where an act's own art is missing (never a full-screen filter).</summary>
        static readonly Color[] ActTint =
        {
            new Color(0.95f, 0.86f, 0.70f), new Color(0.76f, 0.90f, 0.68f), new Color(0.92f, 0.76f, 0.76f),
            new Color(0.78f, 0.90f, 1.00f), new Color(0.95f, 0.78f, 0.68f), new Color(0.76f, 0.74f, 0.95f),
        };

        /// <summary>Light-giving props of each act, first one found stands in for the crypt's braziers and torches.</summary>
        static readonly string[][] ActLights =
        {
            new[] { "prop_sand/dry_torch_stand", "prop_sand/light_shaft" },
            new[] { "prop_jungle/firefly_cluster", "prop_jungle/glow_flower", "prop_jungle/light_shaft" },
            new[] { "prop_flesh/vein_pillar", "prop_flesh/blood_pool", "prop_flesh/gut_chandelier", "prop_flesh/pus_bubbles" },
            new[] { "prop_ice/frozen_lantern", "prop_ice/ice_crystal_cluster", "prop_ice/frozen_campfire" },
            new[] { "prop4/forge_hearth" },
            new[] { "prop5/star_corpse" },
        };

        /// <summary>Tall pieces that stand on a room's back edge like the crypt's pillars.</summary>
        static bool IsTall(string key) =>
            key.Contains("pillar") || key.Contains("column") || key.Contains("statue") || key.Contains("stele") || key.Contains("obelisk")
            || key.Contains("totem") || key.Contains("tree") || key.Contains("arch") || key.Contains("stalagmite") || key.Contains("hoist")
            || key.Contains("chimney") || key.Contains("pedestal") || key.Contains("banner") || key.Contains("waterfall")
            || key.Contains("door") || key.Contains("chandelier"); // a fallen trunk lies on the floor: clutter, not a pillar

        readonly Dictionary<(string, int), string> _actKeyCache = new Dictionary<(string, int), string>();

        /// <summary>An act's own piece for an act I key, or null. Cached: a floor build asks for ~16k keys.</summary>
        string ActKey(string key, int act)
        {
            if (_actKeyCache.TryGetValue((key, act), out var hit)) return hit;
            var made = MakeActKey(key, act);
            _actKeyCache[(key, act)] = made;
            return made;
        }

        string MakeActKey(string key, int act)
        {
            string sfx = ActSuffix[Mathf.Clamp(act, 0, ActSuffix.Length - 1)];
            if (key.StartsWith("tile/", System.StringComparison.Ordinal)) return "tile" + sfx + "/" + key.Substring(5);
            if (key.StartsWith("wall/", System.StringComparison.Ordinal)) return "wall" + sfx + "/" + key.Substring(5);
            if (key.StartsWith("decal/decal_", System.StringComparison.Ordinal))
            {
                // The act's decal kinds (gore/vein, sand/bones, ...) take the place of blood, bone, ash and wax in order.
                string rest = key.Substring(12); // blood_a ...
                int us = rest.LastIndexOf('_');
                if (us < 0) return null;
                string kind = rest.Substring(0, us);
                int slot = System.Array.IndexOf(new[] { "blood", "bone", "ash", "wax" }, kind);
                var all = _catalog.KeysWithPrefix("decal" + sfx + "/decal_");
                if (slot < 0 || all.Length == 0) return null;
                var kinds = new List<string>();
                foreach (var k in all)
                {
                    string r = k.Substring(("decal" + sfx + "/decal_").Length);
                    int u = r.LastIndexOf('_');
                    string kk = u > 0 ? r.Substring(0, u) : r;
                    if (!kinds.Contains(kk)) kinds.Add(kk);
                }
                string pick = kinds[slot % kinds.Count];
                var ofKind = System.Array.FindAll(all, k => k.StartsWith("decal" + sfx + "/decal_" + pick + "_", System.StringComparison.Ordinal) || k == "decal" + sfx + "/decal_" + pick);
                int v = rest.Length > us + 1 ? rest[us + 1] - 'a' : 0;
                return ofKind.Length > 0 ? ofKind[((v % ofKind.Length) + ofKind.Length) % ofKind.Length] : null;
            }
            if (!key.StartsWith("prop/", System.StringComparison.Ordinal)) return null;
            if (act == 4 && Act4Props.TryGetValue(key, out var p4)) return p4;
            if (act == 5 && Act5Props.TryGetValue(key, out var p5)) return p5;
            if (act >= 4) return null;
            // The new acts: lights for lights, the act's own low clutter for coffins, candles and rubble.
            if (key == "prop/brazier" || key == "prop/torch_sconce")
                foreach (var l in ActLights[act]) if (_catalog.Has(l)) return l;
            if (key == "prop/sarcophagus" || key == "prop/bone_pile_candle" || key.StartsWith("prop/rubble_", System.StringComparison.Ordinal))
            {
                var low = LowClutter(act);
                if (low.Length > 0) return low[(key.GetHashCode() & 0x7fffffff) % low.Length];
            }
            return null;
        }

        /// <summary>An act's delivered props that are neither lights nor tall pieces.</summary>
        string[] LowClutter(int act)
        {
            var all = _catalog.KeysWithPrefix("prop" + ActSuffix[act] + "/");
            return System.Array.FindAll(all, k => !IsTall(k) && System.Array.IndexOf(ActLights[act], k) < 0);
        }

        /// <summary>Act I props with an abyss counterpart (dead stars are the only light left).</summary>
        static readonly Dictionary<string, string> Act5Props = new Dictionary<string, string>
        {
            { "prop/brazier", "prop5/star_corpse" }, { "prop/torch_sconce", "prop5/star_corpse" },
            { "prop/sarcophagus", "prop5/constellation_altar" }, { "prop/bone_pile_candle", "prop5/lantern_first" },
            { "prop/rubble_a", "prop5/rock_a" }, { "prop/rubble_b", "prop5/rock_b" },
            { "prop/rubble_c", "prop5/rock_a" }, { "prop/rubble_d", "prop5/rock_b" },
        };

        /// <summary>Act I props with a forge counterpart (the molten hearths are this act's light).</summary>
        static readonly Dictionary<string, string> Act4Props = new Dictionary<string, string>
        {
            { "prop/brazier", "prop4/forge_hearth" }, { "prop/torch_sconce", "prop4/forge_hearth" },
            { "prop/sarcophagus", "prop4/anvil" }, { "prop/bone_pile_candle", "prop4/hammer_rack" },
            { "prop/rubble_a", "prop4/scrap_heap" }, { "prop/rubble_b", "prop4/mold_broken_a" },
            { "prop/rubble_c", "prop4/slag_puddle" }, { "prop/rubble_d", "prop4/mold_broken_b" },
        };

        /// <summary>Reuse pooled renderers across floors instead of destroying ~1000 GameObjects per transition.</summary>
        /// <param name="lift">Screen-space rise in world units (hanging decor on a wall face); keep to 1/64 steps.</param>
        /// <param name="bias">Sort bias on top of the cell's own order (decor on a wall must beat the wall).</param>
        SpriteRenderer Place(ref int used, string key, int variant, float cx, float cy, int fixedOrder, float lift = 0f, int bias = 0, bool flipX = false)
        {
            SpriteRenderer sr;
            if (used < _tilePool.Count) sr = _tilePool[used];
            else
            {
                sr = new GameObject("tile").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(_tileRoot, false);
                sr.sharedMaterial = _litMaterial;
                _tilePool.Add(sr);
            }
            used++;
            var cell = new Vec2(cx, cy);
            sr.gameObject.SetActive(true);
            var world = IsoProjection.ToWorld(cell);
            sr.transform.localPosition = world + new Vector3(0f, lift, 0f);
            bool tint = false;
            // Only act I keys (tile/, wall/, decal/, prop/) look for the act's own piece; a key that already is the act's
            // own (prop_sand/…, prop4/…) is drawn as it is, never washed.
            int slash = key.IndexOf('/');
            string family = slash > 0 ? key.Substring(0, slash) : key;
            bool actOneKey = family == "tile" || family == "wall" || family == "decal" || family == "prop";
            if (actOneKey && !key.StartsWith("prop/camp_", System.StringComparison.Ordinal) && key != "prop/merchant")
            {
                var own = ActKey(key, _act);
                if (own != null && _catalog.Has(own)) key = own;
                else tint = true; // an act I piece standing in: wash it lightly in this act's colour
            }
            sr.sprite = _catalog.Get(key, variant);
            sr.flipX = flipX;
            sr.sharedMaterial = family.StartsWith("wall", System.StringComparison.Ordinal) ? _wallMat
                : family.StartsWith("tile", System.StringComparison.Ordinal) || family.StartsWith("decal", System.StringComparison.Ordinal) ? _floorMat
                : _litMaterial;
            sr.sortingOrder = fixedOrder != 0 ? fixedOrder : IsoProjection.SortOrder(world, bias);
            sr.color = tint ? ActTint[Mathf.Min(_act, ActTint.Length - 1)] : Color.white;
            if (PropLights.TryGetValue(key, out var pl))
                Lights.AddProp(world + new Vector3(0f, pl.lift, 0f), pl.color, pl.intensity, pl.radius, pl.flicker);
            return sr;
        }

        /// <summary>Light sources baked into decor: fire flickers, the shrine glows steady and cold.</summary>
        static readonly Dictionary<string, (Color color, float intensity, float radius, float flicker, float lift)> PropLights =
            new Dictionary<string, (Color, float, float, float, float)>
        {
            { "prop/brazier",          (new Color(1f, 0.52f, 0.20f), 1.25f, 3.6f, 1f, 0.40f) },
            // 막 I 모래: dry torches and shafts of daylight from the cracks above (the brightest act).
            { "prop_sand/dry_torch_stand", (new Color(1f, 0.74f, 0.42f), 1.2f, 3.6f, 0.9f, 0.50f) },
            { "prop_sand/light_shaft",     (new Color(1f, 0.95f, 0.82f), 1.1f, 3.8f, 0.05f, 0.30f) },
            // 막 II 밀림: fireflies and glowing blooms, green-gold.
            { "prop_jungle/firefly_cluster", (new Color(0.78f, 1f, 0.45f), 1.0f, 3.4f, 0.35f, 0.40f) },
            { "prop_jungle/glow_flower",     (new Color(0.95f, 0.85f, 0.45f), 0.7f, 2.2f, 0.25f, 0.25f) },
            { "prop_jungle/light_shaft",     (new Color(0.92f, 1f, 0.82f), 0.9f, 3.4f, 0.05f, 0.30f) },
            // 막 III 핏덩이: the only act where red light belongs — kept to the veins and pools themselves.
            { "prop_flesh/vein_pillar",  (new Color(1f, 0.32f, 0.28f), 1.0f, 3.2f, 0.45f, 0.55f) },
            { "prop_flesh/blood_pool",   (new Color(1f, 0.30f, 0.25f), 0.55f, 1.8f, 0.35f, 0.05f) },
            { "prop_flesh/pulsing_sac",  (new Color(1f, 0.45f, 0.40f), 0.6f, 1.8f, 0.6f, 0.30f) },
            // 막 IV 얼음: cold blue crystals and frozen lanterns.
            { "prop_ice/frozen_lantern",      (new Color(0.85f, 0.95f, 1f), 1.1f, 3.6f, 0.2f, 0.50f) },
            { "prop_ice/ice_crystal_cluster", (new Color(0.55f, 0.85f, 1f), 0.9f, 3.0f, 0.1f, 0.35f) },
            { "prop_ice/frozen_campfire",     (new Color(0.75f, 0.92f, 1f), 0.8f, 2.6f, 0.3f, 0.30f) },   // a fire frozen mid-flame: cold light
            { "prop_flesh/gut_chandelier",    (new Color(1f, 0.42f, 0.36f), 0.7f, 2.6f, 0.5f, 0.70f) },
            { "prop_flesh/pus_bubbles",       (new Color(0.85f, 0.9f, 0.45f), 0.45f, 1.5f, 0.6f, 0.10f) },  // sickly yellow-green
            { "prop_jungle/flower_bud_giant", (new Color(1f, 0.85f, 0.55f), 0.5f, 1.8f, 0.2f, 0.40f) },
            { "prop3/glowcap_cluster", (new Color(0.45f, 1f, 0.8f), 1.15f, 3.6f, 0.35f, 0.35f) },  // 막 III: bioluminescence
            { "prop4/forge_hearth",    (new Color(1f, 0.5f, 0.18f), 1.45f, 4.2f, 0.9f, 0.40f) },   // 막 IV: molten hearths
            { "prop5/star_corpse",     (new Color(0.78f, 0.7f, 1f), 1.0f, 3.4f, 0.15f, 0.35f) },   // 막 V: a dead star's last light
            { "prop5/lantern_first",   (new Color(1f, 0.8f, 0.55f), 0.45f, 1.4f, 0.5f, 0.25f) },
            { "prop4/crucible",        (new Color(1f, 0.6f, 0.25f), 0.8f, 2.2f, 0.6f, 0.35f) },
            { "prop4/slag_puddle",     (new Color(1f, 0.45f, 0.15f), 0.5f, 1.5f, 0.4f, 0.05f) },
            { "prop4/ember_barrel",    (new Color(1f, 0.55f, 0.2f), 0.7f, 2.0f, 0.8f, 0.35f) },
            { "prop3/mushroom_violet",        (new Color(0.75f, 0.55f, 1f), 0.6f, 1.8f, 0.3f, 0.30f) },
            { "prop3/mushroom_stalks_violet", (new Color(0.75f, 0.55f, 1f), 0.5f, 1.6f, 0.3f, 0.35f) },
            { "prop2/candelabra",      (new Color(1f, 0.82f, 0.55f), 1.3f, 3.9f, 0.6f, 0.75f) },   // 막 II: pale candle flames
            { "prop2/holy_water_font", (new Color(0.5f, 0.85f, 1f), 0.45f, 1.4f, 0.15f, 0.30f) },
            { "prop/torch_sconce",     (new Color(1f, 0.58f, 0.26f), 1.0f, 2.8f, 1f, 0.55f) },
            { "prop/bone_pile_candle", (new Color(1f, 0.74f, 0.42f), 0.75f, 1.6f, 0.6f, 0.15f) },
            { "prop/shrine_altar",     (new Color(0.55f, 0.72f, 1f), 0.95f, 2.8f, 0.15f, 0.35f) },
            { "prop/chest",            (new Color(1f, 0.82f, 0.40f), 0.30f, 0.9f, 0.1f, 0.10f) },
            { "prop/shrine_damage",    (new Color(1f, 0.45f, 0.30f), 0.90f, 2.4f, 0.2f, 0.45f) },
            { "prop/shrine_speed",     (new Color(0.55f, 0.85f, 1f), 0.90f, 2.4f, 0.2f, 0.45f) },
            { "prop/shrine_regen",     (new Color(0.55f, 1f, 0.55f), 0.90f, 2.4f, 0.2f, 0.45f) },
            { "prop/cursed_chest_closed", (new Color(0.65f, 0.35f, 1f), 0.70f, 1.6f, 0.4f, 0.20f) },
            { "prop/oil_spring",       (new Color(1f, 0.72f, 0.30f), 0.85f, 2.0f, 0.3f, 0.20f) },
            { "prop/camp_campfire",    (new Color(1f, 0.55f, 0.22f), 1.25f, 3.4f, 1f, 0.30f) },
            { "prop/camp_blacksmith_forge", (new Color(1f, 0.48f, 0.18f), 0.95f, 2.4f, 0.8f, 0.35f) },
            { "prop/merchant",         (new Color(1f, 0.80f, 0.55f), 0.55f, 1.6f, 0.5f, 0.50f) },
            { "wall/wall_niche_skull_L", (new Color(1f, 0.70f, 0.40f), 0.55f, 1.1f, 0.7f, 0.60f) },
            { "wall/wall_niche_skull_R", (new Color(1f, 0.70f, 0.40f), 0.55f, 1.1f, 0.7f, 0.60f) },
        };

        static int Hash(int x, int y, int seed) => (x * 73856093 ^ y * 19349663 ^ seed * 83492791) & 0x7fffffff;

        // ---------- organic walls (art/pixellab/ASSETS.md "유기 지형 키트") ----------

        enum WallShape { None, StraightL, StraightR, OuterCorner, InnerCorner, Stub }

        /// <summary>
        /// Only walls behind the floor on screen are drawn (front walls would hide the hero — the usual iso cut-away).
        /// The piece depends on which front neighbours are floor: +y (down-left), +x (down-right), +x+y (down).
        /// A wall with floor behind it too (−x / −y: a step in a blob's front edge, a lone rock) would stand
        /// in front of that floor, so it is cut down to a low collapsed stub.
        /// </summary>
        static WallShape ShapeOf(Floor f, int x, int y)
        {
            bool py = f.IsWalkable(x, y + 1), px = f.IsWalkable(x + 1, y), pxy = f.IsWalkable(x + 1, y + 1);
            if (!py && !px && !pxy) return WallShape.None;
            if (f.IsWalkable(x - 1, y) || f.IsWalkable(x, y - 1)) return WallShape.Stub;
            if (py && px) return WallShape.OuterCorner;
            if (py) return WallShape.StraightL;
            if (px) return WallShape.StraightR;
            return WallShape.InnerCorner;
        }

        static readonly string[] VariantsL = { "wall/wall_straight_stained", "wall/wall_cracked_L", "wall/wall_niche_skull_L", "wall/wall_bars_L", "wall/wall_cracked_L", "wall/wall_collapsed_a", "wall/wall_arch_L", "wall/wall_collapsed_b" };
        static readonly string[] VariantsR = { "wall/wall_straight_stained", "wall/wall_cracked_R", "wall/wall_niche_skull_R", "wall/wall_bars_R", "wall/wall_cracked_R", "wall/wall_collapsed_a", "wall/wall_arch_R", "wall/wall_collapsed_b" };

        /// <summary>Piece for a shape; about one straight run cell in five gets a variant (research doc: every 4–6 cells).</summary>
        string WallKey(WallShape shape, int h)
        {
            string key;
            switch (shape)
            {
                case WallShape.StraightL:
                case WallShape.StraightR:
                    bool left = shape == WallShape.StraightL;
                    key = h % 5 == 0 ? (left ? VariantsL : VariantsR)[(h / 5) % VariantsL.Length] : left ? "wall/wall_straight_L" : "wall/wall_straight_R";
                    break;
                case WallShape.OuterCorner: key = h % 4 == 0 ? "wall/wall_end_pillar" : "wall/wall_outer_corner"; break;
                case WallShape.Stub: key = h % 2 == 0 ? "wall/wall_collapsed_a" : "wall/wall_collapsed_b"; break;
                default: key = "wall/wall_inner_corner"; break;
            }
            return _catalog.Has(key) ? key : "tile/wall"; // stand-in block until the kit is imported
        }

        /// <summary>
        /// Hanging decor on plain wall faces (chains, cobweb strands), cobwebs in concave corners,
        /// rubble on the floor in front of collapsed walls.
        /// </summary>
        void PlaceWallDecor(ref int used, List<(int x, int y, WallShape shape, string key)> walls)
        {
            var f = _world.Floor;
            bool cryptDressing = _act >= 4; // 모래·밀림·핏덩이·얼음 have no chains or cobwebs of their own
            // ...but each has its own wall hangings: banners, vine curtains, sinew webs, icicles.
            string[] hang = cryptDressing ? Available("prop/chain_shackle", "prop/chain_loop", "prop/chain_double_plate", "prop/chain_hook_plate") // forge and abyss: chains, no cobwebs
                : _act == 0 ? Available("prop_sand/tattered_banner")
                : _act == 1 ? Available("prop_jungle/vine_curtain", "prop_jungle/cobweb_root")
                : _act == 2 ? Available("prop_flesh/sinew_web", "prop_flesh/butcher_hook")
                : Available("prop_ice/icicle_cluster");
            string[] rubble = Available("prop/rubble_a", "prop/rubble_b", "prop/rubble_c", "prop/rubble_d");
            string[] webs = new string[0]; // cobwebs belong to the old crypt, not to forge, abyss or the new acts
            foreach (var (x, y, shape, key) in walls)
            {
                int h = Hash(x, y, _world.FloorIndex + 7);
                bool left = shape == WallShape.StraightL;
                // Face midpoint: the edge shared with the floor cell in front (+y for L, +x for R).
                float fx = left ? x + 0.5f : x + 1f, fy = left ? y + 1f : y + 0.5f;
                if (key.StartsWith("wall/wall_collapsed"))
                {
                    // Rubble on the floor cell in front of a broken wall (now and then, so stubs don't all match).
                    bool frontY = f.IsWalkable(x, y + 1);
                    if (rubble.Length > 0 && h % 3 == 0 && (frontY || f.IsWalkable(x + 1, y)))
                        Place(ref used, rubble[h % rubble.Length], 0, frontY ? x + 0.5f : x + 1.5f, frontY ? y + 1.5f : y + 0.5f, 0);
                }
                else if ((key == "wall/wall_straight_L" || key == "wall/wall_straight_R") && h % 9 == 1 && hang.Length > 0)
                {
                    var pick = hang[(h / 9) % hang.Length];
                    Place(ref used, pick, 0, fx, fy, 0, (pick.EndsWith("strands") ? 62 : 52) / 64f, 2);
                }
                else if (shape == WallShape.InnerCorner && h % 5 < 2 && webs.Length > 0)
                    Place(ref used, webs[h % webs.Length], 0, x + 1f, y + 1f, 0, 63 / 64f, 2, h % 2 == 0);
            }
        }

        string[] Available(params string[] keys) => System.Array.FindAll(keys, _catalog.Has);

        // ---------- room decor ----------

        /// <summary>
        /// Decor on each room's back edge (floor cells with a wall right behind them), 3–5 pieces a room,
        /// never in a corridor mouth; floor decals by room kind. Visual only.
        /// </summary>
        void PlaceProps(ref int used, Floor f)
        {
            var rng = new System.Random(f.Width * 7919 + f.Height * 31 + _world.FloorIndex);
            var taken = new HashSet<(int, int)>();
            string[] clutter = Available("prop/sarcophagus", "prop/broken_coffin", "prop/bone_pile_candle", "prop/stalagmite_a",
                "prop/stalagmite_b", "prop/stalagmite_c", "prop/dripstone_column", "prop/pillar_broken_fluted", "prop/pillar_broken_spiral",
                "prop/pillar_broken_banded");
            string[] pillars = Available("prop/pillar_skull_round", "prop/pillar_skull_square", "prop/pillar_banded", "prop/pillar_cracked");
            if (_act <= 3)
            {
                // 모래 · 밀림 · 핏덩이 · 얼음: the act's whole delivered kit, tall pieces on the back edge, the rest as clutter.
                var all = _catalog.KeysWithPrefix("prop" + ActSuffix[_act] + "/");
                var tall = System.Array.FindAll(all, IsTall);
                var low = LowClutter(_act);
                if (low.Length > 0) clutter = low;
                if (tall.Length > 0) pillars = tall;
            }
            else if (_act >= 5)
            {
                // 별 없는 심연: star-shard crystals, obelisks, statues looking up, pilgrims' packs gone to ash, a broken telescope.
                var c5 = Available("prop5/star_shard_a", "prop5/star_shard_b", "prop5/star_shard_c", "prop5/void_pool", "prop5/pilgrim_pack",
                    "prop5/telescope_broken", "prop5/rock_a", "prop5/rock_b", "prop5/prayer_cairn", "prop5/star_chart_disc");
                var p5 = Available("prop5/obelisk", "prop5/sky_statue", "prop5/star_socket_pedestal");
                if (c5.Length > 0) clutter = c5;
                if (p5.Length > 0) pillars = p5;
            }
            else
            {
                // 녹은 대장간: anvils, crucibles, bellows, ingots, moulds; chain hoists and the unfinished star for pillars.
                var c4 = Available("prop4/anvil", "prop4/crucible", "prop4/bellows", "prop4/ingot_pile", "prop4/hammer_rack",
                    "prop4/slag_puddle", "prop4/mold_broken_a", "prop4/mold_broken_b", "prop4/coal_cart", "prop4/ember_barrel",
                    "prop4/quench_trough", "prop4/scrap_heap");
                var p4 = Available("prop4/chain_hoist", "prop4/star_casting", "prop4/chimney_collapsed");
                if (c4.Length > 0) clutter = c4;
                if (p4.Length > 0) pillars = p4;
            }
            if (clutter.Length == 0) clutter = new[] { "prop/sarcophagus" };

            foreach (var room in f.Rooms)
            {
                var edge = f.BackEdge(room);
                edge.RemoveAll(c => NearMouth(f, room, c.x, c.y));
                for (int i = edge.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (edge[i], edge[j]) = (edge[j], edge[i]); }

                bool Put(ref int u, string key, System.Predicate<(int x, int y)> where = null)
                {
                    foreach (var c in edge)
                    {
                        if (where != null && !where(c) || Crowded(taken, c.x, c.y)) continue;
                        taken.Add(c);
                        Place(ref u, key, 0, c.x + 0.5f, c.y + 0.5f, 0);
                        if (key == "prop/brazier") Decal(ref u, f, "decal_ash", rng, c.x, c.y + 1);
                        return true;
                    }
                    return false;
                }
                void Anchor(ref int u, string key)
                {
                    var p = SimWorld.ChestPos(f, room);
                    taken.Add(((int)p.X, (int)p.Y));
                    Place(ref u, key, 0, p.X, p.Y, 0);
                }

                switch (room.Kind)
                {
                    case RoomKind.Boss:
                        Put(ref used, "prop/sarcophagus");
                        Put(ref used, "prop/brazier"); Put(ref used, "prop/brazier");
                        foreach (var p in pillars) { if (rng.Next(2) == 0) Put(ref used, p); }
                        break;
                    case RoomKind.Treasure:
                        Anchor(ref used, "prop/chest");
                        Put(ref used, "prop/brazier");
                        Put(ref used, clutter[rng.Next(clutter.Length)]);
                        break;
                    case RoomKind.Rest:
                        Anchor(ref used, "prop/shrine_altar");
                        Put(ref used, "prop/brazier"); Put(ref used, "prop/brazier");
                        break;
                    case RoomKind.Event:
                        PlaceEventProp(ref used, f, room);
                        Put(ref used, "prop/brazier");
                        break;
                    case RoomKind.Start:
                        // GDD §14: once the camp is open, it travels with the hero — set up in every floor's start room.
                        if (_world.CampUnlocked)
                        {
                            Put(ref used, "prop/camp_campfire");
                            Put(ref used, "prop/camp_tent");
                            Put(ref used, "prop/camp_blacksmith_forge");
                            if (_world.OilStoreLevel > 0) Put(ref used, "prop/camp_oil_store");
                            if (_world.CartographerLevel > 0) Put(ref used, "prop/camp_cartographer_table");
                        }
                        Put(ref used, "prop/brazier");
                        Put(ref used, clutter[rng.Next(clutter.Length)]);
                        break;
                    default:
                        Put(ref used, "prop/brazier");
                        int n = 1 + rng.Next(3);
                        for (int k = 0; k < n; k++) Put(ref used, clutter[rng.Next(clutter.Length)]);
                        if (rng.NextDouble() < 0.5) Put(ref used, "prop/torch_sconce", c => !f.IsWalkable(c.x - 1, c.y));
                        break;
                }

                // Floor decals: blood and bones where fights happen, wax by shrines and chests, ash in the boss hall.
                string[] kinds = room.Kind switch
                {
                    RoomKind.Rest => new[] { "decal_wax" },
                    RoomKind.Treasure => new[] { "decal_wax", "decal_bone" },
                    RoomKind.Start => new[] { "decal_wax", "decal_ash" },
                    RoomKind.Boss => new[] { "decal_bone", "decal_blood", "decal_ash" },
                    _ => new[] { "decal_blood", "decal_bone" },
                };
                int decals = 2 + rng.Next(3);
                for (int k = 0; k < decals; k++)
                {
                    var r = room.Rect;
                    Decal(ref used, f, kinds[rng.Next(kinds.Length)], rng, r.X + 1 + rng.Next(r.W - 2), r.Y + 1 + rng.Next(r.H - 2));
                }
            }
        }

        static readonly string[] ShrineProps = { "prop/shrine_damage", "prop/shrine_speed", "prop/shrine_regen" };
        readonly Dictionary<int, SpriteRenderer> _eventProps = new Dictionary<int, SpriteRenderer>();

        /// <summary>What an event room shows before (and after) its choice. Missing art falls back to the old altar.</summary>
        void PlaceEventProp(ref int used, Floor f, Room room)
        {
            var p = SimWorld.ChestPos(f, room);
            string key;
            switch (room.Event)
            {
                case EventKind.Shrine: key = ShrineProps[Mathf.Clamp(room.EventDefault, 0, 2)]; break;
                case EventKind.CursedChest: key = "prop/cursed_chest_closed"; break;
                case EventKind.Merchant: key = "prop/merchant"; break;
                case EventKind.OilSpring: key = "prop/oil_spring"; break;
                case EventKind.Champion:
                {
                    var c = room.Rect.Center;
                    if (_catalog.Has("decal/decal_champion_mark")) Place(ref used, "decal/decal_champion_mark", 0, c.x + 0.5f, c.y + 0.5f, FloorOrder + 12);
                    return;
                }
                default: return;
            }
            if (!_catalog.Has(key)) key = "prop/shrine_altar";
            _eventProps[room.Id] = Place(ref used, key, 0, p.X, p.Y, 0);
        }

        /// <summary>The chosen blessing replaces the shrine's look; an opened curse shows the open chest.</summary>
        void OnEventResolved(in SimEvent ev)
        {
            if (!_eventProps.TryGetValue(ev.TargetId, out var sr)) return;
            int roomId = ev.TargetId;
            var room = _world.Floor.Rooms.Find(r => r.Id == roomId);
            if (room == null) return;
            string key = null;
            if (room.Event == EventKind.Shrine) key = ShrineProps[Mathf.Clamp(ev.Depth, 0, 2)];
            else if (room.Event == EventKind.CursedChest && ev.Depth == 0) key = "prop/cursed_chest_open";
            if (key != null && _catalog.Has(key)) sr.sprite = _catalog.Get(key);
            Lights.FlashAt(sr.transform.localPosition + new Vector3(0f, 0.4f, 0f), room.Event == EventKind.CursedChest ? new Color(0.7f, 0.35f, 1f) : new Color(1f, 0.85f, 0.55f), 1.3f, 3f, 1.2f);
        }

        void Decal(ref int used, Floor f, string kind, System.Random rng, int x, int y)
        {
            if (!f.IsWalkable(x, y)) return;
            string key = $"decal/{kind}_{(char)('a' + rng.Next(4))}";
            if (_catalog.Has(key)) Place(ref used, key, 0, x + 0.5f, y + 0.5f, FloorOrder + 10);
        }

        /// <summary>Keep corridor mouths clear: a cell next to floor that lies outside the room's box.</summary>
        static bool NearMouth(Floor f, Room room, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (f.IsWalkable(nx, ny) && !room.Rect.Contains(nx, ny)) return true;
            }
            return false;
        }

        static bool Crowded(HashSet<(int, int)> taken, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (taken.Contains((x + dx, y + dy))) return true;
            return false;
        }

        // ---------- units ----------

        /// <summary>An enemy's sprite key: its own sheet, or a stand-in silhouette with a tint and scale (the codex shares it).</summary>
        public static string EnemyArt(SpriteCatalog catalog, string id, out Color tint, out float scale)
        {
            tint = Color.white;
            scale = 1f;
            string key = "enemy/" + id;
            if (catalog.Has(key) || !StandIns.TryGetValue(id, out var look)) return key;
            tint = look.tint;
            scale = look.scale;
            return StandInBase.TryGetValue(id, out var from) && catalog.Has(from) ? from : "enemy/ghoul";
        }

        UnitView ViewFor(Unit u, bool boss)
        {
            if (_units.TryGetValue(u.Id, out var v)) return v;
            string key;
            var tint = Color.white;
            float scale = 1f;
            if (u is Hero hero) key = "hero/" + hero.ClassId;
            else key = EnemyArt(_catalog, ((Enemy)u).Def.Id, out tint, out scale);
            var go = new GameObject(key + "#" + u.Id);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            // Unlit: actors keep their own colours (see the lantern falloff in DrawUnit), outlined and rimmed by the shader.
            bool marked = u is Enemy me && (me.Elite || me.Champion || boss);
            sr.sharedMaterial = u is Hero ? _heroMat : u is Enemy de && DarkArt.Contains(de.Def.Id) ? (marked ? _liftEliteMat : _liftMat) : marked ? _eliteMat : _enemyMat;
            var sh = new GameObject("shadow").AddComponent<SpriteRenderer>();
            sh.transform.SetParent(go.transform, false);
            sh.sharedMaterial = _material;
            sh.sprite = _catalog.Get("fx/disc");
            sh.transform.localScale = new Vector3(boss ? 1.8f : 0.9f, boss ? 0.9f : 0.45f, 1f);
            v = new UnitView { Sr = sr, Shadow = sh, Key = key, Boss = boss, Tint = tint, BaseScale = scale, Phase = (u.Id * 0.618f) % 1f * 6.28f };
            if (marked)
            {
                // Elites and bosses stand on a slow-pulsing ring in the act's accent: told apart by shape, not by colour alone.
                var ring = new GameObject("ring").AddComponent<SpriteRenderer>();
                ring.transform.SetParent(go.transform, false);
                ring.sharedMaterial = _material;
                ring.sprite = _catalog.Get("fx/ring");
                v.Ring = ring;
            }
            // GDD §5.4: an elite wears its prefixes over its head (small, warm, readable from afar).
            if (u is Enemy en && en.Affixes != EliteAffix.None)
            {
                var plate = new GameObject("affixes").AddComponent<TextMesh>();
                plate.transform.SetParent(go.transform, false);
                plate.transform.localPosition = new Vector3(0f, boss ? 2.4f : 1.35f, 0f);
                var font = UiFont.Body;
                plate.font = font;
                plate.fontSize = 40;
                plate.characterSize = 0.03f;
                plate.anchor = TextAnchor.MiddleCenter;
                plate.text = EliteAffixes.Names(en.Affixes) + (en.Champion ? " 정예" : "");
                plate.color = en.Champion ? new Color(1f, 0.75f, 0.35f) : new Color(0.75f, 0.85f, 1f);
                var mr = plate.GetComponent<MeshRenderer>();
                mr.sharedMaterial = font.material;
                mr.sortingOrder = 31000;
            }
            _units[u.Id] = v;
            return v;
        }

        float _igniteGlow;

        // ---------- VFX 쇼케이스 (F1 → V, 11-23): every element's blow, burst, fall, death and the drawn sheets, one beat apart ----------

        /// <summary>On: a row of demonstration effects beside the hero every 1.8 s, cycling through the kinds.</summary>
        public bool Showcase;
        float _showAt;
        int _showStep;
        static readonly Element[] ShowElems = { Element.Ember, Element.Frost, Element.Shock, Element.Rot, Element.Void };
        static readonly string[] ShowFalls = { "meteor", "heaven_spears", "lightning_storm", "arrow_rain", "black_hole_burst" };
        static readonly string[][] ShowSheets =
        {
            new[] { "ember_slash", "storm_cleave", "forge_strike", "judgment", "meteor" },
            new[] { "frost_nova", "res_shatter", "res_overload", "res_plague", "res_collapse" },
            new[] { "nebula_burst", "supernova", "black_hole", "consecrate", "rot_cloud" },
        };

        void TickShowcase(float dt)
        {
            if (!Showcase || (_showAt -= dt) > 0f) return;
            _showAt = 1.8f;
            var h = _world.Hero;
            Director.BeginTick();
            int kind = _showStep++ % 8;
            for (int i = 0; i < 5; i++)
            {
                var at = new Vec2(h.Pos.X + (i - 2) * 2.3f + 1.5f, h.Pos.Y - (i - 2) * 2.3f + 1.5f);
                var e = ShowElems[i];
                switch (kind)
                {
                    case 0: Director.OnEvent(new SimEvent { Type = SimEventType.Hit, SourceId = h.Id, TargetId = -9, Pos = at, Elem = e, Res = Resonance.Spark, Value = 100 }); break;
                    case 1: Director.OnEvent(new SimEvent { Type = SimEventType.GrandResonance, Pos = at, Elem = e, Value = 2.4f }); break;
                    case 2: Director.OnEvent(new SimEvent { Type = SimEventType.Cast, SourceId = 0, Pos = at, Elem = e, Value = 2f, SkillId = ShowFalls[i] }); break;
                    case 3: Director.OnEvent(new SimEvent { Type = SimEventType.Death, TargetId = -9, Pos = at, KillElem = e }); break;
                    case 4: Director.Bolt(h.Pos, at, PixelRamp.Of(e)); break;
                    case 5: case 6: case 7: Vfx.Spawn(ShowSheets[kind - 5][i], at, 1f, null, 0f, false, 0f, true); break;
                }
            }
            if (kind == 1) Director.OnEvent(new SimEvent { Type = SimEventType.LevelUp, Pos = h.Pos });
        }

        public void Render(float alpha, float dt)
        {
            TickShowcase(dt);
            if (_builtFor != _world.Floor) BuildFloor();
            var heroPos = Lerp(_world.Hero.PrevPos, _world.Hero.Pos, alpha);

            var lc = SimWorld.LanternColors[Mathf.Clamp(_world.Hero.LanternColor, 0, SimWorld.LanternColors.Length - 1)];
            Lights.LanternTint = new Color(lc.r, lc.g, lc.b);
            _igniteGlow = Mathf.MoveTowards(_igniteGlow, _world.Ignited ? 1f : 0f, dt / (_world.Ignited ? 0.6f : 2.5f));
            Lights.Update(IsoProjection.ToWorld(heroPos), dt, Time.time, Mathf.SmoothStep(0f, 1f, _igniteGlow));
            var cam = Camera.main;
            if (cam != null) Fog.Update(cam.transform.position, new Vector2(cam.orthographicSize * 2f * cam.aspect, cam.orthographicSize * 2f), Time.time);

            _statusDrawn = 0;
            DrawUnit(_world.Hero, false, heroPos, dt, heroPos);
            Afterimages(dt);
            foreach (var e in _world.Enemies)
                DrawUnit(e, e.Boss, Lerp(e.PrevPos, e.Pos, alpha), dt, heroPos);

            _drops.Render(dt, heroPos);
            Shake = Mathf.Max(0f, Shake - dt * 0.8f);
            BloomKick = Mathf.Max(0f, BloomKick - Time.unscaledDeltaTime * 4f);
            Fx.Render(dt, heroPos, _world.Hero.Pos.X != _world.Hero.PrevPos.X || _world.Hero.Pos.Y != _world.Hero.PrevPos.Y);
            Numbers.Render(dt);
            Vfx.Render(dt, IsoProjection.ToWorld(heroPos));
            Director.Render(dt);
        }

        readonly List<(SpriteRenderer sr, float t)> _ghosts = new List<(SpriteRenderer, float)>();
        float _ghostTimer;

        /// <summary>
        /// 잔상: a hero past ~25% extra move speed (melee classes grow there) leaves fading copies of itself, so sweeping
        /// through the packs reads as speed. Five at most, a quarter second each.
        /// </summary>
        void Afterimages(float dt)
        {
            var h = _world.Hero;
            bool moving = h.Pos.X != h.PrevPos.X || h.Pos.Y != h.PrevPos.Y;
            // …or at the x2 stride between fights (11-23), which runs faster than the hero's own speed.
            bool striding = Vec2.Dist(h.Pos, h.PrevPos) / SimWorld.Dt > h.Stats.MoveSpeed * 1.5f;
            if (h.Alive && moving && (striding || h.Stats.MoveSpeed >= HeroStats.BaseMoveSpeed * 1.25f) && _units.TryGetValue(h.Id, out var v) && (_ghostTimer -= dt) <= 0f)
            {
                _ghostTimer = 0.05f;
                int i = _ghosts.FindIndex(g => g.t <= 0f);
                SpriteRenderer sr;
                if (i < 0 && _ghosts.Count < 5)
                {
                    sr = new GameObject("afterimage").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(_root, false);
                    sr.sharedMaterial = _material;
                    _ghosts.Add((sr, 0f));
                    i = _ghosts.Count - 1;
                }
                if (i >= 0)
                {
                    sr = _ghosts[i].sr;
                    sr.sprite = v.Sr.sprite;
                    sr.flipX = v.Sr.flipX;
                    sr.transform.position = v.Sr.transform.position;
                    sr.transform.localScale = v.Sr.transform.localScale;
                    sr.sortingOrder = v.Sr.sortingOrder - 1;
                    _ghosts[i] = (sr, 0.25f);
                }
            }
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var (sr, t) = _ghosts[i];
                if (t <= 0f) { sr.enabled = false; continue; }
                t -= dt;
                sr.enabled = t > 0f;
                sr.color = new Color(0.75f, 0.85f, 1f, 0.35f * Mathf.Clamp01(t / 0.25f));
                _ghosts[i] = (sr, t);
            }
        }

        /// <summary>
        /// A status shows on the enemy wearing it (희승 11-23: free VFX "우리 게임에 맞게"): PVFX Foundry's loops (CC0) -
        /// flames round the feet while it burns, ice at the feet while frozen, slime while poisoned, stars over the head
        /// while stunned. One per enemy (frozen, burning, poisoned, stunned in that order), at most 24 a frame.
        /// </summary>
        void DrawStatus(Unit u, UnitView v, float dt)
        {
            string id = null;
            if (u is Enemy e && e.Alive && v.Fade > 0.99f && _statusDrawn < 24)
            {
                double now = _world.Time;
                id = e.FreezeUntil > now ? "pvfx_status_frozen" : e.BurnTicks > 0 ? "pvfx_status_burning"
                    : e.DotTicks > 0 ? "pvfx_status_poisoned" : e.StunUntil > now ? "pvfx_status_stun" : null;
            }
            if (id == null || !_catalog.TryGetClip($"vfx/{id}/whole", out var frames, out float fps, out _))
            {
                if (v.Status != null) v.Status.enabled = false;
                v.StatusId = null;
                return;
            }
            _statusDrawn++;
            if (v.Status == null)
            {
                v.Status = new GameObject("status").AddComponent<SpriteRenderer>();
                v.Status.transform.SetParent(v.Sr.transform, false);
                v.Status.sharedMaterial = Vfx.Material;
            }
            if (v.StatusId != id) { v.StatusId = id; v.StatusT = Random.value; } // packs do not burn in lockstep
            v.StatusT += dt;
            v.Status.enabled = true;
            v.Status.sprite = frames[(int)(v.StatusT * fps) % frames.Length];
            // Feet for the auras; the stars ride over the head (the sprite's top in its own space).
            bool overHead = id == "pvfx_status_stun";
            v.Status.transform.localPosition = overHead && v.Sr.sprite != null ? new Vector3(0f, v.Sr.sprite.bounds.max.y + 0.05f, 0f) : Vector3.zero;
            v.Status.sortingOrder = v.Sr.sortingOrder + 1;
            v.Status.color = new Color(1f, 1f, 1f, 0.9f * GameSettings.FxAlpha);
        }

        int _statusDrawn;

        void DrawUnit(Unit u, bool boss, Vec2 pos, float dt, Vec2 heroPos)
        {
            var v = ViewFor(u, boss);
            bool moving = u.Pos.X != u.PrevPos.X || u.Pos.Y != u.PrevPos.Y;
            var t = v.Sr.transform;
            float sink = 0f;

            if (!u.Alive)
            {
                if (v.Anim != "death") v.Play("death", false);
                v.DeadFor += dt;
                bool hasDeath = _catalog.HasAnim(v.Key, "death");
                float hold = hasDeath ? _catalog.Duration(v.Key, "death") + 0.6f : 0.15f;
                if (v.DeadFor > hold) v.Fade = Mathf.Max(0f, v.Fade - dt / 0.8f); // the burning-away dissolve reads at 0.8 s
                if (!hasDeath) sink = Mathf.Min(0.25f, v.DeadFor * 0.5f);
                if (v.Fade <= 0f) { v.Sr.enabled = false; v.Shadow.enabled = false; return; }
            }
            else if (v.AnimLoop || v.AnimDone)
            {
                if (moving) v.Play("walk", true, Mathf.Clamp(u.Stats.MoveSpeed / 2.5f, 0.7f, 2.4f)); // fast heroes stride fast
                else v.Play("idle", true);
            }

            float jitter = 0f;
            if (v.Freeze > 0f)
            {
                v.Freeze -= Time.unscaledDeltaTime; // hit-stop counts real time, even under a world slow
                jitter = (Time.frameCount & 1) == 0 ? 1f / 64f : -1f / 64f;
            }
            else v.AnimT += dt;
            int dir = IsoProjection.Dir8(u.Facing);
            bool done, flip;
            if (u is Hero spinner && spinner.WhirlUntil > _world.Time && u.Alive && _catalog.HasAnim(v.Key, "attack"))
            {
                // 휠윈드: the swing's follow-through frame (blade held out) turned through the eight facings, two turns a
                // second. The idle/walk frames spun before, the greatsword still on the back (희승 11-23: 너무 어색해).
                dir = 7 - ((int)(Time.time * 16f) & 7); // clockwise
                v.Sr.sprite = _catalog.GetAnim(v.Key, "attack", dir, _catalog.Duration(v.Key, "attack") * 0.5f, false, 1f, out _, out flip);
                done = v.AnimDone;
            }
            else v.Sr.sprite = _catalog.GetAnim(v.Key, v.Anim, dir, v.AnimT, v.AnimLoop, v.AnimSpeed, out done, out flip);
            v.Sr.flipX = flip;
            v.AnimDone = done;

            // Procedural life for art without animation frames: breathing when idle, a hop when walking.
            float sx = v.BaseScale, sy = v.BaseScale, lift = 0f;
            if (u.Alive && !_catalog.HasAnim(v.Key, v.Anim))
            {
                if (moving) lift = Mathf.Abs(Mathf.Sin(Time.time * 9f + v.Phase)) * 0.05f;
                else sy *= 1f + 0.025f * Mathf.Sin(Time.time * Mathf.PI + v.Phase);
            }
            if (v.Squash > 0f)
            {
                float k = v.Squash / 0.12f;
                sx *= 1f + 0.12f * k;
                sy *= 1f - 0.12f * k;
                v.Squash -= dt;
            }
            // Death by element: void folds into itself; the rest is in the colour below.
            if (!u.Alive && v.DeathElem == Element.Void) { float k = Mathf.Clamp01(1f - v.DeadFor / 0.35f); sx *= k; sy *= k; }
            t.localScale = new Vector3(sx, sy, 1f);

            var world = IsoProjection.ToWorld(pos);
            var push = Vector3.zero;
            if (v.KickT > 0f) { float k = v.KickT / 0.12f; push += v.Kick * (k * k); v.KickT -= dt; }
            if (!u.Alive && v.Fling != Vector3.zero)
            {
                float f = Mathf.Clamp01(v.DeadFor / 0.25f);
                push += v.Fling * (1f - (1f - f) * (1f - f)) + new Vector3(0f, Mathf.Sin(f * Mathf.PI) * 0.12f, 0f); // a short arc
            }
            t.localPosition = world + push + new Vector3(jitter, lift - sink, 0f);
            v.Sr.sortingOrder = IsoProjection.SortOrder(world, 10 + (u.Id & 3)); // id tie-break: no flicker on equal rows
            v.Shadow.sortingOrder = FloorOrder + 50;
            v.Shadow.transform.position = world;
            DrawStatus(u, v, dt);

            var c = v.Tint;
            // Figure/ground (act-visual-direction §1): actors are unlit and get only a neutral lantern falloff, so the floor's
            // coloured light never paints them the floor's colour and they always read a step brighter than the ground.
            float dl = Vec2.Dist(pos, heroPos);
            float lit = dl <= 7f ? 1f : Mathf.Lerp(1f, 0.45f, Mathf.Clamp01((dl - 7f) / 9f));
            c = new Color(c.r * lit, c.g * lit, c.b * lit, c.a);
            // Hit flash: a short white blend at most half way (d4 §6.4 — a full flash is too bright in the dark acts).
            // Hit flash through the shader (vertex colour cannot go past the art's own brightness).
            float flash = 0f;
            if (v.Flash > 0f) { v.Flash -= dt; if (GameSettings.HitFlash) flash = Mathf.Min(0.8f, v.FlashCol.a) * Mathf.Clamp01(v.Flash / v.FlashLen); } // FlashCol.a caps it (the hero: 0.55)
            if (!u.Alive)
            {
                float d = v.DeadFor;
                switch (v.DeathElem)
                {
                    case Element.Ember: c = Color.Lerp(c, new Color(0.22f, 0.1f, 0.07f, c.a), Mathf.Clamp01(d / 0.4f)); break;  // burnt black
                    case Element.Frost: c = Color.Lerp(c, new Color(0.78f, 0.92f, 1f, c.a), Mathf.Clamp01(d / 0.15f)); if (d > 0.25f) v.Fade = Mathf.Min(v.Fade, Mathf.Clamp01(1f - (d - 0.25f) / 0.2f)); break; // frozen, shatters
                    case Element.Shock: if (d < 0.14f) flash = Mathf.Max(flash, 0.9f * (1f - d / 0.14f)); break;                // one last jolt
                    case Element.Rot: c = Color.Lerp(c, new Color(0.35f, 0.6f, 0.25f, c.a), Mathf.Clamp01(d / 0.5f)); break;    // rots green
                }
            }
            // 11-23 VFX: a corpse breaks up texel by texel, its edge burning in the killing element's colour, instead of
            // turning see-through (the body stays solid while it goes).
            float dissolve = !u.Alive ? 1f - v.Fade : 0f;
            // Rim light (11-23): a blast nearby lights this unit's top rim in its colour for a moment.
            var glow = Director.GlowAt(world);
            bool rimLit = glow.a > 0.03f && v.Sr.sharedMaterial.HasProperty(RimId);
            if (flash > 0f || v.FlashSet || dissolve > 0f || v.DissolveSet || rimLit || v.RimSet)
            {
                v.Mpb ??= new MaterialPropertyBlock();
                v.Sr.GetPropertyBlock(v.Mpb);
                v.Mpb.SetColor(FlashId, new Color(v.FlashCol.r, v.FlashCol.g, v.FlashCol.b, flash));
                // A mid tone of the killing element (the light tones read as cream cotton balls over a pile, review 2).
                var edge = v.DeathElem == Element.None ? new Color(0.75f, 0.22f, 0.08f) : PixelRamp.Of(v.DeathElem).C[2];
                v.Mpb.SetColor(DissolveId, new Color(edge.r, edge.g, edge.b, dissolve));
                if (v.Sr.sharedMaterial.HasProperty(RimId))
                {
                    var baseRim = v.Sr.sharedMaterial.GetColor(RimId);
                    v.Mpb.SetColor(RimId, rimLit ? Color.Lerp(baseRim, new Color(glow.r * 1.6f, glow.g * 1.6f, glow.b * 1.6f, 0.95f), Mathf.Clamp01(glow.a * 1.5f)) : baseRim);
                }
                v.RimSet = rimLit;
                v.Sr.SetPropertyBlock(v.Mpb);
                v.FlashSet = flash > 0f;
                v.DissolveSet = dissolve > 0f;
            }
            c.a = dissolve > 0f ? 1f : v.Fade;
            v.Sr.color = c;
            v.Shadow.color = new Color(0f, 0f, 0f, 0.45f * v.Fade);
            if (v.Ring != null)
            {
                v.Ring.enabled = u.Alive;
                v.Ring.transform.position = world;
                v.Ring.sortingOrder = FloorOrder + 60;
                float r = (boss ? 2.2f : 1.25f) * (1f + 0.06f * Mathf.Sin(Time.time * 2.2f + v.Phase));
                v.Ring.transform.localScale = new Vector3(r * 1.414f / Mathf.Max(0.01f, sx), r * 0.707f / Mathf.Max(0.01f, sy), 1f);
                var a = ActAccent[Mathf.Clamp(_act, 0, ActAccent.Length - 1)];
                v.Ring.color = new Color(a.r, a.g, a.b, (0.45f + 0.15f * Mathf.Sin(Time.time * 2.2f + v.Phase)) * v.Fade);
            }
        }

    }
}
