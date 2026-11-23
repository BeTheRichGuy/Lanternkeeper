# 픽셀아트 에셋 파이프라인 조사 (2026-11-18)

> 대상: 1인 + Claude Code, Unity 6 URP 2D, 쿼터뷰(아이소메트릭) 2.5D 디아블로풍 데스크톱 방치형, Steam 출시.
> 조사 한도(WebSearch 약 12회 / WebFetch 5회) 안에서 수행. 가격·기능은 대부분 2차 출처 기반이라 **결제 전 공식 페이지 재확인 필수**. 확인 못 한 항목은 "미확인"으로 표시.

## 1. 결론 먼저

1. **캐릭터/몬스터 (8방향 걷기·공격·사망)**: 1순위 **PixelLab** (REST API + 원격 MCP 서버가 있어 Claude Code가 직접 구동 가능, 4/8방향 캐릭터 + 스켈레톤 애니메이션 지원). 2순위(방향 일관성이 최우선일 때) **3D→픽셀 렌더 파이프라인** (Meshy/Tripo → Mixamo → Blender/SPRITE.EX/Pixor).
2. **아이소메트릭 타일**: PixelLab `create_isometric_tile` (API/MCP 존재) + Aseprite 수작업 보정.
3. **아이템 아이콘 수백 개**: 1순위 PixelLab 또는 gpt-image 계열로 뽑고 → 팔레트 양자화·다운스케일 스크립트로 통일 (Claude Code가 Python으로 일괄 처리). 대량 생산은 이쪽이 가장 싸다.
4. **UI / VFX**: UI는 Unity 네이티브(9-slice, 벡터형 단색) + 마켓 에셋. VFX는 Unity Particle System + 소수의 픽셀 스프라이트 시트(직접 제작 또는 마켓).
5. **ChatGPT(gpt-image)는 보조 도구로만**: 컨셉아트, 아이콘 초안, 레퍼런스 용. 진짜 픽셀 격자/프레임 일관성은 보장되지 않아 최종 스프라이트 생산 라인으로는 부적합 (아래 3.1).
6. **Steam**: AI 사용 공개 의무 있음(출시물에 들어가는 아트는 "pre-generated" 항목). 이미지 한정 반발 위험은 실재하므로 전략 필요 (섹션 9).

## 2. 비교표

| 도구 | 용도 | 방향 일관성 | 아이소 지원 | 애니메이션 | Claude Code 연동 | 가격 | 상업 이용 |
|---|---|---|---|---|---|---|---|
| ChatGPT / OpenAI gpt-image-2 | 컨셉, 아이콘 초안 | 낮음 (프레임/방향 간 드리프트) | 프롬프트로 흉내, 정확도 낮음 | 스프라이트시트 한 장 생성은 가능하나 불안정 | OpenAI Images API (직접 호출 스크립트) | 1024² 기준 약 $0.01(low)~$0.06(med)~$0.22(high) (2차 출처, 출처 간 불일치) | OpenAI 약관상 출력물 사용 가능 (확인 필요) |
| **PixelLab** | 캐릭터, 타일, 애니, 아이콘 | 높음 (4/8방향 캐릭터 생성, 레퍼런스 회전) | `create_isometric_tile` 있음 | 스켈레톤 애니 (128x128 이하), 텍스트 애니 | **REST API + 원격 MCP (`https://api.pixellab.ai/mcp`, 토큰 헤더)** | 구독 $12/월~ (Pixel Apprentice 등, 확인 필요), API는 호출당 과금 (예: 32px 스켈레톤 애니 약 $0.0136, 64px 8방향 캐릭터 약 $0.0173) | 유료 플랜 포함으로 보임 (약관 확인) |
| Retro Diffusion | 픽셀 이미지, 짧은 애니 | 중간 | 일부 모델 (미확인) | 32px 4프레임 걷기 사이클 등 (RD Animation) | Aseprite 확장 (로컬 GPU). 웹은 크레딧제. API는 이번 조사에서 미확인 | Aseprite 확장 $65 (Lite $20), 웹 크레딧 $5/250크레딧~ | 개발자 약관 확인 필요 |
| Scenario.gg | 커스텀 모델 학습, 에셋 일괄 | 중간 (자체 학습 모델로 스타일 고정 가능) | 학습 모델에 의존 | 출처 간 불일치 (Animate/Rotate 4·8방향 언급 vs 없음) | 공개 API 있음 (세부 미확인) | 유료 $15/월~ (확인 필요) | 유료 플랜은 상업권 포함, 무료는 제한 (2차 출처) |
| Ludo.ai | 게임 아이디어/스프라이트 | 중간 | 미확인 | 스프라이트 애니 제공 (비교 페이지 기준) | 미확인 | 미확인 | 미확인 |
| God Mode AI | 미조사 | - | - | - | - | - | - |
| Gemini image ("nano banana") | 레퍼런스 일관 이미지 편집 | 중간 (참조 이미지 유지 강점) | 프롬프트 의존 | 시트 생성 불안정 | Gemini API | 미확인 | Google 약관 확인 |
| Flux/SDXL + 픽셀 LoRA (ComfyUI) | 로컬 완전 통제 | 워크플로 구성하면 높음 (ControlNet, IP-Adapter) | 가능하나 설정 난이도 높음 | AnimateDiff류, 품질 불안정 | ComfyUI API로 스크립트화 가능 | 무료 + GPU | 모델/LoRA별 라이선스 개별 확인 필수 |
| **3D→픽셀 (Meshy/Tripo + Mixamo + 렌더)** | 캐릭터/몬스터 전 애니 | **최고 (같은 모델·카메라)** | 카메라 각도로 정확히 구현 | Mixamo 라이브러리 (공격/사망 포함) | Blender CLI/Python은 Claude Code가 스크립트 가능. SPRITE.EX/Pixor는 GUI | Meshy/Tripo 구독 + 도구 일회성 | 각 서비스 약관 (생성물 상업권은 유료 플랜) |

God Mode AI, Ludo.ai, Gemini, Scenario의 세부는 조사 한도 때문에 얕다. 최종 도입 전 각 공식 페이지 확인 필요.

## 3. 도구별 메모

### 3.1 ChatGPT / gpt-image-2
- 2026년 4월경 출시된 `gpt-image-2` 계열. 토큰 과금이며 출처마다 요율이 다르다 (입력 $5~8 / 1M, 출력 $8~30 / 1M). 이미지 1장당 low/medium/high 구간 위주로 예산을 잡을 것.
- 한계 (경험적 일반론, 이번 조사에서 직접 검증은 못 함): 진짜 N×N 픽셀 격자가 아닌 "픽셀풍" 이미지가 나오며, 방향·프레임이 바뀔 때 디자인이 흔들린다. 투명 배경 보장도 약하다. 따라서 후처리(격자 스냅, 팔레트 고정)가 필수.
- 적합: 컨셉, 몬스터 디자인 시안, 아이템 아이콘 초안, 레퍼런스 시트. 부적합: 8방향 걷기 시트 최종본.

### 3.2 PixelLab (최우선 후보)
- 원격 MCP 서버: `https://api.pixellab.ai/mcp` (API 토큰을 Authorization 헤더로). 문서: `https://api.pixellab.ai/mcp/docs`.
- 캐릭터 생성: `n_directions` 4 또는 8 (기본 8). v3 모드는 항상 8방향이며 레퍼런스 이미지로 기존 스프라이트를 회전시킬 수 있다.
- 애니메이션: REST `POST /v2/animate-with-skeleton` (이미지 128x128 이하), 스켈레톤 추정 엔드포인트 별도, skeleton v3 베타. MCP에는 `animate_character` 류 도구 (2차 출처 기준, 도구명 불일치 가능).
- 아이소메트릭 타일: `Create isometric tile` 엔드포인트/도구 존재.
- 미확인: Aseprite 플러그인 여부, 정확한 구독 한도, 약관의 상업권 문구.
- 128px 이하 제한이 오히려 "작은 데스크톱 오버레이"에 잘 맞는다.


### 3.2-1 PixelLab 공식 API 페이지 확인값 (WebFetch)
- 캐릭터: 4방향 48x48 약 $0.0105, 8방향 48x48 약 $0.0133, v3 64x64 약 $0.041.
- 애니: skeleton v3 3~15프레임 $0.0436~0.0622, 텍스트 애니 v3 최대 16프레임, PixMiniMax 베타 최대 40프레임 (Tier 1 구독 이상 필요).
- 회전: 64x64 단일 $0.0106, 8방향 v3 32x32 $0.0293. 아이소 타일 32x32 $0.0156 (16~64px 지원, 초기 이미지/강제 팔레트 지원). 아이콘/오브젝트(map object) $0.0099.
- VFX: MCP `vfx_workbench` (구독자 무료), Lite 128px 이하 $0.0048.
- 상업 이용 조건은 이 페이지에 없음 -> ToS 직접 확인 필요. 구독 티어 가격도 이 페이지엔 없음.
- 예: 영웅 1종 8방향 + 애니 4종(각 8프레임 x 8방향 = 방향별 애니 호출) 해도 수 달러 수준. API 비용은 사실상 문제가 안 됨.

### 3.3 Retro Diffusion
- Aseprite 확장(로컬, 일회성 $65 / Lite $20) + 별도 웹(크레딧). 확장과 웹은 모델이 달라 결과가 다르다고 개발자가 명시.
- 걷기 사이클 4프레임 같은 소형 애니 가능. API/아이소 전용 기능은 미확인.
- Claude Code가 직접 구동하기엔 GUI(Aseprite) 중심이라 불리. 아이콘·타일 소재 제작용 보조.

### 3.4 Scenario / Ludo / God Mode / Gemini / ComfyUI
- Scenario: 자체 스타일 모델 학습으로 "같은 화풍 수백 개 아이템 아이콘"에 강점. 유료 플랜 약 $15/월~ (2차 출처). 스프라이트 애니 기능은 출처 간 모순.
- Gemini image: 참조 이미지 기반 편집/일관성이 장점. 픽셀 격자 정확도는 gpt-image와 비슷한 한계.
- ComfyUI + 픽셀 LoRA: 비용 최저, 통제 최고, 설정 비용 최고. 1인 팀이 파이프라인 유지보수까지 감당하기 부담. 후순위.
- Ludo.ai, God Mode AI: 이번 조사에서 근거 부족. 보류.

### 3.5 3D→픽셀 파이프라인 (방향 일관성 최강)
1. 컨셉 이미지 → **Meshy/Tripo/Hunyuan3D**로 텍스처 메시 생성 (T-포즈).
2. **Mixamo**로 자동 리깅 + 애니(걷기, 공격, 사망, 피격). 안정성 때문에 OBJ 업로드 사례가 많다 (커뮤니티 보고).
3. 렌더: Blender 직교 카메라, 아이소 각도, 8방향(45도씩) 회전 렌더, Unlit/플랫 셰이딩으로 색 안정.
   - 전용 도구: **SPRITE.EX** (8방향, 팔레트 적용, FBX/Mixamo 입력, Blender 불필요), **Pixor** (4/8/16방향, 전 프레임 피벗 고정), **Sorceress 3D to 2D** (Isometric 8-Dir 프리셋).
4. 후처리: 저해상도 직접 렌더 → 팔레트 양자화 → 외곽선 통일 → Aseprite 수정.
- 장점: 방향·프레임 완전 일관, 애니 재사용(새 몬스터 = 새 메시만 교체), 수십 종 몬스터 확장에 강함.
- 단점: "손맛 있는 픽셀아트"보다 3D 렌더 느낌이 남을 수 있음. 후처리 품질이 결정. AI 3D 모델은 토폴로지 지저분 → Mixamo 리깅 실패 가능.
- 이 방식은 Steam AI 공개 대상에는 여전히 해당(3D 모델이 AI 생성이면).

### 3.6 정리 도구
- **Aseprite** (약 $20, 소스 컴파일은 무료): 수작업 보정, 팔레트, 태그 애니, Lua 스크립트/CLI 배치로 Claude Code가 구동 가능.
- 팔레트 양자화/픽셀퍼펙트 다운스케일: Python(Pillow, numpy)로 직접 스크립트. 고정 팔레트(예: 32색) + 최근접 색 매핑 + 외곽선 정리. Claude Code가 가장 잘하는 영역.
- Unity 측: `2D Pixel Perfect` 컴포넌트, Point 필터, 압축 끔, PPU 일관 설정.

### 3.7 대체 마켓
- itch.io 에셋팩 (라이선스 개별, CC0/상업 허용 표기 확인), Unity Asset Store (Standard Unity Asset Store EULA, 게임 포함 배포 가능), CraftPix (유료 라이선스, 상업 허용). 아이소 쿼터뷰 8방향 캐릭터팩은 드물고 화풍 통일이 어려워 **UI/VFX/배경 소품 보충용**이 현실적.
- 마켓 에셋에는 AI 생성물이 섞였을 수 있어 Steam 공개 판단 시 출처 확인 필요.

## 4. Steam AI 공개 규정 (2026년 1월 개정 기준, 2차 출처)
- 개정(2026-01): 공개 대상은 **플레이어가 실제로 보는 AI 생성 콘텐츠**. 개발 보조 도구(코드 자동완성, Copilot 등)는 제외.
- 두 구분: **Pre-generated** (아트/코드/사운드/대사 등 출시물에 포함된 AI 생성물; 심사 대상, 스토어 페이지에 공개 표시) / **Live-generated** (실행 중 생성; 가드레일 설명 + 신고 버튼).
- 개발자는 저작권·합법성 책임을 짐. 성인 전용 콘텐츠의 라이브 생성은 금지.
- 이 프로젝트: 픽셀아트를 AI로 만들면 **Pre-generated AI 아트로 공개 표시**해야 한다. 정확한 문구는 Steamworks 문서에서 직접 확인할 것 (본 조사는 1차 출처 미확인).

## 5. 자산 종류별 권장 파이프라인

| 자산 | 권장 | 보조/대안 |
|---|---|---|
| 영웅 (직업 몇 종, 8방향 x 걷기/공격/사망/대기) | PixelLab 캐릭터(8방향) + 스켈레톤 애니, Aseprite 보정. 방향 불일치가 심하면 3D→픽셀 | 3D→픽셀 |
| 몬스터 (수십 종) | 3D→픽셀(공용 리그 재사용) 또는 PixelLab. 일반 몬스터는 4방향+좌우 미러로 절감 | 마켓팩 |
| 아이소 타일/바닥 | PixelLab `create_isometric_tile` + 수작업 이음새 정리 | 마켓 타일셋 |
| 소품/건물 | PixelLab 또는 gpt-image 초안 후 다운스케일 | 마켓 |
| 아이템 아이콘 (수백) | PixelLab/Scenario 스타일 모델 일괄 → Python 양자화 | gpt-image-2 low 품질 대량 |
| UI | Unity UI Toolkit/uGUI + 9-slice, 마켓 UI 팩 | AI 생성 장식만 |
| VFX | Unity Particle System + 소수 직접 제작 시트 | 마켓 VFX |

### 방향 수 결정 (비용 최적화)
- 방치형 + 작은 오버레이 창: **4방향 + 미러(좌우 반전)** 이면 충분할 가능성이 높다. 8방향은 영웅 정도에만. 몬스터는 2~4방향.
- 아이소 2:1 타일에서 4방향(SE, SW, NE, NW)이 곧 대각 4방향이므로 4방향 + 미러는 쿼터뷰와 잘 맞는다.

## 6. 권장 스프라이트 규격 (작은 데스크톱 오버레이 기준, 제안값)
- 캐릭터: **64x64 캔버스** (몸 높이 약 48px), 영웅은 최대 96x96 보스급은 128x128. PixelLab 스켈레톤 애니 한계(128px)와도 맞음.
- 아이소 타일: **2:1, 64x32 px** (소형 창이면 32x16도 후보). 높이 있는 블록은 64x48 등 세로 확장.
- 아이템 아이콘: 32x32 (인벤토리 슬롯 표시 64x64로 정수 배 확대).
- 프레임 수: 대기 4, 걷기 6~8, 공격 6, 사망 6~8.
- 팔레트: 프로젝트 전역 32~48색 고정 팔레트, 외곽선 규칙 통일.
- Unity: PPU = 타일 가로폭(64), Point(no filter), 압축 None, Sprite Atlas, Pixel Perfect Camera 참조 해상도 정수 배율. 창 크기는 정수 배율 고정 권장.
- 위 수치는 이번 조사로 검증한 값이 아니라 설계 제안이다. 프로토타입 1종으로 확정할 것.

## 7. 사용자가 연결해야 할 것
1. **PixelLab 계정 + API 토큰** (구독 또는 API 크레딧). Claude Code에 원격 MCP로 등록: `https://api.pixellab.ai/mcp`, Authorization 헤더에 토큰.
2. (선택) **OpenAI API 키** (gpt-image-2, 컨셉/아이콘 초안).
3. (선택) **Aseprite 라이선스** (Steam $20 내외).
4. (3D 경로를 쓰면) **Meshy 또는 Tripo 구독**, **Adobe 계정(Mixamo, 무료)**, Blender(무료), SPRITE.EX/Pixor 중 하나(일회성).
5. (선택) Scenario 계정, Gemini API 키.
- 키/토큰은 사용자가 직접 발급·입력해야 하며 Claude Code는 계정을 만들지 않는다. 키는 저장소에 커밋하지 말고 환경변수/`.env`(gitignore)로 관리.

## 8. 월 예상 비용 (대략, 2차 출처 기반)
| 항목 | 월 비용 |
|---|---|
| PixelLab 구독/API (제작 집중기) | $12~50 |
| OpenAI gpt-image-2 (초안/아이콘, 수백 장) | $5~30 |
| Meshy/Tripo (3D 경로 사용 시) | $20~30 |
| Aseprite, SPRITE.EX/Pixor (일회성) | 합계 $20~60 일회 |
| 합계 (제작 집중기) | **약 $40~110/월**, 평시 $15~40 |

## 9. 위험과 대응
1. **일관성 붕괴**: AI 생성 스프라이트의 방향/프레임 간 디자인 드리프트. 대응: 캐릭터당 레퍼런스 고정, 3D 경로를 보험으로, 프로토타입 1종 선검증.
2. **Steam 공개 + 플레이어 반발**: AI 아트 표시만으로 부정 리뷰/보이콧이 생길 수 있음 (정량 근거는 이번 조사에서 확보 못 함). 대응: 스토어 페이지에 정직한 공개, AI 출력물을 Aseprite로 실제 수정(반복 정리) 후 사용, 핵심 영웅/키아트는 사람 손 비중 확대, 데모/위시리스트 반응 모니터링.
3. **저작권/라이선스 불확실**: AI 생성물의 저작권 보호 범위는 국가별로 불확실. 대응: 서비스별 약관 캡처 보관, 유료 플랜 사용(상업권), 생성 기록(프롬프트/모델/날짜) 보존.
4. **서비스 종속**: PixelLab 등 SaaS 가격/정책 변경. 대응: 완성 에셋은 PNG로 로컬 보관, 파이프라인 스크립트화.
5. **픽셀 정합성**: AI 이미지는 격자가 틀어짐. 대응: 후처리 스크립트(다운스케일, 팔레트 스냅), Unity Pixel Perfect.
6. **2차 출처 의존**: 가격/기능 상당수가 비교 사이트/디렉토리 기준. 대응: 도입 직전 공식 페이지 확인.

7. **Steam 반발 실제 사례 (2차 출처)**: 2026년 Next Fest 데모 약 1/5이 AI 공개 표시를 달았고, 표시가 판매에 불리하다는 분석도 있으나 방법론 미검증. AI 아트 때문에 게임을 내린 사례(Hardest), 반대로 AI를 안 썼는데 "AI slop"으로 오인받은 픽셀 RPG 사례(Shrine's Legacy)도 있음. 대응: 제작 기록(Aseprite 파일, 레이어, 타임랩스) 보관으로 오인 방어, 공개 문구 선제 작성, 늦은 공개는 역풍(Stackflow 사례 보도)이므로 처음부터 표시.

## 10. 다음 단계 제안
1. PixelLab 무료/최소 플랜으로 영웅 1종 8방향 + 걷기 애니 생성해 Unity에서 64px로 시험.
2. 같은 캐릭터를 3D→픽셀로도 1종 시험해 품질/시간 비교.
3. 둘 중 승자를 고정하고, 아이콘 대량 생산 스크립트(양자화) 작성.
4. Steamworks AI 공개 항목의 정확한 문구 확인.

## Sources
- Steam 반발 관련: https://kotaku.com/steam-next-fest-feb-2026-gen-ai-art-demos-2000673280 , https://indiecator.org/2026/06/16/steam-next-fest-has-an-ai-problem-and-players-cant-filter-it-out/ , https://spilled.gg/rpg-developers-ai-accusations-steam-reviews-game-content , https://finalboss.io/dev-will-delete-steam-game-hardest-over-ai , https://rogueliker.com/stackflow-late-ai-disclosure-reviews-backlash/ , https://games.gg/news/ai-content-labels-killing-game-sales/
- PixelLab API: https://www.pixellab.ai/pixellab-api
- PixelLab MCP docs: https://api.pixellab.ai/mcp/docs
- PixelLab v2 llms.txt: https://api.pixellab.ai/v2/llms.txt
- PixelLab MCP 디렉토리: https://lobehub.com/mcp/pixellab-pixellab-mcp , https://glama.ai/mcp/servers/@samedii/PixelLab
- PixelLab 리뷰(가격, 2차): https://knowara.com/ai-tools/image/pixellab-review/ , https://ai.g2.com/marketplace/tools/pixellab
- Retro Diffusion (itch.io): https://astropulse.itch.io/retrodiffusion , https://astropulse.itch.io/retrodiffusionlite
- Retro Diffusion 문서: https://astropulse.gitbook.io/retro-diffusion/aseprite-extension
- SpriteCook 비교(Retro Diffusion, Scenario): https://www.spritecook.ai/compare/retro-diffusion , https://www.spritecook.ai/compare/scenario
- Ludo.ai 비교: https://ludo.ai/compare/ludo-vs-scenario , https://ludo.ai/compare/best-ai-sprite-generators
- gpt-image-2 가격(2차): https://pricepertoken.com/image/model/openai-gpt-image-2 , https://picsart.com/api-platform/models/gpt-image-2/pricing , https://openrouter.ai/openai/gpt-image-2
- 3D→픽셀 도구: https://xionworld.itch.io/spriteex , https://alternativeto.net/software/pixor/about/ , https://sorceress.games/blog/3d-to-2d-render-your-3d-models-into-game-ready-2d-sprite-sheets
- Blender 아이소 튜토리얼: https://jsabbott.artstation.com/blog/YQaAw/tutorial-creating-2d-pixel-art-style-isometric-sprites-from-a-3d-model-in-blender
- Tripo 3D→2D 가이드: https://www.tripo3d.ai/blog/collect/transforming--d-models-into--d-pixel-characters--a-step-by-step-guide-kalxawsdyeo
- Steam AI 공개 개정: https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/ , https://gamingonlinux.com/2026/01/valve-tweak-steam-ai-disclosure-form-for-developers-to-clarify-its-for-content-consumed-by-players , https://finalboss.io/valve-s-2026-ai-disclosure-update-actionable-guide
