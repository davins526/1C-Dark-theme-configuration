param([string]$Zig)
$ErrorActionPreference='Stop'
& "$PSScriptRoot\create-background.ps1"
if (-not $Zig) { $Zig=(Get-ChildItem "$PSScriptRoot\..\..\work\tools" -Filter zig.exe -Recurse | Select-Object -First 1).FullName }
if (-not $Zig -or -not (Test-Path -LiteralPath $Zig)) { throw 'Укажи путь к Zig 0.14.1: ./build-live.ps1 -Zig C:\tools\zig.exe' }
$vendor=Join-Path $PSScriptRoot 'vendor\minhook-1.3.4'
$sources=@("$PSScriptRoot\native\Dark1C.c","$vendor\src\buffer.c","$vendor\src\hook.c","$vendor\src\trampoline.c","$vendor\src\hde\hde64.c")
& $Zig cc -target x86_64-windows-gnu -shared -O2 -Wall -Wextra -Wno-unused-parameter "-I$vendor\include" @sources -luser32 -lgdi32 -o "$PSScriptRoot\Dark1C.Native.dll"
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки нативного модуля' }
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:Dark1C.LiveWindow "/resource:$PSScriptRoot\Dark1C.Native.dll,Dark1C.Payload.dll" "/resource:$PSScriptRoot\background.png,Dark1C.Payload.png" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /out:"$PSScriptRoot\Dark1C.exe" "$PSScriptRoot\src\Dark1C.cs" "$PSScriptRoot\src\Native.cs" "$PSScriptRoot\src\Payload.cs" "$PSScriptRoot\src\LiveWindow.cs" "$PSScriptRoot\src\Profile.cs"
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки приложения' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ /main:Dark1C.ControlCommand "/resource:$PSScriptRoot\Dark1C.Native.dll,Dark1C.Payload.dll" "/resource:$PSScriptRoot\background.png,Dark1C.Payload.png" /reference:System.Management.dll /out:"$PSScriptRoot\Dark1C.Control.exe" "$PSScriptRoot\src\Native.cs" "$PSScriptRoot\src\Payload.cs" "$PSScriptRoot\src\Control.cs"
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки управления' }
