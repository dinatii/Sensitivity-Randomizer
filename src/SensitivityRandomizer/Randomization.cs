using System;

namespace SensitivityRandomizer
{
    public interface IRandomSource
    {
        uint NextUInt32();
        double NextDouble();
    }

    /// <summary>
    /// Small deterministic PCG32 implementation. A seeded session therefore keeps the
    /// same sequence even if System.Random changes in a later .NET release.
    /// </summary>
    public sealed class Pcg32Random : IRandomSource
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private ulong _state;
        private readonly ulong _increment;

        public Pcg32Random(ulong seed)
        {
            _state = 0;
            _increment = 1442695040888963407UL | 1UL;
            NextUInt32();
            unchecked { _state += seed; }
            NextUInt32();
        }

        public uint NextUInt32()
        {
            unchecked
            {
                var oldState = _state;
                _state = oldState * Multiplier + _increment;
                var xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
                var rotation = (int)(oldState >> 59);
                return (xorshifted >> rotation) | (xorshifted << ((-rotation) & 31));
            }
        }

        public double NextDouble()
        {
            return NextUInt32() / 4294967296.0;
        }

        public static int CreateUnseededSessionSeed()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            var entropy = BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, 8);
            return entropy ^ Environment.TickCount ^ (int)DateTime.UtcNow.Ticks;
        }
    }

    public sealed class RandomizerGenerator
    {
        public const int CoverageZonesPerSide = 4;
        public const int CoverageCycleSize = CoverageZonesPerSide * 2;

        private readonly AppSettings _settings;
        private readonly IRandomSource _random;
        private readonly double[] _coverageBag = new double[CoverageCycleSize];
        private int _coveragePosition = CoverageCycleSize;
        private double _current;

        public RandomizerGenerator(AppSettings settings, IRandomSource random, double startingMultiplier)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (random == null) throw new ArgumentNullException(nameof(random));
            settings.Validate();

            _settings = settings.Clone();
            _random = random;
            _current = Clamp(startingMultiplier, settings.MinimumMultiplier, settings.MaximumMultiplier);
        }

        public double CurrentMultiplier { get { return _current; } }

        public double NextMultiplier()
        {
            double next;
            switch (_settings.Mode)
            {
                case RandomizerMode.BalancedCoverage:
                    next = NextBalancedCoverage();
                    break;

                case RandomizerMode.Uniform:
                    next = Uniform(_settings.MinimumMultiplier, _settings.MaximumMultiplier);
                    break;

                case RandomizerMode.Gaussian:
                    next = NextBalancedGaussian();
                    break;

                case RandomizerMode.RandomWalk:
                    var randomDelta = Uniform(-_settings.MaximumStep, _settings.MaximumStep);
                    var drift = (1.0 - _current) * _settings.ReturnStrength;
                    next = Clamp(_current + randomDelta + drift, _settings.MinimumMultiplier, _settings.MaximumMultiplier);
                    break;

                default:
                    throw new InvalidOperationException(UiText.T("Неизвестный режим randomizer.", "Unknown randomizer mode."));
            }

            _current = RoundStable(next);
            return _current;
        }

        public double NextIntervalSeconds()
        {
            if (_settings.IntervalMode == IntervalMode.Fixed)
                return _settings.MinimumIntervalSeconds;

            return RoundStable(Uniform(_settings.MinimumIntervalSeconds, _settings.MaximumIntervalSeconds));
        }

        private double NextBalancedCoverage()
        {
            if (_coveragePosition >= _coverageBag.Length)
                RefillCoverageBag();

            return _coverageBag[_coveragePosition++];
        }

        private void RefillCoverageBag()
        {
            var lowerLogSpan = -Math.Log(_settings.MinimumMultiplier);
            var upperLogSpan = Math.Log(_settings.MaximumMultiplier);

            // One cycle contains one low and one high value in each of four
            // logarithmic distance bands. For 0.50-2.00 this means all eight
            // zones are visited exactly once, while lower/upper counts are 4/4.
            // A shared position inside paired bands also makes their values exact
            // reciprocals when the configured bounds themselves are reciprocal.
            for (var band = 0; band < CoverageZonesPerSide; band++)
            {
                var positionInBand = OpenUnit();
                var normalizedDistance = (band + positionInBand) / CoverageZonesPerSide;
                _coverageBag[band * 2] = Math.Exp(-lowerLogSpan * normalizedDistance);
                _coverageBag[band * 2 + 1] = Math.Exp(upperLogSpan * normalizedDistance);
            }

            // Shuffle the completed coverage cycle so the zones are guaranteed but
            // their order remains unpredictable and reproducible with a fixed seed.
            for (var i = _coverageBag.Length - 1; i > 0; i--)
            {
                var swapIndex = (int)(_random.NextDouble() * (i + 1));
                if (swapIndex > i) swapIndex = i;
                var temporary = _coverageBag[i];
                _coverageBag[i] = _coverageBag[swapIndex];
                _coverageBag[swapIndex] = temporary;
            }

            _coveragePosition = 0;
        }

        private double OpenUnit()
        {
            const double halfStep = 1.0 / 8589934592.0;
            return Math.Max(halfStep, Math.Min(1.0 - halfStep, _random.NextDouble()));
        }

        private double NextBalancedGaussian()
        {
            var center = _settings.GaussianMean;
            var lowerSpan = center - _settings.MinimumMultiplier;
            var upperSpan = _settings.MaximumMultiplier - center;
            var lowerScale = _settings.GaussianSigma;
            var upperScale = _settings.GaussianSigma;

            // A normally distributed value followed by asymmetric truncation is not
            // centered: for 0.50-2.00, the shorter lower tail is rejected more often.
            // Select the side first (50/50), then use a truncated half-normal on that
            // side. The wider side's scale is calibrated so both sides have the same
            // expected absolute deviation. Therefore P(x < center) = P(x > center)
            // and E[x] = center without clamping or boundary probability piles.
            if (lowerSpan < upperSpan)
            {
                var targetDeviation = TruncatedHalfNormalMean(lowerSpan, lowerScale);
                upperScale = CalibrateHalfNormalScale(upperSpan, targetDeviation);
            }
            else if (upperSpan < lowerSpan)
            {
                var targetDeviation = TruncatedHalfNormalMean(upperSpan, upperScale);
                lowerScale = CalibrateHalfNormalScale(lowerSpan, targetDeviation);
            }

            var useUpperSide = _random.NextDouble() >= 0.5;
            var span = useUpperSide ? upperSpan : lowerSpan;
            var scale = useUpperSide ? upperScale : lowerScale;
            var deviation = NextTruncatedHalfNormal(span, scale);
            return useUpperSide ? center + deviation : center - deviation;
        }

        private double NextTruncatedHalfNormal(double span, double scale)
        {
            // Choose the rejection proposal according to the shape. This keeps the
            // acceptance rate healthy for both a narrow and a broad bell curve.
            if (scale <= span)
            {
                for (var attempt = 0; attempt < 256; attempt++)
                {
                    var u1 = Math.Max(_random.NextDouble(), 1.0 / 4294967296.0);
                    var u2 = _random.NextDouble();
                    var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                    var deviation = scale * Math.Abs(z);
                    if (deviation <= span)
                        return deviation;
                }
            }
            else
            {
                for (var attempt = 0; attempt < 256; attempt++)
                {
                    var deviation = span * _random.NextDouble();
                    var acceptance = Math.Exp(-(deviation * deviation) / (2.0 * scale * scale));
                    if (_random.NextDouble() <= acceptance)
                        return deviation;
                }
            }

            // Both proposal branches accept at least about 60% per attempt. Reaching
            // this fallback would therefore require a broken random source.
            return 0.0;
        }

        private double Uniform(double minimum, double maximum)
        {
            return minimum + (maximum - minimum) * _random.NextDouble();
        }

        private static double CalibrateHalfNormalScale(double span, double targetMean)
        {
            var low = Math.Max(span * 1e-12, 1e-15);
            var high = Math.Max(span, targetMean);
            while (TruncatedHalfNormalMean(span, high) < targetMean)
                high *= 2.0;

            for (var i = 0; i < 80; i++)
            {
                var middle = (low + high) * 0.5;
                if (TruncatedHalfNormalMean(span, middle) < targetMean)
                    low = middle;
                else
                    high = middle;
            }
            return (low + high) * 0.5;
        }

        private static double TruncatedHalfNormalMean(double span, double scale)
        {
            var ratio = span / scale;
            if (ratio < 0.001)
                return span * 0.5 * (1.0 - ratio * ratio / 12.0);

            var denominator = Erf(ratio / Math.Sqrt(2.0));
            var numerator = 1.0 - Math.Exp(-0.5 * ratio * ratio);
            return scale * Math.Sqrt(2.0 / Math.PI) * numerator / denominator;
        }

        private static double Erf(double value)
        {
            // Numerical Recipes approximation; maximum error is about 1.2e-7.
            var sign = value < 0 ? -1.0 : 1.0;
            var x = Math.Abs(value);
            var t = 1.0 / (1.0 + 0.5 * x);
            var tau = t * Math.Exp(
                -x * x - 1.26551223 +
                t * (1.00002368 +
                t * (0.37409196 +
                t * (0.09678418 +
                t * (-0.18628806 +
                t * (0.27886807 +
                t * (-1.13520398 +
                t * (1.48851587 +
                t * (-0.82215223 +
                t * 0.17087277)))))))));
            return sign * (1.0 - tau);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double RoundStable(double value)
        {
            return Math.Round(value, 9, MidpointRounding.AwayFromZero);
        }
    }

    public static class SensitivityMath
    {
        public static double EffectiveSensitivity(double baseSensitivity, double multiplier)
        {
            return baseSensitivity * multiplier;
        }

        public static double EffectiveEdpi(int dpi, double baseSensitivity, double multiplier)
        {
            return dpi * EffectiveSensitivity(baseSensitivity, multiplier);
        }
    }
}
