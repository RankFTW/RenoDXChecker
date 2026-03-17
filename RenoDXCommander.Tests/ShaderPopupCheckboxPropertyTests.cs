using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

// Feature: shader-selection-popup, Property 1: Popup checkbox generation matches available packs with correct pre-selection

/// <summary>
/// Property-based tests for ShaderPopupHelper checkbox generation logic.
/// **Validates: Requirements 2.2, 2.3, 2.4**
/// </summary>
public class ShaderPopupCheckboxPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────

    /// <summary>Generates a list of unique pack entries (Id, DisplayName) with 0–10 items.</summary>
    private static Gen<IReadOnlyList<(string Id, string DisplayName, ShaderPackService.PackCategory Category)>> GenAvailablePacks()
    {
        return Gen.Choose(0, 10).SelectMany(count =>
            Gen.ListOf(count, GenPackEntry()).Select(entries =>
            {
                // Deduplicate by Id (case-insensitive)
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var unique = new List<(string Id, string DisplayName, ShaderPackService.PackCategory Category)>();
                foreach (var e in entries)
                {
                    if (seen.Add(e.Id))
                        unique.Add(e);
                }
                return (IReadOnlyList<(string Id, string DisplayName, ShaderPackService.PackCategory Category)>)unique;
            }));
    }

    /// <summary>Generates a single pack entry with a simple alphanumeric Id and DisplayName.</summary>
    private static Gen<(string Id, string DisplayName, ShaderPackService.PackCategory Category)> GenPackEntry()
    {
        return Gen.Elements(
            "PackAlpha", "PackBeta", "PackGamma", "PackDelta",
            "PackEpsilon", "PackZeta", "PackEta", "PackTheta",
            "PackIota", "PackKappa")
            .Select(id => (id, $"{id} Display", ShaderPackService.PackCategory.Extra));
    }

    /// <summary>
    /// Generates a random subset of pack IDs from the given available packs.
    /// May include IDs not in the available packs to test intersection behavior.
    /// </summary>
    private static Gen<List<string>?> GenCurrentSelection(IReadOnlyList<(string Id, string DisplayName, ShaderPackService.PackCategory Category)> packs)
    {
        // 20% chance of null selection
        var nullGen = Gen.Constant<List<string>?>(null);

        // Build a pool: available IDs + some extra "stale" IDs
        var availableIds = packs.Select(p => p.Id).ToList();
        var extraIds = new[] { "StalePack1", "StalePack2", "ObsoletePack" };
        var pool = availableIds.Concat(extraIds).ToArray();

        var subsetGen = pool.Length == 0
            ? Gen.Constant(new List<string>())
            : Gen.ListOf(pool.Length, Gen.Elements(pool))
                .Select(ids => ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        var selectionGen = subsetGen.Select(s => (List<string>?)s);

        return Gen.Frequency(
            Tuple.Create(1, nullGen),
            Tuple.Create(4, selectionGen));
    }

    // ── Property 1 ────────────────────────────────────────────────────────────

    /// <summary>
    /// For any list of available shader packs and any subset provided as current selection,
    /// ComputeCheckboxModel produces exactly one entry per available pack, and the set of
    /// pre-checked entries equals the intersection of the current selection with available packs.
    /// When current selection is null or empty, all entries are unchecked.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property CheckboxModel_MatchesAvailablePacks_WithCorrectPreSelection()
    {
        var gen = GenAvailablePacks().SelectMany(packs =>
            GenCurrentSelection(packs).Select(sel => (packs, sel)));

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (availablePacks, currentSelection) = tuple;

            // Act
            var model = ShaderPopupHelper.ComputeCheckboxModel(availablePacks, currentSelection);

            // Assert 1: checkbox count equals pack count
            if (model.Count != availablePacks.Count)
                return false.Label(
                    $"Count mismatch: expected {availablePacks.Count}, got {model.Count}");

            // Assert 2: IDs match in order
            for (int i = 0; i < availablePacks.Count; i++)
            {
                if (model[i].Id != availablePacks[i].Id)
                    return false.Label(
                        $"ID mismatch at index {i}: expected '{availablePacks[i].Id}', got '{model[i].Id}'");
            }

            // Assert 3: pre-checked set equals intersection of selection with available packs
            var expectedChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (currentSelection != null && currentSelection.Count > 0)
            {
                var availableIdSet = new HashSet<string>(
                    availablePacks.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
                foreach (var id in currentSelection)
                {
                    if (availableIdSet.Contains(id))
                        expectedChecked.Add(id);
                }
            }

            var actualChecked = new HashSet<string>(
                model.Where(m => m.IsChecked).Select(m => m.Id),
                StringComparer.OrdinalIgnoreCase);

            if (!expectedChecked.SetEquals(actualChecked))
                return false.Label(
                    $"Pre-checked mismatch: expected [{string.Join(",", expectedChecked)}], " +
                    $"got [{string.Join(",", actualChecked)}]");

            return true.Label(
                $"OK: {availablePacks.Count} packs, " +
                $"selection={currentSelection?.Count.ToString() ?? "null"}, " +
                $"checked={actualChecked.Count}");
        });
    }
}
