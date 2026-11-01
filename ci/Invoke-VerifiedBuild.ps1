# Run only on a fresh GitHub-hosted Windows worker. Never executes produced code.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'This entry point is for the GitHub Windows verification worker only.'
}
$root = Split-Path $PSScriptRoot -Parent
$reports = Join-Path $PSScriptRoot 'reports'
$null = New-Item -ItemType Directory -Force -Path $reports
. (Join-Path $PSScriptRoot 'DefenderScan.ps1')
$buildReport = [ordered]@{ schema = 1; commit = $env:GITHUB_SHA; result = 'unverified'; stage = 'defender-preflight'; zigArchiveSha256 = $null }
try {
    # Do not enable/disable protection or change any exclusions on this worker.
    Update-MpSignature -ErrorAction Stop
    $null = Assert-Dark1CDefenderReady
    $buildReport.stage = 'compiler-signature'
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $signature = Get-AuthenticodeSignature -LiteralPath $compiler
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'Expected a valid Microsoft-signed framework compiler.'
    }
    $buildReport.stage = 'toolchain-download'
    $toolRoot = Join-Path $env:RUNNER_TEMP 'dark1c-toolchain'
    $null = New-Item -ItemType Directory -Path $toolRoot
    $archive = Join-Path $toolRoot 'zig.zip'
    Invoke-WebRequest -Uri 'https://ziglang.org/download/0.14.1/zig-x86_64-windows-0.14.1.zip' -OutFile $archive
    $expected = '554f5378228923ffd558eac35e21af020c73789d87afeabf4bfd16f2e6feed2c'
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $expected) { throw 'Official Zig archive checksum mismatch.' }
    $buildReport.zigArchiveSha256 = $hash
    $buildReport.stage = 'toolchain-extract'
    Expand-Archive -LiteralPath $archive -DestinationPath $toolRoot
    $zig = Join-Path $toolRoot 'zig-x86_64-windows-0.14.1\zig.exe'
    $buildReport.stage = 'build'
    # Checkout contains no user wallpaper. create-background.ps1 makes the neutral fallback.
    & (Join-Path $root 'build-live.ps1') -Zig $zig
    $buildReport.stage = 'defender-scan'
    $paths = @('Dark1C.Native.dll', 'Dark1C.exe', 'Dark1C.Control.exe') | ForEach-Object { Join-Path $root $_ }
    $scan = Invoke-Dark1CDefenderScan -ArtifactPaths $paths
    $scan | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reports 'defender.json') -Encoding UTF8
    if ($scan.result -ne 'no-detection-in-this-scan') { throw "Verification failed at $($scan.stage)." }
    $buildReport.result = 'build-and-scan-completed'
    $buildReport.stage = 'complete'
} finally {
    $buildReport | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $reports 'build.json') -Encoding UTF8
}
