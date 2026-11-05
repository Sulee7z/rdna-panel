param([switch]$Force)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$target = Join-Path $env:LOCALAPPDATA "Programs\RDNA Panel"
$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel"
$existing = $null
if (Test-Path $key) { $existing = (Get-ItemProperty $key -ErrorAction SilentlyContinue).InstallLocation }

if ($existing -and (Test-Path (Join-Path $existing "RDNAPanel.exe")) -and ($existing.TrimEnd('\') -ne $target.TrimEnd('\')) -and (-not $Force)) {
    Write-Host ""
    Write-Host "RDNA Panel is already installed at:" -ForegroundColor Yellow
    Write-Host "  $existing"
    Write-Host ""
    Write-Host "To avoid two copies on one machine, uninstall it first (Settings > Apps > RDNA Panel),"
    Write-Host "or re-run this setup with -Force to move the installation here."
    Write-Host ""
    exit 1
}

Write-Host "Installing RDNA Panel to $target ..."
Get-Process RDNAPanel -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$keep = Join-Path $target "data"
$backup = Join-Path $env:TEMP ("rdna_data_" + [guid]::NewGuid().ToString("N"))
if (Test-Path $keep) { Copy-Item $keep $backup -Recurse -Force }
if (Test-Path $target) { Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Path $target -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $here "payload.zip"), $target)
if (Test-Path $backup) { Copy-Item (Join-Path $backup "*") $keep -Recurse -Force; Remove-Item $backup -Recurse -Force }

$exe = Join-Path $target "RDNAPanel.exe"
$ws = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut((Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\RDNA Panel.lnk"))
$lnk.TargetPath = $exe; $lnk.WorkingDirectory = $target; $lnk.IconLocation = $exe; $lnk.Save()
$lnk2 = $ws.CreateShortcut((Join-Path ([Environment]::GetFolderPath("Desktop")) "RDNA Panel.lnk"))
$lnk2.TargetPath = $exe; $lnk2.WorkingDirectory = $target; $lnk2.IconLocation = $exe; $lnk2.Save()
New-Item -Path $key -Force | Out-Null
Set-ItemProperty $key DisplayName "RDNA Panel"
Set-ItemProperty $key DisplayVersion "1.3.0"
Set-ItemProperty $key Publisher "RDNA Panel"
Set-ItemProperty $key InstallLocation $target
Set-ItemProperty $key DisplayIcon $exe
Set-ItemProperty $key UninstallString ('powershell -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $target "uninstall.ps1") + '"')
Write-Host ""
Write-Host "Done. Launching RDNA Panel..."
Start-Process -FilePath $exe
