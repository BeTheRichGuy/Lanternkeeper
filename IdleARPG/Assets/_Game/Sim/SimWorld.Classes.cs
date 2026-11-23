using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// Signature skills (희승 2026-11-21: 익숙한 핵앤슬래시 스킬셋): 휠윈드 (spin while walking through the pack), 속사 (a
    /// stream of arrows), 갈래 화살 (an arrow that splits on hit), and the mage's elements — fire burns, frost chills
    /// into a freeze (frozen enemies take more frost and shatter), lightning stuns briefly and arcs to a neighbour.
    /// </summary>
    public sealed partial class SimWorld
    {
        Cast _whirl;

        /// <summary>Mage: a fire hit burns for this share of itself over 3 s (stacking, capped).</summary>
        public const float BurnShare = 0.35f;
        /// <summary>Mage: frost hits to freeze, freeze length, extra frost damage on the frozen.</summary>
        public const int ChillToFreeze = 3;
        public const double FreezeSeconds = 1.5, ShockStunSeconds = 0.3, ShockEvery = 0.6;
        public const float FrozenFrostBonus = 0.35f, ShockArcShare = 0.35f, ShatterPower = 0.8f;

        bool Whirling => _whirl != null && Time < Hero.WhirlUntil;

        void StartWhirl(Cast c)
        {
            _whirl = c;
            Hero.WhirlUntil = Time + c.Skill.Channel;
        }

        /// <summary>휠윈드: every 0.3 s the blades cut everything around the hero.</summary>
        void TickWhirl()
        {
            if (!Whirling || TickCount % 3 != 0) return;
            var c = _whirl;
            Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = 0, Pos = Hero.Pos, From = Hero.Pos, SkillId = c.Skill.Id, Elem = c.Elem, Value = c.Radius });
            if (c.Skill.Pull > 0f) PullToward(Hero.Pos, c.Radius * 1.5f, c.Skill.Pull * 0.3f, null);
            foreach (var e in InRadius(Hero.Pos, c.Radius)) Strike(c, e, 1f);
        }

        /// <summary>While spinning the hero walks on into the pack instead of stopping to swing.</summary>
        bool WhirlThink()
        {
            if (!Whirling) return false;
            var target = PickTarget();
            // In among them (a cell off): stand and spin rather than chase every step the target takes.
            if (target != null) { if (Vec2.Dist(Hero.Pos, target.Pos) > 1f) MoveHeroToward(target.Pos); }
            else
            {
                var room = NextRoomToExplore();
                if (room != null) MoveHeroToward(CenterOf(room));
            }
            return true;
        }

        /// <summary>속사: the rest of the volley, one arrow a tick (a dead target hands its arrow to the next).</summary>
        void QueueVolley(Cast c)
        {
            for (int k = 1; k <= c.Skill.Volley; k++)
                _copies.Add((TickCount + k, new Cast { Skill = c.Skill, Mods = c.Mods, Elem = c.Elem, Power = c.Power, Radius = c.Radius, Slot = c.Slot, Scorch = c.Scorch, Target = c.Target, Ctx = c.Ctx }));
        }

        /// <summary>갈래 화살: on hit the arrow splits toward the nearest others; at ForkDepth 2 the splits split again.</summary>
        void Fork(Cast c, Enemy from, int depth, List<Enemy> hit)
        {
            var src = from.Pos;
            var picked = new List<Enemy>();
            for (int k = 0; k < c.Skill.Forks; k++)
            {
                var o = NearestExcept(src, 4.5f, hit);
                if (o == null) break;
                hit.Add(o);
                picked.Add(o);
                Events.Add(new SimEvent { Type = SimEventType.Shot, SourceId = Hero.Id, TargetId = o.Id, From = src, Pos = o.Pos, SkillId = c.Skill.Id, Depth = 1 });
                var ctx = c.Ctx; ctx.From = src;
                Strike(c, o, 0.6f, ctx);
            }
            if (depth > 1) foreach (var o in picked) Fork(c, o, depth - 1, hit);
        }

        // ---------- mage elements ----------

        /// <summary>Frost on a frozen enemy lands harder (빙결 피해 극대화).</summary>
        float FrostOnFrozen(Enemy e, Element elem) => IsStarseer && elem == Element.Frost && Time < e.FreezeUntil ? 1f + FrozenFrostBonus : 1f;

        void ElementStatus(Enemy e, float dmg, Element elem)
        {
            if (!IsStarseer || !e.Alive || dmg <= 0f) return;
            switch (elem)
            {
                case Element.Ember:
                {
                    // 화상: stacks up, capped at 0.15 of the hero's damage a tick (1.5× damage a second).
                    float per = dmg * BurnShare / 30f;
                    e.BurnPerTick = Math.Min((e.BurnTicks > 0 ? e.BurnPerTick : 0f) + per, Hero.Stats.Damage * 0.15f);
                    e.BurnTicks = 30;
                    break;
                }
                case Element.Frost:
                    if (Time < e.FreezeUntil) break;
                    if (++e.Chill >= ChillToFreeze)
                    {
                        e.Chill = 0;
                        e.FreezeUntil = Time + (e.Boss ? FreezeSeconds / 3.0 : FreezeSeconds);
                        e.StunUntil = Math.Max(e.StunUntil, e.FreezeUntil);
                    }
                    break;
                case Element.Shock:
                {
                    if (Time < e.ShockReadyAt) break;
                    // 감전: a short stun, and the charge jumps to one neighbour.
                    e.ShockReadyAt = Time + ShockEvery;
                    e.StunUntil = Math.Max(e.StunUntil, Time + (e.Boss ? ShockStunSeconds / 3.0 : ShockStunSeconds));
                    var o = Nearest(e.Pos, 3f, e);
                    if (o != null)
                    {
                        Events.Add(new SimEvent { Type = SimEventType.Shot, SourceId = Hero.Id, TargetId = o.Id, From = e.Pos, Pos = o.Pos, SkillId = "shock_arc" });
                        var arc = HitCtx.None; arc.From = e.Pos; arc.Prescaled = true; arc.SkillId = "shock_arc";
                        ApplyHit(o, dmg * ShockArcShare, Element.Shock, true, 0, 0, arc);
                    }
                    break;
                }
            }
        }

        /// <summary>Burns tick every 0.1 s; once a second the burnt total shows as one number.</summary>
        void TickBurns()
        {
            bool show = TickCount % 10 == 0;
            foreach (var e in _enemies.ToArray())
            {
                if (!e.Alive || e.BurnTicks <= 0) continue;
                e.BurnTicks--;
                e.Stats.Hp -= e.BurnPerTick;
                if (show) Events.Add(new SimEvent { Type = SimEventType.Hit, SourceId = Hero.Id, TargetId = e.Id, Pos = e.Pos, Value = e.BurnPerTick * 10f, Elem = Element.Ember, FromChain = true, SkillId = "burn" });
                if (e.Stats.Hp <= 0f) Kill(e, 0, 0, HitCtx.None);
            }
        }

        /// <summary>연소: the burn left on the target goes off at once (×Combust) and catches the ones around it.</summary>
        void Combust(Cast c, Enemy e)
        {
            if (c.Skill.Combust <= 0f || e.BurnTicks <= 0) return;
            float left = e.BurnPerTick * e.BurnTicks;
            float per = e.BurnPerTick;
            e.BurnTicks = 0;
            var ctx = HitCtx.None; ctx.Prescaled = true; ctx.From = e.Pos; ctx.SkillId = c.Skill.Id;
            if (e.Alive) ApplyHit(e, left * c.Skill.Combust, Element.Ember, true, 0, 0, ctx);
            foreach (var o in InRadius(e.Pos, 2.2f))
            {
                if (o == e) continue;
                o.BurnPerTick = Math.Max(o.BurnTicks > 0 ? o.BurnPerTick : 0f, per);
                o.BurnTicks = 30;
            }
        }

        /// <summary>A frozen enemy that dies shatters, cutting the ones beside it.</summary>
        void Shatter(Enemy e)
        {
            if (!IsStarseer || Time >= e.FreezeUntil) return;
            e.FreezeUntil = 0;
            Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = 0, Pos = e.Pos, From = e.Pos, SkillId = "ice_shatter_burst", Elem = Element.Frost, Value = 1.6f });
            var ctx = HitCtx.None; ctx.From = e.Pos; ctx.SkillId = "ice_shatter_burst";
            foreach (var o in InRadius(e.Pos, 1.6f))
                if (o != e) ApplyHit(o, RollHeroDamage(ShatterPower, Element.Frost, SkillMods.Neutral, o), Element.Frost, true, 0, 0, ctx);
        }
    }
}
