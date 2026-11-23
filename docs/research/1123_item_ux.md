# 아이템 UX 리서치 — "한눈에 이해되는 전리품" (2026-11-23)

대상: Lanternkeeper (방치형 디아블로라이크, 아이소메트릭 픽셀 UI). 문제: 신규 유저가 (1) 어떤 스탯이 더 좋은지, (2) 아이템 색이 무슨 뜻인지, (3) 스킬이 뭘 하는지 모른다. 이 문서는 (1)(2)와 인벤토리 정리를 다룬다. 스킬 설명은 3절의 용어집 방식을 같이 쓰면 된다.

## 0. 조사 방법과 한계 (먼저 읽기)

- ego-browser 작업 공간을 열었으나 첫 스크립트에서 브라우저 알림 권한 프롬프트가 떠 사용자 제어로 넘어갔다. 지침대로 우회하지 않고 멈췄고, 공개 페이지 열람은 WebSearch/WebFetch로 대체했다. 작업 공간이 사용자 쪽에 남아 있어 `finish({ keep: [] })`는 못 돌렸다. 필요하면 사용자가 그 탭을 닫으면 된다.
- 스크린샷을 직접 보지는 못했다. 아래 UI 동작은 위키, 공식 블로그, 포럼, 가이드 글에서 읽은 내용이다. 확인 못 한 것은 "(미확인)", 내 기존 지식에서 온 것은 "(일반 지식)"으로 표시했다.
- 방치형 게임(Nomad Idle, Melvor Idle, Idle Champions, Tap Titans, Soul Knight, Grim Quest, Loot Hero, Idle Slayer) 쪽은 검색에서 직접 근거가 거의 안 나왔다. 그 부분은 비슷한 방치형(Emoji Battle Clicker, Idle Heroes, Lootfiend)과 Diablo Immortal 자료로 보충했고, 해당 게임은 표에서 "근거 없음"으로 적었다.

## 1. 희귀도 색 관례와 학습 방식

### 1-1. 게임별 색 (확인된 것)

| 게임 | 등급 순서 (낮음 → 높음) | 출처 |
|---|---|---|
| WoW | Poor 회색 #9d9d9d → Common 흰색 #ffffff → Uncommon 초록 #1eff00 → Rare 파랑 #0070dd → Epic 보라 #a335ee → Legendary 주황 #ff8000 → Artifact 연금색 #e6cc80 → Heirloom 하늘색 #00ccff | warcraft.wiki.gg/wiki/Quality |
| Diablo 1/2 | 회색(잡템) → 흰색(일반) → 파랑(매직) → 노랑(레어) → 초록(세트) → 금색(유니크). 공식 배틀넷 D2 페이지는 매직을 보라, 크래프트를 주황으로 적어 출처마다 다르다 | Blizzard 포럼 "Rarity levels of each diablo", classic.battle.net/diablo2exp/items |
| Diablo 3 | D2의 금색과 노랑이 헷갈려서 유니크 계열을 주황으로 바꿨다고 위키에 적혀 있음 (세트는 초록) | Diablo wiki 검색 결과 요약 |
| Diablo 4 | 일반 흰색 → 매직 파랑(어픽스 최대 2) → 레어 노랑(2~6) → 전설 주황(일반 4 + 전설 1) → 유니크 금색. UI에서는 아이템 배경색으로도 등급을 보여준다 | charlieintel.com Diablo 4 rarity 글 |
| Path of Exile 1/2 | 일반 흰색(PoE2 회색) → 매직 파랑 → 레어 노랑 → 유니크 주황(PoE2 위키는 갈색이라고 적음) | gamerjournalist.com PoE rarity 글, poe2wiki.net |
| Last Epoch | 일반 흰색(어픽스 없음) → 매직 파랑(1~2) → 레어 노랑(3~4) → 유니크 주황 → 세트 초록 → Exalted 보라(고티어 어픽스) → 전설 빨강(유니크+Exalted). 7등급 | upcomer.com Last Epoch rarity 글 |
| Grim Dawn / Titan Quest | 흰색 → 노랑 → 초록 → 파랑(Epic) → 보라(Legendary). 디아블로식과 달라서 "며칠 적응했다", "한눈에 읽기 느리다"는 불만이 있고 색 선택 옵션 요청이 있음 | Crate 포럼 "Why DID Grim Dawn change the usual ARPG item colors", grimdawn.com 가이드 |
| Torchlight Infinite | 일반 회색 → 매직 파랑 → 레어 보라 → 슈퍼레어 분홍, 전설은 주황 빛기둥 | vhpg.com 가이드, taptap 개발 블로그 #6 |
| Borderlands (Wonderlands) | 흰색 → 초록 → 파랑 → 보라 → 주황, 위에 무지개/펄레센트. 이름, 제조사, 무기 종류 아이콘 글자가 등급색 | dotesports.com |

### 1-2. 관찰

1. 서양 ARPG는 두 계열로 갈린다. 디아블로 계열(흰 → 파랑 → 노랑 → 주황/금)과 WoW 계열(흰 → 초록 → 파랑 → 보라 → 주황). WoW 계열이 모바일/방치형/루터 슈터(Borderlands, Wonderlands)에 훨씬 널리 퍼져 있다. 방치형 신규 유저는 WoW 계열을 더 많이 봤을 확률이 높다. (판단: 관찰에서 나온 추정)
2. 유일하게 모든 게임에서 일관된 것: 흰/회색이 맨 아래, 주황이 위쪽 끝, 보라가 주황 바로 아래(WoW 계열)라는 점. 파랑 > 초록 순서도 WoW 계열에서는 일정하다.
3. 색만 쓰면 문제가 생긴다. D2R에서는 유니크 색이 안 보인다는 요청이 있고, 색맹 유저는 파랑과 보라(또는 파랑 매직과 노랑 레어)를 구분하지 못해 툴팁의 "Rare" 글자를 읽어야 한다고 말한다. 접근성 글은 색마다 모양, 글자, 크기 같은 두 번째 채널을 요구한다. WoW 색맹 모드는 툴팁에 'Rare', 'Epic' 글자를 추가하는 방식이다. (출처: forum.lastepoch.com/t/colorblind-difficulties, Crate 포럼 Colour Blindness, pcgamer.com WoW colorblind, abratabia.com visual-accessibility)
4. Last Epoch에서 루트 필터로 색을 바꾸면 바닥에서만 바뀌고 인벤토리/상자에서는 안 바뀐다는 불만이 있다. 색 규칙은 바닥, 인벤토리, 툴팁, 상점에서 같아야 한다. (Steam 토론, 검색 결과 요약)

### 1-3. 게임은 등급 순서를 어떻게 "가르치나"

솔직히 말하면, 조사한 범위에서 "인벤토리 안의 등급 범례"를 제공한다는 근거는 하나도 못 찾았다. 확인된 학습 수단은 이것들이다.

- 이름 글자색과 아이템 배경/테두리색 (Diablo 4는 배경색)
- 드롭 연출: 전설은 주황 빛기둥과 "쾅" 소리 (Torchlight Infinite 개발 블로그), Borderlands는 무지개 무기가 더 긴 빛기둥을 낸다 (BL2 가이드)
- 루트 필터로 사운드, 빛기둥, 미니맵 아이콘 (PoE NeverSink 필터, FilterBlade)
- 위키와 가이드 글 (즉 게임 밖에서 배움). 이것이 신규 유저에게는 가장 큰 구멍이다.
- 색맹 모드에서 툴팁에 등급 글자 추가 (WoW)

결론: 대부분 게임은 "이미 장르를 아는 사람"에게 의존한다. 우리 게임은 그 가정을 못 쓰므로, 범례와 첫 드롭 교육을 직접 넣는 것이 차별점이 된다.

## 2. 아이템 비교

### 2-1. 확인된 동작

- Diablo 4
  - 기본값: 같은 칸에 장착한 아이템보다 아이템 파워가 높은지 낮은지만 초록/빨강 화살표로 알려준다. 포럼 유저들은 이 화살표가 어픽스와 빌드 적합도를 무시하므로 "힌트일 뿐 판정이 아니다"라고 말한다. 다만 대량 파밍에서 먼저 볼 만한 것을 거르는 데는 유용하다는 옹호도 있다. (us.forums.blizzard.com/en/d4/t/greenred-arrows-on-items-ruin-decision-making/14189)
  - 옵션 Advanced Tooltip Compare (설정 > Gameplay, 기본 꺼짐): 장착 아이템과 나란히 놓고 얻는 효과는 초록, 잃는 효과는 빨강으로 보여준다. 미장착 아이템에서 Shift(콘솔은 버튼)를 누르면 비교가 뜬다. 하단에 잃게 되는 속성이 나온다. 반지와 목걸이는 초록/빨강 숫자가 안 나온다는 불만이 있다. (denofgeek.com, earlygame.com)
  - 포럼 제안: 초보자용으로는 켜두고 선택 옵션으로 끌 수 있게 하자는 의견이 나왔다.
- Diablo 3: 인벤토리 창에서 같은 슬롯 장착품과 비교하며, 초록은 증가, 빨강은 감소 (PoE 포럼과 Blizzard 포럼 요약). 비교가 "한 방향"이라 양손 무기에서 어색하다는 불만이 있다. 캐릭터 카드 하단에 공격, 방어, 회복 요약 수치를 보여주는 구조로 기억하지만 이번에 1차 출처로 확인하지 못했다. (일반 지식, 미확인) 한 가이드는 캐릭터 카드의 평균 데미지만 보면 속아서, 원소 증뎀 같은 항목은 별도로 봐야 한다고 경고한다. (gamepressure.com D3 가이드)
- Last Epoch: Ctrl이 장착품과 비교, Alt가 모디파이어 설명, Alt+Ctrl이 제작/롤 정보. 두 번째 반지 같은 대체 슬롯은 Ctrl 후 Alt. (커뮤니티 가이드 요약, 현재 버전 확인 필요) 롤이 티어 범위 안 어디쯤인지 알려달라는 요청이 있어 아직 직관적이지 않다. (forum.lastepoch.com 제안 글들)
- Path of Exile 1: 미장착 아이템 위에서 Alt(또는 픽업 키)를 누르면 장착품이 옆에 나란히 뜬다. Alt를 누르면 추가 정보가 많아져 오히려 모드를 보기 어렵다는 반응이 있다. (pathofexile.com/forum/view-thread/1217624, 오래된 글)
- Diablo Immortal: 인벤토리에서 "초록 위 화살표" 장비로 갈아입는 것이 가이드의 기본 조언이고, 화살표는 대략적인 지표일 뿐 플레이 스타일을 반영하지 않는다고 적혀 있다. 총괄 점수는 Combat Rating(전투력). (pocketgamer.com, hellhades.com 요약)
- WoW 애드온 Upgrade Arrow: 가방 아이템 중 착용 아이템보다 좋은 것에 초록 화살표를 달고 한 번에 장착하게 해준다. 스펙/목표에 따라 가중치를 둔다고 설명한다. (curseforge 설명) 이 점이 중요하다. 원시 수치 합이 아니라 가중치가 있어야 한다.
- 방치형 소규모: Emoji Battle Clicker 개발 로그는 인벤토리 아이템이 장착품보다 강하면 초록 업그레이드 화살표를 달고, 상점/대장간/상세 화면에도 같은 화살표를 쓰고, 스탯 차이를 초록/빨강으로 칠한다고 적었다. 같은 검색에서 itch.io 개발자 한 명은 자동 장착이 "원시 스탯만 최대화"해서 제거했다고 말했다. Lootfiend는 패시브 때문에 낮은 티어 장비가 더 나은 경우가 있다고 설명한다. (itch.io 포스트들, bluestacks 가이드)
- Brighter Shores의 빨강/초록 화살표 페이지는 403이라 읽지 못했다. (미확인)

### 2-2. 핵심 문제와 해법

| 문제 | 증거 | 우리 해법 |
|---|---|---|
| 화살표가 "아이템 레벨"만 반영하면 어픽스가 중요해지는 중반 이후 오해를 부른다 | D4 포럼 | 화살표를 "내 영웅의 실제 계산된 전투 수치 변화"로 만든다 (아래 제안 1) |
| 비교가 옵션 뒤에 숨어 있고 기본 꺼짐 | D4 Advanced Tooltip Compare | 기본 켜짐. 아이템을 누르면 곧바로 장착품과 나란히 |
| 반지/목걸이처럼 슬롯이 둘인 칸은 비교가 비어 있음 | D4 earlygame, LE Ctrl+Alt | 두 칸 모두 보여주고 "더 약한 쪽"과 비교 |
| 단일 숫자만 보면 공격 올라가고 방어 내려가는 교환형을 못 읽음 | D3 한 방향 비교 불만 | 점수를 공격/방어 두 개로 쪼개고 "교환" 표시 |
| 자동 장착이 원시 합계만 보면 틀림 | itch.io 코멘트, Lootfiend | 자동 장착은 같은 점수식을 쓰고, 왜 골랐는지 한 줄로 알려준다 |

## 3. 스탯 문구와 그룹화

확인된 것:
- PoE2와 Last Epoch는 키 누르기로 용어 설명을 연다. PoE2는 Alt를 누르면 밑줄 친 키워드가 나오고 계속 누른 채 키워드에 올리면 상세 설명이 뜬다. 2단 중첩 툴팁이 있고, 3단은 패치 후 안 나온다는 버그 보고가 있다. 설명이 없는 아이템도 있다. D4 포럼에서 PoE2의 이 방식을 "잘 만들었다"고 칭찬하고, D4는 키워드 중심으로 설계되지 않았다고 답한다. (steamcommunity PoE2 토론, pathofexile.com/forum/view-thread/3831203, us.forums.blizzard.com/en/d4/t/suggestion-as-a-player-i-want-to-be-able-to-view-info-on-a-keyword...)
- Last Epoch Alt: 모디파이어 설명 + 티어/롤 범위. 어픽스는 T1~T7 티어이고 T6~T7은 크래프팅으로 못 만든다. 초보자에게 "T7이 더 높다"는 규칙은 설명이 있어야 이해된다. (maxroll, 커뮤니티 가이드)
- 그룹화(공격/방어/유틸)와 "내 영웅의 실효 수치 표시"는 이번 조사에서 1차 근거를 못 찾았다. D3가 캐릭터 카드로 요약 수치를 보여준 것을 일반 지식으로만 알고 있다. 아래 제안은 근거가 아닌 설계 판단이다.
- 한 가지 일반 원칙: 수치 퍼센트를 문장으로 풀면 신규 유저가 읽는다 ("치명타 확률 +5%" → "치명타가 5% 더 자주 터짐"). 이것도 검색 근거가 아니라 설계 판단이다.

## 4. 인벤토리 정리

확인된 것:
- D4: 대장간에서 등급별 일괄 분해와 "잡템 분해"가 있다. Season 4에서 "모두 분해"가 즐겨찾기 아닌 모든 것을 분해하도록 바뀌어 전설/유니크까지 날려 유저들이 놀랐다 (dexerto, charlieintel). 루트 필터는 확장팩에서 추가 예정이지만 자동 분해는 필터에 포함되지 않는다고 보도됐다 (icy-veins). 시즌 2에서 같은 전설 위상을 공유하는 아이템 묶음 정렬이 들어갔다 (gamespot). "새 아이템" 표시는 이번에 근거를 못 찾았다. 교훈: 대량 삭제 버튼은 보호 규칙(즐겨찾기, 현재 비교에서 이득인 것)이 먼저 있어야 한다.
- PoE: NeverSink 같은 필터가 엄격도 단계(PoE2는 7단계)로 숨김 범위를 정하고, 사운드, 빛기둥, 미니맵 아이콘으로 강조한다. 진행하면 필터 엄격도를 올리라고 안내한다. (pathofexile.com 포럼, maxroll, exitlag 가이드) 교훈: 엄격도 슬라이더 하나로 시작하고, 고급 설정은 숨긴다.
- Last Epoch: 필터에서 아이템 종류, 레벨, 어픽스, 등급을 조건으로 쓰고 색, 사운드를 바꾼다. 전용 필터 UI가 있다 (Shift+F). (icy-veins, gameskinny)
- Diablo Immortal: 자동 줍기에서 등급 필터. 모든 드롭이 분해 가능하고 재료로 환원된다. (pocketgamer 등 가이드)

## 5. 게임별 해법 비교표

(약어: ○ 확인됨, △ 일부/불확실, ✕ 근거 못 찾음)

| 게임 | 등급 색 체계 | 등급 학습 수단 | 비교 방식 | 단일 점수 | 키워드 설명 | 정리/필터 |
|---|---|---|---|---|---|---|
| WoW | 회색-흰-초록-파랑-보라-주황 (○ hex) | 이름색, 색맹 모드는 등급 글자 추가 | 툴팁 비교는 기본, 화살표는 애드온 (Upgrade Arrow) | 아이템 레벨 | ✕ | 가방 정렬, 판매 |
| Diablo 2 | 회-흰-파랑-노랑-초록-금 | 이름색 | 장착품과 나란히 (△) | 없음 | ✕ | 없음, 서드파티 필터 |
| Diablo 3 | 위와 비슷, 유니크는 주황 | 이름색, 주황 빛기둥 (일반 지식) | 인벤토리에서 초록/빨강 델타 (○) | 캐릭터 카드 요약 (△) | ✕ | 분해 |
| Diablo 4 | 흰-파랑-노랑-주황-금 (○) | 배경색 | 기본은 아이템 파워 화살표, 옵션으로 Shift 비교 (○) | 아이템 파워 | ✕ (포럼 요청) | 등급별 일괄 분해, 필터 예정 (○) |
| PoE 1/2 | 흰-파랑-노랑-주황 | 이름색, 필터 사운드/빛기둥 | Alt/픽업 키로 나란히 (△) | 없음 (DPS 표시는 미확인) | ○ Alt 밑줄 키워드 (PoE2) | 필터 엄격도 단계 (○) |
| Last Epoch | 7등급 흰-파랑-노랑-주황-초록-보라-빨강 (○) | 이름색 | Ctrl 비교, Alt 설명, Alt+Ctrl 롤 (△) | 없음, 티어 T1-T7 | ○ Alt 모디파이어 설명 | 필터 UI Shift+F (○) |
| Grim Dawn/TQ | 흰-노랑-초록-파랑-보라 | 이름색, 디아블로와 달라 혼란 (○) | 나란히 | 없음 | ✕ | 서드파티 Rainbow Filter |
| Torchlight Infinite | 회-파랑-보라-분홍, 전설 주황 | 빛기둥 + 소리 (○) | 비교 화살표는 미확인 | 전투력 (일반 지식) | ✕ | ✕ |
| Borderlands | 흰-초록-파랑-보라-주황 (+무지개) | 빛기둥, 글자색 | 카드에 화살표 (△, 한 가이드는 초록 화살표가 항상 더 좋은 건 아니라고 경고) | DPS 표시 | ✕ | ✕ |
| Diablo Immortal | 흰-파랑-노랑-주황 (일반 지식) | 이름색 | 인벤토리 초록 위 화살표 (○) | Combat Rating (○) | ✕ | 자동 줍기 등급 필터, 분해 (○) |
| Emoji Battle Clicker (방치형) | - | - | 초록 화살표 + 초록/빨강 델타, 상점/대장간 동일 (○) | ✕ | ✕ | 자동 장착 요청 (△) |
| Nomad Idle, Melvor Idle, Idle Champions, Tap Titans, Soul Knight, Grim Quest, Loot Hero, Idle Slayer | 근거 못 찾음 | | Melvor는 "View Item Stats"로 장착품과 비교한다는 유저 글 한 건 (△) | | | |

## 6. 우리 게임에 바로 적용할 것 (우선순위 순)

전제: 우리는 오토 전투라 유저가 하는 일이 "무엇을 입을지, 무엇을 버릴지"로 거의 한정된다. 그래서 이 화면이 신규 유저 이탈의 주범이다.

1. 모든 비교를 하나의 "전투력 변화 %"로 요약한다 (최우선)
   - 아이템 아이콘 모서리와 툴팁 맨 위에 `▲ +12%`, `▼ -5%`, `≈ 0%`를 표시한다.
   - 계산: 새 장비를 입었을 때 영웅의 실제 계산값을 쓴다. `점수 = 초당 피해(DPS) × 생존력(유효 체력)`의 변화율. 단순히 스탯 합이 아니라 스킬 링 구성, 현재 룬을 반영한다. (WoW Upgrade Arrow가 가중치를 쓰는 이유, D4 화살표가 아이템 파워만 보다 욕먹는 이유와 같다.)
   - 공격과 방어를 따로 한 줄씩도 보여준다: `공격 +18%  방어 -4%`.

2. 화살표 규칙을 고정하고 문서화한다
   - 총점 ≥ +1%: 초록 ▲. ≤ -1%: 빨강 ▼. 그 사이: 회색 ═.
   - 공격 ▲ 방어 ▼ (또는 반대)이고 총점이 ±1% 이내: 노랑 ◆ "교환"을 붙인다.
   - 색만 쓰지 않는다. 방향(▲▼)과 부호(+/-)를 함께 쓴다 (색맹 대응).
   - 인벤토리 칸, 장착 슬롯, 상점, 보상 팝업, 분해 확인창 모두에서 같은 아이콘을 쓴다 (Emoji Battle Clicker처럼 모든 화면에 동일 규칙).

3. 비교 툴팁은 기본 켜짐, 레이아웃은 고정
   - 모바일/마우스 공통: 아이템을 누르면 왼쪽에 "착용 중", 오른쪽에 "새 아이템" 카드를 나란히 띄운다. D4의 "옵션 뒤에 숨겨서 기본 꺼짐"을 반면교사로 삼는다.
   - 반지처럼 슬롯이 둘이면 더 약한 쪽과 비교한다 (D4가 반지/목걸이를 비워서 욕먹음). 어느 쪽과 비교하는지 "↔ 반지 2와 비교" 한 줄을 적는다.
   - 카드 순서: ① 이름(등급색) + 등급 글자 ② 전투력 변화 큰 글씨 ③ 스탯 델타 목록 ④ 특수 효과 ⑤ 판매/분해 가치.

4. 등급은 6단계, 색 + 글자 + 모양을 함께 쓴다. 추천 hex (어두운 배경 위에서 읽히게 WoW 계열보다 파랑/보라를 밝게):

   | 단계 | 이름 | 이름/테두리 hex | 모양 보조 |
   |---|---|---|---|
   | 1 | 일반 | `#C9CDD3` (회백) | 테두리 1px, 점 1개 |
   | 2 | 고급 | `#4CCB5A` (초록) | 점 2개 |
   | 3 | 희귀 | `#4C9BFF` (파랑, WoW #0070dd보다 밝게) | 점 3개 |
   | 4 | 영웅 | `#B567F0` (보라) | 점 4개 + 모서리 장식 |
   | 5 | 전설 | `#FF9A2E` (주황) | 점 5개 + 2px 이중 테두리 + 반짝임 |
   | 6 | 신화 | `#FF4D4D` (빨강, Last Epoch 최상위가 빨강) | 점 6개 + 이중 테두리 + 천천히 맥동 |

   - 근거: 흰 → 초록 → 파랑 → 보라 → 주황은 WoW, Borderlands, Wonderlands가 공유하는 순서라 가장 많은 사람이 이미 안다. 디아블로 순서(파랑/노랑)를 따르려면 노랑을 4단계에 두면 되지만, 유저층이 방치형 모바일이면 WoW 순서를 권한다. (이 선택은 판단이다. 어느 쪽이든 하나로 통일하는 것이 핵심.)
   - 이름 옆에 항상 등급 글자 `[희귀]`를 적는다 (WoW 색맹 모드가 하는 것, 파랑/보라 혼동 대응). 점 개수는 색을 못 보는 사람을 위한 두 번째 채널이다.
   - 같은 색 규칙을 바닥 드롭, 인벤토리, 툴팁, 분해창에 똑같이 쓴다 (Last Epoch 필터 색이 바닥에만 적용돼서 욕먹은 문제 방지).

5. 첫 드롭마다 등급 교육
   - 각 등급이 처음 떨어졌을 때 한 번만 토스트: `희귀 장비를 얻었다. 희귀 > 고급 > 일반. 옵션이 3개까지 붙는다.`
   - 인벤토리 상단에 `?` 버튼으로 6단계 범례 팝업 (색, 점, 옵션 개수, 상위 등급이 항상 좋다는 게 아니라 "옵션 수가 많다"는 뜻이라고 적음). 조사한 게임 중 범례를 제공하는 곳은 못 찾았으므로 이것이 쉬운 차별점이다.

6. 드롭 연출로 등급을 "소리와 빛"으로도 가르친다
   - 4단계 이상: 짧은 빛기둥 + 전용 효과음 (Torchlight Infinite 전설 주황 빛기둥 + "쾅"). 1~3단계: 기둥 없음, 소리도 작게.
   - 빛기둥 색은 위 표의 hex를 그대로 쓴다.

7. 스탯 문구는 "한 줄 한국어 문장 + 숫자", 그룹으로 묶는다
   - 그룹 3개: 공격 (피해, 치명타, 공속), 방어 (체력, 방어, 저항), 유틸 (이동, 드롭, 쿨감). 그룹 아이콘을 앞에 둔다.
   - 예: `⚔ 치명타 확률 +5%` 대신 `⚔ 치명타가 5% 더 자주 터진다`는 길어서 픽셀 UI에서 어려우니, 짧은 이름 + 롱프레스 설명 (8번)으로 나눈다.
   - 델타는 줄 끝에 `(+3)` 초록 / `(-2)` 빨강으로 붙이고, 변화 없는 줄은 흐리게 한다.

8. 키워드 용어집 (스킬 설명 문제도 같이 해결)
   - 모든 설명 문장에서 키워드(`화상`, `연쇄`, `공명`, `룬`, `윤회 증강`)는 점선 밑줄. PC는 Alt 누른 채 올리기, 모바일은 길게 누르기로 한 단계 아래 설명창을 띄운다 (PoE2의 Alt 밑줄 키워드 방식). 중첩은 2단계까지만 허용한다 (PoE2도 3단이 깨진 사례).
   - 설명이 비어 있는 키워드가 없도록 빌드 검증을 붙인다 (PoE2에서 설명 누락 불만).
   - 스킬 링 슬롯에도 같은 방식: 한 줄 요약(`가장 가까운 적에게 불꽃 3발`) + 눌러서 수치(피해, 쿨다운, 속성).

9. 자동 장착 토글 ("추천 장비 자동 착용")
   - 기본 켜짐. 1번의 점수식으로 +3% 이상일 때만 교체하고, 교체하면 로그/토스트에 한 줄: `검 교체 +12% (공격 ▲)`. 되돌리기 버튼 포함.
   - 근거: 단순 스탯 최대화 자동 장착이 빌드를 망친다는 개발자 코멘트가 있다. 그러니 점수식에 스킬 링/룬 가중치를 반드시 넣고, 잠금(즐겨찾기) 장비는 건드리지 않는다.

10. "새 아이템" 표시와 정렬
   - 새로 얻은 아이템 칸에 NEW 점. 한 번 열어보면 사라짐. 업그레이드 후보(▲)가 있는 칸은 NEW와 별개로 화살표가 먼저 보이게 한다.
   - 기본 정렬: ① 업그레이드(▲) ② 등급 내림차순 ③ 슬롯 종류. 정렬 버튼은 3개 이하.
   - (근거: D4가 정렬 개선을 시즌 업데이트로 넣은 것, "NEW 마커" 자체는 조사에서 확인 못 함. 설계 판단.)

11. 자동 분해는 등급 슬라이더 하나로
   - `자동 분해: 일반까지 / 고급까지 / 희귀까지 / 끔`. 슬라이더 하나가 PoE의 엄격도 단계처럼 "처음은 단순, 나중에 올리라"는 안내를 대신한다. 진행도에 따라 "이제 고급까지 분해해 볼까요?" 제안 토스트.
   - 단, ▲(업그레이드) 후보 아이템과 즐겨찾기는 자동 분해에서 항상 제외 (D4 Season 4 "모두 분해" 사고 방지).
   - 일괄 분해 확인창에는 4단계 이상이 섞이면 빨간 경고와 개수를 함께 보인다.

12. 한 번에 한 번만, 점진적 공개
   - 처음 10분은 등급 3단계(일반/고급/희귀)만 노출하고 점수 한 줄만 보인다. 델타 목록과 용어집은 `자세히` 버튼 뒤에 둔다. D4 포럼에서 "스탯이 복잡해지는 건 레벨 30 즈음"이라는 말이 있듯, 복잡도는 단계적으로.

13. 판정이 틀릴 수 있다는 것을 숨기지 않는다
   - 화살표 옆에 작은 `?`: "현재 스킬 링과 룬 기준 예상 전투력입니다. 스킬 구성을 바꾸면 달라질 수 있어요." (D4와 Diablo Immortal에서 "화살표는 대략적 지표"라는 불만을 선제 대응)
   - 스킬 링 순서를 바꿔서 점수가 크게 달라지는 장비(특수 효과가 특정 스킬 전용)에는 `🔗 스킬 전용` 배지.

14. 첫 5분 튜토리얼 시나리오 (구현 단위로 쪼갬)
   - ① 첫 장비 드롭 → 인벤토리 강조 → "▲ 초록 화살표 = 지금 입은 것보다 강함" 한 번 → 탭하면 자동 장착.
   - ② 첫 ▼ 아이템 → "빨강 = 약함. 분해하면 재료가 됩니다".
   - ③ 첫 ◆ 교환형 아이템 → "공격은 늘고 방어는 줄어요".
   - 각 단계는 해당 상황이 처음 발생할 때만 뜬다.

15. 측정
   - 로그: 아이템을 열어보고 → 장착/분해까지 걸린 시간, ▲ 아이템을 안 입고 분해한 비율, 점수 ▼인데 장착한 비율. 이 지표로 점수식과 임계값 ±1%/+3%를 튜닝한다.

## 7. 구현 메모 (Unity)

- 전투력 계산은 UI 전용 함수 `PreviewPower(equipSlot, candidate)` 하나로 만들고, 자동 장착, 화살표, 분해 보호가 모두 이 함수를 호출하게 한다. 따로 계산하면 화면마다 서로 다른 화살표가 나온다.
- 등급 색은 ScriptableObject 하나에 hex와 점 개수, 글자 이름을 둬서 바닥 드롭, 슬롯 테두리, 툴팁, 분해창이 같은 에셋을 참조하게 한다. 1080p x2 픽셀 UI에서 점 개수는 1~2px 단위로 읽히는지 빌드로 확인할 것.
- 화살표 기호 ▲▼는 픽셀 폰트에 없을 수 있으므로 6x6 스프라이트로 따로 만든다.

## 출처

- WoW 등급 hex: https://warcraft.wiki.gg/wiki/Quality
- D4 비교 옵션: https://www.denofgeek.com/games/diablo-4-best-hidden-setting-compare-gear , https://earlygame.com/general-news/diablo-4-gear-how-to-always-be-best-equipped
- D4 화살표 논쟁: https://us.forums.blizzard.com/en/d4/t/greenred-arrows-on-items-ruin-decision-making/14189
- D4 등급: https://charlieintel.com/diablo/diablo-4-rarity-system-all-rarity-colors-explained-256002
- D4 키워드 요청: https://us.forums.blizzard.com/en/d4/t/suggestion-as-a-player-i-want-to-be-able-to-view-info-on-a-keyword-eg-blood-orb-when-mousing-over-a-piece-of-gear/217847
- D4 자동 분해 / 일괄 분해: https://dexerto.com/diablo/diablo-4-salvage-all-items-change-loot-reborn-2725179 , https://charlieintel.com/diablo/simple-diablo-4-mistake-could-cost-all-your-uniques-legendaries-in-season-4-324334 , https://www.icy-veins.com/d4/news/no-auto-salvage-in-diablo-4-loot-filters-should-players-be-worried/ , https://pcgamer.com/diablo-4-salvage
- D3 비교 가이드: https://www.gamepressure.com/diabloiiireaperofsouls/comparing-of-items/zd62ad
- D1/D2/D3 색 논의: https://us.forums.blizzard.com/en/d4/t/rarity-levels-of-each-diablo-listed/73963 , https://classic.battle.net/diablo2exp/items , https://www.gameinformer.com/2019/05/18/the-surprising-origins-of-loot-rarity-colors
- PoE 등급/비교/필터: https://gamerjournalist.com/path-of-exile-poe-rarity-colors-explained/ , https://www.poe2wiki.net/wiki/Item_rarity (봇 차단으로 본문 못 읽음, 검색 요약 사용) , https://www.pathofexile.com/forum/view-thread/1217624 , https://www.pathofexile.com/forum/view-thread/3693141 , https://maxroll.gg/poe2/news/loot-filter-support-for-path-of-exile-2
- PoE2 Alt 키워드: https://webcdn.pathofexile.com/forum/view-thread/3831203 , https://www.pathofexile.com/forum/view-thread/3595606
- Last Epoch: https://upcomer.com/last-epoch-loot-rarity-colors-explained , https://forum.lastepoch.com/t/community-game-guide/26057/11 , https://maxroll.gg/last-epoch/resources/gear-walkthrough , https://forum.lastepoch.com/t/suggestion-show-affixes-roll-percentage/70911 , https://forum.lastepoch.com/t/colorblind-accessibility-and-loot-filters/68096
- Grim Dawn: https://forums.crateentertainment.com/t/why-did-grim-dawn-change-the-usual-arpg-item-colors/157478 , https://www.grimdawn.com/guide/items/the-hunt-for-loot
- Torchlight Infinite: https://www.taptap.io/post/1105109 , https://www.vhpg.com/item-rarity-torchlight-infinite/
- Borderlands: https://dotesports.com/general/news/all-weapon-rarities-in-tiny-tinas-wonderlands-explained , https://mobalytics.gg/borderlands-4/guides/epics-vs-legendaries
- Diablo Immortal: https://www.pocketgamer.com/diablo-immortal/beginners-guide , https://hellhades.com/diablo-immortal-dont-make-this-gearing-mistake/ , https://www.gamesradar.com/diablo-immortal-combat-rating-check-raise
- 방치형 / 화살표 / 자동 장착: https://caprog.itch.io/emoji-battle-clicker/devlog/1029688/inventory-ui-improvements-equipment-quality-of-life-improvements (직접 열람은 404, 검색 요약), https://www.curseforge.com/wow/addons/upgrade-arrow , https://www.bluestacks.com/blog/game-guides/lootfiendidle-dungeons/lfid-equipments-guide-en.html
- Melvor Idle 비교 유저 글: https://steamcommunity.com/app/1267910/discussions/0/3552805589776962778
- 접근성: https://www.abratabia.com/game-accessibility/visual-accessibility.php , https://www.pcgamer.com/au/colorblind-support-is-coming-to-world-of-warcraft , https://forum.lastepoch.com/t/colorblind-difficulties/38732 , https://forums.crateentertainment.com/t/colour-blindness/40998
