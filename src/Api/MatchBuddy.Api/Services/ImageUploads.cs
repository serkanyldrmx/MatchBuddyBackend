namespace MatchBuddy.Api.Services
{
    /// <summary>
    /// wwwroot/uploads altına resim kaydetme (profil resmi, tesis fotoğrafları).
    /// Uzantı (jpg/jpeg/png), boyut (≤ 5MB) ve dosya imzası (magic bytes) kontrol edilir.
    /// </summary>
    public static class ImageUploads
    {
        public const long MaxBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

        /// <summary>Doğrulama hatası (yoksa null).</summary>
        public static string? Validate(IFormFile? file)
        {
            if (file == null || file.Length == 0) return "Dosya boş.";
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension)) return "Sadece JPG, JPEG veya PNG dosyaları yüklenebilir.";
            if (file.Length > MaxBytes) return "Dosya boyutu 5MB'ı geçemez.";
            // Uzantıya güvenme: dosya imzasını (magic bytes) kontrol et
            if (!HasImageSignature(file)) return "Dosya geçerli bir resim değil.";
            return null;
        }

        /// <summary>Dosyayı wwwroot/uploads/[subfolder]/guid.ext olarak kaydeder, url'ini döner. Önce Validate çağrılmalı.</summary>
        public static string Save(IFormFile file, string? subfolder = null)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!string.IsNullOrEmpty(subfolder)) folder = Path.Combine(folder, subfolder);
            Directory.CreateDirectory(folder);

            var fileName = Guid.NewGuid() + extension;
            using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
            {
                file.CopyTo(stream);
            }
            return string.IsNullOrEmpty(subfolder) ? $"/uploads/{fileName}" : $"/uploads/{subfolder}/{fileName}";
        }

        /// <summary>/uploads/... url'indeki dosyayı siler (yoksa ya da uploads dışındaysa bir şey yapmaz).</summary>
        public static void Delete(string? url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/", StringComparison.Ordinal) || url.Contains("..")) return;
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Dosya silinemese de kayıt silinmiş olur
            }
        }

        public static bool HasImageSignature(IFormFile file)
        {
            Span<byte> header = stackalloc byte[8];
            using var stream = file.OpenReadStream();
            var read = stream.Read(header);
            if (read < 4) return false;

            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;
            // PNG: 89 50 4E 47
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;
            return false;
        }
    }
}
