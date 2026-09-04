# Sensitivity Randomizer

[GitHub overview](README.md) · [Русская документация](README.ru.md)

Author: [dinati.ru](https://dinati.ru/)

A game-neutral Windows utility for variable-practice aim training. It changes
Raw Accel's constant `Sensitivity Multiplier` at scheduled intervals and never
derives sensitivity from mouse movement speed.

The app does not change the mouse's physical DPI or write an in-game setting.
`Mouse DPI` and `Base sensitivity` are display inputs used only to calculate
effective sensitivity and eDPI. The actual change is made through Raw Accel's
`Profile.outputDPI`, which acts as constant gain.

## Highlights

- Balanced Coverage with eight logarithmic zones per cycle, four below and four
  above `1.000`;
- editable multiplier range from `0.10000` to `10.00000`;
- reciprocal bounds using `min × max = 1`;
- Smooth Random Walk, Centered Gaussian and Linear Uniform modes;
- fixed or randomized change intervals;
- session timer, recalibration phase and repeatable seed;
- validated JSON profile import and export;
- Russian, English, German, Spanish, Polish and Ukrainian UI languages;
- Dark, Light and Pastel themes with saved preference;
- compact WPF interface, live chart and lightweight non-layout animations;
- global F8 Start/Stop, F9 Reset and F10 Pause;
- per-write readback and a separate `SensitivityResetGuard.exe`;
- hard Start block whenever speed-based acceleration is detected.

## Installation

1. Install [Raw Accel v1.7.1](https://github.com/RawAccelOfficial/rawaccel/releases/tag/v1.7.1) and reboot Windows.
2. Install [Microsoft Visual C++ Redistributable 2015-2022 x64](https://aka.ms/vs/17/release/vc_redist.x64.exe).
3. Unblock the downloaded ZIP in Windows file properties if that option appears.
4. Extract the complete archive to a normal local folder.
5. Run `SensitivityRandomizer.exe` beside `wrapper.dll`,
   `Newtonsoft.Json.dll` and `SensitivityResetGuard.exe`.

Do not run the EXE from inside the ZIP or copy it away from its companion files.
Windows 10/11 x64 and .NET Framework 4.7.2 or newer are required.

The current RC build has no public Authenticode signature, so SmartScreen can
warn about a new download. Use `SHA256SUMS.txt` to verify the release or build
it from the included source. The EXE is small because WPF ships with .NET
Framework and the Raw Accel wrapper and JSON library are separate DLLs. The
prepared signing workflow is documented in [SIGNING.md](SIGNING.md).

## First run

1. Set `Accel Type = Off/noaccel` in every Raw Accel profile and click Apply.
2. Start the app and click Verify.
3. Confirm constant gain, no acceleration and multiplier `1.000`.
4. Configure the range, mode and timing, then click Start.

The app blocks Start if any speed-based acceleration is active. It never
silently disables an acceleration curve.

## Balanced Coverage

For the default `0.50-2.00` range, one cycle covers:

```text
0.500-0.595  0.595-0.707  0.707-0.841  0.841-1.000
1.000-1.189  1.189-1.414  1.414-1.682  1.682-2.000
```

The zone order is shuffled. Every complete cycle contains exactly four values
below and four above one, while low, middle and high portions of each side are
visited equally often.

Linked bounds recalculate the opposite value automatically:

```text
0.57000 -> 1.75439
1.75000 -> 0.57143
0.10000 <-> 10.00000
```

This is multiplicative symmetry. The geometric mean of a full linked cycle is
`1.000`. Its arithmetic mean can be above one even though the below/above count
is exactly `50/50`; reports show both means and both count- and time-based
balance.

## Interface and shutdown

The Essential page contains mode, range and timing. Optional contains display
calculations, duration, seed and logging. Guide explains setup and safe
shutdown. Parameters irrelevant to the selected mode are hidden.

Language and theme selectors are in the header and persist in `config.json`.
Animations run only on page/interface entrance and button hover. Layout is not
animated while the window is resized.

During a session, status should read RUNNING, the multiplier and chart should
change, and Raw Accel readback should match the displayed value. To stop, click
Stop or Reset, wait for OFF and `1.000`, then click Verify. A normal window close
performs the same reset before exiting. There is no tray mode or autostart.

User files are stored under:

```text
%LocalAppData%\SensitivityRandomizer\
```

Version 1.2 imports the previous local config on first launch and accepts the
legacy base-sensitivity property and portable config marker. New files use only
the new product name.

## Safety and anti-cheat scope

Every write reads the active configuration, changes only
`outputDPI = 1000 × multiplier`, activates it, then verifies all profiles with a
second read. Stop, Reset, timer completion and normal close restore `1.000`.
ResetGuard retries after an abnormal exit.

The app does not read game memory, inject DLLs, inspect the screen, move the
mouse or synthesize input. It controls Raw Accel outside the game process. This
does not guarantee acceptance by any specific anti-cheat. An unsigned custom
EXE is not automatically approved by FACEIT; obtain written confirmation or do
not run it with the anti-cheat during protected matches.

Build requirements are Visual Studio 2022 Build Tools, the `.NET desktop build
tools` workload and the .NET Framework 4.7.2 targeting pack. Run:

```powershell
PowerShell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release
```
