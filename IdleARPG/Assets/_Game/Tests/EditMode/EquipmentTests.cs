using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class EquipmentTests
    {
        static Item Make(string baseId, int req = 1, string essence = "", params (Stat s, float v)[] affixes)
        {
            var it = new Item { Uid = 100, BaseId = baseId, Rarity = Rarity.Rare, Ilvl = req + 5, ReqLevel = req, Name = baseId, EssenceId = essence };
            it.Implicit = new AffixRoll { Stat = ItemData.Base(baseId).ImplicitStat, Value = 0f };
            foreach (var (s, v) in affixes) it.Affixes.Add(new AffixRoll { Stat = s, Value = v, Tier = 1 });
            return it;
        }

        static SimWorld EmptyWorld(bool heroThinks = false)
        {
            var w = new SimWorld(31) { HeroBrainEnabled = heroThinks, EnemyBrainEnabled = false };
            w.UseOpenFloor();
            return w;
        }

        [Test]
        public void Equipping_a_weapon_raises_damage()
        {
            var w = EmptyWorld();
            float before = w.Hero.Stats.Damage;
            w.TryAutoEquip(Make("rusted_greatsword", affixes: (Stat.FlatDamage, 8f)), out bool ok);
            Assert.That(ok, Is.True);
            Assert.That(w.Hero.Stats.Damage, Is.EqualTo(before + 8f).Within(1e-3f));
        }

        [Test]
        public void Worse_item_is_not_equipped()
        {
            var w = EmptyWorld();
            w.TryAutoEquip(Make("rusted_greatsword", affixes: (Stat.FlatDamage, 8f)), out _);
            var weak = Make("rusted_greatsword", affixes: (Stat.FlatDamage, 2f));
            var back = w.TryAutoEquip(weak, out bool ok);
            Assert.That(ok, Is.False);
            Assert.That(back, Is.SameAs(weak));
        }

        [Test]
        public void Item_above_hero_level_is_not_equipped()
        {
            var w = EmptyWorld();
            w.TryAutoEquip(Make("rusted_greatsword", req: 5, affixes: (Stat.FlatDamage, 50f)), out bool ok);
            Assert.That(ok, Is.False);
        }

        [Test]
        public void Second_ring_fills_the_empty_ring_spot()
        {
            var w = EmptyWorld();
            w.TryAutoEquip(Make("brass_ring", affixes: (Stat.DamagePct, 10f)), out _);
            w.TryAutoEquip(Make("brass_ring", affixes: (Stat.DamagePct, 6f)), out bool ok);
            Assert.That(ok, Is.True);
            Assert.That(w.Hero.Gear[EquipSpot.Ring1], Is.Not.Null);
            Assert.That(w.Hero.Gear[EquipSpot.Ring2], Is.Not.Null);
        }

        [Test]
        public void Damage_reduction_lowers_damage_taken()
        {
            var w = new SimWorld(31) { HeroBrainEnabled = false };
            w.ClearEnemies();
            w.TryAutoEquip(Make("chain_mail", affixes: (Stat.DamageReductionPct, 20f)), out _);
            var e = w.SpawnEnemy(new EnemyDef { Id = "test", MaxHp = 1000f, Damage = 10f, MoveSpeed = 0f }, w.Hero.Pos + new Vec2(0.5f, 0f));
            e.Aggro = true;
            w.Tick();
            var hit = w.Events.First(ev => ev.Type == SimEventType.Hit && ev.TargetId == w.Hero.Id);
            // 20% from the mail, then the melee guard every melee class has.
            Assert.That(hit.Value, Is.EqualTo(10f * 0.8f * (1f - HeroStats.MeleeGuardPct / 100f)).Within(1e-3f));
        }

        [Test]
        public void Long_chain_essence_raises_chain_cap_by_two()
        {
            var w = EmptyWorld();
            w.TryAutoEquip(Make("lantern_pendant", essence: "long_chain", affixes: (Stat.MaxHp, 1f)), out bool ok);
            Assert.That(ok, Is.True);
            var line = Enumerable.Range(0, 20).Select(i => w.SpawnEnemy(new EnemyDef { Id = "t", MaxHp = 1f }, new Vec2(50f + i * 1.5f, 50f))).ToList();
            line[0].Mark = Element.Ember;
            w.ApplyHit(line[0], 999f, Element.None);
            int pops = 0;
            for (int i = 0; i < 30; i++) { w.Tick(); pops += w.Events.Count(ev => ev.Type == SimEventType.ChainPop); }
            Assert.That(pops, Is.EqualTo(SimWorld.ChainCap + 2));
        }

        [Test]
        public void Ember_echo_casts_ember_slash_twice()
        {
            var w = EmptyWorld(heroThinks: true);
            w.Hero.Preset = 1; // 파쇄 순환 opens with ember_slash
            w.RefreshRing();
            w.TryAutoEquip(Make("ember_longsword", essence: "ember_echo", affixes: (Stat.FlatDamage, 1f)), out _);
            var e = w.SpawnEnemy(new EnemyDef { Id = "t", MaxHp = 100000f }, w.Hero.Pos + new Vec2(0.6f, 0f));
            e.Aggro = true;
            for (int i = 0; i < 20; i++)
            {
                w.Tick();
                if (w.Events.Any(ev => ev.Type == SimEventType.Cast && ev.SkillId == "ember_slash"))
                {
                    Assert.That(w.Events.Count(ev => ev.Type == SimEventType.Hit && ev.TargetId == e.Id), Is.EqualTo(2));
                    return;
                }
            }
            Assert.Fail("ember_slash never cast");
        }

        // ---------- UX plan §3.0 (다), A4–A6: one 전투력, the arrows, and auto-equip that follows them ----------

        /// <summary>A weapon with these lines (any stat may sit on a test weapon), under its own uid.</summary>
        static Item Sword(int uid, params (Stat s, float v)[] affixes)
        {
            var it = Make("rusted_greatsword", affixes: affixes);
            it.Uid = uid;
            return it;
        }

        [Test]
        public void A_trade_is_not_auto_equipped()
        {
            var w = EmptyWorld();
            var sturdy = Sword(1, (Stat.FlatDamage, 4f), (Stat.MaxHp, 60f));
            w.TryAutoEquip(sturdy, out _);
            // Offence +50%, survival −23%: more 전투력 on paper (+31%), but which half matters is the player's call.
            var sharp = Sword(2, (Stat.FlatDamage, 12f));
            var c = HeroStats.Compare(w.Hero, sharp);
            Assert.That(c.Arrow, Is.EqualTo(GearArrow.Trade));
            Assert.That(c.Power, Is.GreaterThan(HeroStats.BigUpAt));
            Assert.That(c.Offence, Is.GreaterThan(1f));
            Assert.That(c.Survival, Is.LessThan(1f));
            var back = w.TryAutoEquip(sharp, out bool ok);
            Assert.That(ok, Is.False);
            Assert.That(back, Is.SameAs(sharp));
            Assert.That(w.Hero.Gear[EquipSpot.Weapon], Is.SameAs(sturdy));
        }

        [Test]
        public void A_small_survival_loss_is_not_a_trade()
        {
            var w = EmptyWorld();
            w.TryAutoEquip(Sword(1, (Stat.FlatDamage, 4f), (Stat.MaxHp, 5f)), out _);
            // Survival −2.4%: under the 5% a trade needs, so the +50% offence makes it a plain ▲▲.
            var c = HeroStats.Compare(w.Hero, Sword(2, (Stat.FlatDamage, 12f)));
            Assert.That(c.Arrow, Is.EqualTo(GearArrow.BigUp));
            Assert.That(c.AutoEquips, Is.True);
        }

        [Test]
        public void Auto_equip_takes_one_percent_or_more()
        {
            var w = EmptyWorld();
            w.TryAutoEquip(Sword(1, (Stat.FlatDamage, 8f)), out _); // damage 20
            var hair = Sword(2, (Stat.FlatDamage, 8.1f));            // +0.5%: the old 0.1% rule took this
            Assert.That(HeroStats.Compare(w.Hero, hair).Arrow, Is.EqualTo(GearArrow.Same));
            w.TryAutoEquip(hair, out bool ok);
            Assert.That(ok, Is.False);
            var better = Sword(3, (Stat.FlatDamage, 8.5f));          // +2.5%
            Assert.That(HeroStats.Compare(w.Hero, better).Arrow, Is.EqualTo(GearArrow.Up));
            w.TryAutoEquip(better, out ok);
            Assert.That(ok, Is.True);
        }

        [Test]
        public void Arrows_follow_the_power_steps()
        {
            var w = EmptyWorld();
            w.TryAutoEquip(Sword(1, (Stat.FlatDamage, 8f)), out _); // damage 20
            Assert.That(HeroStats.Compare(w.Hero, Sword(2, (Stat.FlatDamage, 12f))).Arrow, Is.EqualTo(GearArrow.BigUp)); // +20%
            Assert.That(HeroStats.Compare(w.Hero, Sword(3, (Stat.FlatDamage, 9f))).Arrow, Is.EqualTo(GearArrow.Up));     // +5%
            Assert.That(HeroStats.Compare(w.Hero, Sword(4, (Stat.FlatDamage, 8f))).Arrow, Is.EqualTo(GearArrow.Same));
            Assert.That(HeroStats.Compare(w.Hero, Sword(5, (Stat.FlatDamage, 6f))).Arrow, Is.EqualTo(GearArrow.Down));   // −10%
            var high = Make("rusted_greatsword", req: 5, affixes: (Stat.FlatDamage, 50f));
            Assert.That(HeroStats.Compare(w.Hero, high).Arrow, Is.EqualTo(GearArrow.Locked));
        }

        [Test]
        public void Compare_is_the_ratio_of_power_after_to_power_now()
        {
            var w = EmptyWorld();
            w.TryAutoEquip(Sword(1, (Stat.FlatDamage, 4f), (Stat.MaxHp, 30f)), out _);
            // Offence, survival and a legendary power all move at once.
            var next = Make("rusted_greatsword", essence: "ember_echo", affixes: new[] { (Stat.FlatDamage, 9f), (Stat.CritChancePct, 3f) });
            float before = HeroStats.Power(w.Hero);
            var c = HeroStats.Compare(w.Hero, next, EquipSpot.Weapon);
            Assert.That(HeroStats.Upgrade(w.Hero, next, EquipSpot.Weapon, out float dps), Is.EqualTo(c.Power).Within(1e-5f));
            Assert.That(dps, Is.EqualTo(c.Offence - 1f).Within(1e-5f));
            Assert.That(c.Essences, Is.EqualTo(1));
            w.Hero.Gear.Equip(next, EquipSpot.Weapon);
            Assert.That(HeroStats.Power(w.Hero) / before, Is.EqualTo(c.Power).Within(1e-4f));
            Assert.That(before, Is.GreaterThan(0f));
        }

        [Test]
        public void A_ring_is_compared_with_the_spot_it_improves_most()
        {
            var w = EmptyWorld();
            var strong = Make("brass_ring", affixes: (Stat.DamagePct, 10f));
            var weak = Make("brass_ring", affixes: (Stat.DamagePct, 3f));
            w.Hero.Gear.Equip(strong, EquipSpot.Ring1);
            w.Hero.Gear.Equip(weak, EquipSpot.Ring2);
            var c = HeroStats.Compare(w.Hero, Make("brass_ring", affixes: (Stat.DamagePct, 6f)));
            Assert.That(c.Spot, Is.EqualTo(EquipSpot.Ring2));
            Assert.That(c.Arrow, Is.EqualTo(GearArrow.Up));
        }

        [Test]
        public void The_last_auto_equip_can_be_undone_and_stays_undone()
        {
            var w = EmptyWorld();
            var old = Sword(1, (Stat.FlatDamage, 4f));
            var better = Sword(2, (Stat.FlatDamage, 12f));
            w.TryAutoEquip(old, out _);
            w.Bag.Items.Add(w.TryAutoEquip(better, out bool ok)); // the replaced piece goes to the bag, as a drop does it
            Assert.That(ok, Is.True);
            Assert.That(w.LastAutoSwap.New, Is.SameAs(better));
            Assert.That(w.LastAutoSwap.Old, Is.SameAs(old));
            Assert.That(w.CanUndoAutoEquip, Is.True);

            Assert.That(w.UndoAutoEquip(), Is.True);
            Assert.That(w.Hero.Gear[EquipSpot.Weapon], Is.SameAs(old));
            Assert.That(w.Bag.Items, Is.EqualTo(new[] { better }));
            Assert.That(w.CanUndoAutoEquip, Is.False);
            // Auto-equip leaves the piece alone from now on, through a save too; wearing it by hand lifts that.
            w.TryAutoEquip(better, out ok);
            Assert.That(ok, Is.False);
            var loaded = SimWorld.FromSave(w.ToSave());
            Assert.That(loaded.AutoEquipDeclined, Does.Contain(better.Uid));
            Assert.That(w.EquipFromBag(better.Uid), Is.True);
            Assert.That(w.AutoEquipDeclined, Is.Empty);
        }

        [Test]
        public void Undo_buys_back_a_piece_that_was_salvaged_on_the_spot()
        {
            var w = new SimWorld(31) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.UseOpenFloor();
            var old = Sword(1, (Stat.FlatDamage, 4f));
            w.TryAutoEquip(old, out _);
            w.TryAutoEquip(Sword(2, (Stat.FlatDamage, 12f)), out _);
            // Nothing put the old piece in the bag and no ash was noted for it: there is nothing to give back.
            Assert.That(w.CanUndoAutoEquip, Is.False);
            w.LastAutoSwap.AshPaid = 3;
            w.Bag.AshShards = 2;
            Assert.That(w.CanUndoAutoEquip, Is.False, "not enough ash left to buy it back");
            w.Bag.AshShards = 5;
            Assert.That(w.UndoAutoEquip(), Is.True);
            Assert.That(w.Bag.AshShards, Is.EqualTo(2));
            Assert.That(w.Hero.Gear[EquipSpot.Weapon], Is.SameAs(old));
        }

        [Test]
        public void The_auto_equip_switch_survives_a_save_and_old_saves_have_it_on()
        {
            Assert.That(SimWorld.FromSave(new SimWorld(5) { AutoEquip = false }.ToSave()).AutoEquip, Is.False);
            Assert.That(SimWorld.FromSave(new SimWorld(5).ToSave()).AutoEquip, Is.True);
            var oldFile = UnityEngine.JsonUtility.FromJson<SaveData>("{\"Seed\":3,\"Level\":4}");
            Assert.That(oldFile.AutoEquip, Is.True);
            Assert.That(oldFile.AutoEquipDeclined, Is.Empty);
        }

        [Test]
        public void With_auto_equip_off_a_drop_goes_to_the_bag()
        {
            var w = EmptyWorld();
            w.AutoEquip = false;
            w.AutoSalvage = -1;
            var item = w.DropItem(w.Hero.Pos, Rarity.Rare);
            Assert.That(w.Hero.Gear.Spots, Has.All.Null);
            Assert.That(w.Bag.Items, Does.Contain(item));
        }
    }
}
