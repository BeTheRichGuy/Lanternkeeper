using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// 환생 증강 카드 (희승 11-22; see <see cref="Augments"/>): the deal when a slot opens, the choice (by hand, or by itself
    /// after <see cref="Augments.AutoPickSeconds"/>), the one re-deal per slot, the per-branch skill sets a card of another
    /// branch switches to, and the cards' moment in combat (crit damage after a skill, a skill setting off another).
    /// </summary>
    public sealed partial class SimWorld
    {
        /// <summary>
        /// UX plan B7 (audit #9: the cards were picked for the player before they could be read): a first life's three
        /// cards wait for the player's hand; after a rebirth the idle default picks again.
        /// </summary>
        public static bool AugmentWaitsForPlayer(Hero h) => h.Rebirths == 0;

        /// <summary>The waiting deal is chosen for the player after <see cref="Augments.AutoPickSeconds"/> (off in tests).</summary>
        public bool AutoPickAugments = true;

        /// <summary>A breath after a pick before the next slot's deal (the screen plays the pick first).</summary>
        public const double DealPause = 2.0;
        double _nextDealAt;

        void TickAugments()
        {
            if (Hero.AugOffer == null)
            {
                if (Hero.Augments.Count < Augments.SlotsAt(Hero.Level) && Time >= _nextDealAt) DealAugments();
                return;
            }
            if (AutoPickAugments && !AugmentWaitsForPlayer(Hero) && Time - Hero.AugOfferAt >= Augments.AutoPickSeconds)
                ChooseAugment(Augments.BestPick(Hero.AugOffer, BranchInUse()));
        }

        void DealAugments()
        {
            Hero.AugOffer = Augments.Deal(Hero, _seed, Hero.AugDeals++);
            if (Hero.AugOffer.Length == 0) { Hero.AugOffer = null; return; }
            Hero.AugOfferAt = Time;
            Events.Add(new SimEvent { Type = SimEventType.AugmentOffered, TargetId = Hero.Id, Pos = Hero.Pos, Value = Hero.Augments.Count, Depth = Hero.AugRerolled ? 1 : 0 });
        }

        /// <summary>Take card <paramref name="index"/> of the waiting deal.</summary>
        public bool ChooseAugment(int index)
        {
            if (Hero.AugOffer == null || index < 0 || index >= Hero.AugOffer.Length) return false;
            var id = Hero.AugOffer[index];
            Hero.Augments.Add(id);
            Hero.AugOffer = null;
            Hero.AugRerolled = false;
            _nextDealAt = Time + DealPause;
            HeroStats.Recompute(Hero);
            var a = Augments.Get(id);
            Events.Add(new SimEvent { Type = SimEventType.AugmentChosen, TargetId = Hero.Id, Pos = Hero.Pos, SkillId = id, Value = a != null ? (int)a.Tier : 0 });
            // A card of another branch turns the build toward it (희승: "카드 구성에 따라 전환").
            if (a != null && a.Branch >= 0 && a.Branch != BranchInUse() && Hero.AugSwitchSets && Hero.TreeAuto) SwitchToBranch(a.Branch);
            return true;
        }

        /// <summary>The one re-deal of this slot (희승: "각 탭에 한해 1회에 한정해서 리롤").</summary>
        public bool RerollAugments()
        {
            if (Hero.AugOffer == null || Hero.AugRerolled) return false;
            Hero.AugRerolled = true;
            Hero.AugOffer = null;
            DealAugments();
            return Hero.AugOffer != null;
        }

        void ClearAugments()
        {
            Hero.Augments.Clear();
            Hero.AugOffer = null;
            Hero.AugRerolled = false;
            Hero.AugDeals = 0;
            Hero.AugCritUntil = 0;
            _nextDealAt = 0; // a new life deals at once
        }

        void LoadAugments(SaveData s)
        {
            Hero.Augments.Clear();
            if (s.Augments != null) foreach (var id in s.Augments) if (Augments.Get(id) != null && Hero.Augments.Count < Augments.MaxSlots) Hero.Augments.Add(id);
            Hero.AugOffer = null;
            if (s.AugOffer != null && s.AugOffer.Count > 0 && s.AugOffer.TrueForAll(id => Augments.Get(id) != null)) Hero.AugOffer = s.AugOffer.ToArray();
            Hero.AugRerolled = s.AugRerolled;
            Hero.AugDeals = Math.Max(0, s.AugDeals);
            Hero.AugOfferAt = Time;
            if (s.BranchSets != null) for (int i = 0; i < Hero.BranchSets.Length && i < s.BranchSets.Count; i++) Hero.BranchSets[i] = s.BranchSets[i] ?? "";
            Hero.AugSwitchSets = s.AugSwitchSets;
            HeroStats.Recompute(Hero);
        }

        // ---------- 갈래별 스킬 세트 ----------

        /// <summary>The skill-tree branch most of the ring's skills come from (0 when none).</summary>
        public int BranchInUse()
        {
            var t = SkillTree.For(Hero.ClassId);
            var n = new int[3];
            foreach (var s in Hero.Ring.Slots)
            {
                var node = s != null ? t.Get("s:" + s.BaseId) : null;
                if (node != null && node.Branch >= 0 && node.Branch < 3) n[node.Branch]++;
            }
            int best = 0;
            for (int b = 1; b < 3; b++) if (n[b] > n[best]) best = b;
            return best;
        }

        /// <summary>Keep the ring in use as the skill set of the branch it belongs to.</summary>
        public void SaveBranchSet()
        {
            int b = BranchInUse();
            for (int i = 0; i < 4; i++) Hero.BranchSets[b * 4 + i] = Hero.Ring.Slots[i]?.BaseId ?? "";
        }

        public bool HasBranchSet(int b) => b >= 0 && b < 3 && Hero.BranchSets[b * 4] != "";

        /// <summary>
        /// The skill set of branch <paramref name="b"/>: the one saved for it, else the recommended set that draws most on
        /// it. With 자동 분배 the tree regrows around it.
        /// </summary>
        public void SwitchToBranch(int b)
        {
            if (b < 0 || b > 2) return;
            if (HasBranchSet(b))
            {
                for (int i = 0; i < 4; i++) Hero.CustomSkills[i] = Hero.BranchSets[b * 4 + i] == "" ? null : Hero.BranchSets[b * 4 + i];
                Hero.UseCustomRing = true;
                RefreshRing();
                return;
            }
            var t = SkillTree.For(Hero.ClassId);
            var presets = SkillBook.PresetsFor(Hero.ClassId);
            int best = -1, bestN = 0;
            for (int p = 0; p < presets.Length; p++)
            {
                int n = 0;
                foreach (var id in presets[p].Ring) { var node = t.Get("s:" + id); if (node != null && node.Branch == b) n++; }
                if (n > bestN) { bestN = n; best = p; }
            }
            if (best >= 0) SetPreset(best);
        }

        // ---------- in combat ----------

        void AfterCastAugments(SkillDef skill, Enemy target)
        {
            if (Hero.AugCrit.TryGetValue(skill.BaseId, out var crit))
            {
                Hero.AugCritUntil = Time + Augments.CritOnUseSeconds;
                Hero.AugCritBonus = crit;
            }
            // By index: a kill inside may level the hero up, and the stats' recompute refills this list.
            for (int pi = 0; pi < Hero.AugProcs.Count; pi++)
            {
                var a = Hero.AugProcs[pi];
                if (a.SkillId != skill.BaseId || target == null || !target.Alive) continue;
                if (_combatRng.NextFloat() >= a.Value) continue;
                var proc = SkillBook.Get(a.ProcSkillId);
                foreach (var s in Hero.Ring.Slots) if (s != null && s.BaseId == a.ProcSkillId) proc = s; // the hero's own (evolved) version
                if (proc == null || !Damaging(proc.Shape)) continue;
                var mods = SkillMods.Neutral;
                int lv = SkillLevelOf(proc);
                float radius = proc.Radius * (proc.Ranged && proc.Shape == SkillShape.Single ? 1f : (1f + Hero.Derived.AreaPct) * SkillBook.LevelArea(lv));
                var c = new Cast
                {
                    Skill = proc, Mods = mods, Elem = proc.Elem, Power = proc.Power * SkillBook.LevelPower(lv) * 0.8f * (1f + Augments.PowerFor(Hero, proc.BaseId)),
                    Radius = radius, Slot = -1, Target = target,
                    Ctx = new HitCtx { Slot = -1, Direct = true, SkillId = proc.Id },
                };
                bool atTarget = proc.AtTarget || proc.Shape == SkillShape.Delayed || proc.Shape == SkillShape.Pull;
                Events.Add(new SimEvent { Type = SimEventType.Cast, SourceId = Hero.Id, TargetId = target.Id, Pos = atTarget ? target.Pos : Hero.Pos, From = Hero.Pos, SkillId = proc.Id, Elem = proc.Elem, Value = radius });
                Execute(c);
            }
        }
    }
}
