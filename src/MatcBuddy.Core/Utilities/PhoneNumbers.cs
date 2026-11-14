namespace MatcBuddy.Core.Utilities
{
    /// <summary>
    /// Telefon numaraları tek biçimde saklanır: Türkiye numarası "05xxxxxxxxx" (0 + 10 hane).
    /// "+90 5xx…", "90 5xx…", "5xx…" ve boşluk/tire/parantez içeren yazımlar aynı numaraya çevrilir.
    /// </summary>
    public static class PhoneNumbers
    {
        /// <summary>Boş ise null; Türkiye biçimine uyuyorsa "0XXXXXXXXXX"; uymuyorsa yalnızca rakamlar (yabancı numara).</summary>
        public static string? Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return null;
            if (digits.Length == 12 && digits.StartsWith("90")) return "0" + digits[2..];
            if (digits.Length == 11 && digits.StartsWith("0")) return digits;
            if (digits.Length == 10 && !digits.StartsWith("0")) return "0" + digits;
            if (digits.StartsWith("00")) return "+" + digits[2..];
            return raw.Trim().StartsWith("+") ? "+" + digits : digits;
        }
    }
}
