using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SensitivityRandomizer;
using Newtonsoft.Json;

internal static class CoreTests
{
    private static int _passed;

    private static int Main()
    {
        try
        {
            Run("default settings validate", TestDefaultSettings);
            Run("legacy standard settings migrate to full range", TestLegacyStandardMigration);
            Run("legacy custom settings are preserved", TestLegacyCustomPreservation);
            Run("legacy asymmetric uniform migrates to balanced coverage", TestLegacyUniformMigration);
            Run("RC4 centered gaussian migrates to balanced coverage", TestRc4GaussianMigration);
            Run("RC5 reciprocal-link preference migrates without changing bounds", TestRc5ReciprocalMigration);
            Run("reciprocal bounds calculator and validation", TestReciprocalBounds);
            Run("expanded 0.10-10.00 range stays balanced and bounded", TestExpandedMultiplierRange);
            Run("seeded sequences are reproducible", TestDeterminism);
            Run("different seeds differ", TestDifferentSeeds);
            Run("balanced coverage visits every logarithmic zone", TestBalancedCoverage);
            Run("gaussian rejection stays in range", TestGaussian);
            Run("uniform stays in range and matches its midpoint", TestUniform);
            Run("asymmetric gaussian has mean one and equal sides", TestAsymmetricGaussianBalance);
            Run("random walk stays bounded and returns toward one", TestRandomWalk);
            Run("fixed and randomized intervals", TestIntervals);
            Run("technical interval is enforced", TestTechnicalIntervalValidation);
            Run("inactive mode parameters do not block validation", TestModeSpecificValidation);
            Run("portable config round-trips", TestPortableConfigurationRoundTrip);
            Run("legacy direct config imports", TestLegacyDirectConfigurationImport);
            Run("legacy base sensitivity property imports", TestLegacyBaseSensitivityProperty);
            Run("six interface languages are available", TestInterfaceLanguages);
            Run("theme preference round-trips", TestThemeRoundTrip);
            Run("randomizer has no mouse-speed input channel", TestNoMouseSpeedInputChannel);
            Run("effective sensitivity math", TestSensitivityMath);
            Run("session statistics", TestSessionStatistics);
            Run("engine stop always resets", () => TestEngineStopResetAsync().GetAwaiter().GetResult());
            Run("engine pause freezes changes", () => TestEnginePauseAsync().GetAwaiter().GetResult());
            Run("apply failure still uses safety reset", () => TestApplyFailureStillResetsAsync().GetAwaiter().GetResult());

            Console.WriteLine("PASS: " + _passed + " core tests");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void TestDefaultSettings()
    {
        var settings = AppSettings.CreateDefault();
        settings.Validate();
        Assert(settings.ProfileName == TrainingPreset.FullRangeName, "full-range profile is not the default");
        Assert(settings.Mode == RandomizerMode.BalancedCoverage, "full-range default is not Balanced Coverage");
        Assert(settings.UseReciprocalBounds, "full-range default does not link reciprocal bounds");
        Equal(0.50, settings.MinimumMultiplier, 0, "default minimum is wrong");
        Equal(2.00, settings.MaximumMultiplier, 0, "default maximum is wrong");
    }

    private static void TestLegacyStandardMigration()
    {
        var legacy = CreateLegacySettings("CS Standard");
        var loaded = LoadThroughJson(legacy);
        Assert(loaded.SettingsVersion == AppSettings.CurrentSettingsVersion, "settings version was not upgraded");
        Assert(loaded.ProfileName == TrainingPreset.FullRangeName, "legacy standard profile was not migrated");
        Assert(loaded.Mode == RandomizerMode.BalancedCoverage, "legacy standard mode was not migrated");
        Equal(0.50, loaded.MinimumMultiplier, 0, "migrated minimum is wrong");
        Equal(2.00, loaded.MaximumMultiplier, 0, "migrated maximum is wrong");
    }

    private static void TestLegacyCustomPreservation()
    {
        var legacy = CreateLegacySettings("Custom");
        legacy.MinimumMultiplier = 0.60;
        legacy.MaximumMultiplier = 1.80;
        legacy.MaximumStep = 0.12;
        var loaded = LoadThroughJson(legacy);
        Assert(loaded.ProfileName == "Custom", "custom profile name was overwritten");
        Equal(0.60, loaded.MinimumMultiplier, 0, "custom minimum was overwritten");
        Equal(1.80, loaded.MaximumMultiplier, 0, "custom maximum was overwritten");
        Equal(0.12, loaded.MaximumStep, 0, "custom step was overwritten");
    }

    private static void TestLegacyUniformMigration()
    {
        var legacy = AppSettings.CreateDefault();
        legacy.SettingsVersion = 3;
        legacy.ProfileName = "Custom";
        legacy.Mode = RandomizerMode.Uniform;
        legacy.MinimumMultiplier = 0.57;
        legacy.MaximumMultiplier = 1.75;
        legacy.GaussianMean = 1.20;
        var loaded = LoadThroughJson(legacy);
        Assert(loaded.Mode == RandomizerMode.BalancedCoverage, "legacy Uniform mode was not migrated");
        Equal(0.57, loaded.MinimumMultiplier, 0, "migration changed the custom minimum");
        Equal(1.75, loaded.MaximumMultiplier, 0, "migration changed the custom maximum");
    }

    private static void TestRc4GaussianMigration()
    {
        var rc4 = AppSettings.CreateDefault();
        rc4.SettingsVersion = 4;
        rc4.ProfileName = "Custom";
        rc4.Mode = RandomizerMode.Gaussian;
        rc4.MinimumMultiplier = 0.57;
        rc4.MaximumMultiplier = 1.75;
        rc4.GaussianMean = 1.0;
        var loaded = LoadThroughJson(rc4);
        Assert(loaded.Mode == RandomizerMode.BalancedCoverage, "RC4 Gaussian mode was not migrated");
        Equal(0.57, loaded.MinimumMultiplier, 0, "RC4 migration changed the custom minimum");
        Equal(1.75, loaded.MaximumMultiplier, 0, "RC4 migration changed the custom maximum");
    }

    private static void TestRc5ReciprocalMigration()
    {
        var exact = AppSettings.CreateDefault();
        exact.SettingsVersion = 5;
        exact.UseReciprocalBounds = false;
        var exactLoaded = LoadThroughJson(exact);
        Assert(exactLoaded.UseReciprocalBounds, "reciprocal RC5 bounds were not linked during migration");
        Equal(0.50, exactLoaded.MinimumMultiplier, 0, "RC5 migration changed the exact minimum");
        Equal(2.00, exactLoaded.MaximumMultiplier, 0, "RC5 migration changed the exact maximum");

        var custom = AppSettings.CreateDefault();
        custom.SettingsVersion = 5;
        custom.ProfileName = "Custom";
        custom.MinimumMultiplier = 0.57;
        custom.MaximumMultiplier = 1.75;
        custom.UseReciprocalBounds = false;
        var customLoaded = LoadThroughJson(custom);
        Assert(!customLoaded.UseReciprocalBounds, "asymmetric RC5 bounds were silently linked");
        Equal(0.57, customLoaded.MinimumMultiplier, 0, "RC5 migration changed the custom minimum");
        Equal(1.75, customLoaded.MaximumMultiplier, 0, "RC5 migration changed the custom maximum");
    }

    private static void TestReciprocalBounds()
    {
        Equal(1.75439, ReciprocalRange.Counterpart(0.57, 5), 0,
            "lower-to-upper reciprocal calculation is wrong");
        Equal(0.57143, ReciprocalRange.Counterpart(1.75, 5), 0,
            "upper-to-lower reciprocal calculation is wrong");
        Assert(ReciprocalRange.AreSymmetric(0.57, 1.75439),
            "rounded reciprocal pair was rejected");
        Assert(!ReciprocalRange.AreSymmetric(0.57, 1.75),
            "asymmetric pair was accepted as reciprocal");

        var settings = AppSettings.CreateDefault();
        settings.MinimumMultiplier = 0.57;
        settings.MaximumMultiplier = 1.75439;
        settings.UseReciprocalBounds = true;
        settings.Validate();

        settings.MaximumMultiplier = 1.75;
        var threw = false;
        try { settings.Validate(); }
        catch (SettingsValidationException) { threw = true; }
        Assert(threw, "linked asymmetric bounds passed validation");

        settings.UseReciprocalBounds = false;
        settings.Validate();
    }

    private static void TestExpandedMultiplierRange()
    {
        Equal(10.0, ReciprocalRange.Counterpart(0.10, 5), 0,
            "0.10 reciprocal did not reach the expanded maximum");
        Equal(0.10, ReciprocalRange.Counterpart(10.0, 5), 0,
            "10.00 reciprocal did not reach the expanded minimum");
        Assert(ReciprocalRange.AreSymmetric(0.10, 10.0),
            "expanded reciprocal endpoints were rejected");

        var settings = AppSettings.CreateDefault();
        settings.MinimumMultiplier = 0.10;
        settings.MaximumMultiplier = 10.0;
        settings.UseReciprocalBounds = true;
        settings.Validate();

        var generator = new RandomizerGenerator(settings, new Pcg32Random(100010), 1.0);
        var observedMinimum = double.PositiveInfinity;
        var observedMaximum = double.NegativeInfinity;
        for (var cycle = 0; cycle < 2000; cycle++)
        {
            var below = 0;
            var above = 0;
            var logSum = 0.0;
            for (var item = 0; item < RandomizerGenerator.CoverageCycleSize; item++)
            {
                var value = generator.NextMultiplier();
                Assert(value > 0.10 && value < 10.0, "expanded coverage escaped open bounds");
                if (value < 1.0) below++;
                if (value > 1.0) above++;
                logSum += Math.Log(value);
                observedMinimum = Math.Min(observedMinimum, value);
                observedMaximum = Math.Max(observedMaximum, value);
            }
            Assert(below == 4 && above == 4, "expanded coverage cycle lost its 4/4 split");
            Equal(0.0, logSum, 0.00000002, "expanded reciprocal cycle is not geometrically centered");
        }
        Assert(observedMinimum < 0.101, "expanded coverage did not reach the low extreme");
        Assert(observedMaximum > 9.90, "expanded coverage did not reach the high extreme");

        settings.UseReciprocalBounds = false;
        settings.MinimumMultiplier = 0.09999;
        var lowerRejected = false;
        try { settings.Validate(); }
        catch (SettingsValidationException) { lowerRejected = true; }
        Assert(lowerRejected, "value below 0.10 passed validation");

        settings.MinimumMultiplier = 0.10;
        settings.MaximumMultiplier = 10.00001;
        var upperRejected = false;
        try { settings.Validate(); }
        catch (SettingsValidationException) { upperRejected = true; }
        Assert(upperRejected, "value above 10.00 passed validation");
    }

    private static AppSettings CreateLegacySettings(string profileName)
    {
        var settings = AppSettings.CreateDefault();
        settings.SettingsVersion = 0;
        settings.ProfileName = profileName;
        settings.Mode = RandomizerMode.RandomWalk;
        settings.MinimumMultiplier = 0.80;
        settings.MaximumMultiplier = 1.20;
        settings.MaximumStep = 0.05;
        settings.ReturnStrength = 0.10;
        settings.GaussianMean = 1.00;
        settings.GaussianSigma = 0.08;
        settings.IntervalMode = IntervalMode.Random;
        settings.MinimumIntervalSeconds = 2.0;
        settings.MaximumIntervalSeconds = 5.0;
        return settings;
    }

    private static AppSettings LoadThroughJson(AppSettings settings)
    {
        var path = Path.Combine(Path.GetTempPath(), "sensitivity-randomizer-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(settings));
            string warning;
            var loaded = AppSettings.Load(path, out warning);
            Assert(warning == null, "settings load warning: " + warning);
            return loaded;
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void TestDeterminism()
    {
        var settings = AppSettings.CreateDefault();
        var left = new RandomizerGenerator(settings, new Pcg32Random(12345), 1.0);
        var right = new RandomizerGenerator(settings, new Pcg32Random(12345), 1.0);

        for (var i = 0; i < 1000; i++)
        {
            Equal(left.NextMultiplier(), right.NextMultiplier(), 0, "multiplier sequence differs at " + i);
            Equal(left.NextIntervalSeconds(), right.NextIntervalSeconds(), 0, "interval sequence differs at " + i);
        }
    }

    private static void TestDifferentSeeds()
    {
        var settings = AppSettings.CreateDefault();
        var left = new RandomizerGenerator(settings, new Pcg32Random(1), 1.0);
        var right = new RandomizerGenerator(settings, new Pcg32Random(2), 1.0);
        var equal = true;
        for (var i = 0; i < 20; i++)
            equal &= left.NextMultiplier() == right.NextMultiplier();
        Assert(!equal, "different seeds produced an identical sequence");
    }

    private static void TestBalancedCoverage()
    {
        var settings = AppSettings.CreateDefault();
        settings.Mode = RandomizerMode.BalancedCoverage;
        settings.MinimumMultiplier = 0.50;
        settings.MaximumMultiplier = 2.00;
        var generator = new RandomizerGenerator(settings, new Pcg32Random(20260903), 1.0);
        var zoneCounts = new int[RandomizerGenerator.CoverageCycleSize];
        var orderPatterns = new HashSet<string>();
        var sum = 0.0;
        var sumLogs = 0.0;
        var observedMinimum = double.PositiveInfinity;
        var observedMaximum = double.NegativeInfinity;
        const int cycles = 10000;

        for (var cycle = 0; cycle < cycles; cycle++)
        {
            var seenThisCycle = new bool[RandomizerGenerator.CoverageCycleSize];
            var cycleOrder = new int[RandomizerGenerator.CoverageCycleSize];
            var below = 0;
            var above = 0;
            var cycleLogSum = 0.0;

            for (var item = 0; item < RandomizerGenerator.CoverageCycleSize; item++)
            {
                var value = generator.NextMultiplier();
                Assert(value > 0.50 && value < 2.00, "coverage value escaped open hard bounds");
                var zone = CoverageZone(value, settings.MinimumMultiplier, settings.MaximumMultiplier);
                Assert(!seenThisCycle[zone], "coverage repeated a zone before completing its cycle");
                seenThisCycle[zone] = true;
                cycleOrder[item] = zone;
                zoneCounts[zone]++;
                if (value < 1.0) below++;
                else if (value > 1.0) above++;
                sum += value;
                sumLogs += Math.Log(value);
                cycleLogSum += Math.Log(value);
                observedMinimum = Math.Min(observedMinimum, value);
                observedMaximum = Math.Max(observedMaximum, value);
            }

            Assert(below == 4 && above == 4, "coverage cycle is not split 4/4 around one");
            Equal(0.0, cycleLogSum, 0.00000002, "reciprocal coverage cycle is not geometrically centered");
            if (cycle < 100) orderPatterns.Add(string.Join("", cycleOrder));
        }

        for (var zone = 0; zone < zoneCounts.Length; zone++)
            Assert(zoneCounts[zone] == cycles, "coverage zone frequency is not exact");
        Assert(orderPatterns.Count > 80, "coverage zone order is insufficiently shuffled");

        var count = cycles * RandomizerGenerator.CoverageCycleSize;
        var arithmeticMean = sum / count;
        var geometricMean = Math.Exp(sumLogs / count);
        var expectedArithmeticMean = 1.5 / Math.Log(4.0);
        Equal(expectedArithmeticMean, arithmeticMean, 0.002, "coverage arithmetic mean does not match log-uniform sampling");
        Equal(1.0, geometricMean, 0.00000001, "coverage geometric mean is not one");
        Assert(observedMinimum < 0.501, "coverage did not explore the lowest zone edge");
        Assert(observedMaximum > 1.996, "coverage did not explore the highest zone edge");
        Console.WriteLine(
            "  coverage stats: arithmetic={0:0.000000}, geometric={1:0.000000}, below=50.000%, above=50.000%, zones={2}",
            arithmeticMean,
            geometricMean,
            string.Join(",", zoneCounts));
    }

    private static int CoverageZone(double value, double minimum, double maximum)
    {
        double normalizedDistance;
        var sideOffset = 0;
        if (value < 1.0)
        {
            normalizedDistance = -Math.Log(value) / -Math.Log(minimum);
        }
        else
        {
            normalizedDistance = Math.Log(value) / Math.Log(maximum);
            sideOffset = RandomizerGenerator.CoverageZonesPerSide;
        }

        var band = Math.Min(
            RandomizerGenerator.CoverageZonesPerSide - 1,
            (int)(normalizedDistance * RandomizerGenerator.CoverageZonesPerSide));
        return sideOffset + band;
    }

    private static void TestUniform()
    {
        var settings = AppSettings.CreateDefault();
        settings.Mode = RandomizerMode.Uniform;
        settings.MinimumMultiplier = 0.8;
        settings.MaximumMultiplier = 1.2;
        var generator = new RandomizerGenerator(settings, new Pcg32Random(99), 1.0);
        var sum = 0.0;
        const int count = 100000;
        for (var i = 0; i < count; i++)
        {
            var value = generator.NextMultiplier();
            Assert(value >= 0.8 && value <= 1.2, "uniform escaped bounds");
            sum += value;
        }
        Equal(1.0, sum / count, 0.003, "uniform mean is biased");
    }

    private static void TestGaussian()
    {
        var settings = AppSettings.CreateDefault();
        settings.Mode = RandomizerMode.Gaussian;
        settings.MinimumMultiplier = 0.8;
        settings.MaximumMultiplier = 1.2;
        settings.GaussianMean = 1.0;
        settings.GaussianSigma = 0.08;
        var generator = new RandomizerGenerator(settings, new Pcg32Random(321), 1.0);
        var sum = 0.0;
        var exactBoundaryCount = 0;
        const int count = 100000;
        for (var i = 0; i < count; i++)
        {
            var value = generator.NextMultiplier();
            Assert(value >= 0.8 && value <= 1.2, "gaussian escaped hard bounds");
            if (value == 0.8 || value == 1.2) exactBoundaryCount++;
            sum += value;
        }
        Equal(1.0, sum / count, 0.004, "truncated gaussian mean is unexpectedly biased");
        Assert(exactBoundaryCount == 0, "gaussian appears clamped instead of rejection sampled");
    }

    private static void TestAsymmetricGaussianBalance()
    {
        var settings = AppSettings.CreateDefault();
        settings.Mode = RandomizerMode.Gaussian;
        settings.MinimumMultiplier = 0.50;
        settings.MaximumMultiplier = 2.00;
        settings.GaussianMean = 1.00;
        settings.GaussianSigma = 0.30;
        var generator = new RandomizerGenerator(settings, new Pcg32Random(20260903), 1.0);
        var sum = 0.0;
        var belowOne = 0;
        var aboveOne = 0;
        var observedMinimum = double.PositiveInfinity;
        var observedMaximum = double.NegativeInfinity;
        var nearCenter = 0;
        var farFromCenter = 0;
        const int count = 200000;

        for (var i = 0; i < count; i++)
        {
            var value = generator.NextMultiplier();
            Assert(value >= 0.50 && value <= 2.00, "balanced gaussian escaped bounds");
            sum += value;
            if (value < 1.0) belowOne++;
            if (value > 1.0) aboveOne++;
            if (Math.Abs(value - 1.0) < 0.10) nearCenter++;
            if (Math.Abs(value - 1.0) > 0.40) farFromCenter++;
            observedMinimum = Math.Min(observedMinimum, value);
            observedMaximum = Math.Max(observedMaximum, value);
        }

        var observedMean = sum / count;
        var observedLowerShare = belowOne / (double)(belowOne + aboveOne);
        Equal(1.0, observedMean, 0.002, "asymmetric balanced-gaussian mean is biased");
        Equal(0.5, observedLowerShare, 0.003,
            "asymmetric balanced-gaussian side frequency is biased");
        Assert(nearCenter > farFromCenter * 2,
            "balanced gaussian is not concentrated around its center");
        Assert(observedMinimum < 0.505, "balanced gaussian no longer reaches the lower edge");
        Assert(observedMaximum > 1.85, "balanced gaussian no longer explores the wider upper tail");
        Console.WriteLine(
            "  asymmetric stats: mean={0:0.000000}, below={1:0.000%}, above={2:0.000%}, min={3:0.000000}, max={4:0.000000}",
            observedMean,
            observedLowerShare,
            1.0 - observedLowerShare,
            observedMinimum,
            observedMaximum);
    }

    private static void TestRandomWalk()
    {
        var settings = AppSettings.CreateDefault();
        settings.Mode = RandomizerMode.RandomWalk;
        settings.MinimumMultiplier = 0.50;
        settings.MaximumMultiplier = 1.50;
        settings.MaximumStep = 0.05;
        settings.ReturnStrength = 0.10;
        var generator = new RandomizerGenerator(settings, new Pcg32Random(777), 1.0);
        var sum = 0.0;
        const int count = 100000;
        for (var i = 0; i < count; i++)
        {
            var value = generator.NextMultiplier();
            Assert(value >= settings.MinimumMultiplier && value <= settings.MaximumMultiplier, "walk escaped bounds");
            sum += value;
        }
        Equal(1.0, sum / count, 0.015, "mean-reverting walk did not remain centered near 1.0");
    }

    private static void TestIntervals()
    {
        var settings = AppSettings.CreateDefault();
        settings.IntervalMode = IntervalMode.Fixed;
        settings.MinimumIntervalSeconds = 3.25;
        settings.MaximumIntervalSeconds = 3.25;
        var fixedGenerator = new RandomizerGenerator(settings, new Pcg32Random(1), 1.0);
        for (var i = 0; i < 100; i++) Equal(3.25, fixedGenerator.NextIntervalSeconds(), 0, "fixed interval changed");

        settings.IntervalMode = IntervalMode.Random;
        settings.MinimumIntervalSeconds = 2;
        settings.MaximumIntervalSeconds = 5;
        var randomGenerator = new RandomizerGenerator(settings, new Pcg32Random(2), 1.0);
        var distinct = new HashSet<double>();
        for (var i = 0; i < 100; i++)
        {
            var value = randomGenerator.NextIntervalSeconds();
            Assert(value >= 2 && value <= 5, "random interval escaped bounds");
            distinct.Add(value);
        }
        Assert(distinct.Count > 90, "random interval has insufficient variation");
    }

    private static void TestTechnicalIntervalValidation()
    {
        var settings = AppSettings.CreateDefault();
        settings.MinimumIntervalSeconds = 1.24;
        var threw = false;
        try { settings.Validate(); }
        catch (SettingsValidationException) { threw = true; }
        Assert(threw, "interval below the Raw Accel technical limit was accepted");
    }

    private static void TestModeSpecificValidation()
    {
        var settings = AppSettings.CreateDefault();
        settings.Mode = RandomizerMode.Uniform;
        settings.MaximumStep = 99;
        settings.ReturnStrength = 99;
        settings.GaussianMean = 99;
        settings.GaussianSigma = 99;
        settings.IntervalMode = IntervalMode.Fixed;
        settings.MinimumIntervalSeconds = 2;
        settings.MaximumIntervalSeconds = 0;
        settings.Validate();

        settings.Mode = RandomizerMode.RandomWalk;
        var threw = false;
        try { settings.Validate(); }
        catch (SettingsValidationException) { threw = true; }
        Assert(threw, "invalid active Random Walk parameters were accepted");
    }

    private static void TestPortableConfigurationRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "sr-portable-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var expected = AppSettings.CreateDefault();
            expected.Language = AppLanguage.English;
            expected.ProfileName = "Custom";
            expected.Mode = RandomizerMode.Gaussian;
            expected.MinimumMultiplier = 0.55;
            expected.MaximumMultiplier = 1.90;
            expected.GaussianMean = 1.10;
            expected.GaussianSigma = 0.25;
            ConfigurationExchange.Export(expected, path);

            var actual = ConfigurationExchange.Import(path);
            Assert(actual.SettingsVersion == AppSettings.CurrentSettingsVersion, "portable settings version is wrong");
            Assert(actual.Language == AppLanguage.English, "portable language did not round-trip");
            Assert(actual.Mode == RandomizerMode.Gaussian, "portable mode did not round-trip");
            Assert(actual.UseReciprocalBounds == expected.UseReciprocalBounds,
                "reciprocal-link preference did not round-trip");
            Equal(expected.MinimumMultiplier, actual.MinimumMultiplier, 0, "portable minimum changed");
            Equal(expected.MaximumMultiplier, actual.MaximumMultiplier, 0, "portable maximum changed");
            Equal(expected.GaussianMean, actual.GaussianMean, 0, "portable mean changed");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void TestLegacyDirectConfigurationImport()
    {
        var path = Path.Combine(Path.GetTempPath(), "sr-direct-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var expected = AppSettings.CreateDefault();
            expected.ProfileName = "Custom";
            expected.MinimumMultiplier = 0.70;
            expected.UseReciprocalBounds = false;
            File.WriteAllText(path, JsonConvert.SerializeObject(expected));
            var actual = ConfigurationExchange.Import(path);
            Equal(0.70, actual.MinimumMultiplier, 0, "direct config import changed the minimum");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void TestLegacyBaseSensitivityProperty()
    {
        var settings = AppSettings.CreateDefault();
        settings.BaseSensitivity = 2.35;
        var json = JsonConvert.SerializeObject(settings).Replace("BaseSensitivity", "CsSensitivity");
        var loaded = AppSettings.FromJson(json);
        Equal(2.35, loaded.BaseSensitivity, 0, "legacy base sensitivity was not imported");
    }

    private static void TestInterfaceLanguages()
    {
        var languages = UiText.SupportedLanguages;
        Assert(languages.Length == 6, "expected six interface languages");
        foreach (var language in languages)
        {
            UiText.SetLanguage(language);
            Assert(!string.IsNullOrWhiteSpace(UiText.T("Импорт", "Import")), "language returned an empty label");
        }
        UiText.SetLanguage(AppLanguage.English);
    }

    private static void TestThemeRoundTrip()
    {
        var settings = AppSettings.CreateDefault();
        settings.Theme = AppTheme.Pastel;
        var loaded = AppSettings.FromJson(JsonConvert.SerializeObject(settings));
        Assert(loaded.Theme == AppTheme.Pastel, "theme preference did not round-trip");
    }

    private static void TestSensitivityMath()
    {
        Equal(1.28, SensitivityMath.EffectiveSensitivity(1.6, 0.8), 1e-12, "effective sensitivity is wrong");
        Equal(640, SensitivityMath.EffectiveEdpi(500, 1.6, 0.8), 1e-12, "effective eDPI is wrong");
    }

    private static void TestNoMouseSpeedInputChannel()
    {
        var nextMultiplier = typeof(RandomizerGenerator).GetMethod("NextMultiplier");
        Assert(nextMultiplier != null, "NextMultiplier is missing");
        Assert(nextMultiplier.GetParameters().Length == 0,
            "NextMultiplier unexpectedly accepts runtime input such as mouse speed");

        var start = typeof(RandomizerEngine).GetMethod("Start");
        Assert(start != null, "RandomizerEngine.Start is missing");
        var parameters = start.GetParameters();
        Assert(parameters.Length == 1 && parameters[0].ParameterType == typeof(AppSettings),
            "engine Start exposes an unexpected live-input channel");
    }

    private static void TestSessionStatistics()
    {
        var stats = new SessionAccumulator();
        stats.Add(0.5, 0.8, 400, TimeSpan.Zero);
        stats.Add(2.0, 3.2, 1600, TimeSpan.FromSeconds(4));
        var summary = stats.BuildSummary(
            DateTime.UtcNow,
            DateTime.UtcNow,
            "test",
            1,
            TimeSpan.FromSeconds(10),
            TimeSpan.Zero,
            null,
            null);
        Equal(1.25, summary.MeanMultiplier, 1e-12, "arithmetic mean statistic is wrong");
        Equal(1.0, summary.GeometricMeanMultiplier, 1e-12, "geometric mean statistic is wrong");
        Equal(Math.Pow(2.0, 0.2), summary.TimeWeightedGeometricMeanMultiplier, 1e-12,
            "time-weighted geometric mean is wrong");
        Equal(0.75, summary.StandardDeviation, 1e-12, "population SD is wrong");
        Equal(0.5, summary.MinimumMultiplier, 0, "minimum statistic is wrong");
        Equal(2.0, summary.MaximumMultiplier, 0, "maximum statistic is wrong");
        Equal(1000, summary.MeanEffectiveEdpi, 1e-12, "mean eDPI is wrong");
        Equal(50.0, summary.BelowBaseChangePercent, 1e-12, "below-one change share is wrong");
        Equal(50.0, summary.AboveBaseChangePercent, 1e-12, "above-one change share is wrong");
        Equal(40.0, summary.BelowBaseTimePercent, 1e-12, "below-one time share is wrong");
        Equal(60.0, summary.AboveBaseTimePercent, 1e-12, "above-one time share is wrong");
    }

    private static async Task TestEngineStopResetAsync()
    {
        var controller = new FakeController(TimeSpan.FromSeconds(1));
        var engine = new RandomizerEngine(controller);
        var firstSample = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SampleApplied += (sender, args) => firstSample.TrySetResult(true);
        var settings = FastEngineSettings();

        engine.Start(settings);
        await WithTimeout(firstSample.Task, 2000);
        await engine.StopAsync();

        Assert(controller.Applied.Count >= 2, "engine did not apply a random value and a reset");
        Equal(1.0, controller.Applied[controller.Applied.Count - 1], 0, "Stop did not finish on multiplier 1.0");
        Assert(engine.State == EngineState.Off, "engine did not return to Off after Stop");
    }

    private static async Task TestEnginePauseAsync()
    {
        var controller = new FakeController(TimeSpan.FromSeconds(1));
        var engine = new RandomizerEngine(controller);
        var firstSample = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SampleApplied += (sender, args) => firstSample.TrySetResult(true);
        var settings = FastEngineSettings();

        engine.Start(settings);
        await WithTimeout(firstSample.Task, 2000);
        engine.TogglePause();
        var countAtPause = controller.Applied.Count;
        await Task.Delay(500);
        Assert(controller.Applied.Count == countAtPause, "a new value was applied while paused");

        engine.TogglePause();
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (controller.Applied.Count == countAtPause && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert(controller.Applied.Count > countAtPause, "randomization did not resume");
        await engine.StopAsync();
        Equal(1.0, controller.Applied[controller.Applied.Count - 1], 0, "pause test did not finish reset");
    }

    private static async Task TestApplyFailureStillResetsAsync()
    {
        var controller = new FailingApplyController();
        var engine = new RandomizerEngine(controller);
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SessionCompleted += (sender, args) => completed.TrySetResult(true);

        engine.Start(FastEngineSettings());
        await WithTimeout(completed.Task, 2000);
        await Task.Delay(20);

        Assert(controller.ApplyCount == 1, "failing apply was not attempted exactly once");
        Assert(controller.ResetCount >= 1, "safety reset was not attempted after apply failure");
        Assert(engine.State == EngineState.Error, "engine did not expose the apply failure");
    }

    private static AppSettings FastEngineSettings()
    {
        var settings = AppSettings.CreateDefault();
        settings.LoggingEnabled = false;
        settings.SessionDurationSeconds = 0;
        settings.RecalibrationDurationSeconds = 0;
        settings.IntervalMode = IntervalMode.Fixed;
        settings.MinimumIntervalSeconds = 1.25;
        settings.MaximumIntervalSeconds = 1.25;
        settings.UseSeed = true;
        settings.Seed = 42;
        return settings;
    }

    private static async Task WithTimeout(Task task, int timeoutMilliseconds)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeoutMilliseconds));
        if (completed != task) throw new TimeoutException("engine test timed out");
        await task;
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine("ok - " + name);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(message + "; expected=" + expected + ", actual=" + actual);
    }

    private sealed class FakeController : IMultiplierController
    {
        private readonly object _sync = new object();
        public List<double> Applied { get; } = new List<double>();
        public TimeSpan WriteDelay { get; private set; }

        public FakeController(TimeSpan writeDelay)
        {
            WriteDelay = writeDelay;
        }

        public Task<MultiplierReadback> InspectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Readback(1.0));
        }

        public Task<MultiplierReadback> ApplyAsync(double multiplier, CancellationToken cancellationToken)
        {
            lock (_sync) Applied.Add(multiplier);
            return Task.FromResult(Readback(multiplier));
        }

        public Task<MultiplierReadback> ResetAsync(CancellationToken cancellationToken)
        {
            lock (_sync) Applied.Add(1.0);
            return Task.FromResult(Readback(1.0));
        }

        private static MultiplierReadback Readback(double multiplier)
        {
            return new MultiplierReadback
            {
                DriverVersion = "test",
                RequestedMultiplier = multiplier,
                RepresentativeMultiplier = multiplier,
                ProfileNames = new[] { "test" },
                ProfileMultipliers = new[] { multiplier },
                AccelerationModes = new string[0]
            };
        }
    }

    private sealed class FailingApplyController : IMultiplierController
    {
        public int ApplyCount { get; private set; }
        public int ResetCount { get; private set; }
        public TimeSpan WriteDelay { get { return TimeSpan.FromSeconds(1); } }

        public Task<MultiplierReadback> InspectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Readback(1.0));
        }

        public Task<MultiplierReadback> ApplyAsync(double multiplier, CancellationToken cancellationToken)
        {
            ApplyCount++;
            return Task.FromException<MultiplierReadback>(
                new InvalidOperationException("speed-based acceleration detected"));
        }

        public Task<MultiplierReadback> ResetAsync(CancellationToken cancellationToken)
        {
            ResetCount++;
            return Task.FromResult(Readback(1.0));
        }

        private static MultiplierReadback Readback(double multiplier)
        {
            return new MultiplierReadback
            {
                DriverVersion = "test",
                RequestedMultiplier = multiplier,
                RepresentativeMultiplier = multiplier,
                ProfileNames = new[] { "test" },
                ProfileMultipliers = new[] { multiplier },
                AccelerationModes = new string[0]
            };
        }
    }
}
