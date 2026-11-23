using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>Act boss techniques: telegraphed slams, summons, prayers; dodging by stance.</summary>
    public class BossTests
    {
        static (SimWorld w, Enemy boss) Arena(int floor, Stance stance)
        {
            var w = new SimWorld(91) { Stance = stance };
            w.GoToFloor(floor);
            var boss = w.Enemies.Single(e => e.Boss);
            boss.Aggro = true;
            boss.Stats.MaxHp = boss.Stats.Hp = 1e9f;
            w.Hero.Stats.MaxHp = w.Hero.Stats.Hp = 1e9f;
            w.Hero.Pos = boss.Pos + new Vec2(0.8f, 0f);
            return (w, boss);
        }

        [Test]
        public void Tomb_lord_draws_a_slam_first_and_it_lands_later()
        {
            var (w, _) = Arena(12, Stance.Charge);
            int warnTick = -1, slamTick = -1;
            for (int i = 0; i < 80 && slamTick < 0; i++)
            {
                w.Tick();
                if (warnTick < 0 && w.Events.Any(ev => ev.Type == SimEventType.Telegraph)) warnTick = i;
                if (w.Events.Any(ev => ev.Type == SimEventType.Slam)) slamTick = i;
            }
            Assert.That(warnTick, Is.GreaterThanOrEqualTo(0));
            Assert.That(slamTick - warnTick, Is.EqualTo(14), "1.4 s of warning");
        }

        [Test]
        public void Tomb_lord_raises_the_dead()
        {
            var (w, _) = Arena(12, Stance.Charge);
            for (int i = 0; i < 100; i++) w.Tick();
            Assert.That(w.Enemies.Count(e => e.Alive && !e.Boss), Is.GreaterThanOrEqualTo(4), "four risen around him");
        }

        [Test]
        public void Careful_hero_steps_out_of_the_slam_and_charge_takes_it()
        {
            float Taken(Stance stance)
            {
                var (w, boss) = Arena(12, stance);
                boss.Stats.Damage = 1000f;
                boss.AttackReadyAt = 1e9; // only the slam hurts
                float hp0 = w.Hero.Stats.Hp, hit = 0f;
                for (int i = 0; i < 60; i++)
                {
                    w.Tick();
                    foreach (var ev in w.Events) if (ev.Type == SimEventType.Hit && ev.TargetId == w.Hero.Id && ev.SourceId == boss.Id) hit += ev.Value;
                }
                return hit;
            }
            Assert.That(Taken(Stance.Charge), Is.GreaterThan(0f));
            Assert.That(Taken(Stance.Careful), Is.EqualTo(0f));
        }

        [Test]
        public void Great_tree_scatters_spores_and_calls_its_children()
        {
            var (w, _) = Arena(SimWorld.ActLength * 1 + SimWorld.ActBossFloorInAct, Stance.Charge);
            int warns = 0, children = 0;
            for (int i = 0; i < 160; i++)
            {
                w.Tick();
                warns += w.Events.Count(ev => ev.Type == SimEventType.Telegraph && ev.SkillId == "queen_spores");
                children += w.Events.Count(ev => ev.Type == SimEventType.BossCast && ev.SkillId == "queen_children");
            }
            Assert.That(warns, Is.GreaterThanOrEqualTo(2), "at least the one on the hero, plus scattered ones");
            Assert.That(children, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Molten_giant_pours_and_casts_a_star()
        {
            var (w, _) = Arena(SimWorld.ActLength * 4 + SimWorld.ActBossFloorInAct, Stance.Charge);
            int pour = 0, star = 0;
            for (int i = 0; i < 160; i++)
            {
                w.Tick();
                pour += w.Events.Count(ev => ev.Type == SimEventType.Telegraph && ev.SkillId == "giant_pour");
                star += w.Events.Count(ev => ev.Type == SimEventType.Telegraph && ev.SkillId == "giant_star");
            }
            Assert.That(pour, Is.GreaterThanOrEqualTo(1));
            Assert.That(star, Is.GreaterThanOrEqualTo(2));
        }

        [TestCase(3)] // 얼음 속 거인: 고드름 낙하, 서리 후광
        [TestCase(5)] // 꺼진 별의 사도
        public void Star_kit_bosses_drop_stars_and_flare_a_halo(int act)
        {
            var (w, _) = Arena(SimWorld.ActLength * act + SimWorld.ActBossFloorInAct, Stance.Charge);
            int stars = 0, halo = 0;
            for (int i = 0; i < 160; i++)
            {
                w.Tick();
                stars += w.Events.Count(ev => ev.Type == SimEventType.Telegraph && ev.SkillId == "apostle_star");
                halo += w.Events.Count(ev => ev.Type == SimEventType.Telegraph && ev.SkillId == "apostle_halo");
            }
            Assert.That(stars, Is.GreaterThanOrEqualTo(2));
            Assert.That(halo, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Pulse_lord_sends_three_waves()
        {
            var (w, _) = Arena(SimWorld.ActLength * 2 + SimWorld.ActBossFloorInAct, Stance.Charge);
            int warns = 0;
            for (int i = 0; i < 60; i++) { w.Tick(); warns += w.Events.Count(ev => ev.Type == SimEventType.Telegraph); }
            Assert.That(warns, Is.EqualTo(3));
        }

        [Test]
        public void Boss_casts_carry_the_technique_name()
        {
            var (w, boss) = Arena(SimWorld.ActBossFloorInAct, Stance.Charge);
            string text = null;
            for (int i = 0; i < 120 && text == null; i++)
            {
                w.Tick();
                foreach (var ev in w.Events) if (ev.Type == SimEventType.BossCast) text = ev.Text;
            }
            Assert.That(text, Is.EqualTo(boss.Def.SpecialName));
        }
    }
}
