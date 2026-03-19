using System.IO;
using System.Security.Cryptography;

namespace NovaSetup.Services;

public sealed class HashVerificationService
{
    private readonly LoggingService? _loggingService;

    public HashVerificationService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public bool VerifyFile(string filePath, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            _loggingService?.LogInfo($"[HashVerification] No SHA256 provided for {filePath} — skipping verification.");
            return true;
        }

        try
        {
            var fileBytes = File.ReadAllBytes(filePath);
            var hash = SHA256.HashData(fileBytes);
            var computed = Convert.ToHexString(hash).ToLowerInvariant();
            var expected = expectedSha256.ToLowerInvariant().Trim();

            if (computed == expected)
            {
                _loggingService?.LogInfo($"[HashVerification] SHA256 verified OK for {filePath}.");
                return true;
            }

            _loggingService?.LogWarning(
                $"[HashVerification] SHA256 MISMATCH for {filePath}. Expected: {expected} Got: {computed}");
            return false;
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"[HashVerification] Could not verify {filePath}: {ex.Message}");
            return false;
        }
    }
}
