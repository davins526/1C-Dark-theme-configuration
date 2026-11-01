# No build, process attachment, security configuration or sample submission here.
function Assert-Dark1CDefenderReady {
    $state = Get-MpComputerStatus -ErrorAction Stop
    if (-not $state.AMServiceEnabled -or -not $state.AntivirusEnabled -or
        -not $state.RealTimeProtectionEnabled -or $state.AMRunningMode -ne 'Normal') {
        throw 'Defender must be active in Normal mode with real-time protection.'
    }
    if (-not $state.AntivirusSignatureLastUpdated -or
        $state.AntivirusSignatureLastUpdated.ToUniversalTime() -lt [DateTime]::UtcNow.AddDays(-2) -or
        $state.AntivirusSignatureLastUpdated.ToUniversalTime() -gt [DateTime]::UtcNow.AddHours(1)) {
        throw 'Defender signatures are missing, stale or have an invalid timestamp.'
    }
    return $state
}

function Get-Dark1CExclusionExitCode {
    param([string]$Path)
    $platform = Join-Path $env:ProgramData 'Microsoft\Windows Defender\Platform'
    $tool = Get-ChildItem -Path "$platform\*\MpCmdRun.exe" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $tool) { $tool = Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe' }
    $signature = Get-AuthenticodeSignature -LiteralPath $tool -ErrorAction Stop
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'Expected a valid Microsoft-signed Defender command-line tool.'
    }
    & $tool -CheckExclusion -Path $Path *> $null
    $exitCode = $LASTEXITCODE
    # The documented success value here is 1; do not leak it to the job shell's
    # final native-exit check. The caller validates it and throws on any other value.
    $global:LASTEXITCODE = 0
    return $exitCode
}

function Assert-Dark1CPathScanned {
    param([string]$Path)
    # Microsoft documents 0 = excluded, 1 = not excluded; other values are errors.
    $exitCode = Get-Dark1CExclusionExitCode -Path $Path
    if ($exitCode -eq 0) { throw 'artifact-excluded' }
    if ($exitCode -ne 1) { throw 'exclusion-tool-error' }
}

function Invoke-Dark1CDefenderScan {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$ArtifactPaths, [string[]]$AdditionalDetectionPaths = @())
    $ErrorActionPreference = 'Stop'
    $report = [ordered]@{
        schema = 1; result = 'unverified'; stage = 'preflight'; reason = $null
        startedUtc = [DateTime]::UtcNow.ToString('o'); finishedUtc = $null
        engine = $null; signatures = $null; signatureUpdatedUtc = $null
        files = @(); detections = @(); exclusionsChecked = $false
        limitation = 'A scan of these bytes is not a Microsoft false-positive verdict or runtime audit.'
    }
    try {
        $state = Assert-Dark1CDefenderReady
        $report.engine = $state.AMEngineVersion
        $report.signatures = $state.AntivirusSignatureVersion
        $report.signatureUpdatedUtc = $state.AntivirusSignatureLastUpdated.ToUniversalTime().ToString('o')
        $report.stage = 'hash-before-scan'
        if ($ArtifactPaths.Count -eq 0) { throw 'No artifacts supplied.' }
        $paths = @($ArtifactPaths | ForEach-Object { (Resolve-Path -LiteralPath $_).ProviderPath })
        $detectionPaths = @($paths) + @($AdditionalDetectionPaths | ForEach-Object { [IO.Path]::GetFullPath($_) })
        $report.stage = 'exclusion-check'
        foreach ($path in $paths) { Assert-Dark1CPathScanned -Path $path }
        $report.exclusionsChecked = $true
        $report.stage = 'hash-before-scan'
        $report.files = @($paths | ForEach-Object {
            [ordered]@{ name = [IO.Path]::GetFileName($_); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
        })
        $report.stage = 'custom-scan'
        foreach ($path in $paths) {
            Start-MpScan -ScanType CustomScan -ScanPath $path -ErrorAction Stop
        }
        $report.stage = 'detection-history'
        # Include detections made by real-time protection before the explicit scan.
        # A remediated/quarantined detection is still a failed verification.
        $detections = @(Get-MpThreatDetection -ErrorAction Stop | Where-Object {
            $resources = @($_.Resources) -join "`n"
            $matchesArtifact = $false
            foreach ($path in $detectionPaths) {
                if ($resources.IndexOf($path, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $matchesArtifact = $true }
            }
            $matchesArtifact
        })
        $report.detections = @($detections | ForEach-Object {
            [ordered]@{ threatId = $_.ThreatID; actionSuccess = $_.ActionSuccess }
        })
        if ($detections.Count -gt 0) { $report.result = 'detected'; throw 'Artifact detection present.' }
        $report.stage = 'hash-after-scan'
        for ($i = 0; $i -lt $paths.Count; $i++) {
            $hash = (Get-FileHash -LiteralPath $paths[$i] -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($hash -ne $report.files[$i].sha256) { throw 'Artifact changed during scan.' }
        }
        $report.stage = 'postflight'
        foreach ($path in $paths) { Assert-Dark1CPathScanned -Path $path }
        $null = Assert-Dark1CDefenderReady
        $report.result = 'no-detection-in-this-scan'
        $report.stage = 'complete'
    } catch {
        # Deliberately omit exception text and raw detection resources from public reports.
        # The stage identifies the failed check without publishing user/process paths.
        if ($report.result -ne 'detected') { $report.result = 'unverified' }
        $report.reason = switch ($_.Exception.Message) {
            'artifact-excluded' { 'artifact-excluded' }
            'exclusion-tool-error' { 'exclusion-tool-error' }
            default { 'check-failed' }
        }
    }
    $report.finishedUtc = [DateTime]::UtcNow.ToString('o')
    return [pscustomobject]$report
}
