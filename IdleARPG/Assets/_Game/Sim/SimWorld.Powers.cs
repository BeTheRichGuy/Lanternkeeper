using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// 고유·전설 효과 (희승 11-24: "디아블로에서 먹었을 때는 기분 좋았는데, 이건 양산형 RPG 아이템 같아"): powers that change
    /// how a class's skills play — a burst, extra bolts, a burning trail — instead of a hidden stat. Each one shows through
    /// events the view already draws (Cast blasts, Shot, Ground, Hit). Ids and texts: <see cref="ItemData.Essences"/>.
    /// </summary>
    public sealed partial class SimWorld
    {
        /// <summary>Fields the powers lay stop here (a dense pack's chain pops could otherwise pave the room).</summary>
        const int PowerGroundCap = 24;
        int _hawkId, _silentHits, _starCasts, _kingsEchoTick = -1;
        Element _kingsEchoMark;
        double _timeEaterReady;

        void ResetPowerState()
        {
            _hawkId = 0;
            _kingsEchoTick = -1;
        }

        static HitCtx PowerCtx(Vec2 from, string skillId)
        {
            var c = HitCtx.None;
            c.From = from;
            c.SkillId = skillId;
            return c;
        }

        /// <summary>Powers that work by themselves, once a tick.</summary>
        void TickPowers()
        {
            if (_kingsEchoTick >= 0 && TickCount >= _kingsEchoTick) { _kingsEchoTick = -1; KingsEcho(); }
            // 식지 않는 용광로: the whirl leaves fire where it passed.
            if (Whirling && TickCount % 6 == 0 && Hero.Has("furnace_trail") && _grounds.Count < PowerGroundCap)
                AddGround(Hero.Pos, 1.2f, 0.2f, Element.Ember, 3f, fx: "evo_meteor_burn");
            if (TickCount % 15 == 0 && Hero.Has("gale_ward")) GaleWard();
            if (TickCount % 10 == 7 && Hero.Has("orbit_stars")) PowerShots(Hero.Pos, null, 3, 7f, 1f, Element.None, "star_orb", 1.2f);
            if (TickCount % 40 == 0 && Hero.Has("hawk_mark")) HawkMark();
        }

        /// <summary>After a ring skill, the ultimate or the basic attack went off.</summary>
        void PowerCast(Cast c, bool scorch)
        {
            // 떨어진 별의 지팡이: every third ring skill calls a star down on its target.
            if (c.Slot >= 0 && Hero.Has("star_fall") && ++_starCasts >= 3)
            {
                _starCasts = 0;
                _blasts.Add(new Blast { AtTick = TickCount + 6, Pos = c.Target.Pos, Radius = 2.2f, Power = 3f, Elem = Element.Ember, Slot = -1, Mods = SkillMods.Neutral, Fx = "meteor" });
            }
            // 심판자의 맹세: judgment comes round a second sooner.
            if (c.Skill.BaseId == "judgment" && c.Slot >= 0 && Hero.Has("judges_verdict")) Hero.Ring.Reduce(c.Slot, 1.0);
            // 재의 왕관: a scorching cast rains cinders on three around.
            if (scorch && Hero.Has("cinder_crown"))
            {
                var near = new List<Enemy>();
                for (int k = 0; k < 3; k++)
                {
                    var t = NearestExcept(Hero.Pos, 5f, near);
                    if (t == null) break;
                    near.Add(t);
                    _blasts.Add(new Blast { AtTick = TickCount + 5 + 2 * k, Pos = t.Pos, Radius = 1.6f, Power = 1.8f, Elem = Element.Ember, Slot = -1, Mods = SkillMods.Neutral, Fx = "meteor" });
                }
            }
            // 순례자의 손아귀: the blessed hammer comes round twice more.
            if (c.Skill.BaseId == "blessed_hammer" && Hero.Has("pilgrim_hammers"))
                for (int k = 1; k <= 2; k++)
                    _blasts.Add(new Blast { AtTick = TickCount + 3 * k, Pos = Hero.Pos, Radius = c.Radius, Power = 0.5f, Elem = c.Elem, Slot = c.Slot, Mods = c.Mods, Fx = c.Skill.Id + "_echo" });
        }

        /// <summary>After one blow of a skill landed on <paramref name="e"/> (it may have died).</summary>
        void PowerStrike(Cast c, Enemy e)
        {
            var id = c.Skill.BaseId;
            if ((id == "ember_slash" || id == "forge_strike") && Hero.Has("grave_burst"))
                FireHeroBlast(e.Pos, 2f, e.Alive ? 1f : 2f, Element.Ember, "cinder_echo_burst");
            else if (id == "judgment" && Hero.Has("judges_verdict"))
            {
                var near = new List<Enemy> { e };
                for (int k = 0; k < 4; k++)
                {
                    var t = NearestExcept(e.Pos, 4.5f, near);
                    if (t == null) break;
                    near.Add(t);
                    _blasts.Add(new Blast { AtTick = TickCount + 2 + 2 * k, Pos = t.Pos, Radius = 1.5f, Power = 1.6f, Elem = Element.Shock, Slot = -1, Mods = SkillMods.Neutral, Fx = "thunderstorm_bolt" });
                }
            }
            else if (id == "frost_brand" && Hero.Has("brand_burst"))
                FireHeroBlast(e.Pos, 1.8f, 0.5f, Element.Frost, "frost_nova");
            else if (id == "ice_bolt" && Hero.Has("ice_splinter"))
                PowerShots(e.Pos, e, 3, 3f, 0.6f, Element.Frost, "ice_bolt");
        }

        /// <summary>After a single-target skill's blows (melee or a shot) are done.</summary>
        void PowerSingle(Cast c)
        {
            var id = c.Skill.BaseId;
            if (id == "zeal" && Hero.Has("zeal_arc")) PowerShots(c.Target.Pos, c.Target, 4, 3.5f, 0.8f, Element.Shock, "shock_arc");
            else if (id == "shield_bash" && Hero.Has("bash_ricochet")) PowerShots(c.Target.Pos, c.Target, 3, 3.5f, 0.8f, Element.Frost, "shield_throw");
            else if (id == "gale_volley" && Hero.Has("volley_pierce"))
            {
                // The arrow goes through: the two behind take the same.
                var hit = new List<Enemy> { c.Target };
                var from = c.Target.Pos;
                for (int k = 0; k < 2; k++)
                {
                    var next = NearestExcept(from, 2.5f, hit);
                    if (next == null) return;
                    hit.Add(next);
                    Events.Add(new SimEvent { Type = SimEventType.Shot, SourceId = Hero.Id, TargetId = next.Id, From = from, Pos = next.Pos, SkillId = c.Skill.Id, Depth = 1 });
                    var ctx = c.Ctx; ctx.From = from;
                    Strike(c, next, 1f, ctx);
                    from = next.Pos;
                }
            }
        }

        /// <summary>After a delayed blast went off.</summary>
        void PowerBlast(Blast b)
        {
            if (_grounds.Count >= PowerGroundCap) return;
            if (b.Fx == "meteor" && Hero.Has("meteor_scorch")) AddGround(b.Pos, b.Radius, 0.3f, Element.Ember, 3f, fx: "evo_meteor_burn");
            else if (b.Fx == "arrow_rain_hit" && Hero.Has("rain_rot")) AddGround(b.Pos, 1f, 0.25f, Element.Rot, 2f, fx: "viper");
        }

        /// <summary>A death-chain link went off at <paramref name="pos"/>.</summary>
        void PowerChainPop(Vec2 pos)
        {
            if (Hero.Has("wick_flame") && _grounds.Count < PowerGroundCap) AddGround(pos, 1.5f, 0.5f, Element.None, 2f, fx: "evo_meteor_burn");
        }

        void PowerKill(Enemy e)
        {
            if (e.Id == _hawkId)
            {
                _hawkId = 0;
                if (Hero.Has("hawk_mark")) PowerShots(e.Pos, null, 5, 5f, 0.6f, Element.None, "chain_arrow");
            }
            if (Hero.Has("time_eater") && Time >= _timeEaterReady)
            {
                // 시간을 삼킨 허리띠: time stops around the kill (bosses shrug it off).
                _timeEaterReady = Time + 8.0;
                Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = 0, Pos = e.Pos, From = Hero.Pos, SkillId = "time_stop", Elem = Element.Void, Value = 4f });
                foreach (var o in InRadius(e.Pos, 4f))
                {
                    if (o.Boss) continue;
                    o.FrozenUntil = Math.Max(o.FrozenUntil, Time + 2.0);
                    o.StunUntil = Math.Max(o.StunUntil, Time + 2.0);
                    o.StoredMul = Math.Max(o.StoredMul, 1.5f);
                }
            }
        }

        void PowerHeroHit()
        {
            // 침묵의 서약: ten blows taken break the silence.
            if (!Hero.Has("silent_bastion") || ++_silentHits < 10) return;
            _silentHits = 0;
            Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + Hero.Stats.MaxHp * 0.05f);
            FireHeroBlast(Hero.Pos, 3f, 2.5f, Element.Shock, "unbroken_vow_burst");
        }

        /// <summary>죽은 왕의 인장: after a 대공명, mark it to ring again a second later.</summary>
        void PowerGrand(Element mark)
        {
            if (!Hero.Has("kings_echo")) return;
            _kingsEchoTick = TickCount + 10;
            _kingsEchoMark = mark;
        }

        void KingsEcho()
        {
            _inGrand = true;
            var hit = Elements.Next(_kingsEchoMark);
            Events.Add(new SimEvent { Type = SimEventType.GrandResonance, SourceId = Hero.Id, Pos = Hero.Pos, Elem = _kingsEchoMark, Value = GrandRadius });
            foreach (var e in InRadius(Hero.Pos, GrandRadius))
            {
                e.Mark = _kingsEchoMark;
                e.MarkSlot = -1;
                ApplyHit(e, RollHeroDamage(2f, hit, SkillMods.Neutral, e), hit, false, 0, 0, HitCtx.None);
            }
            _inGrand = false;
        }

        /// <summary>바람을 걷는 자: whoever comes within two cells is blown a cell back.</summary>
        void GaleWard()
        {
            var near = InRadius(Hero.Pos, 2f);
            if (near.Count == 0) return;
            Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = 0, Pos = Hero.Pos, From = Hero.Pos, SkillId = "whirling_blades", Elem = Element.Shock, Value = 2f });
            PullToward(Hero.Pos, 2f, -1f, null);
            var ctx = PowerCtx(Hero.Pos, "whirling_blades");
            foreach (var e in near) ApplyHit(e, RollHeroDamage(1.2f, Element.Shock, SkillMods.Neutral, e), Element.Shock, false, 0, 0, ctx);
        }

        /// <summary>매의 눈: the toughest enemy near is marked (+25% damage taken) and pays out arrows when it dies.</summary>
        void HawkMark()
        {
            Enemy best = null;
            foreach (var e in InRadius(Hero.Pos, 7f)) if (best == null || e.Stats.Hp > best.Stats.Hp) best = e;
            if (best == null) return;
            _hawkId = best.Id;
            best.HuntedPct = Time < best.HuntedUntil ? Math.Max(best.HuntedPct, 0.25f) : 0.25f;
            best.HuntedUntil = Math.Max(best.HuntedUntil, Time + 4.0);
            Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = 0, TargetId = best.Id, Pos = best.Pos, From = Hero.Pos, SkillId = "hunters_mark", Elem = Element.Void, Value = 1f });
        }

        /// <summary>
        /// Up to <paramref name="count"/> shots from <paramref name="from"/> at the nearest enemies other than <paramref name="skip"/>;
        /// with <paramref name="splash"/> each also hits the others that close to where it lands.
        /// </summary>
        void PowerShots(Vec2 from, Enemy skip, int count, float range, float power, Element elem, string shot, float splash = 0f)
        {
            var hit = new List<Enemy>();
            if (skip != null) hit.Add(skip);
            for (int k = 0; k < count; k++)
            {
                var t = NearestExcept(from, range, hit);
                if (t == null) break;
                hit.Add(t);
                Events.Add(new SimEvent { Type = SimEventType.Shot, SourceId = Hero.Id, TargetId = t.Id, From = from, Pos = t.Pos, SkillId = shot, Depth = 1 });
                var at = t.Pos;
                ApplyHit(t, RollHeroDamage(power, elem, SkillMods.Neutral, t), elem, false, 0, 0, PowerCtx(from, shot));
                if (splash > 0f)
                    foreach (var o in InRadius(at, splash))
                        if (o != t) ApplyHit(o, RollHeroDamage(power, elem, SkillMods.Neutral, o), elem, false, 0, 0, PowerCtx(at, shot));
            }
        }
    }
}
