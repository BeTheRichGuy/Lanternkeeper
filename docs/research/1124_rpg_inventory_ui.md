# ARPG/RPG 인벤토리·캐릭터·스킬 창 구성 (1124 리서치)

표기: [확인]=페이지 본문을 직접 읽음, [요약]=검색 결과 요약문만 확인, [기억]=기존 지식(수치 재검증 필요). 칸 크기(px)는 공개 문서를 못 찾아 대부분 [기억]이거나 비율로만 적었다.

## 1. 게임별 레이아웃 사실

### Diablo IV
- 캐릭터 창 하나에 "스탯 + 장비 + 인벤토리"를 합쳤다. 위쪽이 캐릭터 상세(스탯 패널 + 장비 슬롯 + 캐릭터 프리뷰), 아래쪽이 인벤토리(탭 + 격자 + 재화). [확인] https://gamedevartisan.com/game-design/modern-games-feature-study/feature-study-d4-inventory
- 스탯 패널은 이름/레벨/칭호, 프로필·재료·상세 스탯 버튼, 주 스탯, 부 스탯. "상세 스탯"은 빨간 버튼으로 옆 패널을 밀어내는 방식(점진적 공개). 왼쪽에 공격력/방어/생명/재료/4대 능력치만 노출. [확인] https://www.purediablo.com/diablo4/Inventory
- 인벤토리: 11열 x 3행 = 33칸, 탭마다 33칸(장비/퀘스트/소모품/위상(Aspects)). 아이템은 항상 1칸(과거 D2/D3의 1x1~2x4 가변 크기를 버림). 보관함(Stash)이 별도. 2022-12 이전엔 탈것/정수 탭도 있었다. [확인] 같은 URL
- 화면 비율: Godot로 D4를 재현한 연구는 캐릭터 상세 3/5, 인벤토리 2/5, 스탯:장비 = 1/3:2/3로 분할(재현자의 선택이며 원본 계측은 아님). [확인] gamedevartisan
- 팬 케이스 스터디의 지적: 빌드 교체 때 스킬/패라곤을 수동 재배치(스킬 최대 58점, 패라곤 225점)해야 해서 불편 → 로드아웃 저장, 보관함 검색/필터, "아이템 잠금(대량 판매 시 실수 방지)" 제안. [확인] https://anabelsa87.myportfolio.com/diablo-4-inventory-case-study

### Path of Exile 2
- 창 규칙: 전체화면/중앙 창은 게임을 멈추고, 측면 패널은 멈추지 않으며, 전체화면 1 + 왼쪽 패널 1 + 오른쪽 패널 1개까지만 동시에 열 수 있다. [요약] https://www.poe2wiki.net/wiki/Interface (검색 요약, 본문은 렌더 실패)
- 인벤토리 UI에 대한 불만 스레드가 별도로 있음(가방 크기/정리). 신 아이템 툴팁 템플릿이 D4 같다는 반응. [요약] https://www.pathofexile.com/forum/view-thread/3589132 , https://www.reddit.com/r/PathOfExile2/comments/1djy45y/more_ui_exploration/
- 루트 필터는 게임 안에서 편집 불가, 옵션 > UI 탭 맨 아래에서 .filter 파일을 선택/Reload 하는 방식(PoE1). [확인] https://www.poewiki.net/wiki/Item_filter
- 가방 격자 [기억]: PoE1 12x5 칸, 아이템이 다칸(예: 2x3) 크기 — 격자 퍼즐 요소. D2 10x4.

### Last Epoch
- 루트 필터가 가장 정교한 내장 필터라는 평(규칙/조건/색/소리). Shift+F 또는 ESC > 설정 > Interface "Manage Loot Filters"로 연다. 필터 가져오기/내보내기가 인게임. [확인] https://maxroll.gg/last-epoch/resources/loot-filter-guide , https://support.lastepoch.com/hc/en-us/articles/46361669143067-Loot-Filter
- 필터는 "숨김"이지 "삭제"가 아님(드롭은 되고 이름표만 숨김). [확인] maxroll

### Torchlight Infinite
- 필터는 설정 > Loot 안에 있고, 공식 기본/중급/고급 3종 + 커스텀(템플릿에서 시작). 필터 UI가 복잡해 "특정 티어 옵션만 보기" 설정을 못 찾겠다는 불만. [확인/요약] https://tlidb.com/en/Filter , https://steamcommunity.com/app/1974050/discussions/0/4042609216215248615/

### Lost Ark
- 인벤토리 외에 펫 인벤토리, 원정대(로스터) 공용 보관함, Ctrl+클릭으로 가방↔펫 이동, Alt+우클릭으로 계정 보관함 보내기 등 "이동 단축"에 의존. 전투 데미지 폰트 크기를 따로 설정하는 옵션(UserOption.xml에서 BattleFontSize 0=100%, 1=150%, 2=200%). [요약] https://steamcommunity.com/sharedfiles/filedetails/?id=2750273945 , https://www.inven.co.kr/board/lostark/4821/87753
- 설정/가방 관리용 설정 영상이 따로 나올 만큼 인벤토리 설정이 분산돼 있음(반면교사). [요약] https://www.youtube.com/watch?v=8ntMxmEkPH4

### 모바일/방치형
- Idle Looter(PC/모바일 방치형): 자동분해 필터가 장비 슬롯/레어도/전투력/스탯 기준 + 소비재 탭 분류 정렬 + 젬 무제한 스택으로 가방 압박 제거. [확인] https://mokaaja.itch.io/idle-looter/devlog/1086177/134-official-p0-mode-qol-changes
- Deskrawl류: 루트 필터·자동 판매·창 설정을 "첫 몇 시간 설정 가이드" 한 챕터로 설명해야 할 정도로 핵심 기능. [요약] https://apollotrail.com/blog/deskrawl-beginner-idle-setup-guide-loot-filter-auto-sell-window-settings
- Legend of Mushroom: 홈 화면에 모든 기능이 모여 있고 가이드가 "홈 화면에서 무엇이 어디 있는지"를 따로 설명할 정도로 메뉴가 많음. 장비/램프/유물/맨션 보조 등 기능이 가득. [요약] https://guidesbygrace.uk/navigation-essentials , https://guidesbygrace.uk/gear-lamp
- Diablo Immortal은 모바일에서 UI 커스터마이즈(스킬 버튼 위치 변경)가 3년째 안 된다는 불만. [요약] https://us.forums.blizzard.com/en/diablo-immortal/t/ui-customization/10776

### 이번에 못 얻은 것 (정직한 공백)
- 격자 칸의 픽셀 크기/간격 공식 수치, Torchlight/Lost Ark 창 그림 치수는 문서 못 찾음. Game UI Database(https://www.gameuidatabase.com/gameData.php?id=1783, "1,300게임/55,000 스크린샷")에서 스크린샷을 직접 재야 한다. [요약]

## 2. 일반 UI 원칙 (출처)
- 점진적 공개(Progressive Disclosure), Jakob Nielsen, NN/g 2006: "처음엔 가장 중요한 몇 개만 보이고, 전문 옵션은 요청 시에만 공개". 인쇄 대화상자가 고전 예시이고, 고급 옵션은 보조 대화상자로 보낸다. 기능이 불어나 초기 대화상자가 비대해지는 것을 경고. [확인] https://www.nngroup.com/articles/progressive-disclosure/
- 시각 위계: 크기, 대비, 위치로 "지금 무슨 일, 무엇을 할 수 있나, 방금 무슨 일, 무엇이 중요한가"에 즉답하도록. [요약] https://adventuresincausality.com/principles/GDC-L1-UX-0003
- Celia Hodent, The Gamer's Brain / GDC 2015 "How Neuroscience and UX Can Impact Game Design": 인지 부하 관리, 사용성/참여 분리. [요약] https://www.gdcvault.com/play/1022310/The-Gamer-s-Brain-How , https://celiahodent.com/ (원문 본문은 읽지 못함. "한 화면 한 기능"이라는 문구는 Hodent의 것이 아니라 내 요약이다.)
- 게임 UI 패턴 카탈로그: https://gamedesign.gg/articles/game-ui-design-patterns/ [요약]

## 3. 관찰에서 도출한 구조 규칙 (내 추론)
- 한 창은 한 가지 일: D4는 "내 장비 확인+가방 정리"를 한 창에 합쳤지만 상세 스탯은 슬라이드 패널로 분리. 설정(필터/자동분해)은 거의 모든 게임이 인벤토리 본창이 아니라 설정/별도 창으로 뺀다(PoE=옵션, LE=Shift+F 전용 창, TLI=설정>Loot).
- 탭 4개 안팎(장비/소모/퀘스트/특수).
- 고정 격자 1칸=1아이템(D4)이 모바일과 방치형에 유리: 아이템 크기 퍼즐 없음.
- 툴팁 비교: 현재 착용 대비 증감(+/-) 색, 한 줄 요약 먼저, 세부는 아래. (D4/PoE2 공통 [기억])

## 우리 게임에 적용할 것 (1280x720 기준 제안)
1. 창 구조 확정: (a) 캐릭터+장비+스탯(왼쪽 41%) / (b) 가방 격자(오른쪽 59%) 한 화면, 스탯 상세는 "자세히" 버튼으로 우측 슬라이드. 전투 HUD와 겹치지 않게 창은 화면의 최대 75% 폭.
2. 가방 격자: 11x3 = 33칸/탭(D4 확정치)이 기준이지만 우리는 장비 아이콘 1칸 = 1아이템. 720p 기준 칸 48~56px(정수배 스케일로 3x 기준 16~18px 소스), 간격 4px, 칸 안 글자는 레어도 색 테두리 + 레벨/ilvl 숫자 1개만.
3. 설정 분리: 자동분해/루트 필터/정렬은 인벤토리 안 톱니 아이콘 → 별도 "정리 설정" 창(LE의 Shift+F, PoE 옵션 방식). 인벤토리 본창에는 [정렬] [일괄분해] 두 버튼만 둔다. 필터 프리셋은 공식 3종(보수/보통/공격적) + 직접 편집(TLI 방식).
4. 점진적 공개: 처음엔 레어도 하나만 고르는 슬라이더, "세부 조건" 펼치면 슬롯/전투력/옵션 조건(Idle Looter 방식).
5. 창당 텍스트량: 본창 라벨은 한 줄 12자 이내, 설명 문장은 툴팁으로. 한 화면에 3그룹 이상 정보를 넣지 않는다. "상세"는 두 번째 단계로.
6. 아이템 잠금(D4 사례) 필수, 전설/유니크는 기본 잠금. 일괄분해에서 잠금 제외.
7. 비교 툴팁: 상단에 "착용 중 대비 전투력 +N / -N"(색), 그다음 이름·등급·핵심 능력 한 줄, 기본/추가 옵션 구분(이미 구현된 방향 유지).
8. 이동 단축: 길게 누르기/Shift+클릭으로 착용/분해(Lost Ark 단축의 모바일판), 되돌리기 1회 제공.
9. 열 수 있는 창 수 제한: PoE2 규칙처럼 "전체창 1 + 좌측 1 + 우측 1"로 제한해 창이 겹쳐 지저분해지는 것을 방지.
