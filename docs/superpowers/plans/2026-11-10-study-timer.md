# Stint Study Timer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the WPF Timer & Stopwatch as "Stint", a compact, polished study timer with Focus/Timer/Stopwatch modes, a study log and stats.

**Architecture:** Pure logic (models, persistence, focus cycle, stats, CSV) lives in plain C# classes under `Models/` and `Services/`, unit-tested by `TimerApp.Tests/`. UI is a single borderless `MainWindow` hosting page `UserControl`s from `Views/`, styled by `Themes/Theme.xaml`, with a custom `Controls/ProgressRing`.

**Tech Stack:** .NET 10, WPF, WinForms `NotifyIcon` only, System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-10-study-timer-design.md`

## Global Constraints

- Target `net10.0-windows`; `UseWPF` true; `UseWindowsForms` true (tray only). No other NuGet packages in the app.
- Data at `%AppData%\Stint\data.json`.
- No `MessageBox` in normal flow.
- Don't create folders named `Log`/`Logs` (gitignored).
- Root `TimerApp.csproj` must exclude `TimerApp.Tests/**` and `docs/**` from compile.
- Palette, accents, shortcuts, defaults: exactly as listed in the spec.
- Namespace root stays `TimerApp`.

## Review Focus

1. Corrupt or hand-edited `data.json` → app starts with defaults, the bad file is kept as `data.corrupt-<yyyyMMddHHmmss>.json`, nothing crashes. (Task 1 test)
2. Session that starts before midnight and ends after → counted on its start day; streak/heatmap stay consistent. (Task 2 test)
3. Notes/descriptions containing commas, quotes, newlines → CSV still parses one row per session. (Task 2 test)
4. Deleting a subject that has sessions → those sessions fall back to "General" in log/stats; "General" can't be deleted. (Task 1 test)
5. Saved window position off-screen (monitor unplugged) → window re-centres. (Task 5 manual check)

---

### Task 1: Models and DataStore

**Files:**
- Create: `Models/Session.cs`, `Models/Subject.cs`, `Models/AppSettings.cs`, `Models/AppData.cs`, `Services/DataStore.cs`
- Create: `TimerApp.Tests/TimerApp.Tests.csproj`, `TimerApp.Tests/DataStoreTests.cs`
- Modify: `TimerApp.csproj` (add `UseWindowsForms`, `AssemblyName`=`Stint`, compile excludes)

**Interfaces — Produces:**
- `enum SessionMode { Focus, Timer, Stopwatch }`
- `class Session { Guid Id; DateTime Start; int Seconds; SessionMode Mode; Guid SubjectId; string Description = ""; string Note = ""; int Rating /*0 = none, 1–5*/ }`
- `class Subject { Guid Id; string Name; string Color /*"#RRGGBB"*/ }`; `static readonly Guid Subject.GeneralId = Guid.Parse("00000000-0000-0000-0000-000000000001")`
- `class AppSettings { int FocusMinutes=25; int ShortBreakMinutes=5; int LongBreakMinutes=15; int LongBreakEvery=4; bool AutoStartBreaks; bool AutoStartFocus; int DailyGoalMinutes=120; bool Topmost; bool SoundOn=true; int TimerSeconds=600; Guid LastSubjectId=Subject.GeneralId; double? WindowLeft; double? WindowTop }`
- `class AppData { int Version=1; List<Session> Sessions; List<Subject> Subjects; AppSettings Settings; static AppData CreateDefault(); Subject SubjectFor(Guid id) /*falls back to General*/; bool DeleteSubject(Guid id) /*false for General*/ }`
- `class DataStore(string path) { static string DefaultPath; AppData Load(); void Save(AppData data) }`

- [ ] **Step 1: Write failing tests** in `DataStoreTests`:
  - `Load_MissingFile_ReturnsDefaultsWithGeneral` → `Subjects` has one item with `Id == Subject.GeneralId`, `Settings.FocusMinutes == 25`.
  - `SaveThenLoad_RoundTripsSession` → session fields equal after reload.
  - `Load_CorruptFile_ReturnsDefaultsAndBacksUp` → write `"{not json"`, Load returns defaults, a file matching `data.corrupt-*.json` exists in the directory.
  - `DeleteSubject_General_ReturnsFalse`; `SubjectFor_DeletedId_ReturnsGeneral`.
- [ ] **Step 2:** `dotnet test TimerApp.Tests` → FAIL (types missing).
- [ ] **Step 3:** Implement. Save writes `<path>.tmp` then `File.Move(tmp, path, overwrite: true)`; creates directory. JSON: camelCase, enums as strings, indented.
- [ ] **Step 4:** `dotnet test TimerApp.Tests` → all pass.
- [ ] **Step 5:** Commit `feat: add data models and JSON store`.

### Task 2: FocusCycle, StatsCalculator, CsvExporter

**Files:**
- Create: `Services/FocusCycle.cs`, `Services/StatsCalculator.cs`, `Services/CsvExporter.cs`
- Test: `TimerApp.Tests/FocusCycleTests.cs`, `TimerApp.Tests/StatsTests.cs`, `TimerApp.Tests/CsvTests.cs`

**Interfaces — Consumes:** Task 1 models. **Produces:**
- `enum Phase { Focus, ShortBreak, LongBreak }`
- `class FocusCycle { Phase Current; int CompletedFocus; void Advance(int longBreakEvery); void Reset(); static TimeSpan DurationOf(Phase p, AppSettings s); int DotIndex(int longBreakEvery) /*0-based focus slot in current cycle*/ }`
- `static class SessionRules { static bool ShouldLog(TimeSpan elapsed) /* >= 60 s */ }`
- `static class StatsCalculator { int SecondsOn(IEnumerable<Session>, DateOnly day); int Streak(IEnumerable<Session>, DateOnly today); IReadOnlyList<(DateOnly Day, Dictionary<Guid,int> BySubject)> Week(IEnumerable<Session>, DateOnly lastDay); int[] HeatLevels(IEnumerable<Session>, DateOnly today, int goalMinutes, int days = 84); int Level(int seconds, int goalMinutes) }`
- `static class CsvExporter { string ToCsv(IEnumerable<Session>, AppData data) }` header `Date,Start,Minutes,Mode,Subject,Description,Note,Rating`

- [ ] **Step 1: Write failing tests:**
  - `Advance_FourFocus_GivesLongBreak` (every=4): F→S→F→S→F→S→F→L→F; `CompletedFocus` counts focus completions.
  - `ShouldLog_59s_False_60s_True`.
  - `Streak_TodayAndYesterday_Is2`; `Streak_OnlyYesterday_Is1`; `Streak_GapTwoDaysAgo_Is0WhenNothingTodayOrYesterday`.
  - `SecondsOn_SessionCrossingMidnight_CountsOnStartDay` (start 23:50, 1200 s).
  - `Level_Buckets` with goal 120: 0→0, 60s→1, 30min→2, 60min→3, 120min→4.
  - `HeatLevels_Returns84_LastIsToday`.
  - `Week_Returns7Days_EndingLastDay_GroupedBySubject`.
  - `ToCsv_EscapesCommaQuoteNewline` → field `a,"b"\nc` written as `"a,""b""\nc"`; header line exact.
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** `dotnet test` → pass.
- [ ] **Step 5:** Commit `feat: add focus cycle, stats and CSV export`.

### Task 3: Theme, ProgressRing, window shell

**Files:**
- Create: `Themes/Theme.xaml`, `Controls/ProgressRing.cs`, `Services/WindowEffects.cs`
- Modify: `App.xaml` (merge theme), `MainWindow.xaml`, `MainWindow.xaml.cs`

**Produces:**
- `ProgressRing : FrameworkElement` with DPs `double Progress` (0–1), `Brush TrackBrush`, `Brush ArcBrush`, `double Thickness`; draws in `OnRender` with round caps.
- `WindowEffects.ApplyRoundedCorners(Window)` (DWMWA_WINDOW_CORNER_PREFERENCE=33, DWMWCP_ROUND=2).
- Theme keys: brushes `Bg, Surface, Raised, Text, Muted, Hairline, AccentFocus, AccentBreak, AccentTimer, AccentStopwatch`; styles `IconButton`, `PrimaryRoundButton`, `GhostButton`, `Segmented` (RadioButton), `FieldBox`, `Chip`, `Card`; fonts `DisplayFont`.
- MainWindow: `WindowStyle=None`, `WindowChrome` caption height 40, title bar row, `ContentControl x:Name="PageHost"`; `void Navigate(UserControl page)`, `void GoHome()`.

- [ ] Build: `dotnet build` → 0 errors. Launch and check title bar drag, min, close, rounded corners.
- [ ] Commit `feat: new themed borderless window shell`.

### Task 4: Timer page (Focus / Timer / Stopwatch)

**Files:** Create `Views/TimerPage.xaml(.cs)`, `Views/SessionSheet.xaml(.cs)`; delete old tab UI from MainWindow.

**Consumes:** FocusCycle, SessionRules, AppData, DataStore (via `App.Data` / `App.Store` static properties added in `App.xaml.cs`: `static AppData Data`, `static DataStore Store`, `static void SaveData()`).

Behaviour per spec. Timer page public API used by shortcuts: `void ToggleRun(); void ResetCurrent(); void SkipOrLap(); void SetMode(SessionMode m)`; event `PhaseCompleted(string title, string body)` for notifier.
- Timer mode: tap the time to edit `HH:MM:SS` with three `FieldBox`es while idle; stored in `Settings.TimerSeconds`.
- SessionSheet: overlay sliding up from bottom; 5 star/dot rating, note box, Save / Skip; writes Note/Rating onto the just-logged session.
- [ ] Build and manually run each mode; verify a 1-min focus (temporarily set in settings) logs a session to `data.json`.
- [ ] Commit `feat: timer page with focus cycle, countdown and stopwatch`.

### Task 5: Log, Stats, Settings pages + product touches

**Files:** Create `Views/LogPage`, `Views/SessionEditor` (add/edit dialog as in-window overlay), `Views/StatsPage`, `Views/SettingsPage`, `Services/Notifier.cs`.

- Log: grouped by day (header "Today", "Yesterday", `dddd, d MMM`), day total; row = subject colour dot, description, note, time range, duration, rating; edit/delete on hover; "+ Add" and "Export CSV" (SaveFileDialog).
- Stats: tiles Today (h m of goal + bar), Streak (days), This week total; 7-day stacked bars by subject drawn with `Rectangle`s; 12-week heatmap (7 rows × 12 cols) with accent opacity per level.
- Settings: durations, long-break-every, auto-starts, daily goal, sound, always-on-top; subjects list with add/rename/colour (8 preset swatches)/delete.
- Notifier: `NotifyIcon` with app icon, `Show(string title, string body)` balloon + `C:\Windows\Media\Alarm01.wav` if exists and `SoundOn`, else `SystemSounds.Asterisk`. Double-click tray restores.
- Shortcuts (spec list) in `MainWindow.PreviewKeyDown`, ignored when focus is in a TextBox (except Esc).
- Mini mode toggle (260×84). Position saved on close; restored only if inside `SystemParameters.VirtualScreen*` bounds (Review Focus 5).
- [ ] Build, run, click through every page; `dotnet test` still green.
- [ ] Commit `feat: study log, stats, settings, tray and shortcuts`.
