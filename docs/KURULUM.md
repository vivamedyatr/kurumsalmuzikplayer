# Derleme ve deneme

Sunucu, yayın ve mağaza kurulumu dahil bütün sistem: `../kurumsalmuzik/docs/KURULUM-KILAVUZU.html` §12.

## Gerekenler (geliştirme bilgisayarı)
- .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8`). Program .NET Framework 4.8 hedefler; SDK sadece derlemek için.
- Inno Setup 6 (kurulum dosyası), Git Bash ve Tailscale (yayın betiği sunucuya `onur@kurumsalmuzik-vps.tail8660b4.ts.net`
  ile bağlanır; Tailscale kapalıysa ofisten `KM_HOST=onur@2.24.171.101`).
- `vendor/mpv/mpv.exe` — git'e girmez (84 MB). Kaynağı ve sürümü: `vendor/mpv/MPV-LISANS.txt`
  (mpv 0.35, shinchiro derlemesi `mpv-x86_64-20230101-git-ad65c88.7z`; eski projenin `vendor/` klasöründe arşiviyle duruyor).

## Derleme
```
dotnet build -c Release
dotnet test -c Release
```
Çıktı: `src/KurumsalMuzikPlayer/bin/Release/` (`KurumsalMuzikPlayer.exe`, `Kmp.Core.dll`, `Newtonsoft.Json.dll`, `mpv/`).

## Kurulum dosyası
```
powershell -ExecutionPolicy Bypass -File scripts\kurulum.ps1
```
Derler, testleri çalıştırır, `dist\Kurumsal-Muzik-Player-Kurulum-<sürüm>.exe` üretir (Inno Setup 6 gerekir;
betik `installer\KurumsalMuzikPlayer.iss`). Sürüm `Directory.Build.props` → `InformationalVersion`.
Sessiz kurulum: `/VERYSILENT /SUPPRESSMSGBOXES`. Kurulum ve kaldırma çalışan programı önce `--kapat` ile kapatır.

## Yeni sürüm yayınlamak (kendini güncelleme)
1. `Directory.Build.props`: `InformationalVersion` (görünen sürüm) ve `FileVersion`'ın son parçası (yapı; her yayında artar).
2. `powershell -ExecutionPolicy Bypass -File scripts\kurulum.ps1`
3. `bash scripts/yayinla.sh` (Onur onayıyla) → `kurumsalmuzik.net/indir/kmp/latest.json` + kurulum dosyası; sitedeki sabit
   adres `/indir/Kurumsal-Muzik-Player-Kurulum.exe` de son sürüme çevrilir.
4. Commit, `git tag v<sürüm>`, `git push --follow-tags` (GitHub `vivamedyatr/kurumsalmuzikplayer`).
Kurulu programlar 4 saatte bir bakar, arkada indirir, SHA-256'yı doğrular, **yeniden açılışta** (müzik başlamadan) sessiz
kurar ve kendiliğinden açılır. 3 başarısız denemeden sonra o sürüm atlanır. 2.x'in `/indir/latest.yml`'ine dokunulmaz.
Yerel deneme: `$env:KMP_GUNCELLEME_URL = "http://127.0.0.1:8765/kmp/latest.json"` ve o klasörü sunan bir HTTP sunucusu.

## Yerel sunucuyla deneme
Program canlı sunucu yerine yerel geliştirme sunucusuna bağlanabilir (`../kurumsalmuzik`: `npm run db`, `npm run dev`):
```
$env:KMP_SERVER = "http://localhost:3000"   # sunucu
$env:KMP_DATA_DIR = "<boş bir klasör>"      # ayrı veri klasörü
$env:KMP_DURUM_SN = "10"                    # durum sorgusu 15 dk yerine 10 sn (düşmeyi beklemeden görmek için)
$env:KMP_HIZ_KBPS = "2000"                  # indirme hız sınırı (varsayılan 6000 = 6 Mbps)
$env:KMP_SES_AYGITI = "wasapi/yok"         # olmayan ses aygıtı: "ses çıkışı yok" durumunu denemek için
```
Yerel deneme hesabının bilgileri `deneme.env`'de (git'e girmez; sadece yerel veritabanında geçerli).
Arşiv denemesi için yerel Worker (`npm run dev:media -- --yerel`) ve deneme müziği (`npm run media:test -- <tarz> --album "Ad"`)
gerekir; ikisi de sadece bilgisayardaki taklit R2'yi kullanır.

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
- `ayarlar.json` — ses düzeyi · `pencere.json` — pencerenin yeri ve ölçeği (pencere kapatılınca gizlenir, süreç kapanmaz)
- `cihaz.bin` — şube kaydı ve sunucu anahtarı (DPAPI ile şifreli; başka kullanıcı/bilgisayarda açılmaz)
- `paket.json` — son offline paket (akış dilimleri, albümler, şarkılar)
- `cihaz-paket.json` — bu bilgisayarın albüm listesi ve "Aylık yeni müzik" anahtarı
- `arsiv-anahtari.bin` — bu bilgisayara özel arşiv anahtarı (ilk açılışta üretilir, DPAPI)
- `arsiv\c1\<albüm>.kmp` — albüm kapları (parçalar bellekte şifrelenip eklenir)
- `kapak\<albüm>.jpg` — albüm kapakları
- `anons\<anons>.mp3`, `anonslar.json`, `anons-torba.json` (karışık planların sırası), `anons-gonderilecek.json` (internet gelince gidecek çalma kayıtları)
- `guncelleme\` — inmiş, kurulmayı bekleyen yeni sürüm (`bekleyen.json` + kurulum dosyası)
- `durum.json` — saat farkı, son başarılı bağlantı, hizmet son günü (7 gün kuralı)
- `gecmis.json` — çalma geçmişi (7 gün) · `kayit-kuyrugu.json` — yayın kaydı kuyruğu (15 dk'da bir gider)
- `deneme-arsiv\` — Faz 0 dayanıklılık deneyinin yazdığı sahte arşiv (her deneyde silinip baştan yazılır)

Program kaldırılınca bu klasör de silinir (kurulum betiği `[UninstallDelete]`); güncelleme ona dokunmaz.

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
