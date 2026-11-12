namespace MatcBuddy.Business.Security
{
    /// <summary>
    /// Şifreleri BCrypt ile hash'ler ve doğrular.
    /// Eski (düz metin) kayıtlar için geçiş desteği içerir.
    /// </summary>
    public static class PasswordHasher
    {
        private const int WorkFactor = 11;

        public static string Hash(string password) =>
            BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

        public static bool IsHashed(string? stored) =>
            !string.IsNullOrEmpty(stored) && stored.StartsWith("$2") && stored.Length == 60;

        /// <summary>
        /// Şifreyi doğrular. Kayıt henüz hash'lenmemişse (eski düz metin) düz karşılaştırma yapar
        /// ve <paramref name="needsRehash"/> true döner; çağıran taraf kaydı hash'li haliyle güncellemelidir.
        /// </summary>
        public static bool Verify(string password, string? stored, out bool needsRehash)
        {
            needsRehash = false;
            if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(password))
                return false;

            if (IsHashed(stored))
            {
                try { return BCrypt.Net.BCrypt.Verify(password, stored); }
                catch (BCrypt.Net.SaltParseException) { return false; }
            }

            // Eski düz metin kayıt: sabit zamanlı karşılaştırma
            var ok = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(password),
                System.Text.Encoding.UTF8.GetBytes(stored));
            needsRehash = ok;
            return ok;
        }
    }
}
