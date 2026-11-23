using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §14 야영지 (M2 Task 5).</summary>
    public class CampTests
    {
        static SimWorld Rich()
        {
            var w = new SimWorld(41) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.CampUnlocked = true;
            w.Bag.AshShards = 10000;
            w.Bag.LanternOil = 1000;
            var sword = new Item { Uid = 5000, BaseId = "rusted_greatsword", Rarity = Rarity.Rare, Ilvl = 7, ReqLevel = 1, Name = "test sword" };
            sword.Affixes.Add(new AffixRoll { Stat = Stat.FlatDamage, Value = 30f, Tier = 1 });
            w.Hero.Gear.Equip(sword, EquipSpot.Weapon);
            HeroStats.Recompute(w.Hero);
            return w;
        }

        [Test]
        public void The_camp_opens_when_the_act_boss_falls()
        {
            var w = new SimWorld(42) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            Assert.That(w.CampUnlocked, Is.False);
            w.GoToFloor(SimWorld.ActBossFloorInAct);
            var boss = w.Enemies.First(e => e.Boss);
            w.ApplyHit(boss, 1e9f, Element.None);
            Assert.That(w.CampUnlocked, Is.True);
            Assert.That(w.Events.Any(e => e.Type == SimEventType.CampUnlocked), Is.True);
        }

        [Test]
        public void Nothing_is_sold_before_the_camp_opens()
        {
            var w = Rich();
            w.CampUnlocked = false;
            Assert.That(w.UpgradeOilStore(), Is.False);
            Assert.That(w.Bag.AshShards, Is.EqualTo(10000));
        }

        [Test]
        public void Masterwork_raises_every_stat_of_the_item_by_eight_percent_a_step()
        {
            var w = Rich();
            var spot = EquipSpot.Weapon;
            var item = w.Hero.Gear[spot];
            float before = item.Sum(Stat.FlatDamage);
            int ash = w.Bag.AshShards;
            Assert.That(w.Masterwork(spot), Is.True);
            Assert.That(item.Masterwork, Is.EqualTo(1));
            Assert.That(item.Sum(Stat.FlatDamage), Is.EqualTo(before * 1.08f).Within(1e-3f));
            Assert.That(w.Bag.AshShards, Is.EqualTo(ash - SimWorld.MasterworkCost(0).ash));
            for (int i = 1; i < SimWorld.MaxMasterwork; i++) w.Masterwork(spot);
            Assert.That(w.Masterwork(spot), Is.False, "caps at +12");
        }

        [Test]
        public void Oil_store_lengthens_the_offline_cap_to_sixteen_hours()
        {
            var w = Rich();
            Assert.That(w.OfflineCapSeconds, Is.EqualTo(8 * 3600));
            for (int i = 0; i < 10; i++) w.UpgradeOilStore();
            Assert.That(w.OilStoreLevel, Is.EqualTo(SimWorld.MaxOilStore));
            Assert.That(w.OfflineCapSeconds, Is.EqualTo(16 * 3600));
        }

        [Test]
        public void Camp_survives_a_save()
        {
            var w = Rich();
            w.UpgradeOilStore();
            w.UpgradeCartographer();
            w.Masterwork(EquipSpot.Weapon);
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.CampUnlocked, Is.True);
            Assert.That(b.OilStoreLevel, Is.EqualTo(1));
            Assert.That(b.CartographerLevel, Is.EqualTo(1));
            Assert.That(b.Hero.Gear[EquipSpot.Weapon]?.Masterwork, Is.EqualTo(1));
        }
    }
}
