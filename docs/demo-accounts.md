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

## Finans demosu (harici rezervasyon + giderler) — docs/venues-api.md "Ek paket"
`seed_demo.py` sonunda her demo tesise eklenir; sadece bu kısmı yeniden yüklemek için:
`python -I scripts/seed_demo.py --finance-only [API_BASE]` (demo tesislerin mevcut harici rezervasyonları ve giderleri
silinip aynı kurallarla, tesis adına göre sabit rastgelelikle yeniden eklenir).

- **Harici rezervasyonlar (SlotBlocks, Kind = 1):** tesis başına 6–10 adet, son 60 gün ile gelecek 14 güne yayılmış
  (en az 2 tanesi gelecekte). Fiyat `Owner/Quote` ile tesisin kendi fiyat kurallarından alınır. Gerçekçi Türkçe müşteri
  adları ve telefonları, bazılarında başlık ("Şirket maçı", "Doğum günü maçı"…) ve not ("Kapora alındı"…).
  Geçmişte kalanların ~%75'i ödenmiş, gelecektekilerin ~%30'u (kapora) ödenmiş; kalanı "tahsil edilmemiş" görünür.
  Demo maçlarla çakışan saatler atlanır.
- **Giderler (VenueExpenses):** bu ay ve önceki 2 ay için Kira (25–60 bin ₺), Personel (31–80 bin ₺), Elektrik (4–12 bin ₺),
  Su (0,7–3 bin ₺), ekim–mart arası Doğalgaz, Malzeme, çoğu ayda Bakım-onarım, bazı aylarda Reklam ve Vergi.
  Bu ayın giderleri bugüne kadarki günlere yazılır.

| Tesis | Harici rezervasyon | Gider kaydı (3 ay) |
|---|---|---|
| Yeşil Vadi Spor Tesisleri | 7 | 22 |
| Beşiktaş Arena Halı Saha | 9 | 22 |
| Ataşehir Park Spor Kompleksi | 8 | 20 |
| Çankaya Gol Park | 10 | 21 |
| Bornova Futbol Merkezi | 10 | 21 |

Not: Bu yükleme sırasında `isletme_besiktasarena` hesabının şifresi `Isletme!2026` değildi (sonradan değiştirilmiş);
şifreye dokunulmadı. `--finance-only` bu durumda fiyatı oyuncu `Venues/Quote` ile (aynı fiyat mantığı) hesaplar ve satırları
aynı çakışma kuralıyla SQL üzerinden ekler.

## Takım maçları (yeni kadro kuralı)
Takım maçlarında takım üyeleri artık davet yerine doğrudan kadroya eklenir (ev sahibi Side 1, rakip Side 2;
taraf kapasitesi ceil(UserCount/2), fazlası yedek). Yeniden seed'de bu `SaveMatch` / `ClaimOpponent` ile kendiliğinden olur.
Mevcut demo verisindeki 3 takım maçı tek seferlik SQL ile çevrildi (bildirim yazılmadı, takım davetleri ve davet bildirimleri silindi):
- Kartallar - Moda Spor: 5 + 5 Going
- Yıldızlar - Kartallar Rövanş: 5 + 5 Going
- Moda Spor rakip arıyor: 5 Going (ev sahibi)
