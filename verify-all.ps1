param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$artifactDir = Join-Path $PSScriptRoot 'artifacts'
& (Join-Path $PSScriptRoot 'build.ps1')
& (Join-Path $PSScriptRoot 'verify.ps1') -ArtifactDirectory $artifactDir |
    Tee-Object -FilePath (Join-Path $artifactDir 'verification.log')
& $Python -X utf8 (Join-Path $PSScriptRoot 'independent_check.py') (Join-Path $artifactDir 'independent-states.json') (Join-Path $artifactDir 'independent-comparison.csv')
if ($LASTEXITCODE -ne 0) { throw 'Independent quadrature verification failed.' }
& $Python -X utf8 (Join-Path $PSScriptRoot 'additional_independent_check.py') (Join-Path $artifactDir 'additional-independent-states.json') (Join-Path $artifactDir 'additional-independent-comparison.csv')
if ($LASTEXITCODE -ne 0) { throw 'Independent strip integration verification failed.' }
Write-Output "All verification completed. Artifacts: $artifactDir"
