#!/usr/bin/env bash
set -euo pipefail

echo "== PLCFlow: build and test =="
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release

echo
echo "== Managed PLC runtime =="
dotnet run --project ./src/PLCFlow.Cli --configuration Release -- ./examples/conveyor/conveyor-v02.st --run --input %I0.0=true --cycles 10 --cycle-ms 10

echo
echo "== LLVM IR =="
mkdir -p artifacts
dotnet run --project ./src/PLCFlow.Cli --configuration Release -- ./examples/conveyor/conveyor-v02.st --emit-llvm ./artifacts/conveyor.ll

if command -v clang >/dev/null 2>&1 && command -v node >/dev/null 2>&1; then
  echo
  echo "== WebAssembly runtime =="
  dotnet run --project ./src/PLCFlow.Cli --configuration Release -- ./examples/conveyor/conveyor-v02.st --run-wasm --input %I0.0=true --cycles 10 --cycle-ms 10
else
  echo "Skipping WebAssembly execution because clang and/or node are not on PATH." >&2
fi
