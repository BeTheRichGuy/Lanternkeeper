using System;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 캐릭터 선택 (GDD §7.1: 4 classes, each its own character and save): at first launch, and any time from 설정 →
    /// 캐릭터 선택, like a Diablo character screen. Four cards — the bust at 2× in a pool of lantern light, name, role,
    /// the character's level and place (or 새 캐릭터); hover lifts a card, a click or 1–4 chooses.
    /// </summary>
    public sealed class ClassSelectPanel
    {
        const float CardW = 196f, CardH = 344f, Gap = 16f;
        readonly RectTransform _root;
        readonly Image[] _glow = new Image[4];
        readonly Text[] _status = new Text[4];
        readonly GameObject _back;
        public bool Visible { get; private set; }
        /// <summary>Opened from the game (not the first launch): Esc or 돌아가기 closes it.</summary>
        public bool Closable { get; private set; }
        public Action<string> OnPick;

        static readonly (string id, string portrait, string role, string axis)[] Classes =
        {
            ("ashknight",  "portrait/ember_knight_B_lantern",      "전사 — 대검을 들고 적진 한가운데로",         "휠윈드 · 잿불 베기 · 잿더미 도약"),
            ("oathknight", "portrait/oath_knight_A_mourning_paladin", "성기사 — 방패와 신성한 번개로 버틴다",             "축복의 망치 · 열성 · 천벌"),
            ("windhunter", "portrait/wind_hunter_A_hooded_ranger",  "궁수 — 거리를 두고 화살을 퍼붓는다",         "속사 · 갈래 화살 · 화살비"),
            ("starseer",   "portrait/star_seer_A_astrologer",       "마법사 — 화염·냉기·번개를 다룬다",          "화염구 · 얼음 화살 · 연쇄 번개"),
        };

        public ClassSelectPanel(UiKit ui)
        {
            // Full-screen: a dark veil over the waiting world, the cards in the middle.
            _root = UiKit.Rect(ui.Root, "Class Select", 0f, 0f, 0f, 0f);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            var veil = _root.gameObject.AddComponent<Image>();
            veil.color = new Color(0.01f, 0.008f, 0.012f, 0.92f);
            var box = UiKit.Centered(_root, "cards", 4 * CardW + 3 * Gap, CardH + 110f);
            UiKit.Label(box, "캐릭터 선택", 0f, 0f, box.sizeDelta.x, 22f, 16, TextAnchor.UpperCenter, UiKit.Gold, title: true);
            UiKit.Label(box, "클릭하거나 1~4로 선택하세요. 직업마다 캐릭터가 개별 저장되며, 설정(Esc)에서 언제든 변경할 수 있습니다.",
                0f, 28f, box.sizeDelta.x, 14f, 12, TextAnchor.UpperCenter, UiKit.Dim);
            for (int i = 0; i < Classes.Length; i++)
            {
                int index = i;
                var (id, portrait, role, axis) = Classes[i];
                float x = i * (CardW + Gap), y = 60f;
                var card = UiKit.Fill(box, "card", x, y, CardW, CardH, new Color(0.075f, 0.068f, 0.062f, 0.96f));
                card.raycastTarget = true;
                var frame = ui.Sprite(card.transform, "ui/ui2_window_iron", -4f, -4f, CardW + 8f, CardH + 8f, sliced: true);
                frame.preserveAspect = false;
                _glow[i] = UiKit.Fill(card.transform, "light", CardW / 2f - 100f, 10f, 200f, 170f, new Color(1f, 0.72f, 0.4f, 0.2f));
                _glow[i].sprite = UiKit.Glow(ui, 64);
                if (ui.Catalog.Has(portrait)) ui.Sprite(card.transform, portrait, CardW / 2f - 64f, 30f, 128f, 128f); // 64 px bust at 2×
                UiKit.Label(card.transform, "<color=#8a8178>" + (i + 1) + "</color>  " + SkillBook.ClassName(id), 0f, 172f, CardW, 18f, 16, TextAnchor.UpperCenter, new Color(0.95f, 0.88f, 0.75f), title: true);
                UiKit.Fill(card.transform, "rule", 24f, 198f, CardW - 48f, 1f, UiKit.Line);
                UiKit.Label(card.transform, role.Replace(" — ", "\n"), 12f, 206f, CardW - 24f, 40f, 12, TextAnchor.UpperCenter, new Color(0.82f, 0.76f, 0.68f));
                UiKit.Label(card.transform, axis.Replace(" · ", "\n"), 12f, 252f, CardW - 24f, 56f, 12, TextAnchor.UpperCenter, new Color(0.6f, 0.75f, 0.9f));
                UiKit.Fill(card.transform, "rule", 24f, 314f, CardW - 48f, 1f, UiKit.Line);
                _status[i] = UiKit.Label(card.transform, "새 캐릭터", 0f, 321f, CardW, 14f, 12, TextAnchor.UpperCenter, UiKit.Gold);
                var p = card.gameObject.AddComponent<UiPointer>();
                p.Enter = () => _glow[index].color = new Color(1f, 0.78f, 0.45f, 0.45f);
                p.Exit = () => _glow[index].color = new Color(1f, 0.72f, 0.4f, 0.2f);
                p.LeftClick = () => OnPick?.Invoke(id);
            }
            _back = ui.Button(box, "돌아가기 (Esc)", box.sizeDelta.x / 2f - 70f, 60f + CardH + 16f, 140f, 22f, () => Show(false)).gameObject;
            Show(false);
        }

        /// <summary>Opens the screen with each class's character (from <paramref name="status"/>: "Lv 23 · 3막 5층", or null
        /// for none yet); the one in play is marked.</summary>
        public void Open(string current, Func<string, string> status, bool closable)
        {
            Closable = closable;
            _back.SetActive(closable);
            for (int i = 0; i < Classes.Length; i++)
            {
                string s = status?.Invoke(Classes[i].id);
                bool here = Classes[i].id == current && closable;
                _status[i].text = here ? "<color=#9fd28a>플레이 중</color>  " + (s ?? "") : s ?? "<color=#8a8178>새 캐릭터</color>";
            }
            Show(true);
        }

        public void Show(bool v)
        {
            Visible = v;
            _root.gameObject.SetActive(v);
            if (v) _root.SetAsLastSibling(); // over the HUD, the notices and every window
        }

        public static string PortraitOf(string classId)
        {
            foreach (var c in Classes) if (c.id == classId) return c.portrait;
            return null;
        }

        public static string ClassAt(int index) => Classes[Mathf.Clamp(index, 0, Classes.Length - 1)].id;
    }
}
