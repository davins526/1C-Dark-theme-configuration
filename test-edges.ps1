param([Parameter(Mandatory=$true)][string]$Zig)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Zig -PathType Leaf)) { throw 'Specify the path to Zig 0.14.1.' }
$output = Join-Path $PSScriptRoot 'work\edge-test'
$null = New-Item -ItemType Directory -Force -Path $output
$test = Join-Path $output 'EdgeDrawingTest.exe'
# Private bitmaps and Win32 drawing only; no hooks, injection or 1C dependencies.
& $Zig cc -target x86_64-windows-gnu -O2 -Wall -Wextra -Werror (Join-Path $PSScriptRoot 'native\EdgeDrawingTest.c') -lgdi32 -luser32 -o $test
if ($LASTEXITCODE -ne 0) { throw 'Edge drawing test compilation failed.' }
& $test
if ($LASTEXITCODE -ne 0) { throw 'Edge drawing regression failed.' }
