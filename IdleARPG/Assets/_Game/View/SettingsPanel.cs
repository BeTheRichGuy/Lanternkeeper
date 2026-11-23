using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Esc: comfort and display options on the GUI canvas (재기획 §5.9), grouped — screen and effects, sound, idle —
    /// each row a switch or a stepper. A click or the row's number key changes it (GameBootstrap applies the change
    /// through <see cref="OnPick"/> and calls <see cref="Refresh"/>).
    /// </summary>
    public sealed class SettingsPanel
    {
        const float W = 460f, H = 460f, RowH = 24f; // 11 rows, 4 headings, the character button, the footer line
        readonly RectTransform _win;
        readonly Image[] _track = new Image[11], _knob = new Image[11];
        readonly Text[] _value = new Text[11];
        public bool Visible { get; private set; }
        public Action<int> OnPick;
        /// <summary>캐릭터 선택 (Diablo's character screen).</summary>
        public Action OnCharacters;

        /// <summary>Rows: group heading (or null), label, note, a switch (true) or a stepper (false).</summary>
        static readonly (string group, string label, string note, bool toggle)[] Rows =
        {
            ("화면 및 효과", "화면 흔들림", null, true),
            (null, "피격 섬광", null, true),
            (null, "스킬 효과 강도", null, false),
            (null, "피해 숫자", null, false),
            (null, "분신 이펙트", "직접 시전은 유지", false),
            ("소리", "배경음", null, true),
            (null, "소리", null, true),
            ("창 · 자동 진행", "전체 화면", null, true),
            (null, "자동 점화", "정예·보스·대규모 무리, 20초", true),
            (null, "좌측 상단 알림", "꺼도 일지(G)에 기록됨", true),
            ("고어", "피 표현", "쓰러질 때 피·바닥 핏자국", true),
        };

        public SettingsPanel(UiKit ui)
        {
            _win = ui.Window("Settings Panel", W, H, "설정", Toggle, out _, "ui/ui2_window_iron");
            float y = 34f;
            for (int i = 0; i < Rows.Length; i++)
            {
                int index = i;
                var r = Rows[i];
                if (r.group != null)
                {
                    if (i > 0) y += 6f;
                    UiKit.Label(_win, r.group, 20f, y, 200f, 14f, 12, TextAnchor.UpperLeft, UiKit.Gold, title: true);
                    UiKit.Fill(_win, "rule", 20f, y + 16f, W - 40f, 1f, UiKit.Line);
                    y += 22f;
                }
                // The whole row is the button (hover lights it).
                var row = ui.Button(_win, "", 16f, y, W - 32f, RowH - 2f, () => OnPick?.Invoke(index));
                UiKit.Label(row.transform, "<color=#8a8178>" + (i + 1) + "</color>  " + r.label + (r.note != null ? "  <color=#6f6860>" + r.note + "</color>" : ""),
                    8f, 5f, W - 140f, 14f, 12, TextAnchor.UpperLeft, UiKit.Ink);
                if (r.toggle)
                {
                    _track[i] = UiKit.Fill(row.transform, "track", W - 32f - 50f, 5f, 34f, 12f, Color.white);
                    _track[i].sprite = Pill(ui);
                    _track[i].type = Image.Type.Sliced;
                    _knob[i] = UiKit.Fill(row.transform, "knob", 0f, 3f, 16f, 16f, Color.white);
                    _knob[i].sprite = UiKit.Disc(ui, 16, 0);
                }
                else _value[i] = UiKit.Label(row.transform, "", W - 32f - 132f, 5f, 124f, 14f, 12, TextAnchor.UpperRight, UiKit.Gold);
                y += RowH;
            }
            ui.Button(_win, "캐릭터 선택", W / 2f - 80f, H - 50f, 160f, 22f, () => OnCharacters?.Invoke());
            UiKit.Label(_win, "클릭 또는 숫자 키로 변경 · Esc로 닫기", 16f, H - 22f, W - 32f, 14f, 12, TextAnchor.UpperCenter, UiKit.Dim);
            Show(false);
        }

        /// <summary>A rounded bar for the switch track (sliced so it stretches without bending its ends).</summary>
        static Sprite _pill;
        static Sprite Pill(UiKit ui)
        {
            if (_pill != null) return _pill;
            const int s = 12;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(6f, 6f));
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(d <= 6f ? 255 : 0));
                }
            tex.SetPixels32(px);
            tex.Apply();
            _pill = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(5, 5, 5, 5));
            return _pill;
        }

        public void Refresh(bool music, bool muted, string numbers, bool fullscreen, bool autoIgnite)
        {
            bool[] on = { GameSettings.Shake, GameSettings.HitFlash, false, false, false, music, !muted, fullscreen, autoIgnite, GameSettings.Notices, GameSettings.Blood };
            for (int i = 0; i < Rows.Length; i++)
            {
                if (!Rows[i].toggle) continue;
                _track[i].color = on[i] ? new Color(0.45f, 0.75f, 0.4f) : new Color(0.22f, 0.2f, 0.19f);
                // The knob sits at the right end when on.
                var kr = _knob[i].rectTransform;
                kr.anchoredPosition = new Vector2(W - 32f - 50f + (on[i] ? 18f : -2f), -1f);
                _knob[i].color = on[i] ? new Color(0.95f, 0.92f, 0.82f) : new Color(0.55f, 0.52f, 0.48f);
            }
            _value[2].text = "‹ " + GameSettings.FxName + " ›";
            _value[3].text = "‹ " + numbers + " ›";
            _value[4].text = "‹ " + GameSettings.EchoName + " ›";
        }

        public void Toggle() => Show(!Visible);

        public void Show(bool v)
        {
            Visible = v;
            _win.gameObject.SetActive(v);
            if (v) _win.SetAsLastSibling();
        }
    }
}
