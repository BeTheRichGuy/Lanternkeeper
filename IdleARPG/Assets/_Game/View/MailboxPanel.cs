using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// U: the story mailbox (GDD §26.6) on the GUI canvas. Left: the records, opened ones by title (new ones marked) as
    /// buttons, the rest as their hint in grey. Right: the selected record on burnt parchment. Q/E or a click move through
    /// the opened ones; reading marks it read.
    /// </summary>
    public sealed class MailboxPanel
    {
        const float W = 760f, H = 440f, ListW = 250f;
        readonly UiKit _ui;
        readonly SimWorld _world;
        readonly RectTransform _win, _list;
        readonly Text _count, _title, _body;
        readonly List<GameObject> _rows = new List<GameObject>();
        public bool Visible { get; private set; }
        int _sel, _stamp = -1;

        public MailboxPanel(UiKit ui, SimWorld world)
        {
            _ui = ui;
            _world = world;
            _win = ui.Window("Mailbox Panel", W, H, "기록", Toggle, out _, "ui/ui2_window_iron");
            _count = UiKit.Label(_win, "", 20f, 32f, ListW, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _list = UiKit.Rect(_win, "list", 16f, 52f, ListW, H - 76f);
            _list.gameObject.AddComponent<RectMask2D>(); // long hints stay one line, clipped at the list's edge
            var paper = ui.Sprite(_win, "ui/ui2_parchment", ListW + 32f, 30f, W - ListW - 48f, H - 54f, sliced: true);
            paper.preserveAspect = false;
            paper.color = new Color(0.42f, 0.36f, 0.32f, 1f); // dark enough for light text, like the reliquary card
            _title = UiKit.Label(_win, "", ListW + 56f, 50f, W - ListW - 96f, 18f, 16, TextAnchor.UpperLeft, UiKit.Gold, title: true);
            _body = UiKit.Label(_win, "", ListW + 56f, 80f, W - ListW - 96f, H - 120f, 12, TextAnchor.UpperLeft, new Color(0.9f, 0.85f, 0.76f));
            _body.lineSpacing = 1.35f;
            UiKit.Label(_win, "Q/E 넘기기 · U 닫기", 16f, H - 22f, W - 32f, 14f, 12, TextAnchor.UpperRight, UiKit.Dim);
            SetVisible(false);
        }

        public void Toggle() => SetVisible(!Visible);

        void SetVisible(bool v)
        {
            Visible = v;
            _win.gameObject.SetActive(v);
            if (v) _win.SetAsLastSibling();
            _stamp = -1;
        }

        List<LetterDef> Opened()
        {
            var list = new List<LetterDef>();
            foreach (var l in _world.LettersFor()) if (_world.Hero.Letters.Contains(l.Id)) list.Add(l);
            return list;
        }

        public void Move(int step)
        {
            int n = Opened().Count;
            if (n == 0) return;
            _sel = ((_sel + step) % n + n) % n;
            _stamp = -1;
        }

        public void Render()
        {
            if (!Visible) return;
            var h = _world.Hero;
            var opened = Opened();
            int stamp = _sel * 1000 + opened.Count * 10 + h.LettersRead.Count;
            if (stamp == _stamp) return;
            _stamp = stamp;
            if (_sel >= opened.Count) _sel = 0;

            foreach (var g in _rows) Object.Destroy(g);
            _rows.Clear();
            _count.text = opened.Count + " 편";
            // The opened records first (a window around the selection when there are more than fit), then the hints of
            // the rest in what room is left — every opened record stays reachable by click (review 11-21).
            const int Fit = 15;
            int first = opened.Count <= Fit ? 0 : Mathf.Clamp(_sel - Fit / 2, 0, opened.Count - Fit);
            float y = 0f;
            if (first > 0) { _rows.Add(UiKit.Label(_list, "▲ " + first + "편 더", 4f, y + 2f, ListW - 8f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim).gameObject); y += 18f; }
            for (int i = first; i < opened.Count && i < first + Fit; i++)
            {
                var l = opened[i];
                int index = i;
                bool sel = index == _sel;
                bool unread = !h.LettersRead.Contains(l.Id);
                var b = _ui.Button(_list, "", 0f, y, ListW, 19f, () => { _sel = index; _stamp = -1; });
                UiKit.Label(b.transform, (sel ? "<color=#ffcf5a>" : "") + l.Title + (sel ? "</color>" : "") + (unread && !sel ? "  <color=#ff9a40>새 기록</color>" : ""),
                    8f, 3f, ListW - 16f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink);
                _rows.Add(b.gameObject);
                y += 21f;
            }
            if (first + Fit < opened.Count) { _rows.Add(UiKit.Label(_list, "▼ " + (opened.Count - first - Fit) + "편 더", 4f, y + 2f, ListW - 8f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim).gameObject); y += 18f; }
            foreach (var l in _world.LettersFor())
            {
                if (y > H - 100f) break;
                if (h.Letters.Contains(l.Id)) continue;
                var hint = UiKit.Label(_list, "· " + l.Hint, 4f, y + 3f, ListW - 8f, 14f, 12, TextAnchor.UpperLeft, new Color(0.38f, 0.36f, 0.33f));
                hint.horizontalOverflow = HorizontalWrapMode.Overflow;
                _rows.Add(hint.gameObject);
                y += 20f;
            }
            if (opened.Count == 0) { _title.text = ""; _body.text = "<color=#8a8178>아직 남은 기록이 없습니다.</color>"; return; }
            var cur = opened[_sel];
            _world.MarkRead(cur.Id);
            _title.text = cur.Title;
            _body.text = cur.Text;
        }
    }
}
