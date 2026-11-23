using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 사냥 성과 (docs/research/1122_idle_satisfaction.md P0 — "진행이 안 보인다"): under the minimap, what the hunt is making
    /// right now, measured over the last 60 s — kills a minute, experience and ash an hour, damage a second — each with
    /// an arrow against a minute ago; and right after a new piece or a card, the hero's damage change for a few seconds
    /// (a gain under ~20% is not felt, so the number says it).
    /// </summary>
    public sealed class HuntPanel
    {
        const float W = 268f, H = 74f;
        readonly RectTransform _root;
        readonly Text _left, _right, _delta;
        readonly Queue<(float t, int kills, long xp, int ash)> _samples = new Queue<(float, int, long, int)>();
        float _sampleAt, _deltaFor, _lastDps;
        bool _changed;
        float _prevKpm, _prevXph, _prevAph, _prevDps, _prevAt;
        public bool Visible = true;

        public HuntPanel(UiKit ui)
        {
            _root = UiKit.Rect(ui.Root, "Hunt Panel", 0f, 0f, W, H);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(1f, 1f);
            _root.anchoredPosition = new Vector2(-24f, -204f);
            var plate = _root.gameObject.AddComponent<Image>();
            plate.color = new Color(0.03f, 0.02f, 0.03f, 0.62f);
            plate.raycastTarget = false;
            UiKit.Fill(_root, "edge", 0f, 0f, W, 1f, new Color(1f, 0.78f, 0.45f, 0.35f));
            UiKit.Fill(_root, "edge", 0f, H - 1f, W, 1f, new Color(1f, 0.78f, 0.45f, 0.2f));
            _left = UiKit.Label(_root, "", 8f, 6f, W / 2f - 8f, H - 12f, 11, TextAnchor.UpperLeft, UiKit.Dim);
            _right = UiKit.Label(_root, "", W / 2f, 6f, W / 2f - 8f, H - 12f, 11, TextAnchor.UpperRight, UiKit.Ink);
            _delta = UiKit.Label(_root, "", 0f, H + 2f, W, 16f, 12, TextAnchor.UpperRight, UiKit.Good, title: true);
        }

        /// <summary>A new piece, a card or a level: the next frame says what it did to the damage (a buff's swing does not).</summary>
        public void OnTick(List<SimEvent> events)
        {
            foreach (var ev in events)
                if (ev.Type == SimEventType.Equip || ev.Type == SimEventType.AugmentChosen || ev.Type == SimEventType.LevelUp
                    || ev.Type == SimEventType.Runeword || ev.Type == SimEventType.Rebirth) _changed = true;
        }

        public void Update(SimWorld w, float dt, bool show)
        {
            _root.gameObject.SetActive(show && Visible);
            float now = Time.unscaledTime;
            float dps = HeroStats.Dps(w.Hero);
            if (_changed && _lastDps > 0f && Mathf.Abs(dps - _lastDps) / _lastDps >= 0.02f)
            {
                float pct = (dps - _lastDps) / _lastDps * 100f;
                _delta.text = (pct >= 0f ? "<color=#9fd28a>초당 피해 +" : "<color=#ff8a7a>초당 피해 ") + pct.ToString("0") + "%</color>";
                _deltaFor = 3f;
            }
            if (_changed || _deltaFor <= 0f) _lastDps = dps;
            _changed = false;
            _deltaFor -= dt;
            var dc = _delta.color; dc.a = Mathf.Clamp01(_deltaFor / 0.6f); _delta.color = dc;

            if (now < _sampleAt) return;
            _sampleAt = now + 1f;
            _samples.Enqueue((now, w.Kills, w.TotalXp, w.Bag.AshShards));
            while (_samples.Count > 2 && now - _samples.Peek().t > 60f) _samples.Dequeue();
            var first = _samples.Peek();
            float span = Mathf.Max(1f, now - first.t);
            float kpm = (w.Kills - first.kills) / span * 60f;
            float xph = (w.TotalXp - first.xp) / span * 3600f;
            float aph = Mathf.Max(0f, (w.Bag.AshShards - first.ash) / span * 3600f);
            // Arrows against the reading a minute ago.
            if (_prevAt == 0f || now - _prevAt >= 60f) { _prevKpm = kpm; _prevXph = xph; _prevAph = aph; _prevDps = dps; _prevAt = now; }
            _left.text = "분당 처치\n시간당 경험치\n시간당 잿 파편\n초당 피해";
            _right.text = Line(kpm, _prevKpm, kpm.ToString("0")) + "\n" + Line(xph, _prevXph, DamageNumbers.Short(xph))
                + "\n" + Line(aph, _prevAph, DamageNumbers.Short(aph)) + "\n" + Line(dps, _prevDps, DamageNumbers.Short(dps));
        }

        static string Line(float v, float before, string text)
        {
            string arrow = before <= 0f || Mathf.Abs(v - before) / Mathf.Max(1f, before) < 0.05f ? "  " : v > before ? " <color=#9fd28a>▲</color>" : " <color=#ff8a7a>▼</color>";
            return "<color=#f2e6cf>" + text + "</color>" + arrow;
        }
    }

    /// <summary>
    /// 전설 획득 배너 (1122_numbers_loot.md: 전설은 지속 빔 + 상단 배너): a legendary or ★ find shows its name at the top of the
    /// screen for two seconds, popping in, in its rarity's colour; more than one waits its turn.
    /// </summary>
    public sealed class LootBanner
    {
        readonly RectTransform _root;
        readonly Text _title, _sub;
        readonly Image _glow;
        readonly Queue<Item> _queue = new Queue<Item>();
        float _t = -1f;
        const float In = 0.15f, Hold = 1.9f, Out = 0.45f;

        public LootBanner(UiKit ui)
        {
            _root = UiKit.Rect(ui.Root, "Loot Banner", 0f, 0f, 560f, 54f);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -64f);
            _glow = UiKit.Fill(_root, "glow", -60f, -26f, 680f, 106f, Color.clear);
            _glow.sprite = UiKit.Glow(ui, 64);
            UiKit.Fill(_root, "plate", 40f, 4f, 480f, 46f, new Color(0.03f, 0.02f, 0.03f, 0.75f));
            _title = UiKit.Label(_root, "", 0f, 8f, 560f, 22f, 18, TextAnchor.UpperCenter, UiKit.Gold, title: true);
            _sub = UiKit.Label(_root, "", 0f, 31f, 560f, 14f, 11, TextAnchor.UpperCenter, UiKit.Dim);
            _root.gameObject.SetActive(false);
        }

        public void OnTick(List<SimEvent> events)
        {
            foreach (var ev in events)
                if (ev.Type == SimEventType.Drop && ev.Item != null && ItemStyle.IsJackpot(ev.Item) && _queue.Count < 3) _queue.Enqueue(ev.Item);
        }

        public void Update(float dt)
        {
            if (_t < 0f)
            {
                if (_queue.Count == 0) return;
                var item = _queue.Dequeue();
                var col = ItemStyle.Color(item.Rarity);
                int stars = item.GreaterCount;
                _title.text = "<color=" + ItemStyle.Hex(item.Rarity) + ">" + (stars > 0 ? new string('★', stars) + " " : "") + item.Name + "</color>";
                _sub.text = ItemStyle.RarityName(item.Rarity) + " 획득";
                _glow.color = new Color(col.r, col.g, col.b, 0f);
                _t = 0f;
                _root.gameObject.SetActive(true);
                _root.SetAsLastSibling();
            }
            _t += dt;
            float a = _t < In ? _t / In : _t < In + Hold ? 1f : 1f - (_t - In - Hold) / Out;
            float pop = _t < In ? Mathf.Lerp(1.25f, 1f, _t / In) : 1f;
            _root.localScale = new Vector3(pop, pop, 1f);
            foreach (var g in _root.GetComponentsInChildren<Graphic>())
            {
                var c = g.color;
                c.a = g == _glow ? 0.45f * a : g is Text ? a : 0.75f * a;
                g.color = c;
            }
            if (_t >= In + Hold + Out) { _t = -1f; _root.gameObject.SetActive(false); }
        }
    }
}
