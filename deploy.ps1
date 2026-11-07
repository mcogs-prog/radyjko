# Skrypt One-Click Build dla RADYJKO 3.3.0 (OGŚ)
$ErrorActionPreference = "Stop"

Write-Host "=== RADYJKO 3.3.0 MULTI-LANG (OGŚ) - Budowanie Wydania ===" -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
if ($LASTEXITCODE -ne 0) { exit 1 }

$iscc = (Get-ChildItem -Path "C:\Program Files*", "C:\Users\$env:USERNAME\AppData" -Filter "ISCC.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
if ($iscc) { & $iscc installer/setup.iss }
