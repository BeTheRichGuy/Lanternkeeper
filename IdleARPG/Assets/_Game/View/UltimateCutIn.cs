using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// An ultimate (Lv30, GDD §7) gets a cut-in, "the wick is struck" (docs/research/1123_ultimate_cutin.md): a bone-white
    /// line is drawn across the screen and flares open into a band - white-hot, then ember, then ink - the skill's name
    /// dark against the heat until the band cools and it is lit; at the end band, bust and name are swept off to the
    /// right together. Hard cuts on a 0.05 s grid, no fades (the soft fades were what read as machine-made).
    /// 1.0 s for the first <see cref="FullPlays"/> casts of a skill in a session and then once in <see cref="FullEvery"/>
    /// seconds; the casts between get a 0.6 s name plate (it comes every 30-40 s for hours). Sits under the panels.
    /// </summary>
    public sealed class UltimateCutIn
    {
        const float FullLife = 1f, LightLife = 0.6f;
        /// <summary>Casts of one skill (per session) that get the whole cut-in before it shrinks to the name plate.</summary>
        const int FullPlays = 2;
        /// <summary>After those, the whole cut-in returns when none has played for this long (about one cast in four):
        /// with only the first two, a Lv42 hero's session showed nothing but the plate (capture 11-23).</summary>
        const float FullEvery = 120f;
        // Everything is laid out in design pixels: 1/32 unit, one texel of the 64 px bust drawn at 2x (about 4 screen
        // pixels at 1080p, so the screen is about 480 x 270 of them).
        const int FullH = 44, LightH = 24; // band heights; even, the slanted ends step 1 across per 2 up
        const int FontPx = 15;             // Galmuri14's own size (1500 units per em): one glyph pixel = one design pixel
        const int SubPx = 12;              // Galmuri11's own size, for the line under the name
        const float NameTop = 8f;          // band middle to the top of the name's line (the glyphs are the 14 rows under it)
        const float NameTopOver = 13f;     // the same with the line under it: name rows 12..-1, the line -6..-12
        const float SlowMo = 0.55f;        // how long the call site slows the world: the dark half lifts as it ends
        const int Line = 0, Hot = 1, Cool = 2, Lit = 3, Out = 4;

        static readonly Color Ink = new Color(0.055f, 0.04f, 0.047f), Bone = new Color(0.95f, 0.9f, 0.78f),
            Gold = new Color(1f, 0.812f, 0.353f), Ember = new Color(0.91f, 0.455f, 0.165f);
        // Speed lines inside the full band: row from its middle (the name and its line fill -12..12, so they keep to
        // the margins), length and speed in design pixels.
        static readonly float[] StreakRow = { -15f, 15f, 17f, -17f, 14f }, StreakLen = { 70f, 40f, 110f, 55f, 90f },
            StreakSpeed = { 520f, 760f, 610f, 900f, 680f };
        // What the skill does, in one short line under its name (review 11-24: the name alone said nothing). Written
        // for the cut-in: GameText's two sentences do not cut to a line. A skill not listed here simply has none.
        static readonly Dictionary<string, string> Lines = new Dictionary<string, string>
        {
            { "forge_judgment", "일대를 태우고 공명을 채운다" },
            { "undying_flame", "8초간 사망 연쇄가 약해지지 않는다" },
            { "grand_judgment", "일대에 벼락을 내리고 공명을 채운다" },
            { "sanctuary", "8초간 덜 맞고, 맞으면 되받아친다" },
            { "thunderstorm", "8초간 벼락이 잇따라 떨어진다" },
            { "hunt_time", "8초간 두 배로 빠르게 몰아친다" },
            { "star_collapse", "일대를 무너뜨리고 공명을 채운다" },
            { "eternal_moment", "6초간 고리가 쉬지 않고 돈다" },
        };

        readonly Transform _root;
        readonly SpriteCatalog _catalog;
        readonly Material _material;
        readonly SpriteRenderer _veil, _band, _trail, _endL, _endR, _sliverA, _sliverB, _sliverL, _ruleTop, _ruleBot, _portrait, _icon;
        readonly SpriteRenderer[] _streaks = new SpriteRenderer[5], _embers = new SpriteRenderer[10];
        // The name twice, one pixel apart: Galmuri14's one-pixel strokes had no weight at this size (review 11-24).
        readonly TextMesh[] _name = new TextMesh[2], _shadow = new TextMesh[2];
        readonly TextMesh _sub;
        readonly Dictionary<string, int> _seen = new Dictionary<string, int>();
        readonly Dictionary<int, Sprite> _shards = new Dictionary<int, Sprite>();
        float _t = 1f, _life, _hw = 240f, _hh = 135f, _nameW, _subW, _subPx = 0.5f, _fullAt = -999f;
        int _n = 4; // texture pixels of the world picture per design pixel
        bool _full, _hasPortrait, _hasIcon, _hasSub;
        Color _accent = Ember;

        public UltimateCutIn(Transform camera, SpriteCatalog catalog, Material material)
        {
            _catalog = catalog;
            _material = material;
            _root = new GameObject("Ultimate Cut-in").transform;
            _root.SetParent(camera, false);
            _root.localPosition = new Vector3(0f, 0f, 9.8f);
            var dot = catalog.Get("ui/dot");
            _veil = Layer(dot, 32200);
            _band = Layer(dot, 32201);
            _trail = Layer(dot, 32201);
            _endL = Layer(null, 32201);
            _endR = Layer(null, 32201);
            _sliverA = Layer(null, 32201);
            _sliverB = Layer(null, 32201);
            _sliverL = Layer(null, 32201);
            for (int i = 0; i < _streaks.Length; i++) _streaks[i] = Layer(dot, 32202);
            _ruleTop = Layer(dot, 32203);
            _ruleBot = Layer(dot, 32203);
            _portrait = Layer(null, 32204);
            _icon = Layer(null, 32204);
            _portrait.transform.localScale = _icon.transform.localScale = new Vector3(2f, 2f, 1f); // PPU 64 art: a texel = a design pixel
            for (int i = 0; i < 2; i++) _shadow[i] = Label(UiFont.Title, FontPx, 32205);
            for (int i = 0; i < 2; i++) _name[i] = Label(UiFont.Title, FontPx, 32206);
            _sub = Label(UiFont.Body, SubPx, 32206);
            for (int i = 0; i < _embers.Length; i++) _embers[i] = Layer(dot, 32207);
            _root.gameObject.SetActive(false);
        }

        SpriteRenderer Layer(Sprite sprite, int order)
        {
            var sr = new GameObject("sr").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(_root, false);
            sr.sharedMaterial = _material;
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        TextMesh Label(Font font, int px, int order)
        {
            var go = new GameObject("text");
            go.transform.SetParent(_root, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = px;
            tm.characterSize = 10f / 32f;     // a TextMesh font pixel is characterSize / 10 units: one design pixel
            tm.anchor = TextAnchor.UpperLeft; // offsets in whole glyph pixels (a middle anchor can land on a half)
            tm.richText = false;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = order;
            return tm;
        }

        static float Advance(Font font, string s, int px)
        {
            font.RequestCharactersInTexture(s, px);
            float w = 0f;
            foreach (char ch in s)
                if (font.GetCharacterInfo(ch, out var info, px)) w += info.advance;
            return w;
        }

        /// <summary>
        /// The band's slanted pieces, built once: one texel per design pixel, pivot bottom-left, leaning 1 across per 2 up
        /// (a clean pixel line; a rotated quad would stair-step finer than the art). <paramref name="w"/> &gt; 0 is a
        /// sliver that wide with both sides slanted, 0 the band's right end (upright on its left), -1 its left end.
        /// </summary>
        Sprite Shard(int w, int h)
        {
            int key = h * 100 + w + 1;
            if (_shards.TryGetValue(key, out var s) && s != null) return s;
            int tw = h / 2 + Mathf.Max(0, w);
            var tex = new Texture2D(tw, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[tw * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < tw; x++)
                {
                    int lean = y / 2;
                    bool on = w > 0 ? x >= lean && x < lean + w : w == 0 ? x <= lean : x >= lean;
                    px[y * tw + x] = new Color32(255, 255, 255, (byte)(on ? 255 : 0));
                }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, tw, h), Vector2.zero, 32f);
            _shards[key] = s;
            return s;
        }

        public void Layout(float halfW, float halfH, float ui)
        {
            // The world is drawn into a texture at a whole zoom and that texture is fitted to the screen, so the HUD scale
            // (2 / 1.8 at a grown hero's zoom) puts a design pixel on 4.4 texture pixels. The root takes the nearest scale
            // that makes it a whole number instead: every texel here is the same size, at any zoom.
            int whole = Mathf.Max(1, WorldPresenter.Whole);
            _n = Mathf.Max(1, Mathf.RoundToInt(2f * ui * whole));
            float scale = _n / (2f * whole);
            _root.localScale = Vector3.one * scale;
            _hw = halfW / scale * 32f;
            _hh = halfH / scale * 32f;
            // The line under the name is about half the name's size, on whole texture pixels all the same.
            _subPx = Mathf.Max(1, Mathf.RoundToInt(_n * 0.5f)) / (float)_n;
            _sub.characterSize = 10f / 32f * _subPx;
        }

        /// <param name="forceFull">The whole cut-in whatever the count and the clock (captures, previews).</param>
        public void Play(string classId, SkillDef skill, bool forceFull = false)
        {
            _seen.TryGetValue(skill.Id, out int seen);
            _seen[skill.Id] = seen + 1;
            _full = forceFull || seen < FullPlays || Time.unscaledTime - _fullAt >= FullEvery;
            if (_full) _fullAt = Time.unscaledTime;
            _life = _full ? FullLife : LightLife;
            int h = _full ? FullH : LightH;
            _endL.sprite = Shard(-1, h);
            _endR.sprite = Shard(0, h);
            _sliverA.sprite = _sliverL.sprite = Shard(4, h);
            _sliverB.sprite = Shard(1, h);
            // The bust only in the full cut-in, and only for a class that has one drawn.
            string portrait = ClassSelectPanel.PortraitOf(classId);
            _hasPortrait = _full && portrait != null && _catalog.Has(portrait);
            if (_hasPortrait) _portrait.sprite = _catalog.Get(portrait);
            // The skill's own framed icon stands where a "class · ultimate" caption stood: it says which one, in a picture.
            string icon = "icon/skill_" + skill.Id;
            _hasIcon = _catalog.Has(icon);
            if (_hasIcon) _icon.sprite = _catalog.Get(icon);
            // Two or three syllables alone look lost in the band: set them wide, the way a title card does.
            string name = skill.Name.Length <= 3 && skill.Name.IndexOf(' ') < 0 ? string.Join<char>(" ", skill.Name) : skill.Name;
            for (int i = 0; i < 2; i++) _name[i].text = _shadow[i].text = name;
            _nameW = Advance(UiFont.Title, name, FontPx) + 1f;
            // What it does, in the full cut-in only (the plate stays a name).
            string line = null;
            _hasSub = _full && Lines.TryGetValue(skill.Id, out line);
            _sub.text = _hasSub ? line : "";
            _subW = _hasSub ? Advance(UiFont.Body, line, SubPx) : 0f; // in its own pixels; Layout says how big one is
            // One accent besides ink, bone and gold: the skill's element (ember orange when it has none).
            _accent = skill.Elem == Element.None ? Ember : FxView.ElementColor(skill.Elem);
            _t = 0f;
            _root.gameObject.SetActive(true);
        }

        static Color Dim(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>A design-pixel coordinate on the picture's own pixel grid (motion steps in whole texture pixels).</summary>
        float Snap(float d) => Mathf.Round(d * _n) / _n;

        /// <summary>A flat rectangle from the dot (one design pixel at scale 1): bottom-left corner and size.</summary>
        void Box(SpriteRenderer sr, float x, float y, float w, float h, Color c)
        {
            float l = Snap(x), b = Snap(y), r = Snap(x + w), t = Snap(y + h);
            sr.enabled = r > l && t > b;
            if (!sr.enabled) return;
            sr.transform.localPosition = new Vector3((l + r) / 64f, (b + t) / 64f, 0f);
            sr.transform.localScale = new Vector3(r - l, t - b, 1f);
            sr.color = c;
        }

        /// <summary>A drawn sprite by its bottom-left corner; <paramref name="half"/> is half its size for centre-pivot
        /// art (bust 32, icon 16), 0 for the shards.</summary>
        void Put(SpriteRenderer sr, float x, float y, float half, Color c)
        {
            sr.transform.localPosition = new Vector3((Snap(x) + half) / 32f, (Snap(y) + half) / 32f, 0f);
            sr.color = c;
        }

        /// <summary>Real time (unscaled), so the hit-stop it comes with does not stretch it.</summary>
        public void Render(float realDt)
        {
            if (_t >= _life) return;
            _t += realDt;
            if (_t >= _life) { _root.gameObject.SetActive(false); return; }

            // Hard cuts between phases: line, (hot, cool,) lit, out. With the shake setting off there is no flash.
            bool flare = _full && GameSettings.Shake;
            float tLit = flare ? 0.15f : 0.05f, tOut = _life - 0.15f;
            int phase = _t < 0.05f ? Line : _t < tLit ? (_t < 0.1f ? Hot : Cool) : _t < tOut ? Lit : Out;
            bool solid = phase != Line, shown = phase >= Lit;
            // Out: everything leaves to the right as one piece in three keyed steps, faster each time. It went dark
            // first and left an empty band standing (review 11-24).
            float dx = phase != Out ? 0f : Mathf.Round(2f * _hw * (_t < tOut + 0.05f ? 0.1f : _t < tOut + 0.1f ? 0.4f : 0.8f));

            // In the upper third: clear of the hero and the circle under his feet, and not a caption bar along the bottom.
            int h = _full ? FullH : LightH;
            float yc = Mathf.Round(0.44f * _hh), yb = yc - h / 2, yt = yc + h / 2;
            // The full band runs in from the left edge: bust, icon, name. The plate is the icon and the name, centred.
            float left = Mathf.Round(-_hw), x0, cx;
            if (_full) { x0 = left - 2f; cx = left + (_hasPortrait ? 68f : 16f); }
            else { cx = -Mathf.Round(((_hasIcon ? 40f : 0f) + _nameW) / 2f); x0 = cx - 10f; }
            float start = _full ? x0 : x0 - h / 2; // where the line is struck from, and what the band trails back to
            x0 += dx;
            cx += dx;
            float nameX = cx + (_hasIcon ? 40f : 0f);
            float xe = Mathf.Round(nameX + Mathf.Max(_nameW, _subW * _subPx)) + (_full ? 14f : 10f);

            // The dark falls after the flash (and after the director's two-tone impact frame, which goes by brightness)
            // and half lifts when the slow motion ends: the world is back while the name still stands.
            float dark = !_full || _t < 0.1f ? 0f : _t < SlowMo ? 0.55f : phase == Lit ? 0.28f : _t < tOut + 0.05f ? 0.12f : 0f;
            if (dark > 0f) Box(_veil, -_hw - 4f, -_hh - 4f, _hw * 2f + 8f, _hh * 2f + 8f, new Color(0.03f, 0.02f, 0.03f, dark));
            else _veil.enabled = false;

            // White-hot, then the accent, then ink: iron cooling. The line is the same quad one pixel tall, struck left
            // to right in two steps.
            Color body = phase == Hot ? Bone : phase == Cool ? _accent : Ink;
            if (solid) Box(_band, x0, yb, xe - x0, h, body);
            else Box(_band, start, yc, ((_full ? 2f - left : xe + h / 2) - start) * (_t < 0.025f ? 0.5f : 1f), 1f, Bone);
            // Leaving, the band draws that line again behind it, back to where it started.
            if (phase == Out) Box(_trail, start, yc, x0 - h / 4 - start, 1f, Bone);
            else _trail.enabled = false;

            // The slanted end, and two slivers cut off it and parallel to it: 3 clear, 4 of the accent, 2 clear, 1 of gold.
            // The plate's left end gets the accent sliver too: ink on a dark floor showed no end at all (capture 11-23).
            // The full band shows its left end only as it leaves the screen's edge.
            _endR.enabled = _sliverA.enabled = _sliverB.enabled = solid;
            _endL.enabled = solid && (!_full || phase == Out);
            _sliverL.enabled = solid && !_full;
            if (solid)
            {
                Put(_endR, xe, yb, 0f, body);
                Put(_endL, x0 - h / 2, yb, 0f, body);
                Put(_sliverL, x0 - h / 2 - 7f, yb, 0f, shown ? _accent : body);
                Put(_sliverA, xe + 4f, yb, 0f, shown ? _accent : body);
                Put(_sliverB, xe + 10f, yb, 0f, shown ? Gold : body);
            }

            // Two engraved rules, 2 in from the band's edges.
            if (shown)
            {
                Box(_ruleTop, x0, yt - 3f, xe - x0, 1f, Gold);
                Box(_ruleBot, x0, yb + 2f, xe - x0, 1f, Gold);
            }
            else _ruleTop.enabled = _ruleBot.enabled = false;

            // The bust in its own colours from the first frame (a black silhouette read as a missing texture, review
            // 11-24): keyed poses coming in, no easing curve, then it creeps while held so the still picture stays alive.
            // The icon comes with the light: its frame is a square, and a square on the hot band is only a block.
            float bustDx = phase == Line ? -28f : phase == Hot ? -10f : phase == Cool ? -3f : 8f * Mathf.Clamp01((_t - tLit) / (tOut - tLit));
            float nameDx = phase == Hot ? 6f : phase == Cool ? 2f : 0f;
            _portrait.enabled = _hasPortrait;
            if (_hasPortrait) Put(_portrait, left - 4f + bustDx + dx, yb, 32f, Color.white); // stands on the band's bottom edge, head out of the top
            _icon.enabled = _hasIcon && shown;
            if (_icon.enabled) Put(_icon, cx, yc - 16f, 16f, Color.white);
            // The name is ink on the heat, bone on the ink.
            Color text = shown ? Bone : solid ? Ink : Color.clear, under = shown ? Dim(_accent, 0.45f) : Color.clear;
            float tx = Snap(nameX + nameDx) / 32f, ty = Snap(yc + (_hasSub ? NameTopOver : NameTop)) / 32f;
            for (int i = 0; i < 2; i++)
            {
                _name[i].transform.localPosition = new Vector3(tx + i / 32f, ty, 0f);
                _name[i].color = text;
                _shadow[i].transform.localPosition = new Vector3(tx + (i + 1) / 32f, ty - 1f / 32f, 0f);
                _shadow[i].color = under;
            }
            _sub.transform.localPosition = new Vector3(tx, Snap(yc - 5f) / 32f, 0f);
            _sub.color = shown ? Dim(Bone, 0.75f) : text;

            // Speed lines running left above and below the name, cut off at the band's ends. Not on the plate: it has
            // no rows clear of the name, and lines through the letters cost more than they give.
            for (int i = 0; i < _streaks.Length; i++)
            {
                if (phase != Lit || !_full) { _streaks[i].enabled = false; continue; }
                float head = xe - Mathf.Repeat(_t * StreakSpeed[i] + i * 97f, xe - x0 + StreakLen[i]);
                float l = Mathf.Max(x0, head), r = Mathf.Min(xe, head + StreakLen[i]);
                Box(_streaks[i], l, yc + StreakRow[i], r - l, 1f, Dim(_accent, 0.3f));
            }

            // Embers off the band's top edge: each rises once, cooling bone - accent - dull, and is gone.
            int embers = _full ? _embers.Length : 4;
            for (int i = 0; i < _embers.Length; i++)
            {
                float k = (_t - tLit - Frac(i * 0.618f) * (tOut - tLit - 0.2f)) / 0.4f;
                if (phase != Lit || i >= embers || k < 0f || k >= 1f) { _embers[i].enabled = false; continue; }
                float ex = Mathf.Lerp(nameX, xe, Frac(i * 0.371f + 0.2f)) + (Frac(i * 0.53f) - 0.5f) * 8f * k;
                float ey = yt + (10f + 14f * Frac(i * 0.77f)) * (1f - (1f - k) * (1f - k));
                Box(_embers[i], ex, ey, 1f, 1f, k < 0.3f ? Bone : k < 0.7f ? _accent : Dim(_accent, 0.5f));
            }
        }
    }
}
