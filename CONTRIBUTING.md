# Contributing

PLCFlow is primarily a compiler-engineering portfolio project, but contributions and review are welcome.

## Development checks

Before opening a pull request, run:

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
cd vscode-extension
npm install
npm run compile
```

If LLVM/Clang and Node.js are available, also run the WebAssembly smoke test documented in `README.md`.

## Design principles

- Keep the compiler frontend independent from execution backends.
- Prefer explicit diagnostics over silent coercion.
- Preserve deterministic PLC scan-cycle semantics.
- Add tests for every new language feature or backend behavior.
- Clearly distinguish the supported Structured Text subset from full IEC 61131-3 compliance.
