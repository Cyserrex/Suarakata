<#
    set-version.ps1
    Menaikkan nomor versi di semua tempat sekaligus, supaya alur rilis otomatis
    tidak gagal karena versi tidak seragam.

    Jalankan:
        powershell -ExecutionPolicy Bypass -File .\set-version.ps1 1.3.0
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Versi
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# Set-Content -Encoding utf8 pada Windows PowerShell menambahkan BOM; berkas proyek
# ditulis ulang tanpa BOM supaya diff hanya berisi perubahan nomor versi.
function Tulis($path, $teks) {
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Resolve-Path $path), $teks, $utf8)
}

# --- Suarakata.csproj ---
$csproj = '.\Suarakata.csproj'
$isi = Get-Content $csproj -Raw
$isi = $isi -replace '<Version>[\d\.]+</Version>', "<Version>$Versi</Version>"
$isi = $isi -replace '<FileVersion>[\d\.]+</FileVersion>', "<FileVersion>$Versi.0</FileVersion>"
$isi = $isi -replace '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$Versi.0</AssemblyVersion>"
Tulis $csproj $isi
Write-Host "  Suarakata.csproj      -> $Versi"

# --- installer\Suarakata.iss ---
$iss = '.\installer\Suarakata.iss'
$isi = Get-Content $iss -Raw
$isi = $isi -replace '#define MyAppVersion "[\d\.]+"', "#define MyAppVersion `"$Versi`""
Tulis $iss $isi
Write-Host "  installer\Suarakata.iss -> $Versi"

# --- README.md (nama berkas installer pada contoh) ---
$readme = '.\README.md'
if (Test-Path $readme) {
    $isi = Get-Content $readme -Raw
    $baru = $isi -replace 'Suarakata-Setup-[\d\.]+\.exe', "Suarakata-Setup-$Versi.exe"
    if ($baru -ne $isi) {
        Tulis $readme $baru
        Write-Host "  README.md             -> $Versi"
    }
}

Write-Host ''
Write-Host "Versi diset ke $Versi." -ForegroundColor Green
Write-Host 'Commit lalu push ke main; rilis v' -NoNewline
Write-Host "$Versi" -NoNewline -ForegroundColor Green
Write-Host ' akan dibuat otomatis oleh GitHub Actions.'
