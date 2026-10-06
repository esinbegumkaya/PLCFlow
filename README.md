# PLCFlow

**Structured Text Compiler & Virtual PLC Toolchain**

PLCFlow is a compiler-engineering and industrial-automation tooling project built with C#/.NET, LLVM, WebAssembly and TypeScript. It compiles an IEC 61131-3-inspired Structured Text subset, performs semantic validation, models PLC scan-cycle execution, and provides IDE features through a standalone Language Server and VS Code extension.

> PLCFlow is a portfolio/engineering project, not a certified IEC 61131-3 implementation or safety PLC runtime.

## Highlights

| Area | Implementation |
|---|---|
| Compiler frontend | Lexer → recursive-descent parser → AST → semantic/type analysis |
| PLC model | `%I` / `%Q` process image, persistent memory, deterministic scan cycles |
| Function blocks | `TON` with `IN`, `PT`, `Q`, `ET` |
| Native tooling | C# / .NET 8 CLI and runtime |
| Compiler backend | LLVM IR targeting `wasm32-unknown-unknown` |
| WebAssembly | Clang/LLVM compilation + Node.js execution |
| Editor tooling | C# Language Server + TypeScript VS Code extension |
| IDE features | Diagnostics, hover, go-to-definition, context-aware completion |
| Quality | xUnit, GitHub Actions, Docker, reproducible demo scripts |

## Architecture

```text
Structured Text (.st)
        │
        ▼
 Lexer → Parser → AST → Semantic Analyzer
                      │
          ┌───────────┴───────────┐
          ▼                       ▼
 Managed PLC Runtime          LLVM IR
          │                       │
   %I → scan_cycle → %Q           ▼
                              Clang/LLVM
                                  │
                                  ▼
                            WebAssembly
                                  │
                                  ▼
                              Node.js Host

VS Code (TypeScript) ↔ C# Language Server ↔ Compiler Frontend
```

For the detailed design, see `docs/architecture.md`.

## Structured Text example

```iecst
PROGRAM ConveyorControl
VAR
    StartButton AT %I0.0 : BOOL;
    StopButton  AT %I0.1 : BOOL;
    Motor       AT %Q0.0 : BOOL;
    StartDelay : TON;
    Counter     : INT;
END_VAR

StartDelay(IN := StartButton AND NOT StopButton, PT := T#100ms);
Motor := StartDelay.Q;

IF Motor THEN
    Counter := Counter + 1;
END_IF;
END_PROGRAM
```

## Quick start

Requirements: .NET 8 SDK. LLVM/Clang and Node.js are additionally required for WebAssembly execution and the VS Code extension.

```powershell
dotnet restore
dotnet build
dotnet test
```

### Managed virtual PLC

```powershell
dotnet run --project .\src\PLCFlow.Cli -- .\examples\conveyor\conveyor-v02.st --run --input %I0.0=true --cycles 10 --cycle-ms 10
```

Expected final state includes:

```text
%Q0.0 = True
StartDelay: IN=True, Q=True, ET=100ms, PT=100ms
```

### LLVM IR

```powershell
dotnet run --project .\src\PLCFlow.Cli -- .\examples\conveyor\conveyor-v02.st --emit-llvm conveyor.ll
```

### WebAssembly

```powershell
dotnet run --project .\src\PLCFlow.Cli -- .\examples\conveyor\conveyor-v02.st --run-wasm --input %I0.0=true --cycles 10 --cycle-ms 10
```

Expected WebAssembly state includes:

```text
%Q0.0 = true
StartDelay: Q=true, ET=100ms, PT=100ms
```

## One-command demo

Windows PowerShell:

```powershell
.\scripts\demo.ps1
```

Linux/macOS:

```bash
./scripts/demo.sh
```

The script builds, runs tests, executes the managed PLC example, emits LLVM IR and—when Clang and Node.js are available—executes the WebAssembly backend.

## VS Code Language Server

```powershell
cd .\vscode-extension
npm install
npm run compile
cd ..
```

Open the repository in VS Code and run **Run PLCFlow VS Code Extension** from **Run and Debug**. In the Extension Development Host, open the conveyor example and try:

- hover over `Motor` → `Motor : BOOL at %Q0.0`
- Go to Definition on `Motor`
- temporarily write `Motor := 42;` → semantic diagnostic
- write `StartDelay.` and trigger completion → `Q`, `ET`

## Docker

The image includes the runtime requirements for the LLVM/WebAssembly path.

```bash
docker build -t plcflow .
docker run --rm plcflow examples/conveyor/conveyor-v02.st --run-wasm --input %I0.0=true --cycles 10 --cycle-ms 10
```

Or:

```bash
docker compose run --rm plcflow
```

## CI/CD

GitHub Actions validates three independent paths on pushes and pull requests:

1. .NET restore/build/test
2. TypeScript VS Code extension compilation
3. LLVM → WebAssembly conveyor smoke execution

This keeps the compiler frontend, editor tooling and backend/toolchain integration independently visible.

## Repository structure

```text
PLCFlow/
├── src/
│   ├── PLCFlow.Compiler/
│   ├── PLCFlow.Runtime/
│   ├── PLCFlow.LanguageServer/
│   └── PLCFlow.Cli/
├── tests/PLCFlow.Compiler.Tests/
├── vscode-extension/
├── examples/
├── scripts/
├── docs/
├── .github/workflows/
├── Dockerfile
└── docker-compose.yml
```

## Supported scope

The current subset focuses on `PROGRAM`, `VAR`, `BOOL`, `INT`, direct `%I/%Q` addresses, assignments, arithmetic/comparison/Boolean expressions, `IF/ELSE`, `TON`, time literals, semantic diagnostics and compiler-backed editor tooling.

PLCFlow intentionally does **not** claim full IEC 61131-3 compliance, real-time scheduling guarantees, hardware PLC connectivity or safety certification.

## Documentation

- `docs/architecture.md` — compiler/runtime/LSP architecture
- `docs/language-specification.md` — supported Structured Text subset
- `docs/demo.md` — recommended portfolio demo
- `docs/portfolio.md` — interview talking points and design decisions
- `docs/v0.5.md` — v0.5 engineering-quality release notes
- `CHANGELOG.md` — release history

## License

MIT
