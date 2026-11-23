using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    // Skill system v1 — docs/specs/2026-11-19-skill-rune-synergy-design.md (§5 ring, §6 sigils, §7 evolution, §8 Ashknight).

    public enum SkillShape
    {
        Single,   // melee strike on the target
        Circle,   // burst around the hero (or around the target when AtTarget)
        Cone,     // fan in front of the hero
        Line,     // eruptions marching from the hero toward the target
        Leap,     // jump into the densest pack, land with a burst
        Pull,     // drag the pack around the target together
        Delayed,  // falls on the target's spot after a delay
        Buff,     // timed state on the hero
        Aura,     // timed field around the hero that keeps working
        Armed,    // loads a charge that fires on a later event
    }

    public enum SkillRole { Opener, Extender, Finisher, Defense, Special, Ultimate }

    /// <summary>Support glyphs (보조 문양). A sigil works on its own slot and on the next slot clockwise.</summary>
    public enum Sigil
    {
        None, Chain, Split, Wide, Focus, Haste, Execute, Throng, Echo, DeepMark, Conductor, Ripple,
        ToEmber, ToFrost, ToShock, ToRot, ToVoid, EmberGround, Leech, Rewind, Feed,
    }

    public static class Sigils
    {
        public static string Name(Sigil s)
        {
            switch (s)
            {
                case Sigil.Chain: return "연쇄";
                case Sigil.Split: return "분열";
                case Sigil.Wide: return "확산";
                case Sigil.Focus: return "집중";
                case Sigil.Haste: return "가속";
                case Sigil.Execute: return "처형";
                case Sigil.Throng: return "군중 제어";
                case Sigil.Echo: return "메아리";
                case Sigil.DeepMark: return "깊은 각인";
                case Sigil.Conductor: return "전도체";
                case Sigil.Ripple: return "파문";
                case Sigil.ToEmber: return "변환: 잿불";
                case Sigil.ToFrost: return "변환: 서리";
                case Sigil.ToShock: return "변환: 전격";
                case Sigil.ToRot: return "변환: 부패";
                case Sigil.ToVoid: return "변환: 공허";
                case Sigil.EmberGround: return "잔불 지대";
                case Sigil.Leech: return "흡혈";
                case Sigil.Rewind: return "되감기";
                case Sigil.Feed: return "공명 공급";
                default: return "";
            }
        }
    }

    /// <summary>Everything the sigils on a slot (and on the slot before it) change about one cast.</summary>
    public struct SkillMods
    {
        public float PowerMul, RadiusMul, CooldownMul;
        public int Bounces, ExtraLines, HopBonus;
        /// <summary>기억의 나무 변형 "분신 +1": more copies of this skill.</summary>
        public int ExtraCopies;
        public bool Echo, Execute, Throng, EmberGround, Rewind;
        public float ResBonus, GaugeBonus, Leech, Ripple;
        public Element Convert;
        public Sigil Own, Shared;

        public static SkillMods Neutral => new SkillMods { PowerMul = 1f, RadiusMul = 1f, CooldownMul = 1f };

        public bool Has(Sigil s) => s != Sigil.None && (Own == s || Shared == s);

        /// <summary>Category A (skill modifiers) adds up inside the category: two glyphs never multiply each other.</summary>
        public static SkillMods From(Sigil own, Sigil shared)
        {
            var m = Neutral;
            m.Own = own;
            m.Shared = shared == own ? Sigil.None : shared; // the same glyph twice counts once
            float powerAdd = 0f;
            foreach (var s in new[] { m.Own, m.Shared })
            {
                switch (s)
                {
                    case Sigil.Chain: m.Bounces = 1; break;
                    case Sigil.Split: m.ExtraLines = 2; powerAdd -= 0.25f; break;
                    case Sigil.Wide: m.RadiusMul *= 1.4f; break;
                    case Sigil.Focus: m.RadiusMul *= 0.7f; powerAdd += 0.35f; break;
                    case Sigil.Haste: m.CooldownMul *= 0.75f; powerAdd -= 0.15f; break;
                    case Sigil.Execute: m.Execute = true; break;
                    case Sigil.Throng: m.Throng = true; break;
                    case Sigil.Echo: m.Echo = true; break;
                    case Sigil.DeepMark: m.ResBonus += 0.25f; break;
                    case Sigil.Conductor: m.HopBonus += 1; break;
                    case Sigil.Ripple: m.Ripple += 0.3f; break;
                    case Sigil.ToEmber: m.Convert = Element.Ember; break;
                    case Sigil.ToFrost: m.Convert = Element.Frost; break;
                    case Sigil.ToShock: m.Convert = Element.Shock; break;
                    case Sigil.ToRot: m.Convert = Element.Rot; break;
                    case Sigil.ToVoid: m.Convert = Element.Void; break;
                    case Sigil.EmberGround: m.EmberGround = true; break;
                    case Sigil.Leech: m.Leech += 0.02f; break;
                    case Sigil.Rewind: m.Rewind = true; break;
                    case Sigil.Feed: m.GaugeBonus += 0.5f; break;
                }
            }
            m.PowerMul = Math.Max(0.1f, 1f + powerAdd);
            return m;
        }
    }

    public sealed class SkillDef
    {
        public string Id, Name = "";
        public Element Elem;
        public float Cooldown;
        /// <summary>Multiplier on the hero's base damage.</summary>
        public float Power = 1f;
        public SkillShape Shape;
        /// <summary>Reach for Single, area radius for the others (cells).</summary>
        public float Radius = 1.4f;
        public SkillRole Role;
        /// <summary>0 basic, 1 core, 2 advanced, 3 ultimate.</summary>
        public int Tier;
        public int Unlock = 1;
        public float Arc = 100f, Length = 6f, Duration, Delay;
        public int Hits = 1;

        // Evolution (§7): two of {uses, sigil on the slot, essence equipped}.
        public string EvolveTo, EvolveSigilName;
        public int EvolveUses;
        public Sigil EvolveSigil;
        public string EvolveEssence;
        public string EvolvedFrom;

        // Behaviour knobs (mostly what an evolution changes).
        public float Pull, GroundSeconds, ThornRadius;
        public int Bolts, Lines = 1, Charges = 1;
        public bool ShardFrost, HealBurst, ExecuteSpread, LinkShare, AlwaysEcho, AtTarget;

        // Oathknight knobs (spec §9).
        public float StunSeconds, ShieldPct, SharePct, AuraHeal, GroundHeal;
        public int Bounces, Spears;
        public bool Taunt, TauntMark, AuraPulse, Bell, LeapToTarget, BarrierDouble, Ground;

        // Windhunter knobs (spec §9-1).
        public bool Ranged, LeapAway, ChainIgnite;
        public float DotPower, BurstRadius, HuntedPct, GroundPull, EndPower, Range = 5f;

        // Starseer knobs (spec §9-2).
        public int Constellation;

        // Signature skills (SimWorld.Classes.cs).
        /// <summary>휠윈드: seconds of spinning.</summary>
        public float Channel;
        /// <summary>속사: extra arrows, one a tick. 갈래 화살: splits on hit, and how many times the splits split again.</summary>
        public int Volley, Forks, ForkDepth = 1;
        /// <summary>연소: the burn left on the target goes off ×this. 빙결 파쇄: extra damage on a frozen target.</summary>
        public float Combust, FrozenBonus;

        /// <summary>도탄: a projectile that flies Length cells and bounces off walls this many times, hitting all it passes.</summary>
        public int WallBounces;
        public float TimeStopMul, RewindSeconds, TrailSeconds;

        public bool IsEvolution => EvolvedFrom != null;

        /// <summary>Thrown or targeted at range: what extra copies (투사체·분신) duplicate.</summary>
        public bool Copyable => Ranged || AtTarget || WallBounces > 0 || Shape == SkillShape.Delayed || Shape == SkillShape.Line;
        public string BaseId => EvolvedFrom ?? Id;

        public SkillDef Clone() => (SkillDef)MemberwiseClone();
    }

    /// <summary>Basic attack + 4 ring slots (+ ultimate). Casts the first ready slot in ring order (GDD §5.1).</summary>
    public sealed class SkillRing
    {
        public readonly SkillDef Basic;
        public readonly SkillDef[] Slots;
        public readonly Sigil[] Sigils = new Sigil[4];
        public SkillDef Ultimate;
        readonly double[] _readyAt;
        double _ultReadyAt;

        public SkillRing(SkillDef basic, SkillDef[] slots)
        {
            if (slots == null || slots.Length != 4) throw new ArgumentException("ring has exactly 4 slots", nameof(slots));
            Basic = basic;
            Slots = slots;
            _readyAt = new double[slots.Length];
        }

        public SkillDef NextReady(double now) => NextReady(now, out _);

        /// <summary>First ready slot in clockwise order; <paramref name="slot"/> is −1 for the basic attack.</summary>
        public SkillDef NextReady(double now, out int slot)
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] != null && now >= _readyAt[i]) { slot = i; return Slots[i]; }
            slot = -1;
            return Basic;
        }

        public bool UltimateReady(double now) => Ultimate != null && now >= _ultReadyAt;
        public void MarkUltimateUsed(double now, double cooldown) => _ultReadyAt = now + cooldown;
        public double UltimateLeft(double now) => Math.Max(0.0, _ultReadyAt - now);

        public void MarkUsed(SkillDef skill, double now) => MarkUsed(Array.IndexOf(Slots, skill), now, skill.Cooldown);

        public void MarkUsed(int slot, double now, double cooldown)
        {
            if (slot >= 0) _readyAt[slot] = now + cooldown;
        }

        /// <summary>"되감기": pull a slot's cooldown forward.</summary>
        public void Reduce(int slot, double seconds)
        {
            if (slot >= 0 && slot < _readyAt.Length) _readyAt[slot] -= seconds;
        }

        public double CooldownLeft(int slot, double now) => Math.Max(0.0, _readyAt[slot] - now);

        /// <summary>A slot gets its own glyph and the glyph of the slot before it (clockwise neighbour, Rite Link).</summary>
        public SkillMods ModsFor(int slot)
        {
            if (slot < 0) return SkillMods.Neutral;
            return SkillMods.From(Sigils[slot], Sigils[(slot + Slots.Length - 1) % Slots.Length]);
        }

        /// <summary>Keep cooldown timers of skills that stay in the ring when it is rebuilt.</summary>
        public void CopyTimersFrom(SkillRing old)
        {
            if (old == null) return;
            for (int i = 0; i < Slots.Length; i++)
                for (int j = 0; j < old.Slots.Length; j++)
                    if (Slots[i] != null && old.Slots[j] != null && old.Slots[j].BaseId == Slots[i].BaseId) _readyAt[i] = old._readyAt[j];
            _ultReadyAt = old._ultReadyAt;
        }
    }

    public sealed class RingPreset
    {
        public string Name;
        public string[] Ring;      // final ring, clockwise
        public Sigil[] Sigils;     // per slot
        public string Ultimate;
        public string[] Priority;  // which skills to take first while the ring is still small
        /// <summary>Talent branch this build leans on (0 offense, 1 defense, 2 synergy).</summary>
        public int TalentFocus = 2;
    }

    /// <summary>Ashknight (잿불기사) skills, evolutions and recommended rings. All numbers live here.</summary>
    public static class SkillBook
    {
        public static readonly SkillDef Basic = new SkillDef { Id = "basic", Name = "기본 공격", Elem = Element.None, Cooldown = 0f, Power = 1f, Shape = SkillShape.Single, Radius = 1.4f };

        static readonly List<SkillDef> _ashknight = new List<SkillDef>
        {
            new SkillDef { Id = "ember_slash", Name = "잿불 베기", Elem = Element.Ember, Cooldown = 1.5f, Power = 1.6f, Shape = SkillShape.Single, Radius = 1.4f,
                Role = SkillRole.Opener, Tier = 0, Unlock = 1, EvolveTo = "evo_twin_ember", EvolveUses = 800, EvolveSigil = Sigil.Echo, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "ember_whirl", Name = "휠윈드", Elem = Element.Ember, Cooldown = 6f, Power = 0.65f, Shape = SkillShape.Circle, Radius = 2.2f, Channel = 2.5f,
                Role = SkillRole.Extender, Tier = 0, Unlock = 1, EvolveTo = "evo_flame_vortex", EvolveUses = 800, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "frost_brand", Name = "서리 낙인", Elem = Element.Frost, Cooldown = 3f, Power = 1.3f, Shape = SkillShape.Single, Radius = 1.4f,
                Role = SkillRole.Opener, Tier = 0, Unlock = 2, EvolveTo = "evo_glacial_brand", EvolveUses = 800, EvolveSigil = Sigil.DeepMark, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "heat_absorb", Name = "열기 흡수", Elem = Element.Ember, Cooldown = 8f, Power = 0.6f, Shape = SkillShape.Circle, Radius = 2f,
                Role = SkillRole.Defense, Tier = 0, Unlock = 4, EvolveTo = "evo_ember_heart", EvolveUses = 800, EvolveSigil = Sigil.Leech, EvolveEssence = "ash_feast" },
            new SkillDef { Id = "ash_leap", Name = "잿더미 도약", Elem = Element.Ember, Cooldown = 7f, Power = 1.5f, Shape = SkillShape.Leap, Radius = 2f,
                Role = SkillRole.Extender, Tier = 1, Unlock = 4, EvolveTo = "evo_meteor_fall", EvolveUses = 600, EvolveSigil = Sigil.Conductor, EvolveEssence = "long_chain" },
            new SkillDef { Id = "storm_cleave", Name = "폭풍 가르기", Elem = Element.Shock, Cooldown = 4f, Power = 1.5f, Shape = SkillShape.Cone, Radius = 3f, Arc = 100f,
                Role = SkillRole.Finisher, Tier = 1, Unlock = 6, EvolveTo = "evo_thunder_cleave", EvolveUses = 600, EvolveSigil = Sigil.Chain, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "forge_strike", Name = "용광 일격", Elem = Element.Ember, Cooldown = 5f, Power = 2.6f, Shape = SkillShape.Single, Radius = 1.4f,
                Role = SkillRole.Finisher, Tier = 1, Unlock = 6, EvolveTo = "evo_molten_execution", EvolveUses = 600, EvolveSigil = Sigil.Execute, EvolveEssence = "ash_feast" },
            new SkillDef { Id = "chain_hook", Name = "쇠사슬 끌기", Elem = Element.Void, Cooldown = 5f, Power = 1.3f, Shape = SkillShape.Pull, Radius = 3f, Pull = 0.7f,
                Role = SkillRole.Extender, Tier = 1, Unlock = 8, EvolveTo = "evo_abyss_chain", EvolveUses = 600, EvolveSigil = Sigil.Throng, EvolveEssence = "long_chain" },
            new SkillDef { Id = "cinder_echo", Name = "재의 메아리", Elem = Element.Ember, Cooldown = 4f, Power = 1.8f, Shape = SkillShape.Armed, Radius = 2.2f, Delay = 1.2f, Charges = 1,
                Role = SkillRole.Extender, Tier = 1, Unlock = 8, EvolveTo = "evo_cinder_grave", EvolveUses = 600, EvolveSigil = Sigil.Ripple, EvolveEssence = "long_chain" },
            new SkillDef { Id = "thorn_mail", Name = "가시 갑주", Elem = Element.Ember, Cooldown = 10f, Power = 0.6f, Shape = SkillShape.Buff, Duration = 6f,
                Role = SkillRole.Defense, Tier = 1, Unlock = 8, EvolveTo = "evo_lava_mail", EvolveUses = 600, EvolveSigil = Sigil.EmberGround, EvolveEssence = "ash_feast" },
            new SkillDef { Id = "fissure", Name = "화산 균열", Elem = Element.Ember, Cooldown = 5f, Power = 1.4f, Shape = SkillShape.Line, Radius = 0.9f, Length = 6f,
                Role = SkillRole.Extender, Tier = 2, Unlock = 12, EvolveTo = "evo_triple_fissure", EvolveUses = 400, EvolveSigil = Sigil.Split, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "molten_heart", Name = "녹은 심장", Elem = Element.Ember, Cooldown = 10f, Power = 0.5f, Shape = SkillShape.Buff, Duration = 4f,
                Role = SkillRole.Special, Tier = 2, Unlock = 12, EvolveTo = "evo_erupting_heart", EvolveUses = 400, EvolveSigil = Sigil.Conductor, EvolveEssence = "long_chain" },
            new SkillDef { Id = "frostfire_aura", Name = "서리불꽃 갑옷", Elem = Element.Frost, Cooldown = 7f, Power = 0.35f, Shape = SkillShape.Aura, Radius = 2f, Duration = 6f,
                Role = SkillRole.Special, Tier = 2, Unlock = 16, EvolveTo = "evo_eternal_frostfire", EvolveUses = 400, EvolveSigil = Sigil.DeepMark, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "smith_hammer", Name = "대장장이의 망치", Elem = Element.Shock, Cooldown = 7f, Power = 1.8f, Shape = SkillShape.Delayed, Radius = 2f, Delay = 0.8f,
                Role = SkillRole.Finisher, Tier = 2, Unlock = 16, EvolveTo = "evo_heaven_anvil", EvolveUses = 400, EvolveSigil = Sigil.Echo, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "forge_judgment", Name = "대장간의 심판", Elem = Element.Ember, Cooldown = 30f, Power = 3f, Shape = SkillShape.Circle, Radius = 4f, AtTarget = true,
                Role = SkillRole.Ultimate, Tier = 3, Unlock = 30 },
            new SkillDef { Id = "undying_flame", Name = "꺼지지 않는 불꽃", Elem = Element.Ember, Cooldown = 40f, Power = 0f, Shape = SkillShape.Buff, Duration = 8f,
                Role = SkillRole.Ultimate, Tier = 3, Unlock = 30 },
        };

        /// <summary>서약기사 (spec §9): frost shield → golden lightning (전격, drawn as 성광) overload axis, oath resource.</summary>
        static readonly List<SkillDef> _oathknight = new List<SkillDef>
        {
            new SkillDef { Id = "blessed_hammer", Name = "축복의 망치", Elem = Element.Shock, Cooldown = 1.5f, Power = 1.1f, Shape = SkillShape.Circle, Radius = 2.6f,
                Role = SkillRole.Opener, Tier = 0, Unlock = 1, EvolveTo = "evo_hammer_storm", EvolveUses = 800, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "zeal", Name = "열성", Elem = Element.Shock, Cooldown = 2.5f, Power = 0.5f, Shape = SkillShape.Single, Radius = 1.4f, Hits = 4,
                Role = SkillRole.Extender, Tier = 0, Unlock = 2, EvolveTo = "evo_fanaticism", EvolveUses = 800, EvolveSigil = Sigil.Haste, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "shield_bash", Name = "방패 강타", Elem = Element.Frost, Cooldown = 2f, Power = 1.3f, Shape = SkillShape.Single, Radius = 1.4f, StunSeconds = 0.8f,
                Role = SkillRole.Opener, Tier = 0, Unlock = 1, EvolveTo = "evo_rampart_bash", EvolveUses = 800, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "hallowed_aura", Name = "성화 오라", Elem = Element.Shock, Cooldown = 7f, Power = 0.35f, Shape = SkillShape.Aura, Radius = 2.2f, Duration = 6f, AuraPulse = true,
                Role = SkillRole.Special, Tier = 0, Unlock = 1, EvolveTo = "evo_pilgrim_light", EvolveUses = 800, EvolveSigil = Sigil.Leech, EvolveEssence = "ash_feast" },
            new SkillDef { Id = "rallying_cry", Name = "도발의 외침", Elem = Element.Frost, Cooldown = 9f, Power = 0.6f, Shape = SkillShape.Circle, Radius = 3f, Taunt = true, Duration = 4f,
                Role = SkillRole.Defense, Tier = 0, Unlock = 2, EvolveTo = "evo_iron_cry", EvolveUses = 800, EvolveSigil = Sigil.DeepMark, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "judgment", Name = "심판", Elem = Element.Shock, Cooldown = 4f, Power = 2.2f, Shape = SkillShape.Single, Radius = 1.4f,
                Role = SkillRole.Finisher, Tier = 0, Unlock = 4, EvolveTo = "evo_double_judgment", EvolveUses = 800, EvolveSigil = Sigil.Echo, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "oath_barrier", Name = "맹세의 방벽", Elem = Element.Shock, Cooldown = 10f, Power = 1.5f, Shape = SkillShape.Buff, Radius = 2.5f, Duration = 6f, ShieldPct = 0.25f,
                Role = SkillRole.Defense, Tier = 1, Unlock = 4, EvolveTo = "evo_retort_barrier", EvolveUses = 600, EvolveSigil = Sigil.EmberGround, EvolveEssence = "ash_feast" },
            new SkillDef { Id = "martyr_chains", Name = "순교자의 사슬", Elem = Element.Void, Cooldown = 10f, Power = 0.6f, Shape = SkillShape.Buff, Radius = 3f, Duration = 6f, SharePct = 0.3f,
                Role = SkillRole.Special, Tier = 1, Unlock = 6, EvolveTo = "evo_confession_chains", EvolveUses = 600, EvolveSigil = Sigil.Throng, EvolveEssence = "long_chain" },
            new SkillDef { Id = "consecrate", Name = "축성된 대지", Elem = Element.Shock, Cooldown = 8f, Power = 0.45f, Shape = SkillShape.Circle, Radius = 2.5f, Duration = 5f, Ground = true, GroundHeal = 0.02f,
                Role = SkillRole.Extender, Tier = 1, Unlock = 6, EvolveTo = "evo_holy_ground", EvolveUses = 600, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "shield_throw", Name = "방패 투척", Elem = Element.Frost, Cooldown = 4f, Power = 1.0f, Shape = SkillShape.Single, Radius = 5f, Bounces = 3,
                Role = SkillRole.Opener, Tier = 1, Unlock = 8, EvolveTo = "evo_returning_shield", EvolveUses = 600, EvolveSigil = Sigil.Chain, EvolveEssence = "long_chain" },
            new SkillDef { Id = "bell_of_judgment", Name = "심판의 종", Elem = Element.Shock, Cooldown = 12f, Power = 1.2f, Shape = SkillShape.Circle, Radius = 4f, Bell = true,
                Role = SkillRole.Finisher, Tier = 1, Unlock = 8, EvolveTo = "evo_cathedral_bell", EvolveUses = 600, EvolveSigil = Sigil.Feed, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "holy_charge", Name = "성스러운 돌진", Elem = Element.Shock, Cooldown = 7f, Power = 1.4f, Shape = SkillShape.Leap, Radius = 2f, LeapToTarget = true, StunSeconds = 0.5f,
                Role = SkillRole.Extender, Tier = 1, Unlock = 8, EvolveTo = "evo_charge_of_light", EvolveUses = 600, EvolveSigil = Sigil.Conductor, EvolveEssence = "long_chain" },
            new SkillDef { Id = "grace_echo", Name = "은총의 메아리", Elem = Element.Shock, Cooldown = 9f, Power = 1.0f, Shape = SkillShape.Armed, Radius = 1.8f, Charges = 3,
                Role = SkillRole.Defense, Tier = 2, Unlock = 12, EvolveTo = "evo_grace_chorus", EvolveUses = 400, EvolveSigil = Sigil.Echo, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "unbroken_vow", Name = "무너지지 않는 서약", Elem = Element.Shock, Cooldown = 15f, Power = 1f, Shape = SkillShape.Buff, Radius = 3f, Duration = 5f,
                Role = SkillRole.Special, Tier = 2, Unlock = 12, EvolveTo = "evo_deathless_vow", EvolveUses = 400, EvolveSigil = Sigil.Conductor, EvolveEssence = "ash_feast" },
            new SkillDef { Id = "heaven_spears", Name = "천벌", Elem = Element.Shock, Cooldown = 6f, Power = 1.4f, Shape = SkillShape.Delayed, Radius = 1.2f, Delay = 0.6f, Spears = 3,
                Role = SkillRole.Finisher, Tier = 1, Unlock = 6, EvolveTo = "evo_spear_rain", EvolveUses = 400, EvolveSigil = Sigil.Split, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "warding_sigil", Name = "수호의 각인", Elem = Element.Frost, Cooldown = 8f, Power = 0.25f, Shape = SkillShape.Circle, Radius = 2f, Duration = 6f, Ground = true, AtTarget = true,
                Role = SkillRole.Opener, Tier = 2, Unlock = 16, EvolveTo = "evo_sealing_sigil", EvolveUses = 400, EvolveSigil = Sigil.DeepMark, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "grand_judgment", Name = "대심판", Elem = Element.Shock, Cooldown = 30f, Power = 3.5f, Shape = SkillShape.Circle, Radius = 5f,
                Role = SkillRole.Ultimate, Tier = 3, Unlock = 30 },
            new SkillDef { Id = "sanctuary", Name = "성역", Elem = Element.Shock, Cooldown = 40f, Power = 0.8f, Shape = SkillShape.Buff, Radius = 2f, Duration = 8f,
                Role = SkillRole.Ultimate, Tier = 3, Unlock = 30 },
        };

        /// <summary>바람사냥꾼 basic attack: a bow shot (reach 6.5), so the hunter keeps its distance.</summary>
        public static readonly SkillDef BowBasic = new SkillDef { Id = "bow_shot", Name = "활 쏘기", Elem = Element.None, Cooldown = 0f, Power = 1f, Shape = SkillShape.Single, Radius = 6.5f, Ranged = true };

        /// <summary>바람사냥꾼 (spec §9-1): shock → rot plague axis, rot → void devour, ranged, focus resource.</summary>
        static readonly List<SkillDef> _windhunter = new List<SkillDef>
        {
            new SkillDef { Id = "chain_arrow", Name = "갈래 화살", Elem = Element.Shock, Cooldown = 2f, Power = 1.2f, Shape = SkillShape.Single, Radius = 5.5f, Ranged = true, Forks = 3,
                Role = SkillRole.Opener, Tier = 0, Unlock = 1, EvolveTo = "evo_storm_arrow", EvolveUses = 800, EvolveSigil = Sigil.Chain, EvolveEssence = "long_chain" },
            new SkillDef { Id = "venom_arrow", Name = "독침 화살", Elem = Element.Rot, Cooldown = 2.5f, Power = 1.3f, Shape = SkillShape.Single, Radius = 5.5f, Ranged = true, DotPower = 0.9f,
                Role = SkillRole.Opener, Tier = 0, Unlock = 2, EvolveTo = "evo_plague_arrow", EvolveUses = 800, EvolveSigil = Sigil.Wide, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "whirling_blades", Name = "회오리 칼날", Elem = Element.Shock, Cooldown = 5f, Power = 1.2f, Shape = SkillShape.Circle, Radius = 2.2f,
                Role = SkillRole.Extender, Tier = 0, Unlock = 1, EvolveTo = "evo_blade_storm", EvolveUses = 800, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "roll_shot", Name = "구르기 사격", Elem = Element.Shock, Cooldown = 6f, Power = 1.4f, Shape = SkillShape.Leap, Radius = 5f, LeapAway = true, Ranged = true,
                Role = SkillRole.Extender, Tier = 0, Unlock = 4, EvolveTo = "evo_triple_shot", EvolveUses = 800, EvolveSigil = Sigil.Echo, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "shock_trap", Name = "전격 덫", Elem = Element.Shock, Cooldown = 7f, Power = 0.5f, Shape = SkillShape.Circle, Radius = 1.8f, Duration = 4f, Ground = true, AtTarget = true, ChainIgnite = true,
                Role = SkillRole.Extender, Tier = 1, Unlock = 4, EvolveTo = "evo_chain_trap", EvolveUses = 600, EvolveSigil = Sigil.Conductor, EvolveEssence = "long_chain" },
            new SkillDef { Id = "wind_path", Name = "바람길", Elem = Element.Void, Cooldown = 6f, Power = 0.9f, Shape = SkillShape.Line, Radius = 1.1f, Length = 6f, Pull = 0.5f,
                Role = SkillRole.Extender, Tier = 1, Unlock = 6, EvolveTo = "evo_storm_path", EvolveUses = 600, EvolveSigil = Sigil.Split, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "storm_eye", Name = "폭풍의 눈", Elem = Element.Shock, Cooldown = 12f, Power = 0f, Shape = SkillShape.Buff, Duration = 6f,
                Role = SkillRole.Special, Tier = 1, Unlock = 6, EvolveTo = "evo_typhoon_eye", EvolveUses = 600, EvolveSigil = Sigil.Haste, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "rot_cloud", Name = "부패의 구름", Elem = Element.Rot, Cooldown = 7f, Power = 0.5f, Shape = SkillShape.Circle, Radius = 2.5f, Duration = 5f, Ground = true, AtTarget = true,
                Role = SkillRole.Opener, Tier = 1, Unlock = 8, EvolveTo = "evo_plague_fog", EvolveUses = 600, EvolveSigil = Sigil.Wide, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "arrow_rain", Name = "화살비", Elem = Element.Shock, Cooldown = 7f, Power = 1.1f, Shape = SkillShape.Delayed, Radius = 1.4f, Delay = 0.5f, Spears = 6, Range = 6f,
                Role = SkillRole.Finisher, Tier = 1, Unlock = 8, EvolveTo = "evo_steel_rain", EvolveUses = 600, EvolveSigil = Sigil.Split, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "ricochet_arrow", Name = "도탄 화살", Elem = Element.Shock, Cooldown = 3f, Power = 0.9f, Shape = SkillShape.Single, Radius = 6f, Ranged = true, WallBounces = 3, Length = 16f,
                Role = SkillRole.Opener, Tier = 1, Unlock = 6, EvolveTo = "evo_ricochet_storm", EvolveUses = 600, EvolveSigil = Sigil.Split, EvolveEssence = "long_chain" },
            new SkillDef { Id = "hunters_mark", Name = "사냥꾼의 표식", Elem = Element.Void, Cooldown = 8f, Power = 0.8f, Shape = SkillShape.Single, Radius = 6f, Ranged = true, HuntedPct = 0.3f, Duration = 6f,
                Role = SkillRole.Opener, Tier = 1, Unlock = 8, EvolveTo = "evo_prey_brand", EvolveUses = 600, EvolveSigil = Sigil.Ripple, EvolveEssence = "long_chain" },
            new SkillDef { Id = "shadow_decoy", Name = "그림자 분신", Elem = Element.Void, Cooldown = 10f, Power = 0.2f, Shape = SkillShape.Circle, Radius = 2.5f, Duration = 4f, Ground = true, AtTarget = true, GroundPull = 0.3f, EndPower = 1.8f,
                Role = SkillRole.Special, Tier = 2, Unlock = 12, EvolveTo = "evo_shadow_legion", EvolveUses = 400, EvolveSigil = Sigil.Throng, EvolveEssence = "long_chain" },
            new SkillDef { Id = "void_lance", Name = "공허 관통", Elem = Element.Void, Cooldown = 6f, Power = 2.0f, Shape = SkillShape.Line, Radius = 0.8f, Length = 8f,
                Role = SkillRole.Finisher, Tier = 2, Unlock = 12, EvolveTo = "evo_abyss_lance", EvolveUses = 400, EvolveSigil = Sigil.Focus, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "gale_volley", Name = "속사", Elem = Element.Shock, Cooldown = 4f, Power = 0.55f, Shape = SkillShape.Single, Radius = 5.5f, Ranged = true, Volley = 7,
                Role = SkillRole.Finisher, Tier = 0, Unlock = 2, EvolveTo = "evo_storm_volley", EvolveUses = 400, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "viper_nest", Name = "독사의 둥지", Elem = Element.Rot, Cooldown = 9f, Power = 0.3f, Shape = SkillShape.Armed, Radius = 1.6f, Charges = 4,
                Role = SkillRole.Special, Tier = 2, Unlock = 16, EvolveTo = "evo_viper_den", EvolveUses = 400, EvolveSigil = Sigil.Conductor, EvolveEssence = "long_chain" },
            new SkillDef { Id = "thunderstorm", Name = "천둥 폭풍", Elem = Element.Shock, Cooldown = 35f, Power = 1.5f, Shape = SkillShape.Buff, Radius = 1.2f, Duration = 8f,
                Role = SkillRole.Ultimate, Tier = 3, Unlock = 30 },
            new SkillDef { Id = "hunt_time", Name = "사냥의 시간", Elem = Element.Void, Cooldown = 40f, Power = 0f, Shape = SkillShape.Buff, Duration = 8f,
                Role = SkillRole.Ultimate, Tier = 3, Unlock = 30 },
        };

        /// <summary>별점술사 basic attack: a star orb (reach 6.5).</summary>
        public static readonly SkillDef OrbBasic = new SkillDef { Id = "star_orb", Name = "별빛 구슬", Elem = Element.None, Cooldown = 0f, Power = 1f, Shape = SkillShape.Single, Radius = 6.5f, Ranged = true };

        /// <summary>별점술사 (spec §9-2): void → ember collapse, ember → frost shatter; star blessing loops the ring back.</summary>
        static readonly List<SkillDef> _starseer = new List<SkillDef>
        {
            // 원소 마법 (희승: 불은 화상, 냉기는 빙결, 번개는 감전과 전이 — SimWorld.Classes.cs)
            new SkillDef { Id = "fireball", Name = "화염구", Elem = Element.Ember, Cooldown = 1.5f, Power = 1.3f, Shape = SkillShape.Single, Radius = 5.5f, Ranged = true, BurstRadius = 1.2f,
                Role = SkillRole.Opener, Tier = 0, Unlock = 1, EvolveTo = "evo_great_fireball", EvolveUses = 800, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "combustion", Name = "연소", Elem = Element.Ember, Cooldown = 4f, Power = 1.0f, Shape = SkillShape.Circle, Radius = 2.2f, AtTarget = true, Combust = 2.5f,
                Role = SkillRole.Finisher, Tier = 0, Unlock = 2, EvolveTo = "evo_inferno", EvolveUses = 800, EvolveSigil = Sigil.Execute, EvolveEssence = "ash_feast" },
            new SkillDef { Id = "ice_bolt", Name = "얼음 화살", Elem = Element.Frost, Cooldown = 1.5f, Power = 1.3f, Shape = SkillShape.Single, Radius = 5.5f, Ranged = true,
                Role = SkillRole.Opener, Tier = 0, Unlock = 1, EvolveTo = "evo_ice_lance", EvolveUses = 800, EvolveSigil = Sigil.Echo, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "ice_shatter", Name = "빙결 파쇄", Elem = Element.Frost, Cooldown = 4f, Power = 1.4f, Shape = SkillShape.Circle, Radius = 2.2f, AtTarget = true, FrozenBonus = 1.5f,
                Role = SkillRole.Finisher, Tier = 0, Unlock = 2, EvolveTo = "evo_glacier_break", EvolveUses = 800, EvolveSigil = Sigil.Execute, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "blizzard", Name = "눈보라", Elem = Element.Frost, Cooldown = 8f, Power = 0.45f, Shape = SkillShape.Circle, Radius = 2.6f, Duration = 5f, Ground = true, AtTarget = true,
                Role = SkillRole.Extender, Tier = 1, Unlock = 4, EvolveTo = "evo_whiteout", EvolveUses = 600, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "chain_lightning", Name = "연쇄 번개", Elem = Element.Shock, Cooldown = 1.8f, Power = 1.1f, Shape = SkillShape.Single, Radius = 5.5f, Ranged = true, Bounces = 4,
                Role = SkillRole.Opener, Tier = 0, Unlock = 1, EvolveTo = "evo_storm_chain", EvolveUses = 800, EvolveSigil = Sigil.Chain, EvolveEssence = "long_chain" },
            new SkillDef { Id = "thunder_nova", Name = "번개 폭발", Elem = Element.Shock, Cooldown = 4f, Power = 1.6f, Shape = SkillShape.Circle, Radius = 2.8f,
                Role = SkillRole.Finisher, Tier = 0, Unlock = 2, EvolveTo = "evo_thunder_crown", EvolveUses = 800, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "lightning_storm", Name = "번개 폭풍", Elem = Element.Shock, Cooldown = 7f, Power = 1.0f, Shape = SkillShape.Delayed, Radius = 1.3f, Delay = 0.4f, Spears = 5, Range = 6f,
                Role = SkillRole.Extender, Tier = 1, Unlock = 4, EvolveTo = "evo_tempest", EvolveUses = 600, EvolveSigil = Sigil.Split, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "ball_lightning", Name = "구슬 번개", Elem = Element.Shock, Cooldown = 5f, Power = 1.2f, Shape = SkillShape.Line, Radius = 1f, Length = 7f,
                Role = SkillRole.Extender, Tier = 1, Unlock = 6, EvolveTo = "evo_twin_ball", EvolveUses = 600, EvolveSigil = Sigil.Split, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "meteor", Name = "운석", Elem = Element.Ember, Cooldown = 5f, Power = 2.4f, Shape = SkillShape.Delayed, Radius = 2f, Delay = 0.9f, Spears = 1, Range = 6f,
                Role = SkillRole.Finisher, Tier = 0, Unlock = 1, EvolveTo = "evo_meteor_shower", EvolveUses = 800, EvolveSigil = Sigil.Split, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "frost_nova", Name = "서리 별빛", Elem = Element.Frost, Cooldown = 6f, Power = 1.0f, Shape = SkillShape.Circle, Radius = 2.5f, StunSeconds = 0.6f,
                Role = SkillRole.Defense, Tier = 0, Unlock = 2, EvolveTo = "evo_glacial_nebula", EvolveUses = 800, EvolveSigil = Sigil.Wide, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "nebula_burst", Name = "성운 폭발", Elem = Element.Ember, Cooldown = 5f, Power = 1.6f, Shape = SkillShape.Circle, Radius = 2.5f, AtTarget = true,
                Role = SkillRole.Finisher, Tier = 1, Unlock = 8, EvolveTo = "evo_great_nebula", EvolveUses = 600, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "supernova", Name = "초신성", Elem = Element.Ember, Cooldown = 12f, Power = 2.8f, Shape = SkillShape.Circle, Radius = 3.5f,
                Role = SkillRole.Finisher, Tier = 2, Unlock = 12, EvolveTo = "evo_hypernova", EvolveUses = 400, EvolveSigil = Sigil.Wide, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "starfall_field", Name = "별빛 장판", Elem = Element.Frost, Cooldown = 8f, Power = 0.35f, Shape = SkillShape.Circle, Radius = 3f, Duration = 5f, Ground = true,
                Role = SkillRole.Opener, Tier = 2, Unlock = 16, EvolveTo = "evo_radiant_field", EvolveUses = 400, EvolveSigil = Sigil.DeepMark, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "star_collapse", Name = "별의 붕괴", Elem = Element.Void, Cooldown = 30f, Power = 4f, Shape = SkillShape.Circle, Radius = 5f, AtTarget = true,
                Role = SkillRole.Ultimate, Tier = 3, Unlock = 30 },
            new SkillDef { Id = "eternal_moment", Name = "영원의 순간", Elem = Element.Void, Cooldown = 40f, Power = 0f, Shape = SkillShape.Buff, Duration = 6f,
                Role = SkillRole.Ultimate, Tier = 3, Unlock = 30 },
        };

        /// <summary>별점술사 star skills from before the elemental mage (희승 11-21): known by id (old saves, evolutions), not in the
        /// mage's list, tree or rings any more.</summary>
        static readonly List<SkillDef> _starLegacy = new List<SkillDef>
        {
            new SkillDef { Id = "star_bolt", Name = "별똥 화살", Elem = Element.Void, Cooldown = 1.8f, Power = 1.3f, Shape = SkillShape.Single, Radius = 5.5f, Ranged = true,
                Role = SkillRole.Opener, Tier = 0, Unlock = 1, EvolveTo = "evo_twin_star", EvolveUses = 800, EvolveSigil = Sigil.Echo, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "star_blessing", Name = "별의 축복", Elem = Element.Void, Cooldown = 14f, Power = 0f, Shape = SkillShape.Buff, Duration = 6f,
                Role = SkillRole.Special, Tier = 0, Unlock = 4, EvolveTo = "evo_galaxy_blessing", EvolveUses = 800, EvolveSigil = Sigil.Haste, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "black_hole", Name = "블랙홀", Elem = Element.Void, Cooldown = 9f, Power = 0.3f, Shape = SkillShape.Circle, Radius = 2.8f, Duration = 4f, Ground = true, AtTarget = true, GroundPull = 0.5f, EndPower = 2f,
                Role = SkillRole.Extender, Tier = 1, Unlock = 4, EvolveTo = "evo_event_horizon", EvolveUses = 600, EvolveSigil = Sigil.Wide, EvolveEssence = "long_chain" },
            new SkillDef { Id = "constellation", Name = "별자리 잇기", Elem = Element.Void, Cooldown = 7f, Power = 1.0f, Shape = SkillShape.Circle, Radius = 6f, Constellation = 8,
                Role = SkillRole.Finisher, Tier = 1, Unlock = 6, EvolveTo = "evo_great_constellation", EvolveUses = 600, EvolveSigil = Sigil.DeepMark, EvolveEssence = "shatter_chain" },
            new SkillDef { Id = "prism_star", Name = "반사 별탄", Elem = Element.Void, Cooldown = 3f, Power = 1.0f, Shape = SkillShape.Single, Radius = 6f, Ranged = true, WallBounces = 4, Length = 18f,
                Role = SkillRole.Opener, Tier = 1, Unlock = 6, EvolveTo = "evo_prism_nova", EvolveUses = 600, EvolveSigil = Sigil.Split, EvolveEssence = "wide_whirl" },
            new SkillDef { Id = "time_stop", Name = "시간 정지", Elem = Element.Void, Cooldown = 14f, Power = 0f, Shape = SkillShape.Circle, Radius = 5f, Duration = 3f, TimeStopMul = 1.5f,
                Role = SkillRole.Special, Tier = 1, Unlock = 6, EvolveTo = "evo_eternal_stop", EvolveUses = 600, EvolveSigil = Sigil.Feed, EvolveEssence = "long_chain" },
            new SkillDef { Id = "rewind", Name = "되감기", Elem = Element.Void, Cooldown = 10f, Power = 0f, Shape = SkillShape.Buff, RewindSeconds = 3f,
                Role = SkillRole.Special, Tier = 1, Unlock = 8, EvolveTo = "evo_reverse_time", EvolveUses = 600, EvolveSigil = Sigil.Rewind, EvolveEssence = "ember_echo" },
            new SkillDef { Id = "star_veil", Name = "별의 장막", Elem = Element.Frost, Cooldown = 11f, Power = 1.2f, Shape = SkillShape.Buff, Radius = 2.5f, Duration = 5f, ShieldPct = 0.2f,
                Role = SkillRole.Defense, Tier = 1, Unlock = 8, EvolveTo = "evo_galaxy_veil", EvolveUses = 600, EvolveSigil = Sigil.Leech, EvolveEssence = "ash_feast" },
            new SkillDef { Id = "gravity_well", Name = "중력 붕괴", Elem = Element.Void, Cooldown = 8f, Power = 0.8f, Shape = SkillShape.Pull, Radius = 4f, Pull = 1.2f,
                Role = SkillRole.Extender, Tier = 2, Unlock = 12, EvolveTo = "evo_singularity", EvolveUses = 400, EvolveSigil = Sigil.Throng, EvolveEssence = "long_chain" },
            new SkillDef { Id = "comet_trail", Name = "혜성 꼬리", Elem = Element.Ember, Cooldown = 6f, Power = 1.5f, Shape = SkillShape.Line, Radius = 1f, Length = 7f, TrailSeconds = 2f,
                Role = SkillRole.Extender, Tier = 2, Unlock = 16, EvolveTo = "evo_twin_comet", EvolveUses = 400, EvolveSigil = Sigil.Split, EvolveEssence = "wide_whirl" },
        };

        static readonly Dictionary<string, SkillDef> _byId = Build();

        static Dictionary<string, SkillDef> Build()
        {
            var d = new Dictionary<string, SkillDef> { { Basic.Id, Basic } };
            foreach (var s in _ashknight) d[s.Id] = s;
            foreach (var s in _oathknight) d[s.Id] = s;
            foreach (var s in _windhunter) d[s.Id] = s;
            d[BowBasic.Id] = BowBasic;
            foreach (var s in _starseer) d[s.Id] = s;
            foreach (var s in _starLegacy) d[s.Id] = s;
            d[OrbBasic.Id] = OrbBasic;
            void Evo(string from, string id, string name, Action<SkillDef> change)
            {
                var e = d[from].Clone();
                e.Id = id; e.Name = name; e.EvolvedFrom = from; e.EvolveTo = null;
                change(e);
                d[id] = e;
            }
            Evo("ember_slash", "evo_twin_ember", "쌍날 잿불", e => e.Hits = 2);
            Evo("ember_whirl", "evo_flame_vortex", "불꽃 소용돌이", e => { e.Pull = 0.35f; e.Radius = 2.8f; });
            Evo("frost_brand", "evo_glacial_brand", "빙결 낙인", e => e.ShardFrost = true);
            Evo("heat_absorb", "evo_ember_heart", "잿불 심장", e => e.HealBurst = true);
            Evo("ash_leap", "evo_meteor_fall", "유성 낙하", e => e.GroundSeconds = 3f);
            Evo("storm_cleave", "evo_thunder_cleave", "천둥 가르기", e => e.Bolts = 3);
            Evo("forge_strike", "evo_molten_execution", "쇳물 처형", e => e.ExecuteSpread = true);
            Evo("chain_hook", "evo_abyss_chain", "심연 사슬", e => e.LinkShare = true);
            Evo("cinder_echo", "evo_cinder_grave", "잿불 묘지", e => e.Charges = 2);
            Evo("thorn_mail", "evo_lava_mail", "용암 갑주", e => e.ThornRadius = 1.5f);
            Evo("fissure", "evo_triple_fissure", "세 갈래 균열", e => e.Lines = 3);
            Evo("molten_heart", "evo_erupting_heart", "분화하는 심장", e => e.Duration = 6f);
            Evo("frostfire_aura", "evo_eternal_frostfire", "영겁의 서리불꽃", e => e.Radius = 3f);
            Evo("smith_hammer", "evo_heaven_anvil", "천벌의 모루", e => e.AlwaysEcho = true);
            // 서약기사
            Evo("shield_bash", "evo_rampart_bash", "성벽 강타", e => { e.Shape = SkillShape.Cone; e.Radius = 2.4f; e.Arc = 120f; });
            Evo("hallowed_aura", "evo_pilgrim_light", "순례자의 빛", e => { e.Radius = 3f; e.AuraHeal = 0.005f; });
            Evo("rallying_cry", "evo_iron_cry", "철벽 외침", e => e.TauntMark = true);
            Evo("judgment", "evo_double_judgment", "이중 심판", e => e.Hits = 2);
            Evo("oath_barrier", "evo_retort_barrier", "반격의 방벽", e => e.BarrierDouble = true);
            Evo("martyr_chains", "evo_confession_chains", "고해의 사슬", e => e.SharePct = 0.5f);
            Evo("consecrate", "evo_holy_ground", "성역의 대지", e => e.Radius = 3.5f);
            Evo("shield_throw", "evo_returning_shield", "회귀하는 방패", e => e.Bounces = 6);
            Evo("bell_of_judgment", "evo_cathedral_bell", "대성당의 종", e => e.Power = 1.8f);
            Evo("holy_charge", "evo_charge_of_light", "빛의 돌격", e => e.GroundSeconds = 3f);
            Evo("grace_echo", "evo_grace_chorus", "은총의 합창", e => e.Charges = 5);
            Evo("unbroken_vow", "evo_deathless_vow", "불멸의 서약", e => e.Duration = 7f);
            Evo("heaven_spears", "evo_spear_rain", "천벌 폭풍", e => e.Spears = 5);
            Evo("blessed_hammer", "evo_hammer_storm", "망치 폭풍", e => e.Radius = 3.4f);
            Evo("zeal", "evo_fanaticism", "광신", e => e.Hits = 6);
            Evo("warding_sigil", "evo_sealing_sigil", "봉인의 각인", e => e.StunSeconds = 0.3f);
            foreach (var s in _ashknight)
                if (s.EvolveTo != null) s.EvolveSigilName = Sigils.Name(s.EvolveSigil);
            // 바람사냥꾼
            Evo("chain_arrow", "evo_storm_arrow", "폭풍 갈래 화살", e => e.ForkDepth = 2);
            Evo("venom_arrow", "evo_plague_arrow", "역병 화살", e => e.BurstRadius = 1.5f);
            Evo("whirling_blades", "evo_blade_storm", "칼날 폭풍", e => { e.Radius = 3f; e.Pull = 0.3f; });
            Evo("roll_shot", "evo_triple_shot", "삼연 사격", e => e.Hits = 3);
            Evo("shock_trap", "evo_chain_trap", "연쇄 덫", e => e.Duration = 6f);
            Evo("wind_path", "evo_storm_path", "폭풍길", e => e.Lines = 3);
            Evo("storm_eye", "evo_typhoon_eye", "태풍의 눈", e => e.Duration = 8f);
            Evo("rot_cloud", "evo_plague_fog", "역병 안개", e => e.Radius = 3.5f);
            Evo("arrow_rain", "evo_steel_rain", "강철 비", e => e.Spears = 10);
            Evo("hunters_mark", "evo_prey_brand", "사냥감 낙인", e => e.HuntedPct = 0.5f);
            Evo("ricochet_arrow", "evo_ricochet_storm", "도탄 폭풍", e => { e.WallBounces = 6; e.Length = 24f; });
            Evo("shadow_decoy", "evo_shadow_legion", "그림자 군단", e => e.GroundPull = 0.6f);
            Evo("void_lance", "evo_abyss_lance", "심연 창", e => e.Power = 3f);
            Evo("gale_volley", "evo_storm_volley", "폭풍 속사", e => e.Volley = 11);
            Evo("viper_nest", "evo_viper_den", "독사 굴", e => e.Charges = 6);
            foreach (var s in _oathknight)
                if (s.EvolveTo != null) s.EvolveSigilName = Sigils.Name(s.EvolveSigil);
            // 별점술사
            Evo("fireball", "evo_great_fireball", "대화염구", e => e.BurstRadius = 2f);
            Evo("combustion", "evo_inferno", "업화", e => e.Combust = 4f);
            Evo("ice_bolt", "evo_ice_lance", "얼음 창", e => e.Hits = 2);
            Evo("ice_shatter", "evo_glacier_break", "빙하 붕괴", e => e.Radius = 3f);
            Evo("blizzard", "evo_whiteout", "백색 폭풍", e => e.Radius = 3.6f);
            Evo("chain_lightning", "evo_storm_chain", "폭풍 사슬", e => e.Bounces = 7);
            Evo("thunder_nova", "evo_thunder_crown", "천둥 왕관", e => e.Radius = 3.6f);
            Evo("lightning_storm", "evo_tempest", "대폭풍", e => e.Spears = 8);
            Evo("ball_lightning", "evo_twin_ball", "쌍구슬 번개", e => e.Lines = 2);
            Evo("star_bolt", "evo_twin_star", "쌍성 화살", e => e.Hits = 2);
            Evo("meteor", "evo_meteor_shower", "운석우", e => e.Spears = 3);
            Evo("frost_nova", "evo_glacial_nebula", "빙결 성운", e => e.Radius = 3.5f);
            Evo("star_blessing", "evo_galaxy_blessing", "은하의 축복", e => e.Duration = 8f);
            Evo("black_hole", "evo_event_horizon", "사건의 지평선", e => e.Radius = 3.5f);
            Evo("constellation", "evo_great_constellation", "대성좌", e => e.Constellation = 12);
            Evo("time_stop", "evo_eternal_stop", "영겁의 정지", e => e.TimeStopMul = 2f);
            Evo("prism_star", "evo_prism_nova", "프리즘 신성", e => { e.WallBounces = 7; e.Length = 26f; });
            Evo("rewind", "evo_reverse_time", "역행", e => e.RewindSeconds = 5f);
            Evo("nebula_burst", "evo_great_nebula", "성운 대폭발", e => e.Radius = 3.5f);
            Evo("star_veil", "evo_galaxy_veil", "은하 장막", e => e.ShieldPct = 0.3f);
            Evo("gravity_well", "evo_singularity", "특이점", e => e.Radius = 5f);
            Evo("supernova", "evo_hypernova", "극초신성", e => e.Radius = 4.5f);
            Evo("comet_trail", "evo_twin_comet", "쌍혜성", e => e.Lines = 2);
            Evo("starfall_field", "evo_radiant_field", "성광 지대", e => e.Radius = 4f);
            foreach (var s in _windhunter)
                if (s.EvolveTo != null) s.EvolveSigilName = Sigils.Name(s.EvolveSigil);
            foreach (var s in _starseer)
                if (s.EvolveTo != null) s.EvolveSigilName = Sigils.Name(s.EvolveSigil);
            foreach (var s in _starLegacy)
                if (s.EvolveTo != null) s.EvolveSigilName = Sigils.Name(s.EvolveSigil);
            return d;
        }

        public static IReadOnlyList<SkillDef> Ashknight => _ashknight;
        public static IReadOnlyList<SkillDef> Oathknight => _oathknight;
        public static IReadOnlyList<SkillDef> Windhunter => _windhunter;
        public static IReadOnlyList<SkillDef> Starseer => _starseer;
        public static SkillDef BasicFor(string classId) => classId == "windhunter" ? BowBasic : classId == "starseer" ? OrbBasic : Basic;

        /// <summary>Classes that have a skill set (GDD §7.1; windhunter and starseer come next).</summary>
        public static readonly string[] PlayableClasses = { "ashknight", "oathknight", "windhunter", "starseer" };

        public static string ClassName(string classId) => classId == "oathknight" ? "서약기사" : classId == "windhunter" ? "바람사냥꾼" : classId == "starseer" ? "별점술사" : "잿불기사";
        public static List<SkillDef> For(string classId) =>
            classId == "oathknight" ? _oathknight : classId == "windhunter" ? _windhunter : classId == "starseer" ? _starseer : _ashknight;
        public static RingPreset[] PresetsFor(string classId) =>
            classId == "oathknight" ? OathPresets : classId == "windhunter" ? WindPresets : classId == "starseer" ? StarPresets : Presets;
        public static SkillDef Get(string id) => id != null && _byId.TryGetValue(id, out var s) ? s : null;

        /// <summary>Recommended rings (§8). The player can switch between them; the ring fills in as skills unlock.</summary>
        public static readonly RingPreset[] Presets =
        {
            new RingPreset { Name = "휠윈드", TalentFocus = 0, Ring = new[] { "ember_whirl", "ash_leap", "ember_slash", "heat_absorb" },
                Sigils = new[] { Sigil.Wide, Sigil.Conductor, Sigil.Echo, Sigil.Leech }, Ultimate = "undying_flame",
                Priority = new[] { "ember_whirl", "ember_slash", "ash_leap", "heat_absorb", "frost_brand", "storm_cleave" } },
            new RingPreset { Name = "파쇄 순환", TalentFocus = 0, Ring = new[] { "ember_slash", "frost_brand", "storm_cleave", "ember_whirl" },
                Sigils = new[] { Sigil.DeepMark, Sigil.Feed, Sigil.Chain, Sigil.Wide }, Ultimate = "forge_judgment",
                Priority = new[] { "ember_slash", "ember_whirl", "frost_brand", "storm_cleave", "ash_leap", "heat_absorb" } },
            new RingPreset { Name = "붕괴 몰이", Ring = new[] { "chain_hook", "ember_slash", "ember_whirl", "cinder_echo" },
                Sigils = new[] { Sigil.Throng, Sigil.DeepMark, Sigil.Ripple, Sigil.Conductor }, Ultimate = "undying_flame",
                Priority = new[] { "ember_slash", "ember_whirl", "chain_hook", "cinder_echo", "ash_leap", "frost_brand" } },
            new RingPreset { Name = "전파 폭주", Ring = new[] { "molten_heart", "ember_whirl", "fissure", "forge_strike" },
                Sigils = new[] { Sigil.Conductor, Sigil.Ripple, Sigil.Split, Sigil.Execute }, Ultimate = "undying_flame",
                Priority = new[] { "ember_whirl", "ember_slash", "forge_strike", "molten_heart", "fissure", "ash_leap", "frost_brand" } },
            new RingPreset { Name = "서리불꽃 요새", TalentFocus = 1, Ring = new[] { "frostfire_aura", "ember_slash", "smith_hammer", "thorn_mail" },
                Sigils = new[] { Sigil.Wide, Sigil.DeepMark, Sigil.Echo, Sigil.EmberGround }, Ultimate = "forge_judgment",
                Priority = new[] { "ember_slash", "ember_whirl", "frost_brand", "thorn_mail", "heat_absorb", "frostfire_aura", "smith_hammer" } },
        };

        public static readonly RingPreset[] OathPresets =
        {
            new RingPreset { Name = "축복의 망치", TalentFocus = 0, Ring = new[] { "blessed_hammer", "hallowed_aura", "heaven_spears", "judgment" },
                Sigils = new[] { Sigil.Wide, Sigil.Feed, Sigil.Split, Sigil.Echo }, Ultimate = "grand_judgment",
                Priority = new[] { "blessed_hammer", "zeal", "hallowed_aura", "judgment", "heaven_spears", "consecrate" } },
            new RingPreset { Name = "열성", TalentFocus = 0, Ring = new[] { "zeal", "hallowed_aura", "holy_charge", "judgment" },
                Sigils = new[] { Sigil.Haste, Sigil.Leech, Sigil.Conductor, Sigil.Echo }, Ultimate = "grand_judgment",
                Priority = new[] { "zeal", "blessed_hammer", "hallowed_aura", "judgment", "holy_charge", "shield_bash" } },
            new RingPreset { Name = "심판의 순환", TalentFocus = 0, Ring = new[] { "shield_bash", "warding_sigil", "judgment", "hallowed_aura" },
                Sigils = new[] { Sigil.DeepMark, Sigil.Feed, Sigil.Echo, Sigil.Wide }, Ultimate = "grand_judgment",
                Priority = new[] { "shield_bash", "hallowed_aura", "rallying_cry", "judgment", "oath_barrier", "consecrate" } },
            new RingPreset { Name = "순교자", Ring = new[] { "rallying_cry", "martyr_chains", "consecrate", "bell_of_judgment" },
                Sigils = new[] { Sigil.Throng, Sigil.Conductor, Sigil.Wide, Sigil.Feed }, Ultimate = "sanctuary",
                Priority = new[] { "shield_bash", "hallowed_aura", "rallying_cry", "judgment", "martyr_chains", "consecrate", "bell_of_judgment" } },
            new RingPreset { Name = "불굴의 성벽", TalentFocus = 1, Ring = new[] { "oath_barrier", "shield_throw", "grace_echo", "unbroken_vow" },
                Sigils = new[] { Sigil.EmberGround, Sigil.Chain, Sigil.Echo, Sigil.Conductor }, Ultimate = "sanctuary",
                Priority = new[] { "shield_bash", "hallowed_aura", "oath_barrier", "judgment", "shield_throw", "holy_charge" } },
        };

        public static readonly RingPreset[] WindPresets =
        {
            new RingPreset { Name = "속사 · 갈래 화살", TalentFocus = 0, Ring = new[] { "chain_arrow", "gale_volley", "arrow_rain", "storm_eye" },
                Sigils = new[] { Sigil.DeepMark, Sigil.Haste, Sigil.Wide, Sigil.Feed }, Ultimate = "thunderstorm",
                Priority = new[] { "chain_arrow", "gale_volley", "whirling_blades", "roll_shot", "arrow_rain", "storm_eye" } },
            new RingPreset { Name = "역병 전도", Ring = new[] { "venom_arrow", "chain_arrow", "rot_cloud", "arrow_rain" },
                Sigils = new[] { Sigil.DeepMark, Sigil.Chain, Sigil.Wide, Sigil.Feed }, Ultimate = "thunderstorm",
                Priority = new[] { "chain_arrow", "whirling_blades", "venom_arrow", "roll_shot", "shock_trap", "rot_cloud" } },
            new RingPreset { Name = "덫 사냥꾼", Ring = new[] { "hunters_mark", "wind_path", "shock_trap", "viper_nest" },
                Sigils = new[] { Sigil.Ripple, Sigil.Throng, Sigil.Conductor, Sigil.Conductor }, Ultimate = "hunt_time",
                Priority = new[] { "chain_arrow", "whirling_blades", "venom_arrow", "shock_trap", "wind_path", "hunters_mark" } },
            new RingPreset { Name = "폭풍의 눈", TalentFocus = 0, Ring = new[] { "storm_eye", "chain_arrow", "gale_volley", "void_lance" },
                Sigils = new[] { Sigil.Haste, Sigil.Chain, Sigil.Wide, Sigil.Focus }, Ultimate = "thunderstorm",
                Priority = new[] { "chain_arrow", "whirling_blades", "venom_arrow", "storm_eye", "roll_shot", "wind_path" } },
            new RingPreset { Name = "도탄 사수", TalentFocus = 0, Ring = new[] { "ricochet_arrow", "chain_arrow", "arrow_rain", "gale_volley" },
                Sigils = new[] { Sigil.Split, Sigil.Chain, Sigil.Wide, Sigil.Wide }, Ultimate = "thunderstorm",
                Priority = new[] { "chain_arrow", "whirling_blades", "venom_arrow", "ricochet_arrow", "roll_shot", "arrow_rain" } },
        };

        public static readonly RingPreset[] StarPresets =
        {
            new RingPreset { Name = "잿불", TalentFocus = 0, Ring = new[] { "fireball", "combustion", "meteor", "nebula_burst" },
                Sigils = new[] { Sigil.Wide, Sigil.Execute, Sigil.Split, Sigil.Wide }, Ultimate = "star_collapse",
                Priority = new[] { "fireball", "combustion", "meteor", "nebula_burst", "supernova" } },
            new RingPreset { Name = "서리", TalentFocus = 0, Ring = new[] { "ice_bolt", "ice_shatter", "frost_nova", "blizzard" },
                Sigils = new[] { Sigil.Echo, Sigil.Execute, Sigil.Wide, Sigil.Wide }, Ultimate = "eternal_moment",
                Priority = new[] { "ice_bolt", "ice_shatter", "frost_nova", "blizzard", "starfall_field" } },
            new RingPreset { Name = "전격", TalentFocus = 0, Ring = new[] { "chain_lightning", "thunder_nova", "lightning_storm", "ball_lightning" },
                Sigils = new[] { Sigil.Chain, Sigil.Wide, Sigil.Split, Sigil.Split }, Ultimate = "star_collapse",
                Priority = new[] { "chain_lightning", "thunder_nova", "lightning_storm", "ball_lightning" } },
            new RingPreset { Name = "붕괴 낙하", Ring = new[] { "black_hole", "meteor", "starfall_field", "nebula_burst" },
                Sigils = new[] { Sigil.Wide, Sigil.DeepMark, Sigil.Feed, Sigil.Wide }, Ultimate = "star_collapse",
                Priority = new[] { "star_bolt", "meteor", "frost_nova", "black_hole", "star_blessing", "nebula_burst" } },
            new RingPreset { Name = "별자리", Ring = new[] { "star_bolt", "frost_nova", "constellation", "time_stop" },
                Sigils = new[] { Sigil.DeepMark, Sigil.Wide, Sigil.Feed, Sigil.Conductor }, Ultimate = "star_collapse",
                Priority = new[] { "star_bolt", "meteor", "frost_nova", "constellation", "time_stop", "black_hole" } },
            new RingPreset { Name = "되감기 마법사", TalentFocus = 0, Ring = new[] { "star_blessing", "meteor", "supernova", "rewind" },
                Sigils = new[] { Sigil.Haste, Sigil.Split, Sigil.Wide, Sigil.Rewind }, Ultimate = "eternal_moment",
                Priority = new[] { "star_bolt", "meteor", "frost_nova", "star_blessing", "rewind", "nebula_burst" } },
            new RingPreset { Name = "반사 성탄", TalentFocus = 0, Ring = new[] { "prism_star", "meteor", "supernova", "nebula_burst" },
                Sigils = new[] { Sigil.Split, Sigil.Wide, Sigil.Wide, Sigil.DeepMark }, Ultimate = "star_collapse",
                Priority = new[] { "star_bolt", "meteor", "frost_nova", "prism_star", "black_hole", "nebula_burst" } },
        };

        /// <summary>
        /// 숙련 레벨 (희승: 스킬을 몇 번 썼느냐에 따라 레벨업): every use counts; level = 1 + √(uses / 12), up to 60.
        /// Each level adds power and area, and every 10 levels one more copy of the effect (early enough to be felt in the first hours).
        /// </summary>
        public const int MaxSkillLevel = 60;

        public static int SkillLevel(int uses) => Math.Min(MaxSkillLevel, 1 + (int)Math.Sqrt(Math.Max(0, uses) / 12.0));

        /// <summary>Uses needed to reach a level (inverse of <see cref="SkillLevel"/>).</summary>
        public static int UsesFor(int level) => level <= 1 ? 0 : 12 * (level - 1) * (level - 1);

        public static float LevelPower(int level) => 1f + 0.05f * (Math.Max(1, level) - 1);
        public static float LevelArea(int level) => 1f + 0.015f * (Math.Max(1, level) - 1);
        public const int CopyEvery = 10;
        public static int LevelCopies(int level) => Math.Max(0, level) / CopyEvery;

        /// <summary>GDD §7.2: Lv1 one slot, Lv2 two, Lv4 three, Lv8 four; glyphs from Lv12; ultimate at Lv30.</summary>
        public static int SlotsAt(int level) => level >= 8 ? 4 : level >= 4 ? 3 : level >= 2 ? 2 : 1;
        public const int SigilLevel = 12, UltimateLevel = 30;

        /// <summary>Build the ring for a preset at a level: the preset's ring where unlocked, its priority list for the rest.</summary>
        public static SkillRing BuildRing(int presetIndex, int level, ICollection<string> evolved) => BuildRing("ashknight", presetIndex, level, evolved);

        /// <param name="unlocked">기억의 나무: only these skills may go in (null: every skill the level allows).</param>
        /// <param name="ultimate">With <paramref name="unlocked"/>: the ultimate taken in the tree (null: none).</param>
        public static SkillRing BuildRing(string classId, int presetIndex, int level, ICollection<string> evolved,
            ICollection<string> unlocked = null, string ultimate = null)
        {
            var presets = PresetsFor(classId);
            var p = presets[Math.Max(0, Math.Min(presets.Length - 1, presetIndex))];
            int n = SlotsAt(level);
            var chosen = new List<string>();
            bool Open(string id) => Get(id).Unlock <= level && (unlocked == null || unlocked.Contains(id));
            foreach (var id in p.Ring) if (chosen.Count < n && Open(id)) chosen.Add(id);
            foreach (var id in p.Priority) if (chosen.Count < n && !chosen.Contains(id) && Open(id)) chosen.Add(id);
            // With the tree, any other skill taken there fills a slot the preset could not.
            if (unlocked != null) foreach (var s in For(classId)) if (chosen.Count < n && !chosen.Contains(s.Id) && s.Role != SkillRole.Ultimate && Open(s.Id)) chosen.Add(s.Id);
            // Keep the preset's clockwise order (it is the combo), fillers after.
            chosen.Sort((a, b) => Order(p, a).CompareTo(Order(p, b)));

            var slots = new SkillDef[4];
            var ring = new SkillRing(BasicFor(classId), slots);
            for (int i = 0; i < chosen.Count; i++)
            {
                var def = Get(chosen[i]);
                if (def.EvolveTo != null && evolved != null && evolved.Contains(def.Id)) def = Get(def.EvolveTo);
                slots[i] = def;
                int presetSlot = Array.IndexOf(p.Ring, chosen[i]);
                ring.Sigils[i] = level >= SigilLevel && presetSlot >= 0 ? p.Sigils[presetSlot] : Sigil.None;
            }
            if (unlocked != null) { if (ultimate != null && level >= UltimateLevel) ring.Ultimate = Get(ultimate); }
            else if (level >= UltimateLevel) ring.Ultimate = Get(p.Ultimate);
            return ring;
        }

        /// <summary>Build a player-arranged ring: locked skills and slots beyond the level stay empty, evolutions apply.</summary>
        public static SkillRing BuildCustomRing(string classId, string[] skills, Sigil[] sigils, int level, ICollection<string> evolved, int presetForUltimate,
            ICollection<string> unlocked = null, string ultimate = null)
        {
            var slots = new SkillDef[4];
            var ring = new SkillRing(BasicFor(classId), slots);
            int n = SlotsAt(level);
            for (int i = 0; i < 4 && i < n; i++)
            {
                var def = Get(skills[i]);
                if (def == null || def.Unlock > level || def.Role == SkillRole.Ultimate || For(classId).IndexOf(def) < 0 && def.EvolvedFrom == null) continue;
                if (unlocked != null && !unlocked.Contains(def.BaseId)) continue;
                if (def.EvolveTo != null && evolved != null && evolved.Contains(def.Id)) def = Get(def.EvolveTo);
                slots[i] = def;
                ring.Sigils[i] = level >= SigilLevel ? sigils[i] : Sigil.None;
            }
            var presets = PresetsFor(classId);
            if (unlocked != null) { if (ultimate != null && level >= UltimateLevel) ring.Ultimate = Get(ultimate); }
            else if (level >= UltimateLevel) ring.Ultimate = Get(presets[Math.Max(0, Math.Min(presets.Length - 1, presetForUltimate))].Ultimate);
            return ring;
        }

        /// <summary>Skills of a class the hero can put in the ring now (no ultimates); with the tree, only those taken there.</summary>
        public static List<SkillDef> Available(string classId, int level, ICollection<string> unlocked = null)
        {
            var list = new List<SkillDef>();
            foreach (var s in For(classId))
                if (s.Unlock <= level && s.Role != SkillRole.Ultimate && (unlocked == null || unlocked.Contains(s.Id))) list.Add(s);
            return list;
        }

        static int Order(RingPreset p, string id)
        {
            int i = Array.IndexOf(p.Ring, id);
            return i >= 0 ? i : 10 + Array.IndexOf(p.Priority, id);
        }
    }
}
