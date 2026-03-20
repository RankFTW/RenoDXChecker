using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for VerboseLogging round-trip through the CrashReporterService wrapper.
/// Verifies that the wrapper property and the static property are always in sync.
/// </summary>
// Feature: static-service-interfaces, Property 2: VerboseLogging round-trip
public class CrashReporterServiceVerboseRoundTripPropertyTests
{
    /// <summary>
    /// **Validates: Requirements 2.4**
    ///
    /// For any boolean value, setting VerboseLogging on a CrashReporterService instance
    /// and then reading CrashReporter.VerboseLogging should return the same value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Setting_Wrapper_Syncs_To_Static()
    {
        return Prop.ForAll(Arb.Default.Bool(), value =>
        {
            var original = CrashReporter.VerboseLogging;
            try
            {
                var service = new CrashReporterService();

                // Act: set via wrapper
                service.VerboseLogging = value;

                // Assert: static property reflects the same value
                var staticValue = CrashReporter.VerboseLogging;

                return (staticValue == value)
                    .Label($"Expected CrashReporter.VerboseLogging={value}, got {staticValue}");
            }
            finally
            {
                CrashReporter.VerboseLogging = original;
            }
        });
    }

    /// <summary>
    /// **Validates: Requirements 2.4**
    ///
    /// For any boolean value, setting CrashReporter.VerboseLogging directly
    /// and then reading VerboseLogging from a CrashReporterService instance
    /// should return the same value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Setting_Static_Syncs_To_Wrapper()
    {
        return Prop.ForAll(Arb.Default.Bool(), value =>
        {
            var original = CrashReporter.VerboseLogging;
            try
            {
                var service = new CrashReporterService();

                // Act: set via static property
                CrashReporter.VerboseLogging = value;

                // Assert: wrapper reflects the same value
                var wrapperValue = service.VerboseLogging;

                return (wrapperValue == value)
                    .Label($"Expected service.VerboseLogging={value}, got {wrapperValue}");
            }
            finally
            {
                CrashReporter.VerboseLogging = original;
            }
        });
    }
}
