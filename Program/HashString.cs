using System.Security.Cryptography;

namespace TASA.Program
{
    public class HashString
    {
        private const int Iterations = 600000;
        private const int HashSize = 32;
        private const int SaltSize = 16;

        private static byte[] Rfc2898(string input, byte[] saltBytes)
        {
            return new Rfc2898DeriveBytes(
                    input,
                    saltBytes,
                    Iterations,
                    HashAlgorithmName.SHA256
                )
                .GetBytes(HashSize);
        }

        public class HashVM
        {
            public string Hash { get; set; } = string.Empty;
            public string Salt { get; set; } = string.Empty;
        }
        public static HashVM Hash(string password)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898(password, saltBytes);
            return new HashVM() { Hash = Convert.ToBase64String(hash), Salt = Convert.ToBase64String(saltBytes) };
        }

        public static bool Verify(string input, string hash, string salt)
        {
            var hashBytes = Convert.FromBase64String(hash);
            var saltBytes = Convert.FromBase64String(salt);
            var inputHash = Rfc2898(input, saltBytes);
            if (inputHash.Length != hashBytes.Length)
            {
                return false;
            }
            return CryptographicOperations.FixedTimeEquals(inputHash, hashBytes);
        }
    }
}
