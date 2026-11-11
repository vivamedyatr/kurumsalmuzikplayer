> **Kopya (11 Kas 2026).** Bu dosya Electron'lu 2.x'in (VM Radio Player, bırakıldı) `docs/BRIEF.md`'sidir; 1.0 için
> **işlev listesi** olarak buraya kopyalandı, yerel `VM Offline Radio Player/` klasörü silindi. Değiştirilmez. İçindeki
> `src/…`, `scripts/…`, `docs/KURULUM.md` yolları 2.x deposunu gösterir: GitHub `vivamedyatr/vm-radio-player` (dal `main`).
> 1.0 BRIEF'i (`docs/BRIEF.md`) aksini söylemedikçe buradaki kurallar geçerlidir.

# VM Radio Player 2.0 — BRIEF

> **⛔ BU PROJE BIRAKILDI (karar Onur, 23 Eki 2026).** Electron tabanlı program bazı bilgisayarlarda (yeni Windows +
> Kaspersky/Acronis) açılıştan sonra donuyordu (Windows olay 1002, AppHang). Yerine **Kurumsal Müzik Player 1.0**
> (C# .NET Framework 4.8 + WPF + mpv) `../Kurumsal Müzik Player/` klasöründe baştan yazıldı; **final 1.0.0, 25 Eki
> 2026'da yayınlandı** ve sitedeki sabit indirme adresi (`/indir/Kurumsal-Muzik-Player-Kurulum.exe`) artık 1.0'ı veriyor.
> Bu klasörde yeni iş yapılmaz; şubelerdeki 2.0.17'ler elle 1.0'a geçirilir. 2.x'in güncelleme dosyası (`/indir/latest.yml`)
> ve eski adlı `VM-Radio-Player-2.0-Kurulum.exe` 2.0.17'de kaldı. Bu BRIEF, 1.0 için **işlev listesi** olarak kullanılır.
>
> **`scripts/yayinla.sh` ÇALIŞTIRILMAZ:** sabit indirme adresini 2.x'e geri çevirir (1.0'ın yayını
> `../Kurumsal Müzik Player/scripts/yayinla.sh` ile yapılır).

Tek doğru kaynak. Kararlar tarihleriyle yazılır; değişen karar silinmez, üstü çizilip yenisi eklenir.

> **⚠ CANLI SİSTEM — DEĞİŞİKLİK DONDURMASI (karar Onur, 16 Eki 2026).** Sistem ayakta ve stabil çalışıyor; testlerden sonra
> müşteriler taşınacak. **Onur açıkça onay vermeden hiçbir şey değiştirilmez:** kod, ekleme, silme, veritabanı (geçiş dahil),
> sunucu, R2, Cloudflare, offline program sürümü, yayınlama (deploy). Okumak, incelemek, sorun bulmak ve öneri hazırlamak
> serbesttir; uygulamadan önce ne değişeceği ve etkisi Onur'a yazılır, onay beklenir. Onay o işe özeldir, sonrakine geçmez.

---

## 1. Ne ve neden

VM Offline Radio Player (eski, Java) mağazalarda internetsiz müzik çalan bir Windows programı; sunucu süresi
**2026 sonunda** bitiyor. Yerine **VM Radio Player 2.0** gelir:

- **Kurumsal Müzik Net'e bağlıdır:** aynı arşiv (R2 `kurumsalmuzik-dev`, `music/<Tarz>/<Albüm>/`), aynı sunucu, aynı
  hesaplar, akışlar, anonslar ve yönetim paneli. Ayrı bir arşiv, ayrı bir sunucu yok.
- **Mağazada internetsiz çalar:** akışındaki tarzların müziği bilgisayara şifreli iner; internet yokken 7 gün çalar.
- **Mağaza yayını kurcalayamaz:** programda sadece ses ve çalma geçmişi var. Akış web sitesinden değişir.

Offline hizmet **sadece tarz modu** ile çalışır (karar 9 Eki 2026); bu hizmeti seçen müşteri bunu kabul eder.

## 2. Kararlar (Onur, 9 Eki 2026)

| # | Konu | Karar |
|---|---|---|
| 1 | Platform | Sadece **Windows 7, 8, 8.1, 10, 11**. Tek program, tek kurulum dosyası. iOS/Android gündemde değil. |
| 2 | Arayüz | Sadece **ses aç/kıs** ve **çalma geçmişi** (müzik + anons). Şube yayını kurcalayamaz. |
| 3 | Akış düzenleme | Programda yok. "Akışı düzenle" tarayıcıda Kurumsal Müzik Net'i açar; akış şifresi web'deki gibi istenir. |
| 4 | Akış türü | Offline'da **sadece tarz akışı.** Albüm ve ruh hâli akışı yok. |
| 5 | Ne iner | Sadece akışta olan tarzlar; her tarzdan ~~en fazla 12 saatlik~~ → **en fazla 16 saatlik** müzik (§4; 16 Eki 2026). Web'e eklenen yüzlerce albüm offline'a yansımaz. |
| 6 | Paket değişimi | ~~Tarz başına 3~~ → ~~akış başına 3~~ → **Akış başına 30 günde en fazla 5 albüm**, albüm başına en fazla 40 şarkı (karar 15 Eki 2026): son 90 günde eklenmiş albümlerden tarihi en yeni 5'i girer, girenin tarzında tarihi en eski albüm çıkar (tarz payı korunur); 90 günde yeni yoksa 5 albüm karışık (§4). Liste o zaman yeniden karışır. Kalıcı silinen albüm beklemeden değişir; akış değişince ya da panelden değiştirilince paket baştan kurulabilir. |
| 7 | Akış değiştirme sınırı | Offline akışı **7 günde bir** değişir (hesap başına; yapıldı, 15 Eki 2026). **Offline'da şube bazlı akış yok (16 Eki 2026):** bütün offline şubeler hesabın ortak "Offline akışı"nı çalar; bir şube akış şifresiyle değiştirirse hepsi en geç 1 saat içinde yeni akışa geçer. Sadece tarz akışı. Şifre ifşa olursa Onur panelden akış şifresini değiştirir (açık düzenlemeler hemen kapanır). Web şubeleri kendi akışlarını serbestçe kurar. Onur panelden sınırsız değiştirebilir. |
| 8 | İnternetsiz süre | **7 gün.** 7 gün sunucuya ulaşamazsa "İnternet bağlantısı gerekli" der ve durur. |
| 9 | Sorgu sıklığı | Lisans/hesap süresi **günde bir**, akış **saatte bir**, anonslar **15 dakikada bir.** |
| 10 | Program güncellemesi | Program **kendini günceller.** Yeni sürüm `kurumsalmuzik.net/indir/`'den arka planda iner; **sadece program yeniden açılınca kurulur** (kapanışta ya da açılışta, müzik başlamadan), çalarken asla (Onur, 12 Eki 2026). |
| 11 | Cihaz sınırı | Web şubeleriyle **aynı hesap, aynı cihaz sınırı**, aynı panel. Onur cihazı panelden siler; 14 gün kuralı da otomatik siler. |
| 12 | Kod imzalama | **İmzasız** başlanır (ek maliyet yok). İlk kurulumda Windows'un "bilinmeyen yayıncı" uyarısı çıkar; sonra bakılır. |
| 13 | Windows 7/8 | Electron 22 (Windows 7'yi destekleyen son sürüm) ile **tek program** (yol A). |
| 14 | Toplam sınır | Akış başına toplam **6 GB** (~104 saat), tarzlara eşit bölünür; tarz başına ~~en fazla 12 saat~~ → **en fazla 16 saat** (16 Eki 2026). Akış 08–24 arası 16 dilim olduğundan en fazla 16 tarz: tarz başına en az 6,5 saat. Akış genişleyince bilgisayarlardaki fazla arşiv silinir, daralınca paketten tamamlanır (dengeleme, `deviceAlbums`). |
| 17 | İndirme hızı | En fazla **6 Mbps** (10 Eki 2026). |
| 18 | İnerken çalma | İnen şarkılarla hemen çalar, indirme arkada sürer (10 Eki 2026). |
| 19 | 64 bit | Program 64 bit (10 Eki 2026). 32 bit Windows'lu şube web'den dinler. |
| 20 | Ses takılması | Ses Web Audio'dan ~170 ms tamponla çıkar (varsayılan 10 ms sanal/güçsüz makinede cızırdıyordu); pencere ve ses servisi "Normalin üstü", indirme ayrı süreçte "Normalin altı" öncelikte; ekran en fazla 5 sn'de bir güncellenir (10 Eki 2026). |
| 21 | Albüm başına şarkı | Offline'a her albümün **ilk 40 şarkısı** girer, her yerde (ilk kurulum dahil) (10 Eki 2026). |
| 22 | Aylık yeni müzik anahtarı | Programda AÇIK/KAPALI (varsayılan AÇIK). KAPALI: albüm listesi dondurulur; ilk paket tamamlanır, akışa eklenen yeni tarz iner, **kalıcı silinen albüm yine silinir** (lisans). Tekrar AÇIK: 30 gün geçmişse o an, en fazla 5 albüm değişir; sonraki aylarda 5'er (10 Eki 2026; 15 Eki'de 3 → 5). |
| 23 | Sonraki şarkı | Düğme var; 10 sn'de bir, anons sırasında kapalı. Hareketli ses çubukları yok (10 Eki 2026). |
| 24 | Ses motoru | **mpv** (0.35, AVX2'siz x86_64 derlemesi; Windows 7/8.1 uyumlu), ayrı süreçte **2 sn tampon**; anonslar ikinci mpv'de, müzik anons sırasında duraklar ve kaldığı saniyeden sürer. Tarayıcı motorunun sesi gerçek güçsüz bilgisayarda (Asus Celeron 1007U, Win 8.1) ve sanal makinede takılıyordu (10 Eki 2026). |
| 25 | İndirme bağlantısı | Chromium ağ katmanı (Windows sertifika deposu/proxy); disk işi ayrı düşük öncelikli süreçte. Hata ekranda gerçek sebebiyle yazar, `gunluk.txt`'e düşer (10 Eki 2026). |
| 26 | Pencere | Küçük (varsayılan 760×460, en az 640×400), boyut/yer hatırlanır, küçük ekrana sığar; Winamp'tan esinlenen: kapak, tarz/albüm/şarkı, ilerleme, Sonraki, ses, "Aylık yeni müzik" anahtarı; sekmeler: Akış · Şarkılar · Anonslar · Arşiv (10 Eki 2026). |
| 28 | Tek bilgisayar | Aynı şube adıyla ikinci bilgisayardan programa girilirse **eskisi düşer** (anahtarı geçersiz olur, en geç 15 dk içinde "Bu şube adıyla başka bir bilgisayardan giriş yapıldı" diyerek girişe döner). Web'den akış düzenlemek için giren tarayıcı programı düşürmez; offline cihaz web'de müzik çalmaz, sadece tarz akışı düzenleyicisini açar (15 Eki 2026). |
| 29 | Anons geçişi | Anons mpv'sinde boşluksuz geçiş kapalı (`--gapless-audio=no`): açıkken "dosya bitti" olayı ~2 sn erken geliyor, müzik anonsun son saniyelerinin üstüne giriyordu (ölçüldü). Kuşak bitince müzik 0,4 sn sonra döner (15 Eki 2026). |
| 30 | Anons akışı | Anonslar sekmesinde salt okunur "anons akışı": sıradaki kuşağın saati, yayındaki planlar ve anonsları (bitiş tarihi, inmediyse "iniyor"), başlayacak tarihli anonslar; altında son çalanlar (15 Eki 2026). |
| 27 | Ad ve sürüm | Her yerde (başlık, kurulum, simge) **"VM Radio Player 2.0"**; sadece pencerenin sağ altında tam sürüm yazar, örn. "VM Radio Player 2.0.13" (uzaktan destekte görülsün diye, 15 Eki 2026). Eski 2.0.x kurulum dosyaları silindi (14 Eki 2026). |
| 15 | 7 gün sınırı | **Hesap başına** (merkez bütün şubelerini tek seferde değiştirir). |
| 16 | Özel tarzlar | Kürtçe, lisanslı Türkçe Pop gibi tarzlar sadece izin verilen hesaplara görünür; ruh hâli ve albüm akışına girmez (Kurumsal Müzik Net'te yapılır, §7). |

## 3. Mağazadaki program

Tek pencere, sade:
- **Şimdi çalan:** tarz adı, kapak, şarkı adı. Gün bandı (hangi saatte hangi tarz) — sadece görüntü.
- **Ses:** aç/kıs, sessiz. Durdur düğmesi **yok** (yayın kurcalanmasın).
- **Çalma geçmişi:** son çalan şarkılar ve anonslar (yerel, son 7 gün).
- **Durum satırı:** "Çevrimiçi · son eşitleme 14:05" / "İnternetsiz · 5 gün daha çalar"; indirme sürüyorsa
  "Yeni müzikler iniyor %40" (yayını etkilemez).
- **"Akışı düzenle":** tarayıcıda Kurumsal Müzik Net'in ilgili sayfasını açar.
- Windows açılınca kendiliğinden başlar, tek kopya çalışır, kapatılınca simge durumuna küçülür.

İlk açılış: kullanıcı adı + şifre + şube adı (web'deki şube girişiyle aynı). Cihaz, hesabın cihaz sınırına sayılır.

**Görünüm (karar Onur, 22 Eki 2026; 2.0.15):** web yayın ekranıyla aynı dil — koyu zemin, Archivo geniş/ağır başlık
(çalan tarz), kumlu plak kapağı, Viva kırmızısı, IBM Plex Mono saatler; üstte "● Yayında · saat", sağda sekmeler
(Şarkılar, Anonslar, Akış, Arşiv), altta web'deki **gün bandı** (08–24, tarz renkleriyle, şu an çizgisi). Pencere küçük
kalır (760×460, en az 640×400; 13" laptopta da). Yazı tipleri programın içinde (internetsiz; SIL OFL). Eski Winamp
esintili küçük görünümün (10 Eki 2026) yerini aldı.

## 4. Offline paket (karar 9 Eki 2026)

Web'de bir tarz yüzlerce albüm olabilir; mağazanın zayıf interneti (3 Mbps) bunu indiremez. Bu yüzden sunucu her
**akış** için bir **offline paket** tutar; aynı akışı çalan bütün şubeler aynı paketi indirir (zincirde aynı müzik).

- **Boyut:** paket bütün albümlerden oluşur (albüm yarım alınmaz). Akış başına toplam **6 GB** (≈ 104 saat), akıştaki
  tarzlara eşit bölünür; tarz başına **en fazla 16 saat** (≈ 920 MB; 16 Eki 2026, önce 12). 1–6 tarz: tarz başına tam 16 saat; 8 tarz: 13 saat; 16 tarz: ~6,5 saat.
  Tarzda bundan az müzik varsa tarzın tamamı.
- **İlk paket:** her tarzın en yeni albümleri, tarzın payı dolana kadar.
- **30 günde bir değişim (karar 15 Eki 2026; önce 3):** akış başına **en fazla 5 albüm** değişir.
  1. Son **90 günde** eklenmiş, pakete hiç girmemiş albümlerden **tarihi en yeni 5'i** girer — hangi tarzdan olursa
     olsun. (Örnek akış: Deep House 09–12, Pop Mid 12–15, Pop High 15–18, Dance 18–20, Chillout 20–23.)
  2. Yeni albüm 5'ten azsa (ya da 90 günde hiç yoksa) kalan hak arşivden **karışık** seçilir; hak akışta **en çok saati
     olan tarzdan** başlayarak tarzlara sırayla verilir (eşitse akıştaki sıra).
  3. Giren albümün tarzında pakette **tarihi en eski** albüm çıkar (tarz payı korunur). Karışık çalma listesi
     de o zaman yenilenir.
  Seçim sunucuda yapılır (hafif; aynı akışı çalan bütün şubeler aynı albümleri alsın diye).
- **Kalıcı silinen albüm** (R2'den kaldırılıp sistemden düşen) 30 günü beklemeden aynı tarzdan bir albümle değişir; 3 hakka sayılmaz.
- **Akış değişince** (7 günde bir) yeni tarzların paketi kurulur, akıştan çıkan tarzların albümleri silinir; paylar
  yeniden hesaplanır.

## 5. İndirme ve depolama

- **Öncelik:** önce şu an çalan tarz, sonra sıradaki saatin tarzı, sonra diğerleri.
- **Hız sınırı:** en fazla **6 Mbps** (karar 10 Eki 2026; önce 1 Mbps düşünülmüştü, yavaş bulundu). 2,4 GB'lık paket ~1 saatte iner.
- **İnerken çalar (karar 10 Eki 2026):** program inen şarkılarla hemen çalmaya başlar, indirme arkada sürer (Faz 3).
  Önce şu an çalan tarz iner; yayın ilk birkaç dakikada başlar.
- **Kesilip devam eder:** internet giderse kaldığı parçadan devam eder; yarım parça çalınmaz.
- **Arka planda:** indirme ve silme yayını ve pencereyi asla dondurmaz; silme azar azar yapılır.
- **Kesintisiz geçiş:** akış değişince program elindeki müzikle çalmaya devam eder; yeni tarz inene kadar çalabildiği
  en yakın içeriği çalar, sessizlik olmaz. Eski tarzın dosyaları yenisi tamamen indikten sonra silinir.
- **Şifreli:** `.ts` parçaları diskte AES ile şifreli durur; anahtar cihaza özeldir ve Windows'un kendi koruma
  alanında (DPAPI) saklanır. Klasör kopyalansa başka bilgisayarda çalmaz. Anonslar şifresiz (karar).
- **Medya adresleri:** indirme web'deki imzalı adreslerle (`media.kurumsalmuzik.net`, Worker) yapılır; yeni bir
  medya yolu açılmaz.

## 6. Çalma ve internetsiz çalışma

- Akış, web'deki tarz modu mantığıyla çalar (dilimler, karışık liste, rastgele noktadan katılma), ama **yerel
  dosyalardan.** Saat bilgisayarın saati; her eşitlemede sunucu saatiyle fark düzeltilir.
- **Anonslar:** web'deki planlar (kuşak, saat başı vb.) aynen; liste 15 dakikada bir alınır, MP3'ler yerele iner.
  Bitiş günü dahil, ertesi gece 00:00'da biter (web ile aynı).
- **Lisans:** günde bir sorulur. Hesap süresi bitmişse durur. 7 gün sunucuya ulaşamazsa durur.
- **Çalma geçmişi:** yerelde 7 gün. Anons çalma kayıtları internet gelince sunucuya gönderilir (web'deki gibi).
- **Yayın kaydı (web BRIEF §14; 2.0.15):** çalan şarkılar ve yayının açıldığı/durduğu anlar bilgisayarda birikir,
  15 dk'da bir `/api/offline/log`'a toplu gider; internet yoksa 7 güne kadar bekler. Anonslar çalma bildiriminden yazılır.

## 7. Kurumsal Müzik Net'e ekler (web projesinde, eklenti niteliğinde)

- **Cihaz türü:** cihaz "Web" ya da "Offline". Panelde offline cihazlar etiketle görünür.
- **Offline paketleri:** akış başına paket, 30 günlük döngü (en fazla 5 albüm), silinen albümün değişimi; panelde görüntüleme.
- **Özel tarzlar (önce yapılır, web için de geçerli):** tarz panelden "özel" işaretlenir; vitrin, site, sitemap,
  şablonlar, ruh hâli ve albüm akışı onu hiç görmez. Hesap sayfasında tarz tarz izin verilir; izinli hesap özel tarzı
  sadece **tarz akışında** kullanır. İzin geri alınırsa panel uyarır, o saatlere akıştaki başka bir tarz konur.
- **Offline API:** giriş/cihaz kaydı, "akışım + paketlerim + anonslarım", lisans, çalma kaydı, program güncellemesi.
- **Akış kuralları:** offline cihazı olan hesapta akış düzenleyici sadece tarz sekmesini gösterir; 7 günde bir
  değişiklik sınırı; yönetim paneli sınıra tabi değil.
- Mevcut web yayınına dokunulmaz.

## 8. Teknik yığın

| Katman | Seçim | Not |
|---|---|---|
| Program kabuğu | **Electron 22** | Windows 7/8/8.1'i destekleyen son sürüm (Chromium 108). Program internette gezinmez, sadece kendi sunucumuzla konuşur. |
| Arayüz | Web teknolojisi (TypeScript) | Kurumsal Müzik Net'in görsel diliyle. |
| Çalma | HLS (hls.js), yerel şifreli parçalar | Şifre program içinde çözülür; dosya diske açık yazılmaz. |
| Kurulum | NSIS (electron-builder), kullanıcı bazlı | Yönetici izni istemez. İmzasız (karar 12). |
| Güncelleme | electron-updater | Güncellemeler kendi sunucumuzdan/R2'den. |
| Gizli anahtar | Electron safeStorage (Windows DPAPI) | |

## 9. Fazlar

Her fazın sonunda kanıt senaryosu Onur'la birlikte çalıştırılır.

**Faz 1 — Kabuk, kurulum ve cihaz kaydı.** Kurulum dosyası, Windows'la açılma, tek kopya, giriş ekranı; web'e
cihaz türü ve offline giriş API'si.
*Kanıt: Windows 7 ve Windows 11'de kur → şube girişi yap → panelde "Offline" cihaz görünür → cihaz sınırı dolunca
ikinci bilgisayar duvara çarpar → panelden silinen cihaz programda girişe döner.*

**Faz 2 — Offline paket ve şifreli indirme.** Sunucuda tarz paketleri; programda öncelikli, hız sınırlı, kesilip
devam eden, şifreli indirme.
*Kanıt: 3 Mbps'e kısılmış hatta akıştaki tarzlar iner → indirme sırasında internet kesilip gelince devam eder →
program hiç donmaz → indirilen klasör başka bilgisayara kopyalanınca çalmaz.*

**Faz 3 — Yerelden çalma, anonslar, internetsiz 7 gün.** Tarz akışı, gün bandı, ses, geçmiş, anonslar, lisans.
*Kanıt: interneti kes → akış saatinde tarz değişir, anons zamanında çalar → bilgisayar saati 8 gün ileri alınınca
"İnternet bağlantısı gerekli" → internet gelince devam eder.*

**Faz 4 — Akış kuralları, paket döngüsü, otomatik güncelleme.** 7 gün sınırı, panelden aşma, kesintisiz geçiş,
30 günlük döngü, kalıcı silinen albüm, kendini güncelleme.
*Kanıt: web'den akış değiştir → ikinci deneme reddedilir → panelden değiştirilir → program çalmayı kesmeden yeni
tarzı indirip geçer → yeni sürüm yayınlanınca program kendini günceller.*

**Faz 5 — Mağazada deneme ve geçiş.** Gerçek bir mağazada bir hafta; eski programdan geçiş yöntemi ve duyurusu.

## 10. Açık sorular (Onur karar verir)

Şu an açık soru yok. Yeni sorular fazlar ilerledikçe buraya yazılır.

## 10a. Sıradaki işler (karar Onur, 23 Eki 2026) — sırayla

**1. Pencere donması (2.0.16; yapıldı, yayında 23 Eki 2026).** Müzik etkilenmiyor (ses ayrı süreçte, mpv); sadece pencere donuyor ya da donuk/beyaz
kalıyor. Ne zaman: ekran kapanınca/uyku, pencere simge durumundayken ve çalışırken rastgele; bilgisayara göre
değişiyor. Sebep: Windows ve tarayıcı motoru (Chromium) arka plandaki pencereyi yavaşlatıp askıya alıyor; eski ekran
kartlarında çizim motoru takılabiliyor. **Programı başka dilde (C#) yeniden yazmak çözmez** — Windows'un güç kuralları
her programa uygulanır; çare programın kendisinde. Yapılacaklar:
- Arka plan kısıtlamaları kapalı: pencere gizli/simge durumunda/üstü kapalıyken yavaşlatılmaz.
- Windows'a "uygulamayı askıya alma" isteği (ekranın kapanmasını ya da bilgisayarın uykusunu engellemez).
- Eski/zayıf ekran kartlarında donmaya karşı donanım hızlandırma kapalı (arayüz hafif; yazılımla çizilir).
- Donma bekçisi: pencere yanıt vermezse günlüğe yazılır ve pencere kendini yeniler (müzik etkilenmez); pencere geri
  açılınca ekran hemen tazelenir.
- Başarı ölçüsü: 2.0.16'dan sonra şubelerden donma bildirimi gelmemesi; günlükte (`gunluk.txt`) yanıt vermeme kaydı.
  Yeniden yazma ancak bu önlemlerden sonra donma sürerse, verilere bakılarak yeniden değerlendirilir.

**2. Ad: "Kurumsal Müzik Player" (2.0.16; yapıldı, yayında 23 Eki 2026).** "VM Radio Player 2.0" adı her yerde kalkar: pencere başlığı, kurulum,
masaüstü/başlat kısayolu, tepsi, Windows'un program listesi; sürüm sağ altta ("Kurumsal Müzik Player 2.0.16").
Programın içinde **"by Viva Medya"** ve vivamedya.com bağlantısı (tarayıcıda açılır). Güncelleme kesintisiz olmalı:
uygulama kimliği aynı kalır (otomatik güncelleme sürer), veri klasörü aynı kalır (inen arşiv yeniden inmez), eski ad
kısayolları ve Windows başlangıç kaydı temizlenir. İndirme adresi yeni adla açılır; **eski adres
(`VM-Radio-Player-2.0-Kurulum.exe`) de çalışmaya devam eder** (müşterilere gönderilmiş bağlantılar).

**3. Saatli anons planları X ve Y** (web BRIEF §8.2): her gün seçilen saat(ler)de çalar; offline da aynı kuralla
çalar (`src/shared/announce.ts` web'le birebir). **Yapıldı: 2.0.17.** Sunucu bu anonsları 2.0.17 öncesine göndermez.

## 11. Fazlarda ele alınacak notlar

- **Faz 4 — "Akışı düzenle":** düğme şimdilik `kurumsalmuzik.net/giris`'i açıyor. Web'de şube girişi `/yayin`'a gider ve
  orada müzik kendiliğinden çalmaya başlar; offline cihazda web yayın ekranı çalmamalı, sadece (tarz) akış düzenleyici
  açılmalı. Offline cihaz web girişiyle Web türüne dönmez (web projesinde yapıldı, 9 Eki 2026). **Yapıldı (15 Eki 2026):**
  offline cihaz `/yayin`'da müzik çalmaz, sadece tarz akışı düzenleyicisi (`OfflineFlow.tsx`) açılır.
- **Faz 1 sonucu (9 Eki 2026):** kurulum dosyası 65 MB (32 bit, tek dosya Windows 7–11).
- **Faz 3 yapısı (10 Eki 2026):** ne çalacağına ana süreç karar verir (dilim, tarz inmediyse inmiş başka tarz; tarz en az
  3 şarkısı inince çalınır), pencere çalar. Müzik `vmp://` yerel adreslerinden HLS (hls.js, pakette); parçanın şifresi
  bellekte çözülür. Anons kuşağında müzik durur, anonslar sırayla çalar, müzik **kaldığı yerden** sürer. Anons kuralları
  web'deki `announce.ts`'in birebir kopyası (`src/shared/announce.ts`). Çalma kayıtları internetsizken birikir, gerçek
  saatleriyle gönderilir. Saat her sunucu bağlantısında düzeltilir. Bilgisayar saati geri alınarak 7 gün kuralı
  uzatılabilir; bu bilinen sınır (sıradan kullanıcı için yeterli).
