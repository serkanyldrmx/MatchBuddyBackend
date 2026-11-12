using System.Globalization;
using System.Resources;

[assembly: NeutralResourcesLanguage("tr")]

namespace MatcBuddy.Core.Localization
{
    /// <summary>
    /// Kullanıcıya görünen tüm metinlerin tek kaynağı: Resources/Messages.resx (Türkçe, varsayılan) ve
    /// Messages.{dil}.resx uydu dosyaları. İstek dili, RequestLocalization ara katmanının ayarladığı
    /// CultureInfo.CurrentUICulture'dır (Accept-Language). Bulunamayan anahtar için anahtarın kendisi döner.
    /// </summary>
    public static class Msg
    {
        private static readonly ResourceManager Resources =
            new("MatcBuddy.Core.Resources.Messages", typeof(Msg).Assembly);

        /// <summary>Desteklenen arayüz dilleri (Localization:SupportedCultures ile açılışta ayarlanır).</summary>
        public static IReadOnlyList<string> SupportedLanguages { get; set; } = new[] { "tr", "en" };

        /// <summary>Varsayılan dil (tr). Bildirimler gibi istek dışı/çok alıcılı metinler bu dilde yazılır.</summary>
        public static CultureInfo DefaultCulture { get; set; } = CultureInfo.GetCultureInfo("tr-TR");

        /// <summary>Geçerli istek dilinde metin.</summary>
        public static string Get(string key, params object?[] args) =>
            GetFor(CultureInfo.CurrentUICulture, key, args);

        /// <summary>Varsayılan dilde metin (veritabanına yazılan bildirim metinleri vb.).</summary>
        public static string GetDefault(string key, params object?[] args) =>
            GetFor(DefaultCulture, key, args);

        public static string GetFor(CultureInfo culture, string key, params object?[] args)
        {
            var format = Resources.GetString(key, culture) ?? key;
            if (args == null || args.Length == 0) return format;
            try
            {
                return string.Format(culture, format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// <summary>"60, 90 veya 120" biçiminde liste (bağlaç kaynaktan: COMMON_LIST_OR).</summary>
        public static string JoinOr<T>(IEnumerable<T> items)
        {
            var list = items.Select(i => Convert.ToString(i, CultureInfo.CurrentCulture) ?? string.Empty).ToList();
            if (list.Count <= 1) return string.Join(string.Empty, list);
            return string.Join(", ", list.Take(list.Count - 1)) + " " + Get("COMMON_LIST_OR") + " " + list[^1];
        }

        /// <summary>Anahtar varsayılan (Türkçe) kaynakta tanımlı mı?</summary>
        public static bool Exists(string key) => Resources.GetString(key, CultureInfo.InvariantCulture) != null;

        /// <summary>Varsayılan kaynaktaki tüm anahtarlar (katalog doğrulaması / dokümantasyon için).</summary>
        public static IReadOnlyDictionary<string, string> All(CultureInfo? culture = null)
        {
            var set = Resources.GetResourceSet(culture ?? CultureInfo.InvariantCulture, true, true);
            var dict = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (set == null) return dict;
            foreach (System.Collections.DictionaryEntry e in set)
                dict[(string)e.Key] = e.Value as string ?? string.Empty;
            return dict;
        }
    }

    /// <summary>DI ile kullanılabilen ince sarmalayıcı (Msg ile aynı kaynak).</summary>
    public interface IMessageCatalog
    {
        string this[string key] { get; }
        string Get(string key, params object?[] args);
        string GetFor(CultureInfo culture, string key, params object?[] args);
    }

    public sealed class ResxMessageCatalog : IMessageCatalog
    {
        public string this[string key] => Msg.Get(key);
        public string Get(string key, params object?[] args) => Msg.Get(key, args);
        public string GetFor(CultureInfo culture, string key, params object?[] args) => Msg.GetFor(culture, key, args);
    }
}
