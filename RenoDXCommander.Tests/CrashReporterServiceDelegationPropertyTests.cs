using System.Collections.Concurrent;
using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for CrashReporterService.Log delegation.
/// Verifies that calling Log through the wrapper produces the same breadcrumb
/// entries as calling CrashReporter.Log directly.
/// </summary>
public class CrashReporterServiceDelegationPropertyTests
{
    private static ConcurrentQueue<string> GetBreadcrumbs()
    {
        var field = typeof(CrashReporter).GetField("_breadcrumbs", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find _breadcrumbs field on CrashReporter");
        return (ConcurrentQueue<string>)field.GetValue(null)!;
    }

    // Feature: static-service-interfaces, Property 1: Log delegation preserves breadcrumbs
    /// <summary>
    /// **Validates: Requirements 2.2, 8.1**
    ///
    /// For any non-null string message, calling Log(message) on a CrashReporterService
    /// instance should produce the same breadcrumb entry as calling CrashReporter.Log(message)
    /// directly — the message appears in the breadcrumb trail identically regardless of
    /// which call path is used.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Log_Delegation_Preserves_Breadcrumbs()
    {
        return Prop.ForAll(Arb.Default.NonNull<string>(), nonNullMessage =>
        {
            var message = nonNullMessage.Get;
            var service = new CrashReporterService();
            var breadcrumbs = GetBreadcrumbs();

            // Act: call Log through the service wrapper
            service.Log(message);

            // Assert: the most recent breadcrumb contains the message
            var trail = breadcrumbs.ToArray();
            var lastEntry = trail.LastOrDefault() ?? "";
            var containsMessage = lastEntry.Contains(message);

            return containsMessage
                .Label($"Expected last breadcrumb to contain '{message}', got '{lastEntry}'");
        });
    }
}
