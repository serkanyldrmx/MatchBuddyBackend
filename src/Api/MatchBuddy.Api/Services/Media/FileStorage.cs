using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace MatchBuddy.Api.Services
{
    /// <summary>Medya deposu (docs/media-api.md §1). Anahtar: "{kind}/{yyyy}/{MM}/{guid}/{sm|md|lg}.webp".</summary>
    public interface IFileStorage
    {
        Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);
        /// <summary>Yoksa sessizce geçer.</summary>
        Task DeleteAsync(string key, CancellationToken ct);
        /// <summary>Bir medyanın tüm varyantları ("avatars/2026/11/guid").</summary>
        Task DeletePrefixAsync(string prefix, CancellationToken ct);
    }

    public class StorageOptions
    {
        public const string SectionName = "Storage";
        public const string ProviderLocal = "Local";
        public const string ProviderS3 = "S3";
        public const string CacheControl = "public, max-age=31536000, immutable";

        /// <summary>"Local" (varsayılan) | "S3"</summary>
        public string Provider { get; set; } = ProviderLocal;
        public LocalStorageOptions Local { get; set; } = new();
        public S3StorageOptions S3 { get; set; } = new();

        public bool IsS3 => string.Equals(Provider, ProviderS3, StringComparison.OrdinalIgnoreCase);

        /// <summary>Yerel depo kökü (boşsa {ContentRoot}/App_Data/media).</summary>
        public string ResolveLocalRoot(string contentRootPath) =>
            string.IsNullOrWhiteSpace(Local.Root)
                ? Path.Combine(contentRootPath, "App_Data", "media")
                : Path.GetFullPath(Path.IsPathRooted(Local.Root) ? Local.Root : Path.Combine(contentRootPath, Local.Root));
    }

    public class LocalStorageOptions
    {
        public string? Root { get; set; }
    }

    public class S3StorageOptions
    {
        /// <summary>R2: https://&lt;ACCOUNT_ID&gt;.r2.cloudflarestorage.com</summary>
        public string? ServiceUrl { get; set; }
        public string? Bucket { get; set; }
        public string? AccessKey { get; set; }
        public string? SecretKey { get; set; }
        /// <summary>Herkese açık adres (r2.dev ya da özel alan adı), sonda / olmadan.</summary>
        public string? PublicBaseUrl { get; set; }
        public string Region { get; set; } = "auto";
    }

    internal static class StorageKeys
    {
        /// <summary>Sadece [a-z0-9/._-] ve ".." içermeyen göreli anahtarlar kabul edilir.</summary>
        public static bool IsSafe(string key) =>
            !string.IsNullOrEmpty(key) && key.Length <= 300 && !key.Contains("..") && !key.StartsWith('/') &&
            key.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '/' or '.' or '_' or '-');

        public static void EnsureSafe(string key)
        {
            if (!IsSafe(key)) throw new ArgumentException($"Geçersiz depo anahtarı: {key}", nameof(key));
        }
    }

    /// <summary>Geliştirme deposu: dosyalar {Root}/{key}; API "/media" yolundan sunar.</summary>
    public class LocalFileStorage : IFileStorage
    {
        public string Root { get; }

        public LocalFileStorage(IOptions<StorageOptions> options, IWebHostEnvironment env)
        {
            Root = options.Value.ResolveLocalRoot(env.ContentRootPath);
            Directory.CreateDirectory(Root);
        }

        private string PathOf(string key)
        {
            StorageKeys.EnsureSafe(key.TrimEnd('/'));
            return Path.Combine(Root, key.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
        }

        public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
        {
            var path = PathOf(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(file, ct);
        }

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            var path = PathOf(key);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // silinemese de kayıt silinmiş sayılır
            }
            return Task.CompletedTask;
        }

        public Task DeletePrefixAsync(string prefix, CancellationToken ct)
        {
            var path = PathOf(prefix);
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>S3 uyumlu depo (Cloudflare R2, AWS S3, MinIO...).</summary>
    public class S3FileStorage : IFileStorage, IDisposable
    {
        private readonly AmazonS3Client _client;
        private readonly string _bucket;

        public S3FileStorage(IOptions<StorageOptions> options)
        {
            var s3 = options.Value.S3;
            if (string.IsNullOrWhiteSpace(s3.ServiceUrl) || string.IsNullOrWhiteSpace(s3.Bucket) ||
                string.IsNullOrWhiteSpace(s3.AccessKey) || string.IsNullOrWhiteSpace(s3.SecretKey) ||
                string.IsNullOrWhiteSpace(s3.PublicBaseUrl))
            {
                throw new InvalidOperationException(
                    "Storage:Provider = S3 iken Storage:S3:{ServiceUrl, Bucket, AccessKey, SecretKey, PublicBaseUrl} ayarlanmalıdır (docs/media-api.md \"R2 kurulumu\").");
            }

            var config = new AmazonS3Config
            {
                ServiceURL = s3.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = string.IsNullOrWhiteSpace(s3.Region) ? "auto" : s3.Region,
                // R2 yeni varsayılan bütünlük (CRC) başlıklarını desteklemiyor: sadece gerektiğinde
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };
            _client = new AmazonS3Client(new BasicAWSCredentials(s3.AccessKey, s3.SecretKey), config);
            _bucket = s3.Bucket!;
        }

        public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
        {
            StorageKeys.EnsureSafe(key);
            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                Headers = { CacheControl = StorageOptions.CacheControl },
                // R2 akış (chunked) SigV4 imzalamayı desteklemiyor
                DisablePayloadSigning = true,
                AutoCloseStream = false,
            }, ct);
        }

        public async Task DeleteAsync(string key, CancellationToken ct)
        {
            StorageKeys.EnsureSafe(key);
            // S3 DeleteObject, nesne yoksa da başarılı döner
            await _client.DeleteObjectAsync(_bucket, key, ct);
        }

        public async Task DeletePrefixAsync(string prefix, CancellationToken ct)
        {
            StorageKeys.EnsureSafe(prefix.TrimEnd('/'));
            var normalized = prefix.TrimEnd('/') + "/";
            var request = new ListObjectsV2Request { BucketName = _bucket, Prefix = normalized };
            ListObjectsV2Response response;
            do
            {
                response = await _client.ListObjectsV2Async(request, ct);
                // Tek tek silme: DeleteObjects bazı S3 uyumlu sunucularda Content-MD5 ister
                foreach (var obj in response.S3Objects ?? new List<S3Object>())
                {
                    await _client.DeleteObjectAsync(_bucket, obj.Key, ct);
                }
                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated == true);
        }

        public void Dispose() => _client.Dispose();
    }
}
