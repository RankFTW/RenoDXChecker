using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

// Feature: file-write-retry-helper, Property 2: IOException retry with bounded attempts
// **Validates: Requirements 1.3, 1.4, 1.6, 3.1, 3.2, 3.3**

/// <summary>
/// Property-based tests for FileHelper.WriteAllTextWithRetry IOException retry behavior.
/// For any number of transient IOException failures (0–3), verify the method attempts
/// exactly min(n+1, 3) writes, never throws, and succeeds when an attempt succeeds.
/// </summary>
public class FileHelperIOExceptionRetryPropertyTests : IDisposable
{
    private readonly string _tempDir;

    public FileHelperIOExceptionRetryPropertyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FileHelperRetryTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    /// <summary>
    /// A testable write delegate that throws IOException for the first N calls,
    /// then succeeds by writing to disk.
    /// </summary>
    private sealed class FaultingWriter
    {
        private readonly int _failCount;
        private int _callCount;

        public int CallCount => _callCount;

        public FaultingWriter(int failCount)
        {
            _failCount = failCount;
        }

        public void Write(string path, string content)
        {
            _callCount++;
            if (_callCount <= _failCount)
                throw new IOException($"Simulated transient IO failure on attempt {_callCount}");
            File.WriteAllText(path, content);
        }
    }

    /// <summary>
    /// Generates the number of transient IOException failures: 0, 1, 2, or 3.
    /// </summary>
    private static readonly Gen<int> GenFailCount = Gen.Choose(0, 3);

    /// <summary>
    /// Generates non-null content strings.
    /// </summary>
    private static readonly Gen<string> GenContent =
        Gen.OneOf(
            Gen.Constant(""),
            Gen.Constant("hello world"),
            Arb.Default.NonNull<string>().Generator.Select(s => s.Get)
        );

    [Property(MaxTest = 100)]
    public Property IOException_Retry_BoundedAttempts()
    {
        return Prop.ForAll(
            Arb.From(GenFailCount),
            Arb.From(GenContent),
            (int failCount, string content) =>
            {
                var filePath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.txt");
                var writer = new FaultingWriter(failCount);

                // Should never throw
                var threw = false;
                try
                {
                    FileHelper.WriteAllTextWithRetry(filePath, content, "Test.Retry", writer.Write);
                }
                catch
                {
                    threw = true;
                }

                var expectedAttempts = Math.Min(failCount + 1, 3);
                var neverThrows = !threw;
                var correctAttempts = writer.CallCount == expectedAttempts;

                // If fewer than 3 failures, the write should succeed and file should contain content
                var contentCorrect = true;
                if (failCount < 3)
                {
                    contentCorrect = File.Exists(filePath) && File.ReadAllText(filePath) == content;
                }

                return neverThrows
                    .Label("Method must never throw")
                    .And(correctAttempts
                        .Label($"Expected {expectedAttempts} attempts, got {writer.CallCount} (failCount={failCount})"))
                    .And(contentCorrect
                        .Label($"Content mismatch or file missing when failCount={failCount}"));
            });
    }
}
