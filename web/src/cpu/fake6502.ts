// Fake6502 — 6502 / 65C02 CPU 코어. C# `Fake6502.{Types,Core,Utils}.cs`의 TS 직역.
// 메모리 버스는 생성 시 주입하는 read/write 콜백으로 추상화.

export const CARRY_FLAG     = 0x01;
export const ZERO_FLAG      = 0x02;
export const INTERRUPT_FLAG = 0x04;
export const DECIMAL_FLAG   = 0x08;
export const BREAK_FLAG     = 0x10;
export const CONSTANT_FLAG  = 0x20;
export const OVERFLOW_FLAG  = 0x40;
export const SIGN_FLAG      = 0x80;

const STACK_BASE = 0x0100;

export type MemRead  = (addr: number) => number;
export type MemWrite = (addr: number, value: number) => void;

type AddrFn = (c: Fake6502) => void;
type OpFn   = (c: Fake6502) => void;

interface OpcodeEntry {
  addr: AddrFn;
  op: OpFn;
  cycles: number;
}

export class Fake6502 {
  // ---- CPU 레지스터 (A/X/Y/Flags/S는 8-bit, PC는 16-bit) ----
  A = 0;
  X = 0;
  Y = 0;
  Flags = 0;
  S = 0;
  PC = 0;

  // ---- 에뮬레이션 상태 ----
  instructions = 0;
  clockTicks = 0;
  ea = 0;
  opcode = 0;

  private readonly memRead: MemRead;
  private readonly memWrite: MemWrite;

  constructor(read: MemRead, write: MemWrite) {
    this.memRead = read;
    this.memWrite = write;
  }

  // ---------- 메모리 ----------
  read(addr: number): number {
    return this.memRead(addr & 0xFFFF) & 0xFF;
  }
  write(addr: number, value: number): void {
    this.memWrite(addr & 0xFFFF, value & 0xFF);
  }
  read16(addr: number): number {
    const lo = this.read(addr);
    const hi = this.read((addr + 1) & 0xFFFF);
    return (lo | (hi << 8)) & 0xFFFF;
  }

  // ---------- 플래그 ----------
  private setFlag(mask: number)   { this.Flags = (this.Flags | mask) & 0xFF; }
  private clearFlag(mask: number) { this.Flags = (this.Flags & ~mask) & 0xFF; }

  private zeroCalc(n: number): void {
    if ((n & 0xFF) !== 0) this.clearFlag(ZERO_FLAG);
    else this.setFlag(ZERO_FLAG);
  }
  private signCalc(n: number): void {
    if ((n & 0x80) !== 0) this.setFlag(SIGN_FLAG);
    else this.clearFlag(SIGN_FLAG);
  }
  private carryCalc(n: number): void {
    if ((n & 0xFF00) !== 0) this.setFlag(CARRY_FLAG);
    else this.clearFlag(CARRY_FLAG);
  }
  // n = result(16-bit), m = accumulator, o = memory
  private overflowCalc(n: number, m: number, o: number): void {
    if ((((n ^ m) & (n ^ o)) & 0x80) !== 0) this.setFlag(OVERFLOW_FLAG);
    else this.clearFlag(OVERFLOW_FLAG);
  }

  // ---------- 스택 ----------
  push8(value: number): void {
    this.write(STACK_BASE + this.S, value);
    this.S = (this.S - 1) & 0xFF;
  }
  push16(value: number): void {
    // hi then lo (6502)
    this.push8((value >> 8) & 0xFF);
    this.push8(value & 0xFF);
  }
  pull8(): number {
    this.S = (this.S + 1) & 0xFF;
    return this.read(STACK_BASE + this.S);
  }
  pull16(): number {
    const lo = this.pull8();
    const hi = this.pull8();
    return (lo | (hi << 8)) & 0xFFFF;
  }

  // ---------- 피연산자 (acc 모드 vs 메모리) ----------
  getValue(): number {
    const entry = OPCODES[this.opcode];
    if (entry.addr === Fake6502.acc) return this.A;
    return this.read(this.ea);
  }
  putValue(v: number): void {
    const entry = OPCODES[this.opcode];
    const b = v & 0xFF;
    if (entry.addr === Fake6502.acc) this.A = b;
    else this.write(this.ea, b);
  }

  // ---------- ALU 헬퍼 ----------
  private add8(a: number, b: number, carry: boolean): number {
    let result = a + b + (carry ? 1 : 0);
    this.zeroCalc(result);
    this.overflowCalc(result, a & 0xFF, b);
    this.signCalc(result);
    // NOTE: 원본 C#에서 BCD 보정은 `#if DECIMALMODE` 블록 안이라 비활성. 필요 시 활성화.
    // if ((this.Flags & DECIMAL_FLAG) !== 0) {
    //   const tmp = (((result + 0x66) & 0xFFFF) ^ a ^ b) >>> 3;
    //   result = result + ((tmp & 0x22) * 3);
    // }
    this.carryCalc(result);
    return result & 0xFF;
  }

  private rotateRight(value: number): number {
    const carryIn = this.Flags & CARRY_FLAG;
    const result = ((value >>> 1) | (carryIn << 7)) & 0xFF;
    if ((value & 1) !== 0) this.setFlag(CARRY_FLAG); else this.clearFlag(CARRY_FLAG);
    this.zeroCalc(result);
    this.signCalc(result);
    return result;
  }
  private rotateLeft(value: number): number {
    const carryIn = this.Flags & CARRY_FLAG;
    const result = (value << 1) | carryIn;
    this.carryCalc(result);
    this.zeroCalc(result);
    this.signCalc(result);
    return result & 0xFF;
  }
  private shrLogical(value: number): number {
    const result = (value >>> 1) & 0xFF;
    if ((value & 1) !== 0) this.setFlag(CARRY_FLAG); else this.clearFlag(CARRY_FLAG);
    this.zeroCalc(result);
    this.signCalc(result);
    return result;
  }
  private shlArith(value: number): number {
    const result = value << 1;
    this.carryCalc(result);
    this.zeroCalc(result);
    this.signCalc(result);
    return result & 0xFF;
  }
  private xor8(a: number, b: number): number {
    const result = (a ^ b) & 0xFF;
    this.zeroCalc(result);
    this.signCalc(result);
    return result;
  }
  private and8(a: number, b: number): number {
    const result = a & b & 0xFF;
    this.zeroCalc(result);
    this.signCalc(result);
    return result;
  }
  private inc8(r: number): number {
    const result = (r + 1) & 0xFF;
    this.zeroCalc(result);
    this.signCalc(result);
    return result;
  }
  private dec8(r: number): number {
    const result = (r - 1) & 0xFF;
    this.zeroCalc(result);
    this.signCalc(result);
    return result;
  }
  private compare(r: number): void {
    const value = this.getValue() & 0xFF;
    const result = (r - value) & 0xFFFF;
    if (r >= value) this.setFlag(CARRY_FLAG); else this.clearFlag(CARRY_FLAG);
    if (r === value) this.setFlag(ZERO_FLAG); else this.clearFlag(ZERO_FLAG);
    this.signCalc(result);
  }

  // ---------- 인터럽트 / 리셋 ----------
  nmi(): void {
    this.push16(this.PC);
    this.push8(this.Flags & ~BREAK_FLAG);
    this.Flags = (this.Flags | INTERRUPT_FLAG) & 0xFF;
    this.PC = this.read16(0xFFFA);
  }
  irq(): void {
    if ((this.Flags & INTERRUPT_FLAG) === 0) {
      this.push16(this.PC);
      this.push8(this.Flags & ~BREAK_FLAG);
      this.Flags = (this.Flags | INTERRUPT_FLAG) & 0xFF;
      this.PC = this.read16(0xFFFE);
    }
  }
  reset(): void {
    // 가짜 읽기 시퀀스 (https://www.pagetable.com/?p=410)
    this.read(0x00FF); this.read(0x00FF); this.read(0x00FF);
    this.read(0x0100); this.read(0x01FF); this.read(0x01FE);

    this.PC = this.read16(0xFFFC);
    this.S = 0xFD;
    this.Flags = (this.Flags | CONSTANT_FLAG | INTERRUPT_FLAG) & 0xFF;
    this.instructions = 0;
    this.clockTicks = 0;
  }

  step(): number {
    const op = this.read(this.PC);
    this.PC = (this.PC + 1) & 0xFFFF;
    this.opcode = op;
    this.Flags = (this.Flags | CONSTANT_FLAG) & 0xFF;

    const entry = OPCODES[op];
    entry.addr(this);
    entry.op(this);
    this.clockTicks += entry.cycles;

    const used = this.clockTicks;
    this.clockTicks = 0;
    this.instructions++;
    return used;
  }

  // ============================================================
  //   어드레싱 모드 (static — 레퍼런스 비교용)
  // ============================================================

  static imp(_c: Fake6502): void { /* implied — no operand */ }
  static acc(_c: Fake6502): void { /* accumulator — getValue/putValue가 A 사용 */ }

  static imm(c: Fake6502): void {
    c.ea = c.PC;
    c.PC = (c.PC + 1) & 0xFFFF;
  }
  static zp(c: Fake6502): void {
    c.ea = c.read(c.PC);
    c.PC = (c.PC + 1) & 0xFFFF;
  }
  static zpx(c: Fake6502): void {
    const base = c.read(c.PC);
    c.PC = (c.PC + 1) & 0xFFFF;
    c.ea = (base + c.X) & 0xFF;
  }
  static zpy(c: Fake6502): void {
    const base = c.read(c.PC);
    c.PC = (c.PC + 1) & 0xFFFF;
    c.ea = (base + c.Y) & 0xFF;
  }
  static rel(c: Fake6502): void {
    const off = c.read(c.PC);
    c.PC = (c.PC + 1) & 0xFFFF;
    const signed = (off << 24) >> 24; // sign-extend sbyte
    c.ea = (c.PC + signed) & 0xFFFF;
  }
  static abso(c: Fake6502): void {
    c.ea = c.read16(c.PC);
    c.PC = (c.PC + 2) & 0xFFFF;
  }
  static absx(c: Fake6502): void {
    c.ea = (c.read16(c.PC) + c.X) & 0xFFFF;
    c.PC = (c.PC + 2) & 0xFFFF;
  }
  static absx_p(c: Fake6502): void {
    let ea = c.read16(c.PC);
    const startPage = ea & 0xFF00;
    ea = (ea + c.X) & 0xFFFF;
    if (startPage !== (ea & 0xFF00)) c.clockTicks++;
    c.ea = ea;
    c.PC = (c.PC + 2) & 0xFFFF;
  }
  static absxi(c: Fake6502): void {
    let ea = c.read16(c.PC);
    ea = (ea + c.X) & 0xFFFF;
    c.ea = c.read16(ea);
    c.PC = (c.PC + 2) & 0xFFFF;
  }
  static absy(c: Fake6502): void {
    c.ea = (c.read16(c.PC) + c.Y) & 0xFFFF;
    c.PC = (c.PC + 2) & 0xFFFF;
  }
  static absy_p(c: Fake6502): void {
    let ea = c.read16(c.PC);
    const startPage = ea & 0xFF00;
    ea = (ea + c.Y) & 0xFFFF;
    if (startPage !== (ea & 0xFF00)) c.clockTicks++;
    c.ea = ea;
    c.PC = (c.PC + 2) & 0xFFFF;
  }
  static ind(c: Fake6502): void {
    const eahelp = c.read16(c.PC);
    if ((eahelp & 0xFF) === 0xFF) c.clockTicks++;
    c.ea = c.read16(eahelp);
    c.PC = (c.PC + 2) & 0xFFFF;
  }
  static indx(c: Fake6502): void {
    const zpBase = c.read(c.PC);
    c.PC = (c.PC + 1) & 0xFFFF;
    const eahelp = (zpBase + c.X) & 0xFF;
    const lo = c.read(eahelp);
    const hi = c.read((eahelp + 1) & 0xFF);
    c.ea = (lo | (hi << 8)) & 0xFFFF;
  }
  static indy(c: Fake6502): void {
    const eahelp = c.read(c.PC);
    c.PC = (c.PC + 1) & 0xFFFF;
    const eahelp2 = (eahelp & 0xFF00) | ((eahelp + 1) & 0xFF);
    const lo = c.read(eahelp);
    const hi = c.read(eahelp2);
    c.ea = ((lo | (hi << 8)) + c.Y) & 0xFFFF;
  }
  static indy_p(c: Fake6502): void {
    const eahelp = c.read(c.PC);
    c.PC = (c.PC + 1) & 0xFFFF;
    const eahelp2 = (eahelp & 0xFF00) | ((eahelp + 1) & 0xFF);
    const lo = c.read(eahelp);
    const hi = c.read(eahelp2);
    let ea = (lo | (hi << 8)) & 0xFFFF;
    const startPage = ea & 0xFF00;
    ea = (ea + c.Y) & 0xFFFF;
    if (startPage !== (ea & 0xFF00)) c.clockTicks++;
    c.ea = ea;
  }
  static zpi(c: Fake6502): void {
    const eahelp = c.read(c.PC);
    c.PC = (c.PC + 1) & 0xFFFF;
    const eahelp2 = (eahelp & 0xFF00) | ((eahelp + 1) & 0xFF);
    const lo = c.read(eahelp);
    const hi = c.read(eahelp2);
    c.ea = (lo | (hi << 8)) & 0xFFFF;
  }

  // ============================================================
  //   Opcode 핸들러
  // ============================================================

  static op_adc(c: Fake6502): void {
    const value = c.getValue();
    const carry = (c.Flags & CARRY_FLAG) !== 0;
    c.A = c.add8(c.A, value, carry);
  }
  static op_and(c: Fake6502): void {
    c.A = c.and8(c.A, c.getValue());
  }
  static op_asl(c: Fake6502): void {
    c.putValue(c.shlArith(c.getValue()));
  }
  static op_bra(c: Fake6502): void {
    const oldPC = c.PC;
    c.PC = c.ea;
    if ((oldPC & 0xFF00) !== (c.PC & 0xFF00)) c.clockTicks += 2;
    else c.clockTicks += 1;
  }
  static op_bcc(c: Fake6502): void { if ((c.Flags & CARRY_FLAG)    === 0) Fake6502.op_bra(c); }
  static op_bcs(c: Fake6502): void { if ((c.Flags & CARRY_FLAG)    !== 0) Fake6502.op_bra(c); }
  static op_beq(c: Fake6502): void { if ((c.Flags & ZERO_FLAG)     !== 0) Fake6502.op_bra(c); }
  static op_bne(c: Fake6502): void { if ((c.Flags & ZERO_FLAG)     === 0) Fake6502.op_bra(c); }
  static op_bmi(c: Fake6502): void { if ((c.Flags & SIGN_FLAG)     !== 0) Fake6502.op_bra(c); }
  static op_bpl(c: Fake6502): void { if ((c.Flags & SIGN_FLAG)     === 0) Fake6502.op_bra(c); }
  static op_bvc(c: Fake6502): void { if ((c.Flags & OVERFLOW_FLAG) === 0) Fake6502.op_bra(c); }
  static op_bvs(c: Fake6502): void { if ((c.Flags & OVERFLOW_FLAG) !== 0) Fake6502.op_bra(c); }

  static op_bit(c: Fake6502): void {
    const value = c.getValue();
    c.zeroCalc(c.A & value);
    c.Flags = ((c.Flags & 0x3F) | (value & 0xC0)) & 0xFF;
  }
  static op_bit_imm(c: Fake6502): void {
    const value = c.getValue();
    c.zeroCalc(c.A & value);
  }
  static op_brk(c: Fake6502): void {
    c.PC = (c.PC + 1) & 0xFFFF;
    c.push16(c.PC);
    c.push8(c.Flags | BREAK_FLAG);
    c.Flags = (c.Flags | INTERRUPT_FLAG) & 0xFF;
    c.PC = c.read16(0xFFFE);
  }
  static op_clc(c: Fake6502): void { c.clearFlag(CARRY_FLAG); }
  static op_cld(c: Fake6502): void { c.clearFlag(DECIMAL_FLAG); }
  static op_cli(c: Fake6502): void { c.clearFlag(INTERRUPT_FLAG); }
  static op_clv(c: Fake6502): void { c.clearFlag(OVERFLOW_FLAG); }
  static op_cmp(c: Fake6502): void { c.compare(c.A); }
  static op_cpx(c: Fake6502): void { c.compare(c.X); }
  static op_cpy(c: Fake6502): void { c.compare(c.Y); }
  static op_dec(c: Fake6502): void { c.putValue(c.dec8(c.getValue())); }
  static op_dex(c: Fake6502): void { c.X = c.dec8(c.X); }
  static op_dey(c: Fake6502): void { c.Y = c.dec8(c.Y); }
  static op_eor(c: Fake6502): void { c.A = c.xor8(c.A, c.getValue()); }
  static op_inc(c: Fake6502): void { c.putValue(c.inc8(c.getValue())); }
  static op_inx(c: Fake6502): void { c.X = c.inc8(c.X); }
  static op_iny(c: Fake6502): void { c.Y = c.inc8(c.Y); }
  static op_jmp(c: Fake6502): void { c.PC = c.ea; }
  static op_jsr(c: Fake6502): void {
    c.push16((c.PC - 1) & 0xFFFF);
    c.PC = c.ea;
  }
  static op_lda(c: Fake6502): void {
    const v = c.getValue();
    c.A = v;
    c.zeroCalc(v);
    c.signCalc(v);
  }
  static op_ldx(c: Fake6502): void {
    const v = c.getValue();
    c.X = v;
    c.zeroCalc(v);
    c.signCalc(v);
  }
  static op_ldy(c: Fake6502): void {
    const v = c.getValue();
    c.Y = v;
    c.zeroCalc(v);
    c.signCalc(v);
  }
  static op_lsr(c: Fake6502): void { c.putValue(c.shrLogical(c.getValue())); }
  static op_nop(_c: Fake6502): void { /* no-op */ }
  static op_ora(c: Fake6502): void {
    const result = (c.A | c.getValue()) & 0xFF;
    c.zeroCalc(result);
    c.signCalc(result);
    c.A = result;
  }
  static op_pha(c: Fake6502): void { c.push8(c.A); }
  static op_phx(c: Fake6502): void { c.push8(c.X); }
  static op_phy(c: Fake6502): void { c.push8(c.Y); }
  static op_php(c: Fake6502): void { c.push8(c.Flags | BREAK_FLAG); }
  static op_pla(c: Fake6502): void {
    const v = c.pull8();
    c.A = v;
    c.zeroCalc(v);
    c.signCalc(v);
  }
  static op_plx(c: Fake6502): void {
    const v = c.pull8();
    c.X = v;
    c.zeroCalc(v);
    c.signCalc(v);
  }
  static op_ply(c: Fake6502): void {
    const v = c.pull8();
    c.Y = v;
    c.zeroCalc(v);
    c.signCalc(v);
  }
  static op_plp(c: Fake6502): void {
    c.Flags = (c.pull8() | CONSTANT_FLAG | BREAK_FLAG) & 0xFF;
  }
  static op_rol(c: Fake6502): void {
    const v = c.getValue();
    c.putValue(v); // dummy write
    c.putValue(c.rotateLeft(v));
  }
  static op_ror(c: Fake6502): void {
    const v = c.getValue();
    c.putValue(v); // dummy write
    c.putValue(c.rotateRight(v));
  }
  static op_rti(c: Fake6502): void {
    c.Flags = (c.pull8() | CONSTANT_FLAG | BREAK_FLAG) & 0xFF;
    c.PC = c.pull16();
  }
  static op_rts(c: Fake6502): void {
    c.PC = (c.pull16() + 1) & 0xFFFF;
  }
  static op_sbc(c: Fake6502): void {
    let value = c.getValue() ^ 0xFF; // ones-complement
    // NOTE: 원본 C# 코드의 BCD 보정 (forum.6502.org 게시글 출처). add8의 BCD는 꺼져 있지만 sbc는 켜져 있음.
    if ((c.Flags & DECIMAL_FLAG) !== 0) value = (value - 0x66) & 0xFFFF;
    const carry = (c.Flags & CARRY_FLAG) !== 0;
    c.A = c.add8(c.A, value, carry);
  }
  static op_sec(c: Fake6502): void { c.setFlag(CARRY_FLAG); }
  static op_sed(c: Fake6502): void { c.setFlag(DECIMAL_FLAG); }
  static op_sei(c: Fake6502): void { c.setFlag(INTERRUPT_FLAG); }
  static op_sta(c: Fake6502): void { c.putValue(c.A); }
  static op_stx(c: Fake6502): void { c.putValue(c.X); }
  static op_sty(c: Fake6502): void { c.putValue(c.Y); }
  static op_stz(c: Fake6502): void { c.putValue(0); }
  static op_tax(c: Fake6502): void {
    c.X = c.A;
    c.zeroCalc(c.X);
    c.signCalc(c.X);
  }
  static op_tay(c: Fake6502): void {
    c.Y = c.A;
    c.zeroCalc(c.Y);
    c.signCalc(c.Y);
  }
  static op_tsx(c: Fake6502): void {
    c.X = c.S;
    c.zeroCalc(c.X);
    c.signCalc(c.X);
  }
  static op_trb(c: Fake6502): void {
    const value = c.getValue();
    const result = c.A & ~value & 0xFF;
    c.putValue(result);
    c.zeroCalc((c.A | result) & 0xFF);
  }
  static op_tsb(c: Fake6502): void {
    const value = c.getValue();
    const result = (c.A | value) & 0xFF;
    c.putValue(result);
    c.zeroCalc((c.A | result) & 0xFF);
  }
  static op_txa(c: Fake6502): void {
    c.A = c.X;
    c.zeroCalc(c.A);
    c.signCalc(c.A);
  }
  static op_txs(c: Fake6502): void { c.S = c.X; }
  static op_tya(c: Fake6502): void {
    c.A = c.Y;
    c.zeroCalc(c.A);
    c.signCalc(c.A);
  }

  // ---- 비공식 opcode ----
  static op_lax(c: Fake6502): void {
    const v = c.getValue();
    c.A = v;
    c.X = v;
    c.zeroCalc(v);
    c.signCalc(v);
  }
  static op_sax(c: Fake6502): void { c.putValue(c.A & c.X); }
  static op_dcp(c: Fake6502): void { Fake6502.op_dec(c); Fake6502.op_cmp(c); }
  static op_isb(c: Fake6502): void { Fake6502.op_inc(c); Fake6502.op_sbc(c); }
  static op_slo(c: Fake6502): void { Fake6502.op_asl(c); Fake6502.op_ora(c); }
  static op_rla(c: Fake6502): void {
    const value = c.getValue();
    const result = c.rotateLeft(value);
    c.putValue(value); // dummy
    c.putValue(result);
    c.A = c.and8(c.A, result);
  }
  static op_sre(c: Fake6502): void {
    const value = c.getValue();
    const result = c.shrLogical(value);
    c.putValue(value); // dummy
    c.putValue(result);
    c.A = c.xor8(c.A, result);
  }
  static op_rra(c: Fake6502): void {
    const value = c.getValue();
    const result = c.rotateRight(value);
    c.putValue(value); // dummy
    c.putValue(result);
    const carry = (c.Flags & CARRY_FLAG) !== 0;
    c.A = c.add8(c.A, result, carry);
  }
}

// ============================================================
//   Opcode 디스패치 테이블 (순서 변경 금지!)
// ============================================================
const F = Fake6502;
const E = (addr: AddrFn, op: OpFn, cycles: number): OpcodeEntry => ({ addr, op, cycles });

const OPCODES: readonly OpcodeEntry[] = [
  /* 00 */ E(F.imp,    F.op_brk,    7),
  /* 01 */ E(F.indx,   F.op_ora,    6),
  /* 02 */ E(F.imp,    F.op_nop,    2),
  /* 03 */ E(F.indx,   F.op_slo,    8),
  /* 04 */ E(F.zp,     F.op_tsb,    5),
  /* 05 */ E(F.zp,     F.op_ora,    3),
  /* 06 */ E(F.zp,     F.op_asl,    5),
  /* 07 */ E(F.zp,     F.op_slo,    5),
  /* 08 */ E(F.imp,    F.op_php,    3),
  /* 09 */ E(F.imm,    F.op_ora,    2),
  /* 0A */ E(F.acc,    F.op_asl,    2),
  /* 0B */ E(F.imm,    F.op_nop,    2),
  /* 0C */ E(F.abso,   F.op_tsb,    6),
  /* 0D */ E(F.abso,   F.op_ora,    4),
  /* 0E */ E(F.abso,   F.op_asl,    6),
  /* 0F */ E(F.abso,   F.op_slo,    6),

  /* 10 */ E(F.rel,    F.op_bpl,    2),
  /* 11 */ E(F.indy_p, F.op_ora,    5),
  /* 12 */ E(F.zpi,    F.op_ora,    5),
  /* 13 */ E(F.indy,   F.op_slo,    8),
  /* 14 */ E(F.zp,     F.op_trb,    5),
  /* 15 */ E(F.zpx,    F.op_ora,    4),
  /* 16 */ E(F.zpx,    F.op_asl,    6),
  /* 17 */ E(F.zpx,    F.op_slo,    6),
  /* 18 */ E(F.imp,    F.op_clc,    2),
  /* 19 */ E(F.absy_p, F.op_ora,    4),
  /* 1A */ E(F.acc,    F.op_inc,    2),
  /* 1B */ E(F.absy,   F.op_slo,    7),
  /* 1C */ E(F.abso,   F.op_trb,    6),
  /* 1D */ E(F.absx_p, F.op_ora,    4),
  /* 1E */ E(F.absx,   F.op_asl,    7),
  /* 1F */ E(F.absx,   F.op_slo,    7),

  /* 20 */ E(F.abso,   F.op_jsr,    6),
  /* 21 */ E(F.indx,   F.op_and,    6),
  /* 22 */ E(F.imp,    F.op_nop,    2),
  /* 23 */ E(F.indx,   F.op_rla,    8),
  /* 24 */ E(F.zp,     F.op_bit,    3),
  /* 25 */ E(F.zp,     F.op_and,    3),
  /* 26 */ E(F.zp,     F.op_rol,    5),
  /* 27 */ E(F.zp,     F.op_rla,    5),
  /* 28 */ E(F.imp,    F.op_plp,    4),
  /* 29 */ E(F.imm,    F.op_and,    2),
  /* 2A */ E(F.acc,    F.op_rol,    2),
  /* 2B */ E(F.imm,    F.op_nop,    2),
  /* 2C */ E(F.abso,   F.op_bit,    4),
  /* 2D */ E(F.abso,   F.op_and,    4),
  /* 2E */ E(F.abso,   F.op_rol,    6),
  /* 2F */ E(F.abso,   F.op_rla,    6),

  /* 30 */ E(F.rel,    F.op_bmi,    2),
  /* 31 */ E(F.indy_p, F.op_and,    5),
  /* 32 */ E(F.zpi,    F.op_adc,    5),
  /* 33 */ E(F.indy,   F.op_rla,    8),
  /* 34 */ E(F.zpx,    F.op_bit,    4),
  /* 35 */ E(F.zpx,    F.op_and,    4),
  /* 36 */ E(F.zpx,    F.op_rol,    6),
  /* 37 */ E(F.zpx,    F.op_rla,    6),
  /* 38 */ E(F.imp,    F.op_sec,    2),
  /* 39 */ E(F.absy_p, F.op_and,    4),
  /* 3A */ E(F.acc,    F.op_dec,    2),
  /* 3B */ E(F.absy,   F.op_rla,    7),
  /* 3C */ E(F.absx_p, F.op_bit,    4),
  /* 3D */ E(F.absx_p, F.op_and,    4),
  /* 3E */ E(F.absx,   F.op_rol,    7),
  /* 3F */ E(F.absx,   F.op_rla,    7),

  /* 40 */ E(F.imp,    F.op_rti,    6),
  /* 41 */ E(F.indx,   F.op_eor,    6),
  /* 42 */ E(F.imp,    F.op_nop,    2),
  /* 43 */ E(F.indx,   F.op_sre,    8),
  /* 44 */ E(F.zp,     F.op_nop,    3),
  /* 45 */ E(F.zp,     F.op_eor,    3),
  /* 46 */ E(F.zp,     F.op_lsr,    5),
  /* 47 */ E(F.zp,     F.op_sre,    5),
  /* 48 */ E(F.imp,    F.op_pha,    3),
  /* 49 */ E(F.imm,    F.op_eor,    2),
  /* 4A */ E(F.acc,    F.op_lsr,    2),
  /* 4B */ E(F.imm,    F.op_nop,    2),
  /* 4C */ E(F.abso,   F.op_jmp,    3),
  /* 4D */ E(F.abso,   F.op_eor,    4),
  /* 4E */ E(F.abso,   F.op_lsr,    6),
  /* 4F */ E(F.abso,   F.op_sre,    6),

  /* 50 */ E(F.rel,    F.op_bvc,    2),
  /* 51 */ E(F.indy_p, F.op_eor,    5),
  /* 52 */ E(F.zpi,    F.op_eor,    5),
  /* 53 */ E(F.indy,   F.op_sre,    8),
  /* 54 */ E(F.zpx,    F.op_nop,    4),
  /* 55 */ E(F.zpx,    F.op_eor,    4),
  /* 56 */ E(F.zpx,    F.op_lsr,    6),
  /* 57 */ E(F.zpx,    F.op_sre,    6),
  /* 58 */ E(F.imp,    F.op_cli,    2),
  /* 59 */ E(F.absy_p, F.op_eor,    4),
  /* 5A */ E(F.imp,    F.op_phy,    2),
  /* 5B */ E(F.absy,   F.op_sre,    7),
  /* 5C */ E(F.absx,   F.op_nop,    4),
  /* 5D */ E(F.absx_p, F.op_eor,    4),
  /* 5E */ E(F.absx,   F.op_lsr,    7),
  /* 5F */ E(F.absx,   F.op_sre,    7),

  /* 60 */ E(F.imp,    F.op_rts,    6),
  /* 61 */ E(F.indx,   F.op_adc,    6),
  /* 62 */ E(F.imp,    F.op_nop,    2),
  /* 63 */ E(F.indx,   F.op_rra,    8),
  /* 64 */ E(F.zp,     F.op_stz,    3),
  /* 65 */ E(F.zp,     F.op_adc,    3),
  /* 66 */ E(F.zp,     F.op_ror,    5),
  /* 67 */ E(F.zp,     F.op_rra,    5),
  /* 68 */ E(F.imp,    F.op_pla,    4),
  /* 69 */ E(F.imm,    F.op_adc,    2),
  /* 6A */ E(F.acc,    F.op_ror,    2),
  /* 6B */ E(F.imm,    F.op_nop,    2),
  /* 6C */ E(F.ind,    F.op_jmp,    5),
  /* 6D */ E(F.abso,   F.op_adc,    4),
  /* 6E */ E(F.abso,   F.op_ror,    6),
  /* 6F */ E(F.abso,   F.op_rra,    6),

  /* 70 */ E(F.rel,    F.op_bvs,    2),
  /* 71 */ E(F.indy_p, F.op_adc,    5),
  /* 72 */ E(F.zpi,    F.op_adc,    5),
  /* 73 */ E(F.indy,   F.op_rra,    8),
  /* 74 */ E(F.zpx,    F.op_stz,    4),
  /* 75 */ E(F.zpx,    F.op_adc,    4),
  /* 76 */ E(F.zpx,    F.op_ror,    6),
  /* 77 */ E(F.zpx,    F.op_rra,    6),
  /* 78 */ E(F.imp,    F.op_sei,    2),
  /* 79 */ E(F.absy_p, F.op_adc,    4),
  /* 7A */ E(F.imp,    F.op_ply,    6),
  /* 7B */ E(F.absy,   F.op_rra,    7),
  /* 7C */ E(F.absxi,  F.op_jmp,    6),
  /* 7D */ E(F.absx_p, F.op_adc,    4),
  /* 7E */ E(F.absx,   F.op_ror,    7),
  /* 7F */ E(F.absx,   F.op_rra,    7),

  /* 80 */ E(F.rel,    F.op_bra,    3),
  /* 81 */ E(F.indx,   F.op_sta,    6),
  /* 82 */ E(F.imm,    F.op_nop,    2),
  /* 83 */ E(F.indx,   F.op_sax,    6),
  /* 84 */ E(F.zp,     F.op_sty,    3),
  /* 85 */ E(F.zp,     F.op_sta,    3),
  /* 86 */ E(F.zp,     F.op_stx,    3),
  /* 87 */ E(F.zp,     F.op_sax,    3),
  /* 88 */ E(F.imp,    F.op_dey,    2),
  /* 89 */ E(F.imm,    F.op_bit_imm,2),
  /* 8A */ E(F.imp,    F.op_txa,    2),
  /* 8B */ E(F.imm,    F.op_nop,    2),
  /* 8C */ E(F.abso,   F.op_sty,    4),
  /* 8D */ E(F.abso,   F.op_sta,    4),
  /* 8E */ E(F.abso,   F.op_stx,    4),
  /* 8F */ E(F.abso,   F.op_sax,    4),

  /* 90 */ E(F.rel,    F.op_bcc,    2),
  /* 91 */ E(F.indy,   F.op_sta,    6),
  /* 92 */ E(F.zpi,    F.op_sta,    5),
  /* 93 */ E(F.indy,   F.op_nop,    6),
  /* 94 */ E(F.zpx,    F.op_sty,    4),
  /* 95 */ E(F.zpx,    F.op_sta,    4),
  /* 96 */ E(F.zpy,    F.op_stx,    4),
  /* 97 */ E(F.zpy,    F.op_sax,    4),
  /* 98 */ E(F.imp,    F.op_tya,    2),
  /* 99 */ E(F.absy,   F.op_sta,    5),
  /* 9A */ E(F.imp,    F.op_txs,    2),
  /* 9B */ E(F.absy,   F.op_nop,    5),
  /* 9C */ E(F.abso,   F.op_stz,    4),
  /* 9D */ E(F.absx,   F.op_sta,    5),
  /* 9E */ E(F.absx,   F.op_stz,    5),
  /* 9F */ E(F.absy,   F.op_nop,    5),

  /* A0 */ E(F.imm,    F.op_ldy,    2),
  /* A1 */ E(F.indx,   F.op_lda,    6),
  /* A2 */ E(F.imm,    F.op_ldx,    2),
  /* A3 */ E(F.indx,   F.op_lax,    6),
  /* A4 */ E(F.zp,     F.op_ldy,    3),
  /* A5 */ E(F.zp,     F.op_lda,    3),
  /* A6 */ E(F.zp,     F.op_ldx,    3),
  /* A7 */ E(F.zp,     F.op_lax,    3),
  /* A8 */ E(F.imp,    F.op_tay,    2),
  /* A9 */ E(F.imm,    F.op_lda,    2),
  /* AA */ E(F.imp,    F.op_tax,    2),
  /* AB */ E(F.imm,    F.op_nop,    2),
  /* AC */ E(F.abso,   F.op_ldy,    4),
  /* AD */ E(F.abso,   F.op_lda,    4),
  /* AE */ E(F.abso,   F.op_ldx,    4),
  /* AF */ E(F.abso,   F.op_lax,    4),

  /* B0 */ E(F.rel,    F.op_bcs,    2),
  /* B1 */ E(F.indy_p, F.op_lda,    5),
  /* B2 */ E(F.zpi,    F.op_lda,    5),
  /* B3 */ E(F.indy_p, F.op_lax,    5),
  /* B4 */ E(F.zpx,    F.op_ldy,    4),
  /* B5 */ E(F.zpx,    F.op_lda,    4),
  /* B6 */ E(F.zpy,    F.op_ldx,    4),
  /* B7 */ E(F.zpy,    F.op_lax,    4),
  /* B8 */ E(F.imp,    F.op_clv,    2),
  /* B9 */ E(F.absy_p, F.op_lda,    4),
  /* BA */ E(F.imp,    F.op_tsx,    2),
  /* BB */ E(F.absy_p, F.op_lax,    4),
  /* BC */ E(F.absx_p, F.op_ldy,    4),
  /* BD */ E(F.absx_p, F.op_lda,    4),
  /* BE */ E(F.absy_p, F.op_ldx,    4),
  /* BF */ E(F.absy_p, F.op_lax,    4),

  /* C0 */ E(F.imm,    F.op_cpy,    2),
  /* C1 */ E(F.indx,   F.op_cmp,    6),
  /* C2 */ E(F.imm,    F.op_nop,    2),
  /* C3 */ E(F.indx,   F.op_dcp,    8),
  /* C4 */ E(F.zp,     F.op_cpy,    3),
  /* C5 */ E(F.zp,     F.op_cmp,    3),
  /* C6 */ E(F.zp,     F.op_dec,    5),
  /* C7 */ E(F.zp,     F.op_dcp,    5),
  /* C8 */ E(F.imp,    F.op_iny,    2),
  /* C9 */ E(F.imm,    F.op_cmp,    2),
  /* CA */ E(F.imp,    F.op_dex,    2),
  /* CB */ E(F.imm,    F.op_nop,    2),
  /* CC */ E(F.abso,   F.op_cpy,    4),
  /* CD */ E(F.abso,   F.op_cmp,    4),
  /* CE */ E(F.abso,   F.op_dec,    6),
  /* CF */ E(F.abso,   F.op_dcp,    6),

  /* D0 */ E(F.rel,    F.op_bne,    2),
  /* D1 */ E(F.indy_p, F.op_cmp,    5),
  /* D2 */ E(F.zpi,    F.op_cmp,    5),
  /* D3 */ E(F.indy,   F.op_dcp,    8),
  /* D4 */ E(F.zpx,    F.op_nop,    4),
  /* D5 */ E(F.zpx,    F.op_cmp,    4),
  /* D6 */ E(F.zpx,    F.op_dec,    6),
  /* D7 */ E(F.zpx,    F.op_dcp,    6),
  /* D8 */ E(F.imp,    F.op_cld,    2),
  /* D9 */ E(F.absy_p, F.op_cmp,    4),
  /* DA */ E(F.imp,    F.op_phx,    3),
  /* DB */ E(F.absy,   F.op_dcp,    7),
  /* DC */ E(F.absx,   F.op_nop,    4),
  /* DD */ E(F.absx_p, F.op_cmp,    4),
  /* DE */ E(F.absx,   F.op_dec,    7),
  /* DF */ E(F.absx,   F.op_dcp,    7),

  /* E0 */ E(F.imm,    F.op_cpx,    2),
  /* E1 */ E(F.indx,   F.op_sbc,    6),
  /* E2 */ E(F.imm,    F.op_nop,    2),
  /* E3 */ E(F.indx,   F.op_isb,    8),
  /* E4 */ E(F.zp,     F.op_cpx,    3),
  /* E5 */ E(F.zp,     F.op_sbc,    3),
  /* E6 */ E(F.zp,     F.op_inc,    5),
  /* E7 */ E(F.zp,     F.op_isb,    5),
  /* E8 */ E(F.imp,    F.op_inx,    2),
  /* E9 */ E(F.imm,    F.op_sbc,    2),
  /* EA */ E(F.imp,    F.op_nop,    2),
  /* EB */ E(F.imm,    F.op_sbc,    2),
  /* EC */ E(F.abso,   F.op_cpx,    4),
  /* ED */ E(F.abso,   F.op_sbc,    4),
  /* EE */ E(F.abso,   F.op_inc,    6),
  /* EF */ E(F.abso,   F.op_isb,    6),

  /* F0 */ E(F.rel,    F.op_beq,    2),
  /* F1 */ E(F.indy_p, F.op_sbc,    5),
  /* F2 */ E(F.zpi,    F.op_sbc,    5),
  /* F3 */ E(F.indy,   F.op_isb,    8),
  /* F4 */ E(F.zpx,    F.op_nop,    4),
  /* F5 */ E(F.zpx,    F.op_sbc,    4),
  /* F6 */ E(F.zpx,    F.op_inc,    6),
  /* F7 */ E(F.zpx,    F.op_isb,    6),
  /* F8 */ E(F.imp,    F.op_sed,    2),
  /* F9 */ E(F.absy_p, F.op_sbc,    4),
  /* FA */ E(F.imp,    F.op_plx,    2),
  /* FB */ E(F.absy,   F.op_isb,    7),
  /* FC */ E(F.absx,   F.op_nop,    4),
  /* FD */ E(F.absx_p, F.op_sbc,    4),
  /* FE */ E(F.absx,   F.op_inc,    7),
  /* FF */ E(F.absx,   F.op_isb,    7),
];

if (OPCODES.length !== 256) {
  throw new Error(`Opcode table must have 256 entries, got ${OPCODES.length}`);
}
