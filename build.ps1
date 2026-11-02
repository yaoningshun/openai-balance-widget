param([switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$framework = Split-Path $compiler
$outDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Security.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll','System.Xaml.dll')
$compilerArgs = @('/nologo','/optimize+','/target:winexe',('/out:' + (Join-Path $outDir 'Saldo.exe')),('/resource:' + (Join-Path $projectRoot 'src/View.xaml') + ',Saldo.View.xaml'))
$compilerArgs += '/win32manifest:' + (Join-Path $projectRoot 'src/app.manifest')
if (Test-Path -LiteralPath (Join-Path $projectRoot 'assets/saldo.ico')) { $compilerArgs += '/win32icon:' + (Join-Path $projectRoot 'assets/saldo.ico') }
foreach ($ref in $references) { $compilerArgs += '/reference:' + (Join-Path $framework $ref) }
$compilerArgs += @('src/Core.cs','src/Storage.cs','src/App.cs') | ForEach-Object { Join-Path $projectRoot $_ }
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Test) {
    $testArgs = @('/nologo','/target:exe',('/out:' + (Join-Path $outDir 'Saldo.Tests.exe')),('/reference:' + (Join-Path $framework 'System.Net.Http.dll')),('/reference:' + (Join-Path $framework 'System.Web.Extensions.dll')), (Join-Path $projectRoot 'src/Core.cs'), (Join-Path $projectRoot 'tests/BalanceTests.cs'))
    & $compiler @testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & (Join-Path $outDir 'Saldo.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Get-Item -LiteralPath (Join-Path $outDir 'Saldo.exe') | Select-Object Name,Length
