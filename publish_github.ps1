# Skrypt automatycznej publikacji RADYJKO PRO 5.0.0 FINAL na GitHub (mcogs-prog/radyjko)
$ErrorActionPreference = "Stop"

$RepoUrl = "https://github.com/mcogs-prog/radyjko.git"
$Tag = "v5.0.0"
$ReleaseTitle = "RADYJKO PRO v5.0.0 FINAL (OGŚ)"
$InstallerPath = "dist\RADYJKO_PRO_Setup_v5.0.0_FINAL.exe"

Write-Host "=== 1. Kompilacja aplikacji i budowa instalatora v5.0.0 FINAL ===" -ForegroundColor Cyan
.\deploy.ps1

if (-not (Test-Path ".git")) {
    Write-Host "=== 2. Inicjalizacja repozytorium Git ===" -ForegroundColor Cyan
    git init
    git branch -M main
}

$existingRemote = git remote
if ($existingRemote -contains "origin") {
    git remote set-url origin $RepoUrl
} else {
    git remote add origin $RepoUrl
}

Write-Host "=== 3. Dodawanie plików źródłowych i tworzenie commita ===" -ForegroundColor Yellow
git add .
git commit -m "Release RADYJKO PRO v5.0.0 FINAL (OGŚ)" --allow-empty

Write-Host "=== 4. Tworzenie tagu wersji $Tag ===" -ForegroundColor Yellow
git tag -d $Tag 2>$null
git tag -a $Tag -m $ReleaseTitle

Write-Host "=== 5. Wysyłanie kodu i tagu na GitHub ($RepoUrl) ===" -ForegroundColor Green
git push -u origin main --force
git push origin $Tag --force

if (Get-Command gh -ErrorAction SilentlyContinue) {
    Write-Host "=== 6. Tworzenie wydania GitHub Release przez GitHub CLI ===" -ForegroundColor Green
    if (Test-Path $InstallerPath) {
        gh release create $Tag $InstallerPath --title $ReleaseTitle --notes-file "RELEASE_NOTES_v5.0.0.md" --clobber
        Write-Host "SUKCES! Wydanie $Tag wraz z instalatorem zostało opublikowane na GitHubie!" -ForegroundColor Green
    } else {
        Write-Host "Uwaga: Brak pliku $InstallerPath - utwórz Release ręcznie." -ForegroundColor Yellow
    }
} else {
    Write-Host "Kod i tag $Tag zostały wypchnięte na GitHub!" -ForegroundColor Green
    Write-Host "Aby dodać instalator do Releases, wejdź na: https://github.com/mcogs-prog/radyjko/releases/new?tag=v5.0.0" -ForegroundColor Cyan
    Write-Host "i przeciągnij plik: $InstallerPath" -ForegroundColor Cyan
}
