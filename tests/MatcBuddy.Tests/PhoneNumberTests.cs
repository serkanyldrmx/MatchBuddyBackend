using MatcBuddy.Core.Utilities;

namespace MatcBuddy.Tests;

/// <summary>Telefonlar tek biçimde saklanır: "05xxxxxxxxx".</summary>
public class PhoneNumberTests
{
    [Theory]
    [InlineData("5321234567", "05321234567")]
    [InlineData("05321234567", "05321234567")]
    [InlineData("+90 532 123 45 67", "05321234567")]
    [InlineData("90 (532) 123-45-67", "05321234567")]
    [InlineData("0 532 123 45 67", "05321234567")]
    [InlineData("+49 151 2345 6789", "+4915123456789")]
    [InlineData("0049 151 2345 6789", "+4915123456789")]
    public void Normalizes_to_single_format(string raw, string expected) =>
        Assert.Equal(expected, PhoneNumbers.Normalize(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    public void Empty_is_null(string? raw) => Assert.Null(PhoneNumbers.Normalize(raw));
}
