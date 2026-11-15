# Skrypt One-Click Build dla RADYJKO 5.0.0 FINAL (OGŚ)
$ErrorActionPreference = "Stop"

Write-Host "=== RADYJKO 5.0.0 FINAL (OGŚ) - Budowanie Wydania ===" -ForegroundColor Cyan

Get-ChildItem -Path "." -Filter "*.csproj" | Where-Object { $_.Name -ne "RADYJKO.csproj" } | Remove-Item -Force

New-Item -ItemType Directory -Force -Path "publish" | Out-Null
New-Item -ItemType Directory -Force -Path "dist" | Out-Null

Write-Host "Publikowanie aplikacji .NET 8 (win-x64)..." -ForegroundColor Yellow
dotnet publish "RADYJKO.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
if ($LASTEXITCODE -ne 0) { 
    Write-Host "Błąd kompilacji dotnet!" -ForegroundColor Red
    exit 1 
}

$iscc = (Get-ChildItem -Path "C:\Program Files*", "C:\Users\$env:USERNAME\AppData" -Filter "ISCC.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
if ($iscc) { 
    Write-Host "Kompilowanie instalatora Inno Setup..." -ForegroundColor Green
    & $iscc "installer\setup.iss"
    if (Test-Path "dist\RADYJKO_PRO_Setup_v5.0.0_FINAL.exe") {
        Write-Host "SUKCES: Instalator gotowy w dist\RADYJKO_PRO_Setup_v5.0.0_FINAL.exe!" -ForegroundColor Green
    }
} else {
    Write-Host "Plik ISCC.exe nie został znaleziony. Gotowy plik wykonywalny znajduje się w folderze publish/." -ForegroundColor Yellow
}
