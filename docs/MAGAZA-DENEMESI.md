# Faz 5 — Mağazada deneme (bir hafta) ve dağıtım hazırlığı

Amaç: Kurumsal Müzik Player 1.0'ın gerçek bir mağazada, gerçek bilgisayarda ve gerçek internetle bir hafta boyunca
**donmadan, susmadan** çaldığını görmek; Faz 2–4'ün kanıt senaryolarını orada çalıştırmak; sonra dağıtıma geçmek.

## 1. Hazırlık (Onur, panelde)

- [ ] Mağaza seçildi: ______________________ (tercihen 2.x'in donduğu türden bir bilgisayar: Kaspersky/Acronis ya da eski Windows)
- [ ] Hesapta "offline" izni açık, bir boş cihaz hakkı var (deneme bitince silinir).
- [ ] Hesabın **Offline akışı** kurulu (tarz akışı, 08–24 dilimleri); en az 4 tarz.
- [ ] Hesaba en az bir aralıklı (ör. Plan 2, 5 dk) ve bir **saatli** (Plan X, ör. 09:00 ve 13:30) anons atanmış.
- [ ] Mağaza bilgisayarı: Windows 7 SP1 / 8.1 / 10 / 11, **64 bit**, en az 10 GB boş disk, ses çıkışı bağlı.
  Windows 7/8.1'de .NET Framework 4.8 yoksa kurulum söyler (yönetici izniyle Microsoft'tan kurulur).

## 2. Kurulum (mağazada ya da uzaktan)

1. Kurulum dosyası: `https://kurumsalmuzik.net/indir/kmp/` altındaki son sürüm (Claude yayınlar; adresi Onur'a verir).
2. "Windows kişisel bilgisayarınızı korudu" çıkarsa: **Ek bilgi → Yine de çalıştır** (imzasız program, karar).
3. Kurulum yönetici izni istemez; bitince program açılır → **şube girişi**: kullanıcı adı, şifre, şube adı.
4. Panelde cihaz **Offline · 1.0.x** olarak görünür. Paket iner (en fazla 6 Mbps; 6 GB ≈ 2–3 saat), ilk şarkılar
   inince yayın başlar.

## 3. Birinci gün: kanıt senaryoları (Faz 2–4)

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
| 10 | Deneme haftasında Claude yeni bir sürüm yayınlar | 4 saat içinde arkada iner; ertesi açılışta (ya da yeniden başlatınca) ~10 sn'de kurulur, program kendiliğinden açılır. |
| 11 | Arşiv klasörünü (`%LOCALAPPDATA%\Kurumsal Muzik Player\arsiv`) başka bilgisayara kopyala | Orada açılmaz (anahtar bilgisayara özel). |

## 4. Hafta boyunca uzaktan izleme (Onur, panelden; mağazaya gitmeden)

- **Panel → hesap → Yayın kayıtları:** günlük çalan şarkılar, yayının açık/kapalı olduğu saatler. Boşluk = sorun.
- **Panel → hesap → cihazlar:** son bağlantı ve sürüm (1.0.x). Sürüm güncellemeden sonra kendiliğinden artmalı.
- **Anons çalma kayıtları:** saatli anonslar her gün saatinde.
- Sorun bildirilirse mağazadan: tepsi simgesi → **Günlük klasörünü aç** → o günün `motor-…txt` ve `pencere-…txt` dosyaları.
  Uzaktan bağlantı varsa (Tailscale / AnyDesk) Claude Windows olay günlüğüne de bakar (donma kaydı 1002, çökme 1000).

## 5. Başarı ölçütleri (hafta sonunda)

- [ ] 7 gün boyunca pencere ya da program donmadı (Windows günlüğünde KurumsalMuzikPlayer için 1000/1002 yok).
- [ ] Yayın kaydında mağazanın açık olduğu saatlerde boşluk yok.
- [ ] Anonslar zamanında çaldı ve panele düştü.
- [ ] Yeni sürüm kendiliğinden kuruldu; giriş, arşiv ve ses düzeyi korundu.
- [ ] Antivirüs uyarısı yok (varsa: hangi program, ne dedi — ekran görüntüsü).
- [ ] Mağaza personeli için şikâyet yok (ses, pencere, açılış).

## 6. Dağıtım (deneme başarılı olunca; her biri Onur'un kararı)

1. **Sürüm adı:** "1.0.0-faz…" yerine **1.0.0** (yapı numarası artarak sürer).
2. **Sabit indirme adresi:** `kurumsalmuzik.net/indir/Kurumsal-Muzik-Player-Kurulum.exe` bugün 2.0.17'yi veriyor;
   1.0'a çevrilir (müşteri e-postaları ve sitedeki bağlantılar bu adresi kullanıyor). 2.x'in güncelleme dosyası
   (`/indir/latest.yml`) ayrıca bir karar: 2.0.17'ler olduğu gibi kalır ya da son bir 2.x sürümüyle 1.0'a yönlendirilir.
3. **2.x şubelerin geçişi (karar: elle kurulum):** 1.0 kurulur → aynı şube adıyla giriş (2.x'in cihazını devralır) →
   arşiv bir kez yeniden iner (2–3 saat; o sırada inen şarkılar çalar) → 2.x kaldırılır.
4. **Cloudflare Workers Paid** (aylık 5 $): ücretsiz plan günde 100.000 istek; bir şubenin ilk kurulumu ~6.000 istek.
   Aynı gün ~15'ten fazla şube kurulacaksa önce geçilmeli.
5. **Web temizliği** (sonraki web yayını): Worker'daki `/e/` adresi ve manifestteki `media.archive` kaldırılır
   (1.0 artık kullanmıyor).
6. **Eski Java programın müşterileri** (sunucu yıl sonunda kapanıyor): Kurumsal Müzik Net'e hesap + 1.0.
