<p align="center">
  <img src="assets/logo.png" width="128" alt="Sensitivity Randomizer logo">
</p>

<h1 align="center">Sensitivity Randomizer</h1>

<p align="center">
  A game-independent mouse sensitivity randomizer for Windows, built for aim training and sensitivity variability.
</p>

<p align="center">
  <img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-blue">
  <img alt="Platform" src="https://img.shields.io/badge/platform-x64-lightgrey">
  <img alt="Raw Accel" src="https://img.shields.io/badge/driver-Raw%20Accel-orange">
</p>

<p align="center">
  <a href="../../releases/latest"><strong>Download latest release</strong></a>
  ·
  <a href="#installation">Installation</a>
  ·
  <a href="#how-it-works">How it works</a>
  ·
  <a href="#configuration">Configuration</a>
</p>

---

## What is Sensitivity Randomizer?

Sensitivity Randomizer continuously varies your effective mouse sensitivity within a configurable range.

Unlike in-game sensitivity randomizers, it works at the raw mouse-input level through the **Raw Accel** driver. This makes it game-independent: the same randomization can be used in CS2, VALORANT, aim trainers, desktop applications, or practically any other program that uses mouse input.

The program is intended primarily as a **training tool** for players who want to practice mouse control across a range of sensitivities instead of adapting exclusively to one fixed value.

> Sensitivity Randomizer does **not** use speed-based mouse acceleration.  
> Your multiplier changes over time according to the randomization settings, not according to how quickly you move the mouse.

## Features

- Game-independent sensitivity randomization
- Configurable minimum and maximum multipliers
- Wide multiplier range for unusual or experimental setups
- Baseline multiplier of `1.0`
- Balanced randomization around the baseline
- Optional symmetry between values below and above `1.0`
- Configurable randomization interval
- Raw Accel driver integration
- Import/export of settings
- Multiple interface languages
- Dark, light and pastel themes
- Portable application: no separate application installer required
- Automatic restoration/handling of sensitivity when randomization is stopped

## Screenshot

<p align="center">
  <img src="assets/screenshot.png" width="850" alt="Sensitivity Randomizer interface">
</p>

## How it works

The program treats your normal sensitivity as the **baseline**:

```text
1.00x = your normal sensitivity
0.80x = 20% lower than normal
1.25x = 25% higher than normal
2.00x = twice your normal sensitivity
```

During randomization, Sensitivity Randomizer periodically selects a new multiplier from the configured range and sends the required sensitivity value to the Raw Accel driver.

The important distinction is that this is **not mouse acceleration**. A selected multiplier remains constant regardless of mouse speed until the program changes it again.

When balanced/symmetric randomization is enabled, the randomization logic is designed around the `1.0x` baseline so that training is not intentionally biased toward permanently higher or lower sensitivity.

### Example

With:

```text
Minimum: 0.50x
Maximum: 2.00x
```

your effective sensitivity may change like this:

```text
1.00x → 0.73x → 1.41x → 0.58x → 1.86x → ...
```

Your in-game sensitivity does not need to be changed.

## Requirements

- Windows 10 or Windows 11, 64-bit
- A mouse
- **Raw Accel driver**

Raw Accel is an open-source signed Windows mouse-input driver. Sensitivity Randomizer uses the installed driver to apply sensitivity multipliers.

Official Raw Accel repository:

**https://github.com/RawAccelOfficial/rawaccel**

> Only download Raw Accel from its official GitHub repository. The Raw Accel developers explicitly state that their official GitHub is the authoritative download location.

## Installation

### 1. Download and install Raw Accel

1. Open the official Raw Accel releases page:  
   **https://github.com/RawAccelOfficial/rawaccel/releases/latest**
2. Under **Assets**, download the current `RawAccel_...zip` release.
3. Extract the archive.
4. Run `installer.exe` as instructed by Raw Accel.
5. Restart Windows.

After the driver has been installed successfully and the PC has been restarted, the extracted Raw Accel download folder is **not required for normal Sensitivity Randomizer use**.

You may delete the downloaded ZIP and extracted Raw Accel files if you do not plan to use the Raw Accel GUI.

Keep in mind:

- `rawaccel.exe` is Raw Accel's own GUI.
- `uninstaller.exe` is used to uninstall the Raw Accel driver.
- If you delete the folder and later need the uninstaller, you can download the official Raw Accel release again.

Raw Accel's driver does not permanently store sensitivity settings across Windows restarts. Sensitivity Randomizer applies the values it needs while it is running.

### 2. Download Sensitivity Randomizer

1. Open this repository's **Releases** section.
2. Download the latest Sensitivity Randomizer release.
3. Extract it if the release is distributed as a ZIP.
4. Run `SensitivityRandomizer.exe`.

No separate installer is required unless stated otherwise in a specific release.

## First setup

For a normal setup:

1. Install the Raw Accel driver and restart Windows.
2. Launch Sensitivity Randomizer.
3. Leave your normal in-game sensitivity unchanged.
4. Set your desired randomization range.
5. Set the randomization interval.
6. Enable symmetry/balancing if desired.
7. Start randomization.

A good conservative starting range is:

```text
0.75x – 1.33x
```

A wider training range could be:

```text
0.50x – 2.00x
```

Very wide ranges are intentionally supported, but extreme values can make normal desktop use difficult. Increase the range gradually if you are not sure what is comfortable.

## Configuration

### Minimum / Maximum multiplier

Defines the range from which sensitivity multipliers can be selected.

`1.0x` always represents your normal baseline.

### Randomization interval

Controls how frequently a new multiplier is selected.

Short intervals create more frequent adaptation demands. Longer intervals allow more time to settle into each sensitivity.

There is no universally optimal interval. Choose it according to what you are training.

### Symmetry / balancing

When enabled, randomization is balanced around the baseline instead of simply treating a numerically asymmetric range as an ordinary uniform interval.

This is useful when you want exposure to both lower and higher sensitivities without unintentionally spending most of the session on only one side of your baseline.

### Base sensitivity / DPI fields

These fields are used for displaying or calculating your effective sensitivity.

The program does **not** physically change the hardware DPI stored in your mouse. The actual change is applied as a raw-input sensitivity multiplier through the Raw Accel driver.

## Using it with games

Sensitivity Randomizer is not tied to any particular game.

For most games:

1. Keep your usual in-game sensitivity.
2. Start Sensitivity Randomizer before training.
3. Use the multiplier range to control how far your effective sensitivity can move away from the baseline.
4. Stop randomization when you want to return to your normal sensitivity.

Because the change happens before the game's own sensitivity scaling, no game-specific sensitivity conversion is required.

## Anti-cheat note

Raw Accel is a signed driver designed with anti-cheat compatibility in mind. Its official documentation states that it has historically worked with anti-cheat systems including FACEIT and VALORANT.

However, Sensitivity Randomizer is a separate third-party project and is **not affiliated with Raw Accel, FACEIT, Valve, Riot Games, or any game developer**.

No third-party project can guarantee that every anti-cheat vendor will keep the same policy forever. If you are playing in a tournament or league with specific software restrictions, check the current rules.

## Windows SmartScreen

Unsigned or newly released Windows applications can trigger a Microsoft Defender SmartScreen warning because the executable has little or no reputation yet.

If this project is distributed without a commercial code-signing certificate, this warning can occur even when the file itself has not been detected as malware.

For safety, download Sensitivity Randomizer only from this repository's official **Releases** page.

## Troubleshooting

### The program says Raw Accel is not installed

Make sure you:

1. Ran the Raw Accel `installer.exe`.
2. Restarted Windows after installation.
3. Are using a supported 64-bit version of Windows.

### Sensitivity is not randomized after reboot

This is expected until a program sends settings to the Raw Accel driver again.

Launch Sensitivity Randomizer and start randomization.

### I deleted the Raw Accel download folder

That is fine if the driver was already installed successfully.

If you later need Raw Accel's GUI or uninstaller, download the current official release again.

### My sensitivity feels wrong after closing the program

Stop randomization from inside the application before closing it. If the application terminated unexpectedly, reopen it and restore the baseline value.

If a release has a reproducible restoration bug, please report it in **Issues** with the exact version and steps that caused it.

## Why randomized sensitivity?

The idea is simple: instead of practicing mouse control only under one fixed sensitivity, the player repeatedly adapts to different control scales.

This can be useful as a form of variable practice, especially in dedicated aim-training or mechanical practice sessions.

Sensitivity Randomizer should not be treated as a scientifically proven shortcut to better aim. It is a training tool. Whether it helps depends on the player, training design, range, interval, and total practice quality.

## Updating

New builds are published through GitHub **Releases**.

When updating:

1. Stop the current randomization session.
2. Close the application.
3. Download the new release.
4. Replace the old application files.
5. Start the new version.

Raw Accel normally does not need to be reinstalled for every Sensitivity Randomizer update unless the release notes explicitly say otherwise.

## Uninstalling

### Sensitivity Randomizer

The application is portable. Stop randomization, close it, and delete its files.

### Raw Accel

Use the official Raw Accel `uninstaller.exe`, then restart Windows.

If you previously deleted the Raw Accel package, download the official release again to obtain the uninstaller.

## Privacy

Sensitivity Randomizer does not need access to your game account or game files in order to randomize mouse sensitivity.

If telemetry, crash reporting, update checking, or any network functionality is added in a future version, it should be documented here explicitly.

## Contributing and bug reports

Bug reports and suggestions are welcome through GitHub **Issues**.

When reporting a bug, include:

- Sensitivity Randomizer version
- Windows version
- Raw Accel version
- Steps to reproduce the issue
- Expected behavior
- Actual behavior
- Screenshot or error message, if available

## Credits

Sensitivity Randomizer uses the **Raw Accel** driver for raw mouse-input sensitivity control.

Raw Accel is a separate open-source project and is not developed or maintained by Sensitivity Randomizer.

Created by **dinati**  
https://dinati.ru

## License

Choose a license before publishing the repository.

If you want people to be able to use, modify, and redistribute the source code with minimal restrictions, the **MIT License** is a common choice.

If you do not want to grant those permissions, do not add an open-source license until you have chosen terms that match what you want.
