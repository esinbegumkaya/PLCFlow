# Changelog

All notable changes to PLCFlow are documented here.

## v0.5.0 — Engineering quality & portfolio release

- Expanded automated coverage across compiler semantics and PLC runtime behavior.
- Added full CI validation for .NET, TypeScript extension compilation and LLVM/WebAssembly smoke execution.
- Added a reproducible Docker image containing .NET, LLVM/Clang, LLD and Node.js.
- Added Docker Compose demo support.
- Added one-command PowerShell and Bash demo scripts.
- Reworked architecture and portfolio documentation.
- Added repository editor configuration and clearer scope/limitations.

## v0.4.0 — WebAssembly backend

- LLVM IR to WebAssembly compilation through Clang.
- WebAssembly host ABI for PLC inputs, outputs, timers and cycle time.
- Node.js WebAssembly execution.

## v0.3.0 — Language Server

- Standalone C# Language Server.
- VS Code diagnostics, hover, go-to-definition and context-aware completion.

## v0.2.0 — PLC runtime

- Direct `%I` / `%Q` addressing.
- Process-image based scan cycle.
- `TON` timer support.
