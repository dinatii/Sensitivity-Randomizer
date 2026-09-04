<p align="center">
  <img src="assets/SensitivityRandomizer-logo.png" width="128" alt="Sensitivity Randomizer logo">
</p>

<h1 align="center">Sensitivity Randomizer</h1>

<p align="center">
  A game-independent mouse sensitivity randomizer for Windows, built for variable-practice aim training.
</p>

<p align="center">
  <img alt="Version 1.2.0 RC2" src="https://img.shields.io/badge/version-1.2.0--rc2-7c72e8">
  <img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-2188ff">
  <img alt="Platform x64" src="https://img.shields.io/badge/platform-x64-lightgrey">
  <img alt="License MIT" src="https://img.shields.io/badge/license-MIT-2ea44f">
</p>

<p align="center">
  <a href="../../releases/latest"><strong>Download latest release</strong></a>
  · <a href="#installation">Installation</a>
  · <a href="#first-setup">First setup</a>
  · <a href="#safety-and-anti-cheat">Safety</a>
</p>

<p align="center">
  <a href="README.ru.md">Русская документация</a>
  · <a href="README.en.md">Detailed English documentation</a>
</p>

---

## What is it?

Sensitivity Randomizer periodically changes a constant mouse-input multiplier
inside a configurable range. It works through the installed
[Raw Accel](https://github.com/RawAccelOfficial/rawaccel) driver, so the same
training profile can be used across games, aim trainers and ordinary desktop
input.

It is a variable-practice tool, not an in-game sensitivity editor. The app does
not change your mouse's physical DPI and does not write sensitivity settings to
any game. Its DPI and base-sensitivity fields are display inputs used only to
calculate effective sensitivity and eDPI.

> **No speed-based acceleration.** Every selected multiplier stays constant
> regardless of how quickly the mouse moves. The app refuses to start a session
> if a speed-based Raw Accel profile is detected.

## Features

- **Balanced Coverage:** eight logarithmic zones per cycle, visited once each,
  with exactly four values below and four above `1.000`;
- editable multiplier range from `0.10000` to `10.00000`;
- optional reciprocal bounds satisfying `min × max = 1`;
- Smooth Random Walk, Centered Gaussian and Linear Uniform alternatives;
- fixed or randomized change intervals, session timer and recalibration phase;
- deterministic seed option for repeatable sessions;
- verified JSON configuration import and export;
- Russian, English, German, Spanish, Polish and Ukrainian interfaces;
- Dark, Light and Pastel themes;
- live logarithmic chart, local CSV/JSON logs and session statistics;
- readback verification after writes and an independent ResetGuard fallback;
- portable app with no background service, tray process or autostart entry.

## How it works

Your ordinary sensitivity is the `1.000` baseline:

```text
0.500x = half of the baseline
1.000x = the baseline
1.750x = 75% above the baseline
2.000x = twice the baseline
```

At each scheduled change, the app writes a new constant `outputDPI` multiplier
to Raw Accel and verifies the result. The multiplier remains unchanged until the
next scheduled write.

### Balanced Coverage

Balanced Coverage divides the configured interval into eight equal zones in
logarithmic space. A cycle samples each zone exactly once in shuffled order.
This gives regular exposure to low, middle and high parts of the entire range,
while guaranteeing an equal count below and above `1.000` in every complete
cycle.

This balance is multiplicative, not an arithmetic-mean guarantee. If you want
the endpoints to be equally far from the baseline in multiplicative terms,
enable reciprocal linking. For example:

```text
0.50000 ↔ 2.00000
0.57000 ↔ 1.75439
0.33333 ↔ 3.00000
```

## Requirements

- Windows 10 or Windows 11, 64-bit;
- [Raw Accel v1.7.1](https://github.com/RawAccelOfficial/rawaccel/releases/tag/v1.7.1)
  installed, followed by a Windows restart;
- [Microsoft Visual C++ Redistributable 2015–2022 x64](https://aka.ms/vs/17/release/vc_redist.x64.exe);
- .NET Framework 4.7.2 or newer.

Raw Accel should be downloaded only from its
[official repository](https://github.com/RawAccelOfficial/rawaccel). Version
1.7.1 is the version tested with this release.

## Installation

### 1. Install Raw Accel

1. Download `RawAccel_v1.7.1.zip` from the
   [official release](https://github.com/RawAccelOfficial/rawaccel/releases/tag/v1.7.1).
2. Extract it, run `installer.exe`, and restart Windows.
3. Open the Raw Accel GUI once. For every active profile, set acceleration to
   **Off / noaccel**, apply the profile, and confirm the ordinary multiplier is
   `1.000`.

After installation and the required restart, Sensitivity Randomizer needs only
the installed driver and its own bundled `wrapper.dll`. The downloaded Raw Accel
ZIP and extracted folder may be deleted. That folder also contains Raw Accel's
GUI and `uninstaller.exe`, so download the official package again if you later
need either one.

### 2. Install Sensitivity Randomizer

1. Download `Sensitivity-Randomizer-v1.2.0-rc2-win-x64.zip` from GitHub
   **Releases**.
2. If Windows shows an **Unblock** checkbox in the ZIP properties, enable it.
3. Extract the entire archive to an ordinary local folder. Do not run the EXE
   from inside the ZIP.
4. Keep these files together:

   ```text
   SensitivityRandomizer.exe
   SensitivityResetGuard.exe
   wrapper.dll
   Newtonsoft.Json.dll
   ```

5. Run `SensitivityRandomizer.exe`.

The main executable is intentionally small because the interface and program
logic use the Windows .NET Framework; required native and JSON components are
shipped beside it.

## First setup

1. Click **Verify** before starting a session.
2. Continue only if the app confirms constant gain, acceleration **Off**, and a
   current multiplier of `1.000`.
3. Choose **Balanced Coverage** for even coverage of the configured range.
4. Set minimum and maximum multipliers. Enable reciprocal linking if desired.
5. Choose a fixed or randomized interval and optional session timer.
6. Start the randomizer, then begin the game or training session.

A moderate reciprocal range is `0.75–1.33333`. A wider example is `0.50–2.00`.
Extreme values up to `0.10–10.00` are supported, but can make desktop control
difficult. `F9` always requests an emergency reset to `1.000`.

## Configuration

Only the multiplier range and a change schedule are required. Mode-specific
options appear only when relevant.

| Control | Purpose |
|---|---|
| Minimum / maximum | Limits for the constant multiplier |
| Reciprocal link | Maintains `min × max = 1` when either endpoint changes |
| Randomization mode | Selects coverage behavior between changes |
| Change interval | Fixed duration or randomized minimum/maximum duration |
| Session timer | Stops and restores the baseline when time expires |
| Recalibration | Optionally returns to `1.000` between random values |
| Seed | Repeats a deterministic sequence for comparison |
| Mouse DPI / base sensitivity | Display-only effective-sensitivity calculation |

Use **Export** to save a validated portable JSON profile and **Import** to load
one. Import never silently accepts unsupported versions or invalid ranges.

## Checking operation and shutdown

- The status strip and live graph show the multiplier last read back from Raw
  Accel, not merely the value requested by the UI.
- **Verify** checks the installed driver, profile type, constant-gain state and
  current multiplier.
- **Stop**, timer completion and **F9 Reset** write `1.000` and verify it.
- Closing the main window normally restores and verifies `1.000`, then fully
  exits. The app has no tray mode and does not start with Windows.
- If the main process crashes during an active session, ResetGuard independently
  retries restoration to `1.000` and exits after completing its task.

You can confirm shutdown in Task Manager: neither `SensitivityRandomizer.exe`
nor `SensitivityResetGuard.exe` should remain after normal closure.

## Safety and anti-cheat

The official Raw Accel project describes its signed driver as anti-cheat
friendly. Sensitivity Randomizer is a separate, unsigned third-party utility.
It is not affiliated with or approved by Raw Accel, any game publisher, league,
tournament organiser or anti-cheat vendor.

An unsigned filename alone is not proof of cheating, but nobody outside an
anti-cheat vendor can guarantee that custom software will never be flagged or
that policies will remain unchanged. Check the current rules before protected or
competitive play. Test setup and restoration outside a protected match first.

## Windows SmartScreen

The RC2 executables are currently **unsigned**. Microsoft Defender SmartScreen
may therefore show an “unknown publisher” or reputation warning even when local
antivirus scanning finds no threat. Renaming, repacking or self-signing the EXE
cannot legitimately remove that warning.

For this release:

1. download only from this repository's Releases page;
2. compare the files against `SHA256SUMS.txt`;
3. scan the archive with Microsoft Defender or another service you trust;
4. proceed only if you understand and accept the warning.

A future release signed with a trusted code-signing certificate can show a
verified publisher and build reputation, but SmartScreen reputation is still
controlled by Microsoft. See [SIGNING.md](SIGNING.md) for the release-signing
workflow.

## Troubleshooting

### Nothing happens after launching the EXE

- Extract the **entire** release instead of opening the EXE inside the ZIP.
- Confirm `wrapper.dll` and `Newtonsoft.Json.dll` are beside the EXE.
- Install the x64 Visual C++ Redistributable and .NET Framework 4.7.2+.
- Check `%LOCALAPPDATA%\SensitivityRandomizer\startup.log`.

### Raw Accel is not detected

Run the official Raw Accel installer and restart Windows. Installing only the
GUI files without the driver is insufficient.

### Verification reports acceleration

Open Raw Accel, set every relevant profile to **Off / noaccel**, apply it, then
run **Verify** again. Sensitivity Randomizer deliberately blocks speed-based
profiles.

### The multiplier is not `1.000`

Press `F9`, click **Stop**, or run `SensitivityResetGuard.exe`. Then use
**Verify**. If readback still differs, open Raw Accel, apply a constant-gain
profile at `1.000`, and investigate before playing.

More diagnostics are in [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

## Updating and uninstalling

To update, close the app, extract the new release to a clean folder and copy or
import your profile. Raw Accel normally does not need to be reinstalled unless
the release notes say otherwise.

To remove Sensitivity Randomizer, close it and delete its folder. It installs no
service and creates no autostart entry. To remove Raw Accel itself, run the
official `uninstaller.exe` and restart Windows.

## Privacy

The current release has no telemetry, account login, game-file access or network
requirement. Configurations and diagnostic/session logs stay on the local PC.

## Contributing and bug reports

Issues and pull requests are welcome. A useful bug report includes the app,
Windows and Raw Accel versions; exact reproduction steps; expected and actual
behavior; and the relevant error message or startup log.

Source builds require Visual Studio 2022 Build Tools with the .NET desktop build
tools workload. Run `build.ps1 -Configuration Release`; it executes the static
UI regression checks before compiling the x64 release.

## Credits

Created by [dinati](https://dinati.ru/).

Sensitivity Randomizer uses the separate open-source
[Raw Accel](https://github.com/RawAccelOfficial/rawaccel) driver and ships its
required third-party notices.

## License

Sensitivity Randomizer is released under the [MIT License](LICENSE.txt).
