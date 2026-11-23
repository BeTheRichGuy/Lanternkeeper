using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §11.5 환생과 잔불.</summary>
    public class RebirthTests
    {
        static SimWorld AtCap(long overflow)
        {
            var w = new SimWorld(81) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.Hero.Level = SimWorld.LevelCap;
            w.Hero.OverflowXp = overflow;
            w.RefreshRing();
            HeroStats.Recompute(w.Hero);
            return w;
        }

        [Test]
        public void Past_the_cap_xp_only_piles_up_as_grey_ember()
        {
            var w = AtCap(0);
            w.Hero.Xp = w.Hero.XpToNext - 1;
            var e = w.SpawnEnemy(new EnemyDef { Id = "x", MaxHp = 1f, Xp = 500 }, w.Hero.Pos);
            w.ApplyHit(e, 10f, Element.None);
            Assert.That(w.Hero.Level, Is.EqualTo(SimWorld.LevelCap));
            Assert.That(w.Hero.OverflowXp, Is.GreaterThan(0));
        }

        [Test]
        public void Rebirth_turns_grey_ember_on_and_keeps_every_item()
        {
            long cost = SimWorld.EmberCost(1) + SimWorld.EmberCost(2) + SimWorld.EmberCost(3);
            var w = AtCap(cost + 10);
            var sword = new Item { Uid = 7000, BaseId = "rusted_greatsword", Rarity = Rarity.Rare, Ilvl = 50, ReqLevel = 45, Name = "high sword" };
            sword.Affixes.Add(new AffixRoll { Stat = Stat.FlatDamage, Value = 100f, Tier = 1 });
            w.Hero.Gear.Equip(sword, EquipSpot.Weapon);
            Assert.That(w.PendingEmber, Is.EqualTo(3));
            Assert.That(w.Rebirth(), Is.True);
            Assert.That(w.Hero.Level, Is.EqualTo(1));
            Assert.That(w.Hero.EmberLevel, Is.EqualTo(3));
            Assert.That(w.Hero.IntegratedLevel, Is.EqualTo(4));
            Assert.That(w.Hero.OverflowXp, Is.EqualTo(10));
            Assert.That(w.Hero.Gear[EquipSpot.Weapon], Is.SameAs(sword), "worn gear stays on");
            Assert.That(w.Hero.Stats.Damage, Is.LessThan(100f), "but does nothing until the integrated level reaches it");
            Assert.That(w.Events.Any(ev => ev.Type == SimEventType.Rebirth), Is.True);
        }

        [Test]
        public void A_rebirth_between_ticks_still_reaches_the_view_on_the_next_tick()
        {
            var w = AtCap(SimWorld.EmberCost(1));
            w.Tick();
            w.Rebirth(); // from the camp panel, between ticks
            w.Tick();
            Assert.That(w.Events.Any(ev => ev.Type == SimEventType.Rebirth), Is.True);
            w.Tick();
            Assert.That(w.Events.Any(ev => ev.Type == SimEventType.Rebirth), Is.False, "carried once, not forever");
        }

        [Test]
        public void Offline_xp_past_the_cap_piles_up_as_grey_ember_too()
        {
            var w = new SimWorld(83);
            w.Hero.Level = SimWorld.LevelCap;
            w.RefreshRing();
            HeroStats.Recompute(w.Hero);
            for (int i = 0; i < 1300; i++) w.Tick(); // two minutes of rates for the offline estimate
            w.Hero.Xp = w.Hero.XpToNext - 1;      // any offline XP now crosses the cap
            w.ApplyOffline(3600);
            Assert.That(w.Hero.Level, Is.EqualTo(SimWorld.LevelCap));
            Assert.That(w.Hero.OverflowXp, Is.GreaterThan(0));
        }

        [Test]
        public void An_old_save_past_act_one_counts_it_as_beaten()
        {
            var w = new SimWorld(84);
            w.GoToFloor(SimWorld.ActLength + 2);
            var save = w.ToSave();
            save.DeepestActCleared = -1; // as written before rebirth existed
            Assert.That(SimWorld.FromSave(save).Hero.DeepestActCleared, Is.EqualTo(0));
        }

        [Test]
        public void Boss_summons_give_nothing()
        {
            var w = new SimWorld(85) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.UseOpenFloor();
            var s = w.SpawnEnemy(new EnemyDef { Id = "risen", MaxHp = 1f, Xp = 500 }, new Vec2(50f, 50f));
            s.Summoned = true;
            int kills = w.Kills, xp = w.Hero.Xp;
            w.ApplyHit(s, 10f, Element.None);
            Assert.That(w.Kills, Is.EqualTo(kills));
            Assert.That(w.Hero.Xp, Is.EqualTo(xp));
            Assert.That(w.KillsOf("risen"), Is.Zero);
        }

        [Test]
        public void Rebirth_only_at_the_cap()
        {
            var w = new SimWorld(82);
            Assert.That(w.Rebirth(), Is.False);
        }

        [Test]
        public void Beaten_acts_are_reclimbed_fast()
        {
            var w = AtCap(0);
            w.Hero.DeepestActCleared = 1;
            w.Rebirth();
            Assert.That(w.FastDescent, Is.True);
            Assert.That(w.RepeatMul, Is.EqualTo(3), "act I with act II beaten: x3");
            w.GoToFloor(SimWorld.ActLength * 2); // act III (repeat of II) not beaten
            Assert.That(w.FastDescent, Is.False);
        }

        [Test]
        public void Ember_survives_a_save()
        {
            var w = AtCap(SimWorld.EmberCost(1));
            w.Hero.DeepestActCleared = 0;
            w.Rebirth();
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.EmberLevel, Is.EqualTo(1));
            Assert.That(b.Hero.Rebirths, Is.EqualTo(1));
            Assert.That(b.Hero.DeepestActCleared, Is.EqualTo(0));
        }
    }
}
