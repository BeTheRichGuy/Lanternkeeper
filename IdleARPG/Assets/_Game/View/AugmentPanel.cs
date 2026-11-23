using System;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 증강 카드 뽑기 (희승 11-22: "카드 고를때 뜨는 효과음과 이펙트는 롤 증바람, 뱀파이어 서바이벌의 레벨 표시 처럼 …
    /// 가챠스러운 느낌이 강하게. 약간 빠칭코처럼"). Three cards fly up face down, then turn over one by one — the rarest last
    /// — each in a burst of its tier's light: silver a bell, gold a rising run with a white flash, prism a jackpot (the card
    /// shakes first, then rainbow rays, a screen flash and a rain of sparks). Click a card to take it, or re-deal once;
    /// nobody choosing, the world picks one after 30 s (the panel then plays that pick).
    /// </summary>
    public sealed class AugmentPanel
    {
        const float CardW = 196f, CardH = 280f, Gap = 28f, Deal = 0.12f, Rise = 0.35f, FirstFlip = 0.8f, FlipStep = 0.5f, Flip = 0.26f, Suspense = 0.55f;

        sealed class Card
        {
            public RectTransform Root;
            public Image Rays, Glow, Back, Frame, Icon, Shade;
            public RectTransform Face;
            public Text Tier, Name, Body, Branch, Note;
            public AugmentDef Def;
            public float FlipAt;
            public bool Revealed;
            public float Pop;
            /// <summary>The pointer is on the card, and how far its lift has come (0..1).</summary>
            public bool Over;
            public float Hover;
        }

        sealed class Spark
        {
            public Image Img;
            public Vector2 Pos, Vel;
            public float Life, Age;
        }

        readonly UiKit _ui;
        readonly Action<string> _sfx;
        readonly RectTransform _root, _row;
        readonly Image _veil, _flash;
        readonly Text _title, _foot;
        readonly Button _reroll;
        readonly Card[] _cards = new Card[3];
        readonly Spark[] _sparks = new Spark[96];
        readonly Sprite _rays, _glow;
        SimWorld _world;
        string[] _shown;
        float _t, _flashA;
        int _picked = -1;
        float _pickedAt;
        int _nextSpark;
        int _heldAtShow;

        public bool Visible => _root.gameObject.activeSelf;
        /// <summary>Raised after a card was taken or re-dealt here (the game saves soon).</summary>
        public Action OnChanged;

        public AugmentPanel(UiKit ui, Action<string> sfx)
        {
            _ui = ui;
            _sfx = sfx ?? (_ => { });
            _rays = MakeRays(128);
            _glow = UiKit.Glow(ui, 64);
            _root = UiKit.Rect(ui.Root, "Augment Panel", 0f, 0f, 0f, 0f);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            _veil = _root.gameObject.AddComponent<Image>();
            _veil.color = new Color(0f, 0f, 0f, 0.6f);
            _veil.raycastTarget = true; // the world under the deal takes no clicks

            float rowW = 3 * CardW + 2 * Gap;
            _row = UiKit.Centered(_root, "cards", rowW, CardH + 120f);
            _title = UiKit.Label(_row, "", 0f, 0f, rowW, 24f, 18, TextAnchor.UpperCenter, UiKit.Gold, title: true);
            for (int i = 0; i < 3; i++) _cards[i] = MakeCard(i, i * (CardW + Gap), 44f);
            _foot = UiKit.Label(_row, "", 0f, CardH + 58f, rowW, 16f, 12, TextAnchor.UpperCenter, UiKit.Dim);
            _reroll = ui.Button(_row, "다시 뽑기 (이 칸 1회)", rowW / 2f - 80f, CardH + 82f, 160f, 24f, Reroll);

            for (int i = 0; i < _sparks.Length; i++)
            {
                var img = UiKit.Fill(_root, "spark", 0f, 0f, 4f, 4f, Color.clear);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _sparks[i] = new Spark { Img = img, Life = 0f, Age = 1f };
            }
            _flash = UiKit.Fill(_root, "flash", 0f, 0f, 0f, 0f, Color.clear);
            _flash.rectTransform.anchorMin = Vector2.zero; _flash.rectTransform.anchorMax = Vector2.one;
            _flash.rectTransform.offsetMin = _flash.rectTransform.offsetMax = Vector2.zero;
            _root.gameObject.SetActive(false);
        }

        Card MakeCard(int index, float x, float y)
        {
            var c = new Card();
            // Light behind the card: slow rays and a soft halo, in the tier's colour once it turns.
            c.Rays = UiKit.Fill(_row, "rays", x + CardW / 2f - 210f, y + CardH / 2f - 210f, 420f, 420f, Color.clear);
            c.Rays.sprite = _rays;
            c.Rays.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            c.Rays.rectTransform.anchoredPosition = new Vector2(x + CardW / 2f, -(y + CardH / 2f));
            c.Glow = UiKit.Fill(_row, "halo", x - 40f, y - 40f, CardW + 80f, CardH + 80f, Color.clear);
            c.Glow.sprite = _glow;

            c.Root = UiKit.Rect(_row, "card " + index, x, y, CardW, CardH);
            c.Root.pivot = new Vector2(0.5f, 0.5f);
            c.Root.anchoredPosition = new Vector2(x + CardW / 2f, -(y + CardH / 2f));
            var body = c.Root.gameObject.AddComponent<Image>();
            body.color = new Color(0.05f, 0.045f, 0.06f, 1f);
            var b = c.Root.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            int captured = index;
            b.onClick.AddListener(() => { UiKit.Clicked?.Invoke(); Choose(captured); });
            // 희승 11-22: "카드 선택할때 마우스 올리면 활성화 되게" — the card under the pointer lifts and brightens.
            var ptr = c.Root.gameObject.AddComponent<UiPointer>();
            ptr.Enter = () => { c.Over = true; if (c.Revealed && _picked < 0) _sfx("ui_click"); };
            ptr.Exit = () => c.Over = false;

            c.Frame = UiKit.Fill(c.Root, "frame", 0f, 0f, CardW, CardH, Color.white);
            c.Frame.sprite = _ui.Catalog.Has("ui/ui2_cell") ? _ui.Catalog.Get("ui/ui2_cell") : null;
            c.Frame.type = Image.Type.Sliced;

            // Back: a dark card with a lantern mark, glinting.
            c.Back = UiKit.Fill(c.Root, "back", 6f, 6f, CardW - 12f, CardH - 12f, new Color(0.16f, 0.09f, 0.07f, 1f));
            var mark = UiKit.Label(c.Back.transform, "◆", 0f, CardH / 2f - 40f, CardW - 12f, 60f, 44, TextAnchor.MiddleCenter, new Color(1f, 0.7f, 0.35f, 0.9f), title: true);
            mark.raycastTarget = false;
            UiKit.Label(c.Back.transform, "증강", 0f, CardH / 2f + 22f, CardW - 12f, 20f, 14, TextAnchor.UpperCenter, new Color(1f, 0.75f, 0.45f, 0.7f), title: true);

            c.Face = UiKit.Rect(c.Root, "face", 0f, 0f, CardW, CardH);
            c.Tier = UiKit.Label(c.Face, "", 0f, 12f, CardW, 16f, 12, TextAnchor.UpperCenter, UiKit.Dim, title: true);
            c.Icon = UiKit.Fill(c.Face, "icon", CardW / 2f - 32f, 34f, 64f, 64f, Color.white);
            c.Icon.preserveAspect = true;
            c.Name = UiKit.Label(c.Face, "", 10f, 108f, CardW - 20f, 40f, 15, TextAnchor.UpperCenter, UiKit.Gold, title: true);
            c.Body = UiKit.Label(c.Face, "", 14f, 150f, CardW - 28f, 64f, 12, TextAnchor.UpperCenter, UiKit.Ink);
            c.Branch = UiKit.Label(c.Face, "", 10f, CardH - 52f, CardW - 20f, 14f, 11, TextAnchor.UpperCenter, UiKit.Dim);
            c.Note = UiKit.Label(c.Face, "", 10f, CardH - 34f, CardW - 20f, 28f, 11, TextAnchor.UpperCenter, new Color(0.79f, 0.64f, 1f));
            // A dark film over the card that steps back while another is under the pointer (fading it let the world show through).
            c.Shade = UiKit.Fill(c.Root, "shade", 0f, 0f, CardW, CardH, Color.clear);
            return c;
        }

        // ---------- per frame ----------

        /// <summary><paramref name="canShow"/>: no other screen owns the moment (class choice, return summary).</summary>
        public void Update(float dt, SimWorld w, bool canShow)
        {
            _world = w;
            var offer = w.Hero.AugOffer;
            if (!Visible)
            {
                if (offer != null && canShow) Show(offer);
                return;
            }
            _t += dt;
            if (_picked < 0)
            {
                if (offer == null || w.Hero.Augments.Count > _heldAtShow)
                {
                    // Taken by the world (방치): play that pick.
                    string last = w.Hero.Augments.Count > 0 ? w.Hero.Augments[w.Hero.Augments.Count - 1] : null;
                    int idx = Array.IndexOf(_shown, last);
                    StartPick(idx < 0 ? 0 : idx);
                }
                else if (!Same(offer, _shown)) Show(offer); // re-dealt elsewhere
            }
            Animate(dt);
        }

        static bool Same(string[] a, string[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        void Show(string[] offer)
        {
            _shown = (string[])offer.Clone();
            _heldAtShow = _world.Hero.Augments.Count;
            _t = 0f;
            _picked = -1;
            _flashA = 0f;
            var w = _world;
            int slot = w.Hero.Augments.Count + 1;
            _title.text = "증강 카드 · " + slot + "번째 칸" + (w.Hero.Rebirths > 0 ? "  <color=#8a8178>(" + (w.Hero.Rebirths + 1) + "번째 삶)</color>" : "");
            // The rarest turns over last: the suspense of the deal.
            var order = new int[3] { 0, 1, 2 };
            Array.Sort(order, (a, b) =>
            {
                int ta = a < _shown.Length ? (int)(Augments.Get(_shown[a])?.Tier ?? 0) : -1, tb = b < _shown.Length ? (int)(Augments.Get(_shown[b])?.Tier ?? 0) : -1;
                return ta != tb ? ta.CompareTo(tb) : a.CompareTo(b);
            });
            float at = FirstFlip;
            for (int k = 0; k < 3; k++)
            {
                var c = _cards[order[k]];
                c.Def = order[k] < _shown.Length ? Augments.Get(_shown[order[k]]) : null;
                c.Root.gameObject.SetActive(c.Def != null);
                c.Rays.gameObject.SetActive(c.Def != null);
                c.Glow.gameObject.SetActive(c.Def != null);
                if (c.Def == null) continue;
                if (c.Def.Tier == AugTier.Prism) at += Suspense; // the jackpot makes you wait
                c.FlipAt = at;
                at += FlipStep;
                c.Revealed = false;
                c.Pop = 0f;
                Fill(c);
            }
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _sfx("card_deal");
        }

        void Fill(Card c)
        {
            var a = c.Def;
            var col = TierColor(a.Tier, 0f);
            c.Tier.text = "─ " + Augments.TierName(a.Tier) + " ─";
            c.Tier.color = col;
            c.Name.text = a.Name;
            c.Name.color = col;
            c.Body.text = a.Text;
            string icon = IconFor(a);
            c.Icon.sprite = _ui.Catalog.Has(icon) ? _ui.Catalog.Get(icon) : _ui.Catalog.Get("icon/item_amulet");
            c.Branch.text = a.Branch >= 0 ? "갈래 · " + Talents.BranchName(_world.Hero.ClassId, a.Branch) : "공용";
            bool turns = a.Branch >= 0 && a.Branch != _world.BranchInUse() && _world.Hero.AugSwitchSets && _world.Hero.TreeAuto;
            c.Note.text = turns ? "고르면 「" + Talents.BranchName(_world.Hero.ClassId, a.Branch) + "」 스킬 세트로 전환" : "";
        }

        string IconFor(AugmentDef a)
        {
            if (a.SkillId != null) return "icon/skill_" + a.SkillId;
            if (a.Branch >= 0)
            {
                var skills = Augments.BranchSkills(_world.Hero.ClassId, a.Branch);
                if (skills.Count > 0) return "icon/skill_" + skills[0];
            }
            switch (a.Kind)
            {
                case AugKind.CritChance: return "icon/item_ring";
                case AugKind.AttackSpeed: return "icon/item_gloves";
                case AugKind.MaxHp: return "icon/item_armor";
                case AugKind.MoveSpeed: return "icon/item_boots";
                default: return "icon/item_amulet";
            }
        }

        static Color TierColor(AugTier t, float time)
        {
            if (t == AugTier.Prism) return Color.HSVToRGB(Mathf.Repeat(time * 0.35f, 1f), 0.45f, 1f);
            return t == AugTier.Gold ? new Color(1f, 0.8f, 0.35f) : new Color(0.8f, 0.85f, 0.95f);
        }

        void Animate(float dt)
        {
            float now = Time.unscaledTime;
            bool allUp = true;
            bool anyOver = false;
            foreach (var oc in _cards) if (oc.Def != null && oc.Over && oc.Revealed && _picked < 0) anyOver = true;
            for (int i = 0; i < 3; i++)
            {
                var c = _cards[i];
                if (c.Def == null) continue;
                c.Hover = Mathf.MoveTowards(c.Hover, c.Over && c.Revealed && _picked < 0 ? 1f : 0f, dt * 7f);
                float hov = c.Hover * c.Hover * (3f - 2f * c.Hover);
                // Deal: up from below, one after another.
                float p = Mathf.Clamp01((_t - i * Deal) / Rise);
                float ease = 1f - (1f - p) * (1f - p) * (1f - p);
                float baseY = -(44f + CardH / 2f);
                var pos = c.Root.anchoredPosition;
                pos.y = baseY - (1f - ease) * 420f;
                // Prism suspense: the card trembles before it turns.
                float shake = 0f;
                if (c.Def.Tier == AugTier.Prism && !c.Revealed && _t > c.FlipAt - Suspense) shake = Mathf.Sin(now * 70f) * 3.5f;
                pos.x = i * (CardW + Gap) + CardW / 2f + shake;
                pos.y += hov * 14f; // the card under the pointer rises
                c.Root.anchoredPosition = pos;

                // Turn over: the card narrows to an edge, then opens on its face.
                float q = Mathf.Clamp01((_t - c.FlipAt) / Flip);
                float sx = Mathf.Abs(Mathf.Cos(q * Mathf.PI));
                bool face = q >= 0.5f;
                if (face && !c.Revealed) Reveal(c);
                if (!c.Revealed) allUp = false;
                c.Pop = Mathf.Max(0f, c.Pop - dt * 3f);
                float scale = (1f + c.Pop * 0.18f) * (1f + 0.07f * hov);
                if (_picked >= 0) scale *= i == _picked ? 1f + 0.15f * Mathf.Clamp01((now - _pickedAt) * 4f) : 1f;
                c.Root.localScale = new Vector3(sx * scale, scale, 1f);
                c.Back.gameObject.SetActive(!face);
                c.Face.gameObject.SetActive(face);

                var col = TierColor(c.Def.Tier, now);
                float fade = _picked >= 0 && i != _picked ? Mathf.Clamp01(1f - (now - _pickedAt) * 3f) : 1f;
                var lit2 = Color.Lerp(col, Color.white, 0.35f * hov);
                c.Frame.color = face ? new Color(lit2.r, lit2.g, lit2.b, fade) : new Color(0.7f, 0.5f, 0.3f, fade);
                if (c.Def.Tier == AugTier.Prism && face) c.Name.color = col;
                // While one card is under the pointer the others step back.
                float dim = anyOver && !c.Over && _picked < 0 ? 0.62f : 1f;
                SetAlpha(c, fade);
                c.Shade.color = new Color(0.02f, 0.015f, 0.02f, (1f - dim) * 1.3f * fade);

                // Light: rays turn slowly (fast for prism), the halo breathes; only once the face is up.
                float lit = c.Revealed ? 1f : 0f;
                float tierBoost = c.Def.Tier == AugTier.Prism ? 1f : c.Def.Tier == AugTier.Gold ? 0.75f : 0.45f;
                c.Rays.rectTransform.localEulerAngles = new Vector3(0f, 0f, now * (c.Def.Tier == AugTier.Prism ? 60f : 18f) + i * 40f);
                c.Rays.color = new Color(col.r, col.g, col.b, lit * fade * dim * (0.35f + 0.25f * tierBoost + 0.25f * hov + 0.08f * Mathf.Sin(now * 3f)));
                c.Rays.rectTransform.localScale = Vector3.one * (0.8f + 0.25f * tierBoost + c.Pop * 0.4f + 0.2f * hov);
                c.Glow.color = new Color(col.r, col.g, col.b, lit * fade * dim * (0.25f + 0.35f * tierBoost + 0.35f * hov + 0.1f * Mathf.Sin(now * 4f + i)));
            }

            // Sparks and the white flash.
            foreach (var s in _sparks)
            {
                if (s.Age >= s.Life) { if (s.Img.color.a > 0f) s.Img.color = Color.clear; continue; }
                s.Age += dt;
                s.Vel += new Vector2(0f, -520f) * dt;
                s.Pos += s.Vel * dt;
                s.Img.rectTransform.anchoredPosition = s.Pos;
                var c = s.Img.color;
                c.a = Mathf.Clamp01(1f - s.Age / s.Life);
                s.Img.color = c;
            }
            _flashA = Mathf.Max(0f, _flashA - dt * 2.5f);
            _flash.color = new Color(1f, 0.97f, 0.9f, _flashA);

            // Buttons and the line under the cards.
            _reroll.gameObject.SetActive(allUp && _picked < 0 && !_world.Hero.AugRerolled);
            if (_picked >= 0) _foot.text = "";
            else if (!allUp) _foot.text = "카드를 여는 중…";
            else
            {
                int left = Mathf.Max(0, Mathf.CeilToInt((float)(Augments.AutoPickSeconds - (_world.Time - _world.Hero.AugOfferAt))));
                _foot.text = "카드를 눌러 선택  ·  " + left + "초 뒤 자동 선택";
            }
            if (_picked >= 0 && now - _pickedAt > 1.1f) _root.gameObject.SetActive(false);
        }

        static void SetAlpha(Card c, float a)
        {
            foreach (var g in c.Root.GetComponentsInChildren<Graphic>(true))
            {
                if (g == c.Frame) continue;
                var col = g.color;
                col.a = a;
                g.color = col;
            }
        }

        void Reveal(Card c)
        {
            c.Revealed = true;
            c.Pop = 1f;
            _sfx("card_flip");
            var center = c.Root.position;
            Vector2 local = (Vector2)_root.InverseTransformPoint(center);
            switch (c.Def.Tier)
            {
                case AugTier.Prism:
                    _sfx("card_prism");
                    _flashA = 0.85f;
                    Burst(local, 70, true, 620f);
                    break;
                case AugTier.Gold:
                    _sfx("card_gold");
                    _flashA = Mathf.Max(_flashA, 0.35f);
                    Burst(local, 36, false, 460f, new Color(1f, 0.82f, 0.35f));
                    break;
                default:
                    _sfx("card_silver");
                    Burst(local, 14, false, 300f, new Color(0.85f, 0.9f, 1f));
                    break;
            }
        }

        void Burst(Vector2 at, int n, bool rainbow, float speed, Color color = default)
        {
            for (int k = 0; k < n; k++)
            {
                var s = _sparks[_nextSpark];
                _nextSpark = (_nextSpark + 1) % _sparks.Length;
                float ang = UnityEngine.Random.value * Mathf.PI * 2f;
                float v = speed * (0.35f + UnityEngine.Random.value * 0.65f);
                s.Pos = at;
                s.Vel = new Vector2(Mathf.Cos(ang) * v, Mathf.Sin(ang) * v + 180f);
                s.Age = 0f;
                s.Life = 0.6f + UnityEngine.Random.value * 0.6f;
                float size = 3f + UnityEngine.Random.value * 4f;
                s.Img.rectTransform.sizeDelta = new Vector2(size, size);
                s.Img.color = rainbow ? Color.HSVToRGB(UnityEngine.Random.value, 0.5f, 1f) : color;
                s.Img.transform.SetAsLastSibling();
            }
            _flash.transform.SetAsLastSibling();
        }

        // ---------- choosing ----------

        void Choose(int index)
        {
            if (_picked >= 0 || _world == null || _shown == null || index >= _shown.Length) return;
            var c = _cards[index];
            if (!c.Revealed) return;
            if (!_world.ChooseAugment(index)) return;
            StartPick(index);
            OnChanged?.Invoke();
        }

        void StartPick(int index)
        {
            _picked = index;
            _pickedAt = Time.unscaledTime;
            foreach (var c in _cards) if (!c.Revealed && c.Def != null) Reveal(c); // a pick before the turn shows every card
            _sfx("card_pick");
            var pc = _cards[index];
            if (pc.Def != null)
            {
                Vector2 local = (Vector2)_root.InverseTransformPoint(pc.Root.position);
                Burst(local, 30, pc.Def.Tier == AugTier.Prism, 420f, TierColor(pc.Def.Tier, 0f));
            }
        }

        void Reroll()
        {
            if (_world == null || _picked >= 0) return;
            if (_world.RerollAugments()) { Show(_world.Hero.AugOffer); OnChanged?.Invoke(); }
        }

        // ---------- art ----------

        /// <summary>Sixteen soft rays round a bright core (drawn once; turned and tinted per card).</summary>
        static Sprite MakeRays(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float h = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - h, dy = y + 0.5f - h;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / h;
                    float ang = Mathf.Atan2(dy, dx);
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 8f)), 5f);
                    float fall = Mathf.Clamp01(1f - r);
                    float a = Mathf.Clamp01(ray * fall * fall * 1.2f + fall * fall * fall * 0.5f);
                    byte b = (byte)Mathf.RoundToInt(255f * a);
                    px[y * size + x] = new Color32(255, 255, 255, b);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f);
        }
    }
}
