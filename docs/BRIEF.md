# Kurumsal Müzik Player 1.0 — BRIEF

Tek doğru kaynak. Kararlar tarihleriyle yazılır; değişen karar silinmez, üstü çizilip yenisi eklenir.

> **⚠ CANLI SİSTEM — DEĞİŞİKLİK DONDURMASI (karar Onur, 16 Eki 2026).** Kurumsal Müzik Net (sunucu, veritabanı, R2,
> Cloudflare/Worker, yayınlama) Onur açıkça onay vermeden değiştirilmez. Bu klasördeki programın kodu serbestçe
> yazılır; ama web projesine, Worker'a ya da R2'ye dokunan her adım (§7) önce Onur'a yazılır, onay beklenir. Onay o işe
> özeldir, sonrakine geçmez.

---

## 1. Ne ve neden

Mağazalarda internet olmasa da çalan Windows programı. Kurumsal Müzik Net'e (kurumsalmuzik.net) bağlıdır: aynı arşiv,
aynı sunucu, aynı hesaplar, akışlar, anonslar ve panel. Eski Java programın (VM Offline Radio Player, sunucusu 2026 sonunda
kapanıyor) yerini alır. "Arka plan / fon müziği" denmez.

**Neden baştan (karar Onur, 23 Eki 2026):** önceki deneme (Electron, `../VM Offline Radio Player/`, sürüm 2.0.13–2.0.17)
Onur'un yeni bilgisayarında (Windows 10 19045 + Kaspersky 21.27 + Acronis Active Protection) açılıştan 0–7 dk sonra
donuyordu: Windows günlüğünde olay 1002 "AppHang", donan programın ana süreci. Aynı sürümler eski bilgisayarlarda
donmuyordu. En güçlü olasılık: davranış tabanlı fidye korumasının, imzasız bir programın binlerce dosyayı tek tek
şifreleyip yazmasına (3.204 `.vmp` dosyası) tepki vermesi. Program Windows 7–11'de, her çeşit antivirüsle çalışmak
zorunda; bu yüzden 1.0'da **antivirüs/fidye korumasıyla uyum birinci gereksinimdir.**

Eski projenin `docs/BRIEF.md`'si **işlev listesi** olarak kullanılır (orada yazan her kural burada da geçerli, aksi
yazılmadıkça). Sunucu tarafı (`/api/offline/*`, offline paket, akış kuralları) hazır ve canlıda; değişmez.

## 2. Kararlar

### 2.1 1.0'a özgü (Onur, 23 Eki 2026)

| # | Konu | Karar |
|---|---|---|
| 1 | Dil ve motor | **C#** + **mpv** ses motoru. Chromium/Electron yok. |
| 2 | Arşiv koruması | ~~Seçenek A — sunucu şifreler~~ → **player bellekte şifreler, bilgisayara özel anahtar** (24 Eki 2026, §5). Albüm başına tek dosya; şifre bellekte çözülür. |
| 3 | Kod imzalama | **Şimdilik yok** (imzasız). Kurulumda "bilinmeyen yayıncı" uyarısı çıkar. Antivirüs sorunu çıkarsa yeniden değerlendirilir. |
| 4 | Geçiş | **Elle kurulum.** Müşteriler henüz Kurumsal Müzik Net'e taşınmadı; 2.x'ten otomatik geçiş yapılmaz. |
| 5 | Ad | Her yerde **"Kurumsal Müzik Player"**. ~~Sürüm sadece pencerenin sağ altında; programda vivamedya.com bağlantısı.~~ → 1.0.0'da (yeni arayüz, 25 Eki 2026): sürüm sol menünün altında ("Sürüm 1.0.0") ve giriş ekranında; marka "MÜZİK · BY VIVA MEDYA"; giriş ekranında "Viva Medya · vivamedya.com" **yazısı** var ama tıklanır bağlantı yok (sonraki sürüm adayı, §9). |

### 2.1a 1.0.1 (karar Onur, 25 Eki 2026)

| # | Konu | Karar |
|---|---|---|
| 6 | Hesaptan çıkış | Sol rayda **"Çıkış yap"** (onay penceresiyle). Şube kaydı bu bilgisayardan silinir, müzik durur, giriş ekranı gelir. Sunucudaki cihaz kaydı durur (aynı şube adıyla girilince devralınır, hak harcanmaz); inen arşiv kalır. Bekleyen yayın kaydı çıkıştan önce gönderilir, gönderilemeyen silinir. |
| 7 | Duraklat | Alt çubuktaki kırmızı düğme **duraklat / devam**. Müzik aynı şarkının aynı saniyesinde bekler; devam edince oradan sürer (bu arada dilim değiştiyse yeni tarza geçer). Duraklatılmışken anons çalmaz, "Sonraki" kapalı, yayın kaydına "durdu" yazılır. Program yeniden açılınca çalar (duraklatma kalıcı değil). Anons sırasında duraklatılamaz. |
| 8 | Destek | Web'de **`kurumsalmuzik.net/destek`** (WhatsApp +90 212 807 05 87, info@vivamedya.com). Programda sol rayda, giriş ekranında ve "Yayın durdu" ekranında "Destek ↗". |
| 9 | Ad | "Bugünkü akış" → **"Yayın akışı"** (her gün aynı akış çalar; "bugünkü" yarın başka akış varmış gibi anlaşılıyordu). |

### 2.1b 1.0.2 (karar Onur, 26 Eki 2026)

| # | Konu | Karar |
|---|---|---|
| 10 | Logo | Sol üstte, giriş ve "Yayın durdu" ekranlarında resmî logo: **KURUMSAL / MÜZİK NET** ("NET" kırmızı; web `.s-logo`), altında "BY VİVA MEDYA"; **VİVA MEDYA** vivamedya.com'u açar. |
| 11 | Pencere tepsideyken | Pencere kapatılınca süreç kapanmaz, **gizlenir**; tepsiden/kısayoldan açınca beklemeden gelir (ölçüm: 0,1 sn). Windows açılışında pencere çizilmeden arka planda hazır bekler. Gizliyken ekran güncellenmez, açılınca son durum çizilir. Bellek (özel): motor ~60 MB, pencere ~55 MB, iki mpv ~65 MB. |
| 12 | Bozuk şarkı | Yeniden indirilmez (karar Onur): şarkı 30 sn ilerlemezse bekçi sonrakine geçer. |
| 13 | Ses çıkışı yok | Hoparlör/ses aygıtı yoksa (çıkarıldı, kapalı uzak masaüstü oturumu) mpv her şarkıyı anında bitiriyordu: 1.0.0–1.0.1'de günlüğe dakikada ~800 satır ve panele sahte "çaldı" kaydı (26 Eki'de Onur'un bilgisayarında görüldü). 1.0.2: art arda 3 hatada çalma durur, ekranda "SES ÇIKIŞI YOK", 30 sn'de bir yeniden denenir; "çaldı" kaydı ancak şarkı 1,5 sn çaldıktan sonra yazılır. |

### 2.1c 1.0.3 (29 Eki 2026)

| # | Konu | Karar |
|---|---|---|
| 14 | Pencere geri gelmiyor | Onur'un zayıf notebook'unda (Celeron, 2 çekirdek, 4 GB, Windows 10 1903) 15 dk simge durumunda kalan pencere geri getirilince ekrana gelmedi; günlüğe göre pencere süreci yanıt veriyordu (donma, yeniden açma yok). 1.0.3: pencere simge durumundan ya da tepsiden her geri gelişte başlık çubuğu ekranda değilse ortaya alınır ve tamamı yeniden çizdirilir; pencere durumu (simge/normal/gizli/öne getirme), Windows oturum kilidi ve ekran ayarı değişimi günlüğe yazılır. Notebook'ta denendi (29 Eki): 30+ kez simge durumundan ve tepsiden geri geldi, en uzun bekleme ~4 dk. Exe özelliklerindeki ürün adı ve telif satırındaki bozuk karakterler düzeltildi. |

### 2.2 Eski BRIEF'ten aynen geçen kararlar (özet; ayrıntı orada)

- **Platform:** Windows 7 SP1, 8, 8.1, 10, 11 — 64 bit. Tek program, tek kurulum dosyası, kullanıcı bazlı kurulum (yönetici izni istemez).
- **Arayüz:** ses aç/kıs, sonraki şarkı (10 sn'de bir, anons sırasında kapalı), çalma geçmişi, "Aylık yeni müzik" anahtarı. ~~Durdur yok~~ → 1.0.1'de duraklat/devam var (§2.1a #7); akış programda düzenlenmez ("Akışı düzenle" tarayıcıda açılır).
- **Akış:** sadece tarz akışı; hesabın ortak "Offline akışı"; 7 günde bir değişir (sunucuda).
- **Paket:** akış başına 6 GB, tarz başına en fazla 16 saat, albüm başına ilk 40 şarkı, 30 günde en fazla 5 albüm değişir (sunucuda).
- **İndirme:** en fazla 6 Mbps; önce çalan tarz, sonra sıradaki, sonra diğerleri; kesilip devam eder; inerken çalar (tarz en az 3 şarkısı inince).
- **İnternetsiz:** 7 gün. Sonra "7 gündür internete bağlanılamadı…" der ve durur. Lisans günde bir, akış saatte bir, anonslar 15 dk'da bir sorulur.
- **Anonslar:** web'deki planlar aynen (P1–P6, PA/PB/PC, saatli **X/Y**); müzik anons sırasında duraklar, kaldığı saniyeden sürer. Anonslar şifresiz.
- **Tek bilgisayar:** aynı şube adıyla başka bilgisayardan girilirse eskisi en geç 15 dk içinde girişe döner.
- **Yayın kaydı:** çalan şarkı, anons, açıldı/durdu; 15 dk'da bir `/api/offline/log`'a; internetsizken 7 güne kadar birikir.
- **Güncelleme:** program kendini günceller; yeni sürüm arkada iner, **sadece program yeniden açılınca** kurulur, çalarken asla.
- **Görünüm (karar Onur, 24 Eki 2026; 1.0.0-faz5.1):** web yayın ekranının dili, Spotify / YouTube Music düzeni
  (`docs/tasarim/`): solda menü (Çalıyor, Bugünkü akış, Anonslar, Geçmiş, Arşiv) ve mağaza bilgisi; ortada albüm
  kapağından yayılan renk üstünde büyük kapak ve Archivo Expanded tarz başlığı, bugünkü akış şeridi, sıradaki anons ve son
  çalanlar; altta çalma çubuğu (durdurma yok, "Sonraki", CANLI, ses). Yazı tipleri programın içinde (Archivo, IBM Plex
  Mono; OFL). **Pencere oranı sabit** (Onur: "tasarım bozulmasın"): içerik 1040×610 tasarım boyutunda tek parça
  ölçeklenir, %72–%160 arası ve ekrana sığacak kadar; tam ekran düğmesi kapalı; Windows 10/11'de koyu başlık çubuğu.
  Webp kapak açılamazsa (Windows 7) tarzın renkli kapağı görünür.
- ~~**Görünüm:** web yayın ekranının dili (2.0.15 tasarımı): koyu zemin, Archivo başlık, plak kapağı, Viva kırmızısı, IBM Plex Mono saatler, sekmeler (Şarkılar, Anonslar, Akış, Arşiv), altta gün bandı. 760×460, en az 640×400. Yazı tipleri programın içinde.~~

## 3. Teknik yığın

| Katman | Seçim | Neden |
|---|---|---|
| Çalışma ortamı | **.NET Framework 4.8** | Windows 10 (1903+) ve 11'de hazır gelir. ~~Windows 7 SP1/8.1'e kurulum dosyası ekler (çevrimdışı yükleyici pakette).~~ → Yoksa kurulum durur ve Microsoft'un indirme sayfasını açar (23 Eki 2026: .NET kurulumu yönetici izni ister, programın kurulumu istemez; 115 MB'lık yükleyiciyi her şubeye taşımamak için). Ek çalışma zamanı taşınmaz, program küçük kalır. |
| Arayüz | **WPF** | Yerel Windows penceresi; tarayıcı motoru yok. Koyu tema, özel yazı tipleri, kapak görselleri rahat. Yazılımla çizim (eski ekran kartlarında takılmasın diye). |
| Ses | **mpv.exe** (0.35, AVX2'siz x86_64) | 2.x'te Celeron/Win 8.1'de denendi. Ayrı süreç, JSON IPC (adlandırılmış boru). Müzik ve anons için iki mpv. |
| Ağ | `HttpClient` (TLS 1.2 açıkça) | Program TLS 1.2'yi açılışta kendisi açar. ~~Windows 7'de TLS 1.2 kayıt defteri/güncelleme ihtiyacı kurulumda kontrol edilir.~~ → Kurulumda ayrı bir kontrol **yapılmadı** (1.0.0); Windows 7 SP1'de bağlantı Onur'un testinde doğrulanır (`docs/TEST-LISTESI.md`), sorun çıkarsa sonraki sürümde eklenir. |
| Gizli anahtar | Windows DPAPI (`ProtectedData`, kullanıcı kapsamı) | Cihaz anahtarı ve oturum. |
| Kurulum | **Inno Setup** | Tek .exe (26 MB), kullanıcı bazlı (`%LOCALAPPDATA%\Programs\Kurumsal Müzik Player`), yönetici izni istemez, Türkçe. Kaldırınca inen arşiv ve şube kaydı da silinir. |
| Geliştirme | .NET 8 SDK (SDK tarzı proje, `net48` hedefi) | Visual Studio şart değil; `dotnet build` ile derlenir. |

**Neden C# ve .NET Framework 4.8:** Windows 7'yi resmen destekleyen son çatı; antivirüslerin en iyi tanıdığı program
türü; "tek dosya" .NET 8 paketleri (kendini açan exe) antivirüslerde daha sık yanlış alarm veriyor.

## 4. Mimari — donmaya dayanıklı

Üç süreç; biri takılırsa diğerleri sürer:

```
KurumsalMuzikPlayer.exe  (motor; görünmez, tepside)
 ├─ mpv.exe  müzik      (JSON IPC)
 ├─ mpv.exe  anons      (JSON IPC)
 └─ pencere  (aynı exe, --ui) ── yerel boru ── motor
```

- **Motor** (pencere yok): akış hesabı, indirme, arşiv, anons zamanlayıcı, sunucu, yayın kaydı. Tepsi simgesi motorda.
  Her iş kendi iş parçacığında; disk ve ağ işi asla motorun ana döngüsünü beklemez.
- **Pencere** ayrı süreç: sadece gösterir ve ses/sonraki komutlarını iletir. Kapatılınca kapanır (motor ve müzik sürer);
  tepsiden ya da kısayoldan yeniden açılır. Pencere donarsa motor fark eder, öldürür ve yeniden açar; müzik etkilenmez.
- **Bekçi:** motor mpv'yi izler (çökerse kaldığı şarkıdan yeniden başlatır); mpv'ler motora bağlıdır (motor ölürse
  onlar da kapanır, sahipsiz ses kalmaz). Pencere açıksa motorun bekçisidir: motor 10 sn yoksa yeniden başlatır.
  ~~Motorun kendisi Windows Görev Zamanlayıcı'daki "oturum açılınca başlat + çökünce yeniden başlat" göreviyle ayakta
  tutulur.~~ → Oturum açılınca başlama: Kayıt Defteri Run anahtarı (HKCU; kurulum yazar, kaldırma siler). Çökünce
  yeniden başlama: Windows Hata Bildirimi (`RegisterApplicationRestart`). Görev Zamanlayıcı'ya görev yazmak antivirüslerin
  "kalıcılık" diye işaretlediği bir davranış (23 Eki 2026, BRIEF §1 birinci gereksinim).
- **Günlük:** `%LOCALAPPDATA%\Kurumsal Muzik Player\gunluk\` — dönen (en fazla 7 gün) metin günlüğü; donma, çökme,
  indirme hatası gerçek sebebiyle yazılır (`motor-<gün>.txt`, `pencere-<gün>.txt`). ~~Uzaktan destekte "günlüğü gönder"
  (sunucuya, onayla).~~ → 1.0.0'da yok: mağazadan tepsi simgesi → "Günlük klasörünü aç" ile dosyalar istenir (sonraki
  sürüm adayı, §9). Sunucu yayın kaydından (panel → Yayın kayıtları) programın ne zaman çaldığı uzaktan görülür.
- **Veri klasörü:** `%LOCALAPPDATA%\Kurumsal Muzik Player\` (eski programla karışmaz).

## 5. Arşiv koruması — ~~Seçenek A (23 Eki 2026)~~ → player şifreler, bilgisayara özel anahtar (karar Onur, 24 Eki 2026)

**Sorun:** 2.x her `.ts` parçasını indirip bilgisayarda AES ile şifreliyor ve tek tek dosya olarak yazıyordu (binlerce
yeni, rastgele görünen dosya). Fidye yazılımları tam olarak bunu yapar; davranış korumaları bu deseni yakalar.

**Çözüm:**
1. **Albüm başına tek dosya** (asıl düzeltme). Bir albümün bütün şarkı parçaları tek bir kap dosyasına (`<albüm>.kmp`)
   art arda eklenir. ~3.200 dosya yerine akış başına ~50–150 dosya; `.part` → yeniden adlandırma yok.
2. **Player şifreler, bellekte.** Parça Worker'ın `/m/` adresinden (web ile aynı) iner, bellekte AES-256-CTR ile
   şifrelenip kaba eklenir; açık hâli diske hiç yazılmaz. ~~Sunucu (Worker) şifreler, herkes için tek anahtar, `/e/`
   adresi (23 Eki 2026)~~ — 24 Eki'de bırakıldı: kaplarla birlikte diskte görünen davranış iki yöntemde de aynı
   (birkaç büyük dosyaya rastgele görünen veri ekleniyor; antivirüs şifrenin nerede yapıldığını göremez), Worker'a
   iş ve bağımlılık ekliyordu. Bilgisayara yükü 1 MB'lık parça başına birkaç ms.
3. **Şifre bellekte çözülür**, mpv'ye programın içindeki yerel adres üzerinden verilir (diske açık yazılmaz).
4. **Anahtar bilgisayara özel:** ilk açılışta rastgele üretilir, DPAPI ile `arsiv-anahtari.bin`'de saklanır. Klasör
   başka bilgisayara ya da Windows kullanıcısına kopyalansa açılmaz. Anahtar sürümü `c1` (`arsiv\c1\`); başka
   anahtarla yazılmış eski klasörler açılışta silinir. Program yeniden kurulursa (kaldırma veri klasörünü siler) arşiv baştan iner.

**Worker yükü (24 Eki 2026, canlı ölçüm):** `/m/` isteği başına ~4 ms işlemci (ücretsiz plan sınırı 10 ms); Worker'ın
şifreli `/e/` adresi istek başına ~12 ms (ilk istek) tutuyordu. 1.000 şubede asıl sınır istek sayısı: ilk kurulum
şube başına ~6.000 istek (6 GB / ~1 MB), sonra ayda ~800. Ücretsiz plan günde 100.000 istek → şubelere yaymadan
önce Workers Paid (aylık 5 $, 10 milyon istek dahil) gerekir (karar Onur'da).

**Sunucu tarafı:** web `7ce81e9` (Worker `84590864`) ile canlıya giden `/e/` adresi ve manifestteki `media.archive`
artık kullanılmıyor; **sonraki web yayınında kaldırılacak** (karar Onur 24 Eki 2026). `X-App: kmp` kalır (X/Y anonsları).

**Çalma:** program içinde sadece bu bilgisayardan erişilen (127.0.0.1, rastgele oturum adresli) küçük bir adres her
şarkıyı mpv'ye HLS listesi olarak verir; parçalar istendiği anda kaptan okunup bellekte çözülür.

## 6. Sunucu API'si (hazır, değişmez)

Hepsi `https://kurumsalmuzik.net`, `Authorization: Bearer <cihaz anahtarı>`, `X-App-Version: 1.0.x` ve `X-App: kmp`
başlıklarıyla (sonuncusu §7.1):

| Yol | İş |
|---|---|
| `POST /api/offline/register` | Kullanıcı adı + şifre + şube adı → cihaz anahtarı (aynı şube adı eski bilgisayarı düşürür) |
| `GET /api/offline/status` | Lisans, hesap süresi, cihaz geçerli mi (günde bir; başarısızsa 15 dk'da bir yeniden) |
| `POST /api/offline/manifest` | Akış dilimleri, tarzlar, albümler, şarkılar, imzalı medya adresi ve süresi, paket döngüsü (saatte bir; gövdede eldeki albümler ve "Aylık yeni müzik") |
| `GET /api/offline/announcements` | Anons planları ve dosyaları (saatli X/Y dahil); 15 dk'da bir. **Düşme** (cihaz silindi / başka bilgisayardan girildi) bu sorgunun ya da paketin 401 cevabından anlaşılır → giriş ekranı |
| `POST /api/offline/announcements/played` | Anons çalma bildirimleri (7 güne kadar geriye) |
| `POST /api/offline/log` | Yayın kaydı (en fazla 5.000 olay/istek) |

Medya: `media.kurumsalmuzik.net/m/<şarkı anahtarı>/<NNNN>.ts?t=<imza>` (Worker). Kapaklar ve anons MP3'leri aynı Worker'dan.
Ayrıntılı alanlar eski projede: `src/main/library.ts` (Manifest tipi), `src/main/api.ts`, `src/main/announcer.ts`.

Akış ve anons kuralları web'den **birebir** C#'a çevrilir ve web'in testleriyle aynı örneklerle sınanır:
`kurumsalmuzik/src/lib/announce.ts` (+ `announce.test.ts`), tarz akışı/karışık liste mantığı (eski projede `src/main/engine.ts`, `player.ts`).

## 7. Kurumsal Müzik Net'e gereken ekler (her biri ayrı Onur onayıyla)

1. **Program kimliği:** sunucu bugün `X-App-Version < 2.0.17` olan programa saatli X/Y anonslarını göndermiyor. 1.0
   ayrı bir başlıkla kendini tanıtır (`X-App: kmp`); sunucu bu başlığı görünce sürüm karşılaştırmasını atlar.
   **Yapıldı** (web `7ce81e9`, canlı 23 Eki 2026). ~~Panelde cihaz "Offline · KMP 1.0.3" diye görünür.~~ → Panelde ayrı
   bir "KMP" etiketi yapılmadı; cihaz 2.x gibi "Offline" + sürüm numarasıyla (1.0.x) görünür, 2.x'ten sürümle ayrılır.
2. ~~**Worker "şifreli ver" kipi** (§5) ve manifestte paket anahtarı.~~ Yapıldı ve canlıya gitti (23 Eki), 24 Eki'de bırakıldı
   (şifreleme player'da); `/e/` ve `media.archive` sonraki web yayınında kaldırılır.
3. **Güncelleme dosyası:** `kurumsalmuzik.net/indir/kmp/` altında sürüm bilgisi (`latest.json`: sürüm, yapı numarası,
   dosya adı, SHA-256, boyut) ve kurulum dosyası; `scripts/yayinla.sh` yükler (sunucuda `/var/www/indir/kmp`, nginx'te
   `/indir/` zaten açık — web projesinde değişiklik gerekmez). İndirme adresi `/indir/Kurumsal-Muzik-Player-Kurulum.exe` 1.0 yayına girince 1.0'ı gösterir (o güne kadar 2.0.17).
   **Yapıldı:** ilk yayın 24 Eki 2026; 25 Eki 2026'dan beri sabit adres 1.0.0'ı veriyor ve `yayinla.sh` her yeni 1.0.x'te
   onu da günceller. 2.x'in `/indir/latest.yml`'i ve `VM-Radio-Player-2.0-Kurulum.exe` 2.0.17'de kaldı.

## 8. Fazlar

Her fazın sonunda kanıt senaryosu Onur'la birlikte çalıştırılır. Acele yok; her faz eski bilgisayarlarda ve Onur'un
Kaspersky + Acronis'li bilgisayarında denenir.

**Faz 0 — İskelet ve kanıt deneyi.** Proje yapısı, derleme, boş pencere + tepsi + motor + mpv ile yerel bir dosya çalma.
Ayrıca **dayanıklılık deneyi:** sahte 6 GB'lık arşiv yazan bir deneme aracı (albüm başına tek dosya) Kaspersky + Acronis
açıkken 2 saat çalışır.
*Kanıt: Onur'un bilgisayarında 24 saat donmadan çalar; Windows günlüğünde 1002 yok; antivirüs uyarısı yok.*

**Faz 1 — Giriş ve cihaz.** Kurulum dosyası (Inno Setup), giriş ekranı, `/register`, `/status`, tek kopya, oturum açılınca başlama, düşme.
*Kanıt: Windows 7 ve 11'de kur → şube girişi → panelde "Offline · KMP" cihaz → aynı şube adıyla ikinci bilgisayar girince ilki girişe döner.*

**Faz 2 — İndirme ve arşiv.** Manifest, öncelikli, 6 Mbps sınırlı, kesilip devam eden indirme; albüm kapları; player'da bellekte şifreleme.
*Kanıt: 3 Mbps hatta paket iner → internet kesilip gelince devam eder → program hiç donmaz → klasör başka bilgisayara kopyalanınca çalmaz.*

**Faz 3 — Çalma, anonslar, internetsiz 7 gün.** Tarz akışı, gün bandı, ses, sonraki, geçmiş, anonslar (X/Y dahil), lisans, yayın kaydı.
*Kanıt: interneti kes → akış saatinde tarz değişir, anons zamanında çalar → saat 8 gün ileri alınınca "7 gündür internete bağlanılamadı" → internet gelince devam.*

**Faz 4 — Akış değişimi, paket döngüsü, kendini güncelleme.**
*Kanıt: web'den akış değiştir → program çalmayı kesmeden yeni tarzı indirip geçer → yeni sürüm yayınlanınca program yeniden açılışta güncellenir.*

**Faz 5 — ~~Mağazada deneme~~ → final sürüm ve dağıtım** (karar Onur, 25 Eki 2026): mağazada deneme yapılmaz; final
**1.0.0** yayınlanır, Onur farklı Windows ortamlarında kendisi test eder (`docs/TEST-LISTESI.md`), hata bulunursa yeni sürümle
düzeltilir. ~~Gerçek bir mağazada bir hafta~~; indirme adresinin 1.0'a çevrilmesi.

## 9. Açık sorular ve sonraki sürüm adayları (25 Eki 2026)

**Kararı Onur'da olanlar** (ayrıntı `docs/TEST-LISTESI.md` §6):
1. **Cloudflare Workers Paid** (aylık 5 $): aynı gün ~15'ten fazla şube kurulacaksa önce geçilmeli (§5).
2. **Web temizliği:** Worker'daki `/e/` ve manifestteki `media.archive` sonraki web yayınında kaldırılır (onay bekliyor).
3. **2.x'in güncelleme dosyası** (`/indir/latest.yml`) ve eski adlı `VM-Radio-Player-2.0-Kurulum.exe`: 2.0.17'de mi
   kalsın, yoksa son bir 2.x sürümüyle 1.0'a mı yönlendirilsin.
4. **Eski Java programın müşterileri** (sunucusu yıl sonunda kapanıyor): Kurumsal Müzik Net hesabı + 1.0 ile geçiş planı.
5. **Kod imzalama:** antivirüs/SmartScreen sorunu çıkarsa yeniden değerlendirilir (§2.1 #3).

**Programda olmayanlar — sonraki sürüm adayları** (Onur'un testlerine göre sıralanır; her biri ayrı onayla):
- ~~Giriş ekranında ya da menüde tıklanır vivamedya.com bağlantısı (§2.1 #5).~~ → 1.0.1'de yerine "Destek ↗" (kurumsalmuzik.net/destek) geldi.
- ~~Sessize alma düğmesi.~~ → 1.0.1'de duraklat/devam (§2.1a #7).
- Windows 7'de TLS 1.2 / kök sertifika eksikse kurulumda ya da girişte açık bir uyarı (§3).
- "Günlüğü gönder" (uzaktan destek için günlüğü sunucuya yükleme, §4).
- Süre dolunca ödeme adresini (web'deki yenileme sayfası, web BRIEF §13.5) tıklanır "Hizmet ödemesi yap" düğmesi
  olarak göstermek; bugün adres yazı olarak görünür.

**Onur'un testlerinde açık olanlar (28 Eki 2026):**
- Windows 7'de sorunsuz çalışıyor (Onur'un testi, 26 Eki 2026); testler sürüyor.
- Arka planda başka işler yapılırken nadir **cızırtı**: Onur aynı bilgisayarda YouTube/Spotify ile karşılaştırıp saatleri
  not ediyor; sonuca göre karar verilecek (kod değişikliği yok).
- **32 bit:** program 64 bit kalıyor; Onur müşteri bilgisayarlarının sistem türüne bakıp 32 bit gerekip gerekmediğini söyleyecek.
- Bozuk/okunamayan şarkı yeniden indirilmez, atlanır (karar Onur, 26 Eki 2026; 1.0.2'deki bekçi yeterli).

**Bilinen sınırlar:**
- Saat geri alınırsa: 7 gün kuralı son başarılı bağlantı ve sunucudan öğrenilen saat farkıyla işler; internet yokken
  bilgisayar saati geri alınarak süre uzatılabilir (2.x'te de aynıydı). Bağlantı gelince düzelir.
- Windows 7 webp kapak açamaz; tarzın renk geçişi görünür.
- Program kaldırılınca veri klasörü de silinir (§3 Kurulum): yeniden kurulumda arşiv baştan iner.

## 10. Durum

Kanıt senaryoları mağaza denemesi yerine Onur'un kendi testlerinde (karar 25 Eki 2026; `docs/TEST-LISTESI.md`).

- [x] Faz 0 — İskelet ve kanıt deneyi (iskelet hazır, 23 Eki 2026: motor/pencere/mpv ayrı süreç, bekçiler denendi — pencere donunca 15 sn'de yeniden açılır, mpv çökünce 2 sn'de kaldığı yerden sürer, motor çökünce mpv de kapanır ve pencere motoru ~10 sn'de yeniden başlatır; ilk dayanıklılık denemesi Onur'un Kaspersky + Acronis'li bilgisayarında 9 dk, 0,41 GB albüm kabı yazımı: donma yok, Windows 1000/1002 kaydı yok, antivirüs uyarısı yok — 2.x bu bilgisayarda 0–7 dk içinde donuyordu; 24 saatlik kanıt bekliyor)
- [x] Faz 1 — Giriş ve cihaz (yapıldı, 23 Eki 2026: kurulum dosyası, giriş ekranı, cihaz kaydı DPAPI ile, 15 dk'da bir durum sorgusu, düşme, süre dolması, Windows'la başlama; yerel sunucuyla uçtan uca ve kur/güncelle/kaldır denendi. Onur'un Windows 7 ve 11 kanıtı bekliyor)
- [x] Faz 2 — İndirme ve arşiv (program tarafı yapıldı ve yerel sunucu + yerel Worker ile denendi, 23 Eki 2026: şifreli indirme, albüm kapları, yarıda kesilip devam, arşivden çalma. 24 Eki 2026: şifreleme player'a taşındı (1.0.0-faz2.2), canlı sunucuyla Onur'un bilgisayarında indiriyor ve arşivden çalıyor; kanıt senaryosu bekliyor)
- [x] Faz 3 — Çalma, anonslar, internetsiz 7 gün (yapıldı, 24 Eki 2026, 1.0.0-faz3: saat dilimine göre tarz, inmemiş tarz yerine inmiş olan, anons kuşağı ikinci mpv'de ve müzik kaldığı saniyeden sürer, anons kuralları web'le birebir ve aynı testlerle, 7 gün kuralı, geçmiş, yayın kaydı; pencerede kapak, gün bandı, Geçmiş ve Anonslar sekmeleri. Canlıda Onur'un bilgisayarında çalışıyor; kanıt senaryosu bekliyor)
- [x] Faz 4 — Akış değişimi, paket döngüsü, güncelleme (program tarafı yapıldı, 24 Eki 2026, 1.0.0-faz4.1: "Akışı düzenle" web'i açar ve paketi 2/5/10/20 dk sonra erken sorar; "Aylık yeni müzik" anahtarı; eski tarzlar yenisi inene kadar çalar, pakette olmayan albümler paket tamamen inince silinir; kendini güncelleme yerel adresle denendi: arkada iner, SHA-256 doğrulanır, yeniden açılışta ~10 sn'de kurulur ve program kendiliğinden açılır. İlk canlı yayın 24 Eki 2026: 1.0.0-faz4.1 (yapı 41), 25 Eki 2026: 1.0.0-faz5.1 (yapı 51, yeni arayüz) `/indir/kmp/`'de, SHA-256 doğrulandı; 2.x'in latest.yml'i ve sabit indirme adresi değişmedi. Kanıt senaryosu bekliyor)
- [x] Faz 5 — Final sürüm 1.0.0 (25 Eki 2026, yapı 100, `/indir/kmp/`'de; sitedeki sabit indirme adresi de 1.0.0'ı veriyor; GitHub etiketi `v1.0.0`). Mağazada deneme yok (karar Onur); testler Onur'da, `docs/TEST-LISTESI.md`. Açık kararlar ve sonraki sürüm adayları §9'da.

- [x] **1.0.1** (25 Eki 2026, yapı 101; Onur'un ilk istekleri): hesaptan çıkış, duraklat/devam, Destek bağlantısı, "Yayın akışı" adı (§2.1a). Derlendi, 21 test geçti; Onur'un bilgisayarına kuruldu, duraklat/devam (aynı saniyeden sürdü), çıkış onayı (Vazgeç) ve Yayın akışı denendi; gerçek çıkış Onur'da. Web `/destek` canlıda (49aa1d8), **1.0.1 yayında** (`/indir/kmp/`, sabit adres de 1.0.1; GitHub etiketi `v1.0.1`). Ayrıca: gece yarısından sonra Yayın akışı sayfasında son dilim "ÇALIYOR" görünür.

- [x] **1.0.2** (26 Eki 2026, yapı 102; §2.1b): resmî logo + Viva Medya bağlantısı, pencere gizlenir (süreç kapanmaz), ilerlemeyen şarkıyı atlayan bekçi, ses çıkışı yokken hata fırtınasının önlenmesi. Onur'un bilgisayarında denendi (gizle/aç 0,1 sn; olmayan ses aygıtıyla `KMP_SES_AYGITI` denemesi: 30 sn'de bir tek satır, sahte kayıt yok). **Yayında** (26 Eki 2026, `/indir/kmp/` ve sabit adres; etiket `v1.0.2`).

- [x] **1.0.3** (29 Eki 2026, yapı 103; §2.1c): simge durumundan/tepsiden geri gelen pencere ekrana alınır ve yeniden çizdirilir; pencere, oturum ve ekran olayları günlükte. Onur'un notebook'unda denendi. **Yayında** (29 Eki 2026, `/indir/kmp/` ve sabit adres; etiket `v1.0.3`).

**Belgeler (28 Eki 2026'da güncellendi):** bu BRIEF; derleme ve deneme `docs/KURULUM.md`; test rehberi
`docs/TEST-LISTESI.md`; sunucu, yayın ve mağaza kurulumu dahil her şey `../kurumsalmuzik/docs/KURULUM-KILAVUZU.html` §12
(kopyası proje kökünde); web tarafı test listesi kökteki `TEST-LISTESI.html` (offline bölümü 1.0'a göre).
