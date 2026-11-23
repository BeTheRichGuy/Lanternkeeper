using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>Skill system v1: docs/specs/2026-11-19-skill-rune-synergy-design.md.</summary>
    public class SynergyTests
    {
        static SimWorld EmptyWorld()
        {
            var w = new SimWorld(11) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.UseOpenFloor();
            return w;
        }

        static EnemyDef Def(float hp) => new EnemyDef { Id = "test", MaxHp = hp, Damage = 0f, MoveSpeed = 0f, Xp = 0 };

        [Test]
        public void Collapse_pulls_the_pack_toward_the_detonation()
        {
            var w = EmptyWorld();
            // Pulls slide along walkable floor, so stand in the (always open) middle of the start room.
            var e = w.SpawnEnemy(Def(1000f), w.Hero.Pos);
            var o = w.SpawnEnemy(Def(1000f), w.Hero.Pos + new Vec2(1.4f, 0f));
            e.Mark = Element.Void;
            float before = Vec2.Dist(e.Pos, o.Pos);
            w.ApplyHit(e, 10f, Element.Ember);
            Assert.That(w.Events.Any(ev => ev.Type == SimEventType.Resonance && ev.Res == Resonance.Collapse), Is.True);
            Assert.That(Vec2.Dist(e.Pos, o.Pos), Is.LessThan(before - 0.3f));
        }

        [Test]
        public void Order_bonus_when_the_next_slot_detonates_the_previous_slots_mark()
        {
            var w = EmptyWorld();
            var a = w.SpawnEnemy(Def(1000f), new Vec2(50f, 50f));
            var b = w.SpawnEnemy(Def(1000f), new Vec2(60f, 50f));
            a.Mark = Element.Ember; a.MarkSlot = 0;
            b.Mark = Element.Ember; b.MarkSlot = 2;
            w.ApplyHit(a, 10f, Element.Frost, false, 0, 0, new SimWorld.HitCtx { Slot = 1 });
            w.ApplyHit(b, 10f, Element.Frost, false, 0, 0, new SimWorld.HitCtx { Slot = 1 });
            Assert.That(1000f - a.Stats.Hp, Is.EqualTo(16f).Within(1e-3f), "10 + 10×0.5×1.2");
            Assert.That(1000f - b.Stats.Hp, Is.EqualTo(15f).Within(1e-3f), "no bonus: slot 2 is not just before slot 1");
        }

        [Test]
        public void Full_gauge_sets_off_a_grand_resonance_that_hits_everything_near()
        {
            var w = EmptyWorld();
            var near = Enumerable.Range(0, 5).Select(i => w.SpawnEnemy(Def(100000f), w.Hero.Pos + new Vec2(1f + i * 0.5f, 0f))).ToList();
            var far = w.SpawnEnemy(Def(100000f), w.Hero.Pos + new Vec2(9f, 0f));
            w.Hero.Gauge = SimWorld.GaugeMax;
            w.Hero.LastResonanceMark = Element.Frost;
            w.Tick();
            Assert.That(w.Events.Any(ev => ev.Type == SimEventType.GrandResonance), Is.True);
            Assert.That(near.All(e => e.Stats.Hp < 100000f), Is.True);
            Assert.That(far.Stats.Hp, Is.EqualTo(100000f));
            Assert.That(w.Events.Count(ev => ev.Type == SimEventType.Resonance && ev.Res == Resonance.Overload), Is.EqualTo(near.Count));
            Assert.That(w.Hero.Gauge, Is.LessThan(SimWorld.GaugeMax));
        }

        [Test]
        public void Forward_resonance_fills_the_gauge_and_counts_toward_mastery()
        {
            var w = EmptyWorld();
            var e = w.SpawnEnemy(Def(100000f), new Vec2(50f, 50f));
            w.Hero.ResonanceCounts[(int)Resonance.Shatter] = 299;
            e.Mark = Element.Ember;
            w.ApplyHit(e, 10f, Element.Frost);
            Assert.That(w.Hero.Gauge, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(w.Events.Any(ev => ev.Type == SimEventType.Mastery && ev.Res == Resonance.Shatter && (int)ev.Value == 1), Is.True);
        }

        [Test]
        public void Later_chain_hops_hit_softer()
        {
            var w = EmptyWorld();
            // Weak links all die, so the chain runs its full length along the line.
            var line = Enumerable.Range(0, 8).Select(i => w.SpawnEnemy(Def(1f), new Vec2(50f + i * 1.5f, 50f))).ToList();
            line[0].Mark = Element.Ember;
            w.ApplyHit(line[0], 999f, Element.None);
            var byDepth = new System.Collections.Generic.Dictionary<int, float>();
            for (int i = 0; i < 12; i++)
            {
                w.Tick();
                foreach (var ev in w.Events.Where(ev => ev.Type == SimEventType.Hit && ev.FromChain)) byDepth[ev.Depth] = ev.Value;
            }
            Assert.That(byDepth.ContainsKey(1) && byDepth.ContainsKey(3), Is.True);
            Assert.That(byDepth[3] / byDepth[1], Is.EqualTo(SimWorld.ChainDecay * SimWorld.ChainDecay).Within(1e-3f));
        }

        [Test]
        public void At_most_the_budget_of_chain_pops_go_off_in_one_tick()
        {
            var w = EmptyWorld();
            // 40 marked enemies die at once far apart: 40 pops are due on the same tick.
            for (int i = 0; i < 40; i++)
            {
                var e = w.SpawnEnemy(Def(1f), new Vec2(10f + (i % 8) * 6f, 10f + (i / 8) * 6f));
                e.Mark = Element.Ember;
                w.ApplyHit(e, 999f, Element.None);
            }
            w.Tick();
            int first = w.Events.Count(ev => ev.Type == SimEventType.ChainPop);
            w.Tick();
            int second = w.Events.Count(ev => ev.Type == SimEventType.ChainPop);
            Assert.That(first, Is.EqualTo(SimWorld.ChainBudgetPerTick));
            Assert.That(second, Is.EqualTo(40 - SimWorld.ChainBudgetPerTick));
        }

        [Test]
        public void Ring_grows_with_level_and_keeps_the_preset_order()
        {
            var r1 = SkillBook.BuildRing(1, 1, null);
            Assert.That(r1.Slots.Count(s => s != null), Is.EqualTo(1));
            var r8 = SkillBook.BuildRing(1, 8, null);
            Assert.That(r8.Slots.Select(s => s.Id), Is.EqualTo(new[] { "ember_slash", "frost_brand", "storm_cleave", "ember_whirl" }));
            Assert.That(r8.Sigils.All(s => s == Sigil.None), Is.True, "glyphs open at Lv12");
            var r12 = SkillBook.BuildRing(1, 12, null);
            Assert.That(r12.Sigils[0], Is.EqualTo(Sigil.DeepMark));
            Assert.That(r12.Ultimate, Is.Null);
            Assert.That(SkillBook.BuildRing(1, 30, null).Ultimate.Id, Is.EqualTo("forge_judgment"));
        }

        [Test]
        public void Player_can_arrange_the_ring_within_the_unlocks()
        {
            var w = new SimWorld(12);
            w.Hero.Level = 8;
            w.RefreshRing();
            w.SetCustomSkill(0, "chain_hook");
            Assert.That(w.Hero.Ring.Slots[0].Id, Is.EqualTo("chain_hook"));
            Assert.That(w.Hero.UseCustomRing, Is.True);
            w.SetCustomSkill(1, "chain_hook"); // moving it swaps, never duplicates
            Assert.That(w.Hero.Ring.Slots.Count(s => s != null && s.Id == "chain_hook"), Is.EqualTo(1));
            w.SetCustomSkill(2, "fissure");    // Lv12 skill at Lv8: stays empty
            Assert.That(w.Hero.Ring.Slots[2], Is.Null);
            w.SetCustomSigil(0, Sigil.Wide);   // glyphs from Lv12
            Assert.That(w.Hero.Ring.Sigils[0], Is.EqualTo(Sigil.None));

            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.UseCustomRing, Is.True);
            Assert.That(b.Hero.Ring.Slots[1].Id, Is.EqualTo("chain_hook"));

            w.SetPreset(0);
            Assert.That(w.Hero.UseCustomRing, Is.False, "P goes back to the recommended rings");
        }

        [Test]
        public void A_glyph_works_on_its_slot_and_the_next_one_clockwise()
        {
            var ring = SkillBook.BuildRing(1, 12, null); // sigils: DeepMark, Feed, Chain, Wide
            var m1 = ring.ModsFor(1);
            Assert.That(m1.GaugeBonus, Is.EqualTo(0.5f).Within(1e-4f), "own Feed");
            Assert.That(m1.ResBonus, Is.EqualTo(0.25f).Within(1e-4f), "DeepMark from slot 0");
            var m0 = ring.ModsFor(0);
            Assert.That(m0.RadiusMul, Is.EqualTo(1.4f).Within(1e-4f), "Wide from slot 3 wraps around");
        }

        [Test]
        public void Skill_evolves_at_practice_ten()
        {
            // 기억의 나무 (11-21): 숙련 10 opens the evolution node and 자동 분배 takes it (was: two of three conditions).
            var w = new SimWorld(3) { EnemyBrainEnabled = false };
            w.Hero.Preset = 1; // 파쇄 순환 (ember_slash first); 0 is 휠윈드
            w.RefreshRing();
            w.ClearEnemies();
            w.SpawnEnemy(Def(1e7f), w.Hero.Pos + new Vec2(0.6f, 0f)).Aggro = true;
            w.Hero.SkillUses["ember_slash"] = SkillBook.UsesFor(SkillTree.EvoMastery) - 1;
            for (int i = 0; i < 40 && !w.Hero.Evolved.Contains("ember_slash"); i++) w.Tick();
            Assert.That(w.Hero.Evolved.Contains("ember_slash"), Is.True);
            Assert.That(w.Hero.Ring.Slots[0].Id, Is.EqualTo("evo_twin_ember"));
        }

        [Test]
        public void Skill_state_survives_a_save()
        {
            var a = new SimWorld(5);
            for (int i = 0; i < 1500; i++) a.Tick();
            a.Hero.LegacyEvolved.Add("ember_whirl"); // evolutions are re-derived from the tree; legacy ones persist
            a.Hero.ResonanceCounts[(int)Resonance.Overload] = 1234;
            a.Hero.Gauge = 12.5f;
            a.SetPreset(2);
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(a.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.Preset, Is.EqualTo(2));
            Assert.That(b.Hero.Evolved.Contains("ember_whirl"), Is.True);
            Assert.That(b.Hero.ResonanceCounts[(int)Resonance.Overload], Is.EqualTo(1234));
            Assert.That(b.Hero.Gauge, Is.EqualTo(12.5f));
            Assert.That(b.Hero.SkillUses.Count, Is.EqualTo(a.Hero.SkillUses.Count));
        }

        [Test]
        public void Every_ashknight_skill_casts_without_errors()
        {
            foreach (var skill in SkillBook.Ashknight)
            {
                var w = new SimWorld(7) { EnemyBrainEnabled = false };
                w.ClearEnemies();
                for (int i = 0; i < 6; i++) w.SpawnEnemy(Def(1e6f), w.Hero.Pos + new Vec2(1f + i * 0.4f, (i % 2) * 0.5f)).Aggro = true;
                w.Hero.Level = 30;
                var slots = new SkillDef[4];
                if (skill.Role == SkillRole.Ultimate) { w.Hero.Ring = new SkillRing(SkillBook.Basic, slots) { Ultimate = skill }; }
                else { slots[0] = skill; w.Hero.Ring = new SkillRing(SkillBook.Basic, slots); }
                bool cast = false;
                for (int i = 0; i < 60; i++)
                {
                    w.Tick();
                    cast |= w.Events.Any(ev => ev.Type == SimEventType.Cast && ev.SkillId == skill.Id);
                }
                Assert.That(cast, Is.True, skill.Id);
            }
        }
    }
}
