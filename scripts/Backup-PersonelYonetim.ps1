#Requires -Version 5.1
<#
.SYNOPSIS
  Personel Bilgi ve Yönetim Sistemi yedekleme betiği.

.DESCRIPTION
  1) MSSQL FULL BACKUP
  2) Data Protection anahtarları (App_Data/dp-keys)
  3) Yüklenen dosyalar (App_Data/uploads)
  Retention gününden eski yedekleri siler.

.EXAMPLE
  .\Backup-PersonelYonetim.ps1

.EXAMPLE
  .\Backup-PersonelYonetim.ps1 -SqlServer "(localdb)\mssqllocaldb" -Database "PersonelYonetimDb" -BackupRoot "D:\Backups\PersonelYonetim"
#>
[CmdletBinding()]
param(
  [string]$SqlServer = "(localdb)\mssqllocaldb",
  [string]$Database = "PersonelYonetimDb",
  [string]$BackupRoot = (Join-Path $env:USERPROFILE "Backups\PersonelYonetim"),
  [string]$ApiDataPath = "",
  [int]$RetentionDays = 14,
  [switch]$SkipSql,
  [switch]$SkipFiles
)

$ErrorActionPreference = "Stop"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dest = Join-Path $BackupRoot $stamp
New-Item -ItemType Directory -Force -Path $dest | Out-Null

Write-Host "Yedek klasoru: $dest" -ForegroundColor Cyan

if (-not $ApiDataPath) {
  $candidate = Join-Path $PSScriptRoot "..\backend\src\PersonelYonetim.Api\App_Data"
  $ApiDataPath = [System.IO.Path]::GetFullPath($candidate)
}

if (-not $SkipSql) {
  $bak = Join-Path $dest "$Database.bak"
  $sql = @"
BACKUP DATABASE [$Database]
TO DISK = N'$bak'
WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;
"@
  Write-Host "SQL yedegi aliniyor..." -ForegroundColor Yellow
  & sqlcmd -S $SqlServer -Q $sql -b
  if ($LASTEXITCODE -ne 0) { throw "sqlcmd basarisiz (exit $LASTEXITCODE). sqlcmd PATH'te mi?" }
  Write-Host "SQL tamam: $bak" -ForegroundColor Green
}

if (-not $SkipFiles) {
  $dp = Join-Path $ApiDataPath "dp-keys"
  $uploads = Join-Path $ApiDataPath "uploads"

  if (Test-Path $dp) {
    Copy-Item -Path $dp -Destination (Join-Path $dest "dp-keys") -Recurse -Force
    Write-Host "dp-keys kopyalandi." -ForegroundColor Green
  }
  else {
    Write-Warning "dp-keys bulunamadi: $dp"
  }

  if (Test-Path $uploads) {
    Copy-Item -Path $uploads -Destination (Join-Path $dest "uploads") -Recurse -Force
    Write-Host "uploads kopyalandi." -ForegroundColor Green
  }
  else {
    Write-Warning "uploads bulunamadi: $uploads"
  }
}

# Manifest
$manifest = @{
  takenAtUtc = (Get-Date).ToUniversalTime().ToString("o")
  sqlServer  = $SqlServer
  database   = $Database
  apiDataPath = $ApiDataPath
  host       = $env:COMPUTERNAME
} | ConvertTo-Json
Set-Content -Path (Join-Path $dest "manifest.json") -Value $manifest -Encoding UTF8

# Retention
if ($RetentionDays -gt 0 -and (Test-Path $BackupRoot)) {
  $cutoff = (Get-Date).AddDays(-$RetentionDays)
  Get-ChildItem -Path $BackupRoot -Directory |
    Where-Object { $_.LastWriteTime -lt $cutoff } |
    ForEach-Object {
      Write-Host "Eski yedek siliniyor: $($_.FullName)" -ForegroundColor DarkYellow
      Remove-Item $_.FullName -Recurse -Force
    }
}

Write-Host "Yedekleme tamam." -ForegroundColor Cyan
Write-Host "Restore testi: OPERASYON.md 'Yedek geri yukleme' bolumune bakin."
