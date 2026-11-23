using System;
using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 희승 11-24 "전설이나 고유 아이템들도, 디아블로에서 먹었을때는 되게 기분 좋았는데, 이건 그렇지 않아": the find of a
    /// legendary or unique gets its own moment (docs/research/1124_legendary_drop_feel.md): the shaft stands on the floor
    /// first (DropView), and as the piece flies into the bag a card rises at the top of the screen — the item at 2×
    /// in a pool of its rarity's light, "전설 발견" / "고유 발견" (and 처음 발견 for a unique never seen), the name in the
    /// rarity's colour, what it is, its power in one sentence and what it means for the hero (worn, ▲, ⇄ or kept).
    /// One at a time, the rest queue; a click opens the bag.
    /// </summary>
    public sealed class LootReveal
    {
        const float W = 440f, H = 156f, Hold = 4.2f, In = 0.18f, Out = 0.45f;
        /// <summary>When the card follows the drop: the shaft's arc and lie (DropView: 0.35 s + 2.6 s for a jackpot).</summary>
        const float Delay = 2.6f;
        readonly UiKit _ui;
        readonly RectTransform _card;
        readonly CanvasGroup _group;
        readonly Image _glow, _icon, _edgeTop, _edgeBottom, _frame;
        readonly Text _kind, _name, _base, _power, _verdict;
        readonly Queue<(Item item, float at)> _queue = new Queue<(Item, float)>();
        Item _shown;
        float _t = -1f;
        public Action OnOpenBag;

        public LootReveal(UiKit ui)
        {
            _ui = ui;
            _card = UiKit.Rect(ui.Root, "Loot Reveal", 0f, 0f, W, H);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 1f);
            _card.pivot = new Vector2(0.5f, 1f);
            _card.anchoredPosition = new Vector2(0f, -96f); // under the top-centre HUD lines
            _group = _card.gameObject.AddComponent<CanvasGroup>();
            var back = _card.gameObject.AddComponent<Image>();
            back.color = new Color(0.03f, 0.025f, 0.035f, 0.94f);
            back.raycastTarget = true;
            _frame = ui.Sprite(_card, ui.Catalog.Has("ui/ui_tooltip_frame") ? "ui/ui_tooltip_frame" : UiFrame.Window, -3f, -3f, W + 6f, H + 6f, sliced: true);
            _frame.preserveAspect = false;
            _edgeTop = UiKit.Fill(_card, "edge", 0f, 0f, W, 2f, Color.white);
            _edgeBottom = UiKit.Fill(_card, "edge", 0f, H - 2f, W, 2f, Color.white);
            _glow = UiKit.Fill(_card, "glow", -14f, -10f, 140f, 150f, Color.white);
            _glow.sprite = UiKit.Glow(ui, 64);
            var well = UiKit.Fill(_card, "well", 18f, 26f, 80f, 80f, new Color(0f, 0f, 0f, 0.55f));
            _icon = ui.Sprite(_card, "ui/dot", 26f, 34f, 64f, 64f); // 32 px art at ×2
            _kind = UiKit.Label(_card, "", 112f, 12f, W - 124f, 14f, 12, TextAnchor.UpperLeft, UiKit.Gold);
            _name = UiKit.Label(_card, "", 112f, 30f, W - 124f, 24f, 20, TextAnchor.UpperLeft, Color.white, title: true);
            _base = UiKit.Label(_card, "", 112f, 58f, W - 124f, 30f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _power = UiKit.Label(_card, "", 112f, 76f, W - 124f, 32f, 12, TextAnchor.UpperLeft, new Color(0.85f, 0.66f, 0.36f));
            _verdict = UiKit.Label(_card, "", 112f, H - 48f, W - 124f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            _verdict.horizontalOverflow = HorizontalWrapMode.Overflow;
            // Critique 6: "정보는 맞지만 알림창처럼 보여 흥분이 없다" — a light sweeps across the card as it lands, the name
            // and the item pop, and the card offers the two things a player wants next: wear it, or look at it.
            var clip = UiKit.Rect(_card, "shine clip", 0f, 0f, W, H);
            clip.gameObject.AddComponent<RectMask2D>();
            _shine = UiKit.Fill(clip, "shine", -60f, 0f, 46f, H, new Color(1f, 0.95f, 0.8f, 0f));
            _shine.sprite = UiKit.Glow(ui, 64);
            _equip = ui.Button(_card, "장착", W - 176f, H - 28f, 76f, 22f, Equip);
            _look = ui.Button(_card, "가방에서 보기", W - 96f, H - 28f, 84f, 22f, () => { _t = Mathf.Max(_t, Hold + In); OnOpenBag?.Invoke(); });
            _look.GetComponentInChildren<Text>().fontSize = 11;
            var p = _card.gameObject.AddComponent<UiPointer>();
            p.LeftClick = () => { _t = Mathf.Max(_t, Hold + In); OnOpenBag?.Invoke(); };
            p.Enter = () => _hover = true; // the card stays while the pointer reads it
            p.Exit = () => _hover = false;
            _card.gameObject.SetActive(false);
        }

        readonly Image _shine;
        readonly Button _equip, _look;
        SimWorld _world;
        bool _hover;

        void Equip()
        {
            if (_world == null || _shown == null || !_world.EquipFromBag(_shown.Uid)) return;
            _verdict.text = "<color=#9fd28a>착용했습니다</color>";
            _equip.gameObject.SetActive(false);
            OnEquipped?.Invoke();
        }

        /// <summary>Raised after the card's 장착 so the game saves soon.</summary>
        public Action OnEquipped;

        /// <summary>Takes this tick's drops; a legendary or unique gets a card once its shaft has stood a moment.</summary>
        public void OnTick(List<SimEvent> events)
        {
            foreach (var ev in events)
                if (ev.Type == SimEventType.Drop && ev.Item != null && ev.Item.Rarity >= Rarity.Legendary)
                    _queue.Enqueue((ev.Item, Time.unscaledTime + Delay));
        }

        public void Render(SimWorld world, bool hidden)
        {
            float dt = Time.unscaledDeltaTime;
            if (_shown == null)
            {
                // A card waits while a window is open (it would cover the window's head) and shows when the view is clear.
                if (hidden || _queue.Count == 0 || _queue.Peek().at > Time.unscaledTime) { _card.gameObject.SetActive(false); return; }
                Show(world, _queue.Dequeue().item);
            }
            if (!(_hover && _t > In && _t < In + Hold)) _t += dt;
            float a = _t < In ? _t / In : _t < In + Hold ? 1f : 1f - (_t - In - Hold) / Out;
            // The sweep: once across, 0.15–0.75 s; the name and the item pop with it.
            float sweep = Mathf.Clamp01((_t - 0.15f) / 0.6f);
            _shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-60f, W + 20f, sweep), 0f);
            _shine.color = new Color(1f, 0.95f, 0.8f, sweep > 0f && sweep < 1f ? 0.35f : 0f);
            float pop = _t < 0.12f ? Mathf.Lerp(0.6f, 1.15f, _t / 0.12f) : _t < 0.3f ? Mathf.Lerp(1.15f, 1f, (_t - 0.12f) / 0.18f) : 1f;
            _icon.rectTransform.localScale = new Vector3(pop, pop, 1f);
            float namePop = _t < 0.25f ? Mathf.Lerp(1.2f, 1f, _t / 0.25f) : 1f;
            _name.rectTransform.localScale = new Vector3(namePop, namePop, 1f);
            if (a <= 0f && _t > In) { _shown = null; _card.gameObject.SetActive(false); return; }
            _group.alpha = hidden ? 0f : Mathf.Clamp01(a);
            _group.blocksRaycasts = !hidden;
            // It rises a few pixels as it comes in, and the light behind the item breathes.
            float rise = _t < In ? (1f - _t / In) * 10f : 0f;
            _card.anchoredPosition = new Vector2(0f, -96f - rise);
            var c = ItemStyle.Color(_shown.Rarity);
            _glow.color = new Color(c.r, c.g, c.b, 0.35f + 0.12f * Mathf.Sin(_t * 3f));
        }

        void Show(SimWorld world, Item it)
        {
            _shown = it;
            _world = world;
            _t = 0f;
            _hover = false;
            _card.gameObject.SetActive(true);
            _card.SetAsLastSibling(); // over the HUD; a window opened later rises over it
            var c = ItemStyle.Color(it.Rarity);
            _edgeTop.color = _edgeBottom.color = new Color(c.r, c.g, c.b, 0.9f);
            _icon.sprite = _ui.Catalog.Get(ItemStyle.Icon(it));
            bool unique = it.Rarity == Rarity.Unique;
            bool first = unique && FirstFind(it.Name);
            _kind.text = "<color=" + ItemStyle.Hex(it.Rarity) + ">" + (unique ? "고유 발견" : "전설 발견") + "</color>"
                + (first ? "  <color=#ffe7a0>· 처음 발견!</color>" : "") + (it.GreaterCount > 0 ? "  <color=#ffd24d>" + new string('★', it.GreaterCount) + " 대옵션</color>" : "");
            _name.text = it.Name;
            _name.color = c;
            var b = ItemData.Base(it.BaseId);
            _base.text = (b != null ? b.Name + " · " : "") + "아이템 레벨 " + it.Ilvl;
            var ess = string.IsNullOrEmpty(it.EssenceId) ? null : ItemData.Essence(it.EssenceId);
            _power.text = ess != null ? (unique ? "고유 효과: " : "전설 효과: ") + ess.Text : "";
            // A unique's lore line under its name's facts, as D2 told it (11-24 UniqueDef.Flavor).
            var u = unique && !string.IsNullOrEmpty(it.UniqueId) ? ItemData.Unique(it.UniqueId) : null;
            bool lore = u != null && !string.IsNullOrEmpty(u.Flavor);
            if (lore) _base.text += "\n<color=#9a8a6a>“" + u.Flavor + "”</color>";
            float extra = lore ? 17f : 0f; // the lore line pushes the power and the verdict down, the card grows
            _power.rectTransform.anchoredPosition = new Vector2(112f, -(76f + extra));
            _verdict.rectTransform.anchoredPosition = new Vector2(112f, -(H - 48f + extra));
            _card.sizeDelta = new Vector2(W, H + extra);
            _frame.rectTransform.sizeDelta = new Vector2(W + 6f, H + 6f + extra);
            _edgeBottom.rectTransform.anchoredPosition = new Vector2(0f, -(H - 2f + extra));
            _verdict.text = Verdict(world, it);
            // 장착 only where it can be worn now and is not worn yet; the bag button always.
            bool inBag = world.Bag.Items.Contains(it);
            _equip.gameObject.SetActive(inBag && HeroStats.Compare(world.Hero, it).Arrow != GearArrow.Locked);
            var lift = _card.sizeDelta.y - H; // a lore line pushed the foot down
            ((RectTransform)_equip.transform).anchoredPosition = new Vector2(W - 176f, -(H - 28f + lift));
            ((RectTransform)_look.transform).anchoredPosition = new Vector2(W - 96f, -(H - 28f + lift));
        }

        /// <summary>What the find means now: worn already (auto-equip took it), better, a trade, or kept for later.</summary>
        static string Verdict(SimWorld world, Item it)
        {
            var h = world.Hero;
            foreach (var worn in h.Gear.Spots) if (worn == it) return "<color=#9fd28a>바로 착용했습니다</color>";
            if (!world.Bag.Items.Contains(it)) return "<color=#8a8178>가방이 가득 차 잿 파편이 되었습니다</color>";
            var cmp = HeroStats.Compare(h, it);
            float pct = (cmp.Power - 1f) * 100f;
            switch (cmp.Arrow)
            {
                case GearArrow.BigUp: case GearArrow.Up: return "<color=#5BD66B>▲ 전투력 +" + pct.ToString("0") + "%</color>  <color=#8a8178>공격 " + Signed(cmp.Offence) + " · 생존 " + Signed(cmp.Survival) + "</color>";
                case GearArrow.Trade: return "<color=#FFD24D>⇄ 교환</color>  <color=#8a8178>공격 " + Signed(cmp.Offence) + " · 생존 " + Signed(cmp.Survival) + "</color>";
                case GearArrow.Locked: return "<color=#8a8178>" + (HeroStats.FitsClass(it, h.ClassId) ? "요구 레벨 " + it.ReqLevel : "다른 직업 전용") + " · 가방에 보관했습니다</color>";
                default: return "<color=#8a8178>지금 장비가 더 낫습니다 · 가방에 보관했습니다</color>";
            }
        }

        static string Signed(float ratio) { float v = (ratio - 1f) * 100f; return (v >= 0f ? "+" : "−") + Mathf.Abs(v).ToString("0") + "%"; }

        /// <summary>True the first time this unique is ever found on this machine (kept per player, not per character).</summary>
        static bool FirstFind(string name)
        {
            try
            {
                string seen = PlayerPrefs.GetString("uniques_seen", "");
                string key = "|" + name + "|";
                if (seen.Contains(key)) return false;
                PlayerPrefs.SetString("uniques_seen", (seen.Length == 0 ? "|" : seen) + name + "|");
                return true;
            }
            catch { return false; }
        }
    }
}
