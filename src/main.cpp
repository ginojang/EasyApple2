#define _CRT_SECURE_NO_WARNINGS

#include "fake6502.h"
#include "memory.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#include <windows.h>


uint8_t mem[65536];

static size_t load_rom(const char* path, uint8_t* dst, uint16_t base)
{
	FILE* f = fopen(path, "rb");
	if (!f)
	{
		printf("ROM open failed: %s\n", path);
		return 0;
	}

	fseek(f, 0, SEEK_END);
	size_t size = ftell(f);
	fseek(f, 0, SEEK_SET);

	fread(&dst[base], 1, size, f);
	fclose(f);

	printf("Loaded ROM: %s (%zu bytes) at $%04X\n", path, size, base);
	return size;
}


static uint8_t kbd_latch = 0;   // ASCII
static uint8_t kbd_strobe = 0;  // 1이면 key ready

uint8_t fake6502_mem_read(fake6502_context* c, uint16_t address)
{
    if (address == 0xC000) {
        return (kbd_latch & 0x7F) | (kbd_strobe ? 0x80 : 0x00);
    }
    if (address == 0xC010) {
        kbd_strobe = 0;         // strobe clear
        return 0;
    }

    return mem[address];
}
void fake6502_mem_write(fake6502_context* c, uint16_t address, uint8_t val)
{
	// IO
	mem[address] = val;

}

void console_clear()
{
    HANDLE hOut = GetStdHandle(STD_OUTPUT_HANDLE);
    CONSOLE_SCREEN_BUFFER_INFO csbi;
    DWORD count;
    DWORD cellCount;
    COORD homeCoords = { 0, 0 };

    if (hOut == INVALID_HANDLE_VALUE) return;

    if (!GetConsoleScreenBufferInfo(hOut, &csbi)) return;
    cellCount = csbi.dwSize.X * csbi.dwSize.Y;

    FillConsoleOutputCharacter(hOut, ' ', cellCount, homeCoords, &count);
    FillConsoleOutputAttribute(hOut, csbi.wAttributes, cellCount, homeCoords, &count);
    SetConsoleCursorPosition(hOut, homeCoords);
}

static inline uint16_t a2_text_addr(int row, int col)
{
    // row: 0..23, col: 0..39
    return 0x0400
        + ((row & 0x07) << 7)   // 0x80 * (row%8)
        + ((row >> 3) * 0x28)   // 0x28 * (row/8)
        + col;
}


int main()
{
    fake6502_context cpu;

    memset(mem, 0, sizeof(mem));

    // -------------------------------------------------
    // ROM 로딩
    // -------------------------------------------------
    size_t rom_size = load_rom("rom/Apple2_Plus.rom", mem, 0xD000);
    load_rom("rom/Apple2_Video.rom", mem, 0xC000); // 일단 그대로

    // -------------------------------------------------
    // Reset vector 확인
    // -------------------------------------------------
    uint16_t reset_pc = mem[0xFFFC] | (mem[0xFFFD] << 8);
    printf("Reset Vector = $%04X\n", reset_pc);

    // -------------------------------------------------
    // CPU reset
    // -------------------------------------------------
    fake6502_reset(&cpu);
    printf("CPU PC after reset = $%04X\n", cpu.cpu.pc);

    // -------------------------------------------------
    // 실행 루프 (프레임 기준)
    // -------------------------------------------------
    const int FPS = 24;
    const int CYCLES_PER_FRAME = 1000000 / FPS;


    static int step_count = 0;

    while (1)
    {
        int cycles = 0;

        while (cycles < CYCLES_PER_FRAME)
        {
            fake6502_step(&cpu);
            cycles += cpu.emu.clockticks;
            cpu.emu.clockticks = 0;

            if ((step_count++ % 100000) == 0)
            {
                
                console_clear();

                printf("PC=$%04X A=%02X X=%02X Y=%02X SP=%02X FLAGS=%02X\n",
                    cpu.cpu.pc,
                    cpu.cpu.a,
                    cpu.cpu.x,
                    cpu.cpu.y,
                    cpu.cpu.s,
                    cpu.cpu.flags);


                for (int row = 0; row < 24; row++) {
                    for (int col = 0; col < 40; col++) {
                        uint8_t v = mem[a2_text_addr(row, col)];

                        if (v >= 0xA0 && v <= 0xDF) putchar(v & 0x7F);
                        else if (v >= 0x20 && v <= 0x7F) putchar(v); // 혹시 low-ascii로 찍히는 케이스 대비
                        else putchar('.');
                    }
                    putchar('\n');
                }
                
            }
        }

        // TODO:
        // - video render ($0400 / $2000 / $4000)
        // - keyboard / speaker IO

#ifdef _WIN32
        Sleep(1000 / FPS);
#else
        usleep(1000000 / FPS);
#endif
    }

    return 0;
}