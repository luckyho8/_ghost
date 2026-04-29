# 고스트(Ghost) - 프로젝트 문서

> 이 파일은 프로젝트의 살아있는 문서입니다. 기능이 추가되거나 변경될 때마다 업데이트하세요.

---

## 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **장르** | 퍼즐 / 테트리스 류 |
| **플랫폼** | 모바일 (Android / iOS 예정) |
| **엔진** | Unity (URP) |
| **개발 방식** | 1인 개발 |
| **개발 시작** | 2026년 초 |
| **현재 상태** | VFX/연출/그리드 10칸/StartupScene 적용 완료, 게임오버 판정 + InGameClear 팝업(연출 포함) 1차 구현, 폴리싱 진행 중 |

---

## 현재 구현 현황 (2026-04-29 기준, 4차 업데이트)

### ✅ 구현 완료

#### 핵심 게임플레이
- 블록 낙하, 이동, 회전 (벽킥 포함)
- 하드 드롭
- 라인 클리어 (1~4줄, 콤보 시스템)
- 고스트 피스 (블록 낙하 위치 미리보기)
- 레벨 시스템 (40초마다 레벨업, 낙하 속도 10% 가속)
- 속도 게이지 UI

#### 볼 기믹 시스템
- 게임 시작 시 볼 1개가 그리드 안을 대각선 이동
- 낙하 블록 충돌 → 현재 위치에서 랜덤 블록으로 형태 변환
- 고정 블록 충돌 → 해당 셀 파괴
- 벽 충돌 → 단순 반사 (랜덤 편차로 갇힘 방지)
- 레벨별 볼 크기 성장 + 속도 증가 (Inspector 조절)
- 갇힘 탈출 로직 (연속 충돌 감지 → 중앙 워프)
- 충돌 스케일 펀치 효과 (벽/낙하블록/고정블록 강도 차등)
- 고정 블록 파괴 시 점수 획득 (50 × 레벨 배수)

#### 아이템 시스템 (5종)
| 번호 | 아이템 | 효과 |
|------|--------|------|
| 0 | 폭탄 | 하단 3줄 제거 |
| 1 | 시간 정지 | 10초간 블록 낙하 정지 |
| 2 | 레벨 다운 | 레벨 -2 감소 |
| 3 | 리롤 | 현재 블록을 I블록으로 교체 |
| 4 | 고스트피스 | ON/OFF 영구 토글 (소모 없음) |

#### 고스트피스 토글 시스템
- 게임 시작 시 ON (기본 상태)
- OFF 상태에서 블록 배치 시 점수 ×1.5 보너스

#### 시각 효과
- 라인 완료 예고 하이라이트
- 블록 착지 파티클
- 라인 클리어 연출 (FX_LineClear_01 파티클, 셀별 색상 반영)
  - 짝수줄 좌→우 / 홀수줄 우→좌 교차 방향 순차 파괴
  - 연출 중 볼 일시정지, 완료 후 블록 하강
- 하드드롭 카메라 셰이크
- VFXManager 신설: 파티클 풀링 + 카메라 셰이크 (강도/시간 개별 설정)

#### UI
- 점수 표시 (7자리 + 팝 바운스 효과)
- 최고 점수 표시 + PlayerPrefs 저장 (실시간 갱신)
- 레벨 표시
- 타이머 (MM:SS:ms)
- 아이템 버튼 6개 (수량 배지, 0개시 광고 버튼)
- 조작 버튼 5개 (Left, Down, Drop, Rotate, Right) + 아이콘 적용
- UIBounceEffect: 버튼 눌림 바운스 (컴포넌트 부착만으로 동작)
- Plus_Score 펀치+알파 연출, Description 페이드 연출 (UIManager)
- ScoreEventData SO: 점수 이벤트별 텍스트/점수 테이블 10종 관리

#### 점수 시스템 (시간 기반)
- 기본 배치 10pt
- Fast Drop (0.5초 이내 하드드롭) +50pt × 레벨
- Quick Place (1초 이내 배치) +20pt × 레벨
- 라인 클리어 100/300/700/1500 (1~4줄) × 레벨
- 콤보 보너스 100pt × 콤보수 × 레벨, 8초 타이머
- 고스트 OFF 보너스 ×2 (배치+속도보너스)
- CellDestroy 50pt × 레벨 (볼 기믹 셀 파괴)

#### 게임 종료 처리
- 톱아웃 판정: SpawnBlock에서 다음 블록의 스폰 위치가 점유돼 있으면 게임 오버
- TriggerGameOver: 기록 저장 (BestScore/LastPlay/TotalPlay) + 볼 일시정지 + 클리어 팝업 오픈
- InGameClear 팝업 (Popup_InGameClear.cs)
  - Final Score 자릿수별 캐스케이드 카운트업 (~2.5초)
  - Final Level 단순 카운트업 (~0.6초)
  - Best/Last/Total 펀치 갱신 (Best 갱신 시에만, Last/Total은 항상)
  - Restart 버튼: GameScene 리로드

#### 씬 구성
- StartupScene: 비동기 로딩 → Tap to Start → GameScene 전환
- GameScene: 메인 게임 씬 (그리드 10칸 = 표준 테트리스 기준)

#### 블록 데이터
- 8종 블록 정의 (ScriptableObject)
- 블록 에디터 툴 (Unity Editor 내장)
- 4×4 그리드 기반 블록 설계
- 티어 시스템 (1~3단계, 밸런스 미적용)

#### 기술 스펙
- 그리드 크기: 15 × 27 (X: -7~7, Z: -12~15)
- 커스텀 쉐이더 3종 (BlockStyle, BlockStyleGhost, GridField)
- 레이어: Ball(8), Block(9), Wall(10)
- 입력: 키보드(WASD+G) + 터치 버튼
- 물리: 볼은 Rigidbody velocity 기반 이동

---

## 개발 예정 기능 (로드맵)

### 🔜 단기 (다음 목표)

- [x] **점수 시스템 정리** — 배치 속도 배율(1x/2x/5x), 라인 클리어 100/300/700/1500, 콤보 8초 타이머
- [x] **고스트피스 토글** — ON/OFF 영구 토글, OFF 시 점수 ×2 보너스
- [x] **아이템 5종 확정** — 중력 아이템 제거, 고스트피스를 토글 버튼으로 전환
- [x] **점수 시스템 시간 기반 개편** — Fast Drop / Quick Place / GhostOff 보너스 추가
- [x] **VFX 시스템** — 파티클 풀링, 카메라 셰이크, 라인클리어 연출 교체
- [x] **그리드 10칸 변경** — 표준 테트리스 기준
- [x] **StartupScene** — 비동기 로딩 + Tap to Start
- [x] **게임 오버 판정 + 클리어 팝업** — 톱아웃 → InGameClear 팝업 (점수 카운트업, 기록 갱신, Restart)
- [x] **콤보 연출** — 텍스트 펀치 + 풀스크린 컬러 플래시 (콤보 등급별) + 콤보 비례 카메라 쉐이크 + 8초 타이머 바
- [ ] **UX 전반 개선** — Block Blast 스타일 참고, 넥스트 블록 표시 개선

### 📅 중기

- [ ] **티어별 블록 밸런스** — 레벨별 블록 출현 가중치 조정 (tier 1~3)
- [ ] **볼 기믹 추가 튜닝** — 충돌 효과 연출, 볼 비주얼 개선
- [ ] **UI 연출 강화** — 게임오버 연출, 스코어 갱신 연출, 아이템 사용 피드백
- [ ] **사운드 / 음악** — BGM, SFX (착지음, 라인클리어음, 아이템음)
- [ ] **광고 연동** — 아이템 리필 광고 (UnityAds 또는 Admob)
- [ ] **고스트 피스 비주얼** — box_1x1_Ghost.mat 조정 (반투명 + 블록 색상)

### 🗓 장기

- [ ] **스테이지 모드** — 목표 조건 클리어형 스테이지
- [ ] **랭킹 시스템** — 온라인 리더보드 (Firebase 또는 GameCenter/PlayGames)
- [ ] **업적 시스템**
- [ ] **블록 스킨 / 테마**
- [ ] **앱스토어 출시** (Google Play / App Store)

---

## 기술 아키텍처

```
Assets/Application/
├── Scripts/
│   ├── Game/
│   │   ├── GameManager.cs          — 핵심 게임 루프, 그리드, 라인 클리어
│   │   ├── FallingBlock.cs         — 블록 이동/회전/충돌
│   │   ├── ItemManager.cs          — 아이템 6종 로직
│   │   ├── GimmickBall.cs          — 볼 기믹 이동/충돌/반사
│   │   ├── GimmickBallManager.cs   — 볼 스폰/레벨별 크기·속도 제어
│   │   └── UIBounceEffect.cs       — UI 버튼 바운스 애니메이션
│   └── Data/
│       ├── BlockData.cs            — 블록 데이터 구조체
│       ├── AllBlockData.cs         — 전체 블록 컨테이너 (ScriptableObject)
│       └── Editor/                 — 블록 에디터 툴
├── Bundles/
│   ├── Scenes/GameScene.unity      — 메인 씬
│   └── Games/InGame/
│       ├── BaseCube.prefab         — 블록 큐브 프리팹
│       └── Gimmick_Ball_01.prefab  — 볼 기믹 프리팹
├── Data/
│   └── AllBlockData.asset          — 블록 정의 데이터 파일
└── Shaders/
    ├── BlockStyle.shader           — 블록 렌더링 (불투명)
    ├── BlockStyleGhost.shader      — 고스트 피스용 (투명)
    └── GridField.shader            — 그리드 배경
```

### 핵심 설계 원칙
- **데이터 드리븐**: 블록 형태/색상/난이도는 ScriptableObject로 분리
- **Material Property Block**: 런타임 색상 변경 (인스턴스 생성 없음, 성능 최적화)
- **HashSet/Dictionary 기반 그리드**: O(1) 충돌 검사
- **코루틴 기반 연출**: 모든 시각 효과는 비동기 코루틴
- **레이어 기반 물리**: Ball(8)/Block(9)/Wall(10) 분리, Physics Matrix로 충돌 제어

---

## Claude와 협업하는 법 (개발 팁)

### 새 대화를 시작할 때
```
PROJECT.md를 읽어줘. 오늘은 [기능명]을 구현할 거야.
```

### 플랜 모드로 정리하고 싶을 때
```
PROJECT.md 읽어줘. 플랜모드로 갈 건데, [정리하고 싶은 주제들] 로드맵 정리해줘.
```

### 기능 추가 요청 예시
```
스코어 저장 기능 추가해줘. PlayerPrefs 써서 최고점 저장하고 게임 오버시 비교해서 갱신되게.
```

### 버그 수정 요청 예시
```
라인 클리어 후 가끔 블록이 허공에 뜨는 버그가 있어. GameManager.cs 봐줘.
```

---

## 개발 체크리스트 (배포 전 필수)

- [ ] 모바일 해상도 대응 (Safe Area, 다양한 화면 비율)
- [ ] 게임 오버 처리 완전 구현
- [ ] 저장/불러오기 구현
- [ ] 광고 SDK 연동
- [ ] 퍼포먼스 최적화 (오브젝트 풀링 검토)
- [ ] 사운드 전체 추가
- [ ] 아이콘 / 스플래시 스크린
- [ ] 개인정보처리방침 (앱스토어 필수)

---

## 변경 이력

| 날짜 | 내용 |
|------|------|
| 2026-03-19 | PROJECT.md 최초 작성. 현재 구현 현황 정리 |
| 2026-03-19 | 라인 완료 예고 하이라이트 + 셀레브레이션 연출 추가 (커밋: a727a9d) |
| 2026-03-19 | 고스트 피스 기능 추가 (커밋: f0fc6ab) |
| 2026-03-22 | 볼 기믹 시스템 구현 (GimmickBall + GimmickBallManager) |
| 2026-03-22 | 레이어 구조 추가 (Ball:8, Block:9, Wall:10) + Top_Wall 추가 |
| 2026-03-22 | 아이템/조작 아이콘 11종 생성 및 적용 |
| 2026-03-22 | BlockStyleGhost.shader 추가 (고스트 피스 투명 셰이더) |
| 2026-03-22 | 볼 기믹 버그 수정: 고정 블록 셀 파괴, 갇힘 탈출, 블록 변환 위치 유지 |
| 2026-03-23 | 점수 레벨 배수 보정 (lv1=1.0x~lv10=1.9x), 최고 점수 PlayerPrefs 저장 |
| 2026-03-23 | 볼 고정 블록 파괴 시 점수 획득 (50 × 레벨 배수) |
| 2026-03-23 | 볼 충돌 스케일 펀치 효과 (벽/낙하블록/고정블록 강도 차등 적용) |
| 2026-03-23 | 점수 공식 전면 교체: 배치(10×속도배율×레벨×고스트배율), 라인(100/300/700/1500), 콤보 8초 타이머 |
| 2026-03-23 | 고스트피스 토글 시스템 (OFF 시 ×1.5 보너스) + 아이템 6→5종 (중력 제거) |
| 2026-03-31 | 점수 시스템 개편: ScoreEventData SO + UIManager Plus_Score/Description 연출 + Fast Drop/Quick Place/GhostOff 보너스 (커밋: 6c30d71) |
| 2026-04-03 | VFX 시스템(VFXManager 신설) + 라인클리어 연출 교체(FX_LineClear_01) + 그리드 15→10칸 + StartupScene + 빌드 메타 (커밋: 15a19c8) |
| 2026-04-29 | 게임오버 판정(SpawnBlock 톱아웃) + InGameClear 팝업 신설(Popup_InGameClear.cs) + LastPlay/TotalPlay 기록 + UIManager.OpenGameOver |
| 2026-04-29 | 볼 기믹 디버그 도구(Trail/Gizmo/Overlay/Log) + 고정셀 충돌 그리드기반 노말 + post-collision push (스택 옆면 침투 버그 수정) |
| 2026-04-29 | 콤보 연출 패키지: 텍스트 펀치 + 풀스크린 컬러 플래시(콤보 등급별 색상) + 콤보 비례 카메라 쉐이크 + 8초 타이머 바 |
| 2026-04-29 | 콤보 플래시를 8초 슬로우 페이드아웃으로 변경 — 색상 알파 자체가 타이머 역할. 슬라이더 바 별도 표시 불필요 |
| 2026-04-29 | 콤보 연출 재구성: UI 풀스크린 플래시는 짧은 타격감(0.4s)으로 환원 + BackgroundColorPulse 신설 — BG_Plan 머티리얼 색상이 8초 페이드 복귀로 콤보 타이머 시각화 (시야 안 가림) |
| 2026-04-29 | 콤보 타이머 텍스트 카운트다운(8.00→0.00) UIManager.UpdateComboTimerText + 포맷 문자열 Inspector 노출 |
| 2026-04-29 | 사용 안 하는 17개 패키지 정리 (collab-proxy/test-framework/timeline + 14개 modules) — 빌드 사이즈 ↓, 컴파일 ↑ |
| 2026-04-29 | Ghost Build Tool 신설 (Ghost/Build Tool): Dev/Release × APK/AAB, Min/Target SDK 35 고정, Dev면 .dev suffix 자동, IL2CPP/Mono 자동 전환, Version Code +1 자동, 캐시 클리어 |
| 2026-04-29 | 빌드툴 SDK/백엔드 정책 수정: Min API 23 (호환성), Target API 35 (Play 정책), Dev/Release 모두 IL2CPP — S24 Ultra 등 Android 14- 디바이스 설치 가능해짐 |
| 2026-04-29 | 폰트 클린업 (~60MB 절약): 0-ref 폰트 6쌍 + Pretendard-Bold(38MB 한글 폴백) 삭제, HavenSans/Eng_dst LilitaOne의 Pretendard 폴백 참조 제거. 한글 사용 시 추후 TMPro Font Asset Creator로 5분만에 재생성 |
| 2026-04-29 | TMPro Examples & Extras 폴더 삭제 (3.9MB 절약, 안 쓰는 데모 자산) |
| 2026-04-29 | 빌드툴에 빌드 직전 콘솔 클리어 옵션 추가 — Unity 자체 SetSystemInterested 경고 노이즈 제거용 (LogEntries.Clear 리플렉션) |
| 2026-04-29 | Custom AndroidManifest.xml 추가 (Assets/Plugins/Android/) — Unity 2022.3가 ProjectSettings의 Portrait를 reversePortrait로 잘못 매핑하는 버그 우회. tools:replace로 강제 portrait 적용 |
| 2026-04-29 | Vulkan PreTransform 비활성화 (vulkanEnablePreTransform: 1→0) — UI 180° 회전 버그 근본 원인. Vulkan이 디스플레이 회전을 사전 변환할 때 Canvas Overlay와 충돌해서 화면 전체 뒤집힘 |
