param([string]$ArtifactDirectory = '')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testDir = if ($ArtifactDirectory) { [IO.Path]::GetFullPath($ArtifactDirectory) } else { Join-Path ([IO.Path]::GetTempPath()) ('PMStudio-verify-' + [guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PMStudio.Core.dll') -Destination $testDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PMStudio.exe') -Destination $testDir
& $compiler /nologo /target:exe /out:"$testDir\Tests.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& "$testDir\Tests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
& $compiler /nologo /target:exe /out:"$testDir\UiTests.exe" /r:"$testDir\PMStudio.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'UiTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI test build failed.' }
& "$testDir\UiTests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'UI component tests failed.' }
& $compiler /nologo /target:exe /out:"$testDir\RenderingTests.exe" /r:"$testDir\PMStudio.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'RenderingTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Rendering test build failed.' }
& "$testDir\RenderingTests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Rendering tests failed.' }
Write-Output "Test artifacts: $testDir"
& $compiler /nologo /target:exe /out:"$testDir\ReportTests.exe" /r:"$testDir\PMStudio.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'ReportTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Report test build failed.' }
& "$testDir\ReportTests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Report tests failed.' }
& $compiler /nologo /target:exe /out:"$testDir\ReferenceTests.exe" /r:"$testDir\PMStudio.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'ReferenceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Reference test build failed.' }
& "$testDir\ReferenceTests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Reference tests failed.' }
& $compiler /nologo /target:exe /out:"$testDir\Manual2008Tests.exe" /r:"$testDir\PMStudio.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'Manual2008Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Manual test build failed.' }
& "$testDir\Manual2008Tests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Manual tests failed.' }
& $compiler /nologo /target:exe /out:"$testDir\LayoutTests.exe" /r:"$testDir\PMStudio.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'LayoutTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Layout test build failed.' }
& "$testDir\LayoutTests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Layout tests failed.' }
& $compiler /nologo /target:exe /out:"$testDir\IndependentReferenceTests.exe" /r:"$testDir\PMStudio.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'IndependentReferenceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Independent reference build failed.' }
& "$testDir\IndependentReferenceTests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Independent reference failed.' }

& $compiler /nologo /target:exe /out:"$testDir\InsetLayoutTests.exe" /r:"$testDir\PMStudio.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'InsetLayoutTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Inset layout build failed.' }
& "$testDir\InsetLayoutTests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Inset layout tests failed.' }

& $compiler /nologo /target:exe /out:"$testDir\AdditionalValidationTests.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'AdditionalValidationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Additional validation build failed.' }
& "$testDir\AdditionalValidationTests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Additional validation failed.' }

& $compiler /nologo /target:exe /out:"$testDir\AsymmetricAnnotationTests.exe" /r:"$testDir\PMStudio.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'AsymmetricAnnotationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Asymmetric annotation build failed.' }
& "$testDir\AsymmetricAnnotationTests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Asymmetric annotation tests failed.' }

& $compiler /nologo /target:exe /out:"$testDir\GuidedInputTests.exe" /r:"$testDir\PMStudio.exe" /r:"$testDir\PMStudio.Core.dll" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'GuidedInputTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Guided input test build failed.' }
& "$testDir\GuidedInputTests.exe" $testDir
if ($LASTEXITCODE -ne 0) { throw 'Guided input tests failed.' }
