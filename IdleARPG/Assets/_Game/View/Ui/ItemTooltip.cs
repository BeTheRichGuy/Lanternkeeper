using System.Text;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Item tooltip in Diablo II's manner (희승 11-22), centred, three blocks split by thin rules: head (name in rarity
    /// colour, the base it is made on, rarity · slot · item level), body (the base line in white, then each stat in its
    /// colour — magic blue, element colours, violet build stats, green finds, ★ lines gold —, the legendary power in
    /// orange, sockets and runeword), foot (requirements and the compare line). Placed beside the slot it explains,
    /// flipped to the other side and clamped so it never leaves the screen.
    /// </summary>
    public sealed class ItemTooltip
    {
        public const float Width = 244f;
        const float Pad = 8f, Gap = 5f;
        readonly UiKit _ui;
        readonly RectTransform _box;
        readonly Image _frame;
        readonly Text _head, _body, _foot;
        readonly Image _rule1, _rule2;
        public bool Visible => _box.gameObject.activeSelf;
        public float Height => _box.sizeDelta.y;

        public ItemTooltip(UiKit ui, string name)
        {
            _ui = ui;
            _box = UiKit.Rect(ui.Root, name, 0f, 0f, Width, 40f);
            _box.anchorMin = _box.anchorMax = new Vector2(0.5f, 0.5f);
            var back = _box.gameObject.AddComponent<Image>();
            back.color = new Color(0.02f, 0.02f, 0.03f, 0.92f);
            back.raycastTarget = false;
            _frame = ui.Sprite(_box, UiFrame.Tooltip, -3f, -3f, Width + 6f, 46f, sliced: true);
            _frame.preserveAspect = false;
            _head = UiKit.Label(_box, "", Pad, Pad, Width - Pad * 2f, 20f, 12, TextAnchor.UpperCenter);
            _rule1 = UiKit.Fill(_box, "rule", Pad, 0f, Width - Pad * 2f, 1f, UiKit.Line);
            _body = UiKit.Label(_box, "", Pad, 0f, Width - Pad * 2f, 20f, 12, TextAnchor.UpperCenter);
            _body.lineSpacing = 1.1f;
            _rule2 = UiKit.Fill(_box, "rule", Pad, 0f, Width - Pad * 2f, 1f, UiKit.Line);
            _foot = UiKit.Label(_box, "", Pad, 0f, Width - Pad * 2f, 20f, 12, TextAnchor.UpperCenter, UiKit.Dim);
            _box.gameObject.SetActive(false);
        }

        public void Hide() => _box.gameObject.SetActive(false);

        /// <summary>Fills the blocks. <paramref name="foot"/> may be empty (no rule, no block).</summary>
        public void Show(Item item, string foot, string corner = null)
        {
            var sb = new StringBuilder();
            sb.Append("<color=").Append(ItemStyle.Hex(item.Rarity)).Append(">").Append(item.Name).Append("</color>"); // one Galmuri size: 14 would sit between pixels
            if (!string.IsNullOrEmpty(corner)) sb.Append("  <color=#8a8178>").Append(corner).Append("</color>");
            // The base it is made on, as Diablo II prints under a magic or better item's name.
            var baseDef = ItemData.Base(item.BaseId);
            if (item.Rarity > Rarity.Common && baseDef != null && baseDef.Name != item.Name)
                sb.Append("\n<color=#d8d0c4>").Append(baseDef.Name).Append("</color>");
            sb.Append("\n<color=#8a8178>").Append(ItemStyle.RarityName(item.Rarity)).Append(" ").Append(SlotName(item.Slot))
              .Append(" · 아이템 레벨 ").Append(item.Ilvl);
            if (item.Masterwork > 0) sb.Append(" · 명품화 +").Append(item.Masterwork);
            sb.Append("</color>");
            _head.text = sb.ToString();

            sb.Clear();
            if (item.Implicit.Value != 0f) sb.Append("<color=#f2e6cf>").Append(ItemStyle.LineText(item.Implicit)).Append("</color>\n");
            foreach (var a in item.Affixes) sb.Append(ItemStyle.Line(a, tier: false)).Append('\n');
            if (!string.IsNullOrEmpty(item.EssenceId))
            {
                var ess = ItemData.Essence(item.EssenceId);
                if (ess != null) sb.Append("\n<color=#ff9a40>◆ 고유 능력</color>\n<color=#ffb870>").Append(ess.Text).Append("</color>\n");
            }
            if (item.Sockets > 0)
            {
                var rw = item.Runeword;
                sb.Append('\n');
                if (rw != null) sb.Append("<color=#d9c78a>룬어 『").Append(rw.Name).Append("』</color>\n<color=#bfb59a>").Append(rw.Text).Append("</color>\n");
                sb.Append("<color=#9a9aa8>소켓 (").Append(item.Sockets).Append(")  ");
                for (int k = 0; k < item.Sockets; k++)
                    sb.Append(k < item.RuneSlots.Count ? Runes.Name((Rune)item.RuneSlots[k]) + " " : "◇ ");
                sb.Append("</color>\n");
            }
            _body.text = sb.ToString().TrimEnd('\n');
            _foot.text = foot ?? "";
            Layout();
            _box.gameObject.SetActive(true);
            _box.SetAsLastSibling();
        }

        void Layout()
        {
            float y = Pad;
            float hh = Mathf.Ceil(_head.preferredHeight);
            Place(_head.rectTransform, y, hh);
            y += hh + Gap;
            Place(_rule1.rectTransform, y, 1f);
            y += 1f + Gap;
            float bh = Mathf.Ceil(_body.preferredHeight);
            Place(_body.rectTransform, y, bh);
            y += bh;
            bool foot = _foot.text.Length > 0;
            _rule2.enabled = foot;
            _foot.enabled = foot;
            if (foot)
            {
                y += Gap;
                Place(_rule2.rectTransform, y, 1f);
                y += 1f + Gap;
                float fh = Mathf.Ceil(_foot.preferredHeight);
                Place(_foot.rectTransform, y, fh);
                y += fh;
            }
            y += Pad;
            _box.sizeDelta = new Vector2(Width, y);
            _frame.rectTransform.sizeDelta = new Vector2(Width + 6f, y + 6f);
        }

        static void Place(RectTransform rt, float y, float h)
        {
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
        }

        /// <summary>Beside <paramref name="target"/>: to its right, or its left when the right side has no room.
        /// <paramref name="nudge"/> moves it further out (a compare tooltip stands next to the first one).</summary>
        public void PlaceBeside(RectTransform target, bool preferLeft = false, float nudge = 0f)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners); // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
            var root = _ui.Root;
            Vector2 tl = root.InverseTransformPoint(corners[1]);
            Vector2 tr = root.InverseTransformPoint(corners[2]);
            var half = root.rect.size * 0.5f;
            float w = Width, h = _box.sizeDelta.y;
            float right = tr.x + 6f + nudge, left = tl.x - 6f - w - nudge;
            bool useLeft = preferLeft ? left >= -half.x : right + w > half.x;
            float x = useLeft ? left : right;
            x = Mathf.Clamp(x, -half.x + 2f, half.x - w - 2f);
            float y = Mathf.Clamp(tl.y, -half.y + h + 2f, half.y - 2f);
            _box.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
        }

        /// <summary>Right next to another tooltip (the compare one sits on the far side of the first).</summary>
        public void PlaceNextTo(ItemTooltip other)
        {
            var half = _ui.Root.rect.size * 0.5f;
            var p = other._box.anchoredPosition;
            float x = p.x - Width - 4f;
            if (x < -half.x + 2f) x = p.x + Width + 4f;
            x = Mathf.Clamp(x, -half.x + 2f, half.x - Width - 2f); // small windows: stay on screen (review 11-21)
            float y = Mathf.Clamp(p.y, -half.y + _box.sizeDelta.y + 2f, half.y - 2f);
            _box.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
        }

        public static string SlotName(Slot s)
        {
            switch (s)
            {
                case Slot.Weapon: return "무기";
                case Slot.Helm: return "투구";
                case Slot.Armor: return "갑옷";
                case Slot.Gloves: return "장갑";
                case Slot.Boots: return "장화";
                case Slot.Belt: return "허리띠";
                case Slot.Ring: return "반지";
                default: return "목걸이";
            }
        }
    }
}
