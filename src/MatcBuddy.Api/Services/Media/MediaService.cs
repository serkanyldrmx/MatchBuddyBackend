using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using MediaEntity = MatcBuddy.Entities.Entity.Media;

namespace MatcBuddy.Api.Services
{
    /// <summary>{ mediaId, url (md), thumbUrl (sm), largeUrl (lg), width, height }</summary>
    public class MediaDto
    {
        public int MediaId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string ThumbUrl { get; set; } = string.Empty;
        public string LargeUrl { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class MigrateLegacyResult
    {
        public int MigratedAvatars { get; set; }
        public int MigratedVenuePhotos { get; set; }
        public List<string> Missing { get; set; } = new();
    }

    /// <summary>Medya boru hattı (docs/media-api.md §3).</summary>
    public interface IMediaService
    {
        /// <summary>Doğrula → işle → depoya yaz → Media (Pending).</summary>
        Task<OperationResult<MediaDto>> UploadAsync(IFormFile? file, MediaKind kind, int? uploaderId, CancellationToken ct = default);

        /// <summary>Bellekteki resim (ör. sosyal girişte indirilen profil fotoğrafı) için aynı hat.</summary>
        Task<OperationResult<MediaDto>> UploadBytesAsync(byte[] bytes, MediaKind kind, int? uploaderId, CancellationToken ct = default);

        /// <summary>Pending + aynı sahip + aynı tür ise atomik olarak Attached yapar; değilse "Fotoğraf bulunamadı.".</summary>
        OperationResult<MediaDto> Attach(int mediaId, MediaKind expectedKind, int ownerId);

        /// <summary>Yeni yüklenen (Pending) medyayı sahibine bakmadan bağlar; isteğe bağlı sahibini değiştirir (kayıt akışı).</summary>
        void MarkAttached(int mediaId, int? newOwnerId = null);

        /// <summary>Depodan varyantları siler, Status=Deleted.</summary>
        Task DeleteAsync(int mediaId, CancellationToken ct = default);

        /// <summary>Bir kaydın eski fotoğrafını siler: medya varsa medya, yoksa eski /uploads dosyası (başka kayıt kullanmıyorsa).</summary>
        Task DeleteImageAsync(int? mediaId, string? url, CancellationToken ct = default);

        /// <summary>StadiumPhotos.MediaId'yi yazar ve medyayı bağlar.</summary>
        void LinkStadiumPhoto(int photoId, int mediaId);

        /// <summary>Yeni tesisin fotoğraflarını URL eşleşmesine göre medyaya bağlar.</summary>
        void LinkStadiumPhotos(int stadiumId, IEnumerable<MediaDto> media);

        int? GetStadiumPhotoMediaId(int photoId);

        /// <summary>Medya kaydı yoksa (eski /uploads) null.</summary>
        (int? MediaId, string? Url) GetAvatar(int playerId);

        /// <summary>24 saatten eski Pending medyayı siler. Silinen sayısını döner.</summary>
        Task<int> CleanupPendingAsync(TimeSpan olderThan, CancellationToken ct = default);

        Task<MigrateLegacyResult> MigrateLegacyAsync(CancellationToken ct = default);
    }

    public class MediaService : IMediaService
    {
        public const string NotFoundMessage = ErrorCodes.Media.NotFound;

        private readonly IFileStorage _storage;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<MediaService> _logger;

        public MediaService(IFileStorage storage, IWebHostEnvironment env, ILogger<MediaService> logger)
        {
            _storage = storage;
            _env = env;
            _logger = logger;
        }

        public static string KindFolder(MediaKind kind) => kind switch
        {
            MediaKind.Avatar => "avatars",
            MediaKind.VenuePhoto => "venues",
            _ => "messages",
        };

        /// <summary>Sağlayıcıdan bağımsız göreli URL: /media/{prefix}/{size}.webp</summary>
        public static string UrlFor(string keyPrefix, string size) => $"/media/{keyPrefix}/{size}.webp";

        public static MediaDto ToDto(MediaEntity m) => new()
        {
            MediaId = m.MediaId,
            Url = UrlFor(m.KeyPrefix, "md"),
            ThumbUrl = UrlFor(m.KeyPrefix, "sm"),
            LargeUrl = UrlFor(m.KeyPrefix, "lg"),
            Width = m.Width,
            Height = m.Height,
        };

        // ------------------------------------------------------------------ Yükleme

        public async Task<OperationResult<MediaDto>> UploadAsync(IFormFile? file, MediaKind kind, int? uploaderId, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0) return OperationResult<MediaDto>.BadRequest(ErrorCodes.Media.NoFile);
            if (file.Length > ImageProcessor.MaxBytes) return OperationResult<MediaDto>.BadRequest(ImageProcessor.TooLargeError());

            byte[] bytes;
            await using (var stream = file.OpenReadStream())
            using (var ms = new MemoryStream((int)file.Length))
            {
                await stream.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }
            return await UploadBytesAsync(bytes, kind, uploaderId, ct);
        }

        public async Task<OperationResult<MediaDto>> UploadBytesAsync(byte[] bytes, MediaKind kind, int? uploaderId, CancellationToken ct = default)
        {
            ImageProcessor.Result processed;
            try
            {
                processed = ImageProcessor.Process(bytes, kind);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Resim işlenemedi");
                return OperationResult<MediaDto>.BadRequest(ImageProcessor.InvalidError());
            }
            if (!processed.Success) return OperationResult<MediaDto>.BadRequest(processed.Error!);

            var now = DateTime.Now;
            var prefix = $"{KindFolder(kind)}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}";
            try
            {
                foreach (var v in processed.Variants)
                {
                    using var content = new MemoryStream(v.Data, writable: false);
                    await _storage.PutAsync($"{prefix}/{v.Name}.webp", content, "image/webp", ct);
                }
            }
            catch
            {
                await SafeDeletePrefix(prefix);
                throw;
            }

            var md = processed.Variants.First(v => v.Name == "md");
            var media = new MediaEntity
            {
                Kind = kind,
                KeyPrefix = prefix,
                OwnerPlayerId = uploaderId,
                Width = md.Width,
                Height = md.Height,
                Bytes = processed.Variants.Sum(v => v.Data.Length),
                Status = MediaStatus.Pending,
                CreatedAt = now,
            };
            try
            {
                using var context = new MatcBuddyContext();
                context.Media.Add(media);
                await context.SaveChangesAsync(ct);
            }
            catch
            {
                await SafeDeletePrefix(prefix);
                throw;
            }
            return OperationResult<MediaDto>.Ok(ToDto(media));
        }

        // ------------------------------------------------------------------ Bağlama

        public OperationResult<MediaDto> Attach(int mediaId, MediaKind expectedKind, int ownerId)
        {
            using var context = new MatcBuddyContext();
            var now = DateTime.Now;
            // Koşullu güncelleme: aynı medya iki mesaja eş zamanlı bağlanamaz
            var updated = context.Media
                .Where(m => m.MediaId == mediaId && m.Status == MediaStatus.Pending && m.Kind == expectedKind && m.OwnerPlayerId == ownerId)
                .ExecuteUpdate(s => s.SetProperty(m => m.Status, MediaStatus.Attached).SetProperty(m => m.AttachedAt, now));
            if (updated == 0) return OperationResult<MediaDto>.BadRequest(NotFoundMessage);

            var media = context.Media.AsNoTracking().First(m => m.MediaId == mediaId);
            return OperationResult<MediaDto>.Ok(ToDto(media));
        }

        public void MarkAttached(int mediaId, int? newOwnerId = null)
        {
            using var context = new MatcBuddyContext();
            var media = context.Media.FirstOrDefault(m => m.MediaId == mediaId);
            if (media == null) return;
            media.Status = MediaStatus.Attached;
            media.AttachedAt = DateTime.Now;
            if (newOwnerId.HasValue) media.OwnerPlayerId = newOwnerId;
            context.SaveChanges();
        }

        public void LinkStadiumPhoto(int photoId, int mediaId)
        {
            using var context = new MatcBuddyContext();
            context.StadiumPhotos.Where(p => p.PhotoId == photoId)
                .ExecuteUpdate(s => s.SetProperty(p => p.MediaId, mediaId));
            MarkAttached(mediaId);
        }

        public void LinkStadiumPhotos(int stadiumId, IEnumerable<MediaDto> media)
        {
            using var context = new MatcBuddyContext();
            var photos = context.StadiumPhotos.Where(p => p.StadiumId == stadiumId).ToList();
            foreach (var m in media)
            {
                var photo = photos.FirstOrDefault(p => p.Url == m.Url);
                if (photo == null) continue;
                photo.MediaId = m.MediaId;
            }
            context.SaveChanges();
            foreach (var m in media) MarkAttached(m.MediaId);
        }

        public int? GetStadiumPhotoMediaId(int photoId)
        {
            using var context = new MatcBuddyContext();
            return context.StadiumPhotos.AsNoTracking().Where(p => p.PhotoId == photoId).Select(p => p.MediaId).FirstOrDefault();
        }

        public (int? MediaId, string? Url) GetAvatar(int playerId)
        {
            using var context = new MatcBuddyContext();
            var p = context.Players.AsNoTracking().Where(x => x.PlayerId == playerId)
                .Select(x => new { x.ProfileMediaId, x.ProfilePictureUrl }).FirstOrDefault();
            return p == null ? (null, null) : (p.ProfileMediaId, p.ProfilePictureUrl);
        }

        // ------------------------------------------------------------------ Silme

        public async Task DeleteAsync(int mediaId, CancellationToken ct = default)
        {
            using var context = new MatcBuddyContext();
            var media = context.Media.FirstOrDefault(m => m.MediaId == mediaId);
            if (media == null || media.Status == MediaStatus.Deleted) return;
            await _storage.DeletePrefixAsync(media.KeyPrefix, ct);
            media.Status = MediaStatus.Deleted;
            await context.SaveChangesAsync(ct);
        }

        public async Task DeleteImageAsync(int? mediaId, string? url, CancellationToken ct = default)
        {
            try
            {
                if (mediaId.HasValue)
                {
                    await DeleteAsync(mediaId.Value, ct);
                    return;
                }
                if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/", StringComparison.Ordinal)) return;

                // Eski veride işletmenin profil fotoğrafı, tesis fotoğrafıyla aynı dosya olabilir:
                // dosyayı başka bir kayıt kullanıyorsa silme.
                using var context = new MatcBuddyContext();
                var inUse = context.StadiumPhotos.Any(p => p.Url == url) || context.Players.Count(p => p.ProfilePictureUrl == url) > 0;
                if (!inUse) ImageUploads.Delete(url);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Dosya silinemese de ana işlem başarılı sayılır; bekleyen/eski dosyalar ayrıca temizlenebilir
                _logger.LogWarning(ex, "Eski fotoğraf silinemedi: media {MediaId}, url {Url}", mediaId, url);
            }
        }

        private async Task SafeDeletePrefix(string prefix)
        {
            try
            {
                await _storage.DeletePrefixAsync(prefix, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Depodan silinemedi: {Prefix}", prefix);
            }
        }

        // ------------------------------------------------------------------ Temizlik

        public async Task<int> CleanupPendingAsync(TimeSpan olderThan, CancellationToken ct = default)
        {
            var threshold = DateTime.Now - olderThan;
            using var context = new MatcBuddyContext();
            var stale = context.Media.Where(m => m.Status == MediaStatus.Pending && m.CreatedAt < threshold)
                .OrderBy(m => m.MediaId).Take(P.Int(ParameterKeys.Media.CleanupBatchSize)).ToList();
            var count = 0;
            foreach (var media in stale)
            {
                try
                {
                    await _storage.DeletePrefixAsync(media.KeyPrefix, ct);
                    media.Status = MediaStatus.Deleted;
                    await context.SaveChangesAsync(ct);
                    count++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Bekleyen medya silinemedi: {MediaId}", media.MediaId);
                }
            }

            // Opsiyonel: media.deletedPurgeDays (varsayılan 7) günden eski Deleted satırlar kaldırılır (bağlı kayıtlarda kullanılmıyorsa)
            var deletedThreshold = DateTime.Now.AddDays(-P.Int(ParameterKeys.Media.DeletedPurgeDays));
            await context.Media
                .Where(m => m.Status == MediaStatus.Deleted && m.CreatedAt < deletedThreshold
                            && !context.Players.Any(p => p.ProfileMediaId == m.MediaId)
                            && !context.StadiumPhotos.Any(p => p.MediaId == m.MediaId)
                            && !context.Messages.Any(x => x.MediaId == m.MediaId)
                            && !context.GroupMessages.Any(x => x.MediaId == m.MediaId))
                .ExecuteDeleteAsync(ct);
            return count;
        }

        // ------------------------------------------------------------------ Eski /uploads geçişi

        public async Task<MigrateLegacyResult> MigrateLegacyAsync(CancellationToken ct = default)
        {
            var result = new MigrateLegacyResult();
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");

            byte[]? ReadLegacy(string url)
            {
                if (url.Contains("..")) return null;
                var path = Path.Combine(webRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }

            using var context = new MatcBuddyContext();

            // Profil fotoğrafları
            var players = context.Players.Where(p => p.ProfilePictureUrl != null && p.ProfilePictureUrl.StartsWith("/uploads/")).ToList();
            foreach (var player in players)
            {
                ct.ThrowIfCancellationRequested();
                var url = player.ProfilePictureUrl!;
                var bytes = ReadLegacy(url);
                var upload = bytes == null ? null : await UploadBytesAsync(bytes, MediaKind.Avatar, player.PlayerId, ct);
                if (upload == null || !upload.Success)
                {
                    if (bytes != null) _logger.LogWarning("Eski profil fotoğrafı işlenemedi: {Url} ({Error})", url, upload?.Message);
                    result.Missing.Add(url);
                    continue;
                }
                player.ProfilePictureUrl = upload.Data.Url;
                player.ProfileMediaId = upload.Data.MediaId;
                await context.SaveChangesAsync(ct);
                MarkAttached(upload.Data.MediaId);
                result.MigratedAvatars++;
            }

            // Tesis fotoğrafları (yükleyen: tesisin sahibi)
            var photos = (from p in context.StadiumPhotos
                          join s in context.Stadiums on p.StadiumId equals s.StadiumId
                          where p.Url.StartsWith("/uploads/")
                          select new { Photo = p, s.OwnerPlayerId }).ToList();
            foreach (var row in photos)
            {
                ct.ThrowIfCancellationRequested();
                var url = row.Photo.Url;
                var bytes = ReadLegacy(url);
                var upload = bytes == null ? null : await UploadBytesAsync(bytes, MediaKind.VenuePhoto, row.OwnerPlayerId, ct);
                if (upload == null || !upload.Success)
                {
                    if (bytes != null) _logger.LogWarning("Eski tesis fotoğrafı işlenemedi: {Url} ({Error})", url, upload?.Message);
                    result.Missing.Add(url);
                    continue;
                }
                row.Photo.Url = upload.Data.Url;
                row.Photo.MediaId = upload.Data.MediaId;
                await context.SaveChangesAsync(ct);
                MarkAttached(upload.Data.MediaId);
                result.MigratedVenuePhotos++;
            }

            return result;
        }
    }
}
