using System.Text;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// H: 도움말 in three tabs (UX plan C4; 희승 11-23 "진입장벽이 크게 느껴진달까"): 게임 방법 — five short pages on what the
    /// player actually decides (gear, skill order and resonance, practice, the camp and rebirth); 용어집 — the coined
    /// terms from GameText, paged; 조작 — every key. It used to be the key list only, titled 조작.
    /// </summary>
    public sealed class HelpPanel
    {
        public bool Visible { get; private set; }

        const float W = 920f, H = 520f;
        readonly UiKit _ui;
        readonly RectTransform _win;
        readonly Text _left, _right, _pageLabel;
        readonly Button[] _tabs = new Button[3];
        readonly Button _prev, _next;
        int _tab, _page;
        const int TermsPerPage = 14;

        const string Keys =
            "<color=#ffd38a>전투·진행</color>\n" +
            "  Space · 심장 클릭   등불 점화 (30초 피해 +15%, 3분마다)\n" +
            "  S   태세: 균형 → 맹공 → 신중\n" +
            "  P   추천 배치 바꾸기        R   스킬 직접 배치\n" +
            "  O   원정 명령 (지도 제작자 Lv1)\n" +
            "  L   층 고정 → 도감 우선 → 최상층 반복 (지도 제작자 Lv2 · Lv3)\n" +
            "  1 · 2 · 3   이벤트방 선택\n" +
            "  K   직업 선택 (직업마다 세이브 따로)\n\n" +
            "<color=#ffd38a>창</color>\n" +
            "  C   능력치     I · Tab   가방     F   자동 분해 단계\n" +
            "  Y   야영지 (명품화 · 재굴림 · 재련 · 룬 승급 · 상인 · 환생)\n" +
            "  T   스킬 트리      J   도감 (스킬 → 적 → 도전 과제)\n" +
            "  U   이야기 (Q/E 넘기기)      G   알림 기록 (자동 장착·분해·분배도 남음)\n" +
            "  Esc   창 닫기 / 설정\n" +
            "  오른쪽 아래 버튼   창을 마우스로 열기 · 오른쪽 클릭으로 닫기\n" +
            "  F2   화면 장식 숨기기 (사냥만 보기)";

        const string Keys2 =
            "<color=#ffd38a>화면</color>\n" +
            "  F11 · Alt+Enter   전체 화면\n" +
            "  마우스 휠   확대 · 축소\n" +
            "  V   피해 숫자 (전부 · 중요 · 끔)        Z   자동 지도\n" +
            "  B   보스 키 (창 바로 숨기기, 게임은 계속)\n" +
            "  X   등불 색 (도전 과제로 열림)\n\n" +
            "<color=#ffd38a>소리</color>\n" +
            "  M   전체 음소거        N   배경음\n\n" +
            "<color=#ffd38a>창 안에서</color>\n" +
            "  스킬 배치: 1~4 칸 · Q/E 스킬 · A/D 문양\n" +
            "  야영지: Q/E 부위 · A/D 재굴림 줄 · 1~8 실행\n" +
            "  스킬 트리: 칸 클릭으로 찍기 · 자동 분배 켜기/끄기";

        public HelpPanel(UiKit ui)
        {
            _ui = ui;
            _win = ui.Window("Help Panel", W, H, "도움말", Toggle, out _, "ui/ui2_window_iron");
            string[] names = { "게임 방법", "용어집", "조작" };
            for (int i = 0; i < 3; i++)
            {
                int t = i;
                _tabs[i] = ui.Button(_win, names[i], 20f + i * 150f, 34f, 140f, 22f, () => { _tab = t; _page = 0; Rebuild(); });
            }
            _left = UiKit.Label(_win, "", 20f, 70f, W / 2f - 30f, H - 110f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            _right = UiKit.Label(_win, "", W / 2f + 10f, 70f, W / 2f - 30f, H - 110f, 12, TextAnchor.UpperLeft, UiKit.Ink);
            _left.lineSpacing = _right.lineSpacing = 1.2f;
            UiKit.Fill(_win, "rule", W / 2f - 1f, 72f, 1f, H - 120f, UiKit.Line);
            _prev = ui.Button(_win, "◀ 이전", W - 300f, H - 34f, 90f, 20f, () => { _page--; Rebuild(); });
            _next = ui.Button(_win, "다음 ▶", W - 110f, H - 34f, 90f, 20f, () => { _page++; Rebuild(); });
            _pageLabel = UiKit.Label(_win, "", W - 205f, H - 31f, 90f, 16f, 12, TextAnchor.UpperCenter, UiKit.Dim);
            Rebuild();
            Show(false);
        }

        public void Toggle() => Show(!Visible);

        public void Show(bool v)
        {
            Visible = v;
            _win.gameObject.SetActive(v);
            if (v) { _win.SetAsLastSibling(); Rebuild(); }
        }

        void Rebuild()
        {
            for (int i = 0; i < 3; i++)
            {
                var label = _tabs[i].GetComponentInChildren<Text>();
                if (label != null) label.color = i == _tab ? UiKit.Gold : UiKit.Dim;
            }
            bool paged = _tab == 1;
            int pages = paged ? (GameText.Glossary.Length + TermsPerPage - 1) / TermsPerPage : 1;
            _page = Mathf.Clamp(_page, 0, pages - 1);
            _prev.gameObject.SetActive(paged);
            _next.gameObject.SetActive(paged);
            _pageLabel.text = paged ? (_page + 1) + " / " + pages : "";
            switch (_tab)
            {
                case 0: _left.text = HowLeft(); _right.text = HowRight(); break;
                case 1: Glossary(); break;
                default: _left.text = Keys; _right.text = Keys2 + "\n\n<color=#8a8178>H   도움말 닫기</color>"; break;
            }
        }

        static string HowLeft()
        {
            var sb = new StringBuilder();
            sb.Append("<color=#ffd38a>1  사냥은 알아서 합니다</color>\n")
              .Append("영웅은 혼자 층을 내려가며 싸웁니다. 내가 고르는 것은 두 가지,\n<color=#ffcf5a>무엇을 입을지</color>(가방 I)와 <color=#ffcf5a>스킬을 어떤 순서로 쓸지</color>(스킬 배치 R)입니다.\n")
              .Append("화면 위 '다음:' 줄이 지금 노릴 목표입니다.\n\n");
            sb.Append("<color=#ffd38a>2  장비 색과 화살표</color>\n")
              .Append("<color=#c8c8c0>일반</color> < <color=#6fa0ff>마법</color> < <color=#ffe45c>희귀</color> < <color=#ff8c26>전설</color> < <color=#e0bc74>고유</color>   위로 갈수록 드물고 옵션이 많습니다.\n")
              .Append("내게 더 좋은지는 색이 아니라 화살표가 알려 줍니다:\n<color=#5bd66b>▲</color> 더 강함 · <color=#ff5a4d>▼</color> 더 약함 ·\n<color=#8a8178>═</color> 비슷함 · <color=#ffd24d>⇄</color> 공격과 생존을 맞바꿈.\n")
              .Append("더 좋은 장비(▲)는 자동으로 낍니다.\n\n");
            sb.Append("<color=#ffd38a>3  스킬 순서와 공명</color>\n")
              .Append("스킬은 칸 순서대로 돌아가며 나갑니다. 원소 스킬은 맞은 적에게 <color=#cfe6ff>각인</color>을 남기고,\n")
              .Append("다음 칸이 순환 순서의 다음 원소면 <color=#cfe9b4>공명</color>이 터집니다.\n")
              .Append(SkillCard.Elem(Element.Ember)).Append(" → ").Append(SkillCard.Elem(Element.Frost)).Append(" → ").Append(SkillCard.Elem(Element.Shock))
              .Append(" → ").Append(SkillCard.Elem(Element.Rot)).Append(" → ").Append(SkillCard.Elem(Element.Void)).Append(" → 다시 ").Append(SkillCard.Elem(Element.Ember)).Append('\n')
              .Append("스킬 배치(R)의 공명표 탭에서 원소 순서와 지금 배치가 무엇을 터뜨리는지 봅니다.");
            return sb.ToString();
        }

        static string HowRight()
        {
            var sb = new StringBuilder();
            sb.Append("<color=#ffd38a>4  숙련 · 분신 · 진화</color>\n")
              .Append("스킬은 쓸수록 <color=#a8d0ff>숙련</color>이 오릅니다(위력·범위 증가).\n")
              .Append("숙련 10마다 <color=#cfe6ff>분신</color> +1: 같은 스킬이 다른 적에게 한 번 더 나갑니다.\n")
              .Append("숙련이 차면 스킬 트리(T)의 진화 칸에서 더 강한 형태로 <color=#ffb060>진화</color>시킵니다.\n\n");
            sb.Append("<color=#ffd38a>5  야영지와 환생</color>\n")
              .Append("막 I 보스를 쓰러뜨리면 야영지(Y)가 열립니다.\n잿 파편으로 장비를 다듬습니다\n(명품화 · 재굴림 · 재련 · 룬 승급 · 상인).\n")
              .Append("Lv 60이 되면 야영지의 잔불에서 <color=#ffb060>환생</color>할 수 있습니다. 레벨은 1로 돌아가지만\n쌓인 경험치가 영구 <color=#cfe6ff>잔불 레벨</color>이 되고 장비는 그대로입니다.\n\n");
            // 점화 had no entry here, only a key line in 조작 (loop 3 P0).
            sb.Append("<color=#ffd38a>6  등불 점화</color>\n")
              .Append("'점화 가능 [Space]'가 뜨면 Space나 심장을 누릅니다.\n30초 동안 피해 +15%, 3분마다 다시 쓸 수 있습니다.\n\n");
            sb.Append("<color=#ffd38a>막혔다면</color>\n")
              .Append("· 가방의 ⇄ 장비를 직접 비교해 보기\n· 스킬 배치(R) → 추천 배치 탭의 [순서 시험]으로 더 센 순서 찾기\n· S 키로 태세를 '신중'으로\n· 야영지에서 무기 명품화\n\n")
              .Append("<color=#8a8178>단어 뜻은 [용어집], 키는 [조작] 탭에 있습니다.</color>");
            return sb.ToString();
        }

        void Glossary()
        {
            var l = new StringBuilder();
            var r = new StringBuilder();
            int from = _page * TermsPerPage, to = Mathf.Min(GameText.Glossary.Length, from + TermsPerPage);
            for (int i = from; i < to; i++)
            {
                var (term, meaning) = GameText.Glossary[i];
                var sb = i - from < TermsPerPage / 2 ? l : r;
                sb.Append("<color=#cfe6ff>").Append(term).Append("</color>  ").Append(meaning).Append("\n\n");
            }
            _left.text = l.ToString().TrimEnd('\n');
            _right.text = r.ToString().TrimEnd('\n');
        }
    }
}
