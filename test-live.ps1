param([string]$Cairo='C:\Program Files\1cv8\8.5.1.1529\bin\cairo.dll')
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /main:Dark1C.ProfileTest /out:"$PSScriptRoot\Dark1C.ProfileTest.exe" "$PSScriptRoot\src\Profile.cs" "$PSScriptRoot\src\ProfileTest.cs"
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки теста профиля' }
& "$PSScriptRoot\Dark1C.ProfileTest.exe"
if ($LASTEXITCODE -ne 0) { throw 'Тест профиля не прошёл' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ /main:Dark1C.SmokeTest "/resource:$PSScriptRoot\Dark1C.Native.dll,Dark1C.Payload.dll" "/resource:$PSScriptRoot\background.png,Dark1C.Payload.png" /reference:System.Management.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /out:"$PSScriptRoot\Dark1C.SmokeTest.exe" "$PSScriptRoot\src\Native.cs" "$PSScriptRoot\src\Payload.cs" "$PSScriptRoot\src\SmokeTest.cs"
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки теста' }
$testOutput=Join-Path $env:TEMP ('Dark1C-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $testOutput | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot\Dark1C.SmokeTest.exe" -Destination "$testOutput\Standalone.exe"
& "$testOutput\Standalone.exe" $testOutput $Cairo
if ($LASTEXITCODE -ne 0) { throw 'Тест не прошёл' }
Write-Host "Результаты: $testOutput"
