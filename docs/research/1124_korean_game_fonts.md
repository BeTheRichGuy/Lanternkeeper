# 다크 판타지 픽셀 게임용 한글 폰트 조사 (1124 리서치)

표기: [확인]=공식/저장소/눈누 본문을 직접 읽음, [요약]=검색 요약문만, [기억]=기존 지식. 상용 게임 임베딩은 OFL 1.1이면 가능하다(폰트 파일 자체의 단독 판매 금지, 파생물에 Reserved Font Name 사용 금지, 파생물도 OFL 유지). 눈누 표는 "임베딩 사용 가능"으로 표기하며 "참고용, 정확한 범위는 저작권자 확인"이라고 명시한다. 출시 전에 각 저장소의 LICENSE 원문을 빌드 폴더에 같이 넣을 것.

## 1. 픽셀(도트) 폰트

| 폰트 | 제작자 | 라이선스 | 픽셀 크기/굵기 | 글자 범위 | 분위기 | 공식 받기 |
|---|---|---|---|---|---|---|
| Galmuri(갈무리) | Lee Minseo | OFL-1.1 [확인] | Galmuri14(15px), Galmuri11(12px; Regular/Bold/Condensed), Galmuri9(10px), Galmuri7(8px), Mono 계열. FontPixel 표기로는 9px·10px·12px·16px(Regular/Bold/Condensed)·21px까지 변형. 권장 12px, 메트릭 상 캡 높이 11/엑스 높이 8 | 한글 11,172 음절(Galmuri7만 Adobe-KR 4,358자), 호환 자모, 한자 일부, 일본어 가나, 라틴/키릴/그리스 [확인] | 옛 휴대용 게임기 도트, 깔끔한 가독성 우선, 가장 범용적 | https://github.com/quiple/galmuri (최신 v2.40.4 표기) / https://noonnu.cc/font_page/800 |
| Neo둥근모 (NeoDunggeunmo) / Code / Pro | Eunbin Jeong (Dalgona.) | OFL-1.1 (글꼴 파일·소스 모두) [확인] | 16px 고정 단일 크기, 단일 굵기, 고정폭(Code 포함). Pro는 별도 저장소(neodgm-pro)로 비고정폭 | 12,505자 인코딩, 폭 16px x 11,767자, 8px x 717자(영문) [확인] | 90년대 PC통신/도스 터미널 감성, 레트로 | https://github.com/Dalgona/neodgm , https://neodgm.dalgona.dev/ , https://noonnu.cc/en/font_page/386 |
| 둥근모꼴 + Fixedsys | 길형진(orioncactus) | 상업 무료 사용 가능으로 표기 [요약]; 원본 둥근모꼴은 김중태 IT문화원이 90년대에 퍼블릭 도메인으로 공개 [요약, neodgm.dalgona.dev] | 16px급 비트맵 계열 | 한글+영문 | 코레일 전광판/PC통신 | https://noonnu.cc/font_page/250 , https://orioncactus.com/projects/dunggeunmo |
| 물마루(Mulmaru) / Mulmaru Mono | mushsooni | OFL-1.1 (LICENSE/LICENSE_ko) [확인] | 12px(9pt) 또는 그 배수에서 가장 선명, 굵고 부드러운 획 | 한글 11,172 + ASCII 95 + 호환 자모 51 + 라틴-1 77 + 그리스·콥트 49 + 키릴 65 + 히라가나 88 + 가타카나 90 + 한자 7 + 기타 243 [확인] | 한글패치용으로 만든 "게임에 어울리는" 두껍고 둥근 도트, 모험/캐주얼 | https://github.com/mushsooni/mulmaru , https://mushsooni.github.io/ |
| Fusion Pixel Font | TakWolf | 폰트 OFL-1.1, 코드 MIT [확인] | 8px, 10px, 12px (모노/프로포셔널 각각) | 한글은 Galmuri 8·10·12px를 합성해 제공, 일본어는 Misaki, 중국어는 Ark Pixel 등 [확인] | 다국어 통합 픽셀, 한글은 사실상 Galmuri | https://github.com/TakWolf/fusion-pixel-font |
| 던파 비트비트체 v1/v2 | (주)네오플(넥슨) | 넥슨 자체 라이선스: 개인·기업 모두 무료, 자유롭게 사용·수정·배포(상업 가능). 페이지에 OFL 취지 문구도 포함 [확인/요약] — OFL 1.1 인증 여부는 원문 확인 필요 | 비트맵 느낌의 굵은 제목용 | v1: 한글 2,780 + 추가 1,103 + 라틴 95 + 특수 56. v2: 한글 11,172 + 영문 94 + KS 기호 986 [확인] | "도트/픽셀 아트에 적용하기 딱 좋은" 굵은 타이틀, 던전앤파이터 특유의 계단형 질감 | https://df.nexon.com/data/font/dnfbitbitv2 , https://noonnu.cc/font_page/1233 |
| DOS 명조 / DOS 고딕 | (눈누에 "DOS Myungjo"가 둥근모 유사 폰트로 노출) [요약] | 라이선스·제작자 이번에 본문 미확인 → 사용 전 확인 필요 | 비트맵 명조형 | 미확인 | 90년대 도스 명조 감성(세리프형 도트) | https://noonnu.cc/en/font_page/386 의 유사 폰트 목록 |

권장 렌더링 규칙: 도트 폰트는 정수 배수 크기에서만 또렷하다. Galmuri11=12px의 2x/3x(24/36px), Mulmaru=12px 배수, NeoDGM=16px의 2x/3x(32/48px). 기준 해상도 1280x720에서 UI가 정수 스케일이 아니면 텍스트만 폰트 네이티브 크기 배수로 고정하고, 필터링은 Point로.

## 2. 세리프/타이틀용 폰트 (도트가 아닌 본문·제목)

| 폰트 | 제작자 | 라이선스 | 굵기 | 범위 | 분위기 | 받기 |
|---|---|---|---|---|---|---|
| 마루 부리(MaruBuri) | 네이버(나눔글꼴) | OFL 1.1. 번들·임베드·재배포 가능, 폰트 자체 판매 금지, 출처 표기 권장 [확인] | 5종 굵기 [요약] | 한글 전체 + 라틴 | 단정한 명조(부리) 계열, 디지털 화면 가독성 | https://hangeul.naver.com/font/nanum , https://m-hangeul.naver.com/font/detail/maru-buri |
| 고운바탕(Gowun Batang) | 류양희(Yanghee Ryu) | OFL, Google Fonts [확인(제작자/출처)] | Regular, Bold 2종 | 한글+라틴 | 연필로 쓴 듯 단정하고 부드러운 바탕체 | https://fonts.google.com/specimen/Gowun+Batang |
| 송명(Song Myung) | JIKJI | Google Fonts(OFL) [확인(제작자)] | 단일(Regular) | 한글+라틴(라틴은 Source Serif 4 기반) | 빈티지·거친(Rugged) 느낌, "목판/고서 활자" 쪽으로 다크 판타지 제목에 잘 맞음 | https://fonts.google.com/specimen/Song+Myung |
| 함렛(Hahmlet) | Hypertype | Google Fonts(OFL), 가변 폰트(Weight 축) [확인] | 100~900 가변 | 한글+라틴 | 현대적 세리프, 굵게 하면 제목 | https://fonts.google.com/specimen/Hahmlet |
| 디필리아(Diphylleia) | Minha Hyung, JAMO | Google Fonts [확인(제작자)] | 단일 | 한글+라틴 | 장식적 디스플레이 | https://fonts.google.com/specimen/Diphylleia |
| 나눔명조(Nanum Myeongjo) | Sandoll Communication | Google Fonts(OFL) [확인(제작자·굵기)] | 400, 700, 800 | 한글+라틴 | 정통 명조, 안정적 | https://fonts.google.com/specimen/Nanum+Myeongjo |
| Noto Serif KR | Google/Adobe | OFL | 가변(Weight) 다중 | 한글+한자+가나+라틴+키릴+그리스 [확인] | 중립적 명조, 가장 넓은 커버리지 | https://fonts.google.com/specimen/Noto+Serif+KR |

Google Fonts 페이지의 License 탭 본문은 이번에 읽지 못했다(제작자/굵기만 확인). Google Fonts 수록 폰트는 OFL이라는 일반 지식은 [기억]이므로 각 폰트의 OFL.txt를 내려받아 확인할 것.

## 3. 다크 판타지 한글 게임이 쓰는 폰트 (찾은 범위)
- Diablo IV 한국어: 블리자드가 한국 유저 특전으로 "디아블로 한국어 전용 서체"를 직접 제작해 2023-03-15 오픈 베타 간담회에서 공개. [요약] https://zdnet.co.kr/view/?no=20230315135200 , https://www.khgames.co.kr/news/articleView.html?idxno=210356 . 팬 사이에서는 "켄리스(kenris)" 폰트로 불림. [요약] https://www.youtube.com/watch?v=cSlSfr3MWaE
- Diablo II Resurrected 한국어 모드 관련 글: 인게임 일반 텍스트는 kodia.ttf, 몬스터 이름 밑 면역/강함 표시는 irisl.ttf를 쓴다고 설명. [요약] https://www.fmkorea.com/3998993869 . 블리자드 한국 포럼에는 "kodia/Diablo Light/exocet를 내 앱에서 써도 되나" 문의 글이 있고, 이 폰트들은 블리자드 자산이므로 우리는 쓰지 않는다. [요약] https://kr.forums.blizzard.com/ko/blizzard/t/ (스레드 8579)
- 던전앤파이터: 전용 도트 계열 "던파 비트비트체"(제목/강조용)를 공식 배포. [확인] 위 표. 넥슨 서체는 대부분 무료지만 Klavika NX 같은 일부는 유료이므로 폰트마다 확인 필요. [요약] https://peak.nexon.com/en/post/963
- Lost Ark: 인게임 폰트 정보는 찾지 못했다(검색 결과가 데미지 폰트 크기 설정 글뿐: UserOption.xml의 BattleFontSize 0/1/2 = 100/150/200%). 전투 숫자 크기를 사용자가 고를 수 있게 한 점만 참고. [요약]
- 결론: 다크 판타지 한글화는 "본문은 가독 우선 고딕/명조 계열, 제목/강조만 개성 서체"가 관례로 보이나 이 점은 이번 조사에서 직접 계측하지 않은 내 인상이다.

## 우리 게임에 적용할 것
1. 본문(툴팁/설명/인벤토리 라벨): Galmuri11(12px 배수) 또는 Mulmaru(12px 배수) — 둘 다 OFL, 한글 11,172자 전체 포함. 작은 글자(숫자, 스탯)는 Galmuri9(10px)/Galmuri7(8px, 단 한글 4,358자만)을 쓰되 Galmuri7은 희귀 음절 누락 가능성 때문에 라벨에는 사용 금지.
2. 제목·캐릭터 이름·전설 아이템명: 두 갈래 중 선택 — (a) 같은 도트 계열로 통일: 던파 비트비트체 v2(굵은 계단형 질감, 한글 전체; 라이선스 문구는 패키지 동봉) 또는 Neo둥근모(16px 배수), (b) 대비를 주는 세리프: 송명(Song Myung, 거친 빈티지) / 마루 부리. 전설 이름은 세리프(송명) + 등급색 + 얇은 외곽선이 "고서 활자" 느낌으로 다크 판타지에 맞다(내 판단).
3. 숫자(데미지·골드)는 영문/숫자 폭이 일정한 모노 계열(Galmuri Mono 또는 Mulmaru Mono)로 흔들림 방지.
4. Unity 적용: TextMeshPro 폰트 에셋을 정적(Static)으로 11,172자 대신 "실사용 한글 집합"(GDD 문구에서 추출한 약 2,500~3,000자 + 입력 가능성 대비 동적 폴백)으로 생성하고, Atlas Padding 2~4, Render Mode = Raster(Hinted) 계열 또는 SDF 해제(도트 폰트는 SDF가 가장자리를 뭉갠다 — 이 부분은 내 경험칙으로 빌드 확인 필요).
5. 라이선스 동봉: 빌드에 `Licenses/` 폴더를 두고 Galmuri, Mulmaru, Neo둥근모, 마루 부리 등 사용 폰트의 OFL.txt와 저작권 문구를 크레딧에 표기. OFL 폰트를 수정(글자 추가)하면 이름을 바꾸고 OFL 유지.
6. 후보 최종 조합(제안): 본문 Galmuri11 / 제목·전설명 송명 또는 던파 비트비트체 v2 / 숫자 Galmuri Mono. 에디터에서 3종을 같은 툴팁에 넣어 1080p 빌드로 비교한 뒤 확정.
7. 검증 TODO: DOS명조/고딕 제작자와 라이선스, 던파 비트비트체 v2 정확한 라이선스 원문, Google Fonts 각 폰트의 OFL.txt를 직접 내려받아 확인(다운로드는 하지 않았다).
