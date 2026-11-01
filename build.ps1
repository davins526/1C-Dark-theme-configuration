$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Нужен .NET Framework 4.x с компилятором C#.' }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:"$PSScriptRoot\Dark1C.Preview.exe" "$PSScriptRoot\src\Dark1C.cs"
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать предпросмотр.' }
