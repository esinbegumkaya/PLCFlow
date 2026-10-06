# Portfolio / Interview Notes

## One-sentence description

PLCFlow is a C#/.NET compiler and virtual PLC toolchain that parses an IEC 61131-3-inspired Structured Text subset, validates it semantically, executes it through a deterministic PLC runtime or LLVM/WebAssembly backend, and exposes IDE features through a standalone Language Server and TypeScript VS Code extension.

## Engineering decisions worth discussing

**Shared compiler frontend.** The CLI and Language Server reuse the same lexer, parser, AST and semantic analyzer rather than implementing editor validation separately.

**Explicit process image.** `%I` inputs and `%Q` outputs are modeled separately from program memory so scan-cycle behavior is visible and testable.

**Stateful function blocks.** `TON` persists `IN`, `Q`, `ET` and `PT` across cycles and resets deterministically when its input becomes false.

**Backend separation.** The same validated AST can be interpreted by the managed runtime or lowered to LLVM IR and WebAssembly.

**Host ABI instead of raw memory offsets.** WebAssembly exports explicit setters/getters and `scan_cycle()`, keeping the JavaScript host independent of backend memory layout.

**Compiler-backed editor tooling.** Diagnostics in VS Code use the same semantics as command-line compilation, reducing divergence between the IDE and compiler.

## Scope statement

PLCFlow demonstrates compiler construction and industrial automation tooling; it intentionally does not claim full IEC 61131-3 compatibility, deterministic real-time guarantees, hardware PLC support or safety certification.
