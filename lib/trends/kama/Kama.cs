using System;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// KAMA: Kaufman's Adaptive Moving Average
/// </summary>
/// <remarks>
/// KAMA adapts to market volatility by adjusting its smoothing factor based on an Efficiency Ratio (ER).
/// ER is calculated as the ratio of the absolute price change over a period to the sum of absolute price changes (volatility).
///
/// Formula:
/// ER = Change / Volatility
/// Change = Abs(Price - Price[period])
/// Volatility = Sum(Abs(Price[i] - Price[i-1]), period)
/// SC = (ER * (fast_alpha - slow_alpha) + slow_alpha)^2
/// KAMA = KAMA[prev] + SC * (Price - KAMA[prev])
/// </remarks>
[SkipLocalsInit]
public sealed class Kama : ITValuePublisher
{
    private readonly int _period;
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private readonly RingBuffer _buffer;
    private double _kama;
    private double _p_kama;
    private double _volatilitySum;
    private double _p_volatilitySum;
    private double _lastDiffOut;
    private double _lastValidValue;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

    /// <summary>
    /// Current KAMA value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the KAMA has enough data to produce valid results.
    /// </summary>
    public bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates KAMA with specified parameters.
    /// </summary>
    /// <param name="period">Lookback period for Efficiency Ratio (default 10).</param>
    /// <param name="fastPeriod">Fast EMA period for SC calculation (default 2).</param>
    /// <param name="slowPeriod">Slow EMA period for SC calculation (default 30).</param>
    public Kama(int period = 10, int fastPeriod = 2, int slowPeriod = 30)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (fastPeriod <= 0)
            throw new ArgumentException("Fast period must be greater than 0", nameof(fastPeriod));
        if (slowPeriod <= 0)
            throw new ArgumentException("Slow period must be greater than 0", nameof(slowPeriod));
        if (fastPeriod >= slowPeriod)
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));

        _period = period;
        // Buffer needs to hold period + 1 values to calculate Change over 'period' bars
        // Change = Price[0] - Price[period]
        _buffer = new RingBuffer(period + 1);

        _fastAlpha = 2.0 / (fastPeriod + 1);
        _slowAlpha = 2.0 / (slowPeriod + 1);

        Name = $"Kama({period}, {fastPeriod}, {slowPeriod})";
        _kama = double.NaN;
    }

    public Kama(ITValuePublisher source, int period = 10, int fastPeriod = 2, int slowPeriod = 30)
        : this(period, fastPeriod, slowPeriod)
    {
        source.Pub += (item) => Update(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double val = GetValidValue(input.Value);

        if (isNew)
        {
            _p_kama = _kama;
            _p_volatilitySum = _volatilitySum;

            bool wasFull = _buffer.IsFull;
            double removed = _buffer.Add(val);

            if (wasFull)
            {
                double diff_out = Math.Abs(removed - _buffer[0]);
                _lastDiffOut = diff_out;

                double diff_in = Math.Abs(_buffer[^1] - _buffer[^2]);
                _volatilitySum += diff_in - diff_out;
            }
            else if (_buffer.Count >= 2)
            {
                double diff_in = Math.Abs(_buffer[^1] - _buffer[^2]);
                _volatilitySum += diff_in;
                _lastDiffOut = 0;
            }
        }
        else
        {
            // Restore state
            _kama = _p_kama;
            _buffer.UpdateNewest(val);

            if (_buffer.IsFull)
            {
                double diff_in = Math.Abs(_buffer[^1] - _buffer[^2]);
                _volatilitySum = _p_volatilitySum + diff_in - _lastDiffOut;
            }
            else if (_buffer.Count >= 2)
            {
                double diff_in = Math.Abs(_buffer[^1] - _buffer[^2]);
                _volatilitySum = _p_volatilitySum + diff_in;
            }
        }

        // Calculate KAMA
        if (double.IsNaN(_kama))
        {
            _kama = val;
            _p_kama = val; // Ensure p_kama is initialized
        }
        else
        {
            double change = Math.Abs(_buffer[^1] - _buffer[0]);
            double volatility = _volatilitySum;

            // Avoid division by zero
            double er = (volatility > double.Epsilon) ? change / volatility : 0.0;
            // Cap ER at 1.0 just in case floating point errors push it slightly over
            if (er > 1.0) er = 1.0;

            double sc = er * (_fastAlpha - _slowAlpha) + _slowAlpha;
            sc *= sc;

            _kama = _p_kama + sc * (val - _p_kama);
        }

        Last = new TValue(input.Time, _kama);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries([], []);

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);

        // Use static Calculate for performance
        var outputSpan = new double[len];
        
        // fastPeriod = 2/fastAlpha - 1.
        int fastPeriod = (int)Math.Round(2.0 / _fastAlpha - 1);
        int slowPeriod = (int)Math.Round(2.0 / _slowAlpha - 1);

        Calculate(source.Values, outputSpan, _period, fastPeriod, slowPeriod);

        for (int i = 0; i < len; i++)
        {
            t.Add(source.Times[i]);
            v.Add(outputSpan[i]);
        }

        // Restore state by replaying last few bars
        // This is expensive but necessary to sync the object state
        Reset();
        int startIndex = Math.Max(0, len - _period - 1);
        for (int i = startIndex; i < len; i++)
        {
            Update(source[i]);
        }

        return new TSeries(t, v);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, int fastPeriod = 2, int slowPeriod = 30)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (source.Length != output.Length) throw new ArgumentException("Source and output must have the same length");

        double fastAlpha = 2.0 / (fastPeriod + 1);
        double slowAlpha = 2.0 / (slowPeriod + 1);

        // We need a buffer for price history to calculate ER
        // Size period + 1
        int bufSize = period + 1;
        Span<double> buffer = bufSize <= 256 ? stackalloc double[bufSize] : new double[bufSize];
        int bufferIdx = 0;
        int count = 0;

        double volatilitySum = 0;
        double kama = 0;
        bool kamaInitialized = false;
        double lastValid = 0;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            // Add to buffer
            double removed = buffer[bufferIdx];
            buffer[bufferIdx] = val;

            // Update volatility
            if (count >= 1)
            {
                // diff_in = abs(val - prev)
                // prev is at bufferIdx-1 (circular)
                int prevIdx = (bufferIdx - 1 + bufSize) % bufSize;
                double diff_in = Math.Abs(val - buffer[prevIdx]);

                volatilitySum += diff_in;

                if (count == bufSize)
                {
                    // diff_out = abs(removed - new_oldest)
                    // new_oldest is at (bufferIdx + 1) % bufSize
                    int oldestIdx = (bufferIdx + 1) % bufSize;
                    double diff_out = Math.Abs(removed - buffer[oldestIdx]);
                    volatilitySum -= diff_out;
                }
            }

            bufferIdx = (bufferIdx + 1) % bufSize;
            if (count < bufSize) count++;

            if (!kamaInitialized)
            {
                kama = val;
                kamaInitialized = true;
                output[i] = kama;
            }
            else
            {
                // Calculate ER
                // Change = abs(current - oldest)
                // current = val
                // oldest:
                // if full, oldest is at bufferIdx (which is the next write pos, so it holds the oldest)
                // Wait, bufferIdx points to where we WILL write next.
                // So buffer[bufferIdx] is the oldest value (the one that will be overwritten next).
                // So Change = abs(val - buffer[bufferIdx])

                double change = 0;
                change = (count == bufSize) ? Math.Abs(val - buffer[bufferIdx]) : Math.Abs(val - buffer[0]);


                double er = (volatilitySum > double.Epsilon) ? change / volatilitySum : 0.0;
                if (er > 1.0) er = 1.0;

                double sc = er * (fastAlpha - slowAlpha) + slowAlpha;
                sc *= sc;

                kama += sc * (val - kama);
                output[i] = kama;
            }
        }
    }

    public void Reset()
    {
        _buffer.Clear();
        _kama = double.NaN;
        _p_kama = double.NaN;
        _volatilitySum = 0;
        _p_volatilitySum = 0;
        _lastDiffOut = 0;
        _lastValidValue = 0;
        Last = default;
    }
}
