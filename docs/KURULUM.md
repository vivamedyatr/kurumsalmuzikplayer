# Derleme ve deneme

## Gerekenler (geliştirme bilgisayarı)
- .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8`). Program .NET Framework 4.8 hedefler; SDK sadece derlemek için.
- `vendor/mpv/mpv.exe` — git'e girmez (84 MB). Kaynağı ve sürümü: `vendor/mpv/MPV-LISANS.txt`
  (mpv 0.35, shinchiro derlemesi `mpv-x86_64-20230101-git-ad65c88.7z`; eski projenin `vendor/` klasöründe arşiviyle duruyor).

## Derleme
```
dotnet build -c Release
dotnet test -c Release
```
Çıktı: `src/KurumsalMuzikPlayer/bin/Release/` (`KurumsalMuzikPlayer.exe`, `Kmp.Core.dll`, `Newtonsoft.Json.dll`, `mpv/`).

## Yapı
| Yer | Ne |
|---|---|
| `src/Kmp.Core/` | Pencereden bağımsız çekirdek: veri klasörleri, günlük, albüm kabı, mpv istemcisi, boru kanalı, deney |
| `src/KurumsalMuzikPlayer/Engine/` | Motor: tepsi, müzik, pencere bekçisi (görünmez süreç) |
| `src/KurumsalMuzikPlayer/Ui/` | Pencere (WPF, ayrı süreç) |
| `tests/Kmp.Core.Tests/` | Birim testleri (xUnit) |

## Programın klasörleri
`%LOCALAPPDATA%\Kurumsal Muzik Player\` (`KMP_DATA_DIR` ortam değişkeniyle değiştirilebilir):
- `gunluk\motor-yyyy-MM-dd.txt`, `gunluk\pencere-yyyy-MM-dd.txt` — 7 gün tutulur
- `ayarlar.json` — ses düzeyi
- `deneme-muzik\` — Faz 0: buraya konan ses dosyaları karışık ve döngüde çalar
- `deneme-arsiv\` — Faz 0 dayanıklılık deneyinin yazdığı sahte arşiv (her deneyde silinip baştan yazılır)

## Faz 0 dayanıklılık deneyi
Motor, müzik çalarken gerçek indirme gibi arşiv yazar ve geri okur:
```
KurumsalMuzikPlayer.exe --deneme-arsiv --desen kap --gb 6 --mbps 6
```
- `--desen kap` — 1.0 yöntemi: albüm başına tek dosya, bilgisayarda şifreleme yok.
- `--desen dosya` — 2.x yöntemi (karşılaştırma için): her parça bilgisayarda şifrelenip ayrı dosyaya yazılır.
- `--mbps 0` — hız sınırı yok (en sert deneme).

Sonuç motor günlüğünde (10 dk'da bir "Durum:" satırı ve "yazmayı bitirdi" satırı) ve pencerede görünür.
Windows'un kendi donma/çökme kayıtları:
```
Get-WinEvent -FilterHashtable @{LogName='Application'; Id=1000,1002; StartTime=(Get-Date).AddDays(-1)} | Where-Object Message -match KurumsalMuzikPlayer
```
