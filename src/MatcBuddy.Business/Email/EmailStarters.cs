using MatcBuddy.Entities.DTOs;

namespace MatcBuddy.Business.Email
{
    /// <summary>
    /// Hazır şablon galerisi (veritabanına yazılmaz): yönetici ve işletme ekranında "Hazır şablonlar"dan seçilip
    /// kendi şablonu olarak kaydedilir ya da doğrudan gönderime doldurulur.
    /// </summary>
    public static class EmailStarters
    {
        private static EmailStarterDto S(string key, string category, string name, string subject, string body, string? buttonText,
            string? buttonUrl) => new()
        {
            Key = key,
            Category = category,
            Name = name,
            Subject = subject,
            Body = body,
            ButtonText = buttonText,
            ButtonUrl = buttonUrl,
        };

        public static readonly IReadOnlyList<EmailStarterDto> Admin = new[]
        {
            S("weekend_matches", "matches", "Hafta sonu eksik kadrolar",
                "{{ad}}, hafta sonu seni bekleyen maçlar var ⚽",
                "# Hafta sonu sahaya çıkıyor musun?\n\nMerhaba {{ad}},\n\nBu hafta sonu yakınında **oyuncu arayan maçlar** var. Kadrosu eksik bir maça katıl, yeni takım arkadaşları edin.\n\n- Mevkini seç, boş yere tek dokunuşla katıl\n- Maçtan önce hatırlatma e-postası al\n- Maç sonrası oyuncuları puanla\n\n> İpucu: Maç sayfasında \"E-posta hatırlatıcı\"yı açarsan maçı unutmazsın.",
                "Eksik kadroları gör", "{{site}}/home"),
            S("come_back", "engagement", "Seni özledik (geri dönüş)",
                "{{ad}}, sahalar seni özledi",
                "# Top seni bekliyor, {{ad}}\n\nBir süredir maç yapmadın. Bu hafta yeniden sahaya çıkmaya ne dersin?\n\n- Yakınındaki açık maçlara katıl\n- Arkadaşlarını topla, kendi maçını kur\n- Anlaşmalı sahalardan uygun saat ayırt\n\nSeni yeniden sahada görmek için sabırsızlanıyoruz.",
                "Maç bul", "{{site}}/home"),
            S("new_feature", "announcement", "Yeni özellik duyurusu",
                "MatcBuddy'de yeni: [özellik adı]",
                "# Yeni: [özellik adı]\n\nMerhaba {{ad}},\n\nMatcBuddy'yi senin için daha iyi hale getirmeye devam ediyoruz. Bu güncellemeyle gelenler:\n\n- [Yenilik 1]\n- [Yenilik 2]\n- [Yenilik 3]\n\n> Uygulamayı yenilemen yeterli, yenilikler hemen açılıyor.\n\nGörüşlerini bize her zaman yazabilirsin.",
                "Hemen dene", "{{site}}/home"),
            S("tournament", "matches", "Turnuva duyurusu",
                "🏆 [Turnuva adı] kayıtları başladı",
                "# [Turnuva adı] başlıyor!\n\nMerhaba {{ad}},\n\nTakımını topla, kupayı kaldır. Kayıtlar açıldı, kontenjan sınırlı.\n\n| Başlangıç: [tarih]\n| Yer: [saha]\n| Format: [6'ya 6, grup + eleme]\n| Ödül: [ödül]\n\nTakımın hazır değilse turnuva sayfasından oyuncu da bulabilirsin.",
                "Takımını kaydet", "{{site}}/tournaments"),
            S("invite_friends", "engagement", "Arkadaşını davet et",
                "Kadroyu birlikte tamamlayalım, {{ad}}",
                "# Arkadaşlarını getir\n\nMerhaba {{ad}},\n\nMaçlar kalabalıkla güzel. Arkadaşlarını MatcBuddy'ye davet et, maç kurduğunda tek dokunuşla kadroya çağır.\n\n- Takım oluştur, arkadaşlarını ekle\n- Maçı paylaş, bağlantıyla katılsınlar\n- Kim geliyor, kim yedek, hepsi tek ekranda",
                "Takımımı kur", "{{site}}/home"),
            S("complete_profile", "engagement", "Profilini tamamla",
                "{{ad}}, profilin neredeyse hazır",
                "# Profilini tamamla\n\nMerhaba {{ad}},\n\nTam bir profil, maç yöneticilerinin seni kadroya daha kolay almasını sağlar.\n\n- Fotoğraf ekle\n- Mevkini ve ayağını seç\n- Oynadığın şehri ve bölgeyi yaz\n\nBirkaç dakikanı alır, sahada fark yaratır.",
                "Profilimi düzenle", "{{site}}/userUpdate"),
            S("maintenance", "service", "Bakım / hizmet duyurusu",
                "Planlı bakım: [tarih] [saat]",
                "# Kısa bir bakım yapıyoruz\n\nMerhaba {{ad}},\n\nDaha hızlı ve sorunsuz bir MatcBuddy için planlı bakım yapacağız.\n\n| Tarih: [tarih]\n| Saat: [başlangıç] – [bitiş]\n| Etkisi: Bu sürede uygulamaya erişilemeyebilir\n\nMaçların ve rezervasyonların etkilenmez. Anlayışın için teşekkürler.",
                null, null),
            S("season_recap", "engagement", "Ay sonu / sezon özeti",
                "{{ad}}, bu ay sahada neler oldu?",
                "# Bu ay MatcBuddy'de\n\nMerhaba {{ad}},\n\nBu ay topluluk olarak sahada harika zaman geçirdik:\n\n| Oynanan maç: [sayı]\n| Yeni oyuncu: [sayı]\n| En çok maç yapılan saha: [saha]\n\nGelecek ay seni daha çok sahada görmek istiyoruz!",
                "Maçlara göz at", "{{site}}/home"),
            S("owners_news", "business", "İşletmelere duyuru",
                "İşletme paneline yeni özellikler geldi",
                "# İşletme panelinde yenilikler\n\nMerhaba {{ad}},\n\nTesisini yönetmeyi kolaylaştıran yeni özellikler ekledik:\n\n- [Yenilik 1]\n- [Yenilik 2]\n- [Yenilik 3]\n\n> Sorun ya da önerin varsa bize yanıt vermen yeterli.",
                "İşletme paneline git", "{{site}}/venue"),
        };

        public static readonly IReadOnlyList<EmailStarterDto> Owner = new[]
        {
            S("owner_discount", "campaign", "İndirim / kampanya",
                "Bu hafta %[oran] indirim · {{tesis}}",
                "# Bu hafta sahada indirim var!\n\nMerhaba {{ad}},\n\n**{{tesis}}** tesisinde seçili saatlerde indirim başladı. Takımını topla, uygun fiyata maçını yap.\n\n| İndirim: %[oran]\n| Geçerli saatler: [örn. hafta içi 14:00–18:00]\n| Son gün: [tarih]\n\n> Rezervasyonunu MatcBuddy üzerinden yap, indirim otomatik uygulansın.",
                "Saat ayırt", "{{site}}/venues"),
            S("owner_free_slots", "campaign", "Boş saatler",
                "{{tesis}}: bu akşam boş saatlerimiz var",
                "# Bu akşam saha boş!\n\nMerhaba {{ad}},\n\n**{{tesis}}** tesisinde bu akşam şu saatler hâlâ boş:\n\n- [20:00 – 21:00]\n- [22:00 – 23:00]\n\nİlk gelen alır. Hemen ayırt, kadroyu MatcBuddy'de tamamla.",
                "Boş saatleri gör", "{{site}}/venues"),
            S("owner_league", "events", "Lig / turnuva kaydı",
                "{{tesis}} [Turnuva adı] kayıtları açıldı",
                "# {{tesis}} turnuvası başlıyor\n\nMerhaba {{ad}},\n\nTesisimizde yeni turnuva düzenliyoruz. Takımını kaydet, kupa için mücadele et!\n\n| Başlangıç: [tarih]\n| Format: [6'ya 6]\n| Katılım: [ücret / takım]\n| Ödül: [ödül]\n\nKontenjan sınırlı, kayıt için bize yaz ya da tesise uğra.",
                null, null),
            S("owner_new_pitch", "news", "Yeni saha / yenileme",
                "{{tesis}} yenilendi, seni bekliyoruz",
                "# Yenilendik!\n\nMerhaba {{ad}},\n\n**{{tesis}}** tesisinde sizin için yenilikler yaptık:\n\n- [Yeni çim / yeni saha]\n- [Yenilenen soyunma odaları ve duşlar]\n- [Aydınlatma / otopark]\n\nYeni haliyle ilk maçını yapmak için hemen saat ayırt.",
                "Saat ayırt", "{{site}}/venues"),
            S("owner_holiday", "news", "Özel gün çalışma saatleri",
                "{{tesis}} bayram çalışma saatleri",
                "# Bayram çalışma saatlerimiz\n\nMerhaba {{ad}},\n\nBayram boyunca **{{tesis}}** şu saatlerde açık:\n\n| [1. gün]: [saat aralığı]\n| [2. gün]: [saat aralığı]\n| [3. gün]: [saat aralığı]\n\nİyi bayramlar, sahada görüşmek üzere!",
                null, null),
            S("owner_thanks", "loyalty", "Teşekkür / sadık müşteri",
                "Teşekkürler {{ad}}, sana özel bir sürprizimiz var",
                "# Bizi tercih ettiğin için teşekkürler\n\nMerhaba {{ad}},\n\n**{{tesis}}** olarak bu yıl bizimle oynadığın her maç için teşekkür ederiz.\n\n> Sana özel: Bir sonraki rezervasyonunda [ikram / indirim]. Rezervasyonda bu e-postayı göstermen yeterli.\n\nSahada görüşmek üzere!",
                "Saat ayırt", "{{site}}/venues"),
            S("owner_weather", "news", "Kapalı saha / hava durumu",
                "Hava ne olursa olsun maç devam · {{tesis}}",
                "# Yağmur maçı durdurmasın\n\nMerhaba {{ad}},\n\nHavalar soğudu ama maç devam! **{{tesis}}** tesisinde kapalı ve ısıtmalı sahamız hazır.\n\n- Kapalı, ısıtmalı saha\n- Sıcak duş ve soyunma odası\n- Maç sonrası sıcak içecek\n\nKış saatlerini kaçırmadan yerini ayırt.",
                "Saat ayırt", "{{site}}/venues"),
        };

        public static IReadOnlyList<EmailStarterDto> For(bool owner) => owner ? Owner : Admin;
    }
}
