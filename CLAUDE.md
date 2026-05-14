# CLAUDE.md

이 파일은 Claude Code (claude.ai/code)가 이 저장소의 코드를 다룰 때 참고할 가이드를 제공한다.

## 프로젝트 개요

Unity 6000.3.1f1로 만든 Apple II 에뮬레이터. 6502/65C02 CPU를 에뮬레이션하고 40×24 텍스트 디스플레이를 렌더링하여 Apple II ROM 소프트웨어를 실행한다.

## 빌드 및 실행

이 프로젝트는 Unity 프로젝트로, Unity Editor(버전 6000.3.1f1)를 통해 빌드 및 실행한다.

- 메인 씬: `Assets/Scenes/Apple2.unity`
- ROM 에셋: `Assets/Resoures/rom/` (주의: 폴더명에 오타가 있음 — `Resources`가 아니라 `Resoures`)
- ROM은 런타임에 해당 리소스 폴더에서 `TextAsset`으로 로드됨

별도의 CLI 빌드 명령은 없다. Unity Editor에서 프로젝트를 열고 Play를 누르거나, Unity의 빌드 시스템을 사용한다.

## 아키텍처

에뮬레이터는 `Assets/Scripts/` 아래 네 개의 C# 파셜 클래스(partial class) 파일로 분리되어 있다:

| 파일 | 역할 |
|------|------|
| `Fake6502.Types.cs` | CPU 상태 구조체(A, X, Y, FLAGS, SP, PC), 에뮬레이션 상태, 플래그 상수, 메모리 버스 델리게이트 타입 |
| `Fake6502.Core.cs` | 256개 전체 opcode — 어드레싱 모드, 표준 6502 명령어, 65C02 추가 명령어, 비공식 opcode, opcode 디스패치 테이블 |
| `Fake6502.Utils.cs` | 스택 연산, 메모리 헬퍼, ALU 헬퍼, NMI/IRQ 핸들러, `Reset()`, `Step()` |
| `Apple2Main.cs` | Unity `MonoBehaviour` — 64KB 메모리 배열, ROM 로딩, 키보드 I/O 매핑, 텍스트 디스플레이 렌더링, CPU 실행 루프 |

### 메모리 맵 (Apple2Main.cs)

- `0x0000–0xBFFF` — RAM
- `0xC000` — 키보드 데이터 래치 (읽기)
- `0xC010` — 키보드 스트로브 클리어 (읽기)
- `0xD000–0xFFFF` — ROM (Apple2_Plus.rom이 여기에 로드됨)

메모리 읽기/쓰기는 `ReadMem`/`WriteMem` 델리게이트를 통해 `Fake6502` 파셜 클래스에 주입되어, CPU 코어가 메모리 레이아웃과 분리되도록 한다.

### 실행 루프

`FixedUpdate`가 CPU 실행을 구동한다. 매 프레임마다 사이클 예산(약 600K 사이클, 폭주 방지를 위해 800K로 상한)을 누적하고, 예산이 소진될 때까지 타이트 루프에서 `Step()`을 호출한다. MHz 카운터는 `emulationState.clockTicks`로부터 계산된다.

### 디스플레이

40×24 텍스트 모드. 매 프레임마다 `Apple2Main`은 메모리(`0x0400–0x07FF`)에서 Apple II 텍스트 페이지를 읽고, 문자 코드를 ASCII로 변환하여 `TextMeshPro` 컴포넌트에 기록한다. 깜빡이는 커서는 별도로 렌더링된다.

## 주요 구현 디테일

- **파셜 클래스**: `Fake6502` CPU는 관리 편의를 위해 `Types`, `Core`, `Utils`로 분리된 하나의 논리적 클래스다.
- **Opcode 테이블**: `Core.cs`는 디스패치를 위해 정적 초기화된 `(addressMode, operation, cycles)` 튜플 배열을 사용한다 — 순서를 바꾸면 opcode 인덱스가 깨지므로 변경 금지.
- **페이지 교차 패널티**: `absx_p`, `absy_p`, `indy_p` 어드레싱 모드는 페이지 교차 시 +1 사이클이 추가되며, 이는 `EmulationState`에서 추적된다.
- **Decimal 모드**: `Add8`의 BCD 산술은 조건부 컴파일(`#if DECIMAL_SUPPORT`)된다.
- **공격적 인라이닝**: 성능에 민감한 메서드는 `[MethodImpl(MethodImplOptions.AggressiveInlining)]`을 사용한다.
- **키보드**: `HandleKeyboardInput()`은 현재 스텁 처리되어 있음 — 메모리 매핑된 키보드 래치 로직은 있으나 입력 연결은 미완성.
- **비디오 소프트스위치**: `WriteMem`의 `0xC000–0xC0FF` 영역에 스피커/비디오 소프트스위치 처리를 위한 TODO가 있다.

## 개발 노트

- 코드 주석은 한글로 작성되어 있다.
- 리소스 폴더는 의도적으로 `Resoures`로 표기되어 있다(프로젝트 생성 시의 오타) — 모든 에셋 참조를 함께 업데이트하지 않는 한 이름을 바꾸지 말 것.
- 메인 씬 인스펙터의 FPS 타겟은 24로 설정되어 있다.
