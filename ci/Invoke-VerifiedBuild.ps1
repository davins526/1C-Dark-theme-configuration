# Run only on a fresh GitHub-hosted Windows worker. Never executes the application
# or hook DLL. Standalone drawing tests contain neither injection nor hooks.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'This entry point is for the GitHub Windows verification worker only.'
}
$root = Split-Path $PSScriptRoot -Parent
$reports = Join-Path $PSScriptRoot 'reports'
$null = New-Item -ItemType Directory -Force -Path $reports
. (Join-Path $PSScriptRoot 'DefenderScan.ps1')
$buildReport = [ordered]@{ schema = 1; commit = $env:GITHUB_SHA; result = 'unverified'; stage = 'defender-preflight'; zigArchiveSha256 = $null; textContrastTest = 'not-run'; edgeDrawingTest = 'not-run'; defenderBefore = $null; defenderAfter = $null; removedExclusionCounts = $null; scanDirectoryExclusionExitCode = $null }
try {
    Write-Host 'Preparing Defender protection on the disposable hosted worker.'
    # Strengthen protection only on the disposable GitHub-hosted worker.
    # Never disable protection, add exclusions or change tamper protection.
    $buildReport.defenderBefore = Get-MpComputerStatus -ErrorAction Stop |
        Select-Object AMServiceEnabled, AntivirusEnabled, RealTimeProtectionEnabled, AMRunningMode
    $service = Get-Service -Name WinDefend -ErrorAction Stop
    if ($service.Status -ne 'Running') { Start-Service -Name WinDefend -ErrorAction Stop }
    Set-MpPreference -DisableRealtimeMonitoring $false -DisableBehaviorMonitoring $false -ErrorAction Stop
    $buildReport.stage = 'defender-signatures'
    # The platform may restart its RPC provider immediately after protection is
    # enabled. Retry the update briefly; never continue without a successful update.
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            Update-MpSignature -ErrorAction Stop
            break
        } catch {
            if ($attempt -eq 3) { throw }
            Write-Host "Defender signature update failed during startup; retry $attempt of 2."
            Start-Sleep -Seconds 5
        }
    }
    $buildReport.stage = 'defender-readiness'
    # Signature/platform startup can restart Defender after the initial setting.
    Set-MpPreference -DisableRealtimeMonitoring $false -DisableBehaviorMonitoring $false -ErrorAction Stop
    $buildReport.defenderAfter = Get-MpComputerStatus -ErrorAction Stop |
        Select-Object AMServiceEnabled, AntivirusEnabled, RealTimeProtectionEnabled, AMRunningMode
    Write-Host ('Defender after update: ' + ($buildReport.defenderAfter | ConvertTo-Json -Compress))
    $buildReport.defenderAfter = Wait-Dark1CDefenderReady |
        Select-Object AMServiceEnabled, AntivirusEnabled, RealTimeProtectionEnabled, AMRunningMode
    $buildReport.stage = 'remove-hosted-exclusions'
    Write-Host 'Removing preconfigured scan exclusions on this disposable hosted worker only.'
    $preferences = Get-MpPreference -ErrorAction Stop
    $excludedPaths = @($preferences.ExclusionPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $excludedExtensions = @($preferences.ExclusionExtension | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $excludedProcesses = @($preferences.ExclusionProcess | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $buildReport.removedExclusionCounts = [ordered]@{ paths = $excludedPaths.Count; extensions = $excludedExtensions.Count; processes = $excludedProcesses.Count }
    if ($excludedPaths.Count) { Remove-MpPreference -ExclusionPath $excludedPaths -ErrorAction Stop }
    if ($excludedExtensions.Count) { Remove-MpPreference -ExclusionExtension $excludedExtensions -ErrorAction Stop }
    if ($excludedProcesses.Count) { Remove-MpPreference -ExclusionProcess $excludedProcesses -ErrorAction Stop }
    $preferences = Get-MpPreference -ErrorAction Stop
    if (@($preferences.ExclusionPath | Where-Object { $_ }).Count -or
        @($preferences.ExclusionExtension | Where-Object { $_ }).Count -or
        @($preferences.ExclusionProcess | Where-Object { $_ }).Count) {
        throw 'Hosted scan exclusions remain; verification cannot continue.'
    }
    $buildReport.stage = 'scan-location'
    Write-Host 'Checking a dedicated scan directory outside the checkout before building.'
    # Windows Temp was excluded on hosted run 42369358854 despite empty custom
    # exclusion lists. Use a fresh directory outside Windows and the checkout;
    # still require CheckExclusion=1 for the directory and every artifact.
    $scanRoot = Join-Path ($env:SystemDrive + '\') ('Dark1C-Verification-' + [Guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $scanRoot
    $buildReport.scanDirectoryExclusionExitCode = Get-Dark1CExclusionExitCode -Path $scanRoot
    if ($buildReport.scanDirectoryExclusionExitCode -ne 1) {
        throw "Scan directory exclusion check failed: exit $($buildReport.scanDirectoryExclusionExitCode)."
    }
    $buildReport.stage = 'compiler-signature'
    Write-Host 'Verifying the framework compiler signature.'
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $signature = Get-AuthenticodeSignature -LiteralPath $compiler
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'Expected a valid Microsoft-signed framework compiler.'
    }
    $buildReport.stage = 'toolchain-download'
    $toolRoot = Join-Path $env:RUNNER_TEMP 'dark1c-toolchain'
    $null = New-Item -ItemType Directory -Path $toolRoot
    $archive = Join-Path $toolRoot 'zig.zip'
    Write-Host 'Downloading the official pinned Zig archive.'
    Invoke-WebRequest -UseBasicParsing -TimeoutSec 180 -Uri 'https://ziglang.org/download/0.14.1/zig-x86_64-windows-0.14.1.zip' -OutFile $archive
    $expected = '554f5378228923ffd558eac35e21af020c73789d87afeabf4bfd16f2e6feed2c'
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $expected) { throw 'Official Zig archive checksum mismatch.' }
    $buildReport.zigArchiveSha256 = $hash
    $buildReport.stage = 'toolchain-extract'
    Write-Host 'Extracting the verified Zig archive.'
    Expand-Archive -LiteralPath $archive -DestinationPath $toolRoot
    $zig = Join-Path $toolRoot 'zig-x86_64-windows-0.14.1\zig.exe'
    $buildReport.stage = 'text-contrast-test'
    & (Join-Path $root 'test-contrast.ps1') -Zig $zig
    $buildReport.textContrastTest = 'passed'
    $buildReport.stage = 'edge-drawing-test'
    & (Join-Path $root 'test-edges.ps1') -Zig $zig
    $buildReport.edgeDrawingTest = 'passed'
    $buildReport.stage = 'build'
    Write-Host 'Compiling Dark1C without executing the produced code.'
    # Checkout contains no user wallpaper. create-background.ps1 makes the neutral fallback.
    & (Join-Path $root 'build-live.ps1') -Zig $zig
    $buildReport.stage = 'defender-scan'
    Write-Host 'Scanning the produced DLL and both executables with Defender.'
    $sourcePaths = @('Dark1C.Native.dll', 'Dark1C.exe', 'Dark1C.Control.exe') | ForEach-Object { Join-Path $root $_ }
    $paths = @($sourcePaths | ForEach-Object {
        $destination = Join-Path $scanRoot ([IO.Path]::GetFileName($_))
        Copy-Item -LiteralPath $_ -Destination $destination
        if ((Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) {
            throw 'Scan copy checksum mismatch.'
        }
        $destination
    })
    $scan = Invoke-Dark1CDefenderScan -ArtifactPaths $paths -AdditionalDetectionPaths $sourcePaths
    $scan | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reports 'defender.json') -Encoding UTF8
    if ($scan.result -ne 'no-detection-in-this-scan') { throw "Verification failed at $($scan.stage)." }
    $buildReport.result = 'build-and-scan-completed'
    $buildReport.stage = 'complete'
} finally {
    $buildReport | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $reports 'build.json') -Encoding UTF8
}
