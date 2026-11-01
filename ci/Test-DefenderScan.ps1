# Synthetic text fixtures and mocked Defender commands only; no EXE is built/run.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'DefenderScan.ps1')
$script:scenario = ''
$script:scans = 0
$script:statusCalls = 0
function Get-MpComputerStatus {
    param($ErrorAction)
    $script:statusCalls++
    [pscustomobject]@{
        AMServiceEnabled = $true; AntivirusEnabled = ($script:scenario -ne 'disabled')
        RealTimeProtectionEnabled = ($script:scenario -ne 'real-time-off')
        AMRunningMode = $(if ($script:scenario -eq 'passive') { 'Passive Mode' } else { 'Normal' })
        AntivirusSignatureLastUpdated = $(if ($script:scenario -eq 'stale') { [DateTime]::UtcNow.AddDays(-5) } else { [DateTime]::UtcNow })
        AMEngineVersion = 'mock'; AntivirusSignatureVersion = 'mock'
    }
}
function Start-MpScan {
    param($ScanType, $ScanPath, $ErrorAction)
    $script:scans++
    if ($script:scenario -eq 'scan-error') { throw 'Mock scanner failure' }
    if ($script:scenario -eq 'changed') { [IO.File]::AppendAllText($ScanPath, 'changed') }
    if ($script:scenario -eq 'quarantined') { Remove-Item -LiteralPath $ScanPath }
}
function Get-Dark1CExclusionExitCode {
    param($Path)
    if ($script:scenario -eq 'excluded') { return 0 }
    if ($script:scenario -eq 'exclusion-error') { return -1 }
    return 1
}
function Get-MpThreatDetection {
    param($ErrorAction)
    if ($script:scenario -eq 'history-error') { throw 'Mock history unavailable' }
    if ($script:scenario -eq 'detected') {
        [pscustomobject]@{ Resources = @("file:_$script:fixture"); ThreatID = 123; ActionSuccess = $true }
    } else {
        [pscustomobject]@{ Resources = @('file:_C:\unrelated\fixture.txt'); ThreatID = 456; ActionSuccess = $true }
    }
}
$testDir = Join-Path ([IO.Path]::GetTempPath()) ('dark1c-scan-test-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testDir
$script:fixture = Join-Path $testDir 'harmless-text.txt'
try {
    foreach ($case in @('clean','disabled','real-time-off','passive','stale','excluded','exclusion-error','scan-error','history-error','detected','changed','quarantined','missing')) {
        $script:scenario = $case; $script:scans = 0; $script:statusCalls = 0
        [IO.File]::WriteAllText($script:fixture, 'Ordinary test text. Not executable code.')
        if ($case -eq 'missing') { Remove-Item -LiteralPath $script:fixture }
        $report = Invoke-Dark1CDefenderScan -ArtifactPaths @($script:fixture)
        $expected = if ($case -eq 'clean') { 'no-detection-in-this-scan' } elseif ($case -eq 'detected') { 'detected' } else { 'unverified' }
        if ($report.result -ne $expected) { throw "$case returned $($report.result), expected $expected" }
        if ($case -in @('disabled','real-time-off','passive','stale','excluded','exclusion-error','missing') -and $script:scans -ne 0) { throw "$case scanned despite failed preflight" }
        if ($case -eq 'clean' -and ($script:scans -ne 1 -or $script:statusCalls -ne 2)) { throw 'Scan or postflight not performed.' }
        if (($report | ConvertTo-Json -Depth 6).Contains($testDir)) { throw 'Report leaked a resource path.' }
        Write-Output "PASS $case"
    }
} finally {
    if (Test-Path -LiteralPath $script:fixture) { Remove-Item -LiteralPath $script:fixture }
    Remove-Item -LiteralPath $testDir
}
