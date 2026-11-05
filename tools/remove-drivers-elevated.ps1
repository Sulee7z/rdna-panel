$log = "C:\Windows\Temp\rt-remove-driver.log"
"start $(Get-Date -Format o)" | Out-File $log -Encoding UTF8
$admin = (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
"admin=$admin" | Out-File $log -Append -Encoding UTF8
if (-not $admin) { "not elevated" | Out-File $log -Append -Encoding UTF8; exit 2 }

# 1) InpOut32（Adrenalift 的物理内存读写驱动）
& sc.exe stop inpoutx64 | Out-File $log -Append -Encoding UTF8
& sc.exe delete inpoutx64 | Out-File $log -Append -Encoding UTF8
$sys = "C:\Windows\System32\Drivers\inpoutx64.sys"
if (Test-Path $sys) {
    if (Test-Path "$sys.bak") { Remove-Item "$sys.bak" -Force }
    Move-Item $sys "$sys.bak" -Force
    "moved $sys -> .bak" | Out-File $log -Append -Encoding UTF8
}

# 2) WinRing0（如存在）
foreach ($n in @("WinRing0_1_2_0","WinRing0","WinRing0x64")) {
    & sc.exe stop $n 2>$null | Out-Null
    & sc.exe delete $n 2>$null | Out-File $log -Append -Encoding UTF8
}

# 3) CPU-Z 驱动残留（C:\ProgramData\CPUID Software\sdk/*.sys 副本）
$cpuzDir = "C:\ProgramData\CPUID Software\sdk"
if (Test-Path $cpuzDir) {
    Remove-Item $cpuzDir -Recurse -Force -ErrorAction SilentlyContinue
    "removed $cpuzDir" | Out-File $log -Append -Encoding UTF8
}
Get-ChildItem "C:\Windows\Temp" -Directory -Filter "cpuz*" -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    "removed $($_.FullName)" | Out-File $log -Append -Encoding UTF8
}

"done $(Get-Date -Format o)" | Out-File $log -Append -Encoding UTF8
exit 0
