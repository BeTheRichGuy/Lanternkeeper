using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    public sealed class Stats
    {
        public float MaxHp, Hp, Damage, AttackSpeed, MoveSpeed;
    }

    public abstract class Unit
    {
        public int Id;
        public Vec2 Pos, PrevPos;
        public Stats Stats = new Stats();
        public bool Alive = true;
        public double AttackReadyAt;
        /// <summary>Last movement direction, for 8-way sprite facing.</summary>
        public Vec2 Facing = new Vec2(1f, 0f);
    }

    public sealed class Hero : Unit
    {
        public SkillRing Ring;
        public int Level = 1;
        public int Xp;
        public int XpToNext => XpCurve.ToNext(Level);
        /// <summary>GDD §11.5 잔불 레벨 (0..40), XP piled up past Lv60, rebirths, deepest act beaten to its boss.</summary>
        public int EmberLevel, Rebirths, DeepestActCleared = -1;
        public long OverflowXp;
        /// <summary>GDD §11.5 심지 레벨: endless, from XP once 통합 100 is reached; every level a little of everything.</summary>
        public int WickLevel;
        public long WickXp;
        /// <summary>통합 레벨 = current + ember (≤ 100): what stats, skills, talents and gear requirements read.</summary>
        public int IntegratedLevel => Math.Min(100, Level + EmberLevel);
        public string ClassId = "ashknight";
        public readonly Equipment Gear = new Equipment();
        public readonly Derived Derived = new Derived();
        public bool Has(string essence) => Derived.Essences.Contains(essence);

        // ---------- skill system (docs/specs/2026-11-19-skill-rune-synergy-design.md) ----------
        /// <summary>Recommended ring in use (SkillBook.Presets index).</summary>
        public int Preset;
        /// <summary>잿불기사 열기 0..100: full → the next ring skill is cast as its 작열 version.</summary>
        public float Heat;
        /// <summary>Resonance gauge; full → 대공명.</summary>
        public float Gauge;
        public Element LastResonanceMark = Element.Ember;
        /// <summary>Lifetime casts per base skill id (evolution condition ①).</summary>
        public readonly Dictionary<string, int> SkillUses = new Dictionary<string, int>();
        /// <summary>Base ids of skills that have evolved (kept through rebirth).</summary>
        public readonly HashSet<string> Evolved = new HashSet<string>();
        /// <summary>Lifetime forward resonances by <see cref="Resonance"/> value (mastery).</summary>
        public readonly int[] ResonanceCounts = new int[8];
        public double ThornUntil, MoltenUntil, UndyingUntil, AuraUntil, WhirlUntil;
        public float ThornRadius, AuraRadius, AuraPower;
        public int CinderCharges;
        public SkillDef CinderSkill;
        public SkillMods CinderMods;

        // 서약기사 states (spec §9).
        public double GuardUntil, ShieldUntil, MartyrUntil, VowUntil, SanctuaryUntil, NextSanctuaryPulse;
        public float Shield, ShieldPower, ShieldRadius, MartyrPct, VowStored, VowRadius, SanctuaryPower, AuraHeal, GracePower, GraceRadius;
        public bool ShieldDouble, AuraPulse, VowActive;
        public Element AuraElem;
        public int MartyrLink, GraceCharges;

        // 바람사냥꾼 states (spec §9-1).
        public double StormEyeUntil, ThunderUntil, HuntTimeUntil;
        public float ThunderPower;
        public int ViperCharges;
        public float ViperPower, ViperRadius;

        // 별점술사 states (spec §9-2).
        public double BlessUntil, TimeStopUntil, EternalUntil;

        // GDD §7.2 talents: points per branch (offense, defense, synergy), derived from level and focus.
        public readonly int[] TalentPoints = new int[3];
        /// <summary>기억의 나무: taken nodes (and the order they were taken in, for trimming), 자동 분배, and evolutions earned
        /// before the tree existed (kept whatever the tree holds).</summary>
        public readonly HashSet<string> TreeNodes = new HashSet<string>();
        public readonly List<string> TreeOrder = new List<string>();
        public bool TreeAuto = true;
        public readonly HashSet<string> LegacyEvolved = new HashSet<string>();
        /// <summary>-1 follows the ring preset's branch; 0..2 the player's pick.</summary>
        public int TalentFocus = -1;

        // ---------- 환생 증강 카드 (Augments) ----------
        /// <summary>Cards chosen in this life (up to <see cref="Augments.MaxSlots"/>).</summary>
        public readonly List<string> Augments = new List<string>();
        /// <summary>Three cards waiting to be chosen, or null.</summary>
        public string[] AugOffer;
        /// <summary>This slot's one re-deal is spent.</summary>
        public bool AugRerolled;
        public double AugOfferAt;
        /// <summary>Deals made in this life (the deal's random salt).</summary>
        public int AugDeals;
        /// <summary>Caches from Augments.Apply: element bonus by element, skill power and crit-on-use by skill, procs.</summary>
        public readonly float[] AugElem = new float[6];
        public readonly Dictionary<string, float> AugPower = new Dictionary<string, float>();
        public readonly Dictionary<string, float> AugCrit = new Dictionary<string, float>();
        public readonly List<AugmentDef> AugProcs = new List<AugmentDef>();
        public double AugCritUntil;
        public float AugCritBonus;
        /// <summary>
        /// 갈래별 스킬 세트 (희승 11-22: "각 테크에 따라, 특성 스킬 셋을 저장해서, 카드 구성에 따라 전환"): four skills per
        /// branch, "" where none was saved.
        /// </summary>
        public readonly string[] BranchSets = { "", "", "", "", "", "", "", "", "", "", "", "" };
        /// <summary>Choosing a card of another branch switches the skill set to that branch (자동 분배 only).</summary>
        public bool AugSwitchSets = true;
        public float GaugeRate = 1f;
        public double CheatDeathReady;
        /// <summary>서리 폭발 (elite death): the hero stands frozen until then.</summary>
        public double StunnedUntil;
        /// <summary>도감: runewords ever formed (GDD §12.5).</summary>
        public readonly HashSet<string> RunewordsFound = new HashSet<string>();
        /// <summary>GDD §5.1 배치 퍼즐: a ring the player arranged (base skill ids and glyphs per slot).</summary>
        public bool UseCustomRing;
        public readonly string[] CustomSkills = new string[4];
        public readonly Sigil[] CustomSigils = new Sigil[4];
        public float TimeStopMul;
        public string ShieldFx = "oath_barrier_break";
        public Element ShieldElem = Element.Shock;
        /// <summary>GDD §12.5 적 도감: kills per enemy id (medals at 100 / 1,000 / 10,000).</summary>
        public readonly Dictionary<string, int> KillsById = new Dictionary<string, int>();
        /// <summary>GDD §12.5 도전 과제: ids earned, running counts they read, and the chosen lantern colour.</summary>
        public readonly HashSet<string> Achieved = new HashSet<string>();
        public int GrandCount, MaxChainDepth, IgniteCount, LanternColor;
        /// <summary>GDD §26.6 이야기 우편함: records opened and records read.</summary>
        public readonly HashSet<string> Letters = new HashSet<string>(), LettersRead = new HashSet<string>();
        /// <summary>GDD §5.3 등불 점화 (not saved: a 30 s buff, a 3 min recharge).</summary>
        public double IgniteUntil, IgniteReadyAt;
    }

    /// <summary>
    /// GDD §11.2: minutes per level = 4.51 × L^0.776, kills per minute = 18 + 0.25L, monster XP = 5 × 1.075^(L−1).
    /// Their product is the XP to the next level (L1 = 410, L10 = 5,520, L59 = 1,160,940 → Lv60 in ≈60 h).
    /// </summary>
    public static class XpCurve
    {
        public static int ToNext(int level)
        {
            double l = Math.Max(1, level);
            return (int)Math.Round(4.51 * Math.Pow(l, 0.776) * (18 + 0.25 * l) * MonsterXp(level));
        }

        /// <summary>XP of a standard monster at this area level; EnemyDef.Xp scales it relative to 5.</summary>
        public static double MonsterXp(int areaLevel) => 5.0 * Math.Pow(1.075, Math.Max(0, areaLevel - 1));
    }

    public sealed class EnemyDef
    {
        public string Id;
        /// <summary>Korean display name (boss bar, champion labels).</summary>
        public string Name = "";
        public float MaxHp, Damage, MoveSpeed = 1.5f, AttackSpeed = 0.6f;
        public int Xp = 5;
        /// <summary>&gt; 0: ranged attacker that stops at this distance and shoots (cells).</summary>
        public float Range;
        /// <summary>Damage it shrugs off (뼈 방패병), 0..1.</summary>
        public float Armor;
        /// <summary>Act boss techniques (SimWorld.Bosses): slam, tide, spores, pour, stars; and their display names.</summary>
        public string Kit = "", SlamName = "", SpecialName = "";
        /// <summary>GDD §26.6: who it was before the fog — one line in the enemy codex.</summary>
        public string Lore = "";
    }

    /// <summary>GDD §5.4 elite prefixes (1–3 per elite, more in deeper zones).</summary>
    [Flags]
    public enum EliteAffix { None = 0, Multiplying = 1, EmberAura = 2, Shielded = 4, Hasted = 8, Vampiric = 16, FrostBurst = 32 }

    public static class EliteAffixes
    {
        public static readonly EliteAffix[] All = { EliteAffix.Multiplying, EliteAffix.EmberAura, EliteAffix.Shielded, EliteAffix.Hasted, EliteAffix.Vampiric, EliteAffix.FrostBurst };

        public static string Name(EliteAffix a)
        {
            switch (a)
            {
                case EliteAffix.Multiplying: return "증식하는";
                case EliteAffix.EmberAura: return "잿불 오라";
                case EliteAffix.Shielded: return "보호막";
                case EliteAffix.Hasted: return "날랜";
                case EliteAffix.Vampiric: return "흡혈하는";
                case EliteAffix.FrostBurst: return "서리 폭발";
                default: return "";
            }
        }

        public static string Names(EliteAffix set)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var a in All) if ((set & a) != 0) parts.Add(Name(a));
            return string.Join(" · ", parts);
        }
    }

    public sealed class Enemy : Unit
    {
        public EnemyDef Def;
        public int HomeRoom = -1;
        public bool Aggro, Boss, Elite;
        /// <summary>Where it stood when first seen; an unaggroed enemy wanders a little around it.</summary>
        public Vec2 Home, WanderTo;
        public bool HasHome;
        public double WanderAt;
        /// <summary>Mage elements: 화상 per tick and ticks left, frost hits toward a freeze, frozen until, next 감전.</summary>
        public float BurnPerTick;
        public int BurnTicks, Chill;
        public double FreezeUntil, ShockReadyAt;
        /// <summary>Raised by a boss technique: no XP, drops, runes or codex count (no farming the summons).</summary>
        public bool Summoned;
        public Element Mark;
        /// <summary>The element of the last blow (the view shows a death by it: burnt, shattered, …).</summary>
        public Element LastHitElem;
        public int Stacks;
        public double StunUntil;
        /// <summary>Ring slot that left the current mark (order bonus: the next slot detonating it).</summary>
        public int MarkSlot = -1;
        /// <summary>Damage over time (plague, ember ground) in damage per tick.</summary>
        public float DotPerTick;
        public int DotTicks;
        /// <summary>심연 사슬: enemies sharing a link id split 30% of a hit between them.</summary>
        public int Link;
        /// <summary>떠도는 챔피언 (event room): three drops of at least magic quality.</summary>
        public bool Champion;
        public EliteAffix Affixes;
        /// <summary>보호막 affix: soaks this much before life.</summary>
        public float AffixShield;
        /// <summary>A 증식하는 split: never splits again.</summary>
        public bool Copy;
        /// <summary>사냥꾼의 표식: takes more damage, and its death chain reaches twice as far.</summary>
        public double HuntedUntil;
        public float HuntedPct;
        /// <summary>시간 정지: hits taken while frozen pile up here and land at once (×mul) when time resumes.</summary>
        public double FrozenUntil;
        public float Stored, StoredMul;
        /// <summary>Act boss technique timers (SimWorld.Bosses).</summary>
        public double NextSlam, NextSpecial;
    }

    /// <summary>Zone enemy ranks and per-floor mix (GDD §6.1, Nomad Idle rotation idea).</summary>
    public static class EnemyRoster
    {
        /// <summary>Act I crypt ranks, weakest first (matches art/pixellab ASSETS.md "1막 몬스터").</summary>
        public static readonly EnemyDef[] Crypt =
        {
            new EnemyDef { Id = "villager", Name = "잿빛 마을 시체", Lore = "안개가 처음 닿은 밤, 문을 잠그지 않은 집의 주민들.", MaxHp = 22, Damage = 3, MoveSpeed = 1.5f, Xp = 4 },
            new EnemyDef { Id = "skeleton", Name = "해골 병사", Lore = "마을 경비대. 지킬 것이 사라진 뒤에도 교대 시간을 기다린다.",     MaxHp = 30, Damage = 4, MoveSpeed = 1.5f, Xp = 5 },
            new EnemyDef { Id = "archer",   Name = "해골 궁수", Lore = "망루의 사냥꾼. 마지막 화살은 안개를 향해 쏘았다.",     MaxHp = 20, Damage = 4, MoveSpeed = 1.3f, Xp = 5, Range = 4.5f },
            new EnemyDef { Id = "ghoul",    Name = "묘실 구울", Lore = "굶주린 겨울, 묘실의 제물을 먼저 훔친 자들.",     MaxHp = 36, Damage = 5, MoveSpeed = 2.0f, Xp = 6 },
            new EnemyDef { Id = "wraith",   Name = "안개 망령", Lore = "자기 이름을 잊은 자. 안개는 얼굴부터 지운다.",     MaxHp = 44, Damage = 6, MoveSpeed = 1.8f, Xp = 8 },
            // 2026-11-19 variety pass (희승: "몬스터도 엄청 많이"): fast biters, a caster, a shield wall, a swarm.
            new EnemyDef { Id = "grave_hound",       Name = "묘지 사냥개", Lore = "묘지기의 개. 주인이 군주가 된 뒤에도 곁을 떠나지 않았다.",  MaxHp = 16, Damage = 3, MoveSpeed = 2.7f, AttackSpeed = 1.0f, Xp = 4 },
            new EnemyDef { Id = "candle_priest",     Name = "촛불 사제", Lore = "죽은 이를 위해 초를 밝히던 사제. 이제는 초가 사제를 밝힌다.",    MaxHp = 26, Damage = 5, MoveSpeed = 1.1f, Xp = 7, Range = 5f },
            new EnemyDef { Id = "bone_shieldbearer", Name = "뼈 방패병", Lore = "방패벽의 맨 앞줄. 물러서라는 명령은 끝내 오지 않았다.",    MaxHp = 50, Damage = 5, MoveSpeed = 0.95f, Xp = 9, Armor = 0.35f },
            new EnemyDef { Id = "plague_rat_swarm",  Name = "역병 쥐 떼", Lore = "곡식 창고를 갉던 쥐들. 안개 속에서 하나의 굶주림이 되었다.",   MaxHp = 12, Damage = 2, MoveSpeed = 2.3f, AttackSpeed = 1.2f, Xp = 3 },
        };

        /// <summary>GDD §11.4 elite: two guaranteed drops. 무덤파기 거구. Also the floor boss, scaled up.</summary>
        public static readonly EnemyDef Elite = new EnemyDef { Id = "brute", Name = "무덤파기 거구", Lore = "무덤을 파던 일꾼. 파낸 흙보다 묻은 이웃이 많았다.", MaxHp = 40, Damage = 6, MoveSpeed = 1.1f, Xp = 20 };

        /// <summary>Act I boss 무덤 군주 (hp/damage scaled at spawn).</summary>
        public static readonly EnemyDef BossDef = new EnemyDef { Id = "gravelord", Name = "무덤 군주", Lore = "죽은 자를 지키다 그들의 왕이 된 묘지기.", MaxHp = 70, Damage = 8, MoveSpeed = 1.0f, Xp = 12, Kit = "slam", SlamName = "무덤 내려찍기", SpecialName = "죽은 자여, 일어나라" };

        /// <summary>
        /// Per zone, per floor: which ranks (0-based into <see cref="Crypt"/>) appear. New enemies arrive one at a time
        /// so "what's on the next floor" stays interesting (GDD §6.1, Nomad Idle rotation idea).
        /// </summary>
        static readonly int[][][] ZonePatterns =
        {
            new[] { new[] { 0 }, new[] { 0, 5 }, new[] { 0, 1, 5 }, new[] { 1, 2, 8 } },          // 구역 1: 시체 → 사냥개 → 해골 병사 → 궁수·쥐 떼
            new[] { new[] { 1, 2, 8 }, new[] { 2, 3, 6 }, new[] { 3, 6, 7 }, new[] { 2, 3, 7 } },  // 구역 2: 궁수 → 구울·촛불 사제 → 뼈 방패병
            new[] { new[] { 3, 5, 8 }, new[] { 3, 4, 6 }, new[] { 4, 6, 7 }, new[] { 4, 7, 8 } },  // 구역 3: 구울 → 안개 망령 + 섞기
        };

        public static int[] PatternFor(int zone, int floorInZone) => Acts[0].PatternFor(zone, floorInZone);

        public static EnemyDef Boss => BossDef;

        // ---------- 막 II 가라앉은 수도원 (GDD §6.3, §26.4) ----------

        /// <summary>Act II ranks, weakest first. Floor scaling already makes them Lv 11–20 strong.</summary>
        public static readonly EnemyDef[] Monastery =
        {
            new EnemyDef { Id = "drowned_monk",  Name = "익사한 수도사", Lore = "기도가 모자랐다며 스스로 물에 들어간 수도사.", MaxHp = 30, Damage = 4, MoveSpeed = 1.4f, Xp = 5 },
            new EnemyDef { Id = "mire_eel",      Name = "늪 장어", Lore = "수도원 연못의 장어. 가라앉은 이들의 기도를 먹고 자랐다.",       MaxHp = 14, Damage = 3, MoveSpeed = 2.8f, AttackSpeed = 1.1f, Xp = 4 },
            new EnemyDef { Id = "choir_wraith",  Name = "성가대 망령", Lore = "성가대의 막내. 물속에서도 음을 놓치지 않으려 한다.",   MaxHp = 22, Damage = 4, MoveSpeed = 1.2f, AttackSpeed = 0.4f, Xp = 6, Range = 5f },
            new EnemyDef { Id = "penitent",      Name = "고행자", Lore = "죄를 씻으려 제 등을 친 고행자. 아직 씻기지 않았다고 믿는다.",        MaxHp = 34, Damage = 5, MoveSpeed = 2.2f, AttackSpeed = 0.9f, Xp = 7 },
            new EnemyDef { Id = "tide_acolyte",  Name = "조수의 시종", Lore = "조수를 읽던 시종. 별이 떨어진 밤의 물때만은 맞히지 못했다.",   MaxHp = 26, Damage = 4.5f, MoveSpeed = 1.1f, AttackSpeed = 0.4f, Xp = 7, Range = 5.5f },
            new EnemyDef { Id = "bell_ringer",   Name = "종지기", Lore = "종탑의 종지기. 아무도 오지 않는 예배를 알린다.",        MaxHp = 64, Damage = 6, MoveSpeed = 0.9f, Xp = 10, Armor = 0.3f },
        };

        static readonly int[][][] MonasteryPatterns =
        {
            new[] { new[] { 0 }, new[] { 0, 1 }, new[] { 0, 1, 2 }, new[] { 1, 2, 3 } },            // 잠긴 회랑: 수도사 → 장어 → 성가대
            new[] { new[] { 0, 2, 3 }, new[] { 2, 3, 4 }, new[] { 3, 4, 5 }, new[] { 2, 4, 5 } },   // 성가대의 홀: 고행자·시종 → 종지기
            new[] { new[] { 3, 4, 5 }, new[] { 1, 4, 5 }, new[] { 2, 3, 5 }, new[] { 3, 4, 5 } },   // 참회의 종탑: 전부 섞기
        };

        // ---------- 막 III 균사 왕국 (GDD §6.3, §26.4) ----------

        public static readonly EnemyDef[] Fungal =
        {
            new EnemyDef { Id = "spore_thrall",    Name = "포자 노예",   Lore = "피난 행렬의 맨 끝에 섰던 이. 균사가 손을 잡아 주었다.", MaxHp = 32, Damage = 4, MoveSpeed = 1.4f, Xp = 5 },
            new EnemyDef { Id = "shroom_crawler",  Name = "기어다니는 버섯", Lore = "피난민의 아이가 기르던 버섯. 이제 아이를 찾아 기어 다닌다.", MaxHp = 14, Damage = 3, MoveSpeed = 2.7f, AttackSpeed = 1.1f, Xp = 4 },
            new EnemyDef { Id = "mycelium_weaver", Name = "균사 직조자", Lore = "천을 짜던 직조공. 지금은 서로를 엮는다.", MaxHp = 24, Damage = 4, MoveSpeed = 1.1f, AttackSpeed = 0.4f, Xp = 7, Range = 5f },
            new EnemyDef { Id = "bloated_carrier", Name = "부푼 운반자", Lore = "모두의 짐을 대신 졌던 짐꾼. 균사는 그 짐까지 삼켰다.", MaxHp = 70, Damage = 6, MoveSpeed = 0.85f, Xp = 10, Armor = 0.25f },
            new EnemyDef { Id = "fungal_stalker",  Name = "균사 추적자", Lore = "피난민을 지키던 사냥꾼. 지킬 이들이 하나가 되자 바깥을 겨눈다.", MaxHp = 34, Damage = 6, MoveSpeed = 2.4f, AttackSpeed = 0.9f, Xp = 8 },
            new EnemyDef { Id = "lantern_moth",    Name = "등불 나방",   Lore = "길을 찾던 이들의 마지막 불빛을 아직도 쫓는다.", MaxHp = 12, Damage = 2, MoveSpeed = 3.0f, AttackSpeed = 1.2f, Xp = 3 },
        };

        static readonly int[][][] FungalPatterns =
        {
            new[] { new[] { 0 }, new[] { 0, 1 }, new[] { 0, 1, 5 }, new[] { 1, 2, 5 } },            // 균사의 문턱: 노예 → 기어다니는 것 → 나방·직조자
            new[] { new[] { 0, 2, 3 }, new[] { 2, 3, 4 }, new[] { 3, 4, 5 }, new[] { 2, 4, 5 } },   // 포자 시장: 운반자·추적자
            new[] { new[] { 3, 4, 5 }, new[] { 1, 4, 5 }, new[] { 2, 3, 4 }, new[] { 3, 4, 5 } },   // 고치의 굴: 전부 섞기
        };

        // ---------- 막 IV 녹은 대장간 (GDD §6.3, §26.4) ----------

        public static readonly EnemyDef[] Forge =
        {
            new EnemyDef { Id = "slag_worker",    Name = "광재 일꾼",     Lore = "별의 쇠를 나르던 일꾼. 쇳물이 손에서 팔로, 팔에서 가슴으로 올라왔다.", MaxHp = 34, Damage = 5, MoveSpeed = 1.3f, Xp = 5 },
            new EnemyDef { Id = "ember_imp",      Name = "불씨 견습생",     Lore = "풀무 앞을 지키던 견습공. 불씨만큼 작아졌다.", MaxHp = 14, Damage = 3, MoveSpeed = 2.8f, AttackSpeed = 1.1f, Xp = 4 },
            new EnemyDef { Id = "bellows_priest", Name = "풀무 사제",     Lore = "불에게 기도하던 조합의 사제. 숨을 불어넣는 것이 기도였다.", MaxHp = 24, Damage = 4.5f, MoveSpeed = 1.1f, AttackSpeed = 0.4f, Xp = 7, Range = 5f },
            new EnemyDef { Id = "iron_golem",     Name = "쇠 골렘",       Lore = "조합이 만든 첫 작품. 주인이 없어도 일을 멈추지 않는다.", MaxHp = 80, Damage = 7, MoveSpeed = 0.8f, Xp = 11, Armor = 0.35f },
            new EnemyDef { Id = "forge_hound",    Name = "대장간 사냥개", Lore = "대장간 문을 지키던 개. 쇳물을 핥다가 쇳물이 되었다.", MaxHp = 32, Damage = 6, MoveSpeed = 2.6f, AttackSpeed = 0.9f, Xp = 8 },
            new EnemyDef { Id = "ash_wisp",       Name = "재 도깨비불",   Lore = "녹아 사라진 이들의 마지막 숨. 재로 떠돈다.", MaxHp = 12, Damage = 2.5f, MoveSpeed = 2.4f, AttackSpeed = 1.0f, Xp = 3 },
        };

        static readonly int[][][] ForgePatterns =
        {
            new[] { new[] { 0 }, new[] { 0, 1 }, new[] { 0, 1, 5 }, new[] { 1, 2, 5 } },            // 광재의 갱도
            new[] { new[] { 0, 2, 3 }, new[] { 2, 3, 4 }, new[] { 3, 4, 5 }, new[] { 2, 4, 5 } },   // 풀무의 전당
            new[] { new[] { 3, 4, 5 }, new[] { 1, 4, 5 }, new[] { 2, 3, 4 }, new[] { 3, 4, 5 } },   // 거푸집의 무덤
        };

        // ---------- 막 V 별 없는 심연 (GDD §6.3, §26.4) ----------

        public static readonly EnemyDef[] Abyss =
        {
            new EnemyDef { Id = "fallen_pilgrim", Name = "별을 맞은 순례자", Lore = "별을 보러 심연까지 내려온 순례자. 별을 보았다.", MaxHp = 34, Damage = 5, MoveSpeed = 1.4f, Xp = 5 },
            new EnemyDef { Id = "void_crawler",   Name = "기어다니는 공허", Lore = "꺼진 별 틈에서 기어 나온 것. 무엇이었는지 아무도 모른다.", MaxHp = 15, Damage = 3.5f, MoveSpeed = 2.8f, AttackSpeed = 1.1f, Xp = 4 },
            new EnemyDef { Id = "starless_seer",  Name = "별 없는 점술사", Lore = "별을 읽던 점술사. 이제 읽을 별이 없다.", MaxHp = 24, Damage = 4.5f, MoveSpeed = 1.1f, AttackSpeed = 0.4f, Xp = 7, Range = 5.5f },
            new EnemyDef { Id = "husk_titan",     Name = "빈 껍질 거인", Lore = "별을 맞고도 서 있던 자. 속이 비어도 서 있다.", MaxHp = 85, Damage = 7, MoveSpeed = 0.8f, Xp = 11, Armor = 0.35f },
            new EnemyDef { Id = "abyss_stalker",  Name = "심연 추적자", Lore = "첫 등불지기를 쫓아 내려온 사냥꾼. 아직 쫓고 있다.", MaxHp = 34, Damage = 6, MoveSpeed = 2.6f, AttackSpeed = 0.9f, Xp = 8 },
            new EnemyDef { Id = "star_shard",     Name = "별 파편", Lore = "꺼진 별의 부스러기. 아직 빛이 되고 싶어 떠돈다.", MaxHp = 12, Damage = 2.5f, MoveSpeed = 2.6f, AttackSpeed = 1.0f, Xp = 3 },
        };

        static readonly int[][][] AbyssPatterns =
        {
            new[] { new[] { 0 }, new[] { 0, 1 }, new[] { 0, 1, 5 }, new[] { 1, 2, 5 } },            // 순례자의 끝
            new[] { new[] { 0, 2, 3 }, new[] { 2, 3, 4 }, new[] { 3, 4, 5 }, new[] { 2, 4, 5 } },   // 꺼진 하늘의 평원
            new[] { new[] { 3, 4, 5 }, new[] { 1, 4, 5 }, new[] { 2, 3, 4 }, new[] { 3, 4, 5 } },   // 첫 등불지기의 자리
        };

        /// <summary>막 I 모래 무덤 동굴 (docs/research/act-visual-direction.md): weakest first.</summary>
        public static readonly EnemyDef[] Sand =
        {
            new EnemyDef { Id = "bandaged_wanderer", Name = "붕대 방랑자", Lore = "순례 상인이었다. 붕대 아래 얼굴은 모래가 지웠다.", MaxHp = 22, Damage = 3, MoveSpeed = 1.5f, Xp = 4 },
            new EnemyDef { Id = "scarab_swarm", Name = "갑충 떼", Lore = "낙타 몰이꾼이 기르던 가축의 등에 깃든 벌레들.", MaxHp = 12, Damage = 2, MoveSpeed = 2.7f, Xp = 3, AttackSpeed = 1.1f },
            new EnemyDef { Id = "shroud_wraith", Name = "해진 천 유령", Lore = "장례 행렬의 곡꾼. 아직도 누군가를 위해 운다.", MaxHp = 18, Damage = 3.5f, MoveSpeed = 1.2f, Xp = 5, AttackSpeed = 0.5f, Range = 4.5f },
            new EnemyDef { Id = "sandstone_sentinel", Name = "사암 파수석", Lore = "무덤을 짓던 석공. 자기가 깎은 돌이 되었다.", MaxHp = 45, Damage = 4.5f, MoveSpeed = 0.9f, Xp = 7, Armor = 0.3f },
            new EnemyDef { Id = "sand_wolf", Name = "모래 늑대", Lore = "상단을 지키던 호위견. 주인을 잊고도 모래를 지킨다.", MaxHp = 28, Damage = 4.5f, MoveSpeed = 2.5f, Xp = 5, AttackSpeed = 0.9f },
            new EnemyDef { Id = "sand_lurker", Name = "모래 속 잠복자", Lore = "사막 길잡이. 모래 속에서 길을 기다린다.", MaxHp = 14, Damage = 3, MoveSpeed = 2.3f, Xp = 3 },
        };
        /// <summary>막 II 뿌리 밀림.</summary>
        public static readonly EnemyDef[] Jungle =
        {
            new EnemyDef { Id = "rootwalker", Name = "뿌리 걸음", Lore = "약초꾼. 캐던 뿌리가 발이 되었다.", MaxHp = 32, Damage = 4, MoveSpeed = 1.4f, Xp = 5 },
            new EnemyDef { Id = "vine_hanger", Name = "덩굴 걸이", Lore = "숲지기. 덩굴에 매달려 길을 지켰다.", MaxHp = 14, Damage = 3, MoveSpeed = 2.7f, Xp = 4, AttackSpeed = 1.1f },
            new EnemyDef { Id = "flowerface_witch", Name = "꽃 얼굴 마녀", Lore = "마을 약사. 처방 대신 꽃가루를 뿌린다.", MaxHp = 24, Damage = 4.5f, MoveSpeed = 1.1f, Xp = 7, AttackSpeed = 0.4f, Range = 5f },
            new EnemyDef { Id = "spore_host", Name = "포자낭 숙주", Lore = "나무꾼. 등에 자란 포자낭이 무겁다.", MaxHp = 70, Damage = 6, MoveSpeed = 0.85f, Xp = 10, Armor = 0.25f },
            new EnemyDef { Id = "thorn_boar", Name = "가시 멧돼지", Lore = "숲 마을이 기르던 돼지. 가시가 엄니가 되었다.", MaxHp = 34, Damage = 6, MoveSpeed = 2.4f, Xp = 8, AttackSpeed = 0.9f },
            new EnemyDef { Id = "leafmail_hunter", Name = "잎갑옷 사냥꾼", Lore = "밀렵꾼. 잎을 갑옷 삼아 숨는다.", MaxHp = 12, Damage = 2.5f, MoveSpeed = 2.6f, Xp = 3, AttackSpeed = 1.0f },
        };
        /// <summary>막 III 핏덩이 소굴 (the one act where red belongs; monsters stay pale).</summary>
        public static readonly EnemyDef[] Flesh =
        {
            new EnemyDef { Id = "flesh_crawler", Name = "살덩이 기는 자", Lore = "도살장 일꾼. 다루던 고기와 구분이 사라졌다.", MaxHp = 32, Damage = 4, MoveSpeed = 1.4f, Xp = 5 },
            new EnemyDef { Id = "wailing_brood", Name = "신음하는 응애", Lore = "어린 고아들. 서로를 껴안은 채 굳었다.", MaxHp = 14, Damage = 3, MoveSpeed = 2.7f, Xp = 4, AttackSpeed = 1.1f },
            new EnemyDef { Id = "sinew_summoner", Name = "힘줄 꼭두각시사", Lore = "연극 극단원. 이제 힘줄로 인형을 놀린다.", MaxHp = 24, Damage = 4.5f, MoveSpeed = 1.1f, Xp = 7, AttackSpeed = 0.4f, Range = 5f },
            new EnemyDef { Id = "hook_butcher", Name = "갈고리 도살자", Lore = "푸주한. 갈고리를 내려놓는 법을 잊었다.", MaxHp = 70, Damage = 6, MoveSpeed = 0.85f, Xp = 10, Armor = 0.25f },
            new EnemyDef { Id = "blood_hound", Name = "핏빛 사냥개", Lore = "사냥개. 피 냄새만 기억한다.", MaxHp = 34, Damage = 6, MoveSpeed = 2.4f, Xp = 8, AttackSpeed = 0.9f },
            new EnemyDef { Id = "bone_puppet", Name = "뼈 인형", Lore = "외과의의 조수. 뼈를 맞추다 뼈가 되었다.", MaxHp = 12, Damage = 2.5f, MoveSpeed = 2.6f, Xp = 3, AttackSpeed = 1.0f },
        };
        /// <summary>막 IV 얼음 동굴.</summary>
        public static readonly EnemyDef[] Ice =
        {
            new EnemyDef { Id = "frost_wanderer", Name = "서리 방랑자", Lore = "설산 사냥꾼. 사냥감을 기다리다 얼었다.", MaxHp = 34, Damage = 5, MoveSpeed = 1.3f, Xp = 5 },
            new EnemyDef { Id = "icicle_spider", Name = "고드름 거미", Lore = "광부들이 기르던 짐승. 다리가 고드름이 되었다.", MaxHp = 14, Damage = 3, MoveSpeed = 2.8f, Xp = 4, AttackSpeed = 1.1f },
            new EnemyDef { Id = "frozen_choir", Name = "얼어붙은 합창단", Lore = "성가대원. 마지막 음이 얼어붙었다.", MaxHp = 24, Damage = 4.5f, MoveSpeed = 1.1f, Xp = 7, AttackSpeed = 0.4f, Range = 5f },
            new EnemyDef { Id = "icemail_soldier", Name = "얼음갑옷 병사", Lore = "국경 수비대. 얼음이 갑옷이 되었다.", MaxHp = 80, Damage = 7, MoveSpeed = 0.8f, Xp = 11, Armor = 0.35f },
            new EnemyDef { Id = "frost_bear", Name = "서리 곰", Lore = "설원 사냥의 동반자. 주인 대신 설원을 지킨다.", MaxHp = 34, Damage = 6, MoveSpeed = 2.5f, Xp = 8, AttackSpeed = 0.9f },
            new EnemyDef { Id = "blizzard_wraith", Name = "눈보라 유령", Lore = "조난당한 여행자. 길을 묻는 바람이 되었다.", MaxHp = 12, Damage = 2.5f, MoveSpeed = 2.6f, Xp = 3, AttackSpeed = 1.0f },
        };
        static readonly int[][][] StdPatterns =
        {
            new[] { new[] { 0 }, new[] { 0, 1 }, new[] { 0, 1, 5 }, new[] { 1, 2, 5 } },
            new[] { new[] { 0, 2, 3 }, new[] { 2, 3, 4 }, new[] { 3, 4, 5 }, new[] { 2, 4, 5 } },
            new[] { new[] { 3, 4, 5 }, new[] { 1, 4, 5 }, new[] { 2, 3, 4 }, new[] { 3, 4, 5 } },
        };

        public static readonly ActDef[] Acts =
        {
            new ActDef
            {
                Numeral = "I", Name = "모래 무덤 동굴", Ranks = Sand, Patterns = StdPatterns,
                Elite = new EnemyDef { Id = "bronze_veiled_general", Name = "청동 면사 장군", Lore = "무덤을 지키던 근위대장. 지켜야 할 왕이 모래에 묻혔다.", MaxHp = 40, Damage = 6, MoveSpeed = 1.1f, Xp = 20, Armor = 0.2f },
                Boss = new EnemyDef { Id = "sarcophagus_king", Name = "석관 군주", Lore = "오래전 이 땅의 왕. 별이 지던 밤, 홀로 남았다.", MaxHp = 70, Damage = 8, MoveSpeed = 1.0f, Xp = 12, Kit = "slam", SlamName = "모래 내려찍기", SpecialName = "모래에서 일어서라" },
                ZoneLines = new[]
                {
                    "모래 무덤의 입구\n햇빛이 닿는 마지막 곳. 모래가 이름을 덮는다",
                    "붕대의 회랑\n순례자들은 얼굴을 감고, 끝내 얼굴을 잊었다",
                    "석공의 묘실\n돌을 깎던 이들이 돌이 되어 문을 지킨다",
                    "석관 군주의 묘\n모래에 묻힌 왕이 아직 왕좌에 앉아 있다",
                },
            },
            new ActDef
            {
                Numeral = "II", Name = "뿌리 밀림", Ranks = Jungle, Patterns = StdPatterns,
                Elite = new EnemyDef { Id = "antler_knight", Name = "가지뿔 순록 기사", Lore = "숲 순찰대장. 뿔이 가지가 되어 하늘을 찾는다.", MaxHp = 44, Damage = 6, MoveSpeed = 1.1f, Xp = 20, Armor = 0.2f },
                Boss = new EnemyDef { Id = "crowned_great_tree", Name = "뿌리 왕관 거목", Lore = "마을 신목에 기도하던 장로. 기도가 뿌리가 되었다.", MaxHp = 75, Damage = 9, MoveSpeed = 0.9f, Xp = 13, Kit = "spores", SlamName = "포자 폭풍", SpecialName = "뿌리의 아이들" },
                ZoneLines = new[]
                {
                    "뿌리의 문턱\n땅 위의 숲이 그리워 아래로 뻗은 뿌리들",
                    "반딧불 늪\n빛은 남았는데, 빛을 보던 눈이 없다",
                    "숲 마을의 잔해\n사람들은 나무가 되었고, 나무는 사람을 기억한다",
                    "신목의 심장\n장로는 아직 기도하고 있다",
                },
            },
            new ActDef
            {
                Numeral = "III", Name = "핏덩이 소굴", Ranks = Flesh, Patterns = StdPatterns,
                Elite = new EnemyDef { Id = "fleshmail_warden", Name = "살가죽 갑옷 간수", Lore = "지하 감옥 간수장. 열쇠 대신 갈고리를 쥐었다.", MaxHp = 46, Damage = 6.5f, MoveSpeed = 1.0f, Xp = 20, Armor = 0.25f },
                Boss = new EnemyDef { Id = "pulse_lord", Name = "맥박 군주", Lore = "병든 자를 돌보던 성직자. 병과 하나가 되었다.", MaxHp = 80, Damage = 9, MoveSpeed = 0.9f, Xp = 14, Kit = "tide", SlamName = "피의 파도", SpecialName = "맥박" },
                ZoneLines = new[]
                {
                    "도살장 아래\n여기서 무엇이 잘렸는지 묻지 마라",
                    "힘줄의 무대\n극단은 아직 공연 중이다. 관객이 없을 뿐",
                    "간수의 우리\n갇힌 것과 가둔 것이 같은 살이 되었다",
                    "맥박 군주의 심실\n그는 아직 병자를 돌본다. 병자는 그 자신이다",
                },
            },
            new ActDef
            {
                Numeral = "IV", Name = "얼음 동굴", Ranks = Ice, Patterns = StdPatterns,
                Elite = new EnemyDef { Id = "frost_lord_knight", Name = "서리 군주 기사", Lore = "변방 요새의 기사단장. 끝까지 자리를 비우지 않았다.", MaxHp = 48, Damage = 7, MoveSpeed = 1.0f, Xp = 20, Armor = 0.25f },
                Boss = new EnemyDef { Id = "giant_in_ice", Name = "얼음 속 거인", Lore = "별이 지던 날 끝까지 요새 문을 지킨 문지기.", MaxHp = 85, Damage = 10, MoveSpeed = 0.9f, Xp = 15, Kit = "stars", SlamName = "고드름 낙하", SpecialName = "서리 후광" },
                ZoneLines = new[]
                {
                    "서리 길\n발자국은 얼었고, 발자국의 주인도 얼었다",
                    "고드름 광산\n광부들의 노래가 고드름 끝에 맺혀 있다",
                    "얼음 성가대\n마지막 음이 얼어서 아직 울린다",
                    "얼음 속 문\n문지기는 아직 문을 지킨다",
                },
            },
            new ActDef
            {
                Numeral = "V", Name = "녹은 대장간", Ranks = Forge, Patterns = ForgePatterns,
                Elite = new EnemyDef { Id = "guild_enforcer", Name = "조합 집행자", Lore = "조합의 규칙을 망치로 지키던 자. 규칙만 남았다.", MaxHp = 50, Damage = 7, MoveSpeed = 1.0f, Xp = 20, Armor = 0.25f },
                Boss = new EnemyDef { Id = "molten_giant", Name = "쇳물 거인", Lore = "쇳물과 하나가 된 조합장. 새 별을 벼려 내면 하늘이 돌아올 거라 믿었다.", MaxHp = 90, Damage = 10, MoveSpeed = 0.9f, Xp = 15, Kit = "pour", SlamName = "쇳물 내려찍기", SpecialName = "새 별을 벼려라" },
                ZoneLines = new[]
                {
                    "광재의 갱도\n떨어진 별의 쇠를 캐던 이들이 아직 곡괭이를 쥐고 있다",
                    "풀무의 전당\n불은 꺼지지 않았다. 꺼뜨릴 사람이 없었으니까",
                    "거푸집의 무덤\n새 별을 부으려던 틀이 금 간 채 식어 있다",
                    "쇳물 거인의 노\n그는 아직 별을 두드리고 있다",
                },
            },
            new ActDef
            {
                Numeral = "VI", Name = "별 없는 심연", Ranks = Abyss, Patterns = AbyssPatterns,
                Elite = new EnemyDef { Id = "ash_herald", Name = "재의 전령", Lore = "별이 떨어진다고 외치던 전령. 그때는 아무도 믿지 않았다.", MaxHp = 52, Damage = 7, MoveSpeed = 1.0f, Xp = 20, Armor = 0.25f },
                Boss = new EnemyDef { Id = "dead_star_apostle", Name = "꺼진 별의 사도", Lore = "아무 준비 없이 별을 맞은 자들. 누군가 일러 주었더라면.", MaxHp = 95, Damage = 10, MoveSpeed = 0.9f, Xp = 16, Kit = "stars", SlamName = "별 낙하", SpecialName = "재의 후광" },
                ZoneLines = new[]
                {
                    "순례자의 끝\n별을 보러 내려온 이들이 별의 시체 곁에 앉아 있다",
                    "꺼진 하늘의 평원\n여기엔 위도 아래도 없다. 꺼진 별만 있다",
                    "첫 등불지기의 자리\n누군가 여기서 쓰러졌다. 등불은 아직 따뜻하다",
                    "꺼진 별의 사도\n아무도 경고하지 않았다. 그래서 그들은 함께 별을 맞았다",
                },
            },
        };

        /// <summary>The act for an act index; past the last act the last one repeats (stronger each loop).</summary>
        public static ActDef ForAct(int act) => Acts[Math.Min(Math.Max(0, act), Acts.Length - 1)];
    }

    /// <summary>One act of the descent (GDD §6.3): its monsters, their per-zone mix, elite, boss and zone lines.</summary>
    public sealed class ActDef
    {
        public string Numeral, Name;
        public EnemyDef[] Ranks;
        public int[][][] Patterns;
        public EnemyDef Elite, Boss;
        public string[] ZoneLines;

        public int[] PatternFor(int zone, int floorInZone)
        {
            var z = Patterns[Math.Min(Math.Max(0, zone), Patterns.Length - 1)];
            return z[Math.Min(Math.Max(0, floorInZone), z.Length - 1)];
        }
    }
}
