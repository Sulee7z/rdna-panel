# 一次性配置：让 RadeonTune 以后启动不再弹 UAC
# 用法（只需要管理员权限一次）：
#   powershell -ExecutionPolicy Bypass -File tools\install-task.ps1

param(
    [string]$ExePath = "",
    [string]$TaskName = "RadeonTune-Elevated"
)

$ErrorActionPreference = "Stop"

if (-not $ExePath) {
    $root = Split-Path -Parent $PSScriptRoot
    $ExePath = Join-Path $root "dist\RadeonTune.exe"
}
if (-not (Test-Path $ExePath)) { throw "找不到 exe：$ExePath" }
$ExePath = (Resolve-Path $ExePath).Path
$workDir = Split-Path -Parent $ExePath

# 1) 创建“最高权限”计划任务（不弹 UAC 地运行）
$action = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory $workDir
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
Register-ScheduledTask -TaskName $TaskName -Action $action -Principal $principal -Force | Out-Null
Write-Host "已创建计划任务：$TaskName"

# 2) 桌面快捷方式，双击即通过任务启动（无 UAC）
$desktop = [Environment]::GetFolderPath("Desktop")
$lnk = Join-Path $desktop "RadeonTune.lnk"
$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = "$env:SystemRoot\System32\schtasks.exe"
$sc.Arguments = "/run /tn `"$TaskName`""
$sc.WorkingDirectory = $env:SystemRoot
$sc.WindowStyle = 7
$sc.IconLocation = "$ExePath,0"
$sc.Description = "启动 RadeonTune（免 UAC）"
$sc.Save()
Write-Host "已创建桌面快捷方式：$lnk"

Write-Host ""
Write-Host "之后双击桌面的 RadeonTune 即可，不再弹出 UAC。"
Write-Host "卸载：Unregister-ScheduledTask -TaskName $TaskName -Confirm:`$false"
