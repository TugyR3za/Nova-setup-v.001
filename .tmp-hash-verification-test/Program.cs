using System;
using System.IO;
using System.Security.Cryptography;
using NovaSetup.Services;

internal static class Program
{
    private static void Main()
    {
        RunHashVerificationSmokeTest();
    }

    private static void RunHashVerificationSmokeTest()
    {
        var logger = new LoggingService();
        var verifier = new HashVerificationService(logger);
        var tempFile = Path.Combine(Path.GetTempPath(), $"nova-hash-test-{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllText(tempFile, "NovaSetup hash verification smoke test");
            var correctHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(tempFile))).ToLowerInvariant();
            var wrongHash = new string('0', 64);

            var correctResult = verifier.VerifyFile(tempFile, correctHash);
            Console.WriteLine($"Correct hash case: {(correctResult ? "PASS" : "FAIL")}");

            var wrongResult = verifier.VerifyFile(tempFile, wrongHash);
            Console.WriteLine($"Wrong hash case: {(!wrongResult ? "PASS" : "FAIL")}");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
