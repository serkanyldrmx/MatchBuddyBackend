using System.Globalization;
using System.Reflection;
using System.Resources;
using MatcBuddy.Core.Errors;
using MatcBuddy.Core.Localization;

namespace MatcBuddy.Tests;

/// <summary>
/// Kodda tanımlı her mesaj / hata anahtarının Türkçe (varsayılan) kaynakta metni olmalı.
/// Yeni bir ErrorCodes / MessageKeys sabiti eklenip Messages.resx unutulursa bu test kırılır.
/// </summary>
public class LocalizationTests
{
    private static readonly ResourceManager Resources =
        new("MatcBuddy.Core.Resources.Messages", typeof(Msg).Assembly);

    private static IEnumerable<string> ConstantsOf(Type root) =>
        new[] { root }.Concat(root.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(k => !k.EndsWith('_')) // önek sabitleri (ör. LABEL_EXCUSE_ + EMERGENCY) tam anahtar değildir
            .Distinct();

    public static TheoryData<string> AllKeys()
    {
        var data = new TheoryData<string>();
        foreach (var key in ConstantsOf(typeof(ErrorCodes)).Concat(ConstantsOf(typeof(MessageKeys))).Distinct())
            data.Add(key);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void Every_key_has_a_default_text(string key)
    {
        var text = Resources.GetString(key, CultureInfo.InvariantCulture);
        Assert.False(string.IsNullOrWhiteSpace(text), $"Messages.resx içinde '{key}' anahtarı yok.");
    }
}
