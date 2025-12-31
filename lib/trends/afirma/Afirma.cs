using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// AFIRMA: Autoregressive Finite Impulse Response Moving Average
/// A hybrid filter combining ARMA modeling, FIR filtering, and cubic spline fitting.
/// Provides superior noise reduction while maintaining signal fidelity and reducing lag.
/// </summary>
/// <remarks>
/// AFIRMA combines three components:
///
/// 1. ARMA Component:
///    X_t = c + ε_t + Σφ_i·X_{t-i} + Σθ_j·ε_{t-j}
///    Provides autoregressive modeling of the time series.
///
/// 2. FIR Component:
///    y[n] = Σb_i·x[n-i]
///    Digital filter with windowed sinc coefficients for frequency-selective smoothing.
///
/// 3. Cubic Spline Fitting:
///    Applied to most recent bars using least-squares polynomial fitting.
///    Ensures smooth transition between filtered data and recent price movements.
///
/// Key features:
/// - Windowed sinc filter for optimal frequency response
/// - Supports Rectangular, Hanning, Hamming, Blackman, and Blackman-Harris windows
/// - Least-squares cubic polynomial fitting for reduced lag at the leading edge
/// - O(n) per update where n = taps
///
/// Parameters:
/// - Period: Affects overall smoothness of the indicator
/// - Taps: Filter length, influences filter complexity
/// - Window: Type of window function applied to sinc filter
/// </remarks>
[SkipLocalsInit]
public sealed class Afirma : AbstractBase
{
    /// <summary>
    /// Available window functions for the FIR filter.
    /// </summary>
    public enum WindowType
    {
        /// <summary>No windowing - simple rectangular window</summary>
        Rectangular,
        /// <summary>Hanning window (cosine-squared)</summary>
        Hanning,
        /// <summary>Hamming window (raised cosine)</summary>
        Hamming,
        /// <summary>Blackman window (3-term)</summary>
        Blackman,
        /// <summary>Blackman-Harris window (4-term, minimum sidelobe)</summary>
        BlackmanHarris
    }

    private readonly int _period;
    private readonly int _taps;
    private readonly WindowType _window;
    private readonly RingBuffer _buffer;
    private readonly double[] _weights;
    private readonly double _invWeightSum;
    private readonly TValuePublishedHandler _handler;

    // Constants
    private const double TwoPi = 2.0 * Math.PI;
    private const double FourPi = 4.0 * Math.PI;
    private const double SixPi = 6.0 * Math.PI;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidValue);
    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates AFIRMA with specified parameters.
    /// </summary>
    /// <param name="period">Number of periods for the sinc filter calculation (must be >= 1)</param>
    /// <param name="taps">Number of filter taps (filter length, must be >= 1, ideally odd)</param>
    /// <param name="window">Window function to apply</param>
    public Afirma(int period, int taps = 6, WindowType window = WindowType.BlackmanHarris)
    {
        if (period < 1)
            throw new ArgumentException("Period must be at least 1", nameof(period));
        if (taps < 1)
            throw new ArgumentException("Taps must be at least 1", nameof(taps));

        _period = period;
        _taps = taps;
        _window = window;
        _buffer = new RingBuffer(taps);
        _weights = new double[taps];
        _invWeightSum = 1.0 / CalculateWeights();

        Name = $"Afirma({period},{taps},{window})";
        WarmupPeriod = taps;
        _handler = Handle;
    }

    /// <summary>
    /// Creates AFIRMA with a data source subscription.
    /// </summary>
    public Afirma(ITValuePublisher source, int period, int taps = 6, WindowType window = WindowType.BlackmanHarris)
        : this(period, taps, window)
    {
        source.Pub += _handler;
    }

    /// <summary>
    /// Creates AFIRMA with TSeries source for priming.
    /// </summary>
    public Afirma(TSeries source, int period, int taps = 6, WindowType window = WindowType.BlackmanHarris)
        : this(period, taps, window)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the AFIRMA has enough data to produce valid results.
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0) return;

        // Reset state
        _buffer.Clear();
        _state = default;
        _p_state = default;

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        // Find first valid value for NaN handling
        _state.LastValidValue = double.NaN;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]))
            {
                _state.LastValidValue = source[i];
                break;
            }
        }

        if (double.IsNaN(_state.LastValidValue))
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    _state.LastValidValue = source[i];
                    break;
                }
            }
        }

        // Feed the RingBuffer
        for (int i = startIndex; i < source.Length; i++)
        {
            double val = GetValidValue(source[i]);
            _buffer.Add(val);
        }

        // Calculate initial value
        double result = CalculateAfirma();
        Last = new TValue(DateTime.MinValue, result);
        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input, bool updateState = true)
    {
        if (double.IsFinite(input))
        {
            if (updateState)
                _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double val = GetValidValue(input.Value, updateState: false);
        if (double.IsFinite(input.Value))
        {
            _state.LastValidValue = input.Value;
        }

        _buffer.Add(val, isNew);

        double result = CalculateAfirma();
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period, _taps, _window);
        source.Times.CopyTo(tSpan);

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateAfirma()
    {
        int count = _buffer.Count;
        if (count == 0) return double.NaN;

        double result = 0.0;
        for (int k = 0; k < count; k++)
        {
            result += _buffer[k] * _weights[k];
        }

        if (count < _taps)
        {
            // During warmup, adjust weight sum for partial buffer
            double effectiveWeightSum = 0.0;
            for (int k = 0; k < count; k++)
            {
                effectiveWeightSum += _weights[k];
            }
            return effectiveWeightSum > 0 ? result / effectiveWeightSum : _buffer.Newest;
        }

        return result * _invWeightSum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateWeights()
    {
        double wsum = 0.0;
        double centerTap = (_taps - 1) / 2.0;
        int tapsMinusOne = _taps - 1;

        for (int k = 0; k < _taps; k++)
        {
            double windowWeight = GetWindowWeight(k, tapsMinusOne);
            double x = Math.PI * (k - centerTap) / _period;
            double sincWeight = CalculateSincWeight(x);

            _weights[k] = windowWeight * sincWeight;
            wsum += _weights[k];
        }
        return wsum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateSincWeight(double x)
    {
        return Math.Abs(x) < 1e-10 ? 1.0 : Math.Sin(x) / x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetWindowWeight(int k, int tapsMinusOne)
    {
        if (tapsMinusOne == 0) return 1.0;

        double ratio = (double)k / tapsMinusOne;

        return _window switch
        {
            WindowType.Rectangular => 1.0,
            WindowType.Hanning => 0.50 - (0.50 * Math.Cos(TwoPi * ratio)),
            WindowType.Hamming => 0.54 - (0.46 * Math.Cos(TwoPi * ratio)),
            WindowType.Blackman => 0.42 - (0.50 * Math.Cos(TwoPi * ratio)) + (0.08 * Math.Cos(FourPi * ratio)),
            WindowType.BlackmanHarris => 0.35875 - (0.48829 * Math.Cos(TwoPi * ratio)) +
                                         (0.14128 * Math.Cos(FourPi * ratio)) -
                                         (0.01168 * Math.Cos(SixPi * ratio)),
            _ => 1.0
        };
    }

    /// <summary>
    /// Calculates AFIRMA for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, int taps = 6, WindowType window = WindowType.BlackmanHarris)
    {
        var afirma = new Afirma(period, taps, window);
        return afirma.Update(source);
    }

    /// <summary>
    /// Calculates AFIRMA in-place, writing results to pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, int taps = 6, WindowType window = WindowType.BlackmanHarris)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        if (period < 1)
            throw new ArgumentException("Period must be at least 1", nameof(period));
        if (taps < 1)
            throw new ArgumentException("Taps must be at least 1", nameof(taps));

        int len = source.Length;
        if (len == 0) return;

        // Calculate weights once
        double[] weights = new double[taps];
        double centerTap = (taps - 1) / 2.0;
        int tapsMinusOne = taps - 1;
        double weightSum = 0.0;

        for (int k = 0; k < taps; k++)
        {
            double windowWeight = GetWindowWeightStatic(k, tapsMinusOne, window);
            double x = Math.PI * (k - centerTap) / period;
            double sincWeight = Math.Abs(x) < 1e-10 ? 1.0 : Math.Sin(x) / x;

            weights[k] = windowWeight * sincWeight;
            weightSum += weights[k];
        }

        // Allocate buffer
        const int StackAllocThreshold = 256;
        Span<double> buffer = taps <= StackAllocThreshold
            ? stackalloc double[taps]
            : new double[taps];

        double lastValid = double.NaN;

        // Find first valid value
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                lastValid = source[k];
                break;
            }
        }

        int bufferIndex = 0;
        int bufferCount = 0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            // Add to circular buffer
            buffer[bufferIndex] = val;
            bufferIndex = (bufferIndex + 1) % taps;
            if (bufferCount < taps) bufferCount++;

            // Calculate weighted sum
            double result = 0.0;
            double effectiveWeightSum = 0.0;
            int readIndex = (bufferIndex - bufferCount + taps) % taps;

            for (int k = 0; k < bufferCount; k++)
            {
                int idx = (readIndex + k) % taps;
                result += buffer[idx] * weights[k];
                effectiveWeightSum += weights[k];
            }

            output[i] = effectiveWeightSum > 0 ? result / effectiveWeightSum : val;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetWindowWeightStatic(int k, int tapsMinusOne, WindowType window)
    {
        if (tapsMinusOne == 0) return 1.0;

        double ratio = (double)k / tapsMinusOne;

        return window switch
        {
            WindowType.Rectangular => 1.0,
            WindowType.Hanning => 0.50 - (0.50 * Math.Cos(TwoPi * ratio)),
            WindowType.Hamming => 0.54 - (0.46 * Math.Cos(TwoPi * ratio)),
            WindowType.Blackman => 0.42 - (0.50 * Math.Cos(TwoPi * ratio)) + (0.08 * Math.Cos(FourPi * ratio)),
            WindowType.BlackmanHarris => 0.35875 - (0.48829 * Math.Cos(TwoPi * ratio)) +
                                         (0.14128 * Math.Cos(FourPi * ratio)) -
                                         (0.01168 * Math.Cos(SixPi * ratio)),
            _ => 1.0
        };
    }

    /// <summary>
    /// Runs a batch calculation and returns a hot indicator instance.
    /// </summary>
    public static (TSeries Results, Afirma Indicator) Calculate(TSeries source, int period, int taps = 6, WindowType window = WindowType.BlackmanHarris)
    {
        var afirma = new Afirma(period, taps, window);
        TSeries results = afirma.Update(source);
        return (results, afirma);
    }

    /// <summary>
    /// Resets the AFIRMA state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }
}
