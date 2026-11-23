using System;
using System.IO;
using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>Cases from the M1 code review (2026-11-19).</summary>
    public class M1ReviewTests
    {
        string _dir;

        [SetUp] public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "lk-m1-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        static SimWorld Played(int seed, int ticks)
        {
            var w = new SimWorld(seed);
            for (int i = 0; i < ticks; i++) w.Tick();
            return w;
        }

        static SimWorld ThroughJson(SimWorld w)
        {
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            return SimWorld.FromSave(d);
        }

        [Test]
        public void Json_roundtrip_keeps_settings_counters_and_recent_drops()
        {
            var a = Played(70, 4000);
            a.Filter = FilterPreset.Strict;
            a.AutoEquip = false;
            var b = ThroughJson(a);
            Assert.That(b.Filter, Is.EqualTo(FilterPreset.Strict));
            Assert.That(b.AutoEquip, Is.False);
            Assert.That(b.FirstLegendaryGiven, Is.EqualTo(a.FirstLegendaryGiven));
            Assert.That(b.NextItemUid, Is.EqualTo(a.NextItemUid));
            Assert.That(b.Kills, Is.EqualTo(a.Kills));
            Assert.That(b.Deaths, Is.EqualTo(a.Deaths));
            Assert.That(b.RecentDrops.Select(i => i.Uid), Is.EqualTo(a.RecentDrops.Select(i => i.Uid)));
            Assert.That(b.KillsPerMinute, Is.EqualTo(a.KillsPerMinute).Within(1e-3f));
        }

        [Test]
        public void Json_loaded_world_continues_the_same_loot_stream()
        {
            var a = Played(71, 1500);
            var b = ThroughJson(a);
            for (int i = 0; i < 20; i++)
                Assert.That(b.DropItem(new Vec2(1, 1), Rarity.Common).Name, Is.EqualTo(a.DropItem(new Vec2(1, 1), Rarity.Common).Name));
        }

        [Test]
        public void Backups_rotate_newest_first()
        {
            var sys = new SaveSystem(_dir);
            for (int level = 1; level <= 5; level++)
            {
                var d = new SimWorld(1).ToSave();
                d.Level = level;
                sys.Save(d, DateTime.UtcNow, rotateBackup: true);
            }
            int LevelIn(string name)
            {
                Assert.That(SaveSystem.TryDecode(File.ReadAllText(Path.Combine(_dir, name)), out var d), Is.True, name);
                return d.Level;
            }
            Assert.That(LevelIn(SaveSystem.MainFile), Is.EqualTo(5));
            Assert.That(LevelIn("save.bak1.json"), Is.EqualTo(4));
            Assert.That(LevelIn("save.bak2.json"), Is.EqualTo(3));
            Assert.That(LevelIn("save.bak3.json"), Is.EqualTo(2));
        }

        [Test]
        public void Unreadable_saves_are_reported_and_quarantined_not_overwritten()
        {
            var sys = new SaveSystem(_dir);
            File.WriteAllText(Path.Combine(_dir, SaveSystem.MainFile), "{ broken");
            Assert.That(sys.Load(out _, out var result), Is.Null);
            Assert.That(result, Is.EqualTo(SaveSystem.LoadResult.AllUnreadable));
            var kept = sys.Quarantine(DateTime.UtcNow);
            Assert.That(File.Exists(Path.Combine(kept, SaveSystem.MainFile)), Is.True);
            Assert.That(File.Exists(Path.Combine(_dir, SaveSystem.MainFile)), Is.False);
            Assert.That(sys.Load(out _, out result), Is.Null);
            Assert.That(result, Is.EqualTo(SaveSystem.LoadResult.NoSave));
        }

        [Test]
        public void Save_from_a_newer_build_is_refused()
        {
            var d = new SimWorld(1).ToSave();
            d.Version = SaveData.CurrentVersion + 1;
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(d), out _), Is.False);
        }

        [Test]
        public void Offline_reward_is_not_granted_twice_after_resave()
        {
            var sys = new SaveSystem(_dir);
            var save = Played(72, 3000).ToSave();
            var then = DateTime.UtcNow.AddHours(-3);
            sys.Save(save, then, false);
            var w = SimWorld.FromSave(sys.Load(out _));
            Assert.That(w.ApplyOffline((DateTime.UtcNow - then).TotalSeconds).Empty, Is.False);
            sys.Save(w.ToSave(), DateTime.UtcNow, true);
            var again = sys.Load(out _);
            double away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - again.SavedAtUnix;
            Assert.That(SimWorld.FromSave(again).ApplyOffline(away).Empty, Is.True);
        }

        [Test]
        public void Upgrade_check_does_not_touch_hero_state()
        {
            var w = Played(73, 2000);
            w.Hero.Stats.Hp = 0.5f;
            float dmg = w.Hero.Stats.Damage, maxHp = w.Hero.Stats.MaxHp;
            int ess = w.Hero.Derived.Essences.Count;
            var probe = ItemGen.Roll(new SimRng(9), 9999, 3, Rarity.Rare);
            foreach (var spot in Equipment.SpotsFor(probe.Slot)) HeroStats.Upgrade(w.Hero, probe, spot, out _);
            Assert.That(w.Hero.Stats.Hp, Is.EqualTo(0.5f));
            Assert.That(w.Hero.Stats.Damage, Is.EqualTo(dmg));
            Assert.That(w.Hero.Stats.MaxHp, Is.EqualTo(maxHp));
            Assert.That(w.Hero.Derived.Essences.Count, Is.EqualTo(ess));
        }

        [Test]
        public void Replaced_gear_from_a_drop_lands_in_the_bag()
        {
            var w = new SimWorld(74) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.ClearEnemies();
            w.Filter = FilterPreset.Strict;
            int equippedBefore = w.Hero.Gear.Spots.Count(s => s != null);
            for (int i = 0; i < 200; i++) w.DropItem(new Vec2(1, 1), Rarity.Magic);
            int worn = w.Hero.Gear.Spots.Count(s => s != null);
            Assert.That(worn, Is.GreaterThan(equippedBefore));
            // The strict filter salvages new magic drops, so any magic item in the bag can only be replaced gear.
            Assert.That(w.Bag.Items.Count(it => it.Rarity == Rarity.Magic), Is.GreaterThan(0), "replaced magic gear is kept even under the strict filter");
        }

        [Test]
        public void Level_up_equips_a_bag_item_that_just_became_wearable()
        {
            var w = new SimWorld(75) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.ClearEnemies();
            var sword = new Item { Uid = 5000, BaseId = "rusted_greatsword", Rarity = Rarity.Rare, Ilvl = 7, ReqLevel = 2, Name = "late" };
            sword.Affixes.Add(new AffixRoll { Stat = Stat.FlatDamage, Value = 30f, Tier = 1 });
            w.Bag.Items.Add(sword);
            var e = w.SpawnEnemy(new EnemyDef { Id = "t", MaxHp = 1f, Xp = 1500 }, new Vec2(50, 50));
            w.ApplyHit(e, 99f, Element.None); // enough XP for Lv2 (GDD curve 412; common kills give a third, SimWorld.KillXpScale)
            Assert.That(w.Hero.Level, Is.GreaterThanOrEqualTo(2));
            Assert.That(w.Hero.Gear[EquipSpot.Weapon], Is.SameAs(sword));
            Assert.That(w.Bag.Items.Contains(sword), Is.False);
        }

        [Test]
        public void Boss_drops_happen_before_the_floor_changes()
        {
            var w = new SimWorld(1);
            for (int i = 0; i < 12000; i++)
            {
                w.Tick();
                if (!w.Events.Any(ev => ev.Type == SimEventType.FloorCleared)) continue;
                Assert.That(w.Events.Any(ev => ev.Type == SimEventType.Drop), Is.False, "drops in the floor-change tick would be hidden");
                return;
            }
            Assert.Fail("no floor cleared");
        }

        [Test]
        public void No_rare_or_legendary_before_level_3()
        {
            var rng = new SimRng(5);
            for (int i = 0; i < 20000; i++)
                Assert.That(ItemGen.RollRarity(rng, 2, 0f), Is.LessThanOrEqualTo(Rarity.Magic));
        }
    }
}
