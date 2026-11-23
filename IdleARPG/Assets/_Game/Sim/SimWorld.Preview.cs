using System;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// F1 → P 후반 미리보기: turns this hero into a late one — Lv60 + ember 40, every ring skill at 숙련 45, a full set of
    /// legendaries a little above act VI (the weapon also carrying copies, area and cooldown) — and moves to act VI. Meant for
    /// a run that will not be saved: the View turns saving off first.
    /// </summary>
    public sealed partial class SimWorld
    {
        public const int PreviewMastery = 45;

        public void ApplyLatePreview()
        {
            var h = Hero;
            h.Level = LevelCap;
            h.EmberLevel = EmberCap;
            var rng = new SimRng(0x9E3779B97F4A7C15UL);
            int ilvl = Math.Min(100, 1 + 5 * 10 + 15);
            int uid = int.MaxValue - 1;
            foreach (Slot slot in Enum.GetValues(typeof(Slot)))
                foreach (var spot in Equipment.SpotsFor(slot))
                {
                    var item = ItemGen.Roll(rng, uid--, ilvl, Rarity.Legendary, slot, 0f, Hero.ClassId);
                    item.ReqLevel = 1;
                    if (spot == EquipSpot.Weapon)
                    {
                        item.Affixes.Add(new AffixRoll { Stat = Stat.ProjectileCount, Value = 4, Tier = 8, Greater = true });
                        item.Affixes.Add(new AffixRoll { Stat = Stat.AreaPct, Value = 60, Tier = 8, Greater = true });
                        item.Affixes.Add(new AffixRoll { Stat = Stat.CooldownPct, Value = 30, Tier = 8 });
                    }
                    h.Gear.Equip(item, spot);
                }
            HeroStats.Recompute(h);
            h.Stats.Hp = h.Stats.MaxHp;
            foreach (var s in SkillBook.For(h.ClassId)) h.SkillUses[s.BaseId] = SkillBook.UsesFor(PreviewMastery);
            h.SkillUses[h.Ring.Basic.Id] = SkillBook.UsesFor(PreviewMastery);
            SetPreset(h.Preset);
            GoToFloor(ActLength * 5);
        }
    }
}
