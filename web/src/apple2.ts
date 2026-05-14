// Apple II 머신 — 64KB 메모리 + Fake6502 CPU + 키보드 래치 + 메모리 매핑 I/O.
// Apple2Main.cs의 비-Unity 부분을 TS로 직역.

import { Fake6502 } from './cpu/fake6502.ts';

export class Apple2 {
  readonly mem: Uint8Array;
  private readonly cpu: Fake6502;

  private kbdLatch = 0;
  private kbdStrobe = 0;

  private pendingReset = false;
  private pendingNmi = false;

  constructor() {
    this.mem = new Uint8Array(0x10000);
    this.cpu = new Fake6502(
      (addr) => this.busRead(addr),
      (addr, value) => this.busWrite(addr, value),
    );
  }

  loadRom(bytes: Uint8Array, baseAddr: number): void {
    if (baseAddr + bytes.length > this.mem.length) {
      throw new Error(`ROM too large: ${bytes.length} bytes at $${baseAddr.toString(16)}`);
    }
    this.mem.set(bytes, baseAddr);
  }

  reset(): void { this.cpu.reset(); }

  pressKey(ascii: number): void {
    this.kbdLatch = ascii & 0xFF;
    this.kbdStrobe = 1;
  }

  requestReset(): void { this.pendingReset = true; }
  requestNmi(): void   { this.pendingNmi = true; }

  // 인스트럭션 경계에서 인터럽트/리셋 처리 후 사이클 예산만큼 step
  step(cycleBudget: number): number {
    if (this.pendingReset) { this.cpu.reset(); this.pendingReset = false; }
    if (this.pendingNmi)   { this.cpu.nmi();   this.pendingNmi = false; }

    let cycles = 0;
    while (cycles < cycleBudget) {
      cycles += this.cpu.step();
    }
    return cycles;
  }

  getCpu(): Fake6502 { return this.cpu; }

  // Apple II 텍스트 페이지의 (row, col)에 해당하는 메모리 주소
  static textAddr(row: number, col: number): number {
    return 0x0400
      + ((row & 0x07) << 7)
      + ((row >> 3) * 0x28)
      + col;
  }

  private busRead(address: number): number {
    if (address === 0xC000) {
      return (this.kbdLatch & 0x7F) | (this.kbdStrobe !== 0 ? 0x80 : 0x00);
    }
    if (address === 0xC010) {
      this.kbdStrobe = 0; // strobe clear
      return 0;
    }
    return this.mem[address];
  }

  private busWrite(address: number, value: number): void {
    // TODO: 0xC000–0xC0FF 영역의 speaker / video softswitch 처리
    this.mem[address] = value;
  }
}

// 브라우저 KeyboardEvent → Apple II ASCII (Apple2Main.HandleKeyboardInput 직역)
export function asciiFromKey(ev: KeyboardEvent): number | null {
  switch (ev.key) {
    case 'Enter':      return 0x0D;
    case 'Escape':     return 0x1B;
    case 'Backspace':  return 0x08;
    case 'ArrowLeft':  return 0x08; // ^H
    case 'ArrowRight': return 0x15; // ^U
    case 'ArrowUp':    return 0x0B; // ^K
    case 'ArrowDown':  return 0x0A; // ^J
  }
  if (ev.key.length === 1) {
    const code = ev.key.charCodeAt(0);
    if (code >= 0x61 && code <= 0x7A) return code - 0x20; // 소문자 → 대문자
    if (code >= 0x20 && code < 0x7F)  return code;
  }
  return null;
}
