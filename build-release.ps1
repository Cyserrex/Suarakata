<#
    build-release.ps1
    Build Suarakata (Release), salin ffmpeg ke output, lalu (opsional) buat installer
    bila Inno Setup (ISCC.exe) tersedia.

    Jalankan:
        powershell -ExecutionPolicy Bypass -File .\build-release.ps1
#>

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host '== Build Release ==' -ForegroundColor Cyan
dotnet build .\Suarakata.sln -c Release --nologo

$releaseDir = Join-Path $PSScriptRoot 'bin\Release'

Write-Host '== Bundle ffmpeg ==' -ForegroundColor Cyan
$ffTarget = Join-Path $releaseDir 'ffmpeg.exe'
if (-not (Test-Path $ffTarget)) {
    $ff = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue |
        Sort-Object Length -Descending | Select-Object -First 1
    if (-not $ff) { $ff = Get-Command ffmpeg -ErrorAction SilentlyContinue | ForEach-Object { Get-Item $_.Source } }
    if ($ff) {
        Copy-Item $ff.FullName $ffTarget -Force
        Write-Host "  ffmpeg -> $ffTarget"
    } else {
        Write-Warning '  ffmpeg.exe tidak ditemukan. Letakkan manual di bin\Release.'
    }
} else {
    Write-Host '  ffmpeg.exe sudah ada.'
}

Write-Host '== Installer (opsional) ==' -ForegroundColor Cyan
$iscc = @(
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    & $iscc '.\installer\Suarakata.iss'
    Write-Host '  Installer selesai: installer\Output\Suarakata-Setup-1.0.0.exe' -ForegroundColor Green
} else {
    Write-Warning '  Inno Setup (ISCC.exe) tidak ditemukan. Lewati pembuatan installer.'
    Write-Host  '  Alternatif: zip seluruh isi bin\Release untuk distribusi.'
}

Write-Host 'Selesai.' -ForegroundColor Green
