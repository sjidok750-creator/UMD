$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /utf8output /optimize+ /target:library /out:PMStudio.Core.dll /r:System.Core.dll /r:System.Web.dll /r:System.Web.Extensions.dll Model.cs SectionRecipe.cs AdditionalExamples.cs RebarCatalog.cs Geometry.cs Engine.cs SectionSolver.cs DirectionalSolver.cs Reduction.cs Reports.cs ApiServer.cs
    if ($LASTEXITCODE -ne 0) { throw 'Core build failed.' }
    & $compiler /nologo /utf8output /optimize+ /target:winexe /out:PMStudio.exe /r:PMStudio.Core.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll App.cs GuidedSectionEditor.cs SectionForms.cs Plots.cs Dashboard.cs ReportSheet.cs Theme.cs
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
    & $compiler /nologo /utf8output /target:exe /out:PMStudio.Api.exe /r:PMStudio.Core.dll ApiMain.cs
    if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }
    Write-Output 'Built PMStudio.exe, PMStudio.Api.exe, PMStudio.Core.dll'
} finally { Pop-Location }
