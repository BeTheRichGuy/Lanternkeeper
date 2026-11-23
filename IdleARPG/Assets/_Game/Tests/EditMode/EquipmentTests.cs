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
    }
}
