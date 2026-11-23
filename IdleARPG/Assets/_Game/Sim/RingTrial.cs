using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// 고리 시험 (재기획 §5.6): every order of the ring's skills is hunted for the same stretch from the same saved moment
    /// — the sim is deterministic, so the only difference between runs is the order — and ranked by the damage dealt.
    /// Glyphs stay with their slot. The world given is never touched (each run starts from a copy of its save).
    /// </summary>
    public static class RingTrial
    {
        public sealed class Result
        {
            public string[] Order;
            public double Damage;
            public int Kills;
        }

        /// <param name="ticks">How long each order hunts (10 ticks a second; 400 = 40 s).</param>
        public static List<Result> Run(SimWorld world, int ticks = 400)
        {
            var save = world.ToSave();
            var start = SimWorld.FromSave(save);
            start.BeginCustomRing();
            var skills = (string[])start.Hero.CustomSkills.Clone();
            int open = SkillBook.SlotsAt(start.Hero.IntegratedLevel);
            var seen = new HashSet<string>();
            var results = new List<Result>();
            var head = new string[open];
            System.Array.Copy(skills, head, open);
            foreach (var part in Permutations(head))
            {
                var order = (string[])skills.Clone();
                System.Array.Copy(part, order, open);
                if (!seen.Add(string.Join("|", order))) continue; // empty slots make repeats
                var w = SimWorld.FromSave(save);
                w.SetCustomOrder(order);
                var r = new Result { Order = order };
                for (int t = 0; t < ticks; t++)
                {
                    w.Tick();
                    foreach (var ev in w.Events)
                    {
                        if (ev.Type == SimEventType.Hit && ev.SourceId == w.Hero.Id && ev.Value > 0f) r.Damage += ev.Value;
                        else if (ev.Type == SimEventType.Death && ev.TargetId != w.Hero.Id) r.Kills++;
                    }
                }
                results.Add(r);
            }
            results.Sort((a, b) => b.Damage.CompareTo(a.Damage));
            return results;
        }

        /// <summary>Heap's algorithm, iterative: every order of the four slots.</summary>
        static IEnumerable<string[]> Permutations(string[] items)
        {
            var a = (string[])items.Clone();
            var c = new int[a.Length];
            yield return (string[])a.Clone();
            int i = 0;
            while (i < a.Length)
            {
                if (c[i] < i)
                {
                    int j = i % 2 == 0 ? 0 : c[i];
                    (a[j], a[i]) = (a[i], a[j]);
                    yield return (string[])a.Clone();
                    c[i]++;
                    i = 0;
                }
                else { c[i] = 0; i++; }
            }
        }
    }
}
