using System.Globalization;
using System.Text.Json;

namespace MatcBuddy.Core.Parameters
{
    /// <summary>Sistem parametrelerinin tipli okuyucusu (önbellekli; güncellemede önbellek temizlenir).</summary>
    public interface IParameterService
    {
        /// <summary>Ham değer (tabloda yoksa tanımdaki varsayılan).</summary>
        string GetRaw(string key);
        int GetInt(string key);
        decimal GetDecimal(string key);
        bool GetBool(string key);
        string GetString(string key);
        T GetJson<T>(string key);
        /// <summary>JSON tamsayı dizisi ("[60,90,120]").</summary>
        IReadOnlyList<int> GetIntList(string key);
        void Invalidate();
    }

    /// <summary>Ham değerleri ayrıştıran ortak taban: bozuk değerde tanımdaki varsayılana düşer.</summary>
    public abstract class ParameterReaderBase : IParameterService
    {
        public abstract string GetRaw(string key);
        public virtual void Invalidate() { }

        protected static string DefaultOf(string key) =>
            ParameterCatalog.Find(key)?.DefaultValue
            ?? throw new KeyNotFoundException("Undefined parameter: " + key);

        public int GetInt(string key) =>
            int.TryParse(GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v : int.Parse(DefaultOf(key), CultureInfo.InvariantCulture);

        public decimal GetDecimal(string key) =>
            decimal.TryParse(GetRaw(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
                ? v : decimal.Parse(DefaultOf(key), CultureInfo.InvariantCulture);

        public bool GetBool(string key) =>
            bool.TryParse(GetRaw(key), out var v) ? v : bool.Parse(DefaultOf(key));

        public string GetString(string key) => GetRaw(key);

        public T GetJson<T>(string key)
        {
            try { return JsonSerializer.Deserialize<T>(GetRaw(key))!; }
            catch (JsonException) { return JsonSerializer.Deserialize<T>(DefaultOf(key))!; }
        }

        public IReadOnlyList<int> GetIntList(string key) => GetJson<List<int>>(key) ?? new List<int>();
    }

    /// <summary>Yalnızca koddaki varsayılanları döndürür (tasarım zamanı / servis kurulmadan önce).</summary>
    public sealed class DefaultParameterReader : ParameterReaderBase
    {
        public override string GetRaw(string key) => DefaultOf(key);
    }

    /// <summary>
    /// Statik yardımcıların (VenueRules, VenuePricing…) parametrelere erişimi. Uygulama açılışında
    /// DI'daki IParameterService ile değiştirilir. Kısa yol: <c>P.Int(ParameterKeys.Venue.MaxPhotos)</c>.
    /// </summary>
    public static class Parameters
    {
        public static IParameterService Current { get; set; } = new DefaultParameterReader();
    }

    public static class P
    {
        public static int Int(string key) => Parameters.Current.GetInt(key);
        public static decimal Dec(string key) => Parameters.Current.GetDecimal(key);
        public static bool Bool(string key) => Parameters.Current.GetBool(key);
        public static IReadOnlyList<int> Ints(string key) => Parameters.Current.GetIntList(key);
    }
}
