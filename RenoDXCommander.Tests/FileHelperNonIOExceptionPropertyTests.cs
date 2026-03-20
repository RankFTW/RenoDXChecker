using System.Collections.Concurrent;
using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

// Feature: file-write-retry-helper, Property 3: Non-IOException halts and logs
// **Validates: Requirements 1.5, 3.4**

/// <summary>
/// Property-based tests for FileHelper.WriteAllTextWithRetry non-IOException behavior.
/// For any attempt number (0, 1, or 2) and any non-IOException exception type,
/// verify the method stops retrying immediately, logs via CrashReporter with the
/// caller tag and exception message, and does not throw.
/// </summary>
[Collection("CrashReporterState")]
public class FileHelperNonIOExceptionPropertyTests
{
    /// <summary>
    /// Reflectively accesses the CrashReporter._breadcrumbs queue to verify log output.
    /// </summary>
    private static ConcurrentQueue<string> GetBreadcrumbs()
    {
        var field = typeof(CrashReporter).GetField("_breadcrumbs", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find CrashReporter._breadcrumbs field");
        return (ConcurrentQueue<string>)field.GetValue(null)!;
    }

    /// <summary>
    /// A write delegate that throws IOException on attempts before the target,
    /// then throws a specified non-IOException on the target attempt.
    /// </summary>
    private sealed class NonIOFaultingWriter
    {
        private readonly int _nonIOAttempt;
        private readonly Exception _nonIOException;
        private int _callCount;

        public int CallCount => _callCount;

        public NonIOFaultingWriter(int nonIOAttempt, Exception nonIOException)
        {
            _nonIOAttempt = nonIOAttempt;
            _nonIOException = nonIOException;
        }

        public void Write(string path, string content)
        {
            var current = _callCount++;
            if (current < _nonIOAttempt)
                throw new IOException($"Simulated transient IO failure on attempt {current}");
            throw _nonIOException;
        }
    }

    /// <summary>
    /// Generates the attempt number (0, 1, or 2) on which the non-IOException occurs.
    /// </summary>
    private static readonly Gen<int> GenAttemptNumber = Gen.Choose(0, 2);

    /// <summary>
    /// Generates non-IOException exception instances with random messages.
    /// </summary>
    private static readonly Gen<Exception> GenNonIOException =
        Gen.OneOf(
            Arb.Default.NonNull<string>().Generator.Select(s =>
                (Exception)new UnauthorizedAccessException(s.Get)),
            Arb.Default.NonNull<string>().Generator.Select(s =>
                (Exception)new ArgumentException(s.Get)),
            Arb.Default.NonNull<string>().Generator.Select(s =>
                (Exception)new InvalidOperationException(s.Get)),
            Arb.Default.NonNull<string>().Generator.Select(s =>
                (Exception)new NotSupportedException(s.Get))
        );

    /// <summary>
    /// Generates non-empty caller tags.
    /// </summary>
    private static readonly Gen<string> GenCallerTag =
        Gen.OneOf(
            Gen.Constant("TestService.Save"),
            Gen.Constant("ModInstallService.SaveDb"),
            Arb.Default.NonNull<string>().Generator
                .Where(s => !string.IsNullOrWhiteSpace(s.Get))
                .Select(s => s.Get)
        );

    [Property(MaxTest = 100)]
    public Property NonIOException_HaltsImmediately_LogsAndDoesNotThrow()
    {
        return Prop.ForAll(
            Arb.From(GenAttemptNumber),
            Arb.From(GenNonIOException),
            Arb.From(GenCallerTag),
            (int attemptNumber, Exception nonIOEx, string callerTag) =>
            {
                // Use a unique marker so we can find our specific log entry even under
                // concurrent test execution with a shared CrashReporter._breadcrumbs queue.
                var uniqueMarker = Guid.NewGuid().ToString("N");
                var tagWithMarker = $"{callerTag}_{uniqueMarker}";

                var writer = new NonIOFaultingWriter(attemptNumber, nonIOEx);

                // Should never throw
                var threw = false;
                try
                {
                    FileHelper.WriteAllTextWithRetry("dummy.txt", "content", tagWithMarker, writer.Write);
                }
                catch
                {
                    threw = true;
                }

                // The method should have made exactly (attemptNumber + 1) calls:
                // attemptNumber IOExceptions, then 1 non-IOException that halts it
                var expectedCalls = attemptNumber + 1;
                var correctCallCount = writer.CallCount == expectedCalls;

                // Verify CrashReporter.Log was called with the caller tag and exception message.
                // Search the entire breadcrumbs queue for our unique marker to avoid
                // snapshot-based race conditions with concurrent tests.
                var breadcrumbs = GetBreadcrumbs();
                var allEntries = breadcrumbs.ToArray();
                var hasLogEntry = allEntries.Any(entry =>
                    entry.Contains(uniqueMarker) && entry.Contains(nonIOEx.Message));

                return (!threw)
                    .Label("Method must never throw")
                    .And(correctCallCount
                        .Label($"Expected {expectedCalls} calls, got {writer.CallCount} (attemptNumber={attemptNumber})"))
                    .And(hasLogEntry
                        .Label($"CrashReporter.Log must contain marker '{uniqueMarker}' and exception message '{nonIOEx.Message}'."));
            });
    }
}
