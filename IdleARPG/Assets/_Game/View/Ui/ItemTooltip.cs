using System.Collections.Generic;
using System.Text;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Item tooltip (UX plan A1–A5, "아이템 툴팁 최종 틀"), read top to bottom as the questions come: what is it (name in
    /// its rarity colour, "◆◆◆ 희귀" at the right, base · item level · class), is it better (the arrow line and what it
    /// is compared with — only when the caller passes a compare), what does it have (one stat a row in the one body
    /// colour, grouped 공격 → 방어 → 스킬 → 유틸 with a hairline between groups, each row's change against the worn piece
    /// at the right, the stats the swap would lose in red), then 전설 효과, runeword and the small print (sockets,
    /// required level, salvage value). Rarity colour appears on the name row only. Placed beside the slot it explains,
    /// flipped to the other side and clamped so it never leaves the screen.
    /// </summary>
    public sealed class ItemTooltip
    {
        public const float Width = 276f; // the arrow line ("▲ 전투력 +12%   공격 +15% · 생존 −2%") fits on one row
        const float Pad = 8f, Gap = 5f, Inner = Width - Pad * 2f;
        const string Dim = ItemStyle.SameHex, Body = ItemStyle.BodyHex;

        sealed class Row { public Text Left, Right; }

        readonly UiKit _ui;
        readonly RectTransform _box;
        readonly Image _frame;
        readonly Text _name, _tag, _sub, _verdict, _lost, _extra, _foot, _band;
        readonly Image _rule1, _rule2, _bandBack;
        readonly List<Stat> _rowStats = new List<Stat>(); // the stat of each row, for the group rules (a total row has no AffixRoll of its own)
        const float BandH = 14f;
        readonly List<Row> _rows = new List<Row>();
        readonly List<Image> _groupRules = new List<Image>();
        readonly List<AffixRoll> _lines = new List<AffixRoll>();
        int _rowCount, _ruleCount;
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
            _bandBack = UiKit.Fill(_box, "band", 0f, 0f, Width, BandH, new Color(0.34f, 0.32f, 0.3f, 0.85f)); // "착용 중" / "가방": which side of the compare this is
            _band = UiKit.Label(_box, "", 0f, 0f, Width, BandH, 12, TextAnchor.MiddleCenter, new Color(0.9f, 0.87f, 0.8f));
            _name = UiKit.Label(_box, "", Pad, Pad, Inner, 14f, 12); // one Galmuri size: 14 would sit between pixels
            _tag = UiKit.Label(_box, "", Pad, Pad, Inner, 14f, 12, TextAnchor.UpperRight);
            _sub = UiKit.Label(_box, "", Pad, 0f, Inner, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _verdict = UiKit.Label(_box, "", Pad, 0f, Inner, 14f, 12);
            _rule1 = UiKit.Fill(_box, "rule", Pad, 0f, Inner, 1f, UiKit.Line);
            _lost = UiKit.Label(_box, "", Pad, 0f, Inner, 14f, 12);
            _rule2 = UiKit.Fill(_box, "rule", Pad, 0f, Inner, 1f, UiKit.Line);
            _extra = UiKit.Label(_box, "", Pad, 0f, Inner, 14f, 12);
            _foot = UiKit.Label(_box, "", Pad, 0f, Inner, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _box.gameObject.SetActive(false);
        }

        public void Hide() => _box.gameObject.SetActive(false);

        /// <summary>
        /// Fills the tooltip. <paramref name="foot"/> is the caller's own last line (may be empty).
        /// <paramref name="compare"/> adds the answer line under the head; <paramref name="worn"/> (the piece in the
        /// compared spot, or null for an empty one) adds each row's change and the lost stats.
        /// </summary>
        public void Show(Item item, string foot, string corner = null, GearCompare? compare = null, Item worn = null, string band = null)
        {
            var sb = new StringBuilder();
            _band.text = band ?? "";
            _name.text = "<color=" + ItemStyle.Hex(item.Rarity) + ">" + item.Name + "</color>"
                + (string.IsNullOrEmpty(corner) ? "" : "  <color=" + Dim + ">" + corner + "</color>");
            _tag.text = ItemStyle.Tag(item.Rarity);

            // What it is made on (or its slot, for a common piece that is its own base) · item level · whose it is.
            var baseDef = ItemData.Base(item.BaseId);
            sb.Append(baseDef != null && baseDef.Name != item.Name ? baseDef.Name : SlotName(item.Slot)).Append(" · 아이템 레벨 ").Append(item.Ilvl);
            if (baseDef?.ClassId != null) sb.Append(" · ").Append(SkillBook.ClassName(baseDef.ClassId));
            if (item.Masterwork > 0) sb.Append(" · 명품화 +").Append(item.Masterwork);
            _sub.text = sb.ToString();

            // The answer first (UX plan A4): the old compare sat dim at the very bottom and was missed (audit #5).
            sb.Clear();
            if (compare is GearCompare c)
            {
                if (c.Arrow == GearArrow.Locked) sb.Append("<color=").Append(ItemStyle.LossHex).Append(">Lv ").Append(item.ReqLevel).Append(" 필요</color>");
                else sb.Append(ItemStyle.Verdict(c, EffectName(item.Rarity)));
                sb.Append("\n<color=").Append(Dim).Append(">↔ ");
                string spot = item.Slot == Slot.Ring ? (c.Spot == EquipSpot.Ring2 ? "반지 2" : "반지 1") : null;
                if (worn == null) sb.Append(spot != null ? spot + " 빈 칸에 장착" : "빈 칸에 장착");
                else sb.Append(spot != null ? spot + "와 비교: " : "착용 중: ").Append("</color><color=").Append(Body).Append(">").Append(worn.Name);
                sb.Append("</color>");
            }
            _verdict.text = sb.ToString();

            // The base's own line and the affixes, by group; the base's line leads its group.
            _lines.Clear();
            _rowStats.Clear();
            _rowCount = _ruleCount = 0;
            for (int g = 0; g <= 3; g++)
            {
                if (item.Implicit.Value != 0f && ItemStyle.Group(item.Implicit.Stat) == g) _lines.Add(item.Implicit);
                foreach (var a in item.Affixes) if (ItemStyle.Group(a.Stat) == g) _lines.Add(a);
            }
            for (int li = 0; li < _lines.Count; li++)
            {
                var a = _lines[li];
                var row = NextRow();
                _rowStats.Add(a.Stat);
                // A stat on two lines (base and affix) is tagged 기본 / 추가, and its total and change get a line of their own
                // under them (loop 2: "합 +49.9%" at the right of the second line read as that line's own change).
                bool first = _lines.FindIndex(x => x.Stat == a.Stat) == li;
                bool last = _lines.FindIndex(li + 1, x => x.Stat == a.Stat) < 0;
                bool twice = !first || !last;
                bool hasBase = item.Implicit.Value != 0f && item.Implicit.Stat == a.Stat; // two plain affixes of one stat get no tags
                string tag = twice && hasBase ? "<color=" + Dim + ">" + (first ? "기본" : "추가") + "</color> " : "";
                row.Right.text = !twice && worn != null ? Change(a.Stat, item.Sum(a.Stat), worn.Sum(a.Stat)) : "";
                string line = tag + ItemStyle.Line(a, tier: false);
                float room = Inner - (row.Right.text.Length > 0 ? row.Right.preferredWidth + 6f : 0f);
                row.Left.rectTransform.sizeDelta = new Vector2(room, 14f);
                row.Left.text = a.Greater ? line + "  <color=" + Dim + ">대옵션 ×1.5</color>" : line;
                if (a.Greater && row.Left.preferredWidth > room) row.Left.text = line + "  <color=" + Dim + ">×1.5</color>"; // a long stat: the words give way
                if (twice && last)
                {
                    var sum = NextRow();
                    _rowStats.Add(a.Stat);
                    sum.Right.text = "";
                    sum.Left.rectTransform.sizeDelta = new Vector2(Inner, 14f);
                    sum.Left.text = "<color=" + Dim + ">합 " + Amount(a.Stat, item.Sum(a.Stat)) + "</color>"
                        + (worn != null ? "  " + Change(a.Stat, item.Sum(a.Stat), worn.Sum(a.Stat)) : "");
                }
            }

            // What the swap takes away (UX plan A5): stats the worn piece has and this one lacks.
            sb.Clear();
            if (worn != null)
            {
                foreach (var def in ItemData.Affixes)
                {
                    float had = worn.Sum(def.Stat);
                    if (had <= 0f || item.Sum(def.Stat) > 0f) continue;
                    sb.Append(sb.Length == 0 ? "잃는 옵션: " : ", ").Append(ItemStyle.StatText(def.Stat, had));
                }
                var gone = !string.IsNullOrEmpty(worn.EssenceId) && worn.EssenceId != item.EssenceId ? ItemData.Essence(worn.EssenceId) : null;
                if (gone != null) sb.Append(sb.Length == 0 ? "잃는 옵션: " : ", ").Append(EffectName(worn.Rarity)).Append(" 「").Append(gone.Text).Append("」");
            }
            _lost.text = sb.Length > 0 ? "<color=" + ItemStyle.LossHex + ">" + sb + "</color>" : "";

            sb.Clear();
            if (!string.IsNullOrEmpty(item.EssenceId))
            {
                var ess = ItemData.Essence(item.EssenceId);
                if (ess != null) sb.Append("<color=").Append(ItemStyle.Hex(item.Rarity)).Append(">").Append(EffectName(item.Rarity)).Append("</color><color=").Append(Body).Append(">: ").Append(ess.Text).Append("</color>\n");
            }
            // A unique's lore line under its power, in the muted gold of old ink (11-24, D2's flavour text).
            var u = item.Rarity == Rarity.Unique && !string.IsNullOrEmpty(item.UniqueId) ? ItemData.Unique(item.UniqueId) : null;
            if (u != null && !string.IsNullOrEmpty(u.Flavor)) sb.Append("<color=#9a8a6a>“").Append(u.Flavor).Append("”</color>\n");
            var rw = item.Runeword;
            if (rw != null) sb.Append("<color=#FFFFFF>룬어 『").Append(rw.Name).Append("』</color><color=").Append(Body).Append(">: ").Append(rw.Text).Append("</color>\n");
            _extra.text = sb.ToString().TrimEnd('\n');

            // The small print on one line: sockets (● filled, ○ empty) · required level · what salvaging gives.
            sb.Clear();
            if (item.Sockets > 0)
            {
                sb.Append("소켓 ");
                for (int k = 0; k < item.Sockets; k++) sb.Append(k < item.RuneSlots.Count ? '●' : '○');
                if (item.RuneSlots.Count > 0)
                {
                    sb.Append(" (");
                    for (int k = 0; k < item.RuneSlots.Count; k++) sb.Append(k > 0 ? "·" : "").Append(Runes.Name((Rune)item.RuneSlots[k]));
                    sb.Append(")");
                }
                sb.Append(" · ");
            }
            if (item.ReqLevel > 1) sb.Append("요구 레벨 ").Append(item.ReqLevel).Append(" · ");
            sb.Append("분해 ").Append(LootFilter.SalvageValue(item)).Append(" 잿 파편");
            if (!string.IsNullOrEmpty(foot)) sb.Append('\n').Append(foot);
            _foot.text = sb.ToString();

            Layout();
            _box.gameObject.SetActive(true);
            _box.SetAsLastSibling();
        }

        /// <summary>
        /// A row's change against the worn piece: "(+3)" green, "(−1%)" red, "(새 옵션)" when the worn one has none of
        /// it, nothing when equal. A stat whose label counts down (받는 피해 −N%) keeps the label's sign, so more of it
        /// reads "(−1%)" in green.
        /// </summary>
        /// <summary>A stat's amount alone, as its lines print it ("+44.8%", "−12%").</summary>
        static string Amount(Stat s, float v)
        {
            var def = ItemData.Affix(s);
            bool countsDown = def != null && def.Label.Contains("−{0}");
            return (countsDown ? "−" : "+") + v.ToString(v % 1f == 0f ? "0" : "0.#") + (def != null && def.Percent ? "%" : "");
        }

        /// <summary>"전설 효과" or "고유 효과": the line's head follows the item's own rarity (loop 1: a 고유 said 전설).</summary>
        public static string EffectName(Rarity r) => r == Rarity.Unique ? "고유 효과" : "전설 효과";

        static string Change(Stat s, float now, float before)
        {
            if (before <= 0f) return "<color=" + ItemStyle.GainHex + ">(새 옵션)</color>";
            float d = now - before;
            if (Mathf.Abs(d) < 0.05f) return "";
            var def = ItemData.Affix(s);
            bool countsDown = def != null && def.Label.Contains("−{0}");
            float abs = Mathf.Abs(d);
            return "<color=" + (d > 0f ? ItemStyle.GainHex : ItemStyle.LossHex) + ">(" + (d > 0f != countsDown ? "+" : "−")
                + abs.ToString(abs % 1f == 0f ? "0" : "0.#") + (def != null && def.Percent ? "%" : "") + ")</color>";
        }

        Row NextRow()
        {
            if (_rowCount == _rows.Count)
                _rows.Add(new Row
                {
                    Left = UiKit.Label(_box, "", Pad, 0f, Inner, 14f, 12),
                    Right = UiKit.Label(_box, "", Pad, 0f, Inner, 14f, 12, TextAnchor.UpperRight),
                });
            return _rows[_rowCount++];
        }

        Image NextGroupRule()
        {
            if (_ruleCount == _groupRules.Count)
                _groupRules.Add(UiKit.Fill(_box, "group rule", Pad, 0f, Inner, 1f, new Color(1f, 0.85f, 0.6f, 0.08f)));
            return _groupRules[_ruleCount++];
        }

        void Layout()
        {
            bool banded = _band.text.Length > 0;
            _band.enabled = _bandBack.enabled = banded;
            Place(_band.rectTransform, 0f, BandH);
            Place(_bandBack.rectTransform, 0f, BandH);
            float y = Pad + (banded ? BandH : 0f);
            // Name at the left, "◆◆◆ 희귀" at the right of the same row; a long name wraps short of the tag.
            _name.rectTransform.sizeDelta = new Vector2(Inner - Mathf.Ceil(_tag.preferredWidth) - 6f, 14f);
            Place(_tag.rectTransform, y, 14f);
            y = Stack(_name, y, 2f);
            y = Stack(_sub, y, 3f);
            y = Stack(_verdict, y, 0f);
            y += Gap;
            Place(_rule1.rectTransform, y, 1f);
            y += 1f + Gap;

            int group = -1;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool on = i < _rowCount;
                row.Left.enabled = row.Right.enabled = on;
                if (!on) continue;
                int g = ItemStyle.Group(_rowStats[i]);
                if (group >= 0 && g != group)
                {
                    y += 2f;
                    Place(NextGroupRule().rectTransform, y, 1f);
                    y += 3f;
                }
                group = g;
                float h = Mathf.Ceil(row.Left.preferredHeight);
                Place(row.Left.rectTransform, y, h);
                Place(row.Right.rectTransform, y, h);
                y += h + 1f;
            }
            for (int i = 0; i < _groupRules.Count; i++) _groupRules[i].enabled = i < _ruleCount;
            if (_lost.text.Length > 0) y += 2f;
            y = Stack(_lost, y, 0f);

            _rule2.enabled = true;
            y += Gap;
            Place(_rule2.rectTransform, y, 1f);
            y += 1f + Gap;
            y = Stack(_extra, y, 3f);
            y = Stack(_foot, y, 0f);
            y += Pad;
            _box.sizeDelta = new Vector2(Width, y);
            _frame.rectTransform.sizeDelta = new Vector2(Width + 6f, y + 6f);
        }

        /// <summary>Puts a text block at <paramref name="y"/> at its own height and returns the y under it; an empty one takes no room.</summary>
        static float Stack(Text t, float y, float gapAfter)
        {
            bool on = t.text.Length > 0;
            t.enabled = on;
            if (!on) return y;
            float h = Mathf.Ceil(t.preferredHeight);
            Place(t.rectTransform, y, h);
            return y + h + gapAfter;
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

        /// <summary>Fixed on the left of <paramref name="window"/>, top-aligned with it (UX loop 2 P0-1: beside the hovered cell the
        /// tooltips covered the bag, the orb and the skill bar).</summary>
        public void PlaceLeftOf(RectTransform window, float gap = 8f)
        {
            var corners = new Vector3[4];
            window.GetWorldCorners(corners);
            Vector2 tl = _ui.Root.InverseTransformPoint(corners[1]);
            var half = _ui.Root.rect.size * 0.5f;
            float x = Mathf.Clamp(tl.x - gap - Width, -half.x + 2f, half.x - Width - 2f);
            float y = Mathf.Clamp(tl.y, -half.y + _box.sizeDelta.y + 2f, half.y - 2f);
            _box.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
            _box.SetAsLastSibling(); // the left half is where the character window stands: stay above it
        }

        /// <summary>Left of another tooltip, or under it when there is no room on the left of the screen.</summary>
        public void PlaceLeftOrBelow(ItemTooltip other)
        {
            var half = _ui.Root.rect.size * 0.5f;
            var p = other._box.anchoredPosition;
            float x = p.x - Width - 4f;
            float y = p.y;
            if (x < -half.x + 2f)
            {
                x = p.x;
                y = p.y - other._box.sizeDelta.y - 4f;
            }
            y = Mathf.Clamp(y, -half.y + _box.sizeDelta.y + 2f, half.y - 2f);
            _box.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
            _box.SetAsLastSibling();
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
