$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$benchmarkDir = Join-Path ([IO.Path]::GetTempPath()) ('PMStudio-benchmark-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $benchmarkDir | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PMStudio.Core.dll') -Destination $benchmarkDir
& $compiler /nologo /optimize+ /out:"$benchmarkDir\Benchmark.exe" /r:"$benchmarkDir\PMStudio.Core.dll" /r:System.Core.dll (Join-Path $PSScriptRoot 'Benchmark.cs')
if ($LASTEXITCODE -ne 0) { throw 'Benchmark build failed.' }
& "$benchmarkDir\Benchmark.exe" $benchmarkDir
if ($LASTEXITCODE -ne 0) { throw 'Benchmark failed.' }
Write-Output "Benchmark artifacts: $benchmarkDir"
