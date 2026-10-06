# Roadmap

## Completed

- v0.1 — compiler frontend, semantic analysis, managed runtime, initial LLVM IR
- v0.2 — `%I/%Q` process image, scan-cycle model, `TON`
- v0.3 — standalone Language Server and VS Code tooling
- v0.4 — LLVM → WebAssembly backend and host ABI
- v0.5 — CI/CD, Docker, expanded testing and portfolio documentation

## Potential next steps

- `WHILE`, functions and additional IEC 61131-3 data types
- richer source ranges and diagnostic quick fixes
- symbol rename/references support in the Language Server
- explicit compiler IR between AST and LLVM
- Web-based PLC process visualizer
- benchmark suite for compile time and scan-cycle execution
- optional OPC UA / simulated field-device integration

Future work should preserve the current separation between frontend, runtime, backend and editor tooling.
