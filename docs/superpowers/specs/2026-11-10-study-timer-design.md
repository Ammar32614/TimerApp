# Stint — study timer redesign

## Intent

Turn the plain WPF Timer & Stopwatch into a compact, polished study companion
that feels like a finished product. The user studies with it, so beyond timing it
should remember *what* was studied and show progress over time.

The user gave a reference (a small borderless Pomodoro popup with a big ring and a
central play button) and asked for "better than this". All visual and feature
choices beyond "timer + stopwatch, study-oriented" were delegated to Claude.

## Goals

- Compact borderless popup window, custom title bar, progress ring as hero.
- Three modes: **Focus** (Pomodoro), **Timer** (plain countdown), **Stopwatch** (laps).
- Subject + "what I'm working on" description per session.
- Post-session prompt: short note + focus rating 1–5 (skippable).
- **Study log**: sessions grouped by day; edit, delete, add manually.
- **Stats**: today vs daily goal, streak, weekly bars by subject, 12-week heatmap.
- Product touches: always-on-top, mini mode, Windows notification + sound,
  keyboard shortcuts, tray icon, remembers window position, CSV export.

## Non-goals / anti-goals

- No accounts, cloud sync, site blocking, ambient sounds, gamification.
- No MessageBox popups in normal flow (that is what reads as "not a product").
- No heavy third-party UI/chart libraries.

## Constraints

- WPF on .NET 10 (`net10.0-windows`), as today. WinForms enabled only for
  `NotifyIcon` (tray + balloon notification, which Windows 11 shows as a toast).
- Data stored locally as JSON at `%AppData%\Stint\data.json`.
- Note: `.gitignore` ignores folders named `Log/`/`Logs/` — don't use them.

## Decisions (Claude's calls)

### Name & look
- Name: **Stint**.
- Dark "ink" palette: bg `#14161C`, surface `#1C1F27`, raised `#252A35`,
  text `#ECEEF3`, muted `#8A90A2`, hairline `#2C313C`.
- Mode accents: Focus amber `#F5B14C`, Break teal `#4FD1C5`,
  Timer coral `#FF7A6B`, Stopwatch violet `#9B8CFF`.
- Digits: Segoe UI Variable Display, light weight, tabular numerals.
- Window 380×600, rounded corners (Win11 DWM), drop shadow via WindowChrome.
- Ring: thin track + thick accent arc with rounded caps, animated sweep.

### Navigation
- Title bar: app mark, then icon buttons Log / Stats / Settings, then pin
  (always on top), mini, minimise, close.
- Under the title bar on the timer page: segmented control Focus · Timer · Stopwatch.
- Log, Stats, Settings are full pages inside the window with a back arrow.

### Behaviour
- Focus cycle: focus 25 / short 5 / long 15, long break every 4 focus blocks;
  all configurable. Options: auto-start breaks, auto-start focus. Dots under the
  ring show position in the cycle.
- Controls: Reset (left), big Play/Pause (centre), Skip / Lap (right).
- Only study time is logged (focus blocks, timer runs, stopwatch runs); breaks are not.
- A focus/timer block that completes is logged. Skipping or resetting a run with
  ≥ 1 minute elapsed logs the elapsed time; under 1 minute is discarded.
- Stopwatch: Reset with ≥ 1 minute elapsed logs it (laps are not stored).
- After a logged session, a slide-up sheet asks for note + rating; Skip closes it.
- Subject chip + description field sit above the ring; editable while idle.
- Default subjects: General. User can add/rename/recolour/delete in Settings.
- Streak = consecutive days with any logged study, ending today or yesterday.
- Heatmap intensity buckets relative to daily goal (0, <25%, <50%, <100%, ≥100%).
- Shortcuts: Space start/pause, R reset, L lap, S skip, 1/2/3 switch mode,
  Ctrl+L log, Ctrl+T stats, Esc back.
- Mini mode: 280×72 strip showing a small ring, the time and the play button (widened from 260×84 so a stopwatch time fits beside the controls).
- Closing the window closes the app (tray icon is for notifications + restore
  when minimised); position saved on exit.

### Code shape
- `Models/` (Session, Subject, Settings, AppData), `Services/` (DataStore,
  StatsCalculator, Notifier), `Views/` (UserControls per page), `Controls/`
  (ProgressRing), `Themes/Theme.xaml` (brushes and styles).
- Pure logic (stats, session-length rules, cycle progression) is in plain classes
  covered by an xUnit test project `TimerApp.Tests/`.

## Left to the builder
- Exact spacing, iconography (Segoe Fluent Icons glyphs), animation timings.
