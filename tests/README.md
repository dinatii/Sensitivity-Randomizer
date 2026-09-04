# Core tests

`CoreTests.cs` checks deterministic randomization, statistical sanity, bounds,
interval validation, mode-specific validation, portable config import/export,
sensitivity math, exact eight-zone Balanced Coverage cycles, 50/50 side
balance, geometric centering, edge reachability, RC3/RC4 settings migration,
reciprocal-bound calculation and RC5 migration, expanded `0.10-10.00` endpoint
coverage, time-weighted session statistics, the absence of any mouse-speed
input channel in the generator/engine API, six-language availability, theme
persistence, and the dedicated reset path after a rejected apply. It deliberately does not
fake the Raw Accel driver. Driver integration and the no-acceleration preflight
are verified by the app's real `GetActive -> Activate -> GetActive` sequence on
Windows.

The release build was compiled against .NET Framework 4.7.2 and the official
Raw Accel v1.7.1 `wrapper.dll`. The core tests can also be compiled against a
modern .NET runtime because they do not access Windows or the driver.

`UiArchitectureTests.ps1` checks that the active project compiles only the WPF
window and controls, rejects legacy WinForms resize primitives, and verifies
the idle-work guards used to keep window resizing responsive. It also verifies
the application icon, three-theme palette, header logo and animation ban on
layout dimensions.
