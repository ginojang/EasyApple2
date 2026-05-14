// 엔트리 — ROM fetch → Apple2 부트 → 키보드 바인딩 → RAF 루프.

import { Apple2, asciiFromKey } from './apple2.ts';
import { TextRenderer } from './textRenderer.ts';

const canvas = document.getElementById('screen') as HTMLCanvasElement;
const statusEl = document.getElementById('status') as HTMLDivElement;

// 실제 Apple II는 1.023 MHz. 60fps 기준 ~17050 사이클/프레임.
const CYCLES_PER_FRAME = 17050;

async function bootstrap(): Promise<void> {
  statusEl.textContent = 'fetching ROM…';
  const res = await fetch('rom/Apple2_Plus.bin');
  if (!res.ok) throw new Error(`ROM fetch failed: ${res.status}`);
  const romBytes = new Uint8Array(await res.arrayBuffer());

  const machine = new Apple2();
  machine.loadRom(romBytes, 0xD000);

  const resetVec = machine.mem[0xFFFC] | (machine.mem[0xFFFD] << 8);
  console.log(`Reset vector = $${resetVec.toString(16).padStart(4, '0')}`);
  machine.reset();
  console.log(`CPU PC after reset = $${machine.getCpu().PC.toString(16).padStart(4, '0')}`);

  const renderer = new TextRenderer(canvas, machine);

  window.addEventListener('keydown', (ev) => {
    if (ev.key === 'F12') {
      if (ev.ctrlKey) machine.requestNmi();
      else            machine.requestReset();
      ev.preventDefault();
      return;
    }
    const ascii = asciiFromKey(ev);
    if (ascii !== null) {
      machine.pressKey(ascii);
      ev.preventDefault();
    }
  });

  let lastTime = performance.now();
  let cyclesAcc = 0;
  let secAcc = 0;
  let mhzText = '';

  function frame(now: number): void {
    const dtSec = (now - lastTime) / 1000;
    lastTime = now;

    const used = machine.step(CYCLES_PER_FRAME);
    cyclesAcc += used;
    secAcc += dtSec;
    if (secAcc >= 1.0) {
      const mhz = cyclesAcc / secAcc / 1e6;
      mhzText = `CPU ${mhz.toFixed(2)} MHz`;
      cyclesAcc = 0;
      secAcc = 0;
    }

    renderer.render(dtSec);

    const cpu = machine.getCpu();
    const hex2 = (n: number) => n.toString(16).padStart(2, '0').toUpperCase();
    const hex4 = (n: number) => n.toString(16).padStart(4, '0').toUpperCase();
    statusEl.textContent =
      `${mhzText}  PC=$${hex4(cpu.PC)} A=$${hex2(cpu.A)} X=$${hex2(cpu.X)} ` +
      `Y=$${hex2(cpu.Y)} S=$${hex2(cpu.S)} P=$${hex2(cpu.Flags)}`;

    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
}

bootstrap().catch((err: unknown) => {
  console.error(err);
  const msg = err instanceof Error ? err.message : String(err);
  statusEl.textContent = `ERROR: ${msg}`;
});
