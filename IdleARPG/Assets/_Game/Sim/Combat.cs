using System;

namespace Lanternkeeper.Sim
{
    public enum Element { None, Ember, Frost, Shock, Rot, Void }

    /// <summary>
    /// Forward resonances (a mark detonated by the next element in the cycle) are Shatter..Collapse;
    /// Spark is any other pair, Stack the same element again (docs/specs/2026-11-19-skill-rune-synergy-design.md §2).
    /// </summary>
    public enum Resonance { None, Shatter, Overload, Plague, Devour, Collapse, Spark, Stack }

    public static class Damage
    {
        /// <summary>GDD §5.4: base × (1 + Σadditive) × Πmultiplicative × crit × (1 + vulnerability) × (1 − resist).</summary>
        public static float Compute(float baseDmg, float additivePct, float multiplicative, bool crit, float critMult, float vuln, float resist)
        {
            return baseDmg * (1f + additivePct) * multiplicative * (crit ? critMult : 1f) * (1f + vuln) * (1f - resist);
        }
    }

    public static class Elements
    {
        /// <summary>The cycle: 잿불 → 서리 → 전격 → 부패 → 공허 → 잿불.</summary>
        public static Element Next(Element e)
        {
            switch (e)
            {
                case Element.Ember: return Element.Frost;
                case Element.Frost: return Element.Shock;
                case Element.Shock: return Element.Rot;
                case Element.Rot: return Element.Void;
                case Element.Void: return Element.Ember;
                default: return Element.None;
            }
        }

        /// <summary>The big resonance a mark makes when the next element detonates it.</summary>
        public static Resonance Forward(Element mark)
        {
            switch (mark)
            {
                case Element.Ember: return Resonance.Shatter;
                case Element.Frost: return Resonance.Overload;
                case Element.Shock: return Resonance.Plague;
                case Element.Rot: return Resonance.Devour;
                case Element.Void: return Resonance.Collapse;
                default: return Resonance.None;
            }
        }

        public static bool IsForward(Resonance r) => r >= Resonance.Shatter && r <= Resonance.Collapse;

        /// <summary><paramref name="mark"/> is already on the enemy, <paramref name="hit"/> arrives.</summary>
        public static Resonance Resolve(Element mark, Element hit)
        {
            if (mark == Element.None || hit == Element.None) return Resonance.None;
            if (mark == hit) return Resonance.Stack;
            return Next(mark) == hit ? Forward(mark) : Resonance.Spark;
        }

        /// <summary>Lifetime forward-resonance counts that unlock mastery tiers I/II/III (§2.4).</summary>
        public static int MasteryTier(int count) => count >= 10000 ? 3 : count >= 2000 ? 2 : count >= 300 ? 1 : 0;
        public static float MasteryBonus(int tier) => tier >= 2 ? 0.30f : tier == 1 ? 0.15f : 0f;
    }
}
