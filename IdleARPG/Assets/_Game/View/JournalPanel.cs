using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// G 알림 기록 (was 사냥 일지): the last notices (jackpots, evolutions, copies, wick, achievements, act bosses) with how long ago they
    /// came — for someone who left the hunt running and looks back now. Banded rows on the GUI canvas, newest first.
    /// </summary>
    public sealed class JournalPanel
    {
        const float W = 600f, H = 472f, RowH = 20f;
        const int Rows = 19;
        readonly RectTransform _win;
        readonly Text _count;
        readonly Text[] _ago = new Text[Rows], _line = new Text[Rows];
        public bool Visible { get; private set; }

        public JournalPanel(UiKit ui)
        {
            _win = ui.Window("Journal Panel", W, H, "알림 기록", () => Show(false), out _, "ui/ui2_window_iron");
            _count = UiKit.Label(_win, "", 20f, 32f, W - 40f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            for (int i = 0; i < Rows; i++)
            {
                float y = 52f + i * RowH;
                if (i % 2 == 0) UiKit.Fill(_win, "band", 16f, y - 3f, W - 32f, RowH, new Color(0.075f, 0.068f, 0.062f, 0.9f));
                _ago[i] = UiKit.Label(_win, "", 24f, y, 80f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
                _line[i] = UiKit.Label(_win, "", 104f, y, W - 128f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink);
                _line[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            UiKit.Label(_win, "(G) 닫기", W - 140f, H - 30f, 112f, 14f, 12, TextAnchor.UpperRight, UiKit.Dim);
            // Row texts raycast, so the wheel bubbles up to the window root.
            _win.gameObject.AddComponent<WheelCatcher>().OnWheel = step =>
            {
                if (_history == null) return;
                _offset = Mathf.Clamp(_offset + step, 0, Mathf.Max(0, _history.Count - Rows));
                Fill(_history);
            };
            Show(false);
        }

        public void Toggle(List<(DateTime at, string text)> history)
        {
            Show(!Visible);
            _offset = 0;
            if (Visible) Fill(history);
        }

        /// <summary>The mouse wheel pages through the 50 kept notices (only 19 rows fit).</summary>
        sealed class WheelCatcher : MonoBehaviour, UnityEngine.EventSystems.IScrollHandler
        {
            public Action<int> OnWheel;
            public void OnScroll(UnityEngine.EventSystems.PointerEventData e) => OnWheel?.Invoke(e.scrollDelta.y > 0f ? -3 : 3);
        }

        List<(DateTime at, string text)> _history;
        int _offset;

        void Fill(List<(DateTime at, string text)> history)
        {
            _history = history;
            _offset = Mathf.Clamp(_offset, 0, Mathf.Max(0, history.Count - Rows));
            _count.text = history.Count == 0 ? "아직 남길 만한 기록이 없습니다." : "최근 알림 " + history.Count + "개 · 접속을 넘어 최대 " + ToastView.HistoryMax + "개까지 남습니다"
                + (history.Count > Rows ? " · 휠로 넘기기" : "");
            var now = DateTime.Now;
            for (int r = 0; r < Rows; r++)
            {
                int i = history.Count - 1 - r - _offset;
                _ago[r].text = i >= 0 ? Ago(now - history[i].at) : "";
                _line[r].text = i >= 0 ? history[i].text : "";
            }
        }

        static string Ago(TimeSpan t) =>
            t.TotalSeconds < 60 ? "방금" : t.TotalMinutes < 60 ? (int)t.TotalMinutes + "분 전" : (int)t.TotalHours + "시간 전";

        void Show(bool v)
        {
            Visible = v;
            _win.gameObject.SetActive(v);
            if (v) _win.SetAsLastSibling();
        }
    }
}
