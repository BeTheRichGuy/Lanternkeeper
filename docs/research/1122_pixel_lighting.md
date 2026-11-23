# 픽셀 아트를 "고급스럽게" 보이게 하는 조명 기법 조사와 등불지기 적용안 (2026-11-22)

목적: Unity 6 URP 2D(Renderer2D) 아이소 픽셀 ARPG에서, 픽셀의 선명함을 잃지 않으면서 Octopath Traveler(HD-2D)·Children of Morta·Eastward·Hyper Light Drifter·Diablo II Resurrected 같은 "고급스러운" 인상을 내는 조명 기법 6가지를 조사하고, 우리 프로젝트에서 무엇을 먼저 지을지 순위를 매긴다.

표기: **[출처]** 공개 문서·개발자 글(링크는 끝의 목록) · **[확인]** 우리 저장소·설정에서 직접 읽은 값 · **[추론]** 위 둘로부터 내가 판단한 것(빌드로 검증 필요) · **[미확인]** 찾아봤지만 근거를 못 찾음.

기존 문서(`2026-11-21-modern-pixel-rendering.md`, `d4-visual-techniques.md`)가 이미 다룬 것(HDR 켬, 레이어별 전역 광, 바닥 채도 셰이더, 막별 LUT, 색 비네트, 바닥 안개)은 반복하지 않고 그 위에 무엇을 더할지에 집중한다.

---

## 0. 요약 (먼저 읽을 것)

1. **우리 현재 상태 [확인]**: Unity 6000.6.9f1, URP 17.6.0, HDR 켬(URP 에셋), 기준 해상도 960x540, PPU 64, PixelPerfectCamera 픽셀 스냅. 조명은 전부 런타임에 만든 `Light2D`(전역 앰비언트 1개 + Sprite 타입 점광 다수), 렌더 텍스처 배율 0.5, `ShadowCaster2D` 없음, 노멀맵 없음, 발광 마스크 없음. `ActorSprite.shader`가 1텍셀 외곽선과 위쪽 림을, `GradedLit.shader`가 바닥/벽 채도·감마 보정을 이미 한다(노멀·마스크 슬롯은 있지만 비어 있음).
2. **가장 싸고 효과 큰 것은 "발광 마스크 + HDR + 블룸"** (용암·눈·룬). HDR이 이미 켜져 있으니 셰이더 한 곳을 고치면 된다. 새 아트가 필요 없는 색 키(color key) 방식까지 있다.
3. **노멀맵은 "고급스러움"을 가장 크게 올리지만 위험도 가장 크다.** 저해상도 아이소 타일에서 노멀맵은 큰 면만 의미가 있고, 미세한 디테일은 노이즈가 된다(The Siege and the Sandfox 사례). PixelLab에 `create_normal_map`(kind=iso, lite 0.1 생성/호출, 애니메이션은 스트립 한 장에 0.1)이 있어 아트 비용은 낮다. **소품·벽·보스에 먼저, 바닥은 마지막에.**
4. **림 라이트는 이미 절반 해결.** 지금은 "위쪽 가장자리"만 밝히는데, 가까운 광원 방향과 색을 따라가게 만들면 노멀맵 없이 가장 큰 개선이 된다. 추가 텍스처 읽기가 없다(이미 4방향 알파를 읽는다).
5. **2D 그림자(ShadowCaster2D)는 우리 구조에선 후순위.** 아이소 쿼터뷰에서 화면 평면 그림자가 지면 그림자와 맞지 않고, 런타임 생성은 비공개 필드 리플렉션이 필요하며, 점광이 수십 개라 광원마다 그림자를 켜면 비용이 크다. 대신 **블롭 그림자·접촉 AO 스프라이트**가 싸고 확실하다.
6. **열기 왜곡·볼류메트릭**: Light2D에 내장 `Volumetric` 옵션이 있다(문서 확인). 열기 왜곡은 `_CameraSortingLayerTexture` 또는 `ScriptableRendererFeature2D` 한 장짜리 패스로 가능하고, **왜곡 오프셋을 정수 텍셀로 양자화하지 않으면 픽셀이 뭉개진다.**
7. **권장 순위**: ① 발광 마스크 ② 방향성·유색 림 ③ 블롭 그림자/접촉 AO ④ 소품·벽·보스 노멀맵 ⑤ Light2D 볼류메트릭 ⑥ 열기 왜곡 ⑦ 조명 양자화(디더) 실험 ⑧ ShadowCaster2D(영웅 등불 1개만).

---

## 1. 노멀맵 (픽셀 아트 스프라이트·타일)

### 1.1 무엇을 이루는가
평평한 스프라이트가 광원 방향에 따라 명암이 바뀌어 "돌이 튀어나오고, 균열이 파인" 입체감이 생긴다. 불꽃이 깜빡일 때 돌 질감이 같이 움직이는 것이 HD-2D·Children of Morta류의 "비싸 보이는" 느낌의 핵심 중 하나다. 단, 이것이 Octopath가 쓴 방법이라는 근거는 못 찾았다(아래 1.5).

### 1.2 어떻게 만드나 (업계 방식)
- **손으로 그리기가 기준이다.** The Siege and the Sandfox는 "노멀 돔" 팔레트와 RGB 채널별 칠하기로 직접 칠했고, 전경·배경 타일셋만 3인월이 들었다. 자동 도구(NDo, CrazyBump, AwesomeBump)는 일부 도형에만 도움이 됐고, 자동 변환은 손으로 칠한 것보다 부정확했다. [출처 1]
- **자동 생성 비교 논문(2022)**: 색 맵에 Sobel을 적용하면 가장자리와 음영을 형태로 오독하고, 학습 모델은 비픽셀 이미지로 훈련돼 약하고 흐린 노멀을 낸다. **높이맵에서 Sobel**, **4방향 조명(Sprite Lamp 방식)**이 손 작업에 가장 가까웠다. 자동 기법 중 손으로 칠한 것과 맞먹는 것은 없었다. [출처 2]
- **도구**: Laigter(무료·오픈소스, 커맨드라인 `laigter.exe -d <경로> -n` 지원, 픽셀 아트는 낮은 거리·높이·부드러움 권장), SpriteIlluminator(유료), Sprite Lamp. Laigter는 이미지의 실제 픽셀에서 작동하므로 확대된 아트는 한 "도트"가 9픽셀에 영향을 준다. [출처 3]
- **Unity 자체 기능**: 가져올 때 Normal Map + "Create from Grayscale"로 높이맵에서 생성. [출처 4, 5]
- **우리 도구 [확인]**: PixelLab MCP `create_normal_map`. 입력 `kind`에 `iso`가 있고("isometric tiles and objects"), lite 모드는 0.1 생성/호출이며 **애니메이션 32프레임을 한 호출·한 번 비용으로 스트립으로 돌려주고 한 계획에서 만들어 프레임 간 조명이 안정적**이라고 문서에 적혀 있다. 등불지기의 `create_normal_map` 결과가 실제 품질이 어떤지는 **[미확인]** — 샘플 5~6장으로 먼저 보아야 한다. `surface_detail`(그림의 명암을 범프로 읽음)을 낮추고 `part_shapes`를 올리면 "깨끗한 플랫 셰이딩 아트"에 맞는다고 문서가 안내한다.

### 1.3 저해상도에서 어떻게 보이나
- 32x32 타일에서 미세 디테일 노멀은 노이즈였고, 큰 형태를 조각하는 용도로 바꿔 해결했다. **32x32보다 작은 타일은 입체를 전달할 픽셀이 부족하다.** 깎아낸 각진 면이 부드러운 그라데이션보다 낫고, 형태를 약간 두툼하게 그려야 픽셀마다 뚜렷한 각도를 갖는다. [출처 1]
- 자동·빠른 노멀은 약하게 보이고, 픽셀 아트에 원치 않는 번들거리는 금속 느낌을 줄 수 있다. 작은 샘플로 먼저 시험하라고 권한다. [출처 1]
- **[추론]** 우리 PPU 64, 기준 960x540 기준으로 아이소 바닥 타일(대략 64x32 또는 128x64)은 위 경계선 근처이거나 아래다. 바닥은 평평해서 어차피 노멀이 한 방향이므로 얻는 것이 적고, **얻는 것이 큰 쪽은 벽·기둥·무덤·항아리·보스·영웅**이다.

### 1.4 Unity 6 URP 2D 설정 (공식 문서 기준)
1. 노멀맵 임포트: Texture Type = **Normal Map**, **sRGB 해제**. 마스크 맵은 Default. 스프라이트와 **UV가 1:1로 정렬**돼야 한다(같은 UV로 샘플링). [출처 5]
2. 스프라이트 에디터의 **Secondary Textures** 탭에서 `_NormalMap`(노멀) / `_MaskTex`(마스크)로 연결. 재질은 `Sprite-Lit-Default` 같은 지원 재질. [출처 5]
3. Light2D의 **Normal Map Quality**를 `Fast`/`Accurate`로 켠다(Global 타입은 불가). `Accurate`는 느리고, `Fast`는 아티팩트가 생길 수 있다. **작은 광원·큰 Distance일수록 둘의 차이가 줄어든다.** **Normal Map Distance**는 광원과 스프라이트 사이의 높이를 정한다(작을수록 광원이 가깝고 디테일을 날카롭게 비춘다). [출처 5, 6]
4. 비용: 노멀맵이 있으면 **레이어 배치마다 전체 크기 렌더 텍스처에 깊이 선행 패스**가 추가돼 성능이 떨어질 수 있다고 공식 문서가 명시한다. [출처 5] 문서에 수치는 없다 — 2D Graphics Profiler 모듈과 Frame Debugger로 직접 재야 한다. [출처 7]
5. **우리 파이프라인에서의 연결 [확인 + 추론]**
   - 스프라이트는 편집기의 `ArtImporter`가 가져와 `SpriteCatalog`(ScriptableObject)에 담는다. 그러므로 임포트 시점에 세컨더리 텍스처를 붙이는 것이 가장 자연스럽다(에디터 API는 `ISpriteEditorDataProvider` 계열의 secondary texture provider로 기억하지만 **정확한 이름은 확인 필요**). 런타임에 `Sprite.Create`로 만드는 쪽은 secondary texture 인자를 받는 오버로드가 있다고 기억한다(**확인 필요**).
   - `GradedLit.shader`는 이미 `_NormalMap`을 읽어 `InitializeSurfaceData`에 넘기고 있어 바닥/벽은 노멀만 붙이면 동작한다. 기본값 `"bump"`는 평평한 노멀이다.
   - **우리 광원은 전부 `Light2D.LightType.Sprite`다 [확인].** 공식 Light 2D 레퍼런스는 노멀맵 옵션이 "Global이 아닌" 타입에서 쓸 수 있다고 적었고, 별도 Unity 글은 노멀맵이 Spot/Point/Freeform에서 동작한다고 적었다. 두 문서가 Sprite 타입을 명시적으로 보증하지 않으므로 **Sprite 타입 광원이 노멀맵 명암을 내는지 첫 실험 항목**이다. 안 되면 불꽃 소품은 Spot(Point) 타입으로 바꾸면 되지만 광원 모양(2:1 타원)을 잃는다 — 아래 함정 참조. [출처 5, 6, 8]

### 1.5 함정 (출처 있는 것과 추론 구분)
- **임포트 타입 누락이 1위 원인**: "광원이 움직여도 명암이 안 변한다"는 Unity 6 사례는 모두 노멀맵 Texture Type 미설정이었다. [출처 9] 또 "디버그 시 조명이 안 먹는다"는 보고는 거의 Normal Map 타입 문제로 종결됐다. [출처 10]
- **Y(초록) 채널 반전**: Unreal과 Unity는 Y 규약이 다르다. Unity는 OpenGL 규약(초록=위). 도구 설정에서 맞춘다(PixelLab 기본 `opengl`). [출처 1, 11]
- **타일 뒤집기**: 청크 모드 Tilemap에서 타일을 뒤집으면 노멀 초록 채널이 반전되는 보고가 있다(개별 렌더러 모드에서는 없음). [출처 12 — 포럼 보고 1건, 가볍게 취급] 우리는 아이소 벽을 좌우 반전으로 재사용하면 해당될 수 있다. **[추론]** 반전 재사용 시 노멀 R을 반전한 별도 에셋을 두거나 뒤집지 않는다.
- **평평한 아이소 바닥**: 바닥 타일이 비평탄 노멀을 가지면 광 텍스처가 매끄럽지 않아 문제가 생긴다는 개발자 글이 있다. [출처 13] **[추론]** 바닥은 (0,0,1) 평탄 노멀로 두고 균열·이음새만 약하게.
- **광원 모양 비율**: URP 2D 광원은 2:1 아이소 투영에서 감쇠가 1:1로 남는다는 오래된 스레드가 있고, 타원 스프라이트를 Sprite 타입 광원으로 쓰는 해법이 나왔다. [출처 14] 우리는 이미 Sprite 타입 광원을 쓰고 있어 이 문제를 우회해 왔을 가능성이 크다 — 노멀맵 때문에 Spot으로 바꾸면 이 이점을 잃는다.
- **깜빡임·번쩍임 광원 + 노멀맵 = 명암이 기어다닌다 [추론]**: 우리 소품 광원은 강도를 흔든다(`Prop.Flicker`). 노멀맵이 붙은 표면은 강도 변화에 더해 명암 경계가 요동쳐 "픽셀이 꿈틀거린다"고 보일 수 있다. 노멀이 붙는 소품의 깜빡임 진폭은 줄이거나, 위치 흔들림은 0으로 한다.
- **렌더 스케일 0.5 [확인]**: 광 텍스처가 반해상도이므로 광원 경계 자체는 이미 부드럽다. 노멀 명암은 픽셀 단위로 나오므로 부드러운 빛 위에 날카로운 면 명암이 얹히는 구성이 된다. 오히려 픽셀 아트에 어울리는 조합일 수 있다 **[추론]**.
- **휘발성 기대치**: Octopath가 노멀맵을 썼다는 직접 근거는 찾지 못했다. 확인된 것은 "UE4에서 3D 환경 안에 픽셀 스프라이트를 놓고, 빛이 물체에 닿는 방식과 그림자 지는 방식을 강조했다"까지다. [출처 15, 16] Eastward도 개발사 인터뷰에서 노멀맵 사용을 확인하지 못했다(관람자 추측만 있다). [출처 17] **"이 게임들이 노멀맵으로 한다"고 단정하지 말 것.**

### 1.6 비용·위험 평가
- 아트 비용: 낮음(PixelLab 0.1 생성/호출) — 단, 품질 확인 전에는 불확실. 손 보정 필요 가능.
- 성능: 깊이 선행 패스 추가(수치 미공개, 실측 필요). Accurate보다 Fast를 기본으로.
- 위험: 위 Sprite 타입 광원 호환, 반복 재사용 타일의 뒤집기, 깜빡임 + 노멀 상호작용.
- **추천 도입 순서**: ① 소품 3종(횃불대, 무덤, 항아리)과 보스 1체에 노멀 → 빌드에서 1080p 녹화 ② 괜찮으면 벽 → ③ 바닥은 마지막(보람이 가장 작음).

---

## 2. 림 라이트 / 외곽선 (어두운 배경 위 캐릭터)

### 2.1 무엇을 이루는가
어두운 배경에서 캐릭터 실루엣이 떨어져 보이고, 광원 쪽 가장자리가 빛나 "빛 안에 서 있다"는 인상이 생긴다.

### 2.2 업계 사례와 근거의 한계
- **Hades 방식은 확인하지 못했다 [미확인].** Supergiant의 림 라이트·조명 구현을 설명하는 GDC·포스트모템 자료를 찾지 못했다. 확인된 것은 "모델을 검정으로 윤곽을 잡고 음영·하이라이트 층을 쌓는다"는 2차 자료 한 건뿐이다. [출처 18] 그러므로 "Hades 접근"을 근거로 설계하지 않고, 아래의 일반 기법을 쓴다.
- **xDasher(픽셀 게임)**: 셰이더가 스프라이트의 가장 바깥 픽셀을 찾아 그 픽셀이 받는 빛을 계산해 움직일 때 테두리가 번쩍이게 했다. 또한 "그려 넣은 그림자"를 가장 효율적인 2D 조명이라 평했고, 노멀맵은 파괴 가능한 환경 조각에만 썼다. [출처 19]
- **Unity 공식 글**: 마스크 맵의 R 채널을 캐릭터 림에, G 채널을 소품에 쓰면 각각 별도 광원 묶음이 영향을 줄 수 있고, 림은 프레넬 같은 하이라이트로 실루엣을 환경에서 분리하며 탑다운에서 특히 중요하다고 한다. [출처 8]
- **노멀맵 없는 캐릭터 조명 기법(Game Developer)**: (a) 머리~발 수직 그라데이션 틴트(주변광 차폐 흉내), (b) 알파 마스크를 흐려 "높이장"을 만들고 그 기울기를 노멀로 삼아 N·L을 누적. 효과는 "꽤 미묘"하고 가산 합성은 밝은 픽셀에서 거의 안 보인다고 저자가 적었다. [출처 20]
- **Nectarmancer**: 셰이더에서 가장자리를 찾으면 텍스처 읽기가 추가 8번 든다. 그래서 편집기에서 가장자리를 미리 구워 4채널로 방향 정보를 담았다. 방향 없는 1비트 마스크는 위·아래에서 오는 빛에 틀린 결과를 준다. [출처 21]

### 2.3 우리 셰이더에서의 구현 (URP 2D, 이미 있는 코드 기준)
**[확인]** `ActorSprite.shader`는 이미 4방향 알파를 읽어 외곽선과 "위쪽 림"(`up` 알파가 비고 자신이 불투명한 텍셀)을 만든다. 따라서 아래는 **추가 텍스처 읽기 없는** 확장이다.
1. 위 방향 하나만 보던 림을 4방향(+대각선은 선택)으로 나눠 각 방향의 "비어 있는 이웃" 여부를 구한다. 이웃이 빈 방향 벡터 합 = 그 텍셀의 바깥쪽 법선 근사(2D).
2. 근처 가장 밝은 광원의 **방향 벡터와 색**을 CPU(`LightRig`)에서 계산해 배우 단위로 전달한다. 개별 재질 속성은 SRP 배처를 깨므로 **`MaterialPropertyBlock`보다 정점 색(vertex color)·전역 셰이더 변수** 쪽이 낫다 **[추론]**(현재도 정점 색으로 틴트·낙차 전달 중). 몬스터가 수십~수백이면 "가장 가까운 광원" 1개만 쓰고 CPU 비용(`Graveyard Keeper`는 가장 가까운 광원 계산을 Update에서 하며 부담이 컸다고 적었다 [출처 22])을 제한한다.
3. 림 강도 = `saturate(dot(바깥쪽 법선, 광원 방향))` × 광원 세기 × 거리 감쇠. 색은 광원 색(불꽃이면 주황, 마법진이면 보라). 림 값을 **HDR(>1)** 로 내면 블룸이 아주 얇게 번져 "빛나는 가장자리"가 된다 — 단 임계값 위의 픽셀이 1텍셀 폭이면 이동 시 블룸이 깜빡이므로 **림 HDR 증폭은 약하게**(1.2~1.5)부터 **[추론]**.
4. 노멀맵이 생기면 같은 림이 마스크 맵 R로 대체될 수 있지만, 당분간 **알파 이웃 방식이 아트 비용 0**이다.
5. **블룸 결합 주의**: 블룸은 축소 버퍼에서 흐려지므로 픽셀 완벽 렌더링이어도 블룸 자체는 흔들릴 수 있고, 저해상도 배율에서는 같은 설정으로 더 밝아 보이는 보고가 있다. [출처 23] 우리는 PixelPerfectCamera 위에서 블룸을 쓰므로 **림 글로우는 작게, 발광은 크게** 나눠 설계한다.

### 2.4 비용·위험
- 비용: 이미 있는 4번의 알파 읽기를 재사용 → 추가 비용 거의 0. CPU 쪽 "가장 가까운 광원" 계산만 분할 갱신.
- 위험: 광원이 많은 구역에서 림이 어느 광원을 따를지 튀는 현상(가장 가까운 광원이 바뀔 때). 가중 평균으로 부드럽게.
- 접근성: 림 색이 몬스터 계열 식별 색(D4 문서 §2)과 충돌하지 않게 광원 색을 따르되 채도 상한을 둔다.

---

## 3. 2D 그림자 (ShadowCaster2D · 대안)

### 3.1 Unity URP 2D 그림자의 사실
- **렌더 순서**: 그림자 모양을 그림자 텍스처에 그리고 → 각 광원의 색·모양을 광 텍스처에 그리고 → 조명된 오브젝트를 화면에 그린다. [출처 24]
- **Shadow Caster 2D 속성**: Casting Source(Sprite Renderer/Collider 2D/Shape Editor 등), Casting Option(Self/Cast/Both), Target Sorting Layers, Trim Edge(기본 0.2), Alpha Cutoff(기본 0.1). [출처 25] 여러 오브젝트의 그림자를 합치려면 부모에 **Composite Shadow Caster 2D**를 붙인다. [출처 26]
- **Light 2D 그림자 옵션**: Strength, Softness, Falloff Strength(높을수록 가장자리가 부드럽다). [출처 6] Unity 6에서 소프트 섀도가 추가됐다고 Unity 글이 적었다(광원에서 멀수록 흐려진다). [출처 8]
- **최적화 권고(공식)**: 그림자 텍스처 수를 줄이려면 Light Batching Debugger(Window > 2D)로 정렬 레이어 배치를 병합하고, 광원·그림자 캐스터의 Target Sorting Layers를 맞춘다. 블렌드 스타일은 종류마다 광 렌더 텍스처가 하나씩 생기므로 **한두 개가 모범 사례**. Render Scale로 광 텍스처를 줄일 수 있다. Max Light Render Textures를 올리면 한 번에 사전 렌더하지만 메모리가 는다. [출처 27] Unity 글: "그림자를 만드는 광원 수와 화면 위 블렌드 스타일 전환을 최소화하라(둘 다 눈에 띄는 비용)". [출처 8]
- **런타임 생성 [출처 28 — 커뮤니티]**: `shapePath`는 읽기 전용이라 런타임에 모양을 넣으려면 `m_ShapePath`·`m_ShapePathHash` 비공개 필드를 리플렉션으로 쓴 사례가 있다. 내부 이름이 버전마다 바뀔 수 있어 위험하다. 타일맵은 `CompositeCollider2D` 경로마다 자식 `ShadowCaster2D`를 만드는 패턴이 흔하고, 한 타일맵에 +200 오브젝트가 생겨 상한을 둔 사례가 있다. 성능 보고 하나는 "전역 광이 주된 문제였다"고 했다. 공식 수치는 없다.
- **내부 근거 없음**: "수백 개의 벽에서 ShadowCaster2D가 얼마나 드는가"에 대한 공개된 정량 자료는 찾지 못했다 **[미확인]**. 프로파일링 외에 방법이 없다.

### 3.2 우리 구조와의 궁합 [확인 + 추론]
- 현재 `Max Shadow Render Textures`=1, 그림자 광원 없음. 소품 광원이 Sprite 타입으로 다수 생성·재활용된다(`LightRig.AddProp`).
- 아이소 쿼터뷰에서 ShadowCaster2D 그림자는 **화면 평면에서 광원 반대편으로 뻗는다.** 벽의 "발자국(바닥 접촉 다이아몬드)"을 캐스터로 만들면 바닥에 퍼지는 그림자처럼 보이지만, 높이가 있는 물체의 그림자 길이 표현은 어색하다 **[추론]**. 카메라 각도가 고정이라 눈속임이 가능한지는 직접 봐야 한다.
- 점광 수십 개 × 그림자 = 광원마다 그림자 텍스처 비용 **[추론: 공식 "그림자 광원 최소화" 권고에서 따름]**. 따라서 그림자를 켤 광원은 **영웅의 등불 1개**(+ 보스 방 큰 광원 1개)로 제한한다.

### 3.3 대안 (권장)
1. **블롭 그림자**: 배우 발밑에 어두운 타원 스프라이트(곱셈 또는 알파). Unity 글의 "음의 조명(Multiply 블렌드)·블롭 그림자(흐린 스프라이트를 늘이고 회전)"가 같은 접근. [출처 8] 아이소에서는 타원이 2:1로 납작해야 한다.
2. **광원 방향으로 회전하는 그림자 스프라이트**: Graveyard Keeper는 물체마다 최대 4개의 그림자(태양 1 + 가까운 동적 광원 3)를 회전하는 스프라이트로 그렸고, 회전을 정점 셰이더에서 하는 것이 빨랐다. 단점은 물체마다 조정이 필요하고 울타리 같은 긴 물체는 별로라는 점. [출처 22] 소품은 몇 가지 일반 그림자 스프라이트(약 10종)를 재사용했다.
3. **접촉 AO(손으로 칠한 어두운 가장자리)**: xDasher는 그림자를 스프라이트에 직접 칠하고 발밑 드롭 섀도를 더했으며 "가장 효율적인 2D 조명"이라 했다. [출처 19] **[추론]** 벽-바닥 접점에 짙은 그라데이션 데칼을 깔면 이미 있는 타일을 다시 그리지 않고 입체감이 난다.

### 3.4 위험
ShadowCaster2D는 비공개 API 의존(리플렉션)·광원당 비용·아이소 불일치가 겹친다. **가장 마지막에, 영웅 등불 1개에만, 벽 발자국을 합성(Composite)해** 시험한다.

---

## 4. 발광 마스크 / 자체 발광 픽셀 (용암·눈·룬)

### 4.1 무엇을 이루는가
조명과 무관하게 밝은 픽셀(눈, 룬, 용암 균열)이 어둠 속에서도 빛나고, 블룸으로 번진다. 어두운 분위기 + 선명한 픽셀이라는 대비를 만드는 가장 직접적인 방법이다.

### 4.2 Unity 사실
- 기본 `Sprite-Lit-Default`는 발광을 지원하지 않아 커스텀 셰이더가 필요하다. Unity Learn 튜토리얼은 **Secondary Textures로 발광 맵을 스프라이트에 연결**하고, 발광 맵 × 발광 색으로 빛나는 영역을 정한다. [출처 29] Unity Learn "2D Lighting for Pixel Art" 코스에도 "Light 2D 없이 셰이더 Emission으로 스프라이트를 빛나게 하기" 항목이 있다. [출처 30]
- Shader Graph의 Sprite Lit에 Texture2D 속성을 추가해 Fragment 입력에 연결하는 방식이 공식 안내. 단 문서의 `_MaskTex`는 "보임/숨김 마스크" 설명이 있어 발광 입력은 우리가 따로 더해야 한다. [출처 31]
- **알려진 한계**: 포럼에는 "보조 텍스처 + 자체 셰이더로도 제대로 된 발광이 안 된다. 예시는 가산 조명 합성이라 발광이 여러 스프라이트에 쌓이면 어색하고 장면 조명에 묶인다"는 불만이 있다. [출처 32] 해법은 **발광을 조명 합성 뒤에 더하는 것**(아래).
- 마스크 맵 R 채널은 블렌드 스타일 `Multiply with Mask (R)` / `Additive with Mask (R)`에서 "조명을 받을 영역"을 정한다(R>0). [출처 5, 6] 그러므로 **발광용은 R이 아닌 별도 텍스처**나 G/B/A 채널을 쓰는 편이 충돌이 없다 **[추론]**.

### 4.3 우리 구현안 (HDR이 이미 켜져 있음 [확인])
`GradedLit.shader`의 `LitFragment`는 `CombinedShapeLightShared(...)`가 돌려주는 **조명된 색에** 발광을 더한다:
```
half4 lit = CombinedShapeLightShared(surfaceData, inputData);
lit.rgb += emissive.rgb * _EmissiveHDR;   // _EmissiveHDR > 1 → 블룸 임계값(현재 1.0) 위로
return lit;
```
`ActorSprite.shader`(언릿)는 `rgb`에 같은 식으로 더한다. 방법 3가지(아트 비용 순):
- **(A) 색 키 발광, 아트 0**: 이미 있는 타일에서 "채도 높고 밝은 주황/붉은 픽셀(용암), 시안/초록 눈"을 HSV 임계로 골라 발광으로 취급. **[추론]** 용암·눈이 대체로 주변보다 뚜렷이 밝고 채도 높게 그려져 있으면 통한다. 오검출(불꽃 소품의 붉은 갑옷 등)은 재질 단위로 켜고 끈다.
- **(B) 발광 마스크 한 장**: 흑백/컬러 마스크를 Secondary Texture(`_EmissiveTex` 등 커스텀 이름 — **에디터에서 임의 이름 입력 가능한지는 확인 필요**, 드롭다운 목록 문제 있음 [출처 5])로 붙이거나, 재질 속성으로 직접 연결. PixelLab의 `edit_image` 계열이나 `pixelart_workbench`로 "빛나는 부분만 남긴 마스크"를 만들 수 있다 **[추론]**.
- **(C) 별도 발광 스프라이트 겹치기**: 이미 `AdditiveGlow.shader`가 있다. 눈·룬만 따로 얹는 가장 단순한 방법.

### 4.4 블룸과 픽셀 선명도
- 블룸은 축소 버퍼를 쓰므로 작은 밝은 물체가 가장 흔들린다(Unity 포럼 답변). [출처 23] **[추론]** 눈·룬 같은 작은 픽셀은 **최소 2x2 텍셀, 임계값 위 HDR 1.5~3.0**, 블룸 산란은 작게. 용암처럼 면이 넓은 발광은 HDR 1.2~1.6으로 낮춰도 충분히 번진다.
- 우리 URP 에셋은 이미 HDR on, 블룸 임계 1.0 `[확인: GameBootstrap 주석]`. 기존 문서의 SANABI 사례(임계값 0.9~1.0, 강도 높게)와 맞는다.
- 주의: 카메라 `allowHDR`가 `SceneBuilder`에서 `false`로 쓰여 있다 **[확인]**. URP 에셋 HDR과 카메라 HDR은 별개이므로 **실제 빌드에서 카메라 HDR이 켜지는지** (런타임에서 덮어쓰는지) 확인해야 발광이 1.0 위로 유지된다. 이것이 막혀 있으면 4번 전체가 무의미하다.

### 4.5 비용·위험
- 비용: 프래그먼트당 텍스처 1회 추가(또는 0회: 색 키 방식). 새 패스 없음. 가장 싼 항목.
- 위험: 색 키 방식의 오검출, 블룸 깜빡임(작은 픽셀). 둘 다 임계·크기 규율로 관리.

---

## 5. 볼류메트릭·갓 레이·안개, 열기 왜곡, 풀스크린 패스

### 5.1 Light2D 내장 볼류메트릭 (신규 확인)
- 모든 2D 광원은 **Volumetric** 체크박스로 "빈 공간에서 보이는 빛"을 만든다. **Volumetric Intensity**(0~1: 0이면 빈 공간에서 안 보임), **Volumetric Shadow Strength**(그림자 캐스터가 볼류메트릭을 가리는 정도). Global 타입은 불가. [출처 6] 같은 설명에서 **Rendering Layers**는 볼류메트릭을 끈 광원에만 의미가 있다고 한다. [출처 6]
- 비용·품질은 **[미확인]**: 공식 수치 없음. 기존 문서가 제안한 값(0.08~0.15)은 우리 판단이다.
- **[추론]** 지금의 "화염 소품 위 가산 광선 스프라이트"(우리 구현, 막 리듬에 맞춰 숨쉼)와 중복되므로 **영웅 등불과 보스 방 광원 1~2개에만** 시험하고, 가산 스프라이트와 비교 녹화한다.

### 5.2 안개·갓 레이: 싼 방법
- 공개된 2D용 "갓 레이" 레시피는 못 찾았다(검색 결과는 3D 볼류메트릭 레이마칭뿐). **[미확인]** 3D 레이마칭은 2D에 과하다.
- Graveyard Keeper의 안개는 **화면 전체를 덮는 수평 구름 스프라이트 여러 겹**이고, 위쪽이 더 적은 겹으로 덮여 지붕·나무 윗부분이 보인다. [출처 22] 이와 비슷하게 **타일링 노이즈 + UV 스크롤 + 그라데이션 마스크를 가산/알파 스프라이트에 입히는 것이 가장 싸다 [추론]**. 이미 `FogView.cs`가 있어 확장하면 된다.
- 우리 쪽에는 이미 가산 광선·막별 안개가 있으므로 새로 만들 것이 아니라 **"광원 색을 받는 안개"(안개 색 = 가까운 광원 색 혼합)**와 **"광원에서 벗어날수록 투명해지는 원뿔 광선"**으로 질을 올리는 것이 맞다.

### 5.3 열기 왜곡 / 화면 공간 효과
- **`_CameraSortingLayerTexture` 경로 (Unity 직원 설명)**: Renderer 2D Data의 *Camera Sorting Layer Texture* 구역에서 포함할 정렬 레이어와 다운샘플 여부를 고르고, Shader Graph에 Texture2D 속성을 `_CameraSortingLayerTexture`로(Exposed 해제) 만들어 샘플링한다. [출처 33] 이 스레드는 "직원이 URP 11 예시라 했고 공식 샘플은 나중에 2D Renderer Samples 깃허브에 올라왔다"고 하며 **완전히 동작하는 설정은 확인되지 않았다.** Scene Color 노드는 Sprite Lit 머티리얼에서 동작하지 않는다는 보고도 있다. [출처 34]
- **렌더러 기능 경로 (Unity 6 공식, Render Graph)**: 2D 프로젝트는 `ScriptableRendererFeature2D` + `ScriptableRenderPass2D`, 삽입 지점은 `RenderPassEvent2D`(12개: BeforeRendering … AfterRenderingSprites, BeforeRenderingPostProcessing, AfterRenderingPostProcessing, AfterRendering). `RecordRenderGraph` 안에서 `AddRasterRenderPass`를 쓰고 `Universal2DResourceData`로 2D 광·그림자 텍스처에 접근한다. `AllowPassCulling(false)`는 예시용이라 프로덕션에서는 쓰지 말라고 문서가 경고한다. [출처 35, 36] 풀스크린 패스 기능(Full Screen Pass Renderer Feature)이 2D 렌더러에서 쓰이는지는 문서에서 확인하지 못했다(**미확인**).
- **비용 [추론]**: 화면 한 장 복사 + 샘플 1회 정도. 960x540 기준 해상도에서는 작다. 비싼 것은 "전체 화면 풀 해상도 복사가 매 프레임" 이며 열기 왜곡은 용암·불 소품 근처 **작은 쿼드에만** 적용하면 영향 영역이 작다.
- **픽셀 선명도 규율 [추론]**: 왜곡 오프셋을 **기준 해상도 텍셀(1/960, 1/540)의 정수배로 양자화**해 샘플링한다. 안 하면 부드러운 서브텍셀 이동으로 픽셀이 뭉개진다. 파동은 천천히(2~4fps 계단 애니메이션)하고 진폭은 1~2텍셀.
- **Pixel Perfect Camera 주의**: 기본 필터 Retro AA는 이동 시 번쩍임을 막지만 흐려 보일 수 있고 Point는 선명하지만 번쩍인다. 업스케일 렌더 텍스처 사용 여부에 따라 후처리가 기준 해상도에서 도는지 화면 해상도에서 도는지가 갈리므로, 왜곡·안개 패스를 **어디에서** 거는지(업스케일 전/후) 결정해야 한다. [출처 23, 37] 현재 우리 설정은 Pixel Snapping + CropFrame None이며 업스케일 RT 사용은 **[미확인]**.
- 열기 왜곡 에셋(HazeFX 등)은 존재하나 2D Renderer 호환은 확인하지 않았다. 자작이 낫다.

---

## 6. 구역별 컬러 그레이딩 / LUT, 팔레트 규율

### 6.1 이미 한 것 [확인]
막별 색 보정, 색 비네트, 바닥/벽 채도·감마 셰이더(`GradedLit`), 앰비언트 저채도. 기존 문서 §0~§1에 근거와 수치가 있다.

### 6.2 추가로 얻을 것
- **LUT는 구역별 10장이 흔하다.** Graveyard Keeper는 시간대·구역별 LUT 10장을 만들었고 "만들기 쉽고, 쓰기 쉽고, 빠르다"고 평했다. [출처 22] 우리는 이미 막별 그레이딩이 있으므로 **LUT로 전환할지**는 선택이다. LUT는 픽셀 단위 효과만 가능하고 이웃을 읽는 효과(블러 등)는 못 넣는다. [출처 38]
- **팔레트 규율**: 램프는 단순 명도 변화가 아니라 **색상 회전**을 넣는다 — 그림자는 푸른 쪽, 하이라이트는 노란 쪽으로 15~25도(자연스러움), 30도 이상이면 양식화. 채도는 그림자에서 약간 올리고 가장 밝은 하이라이트에서 내린다. 램프 구조는 기본색 + 밝은 1~3 + 어두운 1~3, 시작은 16색에서 필요할 때만 추가. [출처 39] **[추론]** 우리는 어둡고 붉은 분위기라 "그림자를 푸른 쪽/보라 쪽으로 보내는 것"이 가장 눈에 띄는 개선이다 — 기존 앰비언트가 저채도 무채색 회색 쪽이라면 앰비언트 **색조**를 막 색의 보색 방향으로 약하게 기울이면 된다.
- **조명이 팔레트를 뭉갠다는 문제와 LUT 조명**: LutLight2D는 광 텍스처의 밝기에 따라 **팔레트 램프에서 색을 가져와 치환**해 팔레트의 색이 동적 조명 아래에서도 유지된다. [출처 40] 효과는 "계단식(양자화된) 조명"이다. **[추론]** 이것은 "부드러운 그라데이션 조명이 도트 감성을 깬다"는 문제의 해법 후보이며, 광 텍스처가 이미 반해상도(0.5)이므로 **밝기를 3~5단계로 양자화(+ 4x4 베이어 디더)** 하면 도트 조명 느낌이 난다. 단 `CombinedShapeLightShared`가 최종 색을 돌려주므로 조명값만 따로 꺼내려면 Core2D 인클루드를 풀어 써야 한다 — **실험 항목**, 위험 중간.
- **노멀 없이도 "고급"을 만드는 순서 [추론]**: 색(구역별 LUT) < 조명 위계(키 라이트 + 레이어 분리) < 발광·림 < 그림자·AO < 노멀. 앞쪽이 싸고 뒤쪽이 비싸다.

### 6.3 레퍼런스 게임에 대한 정직한 메모
- **Octopath/HD-2D**: 확인된 것: UE4, 3D 환경 + 2D 스프라이트, 빛과 그림자 강조, 높은 해상도 아트를 "픽셀 게임에 속할 듯 말 듯하게" 만들려는 균형 고민. [출처 15, 16, 41] 노멀맵·특정 셰이더 기법은 **[미확인]**.
- **Diablo II Resurrected, Darkest Dungeon, Blasphemous, Hyper Light Drifter, Moonlighter, Children of Morta, Tree of Savior**: 이번 조사에서는 개별 구현 자료를 찾아 읽지 않았다. 이들에 대해 쓴 모든 구체적 구현 주장은 **[추론]** 이며 이 문서는 하지 않았다. 필요하면 별도 조사.
- **Eastward**: 조명 구현 확인 못 함(위 1.5).

---

## 7. 우선순위 추천 (다음에 지을 것)

판단 기준: 아트 수정 0에 가까움 · 기존 코드 재사용 · 픽셀 선명도 보존 · 위험 낮음 · 눈에 띄는 정도(1080p 빌드 기준). 재활용 원칙(`reuse-existing-assets`)에 따라 새 에셋이 필요한 것은 표시.

| 순위 | 항목 | 새 아트 | 비용 | 위험 | 첫 단계(검증 방법) |
|---|---|---|---|---|---|
| 0 | **카메라 HDR 실제 켜짐 확인** (`allowHDR=false` 쓰는 곳 점검) | 없음 | 1시간 | 없음 | 빌드에서 임시로 HDR 1.5 색을 눈에 칠해 블룸이 번지는지 확인 |
| 1 | **발광 마스크(색 키 → 마스크 → 겹침 순)** — 용암, 눈, 룬, 불꽃 소품 | 없음~소 | 낮음 | 작은 픽셀 블룸 깜빡임 | `GradedLit`/`ActorSprite`에 `_EmissiveHDR` 추가, 용암 타일 1종과 몬스터 눈 1종 |
| 2 | **방향성·유색 림** (가장 가까운 광원 방향/색, 같은 4방향 알파 재사용) | 없음 | 낮음 | 광원 전환 시 튐 | 영웅과 몬스터 1종에서 빌드 녹화 비교 |
| 3 | **블롭 그림자 + 벽-바닥 접촉 AO 데칼** | 소(타원 1장, 그라데이션 데칼 몇 장) | 낮음 | 아이소 타원 비율 | 영웅 발밑 + 벽 한 구획 |
| 4 | **소품·벽·보스 노멀맵** (PixelLab `create_normal_map` kind=iso, lite) | 중(새 노멀 에셋, 아트 비용 낮음) | 중(깊이 선행 패스) | Sprite 타입 광원 호환, 뒤집기, 깜빡임 | 소품 3종 + 보스 1체, `Fast`, Sprite 타입 광원에서 명암 나는지 먼저 확인 |
| 5 | **Light2D Volumetric**을 영웅 등불·보스 광원에 0.08~0.15 | 없음 | 미공개(실측) | 가산 광선과 중복 | A/B 녹화 |
| 6 | **열기 왜곡 풀스크린/쿼드 패스** (`ScriptableRendererFeature2D`, 정수 텍셀 양자화) | 없음 | 낮음~중 | 업스케일 위치, Render Graph 작성 | 용암 방 한 구획의 작은 쿼드로 시작 |
| 7 | **조명 양자화 + 디더** 실험 (도트 조명 느낌) | 없음 | 낮음 | 인클루드 분해 필요 | `GradedLit`에서만 시험, 영웅 주변 반경만 비교 |
| 8 | **ShadowCaster2D** (영웅 등불 1개, 벽 발자국 합성) | 없음 | 미공개(실측) | 리플렉션, 아이소 불일치 | 맨 마지막, 실패해도 잃는 것 없음 |

권장 묶음: **(0 + 1 + 2 + 3)** 을 한 번에 — 전부 새 아트가 거의 없고, 기존 셰이더 두 개와 `LightRig`만 건드린다. 4번은 별도 묶음(새 에셋 사용 사유: 노멀맵은 기존 스프라이트의 부속 텍스처이며 기존 아트는 그대로 쓴다)으로 두고 빌드 녹화로 판정한다.

### 7.1 성능 측정 계획
공식 문서에 수치가 없으므로 항목마다 **Frame Debugger + 2D Graphics Profiler 모듈 + Light Batching Debugger**로 측정한다. [출처 7, 27] 특히 노멀맵(깊이 선행 패스)과 Volumetric은 켠 상태와 끈 상태를 같은 장면(막 III, 몬스터 30마리, 광원 20개)에서 비교한다. 렌더 텍스처 배율 0.5, 블렌드 스타일 종류 수, 정렬 레이어별 광원 배치 수를 같이 기록한다.

### 7.2 이 조사에서 확인하지 못한 것 (정직한 목록)
- Hades의 림 라이트 구현, Octopath/Eastward의 노멀맵 사용 여부 — 근거 없음.
- Sprite 타입 Light2D의 노멀맵 지원 — 문서 간 서술이 갈리므로 실험으로 확정.
- Light2D Volumetric, 노멀맵, ShadowCaster2D의 정량적 비용 — 공식 수치 없음.
- 2D 렌더러에서 "Full Screen Pass" 렌더러 기능 사용 가능 여부 — 문서에서 확인 못 함.
- 임의 이름의 Secondary Texture(`_EmissiveTex`) 허용 여부, 런타임 `Sprite.Create`의 secondary texture 인자 — 기억 기반, API 문서로 재확인 필요.
- PixelLab `create_normal_map`의 실제 결과 품질 — 시험 전.

---

## 출처

1. The Siege and the Sandfox 손으로 그린 노멀맵: https://gamedeveloper.com/art/adding-depth-to-2d-with-hand-drawn-normal-maps-in-i-the-siege-and-the-sandfox-i-
2. 픽셀 아트 노멀맵 생성 기법 분석(2022): https://ar5iv.labs.arxiv.org/html/2212.09692
3. Laigter 관련(itch 댓글·문서 검색 결과): https://azagaya.itch.io/laigter/comments?after=6
4. Unity 6.4 Secondary Textures: https://docs.unity3d.com/6000.4/Documentation/Manual/urp/SecondaryTextures.html
5. Unity 6.6 노멀맵/마스크맵 추가 문서: https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/2d-urp/2d-index/secondary-textures.md
6. Light 2D 컴포넌트 레퍼런스(6.6): https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/2d-urp/2d-index/2dlight-properties.md
7. 2D 그래픽스 프로파일러 모듈 / 2D URP 목차: https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/2d-urp/2d-index.md
8. Unity 포럼 "2D light and shadow techniques with URP"(Unity 기술 콘텐츠 팀): https://discussions.unity.com/t/2d-light-and-shadow-techniques-with-the-universal-render-pipeline/1641848
9. Unity 포럼 "노멀맵이 광원 위치에 맞게 안 나온다"(Unity 6): https://discussions.unity.com/t/2d-sprite-lighting-with-normal-maps-does-not-correctly-with-light-position/1722809
10. Unity 포럼 "Sprite-Lit-Default가 _NormalMap을 못 읽는다"(구버전): https://discussions.unity.com/t/lightweight-rp-sprite-lit-default-isnt-registering-_normalmap-secondary-texture/749655
11. 위 출처 1의 Y 채널 서술과 PixelLab 도구 문서(`create_normal_map` convention 옵션)
12. 청크 모드 뒤집힌 타일 노멀: https://discussions.unity.com/t/inverted-normal-map-for-flipped-tile-in-chunk-tilemap/1717641
13. 2.5D 기하 그림자 글(평평한 아이소 바닥 노멀 문제): https://discussions.unity.com/t/2-5d-geometric-shadows/928850
14. 아이소 타일맵 URP 2D 광원 비율: https://discussions.unity.com/threads/urp-2d-lights-for-isometric-tilemap.830508/
15. Octopath Traveler 아트 스타일(Unreal): https://www.unrealengine.com/spotlights/octopath-traveler-s-hd-2d-art-style-and-story-make-for-a-jrpg-dream-come-true
16. HD-2D 개요: https://en.wikipedia.org/wiki/HD-2D
17. Eastward 관련 80.lv·Game Developer 검색 결과: https://80.lv/articles/eastward-charming-chinese-pixel-art-adventure
18. Hades 3D 모델 작업(2차 자료): https://dtf.ru/id64954/238431-sozdanie-materi-nochi-process-modelirovaniya-nikty-iz-hades
19. xDasher 빛·깊이 4가지: https://pixel-beef.itch.io/xdasher/devlog/192949/4-things-we-did-to-add-light-and-depth-to-our-pixel-art-game
20. 동적 2D 캐릭터 조명(노멀맵 없이): https://gamedeveloper.com/programming/dynamic-2d-character-lighting
21. Nectarmancer 픽셀 조명(가장자리 맵): https://80.lv/articles/how-pixel-art-lighting-was-set-up-in-seamless-world-of-nectarmancer
22. Graveyard Keeper 그래픽 효과(LUT·그림자·안개·바람): https://www.gamedeveloper.com/programming/graveyard-keeper-how-the-graphics-effects-are-made
23. Unity 포럼 블룸·저해상도 렌더(블룸 아티팩트, 정수 업스케일): https://discussions.unity.com/t/low-resolution-rendering-3d-pixel-art-with-integer-upscaling/848462 , https://discussions.unity.com/t/bloom-postprocess-effect-artifacts-volume-profile/821749
24. 2D 조명 소개(렌더 단계): https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/2d-urp/2d-index/lights-2d-intro.md
25. Shadow Caster 2D 레퍼런스: https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/2d-urp/2d-index/2d-shadows/shadow-caster2d.md
26. 2D 광원에서 그림자 추가(Composite): https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/2d-urp/2d-index/2d-shadows/2dshadows.md
27. 2D 조명 최적화(Light Batching Debugger): https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/2d-urp/2d-index/2d-light-batching-debugger.md
28. ShadowCaster2D 런타임 생성·타일맵 커뮤니티 글: https://discussions.unity.com/t/script-for-generating-shadowcaster2ds-for-tilemaps/794605 , https://discussions.unity.com/t/tilemaps-and-shadowcaster2d-help/1716201
29. Unity Learn 발광 셰이더 추가: https://learn.unity.com/tutorial/add-emission-shaders
30. Unity Learn 2D Lighting for Pixel Art: https://learn.unity.com/course/2d-lighting-for-pixel-art
31. 2D 조명 호환 셰이더 준비: https://docs.unity.com/en-us/engine/6000.5/manual/unity2d/2d-urp/2d-index/prep-shader
32. Unity 포럼 Sprite Lit 발광 한계(Experimental 2D Lights 스레드): https://discussions.unity.com/t/experimental-2d-lights-and-shader-graph-support-in-universal-rp-previously-lwrp/744526?page=23
33. Unity 포럼 2D 렌더러 열기 왜곡(Camera Sorting Layer Texture): https://discussions.unity.com/t/unity-2drenderer-heat-distortion-shown-in-the-unity-gdc-showcase-2021-not-working/835412
34. Unity 포럼 Sprite Lit에서 Scene Color 문제 검색 결과 모음: https://discussions.unity.com/t/distortion-ripple-effect-for-sprites/769603
35. 2D 커스텀 렌더 패스 워크플로(6.6): https://docs.unity.com/en-us/engine/current/manual/unity2d/2d-urp/scriptable-renderer-features-2d/custom-render-pass-workflow-urp-2d
36. 2D 삽입 지점(RenderPassEvent2D): https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/2d-urp/scriptable-renderer-features-2d/injection-points-2d
37. Pixel Perfect Camera 레퍼런스(URP): https://docs.unity3d.com/Manual/urp/2d-pixelperfect-ref.html
38. Unity 포스트 프로세싱 색 보정/LUT 문서(구버전): https://docs.unity3d.com/Packages/com.unity.postprocessing@3.2/manual/Color-Grading.html
39. 픽셀 아트 팔레트와 색상 회전: https://www.slynyrd.com/blog/2018/1/10/pixelblog-1-color-palettes , https://www.gamedev.net/articles/visual-arts/color-palettes-r4964/
40. LutLight2D(조명 밝기로 팔레트 램프 치환): https://github.com/unitycoder/LutLight2D
41. Siliconera HD-2D 개발 어려움: https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/

관련 내부 문서: `docs/research/2026-11-21-modern-pixel-rendering.md`, `docs/research/d4-visual-techniques.md`
