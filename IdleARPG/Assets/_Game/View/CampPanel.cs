using System;
using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 야영지 (GDD §14, 재기획 §5.9): a small diorama hub instead of a text menu. Six stations stand around the fire —
    /// blacksmith, oil store, cartographer, the fire itself (rebirth), the transmute altar and the merchant — drawn with
    /// the camp props we have; click one to see its card below with the action and its price. The blacksmith and the
    /// merchant work on the equipment spot picked in the row of worn pieces; reroll works on the affix line picked in
    /// the item card. Keys still work: C toggles, Q/E spot, A/D line, 1–8 the actions top to bottom.
    /// </summary>
    public sealed class CampPanel
    {
        const float W = 880f, H = 480f;
        /// <summary>Mirrors Item.Sum: every stat of the piece gets +8% of its base per 명품화 step (Sim/Items.cs).</summary>
        const int MasterworkStepPct = 8;
        readonly UiKit _ui;
        readonly SimWorld _world;
        readonly RectTransform _win, _detail;
        readonly Text _purse, _quoteText, _note;
        readonly Image[] _stationGlow = new Image[6];
        readonly Text[] _stationName = new Text[6];
        readonly SlotView[] _spots = new SlotView[9];
        readonly Image[] _spotMark = new Image[9];
        readonly ItemTooltip _tip;
        readonly List<GameObject> _cardBits = new List<GameObject>(); // the station card, rebuilt on a change
        public bool Visible { get; private set; }
        public Action OnChanged;

        int _station, _spot, _line, _quote, _stamp = -1;
        string _noteText = "";
        float _noteAt = -10f;

        /// <summary>Stations: name, prop, drawn size, left x, what it is for, and the grey one-liner under its name.</summary>
        static readonly (string name, string prop, float size, float x, string what, string fn)[] Stations =
        {
            ("대장장이", "prop/camp_blacksmith_forge", 128f, 24f, "명품화 모든 옵션 강화 · 재굴림 옵션 한 줄 다시 뽑기 · 재련 옵션 줄 추가", "장비 강화"),
            ("기름 저장고", "prop/camp_oil_store", 96f, 168f, "오프라인 사냥 가능 시간을 늘린다.", "방치 시간 늘리기"),
            ("지도 제작자", "prop/camp_cartographer_table", 96f, 280f, "사냥터 선택 방식을 하나씩 개방한다.", "사냥 방식 개방"),
            ("잔불", "prop/camp_campfire", 64f, 408f, "레벨 상한에서 환생한다. 장비는 그대로 유지하고, 잔불만큼 강해져 다시 내려간다.", "환생 · 다시 시작"),
            ("변환 제단", "prop/shrine_altar", 96f, 560f, "가장 많이 모인 룬 3개를 다음 룬 하나로 변환한다.", "룬 승급"),
            ("수상한 행상인", "prop/merchant", 128f, 690f, "고른 부위의 미확인 장비 판매 · 마법 이상 · 아이템 발견 +150%", "미확인 장비 구매"),
        };

        /// <summary>One speaker per station (the index into <see cref="Stations"/>); the fire and the altar do not talk.</summary>
        static readonly (int station, string line)[] Quotes =
        {
            (0, "대장장이: \"쇳물도 별에서 왔지. 나는 그걸 다시 별처럼 벼릴 뿐이야.\""),
            (0, "대장장이: \"무른 쇠는 없어. 덜 두드린 쇠가 있을 뿐.\""),
            (1, "기름지기: \"기름이 마르면 등불도, 당신도 끝이야. 아껴 쓰라고.\""),
            (1, "기름지기: \"당신이 잠든 사이에도 심지는 탄다. 그걸 지키는 게 내 일이지.\""),
            (2, "지도 제작자: \"길은 내가 그려. 내려가는 건 당신 몫이고.\""),
            (2, "지도 제작자: \"아래로 갈수록 지도가 젖어. 핏덩이 소굴 근처는 특히.\""),
            (5, "수상한 상인: \"안을 들여다보면 값이 오르지. 그러니 묻지 마.\""),
            (5, "수상한 상인: \"전설이 나오면 내 운이고, 안 나오면 당신 운이지.\""),
        };

        /// <summary>The line of the picked station only (it used to be one random line for the whole camp).</summary>
        string QuoteFor(int station)
        {
            int n = 0;
            foreach (var q in Quotes) if (q.station == station) n++;
            if (n == 0) return "";
            int pick = _quote % n;
            foreach (var q in Quotes) if (q.station == station && pick-- == 0) return "<i>" + q.line + "</i>";
            return "";
        }

        public CampPanel(UiKit ui, SimWorld world)
        {
            _ui = ui;
            _world = world;
            _win = ui.Window("Camp Panel", W, H, "야영지", Toggle, out _, "ui/ui2_window_iron");
            _purse = UiKit.Label(_win, "", W - 330f, 8f, 280f, 14f, 12, TextAnchor.UpperRight, UiKit.Ink);

            // ---------- the diorama: a pool of firelight, the tent behind the fire, the stations around it ----------
            const float ground = 196f;
            var pool = UiKit.Fill(_win, "firelight", 200f, 40f, 480f, 220f, new Color(1f, 0.62f, 0.3f, 0.26f));
            pool.sprite = UiKit.Glow(ui, 64);
            UiKit.Fill(_win, "ground line", 16f, ground + 2f, W - 32f, 1f, UiKit.Line);
            if (ui.Catalog.Has("prop/camp_tent")) ui.Sprite(_win, "prop/camp_tent", 376f, ground - 118f, 128f, 128f).color = new Color(0.7f, 0.66f, 0.62f);
            for (int i = 0; i < Stations.Length; i++)
            {
                int index = i;
                var st = Stations[i];
                var glow = UiKit.Fill(_win, "station glow", st.x - 20f, ground - st.size - 10f, st.size + 40f, st.size + 30f, new Color(1f, 0.8f, 0.45f, 0f));
                glow.sprite = UiKit.Glow(ui, 64);
                _stationGlow[i] = glow;
                var prop = ui.Sprite(_win, ui.Catalog.Has(st.prop) ? st.prop : "ui/dot", st.x, ground - st.size, st.size, st.size);
                prop.raycastTarget = true;
                _stationName[i] = UiKit.Label(_win, st.name, st.x - 20f, ground + 6f, st.size + 40f, 14f, 12, TextAnchor.UpperCenter, UiKit.Dim);
                UiKit.Label(_win, st.fn, st.x + st.size * 0.5f - 52f, ground + 20f, 104f, 12f, 11, TextAnchor.UpperCenter, new Color(0.46f, 0.44f, 0.41f));
                var p = prop.gameObject.AddComponent<UiPointer>();
                p.LeftClick = () => { _station = index; _stamp = -1; };
                p.Enter = () => { if (_station != index) _stationName[index].color = UiKit.Ink; };
                p.Exit = () => { if (_station != index) _stationName[index].color = UiKit.Dim; };
            }
            // 28 px below the facility captions (11-23 loop 2 #13: the quote ran into them and read as one block).
            _quoteText = UiKit.Label(_win, "", 16f, ground + 61f, W - 32f, 14f, 12, TextAnchor.UpperCenter, new Color(0.66f, 0.61f, 0.55f));

            // ---------- the station card ----------
            _detail = UiKit.Rect(_win, "detail", 16f, ground + 74f, W - 32f, H - ground - 98f);
            var back = _detail.gameObject.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.3f);
            back.raycastTarget = false;
            _note = UiKit.Label(_win, "", 16f, H - 22f, W - 32f, 14f, 12, TextAnchor.UpperRight, UiKit.Dim);

            // the worn pieces (blacksmith and merchant pick a spot here)
            _tip = new ItemTooltip(ui, "Camp Tooltip");
            for (int i = 0; i < 9; i++)
            {
                int spot = i;
                var sv = _spots[i] = new SlotView(ui, _detail, 12f + i * 40f, 26f, 36f, 36f, SlotStyle.Cell, "ui/ui2_cell", "ui/ui2_cell_hover");
                _spotMark[i] = UiKit.Fill(_detail, "picked", 14f + i * 40f, 64f, 32f, 2f, UiKit.Gold);
                sv.Pointer.LeftClick += () => { _spot = spot; _line = 0; _stamp = -1; };
                sv.Pointer.Enter += () => { if (sv.Item != null) { _tip.Show(sv.Item, ItemStyle.SpotName((EquipSpot)spot)); _tip.PlaceBeside(sv.Root); } };
                sv.Pointer.Exit += () => _tip.Hide();
            }
            SetVisible(false);
        }

        public void Toggle() => SetVisible(!Visible);

        void SetVisible(bool v)
        {
            if (v && !Visible) _quote = UnityEngine.Random.Range(0, 1000);
            Visible = v;
            _win.gameObject.SetActive(v);
            if (!v) { _tip.Hide(); foreach (var s in _spots) s.ClearHover(); }
            else _win.SetAsLastSibling();
            _stamp = -1;
        }

        public void NextSpot(int step)
        {
            int n = _world.Hero.Gear.Spots.Length;
            _spot = ((_spot + step) % n + n) % n;
            _line = 0;
            _stamp = -1;
        }

        /// <summary>A/D: which affix line of the chosen item to reroll.</summary>
        public void NextLine(int step)
        {
            var item = _world.Hero.Gear.Spots[_spot];
            int n = item?.Affixes.Count ?? 0;
            _line = n == 0 ? 0 : ((_line + step) % n + n) % n;
            _stamp = -1;
        }

        /// <summary>1 명품화 · 2 재굴림 · 3 재련 · 4 저장고 · 5 지도 제작자 · 6 변환 제단 · 7 상인 · 8 환생.</summary>
        public bool Buy(int which)
        {
            bool ok = which == 0 ? _world.Masterwork((EquipSpot)_spot) : which == 1 ? _world.Reroll((EquipSpot)_spot, _line)
                : which == 2 ? _world.Temper((EquipSpot)_spot) : which == 3 ? _world.UpgradeOilStore()
                : which == 4 ? _world.UpgradeCartographer() : which == 5 ? _world.TransmuteRune()
                : which == 6 ? _world.Gamble(Equipment.SlotOf((EquipSpot)_spot)) != null : _world.Rebirth();
            _noteText = ok ? "<color=#9fd28a>완료</color>" : "<color=#d07a6a>재료가 부족하거나 최대 단계에 도달했습니다</color>";
            _noteAt = Time.unscaledTime;
            // The key's station comes forward so the result is in view.
            _station = which <= 2 ? 0 : which == 3 ? 1 : which == 4 ? 2 : which == 5 ? 4 : which == 6 ? 5 : 3;
            _stamp = -1;
            if (ok) OnChanged?.Invoke();
            return ok;
        }

        static string Cost((int ash, int oil) c) => $"잿 파편 {c.ash} · 등불 기름 {c.oil}";

        public void Render()
        {
            if (!Visible) return;
            float t = Time.unscaledTime;
            // The picked station breathes in the firelight.
            for (int i = 0; i < Stations.Length; i++)
                _stationGlow[i].color = new Color(1f, 0.8f, 0.45f, i == _station ? 0.32f + 0.08f * Mathf.Sin(t * 2.5f) : 0f);
            if (_noteText.Length > 0 && t - _noteAt > 4f) { _noteText = ""; _stamp = -1; }

            var w = _world;
            int stamp = 17;
            unchecked
            {
                // Everything the card reads: the hunt goes on while the camp is open (auto-equip, runes, area level).
                foreach (int x in new[] { w.Bag.AshShards, w.Bag.LanternOil, w.OilStoreLevel, w.CartographerLevel, _spot, _line, _station,
                    w.CampUnlocked ? 1 : 0, _noteText.Length, w.Hero.Level, (int)w.TransmuteFrom, w.PendingEmber, w.GambleCost, w.CanRebirth ? 1 : 0 })
                    stamp = stamp * 31 + x;
                foreach (var it in w.Hero.Gear.Spots)
                    stamp = stamp * 31 + (it == null ? 0 : it.Uid * 7 + it.Masterwork * 3 + it.Rerolls + it.Affixes.Count * 11 + it.StabilityLeft);
                foreach (int n in w.Bag.RuneCounts) stamp = stamp * 31 + n;
            }
            if (stamp == _stamp) return;
            _stamp = stamp;
            Rebuild();
        }

        Text CardText(string text, float x, float y, float w, float h, Color? c = null)
        {
            var l = UiKit.Label(_detail, text, x, y, w, h, 12, TextAnchor.UpperLeft, c);
            _cardBits.Add(l.gameObject);
            return l;
        }

        void CardButton(string text, float x, float y, float w, int which, bool enabled)
        {
            // Critique 6 #8: "name · effect" on the left, the price on the right (one run-on line read as a sentence).
            int at = text.IndexOf(" · 잿 파편", StringComparison.Ordinal);
            string left = at >= 0 ? text.Substring(0, at) : text, price = at >= 0 ? text.Substring(at + 3) : null;
            var b = _ui.Button(_detail, left, x, y, w, 21f, () => Buy(which));
            b.interactable = enabled;
            // A refused button reads as a dark bar with light text (grey on grey was unreadable) and says why in its words.
            var cb = b.colors;
            cb.disabledColor = new Color(0.102f, 0.102f, 0.102f, 1f);
            b.colors = cb;
            var label = b.GetComponentInChildren<Text>();
            label.color = enabled ? UiKit.Gold : new Color(0.69f, 0.69f, 0.69f);
            if (price != null)
            {
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(10f, label.rectTransform.offsetMin.y);
                var cost = UiKit.Label(b.transform, price, 0f, 3f, w - 10f, 16f, 12, TextAnchor.UpperRight, enabled ? new Color(0.86f, 0.8f, 0.66f) : new Color(0.55f, 0.55f, 0.55f));
                cost.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            _cardBits.Add(b.gameObject);
        }

        void Rebuild()
        {
            foreach (var g in _cardBits) UnityEngine.Object.Destroy(g);
            _cardBits.Clear();
            var w = _world;
            var h = w.Hero;
            _purse.text = "잿 파편 <color=#ffcf5a>" + w.Bag.AshShards.ToString("N0") + "</color>   등불 기름 <color=#ffcf5a>" + w.Bag.LanternOil + "</color>";
            _quoteText.text = QuoteFor(_station);
            _note.text = _noteText;
            for (int i = 0; i < Stations.Length; i++) _stationName[i].color = i == _station ? UiKit.Gold : UiKit.Dim;

            bool spots = w.CampUnlocked && (_station == 0 || _station == 5);
            if (!spots) { _tip.Hide(); foreach (var sv in _spots) sv.ClearHover(); } // a hidden cell never gets its pointer exit
            for (int i = 0; i < 9; i++)
            {
                _spots[i].Root.gameObject.SetActive(spots);
                _spotMark[i].enabled = spots && i == _spot;
                if (!spots) continue;
                var was = _spots[i].Item;
                _spots[i].Show(h.Gear.Spots[i], null);
                if (_spots[i].Hovering && was != _spots[i].Item) // the piece under the pointer changed (auto-equip)
                {
                    if (_spots[i].Item != null) { _tip.Show(_spots[i].Item, ItemStyle.SpotName((EquipSpot)i)); _tip.PlaceBeside(_spots[i].Root); }
                    else _tip.Hide();
                }
            }
            var st = Stations[_station];
            CardText("<b>" + st.name + "</b>", 12f, 6f, 300f, 16f, UiKit.Gold);
            if (!w.CampUnlocked)
            {
                CardText("막 I의 석관 군주를 처치하면 야영지가 개방됩니다.", 12f, 30f, 600f, 14f, UiKit.Dim);
                return;
            }
            float cx = spots ? 384f : 12f; // the card's text column, right of the spot row when it shows
            float cw = W - 32f - cx - 12f;
            CardText(st.what, spots ? cx : 120f, 6f, spots ? cw : 600f, 16f, UiKit.Dim);
            var item = h.Gear.Spots[_spot];
            switch (_station)
            {
                case 0: Blacksmith(item, cx, cw); break;
                case 1:
                    CardText("Lv " + w.OilStoreLevel + "   오프라인 상한 " + (8 + 2 * w.OilStoreLevel) + "시간"
                        + (w.OilStoreLevel >= SimWorld.MaxOilStore ? "  <color=#8a8178>(최대)</color>" : "  →  " + (10 + 2 * w.OilStoreLevel) + "시간"), 12f, 34f, 600f, 14f);
                    if (w.OilStoreLevel < SimWorld.MaxOilStore)
                        CardButton("[4] 저장고 확장 · " + Cost(SimWorld.OilStoreCost(w.OilStoreLevel)), 12f, 60f, 300f, 3, true);
                    break;
                case 2:
                    CardText("Lv " + w.CartographerLevel + "   " + (w.CartographerLevel == 0 ? "다음: 원정 지령 [O]" : w.CartographerLevel == 1 ? "원정 지령 활성 · 다음: 층 고정 [L]"
                        : w.CartographerLevel == 2 ? "원정 지령 · 층 고정 활성 · 다음: 도감 우선 [L]" : "원정 지령 · 층 고정 · 도감 우선 활성"), 12f, 34f, 700f, 14f);
                    if (w.CartographerLevel < SimWorld.MaxCartographer)
                        CardButton("[5] 다음 기능 · " + Cost(SimWorld.CartographerCost(w.CartographerLevel)), 12f, 60f, 300f, 4, true);
                    break;
                case 3:
                    CardText("잔불 " + h.EmberLevel + " · 환생 " + h.Rebirths + "회", 12f, 34f, 600f, 14f);
                    if (!w.CanRebirth) CardText("Lv " + SimWorld.LevelCap + "에서 개방됩니다.", 12f, 52f, 600f, 14f, UiKit.Dim);
                    else
                    {
                        CardText("<color=#ffb060>잔불 +" + w.PendingEmber + "</color>  Lv 1로 · 장비 유지 · 돌파한 막은 고속 진행(방 절반, 처치 ×2~5)", 12f, 52f, 800f, 14f);
                        CardButton("[8] 잔불 점화", 12f, 76f, 200f, 7, true);
                    }
                    break;
                case 4:
                    var from = w.TransmuteFrom;
                    if (from == Rune.None) CardText("같은 룬 3개가 모이면 개방됩니다.", 12f, 34f, 600f, 14f, UiKit.Dim);
                    else
                    {
                        CardText("<color=#a8c4ff>" + Runes.Name(from) + " ×3  →  " + Runes.Name(from + 1) + "</color>", 12f, 34f, 600f, 14f);
                        CardButton("[6] 변환 · 잿 파편 " + SimWorld.TransmuteAsh, 12f, 58f, 220f, 5, true);
                    }
                    break;
                default:
                    CardText(ItemStyle.SpotName((EquipSpot)_spot) + " 부위 미확인 장비", cx, 30f, cw, 14f);
                    CardButton("[7] 구매 · 잿 파편 " + w.GambleCost, cx, 54f, 220f, 6, w.Bag.AshShards >= w.GambleCost);
                    break;
            }
        }

        void Blacksmith(Item item, float cx, float cw)
        {
            if (item == null) { CardText(ItemStyle.SpotName((EquipSpot)_spot) + " · 비어 있음", cx, 30f, cw, 14f, UiKit.Dim); return; }
            CardText("<color=" + ItemStyle.Hex(item.Rarity) + ">" + item.Name + "</color>  +" + item.Masterwork, cx, 30f, cw, 14f);
            bool mwMax = item.Masterwork >= SimWorld.MaxMasterwork;
            CardButton(mwMax ? "명품화 최대" : "[1] 명품화 +" + (item.Masterwork + 1) + " · 모든 옵션 +" + MasterworkStepPct + "% · " + Cost(SimWorld.MasterworkCost(item.Masterwork)), cx, 50f, cw, 0, !mwMax);
            // Affix lines: click one to pick it for the reroll.
            int budget = Item.StabilityBudget(item.Rarity);
            // 안정도 is the crafting budget of the piece (the costs are on the buttons); it never breaks the item.
            CardText("옵션  <color=#8a8178>안정도 " + item.StabilityLeft + " / " + budget + " (재굴림·재련에 쓰는 횟수)</color>",
                12f, 76f, 360f, 14f, UiKit.Dim);
            if (_line >= item.Affixes.Count) _line = 0;
            for (int a = 0; a < item.Affixes.Count; a++)
            {
                int line = a;
                bool sel = a == _line, locked = item.RerollLine >= 0 && item.RerollLine != a;
                var t = CardText((sel ? "▶ " : "   ") + ItemStyle.Line(item.Affixes[a], false)
                    + (item.Affixes[a].Tier > 0 ? "  <color=#8a8178>" + item.Affixes[a].Tier + "단계</color>" : ""), 12f, 94f + a * 16f, 360f, 14f,
                    locked ? new Color(0.4f, 0.38f, 0.35f) : sel ? UiKit.Gold : UiKit.Ink);
                t.raycastTarget = true;
                var p = t.gameObject.AddComponent<UiPointer>();
                p.LeftClick = () => { _line = line; _stamp = -1; };
            }
            // One bar per action, so two identical sentences no longer leave it unclear which one is 재굴림 and which 재련.
            string FixedWhy(string key, string action) => "[" + key + "] " + action + " — " + (item.Rarity == Rarity.Unique ? "고유 등급은 못 함" : "일반 등급은 못 함 (마법 이상부터)");
            string why = budget == 0 ? FixedWhy("2", "재굴림")
                : item.StabilityLeft < SimWorld.RerollStability ? "안정도가 " + SimWorld.RerollStability + " 미만이라 재굴림을 못 합니다"
                : "재굴림은 처음 고른 " + (item.RerollLine + 1) + "번 줄만 계속할 수 있습니다";
            bool canReroll = item.Affixes.Count > 0 && SimWorld.CanReroll(item, _line);
            CardButton(canReroll ? "[2] " + (_line + 1) + "번 줄 재굴림 · 잿 파편 " + SimWorld.RerollCost(item.Rerolls).ash + " · 안정도 " + SimWorld.RerollStability
                : item.Affixes.Count == 0 ? "재굴림할 옵션이 없습니다" : why, cx, 76f, cw, 1, canReroll);
            bool canTemper = SimWorld.CanTemper(item);
            CardButton(canTemper ? "[3] 재련 · 옵션 줄 추가 · 잿 파편 " + SimWorld.TemperCost(item).ash + " · 기름 1 · 안정도 " + SimWorld.TemperStability
                : SimWorld.MaxAffixes(item.Rarity) == 0 ? FixedWhy("3", "재련")
                : item.Affixes.Count >= SimWorld.MaxAffixes(item.Rarity) ? "옵션 줄이 가득 찼습니다 (최대 " + SimWorld.MaxAffixes(item.Rarity) + "줄)"
                : item.StabilityLeft >= SimWorld.TemperStability ? "더할 수 있는 옵션이 없습니다"
                : "안정도가 " + SimWorld.TemperStability + " 미만이라 재련을 못 합니다",
                cx, 100f, cw, 2, canTemper);
        }
    }
}
