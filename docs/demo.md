# Demo Script

This is the recommended 2–3 minute portfolio demonstration.

## 1. Show editor tooling

Open `examples/conveyor/conveyor-v02.st` in the PLCFlow Extension Development Host.

- Hover `Motor` to show `BOOL` and `%Q0.0`.
- Use Go to Definition on `Motor`.
- Temporarily enter `Motor := 42;` to show compiler-backed diagnostics.
- Enter `StartDelay.` and trigger completion to show `Q` / `ET`.

## 2. Show managed PLC execution

```powershell
dotnet run --project .\src\PLCFlow.Cli -- .\examples\conveyor\conveyor-v02.st --run --input %I0.0=true --cycles 10 --cycle-ms 10
```

Point out the input process image, `%Q0.0`, timer state and scan-cycle count.

## 3. Show compiler output

```powershell
dotnet run --project .\src\PLCFlow.Cli -- .\examples\conveyor\conveyor-v02.st --emit-llvm conveyor.ll
```

Open `conveyor.ll` and show `scan_cycle`, PLC globals and timer lowering.

## 4. Show WebAssembly execution

```powershell
dotnet run --project .\src\PLCFlow.Cli -- .\examples\conveyor\conveyor-v02.st --run-wasm --input %I0.0=true --cycles 10 --cycle-ms 10
```

The same control logic should produce `%Q0.0 = true` through the WebAssembly backend.

## 5. Close with engineering quality

Show `.github/workflows/ci.yml`, automated tests and the Dockerfile. Explain that each pull request validates the compiler, runtime, extension compilation and WebAssembly smoke path.
