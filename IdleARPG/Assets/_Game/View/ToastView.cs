using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Top-left notices (재기획 §5.8) as small cards on the GUI canvas: an icon (the item, the skill, the monster), a
    /// colour bar by kind or rarity, the line. The same kind within its life counts up "×N" instead of stacking.
    /// Three weights: quiet (level steps, tips), normal, big (legendary or better, evolutions, act bosses — a glow and a
    /// longer stay). Four at most, newest on top. Everything also goes to the 사냥 일지 history.
    /// </summary>
    public sealed class ToastView
    {
        enum Weight { Quiet, Normal, Big }

        sealed class Card
        {
            public RectTransform Root;
            public Image Glow, Back, Bar, Icon;
            public Text Text;
            public CanvasGroup Group;
            public string Key;
            public int Count;
            public float T, Life;
            public bool Live;
            public Weight W;
        }

        readonly UiKit _ui;
        readonly RectTransform _root;
        readonly List<Card> _cards = new List<Card>();
        const int Max = 4;
        const float CardW = 240f, CardH = 30f, Gap = 4f;

        /// <summary>F2 hides the screen furniture: the cards keep counting in the journal, unseen.</summary>
        public bool Hidden { get => !_root.gameObject.activeSelf; set => _root.gameObject.SetActive(!value); }

        public ToastView(UiKit ui)
        {
            _ui = ui;
            // Made right after the GUI canvas: every window built later draws over it, the HUD goes first under it.
            _root = UiKit.Rect(ui.Root, "Toasts", 8f, 8f, CardW, (CardH + Gap) * Max);
        }

        public void OnTick(List<SimEvent> events, SimWorld world)
        {
            foreach (var ev in events)
            {
                switch (ev.Type)
                {
                    case SimEventType.Drop when ItemStyle.IsJackpot(ev.Item):
                    {
                        int stars = ev.Item.GreaterCount;
                        Push("drop" + ev.Item.Uid, "<color=" + ItemStyle.Hex(ev.Item.Rarity) + ">" + (stars > 0 ? new string('★', stars) + " " : "") + ev.Item.Name + "</color>"
                             + " <color=#8a8178>" + ItemStyle.RarityName(ev.Item.Rarity) + "</color>",
                            ItemStyle.Icon(ev.Item), ItemStyle.Color(ev.Item.Rarity), ev.Item.Rarity >= Rarity.Legendary ? Weight.Big : Weight.Normal);
                        break;
                    }
                    case SimEventType.Equip when ev.Item != null && (ev.Value >= 0.05f || ev.Item.Rarity >= Rarity.Legendary):
                        Push("equip", "장착 · <color=" + ItemStyle.Hex(ev.Item.Rarity) + ">" + ev.Item.Name + "</color>"
                             + (ev.Value > 0.005f ? "  <color=#9fd28a>피해 +" + (ev.Value * 100f).ToString("0") + "%</color>" : ""),
                            ItemStyle.Icon(ev.Item), new Color(0.6f, 0.9f, 0.55f), Weight.Normal);
                        break;
                    case SimEventType.Evolved:
                        Push("evo" + ev.SkillId, "<color=#ffc060>진화 · " + (SkillBook.Get(ev.SkillId)?.Name ?? ev.SkillId) + "</color>",
                            SkillIcon(ev.SkillId), new Color(1f, 0.7f, 0.3f), Weight.Big);
                        break;
                    case SimEventType.SkillLevelUp when (int)ev.Value % SkillBook.CopyEvery == 0:
                        Push("copy" + ev.SkillId, "<color=#ffd890>" + ev.Text + " 숙련 " + (int)ev.Value + " · 분신 +1</color>", SkillIcon(ev.SkillId), UiKit.Gold, Weight.Normal);
                        break;
                    case SimEventType.WickLevelUp:
                        Push("wick", "<color=#ffe7a0>심지 " + (int)ev.Value + "</color>", "ui/ui2_lantern_hang", new Color(1f, 0.9f, 0.6f), Weight.Normal);
                        break;
                    case SimEventType.Achievement:
                        Push("ach" + ev.SkillId, "<color=#f2d98c>도전 과제 · " + ev.SkillId + "</color>"
                             + ((int)ev.Value > 0 ? "  <color=#8a8178>등불 색 " + SimWorld.LanternColors[(int)ev.Value].name + " [X]</color>" : ""),
                            "ui/ui3_menu_codex", new Color(0.95f, 0.85f, 0.55f), Weight.Normal);
                        break;
                    case SimEventType.Medal:
                        Push("medal" + ev.SkillId, "<color=#e8d8a8>도감 메달 · " + ev.SkillId + " (" + SimWorld.MedalName((int)ev.Value) + ")</color>",
                            "ui/ui3_menu_codex", new Color(0.9f, 0.8f, 0.55f), Weight.Normal);
                        break;
                    case SimEventType.CampUnlocked:
                        Push("camp", "<color=#ffcc80>야영지 개방</color>  <color=#8a8178>[Y]</color>", "ui/ui3_menu_camp", new Color(1f, 0.8f, 0.5f), Weight.Big);
                        break;
                    case SimEventType.Unlocked:
                        Push("unlock" + ev.SkillId, "<color=#b8f0c8>" + ev.SkillId + "</color>", "ui/ui3_menu_tree", new Color(0.7f, 0.95f, 0.78f), Weight.Normal);
                        break;
                    case SimEventType.LevelUp when (int)ev.Value == SimWorld.LevelCap:
                        Push("cap", "<color=#ffd38a>Lv " + SimWorld.LevelCap + " · 환생 가능 (야영지 [Y])</color>", "ui/ui3_menu_camp", UiKit.Gold, Weight.Big);
                        break;
                    case SimEventType.LevelUp when (int)ev.Value % 10 == 0:
                        Push("level", "<color=#ffd38a>Lv " + (int)ev.Value + "</color>", null, UiKit.Gold, Weight.Quiet);
                        break;
                    case SimEventType.Rebirth:
                        Push("rebirth", "<color=#ffb070>환생 · 잔불 " + (int)ev.Value + "</color>", "prop/camp_campfire", new Color(1f, 0.6f, 0.3f), Weight.Big);
                        break;
                    case SimEventType.Letter:
                        Push("letter", "<color=#d8c8f2>새 기록 · " + ev.SkillId + "</color>  <color=#8a8178>[U]</color>", "ui/ui3_menu_mail", new Color(0.8f, 0.72f, 0.95f), Weight.Normal);
                        break;
                    case SimEventType.Death when FirstOfItsKind(world, ev.TargetId, out string newName, out string defId):
                        Push("codex", "<color=#c8d8b0>도감 · 신규 적: " + newName + "</color>  <color=#8a8178>[J]</color>", EnemyIcon(defId), new Color(0.72f, 0.82f, 0.6f), Weight.Normal);
                        break;
                    case SimEventType.Death when world.IsActBossFloor && IsActBoss(world, ev.TargetId):
                        Push("boss", "<color=#ffb070>막 " + world.ActNow.Numeral + " 보스 처치 · " + world.ActNow.Boss.Name + "</color>",
                            EnemyIcon(world.ActNow.Boss.Id), new Color(1f, 0.55f, 0.4f), Weight.Big);
                        break;
                }
            }
        }

        string SkillIcon(string id)
        {
            var s = SkillBook.Get(id);
            string key = "icon/skill_" + id;
            if (_ui.Catalog.Has(key)) return key;
            return s != null ? "icon/skill_" + s.BaseId : null;
        }

        string EnemyIcon(string defId) => defId == null ? null : WorldView.EnemyArt(_ui.Catalog, defId, out _, out _);

        /// <summary>The codex just counted its first of this kind (a new monster met in a new act).</summary>
        readonly HashSet<string> _seen = new HashSet<string>();
        bool _seeded;

        bool FirstOfItsKind(SimWorld w, int id, out string name, out string defId)
        {
            name = null;
            defId = null;
            // Kinds already in the codex when this view started are not new (a pack of firsts in one tick still counts once).
            if (!_seeded) { _seeded = true; foreach (var kv in w.Hero.KillsById) if (kv.Value > 0 && !JustKilled(w, kv.Key)) _seen.Add(kv.Key); }
            foreach (var e in w.Enemies)
            {
                if (e.Id != id) continue;
                if (e.Summoned || e.Boss || !_seen.Add(e.Def.Id)) return false;
                name = e.Def.Name;
                defId = e.Def.Id;
                return !string.IsNullOrEmpty(name);
            }
            return false;
        }

        /// <summary>Seeding on the very first tick: a kind whose whole count died this tick is still new.</summary>
        static bool JustKilled(SimWorld w, string defId)
        {
            int now = 0;
            foreach (var ev in w.Events)
                if (ev.Type == SimEventType.Death)
                    foreach (var e in w.Enemies) if (e.Id == ev.TargetId && e.Def.Id == defId) now++;
            return now > 0 && now >= w.KillsOf(defId);
        }

        static bool IsActBoss(SimWorld w, int id)
        {
            foreach (var e in w.Enemies) if (e.Id == id) return e.Boss && !e.Summoned;
            return false;
        }

        /// <summary>사냥 일지: the last notices with the real time they came, for someone who looked away (G).</summary>
        public readonly List<(System.DateTime at, string text)> History = new List<(System.DateTime, string)>();
        public const int HistoryMax = 30;

        /// <summary>A plain notice from outside the sim (first-run tips).</summary>
        public void Tip(string text) => Push("tip" + text, "<color=#cfe6ff>" + text + "</color>", null, new Color(0.65f, 0.8f, 1f), Weight.Quiet);

        /// <summary>재기획 §7.5 정체 알림: the hunt has not passed its best floor for a while and keeps dying.</summary>
        public void Stuck(int minutes, int deaths) =>
            Push("stuck", "<color=#ffb070>" + minutes + "분째 같은 곳에서 막혀 있어요</color>  <color=#8a8178>사망 " + deaths + " · 장비·트리 확인 [Tab][T]</color>",
                "ui/ui3_menu_gear", new Color(1f, 0.6f, 0.35f), Weight.Big);

        void Push(string key, string text, string icon, Color bar, Weight weight)
        {
            History.Add((System.DateTime.Now, text));
            if (History.Count > HistoryMax) History.RemoveAt(0);
            if (!GameSettings.Notices) return; // off in settings: the journal still keeps it

            // The same kind still up counts up and stays (equips, codex firsts and level steps come in runs).
            foreach (var c in _cards)
                if (c.Live && c.Key == key)
                {
                    c.Count++;
                    c.Text.text = text + "  <color=#8a8178>×" + c.Count + "</color>";
                    c.T = Mathf.Min(c.T, 0.2f);
                    Fit(c);
                    return;
                }

            Card card = null;
            int live = 0;
            foreach (var x in _cards) if (x.Live) live++;
            if (live >= Max) foreach (var x in _cards) if (x.Live && (card == null || x.T > card.T)) card = x; // the oldest goes
            if (card == null) card = _cards.Find(x => !x.Live);
            if (card == null) { card = NewCard(); _cards.Add(card); }
            card.Key = key;
            card.Count = 1;
            card.W = weight;
            card.Life = weight == Weight.Big ? 6.5f : weight == Weight.Quiet ? 3.5f : 4.5f;
            card.T = 0f;
            card.Live = true;
            card.Text.text = text;
            bool hasIcon = icon != null && _ui.Catalog.Has(icon);
            card.Icon.enabled = hasIcon;
            if (hasIcon) card.Icon.sprite = _ui.Catalog.Get(icon);
            card.Text.rectTransform.anchoredPosition = new Vector2(hasIcon ? 34f : 10f, -8f);
            card.Bar.color = bar;
            card.Root.gameObject.SetActive(true);
            Fit(card);
        }

        /// <summary>The card grows with its line (never narrower than the standard width).</summary>
        static void Fit(Card c)
        {
            float w = Mathf.Max(CardW, Mathf.Ceil(c.Text.rectTransform.anchoredPosition.x + c.Text.preferredWidth + 10f));
            c.Root.sizeDelta = new Vector2(w, CardH);
            c.Back.rectTransform.sizeDelta = new Vector2(w, CardH);
            c.Glow.rectTransform.sizeDelta = new Vector2(w + 32f, CardH + 20f);
            c.Text.rectTransform.sizeDelta = new Vector2(w - 10f - c.Text.rectTransform.anchoredPosition.x, 14f);
        }

        Card NewCard()
        {
            var c = new Card();
            c.Root = UiKit.Rect(_root, "toast", 0f, 0f, CardW, CardH);
            c.Group = c.Root.gameObject.AddComponent<CanvasGroup>();
            c.Group.blocksRaycasts = false; // notices never catch clicks meant for the world
            c.Glow = UiKit.Fill(c.Root, "glow", -16f, -10f, CardW + 32f, CardH + 20f, Color.clear);
            c.Glow.sprite = UiKit.Glow(_ui, 64);
            c.Back = UiKit.Fill(c.Root, "back", 0f, 0f, CardW, CardH, new Color(0.03f, 0.027f, 0.035f, 0.88f));
            c.Bar = UiKit.Fill(c.Root, "bar", 0f, 0f, 3f, CardH, Color.white);
            c.Icon = _ui.Sprite(c.Root, "ui/dot", 6f, 3f, 24f, 24f);
            c.Text = UiKit.Label(c.Root, "", 34f, 8f, CardW - 40f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            c.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return c;
        }

        public void Render(float dt)
        {
            dt = Mathf.Min(dt, 0.1f); // a stalled frame (window switch, load) must not eat the notices whole
            // Newest on top.
            _cards.Sort((a, b) => a.Live != b.Live ? (a.Live ? -1 : 1) : a.T.CompareTo(b.T));
            int row = 0;
            foreach (var c in _cards)
            {
                if (!c.Live) continue;
                c.T += dt;
                if (c.T >= c.Life) { c.Live = false; c.Root.gameObject.SetActive(false); continue; }
                // Slides in from the left, fades out; positions step to whole UI pixels.
                float slide = Mathf.Round(Mathf.Lerp(-30f, 0f, Mathf.Clamp01(c.T / 0.18f)));
                c.Root.anchoredPosition = new Vector2(slide, -row * (CardH + Gap));
                float a = Mathf.Clamp01(c.T / 0.15f) * Mathf.Clamp01((c.Life - c.T) / 0.6f);
                a = Mathf.Round(a * 8f) / 8f; // eight steps: the canvas rebuilds a few times per card, not every frame
                if (!Mathf.Approximately(c.Group.alpha, a)) c.Group.alpha = a;
                var glow = c.W == Weight.Big ? new Color(c.Bar.color.r, c.Bar.color.g, c.Bar.color.b, 0.35f) : Color.clear;
                if (c.Glow.color != glow) c.Glow.color = glow;
                row++;
            }
        }
    }
}
