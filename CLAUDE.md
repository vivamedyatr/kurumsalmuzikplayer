# Kurumsal Müzik Player 1.0 (offline, C#)

Mağazaların internet olmasa da çalan Windows programı. Kurumsal Müzik Net'e (kurumsalmuzik.net) bağlıdır: aynı arşiv
(R2), aynı sunucu, aynı hesaplar ve panel. Electron ile yazılan 2.x (`../VM Offline Radio Player/`, BIRAKILDI) yerine
C# + mpv ile baştan yazılır.

## Önce oku
- `docs/BRIEF.md` — **tek doğru kaynak.** Kararlar, mimari, fazlar. Bir şey çelişirse BRIEF kazanır.
- `../VM Offline Radio Player/docs/BRIEF.md` — **işlev listesi** (1.0 BRIEF'i aksini söylemedikçe oradaki kurallar geçerli).
  Oradaki kod (`src/main/*.ts`) sunucuyla konuşmanın ve akış hesabının çalışan örneğidir; mantık oradan çevrilir.
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

## Durum
BRIEF §10'a bak.
