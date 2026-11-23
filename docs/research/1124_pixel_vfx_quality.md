# 고퀄 픽셀 아트 액션 게임의 VFX: 무엇이 좋게 보이게 하나 (1124 리서치)

표기: [확인]=페이지 본문을 직접 읽음, [요약]=검색 결과 요약문만, [기억]=기존 지식(재검증 필요), [저신뢰]=출처가 2차/자동생성 성격이라 수치를 참고만 할 것.
솔직한 한계: Hyper Light Drifter 개발 블로그, Dead Cells 애니메이션 GDC 발표, Sea of Stars 조명 기술 글은 이번에 "프레임 수/ms 수치"까지는 얻지 못했다. 얻은 수치는 아래에 출처와 함께 적었고, 못 얻은 것은 공백으로 두었다.

## 1. 질문: 이펙트 해상도를 스프라이트 픽셀 격자보다 높이면(믹셀/고해상도 이펙트) 도움이 되나 해가 되나

### 증거
- "믹셀"(서로 크기가 다른 픽셀이 섞임)은 커뮤니티 정설로는 초보자의 실수. 다만 이펙트에 한해서는 예외라는 글이 있다: 하트·분노 마크·땀방울·슬라이딩 먼지 같은 이펙트를 스프라이트보다 낮은 해상도(더 큰 픽셀)로 그려 본체와 분리하고 "픽셀 느낌"을 강화. Minecraft, Terraria, Stardew Valley에도 믹셀이 존재한다고 서술. 즉 "이펙트를 달리 하는" 선례는 **더 거친(큰 픽셀)** 쪽이다. [확인] https://yari-pixels.github.io/Articles/mixels.html
- 반대 방향(스프라이트보다 곱게) 사례는 "픽셀 격자 위에 부드러운 빛/블룸을 얹는" 형태가 주류: Sea of Stars는 "완전 동적 조명, 자체 렌더 파이프라인으로 2D 픽셀 아트의 한계를 밀어붙임"이라 공식 설명 [확인] https://sabotagestudio.com/presskits/sea-of-stars/ ; Eastward는 2D인데 3D 조명 시스템을 쓴다는 유저 분석 [요약] https://www.reddit.com/r/eastward/comments/n3fnf8/ . 블룸/글로우는 "밝은 영역을 추출→가우시안 블러→가산(additive) 합성"으로 정의되며 저해상도 미학을 유지하면서 후광을 준다. [확인(저신뢰 2차)] https://grokipedia.com/page/Glow_and_Bloom_Effects_in_Pixel_Art_Games
- 정수 스케일이 흐트러지면 픽셀이 불규칙해진다: 320x180을 x6 하면 1920x1080. 이펙트를 별도 해상도로 렌더하려면 이 정수배 관계를 지켜야 한다. [확인] https://notkey.studio/en/tutorials/choosing-the-right-render-resolution-for-a-pixel-art-game/ . CrossCode는 정수가 아닌 스케일에서 흐려지는 문제로 유저들이 해상도 값을 3x(1704x960)/4x(2272x1280)로 직접 수정. [요약] https://steamcommunity.com/app/368340/discussions/0/1637536330470565174/
- 픽셀 히트 FX 상용 에셋은 16px/32px 스프라이트에 맞춰 "두 가지 네이티브 크기로 각각 그려서(스케일 안 함)" 제공, 흔들림은 "정수 픽셀 오프셋만" 쓴다. [확인] https://yuzukikaku.itch.io/pixel-hit-fx-vol1
- Dead Cells: 캐릭터 높이가 게임 안에서 50px, 3D 렌더를 안티앨리어싱 없이 작게 뽑는다. 이펙트로 움직임/타격감/세기를 준다고 명시. [확인] https://www.gamedeveloper.com/production/art-design-deep-dive-using-a-3d-pipeline-for-2d-animation-in-i-dead-cells-i-

### 판정 (내 해석, 위 증거 범위 내)
- "스프라이트 격자보다 곱게(해상도 높게) 그린 형태가 분명한 이펙트(곡선 슬래시, 매끈한 원 폭발)"는 같은 화면의 스프라이트와 부조화로 읽혀 해가 된다는 쪽이 압도적 의견. 이번에 찾은 성공 사례 중 "고해상도 형태 이펙트"를 쓴 게임은 확인하지 못했다.
- 반면 "빛/블룸/조명/그림자/화면 후처리" 같은 형태 없는 부드러운 효과를 픽셀 위에 얹는 것은 Sea of Stars, Eastward가 쓴다. 이 둘은 서로 다른 문제이니 분리해서 판단할 것.
- 안전한 기본값: 이펙트의 "형태"는 스프라이트와 같은 픽셀 격자(또는 정수배로 더 큰 픽셀)에 스냅, "빛"은 격자 무관한 가산 블렌드/블룸/조명으로.

## 2. 무엇이 더 중요한가 (수치 모음)

### 2-1. 타이밍 구조 (예고-충격-소산)
- Dead Cells: 포즈 투 포즈로 키프레임만 쓰고 "보간 프레임은 키프레임 앞/뒤에만, 중간에 넣지 않는다", 대신 VFX로 움직임·충격·힘을 표현. 프레임을 normal map과 함께 내보내 blend mode를 얹을 수 있다. [확인] 위 Gamedeveloper
- 같은 팀 인터뷰(80.lv): "파티클, 스톱프레임, 슬로다운을 많이 쓴다. 치명타는 게임을 1프레임 멈춘 뒤 "몇 tenths of a second"(0.몇 초) 슬로다운, 핏방울, 전용 타격음. 이 합이 좀비 두개골에 큰 칼을 꽂는 무게감을 만든다." [확인] https://80.lv/articles/interview-with-the-developers-of-dead-cells
- Celeste: 대시는 15프레임이며 그중 3프레임이 프리즈. [확인(Celeste Wiki 요약문)] https://celeste.ink/wiki/Tech

### 2-2. 히트 스톱 (ms)
- 격투/액션 일반: 일반적으로 2~5프레임(60fps에서 32~80ms). [요약] https://www.reddit.com/r/gamedev/comments/5dfdr6/
- 가벼운 잽 33ms ~ 무거운 크리 110ms (계산기 도구 설명). [요약] https://camberlabs.github.io/tools/hitstop-freeze-frame-impact-duration-calculator/
- 상용 히트FX 에셋의 기본: "한 호출로 0.06초 프리즈 + 카메라 흔들림 + 적 흰색 플래시 + 데미지 숫자 + 콤보". 더 짧은 정지가 더 긴 정지를 끊지 않는다(큐 규칙). 히트 프로파일 표(경/중/중/크리/피니시)를 데이터로 관리. [확인] https://yuzukikaku.itch.io/pixel-hit-fx-vol1
- 개발자 실측 글: 0.05~0.1초가 적당, 너무 짧으면 안 느껴지고 길면 렉으로 오인. 플래시 50~100ms, 효과음 동기 오차 12ms 이내(<15ms 일 때 확인 속도 22% 향상이라는 인용 — 출처 자료는 확인 못함). 순서: 진동+소리 동시 → 플래시(20ms) → 파티클(50ms) → 떠오르는 텍스트(100ms). [확인(2차, 인용 신뢰도 낮음)] https://eastondev.com/blog/en/posts/dev/20260521-game-feedback-feel/
- 히트 스톱은 "공격자와 피격자만" 멈추는 방식도 있음(전체 정지 아님). [확인] https://onlyfarms.gg/wiki/general/hit-stop-in-gaming
- 참고용 [저신뢰] GitHub 문서(자동 생성 성격으로 보여 수치 출처 불명): 가벼운 2~3프레임, 무거운 5~8프레임, 치명 8~12프레임, Celeste 대시 2프레임(33ms)·±3px, Hades 3프레임·±5px 등. https://github.com/raduacg/game-mechanics-optimizations/blob/main/G_05_screen_effects.md . 위의 [확인] 수치와 어긋나는 값(예: Celeste 3프레임 vs 2프레임)이 있어 그대로 쓰지 말 것.

### 2-3. 화면 흔들림
- Vlambeer "The Art of Screenshake"(Jan Willem Nijman, INDIGO Classes 2013, 약 45분): 지루한 슈터에 30가지 작은 수정을 더하는 시연, 예로 총 쏜 반대 방향으로 화면 살짝 흔들기, 적이 33% 확률로 해롭지 않게 폭발 등. [확인] https://www.gamedeveloper.com/design/vlambeer-co-founder-shares-advice-on-building-better-action-games
- 그의 공식(트윗): 매 프레임 `shake -= 1`(0 이하면 0), `shake > 10`이면 `shake *= 0.9`, 오프셋 = `random(shake) - shake/2` (x,y 각각). PICO-8 저해상도 버전은 -0.5. [확인] https://www.ladybenko.net/notes/screenshake/
- 흔들림은 정수 픽셀 오프셋으로, 강한 것이 약한 것을 이긴다, 방향성(넉백 방향)·ease-out 감쇠. [확인] yuzukikaku
- 접근성: Celeste는 스크린셰이크 끄기 옵션이 있고 끄면 7-C가 쉬워졌다는 유저 보고(타이밍 방해). [요약] https://www.reddit.com/r/celestegame/comments/9fwv5z/

### 2-4. 프레임 수 / fps
- 못 얻음: Hyper Light Drifter의 이펙트 프레임 수·fps 공식 수치, Diablo 2 이펙트 프레임 수는 이번에 본문 확보 실패. D2 스프라이트는 DCC/DC6 형식이며 레이어별 "draw effects"(투명/가산 등)를 합성한다는 것만 도구 문서로 확인 [요약] https://github.com/RoofooEvazan/pd2-sprite-studio . D2가 25fps 애니메이션이라는 것은 [기억].
- HLD 제작자 인터뷰: 처음엔 HD 일러스트풍으로 기획했지만 혼자서는 비현실적이라 저해상도 픽셀 아트로 전환(제작 효율). [확인] https://www.gamedeveloper.com/business/the-ultra-modern-stylings-of-hyper-light-drifter
- Saint11(Pedro Medeiros) 튜토리얼 모음: 공격/펀치, 피 튀김, 부서짐, 총알, 폭발 등 항목별 512x512 애니메이션 GIF. "폭발은 불과 연기가 많아 따로 튜토리얼이 있다". 프레임 수는 이번엔 이미지를 읽지 않아 미확인. [확인/요약] https://saint11.art/blog/pixel-art-tutorials/ , https://www.patreon.com/saint11/posts/explosions-part-7719254

### 2-5. 가산 블렌드 / 블룸 / 파티클
- 가산 블렌드 스프라이트시트가 검정 네모로 나오는 문제는 "가산 블렌드용으로 내보내거나 진짜 알파로 내보낸 뒤 엔진에서 블렌드 모드 설정"으로 해결. [요약] https://www.particlefx.studio/guides/additive-blending-black-background
- Noita는 픽셀 물질 시뮬레이션(셀룰러 오토마타) 그 자체가 이펙트. [요약] https://www.reddit.com/r/gamedev/comments/d93op6/
- 이번에 확인 못한 것: Moonlighter 2D 조명 기법, Blasphemous 이펙트/조명 분석, CrossCode 이펙트 해상도 정책(검색 결과가 도구/팬 글뿐이었음).

## 우리 게임에 적용할 것
1. 이펙트 형태는 스프라이트와 동일 픽셀 격자에 스냅하고, 피 튀김·먼지 같은 보조 이펙트는 스프라이트 픽셀의 2배 크기까지 허용(믹셀 선례: 거친 쪽만). 곡선 위주의 고해상도 슬래시/폭발 텍스처는 쓰지 않는다.
2. 빛은 격자 밖에서 해결: 스킬 광원 = 가산 블렌드 글로우 스프라이트 + 약한 전역 블룸 + 2D 라이트(Unity URP 2D). 형태 있는 이펙트와 빛을 별도 레이어로 관리하고 블룸 임계값을 높여 이펙트 코어만 번지게.
3. 임팩트 3단 표준 테이블 한 곳에서 관리(히트 프로파일): 일반 = 히트스톱 1~2프레임(약 16~33ms)+흰 플래시 1~2프레임, 강타 = 3~4프레임(약 50~67ms)+흔들림 2~3px, 치명/마무리 = 5~6프레임(약 83~100ms; 근거: 상용 에셋 기본 60ms, 실측 글 50~100ms, Dead Cells 치명 1프레임+슬로다운)+흔들림 3~5px+0.1~0.3초 슬로모. 방치형은 적이 많아 겹치므로 "짧은 정지는 긴 정지를 끊지 않는다" 규칙과 동시 발동 상한을 둘 것.
4. 흔들림 공식은 Vlambeer 방식(감소량 1/프레임, >10이면 x0.9)을 정수 픽셀로 양자화, 방향성 흔들림, 설정에서 "화면 흔들림 강도 0/50/100%".
5. 예고-충격-소산 3단: 큰 스킬은 선행 예고 2~4프레임(수축/차징 빛) → 충격 1~2프레임(최대 밝기, 흰/가산) → 소산 6~10프레임(연기/잔불/파편이 지연되며 흩어짐). Dead Cells식으로 "본체 애니는 키 포즈 위주, 느낌은 이펙트로".
6. 파편/먼지/불똥 같은 보조 파티클은 풀링하고 격자 스냅, 색은 팔레트 3~4단계(밝은 코어-본색-어두운 가장자리)로 제한해 화면 소음을 줄인다.
7. 소리 동기 오차 12~15ms 이하 목표(검증 필요한 2차 인용이므로 "프레임 단위로 충격 프레임에 SFX 트리거"로 구현).
8. 검증 방법: 1080p 전체 화면 빌드로 녹화해 확인(에디터에서는 분홍 조명/배율 문제가 안 보였다는 기존 메모 유지).
9. 추후 재조사 필요: HLD 개발 블로그, Dead Cells 발표 영상(Thomas Vasseur), Saint11 폭발 GIF의 프레임 수를 직접 세어 목록화.
