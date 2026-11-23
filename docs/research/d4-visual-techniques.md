# 디아블로 IV(D4)의 룩·가독성 기법과 등불지기(2D URP 픽셀)로의 번역

작성 목적: 공개 자료로 D4가 "어둡지만 읽히는" 화면을 어떻게 만드는지 확인하고, 그것을 Unity 6 URP 2D 픽셀 게임에서 할 수 있는 구체 작업으로 바꾼다.
앞선 문서 `act-visual-direction.md`(막별 팔레트·라이트 수치)와 `arpg-ui-ux-patterns.md`(UI 15개 개선안)와 겹치는 내용은 반복하지 않고, 겹치면 "→ 기존 문서" 로만 표시한다.

근거 표기
- **[확인]** : 아래 URL을 직접 열어 내용을 확인한 것(요약은 내 말로 다시 씀).
- **[검색요약]** : 검색 결과 요약만 보았고 원문을 열지 못한 것(원문이 403 등으로 막힘). 신뢰도 중간.
- **[미확인]** : 출처가 서로 어긋나거나 원문을 못 봐서 단정하지 않는 것.
- **[제안]** : 출처가 아니라 내가 원칙에서 만든 우리 게임용 제안값. 플레이 테스트로 검증해야 한다.
게임 파일은 내려받거나 열어보지 않았다. 직접 인용은 쓰지 않고 요약만 했다.

조사 한계(먼저 밝혀 둠)
- ArtStation Magazine "Art Blast"(환경·UI), GDC 2023/2024 D4 세션 페이지, 일부 Icy Veins 기사는 403이라 원문을 못 봤다. GDC 세션은 검색 결과 요약으로만 확인했다.
- IGN 인터뷰(전투 비주얼 "정보 대 몰입")의 본문은 열지 못했다. 같은 인터뷰를 인용한 2차 기사만 확인했다.
- 몬스터 림 라이트, 외곽선 두께, 채도 수치처럼 "D4가 실제로 어떤 값을 쓰는지"는 공개 자료에서 찾지 못했다. 그 부분은 전부 [제안]이다.
- 보스 텔레그래프 설계 철학을 Blizzard가 직접 설명한 글은 찾지 못했다(Blizzard 글은 "읽혀야 한다"까지만 말함).

---

## 1. 색과 조명 철학

### 1.1 두 기둥: "어둠으로의 복귀"와 "올드 마스터"
- Blizzard는 환경 아트의 기둥을 둘로 둔다. 하나는 "어둠으로의 복귀"(위험하고 어두운 중세 고딕 세계, 던전·조명·소품 전체에 적용), 다른 하나는 고전 회화(특히 렘브란트)의 절제된 디테일, 명암 폭, 색 선택을 기준으로 삼는 "올드 마스터"다. [확인] https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022
- 아트 디렉터 인터뷰도 같은 맥락: 루브르의 르네상스·중세 회화에서 영감을 얻었고, 희망의 빛이 멀리 한 점 있고 절망이 둘러싼 구도를 참고작 하나로 언급한다. [확인] https://www.pcgamesn.com/diablo-4/artwork-interview-john-mueller
- 팔레트는 "현실에 붙은, 거친" 쪽. 모든 지역이 어두운 건 아니다(밝은 구역도 있음). [확인] 위 두 링크 + https://techradar.com/news/diablo-4-looks-gritty-and-dark-so-dark-youll-need-to-squint-to-see-anything
- **번역**: 우리 `act-visual-direction.md`의 "전역광은 중립, 색은 국소 광원과 소품에서"와 방향이 같다. 새로 얻는 점은 "렘브란트식 = 어둠 속 한 점의 빛 + 넓은 낮은 명도 구간"이라는 구도 규칙이다. 화면의 80%를 어둡게 두고, 시선이 갈 곳(플레이어 랜턴, 목표, 위협)에만 빛을 모은다.

### 1.2 지역별 팔레트: 지역마다 "한 가지 색 이야기"
공식 환경 업데이트가 든 예 [확인] https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022
- 건조한 마을: 따뜻하고 산화된 색조(바싹 마른 느낌).
- 수도원 지역: 어두운 바위에 옅은 금발색·녹슨 색 풀, 거기에 푸른 알칼리 호수와 지열 웅덩이를 "색의 터짐"으로 사용.
- 요새 도시 Kyovashad: 따뜻한 흙색, 이웃한 얼어붙은 산악 지대는 더 차갑게 대비.
- 침수 던전: 썩은 듯한 녹색·노랑으로 습하고 무거운 느낌.
- **번역**: 지역 팔레트는 "넓은 면은 저채도 한 계열 + 작은 면에 보색 터짐 1개" 구조다. 우리 막 I(사암+청록 몬스터), 막 IV(청회색+따뜻한 몬스터)가 이미 이 구조. 추가: 각 막에 "색의 터짐 1곳"(예: 막 I 청록 오아시스 웅덩이, 막 V 식은 슬래그의 청강색 균열)을 지도에 몇 군데 배치해 막의 기억점으로 만든다. [제안]

### 1.3 조명이 분위기를 만드는 방식
- 조명은 물리 기반. 부드러운 빛(안개, 부드러운 그림자, 반사광)이 미감의 핵심이다. [확인] 환경 업데이트 링크
- 던전은 "선택적 조명"으로 갈 길을 암시한다. 요새 던전은 대부분 어둡고, 침수 던전은 더 확산된 빛. [확인] 환경 업데이트 링크
- 대비가 임팩트를 만든다: 축축하고 차가운 야외 옆에 따뜻하고 흐릿한 선술집을 두는 식. [확인] 환경 업데이트 링크
- **번역**: 던전의 모든 곳을 똑같이 어둡게 하지 않는다. (a) 길·목표·출구 근처에만 "키 라이트"를 두고 나머지는 낮게, (b) 각 막에 따뜻한 "쉼터"(캠프)를 대비용 오아시스로 둔다. 등불지기 세계관(랜턴)과 정확히 맞는다. [제안]

### 1.4 대기: 볼류메트릭 안개와 먼지
- D4는 겹쳐 놓은 파티클 스프라이트로 먼지·연기를 흉내 내지 않고, 공기 중 빛의 산란을 시뮬레이션한다. 파티클도 조명을 받는다. 스테인드글라스 창은 주변 공기를 비춘다. [확인] https://news.blizzard.com/en-us/article/23964183/iv
- 공식 설명은 이 먼지·입자 표현이 어둡고 거친 톤의 핵심이라고 한다. [검색요약] 위 기사의 검색 요약
- 날씨도 중요: 비가 오면 표면이 젖고 바퀴 자국에 물이 고이며, 안개·냉기·바람 먼지가 지역 정체성을 만든다. [확인] 환경 업데이트 링크
- **번역(2D)**: 3D 볼류메트릭 대신, (1) 라이트 2D를 받는 안개 스프라이트 2~3겹(저알파, 천천히 스크롤)과 (2) Sprite Lit 머티리얼을 쓴 먼지 파티클을 쓰면 "랜턴 가까이에서만 밝아지는 먼지"가 된다. 막마다 파티클 세트를 다르게(모래 먼지, 포자, 눈가루, 불씨, 재, 별가루). → 6번 항목.

### 1.5 톤매핑과 색보정 (D4가 실제로 겪은 문제)
- 오픈 베타에서 색 변환 테이블의 로그 함수가 검정을 약 0.07 니트로 들어 올려, HDR 화면에서 "뿌연 안개"로 보였다. 정식 전에 수정. 이후 HDR 전용 톤매핑 곡선을 아트팀이 조정해 대비와 채도를 되찾았고, 광원의 주황 빛이 더 따뜻해지고 조명 받은 곳의 채도가 올랐다. [확인] https://news.blizzard.com/en-us/article/23964183/iv
- 같은 기사: SDR 출력은 톤매핑으로 그림자를 들어 올려 "눌린 검정"이 생기고, HDR은 던전의 깊은 그림자와 순수한 검정을 살린다. [확인]
- 플레이어 포럼에도 HDR에서 검정이 떠 어둠이 뿌옇다는 불만이 있다. [확인, 플레이어 의견] https://us.forums.blizzard.com/en/d4/t/feedback-on-d4s-graphics-ui-and-visual-presentation/13290
- **번역**: (1) 우리의 "검정이 뜨는" 위험 = Color Adjustments의 Post Exposure/Contrast, 비네팅 뒤의 검정 바닥, 블룸의 안개 낌. 검정(0,0,0)은 실제로 검정이어야 한다. (2) "광원의 코어를 포화·따뜻하게, 그림자는 중립"이 D4의 곡선 방향이다. 블룸 임계값을 높여(광원만 번지게) 바닥이 뿌옇게 번지지 않게. → 11번 항목.

### 1.6 D4가 스스로 인정한 어둠의 대가
- 어두움이 가독성을 해친다는 플레이어 불만(폭발·효과가 풍경과 뭉침, 합창하는 광역기가 바닥과 텔레그래프까지 덮음)이 계속 나온다. Blizzard는 Season 4에서 월드 속 캐릭터의 대비를 크게 높이는 옵션(강도와 색 조절 가능)을 넣었다고 알려졌으나 보도마다 표현이 달라 원문 확인이 필요하다. [미확인] https://www.icy-veins.com/d4/news/diablo-4-season-4-loot-reborn-patch-notes/ (검색 요약)
- **교훈**: 어두운 룩은 "옵션으로 읽기 쉽게 되돌릴 수 있게" 설계한다. → 7번 항목(접근성/대비 옵션).

---

## 2. 형태-배경 가독성 (몬스터·엘리트·보스·플레이어·전리품)

### 2.1 몬스터 디자인 규칙 (공식 설명)
출처: https://news.blizzard.com/en-us/article/23665024/diablo-iv-quarterly-update-june-2021 [확인]
- 외곽선(실루엣)이 먼저 읽혀야 한다. 아이소 카메라에서 빠르게 식별되도록.
- 색은 "묶여서" 보여야 하고, 게임 거리에서는 디테일이 가독성을 방해하면 안 된다.
- 큰 형태 → 2차 → 3차 디테일 순서. 가까이서만 보이는 디테일(고름·물집·하이라이트)은 게임 카메라에서 화면을 어지럽히지 않아야 한다.
- 예: 차가운 무채색 몸의 거미에 채도 높은 빨간 악센트 한 곳을 두어 눈에 띄게 한다.
- 디자인 목표가 보스 기믹을 이끈다(피 주교의 혈관 형태가 피 폭탄 장판 기믹을 만듦 등).
- 갑옷은 큰 형태 위주로 단순화해 가독성을 해치는 요소를 줄인다. [검색요약] https://gamespot.com/articles/diablo-4s-art-direction-emphasizes-hand-crafted-details-plenty-of-customization-and-just-enough-realism/1100-6493411/
- **번역**: 이 문서의 "무채색 몸 + 채도 높은 악센트 1~2곳" 규칙은 우리 `act-visual-direction.md`의 막별 악센트와 일치. 새로 추가할 것은 "디테일 3단 예산"을 PixelLab 프롬프트와 검수 체크리스트에 넣는 것. → 12번 항목.

### 2.2 색 코드를 일부러 뺀 선택과 그 부작용
- D4 경영진은 IGN 인터뷰에서 예전 시리즈의 "강한 몬스터 색 표시"를 줄이고 더 현실적으로 보이게 하겠다고 했고, 그 결과 엘리트를 한눈에 가려내기 어려워졌다는 보도가 있다. 엘리트는 이제 어픽스(이름표 아래 표시)로 구분한다. [확인, 2차 보도] https://mein-mmo.de/en/diablo-4-sacrifices-an-important-comfort-feature-to-be-more-realistic,905787
- 챔피언은 보라색 오라로 구분된다는 가이드가 있으나 공식 확인은 못 했다. [미확인] https://www.wepc.com/gaming/diablo-4-glowing-purple-enemies/
- 플레이어는 엘리트 이름 색 표시를 포럼에서 요청했다. [확인, 요청일 뿐] https://us.forums.blizzard.com/en/d4/t/can-we-get-elite-monster-names-in-color/211470
- **번역**: 우리는 "현실감"보다 "한눈에 읽기"가 우선이므로 이 선택을 따르지 않는다. 엘리트/보스는 (a) 크기(1.2~1.4배), (b) 발밑 링, (c) 이름표 색, (d) 몬스터 악센트의 강화로 구분한다. → 3번 항목.

### 2.3 "맞았을 때 빛나는" 효과(히트 글로우)의 논쟁
- D4에는 적이 맞으면 밝게 번쩍이는 효과가 있고, 일부 플레이어와 개발팀 일부가 "어두운 게임 톤에 비해 너무 밝고 방해된다"고 느껴 접근성 옵션에서 끌 수 있게 했다. 감독은 효과 자체는 마음에 든다고 말했다. [확인] https://mein-mmo.de/en/one-of-the-most-annoying-features-from-diablo-4-can-be-turned-off,651559
- **번역**: 히트 피드백은 필요하지만 "어두운 화면 위 밝은 번쩍임"은 눈 피로(우리가 이미 겪은 붉은 워시 문제와 같은 종류)가 된다. 밝기 상한과 시간 상한을 두고 끌 수 있게. → 4번 항목.

### 2.4 플레이어·NPC·적 강조 외곽선(접근성)
- D4는 플레이어, NPC, 적, 상호작용 오브젝트에 대해 외곽선 강조를 켜고 색을 직접 고르게 한다. [확인] https://news.blizzard.com/en-us/article/23954932/combatting-demons-with-accessibility-in-diablo-iv , https://www.gamepressure.com/diablo-iv/accessibility-features/ze109bb
- **번역**: "개체 종류별 외곽선 색 지정"은 단일 셰이더 + 설정값 몇 개로 만들 수 있다. 기본값은 약하게(어두운 룩 유지), 옵션에서 강하게. → 7번 항목.

### 2.5 아군(소환수)은 적보다 덜 밝아야 한다
- 오픈 베타 피드백 반영으로 네크로맨서의 해골 전사·마법사의 밝기를 줄여 덜 눈에 띄게 했다. [확인] https://news.blizzard.com/en-us/diablo4/23938289/diablo-iv-open-beta-retrospective-transforming-feedback-into-change
- 플레이어 피드백에는 순백 뼈와 밝은 파란 광채의 소환수, 지나치게 밝은 유령 적에 대한 불만도 있다. [확인, 플레이어 의견] 포럼 링크(1.5)
- **번역**: 시각 위계 = 위협(텔레그래프) > 적 > 전리품 > 플레이어 > 아군·환경 효과. 아군 이펙트는 알파·밝기를 낮춘다. → 5번 항목.

### 2.5-b 전리품 가시성
- D4는 내장 루트 필터를 도입했다(희귀도·아이템 파워 조건 등). 화면 어지러움을 줄이는 수단. [검색요약] https://www.icy-veins.com/d4/news/goodbye-screen-clutter-diablo-4-is-getting-a-loot-filter/
- 희귀도 색은 흰/파랑/노랑/주황(+고유·신화)이 일반적이나 출처별로 마법 아이템 색 서술이 다르다(보라 vs 파랑). [미확인] https://www.tweaktown.com/news/89882/these-classic-diablo-unique-items-will-return-in-iv/index.html
- 최근 패치에서 특정 아이템에 독립된 미니맵 아이콘과 고유 드롭 사운드를 줘 눈에 띄게 했다. [검색요약] https://www.icy-veins.com/d4/news/lord-of-hatreds-first-major-patch-targets-war-plans-talismans-and-horadric-cube/
- **번역**: 가시성 = 색 + 소리 + 미니맵 아이콘의 3중 채널. 방치형은 필터가 곧 UX. → 13번 항목.

---

## 3. 환경 다양성

### 3.1 모듈식 타일셋 + 소품 + 상호작용물 + 조명
- 150개 이상의 던전을 재사용 가능한 모듈형 타일셋에 소품·상호작용물·조명을 얹어 만든다. [확인] https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022
- "테마 레이어"를 켜고 끄면 같은 던전이 매장지가 되거나 빈 동굴이 되는 식으로 정체성이 바뀐다(드루이드 문화 키트 예). [확인] 같은 링크
- 타일셋 사이를 잇는 "전환 씬"으로 한 던전 안에서 서로 다른 타일셋을 연결하되 배치는 무작위. 건조한 요새와 침수 유적을 잇는 예. [확인] 같은 링크
- **번역**: 막별 키트(바닥·벽·소품 14~15개)는 이미 정해졌다(→ 기존 문서). 추가로 (a) 막마다 "변주 레이어" 2개(예: 막 I '봉인된 석실' / '붕괴된 터널'), (b) 막 경계 직전 방에 "전환 타일"(두 막의 바닥이 섞임)을 만들면 6막이 서로 다른 게임처럼 보이면서도 이어진다. → 10번 항목.

### 3.2 아이소 카메라라서 가능한 아트 결정
- 고정 카메라라서 전경 요소를 놓을 때 플레이 공간을 가리지 않게 배치할 수 있고, 가독성을 위해 디테일을 더하거나 뺀다. 실루엣과 깊이로 길찾기·가시성을 돕는다. [확인] 환경 업데이트 링크
- GDC 2024 "Diablo IV: The Art of Open World Sanctuary" 세션 설명은 아이소 카메라 개발의 고유한 도전, 성능, 넓은 월드의 일관성을 다룬다고 소개한다. [검색요약] https://schedule.gdconf.com/session/diablo-iv-the-art-of-open-world-sanctuary/899417
- **번역**: 우리 카메라도 고정이므로 "걷는 영역(바닥)은 저대비·저디테일, 가장자리와 벽은 중간 디테일, 그 바깥은 어둠"의 3겹으로 세트 드레싱 밀도를 나눈다. 걷는 영역 안의 소품은 최소화하고 가장자리에 모은다. → 10번 항목.

### 3.3 환경의 "이야기"
- 장소는 과거·최근 사건을 암시하고, 흔들리는 배·말리는 그물 같은 소품으로 살아 있는 느낌을 준다. [확인] 환경 업데이트 링크
- **번역**: 우리 소품 목록의 "마지막 등불지기의 쓰러진 흔적", "비석" 같은 서사 소품을 길 위 정해진 지점(랜드마크)에 배치하고, 랜드마크는 키 라이트로 한 번 비춘다. [제안]

---

## 4. 전투 피드백

### 4.1 "게임플레이 우선" VFX 원칙
출처: https://news.blizzard.com/en-us/article/23746639/diablo-iv-quarterly-updatedecember-2021 [확인]
- 플레이어 스킬, 몬스터 텔레그래프, 함정, 피해 지역, 캐릭터 상태 효과는 어떤 지역과 조명 조건에서도 읽혀야 한다.
- 화려한 이펙트는 궁극기 등 강한 스킬에 아껴 둔다. 약한 스킬은 배경에 머문다. 클래스별 시각 강도가 스킬 위력에 따라 오른다.
- 피해 영역(애니메이션 되는 범위)이 이펙트와 프레임 단위로 맞는다. 회오리는 캐릭터와 함께 움직이는 부채꼴, 범위기는 시간에 따라 확장, 근접 스윙은 무기 궤적을 따른다. 그래서 타이밍 요소가 생긴다.
- 타격 효과는 실제 맞은 위치에 생기고 공격 방향으로 밀어낸다(무릎을 맞추면 무릎에서 피가 튀고 다리가 꺾임).
- 피·내장은 조명에 반응하고, 근처 몬스터의 피가 영웅 갑옷에 튀었다가 시간이 지나면 깨끗해진다.
- 많은 이펙트가 스스로 빛을 내 어두운 환경에서 보이게 한다. 일부 궁극기는 날씨와 조명을 일시 변경한다.
- 옛 디아블로의 "조명 안 받는 파티클"은 어두운 던전에서 너무 밝았다. D4 이펙트는 주변 빛(야외 햇빛/실내 횃불)을 받는다.
- 게임플레이에 중요한 이펙트는 물리 규칙을 일부러 비틀어 항상 선명하게 하고, 몰입용 요소는 완전히 물리 조명을 받게 하는 셰이더 라이브러리를 둔다.
- 한계: 이 기사는 "몬스터 텔레그래프는 읽혀야 한다"고만 하고 설계 방법은 설명하지 않는다.
- **번역**: 우리 시뮬레이션은 결정적이므로 "텔레그래프 채움 = 시전 타이머 ÷ 선딜, 피해 발생 = 채움 100%"로 연결할 수 있다(D4의 프레임 정합과 같은 효과). 이펙트는 두 등급으로 나눈다: "게임플레이 이펙트(항상 선명)"와 "분위기 이펙트(조명 받음, 낮은 알파)". → 2, 5번 항목.

### 4.2 보스 텔레그래프의 실제 모습(관찰)
- 가이드 기사들을 종합하면 D4의 위험 표시는 주로 바닥의 붉은 강조 영역(선, 원, 삼각형 등)이고, 번개 공격에는 파란 원이 쓰이는 경우도 있다. 일부 공격은 예고가 부실하다는 플레이어 불만(릴리트 삼중 베기)이 있다. [검색요약/2차] https://gamerguides.com/diablo-iv/guide/seasons/season-1/diablo-4-echo-of-varshan-boss-fight-walkthrough , https://segmentnext.com/diablo-4/bosses/lilith , https://destructoid.com/?p=387724 , https://us.forums.blizzard.com/en/d4/t/echo-of-lilith-triple-slash-wolverine-attack-is-poorly-telegraphed/139035
- 효과 과잉으로 바닥·텔레그래프·내 캐릭터까지 안 보인다는 불만이 핵심 문제로 남아 있고, 다른 플레이어 효과를 숨기거나 불투명도·윤곽선만 보이게 하는 옵션 요청이 있다. 공식 답변은 아직 없다. [확인] https://wp-prod.icy-veins.com/diablo-4-effect-toggles-players-renew-calls-for-this-feature/
- 과거 시체 폭발 같은 효과가 화면을 가려 패치로 가시성을 개선한 사례가 있다(상세 미확인). [검색요약] https://www.gamesradar.com/diablo-4-necromancer-corpse-explosions-have-finally-been-fixed-so-you-can-actually-see-the-game
- **번역**: "붉은 바닥 = 위험"은 우리 막 III(핏덩이 소굴)에서는 통하지 않는다(바닥이 이미 붉음). 그래서 "색"이 아니라 "형태 문법"을 일정하게 한다: 항상 [가는 밝은 외곽선 + 안쪽에서 바깥으로 차오르는 채움 + 가장자리 점멸 1회]. 색은 막별로 바꿔도 이 문법이 같으면 학습된다. [제안] → 2번 항목.

### 4.3 대미지 숫자
- 패치 2.0.3에서 큰 숫자가 K 단위로 축약되어 표시된다(예: 10,000이 10K 형태). [확인] https://www.icy-veins.com/d4/news/huge-diablo-4-patch-2-0-3-notes-realmwalker-and-undercity-improvements-mythic-unique-buffs-uniques-return-and-more/ (옵션 여부는 미확인)
- Season 5부터 "전부/없음"에서 유형별 표시 설정으로 바뀌어 압도(Overpower) 피해 등 특정 문구만 끌 수 있다. [확인, 기존 문서 출처] https://mein-mmo.de/en/diablo-4-in-the-long-run-the-most-important-feature-a-small-change-in-season-5-brings-great-joy,1161471
- 플레이어 불만: 상태 문구(취약, 면역, 속박 등)가 시야를 가린다, 일부 화면 표시가 아케이드식 오버레이 같다. [확인, 플레이어 의견] 포럼 링크(1.5)
- **번역**: → 기존 UI 문서 9번(합산, 상한, 크기)에 "K 축약 + 유형별 모드 + 상태 문구는 아이콘화"를 확정으로 얹는다. → 6번 항목.

### 4.4 화면 흔들림·번쩍임
- D4 접근성에는 "스트로빙 줄이기", "전투 피격 플래시 끄기", "화면 흔들림 끄기"가 있다. [확인] https://www.gamepressure.com/diablo-iv/accessibility-features/ze109bb
- **번역**: 우리도 흔들림·피격 플래시·번쩍임 3종을 켜기/끄기 옵션으로. 기본 흔들림은 보스 강타·플레이어 피격에만 약하게. [제안] → 4, 7번 항목.

### 4.5 죽음 연출
- D4는 클래스별 고유 처형(얼려 부수기, 태우기, 절단 등)과 조명에 반응하는 피를 쓴다. [확인] Dec 2021 업데이트 링크
- **번역**: 우리는 몬스터 사망 시 (1) 0.1초 정지 프레임(hit-stop) → (2) 막별 잔해 파티클(모래 가루, 포자, 얼음 조각, 슬래그, 별가루) → (3) 낮은 대비 바닥 데칼로 남겨 청소. 시체가 쌓여 전투 가독성을 해치지 않게 데칼은 알파 0.35 이하로 일정 시간 후 사라지게. [제안] → 15번 항목.

---

## 5. UI

공개 자료에서 확인한 것이 적다(UI 아트 인터뷰는 HUD 배치나 폰트 선택을 상세히 말하지 않음). [확인] https://interfaceingame.com/articles/interview-with-fernando-forero

### 5.1 확인된 D4 UI 결정
- 오픈 베타 후: 기본 산세리프 폰트를 새 세리프 폰트로 교체, 행동 바가 중앙으로 오면서 채팅은 왼쪽, 인벤토리의 "재료와 능력치" 탭은 능력치를 기본 표시. 문을 열면 미니맵에 핑이 찍혀 지나간 곳이 보인다. [확인] https://news.blizzard.com/en-us/diablo4/23938289/diablo-iv-open-beta-retrospective-transforming-feedback-into-change
- 접근성: 글자 크기 3단(작게/보통/크게), 자막 색·크기·배경 불투명도, 색맹 필터, 커서 크기, 개체별 외곽선 색, 채팅 색·배경 불투명도. [확인] https://www.gamepressure.com/diablo-iv/accessibility-features/ze109bb , https://news.blizzard.com/en-us/article/23954932/combatting-demons-with-accessibility-in-diablo-iv
- 런치부터 있었던 것: 색 대비 설정, 자막, 재매핑, 전리품 소리 신호, 홀드 입력 제거. [확인] https://wp-prod.icy-veins.com/?p=83972
- 플레이어 피드백(공식 아님): 월드 속 글자(드랍 이름·대사)가 아트 스타일과 따로 논다, 미니맵을 더 투명하게, 항상 떠 있는 안내 문구는 끌 수 있어야 한다. [확인, 플레이어 의견] 포럼 링크(1.5)
- 미니맵: Season 2에서 미니맵이 더 넓은 영역을 보여주도록 줌 아웃. 이후 전체 오버레이 맵이 확장팩에서 추가될 예정이라는 보도가 있으나 출처 신뢰도가 낮다. [검색요약/미확인] https://www.diablobytes.com/diablo-iv/news/2026-04-23-diablo-4-is-bringing-back-a-classic-feature/ , https://www.icy-veins.com/d4/news/blizzard-detail-new-zoomed-out-view-coming-in-diablo-4-season-4/

### 5.2 번역
- 우리 Galmuri 폰트는 이미 아트와 맞는다. 추가할 것은 3단 글자 크기, 미니맵 불투명도, 안내 문구 끄기. → 기존 UI 문서 항목과 함께 7, 14번 항목.
- 희귀도: 색 + 형태(테두리/아이콘/글자) 병기는 기존 문서 7번 유지.

---

## 6. 등불지기에 적용할 변경 15개 (우선순위 순)

공통 전제: 기존 문서의 결정(전역광 중립, 랜턴 `#FFEBCF`, 막별 팔레트, 액터 보조광, 블룸 임계 0.9~1.0)은 유지한다. 아래는 그 위에 더하는 항목이다. 난이도 S(반나절 이하), M(1~2일), L(3일 이상).

### 1. 몬스터 림 + 얇은 외곽선 셰이더 (M)
- **무엇**: 몬스터 스프라이트의 윗가장자리 1px 림(막별 림색)과 아주 약한 어두운 외곽선을 셰이더로 얹는다. 위 쪽 가장자리만 림이라 아이소 광원 방향과 맞는다.
- **왜**: D4는 실루엣과 "차가운 몸 + 채도 높은 악센트"로 분리한다 [확인 2.1]. 개체 종류별 외곽선 강조도 제공한다 [확인 2.4]. 림 수치 자체는 출처에 없다 [제안].
- **방법(URP 2D)**: Shader Graph(Sprite Lit)로 알파 채널을 위/위좌측으로 1~2px 오프셋 샘플링, `rim = saturate(alpha - alpha(offset))`를 Emission에 `_RimColor` 곱해 더한다. 외곽선은 4방향 알파 샘플로 `outline = max(neighbors) - alpha`. 두께는 텍셀 단위(`_MainTex_TexelSize`)로 계산해 픽셀 정합 유지. 머티리얼 프로퍼티 블록으로 막별 `_RimColor` 전달(배치 깨짐 주의, 스프라이트 아틀라스+MaterialPropertyBlock 대신 막별 머티리얼 6개 권장). 림 강도 0.35~0.6, 외곽선 알파 0.5. [제안]

### 2. 텔레그래프 문법 통일 (M)
- **무엇**: 모든 위협 예고를 [밝은 가는 외곽선 + 안에서 바깥으로 차오르는 채움 + 채움 완료 직전 1회 점멸]로 통일. 모양(원/선/부채꼴)만 공격별로 다르게.
- **왜**: 읽힘은 모든 지역·조명에서 보장되어야 한다 [확인 4.1]. D4도 빨강이 흔하지만 파랑 등 예외가 있고 일부 공격은 예고가 부실하다는 불만이 있다 [검색요약 4.2]. 우리 막 III은 바닥이 붉어서 "빨강=위험"이 깨지므로 색이 아닌 문법이 상수가 되어야 한다 [제안].
- **방법**: 바닥 데칼 스프라이트(Sprite Unlit, 라이트 영향 안 받게)에 `_Progress`(0~1) 프로퍼티를 가진 셰이더 그래프: `fill = step(dist, _Progress*radius)`, 외곽선은 `smoothstep` 링. 진행도는 시뮬 이벤트의 "시전 시작 틱/선딜 틱"에서 계산(렌더가 시뮬에 종속, 결정성 유지). 색: 막 I `#FFC94A`, II `#B8F060`, III `#FFF2D8`(붉은 바닥 위 밝은 백색계), IV `#8FE0FF`, V `#FFB347`, VI `#B79CFF`(VFX 색과 동일, 기존 문서 표). 알파 0.45(채움)·0.9(외곽선). 소팅: 바닥 위, 액터 아래. [제안]
- **주의**: Global Light 영향을 받으면 어두운 막에서 묻히므로 Unlit 머티리얼로 둔다(D4도 게임플레이 이펙트는 물리 규칙을 비틀어 선명하게 유지 [확인 4.1]).

### 3. 엘리트·보스 식별 채널 4종 (S~M)
- **무엇**: (a) 엘리트 스프라이트 스케일 1.2~1.4, (b) 발밑 링 데칼(막 악센트색, 완만한 펄스), (c) 이름표 색·아이콘, (d) 림 강도 +30%.
- **왜**: D4는 색 코드를 줄여 엘리트 식별이 어려워졌다는 보도가 있다 [확인 2.2]. 우리는 가독성이 우선.
- **방법**: 링은 Sprite Unlit 데칼, 크기는 콜라이더 반경×1.6. 이름표는 월드 스페이스 TextMesh/TMP + 1px 외곽선. 보스는 추가로 체력 바를 화면 상단에 고정. [제안]

### 4. 히트 플래시·히트 스톱·흔들림 규격화 (S)
- **무엇**: 피격 시 2프레임(약 0.06초) 흰색 틴트(최대 50% 혼합, 알파 1이 아님), 맞은 위치에 스파크 스프라이트, 근소한 밀림(1~2px), 치명타·강타만 0.04~0.08초 히트 스톱. 화면 흔들림은 보스 강타·플레이어 피격만, 진폭 ≤ 2px(월드 단위 기준).
- **왜**: D4의 피격 글로우는 "어두운 톤에 너무 밝다"는 이유로 옵션이 되었다 [확인 2.3]. 타격은 실제 맞은 위치에서 공격 방향으로 밀려난다 [확인 4.1]. 흔들림·번쩍임은 옵션으로 [확인 4.4].
- **방법**: 몬스터 머티리얼에 `_FlashAmount`(0~0.5) 프로퍼티 → `lerp(color, white, _FlashAmount)`. 코루틴/타이머로 0.06초 감쇠. 히트 스톱은 `Time.timeScale`이 아니라 해당 액터 애니메이터 속도만 0.05초 낮추는 방식(방치형이라 전체 일시정지는 부적합). 흔들림은 Cinemachine Impulse 또는 카메라 오프셋 노이즈(트라우마 모델)를 쓰되 설정 `shakeScale`(0~1)을 곱한다. [제안]

### 5. VFX 시각 위계: 등급·알파·짧은 스킬 라이트 (M)
- **무엇**: 스킬 이펙트에 3단계(약/중/강)를 둔다. 약=알파 0.5·스케일 작음, 강(궁극기·보스 패턴)만 풀 밝기와 Light 2D 펄스. 아군 이펙트는 적·텔레그래프보다 어둡게(알파 0.5~0.6).
- **왜**: 화려한 효과는 강한 스킬에 아껴 쓴다 [확인 4.1]. 아군 해골의 밝기를 베타 후 낮췄다 [확인 2.5]. 다른 플레이어 이펙트가 바닥과 텔레그래프를 가리는 것이 주요 불만 [확인 4.2].
- **방법**: Light 2D(Point)를 오브젝트 풀로 관리(최대 동시 6개), 지속 0.1~0.25초, 강도 0.6~1.2, 색은 이펙트색. 이펙트 머티리얼은 Sprite Lit(분위기용) / Sprite Unlit(게임플레이용) 2종. 동시 이펙트 수 상한과 파티클 최대 개수를 등급별로 지정. [제안]

### 6. 대미지 숫자 파이프라인 (M)
- **무엇**: 0.2초 합산, K/M 축약, 대상당 초당 최대 N개, 일반=작고 반투명, 치명타=크고 불투명, 상태 문구는 아이콘, 설정 3단(전부/중요만/끔), 1px 어두운 외곽선.
- **왜**: D4는 숫자를 K로 축약했고 [확인 4.3], Season 5에서 유형별 표시 설정을 넣었다 [확인 4.3]. 상태 문구가 시야를 가린다는 불만이 있다 [확인 4.3].
- **방법**: TextMeshPro 풀(오브젝트 풀), 월드 스페이스 대신 스크린 스페이스 오버레이 캔버스로 수직 이동 + 알파 페이드 0.6초. 글자 크기는 `log10(값)`로 완만하게(0.8~1.3배). 막 VI 가독성을 위해 외곽선 필수. [제안]

### 7. 접근성/가독성 옵션 패널 (M)
- **무엇**: ①개체별 외곽선 색(플레이어·적·NPC·상호작용물, 기본은 약하게), ②"캐릭터 대비 강조" 슬라이더(액터 보조광 강도 + 외곽선 알파), ③번쩍임 줄이기, ④피격 플래시, ⑤화면 흔들림, ⑥글자 크기 3단, ⑦다른 이펙트 불투명도.
- **왜**: D4는 외곽선·글자 크기·번쩍임·흔들림 옵션을 제공하고 [확인 2.4, 4.4, 5.1], 월드 속 캐릭터 대비를 키우는 옵션을 넣었다고 보도됨 [미확인 1.6]. 어두운 룩은 옵션으로 보완해야 한다는 것이 D4의 교훈.
- **방법**: `GameSettings` ScriptableObject + PlayerPrefs. 슬라이더는 URP 쪽 값에 연결: 액터 전용 Global Light 2D(Target Sorting Layers = 액터)의 `intensity`, 외곽선 셰이더의 `_OutlineAlpha`, 글로벌 `Shader.SetGlobalFloat`. 기존 UI 문서 3번(ESC/설정)과 같은 화면에 넣는다. [제안]

### 8. 키 라이트 중심의 방 조명 + 캠프 오아시스 (M)
- **무엇**: 각 방/구역에서 길·목표·랜드마크에만 소형 키 라이트(Point/Spot, 반경 3~5 타일)를 두고, 나머지는 전역(낮은 중립)에 맡긴다. 캠프(쉼터)는 항상 가장 따뜻하고 밝은 곳.
- **왜**: D4 던전은 선택적 조명으로 길을 암시하고 [확인 1.3], 따뜻한 쉼터와 차가운 야외의 대비가 임팩트를 만든다 [확인 1.3].
- **방법**: 맵 생성기가 목표/출구/소품 광원 위치를 정할 때 "키 라이트 예산"(방당 2~4개, 막 VI는 1~2개)을 사용. Light 2D `Falloff Strength` 0.6~0.8, 내부 반경 0.3×외부. 캠프 라이트는 `#FFEBCF` 강도 +20%에 흔들림(0.5~1Hz, ±8% 노이즈). 기존 문서 3장의 "소품 광 강도" 범위 안에서 운영. [제안]

### 9. 조명 받는 안개 레이어 + 막별 대기 파티클 (M)
- **무엇**: 바닥 위·액터 아래에 안개 스프라이트 2겹(알파 0.06~0.12, 서로 다른 속도로 스크롤)과 막별 파티클(모래 먼지, 포자, 눈가루, 불씨, 재, 별가루)을 Sprite Lit로.
- **왜**: D4의 파티클은 조명을 받고 먼지·입자가 어둡고 거친 톤의 핵심이다 [확인 1.4]. 날씨·안개가 지역 정체성이다 [확인 1.4].
- **방법**: 안개 텍스처는 저해상도(128~256 노이즈) + `Wrap Mode Repeat`, UV 스크롤 셰이더(Sprite Lit 호환: Shader Graph Sprite Lit Master). 색은 막 전역색(저채도). 파티클은 Particle System의 Renderer 머티리얼에 Sprite Lit 셰이더를 지정해 라이트 2D를 받게 한다(문서: https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/2d-urp/2d-index/prep-shader [확인]). 총량은 화면당 파티클 150 이하. 막 VI는 최소(조용함 유지). [제안]

### 10. 타일 키트 구조: 변주 레이어, 전환 타일, 세트 드레싱 3겹 (L)
- **무엇**: (a) 막당 변주 레이어 2종(소품 세트 교체로 분위기 변경), (b) 막 경계 전환 타일, (c) 걷는 영역=저대비·소품 최소, 가장자리=중간 디테일, 외곽=어둠의 3겹 밀도 규칙.
- **왜**: D4는 모듈 타일셋 + 테마 레이어 + 전환 씬으로 150개 이상 던전을 만든다 [확인 3.1]. 고정 카메라에서 걷는 공간을 가리지 않게 배치한다 [확인 3.2].
- **방법**: 타일맵 레이어를 Floor / FloorDecal / Wall / Props / Overhead 5개로 분리하고, 변주는 Props·FloorDecal 프리팹 세트만 교체(바닥/벽은 유지). 전환 타일은 두 막 바닥 팔레트의 중간색 타일을 PixelLab으로 2~3종 추가. 소품 배치 스크립트에 "경로 반경 1.5타일은 소품 금지" 규칙. [제안]

### 11. 포스트 프로세싱 위생 점검 (S)
- **무엇**: ①검정이 뜨지 않는지 확인(Post Exposure 0, 비네팅 뒤 검정 유지), ②블룸은 임계값을 높여 광원 코어만 번지게, ③색보정은 광원 코어의 채도만 올리고 그림자는 중립(Split Toning으로 그림자를 물들이지 않음).
- **왜**: D4가 베타에서 검정이 떠 안개처럼 보이는 문제를 겪고 톤 곡선으로 해결했다 [확인 1.5]. 광원 색이 더 따뜻해지고 조명 받는 곳의 채도가 오르는 방향이다 [확인 1.5].
- **방법**: 같은 장면의 스크린샷을 흑백 변환해 가장 어두운 픽셀이 (0~3)/255인지 확인(Editor 스크립트로 히스토그램 로그). Volume: Bloom Threshold 1.0 / Intensity 0.25~0.4, Color Adjustments Saturation 0~+5(광원이 아닌 전체는 건드리지 않음), Lift Gamma Gain 미사용. [제안]

### 12. 몬스터 디테일 3단 예산 + PixelLab 검수표 (S)
- **무엇**: 몬스터마다 (1단) 실루엣 큰 형태, (2단) 악센트 1~2곳, (3단) 근접 디테일을 구분해 기록하고, 검수는 "4배 축소 후 실루엣이 읽히는가"로 한다.
- **왜**: D4는 큰 형태 → 2차 → 3차 순서, 게임 거리에서 디테일이 읽힘을 방해하지 않게 한다 [확인 2.1].
- **방법**: 생성한 스프라이트를 25% 크기로 축소한 시트를 만들어 바닥 타일 위에 합성해 확인하는 Editor 도구(스크립트). 프롬프트에는 "clear silhouette, one saturated accent, no fine texture noise" 추가. [제안]

### 13. 전리품 가독성 3채널 + 필터 (M)
- **무엇**: 희귀도별 (a) 바닥 라벨 색+테두리 모양, (b) 드롭 사운드, (c) 미니맵 아이콘. 그리고 희귀도 이하 자동 숨김(루트 필터) 설정.
- **왜**: D4는 내장 루트 필터 [검색요약 2.5-b], 특정 아이템에 독립 미니맵 아이콘·사운드를 부여했다 [검색요약 2.5-b]. 전리품 소리 신호는 런치부터 있었다 [확인 5.1].
- **방법**: 바닥 라벨은 TMP + 1px 외곽선 + 테두리 스프라이트, 희귀도별 AudioClip, 필터는 `LootFilterSettings`(최소 희귀도, 숨김 임계). 기존 UI 문서 7번(색 병기)과 함께 진행. [제안]

### 14. 미니맵 불투명도·줌·전체 오버레이 (S~M)
- **무엇**: 미니맵 불투명도 슬라이더, 줌 2단, M으로 전체 반투명 오버레이.
- **왜**: D4 미니맵은 줌아웃이 있었고 [검색요약 5.1], 투명도 요청이 포럼에 있다 [확인 5.1 플레이어 의견]. 오버레이 맵은 확장팩에서 추가라는 보도(신뢰도 낮음) [미확인].
- **방법**: 미니맵 RawImage의 `CanvasGroup.alpha`와 두 번째 카메라(Orthographic Size 2단). 기존 UI 문서 11번과 통합. [제안]

### 15. 사망 연출과 바닥 데칼 정리 (S)
- **무엇**: 사망 = 0.05~0.1초 정지 → 막별 파편 파티클 → 낮은 대비 데칼(알파 ≤0.35)로 남겨 8~12초 후 페이드. 데칼 동시 최대 60개.
- **왜**: D4는 사망 연출과 조명에 반응하는 피 표현을 중시하지만 [확인 4.5], 효과가 쌓여 화면을 덮는 불만이 크다 [확인 4.2].
- **방법**: 데칼은 Sprite Unlit, 소팅 레이어 FloorDecal(텔레그래프보다 아래). 오브젝트 풀 + 가장 오래된 것부터 회수. 막별 색은 몬스터 악센트의 30% 명도 버전. [제안]

---

## 7. 시작 순서 제안과 검증

- 1단계(눈에 보이는 개선, 하루 이내): 11번 → 4번 → 3번(a,b) → 14번.
- 2단계(핵심 가독성): 1번 → 2번 → 5번 → 6번.
- 3단계(분위기·구조): 8번 → 9번 → 15번 → 12번 → 7번.
- 4단계(콘텐츠 구조): 10번, 13번.
- 검증(기존 문서 3장 8번에 추가): ①난전 스크린샷을 25% 축소해도 몬스터·텔레그래프가 읽히는가, ②6막 스크린샷 퀴즈, ③번쩍임·흔들림·외곽선 옵션을 끈 상태와 켠 상태 각각에서 읽히는가, ④30초 동안 눈 피로가 없는가(붉은 워시 재발 확인).

## 8. 확인하지 못한 것 (확장 조사 후보)

- D4 몬스터의 림 라이트/외곽선 사용 여부와 수치: 공개 자료에서 못 찾음. 우리 1번 항목은 순수 [제안].
- IGN 인터뷰 본문(전투 비주얼 "정보 대 몰입"): 본문을 못 열었다. https://www.wowhead.com/diablo-4/cn/news/diablo-iv-ign-interview-combat-design-and-visuals-information-versus-immersion-329803 (제목만 확인)
- ArtStation Art Blast(환경, UI 컨셉): 403. https://magazine.artstation.com/2023/07/blizzard-entertainment-diablo-iv-environment-art-blast/ , https://magazine.artstation.com/2023/07/blizzard-entertainment-diablo-iv-concept-art-ui-art-blast/
- GDC 2023 "Pillars of Hell", GDC 2024 "Art of Open World Sanctuary": 세션 설명만(검색 요약). 슬라이드 본문은 못 봄. https://schedule.gdconf.com/session/the-pillars-of-hell-achieving-the-artistic-vision-of-diablo-iv/902745
- Season 4 "월드 속 캐릭터 대비 강화" 옵션의 정확한 이름과 동작: 2차 요약만. https://www.icy-veins.com/d4/news/diablo-4-season-4-loot-reborn-patch-notes/
- 텔레그래프 색/형태에 대한 Blizzard의 공식 설계 문서: 못 찾음. 4.2는 가이드 기사와 포럼으로 종합한 관찰.
- Digital Foundry 원본 분석: 못 찾음. 콘솔 해상도/업스케일 중심의 2차 요약만 있었고, 라이팅을 칭찬했다는 정도다. https://wccftech.com/diablo-iv-ps5-xbox-series-x-s-ps4-analysis-native-res-low-fsr/
- Unity 2D Light 속성(Target Sorting Layers, Volume Opacity 등)의 정확한 위치: Unity 6 매뉴얼 개요 페이지만 확인했다. https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/2d-urp/2d-index (상세 속성은 에디터에서 확인 필요)

## 출처 목록

Blizzard 공식
- 환경 아트 분기 업데이트(2022-03): https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022
- 그래픽 해설 "Peeling Back the Varnish": https://news.blizzard.com/en-us/article/23964183/iv
- 캐릭터·몬스터 아트(2021-06): https://news.blizzard.com/en-us/article/23665024/diablo-iv-quarterly-update-june-2021
- 시각 효과 분기 업데이트(2021-12): https://news.blizzard.com/en-us/article/23746639/diablo-iv-quarterly-updatedecember-2021
- 오픈 베타 회고: https://news.blizzard.com/en-us/diablo4/23938289/diablo-iv-open-beta-retrospective-transforming-feedback-into-change
- 접근성 글: https://news.blizzard.com/en-us/article/23954932/combatting-demons-with-accessibility-in-diablo-iv

인터뷰·보도
- PCGamesN 아트 디렉터 인터뷰: https://www.pcgamesn.com/diablo-4/artwork-interview-john-mueller
- TechRadar: https://techradar.com/news/diablo-4-looks-gritty-and-dark-so-dark-youll-need-to-squint-to-see-anything
- GameSpot 아트 디렉션(검색 요약): https://gamespot.com/articles/diablo-4s-art-direction-emphasizes-hand-crafted-details-plenty-of-customization-and-just-enough-realism/1100-6493411/
- mein-mmo 히트 글로우: https://mein-mmo.de/en/one-of-the-most-annoying-features-from-diablo-4-can-be-turned-off,651559
- mein-mmo 색 코드 제거: https://mein-mmo.de/en/diablo-4-sacrifices-an-important-comfort-feature-to-be-more-realistic,905787
- mein-mmo 시즌 5 전투 문구: https://mein-mmo.de/en/diablo-4-in-the-long-run-the-most-important-feature-a-small-change-in-season-5-brings-great-joy,1161471
- Icy Veins 이펙트 토글 요청: https://wp-prod.icy-veins.com/diablo-4-effect-toggles-players-renew-calls-for-this-feature/
- Icy Veins 접근성 2년: https://wp-prod.icy-veins.com/?p=83972
- Icy Veins 패치 2.0.3: https://www.icy-veins.com/d4/news/huge-diablo-4-patch-2-0-3-notes-realmwalker-and-undercity-improvements-mythic-unique-buffs-uniques-return-and-more/
- Icy Veins 루트 필터: https://www.icy-veins.com/d4/news/goodbye-screen-clutter-diablo-4-is-getting-a-loot-filter/
- Gamepressure 접근성: https://www.gamepressure.com/diablo-iv/accessibility-features/ze109bb
- GamesRadar 시체 폭발 가시성: https://www.gamesradar.com/diablo-4-necromancer-corpse-explosions-have-finally-been-fixed-so-you-can-actually-see-the-game
- Interface In Game UI 아티스트 인터뷰: https://interfaceingame.com/articles/interview-with-fernando-forero
- 플레이어 피드백(Blizzard 포럼): https://us.forums.blizzard.com/en/d4/t/feedback-on-d4s-graphics-ui-and-visual-presentation/13290

Unity
- 2D URP 개요: https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/2d-urp/2d-index
- 라이트 블렌드 스타일: https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/2d-urp/2d-index/2d-light-blend-modes
- Sprite Lit/Unlit 셰이더: https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/2d-urp/2d-index/prep-shader
- 픽셀 퍼펙트 카메라: https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/2d-urp/2d-pixelperfect
