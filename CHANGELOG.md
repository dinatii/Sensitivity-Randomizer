# Changelog

## 1.2.0 RC2 - 2026-09-04

- Added a polished GitHub overview and separate English/Russian detailed docs.
- Prepared independent clean source and portable win-x64 release packages.
- Added a themed clickable `Author: dinati.ru` link to the application header.
- Added the author and website to the executable version metadata.
- Added a release-signing script for a public certificate in the Windows
  certificate store or Microsoft Artifact Signing.
- The signing script uses SHA-256 with an RFC 3161 timestamp, verifies both
  project executables, rejects an obvious self-signed certificate and
  regenerates release checksums after signing.
- Documented the exact SmartScreen limitation: a valid signature establishes a
  verified publisher and lets reputation accumulate, but only Microsoft Store
  distribution avoids the first-download warning by design.

## 1.2.0 RC1 - 2026-09-04

- Renamed the product, assemblies, executables, solution, namespaces, local data
  folder and portable config format to the game-neutral Sensitivity Randomizer.
- Replaced game-specific sensitivity wording with Base sensitivity and clarified
  that DPI and base sensitivity are display inputs, while only Raw Accel
  `outputDPI` is written.
- Added a vector logo, 1024 px PNG and multi-resolution ICO. The icon is embedded
  in both the main executable and ResetGuard, and the logo appears in the header.
- Added Dark, Light and Pastel palettes with persistent selection.
- Expanded localization to Russian, English, German, Spanish, Polish and
  Ukrainian, with system-language detection and saved preference.
- Added 150-180 ms page/interface transitions and subtle hover scaling using
  opacity and render transforms only. The resize path remains animation-free.
- Preserved import of the previous local config, base-sensitivity property and
  portable config marker while new files contain the new product identity.
- Retained the deferred WPF shutdown fix and duplicate fatal-dialog suppression.
- Expanded the headless regression suite to 29 tests.

## 1.1.0 RC8 - 2026-09-03

- Replaced the active WinForms interface with a code-native WPF interface while
  preserving the Raw Accel controller, randomization engine, configuration,
  logging, localization, hotkeys and ResetGuard behavior.
- Removed the entire resize-time layout pipeline from the active executable:
  no `SplitContainer`, `FlowLayoutPanel`, `Resize`/`SizeChanged` window handler,
  deferred card sizing, native rounded Regions or recursive layout calls.
- Rebuilt the window around WPF Grid measurement, device-independent sizing,
  wrapping setting copy and a compact custom scroll surface. The minimum window
  size is a supported layout instead of a special-case code path.
- Reduced idle UI work: the status timer now runs twice per second, the graph is
  not invalidated while idle, and ResetGuard's marker file is no longer checked
  on every inactive timer tick.
- Replaced the previous clipped numeric controls and switches with lightweight
  WPF controls, added field-specific validation errors, and kept inactive
  mode-specific settings out of validation.
- The release build compiles without warnings as an x64 .NET Framework 4.7.2
  WPF executable. The 26-test core suite remains green.

## 1.1.0 RC7 - 2026-09-03

- Replaced the previous dashboard shell with the new Sensitivity Lab interface:
  a compact live strip, pill navigation, flatter setting groups, a focused
  telemetry column and a single bottom command dock.
- Removed the interactive `SplitContainer` from the active UI. The workspace now
  uses a stable percentage layout, while scroll-page reflow remains coalesced,
  and expensive settings-card measurement is deferred until an interactive
  resize drag ends.
- Added rounded candy-style surfaces, restrained teal/purple gradients, custom
  toggle switches, clearer hover states and a less box-heavy visual hierarchy.
- Expanded the validated multiplier editor and controller range from
  `0.25-2.00` to `0.10-10.00`. Defaults remain `0.50-2.00`.
- Reciprocal linking now supports the complete `0.10000 <-> 10.00000` pair.
- Changed the live multiplier chart to a logarithmic vertical scale so values
  below and above `1.000` remain visually comparable across extreme ranges.
- Added a regression covering the expanded endpoints, open generator bounds,
  exact 4/4 cycle balance and geometric centering. The suite now has 26 tests.

## 1.1.0 RC6 - 2026-09-03

- Added an optional multiplicative-symmetry link for Balanced Coverage.
- Editing either multiplier bound now recalculates the other as its reciprocal;
  `0.57000` becomes `1.75439`, and `1.75000` becomes `0.57143`.
- The link is enabled by default for Full Range, shown only when relevant, and
  persisted through saved and portable configurations.
- Linked bounds use five decimal places and are validated with `min × max = 1`.
- RC5 configurations with already reciprocal bounds enable the link during
  migration. Deliberately asymmetric custom bounds remain unchanged and unlinked.
- Added reciprocal calculation, validation and migration regressions. The suite
  now has 25 tests.

## 1.1.0 RC5 - 2026-09-03

- Replaced the default bell-shaped distribution with Balanced Coverage, which
  divides both sides of `1.000` into four logarithmic zones.
- Every shuffled eight-change cycle visits each zone exactly once: four values
  below `1.000`, four above it, with no dependence on mouse speed.
- For reciprocal bounds such as `0.50-2.00`, paired samples are reciprocal and
  every complete cycle has geometric mean `1.000`.
- Added geometric means and below/above-one shares by both change count and
  elapsed time to the session summary. Arithmetic mean remains as a clearly
  labelled reference value.
- Existing RC3 Uniform and RC4 centered-Gaussian configurations around one
  migrate to Balanced Coverage while preserving custom bounds and timing.
- Added exact zone-frequency, cycle-balance, edge-coverage and time-weighted
  statistics regressions. The suite now has 23 tests.

## 1.1.0 RC4 - 2026-09-03

- Changed Full Range and the application default from linear Uniform to a
  centered, bounded Gaussian distribution.
- Balanced Gaussian now selects the lower or upper side with equal probability
  and calibrates their half-normal spreads to keep the arithmetic mean at the
  selected center even with asymmetric bounds such as `0.50-2.00`.
- Existing v3 Uniform configurations whose range contains `1.000` migrate to
  Balanced Gaussian with mean `1.000`; their custom bounds and timing remain.
- Linear Uniform remains available as an explicitly labelled baseline and now
  warns that `0.50-2.00` has mean `1.25`.
- Added 200,000-sample regression coverage for asymmetric mean, 50/50 side
  frequency, hard bounds and upper-tail exploration. The suite now has 21 tests.

## 1.1.0 - 2026-08-27

- RC3 removes per-pixel rounded `Region` allocation and forced recursive layout
  during window resizing. Settings reflow is now coalesced to a 50 ms cadence.
- Reworked narrow setting rows so descriptions have two readable lines and
  editors keep a stable width instead of competing with the copy column.
- Added dark owner-drawn drop-down and numeric buttons, plus a consistently
  aligned header action group.
- RC2 fixes a startup exception caused by assigning `SplitContainer` panel
  minimums before WinForms had measured the modern workspace.
- Added regression coverage for both the 150 px construction width and the
  fully laid-out workspace width.
- Rebuilt the WinForms UI around a compact responsive layout with modern cards,
  clearer hierarchy and a smaller minimum window size.
- Split settings into Essential, Optional and Guide tabs.
- Mode-specific fields are now shown only when relevant: Random Walk, Gaussian,
  randomized timing, seed and recalibration controls collapse contextually.
- Added visible descriptions, detailed tooltips and live validation before Start.
- Added immediate Russian/English UI switching with persisted preference.
- Added validated import/export for portable JSON configurations, including
  compatibility with direct config files from earlier versions.
- Validation now ignores parameters that are inactive for the selected mode.
- Expanded the headless core suite from 15 to 19 tests.

## 1.0.4 - 2026-08-22

- Speed-based acceleration is now a hard block instead of a compatibility
  warning. All active Raw Accel profiles must use `Off/noaccel`.
- Constant gain is checked before and after every randomized multiplier write.
- Safety reset uses a separate path so multiplier `1.000` can still be restored
  if acceleration was enabled externally.
- Added a `ПРОВЕРИТЬ` action showing constant-gain verification, active
  multiplier readback, engine state and whether shutdown is confirmed.

## 1.0.3 - 2026-08-22

- Fixed collapsed settings groups that rendered labels one character per line
  and hid all input controls.
- Added a `Full Range 0.50-2.00` preset using Uniform randomization and made it
  the default for new configurations.
- Existing untouched `CS Standard` configurations migrate once to Full Range;
  custom configurations are preserved.
- Multiplier bounds and fixed/random timing controls are now clearly labelled
  and remain directly editable in the GUI.

## 1.0.2 - 2026-08-22

- Fixed the startup crash caused by assigning `SplitContainer` dimensions before
  WinForms had laid out the main window.
- Splitter distance and panel minimums are now applied only against the actual
  available width.

## 1.0.1 - 2026-08-22

- Added a dependency and integrity preflight before Raw Accel code is loaded.
- Startup failures now always produce a visible error and `startup.log`.
- The main window is shown before Raw Accel driver inspection begins.
- Added explicit diagnostics for incomplete ZIP extraction and missing VC++ x64 runtime.

## 1.0.0 - 2026-08-21

- Initial complete implementation from the supplied specification.
- Three randomization modes, timing, profiles, seed, timers, recalibration,
  logging, graph and session statistics.
- Official Raw Accel wrapper integration with per-write readback.
- Global F8/F9/F10 hotkeys and on-demand crash ResetGuard.
