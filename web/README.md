# EasyApple2 Web

Apple II 에뮬레이터의 웹(Vite + TypeScript) 포팅. Unity 의존성 없이 브라우저에서 실행.

## 시작하기

```bash
cd web
npm install
npm run dev
```

기본 포트: <http://localhost:5173>

## 구조 (예정)

```
web/
├── index.html          # 캔버스 + 부팅 셸
├── public/
│   └── rom/            # Apple2_Plus 같은 ROM 바이너리 (정적 자산)
├── src/
│   ├── main.ts         # 엔트리 — 캔버스/입력/RAF 루프
│   ├── apple2.ts       # 64KB 메모리, ROM 로드, I/O 매핑 (Apple2Main.cs 대응)
│   └── cpu/
│       └── fake6502.ts # 6502/65C02 코어 (Fake6502.*.cs 대응)
└── vite.config.ts
```

## 스크립트

- `npm run dev` — Vite 개발 서버
- `npm run build` — `tsc` 타입 체크 + `vite build`로 `dist/` 생성
- `npm run preview` — 프로덕션 빌드 미리보기
- `npm run typecheck` — 타입만 검사

## ROM

`Assets/Resoures/rom/Apple2_Plus.bytes`를 `web/public/rom/`로 복사해 `/rom/Apple2_Plus.bin`로 fetch한다(파일명/확장자는 추후 확정).
