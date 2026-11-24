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
        // 11-23 (review: "후반 화면의 데미지 숫자와 글자 겹침"): late on nearly every hit is a big one and each enemy of a pack
        // raised its own, so a pack fight printed a slab of digits. Hits landing close together now add into one number
        // (a pack's blow reads as one total), and fewer are up at once.
        const int MaxLive = 72, OrdinaryCap = 48, BigCap = 18, OrderOnTop = 32150; // above the HUD, under the cut-in (32200), fades (32300) and panels
        sealed class Num { public TextMesh Text, Shadow; public MeshRenderer Mr, ShadowMr; public Vector3 Origin, Drift; public float T, Life, Size, Value, Pop; public Color Color; public bool Big, OnHero; public int Target; }
        const float MergeWindow = 0.3f;
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
            // 11-24: the owner wants a number over every monster hit, so everyone starts again on 전체 (a new key: the old
            // default 주요 수치만 was saved for most players); only 끄기 carries over.
            try { Mode = PlayerPrefs.HasKey("dmg_numbers_mode2") ? Mathf.Clamp(PlayerPrefs.GetInt("dmg_numbers_mode2"), 0, 2) : PlayerPrefs.GetInt("dmg_numbers_mode", 0) == 2 ? 2 : 0; }
            catch { Mode = 0; }
        }

        public void Toggle()
        {
            Mode = (Mode + 1) % 3;
            PlayerPrefs.SetInt("dmg_numbers_mode2", Mode);
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
            _heroId = heroId;
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
                if (ev.Value >= big && bigShown < 6)
                {
                    bigShown++;
                    Spawn(ev.Pos, ev.Value, ev.Elem == Element.None ? new Color(1f, 0.86f, 0.5f) : FxView.ElementColor(ev.Elem), true, ev.TargetId);
                }
                else if (boss || Mode == 0) Spawn(ev.Pos, ev.Value, new Color(0.96f, 0.94f, 0.9f), false, ev.TargetId);
                else if (!any || ev.Value > best.Value) { best = ev; any = true; }
            }
            // 11-24 (owner: "몬스터들 위에 바로 데미지 안뜨는게 오히려 타격감을 저하"): in 전체 every hit prints over the monster it
            // struck (above); 주요 수치만 keeps the old ration of one ordinary number a tick.
            if (any && best.Value < big) Spawn(best.Pos, best.Value, new Color(0.78f, 0.75f, 0.7f), false, best.TargetId);
        }

        int _heroId = -1;

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

        /// <summary>Where a new number may sit around its monster's head, nearest first (world units).</summary>
        static readonly Vector3[] Fan =
        {
            new Vector3(0f, 0f), new Vector3(-0.55f, 0.12f), new Vector3(0.55f, 0.12f), new Vector3(0f, 0.34f),
            new Vector3(-0.6f, 0.46f), new Vector3(0.6f, 0.46f), new Vector3(-1.1f, 0.2f), new Vector3(1.1f, 0.2f),
            new Vector3(0f, 0.68f), new Vector3(-1.1f, 0.62f), new Vector3(1.1f, 0.62f), new Vector3(0f, 1.0f),
        };

        /// <summary>재기획 §4.5: six size steps (0.8–1.6×) by the hit against the hero's base damage, so a number's size reads.</summary>
        float SizeFor(float value, bool big)
        {
            int band = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Max(1f, value / _baseDamage), 2f)), 0, 5);
            return (big ? 0.056f : 0.036f) * (0.8f + 0.16f * band); // ordinary ones smaller (11-24): one over every hit
        }

        void Spawn(Vec2 at, float value, Color color, bool big, int target)
        {
            // Critique 6 P0: numbers printed over the top HUD lines, the right-hand menu and the bottom bar. A hit whose spot
            // lies in those bands shows no number (the HUD's text wins there).
            var cam = Camera.main;
            if (cam != null)
            {
                var vp = cam.WorldToViewportPoint(_root.TransformPoint(IsoProjection.ToWorld(at) + new Vector3(0f, 0.85f, 0f)));
                if (vp.y > 0.84f || vp.y < 0.2f || vp.x > 0.92f || vp.x < 0.02f) return; // 0.84: room for the rise under the top lines
            }
            // Merge into the number already counting on this target (a quick flurry reads as one rising total).
            // What the hero takes never mixes with what it deals.
            bool onHero = target == _heroId;
            foreach (var m in _nums)
            {
                if (m.T >= m.Life || m.T > MergeWindow || m.OnHero != onHero) continue;
                if (m.Target != target) continue; // 11-24: each monster keeps its own number (a pack no longer sums into one)
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
            int live = 0, liveBig = 0;
            foreach (var m in _nums) if (m.T < m.Life) { live++; if (m.Big) liveBig++; }
            if (!big && live >= OrdinaryCap) return; // a crowded screen keeps its room for the big ones
            if (big && !onHero && liveBig >= BigCap) return; // the totals already up carry the fight
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
            n.OnHero = onHero;
            n.Value = value;
            n.Pop = 0f;
            n.Text.text = n.Shadow.text = Short(value);
            n.Big = big;
            n.Size = SizeFor(value, big);
            n.Color = color;
            n.T = 0f;
            n.Life = big ? 0.95f : 0.7f;
            // Never print over a number already up (11-24 capture: a pack printed "1.9만1.9만" in one slab): try the spot over
            // the monster, then places fanning out around it; an ordinary hit with no clear place is left out (the
            // numbers around it already tell the fight), a big one takes the last place tried.
            var head = IsoProjection.ToWorld(at) + new Vector3(Random.Range(-0.12f, 0.12f), 0.75f + Random.Range(0f, 0.1f), 0f);
            float halfW = 0.16f + 0.11f * n.Text.text.Length * (n.Size / 0.042f), halfH = 0.2f * (n.Size / 0.042f);
            bool placed = false;
            for (int tries = 0; tries < Fan.Length && !placed; tries++)
            {
                n.Origin = head + Fan[tries];
                placed = true;
                foreach (var m in _nums)
                {
                    if (m == n || m.T >= m.Life) continue;
                    var d = m.Text.transform.localPosition - n.Origin;
                    float mw = 0.16f + 0.11f * m.Text.text.Length * (m.Size / 0.042f);
                    if (Mathf.Abs(d.x) < halfW + mw && Mathf.Abs(d.y) < halfH + 0.2f * (m.Size / 0.042f)) { placed = false; break; }
                }
            }
            if (!placed && !big && !onHero) { n.T = n.Life; n.Mr.enabled = n.ShadowMr.enabled = false; return; }
            n.Drift = new Vector3(Random.Range(-0.35f, 0.35f), big ? 0.9f : 0.6f, 0f);
            n.Mr.enabled = true;
            n.ShadowMr.enabled = true;
        }

        /// <summary>A viewport rect where no number shows (the find card while it is up; critique 7 P0).</summary>
        public static Rect Blocked;

        public void Render(float dt)
        {
            var cam = Camera.main;
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
                // The HUD bands win wherever the number is now: the camera follows the hero, so a number born mid-screen
                // can slide under the top lines or the menu while it lives (capture 11-24 05:26).
                if (cam != null)
                {
                    var vp = cam.WorldToViewportPoint(n.Text.transform.position);
                    bool show = vp.y <= 0.88f && vp.y >= 0.18f && vp.x <= 0.92f && vp.x >= 0.02f && !Blocked.Contains(new Vector2(vp.x, vp.y));
                    if (n.Mr.enabled != show) n.Mr.enabled = n.ShadowMr.enabled = show;
                }
            }
        }
    }
}
