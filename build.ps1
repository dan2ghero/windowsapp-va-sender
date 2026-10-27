$ErrorActionPreference='Stop'
$base=$PSScriptRoot
$sdk=(& dotnet --list-sdks | Select-Object -Last 1) -replace '^([^ ]+) \[(.*)\]$','$2\$1'
$compiler=Join-Path $sdk 'Roslyn\bincore\csc.dll'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$common=@('/nologo','/noconfig','/nostdlib+','/langversion:12','/platform:x64','/optimize+',('/win32manifest:'+(Join-Path $base 'src\app.manifest')))
foreach($dll in 'mscorlib','System','System.Core','System.Windows.Forms','System.Drawing','System.Web.Extensions','System.IO.Compression','System.IO.Compression.FileSystem') {$common+=('/reference:'+(Join-Path $framework ($dll+'.dll')))}
$sources=Get-ChildItem (Join-Path $base 'src') -Filter '*.cs' | Where-Object Name -ne 'AudioCapture.cs' | ForEach-Object FullName
& dotnet $compiler @common /target:winexe ('/out:'+(Join-Path $base 'windowsapp-va-sender.exe')) @sources
if($LASTEXITCODE -ne 0){throw 'UI compile failed'}
New-Item -ItemType Directory -Force (Join-Path $base 'tools') | Out-Null
& dotnet $compiler @common /nullable:enable /target:exe ('/out:'+(Join-Path $base 'tools\AppAudio.exe')) (Join-Path $base 'src\AudioCapture.cs') (Join-Path $base 'src\ProcessIdentity.cs')
if($LASTEXITCODE -ne 0){throw 'Audio helper compile failed'}
Write-Host 'Build OK (.NET Framework 4.8 x64; no NuGet packages)'
