using System.Globalization;
using System.Text.Json;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>Parametre yönetimi (yönetici uç noktaları + açılış senkronu).</summary>
    public interface IParameterStore
    {
        /// <summary>Koddaki tanımlarda olup tabloda olmayan parametreleri varsayılanla ekler; meta veriyi günceller.</summary>
        int SyncDefinitions();
        List<ParameterGroupDto> ListGrouped();
        OperationResult<ParameterDto> Update(string key, string? value, int changedBy);
        OperationResult<List<ParameterAuditDto>> History(string key, int take);
        /// <summary>IsPublic parametreler: anahtar → tipli değer.</summary>
        Dictionary<string, object?> PublicValues();
    }

    /// <summary>
    /// SystemParameters tablosu üzerinde IParameterService: tüm tablo tek önbellek girdisinde tutulur
    /// (5 dk mutlak süre; güncellemede hemen temizlenir).
    /// </summary>
    public sealed class EFParameterService : ParameterReaderBase, IParameterStore
    {
        private const string CacheKey = "platform:parameters";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private readonly IMemoryCache _cache;

        public EFParameterService(IMemoryCache cache)
        {
            _cache = cache;
        }

        private Dictionary<string, string> Values() =>
            _cache.GetOrCreate(CacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                using var context = new MatchBuddyContext();
                return context.SystemParameters.AsNoTracking()
                    .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            })!;

        public override string GetRaw(string key)
        {
            try
            {
                if (Values().TryGetValue(key, out var v)) return v;
            }
            catch (Exception)
            {
                // Veritabanına ulaşılamazsa kod varsayılanı ile devam et
            }
            return DefaultOf(key);
        }

        public override void Invalidate() => _cache.Remove(CacheKey);

        // ------------------------------------------------------------------ Yönetim

        public int SyncDefinitions()
        {
            using var context = new MatchBuddyContext();
            var existing = context.SystemParameters.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.Now;
            var added = 0;
            foreach (var d in ParameterCatalog.All)
            {
                var description = Msg.GetDefault(d.DescriptionKey);
                if (existing.TryGetValue(d.Key, out var row))
                {
                    row.DataType = d.DataType;
                    row.Group = d.Group;
                    row.Description = description;
                    row.MinValue = d.MinValue;
                    row.MaxValue = d.MaxValue;
                    row.IsPublic = d.IsPublic;
                    continue;
                }
                context.SystemParameters.Add(new SystemParameter
                {
                    Key = d.Key,
                    Value = d.DefaultValue,
                    DataType = d.DataType,
                    Group = d.Group,
                    Description = description,
                    MinValue = d.MinValue,
                    MaxValue = d.MaxValue,
                    IsPublic = d.IsPublic,
                    UpdatedAt = now,
                });
                added++;
            }
            context.SaveChanges();
            Invalidate();
            return added;
        }

        private static string TypeName(ParameterDataType t) => t.ToString().ToLowerInvariant();

        private static ParameterDto ToDto(SystemParameter p, string? userName) => new()
        {
            Key = p.Key,
            Value = p.Value,
            DataType = TypeName(p.DataType),
            Group = p.Group,
            Description = p.Description,
            MinValue = p.MinValue,
            MaxValue = p.MaxValue,
            DefaultValue = ParameterCatalog.Find(p.Key)?.DefaultValue ?? p.Value,
            IsPublic = p.IsPublic,
            UpdatedAt = p.UpdatedAt,
            UpdatedByPlayerId = p.UpdatedByPlayerId,
            UpdatedByUserName = userName,
        };

        public List<ParameterGroupDto> ListGrouped()
        {
            using var context = new MatchBuddyContext();
            var rows = (from p in context.SystemParameters.AsNoTracking()
                        join u in context.Players.AsNoTracking() on p.UpdatedByPlayerId equals u.PlayerId into us
                        from u in us.DefaultIfEmpty()
                        select new { p, UserName = u != null ? u.UserName : null }).ToList();
            var order = ParameterCatalog.All.Select((d, i) => (d.Key, i)).ToDictionary(x => x.Key, x => x.i, StringComparer.OrdinalIgnoreCase);
            return rows
                .OrderBy(r => order.TryGetValue(r.p.Key, out var i) ? i : int.MaxValue).ThenBy(r => r.p.Key)
                .GroupBy(r => r.p.Group)
                .Select(g => new ParameterGroupDto { Group = g.Key, Parameters = g.Select(r => ToDto(r.p, r.UserName)).ToList() })
                .ToList();
        }

        /// <summary>Değeri türüne ve aralığına göre doğrular; normalize edilmiş metni döner.</summary>
        public static OperationResult<string> Validate(SystemParameter p, string? value)
        {
            if (value == null || (value.Trim().Length == 0 && p.DataType != ParameterDataType.String))
                return OperationResult<string>.FieldError("value", ErrorCodes.Parameter.ValueRequired);
            var inv = CultureInfo.InvariantCulture;
            value = value.Trim();
            switch (p.DataType)
            {
                case ParameterDataType.Int:
                    if (!int.TryParse(value, NumberStyles.Integer, inv, out var i))
                        return OperationResult<string>.FieldError("value", ErrorCodes.Parameter.InvalidType, p.Key, "int");
                    if ((p.MinValue != null && i < int.Parse(p.MinValue, inv)) || (p.MaxValue != null && i > int.Parse(p.MaxValue, inv)))
                        return OperationResult<string>.FieldError("value", ErrorCodes.Parameter.OutOfRange, p.Key, p.MinValue ?? "-", p.MaxValue ?? "-");
                    return OperationResult<string>.Ok(i.ToString(inv));
                case ParameterDataType.Decimal:
                    if (!decimal.TryParse(value, NumberStyles.Number, inv, out var d))
                        return OperationResult<string>.FieldError("value", ErrorCodes.Parameter.InvalidType, p.Key, "decimal");
                    if ((p.MinValue != null && d < decimal.Parse(p.MinValue, inv)) || (p.MaxValue != null && d > decimal.Parse(p.MaxValue, inv)))
                        return OperationResult<string>.FieldError("value", ErrorCodes.Parameter.OutOfRange, p.Key, p.MinValue ?? "-", p.MaxValue ?? "-");
                    return OperationResult<string>.Ok(d.ToString(inv));
                case ParameterDataType.Bool:
                    if (!bool.TryParse(value, out var b))
                        return OperationResult<string>.FieldError("value", ErrorCodes.Parameter.InvalidType, p.Key, "bool");
                    return OperationResult<string>.Ok(b ? "true" : "false");
                case ParameterDataType.Json:
                    try
                    {
                        using var doc = JsonDocument.Parse(value);
                        // Varsayılanı tamsayı dizisi olanlar (ör. venue.allowedDurations) pozitif tamsayı dizisi olmalı
                        var def = ParameterCatalog.Find(p.Key)?.DefaultValue;
                        if (def != null && def.StartsWith('['))
                        {
                            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0
                                || doc.RootElement.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.Number || !e.TryGetInt32(out var n) || n <= 0))
                                return OperationResult<string>.FieldError("value", ErrorCodes.Parameter.InvalidList, p.Key);
                        }
                        return OperationResult<string>.Ok(JsonSerializer.Serialize(doc.RootElement));
                    }
                    catch (JsonException)
                    {
                        return OperationResult<string>.FieldError("value", ErrorCodes.Parameter.InvalidJson);
                    }
                default:
                    return OperationResult<string>.Ok(value);
            }
        }

        public OperationResult<ParameterDto> Update(string key, string? value, int changedBy)
        {
            using var context = new MatchBuddyContext();
            var p = context.SystemParameters.FirstOrDefault(x => x.Key == key);
            if (p == null) return OperationResult<ParameterDto>.NotFound(ErrorCodes.Parameter.NotFound);

            var valid = Validate(p, value);
            if (!valid.Success) return OperationResult<ParameterDto>.From(valid);

            var old = p.Value;
            var now = DateTime.Now;
            if (old != valid.Data)
            {
                p.Value = valid.Data;
                p.UpdatedAt = now;
                p.UpdatedByPlayerId = changedBy;
                context.ParameterAudits.Add(new ParameterAudit
                {
                    SystemParameterId = p.SystemParameterId,
                    Key = p.Key,
                    OldValue = old,
                    NewValue = valid.Data,
                    ChangedByPlayerId = changedBy,
                    ChangedAt = now,
                });
                context.SaveChanges();
                Invalidate();
            }
            var userName = context.Players.AsNoTracking().Where(x => x.PlayerId == p.UpdatedByPlayerId).Select(x => x.UserName).FirstOrDefault();
            return OperationResult<ParameterDto>.Ok(ToDto(p, userName), Msg.Get(MessageKeys.Parameter.Updated));
        }

        public OperationResult<List<ParameterAuditDto>> History(string key, int take)
        {
            using var context = new MatchBuddyContext();
            var p = context.SystemParameters.AsNoTracking().FirstOrDefault(x => x.Key == key);
            if (p == null) return OperationResult<List<ParameterAuditDto>>.NotFound(ErrorCodes.Parameter.NotFound);
            var rows = (from a in context.ParameterAudits.AsNoTracking()
                        where a.SystemParameterId == p.SystemParameterId
                        join u in context.Players.AsNoTracking() on a.ChangedByPlayerId equals u.PlayerId into us
                        from u in us.DefaultIfEmpty()
                        orderby a.ChangedAt descending, a.ParameterAuditId descending
                        select new ParameterAuditDto
                        {
                            Id = a.ParameterAuditId,
                            Key = a.Key,
                            OldValue = a.OldValue,
                            NewValue = a.NewValue,
                            ChangedByPlayerId = a.ChangedByPlayerId,
                            ChangedByUserName = u != null ? u.UserName : null,
                            ChangedAt = a.ChangedAt,
                        }).Take(Math.Clamp(take, 1, 500)).ToList();
            return OperationResult<List<ParameterAuditDto>>.Ok(rows);
        }

        public Dictionary<string, object?> PublicValues()
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var d in ParameterCatalog.All.Where(d => d.IsPublic))
            {
                result[d.Key] = d.DataType switch
                {
                    ParameterDataType.Int => GetInt(d.Key),
                    ParameterDataType.Decimal => GetDecimal(d.Key),
                    ParameterDataType.Bool => GetBool(d.Key),
                    ParameterDataType.Json => GetJson<JsonElement>(d.Key),
                    _ => GetString(d.Key),
                };
            }
            return result;
        }
    }
}
