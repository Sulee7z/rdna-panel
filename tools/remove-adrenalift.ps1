param(
    [switch]$Execute,
    [switch]$IncludeWinRing,
    [switch]$RemoveResearchCopy
)
# 移除 Adrenalift 及其使用的内核驱动（InpOut32 / 可选 WinRing0）
$ErrorActionPreference = "SilentlyContinue"
$log = New-Object System.Collections.Generic.List[string]
$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$isAdmin = (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$log.Add("admin=$isAdmin  execute=$Execute")

function Remove-Svc([string]$name) {
    $key = "HKLM:\SYSTEM\CurrentControlSet\Services\$name"
    if (-not (Test-Path $key)) { return }
    $img = (Get-ItemProperty $key).ImagePath
    $log.Add("service $name -> $img")
    if (-not $isAdmin) { $log.Add("  skip: need admin"); return }
    & sc.exe stop $name | Out-Null
    & sc.exe delete $name | Out-Null
    $log.Add("  service stopped+deleted")
    if ($img) {
        $f = ($img -replace '^\\\?\?\\','') -replace '^\\SystemRoot\\', "$env:SystemRoot\"
        if (Test-Path $f) {
            $bak = "$f.bak"
            if (Test-Path $bak) { Remove-Item $bak -Force }
            Move-Item $f $bak -Force
            $log.Add("  driver -> $bak (可改名回退)")
        }
    }
}

# 1) Adrenalift 主驱动：InpOut32
Remove-Svc "inpoutx64"

# 2) WinRing0（若安装过）
if ($IncludeWinRing) {
    foreach ($n in @("WinRing0_1_2_0","WinRing0","WinRing0x64")) { Remove-Svc $n }
}

# 3) 用户数据
$appDir = Join-Path $env:LOCALAPPDATA "app.adrenalift"
if (Test-Path $appDir) {
    if ($Execute) { Remove-Item $appDir -Recurse -Force; $log.Add("removed $appDir") }
    else { $log.Add("would remove $appDir") }
}

# 4) 研究副本（可选）
$research = Join-Path (Split-Path -Parent $PSScriptRoot) "..\.research\adrenalift"
if ($RemoveResearchCopy -and (Test-Path $research)) {
    if ($Execute) { Remove-Item $research -Recurse -Force; $log.Add("removed $research") }
    else { $log.Add("would remove $research") }
}

$log | ForEach-Object { Write-Output $_ }
