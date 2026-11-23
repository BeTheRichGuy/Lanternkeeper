using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Floating damage numbers, rationed for a screen with dozens of hits a second: big hits (3× the hero's base damage
    /// and up: crits, resonances, deep chains) in their element's colour, popping; of the ordinary hits only the
    /// largest per tick, small and grey; every hit on a boss; what the hero takes in red. V toggles them (saved).
    /// 재기획 §4: hits on one target within half a second add into the number already up (it counts up and pops again)
    /// instead of stacking new lines, and every number carries a dark drop outline so it reads on any floor.
    /// </summary>
    public sealed class DamageNumbers
    {
        // 1122_numbers_loot.md: a cap on what is up at once, with room kept for the big ones.
        const int MaxLive = 32, OrdinaryCap = 20, OrderOnTop = 32150; // above the HUD, under the cut-in (32200), fades (32300) and panels
        sealed class Num { public TextMesh Text, Shadow; public MeshRenderer Mr, ShadowMr; public Vector3 Origin, Drift; public float T, Life, Size, Value, Pop; public Color Color; public bool Big; public int Target; }
        const float MergeWindow = 0.5f;
        static readonly Vector3 ShadowOffset = new Vector3(1f / 64f, -1f / 64f, 0f);

        readonly Transform _root;
        readonly Font _font;
        readonly List<Num> _nums = new List<Num>();
        /// <summary>0 all, 1 important only (big hits, bosses, hits on the hero), 2 off.</summary>
        public int Mode { get; private set; }
        public bool Enabled => Mode != 2;
        public string ModeName => Mode == 0 ? "전체" : Mode == 1 ? "주요 수치만" : "끄기";

        public DamageNumbers(Transform root)
        {
            _root = root;
            _font = UiFont.Title;
            // Old saves stored 1/0 under dmg_numbers; the mode lives under dmg_numbers_mode now.
            // A new player starts on 주요 수치만 (big hits, bosses, hits on the hero): every hit was a wall of digits.
            try { Mode = PlayerPrefs.HasKey("dmg_numbers_mode") ? Mathf.Clamp(PlayerPrefs.GetInt("dmg_numbers_mode"), 0, 2) : PlayerPrefs.HasKey("dmg_numbers") ? (PlayerPrefs.GetInt("dmg_numbers") == 1 ? 0 : 2) : 1; }
            catch { Mode = 0; }
        }

        public void Toggle()
        {
            Mode = (Mode + 1) % 3;
            PlayerPrefs.SetInt("dmg_numbers_mode", Mode);
            if (!Enabled) foreach (var n in _nums) { n.T = n.Life; n.Mr.enabled = false; n.ShadowMr.enabled = false; }
        }

        /// <summary>Korean units (만 · 억 · 조), three figures at most: 16,400 → 1.6만, 3,240,000 → 324만.</summary>
        public static string Short(float v)
        {
            if (v >= 1e12f) return (v / 1e12f).ToString(v >= 1e13f ? "0" : "0.0") + "조";
            if (v >= 1e8f) return (v / 1e8f).ToString(v >= 1e9f ? "0" : "0.0") + "억";
            if (v >= 1e4f) return (v / 1e4f).ToString(v >= 1e5f ? "0" : "0.0") + "만";
            return Mathf.Max(1, Mathf.RoundToInt(v)).ToString();
        }

        public void OnTick(SimWorld world, System.Func<int, bool> isBoss)
        {
            if (!Enabled) return;
            int heroId = world.Hero.Id;
            float big = world.Hero.Stats.Damage * 3f;
            _baseDamage = Mathf.Max(1f, world.Hero.Stats.Damage);
            SimEvent best = default;
            bool any = false;
            int bigShown = 0;
            foreach (var ev in world.Events)
            {
                if (ev.Type != SimEventType.Hit || ev.Value <= 0f) continue;
                if (ev.TargetId == heroId) { Spawn(ev.Pos, ev.Value, new Color(1f, 0.35f, 0.3f), false, ev.TargetId); continue; }
                bool boss = isBoss(ev.TargetId);
                if (ev.Value >= big && bigShown < 3) // three big numbers a tick at most: a pack fight stayed a wall of digits
                {
                    bigShown++;
                    Spawn(ev.Pos, ev.Value, ev.Elem == Element.None ? new Color(1f, 0.86f, 0.5f) : FxView.ElementColor(ev.Elem), true, ev.TargetId);
                }
                else if (boss) Spawn(ev.Pos, ev.Value, new Color(0.95f, 0.9f, 0.8f), false, ev.TargetId);
                else if (!any || ev.Value > best.Value) { best = ev; any = true; }
            }
            if (any && best.Value < big && Mode == 0) Spawn(best.Pos, best.Value, new Color(0.78f, 0.75f, 0.7f), false, best.TargetId);
        }

        TextMesh NewText(string name, int order, out MeshRenderer mr)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _font.material;
            mr.sortingOrder = order;
            return tm;
        }

        float _baseDamage = 1f;

        /// <summary>재기획 §4.5: six size steps (0.8–1.6×) by the hit against the hero's base damage, so a number's size reads.</summary>
        float SizeFor(float value, bool big)
        {
            int band = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Max(1f, value / _baseDamage), 2f)), 0, 5);
            return (big ? 0.056f : 0.042f) * (0.8f + 0.16f * band);
        }

        void Spawn(Vec2 at, float value, Color color, bool big, int target)
        {
            // Merge into the number already counting on this target.
            foreach (var m in _nums)
            {
                if (m.T >= m.Life || m.Target != target || m.T > MergeWindow) continue;
                m.Value += value;
                m.Text.text = m.Shadow.text = Short(m.Value);
                if (big && !m.Big) { m.Big = true; m.Color = color; m.Life = 0.95f; m.Drift.y = 0.9f; }
                m.Size = SizeFor(m.Value, m.Big); // the running total grows its number
                // Pop again and restart the rise from where it stands (stretching Life made it sink back).
                m.Origin = m.Text.transform.localPosition;
                m.Drift *= 0.5f;
                m.T = 0f;
                m.Pop = 0f;
                return;
            }
            int live = 0;
            foreach (var m in _nums) if (m.T < m.Life) live++;
            if (!big && live >= OrdinaryCap) return; // a crowded screen keeps its room for the big ones
            var n = _nums.Find(x => x.T >= x.Life);
            if (n == null)
            {
                if (_nums.Count >= MaxLive) return;
                var shadow = NewText("dmg shadow", OrderOnTop, out var smr);
                var tm = NewText("dmg", OrderOnTop + 1, out var mr);
                n = new Num { Text = tm, Mr = mr, Shadow = shadow, ShadowMr = smr };
                _nums.Add(n);
            }
            n.Target = target;
            n.Value = value;
            n.Pop = 0f;
            n.Text.text = n.Shadow.text = Short(value);
            n.Big = big;
            n.Size = SizeFor(value, big);
            n.Color = color;
            n.T = 0f;
            n.Life = big ? 0.95f : 0.6f;
            // A little scatter so a burst of numbers fans out instead of printing over itself.
            n.Origin = IsoProjection.ToWorld(at) + new Vector3(Random.Range(-0.25f, 0.25f), 0.75f + Random.Range(0f, 0.2f), 0f);
            // Never print over a number already up: step up a line until clear (a pack died into one unreadable stack).
            for (int tries = 0; tries < 5; tries++)
            {
                bool clash = false;
                foreach (var m in _nums)
                {
                    if (m == n || m.T >= m.Life) continue;
                    var d = m.Text.transform.localPosition - n.Origin;
                    if (Mathf.Abs(d.x) < 0.45f && Mathf.Abs(d.y) < 0.22f) { clash = true; break; }
                }
                if (!clash) break;
                n.Origin.y += 0.24f;
            }
            n.Drift = new Vector3(Random.Range(-0.35f, 0.35f), big ? 0.9f : 0.6f, 0f);
            n.Mr.enabled = true;
            n.ShadowMr.enabled = true;
        }

        public void Render(float dt)
        {
            foreach (var n in _nums)
            {
                if (n.T >= n.Life) continue;
                n.T += dt;
                n.Pop += dt;
                if (n.T >= n.Life) { n.Mr.enabled = false; n.ShadowMr.enabled = false; continue; }
                float k = n.T / n.Life;
                // Rise fast then hang; big ones overshoot in size (a punch), then settle — again on every merged hit.
                float rise = 1f - (1f - k) * (1f - k);
                var pos = n.Origin + n.Drift * rise;
                n.Text.transform.localPosition = pos;
                n.Shadow.transform.localPosition = pos + ShadowOffset;
                float s = n.Pop < 0.12f ? Mathf.Lerp(n.Big ? 1.6f : 1.25f, 1f, n.Pop / 0.12f) : 1f;
                float size = n.Size * s;
                if (Mathf.Abs(n.Text.characterSize - size) > 1e-4f) n.Text.characterSize = n.Shadow.characterSize = size; // TextMesh rebuilds on set
                var c = n.Color;
                c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                if (n.Text.color != c) { n.Text.color = c; n.Shadow.color = new Color(0.03f, 0.02f, 0.03f, c.a * 0.85f); }
            }
        }
    }
}
