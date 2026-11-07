param([Parameter(Mandatory=$true)][string]$Zig)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Zig -PathType Leaf)) { throw 'Specify the path to Zig 0.14.1.' }
$output = Join-Path $PSScriptRoot 'work\contrast-test'
$null = New-Item -ItemType Directory -Force -Path $output
$test = Join-Path $output 'TextContrastTest.exe'
# Only colour math and private GDI state; no injection code or 1C dependencies.
& $Zig cc -target x86_64-windows-gnu -O2 -Wall -Wextra -Werror (Join-Path $PSScriptRoot 'native\TextContrastTest.c') -lgdi32 -o $test
if ($LASTEXITCODE -ne 0) { throw 'Text contrast test compilation failed.' }
& $test
if ($LASTEXITCODE -ne 0) { throw 'Text contrast regression failed.' }
