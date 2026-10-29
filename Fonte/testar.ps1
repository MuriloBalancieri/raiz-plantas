param([string]$OutputDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) ('RaizTestes_' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$testRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$appSources = @('Core.cs','InitialCredentials.cs','SpeciesCare.cs','Care.cs','DailyCare.cs','CareUi.cs','App.cs','UiLayout.cs','SetupAndBackground.cs','DailyHistory.cs','CityPicker.cs','AppPaths.cs','Messaging.cs','MessagingUi.cs','Telegram.cs','TelegramWater.cs','TelegramUi.cs','WebhookServer.cs','Plotter.cs','Tests.cs','CareTests.cs','MessagingTests.cs','TelegramTests.cs','SpeciesTests.cs','UiTests.cs','InitialCredentialTests.cs','FeatureTests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compilerPath /nologo /target:exe /main:Tests "/out:$testRoot\RaizTests.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Web.dll /r:System.Net.Http.dll /r:System.Security.dll $appSources
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilacao dos testes.' }
& "$testRoot\RaizTests.exe" "$testRoot\Dados"
if ($LASTEXITCODE -ne 0) { throw 'Um teste falhou.' }
Write-Output "Resultados isolados em $testRoot"




