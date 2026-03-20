# Refactoring Roadmap

## ✅ 1. Extract duplicated file-write retry logic into a shared helper
- **Status:** Done
- **Risk:** Low
- **Spec:** `.kiro/specs/file-write-retry-helper/`
- Extracted `FileHelper.WriteAllTextWithRetry` and replaced 6 call sites

## ✅ 2. Fix blocking async patterns (`.Wait()`, `.Result`)
- **Status:** Done
- **Risk:** Medium
- **Impact:** High — prevents potential deadlocks, improves responsiveness
- Converted `GameInitializationService.DetectAllGamesDeduped()` to `DetectAllGamesDedupedAsync()` using `await Task.WhenAll()` instead of `.Wait()`
- Replaced all `.Result` accesses in `MainViewModel.InitializeAsync` with proper `await` on already-awaited tasks
- Updated `IGameInitializationService` interface to match

## 3. Add logging to bare catch blocks
- **Status:** Not started
- **Risk:** Low
- **Impact:** Medium — improves debuggability
- `GameDetectionService` catches `SecurityException` with no logging
- `MainViewModel` has `catch { _lumaMods = new(); }` with no logging
- Add `CrashReporter.Log` calls to all silent catch blocks

## 4. Break up MainWindow.xaml.cs (~3400 lines)
- **Status:** Not started
- **Risk:** Medium-High
- **Impact:** High — most impactful for maintainability
- Extract event handler logic, flyout building, and business logic into dedicated builders/services
- Follow the pattern already established by `CardBuilder` and `DetailPanelBuilder`

## 5. Wrap static services behind interfaces
- **Status:** Not started
- **Risk:** Medium
- **Impact:** Medium — cleaner DI, better testability
- `CrashReporter` — static methods used everywhere
- `AuxInstallService` — static methods used everywhere
- Wrap behind interfaces and inject via existing DI container

## 6. Minor cleanup
- **Status:** Not started
- **Risk:** Low
- **Impact:** Low
- Hardcoded title bar colors in `MainWindow` constructor → reference theme resources
- Unused `using` directives scattered across files
