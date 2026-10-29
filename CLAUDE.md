# Kurumsal Müzik Player 1.0 (offline, C#)

Mağazaların internet olmasa da çalan Windows programı. Kurumsal Müzik Net'e (kurumsalmuzik.net) bağlıdır: aynı arşiv
(R2), aynı sunucu, aynı hesaplar ve panel. Electron ile yazılan 2.x (`../VM Offline Radio Player/`, BIRAKILDI) yerine
C# + mpv ile baştan yazılır.

## Önce oku
- `docs/BRIEF.md` — **tek doğru kaynak.** Kararlar, mimari, fazlar. Bir şey çelişirse BRIEF kazanır.
- `../VM Offline Radio Player/docs/BRIEF.md` — **işlev listesi** (1.0 BRIEF'i aksini söylemedikçe oradaki kurallar geçerli).
  Oradaki kod (`src/main/*.ts`) sunucuyla konuşmanın ve akış hesabının çalışan örneğidir; mantık oradan çevrilir.
- `docs/KURULUM.md` (derleme, deneme, yayın), `docs/TEST-LISTESI.md` (Onur'un test rehberi, dağıtım kararları).
  Sunucu ve mağaza kurulumu dahil bütün sistem: `../kurumsalmuzik/docs/KURULUM-KILAVUZU.html` §12.
- Web sistemi: `../kurumsalmuzik/` (kendi `CLAUDE.md` ve `docs/BRIEF.md`'si var). Akış ve anons kuralları
  (`src/lib/announce.ts`) oradan birebir çevrilir; burada yeniden icat edilmez.

## Proje sahibi
Onur Özden. Viva Medya'yı tek başına işletiyor; tam zamanlı yazılımcı değil.
- Türkçe konuş. Kısa, doğrudan, net. Teknik terimi gerekiyorsa bir cümleyle açıkla.
- Her adımın sonunda ne yaptığını ve Onur'un neyi nasıl deneyebileceğini söyle.
- Kararlarını gerekçelendir; ama BRIEF'teki açık kararları **sen verme, sor.**

## Çalışma kuralları
- **Antivirüs/fidye korumasıyla uyum birinci gereksinim** (BRIEF §1): bilgisayarda toplu şifreleme yok, binlerce küçük
  dosya yok, kendini açan/paketlenmiş exe yok, gizli süreç numarası yok. Şüpheli görünebilecek her yeni davranış önce
  Onur'un Kaspersky + Acronis'li bilgisayarında denenir.
- **Hiçbir iş motorun ya da pencerenin ana iş parçacığını bekletmez** (disk, ağ, mpv IPC hepsi arka planda).
- Fazlara sadık kal (BRIEF §8). Sıradaki faza geçmeden önceki fazın kanıt senaryosunu birlikte çalıştırın.
- **Canlı sistem dondurması:** web projesi, sunucu, veritabanı, R2, Worker, Cloudflare — Onur açıkça onay vermeden
  değişmez (BRIEF §7'deki ekler dahil, her biri ayrı onayla).
- Eski offline sunucuya (72.61.189.234) ve `radyo.vivamedya.com`'a dokunulmaz.
- Gizli bilgiler (`.env`, anahtarlar, şifreler) asla commit'lenmez, sohbete yazdırılmaz.
- Saat ve süre kararları her zaman `Europe/Istanbul`.
- Arayüz metinleri Türkçe; kod, değişken ve dosya adları İngilizce.
- Küçük, anlamlı commit'ler; commit sonrası `git push` (GitHub: vivamedyatr/kurumsalmuzikplayer, Private).

## Derleme
- .NET 8 SDK (makinede kurulu), hedef `net48` (SDK tarzı proje). `dotnet build` / `dotnet test`.
- PowerShell'de her komutta PATH'i kayıt defterinden tazele (`$env:Path = [Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [Environment]::GetEnvironmentVariable("Path","User")`).

## Yayın
- **1.0.3 yayında** (29 Eki 2026, yapı 103; ilk final 1.0.0 yapı 100). Onur farklı Windows ortamlarında test ediyor; bulduğu hata yeni
  sürümle düzeltilir (1.0.1, 1.0.2, 1.0.3…). Her yayın: `Directory.Build.props` (sürüm + FileVersion'ın son parçası bir artar)
  → `scripts/kurulum.ps1` → `bash scripts/yayinla.sh` (**her yayın Onur onayıyla**) → commit, `git tag v1.0.x`, push.
- Yayın betiği sunucuya Tailscale ile `onur@kurumsalmuzik-vps.tail8660b4.ts.net` üzerinden bağlanır; sitedeki sabit
  indirme adresini de günceller. 2.x'in `/indir/latest.yml`'ine dokunulmaz.

## Durum
BRIEF §10'a bak; açık kararlar ve sonraki sürüm adayları BRIEF §9'da.
