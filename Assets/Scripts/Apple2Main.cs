using System;
using System.Text;
using UnityEngine;
using TMPro;



public class Apple2Main : MonoBehaviour
{
    // ----------------------------
    // ROM assets (binary)
    // ----------------------------
    [Header("ROMs (TextAsset binary)")]
    public TextAsset apple2PlusRom;   // Apple2_Plus.rom
    //public TextAsset apple2VideoRom;  // Apple2_Video.rom

    // ----------------------------
    // UI
    // ----------------------------
    [Header("UI (optional)")]
    public TMP_Text debugText;   // 레지스터 출력
    public TMP_Text screenText;  // 40x24 출력

    // ----------------------------
    // Memory (64KB)
    // ----------------------------
    public byte[] mem = new byte[65536];

    // ----------------------------
    // Keyboard latch (same as C)
    // ----------------------------
    private byte kbdLatch = 0;   // ASCII
    private byte kbdStrobe = 0;  // 1이면 key ready

    // ----------------------------
    // CPU
    // ----------------------------
    private Fake6502 cpu;

    // ----------------------------
    // Timing
    // ----------------------------
    public int fps = 24;
    
    // ----------------------------
    // Dump / Debug
    // ----------------------------
    public int dumpEverySteps = 100000; // C 코드 step_count % 100000
    private int stepCount = 0;

    private readonly StringBuilder sbDebug = new StringBuilder(256);
    private readonly StringBuilder sbScreen = new StringBuilder(40 * 24 + 32);

    const char CursorChar = '▌';
    const char CursorToken = '`';
    
    [SerializeField] private float cursorBlinkInterval = 0.5f; // 0.5초마다 토글
    private float _cursorBlinkTimer = 0f;
    private bool _cursorOn = true;

    private long _cyclesAcc;
    private float _secAcc;

    string debugCPUSpeed;
        

    [SerializeField] int cyclesPerFixed = 600000;     // fixedDeltaTime 기준으로 잡거나 고정
    [SerializeField] int maxCyclesPerFixed = 800000;  // 스파이럴 방지 캡


    void Start()
    {
        Array.Clear(mem, 0, mem.Length);

        // CPU init (bus callbacks)
        cpu = new Fake6502(ReadMem, WriteMem);

        // -----------------------------------------
        // ROM Load (C main과 동일)
        // -----------------------------------------
        LoadRom(apple2PlusRom, 0xD000);
        //LoadRom(apple2VideoRom, 0xC000);

        // -----------------------------------------
        // Reset vector check
        // -----------------------------------------
        ushort resetPc = (ushort)(mem[0xFFFC] | (mem[0xFFFD] << 8));
        Log($"Reset Vector = ${resetPc:X4}");

        // -----------------------------------------
        // CPU reset
        // -----------------------------------------
        cpu.Reset();
        Log($"CPU PC after reset = ${cpu.cpu.PC:X4}");

        
    }

    private void Update()
    {
        HandleKeyboardInput();
        DumpDebug();
        DumpText40x24();
    }



    private void FixedUpdate()
    {
        int budget = cyclesPerFixed;
        if (budget > maxCyclesPerFixed) budget = maxCyclesPerFixed;

        int cycles = 0;
        while (cycles < budget)
        {
            cycles += cpu.Step(); // Step은 "instruction 1개 + 사용 cycle 반환"
        }

        _cursorBlinkTimer += Time.fixedDeltaTime;
        if (_cursorBlinkTimer >= cursorBlinkInterval)
        {
            _cursorBlinkTimer -= cursorBlinkInterval;
            _cursorOn = !_cursorOn;
        }

        //
        _cyclesAcc += cycles;

        _secAcc += Time.fixedDeltaTime;
        if (_secAcc >= 1.0f)
        {
            double mhz = _cyclesAcc / _secAcc / 1_000_000.0;
            debugCPUSpeed = $"CPU : {mhz:F2} MHz  (cycles={_cyclesAcc}, sec={_secAcc:F2})";
            _cyclesAcc = 0;
            _secAcc = 0f;
        }
    }


    // ------------------------------------------------------------
    // ROM loader (TextAsset bytes copy)
    // ------------------------------------------------------------
    private void LoadRom(TextAsset rom, int baseAddr)
    {
        if (rom == null || rom.bytes == null || rom.bytes.Length == 0)
        {
            Log($"ROM open failed (missing asset): base=${baseAddr:X4}");
            return;
        }

        int size = rom.bytes.Length;
        if (baseAddr + size > mem.Length)
        {
            throw new Exception($"ROM too large: {rom.name} size={size}, base=${baseAddr:X4}");
        }

        Buffer.BlockCopy(rom.bytes, 0, mem, baseAddr, size);
        Log($"Loaded ROM: {rom.name} ({size} bytes) at ${baseAddr:X4}");
    }

    // ------------------------------------------------------------
    // Memory mapped IO (same as your C code)
    // ------------------------------------------------------------
    private byte ReadMem(Fake6502 c, ushort address)
    {
        if (address == 0xC000)
        {
            // (kbd_latch & 0x7F) | (kbd_strobe ? 0x80 : 0x00)
            return (byte)((kbdLatch & 0x7F) | (kbdStrobe != 0 ? 0x80 : 0x00));
        }
        if (address == 0xC010)
        {
            kbdStrobe = 0; // strobe clear
            return 0;
        }

        return mem[address];
    }

    private void WriteMem(Fake6502 c, ushort address, byte val)
    {
        // TODO: IO 분기 추가 (speaker, video softswitch 등)
        mem[address] = val;
    }

    // ------------------------------------------------------------
    // Keyboard input (Unity)
    // ------------------------------------------------------------
    private void HandleKeyboardInput()
    {
        /*
        // inputString: 이번 프레임에 들어온 printable 문자들
        string s = Input.inputString;
        if (!string.IsNullOrEmpty(s))
        {
            char ch = s[s.Length - 1];
            kbdLatch = (byte)ch;
            kbdStrobe = 1;
        }

        // 특수키(Enter/Backspace 등) 필요하면 여기서 매핑
        if (Input.GetKeyDown(KeyCode.Return))
        {
            kbdLatch = 0x0D; // CR
            kbdStrobe = 1;
        }
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            kbdLatch = 0x08;
            kbdStrobe = 1;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 급정지용(디버그)
            // enabled = false;
        }
        */
    }

    // ------------------------------------------------------------
    // Text page address (same as C inline)
    // ------------------------------------------------------------
    private static ushort A2TextAddr(int row, int col)
    {
        return (ushort)(0x0400
            + ((row & 0x07) << 7)   // 0x80 * (row%8)
            + ((row >> 3) * 0x28)   // 0x28 * (row/8)
            + col);
    }

    // ------------------------------------------------------------
    // Dump: CPU regs + text page
    // ------------------------------------------------------------
    private void DumpDebug()
    {
        sbDebug.Clear();
        sbDebug.AppendFormat(
            "{0}\nPC=${1:X4} A={2:X2} X={3:X2} Y={4:X2} SP={5:X2} FLAGS={6:X2}\n",
            debugCPUSpeed, cpu.cpu.PC, cpu.cpu.A, cpu.cpu.X, cpu.cpu.Y, cpu.cpu.S, cpu.cpu.Flags
        );

        if (debugText != null) debugText.text = sbDebug.ToString();
        else Log(sbDebug.ToString());
    }

    private void DumpText40x24()
    {
        sbScreen.Clear();

        for (int row = 0; row < 24; row++)
        {
            for (int col = 0; col < 40; col++)
            {
                byte v = mem[A2TextAddr(row, col)];

                char ch;
                if (v >= 0xA0 && v <= 0xDF) ch = (char)(v & 0x7F);
                else if (v >= 0x20 && v <= 0x7F) ch = (char)v;
                else ch = ' ';

                if (ch == CursorToken)
                    ch = _cursorOn ? CursorChar : ' ';

                sbScreen.Append(ch);
            }
            sbScreen.Append('\n');
        }

        if (screenText != null) screenText.text = sbScreen.ToString();
    }

    private void Log(string msg)
    {
        Debug.Log(msg);
        // debugText가 있고 “로그 영역”까지 같이 쓰고 싶으면 여기서 append도 가능
    }
}
