using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>H: every key on one page, two columns on the GUI canvas.</summary>
    public sealed class HelpPanel
    {
        public bool Visible { get; private set; }

        const string Left =
            "<b>조작</b>\n\n" +
            "<color=#ffd38a>전투·진행</color>\n" +
            "  Space · 등불 클릭   등불 점화 (30초 피해 +15%, 3분마다)\n" +
            "  S   태세: 균형 → 돌격 → 신중\n" +
            "  P   추천 배치 바꾸기        R   스킬 직접 배치\n" +
            "  O   원정 지령 (지도 제작자 Lv1)\n" +
            "  L   층 고정 → 도감 먼저 → 꼭대기 반복 (지도 제작자 Lv2 · Lv3)\n" +
            "  1 · 2 · 3   이벤트방 선택\n" +
            "  K   다른 직업으로 (직업마다 세이브 따로)\n\n" +
            "<color=#ffd38a>패널</color>\n" +
            "  C   캐릭터 (능력치)     I · Tab   소지품     F   자동 분해 단계\n" +
            "  Y   야영지 (명품화 · 재굴림 · 재련 · 룬 승급 · 상인 · 환생)\n" +
            "  T   스킬 트리                     J   도감 (스킬 → 적 → 도전 과제)\n" +
            "  U   기록 (Q/E 넘기기)        G   사냥 일지 (최근 알림 30개)\n" +
            "  Esc   패널 닫기 / 설정 (흔들림 · 번쩍임 · 효과 강도 · 전체 화면)\n" +
            "  오른쪽 아래 버튼   패널을 마우스로 열기 · 오른쪽 클릭으로 닫기\n" +
            "  F1   테스트 패널 (막 이동 · 레벨 · 전설 · 숙련)\n" +
            "  F2   화면 장식 숨기기 (HUD · 알림 · 지도, 사냥만 보기)";

        const string Right =
            "\n\n<color=#ffd38a>화면</color>\n" +
            "  F11 · Alt+Enter   전체 화면\n" +
            "  마우스 휠   확대 · 축소\n" +
            "  V   피해 숫자 (전부 · 중요 · 끔)        Z   자동 지도 (클릭하면 크게)\n" +
            "  B   보스 키 (창 바로 숨기기, 게임은 계속)\n" +
            "  X   등불 색 (도전 과제로 열림)\n\n" +
            "<color=#ffd38a>소리</color>\n" +
            "  M   전체 음소거        N   배경음\n\n" +
            "<color=#ffd38a>패널 안에서</color>\n" +
            "  스킬 배치: 1~4 칸 · Q/E 스킬 · A/D 문양\n" +
            "  야영지: Q/E 부위 · A/D 재굴림 줄 · 1~8 실행\n" +
            "  스킬 트리: 칸 클릭으로 찍기 · 자동 분배 켜기/끄기 · 초기화\n\n" +
            "<color=#8a8178>H   도움말 닫기</color>";

        public HelpPanel(UiKit ui)
        {
            _win = ui.Window("Help Panel", W, H, "조작", Toggle, out _, "ui/ui2_window_iron");
            var left = UiKit.Label(_win, Left.Replace("<b>조작</b>\n\n", ""), 20f, 34f, W / 2f - 30f, H - 50f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            var right = UiKit.Label(_win, Right.TrimStart('\n'), W / 2f + 10f, 34f, W / 2f - 30f, H - 50f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            left.lineSpacing = right.lineSpacing = 1.2f;
            UiKit.Fill(_win, "rule", W / 2f - 1f, 40f, 1f, H - 70f, UiKit.Line);
            Show(false);
        }

        const float W = 920f, H = 420f; // the longest key lines fit their column without wrapping
        readonly RectTransform _win;

        public void Toggle() => Show(!Visible);

        public void Show(bool v)
        {
            Visible = v;
            _win.gameObject.SetActive(v);
            if (v) _win.SetAsLastSibling();
        }
    }
}
