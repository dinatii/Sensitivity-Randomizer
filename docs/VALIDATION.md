# Validation record

## Automated core tests

The following twenty-nine tests pass:

1. default full-range settings validation;
2. migration of untouched legacy Standard settings to Full Range;
3. preservation of legacy custom settings during migration;
4. migration of v3 custom Uniform settings to Balanced Coverage without changing bounds;
5. migration of RC4 centered-Gaussian settings to Balanced Coverage without changing bounds;
6. RC5 reciprocal-link migration without changing either stored bound;
7. five-decimal reciprocal calculation and rejection of falsely linked asymmetric bounds;
8. expanded `0.10-10.00` endpoints, open bounds, exact 4/4 balance and geometric centering;
9. seeded multiplier and interval reproducibility;
10. different-seed divergence;
11. exact Balanced Coverage visitation of all eight logarithmic zones across 10,000 complete cycles;
12. symmetric Gaussian bounds, mean sanity and absence of clamped boundary piles;
13. linear Uniform bounds and midpoint-mean sanity over 100,000 samples;
14. asymmetric `0.50-2.00` Gaussian mean, 50/50 side frequency and tail reachability over 200,000 samples;
15. random-walk bounds and long-run centering;
16. fixed/random interval behavior;
17. enforcement of the 1.25-second technical minimum;
18. inactive mode parameters do not block validation;
19. portable JSON configuration export/import round-trip;
20. direct legacy `config.json` import compatibility;
21. generator and engine APIs expose no mouse-speed input channel;
22. base sensitivity/eDPI formulas;
23. arithmetic/geometric means, below/above shares and time-weighted session statistics;
24. engine Stop always finishes with multiplier `1.000`;
25. Pause prevents further applies until Resume;
26. an apply failure still takes the dedicated safety-reset path.
27. the retired base-sensitivity JSON property remains importable;
28. all six interface languages are available and produce non-empty labels;
29. theme preference survives a settings round-trip.

## Build validation

- Main application and ResetGuard compile with zero compiler warnings as x64
  .NET Framework 4.7.2 PE executables.
- Version 1.2.0 places the dependency preflight in a non-inlined bootstrap before
  any Raw Accel type is used, and logs each stage through `Main window shown`.
- RC8 replaces the active WinForms interface with a WPF visual tree. The window
  uses native Grid/ScrollViewer measurement and has no window resize callback,
  splitter, per-pixel card sizing, recursive `PerformLayout`, or rounded native
  Region allocation.
- The live graph is no longer invalidated while the engine is idle. The UI timer
  runs twice per second, and the inactive path no longer probes the ResetGuard
  marker file on every tick.
- Version 1.2 RC2 retains the deferred final WPF `Close()` on the next Dispatcher turn. This prevents
  the synchronous inactive-engine path from re-entering `Close()` inside the
  original `Closing` event. A static release check rejects that regression.
- RC5 replaces the bell-shaped Full Range mode with Balanced Coverage. Across
  80,000 deterministic samples, all eight zones receive exactly 10,000 values,
  every cycle is split 4/4 around one, and geometric mean is `1.000000`.
- The same regression confirms exploration to within `0.001` of the lower edge
  and within `0.004` of the upper edge. Zone order is shuffled rather than
  repeated as a fixed pattern.
- RC6 verifies reciprocal calculations in both directions, tolerance-aware
  validation and non-destructive migration of asymmetric RC5 configurations.
- RC7 additionally validates the reciprocal `0.10-10.00` endpoints over 2,000
  complete cycles, including open hard bounds and exact geometric centering.
- Setting rows reserve a stable 186-DIP editor column at the minimum supported
  window width; descriptions use a wrapping copy area rather than a single
  aggressively truncated line.
- Essential, Optional and Guide pages are independently scrollable. Mode,
  interval, seed and recalibration rows collapse when they are not relevant.
- Russian, English, German, Spanish, Polish and Ukrainian UI paths compile from
  the same deterministic executable;
  language changes rebuild only inactive UI controls and never the engine.
- Dark, Light and Pastel palettes are selected before rebuilding controls and
  persist through the version-7 settings schema.
- Page and hover transitions animate only opacity and render transforms. No
  animation, callback or polling work is attached to the window resize path.
- Both PE executables include the same multi-resolution application icon.
- Compilation references the `wrapper.dll` and `Newtonsoft.Json.dll` bytes from
  the official Raw Accel v1.7.1 ZIP.
- The downloaded Raw Accel ZIP SHA-256 was checked against the digest published
  by the official GitHub release API before extracting dependencies.

## Runtime boundary

The build environment is not Windows and cannot load the Raw Accel kernel
driver. For that reason no fabricated driver integration test is claimed.
Instead the production application performs a real startup check and a real
`GetActive -> Activate -> GetActive` verification on the target Windows PC.

The same boundary applies to the WPF startup path and native VC++ runtime:
the code and PE outputs are validated here, but the final startup smoke test must
run on Windows. If startup fails, attach `%LocalAppData%\SensitivityRandomizer\startup.log`.

Before relying on it for a long session, run this hardware smoke test:

1. Confirm Raw Accel GUI shows Sens Multiplier `1.000`.
2. Start Full Range and wait for two changes.
3. Confirm the app shows readback-verified values and Raw Accel reflects the same multiplier.
4. Press F10 and confirm the value remains fixed for at least 10 seconds.
5. Press F9, wait up to 2 seconds, and confirm both programs show `1.000`.
6. Start again, end the main EXE from Task Manager without ending ResetGuard,
   wait up to 5 seconds, and confirm Raw Accel returns to `1.000`.

That final smoke test validates the actual local driver, security software,
hotkey registration, and guard behavior that cannot be reproduced elsewhere.
