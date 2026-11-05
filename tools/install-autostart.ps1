param(
    [string]$ExePath = "C:\RadeonTune\RadeonTune.exe",
    [int]$DelaySeconds = 30,
    [switch]$Uninstall
)
# 登录自启（写 HKCU Run 键，无需管理员）
#   启用：... -DelaySeconds 60
#   取消：... -Uninstall
$ErrorActionPreference = "Stop"
$runKey = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$settingsPath = Join-Path (Split-Path -Parent $ExePath) "data\settings.json"

if ($Uninstall) {
    Remove-ItemProperty -Path $runKey -Name "RadeonTune" -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName "RadeonTune-AutoApply" -Confirm:$false -ErrorAction SilentlyContinue
    if (Test-Path $settingsPath) {
        $s = Get-Content $settingsPath -Raw | ConvertFrom-Json
        $s.Enabled = $false
        $s | ConvertTo-Json | Set-Content $settingsPath -Encoding UTF8
    }
    Write-Output "已关闭登录自启"
    exit 0
}

if ($DelaySeconds -lt 0) { $DelaySeconds = 0 }
if ($DelaySeconds -gt 3600) { $DelaySeconds = 3600 }
if (-not (Test-Path $ExePath)) { throw "exe not found: $ExePath" }
$value = "`"$ExePath`" --minimized --delay $DelaySeconds"
New-ItemProperty -Path $runKey -Name "RadeonTune" -Value $value -PropertyType String -Force | Out-Null
Unregister-ScheduledTask -TaskName "RadeonTune-AutoApply" -Confirm:$false -ErrorAction SilentlyContinue
Write-Output "已启用：登录后 $DelaySeconds 秒启动并最小化到托盘"
Write-Output "Run 值：$value"
