param(
    [Parameter(Position = 0)][string]$Action = "save",
    [Parameter(Position = 1)][string]$Message = ""
)

$root = Split-Path -Parent $PSScriptRoot
$store = Join-Path (Split-Path -Parent $root) ".rt-versions"
$log = Join-Path $store "log.txt"
$files = @("App.xaml", "App.xaml.cs", "MainWindow.xaml", "MainWindow.xaml.cs",
           "Engine.cs", "GameLibrary.cs", "RtCore.cs", "RadeonTune.csproj", "README.md",
           "CoverFetcher.cs", "FsrDetector.cs", "EdidInfo.cs", "VfSweep.cs", "VfTest.cs", "VfSampler.cs",
           "native\rt_core.cpp")
# 原生源码不在项目目录，单独纳入版本管理
$nativeDir = Join-Path (Split-Path -Parent $root) ".research\radeontune"
$nativeFiles = @("rt_core.cpp", "adlx_shim.cpp")

if (-not (Test-Path $store)) { New-Item -ItemType Directory -Path $store | Out-Null }

switch ($Action) {
    "save" {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $dir = Join-Path $store $stamp
        New-Item -ItemType Directory -Path $dir | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $dir "native") -Force | Out-Null
        foreach ($f in $files) {
            $src = Join-Path $root $f
            if (Test-Path $src) {
                $dst = Join-Path $dir $f
                New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force | Out-Null
                Copy-Item $src $dst
            }
        }
        New-Item -ItemType Directory -Path (Join-Path $dir "native") -Force | Out-Null
        foreach ($f in $nativeFiles) {
            $src = Join-Path $nativeDir $f
            if ((Test-Path $src) -and ((Get-Item $src).Length -gt 0)) { Copy-Item $src (Join-Path $dir ("native\" + $f)) }
        }
        $hash = (Get-FileHash (Join-Path $root "MainWindow.xaml.cs") -Algorithm SHA256).Hash.Substring(0, 12)
        $line = "$stamp  $hash  $Message"
        Add-Content -Path $log -Value $line -Encoding UTF8
        Write-Output "saved $stamp ($hash) $Message"
    }
    "list" {
        if (Test-Path $log) { Get-Content $log } else { Write-Output "no snapshots" }
    }
    "restore" {
        if (-not $Message) { Write-Output "usage: version.ps1 restore <stamp>"; break }
        $dir = Join-Path $store $Message
        if (-not (Test-Path $dir)) { Write-Output "no such snapshot: $Message"; break }
        foreach ($f in $files) {
            $src = Join-Path $dir $f
            if (Test-Path $src) { Copy-Item $src (Join-Path $root $f) -Force }
        }
        $nat = Join-Path $dir "native"
        if (Test-Path $nat) {
            foreach ($f in $nativeFiles) {
                $src = Join-Path $nat $f
                if (Test-Path $src) { Copy-Item $src (Join-Path $nativeDir $f) -Force }
            }
        }
        Write-Output "restored $Message"
    }
    default { Write-Output "usage: version.ps1 [save <msg> | list | restore <stamp>]" }
}
