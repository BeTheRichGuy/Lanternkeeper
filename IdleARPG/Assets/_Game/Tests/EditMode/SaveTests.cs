using System;
using System.IO;
using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class SaveTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "lk-save-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static SimWorld Played(int seed, int ticks)
        {
            var w = new SimWorld(seed);
            for (int i = 0; i < ticks; i++) w.Tick();
            return w;
        }

        [Test]
        public void Roundtrip_preserves_hero_and_bag()
        {
            var a = Played(55, 3000);
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(a.ToSave()), out var data), Is.True);
            var b = SimWorld.FromSave(data);

            Assert.That(b.Hero.Level, Is.EqualTo(a.Hero.Level));
            Assert.That(b.Hero.Xp, Is.EqualTo(a.Hero.Xp));
            Assert.That(b.FloorIndex, Is.EqualTo(a.FloorIndex));
            Assert.That(b.Bag.Items.Select(i => i.Uid), Is.EqualTo(a.Bag.Items.Select(i => i.Uid)));
            Assert.That(b.Bag.AshShards, Is.EqualTo(a.Bag.AshShards));
            for (int s = 0; s < 9; s++)
                Assert.That(b.Hero.Gear.Spots[s]?.Uid, Is.EqualTo(a.Hero.Gear.Spots[s]?.Uid), $"spot {s}");
            Assert.That(b.Hero.Stats.Damage, Is.EqualTo(a.Hero.Stats.Damage).Within(1e-4f));
            Assert.That(b.Hero.Stats.MaxHp, Is.EqualTo(a.Hero.Stats.MaxHp).Within(1e-4f));
            Assert.That(b.Hero.Derived.Essences, Is.EquivalentTo(a.Hero.Derived.Essences));
        }

        [Test]
        public void Loaded_world_continues_the_same_loot_stream()
        {
            var a = Played(56, 1200);
            var b = SimWorld.FromSave(a.ToSave());
            for (int i = 0; i < 20; i++)
                Assert.That(b.DropItem(new Vec2(1, 1), Rarity.Common).Name, Is.EqualTo(a.DropItem(new Vec2(1, 1), Rarity.Common).Name));
        }

        [Test]
        public void Corrupt_main_falls_back_to_backup()
        {
            var sys = new SaveSystem(_dir);
            var w = Played(57, 600);
            sys.Save(w.ToSave(), DateTime.UtcNow, rotateBackup: false);
            int savedLevel = w.Hero.Level;
            for (int i = 0; i < 1200; i++) w.Tick();
            sys.Save(w.ToSave(), DateTime.UtcNow, rotateBackup: true); // previous becomes bak1

            var main = Path.Combine(_dir, SaveSystem.MainFile);
            File.WriteAllText(main, File.ReadAllText(main).Replace("\"Level\\\":", "\"Level\\\":9")); // tamper: checksum no longer matches

            var loaded = sys.Load(out var source);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(source, Is.EqualTo("save.bak1.json"));
            Assert.That(loaded.Level, Is.EqualTo(savedLevel));
        }

        [Test]
        public void Garbage_file_is_rejected_and_nothing_valid_returns_null()
        {
            var sys = new SaveSystem(_dir);
            File.WriteAllText(Path.Combine(_dir, SaveSystem.MainFile), "not json at all {");
            Assert.That(sys.Load(out _), Is.Null);
        }

        [Test]
        public void Missing_fields_keep_their_defaults()
        {
            // An old file without Filter / AutoEquip: decoding must fall back to the defaults.
            var d = UnityEngine.JsonUtility.FromJson<SaveData>("{\"Seed\":3,\"Level\":4}");
            Assert.That(d.Level, Is.EqualTo(4));
            Assert.That(d.Filter, Is.EqualTo((int)FilterPreset.Normal));
            Assert.That(d.AutoEquip, Is.True);
            Assert.That(d.NextItemUid, Is.EqualTo(1));
        }

        [Test]
        public void Saved_time_is_recorded()
        {
            var sys = new SaveSystem(_dir);
            var t = new DateTime(2026, 11, 19, 3, 0, 0, DateTimeKind.Utc);
            sys.Save(new SimWorld(1).ToSave(), t, false);
            Assert.That(sys.Load(out _).SavedAtUnix, Is.EqualTo(new DateTimeOffset(t).ToUnixTimeSeconds()));
        }
    }
}
