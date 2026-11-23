using System;

namespace Lanternkeeper.Sim
{
    /// <summary>GDD §5.2 태세: how boldly the hero fights. S cycles it.</summary>
    public enum Stance { Balanced, Charge, Careful }

    public sealed partial class SimWorld
    {
        public Stance Stance = Stance.Balanced;
        bool _resting;

        public static string StanceName(Stance s) => s == Stance.Charge ? "맹공" : s == Stance.Careful ? "신중" : "균형";

        /// <summary>돌격 hits harder and takes more; 신중 the other way round.</summary>
        float StanceDamageMul => Stance == Stance.Charge ? 1.12f : Stance == Stance.Careful ? 0.92f : 1f;
        float StanceTakenMul => Stance == Stance.Charge ? 1.12f : Stance == Stance.Careful ? 0.88f : 1f;

        /// <summary>
        /// Between packs, a hero low on life waits to recover (out-of-combat regen is 5%/s) before the next room:
        /// 균형 below 35% until 75%, 신중 below 70% until 95%, 돌격 never.
        /// </summary>
        bool RestBetweenPacks()
        {
            if (Stance == Stance.Charge) { _resting = false; return false; }
            float hp = Hero.Stats.Hp / Hero.Stats.MaxHp;
            float low = Stance == Stance.Careful ? 0.7f : 0.35f, high = Stance == Stance.Careful ? 0.95f : 0.75f;
            if (!_resting && hp < low) _resting = true;
            else if (_resting && hp >= high) _resting = false;
            return _resting;
        }

        public bool Resting => _resting;
    }
}
