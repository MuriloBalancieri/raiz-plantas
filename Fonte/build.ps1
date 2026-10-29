$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'Compilador do .NET Framework nao encontrado.' }
$appSources = @('Core.cs','InitialCredentials.cs','SpeciesCare.cs','Care.cs','CareUi.cs','App.cs','UiLayout.cs','CityPicker.cs','AppPaths.cs','Messaging.cs','MessagingUi.cs','Telegram.cs','TelegramUi.cs','WebhookServer.cs','Plotter.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compilerPath /nologo /target:winexe "/out:$projectRoot\Raiz.exe" "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$PSScriptRoot\raiz.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Web.dll /r:System.Net.Http.dll /r:System.Security.dll $appSources
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o aplicativo.' }
Write-Output 'Raiz.exe compilado.'
