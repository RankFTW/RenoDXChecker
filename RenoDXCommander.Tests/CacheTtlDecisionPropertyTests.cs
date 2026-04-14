// Feature: nexus-pcgw-integration, Property 10: Cache TTL decision
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for cache TTL decision.
/// For any DateTime last-write and current time, IsCacheFresh returns true
/// iff (current - lastWrite) &lt; 24 hours.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
///
/// **Validates: Requirements 1.5**
/// </summary>
public class CacheTtlDecisionPropertyTests
{
    private static readonly TimeSpan TwentyFourHours = TimeSpan.FromHours(24);

    /// <summary>
    /// Generator that produces a pair of (lastWriteUtc, currentUtc) where
    /// currentUtc >= lastWriteUtc, ensuring a non-negative time span.
    /// </summary>
    private static Arbitrary<(DateTime lastWrite, DateTime current)> DateTimePairArb()
    {
        var gen =
            from baseTicks in Gen.Choose(0, int.MaxValue)
                .Select(t => new DateTime(
                    Math.Clamp((long)t * 100_000L, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks),
                    DateTimeKind.Utc))
            from offsetHours in Gen.Choose(0, 72) // 0 to 72 hours offset
            from offsetMinutes in Gen.Choose(0, 59)
            from offsetSeconds in Gen.Choose(0, 59)
            let offset = new TimeSpan(offsetHours, offsetMinutes, offsetSeconds)
            let current = baseTicks.Ticks + offset.Ticks <= DateTime.MaxValue.Ticks
                ? baseTicks.Add(offset)
                : baseTicks
            select (lastWrite: baseTicks, current: current);

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 10: Cache TTL decision
    ///
    /// **Validates: Requirements 1.5**
    ///
    /// For any DateTime representing the cache file's last-write time and a current
    /// DateTime, IsCacheFresh returns true iff (current - lastWrite) &lt; 24 hours.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IsCacheFresh_ReturnsTrue_Iff_DifferenceIsLessThan24Hours()
    {
        return Prop.ForAll(DateTimePairArb(), ((DateTime lastWrite, DateTime current) pair) =>
        {
            var elapsed = pair.current - pair.lastWrite;
            var expected = elapsed < TwentyFourHours;

            var actual = NexusModsService.IsCacheFresh(pair.lastWrite, pair.current);

            return (actual == expected).Label(
                $"lastWrite={pair.lastWrite:O}, current={pair.current:O}, " +
                $"elapsed={elapsed}, expected={expected}, actual={actual}");
        });
    }
}
