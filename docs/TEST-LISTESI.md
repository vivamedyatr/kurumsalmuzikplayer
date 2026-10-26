# Test listesi — Kurumsal Müzik Player 1.0

**Karar (Onur, 25 Eki 2026):** mağazada deneme yapılmaz. Final sürüm **1.0.0** yayınlanır; Onur testleri farklı
senaryolarda ve farklı Windows ortamlarında (7, 8.1, 10, 11; farklı antivirüsler) kendisi yapar. Hata bulunursa yeni
sürümle düzeltilir; kurulu programlar yeni sürümü kendiliğinden alır (en geç 4 saatte iner, yeniden açılışta kurulur).

Aşağıdaki liste bu testler için bir rehberdir.

## 1. Hazırlık (panelde)

- [ ] Deneme bilgisayarları: Windows 7 SP1, 8.1, 10, 11 (64 bit); en az birinde Kaspersky ve/veya Acronis.
- [ ] Hesapta "offline" izni açık, bir boş cihaz hakkı var (deneme bitince silinir).
- [ ] Hesabın **Offline akışı** kurulu (tarz akışı, 08–24 dilimleri); en az 4 tarz.
- [ ] Hesaba en az bir aralıklı (ör. Plan 2, 5 dk) ve bir **saatli** (Plan X, ör. 09:00 ve 13:30) anons atanmış.
- [ ] Her bilgisayarda: **64 bit** Windows, en az 10 GB boş disk, ses çıkışı bağlı.
  Windows 7/8.1'de .NET Framework 4.8 yoksa kurulum söyler (yönetici izniyle Microsoft'tan kurulur).

## 2. Kurulum

1. Kurulum dosyası: `https://kurumsalmuzik.net/indir/Kurumsal-Muzik-Player-Kurulum.exe` (her zaman son 1.0 sürümü; sürümlü
   dosyalar `/indir/kmp/`'de, son sürüm bilgisi `/indir/kmp/latest.json`).
2. "Windows kişisel bilgisayarınızı korudu" çıkarsa: **Ek bilgi → Yine de çalıştır** (imzasız program, karar).
3. Kurulum yönetici izni istemez; bitince program açılır → **şube girişi**: kullanıcı adı, şifre, şube adı.
4. Panelde cihaz **Offline · 1.0.x** olarak görünür. Paket iner (en fazla 6 Mbps; 6 GB ≈ 2–3 saat), ilk şarkılar
   inince yayın başlar.

## 3. Senaryolar (Faz 2–4'ün kanıtları)

| # | Ne yapılır | Ne görülmeli |
|---|---|---|
| 1 | Kurulumdan sonra bekle | 1–2 dakikada müzik başlar; pencerede tarz, şarkı, kapak, gün bandı. İndirme sürerken yayın kesilmez. |
| 2 | İndirme sürerken interneti 5 dk kes, sonra geri aç | Müzik sürer; indirme kaldığı yerden devam eder ("Yeni müzikler iniyor %…"). |
| 3 | Programın penceresini kapat, tepsiden aç | Müzik hiç durmaz. |
| 4 | Bilgisayarı yeniden başlat | Program kendiliğinden açılır (simge durumunda), müzik çalar. |
| 5 | Saatli anons saatini bekle (Plan X) | Müzik duraklar, anons çalar, müzik kaldığı saniyeden sürer. Panelde anons çalma kaydı görünür. |
| 6 | Akış saatinin değişmesini bekle (ör. 12:00) | Tarz değişir; gün bandındaki çizgi ilerler. |
| 7 | "Akışı düzenle" → web'de bir dilimin tarzını değiştir | En geç 20 dk içinde program yeni tarzı indirmeye başlar; o inene kadar eldeki tarzlar çalar. |
| 8 | İnterneti kes, 1 saat bekle | Akış ve anonslar internetsiz sürer; ekranda "İnternet yok: N gün daha çalar". |
| 9 | (İsteğe bağlı, deneme sonunda) Bilgisayar saatini 8 gün ileri al | "7 gündür internete bağlanılamadı…" yazar, müzik durur. Saat düzelip internet gelince sürer. |
| 10 | Claude yeni bir sürüm yayınlar (bir düzeltme gelince) | 4 saat içinde arkada iner; ertesi açılışta (ya da yeniden başlatınca) ~10 sn'de kurulur, program kendiliğinden açılır. |
| 11 | Arşiv klasörünü (`%LOCALAPPDATA%\Kurumsal Muzik Player\arsiv`) başka bilgisayara kopyala | Orada açılmaz (anahtar bilgisayara özel). |
| 12 | **Windows 7 SP1:** kurulumdan sonra giriş yap (güncellemeleri az bir Windows 7'de de dene) | Sunucuya bağlanır (TLS 1.2). Bağlanamazsa: günlükteki hata satırı + Windows Update durumu → bana. 1.0.0'da bunun için ayrı bir kontrol/uyarı yok (BRIEF §9). |
| 13 | Pencereyi kenarından büyüt/küçült, başka ekrana taşı, kapat-aç | Oranı bozulmadan ölçeklenir; yeri ve boyutu hatırlanır. |
| 14 | Programı kaldır, yeniden kur | Kaldırma veri klasörünü de siler: yeniden giriş ister, arşiv baştan iner (beklenen davranış). |
| 15 | **1.0.1:** alt çubuktaki kırmızı düğmeyle duraklat, 1–2 dk bekle, devam et | Aynı şarkı kaldığı saniyeden sürer. Duraklatılmışken "DURAKLATILDI" yazar, anons çalmaz, "Sonraki" kapalı. Panelin yayın kaydında o aralık "durdu". |
| 16 | **1.0.1:** sol rayda "Çıkış yap" → Vazgeç, sonra tekrar → Çıkış yap | Vazgeç'te hiçbir şey değişmez. Çıkışta müzik durur, giriş ekranında "Çıkış yapıldı" yazar. Aynı şube adıyla girince yeni cihaz hakkı harcanmaz, arşiv yeniden inmez. |
| 17 | **1.0.1:** sol raydaki, giriş ekranındaki ve "Yayın durdu" ekranındaki "Destek ↗" | Tarayıcıda kurumsalmuzik.net/destek açılır (WhatsApp ve e-posta). |
| 18 | **1.0.2:** pencereyi kapat (X), tepsiden ya da kısayoldan aç | Beklemeden açılır (süreç kapanmamıştı). Logo "KURUMSAL / MÜZİK NET", altında "BY VİVA MEDYA"; VİVA MEDYA'ya tıklayınca vivamedya.com açılır. |
| 19 | **1.0.2:** çalarken hoparlörü/ses kablosunu çıkar (ya da Windows ses aygıtını devre dışı bırak), 1 dk bekle, geri tak | Ekranda "SES ÇIKIŞI YOK" yazar; geri takınca en geç 30 sn'de müzik kendiliğinden başlar. Panel yayın kaydında o arada sahte şarkı yok. |

## 4. Uzaktan izleme (panelden)

- **Panel → hesap → Yayın kayıtları:** günlük çalan şarkılar, yayının açık/kapalı olduğu saatler. Boşluk = sorun.
- **Panel → hesap → cihazlar:** son bağlantı ve sürüm (1.0.x). Sürüm güncellemeden sonra kendiliğinden artmalı.
- **Anons çalma kayıtları:** saatli anonslar her gün saatinde.
- Sorun bildirilirse mağazadan: tepsi simgesi → **Günlük klasörünü aç** → o günün `motor-…txt` ve `pencere-…txt` dosyaları.
  Uzaktan bağlantı varsa (Tailscale / AnyDesk) Claude Windows olay günlüğüne de bakar (donma kaydı 1002, çökme 1000).

## 5. Bakılacaklar

- [ ] Pencere ya da program donmadı (Windows günlüğünde KurumsalMuzikPlayer için 1000/1002 yok).
- [ ] Yayın kaydında mağazanın açık olduğu saatlerde boşluk yok.
- [ ] Anonslar zamanında çaldı ve panele düştü.
- [ ] Yeni sürüm kendiliğinden kuruldu; giriş, arşiv ve ses düzeyi korundu.
- [ ] Antivirüs uyarısı yok (varsa: hangi program, ne dedi — ekran görüntüsü).
- [ ] Ses, pencere, açılış: kullanırken rahatsız eden bir şey yok.

## 5.1 1.0.0'da bilerek olmayanlar (sonraki sürüm adayları, BRIEF §9)

Testte eksik olarak not edilebilir; öncelik Onur'un:
- ~~Tıklanır vivamedya.com bağlantısı~~ → 1.0.1'de "Destek ↗". ~~Sessize alma~~ → 1.0.1'de duraklat/devam.
- Windows 7'de TLS/sertifika eksikliği için açık uyarı.
- "Günlüğü gönder" (bugün günlük dosyaları elle istenir).

## 6. Dağıtım (her biri Onur'un kararı)

1. **Sürüm adı:** **1.0.0** (yapı 100; 25 Eki 2026) → **1.0.1** (yapı 101; 25 Eki 2026: çıkış, duraklat, Destek, Yayın akışı) → **1.0.2** (yapı 102; 26 Eki 2026: logo, pencere gizlenir, takılan şarkı atlanır, ses çıkışı yok). Sonrakiler 1.0.3… (yapı numarası her yayında artar).
2. **Sabit indirme adresi:** ~~bugün 2.0.17'yi veriyor~~ → `kurumsalmuzik.net/indir/Kurumsal-Muzik-Player-Kurulum.exe`
   **1.0.0'ı veriyor** (karar Onur, 25 Eki 2026; müşteri e-postaları ve sitedeki bağlantılar bu adresi kullanıyor; her yeni
   1.0.x yayınında `scripts/yayinla.sh` bunu da günceller). Eski adlı `VM-Radio-Player-2.0-Kurulum.exe` 2.0.17'de kaldı;
   2.0.17'nin kendi dosyası (`…-2.0.17.exe`) sunucuda duruyor (geri dönüş gerekirse). 2.x'in güncelleme dosyası
   (`/indir/latest.yml`) ayrıca bir karar: 2.0.17'ler olduğu gibi kalır ya da son bir 2.x sürümüyle 1.0'a yönlendirilir.
3. **2.x şubelerin geçişi (karar: elle kurulum):** 1.0 kurulur → aynı şube adıyla giriş (2.x'in cihazını devralır) →
   arşiv bir kez yeniden iner (2–3 saat; o sırada inen şarkılar çalar) → 2.x kaldırılır.
4. **Cloudflare Workers Paid** (aylık 5 $): ücretsiz plan günde 100.000 istek; bir şubenin ilk kurulumu ~6.000 istek.
   Aynı gün ~15'ten fazla şube kurulacaksa önce geçilmeli.
5. **Web temizliği** (sonraki web yayını): Worker'daki `/e/` adresi ve manifestteki `media.archive` kaldırılır
   (1.0 artık kullanmıyor).
6. **Eski Java programın müşterileri** (sunucu yıl sonunda kapanıyor): Kurumsal Müzik Net'e hesap + 1.0.
