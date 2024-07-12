using System.Security.Cryptography;
using System.Text;

namespace EHR;

public class HashAuth(string hashValue, string salt = null, HashAlgorithm algorithm = null)
{
    private readonly HashAlgorithm algorithm = algorithm ?? SHA256.Create();

    public bool CheckString(string value)
    {
        var hash = CalculateHash(value);
        return hashValue == hash;
    }

    private string CalculateHash(string source) => CalculateHash(source, salt, algorithm);

    private static string CalculateHash(string source, string salt = null, HashAlgorithm algorithm = null)
    {
        // 0. Initialize algorithm
        algorithm ??= SHA256.Create();

        // 1. Apply salt
        if (salt != null) source += salt;

        // 2. Convert source to a byte array
        var sourceBytes = Encoding.UTF8.GetBytes(source);

        // 3. Hash sourceBytes
        var hashBytes = algorithm.ComputeHash(sourceBytes);

        // 4. Convert hashBytes to a string
        var sb = new StringBuilder();
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("x2")); // Convert each byte to 2-digit hexadecimal notation
        }

        return sb.ToString();
    }

    // To check the hash value, create an instance after hashing
    // This is only for checking the hash value and testing the operation at the same time. Do not use after checking.
    public static HashAuth CreateByUnhashedValue(string value, string salt = null)
    {
        // 1. Calculate hash value
        var algorithm = SHA256.Create();
        string hashValue = CalculateHash(value, salt, algorithm);

        // 2. Log output of hash value
        // With salt: Hash value calculation result: <value> => <hashValue> (salt: <saltValue>)
        // Without salt: Hash value calculation result: <value> => <hashValue>
        Logger.Info($"Hash value calculation result: {value} => {hashValue} {(salt == null ? string.Empty : $"(salt: {salt})")}", "HashAuth");
        Logger.Warn("Please paste the above values into the source code.", "HashAuth");

        // 3. Create and return a HashAuth instance
        return new(hashValue, salt, algorithm);
    }
}