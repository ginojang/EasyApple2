using System;
using System.Runtime.CompilerServices;

public sealed partial class Fake6502
{
    // ------------------------------------------------------------
    // Stack helpers (fake6502_push_8/16, pull_8/16)
    // ------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push8(byte value)
    {
        // mem[0x0100 + S] = value; S--
        Write((ushort)(STACK_BASE + cpu.S), value);
        cpu.S--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push16(ushort value)
    {
        // push hi then lo (same as C)
        Push8((byte)((value >> 8) & 0xFF));
        Push8((byte)(value & 0xFF));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Pull8()
    {
        // S++; return mem[0x0100 + S]
        cpu.S++;
        return Read((ushort)(STACK_BASE + cpu.S));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort Pull16()
    {
        // low first, then high (same as C)
        byte lo = Pull8();
        byte hi = Pull8();
        return (ushort)((hi << 8) | lo);
    }

    // ------------------------------------------------------------
    // Memory helpers
    // ------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort Read16(ushort addr)
    {
        // Read two consecutive bytes from memory (no JMP(ind) bug here)
        byte lo = Read(addr);
        byte hi = Read((ushort)(addr + 1));
        return (ushort)(lo | (hi << 8));
    }

    // ------------------------------------------------------------
    // Get/Put operand value (acc vs memory) - C와 동일 로직
    // ------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetValue()
    {
        // if (opcodes[emu.opcode].addr_mode == acc) return A else mem[EA]
        var entry = Opcodes[emu.Opcode];
        if (entry.AddrMode == acc)
            return cpu.A;

        return Read(emu.EA);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PutValue(ushort saveVal)
    {
        var entry = Opcodes[emu.Opcode];
        byte v8 = (byte)(saveVal & 0x00FF);

        if (entry.AddrMode == acc)
            cpu.A = v8;
        else
            Write(emu.EA, v8);
    }

    // ------------------------------------------------------------
    // ALU helpers (add8, rotates, shifts, logic, inc/dec, compare)
    // ------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Add8(ushort a, ushort b, bool carry)
    {
        ushort result = (ushort)(a + b + (carry ? 1 : 0));

        ZeroCalc(result);
        OverflowCalc(result, (byte)a, b);
        SignCalc(result);

#if DECIMALMODE
        // Apply decimal mode fix from http://forum.6502.org/viewtopic.php?p=37758#p37758
        if ((cpu.Flags & DECIMAL_FLAG) != 0)
        {
            // result += ((((result + 0x66) ^ a ^ b) >> 3) & 0x22) * 3;
            ushort tmp = (ushort)(((ushort)(result + 0x66) ^ a ^ b) >> 3);
            result = (ushort)(result + ((tmp & 0x22) * 3));
        }
#endif

        CarryCalc(result);
        return (byte)result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte RotateRight(ushort value)
    {
        ushort result = (ushort)((value >> 1) | ((cpu.Flags & CARRY_FLAG) << 7));

        if ((value & 1) != 0) CarrySet();
        else CarryClear();

        ZeroCalc(result);
        SignCalc(result);

        return (byte)result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte RotateLeft(ushort value)
    {
        ushort result = (ushort)((value << 1) | (cpu.Flags & CARRY_FLAG));

        CarryCalc(result);
        ZeroCalc(result);
        SignCalc(result);

        return (byte)result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte LogicalShiftRight(byte value)
    {
        ushort result = (ushort)(value >> 1);

        if ((value & 1) != 0) CarrySet();
        else CarryClear();

        ZeroCalc(result);
        SignCalc(result);

        return (byte)result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ArithmeticShiftLeft(byte value)
    {
        ushort result = (ushort)(value << 1);

        CarryCalc(result);
        ZeroCalc(result);
        SignCalc(result);

        return (byte)result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ExclusiveOr(byte a, byte b)
    {
        ushort result = (ushort)(a ^ b);

        ZeroCalc(result);
        SignCalc(result);

        return (byte)result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte BooleanAnd(byte a, byte b)
    {
        ushort result = (ushort)(a & b);

        ZeroCalc(result);
        SignCalc(result);

        return (byte)result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Increment(byte r)
    {
        ushort result = (ushort)(r + 1);
        ZeroCalc(result);
        SignCalc(result);
        return (byte)result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Decrement(byte r)
    {
        ushort result = (ushort)(r - 1);
        ZeroCalc(result);
        SignCalc(result);
        return (byte)result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Compare(ushort r)
    {
        ushort value = GetValue();
        ushort result = (ushort)(r - value);

        if (r >= (byte)(value & 0x00FF)) CarrySet();
        else CarryClear();

        if (r == (byte)(value & 0x00FF)) ZeroSet();
        else ZeroClear();

        SignCalc(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Nmi()
    {
        // push PC, push flags(without B), set I, PC = [FFFA]
        Push16(cpu.PC);
        Push8((byte)(cpu.Flags & ~BREAK_FLAG));

        cpu.Flags = (byte)(cpu.Flags | INTERRUPT_FLAG);
        cpu.PC = Read16(0xFFFA);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Irq()
    {
        // if I flag == 0
        if ((cpu.Flags & INTERRUPT_FLAG) == 0)
        {
            Push16(cpu.PC);
            Push8((byte)(cpu.Flags & ~BREAK_FLAG));

            cpu.Flags = (byte)(cpu.Flags | INTERRUPT_FLAG);
            cpu.PC = Read16(0xFFFE);
        }
    }



    // ------------------------------------------------------------
    // fake6502_reset 포팅
    // ------------------------------------------------------------
    public void Reset()
    {
        // The 6502 normally does some fake reads after reset because
        // reset is a hacked-up version of NMI/IRQ/BRK
        // See https://www.pagetable.com/?p=410

        // fake reads (same sequence)
        Read(0x00FF);
        Read(0x00FF);
        Read(0x00FF);
        Read(0x0100);
        Read(0x01FF);
        Read(0x01FE);

        cpu.PC = Read16(0xFFFC);
        cpu.S = 0xFD;

        // flags |= CONSTANT | INTERRUPT
        cpu.Flags = (byte)(cpu.Flags | CONSTANT_FLAG | INTERRUPT_FLAG);

        emu.Instructions = 0;
        emu.ClockTicks = 0;
    }

    public int Step()
    {
        // opcode fetch
        byte opcode = Read(cpu.PC);
        cpu.PC++;

        emu.Opcode = opcode;

        // flags |= CONSTANT
        cpu.Flags = (byte)(cpu.Flags | CONSTANT_FLAG);

        // dispatch via table
        // (table이 아직 비어있으면 여기서 터지게 해서 빠르게 잡자)
        if (Opcodes == null || Opcodes.Length < 256)
            throw new InvalidOperationException("Fake6502.Opcodes[] must be initialized with 256 entries.");

        var entry = Opcodes[opcode];
        if (entry.AddrMode == null || entry.Op == null)
            throw new NotImplementedException($"Opcode ${opcode:X2} missing handler(s).");

        entry.AddrMode(this);
        entry.Op(this);

        // clockticks accumulate (C 그대로)
        emu.ClockTicks += entry.ClockTicks;

        // Unity 메인 루프는 cycles를 더하면 되므로, 이번 Step에서 사용한 사이클만 반환
        int used = emu.ClockTicks;
        emu.ClockTicks = 0;

        emu.Instructions++;
        return used;
    }
}
