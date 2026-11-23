using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// Act boss techniques (GDD §6.3, §26.4): the danger is drawn on the floor first (a telegraph), then lands.
    /// 무덤 군주 slams where the hero stands and raises the dead; 익사한 수도원장 sends a tide in three waves and prays
    /// to mend himself. The hero steps out of a telegraph unless the stance is 돌격 (GDD §5.2), who takes it.
    /// </summary>
    public sealed partial class SimWorld
    {
        sealed class Telegraph
        {
            public Vec2 Pos;
            public float Radius, Damage;
            public double At;
            public int Source;
            public string Fx;
        }

        readonly List<Telegraph> _telegraphs = new List<Telegraph>();

        void BossThink(Enemy e)
        {
            if (!e.Boss || !IsActBossFloor || !e.Aggro || Time < e.StunUntil) return;
            if (e.NextSlam == 0) { e.NextSlam = Time + 4.0; e.NextSpecial = Time + 9.0; }
            // Techniques come with the boss's kit (EnemyDef.Kit), so a new act boss picks one of the five sets.
            string kit = e.Def.Kit;
            bool abbot = kit == "tide", queen = kit == "spores", giant = kit == "pour", apostle = kit == "stars";
            if (Time >= e.NextSlam)
            {
                e.NextSlam = Time + (abbot ? 8.0 : queen ? 7.0 : giant ? 8.5 : apostle ? 7.5 : 9.0);
                if (apostle)
                {
                    // 별 낙하: dead stars fall one after another, the first on the hero.
                    AddTelegraph(e, Hero.Pos, 1.4f, e.Stats.Damage * 1.7f, 1.4, "apostle_star");
                    for (int i = 0; i < 4; i++)
                    {
                        float a = _combatRng.NextFloat() * (float)Math.PI * 2f;
                        var p = Hero.Pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * (1.5f + _combatRng.NextFloat() * 2f);
                        if (Floor.IsWalkable(p)) AddTelegraph(e, p, 1.4f, e.Stats.Damage * 1.7f, 1.7 + 0.3 * i, "apostle_star");
                    }
                }
                else if (giant)
                {
                    // 쇳물 내려찍기: the widest, heaviest slam of the descent.
                    AddTelegraph(e, Hero.Pos, 2.6f, e.Stats.Damage * 2.8f, 1.6, "giant_pour");
                }
                else if (queen)
                {
                    // 포자 폭풍: four spore bursts scattered around the hero, one right on it.
                    AddTelegraph(e, Hero.Pos, 1.3f, e.Stats.Damage * 1.6f, 1.3, "queen_spores");
                    for (int i = 0; i < 3; i++)
                    {
                        float a = _combatRng.NextFloat() * (float)Math.PI * 2f;
                        var p = Hero.Pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * (1.8f + _combatRng.NextFloat() * 1.5f);
                        if (Floor.IsWalkable(p)) AddTelegraph(e, p, 1.3f, e.Stats.Damage * 1.6f, 1.3 + 0.25 * (i + 1), "queen_spores");
                    }
                }
                else if (abbot)
                {
                    // 해일: three waves rolling from the abbot through the hero, a beat apart.
                    var dir = Hero.Pos - e.Pos;
                    dir = dir.LengthSq > 1e-6f ? dir.Normalized : new Vec2(1f, 0f);
                    for (int i = 0; i < 3; i++)
                        AddTelegraph(e, Hero.Pos + dir * ((i - 1) * 1.6f), 1.5f, e.Stats.Damage * 1.8f, 1.2 + i * 0.35, "abbot_tide");
                }
                else AddTelegraph(e, Hero.Pos, 2.2f, e.Stats.Damage * 2.5f, 1.4, "boss_grave_slam");
            }
            if (Time >= e.NextSpecial)
            {
                e.NextSpecial = Time + (abbot ? 14.0 : giant ? 13.0 : apostle ? 14.0 : 15.0);
                if (apostle)
                {
                    // 재의 후광: the halo of ash flares around him; only distance saves the hero.
                    AddTelegraph(e, e.Pos, 3.4f, e.Stats.Damage * 2.2f, 2.0, "apostle_halo");
                    Events.Add(new SimEvent { Type = SimEventType.BossCast, SourceId = e.Id, Pos = e.Pos, SkillId = "apostle_halo", Text = e.Def.SpecialName });
                }
                else if (giant)
                {
                    // 새 별 주조: molten points of a star burst around him, the hero's spot last.
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * (float)Math.PI / 3f;
                        var p = e.Pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * 2.4f;
                        if (Floor.IsWalkable(p)) AddTelegraph(e, p, 1.1f, e.Stats.Damage * 1.5f, 1.2 + 0.12 * i, "giant_star");
                    }
                    AddTelegraph(e, Hero.Pos, 1.2f, e.Stats.Damage * 1.5f, 2.0, "giant_star");
                    Events.Add(new SimEvent { Type = SimEventType.BossCast, SourceId = e.Id, Pos = e.Pos, SkillId = "giant_star", Text = e.Def.SpecialName });
                }
                else if (queen)
                {
                    // 아이들아: her children crawl out of the mycelium to shield her.
                    var def = ActNow.Ranks[Math.Min(1, ActNow.Ranks.Length - 1)];
                    var (hpMul, dmgMul) = FloorScaling();
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * (float)Math.PI * 2f / 5f;
                        var p = e.Pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * 1.2f;
                        if (!Floor.IsWalkable(p)) p = e.Pos;
                        var s = SpawnEnemy(def, p, hpMul * PackHp, dmgMul * PackDmg, true);
                        s.Aggro = true;
                        s.Summoned = true;
                        s.HomeRoom = e.HomeRoom;
                    }
                    Events.Add(new SimEvent { Type = SimEventType.BossCast, SourceId = e.Id, Pos = e.Pos, SkillId = "queen_children", Text = e.Def.SpecialName });
                }
                else if (abbot)
                {
                    // 익사의 기도: he is still praying, and the water answers.
                    e.Stats.Hp = Math.Min(e.Stats.MaxHp, e.Stats.Hp + e.Stats.MaxHp * 0.05f);
                    Events.Add(new SimEvent { Type = SimEventType.BossCast, SourceId = e.Id, Pos = e.Pos, SkillId = "abbot_prayer", Text = e.Def.SpecialName });
                }
                else
                {
                    // 죽은 자여, 일어나라: four of the act's soldiers climb out around him.
                    var def = ActNow.Ranks[Math.Min(1, ActNow.Ranks.Length - 1)];
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * (float)Math.PI / 2f + 0.4f;
                        var p = e.Pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * 1.4f;
                        if (!Floor.IsWalkable(p)) p = e.Pos;
                        // Floor-strength soldiers: tied to the boss's own life they fed the death chain (ChainPower reads
                        // the victim's max life) and the boss died to its own summons.
                        var (hpMul, dmgMul) = FloorScaling();
                        var s = SpawnEnemy(def, p, hpMul * PackHp, dmgMul * PackDmg, true);
                        s.Aggro = true;
                        s.Summoned = true;
                        s.HomeRoom = e.HomeRoom;
                    }
                    Events.Add(new SimEvent { Type = SimEventType.BossCast, SourceId = e.Id, Pos = e.Pos, SkillId = "raise_dead", Text = e.Def.SpecialName });
                }
            }
        }

        void AddTelegraph(Enemy source, Vec2 at, float radius, float damage, double delay, string fx)
        {
            _telegraphs.Add(new Telegraph { Pos = at, Radius = radius, Damage = damage, At = Time + delay, Source = source.Id, Fx = fx });
            Events.Add(new SimEvent { Type = SimEventType.Telegraph, SourceId = source.Id, Pos = at, Value = radius, Depth = (int)Math.Round(delay / Dt), SkillId = fx });
        }

        void TickTelegraphs()
        {
            for (int i = _telegraphs.Count - 1; i >= 0; i--)
            {
                var t = _telegraphs[i];
                if (Time < t.At) continue;
                _telegraphs.RemoveAt(i);
                Events.Add(new SimEvent { Type = SimEventType.Slam, SourceId = t.Source, Pos = t.Pos, Value = t.Radius, SkillId = t.Fx });
                if (!Hero.Alive || Vec2.Dist(Hero.Pos, t.Pos) > t.Radius) continue;
                float taken = IncomingDamage(t.Damage);
                Hero.Stats.Hp -= taken;
                Events.Add(new SimEvent { Type = SimEventType.Hit, SourceId = t.Source, TargetId = Hero.Id, Pos = Hero.Pos, Value = taken });
                var src = _enemies.Find(x => x.Id == t.Source && x.Alive);
                if (src != null) OnHeroHit(src, taken);
            }
        }

        /// <summary>Step out of a drawn danger (all stances but 돌격). True while dodging, which spends the hero's turn.</summary>
        bool DodgeTelegraph()
        {
            if (Stance == Stance.Charge) return false;
            foreach (var t in _telegraphs)
            {
                if (Vec2.Dist(Hero.Pos, t.Pos) > t.Radius + 0.3f) continue;
                var away = Hero.Pos - t.Pos;
                away = away.LengthSq > 1e-4f ? away.Normalized : new Vec2(-Hero.Facing.Y, Hero.Facing.X);
                var goal = t.Pos + away * (t.Radius + 0.8f);
                var step = Slide(Hero.Pos, Vec2.MoveToward(Hero.Pos, goal, Hero.Stats.MoveSpeed * ShrineMoveMul * Dt));
                if (Vec2.Dist(step, Hero.Pos) < 1e-4f) continue; // cornered: fight on
                Hero.Facing = (step - Hero.Pos).Normalized;
                Hero.Pos = step;
                return true;
            }
            return false;
        }

        void ClearTelegraphs() => _telegraphs.Clear();

        bool InTelegraph(Vec2 p)
        {
            foreach (var t in _telegraphs) if (Vec2.Dist(p, t.Pos) <= t.Radius + 0.3f) return true;
            return false;
        }

        /// <summary>
        /// Having stepped out, do not walk straight back in after a target standing in the danger: wait for it to land
        /// (돌격 walks in regardless). Without this the hero bobbed in and out for the whole wind-up.
        /// </summary>
        bool HoldOutsideTelegraph(Vec2 goal) => Stance != Stance.Charge && _telegraphs.Count > 0 && !InTelegraph(Hero.Pos) && InTelegraph(goal);
    }
}
