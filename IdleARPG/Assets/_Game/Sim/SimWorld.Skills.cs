using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// Skill engine: casting, the ten skill shapes, glyphs, heat (작열), the elemental cycle resonances, the resonance
    /// gauge (대공명), mastery, evolution, and the death chain with decay and a per-tick budget.
    /// docs/specs/2026-11-19-skill-rune-synergy-design.md is the source of every rule and number here.
    /// </summary>
    public sealed partial class SimWorld
    {
        public const int ChainCap = 6;               // §3: base hops (spike max 7 → 6 + bonuses)
        public const int ChainBonusCap = 3;          // hop bonuses from essences, glyphs and skills add up to +3
        public const float ChainRadius = 1.8f, ChainDecay = 0.85f, ChainResonanceFactor = 0.7f;
        public const int ChainBudgetPerTick = 24;
        public const float GaugeMax = 20f, GrandRadius = 5f, HeatMax = 100f;
        public static double KillXpScale = 0.4;

        struct PendingPop { public Vec2 Pos; public Element Mark; public float Power, Ripple; public int Depth, Cascade, AtTick, HopBonus; }
        readonly List<PendingPop> _pending = new List<PendingPop>();
        /// <summary>Enemy ids each live cascade has already hit. Lookup only, never iterated (determinism).</summary>
        readonly Dictionary<int, HashSet<int>> _cascadeHits = new Dictionary<int, HashSet<int>>();

        struct Blast
        {
            public int AtTick, Slot;
            public Vec2 Pos;
            public float Radius, Power;
            public Element Elem, Remark;
            public string Fx;
            public SkillMods Mods;
            public float Pull, Trail;
        }
        readonly List<Blast> _blasts = new List<Blast>();
        /// <summary>Skill copies waiting for their tick (Duplicate staggers them).</summary>
        readonly List<(int at, Cast c)> _copies = new List<(int, Cast)>();

        sealed class Ground
        {
            public Vec2 Pos;
            public float Radius, Power, Heal, Stun, Pull, EndPower;
            public Element Elem;
            public int UntilTick;
            public bool Ignite;
            public string Fx;
            public readonly HashSet<int> Ignited = new HashSet<int>();
        }
        readonly List<Ground> _grounds = new List<Ground>();
        int _nextLink = 1;
        bool _inGrand;

        /// <summary>What a hit carries besides its damage: the cast's glyph bonuses and where it came from (for the view).</summary>
        public struct HitCtx
        {
            public int Slot, HopBonus;
            public float ResBonus, GaugeBonus, Leech, Ripple;
            public bool Direct;
            /// <summary>Damage already carrying ignition/stance/medal scaling (splashes cut from a scaled hit).</summary>
            public bool Prescaled;
            public Vec2 From;
            public Resonance Res;
            /// <summary>The skill behind the blow, carried onto its Hit event (the view's hit weight, 재기획 §4.3).</summary>
            public string SkillId;
            public static HitCtx None => new HitCtx { Slot = -1 };
        }

        sealed class Cast
        {
            public SkillDef Skill;
            public SkillMods Mods;
            public Element Elem;
            public float Power, Radius;
            public int Slot;
            public bool Scorch;
            public Enemy Target;
            public HitCtx Ctx;
        }

        void ResetSkillState()
        {
            _pending.Clear();
            _cascadeHits.Clear();
            _blasts.Clear();
            _copies.Clear();
            _whirl = null;
            _grounds.Clear();
        }

        // ---------- ring ----------

        /// <summary>Rebuild the ring from the preset, level and evolutions (cooldowns of kept skills carry over).</summary>
        public void RefreshRing()
        {
            var old = Hero.Ring;
            // 기억의 나무 first: what it holds decides which skills the ring may use, the talent points and the evolutions.
            if (Hero.TreeAuto) SkillTree.AutoAllocate(Hero, WantedSkills());
            else SkillTree.TrimToPoints(Hero);
            var fresh = SkillTree.Apply(Hero);
            var unlocked = new HashSet<string>();
            string ultimate = null;
            foreach (var id in Hero.TreeNodes)
            {
                if (id.StartsWith("s:", StringComparison.Ordinal)) unlocked.Add(id.Substring(2));
                else if (id.StartsWith("u:", StringComparison.Ordinal)) ultimate = id.Substring(2);
            }
            Hero.Ring = Hero.UseCustomRing
                ? SkillBook.BuildCustomRing(Hero.ClassId, Hero.CustomSkills, Hero.CustomSigils, Hero.IntegratedLevel, Hero.Evolved, Hero.Preset, unlocked, ultimate)
                : SkillBook.BuildRing(Hero.ClassId, Hero.Preset, Hero.IntegratedLevel, Hero.Evolved, unlocked, ultimate);
            Hero.Ring.CopyTimersFrom(old);
            HeroStats.Recompute(Hero);
            foreach (var id in fresh)
            {
                var from = SkillBook.Get(id);
                if (from?.EvolveTo != null) Events.Add(new SimEvent { Type = SimEventType.Evolved, SourceId = Hero.Id, Pos = Hero.Pos, SkillId = from.EvolveTo });
            }
        }

        /// <summary>What 자동 분배 grows toward: the ring in use (the player's own, or the preset's ring then its
        /// priority list) and its ultimate.</summary>
        List<string> WantedSkills()
        {
            var list = new List<string>();
            var presets = SkillBook.PresetsFor(Hero.ClassId);
            var p = presets[Math.Max(0, Math.Min(presets.Length - 1, Hero.Preset))];
            if (Hero.UseCustomRing) foreach (var id in Hero.CustomSkills) if (id != null) list.Add(id);
            foreach (var id in p.Ring) if (!list.Contains(id)) list.Add(id);
            foreach (var id in p.Priority) if (!list.Contains(id)) list.Add(id);
            if (p.Ultimate != null) list.Add(p.Ultimate);
            return list;
        }

        /// <summary>The tree panel: take a node by hand (자동 분배 goes off).</summary>
        public bool TakeTreeNode(string id)
        {
            if (!SkillTree.Allocate(Hero, id)) return false;
            RefreshRing();
            return true;
        }

        /// <summary>The tree panel: 자동 분배 on (re-grows the tree along the ring) or off (keeps what is taken).</summary>
        public void SetTreeAuto(bool on)
        {
            Hero.TreeAuto = on;
            RefreshRing();
        }

        /// <summary>The tree panel: give back every point (hand mode).</summary>
        public void ResetTree()
        {
            SkillTree.ResetAll(Hero);
            RefreshRing();
        }


        /// <summary>R panel: start arranging from the ring in use (so nothing changes until the player edits).</summary>
        public void BeginCustomRing()
        {
            if (Hero.UseCustomRing) return;
            for (int i = 0; i < 4; i++)
            {
                var s = Hero.Ring.Slots[i];
                Hero.CustomSkills[i] = s?.BaseId;
                Hero.CustomSigils[i] = Hero.Ring.Sigils[i];
            }
            Hero.UseCustomRing = true;
        }

        public void SetCustomSkill(int slot, string skillId)
        {
            if (slot < 0 || slot > 3) return;
            BeginCustomRing();
            // A skill sits in one slot only: swap if it was elsewhere.
            for (int i = 0; i < 4; i++) if (i != slot && Hero.CustomSkills[i] == skillId) Hero.CustomSkills[i] = Hero.CustomSkills[slot];
            Hero.CustomSkills[slot] = skillId;
            RefreshRing();
        }

        /// <summary>고리 시험's pick: the ring's skills in this order (glyphs stay with their slots).</summary>
        public void SetCustomOrder(string[] order)
        {
            if (order == null || order.Length != 4) return;
            BeginCustomRing();
            for (int i = 0; i < 4; i++) Hero.CustomSkills[i] = order[i];
            RefreshRing();
        }

        public void SetCustomSigil(int slot, Sigil sigil)
        {
            if (slot < 0 || slot > 3) return;
            BeginCustomRing();
            Hero.CustomSigils[slot] = sigil;
            RefreshRing();
        }

        public void SetPreset(int index)
        {
            Hero.UseCustomRing = false;
            int n = SkillBook.PresetsFor(Hero.ClassId).Length;
            Hero.Preset = ((index % n) + n) % n;
            RefreshRing();
        }

        /// <summary>How close the hero walks before casting what comes next.</summary>
        float CastReach() => CastReach(Hero.Ring.NextReady(Time));

        /// <summary>The hero walks in only as far as the basic attack (or a ready skill reaching farther) needs.</summary>
        float EngageReach()
        {
            float r = CastReach(Hero.Ring.Basic);
            for (int i = 0; i < Hero.Ring.Slots.Length; i++)
            {
                var s = Hero.Ring.Slots[i];
                if (s != null && Hero.Ring.CooldownLeft(i, Time) <= 0.0) r = Math.Max(r, CastReach(s));
            }
            return r;
        }

        /// <summary>First ready slot in ring order that reaches <paramref name="dist"/>; the basic attack otherwise.</summary>
        SkillDef ReadyWithin(float dist, out int slot)
        {
            for (int i = 0; i < Hero.Ring.Slots.Length; i++)
            {
                var s = Hero.Ring.Slots[i];
                if (s != null && Hero.Ring.CooldownLeft(i, Time) <= 0.0 && CastReach(s) >= dist) { slot = i; return s; }
            }
            slot = -1;
            return Hero.Ring.Basic;
        }

        float CastReach(SkillDef next)
        {
            // 희승: ranged felt short — shots reach their full radius, and 사거리 % stretches every ranged reach.
            float far = 1f + Hero.Derived.RangePct;
            switch (next.Shape)
            {
                // A ranged skill reaches at least as far as the bow or orb, so the hero never walks in just to use it
                // (희승: 궁수가 멈칫멈칫한다 — walk a step, shoot, walk a step).
                case SkillShape.Single: return next.Ranged ? Math.Max(next.Radius, Hero.Ring.Basic.Radius) * 1.1f * far : Math.Max(MeleeRange, next.Radius * 0.8f);
                case SkillShape.Circle: return next.AtTarget ? 6f * far : Math.Max(MeleeRange, next.Radius * 0.8f * (1f + Hero.Derived.AreaPct));
                case SkillShape.Cone: return Math.Max(MeleeRange, next.Radius * 0.7f * (1f + Hero.Derived.AreaPct));
                case SkillShape.Line: return next.Length * 0.8f * far;
                case SkillShape.Leap: return 6f;
                case SkillShape.Pull: return 4f * far;
                case SkillShape.Delayed: return 6f * far;
                default: return 3f;
            }
        }

        int SkillLevelOf(SkillDef s) => Hero.SkillUses.TryGetValue(s.BaseId, out int n) ? SkillBook.SkillLevel(n) : 1;

        bool UltimateWorthIt(Enemy target)
        {
            if (target.Boss || target.Elite) return true;
            int near = 0;
            foreach (var e in _enemies) if (e.Alive && Vec2.DistSq(e.Pos, Hero.Pos) <= 16f) near++;
            return near >= 4;
        }

        // ---------- casting ----------

        void TryCast(Enemy target) => TryCast(target, 0f);

        void TryCast(Enemy target, float dist)
        {
            if (Time < Hero.AttackReadyAt) return;
            SkillDef skill;
            int slot;
            if (Hero.Ring.UltimateReady(Time) && UltimateWorthIt(target))
            {
                skill = Hero.Ring.Ultimate;
                slot = -2;
                Hero.Ring.MarkUltimateUsed(Time, skill.Cooldown);
            }
            else skill = ReadyWithin(dist, out slot);

            var mods = SkillTree.WithTreeMods(Hero, slot >= 0 ? skill : null, Hero.Ring.ModsFor(slot)); // + 기억의 나무 변형
            if (slot >= 0)
            {
                double huntMul = (Time < Hero.HuntTimeUntil ? 0.5 : 1.0) * (Time < Hero.EternalUntil ? 0.334 : 1.0); // 사냥의 시간 / 영원의 순간
                Hero.Ring.MarkUsed(slot, Time, skill.Cooldown * mods.CooldownMul * huntMul * (1.0 - Hero.Derived.CooldownCut));
                if (mods.Rewind && slot != 0) Hero.Ring.Reduce(0, 1.0);
            }
            double speed = Hero.Stats.AttackSpeed * (Time < Hero.StormEyeUntil ? 1.5 : 1.0) * (Time < Hero.HuntTimeUntil ? 2.0 : 1.0)
                * (Time < Hero.BlessUntil || Time < Hero.EternalUntil ? 1.4 : 1.0);
            Hero.AttackReadyAt = Time + 1.0 / speed;
            _castLock = Time + Math.Min(0.3, 0.6 / speed); // hold still while the shot leaves
            var toTarget = target.Pos - Hero.Pos;
            if (toTarget.LengthSq > 1e-6f) Hero.Facing = toTarget.Normalized;

            // 작열: a full heat bar turns the next ring skill into its scorching version.
            bool scorch = slot >= 0 && Hero.Heat >= HeatMax;
            if (scorch) Hero.Heat = 0f;
            float scorchMul = (Talents.Has(Hero, Talents.Offense, 2) ? 2f : 1.5f) + (Hero.Derived.Runewords.Contains("rw_firering") ? 0.5f : 0f); // 종말의 불꽃 / 화염의 고리
            Hero.Heat = Math.Min(HeatMax, Hero.Heat + (IsOathknight ? 1f : IsWindhunter ? 2f : IsStarseer ? 3f : 4f));

            var elem = mods.Convert != Element.None ? mods.Convert : skill.Elem;
            if (scorch && elem == Element.None) elem = Element.Ember;
            int level = SkillLevelOf(skill);
            float radius = skill.Radius * mods.RadiusMul * (scorch ? 1.3f : 1f)
                * (skill.Ranged && skill.Shape == SkillShape.Single ? 1f : (1f + Hero.Derived.AreaPct) * SkillBook.LevelArea(level));
            if (skill.BaseId == "ember_whirl" && Hero.Has("wide_whirl")) radius *= 1.4f;
            var c = new Cast
            {
                Skill = skill, Mods = mods, Elem = elem, Power = skill.Power * (scorch ? scorchMul : 1f) * SkillBook.LevelPower(level) * (1f + Augments.PowerFor(Hero, skill.BaseId)), Radius = radius,
                Slot = slot, Scorch = scorch, Target = target,
                Ctx = new HitCtx { Slot = slot, ResBonus = mods.ResBonus, GaugeBonus = mods.GaugeBonus, Leech = mods.Leech, Ripple = mods.Ripple, HopBonus = mods.HopBonus, Direct = true, SkillId = skill.Id },
            };
            bool atTarget = skill.AtTarget || skill.Shape == SkillShape.Delayed || skill.Shape == SkillShape.Pull;
            Events.Add(new SimEvent
            {
                Type = SimEventType.Cast, SourceId = Hero.Id, TargetId = target.Id, Pos = atTarget ? target.Pos : Hero.Pos, From = Hero.Pos,
                SkillId = skill.Id, Elem = elem, Value = radius, Depth = scorch ? 1 : 0,
            });
            CountUse(skill, mods);
            Execute(c);
            AfterCastAugments(skill, target);
            if (skill.Channel > 0f) StartWhirl(c);
            if (skill.Volley > 0) QueueVolley(c);
            Duplicate(c, SkillBook.LevelCopies(level) + mods.ExtraCopies + (skill.Copyable ? Hero.Derived.Projectiles : 0));
            // 별의 축복 (Noita wrap): the last ring slot calls the first one again at 60%.
            if ((Time < Hero.BlessUntil || Time < Hero.EternalUntil) && slot > 0 && slot == LastSlot() && Hero.Ring.Slots[0] != null)
            {
                var first = Hero.Ring.Slots[0];
                var fm = Hero.Ring.ModsFor(0);
                var echo = new Cast
                {
                    Skill = first, Mods = fm, Elem = fm.Convert != Element.None ? fm.Convert : first.Elem, Power = first.Power * 0.6f, Radius = first.Radius * fm.RadiusMul,
                    Slot = 0, Target = target,
                    Ctx = new HitCtx { Slot = 0, ResBonus = fm.ResBonus, GaugeBonus = fm.GaugeBonus, Leech = fm.Leech, Ripple = fm.Ripple, HopBonus = fm.HopBonus, Direct = true, SkillId = first.Id },
                };
                Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = Hero.Id, TargetId = target.Id, Pos = first.AtTarget || first.Shape == SkillShape.Delayed ? target.Pos : Hero.Pos, From = Hero.Pos, SkillId = first.Id, Elem = echo.Elem, Value = echo.Radius });
                Execute(echo);
            }

            if ((mods.Echo || skill.AlwaysEcho) && Damaging(skill.Shape))
            {
                var at = skill.Shape == SkillShape.Circle && !skill.AtTarget ? Hero.Pos : target.Pos;
                _blasts.Add(new Blast { AtTick = TickCount + 6, Pos = at, Radius = Math.Max(1.2f, radius * 0.8f), Power = c.Power * 0.5f, Elem = elem, Slot = slot, Mods = mods, Fx = skill.Id + "_echo" });
            }
            if (mods.EmberGround && Damaging(skill.Shape)) AddGround(target.Pos, 1.2f, c.Power * 0.2f, Element.Ember, 2f);
        }

        int LastSlot()
        {
            for (int i = Hero.Ring.Slots.Length - 1; i >= 0; i--) if (Hero.Ring.Slots[i] != null) return i;
            return -1;
        }

        static bool Damaging(SkillShape s) => s != SkillShape.Buff && s != SkillShape.Aura && s != SkillShape.Armed;

        void Execute(Cast c)
        {
            var s = c.Skill;
            switch (s.Shape)
            {
                case SkillShape.Single when s.WallBounces > 0:
                {
                    var d = c.Target.Pos - Hero.Pos;
                    Ricochet(c, d, 1f);
                    break;
                }
                case SkillShape.Single when s.Ranged && s.Bounces == 0 && Time >= Hero.StormEyeUntil:
                {
                    for (int h = 0; h < s.Hits; h++)
                    {
                        Events.Add(new SimEvent { Type = SimEventType.Shot, SourceId = Hero.Id, TargetId = c.Target.Id, From = Hero.Pos, Pos = c.Target.Pos, SkillId = s.Id });
                        var ctx = c.Ctx; ctx.From = Hero.Pos;
                        Strike(c, c.Target, 1f, ctx);
                    }
                    if (s.Forks > 0) Fork(c, c.Target, Math.Max(1, s.ForkDepth), new List<Enemy> { c.Target });
                    break;
                }
                case SkillShape.Single:
                {
                    // 폭풍의 눈 makes every ranged shot pierce two more.
                    int extra = s.Ranged && Time < Hero.StormEyeUntil ? 2 : 0;
                    if (s.Bounces + extra > 0)
                    {
                        // 방패 투척: a thrown shield that ricochets from target to target (each leg is a Shot for the view).
                        var hit = new List<Enemy>();
                        var from = Hero.Pos;
                        var cur = c.Target;
                        for (int b = 0; b <= s.Bounces + extra + c.Mods.Bounces && cur != null; b++)
                        {
                            Events.Add(new SimEvent { Type = SimEventType.Shot, SourceId = Hero.Id, TargetId = cur.Id, From = from, Pos = cur.Pos, SkillId = s.Id });
                            var ctx = c.Ctx; ctx.From = from;
                            Strike(c, cur, b == 0 ? 1f : 0.8f, ctx);
                            hit.Add(cur);
                            from = cur.Pos;
                            cur = NearestExcept(from, 3.5f, hit);
                        }
                        break;
                    }
                    for (int h = 0; h < s.Hits; h++) Strike(c, c.Target, 1f);
                    if (s.BaseId == "ember_slash" && Hero.Has("ember_echo")) Strike(c, c.Target, 1f);
                    // 휩쓸기 (희승: 워리어 계열은 다 휩쓸고 다닌다): a melee class's basic swing also cuts what stands
                    // around its target at 40%, the arc widening with skill area.
                    if (s == Hero.Ring.Basic && !s.Ranged && HeroStats.IsMeleeClass(Hero.ClassId))
                    {
                        float arc = 1.1f * (1f + Hero.Derived.AreaPct);
                        foreach (var e in InRadius(c.Target.Pos, arc))
                            if (e != c.Target) { var ctx = c.Ctx; ctx.From = c.Target.Pos; Strike(c, e, 0.4f, ctx); }
                    }
                    if (c.Mods.Bounces > 0)
                    {
                        var next = Nearest(c.Target.Pos, 3f, c.Target);
                        if (next != null) { var ctx = c.Ctx; ctx.From = c.Target.Pos; Strike(c, next, 0.7f, ctx); }
                    }
                    break;
                }
                case SkillShape.Circle when s.Ground:
                {
                    // 축성된 대지 / 수호의 각인: a field that pulses every half second.
                    var at = s.AtTarget ? c.Target.Pos : Hero.Pos;
                    AddGround(at, c.Radius, c.Power, c.Elem, s.Duration, s.GroundHeal, s.StunSeconds, s.ChainIgnite, s.GroundPull, s.EndPower, s.BaseId);
                    break;
                }
                case SkillShape.Circle when s.Bell:
                {
                    // 심판의 종 (독창): every marked enemy in range sets off its own forward resonance at once.
                    float oathBonus = 1f + Hero.Heat / 100f;
                    Hero.Heat = 0f;
                    foreach (var e in InRadius(Hero.Pos, c.Radius))
                    {
                        var hitElem = e.Mark != Element.None ? Elements.Next(e.Mark) : Element.None;
                        ApplyHit(e, RollHeroDamage(c.Power * oathBonus, hitElem, c.Mods, e), hitElem, false, 0, 0, c.Ctx);
                    }
                    break;
                }
                case SkillShape.Circle when s.Constellation > 0:
                {
                    // 별자리 잇기 (독창): link the marked into a constellation; each star takes √(stars) × power.
                    var stars = InRadius(Hero.Pos, c.Radius).FindAll(e => e.Mark != Element.None);
                    stars.Sort((a, b) => Math.Atan2(a.Pos.Y - Hero.Pos.Y, a.Pos.X - Hero.Pos.X).CompareTo(Math.Atan2(b.Pos.Y - Hero.Pos.Y, b.Pos.X - Hero.Pos.X)));
                    if (stars.Count > s.Constellation) stars.RemoveRange(s.Constellation, stars.Count - s.Constellation);
                    float mul = (float)Math.Sqrt(Math.Max(1, stars.Count));
                    for (int k = 0; k < stars.Count; k++)
                    {
                        if (k > 0) Events.Add(new SimEvent { Type = SimEventType.Pull, SourceId = Hero.Id, From = stars[k - 1].Pos, Pos = stars[k].Pos, SkillId = s.Id });
                        Strike(c, stars[k], mul);
                    }
                    break;
                }
                case SkillShape.Circle when s.TimeStopMul > 0f:
                {
                    // 시간 정지 (독창): freeze everything near; what they take meanwhile lands at once when time resumes.
                    Hero.TimeStopUntil = Time + s.Duration;
                    foreach (var e in InRadius(Hero.Pos, c.Radius))
                    {
                        e.FrozenUntil = Time + s.Duration;
                        e.StunUntil = Math.Max(e.StunUntil, Time + s.Duration);
                        e.StoredMul = s.TimeStopMul;
                    }
                    break;
                }
                case SkillShape.Circle when s.Taunt:
                {
                    // 도발의 외침: drag the room onto the knight, brace for it, and take the oath.
                    foreach (var e in PullToward(Hero.Pos, c.Radius, 0.4f, null))
                    {
                        e.Aggro = true;
                        if (s.TauntMark && e.Mark == Element.None) { e.Mark = Element.Frost; e.MarkSlot = c.Slot; }
                        Strike(c, e, 1f);
                    }
                    Hero.GuardUntil = Time + s.Duration;
                    Hero.Heat = Math.Min(HeatMax, Hero.Heat + 20f);
                    break;
                }
                case SkillShape.Circle:
                {
                    var centre = s.AtTarget ? c.Target.Pos : Hero.Pos;
                    if (s.Pull > 0f) PullToward(centre, c.Radius, s.Pull, null);
                    float healBurst = 0f;
                    if (s.BaseId == "heat_absorb")
                    {
                        float heal = Hero.Heat * 0.002f * Hero.Stats.MaxHp;
                        Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + heal);
                        Hero.Heat = 0f;
                        if (s.HealBurst) healBurst = heal;
                    }
                    var fromCentre = c.Ctx; fromCentre.From = centre; // the view throws the bits away from the blast's centre
                    foreach (var e in InRadius(centre, c.Radius))
                    {
                        Strike(c, e, 1f, fromCentre);
                        if (healBurst > 0f) ApplyHit(e, healBurst, Element.None, false, 0, 0, HitCtx.None);
                    }
                    if (s.Id == "forge_judgment" || s.Id == "grand_judgment" || s.Id == "star_collapse") AddGauge(GaugeMax * 0.5f, force: true);
                    break;
                }
                case SkillShape.Cone:
                {
                    var dir = Hero.Facing;
                    float cosHalf = (float)Math.Cos(s.Arc * 0.5f * Math.PI / 180.0);
                    var hit = new List<Enemy>();
                    foreach (var e in InRadius(Hero.Pos, c.Radius))
                    {
                        var d = e.Pos - Hero.Pos;
                        float len = d.Length;
                        if (len < 0.4f || (d.X * dir.X + d.Y * dir.Y) / len >= cosHalf) { Strike(c, e, 1f); hit.Add(e); }
                    }
                    // 천둥 가르기: bolts leap from the edge of the fan to enemies it missed.
                    for (int b = 0; b < s.Bolts; b++)
                    {
                        var tip = Hero.Pos + dir * c.Radius;
                        var t = NearestExcept(tip, 3.5f, hit);
                        if (t == null) break;
                        hit.Add(t);
                        var ctx = c.Ctx; ctx.From = tip;
                        Strike(c, t, 0.6f, ctx);
                    }
                    break;
                }
                case SkillShape.Line:
                {
                    int lines = s.Lines + c.Mods.ExtraLines;
                    var dir = Hero.Facing;
                    for (int l = 0; l < lines; l++)
                    {
                        float ang = (l - (lines - 1) / 2f) * 0.35f; // ≈20° between lines
                        var d = Rotate(dir, ang);
                        for (int step = 1; step <= (int)s.Length; step++)
                        {
                            var p = Hero.Pos + d * step;
                            if (!Floor.IsWalkable(p)) break;
                            _blasts.Add(new Blast { AtTick = TickCount + step, Pos = p, Radius = c.Radius * 0.75f, Power = c.Power, Elem = c.Elem, Slot = c.Slot, Mods = c.Mods, Pull = s.Pull, Trail = s.TrailSeconds,
                                Fx = s.BaseId == "fissure" ? "fissure_burst" : s.BaseId + "_burst" });
                        }
                    }
                    break;
                }
                case SkillShape.Leap when s.LeapAway:
                {
                    // 구르기 사격: roll back out of the pack, then shoot.
                    var from = Hero.Pos;
                    var away = Hero.Pos - c.Target.Pos;
                    if (away.LengthSq > 1e-6f)
                    {
                        var dest = Slide(Hero.Pos, Hero.Pos + away.Normalized * 2.5f);
                        if (Floor.IsWalkable(dest)) { Hero.Pos = dest; _path.Clear(); }
                    }
                    Events.Add(new SimEvent { Type = SimEventType.Leap, SourceId = Hero.Id, From = from, Pos = Hero.Pos, SkillId = s.Id, Value = 0.6f });
                    for (int h = 0; h < s.Hits; h++)
                    {
                        Events.Add(new SimEvent { Type = SimEventType.Shot, SourceId = Hero.Id, TargetId = c.Target.Id, From = Hero.Pos, Pos = c.Target.Pos, SkillId = s.Id });
                        var ctx = c.Ctx; ctx.From = Hero.Pos;
                        Strike(c, c.Target, 1f, ctx);
                    }
                    break;
                }
                case SkillShape.Leap:
                {
                    var from = Hero.Pos;
                    var dest = s.LeapToTarget ? c.Target.Pos : DensestPoint(6f) ?? c.Target.Pos;
                    if (Floor.IsWalkable(dest))
                    {
                        Hero.Pos = dest;
                        _path.Clear();
                    }
                    Events.Add(new SimEvent { Type = SimEventType.Leap, SourceId = Hero.Id, From = from, Pos = Hero.Pos, SkillId = s.Id, Value = c.Radius });
                    foreach (var e in InRadius(Hero.Pos, c.Radius)) Strike(c, e, 1f);
                    if (s.GroundSeconds > 0f) AddGround(Hero.Pos, c.Radius, c.Power * 0.25f, s.Elem == Element.None ? Element.Ember : s.Elem, s.GroundSeconds,
                        fx: s.Id == "evo_meteor_fall" ? "evo_meteor_burn" : null);
                    break;
                }
                case SkillShape.Pull:
                {
                    var centre = c.Target.Pos;
                    var pulled = PullToward(centre, c.Radius, s.Pull, null);
                    Events.Add(new SimEvent { Type = SimEventType.Pull, SourceId = Hero.Id, From = Hero.Pos, Pos = centre, SkillId = s.Id, Value = c.Radius });
                    int link = s.LinkShare ? _nextLink++ : 0;
                    foreach (var e in pulled)
                    {
                        if (link != 0) e.Link = link;
                        var pullCtx = c.Ctx; pullCtx.From = centre;
                        Strike(c, e, 1f, pullCtx);
                    }
                    break;
                }
                case SkillShape.Delayed when s.Spears > 0:
                {
                    // 천상의 창: spears fall on the nearest few, each a beat after the last.
                    var targets = new List<Enemy>();
                    for (int k = 0; k < s.Spears + c.Mods.ExtraLines; k++)
                    {
                        var t = NearestExcept(Hero.Pos, s.Range, targets);
                        if (t == null) break;
                        targets.Add(t);
                    }
                    int delay = Math.Max(1, (int)Math.Round(s.Delay / Dt));
                    for (int k = 0; k < targets.Count; k++)
                        _blasts.Add(new Blast { AtTick = TickCount + delay + k * 2, Pos = targets[k].Pos, Radius = c.Radius, Power = c.Power, Elem = c.Elem, Slot = c.Slot, Mods = c.Mods, Fx = s.BaseId == "arrow_rain" ? "arrow_rain_hit" : s.BaseId == "meteor" ? "meteor" : s.BaseId == "lightning_storm" ? "thunderstorm_bolt" : "heaven_spear" });
                    break;
                }
                case SkillShape.Delayed:
                {
                    int extra = Math.Min(3, c.Target.Stacks + (c.Target.Mark != Element.None ? 1 : 0));
                    int delay = Math.Max(1, (int)Math.Round(s.Delay / Dt));
                    for (int k = 0; k <= extra; k++)
                        _blasts.Add(new Blast { AtTick = TickCount + delay + k * 3, Pos = c.Target.Pos, Radius = c.Radius, Power = c.Power * (k == 0 ? 1f : 0.6f), Elem = c.Elem, Slot = c.Slot, Mods = c.Mods, Fx = "smith_hammer_drop" });
                    break;
                }
                case SkillShape.Buff:
                {
                    if (s.BaseId == "thorn_mail") { Hero.ThornUntil = Time + s.Duration; Hero.ThornRadius = s.ThornRadius; }
                    else if (s.BaseId == "molten_heart")
                    {
                        if (Hero.Stats.Hp > Hero.Stats.MaxHp * 0.5f) Hero.Stats.Hp -= Hero.Stats.MaxHp * 0.08f; // never bleeds a hero who is already in trouble
                        Hero.MoltenUntil = Time + s.Duration;
                    }
                    else if (s.Id == "undying_flame") Hero.UndyingUntil = Time + s.Duration;
                    else if (s.ShieldPct > 0f)
                    {
                        Hero.ShieldFx = s.BaseId == "star_veil" ? "star_veil_break" : "oath_barrier_break";
                        Hero.ShieldElem = c.Elem == Element.None ? Element.Shock : c.Elem;
                        Hero.Shield = Hero.Stats.MaxHp * s.ShieldPct;
                        Hero.ShieldUntil = Time + s.Duration;
                        Hero.ShieldPower = c.Power;
                        Hero.ShieldRadius = c.Radius;
                        Hero.ShieldDouble = s.BarrierDouble;
                    }
                    else if (s.BaseId == "martyr_chains")
                    {
                        // 순교자의 사슬 (독창): bind up to six; they take a share of every blow meant for the knight.
                        int link = _nextLink++;
                        int bound = 0;
                        foreach (var e in InRadius(Hero.Pos, c.Radius))
                        {
                            if (bound >= 6) break;
                            e.Link = link;
                            e.Aggro = true;
                            bound++;
                            Events.Add(new SimEvent { Type = SimEventType.Pull, SourceId = Hero.Id, From = Hero.Pos, Pos = e.Pos, SkillId = s.Id });
                        }
                        Hero.MartyrLink = link;
                        Hero.MartyrPct = s.SharePct;
                        Hero.MartyrUntil = Time + s.Duration;
                    }
                    else if (s.BaseId == "unbroken_vow")
                    {
                        Hero.VowUntil = Time + s.Duration;
                        Hero.VowStored = 0f;
                        Hero.VowRadius = c.Radius;
                        Hero.VowActive = true;
                    }
                    else if (s.BaseId == "storm_eye") Hero.StormEyeUntil = Time + s.Duration;
                    else if (s.BaseId == "star_blessing") Hero.BlessUntil = Time + s.Duration;
                    else if (s.Id == "eternal_moment") Hero.EternalUntil = Time + s.Duration;
                    else if (s.BaseId == "rewind")
                    {
                        // 되감기 (독창): wind every ring slot's cooldown back.
                        for (int k = 0; k < Hero.Ring.Slots.Length; k++) if (k != c.Slot) Hero.Ring.Reduce(k, s.RewindSeconds);
                    }
                    else if (s.Id == "thunderstorm") { Hero.ThunderUntil = Time + s.Duration; Hero.ThunderPower = c.Power; }
                    else if (s.Id == "hunt_time") Hero.HuntTimeUntil = Time + s.Duration;
                    else if (s.Id == "sanctuary")
                    {
                        Hero.SanctuaryUntil = Time + s.Duration;
                        Hero.SanctuaryPower = c.Power;
                    }
                    break;
                }
                case SkillShape.Aura:
                    Hero.AuraUntil = Time + s.Duration;
                    Hero.AuraRadius = c.Radius;
                    Hero.AuraPower = c.Power;
                    Hero.AuraPulse = s.AuraPulse;
                    Hero.AuraElem = c.Elem;
                    Hero.AuraHeal = s.AuraHeal;
                    break;
                case SkillShape.Armed when s.BaseId == "viper_nest":
                    Hero.ViperCharges = s.Charges;
                    Hero.ViperPower = c.Power;
                    Hero.ViperRadius = c.Radius;
                    break;
                case SkillShape.Armed when s.BaseId == "grace_echo":
                    Hero.GraceCharges = s.Charges;
                    Hero.GracePower = c.Power;
                    Hero.GraceRadius = c.Radius;
                    break;
                case SkillShape.Armed:
                    Hero.CinderCharges = s.Charges;
                    Hero.CinderSkill = s;
                    Hero.CinderMods = c.Mods;
                    break;
            }
        }

        void Strike(Cast c, Enemy e, float mul) => Strike(c, e, mul, c.Ctx);

        void Strike(Cast c, Enemy e, float mul, HitCtx ctx)
        {
            if (!e.Alive) return;
            if (ctx.From.X == 0f && ctx.From.Y == 0f) ctx.From = Hero.Pos; // where the blow came from (view only: the scatter's direction)
            var s = c.Skill;
            // 용광 일격: cash in every stack (and the mark) on the target.
            if (s.BaseId == "forge_strike")
            {
                int stacks = e.Stacks + (e.Mark != Element.None ? 1 : 0);
                mul *= 1f + 0.4f * stacks;
                e.Stacks = 0;
            }
            if (s.FrozenBonus > 0f && Time < e.FreezeUntil) mul *= 1f + s.FrozenBonus; // 빙결 파쇄
            ApplyHit(e, RollHeroDamage(c.Power * mul, c.Elem, c.Mods, e), c.Elem, false, 0, 0, ctx);
            Combust(c, e);
            if (s.StunSeconds > 0f && e.Alive && !s.Ground) e.StunUntil = Math.Max(e.StunUntil, Time + s.StunSeconds);
            if (s.DotPower > 0f && e.Alive)
            {
                // 독침 화살: poison for 3 s.
                e.DotPerTick = Math.Max(e.DotTicks > 0 ? e.DotPerTick : 0f, Hero.Stats.Damage * s.DotPower / 30f);
                e.DotTicks = 30;
            }
            if (s.HuntedPct > 0f && e.Alive) { e.HuntedUntil = Time + s.Duration; e.HuntedPct = s.HuntedPct; }
            if (s.BurstRadius > 0f)
                foreach (var o in InRadius(e.Pos, s.BurstRadius))
                    if (o != e) ApplyHit(o, RollHeroDamage(c.Power * 0.5f, c.Elem, c.Mods, o), c.Elem, false, 0, 0, HitCtx.None);
            if (s.ExecuteSpread && !e.Alive)
                foreach (var o in InRadius(e.Pos, 2f))
                {
                    o.Stacks = Math.Min(5, o.Stacks + 1);
                    if (o.Mark == Element.None) o.Mark = Element.Ember;
                }
        }

        /// <summary>
        /// §4: base × skill power × (1 + Σadditive) × A(skill glyphs) × B(essence) × C(conditions) × crit.
        /// Crits roll on the combat stream.
        /// </summary>
        float RollHeroDamage(float power, Element elem, SkillMods mods, Enemy target)
        {
            var d = Hero.Derived;
            bool crit = _combatRng.NextFloat() < d.CritChance + (Time < Hero.BlessUntil || Time < Hero.EternalUntil ? 0.2f : 0f); // 별의 축복: +20% crit
            float a = mods.PowerMul;
            float b = 1f;
            float cAdd = 0f;
            if (target != null)
            {
                if (mods.Execute && target.Stats.Hp < target.Stats.MaxHp * 0.3f) cAdd += 0.6f;
                if (Talents.Has(Hero, Talents.Offense, 1) && target.Stats.Hp > target.Stats.MaxHp * 0.5f) cAdd += 0.25f; // 학살자
                if (mods.Throng)
                {
                    int n = 0;
                    foreach (var e in _enemies) if (e.Alive && e != target && Vec2.DistSq(e.Pos, target.Pos) <= 9f) n++;
                    cAdd += Math.Min(0.3f, 0.03f * n);
                }
            }
            float critMult = d.CritMult + (Time < Hero.AugCritUntil ? Hero.AugCritBonus : 0f); // 증강: crit damage after its skill
            return Damage.Compute(Hero.Stats.Damage * power, d.DamagePct + d.ElementPct(elem) + Hero.AugElem[(int)elem] + ShrineDamagePct, a * b * (1f + cAdd), crit, critMult, 0f, 0f);
        }

        float RollHeroDamage(SkillDef skill) => RollHeroDamage(skill.Power, skill.Elem, SkillMods.Neutral, null);

        // ---------- evolution (§7) ----------

        /// <summary>
        /// 복제 (희승: 이펙트가 커지기만 하는 게 아니라 복제되는 느낌): copies of a thrown or targeted skill fly at the
        /// next enemies around; a burst around the hero repeats over other packs as echoes. Each copy hits at 60%.
        /// </summary>
        void Duplicate(Cast c, int copies)
        {
            if (copies <= 0 || !Damaging(c.Skill.Shape) || c.Skill.Shape == SkillShape.Leap || c.Skill.Shape == SkillShape.Pull) return;
            copies = Math.Min(copies, HeroStats.MaxProjectiles + SkillBook.MaxSkillLevel / SkillBook.CopyEvery);
            var used = new List<Enemy> { c.Target };
            float reach = Math.Max(6f, CastReach(c.Skill)) * 1.5f;
            for (int k = 0; k < copies; k++)
            {
                var other = NearestExcept(c.Target.Pos, reach, used);
                if (other == null)
                {
                    // Nothing else around: a wall-bouncing shot still fans out; others stop.
                    if (c.Skill.WallBounces <= 0) break;
                    float a = (k + 1) * 0.5f * ((k & 1) == 0 ? 1f : -1f);
                    var dir = (c.Target.Pos - Hero.Pos).LengthSq > 1e-6f ? (c.Target.Pos - Hero.Pos).Normalized : Hero.Facing;
                    var rot = new Vec2(dir.X * (float)Math.Cos(a) - dir.Y * (float)Math.Sin(a), dir.X * (float)Math.Sin(a) + dir.Y * (float)Math.Cos(a));
                    Ricochet(c, rot, 0.6f);
                    continue;
                }
                used.Add(other);
                bool aroundHero = c.Skill.Shape == SkillShape.Circle && !c.Skill.AtTarget || c.Skill.Shape == SkillShape.Cone;
                if (aroundHero)
                {
                    _blasts.Add(new Blast { AtTick = TickCount + 2 + k, Pos = other.Pos, Radius = Math.Max(1.2f, c.Radius * 0.8f), Power = c.Power * 0.6f, Elem = c.Elem, Slot = c.Slot, Mods = c.Mods, Fx = c.Skill.Id + "_echo" });
                    continue;
                }
                // 재기획 §4.4: copies go off one by one a tick apart on their own targets — a rhythm of flashes around the
                // pack instead of one blob of every copy on the same frame.
                _copies.Add((TickCount + 1 + k, new Cast { Skill = c.Skill, Mods = c.Mods, Elem = c.Elem, Power = c.Power * 0.6f, Radius = c.Radius, Slot = c.Slot, Scorch = c.Scorch, Target = other, Ctx = c.Ctx }));
            }
        }

        /// <summary>
        /// 도탄: the shot flies straight, bounces off walls up to WallBounces times, and hits every enemy it passes once.
        /// Each leg is a Shot event (Depth = leg index, so the view can fly them one after another).
        /// </summary>
        void Ricochet(Cast c, Vec2 dir, float powerMul)
        {
            var s = c.Skill;
            float left = s.Length * (1f + Hero.Derived.RangePct);
            const float step = 0.25f, hitR = 0.65f;
            var pos = Hero.Pos;
            var legStart = pos;
            int bounces = 0, leg = 0;
            var hit = new HashSet<int>();
            if (dir.LengthSq < 1e-6f) dir = new Vec2(1f, 0f);
            dir = dir.Normalized;
            int guard = 0;
            while (left > 0f && guard++ < 400)
            {
                var next = pos + dir * step;
                if (!Floor.IsWalkable(next))
                {
                    if (bounces >= s.WallBounces) break;
                    bool bx = !Floor.IsWalkable(new Vec2(next.X, pos.Y)), by = !Floor.IsWalkable(new Vec2(pos.X, next.Y));
                    if (bx || !by) dir = new Vec2(-dir.X, dir.Y);
                    if (by || !bx) dir = new Vec2(dir.X, -dir.Y);
                    bounces++;
                    Events.Add(new SimEvent { Type = SimEventType.Shot, SourceId = Hero.Id, From = legStart, Pos = pos, SkillId = s.Id, Depth = leg++ });
                    legStart = pos;
                    continue;
                }
                pos = next;
                left -= step;
                foreach (var e in _enemies)
                {
                    if (!e.Alive || hit.Contains(e.Id) || Vec2.DistSq(e.Pos, pos) > hitR * hitR) continue;
                    hit.Add(e.Id);
                    var ctx = c.Ctx; ctx.From = legStart;
                    Strike(c, e, powerMul, ctx);
                }
            }
            Events.Add(new SimEvent { Type = SimEventType.Shot, SourceId = Hero.Id, From = legStart, Pos = pos, SkillId = s.Id, Depth = leg });
        }

        void CountUse(SkillDef skill, SkillMods mods)
        {
            string id = skill.BaseId;
            Hero.SkillUses.TryGetValue(id, out int uses);
            Hero.SkillUses[id] = ++uses;
            if (SkillBook.SkillLevel(uses) > SkillBook.SkillLevel(uses - 1))
                Events.Add(new SimEvent { Type = SimEventType.SkillLevelUp, SourceId = Hero.Id, Pos = Hero.Pos, SkillId = skill.Id, Value = SkillBook.SkillLevel(uses), Text = skill.Name });
            if (skill == Hero.Ring.Basic) return;
            if (skill.IsEvolution || skill.EvolveTo == null || Hero.Evolved.Contains(id)) return;
            // 기억의 나무: practice opens the evolution node (숙련 10); 자동 분배 takes it at once (RefreshRing raises the
            // 진화 event). By hand the player takes it in the tree.
            if (Hero.TreeAuto && SkillBook.SkillLevel(uses) == SkillTree.EvoMastery && SkillBook.SkillLevel(uses - 1) < SkillTree.EvoMastery) RefreshRing();
        }

        // ---------- per-tick skill upkeep ----------

        void TickSkills()
        {
            TickWhirl();
            TickBurns();
            // Delayed blasts: fissure eruptions, hammers, echoes, cinder echo bursts.
            if (_blasts.Count > 0)
            {
                var due = _blasts.FindAll(b => b.AtTick <= TickCount);
                _blasts.RemoveAll(b => b.AtTick <= TickCount);
                foreach (var b in due) FireBlast(b);
            }

            // Staggered copies: each fires on its tick; a target that died meanwhile hands the copy to the nearest other.
            if (_copies.Count > 0)
            {
                var due = _copies.FindAll(x => x.at <= TickCount);
                _copies.RemoveAll(x => x.at <= TickCount);
                foreach (var (_, copy) in due)
                {
                    if (!copy.Target.Alive)
                    {
                        var next = NearestExcept(copy.Target.Pos, 4f, new List<Enemy>());
                        if (next == null) continue;
                        copy.Target = next;
                    }
                    bool atTarget = copy.Skill.AtTarget || copy.Skill.Shape == SkillShape.Delayed;
                    Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = Hero.Id, TargetId = copy.Target.Id, Pos = atTarget ? copy.Target.Pos : Hero.Pos, From = Hero.Pos, SkillId = copy.Skill.Id, Elem = copy.Elem, Value = copy.Radius, Depth = 2 });
                    Execute(copy);
                }
            }

            // Ground fields pulse every half second.
            if (_grounds.Count > 0)
            {
                // By index over the fields there at the start: a kill inside may lay a new one (it pulses from the next beat).
                for (int gi = 0, gn = _grounds.Count; gi < gn; gi++)
                {
                    var g = _grounds[gi];
                    if (g.UntilTick < TickCount && g.EndPower > 0f)
                        FireHeroBlast(g.Pos, g.Radius, g.EndPower, g.Elem, (g.Fx ?? "ground") + "_burst"); // 그림자 분신 goes off
                }
                _grounds.RemoveAll(g => g.UntilTick < TickCount);
                if (TickCount % 5 == 0)
                    for (int gi = 0, gn = _grounds.Count; gi < gn; gi++)
                    {
                        var g = _grounds[gi];
                        if (g.Pull > 0f) PullToward(g.Pos, g.Radius * 1.6f, g.Pull, null); // 그림자 분신 lures the pack in
                        foreach (var e in InRadius(g.Pos, g.Radius))
                        {
                            // 전격 덫 (점화 장치): a marked enemy caught in the trap starts its death chain without dying.
                            if (g.Ignite && e.Mark != Element.None && g.Ignited.Add(e.Id))
                                QueuePop(e.Pos, e.Mark, LiveChainPower * 0.6f, 0, 0, e.Id);
                            ApplyHit(e, RollHeroDamage(g.Power, g.Elem, SkillMods.Neutral, e), g.Elem, false, 0, 0, HitCtx.None);
                            if (g.Stun > 0f && e.Alive) e.StunUntil = Math.Max(e.StunUntil, Time + g.Stun);
                        }
                        // 축성된 대지: standing on holy ground mends the knight.
                        if (g.Heal > 0f && Vec2.DistSq(Hero.Pos, g.Pos) <= g.Radius * g.Radius)
                            Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + Hero.Stats.MaxHp * g.Heal * 0.5f);
                    }
            }

            // Damage over time (plague).
            foreach (var e in _enemies.ToArray())
            {
                if (!e.Alive || e.DotTicks <= 0) continue;
                e.DotTicks--;
                e.Stats.Hp -= e.DotPerTick;
                if (e.Stats.Hp <= 0f) Kill(e, 0, 0, HitCtx.None);
            }

            // 서리불꽃 갑옷: every second flip nearby marks ember ↔ frost, so it keeps shattering by itself.
            if (Time < Hero.AuraUntil && TickCount % 10 == 0)
            {
                foreach (var e in InRadius(Hero.Pos, Hero.AuraRadius))
                {
                    // 성화 오라 pulses its own element; 서리불꽃 갑옷 flips marks ember ↔ frost.
                    var elem = Hero.AuraPulse ? Hero.AuraElem : e.Mark == Element.Ember ? Element.Frost : Element.Ember;
                    ApplyHit(e, RollHeroDamage(Hero.AuraPower, elem, SkillMods.Neutral, e), elem, false, 0, 0, HitCtx.None);
                }
                if (Hero.AuraHeal > 0f) Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + Hero.Stats.MaxHp * Hero.AuraHeal);
            }

            // 잿불 오라 elites burn whoever stands next to them.
            if (TickCount % 10 == 0)
                foreach (var e in _enemies)
                    if (e.Alive && e.Aggro && (e.Affixes & EliteAffix.EmberAura) != 0 && Vec2.DistSq(e.Pos, Hero.Pos) <= 4f)
                    {
                        float burn = IncomingDamage(e.Stats.Damage * 0.3f);
                        Hero.Stats.Hp -= burn;
                        Events.Add(new SimEvent { Type = SimEventType.Hit, SourceId = e.Id, TargetId = Hero.Id, Pos = Hero.Pos, Value = burn, Elem = Element.Ember });
                    }

            // 바람사냥꾼 집중 / 별점술사 시간 refill over time.
            if (IsWindhunter) Hero.Heat = Math.Min(HeatMax, Hero.Heat + 6f * Dt);
            if (IsStarseer) Hero.Heat = Math.Min(HeatMax, Hero.Heat + 2f * Dt);

            // 시간 정지 ends: everything stored lands at once.
            bool released = false;
            foreach (var e in _enemies.ToArray())
            {
                if (!e.Alive || e.Stored <= 0f || Time < e.FrozenUntil) continue;
                float dmg = e.Stored * Math.Max(1f, e.StoredMul);
                e.Stored = 0f;
                released = true;
                e.Stats.Hp -= dmg;
                Events.Add(new SimEvent { Type = SimEventType.Hit, SourceId = Hero.Id, TargetId = e.Id, Pos = e.Pos, Value = dmg, Res = Resonance.None });
                if (e.Stats.Hp <= 0f) Kill(e, 0, 0, HitCtx.None);
            }
            if (released) Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = 0, Pos = Hero.Pos, From = Hero.Pos, SkillId = "time_stop_release", Value = 5f });

            // 천둥 폭풍: a bolt every half second on someone within six cells.
            if (Time < Hero.ThunderUntil && TickCount % 5 == 0)
            {
                var near = InRadius(Hero.Pos, 6f);
                if (near.Count > 0)
                {
                    var t = near[_combatRng.Range(0, near.Count)];
                    FireHeroBlast(t.Pos, 1.2f, Hero.ThunderPower, Element.Shock, "thunderstorm_bolt");
                }
            }

            // 무너지지 않는 서약: when the vow ends, everything it swallowed comes back out.
            if (Hero.VowActive && Time >= Hero.VowUntil)
            {
                Hero.VowActive = false;
                float power = Math.Min(8f, 1f + Hero.VowStored / Math.Max(1f, Hero.Stats.Damage));
                FireHeroBlast(Hero.Pos, Hero.VowRadius, power, Element.Shock, "unbroken_vow_burst");
            }
        }

        void FireBlast(Blast b)
        {
            Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = 0, Pos = b.Pos, From = Hero.Pos, SkillId = b.Fx, Elem = b.Elem, Value = b.Radius });
            var ctx = new HitCtx { Slot = b.Slot, ResBonus = b.Mods.ResBonus, GaugeBonus = b.Mods.GaugeBonus, Leech = b.Mods.Leech, Ripple = b.Mods.Ripple, HopBonus = b.Mods.HopBonus, Direct = true, SkillId = b.Fx, From = b.Pos };
            if (b.Pull > 0f) PullToward(b.Pos, b.Radius * 2f, b.Pull, null); // 바람길: gather the pack onto the line
            if (b.Trail > 0f && _grounds.Count < 40) AddGround(b.Pos, b.Radius, b.Power * 0.2f, b.Elem, b.Trail); // 혜성 꼬리 burns behind it
            foreach (var e in InRadius(b.Pos, b.Radius))
                ApplyHit(e, RollHeroDamage(b.Power, b.Elem, b.Mods, e), b.Elem, false, 0, 0, ctx);
            // 재의 메아리: the burst restarts the dead enemy's death chain.
            if (b.Remark != Element.None) QueuePop(b.Pos, b.Remark, Hero.Stats.Damage * 1.2f * (1f + Hero.Derived.DamagePct), 0, 0, 0, b.Mods.HopBonus, b.Mods.Ripple);
        }

        void AddGround(Vec2 pos, float radius, float power, Element elem, float seconds, float heal = 0f, float stun = 0f,
            bool ignite = false, float pull = 0f, float endPower = 0f, string fx = null)
        {
            _grounds.Add(new Ground { Pos = pos, Radius = radius, Power = power, Elem = elem, Heal = heal, Stun = stun, Ignite = ignite, Pull = pull, EndPower = endPower, Fx = fx,
                UntilTick = TickCount + (int)Math.Round(seconds / Dt) });
            Events.Add(new SimEvent { Type = SimEventType.Ground, Pos = pos, Elem = elem, Value = radius, Depth = (int)Math.Round(seconds / Dt), SkillId = fx });
        }

        /// <summary>Enemy hit the hero: heat rises, thorn mail answers.</summary>
        bool IsOathknight => Hero.ClassId == "oathknight";
        bool IsWindhunter => Hero.ClassId == "windhunter";
        bool IsStarseer => Hero.ClassId == "starseer";

        /// <summary>
        /// Everything that stands between a blow and the hero's life: armour, guard, sanctuary, martyr chains,
        /// the barrier, and the vow (which never lets life fall below 1 and keeps what it swallowed).
        /// </summary>
        float IncomingDamage(float raw)
        {
            float d = raw * (1f - Hero.Derived.DamageReduction) * StanceTakenMul;
            if (Time < Hero.GuardUntil) d *= 0.7f;
            if (Time < Hero.SanctuaryUntil) d *= 0.4f;
            if (Time < Hero.MartyrUntil && Hero.MartyrPct > 0f)
            {
                var bound = _enemies.FindAll(e => e.Alive && e.Link == Hero.MartyrLink);
                if (bound.Count > 0)
                {
                    float share = d * Hero.MartyrPct;
                    d -= share;
                    foreach (var e in bound) Secondary(e, share / bound.Count * 2f, Hero.Pos, Resonance.None); // they suffer double
                }
            }
            if (Hero.Shield > 0f && Time < Hero.ShieldUntil)
            {
                float absorbed = Math.Min(Hero.Shield, d);
                Hero.Shield -= absorbed;
                d -= absorbed;
                if (Hero.Shield <= 0f)
                {
                    // 맹세의 방벽 breaks into a holy blast.
                    FireHeroBlast(Hero.Pos, Hero.ShieldRadius, Hero.ShieldPower * (Hero.ShieldDouble ? 2f : 1f), Hero.ShieldElem, Hero.ShieldFx);
                }
            }
            if (Hero.VowActive && Time < Hero.VowUntil)
            {
                Hero.VowStored += d;
                if (Hero.Stats.Hp - d < 1f) d = Math.Max(0f, Hero.Stats.Hp - 1f);
            }
            return d;
        }

        void FireHeroBlast(Vec2 at, float radius, float power, Element elem, string fx) =>
            FireBlast(new Blast { AtTick = TickCount, Pos = at, Radius = radius, Power = power, Elem = elem, Slot = -1, Mods = SkillMods.Neutral, Fx = fx });

        void OnHeroHit(Enemy attacker, float taken)
        {
            Hero.Heat = Math.Min(HeatMax, Hero.Heat + (IsOathknight ? 8f : 6f));
            // 은총의 메아리: each blow taken while armed answers with a holy burst.
            if (Hero.GraceCharges > 0)
            {
                Hero.GraceCharges--;
                FireHeroBlast(Hero.Pos, Hero.GraceRadius, Hero.GracePower, Element.Shock, "grace_echo_burst");
            }
            // 성역: blows taken pulse back (at most three times a second).
            if (Time < Hero.SanctuaryUntil && Time >= Hero.NextSanctuaryPulse)
            {
                Hero.NextSanctuaryPulse = Time + 0.3;
                FireHeroBlast(Hero.Pos, 2f, Hero.SanctuaryPower, Element.Shock, "grace_echo_burst");
            }
            if (Time >= Hero.ThornUntil) return;
            float dmg = Math.Max(taken * 0.6f, Hero.Stats.Damage * 0.6f) * (1f + Hero.Derived.DamagePct);
            if (Hero.ThornRadius > 0f)
                foreach (var e in InRadius(Hero.Pos, Hero.ThornRadius)) ApplyHit(e, dmg, Element.Ember, false, 0, 0, HitCtx.None);
            else ApplyHit(attacker, dmg, Element.Ember, false, 0, 0, HitCtx.None);
        }

        // ---------- hits, resonance, death chain ----------

        public void ApplyHit(Enemy e, float dmg, Element elem, bool fromChain = false, int cascade = 0, int depth = 0) =>
            ApplyHit(e, dmg, elem, fromChain, cascade, depth, HitCtx.None);

        public void ApplyHit(Enemy e, float dmg, Element elem, bool fromChain, int cascade, int depth, HitCtx ctx)
        {
            if (!e.Alive) return;
            // 등불 점화 and 태세 scale everything the hero deals — skills, resonances, chains — so they live here.
            if (!ctx.Prescaled) dmg *= (Time < Hero.IgniteUntil ? 1f + IgniteDamage : 1f) * StanceDamageMul * MedalDamageMul(e);
            e.Aggro = true;
            if (elem != Element.None) e.LastHitElem = elem;
            dmg *= FrostOnFrozen(e, elem);
            var markBefore = e.Mark;
            if (elem != Element.None)
            {
                var res = Elements.Resolve(markBefore, elem);
                if (res != Resonance.None)
                {
                    bool forward = Elements.IsForward(res);
                    int tier = forward ? Elements.MasteryTier(Hero.ResonanceCounts[(int)res]) : 0;
                    float bonus = Hero.Derived.ResonancePct + ctx.ResBonus + Elements.MasteryBonus(tier);
                    if (forward && ctx.Slot >= 0 && e.MarkSlot == (ctx.Slot + 3) % 4) bonus += 0.2f; // order bonus (§5)
                    if (forward && Hero.Derived.Runewords.Contains("rw_cycle")) bonus += 0.25f; // 룬어 순환
                    float k = (1f + bonus) * (fromChain ? ChainResonanceFactor : 1f);
                    float extra = 0f;
                    switch (res)
                    {
                        case Resonance.Shatter:
                            extra = dmg * 0.5f * k;
                            foreach (var o in InRadius(e.Pos, 1.2f))
                            {
                                if (o == e) continue;
                                if (tier >= 3 && o.Mark == Element.None) o.Mark = Element.Frost;
                                Secondary(o, dmg * 0.5f * k, e.Pos, Resonance.Shatter);
                            }
                            break;
                        case Resonance.Overload:
                        {
                            e.StunUntil = Math.Max(e.StunUntil, Time + 1.0 + (Hero.Derived.Runewords.Contains("rw_icecage") ? 0.5 : 0.0)); // 룬어 얼음 감옥
                            var struck = new List<Enemy> { e };
                            for (int i = 0; i < (tier >= 3 ? 4 : 2); i++)
                            {
                                var t = NearestExcept(e.Pos, 3f, struck);
                                if (t == null) break;
                                struck.Add(t);
                                Secondary(t, dmg * 0.4f * k, e.Pos, Resonance.Overload);
                            }
                            break;
                        }
                        case Resonance.Plague:
                        {
                            float total = dmg * 0.6f * k;
                            foreach (var o in InRadius(e.Pos, (tier >= 3 ? 3f : 2f) + (Hero.Derived.Runewords.Contains("rw_plaguebreath") ? 1f : 0f)))
                            {
                                if (o != e) o.Mark = Element.Rot;
                                o.DotPerTick = Math.Max(o.DotPerTick * (o.DotTicks > 0 ? 1f : 0f), total / 30f);
                                o.DotTicks = 30;
                            }
                            break;
                        }
                        case Resonance.Devour:
                            extra = dmg * 0.2f * k;
                            Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + Hero.Stats.MaxHp * (tier >= 3 ? 0.03f : 0.02f));
                            AddGauge((tier >= 3 ? 2f : 1f) * (1f + ctx.GaugeBonus));
                            break;
                        case Resonance.Collapse:
                            extra = dmg * 0.5f * k;
                            foreach (var o in PullToward(e.Pos, 2.5f, Hero.Derived.Runewords.Contains("rw_voidgate") ? 1.2f : 0.6f, e))
                                if (tier >= 3) o.StunUntil = Math.Max(o.StunUntil, Time + 0.5);
                            break;
                        case Resonance.Spark:
                            extra = dmg * 0.3f * k;
                            break;
                        case Resonance.Stack:
                            e.Stacks = Math.Min(5, e.Stacks + 1);
                            dmg *= 1f + 0.1f * e.Stacks;
                            break;
                    }
                    dmg += extra;
                    if (forward)
                    {
                        int before = Hero.ResonanceCounts[(int)res]++;
                        if (Elements.MasteryTier(before + 1) > Elements.MasteryTier(before))
                            Events.Add(new SimEvent { Type = SimEventType.Mastery, Pos = Hero.Pos, Res = res, Value = Elements.MasteryTier(before + 1) });
                        Hero.LastResonanceMark = markBefore;
                        AddGauge(1f + ctx.GaugeBonus);
                    }
                    else if (res == Resonance.Spark) AddGauge(0.3f * (1f + ctx.GaugeBonus));
                    Events.Add(new SimEvent { Type = SimEventType.Resonance, TargetId = e.Id, Pos = e.Pos, Res = res, Elem = elem, Value = extra, FromChain = fromChain });
                    // Essence "shatter_chain": a shatter blows up on the spot like a death-chain link.
                    if (res == Resonance.Shatter && Hero.Has("shatter_chain"))
                        QueuePop(e.Pos, Element.Ember, LiveChainPower, 0, 0);
                }
                if (res != Resonance.Stack) e.Stacks = 0;
                // A forward resonance consumes the mark; a spark or a fresh hit leaves the new element's mark.
                if (Elements.IsForward(res)) { e.Mark = Element.None; e.MarkSlot = -1; }
                else { e.Mark = elem; e.MarkSlot = ctx.Slot; }
            }

            if (Time < e.HuntedUntil) dmg *= 1f + e.HuntedPct; // 사냥꾼의 표식
            if (e.Def.Armor > 0f) dmg *= 1f - e.Def.Armor;  // 뼈 방패병
            if (e.AffixShield > 0f)
            {
                // 보호막 elite: the barrier takes it first.
                float soak = Math.Min(e.AffixShield, dmg);
                e.AffixShield -= soak;
                dmg -= soak;
            }
            if (Time < e.FrozenUntil)
            {
                // 시간 정지: the blow is remembered, not felt — yet.
                e.Stored += dmg;
                Events.Add(new SimEvent { Type = SimEventType.Hit, SourceId = Hero.Id, TargetId = e.Id, Pos = e.Pos, From = ctx.From, Res = ctx.Res, Value = 0f, Elem = elem, FromChain = fromChain, Depth = depth, SkillId = ctx.SkillId });
                return;
            }
            if (ctx.Leech > 0f) Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + dmg * ctx.Leech);
            if (ctx.Direct) HealOnMeleeHit();
            e.Stats.Hp -= dmg;
            if (!fromChain && e.Stats.Hp > 0f) ElementStatus(e, dmg, elem);
            Events.Add(new SimEvent { Type = SimEventType.Hit, SourceId = Hero.Id, TargetId = e.Id, Pos = e.Pos, From = ctx.From, Res = ctx.Res, Value = dmg, Elem = elem, FromChain = fromChain, Depth = depth, SkillId = ctx.SkillId });

            // 심연 사슬: linked enemies share 30% of a direct hit.
            if (e.Link != 0 && ctx.Direct)
                foreach (var o in _enemies.ToArray())
                    if (o != e && o.Alive && o.Link == e.Link) Secondary(o, dmg * 0.3f, e.Pos, Resonance.None);

            if (e.Stats.Hp <= 0f) Kill(e, cascade, depth, ctx);
            // 녹은 심장: a direct hit on a marked enemy starts its death chain without the kill.
            else if (ctx.Direct && Time < Hero.MoltenUntil && markBefore != Element.None)
                QueuePop(e.Pos, markBefore, LiveChainPower * 0.5f, 0, 0, e.Id, ctx.HopBonus, ctx.Ripple);
        }

        /// <summary>A splash from a resonance: no element (so no resonance of its own), but it can kill and chain.</summary>
        void Secondary(Enemy e, float dmg, Vec2 from, Resonance res)
        {
            var ctx = HitCtx.None;
            ctx.From = from;
            ctx.Res = res;
            ctx.Prescaled = true; // cut from a hit that was already scaled
            ApplyHit(e, dmg, Element.None, false, 0, 0, ctx);
        }

        void AddGauge(float amount, bool force = false)
        {
            if (_inGrand && !force) return;
            Hero.Gauge = Math.Min(GaugeMax * 1.5f, Hero.Gauge + amount * (force ? 1f : Hero.GaugeRate));
        }

        /// <summary>
        /// Melee life on hit: a direct blow heals 0.4% of max life, up to 3% a second. Ranged heroes kite; a melee hero
        /// stands in the pack and against an act boss, and without this died in ~15 s at the act IV boss (6 h, 8 tries).
        /// </summary>
        public const float MeleeLifeOnHit = 0.004f, MeleeLifeOnHitPerSecond = 0.03f;
        double _lifeOnHitTime = -1;
        float _lifeOnHitLeft;

        void HealOnMeleeHit()
        {
            if (!HeroStats.IsMeleeClass(Hero.ClassId)) return;
            if (Time != _lifeOnHitTime) { _lifeOnHitTime = Time; _lifeOnHitLeft = MeleeLifeOnHitPerSecond * (float)Dt; }
            float heal = Math.Min(_lifeOnHitLeft, MeleeLifeOnHit) * Hero.Stats.MaxHp;
            if (heal <= 0f) return;
            _lifeOnHitLeft -= heal / Hero.Stats.MaxHp;
            Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + heal);
        }

        /// <summary>§2.3 대공명: the last resonance's element detonates on every enemy around the hero at once.</summary>
        void TryGrandResonance()
        {
            if (Hero.Gauge < GaugeMax) return;
            _inGrand = true;
            Hero.Gauge = 0f;
            var mark = Hero.LastResonanceMark == Element.None ? Element.Ember : Hero.LastResonanceMark;
            var hit = Elements.Next(mark);
            Events.Add(new SimEvent { Type = SimEventType.GrandResonance, SourceId = Hero.Id, Pos = Hero.Pos, Elem = mark, Value = GrandRadius });
            foreach (var e in InRadius(Hero.Pos, GrandRadius))
            {
                e.Mark = mark;
                e.MarkSlot = -1;
                ApplyHit(e, RollHeroDamage(2f, hit, SkillMods.Neutral, e), hit, false, 0, 0, HitCtx.None);
            }
            // 이중 대공명: the wave rolls on to the next element at 60%.
            if (Talents.Has(Hero, Talents.Synergy, 2))
            {
                var hit2 = Elements.Next(hit);
                Events.Add(new SimEvent { Type = SimEventType.GrandResonance, SourceId = Hero.Id, Pos = Hero.Pos, Elem = hit, Value = GrandRadius });
                foreach (var e in InRadius(Hero.Pos, GrandRadius))
                {
                    e.Mark = hit;
                    e.MarkSlot = -1;
                    ApplyHit(e, RollHeroDamage(1.2f, hit2, SkillMods.Neutral, e), hit2, false, 0, 0, HitCtx.None);
                }
            }
            _inGrand = false;
        }

        void Kill(Enemy e, int cascade, int depth, HitCtx ctx)
        {
            e.Alive = false;
            e.Stats.Hp = 0f;
            e.DotTicks = 0;
            e.BurnTicks = 0;
            Shatter(e);
            OnEliteDeath(e);
            Events.Add(new SimEvent { Type = SimEventType.Death, TargetId = e.Id, Pos = e.Pos, Elem = e.Mark, Depth = depth, KillElem = e.LastHitElem });
            // GDD §11.2 monster XP by area level (EnemyDef.Xp is relative to a standard 5).
            // Dense packs kill ≈55/min instead of GDD §11.2's 18/min, so a common kill is worth a third (KillXpScale).
            // 재등반 배속 (GDD §11.5): on a beaten act after rebirth a kill counts RepeatMul times.
            if (!e.Summoned)
            {
                GainXp((int)Math.Round(e.Def.Xp / 5.0 * XpCurve.MonsterXp(AreaLevel) * (e.Boss ? 10 : e.Elite ? 3 : KillXpScale) * RepeatMul));
                Kills++;
                CountKill(e);
            }
            UnlockCampIfActBoss(e);
            NoteActCleared(e);
            if (e.Summoned) { }
            else if (e.Champion) for (int i = 0; i < 3; i++) DropItem(e.Pos, Rarity.Magic);
            else if (e.Boss && IsActBossFloor)
            {
                // The act boss is the farming highlight: a shower of eight, one legendary for sure and two rare or better.
                for (int i = 0; i < DropTable.ActBossDrops; i++) DropItem(e.Pos, i == 0 ? Rarity.Legendary : i <= 2 ? Rarity.Rare : Rarity.Common);
            }
            else if (e.Boss) for (int i = 0; i < DropTable.BossDrops; i++) DropItem(e.Pos, Rarity.Common);
            else if (e.Elite) for (int i = 0; i < DropTable.EliteDrops; i++) DropItem(e.Pos, Rarity.Common);
            else for (int k = 0, n = RepeatMul; k < n; k++) if (_lootRng.NextFloat() < DropTable.NormalChance) DropItem(e.Pos, Rarity.Common);
            // GDD §8.7 runes: commons 1.5%, elites 15%, bosses two.
            int runes = e.Summoned ? 0 : e.Boss ? 2 : e.Elite ? (_lootRng.NextFloat() < 0.15f ? 1 : 0) : (_lootRng.NextFloat() < 0.015f ? 1 : 0);
            for (int i = 0; i < runes; i++) GainRune(Runes.Roll(_lootRng, AreaLevel), e.Pos);
            if (Hero.Derived.Runewords.Contains("rw_bloodpact")) Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + Hero.Stats.MaxHp * 0.005f); // 룬어 피의 계약
            if (Talents.Has(Hero, Talents.Defense, 1)) Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + Hero.Stats.MaxHp * 0.01f); // 피의 맹세
            if (e.Mark != Element.None && Hero.Has("ash_feast"))
                Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + Hero.Stats.MaxHp * 0.02f);

            if (e.Mark == Element.None) return;
            // 재의 메아리: the spot erupts again a moment later and restarts the chain.
            if (Hero.CinderCharges > 0 && Hero.CinderSkill != null)
            {
                Hero.CinderCharges--;
                var cs = Hero.CinderSkill;
                _blasts.Add(new Blast
                {
                    AtTick = TickCount + Math.Max(1, (int)Math.Round(cs.Delay / Dt)), Pos = e.Pos, Radius = cs.Radius * Hero.CinderMods.RadiusMul,
                    Power = cs.Power, Elem = Element.Ember, Remark = e.Mark, Slot = -1, Mods = Hero.CinderMods, Fx = "cinder_echo_burst",
                });
            }
            // 독사의 둥지: the corpse turns into a rot cloud.
            if (Hero.ViperCharges > 0)
            {
                Hero.ViperCharges--;
                AddGround(e.Pos, Hero.ViperRadius, Hero.ViperPower, Element.Rot, 2f, fx: "viper");
            }
            if (depth >= EffectiveChainCapWith(ctx.HopBonus)) return;
            float ripple = ctx.Ripple + (Time < e.HuntedUntil ? 1f : 0f); // a hunted kill's chain reaches twice as far
            QueuePop(e.Pos, e.Mark, ChainPower(e), depth, cascade, e.Id, ctx.HopBonus, ripple);
        }

        /// <summary>GDD §5.4 elite prefixes that act on death.</summary>
        void OnEliteDeath(Enemy e)
        {
            if (e.Affixes == EliteAffix.None) return;
            if ((e.Affixes & EliteAffix.Multiplying) != 0 && !e.Copy)
                for (int i = 0; i < 2; i++)
                {
                    var offset = new Vec2(i == 0 ? 0.6f : -0.6f, i == 0 ? -0.3f : 0.3f);
                    var at = Floor.IsWalkable(e.Pos + offset) ? e.Pos + offset : e.Pos;
                    var c = SpawnEnemy(e.Def, at, e.Stats.MaxHp * 0.3f / Math.Max(1f, e.Def.MaxHp), e.Stats.Damage * 0.5f / Math.Max(0.01f, e.Def.Damage), true);
                    c.Copy = true;
                    c.Elite = true;
                    c.Aggro = true;
                    c.HomeRoom = e.HomeRoom;
                    c.Affixes = e.Affixes & ~EliteAffix.Multiplying;
                }
            if ((e.Affixes & EliteAffix.FrostBurst) != 0)
            {
                Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = 0, Pos = e.Pos, From = e.Pos, SkillId = "elite_frost_burst", Elem = Element.Frost, Value = 2.5f });
                if (Vec2.DistSq(Hero.Pos, e.Pos) <= 2.5f * 2.5f) Hero.StunnedUntil = Math.Max(Hero.StunnedUntil, Time + 0.5);
            }
        }

        public void GainRune(Rune r, Vec2 at)
        {
            Bag.RuneCounts[(int)r]++;
            Events.Add(new SimEvent { Type = SimEventType.RuneDrop, Pos = at, Value = (int)r });
            SocketRunes();
        }

        /// <summary>Idle auto-socketing: complete any runeword the bag allows in an equipped item's empty sockets.</summary>
        public void SocketRunes()
        {
            bool any = false;
            foreach (var it in Hero.Gear.Spots)
                if (Runes.AutoSocket(it, Bag.RuneCounts))
                {
                    any = true;
                    if (it.Runeword != null) Hero.RunewordsFound.Add(it.Runeword.Id);
                    Events.Add(new SimEvent { Type = SimEventType.Runeword, Pos = Hero.Pos, SkillId = it.Runeword?.Name, Item = it });
                }
            if (any) HeroStats.Recompute(Hero);
        }

        /// <summary>
        /// By hand (Diablo-style): one rune from the hoard into the next empty socket of <paramref name="item"/> (worn or in
        /// the bag). The rune adds its stat; filling a runeword's recipe in order makes the runeword. False if it cannot go in.
        /// </summary>
        public bool SocketRune(Item item, Rune r)
        {
            if (item == null || r == Rune.None || Bag.RuneCounts[(int)r] <= 0 || item.RuneSlots.Count >= item.Sockets) return false;
            Bag.RuneCounts[(int)r]--;
            item.RuneSlots.Add((int)r);
            if (item.Runeword != null)
            {
                Hero.RunewordsFound.Add(item.Runeword.Id);
                Events.Add(new SimEvent { Type = SimEventType.Runeword, Pos = Hero.Pos, SkillId = item.Runeword.Name, Item = item });
            }
            HeroStats.Recompute(Hero);
            return true;
        }

        public int EffectiveChainCap => EffectiveChainCapWith(0);

        int EffectiveChainCapWith(int hopBonus)
        {
            int bonus = (Hero.Has("long_chain") ? 2 : 0) + (Hero.Derived.Runewords.Contains("rw_lanternkeeper") ? 1 : 0) + hopBonus + (Time < Hero.UndyingUntil ? 3 : 0) + (Talents.Has(Hero, Talents.Synergy, 1) ? 1 : 0); // 긴 사슬
            return ChainCap + Math.Min(ChainBonusCap, bonus);
        }

        /// <summary>A pop is sized by the victim (half its max life) so a pack of equals can carry the chain (PoE detonate-dead idea).</summary>
        float ChainPower(Enemy e) => Math.Max(Hero.Stats.Damage * 1.2f, e.Stats.MaxHp * 0.5f) * (1f + Hero.Derived.DamagePct);

        /// <summary>
        /// A chain started on an enemy that is still alive (전격 덫, 파쇄 연쇄, 녹은 심장): the hero's strength only. Reading the
        /// victim's max life there let a boss's own pops take a quarter of its life each.
        /// </summary>
        float LiveChainPower => Hero.Stats.Damage * 1.2f * (1f + Hero.Derived.DamagePct);

        void QueuePop(Vec2 pos, Element mark, float power, int depth, int cascade, int originId = 0, int hopBonus = 0, float ripple = 0f)
        {
            if (cascade == 0)
            {
                cascade = _nextCascade++;
                _cascadeHits[cascade] = originId != 0 ? new HashSet<int> { originId } : new HashSet<int>();
            }
            // §3 hop decay (꺼지지 않는 불 removes it).
            if (Time >= Hero.UndyingUntil) power *= (float)Math.Pow(ChainDecay, depth);
            _pending.Add(new PendingPop { Pos = pos, Mark = mark, Power = power, Depth = depth, Cascade = cascade, AtTick = TickCount + 1, HopBonus = hopBonus, Ripple = ripple });
        }

        void ProcessChain()
        {
            if (_pending.Count == 0) return;
            var due = _pending.FindAll(p => p.AtTick <= TickCount);
            _pending.RemoveAll(p => p.AtTick <= TickCount);
            int budget = ChainBudgetPerTick;
            foreach (var pop in due)
            {
                if (budget-- <= 0)
                {
                    // §3 per-tick budget: the rest go off next tick (same damage, calmer screen).
                    var later = pop;
                    later.AtTick = TickCount + 1;
                    _pending.Add(later);
                    continue;
                }
                float radius = ChainRadius * (1f + Hero.Derived.ChainRadiusPct) * (1f + pop.Ripple);
                float r2 = radius * radius;
                Events.Add(new SimEvent { Type = SimEventType.ChainPop, Pos = pop.Pos, Elem = pop.Mark, Depth = pop.Depth, Value = radius });
                AddGauge(0.2f);
                if (!_cascadeHits.TryGetValue(pop.Cascade, out var hit)) _cascadeHits[pop.Cascade] = hit = new HashSet<int>();
                var ctx = new HitCtx { Slot = -1, HopBonus = pop.HopBonus, Ripple = pop.Ripple, From = pop.Pos };
                foreach (var e in _enemies.ToArray())
                {
                    if (!e.Alive || hit.Contains(e.Id) || Vec2.DistSq(e.Pos, pop.Pos) > r2 || !InSight(pop.Pos, e.Pos)) continue;
                    hit.Add(e.Id);
                    e.Mark = pop.Mark; // mark transfers before the blast, so a kill keeps the chain going
                    ApplyHit(e, pop.Power, Element.None, true, pop.Cascade, pop.Depth + 1, ctx);
                }
            }
            // Forget cascades with nothing left to pop.
            foreach (var pop in due)
                if (!_pending.Exists(p => p.Cascade == pop.Cascade)) _cascadeHits.Remove(pop.Cascade);
        }

        // ---------- geometry helpers ----------

        /// <summary>Living enemies within <paramref name="r"/> that the blast can reach: walls stop area effects,
        /// so a spell or a chain never wakes the pack in the next room through stone.</summary>
        List<Enemy> InRadius(Vec2 c, float r)
        {
            var list = new List<Enemy>();
            float r2 = r * r;
            foreach (var e in _enemies) if (e.Alive && Vec2.DistSq(e.Pos, c) <= r2 && InSight(c, e.Pos)) list.Add(e);
            return list;
        }

        bool InSight(Vec2 a, Vec2 b)
        {
            float len = Vec2.Dist(a, b);
            int steps = Math.Max(1, (int)Math.Ceiling(len / 0.25f));
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                if (!Floor.IsWalkable(new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t))) return false;
            }
            return true;
        }

        Enemy Nearest(Vec2 c, float r, Enemy except) => NearestExcept(c, r, except == null ? null : new List<Enemy> { except });

        Enemy NearestExcept(Vec2 c, float r, List<Enemy> except)
        {
            Enemy best = null;
            float bd = r * r;
            foreach (var e in _enemies)
            {
                if (!e.Alive || (except != null && except.Contains(e))) continue;
                float d = Vec2.DistSq(e.Pos, c);
                if (d <= bd && !InSight(c, e.Pos)) continue;
                if (d <= bd) { bd = d; best = e; }
            }
            return best;
        }

        /// <summary>Drag enemies within <paramref name="r"/> toward <paramref name="c"/> (bosses only a third as far).</summary>
        List<Enemy> PullToward(Vec2 c, float r, float dist, Enemy except)
        {
            var moved = InRadius(c, r);
            moved.Remove(except);
            foreach (var e in moved)
            {
                var d = c - e.Pos;
                float len = d.Length;
                if (len < 0.55f) continue;
                float step = Math.Min(dist * (e.Boss ? 0.33f : 1f), len - 0.5f);
                e.Pos = Slide(e.Pos, e.Pos + d * (step / len));
            }
            return moved;
        }

        /// <summary>The enemy with the most neighbours within 2 cells, among those within <paramref name="range"/> of the hero.</summary>
        Vec2? DensestPoint(float range)
        {
            Enemy best = null;
            int bestN = -1;
            float r2 = range * range;
            foreach (var e in _enemies)
            {
                if (!e.Alive || Vec2.DistSq(e.Pos, Hero.Pos) > r2) continue;
                int n = 0;
                foreach (var o in _enemies) if (o.Alive && Vec2.DistSq(o.Pos, e.Pos) <= 4f) n++;
                if (n > bestN) { bestN = n; best = e; }
            }
            return best?.Pos;
        }

        static Vec2 Rotate(Vec2 v, float rad)
        {
            float c = (float)Math.Cos(rad), s = (float)Math.Sin(rad);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }
}
