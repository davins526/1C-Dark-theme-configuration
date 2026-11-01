param([string]$Cairo='C:\Program Files\1cv8\8.5.1.1529\bin\cairo.dll')
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /optimize+ /main:Dark1C.SmokeTest /reference:System.Management.dll /out:"$PSScriptRoot\Dark1C.SmokeTest.exe" "$PSScriptRoot\src\Native.cs" "$PSScriptRoot\src\SmokeTest.cs"
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки теста' }
$testOutput=Join-Path $env:TEMP ('Dark1C-test-'+[Guid]::NewGuid().ToString('N'))
& "$PSScriptRoot\Dark1C.SmokeTest.exe" $testOutput $Cairo
if ($LASTEXITCODE -ne 0) { throw 'Тест не прошёл' }
Write-Host "Результаты: $testOutput"
