# Stint Update 1 (v1.1.0)

Source: the user's "Update 1.docx" and their answers to follow-up questions, approved in chat on 2026-11-10.

## Features

1. **Named colour themes.** Midnight (current), Daylight (light), Paper (sepia light), Forest, Ocean, Dusk.
   They're chosen in Settings → Appearance with preview swatches, apply instantly and are saved as `settings.theme`.
   Each theme defines background, surface, raised, text, muted, hairline, strong border, scrim, on-accent and the
   four mode accents (focus, break, timer, stopwatch). Subject colours are user data and don't change with the theme.
2. **Save to log at any time.**
   - A "Save N min to log" button sits under the controls whenever a study run has time on it, in Focus, Timer
     and Stopwatch, whether running or paused. It stops the run, logs the exact elapsed time (no minimum) and
     opens the after-session sheet. In Focus mode it ends the block early and moves on to the break.
   - Reset (and Skip during an active focus block) no longer logs. It discards the run, and a "Run discarded"
     bar with Undo shows for 6 s.
   - Unchanged: a timer or focus block that runs out still logs automatically, and closing the app mid-run still
     logs the run if it's at least 1 minute long.
   - Sessions under a minute display as "<1m" or "N sec".
3. **About page.** Opened from Settings. It shows the name, icon, version, description, author (Ammar32614),
   keyboard shortcuts, data location and links (GitHub, report a problem, MIT license). It works offline; links
   open the browser only when clicked.
4. **Custom alarm sound.** Settings → Alarm sound has Choose… (WAV/MP3/WMA), Preview and Use default. The chosen
   file is copied to `%AppData%\Stint\sounds` and its original name is shown.
5. **Size.** Ship English-only framework resources (`SatelliteResourceLanguages=en`), which takes the exe from
   72 to 66 MB. We deliberately don't depend on a separately installed .NET runtime.
6. The SmartScreen warning stays as it is, by the user's choice.

## Decisions (Claude's calls)

- Theme names and palettes.
- The Timer and Focus mid-run logging design. The user specified only "Save" for the stopwatch and left the
  timer to Claude; Focus was made consistent with both.
- Ctrl+S saves the current run.
- About opens as a child page of Settings, and Back returns to Settings.
