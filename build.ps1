$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:DOTNET_CLI_HOME = 'D:\Codex\tools\dotnet-home'
$env:NUGET_PACKAGES = 'D:\Codex\tools\nuget'
$env:TEMP = 'D:\Codex\tools\temp'
$env:TMP = $env:TEMP
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null
$sdk = 'D:\Codex\tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { throw 'Нужен .NET 8 SDK в D:\Codex\tools\dotnet' }
& $sdk publish Keyline.csproj -c Release -r win-x64 --self-contained true -o '..\Keyline-Windows' -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки Keyline' }
