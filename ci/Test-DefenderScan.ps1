# Synthetic text fixtures and mocked Defender commands only; no EXE is built/run.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'DefenderScan.ps1')
$script:scenario = ''
$script:scans = 0
$script:statusCalls = 0
$script:exclusionCalls = 0
function Get-MpComputerStatus {
    param($ErrorAction)
    $script:statusCalls++
    [pscustomobject]@{
        AMServiceEnabled = $true; AntivirusEnabled = ($script:scenario -ne 'disabled')
        RealTimeProtectionEnabled = ($script:scenario -ne 'real-time-off' -and
            -not ($script:scenario -eq 'startup' -and $script:statusCalls -lt 3))
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
    $script:exclusionCalls++
    if ($script:scenario -eq 'exclusion-propagation' -and $script:exclusionCalls -lt 3) { return 0 }
    if ($script:scenario -eq 'excluded') { return 0 }
    if ($script:scenario -eq 'exclusion-error') { return -1 }
    return 1
}
function Get-MpThreatDetection {
    param($ErrorAction)
    if ($script:scenario -eq 'history-error') { throw 'Mock history unavailable' }
    if ($script:scenario -eq 'source-detected') {
        [pscustomobject]@{ Resources = @("file:_$script:sourceFixture"); ThreatID = 123; ActionSuccess = $true }
    } elseif ($script:scenario -eq 'detected') {
        [pscustomobject]@{ Resources = @("file:_$script:fixture"); ThreatID = 123; ActionSuccess = $true }
    } else {
        [pscustomobject]@{ Resources = @('file:_C:\unrelated\fixture.txt'); ThreatID = 456; ActionSuccess = $true }
    }
}
$testDir = Join-Path ([IO.Path]::GetTempPath()) ('dark1c-scan-test-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testDir
$script:fixture = Join-Path $testDir 'harmless-text.txt'
$script:sourceFixture = Join-Path $testDir 'original-text.txt'
try {
    $script:scenario='startup'; $script:statusCalls=0
    $null = Wait-Dark1CDefenderReady -Attempts 3 -DelaySeconds 0
    if ($script:statusCalls -ne 3) { throw 'Readiness did not wait for active protection.' }
    Write-Output 'PASS readiness-startup'
    $script:scenario='real-time-off'; $script:statusCalls=0
    $refused=$false
    try { $null = Wait-Dark1CDefenderReady -Attempts 3 -DelaySeconds 0 } catch { $refused=$true }
    if (-not $refused -or $script:statusCalls -ne 3) { throw 'Persistent disabled protection was not rejected within the retry bound.' }
    Write-Output 'PASS readiness-timeout'
    $script:scenario='exclusion-propagation'; $script:exclusionCalls=0
    if ((Wait-Dark1CExclusionRemoval -Path $script:fixture -Attempts 3 -DelaySeconds 0) -ne 1 -or $script:exclusionCalls -ne 3) { throw 'Exclusion removal was not confirmed.' }
    Write-Output 'PASS exclusion-propagation'
    foreach ($case in @('excluded','exclusion-error')) {
        $script:scenario=$case; $script:exclusionCalls=0; $reason=$null
        try { $null=Wait-Dark1CExclusionRemoval -Path $script:fixture -Attempts 3 -DelaySeconds 0 } catch { $reason=$_.Exception.Message }
        $expectedReason=if($case -eq 'excluded'){'artifact-excluded'}else{'exclusion-tool-error'}
        $expectedCalls=if($case -eq 'excluded'){3}else{1}
        if($reason -ne $expectedReason -or $script:exclusionCalls -ne $expectedCalls){throw "Invalid exclusion wait result: $case"}
        Write-Output "PASS wait-$case"
    }
    foreach ($case in @('clean','disabled','real-time-off','passive','stale','excluded','exclusion-error','scan-error','history-error','detected','source-detected','changed','quarantined','missing')) {
        $script:scenario = $case; $script:scans = 0; $script:statusCalls = 0
        [IO.File]::WriteAllText($script:fixture, 'Ordinary test text. Not executable code.')
        if ($case -eq 'missing') { Remove-Item -LiteralPath $script:fixture }
        $report = Invoke-Dark1CDefenderScan -ArtifactPaths @($script:fixture) -AdditionalDetectionPaths @($script:sourceFixture)
        $expected = if ($case -eq 'clean') { 'no-detection-in-this-scan' } elseif ($case -in @('detected','source-detected')) { 'detected' } else { 'unverified' }
        if ($case -eq 'excluded' -and $report.reason -ne 'artifact-excluded') { throw 'Excluded path reason lost.' }
        if ($case -eq 'exclusion-error' -and $report.reason -ne 'exclusion-tool-error') { throw 'Exclusion error reason lost.' }
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
