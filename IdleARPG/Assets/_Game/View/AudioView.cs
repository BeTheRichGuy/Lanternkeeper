using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// GDD §18 소리: Sim events → short sounds, a looping ambience per act, and music that starts off.
    /// Hits and chain pops come dozens a second, so every sound has a minimum gap and a cap on copies playing, voices
    /// come from a small pool, and far-off events are quieter. Clips live in Resources/Audio/{sfx,ambient,music}
    /// (imported from art/audio); a missing clip is simply silent. M mutes everything, N toggles the music.
    /// </summary>
    public sealed class AudioView
    {
        const int Voices = 20;
        readonly AudioSource[] _voices = new AudioSource[Voices];
        readonly AudioSource _ambient, _music;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        readonly SimWorld _world;
        int _act = -1;

        Dictionary<int, Enemy> _byId;
        long _byIdTick = -1;

        public float SfxVolume = 0.45f, AmbientVolume = 0.35f, MusicVolume = 0.3f;
        public bool Muted { get; private set; }
        public bool MusicOn { get; private set; }

        public AudioView(Transform host, SimWorld world)
        {
            _world = world;
            for (int i = 0; i < Voices; i++) _voices[i] = NewSource(host, "sfx " + i, false);
            _ambient = NewSource(host, "ambient", true);
            _music = NewSource(host, "music", true);
            Muted = Prefs("snd_muted", 0) == 1;
            MusicOn = Prefs("snd_music", 0) == 1; // GDD §18: music starts off
            Apply();
        }

        static int Prefs(string key, int fallback)
        {
            try { return PlayerPrefs.GetInt(key, fallback); } catch { return fallback; }
        }

        static AudioSource NewSource(Transform host, string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        public void ToggleMute() { Muted = !Muted; PlayerPrefs.SetInt("snd_muted", Muted ? 1 : 0); Apply(); }
        public void ToggleMusic() { MusicOn = !MusicOn; PlayerPrefs.SetInt("snd_music", MusicOn ? 1 : 0); _act = -1; Apply(); }

        void Apply()
        {
            AudioListener.volume = Muted ? 0f : 1f;
            _music.volume = MusicVolume;
            _ambient.volume = AmbientVolume;
            if (!MusicOn) _music.Stop();
        }

        AudioClip Clip(string path)
        {
            if (!_clips.TryGetValue(path, out var c)) _clips[path] = c = Resources.Load<AudioClip>("Audio/" + path);
            return c;
        }

        readonly Dictionary<AudioClip, float> _gain = new Dictionary<AudioClip, float>();

        /// <summary>
        /// The effects arrive peak-normalised but their perceived loudness spans ~18 dB (RMS −11 to −29). Measure each
        /// clip once and bring it to −18 dB RMS (gain 0.35–2.5) so the per-event volumes above mean what they say.
        /// </summary>
        float Loudness(AudioClip clip)
        {
            if (_gain.TryGetValue(clip, out float g)) return g;
            g = 1f;
            if (clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.samples > 0)
            {
                var data = new float[clip.samples * clip.channels];
                if (clip.GetData(data, 0))
                {
                    double sum = 0;
                    foreach (float s in data) sum += s * s;
                    float rms = Mathf.Sqrt((float)(sum / data.Length));
                    if (rms > 1e-5f) g = Mathf.Clamp(Mathf.Pow(10f, (-18f - 20f * Mathf.Log10(rms)) / 20f), 0.35f, 2.5f);
                }
            }
            _gain[clip] = g;
            return g;
        }

        /// <summary>Act changes swap the ambience (and the music when it is on).</summary>
        public void SetAct(int act)
        {
            if (act == _act) return;
            _act = act;
            // An act without its own loop yet borrows the nearest one.
            // Each act its own loop and music (I 모래, II 밀림, III 핏덩이, IV 얼음, V 대장간, VI 심연); until a new act's
            // own file arrives it borrows the nearest old one, then act I's.
            string[] amb = { "amb_sand", "amb_jungle", "amb_flesh", "amb_ice", "amb_forge", "amb_abyss" };
            string[] ambNear = { "amb_crypt", "amb_fungal", "amb_monastery", "amb_abyss", "amb_forge", "amb_abyss" };
            string[] bgm = { "bgm_sand", "bgm_jungle", "bgm_flesh", "bgm_ice", "bgm_act4", "bgm_act5" };
            string[] bgmNear = { "bgm_act1", "bgm_act3", "bgm_act2", "bgm_act5", "bgm_act4", "bgm_act5" };
            int a = Mathf.Clamp(act, 0, amb.Length - 1);
            Loop(_ambient, Clip("ambient/" + amb[a]) ?? Clip("ambient/" + ambNear[a]) ?? Clip("ambient/amb_crypt"));
            if (MusicOn) Loop(_music, Clip("music/" + bgm[a]) ?? Clip("music/" + bgmNear[a]) ?? Clip("music/bgm_act1"));
        }

        static void Loop(AudioSource s, AudioClip c)
        {
            if (c == null) { s.Stop(); return; }
            if (s.clip == c && s.isPlaying) return;
            s.clip = c;
            s.Play();
        }

        /// <summary>
        /// Play <paramref name="id"/> unless it played less than <paramref name="gap"/> seconds ago. Louder near the hero,
        /// a little pitch spread so repeats do not sound stamped.
        /// </summary>
        /// <summary>GUI sounds (재기획 §9): a button's click, a window opening. Quiet, never stacked within 50 ms.</summary>
        public void Ui(string id) => Play(id, 0.05f, 0.55f);

        /// <summary>A full-volume one-shot from the GUI (증강 카드 뽑기).</summary>
        public void Sfx(string id) => Play(id, 0.02f, 1f);

        void Play(string id, float gap, float volume = 1f, Vec2? at = null, float pitch = 1f)
        {
            if (Muted) return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(id, out float last) && now - last < gap) return;
            var clip = Clip("sfx/" + id);
            if (clip == null) return;
            _lastPlayed[id] = now;
            if (at.HasValue)
            {
                float d = Vec2.Dist(at.Value, _world.Hero.Pos);
                volume *= d <= 6f ? 1f : Mathf.Lerp(1f, 0.25f, Mathf.Clamp01((d - 6f) / 14f));
            }
            AudioSource free = null;
            foreach (var v in _voices) if (!v.isPlaying) { free = v; break; }
            if (free == null) return; // every voice busy: the next one will do
            free.clip = clip;
            free.volume = Mathf.Min(1f, SfxVolume * volume * Loudness(clip));
            free.pitch = pitch * Random.Range(0.94f, 1.06f);
            free.Play();
        }

        /// <summary>The act's own sound when it exists, else the act I/II one it grew from.</summary>
        string Have(string id, string fallback) => Clip("sfx/" + id) != null ? id : fallback;

        /// <summary>The four new acts' bosses each have one signature sound (모래, 뿌리, 맥박, 얼음) for both techniques.</summary>
        static readonly string[] ActBossSound = { "boss_sand", "boss_roots", "boss_pulse", "boss_ice" };

        string ActBoss() => _act >= 0 && _act < ActBossSound.Length && Clip("sfx/" + ActBossSound[_act]) != null ? ActBossSound[_act] : null;

        string SlamSound(string skill)
        {
            var own = ActBoss();
            if (own != null) return own;
            switch (skill)
            {
                case "abbot_tide": return "boss_tide";
                case "queen_spores": return Have("boss_spores", "res_plague");
                case "giant_pour": case "giant_star": return Have("boss_molten", "boss_slam");
                case "apostle_star": return Have("boss_starfall", "meteor_impact");
                case "apostle_halo": return Have("boss_halo", "black_hole");
                default: return "boss_slam";
            }
        }

        string CastSoundBoss(string skill)
        {
            var own = ActBoss();
            if (own != null) return own;
            switch (skill)
            {
                case "abbot_prayer": return "boss_prayer";
                case "queen_children": return "boss_summon";
                case "giant_star": return Have("boss_molten", "boss_slam");
                case "apostle_halo": return Have("boss_halo", "black_hole");
                default: return "boss_summon";
            }
        }

        static string ResSound(Resonance r)
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

        static string HitSound(Element e)
        {
            switch (e)
            {
                case Element.Ember: return "hit_ember";
                case Element.Frost: return "hit_frost";
                case Element.Shock: return "hit_shock";
                case Element.Rot: return "hit_rot";
                case Element.Void: return "hit_void";
                default: return "hit_physical";
            }
        }

        string CastSound(string skill)
        {
            switch (skill)
            {
                case "meteor": case "evo_meteor_shower": case "evo_meteor_fall": return "meteor_impact";
                case "bell_of_judgment": case "evo_cathedral_bell": return "holy_bell";
                case "shield_bash": case "evo_rampart_bash": case "oath_barrier": case "evo_retort_barrier": return "shield_block";
                case "shock_trap": case "evo_chain_trap": return "trap_snap";
                case "time_stop": case "evo_eternal_stop": case "rewind": case "evo_reverse_time": return "time_stop";
                case "black_hole": case "evo_event_horizon": case "gravity_well": case "evo_singularity": return "black_hole";
            }
            var cls = _world.Hero.ClassId;
            return cls == "windhunter" ? "bow_shot" : cls == "starseer" ? "orb_cast" : "swing";
        }

        static bool Bony(string id) => id == "skeleton" || id == "archer" || id == "bone_shieldbearer" || id == "brute" || id == "gravelord"
            // the new acts' dry and hard bodies: bone, sandstone, bronze, ice
            || id == "bone_puppet" || id == "sandstone_sentinel" || id == "bronze_veiled_general" || id == "sarcophagus_king"
            || id == "icemail_soldier" || id == "frost_lord_knight" || id == "giant_in_ice" || id == "icicle_spider";

        public void OnTick(List<SimEvent> events)
        {
            int heroId = _world.Hero.Id;
            foreach (var ev in events)
            {
                switch (ev.Type)
                {
                    case SimEventType.Hit when ev.TargetId == heroId:
                        Play("hero_hurt", 0.3f, 0.7f);
                        break;
                    case SimEventType.Hit when ev.SourceId == heroId && !ev.FromChain:
                        Play(HitSound(ev.Elem), 0.07f, 0.55f, ev.Pos);
                        break;
                    case SimEventType.Cast when ev.SourceId == heroId:
                        Play(CastSound(ev.SkillId), 0.12f, 0.6f);
                        break;
                    case SimEventType.Resonance when Elements.IsForward(ev.Res):
                        Play(ResSound(ev.Res), 0.09f, 0.7f, ev.Pos);
                        break;
                    case SimEventType.ChainPop:
                        // Deeper pops climb a whole-tone scale (one octave by the 6th hop): a chain sounds like a run.
                        Play("chain_pop", 0.05f, 0.45f + 0.04f * Mathf.Min(ev.Depth, 6), ev.Pos, Mathf.Pow(2f, Mathf.Min(ev.Depth, 6) * 2f / 12f));
                        break;
                    case SimEventType.GrandResonance: Play("grand_resonance", 0.5f, 1f); break;
                    case SimEventType.Death:
                    {
                        Enemy dead = null;
                        if (_byId == null || _byIdTick != _world.TickCount)
                        {
                            // One lookup table per tick: a chain can kill dozens at once.
                            (_byId ??= new Dictionary<int, Enemy>()).Clear();
                            foreach (var e in _world.Enemies) _byId[e.Id] = e;
                            _byIdTick = _world.TickCount;
                        }
                        _byId.TryGetValue(ev.TargetId, out dead);
                        Play(dead != null && Bony(dead.Def.Id) ? "enemy_death_bone" : "enemy_death_flesh", 0.08f, 0.5f, ev.Pos);
                        break;
                    }
                    case SimEventType.Shot when ev.SourceId != heroId: Play("enemy_arrow", 0.15f, 0.45f, ev.From); break;
                    case SimEventType.LevelUp: Play("level_up", 0.5f, 0.9f); break;
                    case SimEventType.Rebirth: Play("evolve", 0.5f, 1f); Play("grand_resonance", 0.5f, 0.8f); break;
                    case SimEventType.Drop when ItemStyle.IsJackpot(ev.Item): Play("legendary_drop", 0.3f, 1f); break;
                    case SimEventType.Drop when ev.Item != null && ev.Item.Rarity == Rarity.Rare: Play("rare_drop", 0.2f, 0.6f, ev.Pos); break;
                    case SimEventType.RuneDrop: Play("rune_drop", 0.2f, 0.7f, ev.Pos); break;
                    case SimEventType.Runeword: Play("runeword", 0.5f, 0.9f); break;
                    case SimEventType.Evolved: Play("evolve", 0.5f, 1f); break;
                    case SimEventType.Unlocked: Play("unlock", 0.4f, 0.8f); break;
                    case SimEventType.Achievement: Play("unlock", 0.4f, 0.9f); break;
                    case SimEventType.Letter: Play("rune_drop", 0.4f, 0.8f); break;
                    case SimEventType.ChestOpened: Play("chest_open", 0.3f, 0.8f, ev.Pos); break;
                    case SimEventType.CampUnlocked: Play("camp_open", 0.5f, 0.9f); break;
                    case SimEventType.Ignited: Play("ignite", 0.5f, 1f); break;
                    case SimEventType.HeroDied: Play("hero_death", 1f, 1f); break;
                    case SimEventType.Telegraph: Play("telegraph_warn", 0.25f, 0.8f, ev.Pos); break;
                    case SimEventType.Slam: Play(SlamSound(ev.SkillId), 0.15f, 1f, ev.Pos); break;
                    case SimEventType.BossCast: Play(CastSoundBoss(ev.SkillId), 0.5f, 1f, ev.Pos); break;
                }
            }
        }
    }
}
