// 40×24 Apple II 텍스트 모드 렌더러.
// v1: 캔버스 monospace 폰트로 그림. v2에서 Apple II 캐릭터 ROM 비트맵으로 교체 예정.

import { Apple2 } from './apple2.ts';

const COLS = 40;
const ROWS = 24;
const CELL_W = 14; // 280 * 2 / 40
const CELL_H = 16; // 192 * 2 / 24

const CURSOR_TOKEN = 0x60; // backtick — Apple2Main.CursorToken과 동일

export class TextRenderer {
  private readonly ctx: CanvasRenderingContext2D;
  private readonly machine: Apple2;

  private cursorOn = true;
  private cursorTimerSec = 0;
  private cursorIntervalSec = 0.5;

  constructor(canvas: HTMLCanvasElement, machine: Apple2) {
    canvas.width = COLS * CELL_W;
    canvas.height = ROWS * CELL_H;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('2D context unavailable');
    this.ctx = ctx;
    this.machine = machine;
  }

  render(dtSec: number): void {
    this.cursorTimerSec += dtSec;
    if (this.cursorTimerSec >= this.cursorIntervalSec) {
      this.cursorTimerSec -= this.cursorIntervalSec;
      this.cursorOn = !this.cursorOn;
    }

    const ctx = this.ctx;
    const w = ctx.canvas.width;
    const h = ctx.canvas.height;

    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, w, h);

    ctx.fillStyle = '#33ff66';
    ctx.font = `${CELL_H - 2}px ui-monospace, Menlo, Consolas, monospace`;
    ctx.textBaseline = 'top';

    const mem = this.machine.mem;
    for (let row = 0; row < ROWS; row++) {
      for (let col = 0; col < COLS; col++) {
        const v = mem[Apple2.textAddr(row, col)];

        let ch: string;
        if (v >= 0xA0 && v <= 0xDF)       ch = String.fromCharCode(v & 0x7F);
        else if (v >= 0x20 && v <= 0x7F)  ch = String.fromCharCode(v);
        else                              ch = ' ';

        if (ch.charCodeAt(0) === CURSOR_TOKEN) {
          ch = this.cursorOn ? '█' : ' '; // █
        }

        if (ch !== ' ') {
          ctx.fillText(ch, col * CELL_W + 1, row * CELL_H);
        }
      }
    }
  }
}
