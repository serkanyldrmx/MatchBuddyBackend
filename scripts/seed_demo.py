"""
Anlaşmalı tesis paketi demo verisi (docs/venues-api.md "Veri sıfırlama ve demo verisi").

Önce:  scripts/reset_and_seed_venues.sql  (yedek alındıktan sonra) çalıştırılır.
Sonra: python -I scripts/seed_demo.py [API_BASE]   (varsayılan http://localhost:5099/api)

Veriler gerçek API üzerinden eklenir (şifreler hash'lenir, iş kuralları çalışır):
  - geçici admin `seed_admin` (kayıt + SQL ile Role=1) -> sonunda silinir
  - 5 işletme + tesis (CreateOwner, fotoğraflar MatchBuddyReact git geçmişinden)
  - işletme girişleriyle: tesis bilgileri, sahalar, açılış saatleri, fiyat kuralları, kampanyalar
  - 16 demo oyuncu (demo_<ad>, şifre Demo!2026), 3 demo takım
  - 12 gelecek maç (bekleyen / onaylı, 2 takım maçı, 1 "rakip aranıyor") ve
    6 geçmiş maç (skor, MVP oyları, puanlar, tesis yorumları + işletme yanıtları)
Geçmiş maçlar: gelecek bir saate kurulur, onaylanır, sonra tarihleri SQL ile geçmişe taşınır
(SaveMatch geçmiş saat kabul etmez).

Hesaplar docs/demo-accounts.md dosyasındadır.
"""
import sys

sys.stdout.reconfigure(encoding="utf-8")

import json
import os
import random
import subprocess
import tempfile
from datetime import datetime, timedelta

import requests

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5099/api"
FRONTEND_REPO = r"D:\Project\repos\MatchBuddyReact"
OWNER_PASSWORD = "Isletme!2026"
DEMO_PASSWORD = "Demo!2026"
ADMIN_USER = "seed_admin"
ADMIN_PASSWORD = "SeedAdmin!2026"
random.seed(2026)


# ---------------------------------------------------------------- yardımcılar

def sql(query):
    ps = r"""
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
$c = New-Object System.Data.SqlClient.SqlConnection 'Server=(localdb)\mssqllocaldb;Database=MatchBuddy;Trusted_Connection=true'
$c.Open()
$cmd = $c.CreateCommand(); $cmd.CommandText = [Console]::In.ReadToEnd()
$da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
$t = New-Object System.Data.DataTable
[void]$da.Fill($t)
$rows = @()
foreach ($r in $t.Rows) { $h = @{}; foreach ($col in $t.Columns) { $v = $r[$col]; if ($v -is [DBNull]) { $v = $null } elseif ($v -is [DateTime]) { $v = $v.ToString('o') }; $h[$col.ColumnName] = $v }; $rows += ,$h }
ConvertTo-Json -InputObject @($rows) -Depth 3 -Compress
$c.Close()
"""
    p = subprocess.run(["powershell", "-NoProfile", "-Command", ps], input=query, capture_output=True, text=True, encoding="utf-8")
    if p.returncode != 0:
        raise RuntimeError(p.stderr)
    out = p.stdout.strip()
    if not out:
        return []
    data = json.loads(out)
    return data if isinstance(data, list) else [data]


def H(tok):
    return {"Authorization": f"Bearer {tok}"}


def ok(r, what):
    if r.status_code != 200:
        raise RuntimeError(f"{what}: {r.status_code} {r.text[:500]}")
    return r.json() if r.text else None


def get(path, tok, **params):
    return ok(requests.get(f"{BASE}/{path}", headers=H(tok), params=params, timeout=60), path)


def post(path, tok, body=None, what=None):
    return ok(requests.post(f"{BASE}/{path}", headers=H(tok), json=body, timeout=60), what or path)


def register(user, name, surname, password, phone="5550000000"):
    model = dict(playerName=name, playerSurname=surname, userName=user, password=password,
                 email=f"{user}@matchbuddy.local", phoneNumber=phone, address="Türkiye",
                 size=random.randint(168, 192), weight=random.randint(62, 92), age=random.randint(19, 38))
    r = requests.post(f"{BASE}/Players/SavePlayer", files={"playerModel": (None, json.dumps(model))}, timeout=30)
    ok(r, f"register {user}")


def login(user, password):
    d = ok(requests.post(f"{BASE}/Players/PlayerLogin", json={"userName": user, "password": password}, timeout=30), f"login {user}")
    return d["token"], d["player"]["playerId"]


def extract_images():
    names = ["stadyum.jpg", "stadyum2.jpg", "stadyum4.jpg", "stadyum5.jpg", "stadyumm.jpg",
             "foto1.jpg", "foto2.jpg", "foto3.jpg", "banner-bg.jpg", "top1.jpg"]
    folder = tempfile.mkdtemp(prefix="mb_seed_img_")
    files = []
    for n in names:
        p = subprocess.run(["git", "-C", FRONTEND_REPO, "show", f"HEAD:src/images/{n}"], capture_output=True)
        if p.returncode == 0 and p.stdout[:3] == b"\xff\xd8\xff":
            path = os.path.join(folder, n)
            with open(path, "wb") as f:
                f.write(p.stdout)
            files.append(path)
    if not files:
        raise RuntimeError("MatchBuddyReact git geçmişinde stadyum görseli bulunamadı")
    print(f"{len(files)} görsel çıkarıldı: {', '.join(os.path.basename(f) for f in files)}")
    return files


def at(days, hour, minute=0):
    d = (datetime.now() + timedelta(days=days)).replace(hour=hour, minute=minute, second=0, microsecond=0)
    return d


def iso(d):
    return d.strftime("%Y-%m-%dT%H:%M:00")


# ---------------------------------------------------------------- veri

VENUES = [
    dict(user="isletme_yesilvadi", owner="Yeşil Vadi Spor Tesisleri", contact="Murat Aksoy", phone="05321112233",
         name="Yeşil Vadi Spor Tesisleri", city="İstanbul", district="Kadıköy",
         address="Fikirtepe Mah. Dumlupınar Sok. No:12, Kadıköy/İstanbul", location="40.993500, 29.046100",
         vphone="02163334455", desc="Kadıköy'ün kalbinde iki kapalı ve bir açık halı saha. Maç sonrası çay bizden!",
         policy="Maç saatinden 24 saat öncesine kadar ücretsiz iptal. Daha geç iptallerde saha ücretinin %50'si alınır.",
         amen=dict(cleatRental=True, cleatRentalPrice=100, hasCamera=True, cameraPrice=300, hasShower=True, hasLockerRoom=True,
                   hasParking=False, hasCafe=True, hasShuttle=False, providesBibs=True, providesBall=True),
         pitches=[("Saha 1", 1, True, 7, 1500), ("Saha 2", 1, True, 7, 1400), ("Açık Saha", 1, False, 8, 1300)],
         hours=("09:00", "02:00"), evening=2100,
         campaign=dict(title="Hafta İçi Öğlen İndirimi", description="Pazartesi–Cuma 12:00–16:00 arası %25 indirim.",
                       daysMask=31, fromHour=12, toHour=16, discountPercent=25, fixedPrice=None)),
    dict(user="isletme_besiktasarena", owner="Beşiktaş Arena Halı Saha", contact="Selin Karaca", phone="05334445566",
         name="Beşiktaş Arena Halı Saha", city="İstanbul", district="Beşiktaş",
         address="Dikilitaş Mah. Yenidoğan Sok. No:8, Beşiktaş/İstanbul", location="41.048000, 29.006000",
         vphone="02125556677", desc="Boğaz manzaralı çatı katı sahası ve kapalı salon. Profesyonel aydınlatma.",
         policy="12 saat öncesine kadar ücretsiz iptal.",
         amen=dict(cleatRental=True, cleatRentalPrice=120, hasCamera=True, cameraPrice=400, hasShower=True, hasLockerRoom=True,
                   hasParking=True, hasCafe=False, hasShuttle=True, providesBibs=True, providesBall=True),
         pitches=[("Çatı Sahası", 1, False, 7, 1800), ("Kapalı Salon", 1, True, 6, 1600)],
         hours=("10:00", "00:00"), evening=2400,
         campaign=dict(title="Sabah Erken Maç", description="Hafta sonu 10:00–13:00 arası sabit 1.200 ₺.",
                       daysMask=96, fromHour=10, toHour=13, discountPercent=None, fixedPrice=1200)),
    dict(user="isletme_atasehirpark", owner="Ataşehir Park Spor Kompleksi", contact="Kerem Yalın", phone="05357778899",
         name="Ataşehir Park Spor Kompleksi", city="İstanbul", district="Ataşehir",
         address="Barbaros Mah. Begonya Sok. No:3, Ataşehir/İstanbul", location="40.992300, 29.124400",
         vphone="02164441122", desc="Üç sahalı kompleks, geniş otopark ve servis imkânı.",
         policy="İptaller en geç 6 saat önce yapılmalıdır.",
         amen=dict(cleatRental=False, cleatRentalPrice=None, hasCamera=True, cameraPrice=250, hasShower=True, hasLockerRoom=True,
                   hasParking=True, hasCafe=True, hasShuttle=True, providesBibs=True, providesBall=False),
         pitches=[("A Sahası", 1, True, 7, 1450), ("B Sahası", 1, True, 7, 1450), ("C Sahası", 2, False, 10, 2200)],
         hours=("08:00", "01:00"), evening=1900,
         campaign=dict(title="Salı Gecesi Kampanyası", description="Salı 22:00 sonrası %20 indirim.",
                       daysMask=2, fromHour=22, toHour=24, discountPercent=20, fixedPrice=None)),
    dict(user="isletme_cankayagol", owner="Çankaya Gol Park", contact="Aylin Demirtaş", phone="05061231234",
         name="Çankaya Gol Park", city="Ankara", district="Çankaya",
         address="Kızılırmak Mah. 1443. Cad. No:22, Çankaya/Ankara", location="39.908600, 32.810600",
         vphone="03124567890", desc="Ankara'nın en yeni kapalı halı sahaları. Isıtmalı soyunma odaları.",
         policy="24 saat öncesine kadar ücretsiz iptal.",
         amen=dict(cleatRental=True, cleatRentalPrice=80, hasCamera=False, cameraPrice=None, hasShower=True, hasLockerRoom=True,
                   hasParking=True, hasCafe=False, hasShuttle=False, providesBibs=False, providesBall=True),
         pitches=[("Saha 1", 1, True, 7, 1200), ("Saha 2", 3, True, 5, 1000)],
         hours=("09:00", "00:00"), evening=1600,
         campaign=dict(title="Öğrenci Saatleri", description="Hafta içi 14:00–17:00 arası %30 indirim.",
                       daysMask=31, fromHour=14, toHour=17, discountPercent=30, fixedPrice=None)),
    dict(user="isletme_bornovafm", owner="Bornova Futbol Merkezi", contact="Can Ertürk", phone="05427896541",
         name="Bornova Futbol Merkezi", city="İzmir", district="Bornova",
         address="Kazımdirik Mah. 372. Sok. No:15, Bornova/İzmir", location="38.456000, 27.214000",
         vphone="02323456789", desc="Ege Üniversitesi'ne yürüme mesafesinde, açık ve kapalı sahalar.",
         policy="Rezervasyondan 48 saat öncesine kadar ücretsiz iptal.",
         amen=dict(cleatRental=True, cleatRentalPrice=90, hasCamera=True, cameraPrice=200, hasShower=False, hasLockerRoom=True,
                   hasParking=False, hasCafe=True, hasShuttle=False, providesBibs=True, providesBall=True),
         pitches=[("Saha 1", 1, False, 7, 1100)],
         hours=("10:00", "01:00"), evening=1500,
         campaign=dict(title="Pazar Akşamı Fırsatı", description="Pazar 20:00 sonrası saatlik 1.000 ₺.",
                       daysMask=64, fromHour=20, toHour=24, discountPercent=None, fixedPrice=1000)),
]

DEMO_PLAYERS = [
    ("ali", "Ali", "Yılmaz"), ("burak", "Burak", "Kaya"), ("cem", "Cem", "Demir"), ("deniz", "Deniz", "Şahin"),
    ("emre", "Emre", "Çelik"), ("furkan", "Furkan", "Arslan"), ("gokhan", "Gökhan", "Öztürk"), ("hakan", "Hakan", "Aydın"),
    ("ilker", "İlker", "Doğan"), ("kaan", "Kaan", "Kılıç"), ("levent", "Levent", "Aslan"), ("mert", "Mert", "Koç"),
    ("onur", "Onur", "Kurt"), ("serdar", "Serdar", "Özdemir"), ("tolga", "Tolga", "Polat"), ("umut", "Umut", "Erdem"),
]

TEAMS = [
    ("Kadıköy Kartalları", ["ali", "burak", "cem", "deniz", "emre"]),
    ("Moda Spor", ["furkan", "gokhan", "hakan", "ilker", "kaan"]),
    ("Anadolu Yıldızları", ["levent", "mert", "onur", "serdar", "tolga"]),
]

REVIEWS = [
    (5, "Zemin harika, soyunma odaları tertemiz. Kesinlikle tekrar geleceğiz."),
    (4, "Saha güzel ama otopark sıkıntılı."),
    (5, "Işıklandırma çok iyi, personel ilgili."),
    (3, "Fiyat biraz yüksek, yine de saha bakımlı."),
    (4, "Krampon kiralama işimizi gördü, teşekkürler."),
    (5, "Maç kaydı hizmeti süper, golleri izledik!"),
]
REPLIES = ["Güzel yorumunuz için teşekkür ederiz, yine bekleriz!", "Geri bildiriminiz için teşekkürler, otopark için çalışıyoruz."]


def main():
    print(f"API: {BASE}")
    images = extract_images()

    # ---------------- geçici admin
    sql(f"DELETE FROM Players WHERE UserName = '{ADMIN_USER}'")
    register(ADMIN_USER, "Seed", "Admin", ADMIN_PASSWORD)
    sql(f"UPDATE Players SET Role = 1, IsAdmin = 1 WHERE UserName = '{ADMIN_USER}'")
    admin_tok, admin_id = login(ADMIN_USER, ADMIN_PASSWORD)

    try:
        # ---------------- demo oyuncular
        players = {}
        for i, (key, name, surname) in enumerate(DEMO_PLAYERS):
            user = f"demo_{key}"
            register(user, name, surname, DEMO_PASSWORD, phone=f"0555{1000000 + i * 1111:07d}")
            tok, pid = login(user, DEMO_PASSWORD)
            players[key] = dict(user=user, tok=tok, id=pid, name=f"{name} {surname}")
        print(f"{len(players)} demo oyuncu")

        # ---------------- işletmeler + tesisler
        venues = []
        img_i = 0
        for v in VENUES:
            photos = []
            for _ in range(2):
                path = images[img_i % len(images)]
                img_i += 1
                photos.append(("photos", (os.path.basename(path), open(path, "rb").read(), "image/jpeg")))
            model = dict(owner=dict(name=v["owner"], contactName=v["contact"], userName=v["user"], email=f"{v['user']}@matchbuddy.local",
                                    phoneNumber=v["phone"], password=OWNER_PASSWORD,
                                    membershipUntil=(datetime.now() + timedelta(days=365)).strftime("%Y-%m-%dT00:00:00")),
                         venue=dict(name=v["name"], city=v["city"], district=v["district"], address=v["address"],
                                    location=v["location"], phone=v["vphone"], description=v["desc"]))
            r = requests.post(f"{BASE}/Admin/CreateOwner", headers=H(admin_tok), data={"model": json.dumps(model)}, files=photos, timeout=60)
            d = ok(r, f"CreateOwner {v['user']}")
            tok, oid = login(v["user"], OWNER_PASSWORD)
            sid = d["stadiumId"]

            # tesis bilgileri ve olanaklar
            body = dict(stadiumId=sid, name=v["name"], description=v["desc"], phone=v["vphone"], address=v["address"],
                        city=v["city"], district=v["district"], location=v["location"], cancellationPolicy=v["policy"], **v["amen"])
            post("Owner/Venue/Update", tok, body)
            # 3. fotoğraf
            path = images[img_i % len(images)]
            img_i += 1
            ok(requests.post(f"{BASE}/Owner/Photos/Upload", headers=H(tok), data={"stadiumId": str(sid)},
                             files={"file": (os.path.basename(path), open(path, "rb").read(), "image/jpeg")}, timeout=60), "Photos/Upload")

            # sahalar: varsayılan "Saha 1" güncellenir, diğerleri eklenir
            existing = get("Owner/Pitches", tok, stadiumId=sid)
            pitch_ids = []
            for i, (pname, surface, indoor, pps, price) in enumerate(v["pitches"]):
                b = dict(stadiumId=sid, name=pname, surface=surface, isIndoor=indoor, playersPerSide=pps, basePrice=price, isActive=True)
                if i == 0:
                    b["pitchId"] = existing[0]["pitchId"]
                pitch_ids.append(post("Owner/Pitches/Save", tok, b)["pitchId"])

            # açılış saatleri (Pazartesi kapalı olan yok; pazar bir saat erken kapanır)
            open_t, close_t = v["hours"]
            days = [dict(dayOfWeek=d, isClosed=False, openTime=open_t, closeTime=close_t if d != 6 else "23:00") for d in range(7)]
            post("Owner/OpeningHours", tok, dict(stadiumId=sid, days=days))

            # akşam fiyat kuralı (her gün 18–24) + hafta sonu gündüz kuralı
            post("Owner/PriceRules/Save", tok, dict(stadiumId=sid, pitchId=None, daysMask=127, fromHour=18, toHour=24, pricePerHour=v["evening"]))
            post("Owner/PriceRules/Save", tok, dict(stadiumId=sid, pitchId=None, daysMask=96, fromHour=12, toHour=18,
                                                    pricePerHour=v["pitches"][0][4] + 200))
            # kampanya
            c = v["campaign"]
            post("Owner/Campaigns/Save", tok, dict(stadiumId=sid, pitchId=None, title=c["title"], description=c["description"],
                                                   startDate=datetime.now().strftime("%Y-%m-%d"),
                                                   endDate=(datetime.now() + timedelta(days=60)).strftime("%Y-%m-%d"),
                                                   daysMask=c["daysMask"], fromHour=c["fromHour"], toHour=c["toHour"],
                                                   discountPercent=c["discountPercent"], fixedPrice=c["fixedPrice"], isActive=True))
            venues.append(dict(v=v, tok=tok, owner_id=oid, sid=sid, pitches=pitch_ids))
            print(f"tesis {sid}: {v['name']} ({len(pitch_ids)} saha)")

        listed = get("Venues/List", players["ali"]["tok"])
        assert len(listed) == 5, f"5 tesis görünür olmalı, {len(listed)} görünüyor"

        # ---------------- takımlar
        teams = {}
        for tname, members in TEAMS:
            creator = players[members[0]]
            post("Team/SaveTeam", creator["tok"], dict(teamName=tname, playerId=[players[m]["id"] for m in members]))
            tid = sql(f"SELECT TOP 1 TeamId FROM Teams t WHERE EXISTS (SELECT 1 FROM PlayerTeam pt WHERE pt.TeamId = t.TeamId AND pt.PlayerId = {creator['id']}) ORDER BY TeamId DESC")[0]["TeamId"]
            teams[tname] = dict(id=tid, members=members)
        print(f"{len(teams)} demo takım")

        keys = list(players.keys())

        def create(creator_key, venue, pitch_idx, when, name, users=10, dur=60, cleats=0, camera=False,
                   home=None, away=None, lfo=False):
            v = venue["v"]
            body = dict(matchName=name, description=f"{v['name']} - demo maç", userCount=users,
                        pitchId=venue["pitches"][pitch_idx], matchDate=iso(when), durationMinutes=dur,
                        cleatCount=cleats if v["amen"]["cleatRental"] else 0,
                        cameraRequested=camera and v["amen"]["hasCamera"],
                        homeTeamId=teams[home]["id"] if home else None, awayTeamId=teams[away]["id"] if away else None,
                        lookingForOpponent=lfo)
            return post("Match/SaveMatch", players[creator_key]["tok"], body, f"SaveMatch {name}")["matchId"]

        def join(mid, who):
            for k in who:
                r = requests.post(f"{BASE}/Match/Join", headers=H(players[k]["tok"]), json={"matchId": mid}, timeout=30)
                if r.status_code != 200:
                    print(f"  join uyarı {k} -> {mid}: {r.text[:120]}")

        def respond(venue, mid, approve, reason=None):
            post("Owner/RespondReservation", venue["tok"], dict(matchId=mid, approve=approve, reason=reason))

        def accept_invites(mid, who):
            for k in who:
                inv = get("Invites/My", players[k]["tok"])
                for i in inv.get("matchInvites", []):
                    if i["match"]["matchId"] == mid:
                        post("Invites/Respond", players[k]["tok"], dict(kind="match", inviteId=i["inviteId"], accept=True))

        # ---------------- 12 gelecek maç
        future = []
        plan = [
            # (kurucu, tesis, saha, gün, saat, dk, ad, onay, katılımcı sayısı)
            ("ali", 0, 0, 1, 20, 60, "Salı Akşamı Klasiği", True, 9),
            ("burak", 0, 1, 2, 21, 90, "Kadıköy Derbisi", True, 11),
            ("cem", 1, 0, 2, 19, 60, "Çatıda Maç Keyfi", False, 5),
            ("deniz", 1, 1, 3, 20, 60, "Ofis Ligi Haftası", True, 8),
            ("emre", 2, 0, 3, 21, 60, "Ataşehir Gece Maçı", False, 4),
            ("furkan", 2, 2, 4, 19, 120, "Büyük Saha Turnuvası", True, 12),
            ("gokhan", 3, 0, 4, 20, 60, "Ankara Akşam Maçı", False, 6),
            ("hakan", 3, 1, 5, 18, 60, "Parke Futsal", True, 7),
            ("ilker", 4, 0, 5, 20, 90, "İzmir Haftasonu Maçı", False, 3),
        ]
        for creator, vi, pi, day, hour, dur, name, approve, n in plan:
            venue = venues[vi]
            when = at(day, hour)
            mid = create(creator, venue, pi, when, name, users=14 if venue["v"]["pitches"][pi][3] >= 7 else 10, dur=dur,
                         cleats=random.choice([0, 2, 3]), camera=random.random() < 0.5)
            others = [k for k in keys if k != creator]
            random.shuffle(others)
            join(mid, others[:n])
            if approve:
                respond(venue, mid, True)
            future.append(mid)
            print(f"gelecek maç {mid}: {name} ({'onaylı' if approve else 'bekliyor'})")

        # 2 takım maçı
        mid = create("ali", venues[0], 0, at(7, 21), "Kartallar - Moda Spor", users=14, home="Kadıköy Kartalları", away="Moda Spor", camera=True)
        accept_invites(mid, ["burak", "cem", "deniz", "furkan", "gokhan", "hakan"])
        respond(venues[0], mid, True)
        future.append(mid)
        mid = create("levent", venues[2], 1, at(8, 20), "Yıldızlar - Kartallar Rövanş", users=14, home="Anadolu Yıldızları", away="Kadıköy Kartalları")
        accept_invites(mid, ["mert", "onur", "emre"])
        future.append(mid)
        # 1 rakip aranıyor
        mid = create("furkan", venues[1], 0, at(9, 21), "Moda Spor rakip arıyor", users=14, home="Moda Spor", lfo=True)
        accept_invites(mid, ["gokhan", "kaan"])
        respond(venues[1], mid, True)
        future.append(mid)
        print(f"{len(future)} gelecek maç")

        # ---------------- 6 geçmiş maç
        past = []
        past_plan = [(0, 0, 1, "ali"), (0, 1, 3, "burak"), (1, 0, 4, "cem"), (2, 0, 6, "deniz"), (3, 0, 8, "emre"), (4, 0, 10, "furkan")]
        for i, (vi, pi, days_ago, creator) in enumerate(past_plan):
            venue = venues[vi]
            tmp = at(30 + i, 20)
            mid = create(creator, venue, pi, tmp, f"Geçen Haftanın Maçı #{i + 1}", users=10, cleats=2, camera=(i % 2 == 0))
            squad = [k for k in keys if k != creator]
            random.shuffle(squad)
            squad = squad[:7]
            join(mid, squad)
            respond(venue, mid, True)
            real = at(-days_ago, 20)
            sql(f"UPDATE Matchs SET MatchDate = '{real.strftime('%Y-%m-%dT%H:%M:%S')}', CreatedAt = '{(real - timedelta(days=5)).strftime('%Y-%m-%dT%H:%M:%S')}' WHERE MatchId = {mid}")
            members = [creator] + squad
            hs, as_ = random.randint(3, 9), random.randint(2, 8)
            stats = [dict(playerId=players[k]["id"], goals=random.randint(0, 3), assists=random.randint(0, 2)) for k in members]
            post("Match/SubmitResult", players[creator]["tok"], dict(matchId=mid, homeScore=hs, awayScore=as_, stats=stats))
            star = random.choice(members)
            for k in members:
                target = star if k != star else members[0] if star != members[0] else members[1]
                post("Match/VoteMvp", players[k]["tok"], dict(matchId=mid, votedPlayerId=players[target]["id"]))
                rated = [m for m in members if m != k][:4]
                post("Match/RatePlayers", players[k]["tok"], dict(matchId=mid, ratings=[dict(playerId=players[m]["id"], score=random.randint(3, 5)) for m in rated]))
            for j, k in enumerate(members[:3]):
                rating, comment = REVIEWS[(i + j) % len(REVIEWS)]
                post("Venues/Review", players[k]["tok"], dict(matchId=mid, rating=rating, comment=comment))
            past.append(mid)
            print(f"geçmiş maç {mid}: {hs}-{as_}")

        # işletme yanıtları
        for venue in venues[:3]:
            reviews = get("Owner/Reviews", venue["tok"], stadiumId=venue["sid"])
            for j, r in enumerate(reviews[:2]):
                post("Owner/Reviews/Reply", venue["tok"], dict(reviewId=r["reviewId"], reply=REPLIES[j % len(REPLIES)]))

        print("\n==== ÖZET ====")
        print(f"tesis: {len(venues)}, işletme: {len(venues)}, demo oyuncu: {len(players)}, takım: {len(teams)}, "
              f"gelecek maç: {len(future)}, geçmiş maç: {len(past)}")
        for venue in venues:
            print(f"  {venue['sid']}: {venue['v']['name']} — {venue['v']['user']}")
    finally:
        # geçici admin silinir
        sql(f"""
DECLARE @id int = (SELECT PlayerId FROM Players WHERE UserName = '{ADMIN_USER}');
DELETE FROM PlayerNotification WHERE PlayerId = @id;
DELETE FROM PlayerSettings WHERE PlayerId = @id;
DELETE FROM Activities WHERE ActorPlayerId = @id OR TargetPlayerId = @id;
DELETE FROM Players WHERE PlayerId = @id;
SELECT 1 AS ok""")
        print("seed_admin silindi")


if __name__ == "__main__":
    main()
