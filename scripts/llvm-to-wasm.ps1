param(
    [Parameter(Mandatory = $true)][string]$InputLl,
    [Parameter(Mandatory = $true)][string]$OutputWasm
)

$clang = Get-Command clang -ErrorAction SilentlyContinue
if (-not $clang) {
    Write-Error "clang was not found on PATH. Install LLVM/Clang first."
    exit 1
}

& clang --target=wasm32 -nostdlib -Wl,--no-entry -Wl,--export-all -Wl,--strip-all $InputLl -o $OutputWasm
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "WebAssembly written to $OutputWasm"
