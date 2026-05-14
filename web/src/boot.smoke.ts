// 실제 ROM을 로드해서 부팅이 끝났을 때 텍스트 페이지를 덤프.
// 브라우저 없이 Node에서 검증용.

import { readFileSync } from 'node:fs';
import { Apple2 } from './apple2.ts';

const rom = new Uint8Array(readFileSync('public/rom/Apple2_Plus.bin'));
const machine = new Apple2();
machine.loadRom(rom, 0xD000);

const resetVec = machine.mem[0xFFFC] | (machine.mem[0xFFFD] << 8);
console.log(`Reset vector = $${resetVec.toString(16).padStart(4, '0').toUpperCase()}`);
machine.reset();
console.log(`PC after reset = $${machine.getCpu().PC.toString(16).padStart(4, '0').toUpperCase()}`);

// Apple II ROM이 부팅을 끝내고 BASIC 프롬프트까지 가는 데 보통 수십~수백만 사이클.
// 충분히 많이 돌려서 안정 상태에 도달.
const totalCycles = 5_000_000;
let cycles = 0;
const t0 = performance.now();
while (cycles < totalCycles) cycles += machine.getCpu().step();
const t1 = performance.now();

const cpu = machine.getCpu();
console.log(`Ran ${cycles} cycles (${(cycles / 1e6).toFixed(2)}M) in ${(t1 - t0).toFixed(0)}ms`);
console.log(`Final: PC=$${cpu.PC.toString(16).padStart(4,'0').toUpperCase()} A=$${cpu.A.toString(16).padStart(2,'0').toUpperCase()} X=$${cpu.X.toString(16).padStart(2,'0').toUpperCase()} Y=$${cpu.Y.toString(16).padStart(2,'0').toUpperCase()} S=$${cpu.S.toString(16).padStart(2,'0').toUpperCase()} P=$${cpu.Flags.toString(16).padStart(2,'0').toUpperCase()}`);

// 텍스트 페이지 덤프 (Apple2Main.DumpText40x24 직역)
console.log('\n--- Text page ($0400-$07FF) ---');
console.log('+' + '-'.repeat(40) + '+');
for (let row = 0; row < 24; row++) {
  let line = '';
  for (let col = 0; col < 40; col++) {
    const v = machine.mem[Apple2.textAddr(row, col)];
    if (v >= 0xA0 && v <= 0xDF)       line += String.fromCharCode(v & 0x7F);
    else if (v >= 0x20 && v <= 0x7F)  line += String.fromCharCode(v);
    else                              line += ' ';
  }
  console.log('|' + line + '|');
}
console.log('+' + '-'.repeat(40) + '+');
