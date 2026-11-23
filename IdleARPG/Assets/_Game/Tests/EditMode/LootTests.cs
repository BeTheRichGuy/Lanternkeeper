using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class LootTests
    {
        static SimWorld EmptyWorld()
        {
            var w = new SimWorld(41) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.ClearEnemies();
            return w;
        }

        static Item Junk(Rarity r, int ilvl = 1) => new Item { Uid = 1, BaseId = "bone_ring", Rarity = r, Ilvl = ilvl, ReqLevel = 1, Name = "x" };

        [Test]
        public void Normal_monster_drop_rate_is_about_8_percent()
        {
            var w = EmptyWorld();
            int drops = 0, n = 3000;
            for (int i = 0; i < n; i++)
            {
                var e = w.SpawnEnemy(new EnemyDef { Id = "t", MaxHp = 1f, Xp = 0 }, new Vec2(50f, 50f));
                w.Events.Clear();
                w.ApplyHit(e, 99f, Element.None);
                drops += w.Events.Count(ev => ev.Type == SimEventType.Drop);
            }
            Assert.That(drops / (float)n, Is.EqualTo(DropTable.NormalChance).Within(0.015f));
        }

        [Test]
        public void First_legendary_is_guaranteed_at_level_3()
        {
            var w = EmptyWorld();
            w.Hero.Level = 3;
            HeroStats.Recompute(w.Hero);
            var item = w.DropItem(new Vec2(50f, 50f), Rarity.Common);
            Assert.That(item.Rarity, Is.EqualTo(Rarity.Legendary));
            Assert.That(w.FirstLegendaryGiven, Is.True);
        }

        /// <summary>희승 11-22: 직업 전용 장비, 내 직업 것만 — a hunter finds bows and hunter gear (and shared jewellery), never a sword.</summary>
        [Test]
        public void Drops_are_only_for_the_heros_class()
        {
            foreach (var cls in SkillBook.PlayableClasses)
            {
                var rng = new SimRng(7);
                for (int i = 0; i < 400; i++)
                {
                    var item = ItemGen.Roll(rng, i + 1, 30, (Rarity)(i % 5), null, 0f, cls);
                    var b = ItemData.Base(item.BaseId);
                    Assert.That(b.ClassId == null || b.ClassId == cls, Is.True, cls + " got " + item.BaseId);
                }
            }
            Assert.That(System.Array.Exists(ItemData.Bases, b => b.ClassId == "windhunter" && b.Slot == Slot.Weapon && b.Name.Contains("궁")), Is.True, "bows exist");
        }

        [Test]
        public void Boss_drops_four_items()
        {
            var w = EmptyWorld();
            var boss = w.SpawnEnemy(new EnemyDef { Id = "t", MaxHp = 1f, Xp = 0 }, new Vec2(50f, 50f));
            boss.Boss = true;
            w.Events.Clear();
            w.ApplyHit(boss, 99f, Element.None);
            Assert.That(w.Events.Count(ev => ev.Type == SimEventType.Drop), Is.EqualTo(DropTable.BossDrops));
        }

        [Test]
        public void Every_preset_protects_legendaries_and_stars_first_and_ends_with_a_catch_all()
        {
            foreach (FilterPreset p in System.Enum.GetValues(typeof(FilterPreset)))
            {
                if (p == FilterPreset.Custom) continue; // the player's own list (tested below)
                var rules = LootFilter.Rules(p).ToList();
                Assert.That(rules[0].Cond, Is.EqualTo(RuleCond.RarityAtLeast), p.ToString());
                Assert.That(rules[1].Cond, Is.EqualTo(RuleCond.StarsAtLeast), p.ToString());
                Assert.That(rules[rules.Count - 1].Cond, Is.EqualTo(RuleCond.Always), p.ToString());
            }
        }

        [Test]
        public void Custom_rules_decide_after_the_protecting_ones_and_survive_a_save()
        {
            var w = new SimWorld(3, "ashknight");
            w.Filter = FilterPreset.Custom;
            w.CustomRules.Clear();
            w.CustomRules.Add(new LootRule(RuleCond.SocketsAtLeast, 2, LootAction.Keep));
            w.CustomRules.Add(new LootRule(RuleCond.Always, 0, LootAction.Salvage));
            var rng = new SimRng(4);
            var rare = ItemGen.Roll(rng, 1, 20, Rarity.Rare, null, 0f);
            rare.Sockets = 0;
            Assert.That(LootFilter.Decide(w.Filter, rare, "ashknight", w.CustomRules), Is.EqualTo(LootAction.Salvage));
            rare.Sockets = 2;
            Assert.That(LootFilter.Decide(w.Filter, rare, "ashknight", w.CustomRules), Is.EqualTo(LootAction.Keep));
            var leg = ItemGen.Roll(rng, 2, 20, Rarity.Legendary, null, 0f);
            Assert.That(LootFilter.Decide(w.Filter, leg, "ashknight", w.CustomRules), Is.EqualTo(LootAction.Keep), "legendaries stay protected");
            var back = SimWorld.FromSave(w.ToSave());
            Assert.That(back.Filter, Is.EqualTo(FilterPreset.Custom));
            Assert.That(back.CustomRules.Count, Is.EqualTo(2));
            Assert.That(back.CustomRules[0].Cond, Is.EqualTo(RuleCond.SocketsAtLeast));
            Assert.That(back.CustomRules[1].Action, Is.EqualTo(LootAction.Salvage));
        }

        [TestCase(FilterPreset.Beginner, Rarity.Common, LootAction.Keep)]
        [TestCase(FilterPreset.Normal, Rarity.Common, LootAction.Salvage)]
        [TestCase(FilterPreset.Normal, Rarity.Magic, LootAction.Keep)]
        [TestCase(FilterPreset.Strict, Rarity.Magic, LootAction.Salvage)]
        [TestCase(FilterPreset.Strict, Rarity.Rare, LootAction.Keep)]
        [TestCase(FilterPreset.Strict, Rarity.Legendary, LootAction.Keep)]
        [TestCase(FilterPreset.Endgame, Rarity.Rare, LootAction.Salvage)]
        [TestCase(FilterPreset.Endgame, Rarity.Legendary, LootAction.Keep)]
        public void Filter_presets(FilterPreset p, Rarity r, LootAction expected)
        {
            Assert.That(LootFilter.Decide(p, Junk(r)), Is.EqualTo(expected));
        }

        [TestCase(FilterPreset.Strict, Rarity.Magic, 2, LootAction.Keep)]
        [TestCase(FilterPreset.Strict, Rarity.Magic, 1, LootAction.Salvage)]
        [TestCase(FilterPreset.Endgame, Rarity.Common, 3, LootAction.Keep)]
        [TestCase(FilterPreset.Endgame, Rarity.Rare, 2, LootAction.Salvage)]
        public void Socketed_runeword_bases_survive_stricter_filters(FilterPreset p, Rarity r, int sockets, LootAction expected)
        {
            var item = Junk(r);
            item.Sockets = sockets;
            Assert.That(LootFilter.Decide(p, item), Is.EqualTo(expected));
        }

        [Test]
        public void A_full_bag_never_grows_but_locked_pieces_are_never_taken()
        {
            var bag = new Bag();
            var locked = Junk(Rarity.Common);
            locked.Locked = true;
            bag.Add(locked);
            for (int i = 0; i < Bag.Capacity + 5; i++) bag.Add(Junk(Rarity.Legendary));
            Assert.That(bag.Items.Count, Is.EqualTo(Bag.Capacity));
            Assert.That(bag.Items.Contains(locked), Is.True, "잠금 always wins, even over a legendary");
            Assert.That(bag.AshShards, Is.GreaterThan(0));
        }

        [Test]
        public void Auto_salvage_takes_the_chosen_rarities_and_spares_the_chase()
        {
            var w = new SimWorld(5) { AutoSalvage = (int)Rarity.Magic };
            Assert.That(w.IsJunk(Junk(Rarity.Common)), Is.True);
            Assert.That(w.IsJunk(Junk(Rarity.Magic)), Is.True);
            Assert.That(w.IsJunk(Junk(Rarity.Rare)), Is.False);
            Assert.That(w.IsJunk(Junk(Rarity.Legendary)), Is.False);
            var socketed = Junk(Rarity.Magic);
            socketed.Sockets = 2;
            Assert.That(w.IsJunk(socketed), Is.False, "a runeword base is kept");
            w.AutoSalvage = -1;
            Assert.That(w.IsJunk(Junk(Rarity.Common)), Is.False, "끔 keeps everything");
            Assert.That(SimWorld.FromSave(new SimWorld(5) { AutoSalvage = 2 }.ToSave()).AutoSalvage, Is.EqualTo(2));
        }

        [Test]
        public void Bag_overflow_salvages_the_weakest_item()
        {
            var bag = new Bag();
            for (int i = 0; i < Bag.Capacity; i++) bag.Add(Junk(Rarity.Rare, 5));
            var weak = Junk(Rarity.Magic, 1);
            var salvaged = bag.Add(weak);
            Assert.That(salvaged, Is.SameAs(weak));
            Assert.That(bag.Items.Count, Is.EqualTo(Bag.Capacity));
            Assert.That(bag.AshShards, Is.GreaterThan(0));
        }

        [Test]
        public void Replaced_gear_goes_to_the_bag_not_to_salvage()
        {
            var w = EmptyWorld();
            w.Filter = FilterPreset.Strict;
            var first = new Item { Uid = 900, BaseId = "brass_ring", Rarity = Rarity.Magic, Ilvl = 1, ReqLevel = 1, Name = "a" };
            first.Affixes.Add(new AffixRoll { Stat = Stat.DamagePct, Value = 5f, Tier = 1 });
            var second = new Item { Uid = 901, BaseId = "brass_ring", Rarity = Rarity.Magic, Ilvl = 1, ReqLevel = 1, Name = "b" };
            second.Affixes.Add(new AffixRoll { Stat = Stat.DamagePct, Value = 3f, Tier = 1 });
            var third = new Item { Uid = 902, BaseId = "brass_ring", Rarity = Rarity.Magic, Ilvl = 1, ReqLevel = 1, Name = "c" };
            third.Affixes.Add(new AffixRoll { Stat = Stat.DamagePct, Value = 30f, Tier = 1 });
            w.TryAutoEquip(first, out _);
            w.TryAutoEquip(second, out _);
            var old = w.TryAutoEquip(third, out bool ok);
            Assert.That(ok, Is.True);
            Assert.That(old, Is.SameAs(second), "replaces the weaker ring");
        }
    }
}
