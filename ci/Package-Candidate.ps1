# Package only bytes identified by this job's successful scan. Never executes them.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Candidate packaging is for the GitHub-hosted verification worker only.'
}
$root = Split-Path $PSScriptRoot -Parent
$reports = Join-Path $PSScriptRoot 'reports'
$build = Get-Content -LiteralPath (Join-Path $reports 'build.json') -Raw | ConvertFrom-Json
$scan = Get-Content -LiteralPath (Join-Path $reports 'defender.json') -Raw | ConvertFrom-Json
if ($build.commit -ne $env:GITHUB_SHA -or $build.result -ne 'build-and-scan-completed' -or
    $scan.result -ne 'no-detection-in-this-scan' -or $scan.stage -ne 'complete' -or
    -not $scan.exclusionsChecked -or @($scan.detections).Count -ne 0 -or @($scan.files).Count -ne 3) {
    throw 'This commit does not have a completed successful scan report.'
}
foreach ($name in @('Dark1C.exe','Dark1C.Native.dll','Dark1C.Control.exe')) {
    $entry = @($scan.files | Where-Object { $_.name -ceq $name })
    if ($entry.Count -ne 1 -or $entry[0].sha256 -notmatch '^[0-9a-f]{64}$' -or
        (Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry[0].sha256) {
        throw "Missing, changed or unverified build output: $name"
    }
}
$destination = Join-Path $PSScriptRoot 'package'
if (Test-Path -LiteralPath $destination) { throw 'Candidate directory already exists.' }
$null = New-Item -ItemType Directory -Path $destination
Copy-Item -LiteralPath (Join-Path $root 'Dark1C.exe') -Destination $destination
$mainHash = ($scan.files | Where-Object { $_.name -ceq 'Dark1C.exe' }).sha256
if ((Get-FileHash -LiteralPath (Join-Path $destination 'Dark1C.exe') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $mainHash) {
    throw 'Candidate copy checksum mismatch.'
}
Copy-Item -LiteralPath (Join-Path $root 'LICENSE.txt') -Destination $destination
Copy-Item -LiteralPath (Join-Path $root 'vendor\minhook-1.3.4\LICENSE.txt') -Destination (Join-Path $destination 'MINHOOK-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $root 'docs\BUILD-CANDIDATE.md') -Destination (Join-Path $destination 'README.md')
Copy-Item -LiteralPath (Join-Path $reports 'build.json'), (Join-Path $reports 'defender.json') -Destination $destination
@("Source commit: $env:GITHUB_SHA", "Source: $env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/tree/$env:GITHUB_SHA",
    "Verification: $env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID",
    'Experimental candidate. Application not executed by this workflow. No Microsoft false-positive determination.') |
    Set-Content -LiteralPath (Join-Path $destination 'PROVENANCE.txt') -Encoding UTF8
$manifest = @(Get-ChildItem -LiteralPath $destination -File | Sort-Object Name | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
})
$manifest | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ASCII
