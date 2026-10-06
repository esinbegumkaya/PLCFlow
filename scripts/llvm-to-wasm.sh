#!/usr/bin/env bash
set -euo pipefail
if [ "$#" -ne 2 ]; then
  echo "Usage: $0 input.ll output.wasm" >&2
  exit 1
fi
clang --target=wasm32 -nostdlib -Wl,--no-entry -Wl,--export-all -Wl,--strip-all "$1" -o "$2"
echo "WebAssembly written to $2"
