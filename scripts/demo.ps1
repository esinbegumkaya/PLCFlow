$ErrorActionPreference = "Stop"

Write-Host "== PLCFlow: build and test =="
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release

Write-Host "`n== Managed PLC runtime =="
dotnet run --project .\src\PLCFlow.Cli --configuration Release -- .\examples\conveyor\conveyor-v02.st --run --input %I0.0=true --cycles 10 --cycle-ms 10

Write-Host "`n== LLVM IR =="
New-Item -ItemType Directory -Force -Path .\artifacts | Out-Null
dotnet run --project .\src\PLCFlow.Cli --configuration Release -- .\examples\conveyor\conveyor-v02.st --emit-llvm .\artifacts\conveyor.ll

if ((Get-Command clang -ErrorAction SilentlyContinue) -and (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "`n== WebAssembly runtime =="
    dotnet run --project .\src\PLCFlow.Cli --configuration Release -- .\examples\conveyor\conveyor-v02.st --run-wasm --input %I0.0=true --cycles 10 --cycle-ms 10
} else {
    Write-Warning "Skipping WebAssembly execution because clang and/or node are not on PATH."
}
