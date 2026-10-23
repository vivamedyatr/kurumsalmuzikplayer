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
| 2 | Arşiv koruması | **Seçenek A — sunucu şifreler** (§5). Program indirdiğini dokunmadan diske yazar, şifre bellekte çözülür. |
| 3 | Kod imzalama | **Şimdilik yok** (imzasız). Kurulumda "bilinmeyen yayıncı" uyarısı çıkar. Antivirüs sorunu çıkarsa yeniden değerlendirilir. |
| 4 | Geçiş | **Elle kurulum.** Müşteriler henüz Kurumsal Müzik Net'e taşınmadı; 2.x'ten otomatik geçiş yapılmaz. |
| 5 | Ad | Her yerde **"Kurumsal Müzik Player"**; sürüm sadece pencerenin sağ altında ("Kurumsal Müzik Player 1.0.3"). Programda "by Viva Medya" + vivamedya.com bağlantısı. |

### 2.2 Eski BRIEF'ten aynen geçen kararlar (özet; ayrıntı orada)

- **Platform:** Windows 7 SP1, 8, 8.1, 10, 11 — 64 bit. Tek program, tek kurulum dosyası, kullanıcı bazlı kurulum (yönetici izni istemez).
- **Arayüz:** ses aç/kıs, sonraki şarkı (10 sn'de bir, anons sırasında kapalı), çalma geçmişi, "Aylık yeni müzik" anahtarı. Durdur yok; akış programda düzenlenmez ("Akışı düzenle" tarayıcıda açılır).
- **Akış:** sadece tarz akışı; hesabın ortak "Offline akışı"; 7 günde bir değişir (sunucuda).
- **Paket:** akış başına 6 GB, tarz başına en fazla 16 saat, albüm başına ilk 40 şarkı, 30 günde en fazla 5 albüm değişir (sunucuda).
- **İndirme:** en fazla 6 Mbps; önce çalan tarz, sonra sıradaki, sonra diğerleri; kesilip devam eder; inerken çalar (tarz en az 3 şarkısı inince).
- **İnternetsiz:** 7 gün. Sonra "İnternet bağlantısı gerekli" der ve durur. Lisans günde bir, akış saatte bir, anonslar 15 dk'da bir sorulur.
- **Anonslar:** web'deki planlar aynen (P1–P6, PA/PB/PC, saatli **X/Y**); müzik anons sırasında duraklar, kaldığı saniyeden sürer. Anonslar şifresiz.
- **Tek bilgisayar:** aynı şube adıyla başka bilgisayardan girilirse eskisi en geç 15 dk içinde girişe döner.
- **Yayın kaydı:** çalan şarkı, anons, açıldı/durdu; 15 dk'da bir `/api/offline/log`'a; internetsizken 7 güne kadar birikir.
- **Güncelleme:** program kendini günceller; yeni sürüm arkada iner, **sadece program yeniden açılınca** kurulur, çalarken asla.
- **Görünüm:** web yayın ekranının dili (2.0.15 tasarımı): koyu zemin, Archivo başlık, plak kapağı, Viva kırmızısı, IBM Plex Mono saatler, sekmeler (Şarkılar, Anonslar, Akış, Arşiv), altta gün bandı. 760×460, en az 640×400. Yazı tipleri programın içinde.

## 3. Teknik yığın

| Katman | Seçim | Neden |
|---|---|---|
| Çalışma ortamı | **.NET Framework 4.8** | Windows 10 (1903+) ve 11'de hazır gelir; Windows 7 SP1/8.1'e kurulum dosyası ekler (çevrimdışı yükleyici pakette). Ek çalışma zamanı taşınmaz, program küçük kalır. |
| Arayüz | **WPF** | Yerel Windows penceresi; tarayıcı motoru yok. Koyu tema, özel yazı tipleri, kapak görselleri rahat. Yazılımla çizim (eski ekran kartlarında takılmasın diye). |
| Ses | **mpv.exe** (0.35, AVX2'siz x86_64) | 2.x'te Celeron/Win 8.1'de denendi. Ayrı süreç, JSON IPC (adlandırılmış boru). Müzik ve anons için iki mpv. |
| Ağ | `HttpClient` (TLS 1.2 açıkça) | Windows 7'de TLS 1.2 kayıt defteri/güncelleme ihtiyacı kurulumda kontrol edilir. |
| Gizli anahtar | Windows DPAPI (`ProtectedData`, kullanıcı kapsamı) | Cihaz anahtarı ve oturum. |
| Kurulum | **Inno Setup** | Tek .exe, kullanıcı bazlı, Türkçe; .NET 4.8 yoksa kurar. |
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
- **Bekçi:** motor mpv'yi izler (çökerse kaldığı şarkıdan yeniden başlatır). Motorun kendisi Windows Görev
  Zamanlayıcı'daki "oturum açılınca başlat + çökünce yeniden başlat" göreviyle ayakta tutulur (Kayıt Defteri Run anahtarı yerine; tek kopya kilidi ile).
- **Günlük:** `%LOCALAPPDATA%\Kurumsal Muzik Player\gunluk\` — dönen (en fazla 7 gün) metin günlüğü; donma, çökme,
  indirme hatası gerçek sebebiyle yazılır. Uzaktan destekte "günlüğü gönder" (sunucuya, onayla).
- **Veri klasörü:** `%LOCALAPPDATA%\Kurumsal Muzik Player\` (eski programla karışmaz).

## 5. Arşiv koruması — Seçenek A (karar Onur, 23 Eki 2026)

**Sorun:** 2.x her `.ts` parçasını indirip bilgisayarda AES ile şifreliyor ve tek tek dosya olarak yazıyordu (binlerce
yeni, rastgele görünen dosya). Fidye yazılımları tam olarak bunu yapar; davranış korumaları bu deseni yakalar.

**Çözüm:**
1. **Sunucu (Worker) şifreler.** Program parçayı istediğinde Worker onu **paket anahtarıyla** AES-CTR ile şifreleyip
   gönderir. Program bilgisayarda hiç şifreleme yapmaz; gelen baytları olduğu gibi yazar.
2. **Albüm başına tek dosya.** Bir albümün bütün şarkı parçaları tek bir kap dosyasına (`<albüm>.kmp`) art arda eklenir;
   başta küçük bir içindekiler tablosu. ~3.200 dosya yerine akış başına ~50–150 dosya.
3. **Şifre bellekte çözülür**, mpv'ye programın içindeki yerel akış üzerinden verilir (diske açık yazılmaz).
4. **Anahtar:** cihaz kaydında/manifestte sunucudan gelir, DPAPI ile saklanır. Klasör başka bilgisayara kopyalansa
   anahtar orada açılmaz → çalmaz (bugünkü koruma ile aynı).

**Neden güvenli:** ses dosyaları zaten rastgele görünür; şüpheli olan, bir programın yerelde okuyup-şifreleyip-yazması
ve dosya sayısıdır. A ikisini de ortadan kaldırır (Spotify ve Steam önbellekleri de böyle çalışır).

**Sunucu tarafı (web projesinde, Onur onayıyla — §7):** Worker'a "şifreli ver" kipi; anahtar Worker gizlisinde, imzalı
adreste paket kimliği. Cloudflare önbelleği şifresiz parçayı tutar; şifreleme her istekte Worker'da yapılır (CTR hızlı,
ücretsiz plan CPU sınırına dikkat — Faz 2'de ölçülür).

## 6. Sunucu API'si (hazır, değişmez)

Hepsi `https://kurumsalmuzik.net`, `Authorization: Bearer <cihaz anahtarı>`, `X-App-Version` başlığıyla:

| Yol | İş |
|---|---|
| `POST /api/offline/register` | Kullanıcı adı + şifre + şube adı → cihaz anahtarı (aynı şube adı eski bilgisayarı düşürür) |
| `GET /api/offline/status` | Lisans, hesap süresi, cihaz geçerli mi (günde bir + 15 dk'da bir düşme kontrolü) |
| `GET /api/offline/manifest` | Akış dilimleri, tarzlar, albümler, şarkılar, imzalı medya adresi ve süresi, paket döngüsü |
| `GET /api/offline/announcements` | Anons planları ve dosyaları (saatli X/Y dahil) |
| `POST /api/offline/announcements/played` | Anons çalma bildirimleri (7 güne kadar geriye) |
| `POST /api/offline/log` | Yayın kaydı (en fazla 5.000 olay/istek) |

Medya: `media.kurumsalmuzik.net/m/<şarkı anahtarı>/<NNNN>.ts?t=<imza>` (Worker). Kapaklar ve anons MP3'leri aynı Worker'dan.
Ayrıntılı alanlar eski projede: `src/main/library.ts` (Manifest tipi), `src/main/api.ts`, `src/main/announcer.ts`.

Akış ve anons kuralları web'den **birebir** C#'a çevrilir ve web'in testleriyle aynı örneklerle sınanır:
`kurumsalmuzik/src/lib/announce.ts` (+ `announce.test.ts`), tarz akışı/karışık liste mantığı (eski projede `src/main/engine.ts`, `player.ts`).

## 7. Kurumsal Müzik Net'e gereken ekler (her biri ayrı Onur onayıyla)

1. **Program kimliği:** sunucu bugün `X-App-Version < 2.0.17` olan programa saatli X/Y anonslarını göndermiyor. 1.0
   ayrı bir başlıkla kendini tanıtır (`X-App: kmp`); sunucu bu başlığı görünce sürüm karşılaştırmasını atlar. Panelde
   cihaz "Offline · KMP 1.0.3" diye görünür.
2. **Worker "şifreli ver" kipi** (§5) ve manifestte paket anahtarı. Eski 2.x programlar şifresiz kipi kullanmaya devam eder.
3. **Güncelleme dosyası:** `kurumsalmuzik.net/indir/kmp/` altında sürüm bilgisi (`latest.json`: sürüm, adres, SHA-256) ve
   kurulum dosyası. İndirme adresi `/indir/Kurumsal-Muzik-Player-Kurulum.exe` 1.0 yayına girince 1.0'ı gösterir (o güne kadar 2.0.17).

## 8. Fazlar

Her fazın sonunda kanıt senaryosu Onur'la birlikte çalıştırılır. Acele yok; her faz eski bilgisayarlarda ve Onur'un
Kaspersky + Acronis'li bilgisayarında denenir.

**Faz 0 — İskelet ve kanıt deneyi.** Proje yapısı, derleme, boş pencere + tepsi + motor + mpv ile yerel bir dosya çalma.
Ayrıca **dayanıklılık deneyi:** sahte 6 GB'lık arşiv yazan bir deneme aracı (albüm başına tek dosya) Kaspersky + Acronis
açıkken 2 saat çalışır.
*Kanıt: Onur'un bilgisayarında 24 saat donmadan çalar; Windows günlüğünde 1002 yok; antivirüs uyarısı yok.*

**Faz 1 — Giriş ve cihaz.** Kurulum dosyası (Inno Setup), giriş ekranı, `/register`, `/status`, tek kopya, oturum açılınca başlama, düşme.
*Kanıt: Windows 7 ve 11'de kur → şube girişi → panelde "Offline · KMP" cihaz → aynı şube adıyla ikinci bilgisayar girince ilki girişe döner.*

**Faz 2 — İndirme ve arşiv.** Manifest, öncelikli, 6 Mbps sınırlı, kesilip devam eden indirme; albüm kapları; Worker şifreli kip (onaylı).
*Kanıt: 3 Mbps hatta paket iner → internet kesilip gelince devam eder → program hiç donmaz → klasör başka bilgisayara kopyalanınca çalmaz.*

**Faz 3 — Çalma, anonslar, internetsiz 7 gün.** Tarz akışı, gün bandı, ses, sonraki, geçmiş, anonslar (X/Y dahil), lisans, yayın kaydı.
*Kanıt: interneti kes → akış saatinde tarz değişir, anons zamanında çalar → saat 8 gün ileri alınınca "İnternet bağlantısı gerekli" → internet gelince devam.*

**Faz 4 — Akış değişimi, paket döngüsü, kendini güncelleme.**
*Kanıt: web'den akış değiştir → program çalmayı kesmeden yeni tarzı indirip geçer → yeni sürüm yayınlanınca program yeniden açılışta güncellenir.*

**Faz 5 — Mağazada deneme ve dağıtım.** Gerçek bir mağazada bir hafta; indirme adresinin 1.0'a çevrilmesi.

## 9. Açık sorular (Onur karar verir)

Şu an yok. Fazlar ilerledikçe buraya yazılır.

## 10. Durum

- [ ] Faz 0 — İskelet ve kanıt deneyi (iskelet hazır, 23 Eki 2026: motor/pencere/mpv ayrı süreç, bekçiler denendi — pencere donunca 15 sn'de yeniden açılır, mpv çökünce 2 sn'de kaldığı yerden sürer, motor çökünce mpv de kapanır ve pencere motoru ~10 sn'de yeniden başlatır; dayanıklılık deneyi Onur'un Kaspersky + Acronis'li bilgisayarında sürüyor)
- [ ] Faz 1 — Giriş ve cihaz
- [ ] Faz 2 — İndirme ve arşiv
- [ ] Faz 3 — Çalma, anonslar, internetsiz 7 gün
- [ ] Faz 4 — Akış değişimi, paket döngüsü, güncelleme
- [ ] Faz 5 — Mağazada deneme ve dağıtım
