using MatchBuddy.Entities.Entity;
using SkiaSharp;

namespace MatchBuddy.Api.Services
{
    /// <summary>
    /// Resim işleme (docs/media-api.md §2): format imzadan tespit, boyut kontrolü piksel çözülmeden önce,
    /// EXIF yönü uygulanır, tüm metadata atılır (pikseller WebP olarak yeniden kodlanır), sm/md/lg üretilir.
    /// </summary>
    public static class ImageProcessor
    {
        public const long MaxBytes = 10 * 1024 * 1024;
        public const long MaxPixels = 40_000_000;

        public const string ErrorInvalid = "Dosya geçerli bir resim değil.";
        public const string ErrorTooLarge = "Fotoğraf en fazla 10 MB olabilir.";
        public const string ErrorTooManyPixels = "Fotoğraf çok büyük (en fazla 40 MP).";

        public static readonly string[] SizeNames = { "sm", "md", "lg" };

        /// <summary>Uzun kenar sınırları (avatar: kare kenar). Büyütme yapılmaz.</summary>
        public static int[] SizesFor(MediaKind kind) => kind switch
        {
            MediaKind.Avatar => new[] { 128, 320, 640 },
            MediaKind.VenuePhoto => new[] { 480, 1200, 1920 },
            _ => new[] { 480, 1280, 2048 },
        };

        public sealed record Variant(string Name, byte[] Data, int Width, int Height);

        public sealed class Result
        {
            public string? Error { get; init; }
            public List<Variant> Variants { get; init; } = new();
            public bool Success => Error == null;
        }

        public static Result Process(byte[] input, MediaKind kind)
        {
            if (input.Length == 0) return new Result { Error = ErrorInvalid };
            if (input.Length > MaxBytes) return new Result { Error = ErrorTooLarge };

            using var data = SKData.CreateCopy(input);
            using var codec = SKCodec.Create(data);
            if (codec == null ||
                codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            {
                return new Result { Error = ErrorInvalid };
            }

            // Decompression bomb koruması: sadece başlık okunmuştur
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0) return new Result { Error = ErrorInvalid };
            if ((long)info.Width * info.Height > MaxPixels) return new Result { Error = ErrorTooManyPixels };

            var origin = codec.EncodedOrigin;
            var sizes = SizesFor(kind);
            var largest = sizes[^1];

            using var decoded = Decode(codec, kind, largest);
            if (decoded == null) return new Result { Error = ErrorInvalid };

            using var oriented = ApplyOrigin(decoded, origin);
            var source = oriented ?? decoded;

            // Avatar: ortadan kare kırp
            SKBitmap? cropped = null;
            try
            {
                if (kind == MediaKind.Avatar && source.Width != source.Height)
                {
                    var side = Math.Min(source.Width, source.Height);
                    var x = (source.Width - side) / 2;
                    var y = (source.Height - side) / 2;
                    cropped = new SKBitmap(new SKImageInfo(side, side, source.ColorType, source.AlphaType));
                    using (var canvas = new SKCanvas(cropped))
                    {
                        canvas.Clear(SKColors.Transparent);
                        canvas.DrawBitmap(source, new SKRect(x, y, x + side, y + side), new SKRect(0, 0, side, side));
                    }
                    source = cropped;
                }

                var variants = new List<Variant>(3);
                for (var i = 0; i < sizes.Length; i++)
                {
                    var (w, h) = Fit(source.Width, source.Height, sizes[i]);
                    var quality = SizeNames[i] == "sm" ? 75 : 82;
                    variants.Add(new Variant(SizeNames[i], Encode(source, w, h, quality), w, h));
                }
                return new Result { Variants = variants };
            }
            finally
            {
                cropped?.Dispose();
            }
        }

        /// <summary>Uzun kenar max'a sığacak şekilde (büyütme yok).</summary>
        private static (int W, int H) Fit(int width, int height, int max)
        {
            var longSide = Math.Max(width, height);
            if (longSide <= max) return (width, height);
            var scale = (double)max / longSide;
            return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
        }

        /// <summary>
        /// Pikselleri çözer. JPEG'de codec'in desteklediği küçültülmüş çözme (1/2, 1/4, 1/8) en büyük varyant için
        /// yeterli çözünürlük kalıyorsa kullanılır (bellek ve süre tasarrufu).
        /// </summary>
        private static SKBitmap? Decode(SKCodec codec, MediaKind kind, int largest)
        {
            var full = codec.Info;
            var needed = largest; // gereken en kısa ölçü (avatar: kısa kenar; diğer: uzun kenar)
            int Measure(SKSizeI s) => kind == MediaKind.Avatar ? Math.Min(s.Width, s.Height) : Math.Max(s.Width, s.Height);

            var target = new SKSizeI(full.Width, full.Height);
            if (Measure(target) > needed)
            {
                foreach (var scale in new[] { 0.125f, 0.25f, 0.5f })
                {
                    var scaled = codec.GetScaledDimensions(scale);
                    if (scaled.Width > 0 && scaled.Height > 0 && Measure(scaled) >= needed)
                    {
                        target = scaled;
                        break;
                    }
                }
            }

            var decodeInfo = new SKImageInfo(target.Width, target.Height, SKColorType.Rgba8888,
                full.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
            var bitmap = new SKBitmap(decodeInfo);
            var result = codec.GetPixels(decodeInfo, bitmap.GetPixels());
            if (result != SKCodecResult.Success)
            {
                bitmap.Dispose();
                return null;
            }
            return bitmap;
        }

        /// <summary>EXIF yönünü piksellere uygular (TopLeft ise null döner: kaynak olduğu gibi kullanılır).</summary>
        private static SKBitmap? ApplyOrigin(SKBitmap src, SKEncodedOrigin origin)
        {
            if (origin == SKEncodedOrigin.TopLeft) return null;

            int w = src.Width, h = src.Height;
            var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var dst = new SKBitmap(new SKImageInfo(swap ? h : w, swap ? w : h, src.ColorType, src.AlphaType));
            using var canvas = new SKCanvas(dst);
            canvas.Clear(SKColors.Transparent);
            switch (origin)
            {
                case SKEncodedOrigin.TopRight: // yatay ayna
                    canvas.Translate(w, 0);
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.BottomRight: // 180°
                    canvas.Translate(w, h);
                    canvas.RotateDegrees(180);
                    break;
                case SKEncodedOrigin.BottomLeft: // dikey ayna
                    canvas.Translate(0, h);
                    canvas.Scale(1, -1);
                    break;
                case SKEncodedOrigin.LeftTop: // transpoze
                    canvas.RotateDegrees(90);
                    canvas.Scale(1, -1);
                    break;
                case SKEncodedOrigin.RightTop: // 90° saat yönü
                    canvas.Translate(h, 0);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.RightBottom: // transverse
                    canvas.Translate(h, w);
                    canvas.RotateDegrees(90);
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.LeftBottom: // 270° saat yönü
                    canvas.Translate(0, w);
                    canvas.RotateDegrees(270);
                    break;
            }
            canvas.DrawBitmap(src, 0, 0);
            canvas.Flush();
            return dst;
        }

        private static byte[] Encode(SKBitmap source, int width, int height, int quality)
        {
            SKBitmap? resized = null;
            try
            {
                var bitmap = source;
                if (width != source.Width || height != source.Height)
                {
                    resized = source.Resize(new SKImageInfo(width, height, source.ColorType, source.AlphaType), SKFilterQuality.High)
                              ?? throw new InvalidOperationException("Resim yeniden boyutlandırılamadı.");
                    bitmap = resized;
                }
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Webp, quality)
                                    ?? throw new InvalidOperationException("WebP kodlanamadı.");
                return encoded.ToArray();
            }
            finally
            {
                resized?.Dispose();
            }
        }
    }
}
