-- =====================================================================================
-- Anlaşmalı tesis paketi (docs/venues-api.md): veri sıfırlama. Yeniden çalıştırılabilir.
--
-- ÖNCE YEDEK ALIN:
--   BACKUP DATABASE [MatcBuddy] TO DISK = N'<klasör>\MatcBuddy_before_venues_<zaman>.bak' WITH INIT
--
-- Silinenler: tüm maçlar ve bağlı satırları, maç / tesis bildirimleri, turnuvalar, tüm tesisler
-- (ve tesis tabloları). Ayrıca daha önceki seed çalıştırmalarının demo hesapları (isletme_*, demo_*,
-- seed_admin) ve bu hesapların takım / takip / mesaj kayıtları silinir.
-- Korunanlar: gerçek oyuncular, takımlar, takipler, mesajlar, gruplar.
--
-- Ardından demo veri API üzerinden eklenir:  scripts/seed_demo.py
-- =====================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ---- Bildirimler: maça ya da tesise bağlı olanlar
DELETE pn FROM PlayerNotification pn
  JOIN Notification n ON n.NotificationId = pn.NotificationId
 WHERE n.MatchId IS NOT NULL OR n.StadiumId IS NOT NULL;
DELETE FROM Notification WHERE MatchId IS NOT NULL OR StadiumId IS NOT NULL;

-- ---- Maçlar ve bağlı satırlar
DELETE FROM VenueReviews;
DELETE FROM MvpVotes;
DELETE FROM PlayerRatings;
DELETE FROM MatchLikes;
DELETE FROM MatchParticipants;
DELETE FROM MatchComments;
DELETE FROM MatchInvites;
DELETE FROM Activities WHERE MatchId IS NOT NULL;
DELETE FROM MatchTeam;
DELETE FROM Matchs;

-- ---- Turnuvalar
DELETE FROM TournamentFixtures;
DELETE FROM TournamentTeams;
DELETE FROM Tournaments;
-- Turnuva bildirimleri (MatchId/StadiumId boş olabilir)
DELETE pn FROM PlayerNotification pn
  JOIN Notification n ON n.NotificationId = pn.NotificationId
 WHERE n.Type = 12;
DELETE FROM Notification WHERE Type = 12;
DELETE FROM Activities WHERE Type = 7; -- TournamentWon

-- ---- Tesisler
DELETE FROM SlotBlocks;
DELETE FROM Campaigns;
DELETE FROM PriceRules;
DELETE FROM OpeningHours;
DELETE FROM StadiumPhotos;
DELETE FROM Pitches;
DELETE FROM Stadiums;

-- ---- Önceki seed'in demo hesapları
DECLARE @demo TABLE (Id int PRIMARY KEY);
INSERT INTO @demo SELECT PlayerId FROM Players
 WHERE UserName LIKE 'isletme[_]%' OR UserName LIKE 'demo[_]%' OR UserName = 'seed_admin';

-- Sadece demo üyelerden oluşan takımlar
DECLARE @demoTeams TABLE (Id int PRIMARY KEY);
INSERT INTO @demoTeams
SELECT pt.TeamId FROM PlayerTeam pt
  LEFT JOIN @demo d ON d.Id = pt.PlayerId
 GROUP BY pt.TeamId
HAVING COUNT(d.Id) > 0 AND COUNT(d.Id) = COUNT(*);

DELETE pn FROM PlayerNotification pn WHERE pn.PlayerId IN (SELECT Id FROM @demo);
DELETE FROM Notification WHERE NotificationId NOT IN (SELECT NotificationId FROM PlayerNotification)
   AND (Type IN (9, 11)); -- takip / takım daveti bildirimlerinden alıcısı kalmayanlar
DELETE FROM Activities WHERE ActorPlayerId IN (SELECT Id FROM @demo) OR TargetPlayerId IN (SELECT Id FROM @demo)
   OR TeamId IN (SELECT Id FROM @demoTeams);
DELETE FROM TeamInvites WHERE InviterId IN (SELECT Id FROM @demo) OR InviteeId IN (SELECT Id FROM @demo)
   OR TeamId IN (SELECT Id FROM @demoTeams);
DELETE FROM PlayerTeam WHERE PlayerId IN (SELECT Id FROM @demo) OR TeamId IN (SELECT Id FROM @demoTeams);
DELETE FROM Teams WHERE TeamId IN (SELECT Id FROM @demoTeams);
DELETE FROM Follows WHERE FollowerId IN (SELECT Id FROM @demo) OR FolloweeId IN (SELECT Id FROM @demo);
DELETE FROM Blocks WHERE BlockerId IN (SELECT Id FROM @demo) OR BlockedId IN (SELECT Id FROM @demo);
DELETE FROM Reports WHERE ReporterId IN (SELECT Id FROM @demo) OR ReportedPlayerId IN (SELECT Id FROM @demo);
DELETE FROM PlayerSettings WHERE PlayerId IN (SELECT Id FROM @demo);
DELETE FROM Messages WHERE SendPlayerId IN (SELECT Id FROM @demo) OR RecipientPlayerId IN (SELECT Id FROM @demo);
DELETE FROM GroupMessages WHERE SendPlayerId IN (SELECT Id FROM @demo);
DELETE FROM GroupPlayers WHERE PlayerId IN (SELECT Id FROM @demo);
DELETE FROM Players WHERE PlayerId IN (SELECT Id FROM @demo);

COMMIT TRANSACTION;

SELECT (SELECT COUNT(*) FROM Matchs) AS matches, (SELECT COUNT(*) FROM Stadiums) AS stadiums,
       (SELECT COUNT(*) FROM Tournaments) AS tournaments, (SELECT COUNT(*) FROM Players) AS players,
       (SELECT COUNT(*) FROM Teams) AS teams;
