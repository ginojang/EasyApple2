# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Apple II emulator built in Unity 6000.3.1f1. Emulates a 6502/65C02 CPU and renders a 40×24 text display to run Apple II ROM software.

## Build & Run

This is a Unity project — build and run via the Unity Editor (version 6000.3.1f1).

- Main scene: `Assets/Scenes/Apple2.unity`
- ROM assets: `Assets/Resoures/rom/` (note: typo in folder name — `Resoures`, not `Resources`)
- The ROM is loaded as a `TextAsset` from that resources folder at runtime

There are no standalone CLI build commands. Open the project in Unity Editor and press Play, or use Unity's build system.

## Architecture

The emulator is split across four C# partial-class files under `Assets/Scripts/`:

| File | Role |
|------|------|
| `Fake6502.Types.cs` | CPU state struct (A, X, Y, FLAGS, SP, PC), emulation state, flag constants, memory bus delegate types |
| `Fake6502.Core.cs` | All 256 opcodes — addressing modes, standard 6502 instructions, 65C02 additions, undocumented opcodes, opcode dispatch table |
| `Fake6502.Utils.cs` | Stack ops, memory helpers, ALU helpers, NMI/IRQ handlers, `Reset()`, `Step()` |
| `Apple2Main.cs` | Unity `MonoBehaviour` — 64KB memory array, ROM loading, keyboard I/O mapping, text display rendering, CPU execution loop |

### Memory Map (Apple2Main.cs)

- `0x0000–0xBFFF` — RAM
- `0xC000` — keyboard data latch (read)
- `0xC010` — keyboard strobe clear (read)
- `0xD000–0xFFFF` — ROM (Apple2_Plus.rom loaded here)

Memory reads/writes are injected into the `Fake6502` partial class via `ReadMem`/`WriteMem` delegates, keeping the CPU core decoupled from the memory layout.

### Execution Loop

`FixedUpdate` drives CPU execution. Each frame accumulates a cycle budget (~600K cycles, capped at 800K to prevent spiral) and calls `Step()` in a tight loop until the budget is spent. An MHz counter is derived from `emulationState.clockTicks`.

### Display

40×24 text mode. Each frame, `Apple2Main` reads the Apple II text page from memory (`0x0400–0x07FF`), converts character codes to ASCII, and writes to a `TextMeshPro` component. A blinking cursor is rendered separately.

## Key Implementation Details

- **Partial classes**: The `Fake6502` CPU is one logical class split across `Types`, `Core`, and `Utils` for manageability.
- **Opcode table**: `Core.cs` uses a statically-initialized array of `(addressMode, operation, cycles)` tuples for dispatch — do not reorder or the opcode indices will break.
- **Page-crossing penalties**: `absx_p`, `absy_p`, `indy_p` addressing modes add +1 cycle on page cross; this is tracked in `EmulationState`.
- **Decimal mode**: BCD arithmetic in `Add8` is conditionally compiled (`#if DECIMAL_SUPPORT`).
- **Aggressive inlining**: Performance-critical methods use `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
- **Keyboard**: `HandleKeyboardInput()` is currently stubbed out — the memory-mapped keyboard latch logic exists but input wiring is incomplete.
- **Video softswitches**: `WriteMem` has a TODO at `0xC000–0xC0FF` for speaker/video softswitch handling.

## Development Notes

- Code comments are in Korean.
- The resources folder is intentionally spelled `Resoures` (typo from project creation) — do not rename it without updating all asset references.
- FPS target is set to 24 in the inspector on the main scene.
