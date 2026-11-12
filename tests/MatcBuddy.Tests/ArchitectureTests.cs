using System.Reflection;

namespace MatcBuddy.Tests;

/// <summary>
/// Katman kuralları: bağımlılıklar yalnızca içeri doğru akar.
/// Core ← Entities ← DataAccess ← Business ← Api
/// Bu testler kırılırsa yeni bir bağımlılık yanlış yöne eklenmiş demektir.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Core = typeof(MatcBuddy.Core.Localization.Msg).Assembly;
    private static readonly Assembly Entities = typeof(MatcBuddy.Entities.Entity.Player).Assembly;
    private static readonly Assembly DataAccess = typeof(MatcBuddy.DataAccess.MatcBuddyContext).Assembly;
    private static readonly Assembly Business = typeof(MatcBuddy.Business.Auth.AuthManager).Assembly;
    private static readonly Assembly Api = typeof(MatcBuddy.Api.Program).Assembly;

    private static bool References(Assembly from, Assembly to) =>
        from.GetReferencedAssemblies().Any(a => a.Name == to.GetName().Name);

    [Fact]
    public void Core_depends_on_no_other_layer()
    {
        Assert.False(References(Core, Entities));
        Assert.False(References(Core, DataAccess));
        Assert.False(References(Core, Business));
        Assert.False(References(Core, Api));
    }

    [Fact]
    public void Entities_do_not_depend_on_outer_layers()
    {
        Assert.False(References(Entities, DataAccess));
        Assert.False(References(Entities, Business));
        Assert.False(References(Entities, Api));
    }

    [Fact]
    public void DataAccess_does_not_depend_on_Business_or_Api()
    {
        Assert.False(References(DataAccess, Business));
        Assert.False(References(DataAccess, Api));
    }

    [Fact]
    public void Business_does_not_depend_on_Api()
    {
        Assert.False(References(Business, Api));
    }

    [Fact]
    public void All_assemblies_use_the_product_namespace()
    {
        foreach (var asm in new[] { Core, Entities, DataAccess, Business, Api })
        {
            Assert.StartsWith("MatcBuddy.", asm.GetName().Name);
            var foreign = asm.GetTypes()
                .Where(t => t.Namespace != null && !t.Namespace.StartsWith("MatcBuddy", StringComparison.Ordinal)
                            && !t.Namespace.StartsWith("Microsoft", StringComparison.Ordinal)
                            && !t.Namespace.StartsWith("System", StringComparison.Ordinal))
                .Select(t => t.FullName)
                .ToList();
            Assert.True(foreign.Count == 0, $"{asm.GetName().Name}: ürün ad alanı dışında tipler: {string.Join(", ", foreign)}");
        }
    }
}
