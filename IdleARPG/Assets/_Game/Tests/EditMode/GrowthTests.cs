using System.Collections.Generic;
using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>
    /// 희승: 스킬은 쓸수록 레벨업, 효과는 커지는 게 아니라 복제되고, 벽에 튕기는 투사체, 전사는 속도로 원거리는 범위로 성장.
    /// </summary>
    public class GrowthTests
    {
        static EnemyDef Dummy(float hp = 1e6f) => new EnemyDef { Id = "test", MaxHp = hp, Damage = 0f, MoveSpeed = 0f, Xp = 0 };

        static SimWorld With(string cls, int level, params string[] ring)
        {
            var w = new SimWorld(71, cls) { EnemyBrainEnabled = false };
            w.UseOpenFloor();
            w.Hero.Level = level;
            HeroStats.Recompute(w.Hero);
            var slots = new SkillDef[4];
            for (int i = 0; i < ring.Length; i++) slots[i] = SkillBook.Get(ring[i]);
            w.Hero.Ring = new SkillRing(SkillBook.BasicFor(cls), slots);
            return w;
        }

        [TestCase(0, 1)]
        [TestCase(12, 2)]
        [TestCase(48, 3)]
        [TestCase(2352, 15)]
        [TestCase(10000000, SkillBook.MaxSkillLevel)]
        public void Skill_level_grows_with_uses(int uses, int level)
        {
            Assert.That(SkillBook.SkillLevel(uses), Is.EqualTo(level));
            if (level < SkillBook.MaxSkillLevel) Assert.That(SkillBook.SkillLevel(SkillBook.UsesFor(level)), Is.EqualTo(level));
        }

        [Test]
        public void Using_a_skill_counts_and_announces_its_level_ups()
        {
            var w = With("ashknight", 10);
            var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(1f, 0f));
            e.Aggro = true;
            int ups = 0;
            for (int i = 0; i < 200; i++) { w.Tick(); ups += w.Events.Count(ev => ev.Type == SimEventType.SkillLevelUp); }
            Assert.That(w.Hero.SkillUses.Values.Sum(), Is.GreaterThan(12));
            Assert.That(ups, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Practised_skills_hit_harder()
        {
            float Damage(int uses)
            {
                var w = With("ashknight", 10);
                w.Hero.SkillUses[SkillBook.Basic.Id] = uses;
                var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(1f, 0f));
                e.Aggro = true;
                float dealt = 0f;
                for (int i = 0; i < 40; i++) { w.Tick(); dealt += w.Events.Where(ev => ev.Type == SimEventType.Hit && ev.TargetId == e.Id).Sum(ev => ev.Value); }
                return dealt;
            }
            Assert.That(Damage(SkillBook.UsesFor(21)), Is.GreaterThan(Damage(0) * 1.6f), "level 21: +100% power");
        }

        [Test]
        public void Melee_classes_grow_speed_and_ranged_classes_grow_area_and_reach()
        {
            var knight = With("ashknight", 60);
            var hunter = With("windhunter", 60);
            Assert.That(knight.Hero.Stats.AttackSpeed, Is.GreaterThan(hunter.Hero.Stats.AttackSpeed * 1.3f));
            Assert.That(knight.Hero.Stats.MoveSpeed, Is.GreaterThan(hunter.Hero.Stats.MoveSpeed));
            Assert.That(hunter.Hero.Derived.AreaPct, Is.GreaterThan(knight.Hero.Derived.AreaPct + 0.2f));
            Assert.That(hunter.Hero.Derived.RangePct, Is.GreaterThan(0.25f));
        }

        [Test]
        public void Ricochet_bounces_off_walls_and_hits_what_it_passes()
        {
            var w = With("windhunter", 30, "ricochet_arrow");
            w.UseOpenFloor(14); // a small room: the arrow reaches a wall
            w.Hero.Pos = w.Hero.PrevPos = new Vec2(3.5f, 7f);
            var a = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(3f, 0f));
            var b = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(5f, 0.2f));
            a.Aggro = b.Aggro = true;
            int legs = 0;
            bool hitB = false;
            for (int i = 0; i < 60 && legs < 2; i++)
            {
                w.Tick();
                legs = System.Math.Max(legs, w.Events.Count(ev => ev.Type == SimEventType.Shot && ev.SkillId == "ricochet_arrow"));
                hitB |= w.Events.Any(ev => ev.Type == SimEventType.Hit && ev.TargetId == b.Id);
            }
            Assert.That(legs, Is.GreaterThanOrEqualTo(2), "it bounced at least once");
            Assert.That(hitB, Is.True, "the arrow flies on through the first enemy");
        }

        [Test]
        public void Extra_projectiles_copy_a_shot_onto_other_enemies()
        {
            var w = With("starseer", 30);
            w.Hero.Derived.Projectiles = 3;
            var targets = Enumerable.Range(0, 5).Select(i => w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(3f + i * 0.6f, i - 2f))).ToList();
            foreach (var t in targets) t.Aggro = true;
            // Copies go off a tick apart (재기획 §4.4): count the targets struck over the volley, not in one tick.
            var struck = new HashSet<int>();
            for (int i = 0; i < 20 && struck.Count < 4; i++)
            {
                w.Tick();
                foreach (var ev in w.Events) if (ev.Type == SimEventType.Hit && ev.SourceId == w.Hero.Id) struck.Add(ev.TargetId);
            }
            Assert.That(struck.Count, Is.GreaterThanOrEqualTo(4), "one orb and three copies");
        }

        [Test]
        public void Greater_affixes_appear_deeper_only_on_rare_and_better()
        {
            Assert.That(ItemGen.GreaterChance(ItemGen.GreaterFrom - 1), Is.Zero, "act I has none");
            Assert.That(ItemGen.GreaterChance(ItemGen.GreaterFrom), Is.GreaterThan(0f), "act II starts the chase (11-21)");
            Assert.That(ItemGen.GreaterChance(60), Is.GreaterThan(ItemGen.GreaterChance(25)));
            Assert.That(ItemGen.GreaterChance(200), Is.EqualTo(0.05f).Within(1e-6f), "capped: the chase stays rare");
            var rng = new SimRng(5);
            int greater = 0, magicGreater = 0;
            for (int i = 0; i < 400; i++)
            {
                greater += ItemGen.Roll(rng, i, 60, Rarity.Rare, null, 0.5f).GreaterCount;
                magicGreater += ItemGen.Roll(rng, i, 60, Rarity.Magic, null, 0.5f).GreaterCount;
            }
            Assert.That(greater, Is.GreaterThan(200));
            Assert.That(magicGreater, Is.Zero);
        }

        [Test]
        public void Past_integrated_100_xp_feeds_the_endless_wick()
        {
            var w = new SimWorld(72);
            w.Hero.Level = SimWorld.LevelCap;
            w.Hero.EmberLevel = SimWorld.EmberCap;
            HeroStats.Recompute(w.Hero);
            float dmg0 = w.Hero.Derived.DamagePct, area0 = w.Hero.Derived.AreaPct;
            w.Hero.Xp = (int)System.Math.Min(int.MaxValue - 1, SimWorld.WickCost(0) + SimWorld.WickCost(1));
            w.GrantXpForTest(0);
            Assert.That(w.Hero.WickLevel, Is.EqualTo(2));
            Assert.That(w.Hero.OverflowXp, Is.Zero, "no grey ember once ember is full");
            Assert.That(w.Hero.Derived.DamagePct, Is.EqualTo(dmg0 + 0.02f).Within(1e-4f));
            Assert.That(w.Hero.Derived.AreaPct, Is.GreaterThan(area0));
            var back = SimWorld.FromSave(w.ToSave());
            Assert.That(back.Hero.WickLevel, Is.EqualTo(2));
        }

        [Test]
        public void Skills_practise_while_away_too()
        {
            var w = new SimWorld(73) { HeroBrainEnabled = true };
            for (int i = 0; i < 6000; i++) w.Tick(); // ten minutes of hunting to set the rates
            int before = w.Hero.SkillUses.Values.Sum();
            var r = w.ApplyOffline(4 * 3600);
            Assert.That(w.Hero.SkillUses.Values.Sum(), Is.GreaterThan(before + 1000));
            Assert.That(r.SkillLevelsGained, Is.GreaterThan(0));
        }

        [Test]
        public void Class_fit_filter_keeps_gear_for_the_class_growth_axis()
        {
            Item Rare(params Stat[] stats)
            {
                var it = new Item { BaseId = ItemData.Bases[0].Id, Rarity = Rarity.Rare, Ilvl = 10 };
                foreach (var s in stats) it.Affixes.Add(new AffixRoll { Stat = s, Value = 5, Tier = 3 });
                return it;
            }
            var speed = Rare(Stat.AttackSpeedPct, Stat.MoveSpeedPct, Stat.MaxHp);
            var area = Rare(Stat.AreaPct, Stat.RangePct, Stat.MaxHp);
            Assert.That(LootFilter.Decide(FilterPreset.ClassFit, speed, "ashknight"), Is.EqualTo(LootAction.Keep));
            Assert.That(LootFilter.Decide(FilterPreset.ClassFit, speed, "starseer"), Is.EqualTo(LootAction.Salvage));
            Assert.That(LootFilter.Decide(FilterPreset.ClassFit, area, "starseer"), Is.EqualTo(LootAction.Keep));
            Assert.That(LootFilter.Decide(FilterPreset.ClassFit, Rare(Stat.MaxHp, Stat.RegenPct), "starseer"), Is.EqualTo(LootAction.Salvage));
        }

        [Test]
        public void Act_bosses_drop_a_shower_with_a_legendary()
        {
            var w = new SimWorld(74) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.GoToFloor(SimWorld.ActBossFloorInAct);
            var boss = w.Enemies.Single(e => e.Boss);
            w.Events.Clear();
            w.ApplyHit(boss, 1e9f, Element.None);
            var drops = w.Events.Where(ev => ev.Type == SimEventType.Drop).ToList();
            Assert.That(drops.Count, Is.EqualTo(DropTable.ActBossDrops));
            Assert.That(drops.Any(ev => ev.Item.Rarity >= Rarity.Legendary), Is.True);
        }

        [Test]
        public void Auto_ignite_kindles_for_an_elite_and_burns_shorter()
        {
            var w = With("ashknight", 10);
            Assert.That(w.AutoIgnite, Is.False, "off unless the player turns it on");
            var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(3f, 0f));
            e.Elite = true;
            w.Tick();
            Assert.That(w.Ignited, Is.False);
            w.AutoIgnite = true;
            w.Tick();
            Assert.That(w.Ignited, Is.True);
            Assert.That(w.IgniteLeft, Is.LessThanOrEqualTo(SimWorld.AutoIgniteSeconds + 0.01f));
            Assert.That(SimWorld.FromSave(w.ToSave()).AutoIgnite, Is.True);
        }

        [Test]
        public void A_bag_of_legendaries_stays_at_forty_keeping_stars()
        {
            var bag = new Bag();
            var rng = new SimRng(9);
            for (int i = 0; i < Bag.Capacity * 3; i++)
            {
                var it = ItemGen.Roll(rng, i, 30, Rarity.Legendary, null, 0f);
                if (i % 10 == 0) { var a = it.Affixes[0]; a.Greater = true; it.Affixes[0] = a; }
                bag.Add(it);
            }
            Assert.That(bag.Items.Count, Is.EqualTo(Bag.Capacity), "희승 11-22: never more than the forty places");
            Assert.That(bag.Items.Count(it => it.GreaterCount > 0), Is.EqualTo(Bag.Capacity * 3 / 10), "★ pieces outweigh plain legendaries and all stay");
        }

        [Test]
        public void A_full_bag_salvages_plain_rares_before_any_star_piece()
        {
            var bag = new Bag();
            var rng = new SimRng(11);
            for (int i = 0; i < Bag.Capacity; i++)
            {
                var it = ItemGen.Roll(rng, i, 30, Rarity.Rare, null, 0f);
                if (i % 2 == 0) { var a = it.Affixes[0]; a.Greater = true; it.Affixes[0] = a; }
                bag.Add(it);
            }
            for (int i = 0; i < 40; i++) bag.Add(ItemGen.Roll(rng, 100 + i, 30, Rarity.Rare, null, 0f));
            Assert.That(bag.Items.Count(it => it.GreaterCount > 0), Is.EqualTo(Bag.Capacity / 2), "every ★ rare survives the overflow");
        }

        [TestCase(FilterPreset.ClassFit)]
        [TestCase(FilterPreset.StarHunt)]
        public void New_filter_presets_survive_a_save(FilterPreset p)
        {
            var w = new SimWorld(75) { Filter = p };
            Assert.That(SimWorld.FromSave(w.ToSave()).Filter, Is.EqualTo(p));
        }

        [Test]
        public void Star_hunt_keeps_only_stars_legendaries_and_runeword_bases()
        {
            var rng = new SimRng(3);
            var rare = ItemGen.Roll(rng, 1, 30, Rarity.Rare, null, 0f);
            rare.Sockets = 0;
            Assert.That(LootFilter.Decide(FilterPreset.StarHunt, rare), Is.EqualTo(LootAction.Salvage));
            var a = rare.Affixes[0]; a.Greater = true; rare.Affixes[0] = a;
            Assert.That(LootFilter.Decide(FilterPreset.StarHunt, rare), Is.EqualTo(LootAction.Keep));
            Assert.That(LootFilter.Decide(FilterPreset.StarHunt, ItemGen.Roll(rng, 2, 30, Rarity.Legendary, null, 0f)), Is.EqualTo(LootAction.Keep));
        }

        [Test]
        public void Enemies_grow_gentler_per_floor_from_act_three()
        {
            float early = SimWorld.GrowthAt(13).hp / SimWorld.GrowthAt(0).hp;
            float late = SimWorld.GrowthAt(SimWorld.LateFrom + 13).hp / SimWorld.GrowthAt(SimWorld.LateFrom).hp;
            Assert.That(early, Is.EqualTo((float)System.Math.Pow(SimWorld.HpGrowth, 13)).Within(0.01f), "acts I–II keep their tuned pace");
            Assert.That(late, Is.EqualTo((float)System.Math.Pow(SimWorld.HpGrowthLate, 13)).Within(0.01f));
            Assert.That(late, Is.LessThan(early * 0.75f), "an act from III on is less of a wall");
            Assert.That(SimWorld.GrowthAt(SimWorld.ActLength * 5).hp, Is.LessThan(2500f), "act VI is reachable, not ×8800");
        }

        [Test]
        public void Ranged_heroes_hit_harder_each_level_and_melee_do_not()
        {
            float Ratio(string cls)
            {
                var w = With(cls, 31);
                float k = HeroStats.RangedPowerPerLevel;
                try
                {
                    HeroStats.RangedPowerPerLevel = 0f;
                    HeroStats.Recompute(w.Hero);
                    float plain = w.Hero.Stats.Damage;
                    HeroStats.RangedPowerPerLevel = k;
                    HeroStats.Recompute(w.Hero);
                    return w.Hero.Stats.Damage / plain;
                }
                finally { HeroStats.RangedPowerPerLevel = k; HeroStats.Recompute(w.Hero); }
            }
            Assert.That(Ratio("starseer"), Is.EqualTo(1f + HeroStats.RangedPowerPerLevel * 30).Within(1e-3f));
            Assert.That(Ratio("windhunter"), Is.EqualTo(1f + HeroStats.RangedPowerPerLevel * 30).Within(1e-3f));
            Assert.That(Ratio("ashknight"), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Late_preview_makes_a_full_late_hero_in_act_six()
        {
            var w = new SimWorld(72, "starseer");
            w.ApplyLatePreview();
            var h = w.Hero;
            Assert.That(h.Level, Is.EqualTo(SimWorld.LevelCap));
            Assert.That(h.EmberLevel, Is.EqualTo(SimWorld.EmberCap));
            Assert.That(h.Gear.Spots.All(i => i != null && i.Rarity == Rarity.Legendary), Is.True, "every spot worn");
            Assert.That(h.Derived.Projectiles, Is.GreaterThanOrEqualTo(4));
            Assert.That(SkillBook.SkillLevel(h.SkillUses[h.Ring.Slots.First(s => s != null).BaseId]), Is.EqualTo(SimWorld.PreviewMastery));
            Assert.That(w.ActLoop, Is.EqualTo(5));
            Assert.That(h.Stats.Hp, Is.EqualTo(h.Stats.MaxHp));
        }

        [Test]
        public void Ranged_heroes_shoot_from_further_with_reach()
        {
            var w = With("windhunter", 1);
            var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(5.4f, 0f));
            e.Aggro = true;
            var start = w.Hero.Pos;
            bool shot = false;
            for (int i = 0; i < 20 && !shot; i++) { w.Tick(); shot = w.Events.Any(ev => ev.Type == SimEventType.Shot && ev.SourceId == w.Hero.Id); }
            Assert.That(shot, Is.True);
            Assert.That(Vec2.Dist(w.Hero.Pos, start), Is.LessThan(0.5f), "5.4 cells is within the bow's reach now");
        }
    }
}
