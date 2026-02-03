using System;
using System.Runtime.CompilerServices;

public sealed partial class Fake6502
{
    // ------------------------------------------------------------
    // Flags (FAKE6502_*_FLAG)
    // ------------------------------------------------------------
    public const byte CARRY_FLAG = 0x01;
    public const byte ZERO_FLAG = 0x02;
    public const byte INTERRUPT_FLAG = 0x04;
    public const byte DECIMAL_FLAG = 0x08;
    public const byte BREAK_FLAG = 0x10;
    public const byte CONSTANT_FLAG = 0x20;
    public const byte OVERFLOW_FLAG = 0x40;
    public const byte SIGN_FLAG = 0x80;

    public const ushort STACK_BASE = 0x0100;

    // ------------------------------------------------------------
    // State structs (fake6502_cpu_state / fake6502_emu_state)
    // ------------------------------------------------------------
    public struct CpuState
    {
        public byte A;
        public byte X;
        public byte Y;
        public byte Flags;   // P
        public byte S;       // SP
        public ushort PC;
    }

    public struct EmuState
    {
        public int Instructions;
        public int ClockTicks;
        public ushort EA;    // effective address
        public byte Opcode;
    }

    public CpuState cpu;
    public EmuState emu;

    // ------------------------------------------------------------
    // Memory bus callbacks (fake6502_mem_read/write)
    // ------------------------------------------------------------
    public delegate byte MemRead(Fake6502 c, ushort address);
    public delegate void MemWrite(Fake6502 c, ushort address, byte value);

    private readonly MemRead memRead;
    private readonly MemWrite memWrite;

    public Fake6502(MemRead read, MemWrite write)
    {
        memRead = read ?? throw new ArgumentNullException(nameof(read));
        memWrite = write ?? throw new ArgumentNullException(nameof(write));
    }

    // ------------------------------------------------------------
    // Opcode table type (fake6502_opcode)
    // ------------------------------------------------------------
    public delegate void AddrModeFn(Fake6502 c);
    public delegate void OpcodeFn(Fake6502 c);

    public readonly struct OpcodeEntry
    {
        public readonly AddrModeFn AddrMode;
        public readonly OpcodeFn Op;
        public readonly int ClockTicks;

        public OpcodeEntry(AddrModeFn addrMode, OpcodeFn op, int clockTicks)
        {
            AddrMode = addrMode;
            Op = op;
            ClockTicks = clockTicks;
        }
    }

    // C의 extern fake6502_opcode fake6502_opcodes[];
    // -> C#에선 배열로 둠. 실제 내용은 .cs(다음 단계)에서 채움.
//    public static OpcodeEntry[] Opcodes = Array.Empty<OpcodeEntry>();

  
    // ------------------------------------------------------------
    // Macro replacements: flag set/clear
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetFlag(byte mask) => cpu.Flags |= mask;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearFlag(byte mask) => cpu.Flags = (byte)(cpu.Flags & ~mask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsFlagSet(byte mask) => (cpu.Flags & mask) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CarrySet() => SetFlag(CARRY_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CarryClear() => ClearFlag(CARRY_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ZeroSet() => SetFlag(ZERO_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ZeroClear() => ClearFlag(ZERO_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InterruptSet() => SetFlag(INTERRUPT_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InterruptClear() => ClearFlag(INTERRUPT_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecimalSet() => SetFlag(DECIMAL_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecimalClear() => ClearFlag(DECIMAL_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OverflowSet() => SetFlag(OVERFLOW_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OverflowClear() => ClearFlag(OVERFLOW_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SignSet() => SetFlag(SIGN_FLAG);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SignClear() => ClearFlag(SIGN_FLAG);

    // fake6502_accum_save(c, n)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AccumSave(ushort n) => cpu.A = (byte)(n & 0x00FF);

    // ------------------------------------------------------------
    // Macro replacements: flag calculations
    //  - zero_calc / sign_calc / carry_calc / overflow_calc
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ZeroCalc(ushort n)
    {
        if ((n & 0x00FF) != 0) ZeroClear();
        else ZeroSet();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SignCalc(ushort n)
    {
        if ((n & 0x0080) != 0) SignSet();
        else SignClear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CarryCalc(ushort n)
    {
        if ((n & 0xFF00) != 0) CarrySet();
        else CarryClear();
    }

    // n = result, m = accumulator, o = memory
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OverflowCalc(ushort n, byte m, ushort o)
    {
        // if (((n) ^ (uint16_t)(m)) & ((n) ^ (o)) & 0x0080) overflow_set else clear
        if ((((n ^ m) & (n ^ o)) & 0x0080) != 0) OverflowSet();
        else OverflowClear();
    }


    // ------------------------------------------------------------
    // Helpers for memory access
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Read(ushort address) => memRead(this, address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(ushort address, byte value) => memWrite(this, address, value);
}
