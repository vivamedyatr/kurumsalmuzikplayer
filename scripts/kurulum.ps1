# Kurulum dosyasını üretir: dist\Kurumsal-Muzik-Player-Kurulum-<sürüm>.exe
# Gerekenler: .NET 8 SDK, Inno Setup 6, vendor\mpv\mpv.exe (docs\KURULUM.md)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User")

if (-not (Test-Path "$root\vendor\mpv\mpv.exe")) { throw "vendor\mpv\mpv.exe yok (docs\KURULUM.md)" }
$version = ([xml](Get-Content "$root\Directory.Build.props" -Raw)).Project.PropertyGroup.InformationalVersion
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 bulunamadı" }

Write-Host "Derleniyor ($version)..."
dotnet build "$root\KurumsalMuzikPlayer.sln" -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "derleme başarısız" }
dotnet test "$root\KurumsalMuzikPlayer.sln" -c Release --no-build --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "testler başarısız" }

Write-Host "Kurulum dosyası üretiliyor..."
& $iscc /Q "/DAppVersion=$version" "$root\installer\KurumsalMuzikPlayer.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup başarısız" }
$out = Get-Item "$root\dist\Kurumsal-Muzik-Player-Kurulum-$version.exe"
"{0} ({1:0.0} MB)" -f $out.FullName, ($out.Length / 1MB)
(Get-FileHash $out.FullName -Algorithm SHA256).Hash
