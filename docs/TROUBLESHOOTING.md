# Startup troubleshooting

## Correct launch

1. Install official Raw Accel v1.7.1 and reboot Windows.
2. Install Microsoft Visual C++ Redistributable 2015-2022 x64:
   https://aka.ms/vc14/vc_redist.x64.exe
3. Right-click the downloaded ZIP, open Properties and select Unblock if Windows
   shows that option.
4. Extract the complete archive. Do not start an EXE from the compressed-folder
   view in Explorer.
5. Start `release\win-x64\SensitivityRandomizer.exe` with `wrapper.dll`,
   `Newtonsoft.Json.dll` and `SensitivityResetGuard.exe` beside it.

Administrator rights are normally unnecessary after the Raw Accel driver has
already been installed.

## Diagnostic behavior in v1.2.0

Before any Raw Accel type is loaded, the application checks:

- Windows and process architecture are x64;
- all required neighboring files exist;
- `wrapper.dll` and `Newtonsoft.Json.dll` match the official Raw Accel v1.7.1
  release hashes;
- `VCRUNTIME140.dll` and `MSVCP140.dll` can be loaded;
- the main window is visible before the driver inspection starts.

Any failure produces an error dialog. The full stage-by-stage log is written to:

```text
%LocalAppData%\SensitivityRandomizer\startup.log
```

Crash and reset failures use:

```text
%LocalAppData%\SensitivityRandomizer\crash.log
%LocalAppData%\SensitivityRandomizer\reset-guard.log
```

## If the process exists but no window is visible

1. End `SensitivityRandomizer.exe` and `SensitivityResetGuard.exe` in Task
   Manager.
2. Start the main EXE once more.
3. Open `startup.log` and check its final line.
4. If it says `Main window shown`, use Alt+Tab or Win+Shift+Left/Right to recover
   a window placed on a disconnected monitor.

Do not test fixes inside a protected FACEIT match. This custom executable has
not been approved by FACEIT.
