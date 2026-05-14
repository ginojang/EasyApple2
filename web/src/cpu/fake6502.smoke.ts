// 간단 스모크 테스트 — node --experimental-strip-types로 실행
// LDA / STA / ADC / JMP / 분기 등 기본 동작 검증

import { Fake6502, CARRY_FLAG, ZERO_FLAG, SIGN_FLAG } from './fake6502.ts';

const mem = new Uint8Array(0x10000);
const cpu = new Fake6502(
  (a) => mem[a],
  (a, v) => { mem[a] = v; },
);

function assertEq(label: string, actual: number, expected: number): void {
  const a = actual & 0xFF;
  const e = expected & 0xFF;
  if (a !== e) {
    console.error(`FAIL ${label}: expected $${e.toString(16).padStart(2,'0')}, got $${a.toString(16).padStart(2,'0')}`);
    process.exit(1);
  }
  console.log(`OK   ${label}: $${a.toString(16).padStart(2,'0')}`);
}

function run(label: string, code: number[], start = 0x0600): void {
  mem.fill(0);
  code.forEach((b, i) => { mem[start + i] = b; });
  // Reset vector
  mem[0xFFFC] = start & 0xFF;
  mem[0xFFFD] = (start >> 8) & 0xFF;
  cpu.A = 0; cpu.X = 0; cpu.Y = 0; cpu.Flags = 0; cpu.S = 0;
  cpu.reset();
  for (let i = 0; i < 100 && cpu.PC < start + code.length; i++) cpu.step();
  console.log(`-- ${label} -- A=${cpu.A.toString(16)} X=${cpu.X.toString(16)} Y=${cpu.Y.toString(16)} P=${cpu.Flags.toString(16)} PC=${cpu.PC.toString(16)}`);
}

// 1. LDA #$42 / STA $10
run('LDA imm + STA zp', [0xA9, 0x42, 0x85, 0x10, 0x00]);
assertEq('A',         cpu.A,     0x42);
assertEq('mem[$10]',  mem[0x10], 0x42);

// 2. ADC: LDA #$10; CLC; ADC #$05  →  A=$15
run('CLC + ADC imm', [0xA9, 0x10, 0x18, 0x69, 0x05, 0x00]);
assertEq('A=$15', cpu.A, 0x15);

// 3. Carry on overflow: LDA #$FF; SEC; ADC #$01 (with carry-in)  →  A=$01, C=1
run('SEC + ADC overflow', [0xA9, 0xFF, 0x38, 0x69, 0x01, 0x00]);
assertEq('A=$01', cpu.A, 0x01);
if ((cpu.Flags & CARRY_FLAG) === 0) { console.error('FAIL carry should be set'); process.exit(1); }
console.log('OK   carry set');

// 4. Branch: LDX #$03 / loop: DEX / BNE loop  → X=0, Z=1
run('DEX/BNE loop', [
  0xA2, 0x03,       // LDX #$03
  0xCA,             // DEX
  0xD0, 0xFD,       // BNE -3
  0x00,
]);
assertEq('X=0', cpu.X, 0);
if ((cpu.Flags & ZERO_FLAG) === 0) { console.error('FAIL zero should be set'); process.exit(1); }
console.log('OK   zero set after loop');

// 5. JSR/RTS: subroutine that loads A=$AA
run('JSR/RTS', [
  0x20, 0x09, 0x06, // JSR $0609
  0x00,             // BRK marker (we won't reach)
  0x00, 0x00, 0x00, 0x00, 0x00,
  0xA9, 0xAA,       // sub: LDA #$AA
  0x60,             // RTS
]);
assertEq('A=$AA', cpu.A, 0xAA);

// 6. Sign flag via LDA #$80
run('LDA #$80 sets N', [0xA9, 0x80, 0x00]);
if ((cpu.Flags & SIGN_FLAG) === 0) { console.error('FAIL sign should be set'); process.exit(1); }
console.log('OK   sign set');

console.log('\nAll smoke tests passed.');
