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
            LoadHistory();
        }

        /// <summary>Unity may skip the last 10 s of notices on a quit; flush them.</summary>
        public void OnApplicationQuit() => SaveHistory(true);

        public void OnTick(List<SimEvent> events, SimWorld world)
        {
            foreach (var ev in events)
            {
                switch (ev.Type)
                {
                    case SimEventType.Drop when ItemStyle.IsJackpot(ev.Item):
                    {
                        int stars = ev.Item.GreaterCount;
                        // The rarity in pips and words beside the colour (UX plan A1), as on the tooltip's name row.
                        Push("drop" + ev.Item.Uid, "<color=" + ItemStyle.Hex(ev.Item.Rarity) + ">" + (stars > 0 ? new string('★', stars) + " " : "") + ev.Item.Name + "</color>"
                             + "  " + ItemStyle.Tag(ev.Item.Rarity),
                            ItemStyle.Icon(ev.Item), ItemStyle.Color(ev.Item.Rarity), ev.Item.Rarity >= Rarity.Legendary ? Weight.Big : Weight.Normal);
                        break;
                    }
                    // An auto-equip carries the 전투력 it gained (always +1% or more); the camp's masterwork, reroll and
                    // temper raise the same event with no value and keep the older line below.
                    case SimEventType.Equip when ev.Item != null && ev.Value > 0f:
                        AutoEquipped(ev.Item, 1f + ev.Value, world.LastAutoSwap != null && world.LastAutoSwap.New == ev.Item ? world.LastAutoSwap.Old : null);
                        break;
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
            TrackSalvage(events, world);
        }

        // ---------- 알림 기록: what the automation did, batched (UX audit 11-23 P0-4) ----------

        SimWorld _world;
        readonly HashSet<int> _bagUids = new HashSet<int>();
        bool _bagSeeded;
        readonly int[] _salvaged = new int[16];
        int _salvAsh, _treeSeen = -1, _treePending;
        float _lastFlush = -999f, _lastTreeCheck;
        const float FlushEvery = 10f;

        /// <summary>
        /// The sim says nothing when 자동 분해 or a full bag turns a piece to ash, so the view infers it: a tick that dropped
        /// something and lost a piece that is not in the bag or worn any more. Manual salvage never has a drop in the same tick.
        /// </summary>
        void TrackSalvage(List<SimEvent> events, SimWorld world)
        {
            _world = world;
            bool dropped = false;
            foreach (var ev in events) if (ev.Type == SimEventType.Drop && ev.Item != null) { dropped = true; break; }
            if (dropped && _bagSeeded)
            {
                var gone = new HashSet<int>();
                foreach (var ev in events)
                    if (ev.Type == SimEventType.Drop && ev.Item != null && !Kept(world, ev.Item) && gone.Add(ev.Item.Uid)) CountSalvaged(ev.Item);
                var kept = new HashSet<int>();
                foreach (var it in world.Bag.Items) kept.Add(it.Uid);
                // Pieces from the previous snapshot that left the bag (a full bag's weakest, the piece an auto-equip replaced).
                foreach (int uid in _bagUids)
                {
                    if (kept.Contains(uid) || gone.Contains(uid) || WornUid(world, uid)) continue;
                    var it = LastSeen(uid);
                    if (it != null) { gone.Add(uid); CountSalvaged(it); }
                }
            }
            _bagUids.Clear();
            _bagItems.Clear();
            foreach (var it in world.Bag.Items) { _bagUids.Add(it.Uid); _bagItems[it.Uid] = it; }
            _bagSeeded = true;
            Flush();
        }

        readonly Dictionary<int, Item> _bagItems = new Dictionary<int, Item>();
        Item LastSeen(int uid) => _bagItems.TryGetValue(uid, out var it) ? it : null;

        static bool Kept(SimWorld w, Item item) => w.Bag.Items.Contains(item) || WornUid(w, item.Uid);

        static bool WornUid(SimWorld w, int uid)
        {
            foreach (var it in w.Hero.Gear.Spots) if (it != null && it.Uid == uid) return true;
            return false;
        }

        void CountSalvaged(Item it)
        {
            _salvaged[Mathf.Clamp((int)it.Rarity, 0, _salvaged.Length - 1)]++;
            _salvAsh += LootFilter.SalvageValue(it);
        }

        /// <summary>Writes the pending automation lines to the journal only (no card) at most once per FlushEvery seconds.</summary>
        void Flush()
        {
            if (_world == null) return;
            float now = Time.unscaledTime;
            if (now - _lastTreeCheck >= 1f)
            {
                _lastTreeCheck = now;
                int sp = SkillTree.Spent(_world.Hero);
                if (_treeSeen >= 0 && _world.Hero.TreeAuto && sp > _treeSeen) _treePending += sp - _treeSeen;
                _treeSeen = sp;
            }
            if (now - _lastFlush < FlushEvery) return;
            bool any = false;
            var sb = new System.Text.StringBuilder();
            int total = 0;
            for (int r = 0; r < _salvaged.Length; r++)
            {
                if (_salvaged[r] == 0) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append("<color=").Append(ItemStyle.Hex((Rarity)r)).Append('>').Append(ItemStyle.RarityName((Rarity)r)).Append("</color> ").Append(_salvaged[r]);
                total += _salvaged[r];
                _salvaged[r] = 0;
            }
            if (total > 0)
            {
                AddHistory(sb + "개 분해 · 잿 파편 +" + _salvAsh.ToString("N0"));
                _salvAsh = 0;
                any = true;
            }
            if (_treePending > 0)
            {
                AddHistory("스킬 트리 포인트 " + _treePending + "개를 자동 분배했습니다");
                _treePending = 0;
                any = true;
            }
            if (any) _lastFlush = now;
        }

        void AddHistory(string text)
        {
            History.Add((System.DateTime.Now, text));
            if (History.Count > HistoryMax) History.RemoveAt(0);
            _historyDirty = true;
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
        public const int HistoryMax = 50; // the automation lines share the list with the notices

        // ---------- the history survives sessions (UX loop 2 #14): PlayerPrefs, one string, capped ----------

        const string HistoryKey = "journal_history";
        const char FieldSep = '\u001f', RecordSep = '\u001e'; // control chars never appear in a notice
        const int HistoryCharCap = 24000;
        bool _historyDirty;
        float _historySavedAt;

        void LoadHistory()
        {
            try
            {
                string raw = PlayerPrefs.GetString(HistoryKey, "");
                if (raw.Length == 0 || raw.Length > HistoryCharCap) return; // an oversized blob is not ours: start empty
                foreach (var rec in raw.Split(RecordSep))
                {
                    var f = rec.Split(FieldSep);
                    if (f.Length == 2 && long.TryParse(f[0], out long ticks) && ticks > 0 && ticks <= System.DateTime.MaxValue.Ticks)
                        History.Add((new System.DateTime(ticks), f[1]));
                }
                while (History.Count > HistoryMax) History.RemoveAt(0);
            }
            catch { History.Clear(); }
        }

        /// <summary>Writes the history at most every 10 s and only when it changed (a PlayerPrefs.Save per notice would stutter).</summary>
        void SaveHistory(bool force = false)
        {
            if (!_historyDirty || (!force && Time.unscaledTime - _historySavedAt < 10f)) return;
            _historyDirty = false;
            _historySavedAt = Time.unscaledTime;
            try
            {
                var sb = new System.Text.StringBuilder();
                for (int i = History.Count - 1; i >= 0 && sb.Length < HistoryCharCap - 600; i--) // newest first when the cap bites
                {
                    if (sb.Length > 0) sb.Insert(0, RecordSep);
                    sb.Insert(0, History[i].at.Ticks.ToString() + FieldSep + History[i].text);
                }
                PlayerPrefs.SetString(HistoryKey, sb.ToString());
                PlayerPrefs.Save();
            }
            catch { }
        }

        /// <summary>A plain notice from outside the sim (first-run tips).</summary>
        public void Tip(string text) => Push("tip" + text, "<color=#cfe6ff>" + text + "</color>", null, new Color(0.65f, 0.8f, 1f), Weight.Quiet);

        /// <summary>A key changed a setting (UX audit 11-23 #11: S·P·F·O·L·X changed the build in silence): say what it is now.</summary>
        public void Setting(string text) => Push("set" + text, "<color=#ffe7b0>" + text + "</color>", null, new Color(1f, 0.85f, 0.5f), Weight.Normal);

        /// <summary>UX plan A7: a hand salvage says what went and how to take it back; five seconds, as long as Z answers.</summary>
        public void Salvaged(Item item) =>
            Push("salvaged" + item.Uid, "분해: <color=" + ItemStyle.Hex(item.Rarity) + ">" + item.Name + "</color> · <color=#ffd38a>Z</color> 되돌리기",
                ItemStyle.Icon(item), new Color(1f, 0.6f, 0.35f), Weight.Normal, null, 5f);

        /// <summary>
        /// UX plan A6 (자동이 한 일은 보고한다): every auto-equip says what it swapped and by how much — "무기 교체 ▲ +12%
        /// 혜성 지팡이" — where only +5% or legendary ones used to (audit #7: the gear changed and nobody said so). One card
        /// per slot, so a run of weapon swaps counts up on the latest; each one is kept in the history all the same.
        /// </summary>
        /// <param name="power">The 전투력 ratio of the swap (1.12 = +12%).</param>
        public void AutoEquipped(Item item, float power, Item old = null)
        {
            var arrow = power >= HeroStats.BigUpAt ? GearArrow.BigUp : GearArrow.Up;
            Push("equip" + item.Slot, ItemTooltip.SlotName(item.Slot) + " 교체 <color=" + ItemStyle.ArrowHex(arrow) + ">" + ItemStyle.ArrowGlyph(arrow) + "</color> "
                 + ItemStyle.Pct(power) + "  <color=" + ItemStyle.Hex(item.Rarity) + ">" + item.Name + "</color>",
                ItemStyle.Icon(item), new Color(0.36f, 0.84f, 0.42f), Weight.Normal,
                // The journal line names both pieces: what it took off, what it put on, and the 전투력 gained.
                "<color=" + ItemStyle.Hex(item.Rarity) + ">" + item.Name + "</color> 장착"
                + (old != null ? " ← <color=" + ItemStyle.Hex(old.Rarity) + ">" + old.Name + "</color>" : " (" + ItemTooltip.SlotName(item.Slot) + ")")
                + "  <color=" + ItemStyle.ArrowHex(arrow) + ">전투력 " + ItemStyle.Pct(power) + "</color>");
        }

        /// <summary>재기획 §7.5 정체 알림: the hunt has not passed its best floor for a while and keeps dying.</summary>
        public void Stuck(int minutes, int deaths) =>
            Push("stuck", "<color=#ffb070>" + minutes + "분째 같은 곳에서 막혀 있어요</color>  <color=#8a8178>사망 " + deaths + " · 장비·트리 확인 [Tab][T]</color>",
                "ui/ui3_menu_gear", new Color(1f, 0.6f, 0.35f), Weight.Big);

        void Push(string key, string text, string icon, Color bar, Weight weight, string journal = null, float life = 0f)
        {
            History.Add((System.DateTime.Now, journal ?? text));
            if (History.Count > HistoryMax) History.RemoveAt(0);
            _historyDirty = true;
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
            card.Life = life > 0f ? life : weight == Weight.Big ? 6.5f : weight == Weight.Quiet ? 3.5f : 4.5f;
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
            Flush();
            SaveHistory();
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
