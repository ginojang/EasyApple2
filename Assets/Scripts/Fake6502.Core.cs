using System.Runtime.CompilerServices;
using System;

public sealed partial class Fake6502
{
    // implied
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void imp(Fake6502 c)
    {
        // implied: no EA
        // (some emus keep EA unchanged; we follow C which does nothing)
    }

    // accumulator
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void acc(Fake6502 c)
    {
        // accumulator: operand is A (no EA)
        // (C version does nothing)
    }

    // immediate: EA points to immediate byte in instruction stream
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void imm(Fake6502 c)
    {
        c.emu.EA = c.cpu.PC;
        c.cpu.PC++;
    }

    // zero-page
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void zp(Fake6502 c)
    {
        // EA = mem[PC], PC++
        c.emu.EA = c.Read(c.cpu.PC);
        c.cpu.PC++;
    }

    // zero-page,X (wraparound)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void zpx(Fake6502 c)
    {
        // EA = (mem[PC] + X) & 0xFF, PC++
        byte baseZp = c.Read(c.cpu.PC);
        c.cpu.PC++;

        c.emu.EA = (ushort)((baseZp + c.cpu.X) & 0xFF);
    }

    // zero-page,Y (wraparound)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void zpy(Fake6502 c)
    {
        // EA = (mem[PC] + Y) & 0xFF, PC++
        byte baseZp = c.Read(c.cpu.PC);
        c.cpu.PC++;

        c.emu.EA = (ushort)((baseZp + c.cpu.Y) & 0xFF);
    }
    
    // ------------------------------------------------------------
    // relative (branch): 8-bit immediate, sign-extended
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void rel(Fake6502 c)
    {
        // C:
        // uint16_t rel = mem_read(pc++);
        // if (rel & 0x80) rel |= 0xFF00;
        // ea = pc + rel;

        byte off8 = c.Read(c.cpu.PC);
        c.cpu.PC++;

        short rel16 = (sbyte)off8;                 // sign-extend
        c.emu.EA = (ushort)(c.cpu.PC + rel16);     // wrap naturally to 16-bit
    }

    // ------------------------------------------------------------
    // absolute
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void abso(Fake6502 c)
    {
        c.emu.EA = c.Read16(c.cpu.PC);
        c.cpu.PC += 2;
    }

    // ------------------------------------------------------------
    // absolute,X
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void absx(Fake6502 c)
    {
        c.emu.EA = c.Read16(c.cpu.PC);
        c.emu.EA = (ushort)(c.emu.EA + c.cpu.X);
        c.cpu.PC += 2;
    }

    // ------------------------------------------------------------
    // absolute,X with cycle penalty on page cross
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void absx_p(Fake6502 c)
    {
        ushort ea = c.Read16(c.cpu.PC);
        ushort startPage = (ushort)(ea & 0xFF00);

        ea = (ushort)(ea + c.cpu.X);

        if (startPage != (ea & 0xFF00))
            c.emu.ClockTicks++;

        c.emu.EA = ea;
        c.cpu.PC += 2;
    }

    // ------------------------------------------------------------
    // (absolute,X) : read pointer from (abs + X)
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void absxi(Fake6502 c)
    {
        ushort ea = c.Read16(c.cpu.PC);
        ea = (ushort)(ea + c.cpu.X);
        ea = c.Read16(ea);

        c.emu.EA = ea;
        c.cpu.PC += 2;
    }

    // ------------------------------------------------------------
    // absolute,Y
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void absy(Fake6502 c)
    {
        c.emu.EA = c.Read16(c.cpu.PC);
        c.emu.EA = (ushort)(c.emu.EA + c.cpu.Y);
        c.cpu.PC += 2;
    }

    // ------------------------------------------------------------
    // absolute,Y with cycle penalty on page cross
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void absy_p(Fake6502 c)
    {
        ushort ea = c.Read16(c.cpu.PC);
        ushort startPage = (ushort)(ea & 0xFF00);

        ea = (ushort)(ea + c.cpu.Y);

        if (startPage != (ea & 0xFF00))
            c.emu.ClockTicks++;

        c.emu.EA = ea;
        c.cpu.PC += 2;
    }

    // indirect
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ind(Fake6502 c)
    {
        ushort eahelp = c.Read16(c.cpu.PC);

        // C 코드의 "if (LSB==0xFF) clockticks++" 그대로
        if ((eahelp & 0x00FF) == 0x00FF)
            c.emu.ClockTicks++;

        c.emu.EA = c.Read16(eahelp);
        c.cpu.PC += 2;
    }

    // ------------------------------------------------------------
    // (indirect,X)
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void indx(Fake6502 c)
    {
        // eahelp = (mem[PC++] + X) & 0xFF
        byte zpBase = c.Read(c.cpu.PC);
        c.cpu.PC++;

        ushort eahelp = (ushort)((zpBase + c.cpu.X) & 0xFF);

        // EA = mem[eahelp] | (mem[(eahelp+1)&0xFF] << 8)
        byte lo = c.Read((ushort)(eahelp & 0x00FF));
        byte hi = c.Read((ushort)((eahelp + 1) & 0x00FF));

        c.emu.EA = (ushort)(lo | (hi << 8));
    }

    // ------------------------------------------------------------
    // (indirect),Y
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void indy(Fake6502 c)
    {
        ushort eahelp = c.Read(c.cpu.PC);
        c.cpu.PC++;

        // zero-page wraparound for pointer high byte
        ushort eahelp2 = (ushort)((eahelp & 0xFF00) | ((eahelp + 1) & 0x00FF));

        byte lo = c.Read(eahelp);
        byte hi = c.Read(eahelp2);

        ushort ea = (ushort)(lo | (hi << 8));
        ea = (ushort)(ea + c.cpu.Y);

        c.emu.EA = ea;
    }

    // ------------------------------------------------------------
    // (indirect),Y with page-cross cycle penalty
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void indy_p(Fake6502 c)
    {
        ushort eahelp = c.Read(c.cpu.PC);
        c.cpu.PC++;

        ushort eahelp2 = (ushort)((eahelp & 0xFF00) | ((eahelp + 1) & 0x00FF));

        byte lo = c.Read(eahelp);
        byte hi = c.Read(eahelp2);

        ushort ea = (ushort)(lo | (hi << 8));
        ushort startPage = (ushort)(ea & 0xFF00);

        ea = (ushort)(ea + c.cpu.Y);

        if (startPage != (ea & 0xFF00))
            c.emu.ClockTicks++;

        c.emu.EA = ea;
    }

    // ------------------------------------------------------------
    // (zp) : 65C02-style zero-page indirect (no index)
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void zpi(Fake6502 c)
    {
        ushort eahelp = c.Read(c.cpu.PC);
        c.cpu.PC++;

        ushort eahelp2 = (ushort)((eahelp & 0xFF00) | ((eahelp + 1) & 0x00FF));

        byte lo = c.Read(eahelp);
        byte hi = c.Read(eahelp2);

        c.emu.EA = (ushort)(lo | (hi << 8));
    }




    /// <summary>
    /// 
    /// </summary>
    /// 
    /// 

    // ------------------------------------------------------------
    // ADC
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void adc(Fake6502 c)
    {
        ushort value = c.GetValue();
        bool carry = (c.cpu.Flags & CARRY_FLAG) != 0;

        c.AccumSave(c.Add8(c.cpu.A, value, carry));
    }

    // ------------------------------------------------------------
    // AND  (추천: 이름 충돌/가독성 때문에 and_로 해도 됨)
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void and(Fake6502 c)
    {
        byte m = (byte)c.GetValue();
        c.AccumSave(c.BooleanAnd(c.cpu.A, m));
    }

    // ------------------------------------------------------------
    // ASL
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void asl(Fake6502 c)
    {
        byte v = (byte)c.GetValue();
        c.PutValue(c.ArithmeticShiftLeft(v));
    }

    // ------------------------------------------------------------
    // BRA (branch helper used by conditional branches)
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void bra(Fake6502 c)
    {
        ushort oldpc = c.cpu.PC;
        c.cpu.PC = c.emu.EA;

        // check if jump crossed a page boundary
        if ((oldpc & 0xFF00) != (c.cpu.PC & 0xFF00))
            c.emu.ClockTicks += 2;
        else
            c.emu.ClockTicks += 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void bcc(Fake6502 c)
    {
        if ((c.cpu.Flags & CARRY_FLAG) == 0)
            bra(c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void bcs(Fake6502 c)
    {
        if ((c.cpu.Flags & CARRY_FLAG) == CARRY_FLAG)
            bra(c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void beq(Fake6502 c)
    {
        if ((c.cpu.Flags & ZERO_FLAG) == ZERO_FLAG)
            bra(c);
    }

    // ------------------------------------------------------------
    // BIT (Z = A & M, N/V copied from M[7:6])
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void bit(Fake6502 c)
    {
        byte value = (byte)c.GetValue();
        byte result = (byte)(c.cpu.A & value);

        c.ZeroCalc(result);

        // flags = (flags & 0x3F) | (value & 0xC0)
        // 0x3F keeps bits 0..5, replace N/V (bits 7..6) from memory value
        c.cpu.Flags = (byte)((c.cpu.Flags & 0x3F) | (value & 0xC0));
    }

    // immediate BIT variant: only affects Z in this implementation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void bit_imm(Fake6502 c)
    {
        byte value = (byte)c.GetValue();
        byte result = (byte)(c.cpu.A & value);
        c.ZeroCalc(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void bmi(Fake6502 c)
    {
        if ((c.cpu.Flags & SIGN_FLAG) == SIGN_FLAG)
            bra(c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void bne(Fake6502 c)
    {
        if ((c.cpu.Flags & ZERO_FLAG) == 0)
            bra(c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void bpl(Fake6502 c)
    {
        if ((c.cpu.Flags & SIGN_FLAG) == 0)
            bra(c);
    }

    // ------------------------------------------------------------
    // BRK
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void brk(Fake6502 c)
    {
        c.cpu.PC++; // C 코드 그대로: PC++

        // push next instruction address onto stack
        c.Push16(c.cpu.PC);

        // push CPU flags to stack (B flag set)
        c.Push8((byte)(c.cpu.Flags | BREAK_FLAG));

        // set interrupt flag
        c.cpu.Flags = (byte)(c.cpu.Flags | INTERRUPT_FLAG);

        // jump to IRQ vector
        c.cpu.PC = c.Read16(0xFFFE);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void bvc(Fake6502 c)
    {
        if ((c.cpu.Flags & OVERFLOW_FLAG) == 0)
            bra(c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void bvs(Fake6502 c)
    {
        if ((c.cpu.Flags & OVERFLOW_FLAG) == OVERFLOW_FLAG)
            bra(c);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void clc(Fake6502 c)
    {
        c.CarryClear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void cld(Fake6502 c)
    {
        c.DecimalClear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void cli(Fake6502 c)
    {
        c.InterruptClear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void clv(Fake6502 c)
    {
        c.OverflowClear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void cmp(Fake6502 c)
    {
        c.Compare(c.cpu.A);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void cpx(Fake6502 c)
    {
        c.Compare(c.cpu.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void cpy(Fake6502 c)
    {
        c.Compare(c.cpu.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void dec(Fake6502 c)
    {
        byte v = (byte)c.GetValue();
        c.PutValue(c.Decrement(v));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void dex(Fake6502 c)
    {
        c.cpu.X = c.Decrement(c.cpu.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void dey(Fake6502 c)
    {
        c.cpu.Y = c.Decrement(c.cpu.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void eor(Fake6502 c)
    {
        byte m = (byte)c.GetValue();
        c.AccumSave(c.ExclusiveOr(c.cpu.A, m));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void inc(Fake6502 c)
    {
        byte v = (byte)c.GetValue();
        c.PutValue(c.Increment(v));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void inx(Fake6502 c)
    {
        c.cpu.X = c.Increment(c.cpu.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void iny(Fake6502 c)
    {
        c.cpu.Y = c.Increment(c.cpu.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void jmp(Fake6502 c)
    {
        c.cpu.PC = c.emu.EA;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void jsr(Fake6502 c)
    {
        // 6502 JSR pushes (PC-1) high then low (우리 Push16이 hi->lo)
        c.Push16((ushort)(c.cpu.PC - 1));
        c.cpu.PC = c.emu.EA;
    }

    // ------------------------------------------------------------
    // LDA / LDX / LDY
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void lda(Fake6502 c)
    {
        byte v = (byte)c.GetValue();
        c.cpu.A = v;
        c.ZeroCalc(v);
        c.SignCalc(v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ldx(Fake6502 c)
    {
        byte v = (byte)c.GetValue();
        c.cpu.X = v;
        c.ZeroCalc(v);
        c.SignCalc(v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ldy(Fake6502 c)
    {
        byte v = (byte)c.GetValue();
        c.cpu.Y = v;
        c.ZeroCalc(v);
        c.SignCalc(v);
    }

    // ------------------------------------------------------------
    // LSR
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void lsr(Fake6502 c)
    {
        byte v = (byte)c.GetValue();
        c.PutValue(c.LogicalShiftRight(v));
    }

    // ------------------------------------------------------------
    // NOP
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void nop(Fake6502 c)
    {
        // intentionally empty
    }

    // ------------------------------------------------------------
    // ORA
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ora(Fake6502 c)
    {
        byte m = (byte)c.GetValue();
        ushort result = (ushort)(c.cpu.A | m);

        c.ZeroCalc(result);
        c.SignCalc(result);

        c.AccumSave((byte)result);
    }

    // ------------------------------------------------------------
    // Stack operations
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void pha(Fake6502 c)
    {
        c.Push8(c.cpu.A);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void phx(Fake6502 c)
    {
        c.Push8(c.cpu.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void phy(Fake6502 c)
    {
        c.Push8(c.cpu.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void php(Fake6502 c)
    {
        c.Push8((byte)(c.cpu.Flags | BREAK_FLAG));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void pla(Fake6502 c)
    {
        byte v = c.Pull8();
        c.cpu.A = v;
        c.ZeroCalc(v);
        c.SignCalc(v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void plx(Fake6502 c)
    {
        byte v = c.Pull8();
        c.cpu.X = v;
        c.ZeroCalc(v);
        c.SignCalc(v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ply(Fake6502 c)
    {
        byte v = c.Pull8();
        c.cpu.Y = v;
        c.ZeroCalc(v);
        c.SignCalc(v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void plp(Fake6502 c)
    {
        // pull flags, then force CONSTANT and BREAK flags
        c.cpu.Flags = (byte)(c.Pull8() | CONSTANT_FLAG | BREAK_FLAG);
    }

    // ------------------------------------------------------------
    // ROL / ROR (read-modify-write; includes dummy write)
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void rol(Fake6502 c)
    {
        ushort value = c.GetValue();

        // dummy write (keep as C)
        c.PutValue(value);

        c.PutValue(c.RotateLeft(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ror(Fake6502 c)
    {
        ushort value = c.GetValue();

        // dummy write (keep as C)
        c.PutValue(value);

        c.PutValue(c.RotateRight(value));
    }

    // ------------------------------------------------------------
    // RTI / RTS
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void rti(Fake6502 c)
    {
        c.cpu.Flags = (byte)(c.Pull8() | CONSTANT_FLAG | BREAK_FLAG);
        c.cpu.PC = c.Pull16();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void rts(Fake6502 c)
    {
        c.cpu.PC = (ushort)(c.Pull16() + 1);
    }

    // ------------------------------------------------------------
    // SBC
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sbc(Fake6502 c)
    {
        ushort value = (ushort)(c.GetValue() ^ 0x00FF); // ones complement

//#if DECIMALMODE
        // Apply decimal mode fix from http://forum.6502.org/viewtopic.php?p=37758#p37758
        if ((c.cpu.Flags & DECIMAL_FLAG) != 0)
            value = (ushort)(value - 0x0066); // use nines complement for BCD
//#endif

        bool carry = (c.cpu.Flags & CARRY_FLAG) != 0;
        c.AccumSave(c.Add8(c.cpu.A, value, carry));
    }

    // ------------------------------------------------------------
    // SEC / SED / SEI
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sec(Fake6502 c) => c.CarrySet();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sed(Fake6502 c) => c.DecimalSet();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sei(Fake6502 c) => c.InterruptSet();

    // ------------------------------------------------------------
    // Stores
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sta(Fake6502 c) => c.PutValue(c.cpu.A);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void stx(Fake6502 c) => c.PutValue(c.cpu.X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sty(Fake6502 c) => c.PutValue(c.cpu.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void stz(Fake6502 c) => c.PutValue(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void tax(Fake6502 c)
    {
        c.cpu.X = c.cpu.A;
        c.ZeroCalc(c.cpu.X);
        c.SignCalc(c.cpu.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void tay(Fake6502 c)
    {
        c.cpu.Y = c.cpu.A;
        c.ZeroCalc(c.cpu.Y);
        c.SignCalc(c.cpu.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void tsx(Fake6502 c)
    {
        c.cpu.X = c.cpu.S;
        c.ZeroCalc(c.cpu.X);
        c.SignCalc(c.cpu.X);
    }

    // ------------------------------------------------------------
    // TRB (65C02) - keep semantics exactly as your C code
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void trb(Fake6502 c)
    {
        ushort value = c.GetValue();
        ushort result = (ushort)(c.cpu.A & ~value);

        c.PutValue(result);

        // C 그대로: zero_calc((A | result) & 0xFF)
        byte z = (byte)((c.cpu.A | result) & 0x00FF);
        c.ZeroCalc(z);
    }

    // ------------------------------------------------------------
    // TSB (65C02) - keep semantics exactly as your C code
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void tsb(Fake6502 c)
    {
        ushort value = c.GetValue();
        ushort result = (ushort)(c.cpu.A | value);

        c.PutValue(result);

        byte z = (byte)((c.cpu.A | result) & 0x00FF);
        c.ZeroCalc(z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void txa(Fake6502 c)
    {
        c.cpu.A = c.cpu.X;
        c.ZeroCalc(c.cpu.A);
        c.SignCalc(c.cpu.A);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void txs(Fake6502 c)
    {
        c.cpu.S = c.cpu.X;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void tya(Fake6502 c)
    {
        c.cpu.A = c.cpu.Y;
        c.ZeroCalc(c.cpu.A);
        c.SignCalc(c.cpu.A);
    }

    // ------------------------------------------------------------
    // LAX (undocumented): A = X = M
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void lax(Fake6502 c)
    {
        byte v = (byte)c.GetValue();
        c.cpu.A = v;
        c.cpu.X = v;

        c.ZeroCalc(v);
        c.SignCalc(v);
    }

    // ------------------------------------------------------------
    // SAX (undocumented): M = A & X
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sax(Fake6502 c)
    {
        c.PutValue((ushort)(c.cpu.A & c.cpu.X));
    }

    // ------------------------------------------------------------
    // DCP (undocumented): DEC + CMP
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void dcp(Fake6502 c)
    {
        dec(c);
        cmp(c);
    }

    // ------------------------------------------------------------
    // ISB / ISC (undocumented): INC + SBC
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void isb(Fake6502 c)
    {
        inc(c);
        sbc(c);
    }

    // ------------------------------------------------------------
    // SLO (undocumented): ASL + ORA
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void slo(Fake6502 c)
    {
        asl(c);
        ora(c);
    }

    // ------------------------------------------------------------
    // RLA (undocumented): ROL + AND
    // (keep dummy write semantics exactly)
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void rla(Fake6502 c)
    {
        ushort value = c.GetValue();
        byte result = c.RotateLeft(value);

        // dummy write + real write
        c.PutValue(value);
        c.PutValue(result);

        c.AccumSave(c.BooleanAnd(c.cpu.A, result));
    }

    // ------------------------------------------------------------
    // SRE (undocumented): LSR + EOR
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sre(Fake6502 c)
    {
        ushort value = c.GetValue();
        byte result = c.LogicalShiftRight((byte)value);

        // dummy write + real write
        c.PutValue(value);
        c.PutValue(result);

        c.AccumSave(c.ExclusiveOr(c.cpu.A, result));
    }

    // ------------------------------------------------------------
    // RRA (undocumented): ROR + ADC
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void rra(Fake6502 c)
    {
        ushort value = c.GetValue();
        byte result = c.RotateRight(value);

        // dummy write + real write
        c.PutValue(value);
        c.PutValue(result);

        bool carry = (c.cpu.Flags & CARRY_FLAG) != 0;
        c.AccumSave(c.Add8(c.cpu.A, result, carry));
    }



    public static OpcodeEntry[] Opcodes = new OpcodeEntry[256]
    {
            /* 00 */ new OpcodeEntry(imp, brk, 7),
            /* 01 */ new OpcodeEntry(indx, ora, 6),
            /* 02 */ new OpcodeEntry(imp, nop, 2),
            /* 03 */ new OpcodeEntry(indx, slo, 8),
            /* 04 */ new OpcodeEntry(zp, tsb, 5),
             
            /* 05 */ new OpcodeEntry(zp, ora, 3),
            /* 06 */ new OpcodeEntry(zp, asl, 5),
            /* 07 */ new OpcodeEntry(zp, slo, 5),
            /* 08 */ new OpcodeEntry(imp, php, 3),
            /* 09 */ new OpcodeEntry(imm, ora, 2),
            /* 0A */ new OpcodeEntry(acc, asl, 2),
            /* 0B */ new OpcodeEntry(imm, nop, 2),
            /* 0C */ new OpcodeEntry(abso, tsb, 6),
            /* 0D */ new OpcodeEntry(abso, ora, 4),
            /* 0E */ new OpcodeEntry(abso, asl, 6),
            /* 0F */ new OpcodeEntry(abso, slo, 6),

        /* 01 */
        new OpcodeEntry(rel, bpl, 2),
        new OpcodeEntry(indy_p, ora, 5),
        new OpcodeEntry(zpi, ora, 5),
        new OpcodeEntry(indy, slo, 8),
        new OpcodeEntry(zp, trb, 5),
        new OpcodeEntry(zpx, ora, 4),
        new OpcodeEntry(zpx, asl, 6),
        new OpcodeEntry(zpx, slo, 6),
        new OpcodeEntry(imp, clc, 2),
        new OpcodeEntry(absy_p, ora, 4),
        new OpcodeEntry(acc, inc, 2),
        new OpcodeEntry(absy, slo, 7),
        new OpcodeEntry(abso, trb, 6),
        new OpcodeEntry(absx_p, ora, 4),
        new OpcodeEntry(absx, asl, 7),
        new OpcodeEntry(absx, slo, 7),
        /* 02 */
        new OpcodeEntry(abso, jsr, 6),
        new OpcodeEntry(indx, and, 6),
        new OpcodeEntry(imp, nop, 2),
        new OpcodeEntry(indx, rla, 8),
        new OpcodeEntry(zp, bit, 3),
        new OpcodeEntry(zp, and, 3),
        new OpcodeEntry(zp, rol, 5),
        new OpcodeEntry(zp, rla, 5),
        new OpcodeEntry(imp, plp, 4),
        new OpcodeEntry(imm, and, 2),
        new OpcodeEntry(acc, rol, 2),
        new OpcodeEntry(imm, nop, 2),
        new OpcodeEntry(abso, bit, 4),
        new OpcodeEntry(abso, and, 4),
        new OpcodeEntry(abso, rol, 6),
        new OpcodeEntry(abso, rla, 6),
        /* 30 */
        new OpcodeEntry(rel, bmi, 2),
        new OpcodeEntry(indy_p, and, 5),
        new OpcodeEntry(zpi, adc, 5),
        new OpcodeEntry(indy, rla, 8),
        new OpcodeEntry(zpx, bit, 4),
        new OpcodeEntry(zpx, and, 4),
        new OpcodeEntry(zpx, rol, 6),
        new OpcodeEntry(zpx, rla, 6),
        new OpcodeEntry(imp, sec, 2),
        new OpcodeEntry(absy_p, and, 4),
        new OpcodeEntry(acc, dec, 2),
        new OpcodeEntry(absy, rla, 7),
        new OpcodeEntry(absx_p, bit, 4),
        new OpcodeEntry(absx_p, and, 4),
        new OpcodeEntry(absx, rol, 7),
        new OpcodeEntry(absx, rla, 7),
        /* 40 */
        new OpcodeEntry(imp, rti, 6),
        new OpcodeEntry(indx, eor, 6),
        new OpcodeEntry(imp, nop, 2),
        new OpcodeEntry(indx, sre, 8),
        new OpcodeEntry(zp, nop, 3),
        new OpcodeEntry(zp, eor, 3),
        new OpcodeEntry(zp, lsr, 5),
        new OpcodeEntry(zp, sre, 5),
        new OpcodeEntry(imp, pha, 3),
        new OpcodeEntry(imm, eor, 2),
        new OpcodeEntry(acc, lsr, 2),
        new OpcodeEntry(imm, nop, 2),
        new OpcodeEntry(abso, jmp, 3),
        new OpcodeEntry(abso, eor, 4),
        new OpcodeEntry(abso, lsr, 6),
        new OpcodeEntry(abso, sre, 6),
        /* 50 */
        new OpcodeEntry(rel, bvc, 2),
        new OpcodeEntry(indy_p, eor, 5),
        new OpcodeEntry(zpi, eor, 5),
        new OpcodeEntry(indy, sre, 8),
        new OpcodeEntry(zpx, nop, 4),
        new OpcodeEntry(zpx, eor, 4),
        new OpcodeEntry(zpx, lsr, 6),
        new OpcodeEntry(zpx, sre, 6),
        new OpcodeEntry(imp, cli, 2),
        new OpcodeEntry(absy_p, eor, 4),
        new OpcodeEntry(imp, phy, 2),
        new OpcodeEntry(absy, sre, 7),
        new OpcodeEntry(absx, nop, 4),
        new OpcodeEntry(absx_p, eor, 4),
        new OpcodeEntry(absx, lsr, 7),
        new OpcodeEntry(absx, sre, 7),
        /* 60 */
        new OpcodeEntry(imp, rts, 6),
        new OpcodeEntry(indx, adc, 6),
        new OpcodeEntry(imp, nop, 2),
        new OpcodeEntry(indx, rra, 8),
        new OpcodeEntry(zp, stz, 3),
        new OpcodeEntry(zp, adc, 3),
        new OpcodeEntry(zp, ror, 5),
        new OpcodeEntry(zp, rra, 5),
        new OpcodeEntry(imp, pla, 4),
        new OpcodeEntry(imm, adc, 2),
        new OpcodeEntry(acc, ror, 2),
        new OpcodeEntry(imm, nop, 2),
        new OpcodeEntry(ind, jmp, 5),
        new OpcodeEntry(abso, adc, 4),
        new OpcodeEntry(abso, ror, 6),
        new OpcodeEntry(abso, rra, 6),
        /* 70 */
        new OpcodeEntry(rel, bvs, 2),
        new OpcodeEntry(indy_p, adc, 5),
        new OpcodeEntry(zpi, adc, 5),
        new OpcodeEntry(indy, rra, 8),
        new OpcodeEntry(zpx, stz, 4),
        new OpcodeEntry(zpx, adc, 4),
        new OpcodeEntry(zpx, ror, 6),
        new OpcodeEntry(zpx, rra, 6),
        new OpcodeEntry(imp, sei, 2),
        new OpcodeEntry(absy_p, adc, 4),
        new OpcodeEntry(imp, ply, 6),
        new OpcodeEntry(absy, rra, 7),
        new OpcodeEntry(absxi, jmp, 6),
        new OpcodeEntry(absx_p, adc, 4),
        new OpcodeEntry(absx, ror, 7),
        new OpcodeEntry(absx, rra, 7),
        /* 80 */
        new OpcodeEntry(rel, bra, 3),
        new OpcodeEntry(indx, sta, 6),
        new OpcodeEntry(imm, nop, 2),
        new OpcodeEntry(indx, sax, 6),
        new OpcodeEntry(zp, sty, 3),
        new OpcodeEntry(zp, sta, 3),
        new OpcodeEntry(zp, stx, 3),
        new OpcodeEntry(zp, sax, 3),
        new OpcodeEntry(imp, dey, 2),
        new OpcodeEntry(imm, bit_imm, 2),
        new OpcodeEntry(imp, txa, 2),
        new OpcodeEntry(imm, nop, 2),
        new OpcodeEntry(abso, sty, 4),
        new OpcodeEntry(abso, sta, 4),
        new OpcodeEntry(abso, stx, 4),
        new OpcodeEntry(abso, sax, 4),
        /* 90 */
        new OpcodeEntry(rel, bcc, 2),
        new OpcodeEntry(indy, sta, 6),
        new OpcodeEntry(zpi, sta, 5),
        new OpcodeEntry(indy, nop, 6),
        new OpcodeEntry(zpx, sty, 4),
        new OpcodeEntry(zpx, sta, 4),
        new OpcodeEntry(zpy, stx, 4),
        new OpcodeEntry(zpy, sax, 4),
        new OpcodeEntry(imp, tya, 2),
        new OpcodeEntry(absy, sta, 5),
        new OpcodeEntry(imp, txs, 2),
        new OpcodeEntry(absy, nop, 5),
        new OpcodeEntry(abso, stz, 4),
        new OpcodeEntry(absx, sta, 5),
        new OpcodeEntry(absx, stz, 5),
        new OpcodeEntry(absy, nop, 5),
        /* A0 */
        new OpcodeEntry(imm, ldy, 2),
        new OpcodeEntry(indx, lda, 6),
        new OpcodeEntry(imm, ldx, 2),
        new OpcodeEntry(indx, lax, 6),
        new OpcodeEntry(zp, ldy, 3),
        new OpcodeEntry(zp, lda, 3),
        new OpcodeEntry(zp, ldx, 3),
        new OpcodeEntry(zp, lax, 3),
        new OpcodeEntry(imp, tay, 2),
        new OpcodeEntry(imm, lda, 2),
        new OpcodeEntry(imp, tax, 2),
        new OpcodeEntry(imm, nop, 2),
        new OpcodeEntry(abso, ldy, 4),
        new OpcodeEntry(abso, lda, 4),
        new OpcodeEntry(abso, ldx, 4),
        new OpcodeEntry(abso, lax, 4),
        /* B0 */
        new OpcodeEntry(rel, bcs, 2),
        new OpcodeEntry(indy_p, lda, 5),
        new OpcodeEntry(zpi, lda, 5),
        new OpcodeEntry(indy_p, lax, 5),
        new OpcodeEntry(zpx, ldy, 4),
        new OpcodeEntry(zpx, lda, 4),
        new OpcodeEntry(zpy, ldx, 4),
        new OpcodeEntry(zpy, lax, 4),
        new OpcodeEntry(imp, clv, 2),
        new OpcodeEntry(absy_p, lda, 4),
        new OpcodeEntry(imp, tsx, 2),
        new OpcodeEntry(absy_p, lax, 4),
        new OpcodeEntry(absx_p, ldy, 4),    
        new OpcodeEntry(absx_p, lda, 4),
        new OpcodeEntry(absy_p, ldx, 4),
        new OpcodeEntry(absy_p, lax, 4),
        /* C0 */
        new OpcodeEntry(imm, cpy, 2),
        new OpcodeEntry(indx, cmp, 6),
        new OpcodeEntry(imm, nop, 2),
        new OpcodeEntry(indx, dcp, 8),
        new OpcodeEntry(zp, cpy, 3),
        new OpcodeEntry(zp, cmp, 3),
        new OpcodeEntry(zp, dec, 5),
        new OpcodeEntry(zp, dcp, 5),
        new OpcodeEntry(imp, iny, 2),
        new OpcodeEntry(imm, cmp, 2),
        new OpcodeEntry(imp, dex, 2),
        new OpcodeEntry(imm, nop, 2),
        new OpcodeEntry(abso, cpy, 4),
        new OpcodeEntry(abso, cmp, 4),
        new OpcodeEntry(abso, dec, 6),
        new OpcodeEntry(abso, dcp, 6),
        /* D0 */
        new OpcodeEntry(rel, bne, 2),
        new OpcodeEntry(indy_p, cmp, 5),
        new OpcodeEntry(zpi, cmp, 5),
        new OpcodeEntry(indy, dcp, 8),
        new OpcodeEntry(zpx, nop, 4),
        new OpcodeEntry(zpx, cmp, 4),
        new OpcodeEntry(zpx, dec, 6),
        new OpcodeEntry(zpx, dcp, 6),
        new OpcodeEntry(imp, cld, 2),
        new OpcodeEntry(absy_p, cmp, 4),
        new OpcodeEntry(imp, phx, 3),
        new OpcodeEntry(absy, dcp, 7),
        new OpcodeEntry(absx, nop, 4),
        new OpcodeEntry(absx_p, cmp, 4),
        new OpcodeEntry(absx, dec, 7),
        new OpcodeEntry(absx, dcp, 7),
        /* E0 */
        new OpcodeEntry(imm, cpx, 2),
        new OpcodeEntry(indx, sbc, 6),
        new OpcodeEntry(imm, nop, 2),
        new OpcodeEntry(indx, isb, 8),
        new OpcodeEntry(zp, cpx, 3),
        new OpcodeEntry(zp, sbc, 3),
        new OpcodeEntry(zp, inc, 5),
        new OpcodeEntry(zp, isb, 5),
        new OpcodeEntry(imp, inx, 2),
        new OpcodeEntry(imm, sbc, 2),
        new OpcodeEntry(imp, nop, 2),
        new OpcodeEntry(imm, sbc, 2),
        new OpcodeEntry(abso, cpx, 4),
        new OpcodeEntry(abso, sbc, 4),
        new OpcodeEntry(abso, inc, 6),
        new OpcodeEntry(abso, isb, 6),
        /* F0 */
        new OpcodeEntry(rel, beq, 2),
        new OpcodeEntry(indy_p, sbc, 5),
        new OpcodeEntry(zpi, sbc, 5),
        new OpcodeEntry(indy, isb, 8),
        new OpcodeEntry(zpx, nop, 4),
        new OpcodeEntry(zpx, sbc, 4),
        new OpcodeEntry(zpx, inc, 6),
        new OpcodeEntry(zpx, isb, 6),
        new OpcodeEntry(imp, sed, 2),
        new OpcodeEntry(absy_p, sbc, 4),
        new OpcodeEntry(imp, plx, 2),
        new OpcodeEntry(absy, isb, 7),
        new OpcodeEntry(absx, nop, 4),
        new OpcodeEntry(absx_p, sbc, 4),
        new OpcodeEntry(absx, inc, 7),
        new OpcodeEntry(absx, isb, 7),

    };


   
}
