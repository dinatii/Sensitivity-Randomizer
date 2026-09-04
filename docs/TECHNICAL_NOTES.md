# Technical notes

## Raw Accel integration

Target: official Raw Accel v1.7.1 distribution, whose driver/wrapper ABI reports
version 1.7.0.

The official C++/CLI `wrapper.dll` exposes:

- `VersionHelper.ValidOrThrow()` for ABI/version validation;
- `DriverConfig.GetActive()` for actual driver readback;
- `DriverConfig.SetProfileAt()` to keep its managed/native profile copies in sync;
- `DriverConfig.Activate()` for the official `DeviceIoControl` write path.

Raw Accel internally stores the GUI Sens Multiplier as `profile.output_dpi / 1000`.
The app therefore changes only:

```text
profile.outputDPI = requestedMultiplier × 1000
```

It performs this for every driver profile so the active mouse-specific profile
cannot retain a stale value. All other data is freshly read before each write
and carried through unchanged.

Before and after every randomized write, all active profiles must report
`argsX.mode = noaccel` and, when a separate Y curve is used,
`argsY.mode = noaccel`. Detection of any speed-based curve aborts the write and
stops the session. The safety-reset path is separate: it may restore
`outputDPI = 1000` even when acceleration was enabled externally, but it never
edits or conceals that curve.

The project does not use `writer.exe`, because that CLI only accepts a complete
JSON file and does not itself expose readback. Direct use of the official
wrapper avoids overwriting settings from a stale file.

## Rate limiting and scheduling

The Raw Accel driver source defines `WRITE_DELAY = 1000` ms and delays each
WRITE before applying it. The engine serializes every access with one
`SemaphoreSlim`; writes are never launched concurrently.

For requested hold interval `T`, the next blocking write starts after roughly:

```text
max(0, T - WRITE_DELAY)
```

The driver then applies the new value near the requested `T` boundary. Windows
scheduling and driver load can introduce jitter, so logs record application
completion rather than pretending exact real-time timing.

## Randomization

### Balanced Coverage

This is the default mode for Full Range. It treats sensitivity as a
multiplicative quantity and divides the logarithmic distance on each side of
`1.000` into four equal bands:

```text
d = (band + U(0,1)) / 4
lower = exp(log(min) * d)
upper = exp(log(max) * d)
```

One lower and one upper sample are generated for each band, producing eight
samples. The completed bag is Fisher-Yates shuffled before it is returned. Each
full cycle therefore contains exactly one sample from every zone and exactly
four samples on either side of one. There is no sequence rule beyond the
shuffled coverage guarantee.

When the bounds are reciprocal, as with `0.50-2.00`, paired samples share the
same `d` and are exact reciprocals before stable rounding. Thus every complete
cycle has geometric mean `1.000`. The corresponding arithmetic mean is about
`1.082`, as expected for log-uniform exposure; it is not evidence of more
upper-side samples.

Random hold intervals are drawn independently. Consequently exact 50/50 change
counts need not produce exact 50/50 elapsed time in a short session. Session
summaries expose both count shares and time shares, plus geometric means by
changes and by elapsed time.

The optional `UseReciprocalBounds` setting maintains `min × max = 1` in the
GUI. Either editor can be the source; its counterpart is calculated as
`round(1 / value, 5)`. Linked controls remain on opposite sides of one and are
validated to a product tolerance of `0.00002`. With the hard maximum of
`10.000`, linking constrains the lower editor to `0.10000-0.99999`. Disabling
the link restores the full independent editor limits.

### Linear Uniform

```text
x = min + (max - min) × U[0,1)
```

Its expected value is the interval midpoint. It is retained as an explicitly
labelled baseline, not used by the default Full Range profile.

### Balanced Gaussian

```text
side = lower or upper with probability 0.5 each
distance = truncated_half_normal(scale_for_side, span_for_side)
x = mean ± distance
E[lower_distance] = E[upper_distance]
```

The shorter side uses the configured sigma. A binary search calibrates the
half-normal scale on the wider side until both conditional mean distances are
equal. Consequently `P(x < mean) = P(x > mean) = 0.5` and `E[x] = mean`, even
when the hard bounds are asymmetric. Box-Muller and rejection sampling avoid
artificial probability piles at either boundary. The distribution remains
bell-shaped around the configured mean; the more distant edge is possible but
appropriately rarer.

### Smooth random walk

```text
random_delta = U[-max_step, +max_step]
drift = (1 - current) × return_strength
next = clamp(current + random_delta + drift, min, max)
```

`max_step` describes the random component. The total change can be slightly
larger because mean-reversion drift is added separately, exactly as specified.

### Reproducibility

PCG32 supplies all multiplier and interval randomness. A seed therefore remains
stable across .NET Framework updates. Values are rounded to 9 decimal places
before becoming generator state.

## Configuration and localization

The persisted settings schema is version 7. `Language` and `Theme` are stored as
named enums. Existing v3 Linear Uniform settings whose range contains `1.000`, and RC4
centered-Gaussian settings with mean `1.000`, migrate to Balanced Coverage;
custom bounds and timing remain intact. RC5 configurations enable reciprocal
linking only when their stored bounds already satisfy the relation, so migration
never rewrites an asymmetric custom range. Gaussian settings with a deliberately
custom mean are preserved.
UI text is selected at runtime. Russian and English remain the canonical source
strings; German, Spanish, Polish and Ukrainian use a keyed translation layer
with English fallback for low-level diagnostics. Theme selection swaps a frozen
palette before rebuilding inactive UI controls.

Portable exports use an explicit envelope:

```text
Format = SensitivityRandomizerConfig
FormatVersion = 1
ExportedUtc
Settings
```

Imports are limited to 1 MiB, parsed as JSON, checked for a supported envelope
version and passed through normal settings migration and validation. Direct
settings JSON from earlier releases remains accepted. Import only changes local
controls and never writes Raw Accel until Start is explicitly pressed.

The program can migrate the previous application-data config on first launch.
New settings, logs, portable configs, assembly metadata and process names use
the game-neutral product identity.

## UI rendering and animation

The active interface is a code-native WPF visual tree. Grid and ScrollViewer
perform normal retained-mode measurement; there is no window resize handler,
manual card sizing or native rounded Region allocation. The live chart is not
invalidated while the engine is idle.

Entrance transitions use short opacity and `TranslateTransform` animations.
Button hover uses `ScaleTransform`. These are composition properties, do not
change desired size and are never started by resize events.

## Safety and races

- Stop/Panic cancels future work but never aborts an in-flight Raw Accel write.
- The in-flight write completes, then reset is queued through the same gate.
- Reset is attempted twice in-process.
- ResetGuard watches the main process and tries three more times after an
  abnormal exit, but only while an authorization marker is armed.
- Every successful write, including reset, is read back from the driver.
- Speed-based acceleration is checked before and after every randomized write;
  Start fails closed if any active profile is not `Off/noaccel`.
- Concurrent Apply from the Raw Accel GUI can still race. Readback detects many
  such races, but the supported operating rule is one controller at a time.

## Known deliberate limits

- Raw Accel 1.7.x only. The official wrapper rejects incompatible older/newer ABI.
- Windows 10/11 x64 only.
- Instant transitions only. Interpolation would exceed the driver's intentional
  write-rate boundary and is not implemented.
- Custom multiplier range is hard-limited to 0.10-10.00. The wider limit is for
  explicit user experiments; presets intentionally remain much narrower.
- No hard-kill/power-loss guarantee is possible if both user-mode processes and
  the OS stop before a reset write reaches the driver.
