using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 이벤트방 선택 (UX plan D7): the offered event as a small box at the right middle, over any open window — its three
    /// options as buttons (also keys 1·2·3), the default marked, and a bar for the seconds left before the default is
    /// taken. It was a line of world text under the hero that any window covered.
    /// </summary>
    public sealed class EventPrompt
    {
        const float W = 300f, Row = 26f;
        readonly RectTransform _box;
        readonly Text _title, _foot;
        readonly Button[] _buttons = new Button[3];
        readonly Text[] _labels = new Text[3];
        readonly Image _barFill, _edge;
        SimWorld _world;
        SimWorld.OfferedEvent _shown;
        int _secsShown = -1;

        public EventPrompt(UiKit ui)
        {
            _box = UiKit.Rect(ui.Root, "Event Prompt", 0f, 0f, W, 60f);
            _box.anchorMin = _box.anchorMax = new Vector2(1f, 0.5f);
            _box.pivot = new Vector2(1f, 0.5f);
            _box.anchoredPosition = new Vector2(-16f, 40f);
            var back = _box.gameObject.AddComponent<Image>();
            back.color = new Color(0.04f, 0.03f, 0.03f, 0.94f);
            _edge = UiKit.Fill(_box, "edge", 0f, 0f, 3f, 60f, new Color(1f, 0.8f, 0.4f, 0.9f));
            _title = UiKit.Label(_box, "", 14f, 8f, W - 24f, 18f, 14, TextAnchor.UpperLeft, UiKit.Gold, title: true);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                _buttons[i] = ui.Button(_box, "", 12f, 32f + i * Row, W - 24f, Row - 4f, () => { if (_world != null) _world.ChooseEvent(index); });
                _labels[i] = _buttons[i].GetComponentInChildren<Text>();
                _labels[i].alignment = TextAnchor.MiddleLeft;
                _labels[i].supportRichText = true;
            }
            UiKit.Fill(_box, "bar back", 12f, 0f, W - 24f, 3f, new Color(0f, 0f, 0f, 0.7f));
            _barFill = UiKit.Fill(_box, "bar", 12f, 0f, W - 24f, 3f, new Color(1f, 0.8f, 0.4f, 0.9f));
            _foot = UiKit.Label(_box, "", 12f, 0f, W - 24f, 14f, 12, TextAnchor.UpperLeft, UiKit.Dim);
            _box.gameObject.SetActive(false);
        }

        /// <summary>Each frame: shown while an event waits and nothing covers the screen (augment cards keep the centre first).</summary>
        public void Render(SimWorld world, bool hidden)
        {
            _world = world;
            var p = world.PendingEvent;
            if (p == null || hidden || world.Hero.AugOffer != null)
            {
                if (_box.gameObject.activeSelf) _box.gameObject.SetActive(false);
                _shown = null;
                return;
            }
            if (!_box.gameObject.activeSelf) _box.gameObject.SetActive(true);
            if (_box.GetSiblingIndex() != _box.parent.childCount - 1) _box.SetAsLastSibling(); // over any window
            float left = Mathf.Max(0f, (p.DeadlineTick - world.TickCount) * SimWorld.Dt);
            int secs = Mathf.CeilToInt(left);
            float full = SimWorld.EventDecisionTicks * SimWorld.Dt;
            _barFill.rectTransform.sizeDelta = new Vector2(Mathf.Round((W - 24f) * Mathf.Clamp01(left / full)), 3f);
            if (p == _shown && secs == _secsShown) return;
            if (p != _shown) Layout(p);
            _shown = p;
            _secsShown = secs;
            _foot.text = "누르거나 1·2·3 · " + secs + "초 뒤 기본값";
        }

        void Layout(SimWorld.OfferedEvent p)
        {
            _title.text = p.Title;
            int n = Mathf.Min(3, p.Options.Length);
            for (int i = 0; i < 3; i++)
            {
                bool on = i < n;
                _buttons[i].gameObject.SetActive(on);
                if (!on) continue;
                bool def = i == p.Default;
                _labels[i].text = "  [" + (i + 1) + "] " + p.Options[i] + (def ? "  <color=#8a8178>기본</color>" : "");
                _labels[i].color = def ? UiKit.Gold : UiKit.Ink;
            }
            float barY = 32f + n * Row + 4f;
            _barFill.rectTransform.anchoredPosition = new Vector2(12f, -barY);
            ((RectTransform)_barFill.transform.parent.Find("bar back")).anchoredPosition = new Vector2(12f, -barY);
            _foot.rectTransform.anchoredPosition = new Vector2(12f, -(barY + 8f));
            _box.sizeDelta = new Vector2(W, barY + 28f);
            _edge.rectTransform.sizeDelta = new Vector2(3f, barY + 28f);
        }
    }
}
