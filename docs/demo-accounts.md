# Demo hesapları (sadece yerel geliştirme)

`scripts/reset_and_seed_venues.sql` + `scripts/seed_demo.py` ile oluşturulur. Bu şifreler yalnızca yerel
demo verisi içindir; gerçek ortamda kullanılmaz. Seed yeniden çalıştırılınca bu hesaplar silinip yeniden oluşturulur.

## İşletme hesapları (Role = 2) — şifre: `Isletme!2026`

| Kullanıcı adı | İşletme / tesis | Şehir / ilçe | Sahalar |
|---|---|---|---|
| `isletme_yesilvadi` | Yeşil Vadi Spor Tesisleri | İstanbul / Kadıköy | Saha 1, Saha 2 (kapalı), Açık Saha |
| `isletme_besiktasarena` | Beşiktaş Arena Halı Saha | İstanbul / Beşiktaş | Çatı Sahası, Kapalı Salon |
| `isletme_atasehirpark` | Ataşehir Park Spor Kompleksi | İstanbul / Ataşehir | A Sahası, B Sahası, C Sahası (doğal çim) |
| `isletme_cankayagol` | Çankaya Gol Park | Ankara / Çankaya | Saha 1, Saha 2 (parke) |
| `isletme_bornovafm` | Bornova Futbol Merkezi | İzmir / Bornova | Saha 1 |

Üyelik: seed anından itibaren 1 yıl. Her tesiste akşam (18–24) fiyat kuralı, hafta sonu gündüz kuralı ve 1 aktif kampanya var.

## Demo oyuncular (Role = 0) — şifre: `Demo!2026`

`demo_ali`, `demo_burak`, `demo_cem`, `demo_deniz`, `demo_emre`, `demo_furkan`, `demo_gokhan`, `demo_hakan`,
`demo_ilker`, `demo_kaan`, `demo_levent`, `demo_mert`, `demo_onur`, `demo_serdar`, `demo_tolga`, `demo_umut`

Demo takımlar:
- **Kadıköy Kartalları:** ali, burak, cem, deniz, emre
- **Moda Spor:** furkan, gokhan, hakan, ilker, kaan
- **Anadolu Yıldızları:** levent, mert, onur, serdar, tolga

## Demo maçlar
- 12 gelecek maç: bir kısmı onay bekliyor, bir kısmı onaylı; 2 takım maçı (Kartallar - Moda Spor, Yıldızlar - Kartallar), 1 "rakip aranıyor" (Moda Spor).
- 6 geçmiş maç: skor, gol/asist, MVP oyları, oyuncu puanları, tesis yorumları ve bazı işletme yanıtları.

## Admin
Seed, geçici `seed_admin` hesabı kullanır ve sonunda siler. Mevcut admin hesabına dokunulmaz.
