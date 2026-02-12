using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SMI: Stochastic Momentum Index
/// </summary>
/// <remarks>
/// Measures where the close sits relative to the midpoint of the recent
/// high-low range, then double-smooths the result with cascaded EMAs.
///
/// Two methods are supported:
///   <b>Blau</b> (default): compute ratio first, then smooth.
///     raw = 100 × (close − midpoint) / rangeHalf
///     K = EMA₂(EMA₁(raw, kSmooth), kSmooth)
///
///   <b>Chande/Kroll</b>: smooth numerator and denominator separately.
///     K = 100 × EMA₂(EMA₁(close − midpoint)) / EMA₂(EMA₁(rangeHalf))
///
/// D (signal) = EMA(K, dSmooth) for both methods.
/// Range: −100 to +100. Values beyond ±40 indicate extreme momentum.
///
/// References:
///   William Blau, "Momentum, Direction, and Divergence" (1995)
///   Tushar Chande &amp; Stanley Kroll, "The New Technical Trader" (1994)
///   PineScript reference: smi.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Smi : ITValuePublisher
{
    private readonly int _kPeriod;
    private readonly int _kSmooth;
    private readonly int _dSmooth;
    private readonly bool _blau;
    private readonly double _a1; // EMA alpha for kSmooth
    private readonly double _d1; // 1 − _a1
    private readonly double _a3; // EMA alpha for dSmooth
    private readonly double _d3; // 1 − _a3

    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly MonotonicDeque _maxDeque;
    private readonly MonotonicDeque _minDeque;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        // Blau path
        double Ema1, double Ema2,
        // Chande/Kroll path (numerator + denominator separate EMAs)
        double NumEma1, double NumEma2, double DenEma1, double DenEma2,
        // Signal EMA
        double Ema3,
        // Warmup compensators
        double E1, double E2, double E3,
        bool Warmup,
        // Last valid inputs
        double LastValidHigh, double LastValidLow, double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue K { get; private set; }
    public TValue D { get; private set; }
    public bool IsHot => _count >= _kPeriod;

    public event TValuePublishedHandler? Pub;

    public Smi(int kPeriod = 10, int kSmooth = 3, int dSmooth = 3, bool blau = true)
    {
        if (kPeriod <= 0)
        {
            throw new ArgumentException("kPeriod must be greater than 0", nameof(kPeriod));
        }
        if (kSmooth <= 0)
        {
            throw new ArgumentException("kSmooth must be greater than 0", nameof(kSmooth));
        }
        if (dSmooth <= 0)
        {
            throw new ArgumentException("dSmooth must be greater than 0", nameof(dSmooth));
        }

        _kPeriod = kPeriod;
        _kSmooth = kSmooth;
        _dSmooth = dSmooth;
        _blau = blau;
        _a1 = 2.0 / (_kSmooth + 1);
        _d1 = 1.0 - _a1;
        _a3 = 2.0 / (_dSmooth + 1);
        _d3 = 1.0 - _a3;

        _hBuf = new double[_kPeriod];
        _lBuf = new double[_kPeriod];
        _maxDeque = new MonotonicDeque(_kPeriod);
        _minDeque = new MonotonicDeque(_kPeriod);
        _count = 0;
        _index = -1;

        _s = new State(0, 0, 0, 0, 0, 0, 0, 1, 1, 1, true,
            double.NaN, double.NaN, double.NaN);
        _ps = _s;

        Name = $"Smi({kPeriod},{kSmooth},{dSmooth})";
        WarmupPeriod = kPeriod + kSmooth + dSmooth;
        _barHandler = HandleBar;
    }

    public Smi(TBarSeries source, int kPeriod = 10, int kSmooth = 3, int dSmooth = 3, bool blau = true)
        : this(kPeriod, kSmooth, dSmooth, blau)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _index++;
            if (_count < _kPeriod)
            {
                _count++;
            }
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(high)) { s.LastValidHigh = high; }
        else { high = s.LastValidHigh; }

        if (double.IsFinite(low)) { s.LastValidLow = low; }
        else { low = s.LastValidLow; }

        if (double.IsFinite(close)) { s.LastValidClose = close; }
        else { close = s.LastValidClose; }

        if (double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
        {
            _s = s;
            Last = new TValue(input.Time, double.NaN);
            K = new TValue(input.Time, double.NaN);
            D = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        int bufIdx = _index < 0 ? 0 : (int)(_index % _kPeriod);
        _hBuf[bufIdx] = high;
        _lBuf[bufIdx] = low;

        if (isNew)
        {
            _maxDeque.PushMax(_index, high, _hBuf);
            _minDeque.PushMin(_index, low, _lBuf);
        }
        else
        {
            _maxDeque.RebuildMax(_hBuf, _index, _count);
            _minDeque.RebuildMin(_lBuf, _index, _count);
        }

        double highest = _maxDeque.GetExtremum(_hBuf);
        double lowest = _minDeque.GetExtremum(_lBuf);
        double midpoint = (highest + lowest) * 0.5;
        double rangeHalf = (highest - lowest) * 0.5;

        double kValue;

        if (_blau)
        {
            double rawSmi = rangeHalf > 0.0 ? 100.0 * (close - midpoint) / rangeHalf : 0.0;

            // Double EMA smoothing on raw ratio
            s.Ema1 = Math.FusedMultiplyAdd(s.Ema1, _d1, _a1 * rawSmi);
            double firstEma;

            if (s.Warmup)
            {
                s.E1 *= _d1;
                s.E2 *= _d1;
                s.E3 *= _d3;
                double c1 = 1.0 / (1.0 - s.E1);
                double c2 = 1.0 / (1.0 - s.E2);
                double c3 = 1.0 / (1.0 - s.E3);

                firstEma = s.Ema1 * c1;
                s.Ema2 = Math.FusedMultiplyAdd(s.Ema2, _d1, _a1 * firstEma);
                kValue = s.Ema2 * c2;
                s.Ema3 = Math.FusedMultiplyAdd(s.Ema3, _d3, _a3 * kValue);
                double dValue = s.Ema3 * c3;

                s.Warmup = Math.Max(Math.Max(s.E1, s.E2), s.E3) > 1e-10;

                _s = s;
                K = new TValue(input.Time, kValue);
                D = new TValue(input.Time, dValue);
                Last = K;
                PubEvent(Last, isNew);
                return Last;
            }

            firstEma = s.Ema1;
            s.Ema2 = Math.FusedMultiplyAdd(s.Ema2, _d1, _a1 * firstEma);
            kValue = s.Ema2;
            s.Ema3 = Math.FusedMultiplyAdd(s.Ema3, _d3, _a3 * kValue);

            _s = s;
            K = new TValue(input.Time, kValue);
            D = new TValue(input.Time, s.Ema3);
            Last = K;
            PubEvent(Last, isNew);
            return Last;
        }

        // Chande/Kroll: smooth numerator and denominator separately
        double numerator = close - midpoint;
        double denominator = rangeHalf;

        // First EMA layer
        s.NumEma1 = Math.FusedMultiplyAdd(s.NumEma1, _d1, _a1 * numerator);
        s.DenEma1 = Math.FusedMultiplyAdd(s.DenEma1, _d1, _a1 * denominator);

        if (s.Warmup)
        {
            s.E1 *= _d1;
            s.E2 *= _d1;
            s.E3 *= _d3;
            double c1 = 1.0 / (1.0 - s.E1);
            double c2 = 1.0 / (1.0 - s.E2);
            double c3 = 1.0 / (1.0 - s.E3);

            double numFirst = s.NumEma1 * c1;
            double denFirst = s.DenEma1 * c1;

            // Second EMA layer
            s.NumEma2 = Math.FusedMultiplyAdd(s.NumEma2, _d1, _a1 * numFirst);
            s.DenEma2 = Math.FusedMultiplyAdd(s.DenEma2, _d1, _a1 * denFirst);

            double smoothNum = s.NumEma2 * c2;
            double smoothDen = s.DenEma2 * c2;

            kValue = smoothDen > 0.0 ? 100.0 * smoothNum / smoothDen : 0.0;

            s.Ema3 = Math.FusedMultiplyAdd(s.Ema3, _d3, _a3 * kValue);
            double dVal = s.Ema3 * c3;

            s.Warmup = Math.Max(Math.Max(s.E1, s.E2), s.E3) > 1e-10;

            _s = s;
            K = new TValue(input.Time, kValue);
            D = new TValue(input.Time, dVal);
            Last = K;
            PubEvent(Last, isNew);
            return Last;
        }

        double numF = s.NumEma1;
        double denF = s.DenEma1;

        s.NumEma2 = Math.FusedMultiplyAdd(s.NumEma2, _d1, _a1 * numF);
        s.DenEma2 = Math.FusedMultiplyAdd(s.DenEma2, _d1, _a1 * denF);

        kValue = s.DenEma2 > 0.0 ? 100.0 * s.NumEma2 / s.DenEma2 : 0.0;

        s.Ema3 = Math.FusedMultiplyAdd(s.Ema3, _d3, _a3 * kValue);

        _s = s;
        K = new TValue(input.Time, kValue);
        D = new TValue(input.Time, s.Ema3);
        Last = K;
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double val = input.Value;
        return Update(new TBar(input.Time, val, val, val, val, 0), isNew);
    }

    public (TSeries K, TSeries D) Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var kArr = new double[len];
        var dArr = new double[len];

        Batch(source.High.Values, source.Low.Values, source.Close.Values,
            kArr, dArr, _kPeriod, _kSmooth, _dSmooth, _blau);

        var tK = new List<long>(len);
        var vK = new List<double>(len);
        var tD = new List<long>(len);
        var vD = new List<double>(len);

        CollectionsMarshal.SetCount(tK, len);
        CollectionsMarshal.SetCount(vK, len);
        CollectionsMarshal.SetCount(tD, len);
        CollectionsMarshal.SetCount(vD, len);

        source.Open.Times.CopyTo(CollectionsMarshal.AsSpan(tK));
        CollectionsMarshal.AsSpan(tK).CopyTo(CollectionsMarshal.AsSpan(tD));
        kArr.AsSpan().CopyTo(CollectionsMarshal.AsSpan(vK));
        dArr.AsSpan().CopyTo(CollectionsMarshal.AsSpan(vD));

        // Restore streaming state by replaying
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return (new TSeries(tK, vK), new TSeries(tD, vD));
    }

    public void Prime(TBarSeries source)
    {
        Reset();
        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    public void Reset()
    {
        Array.Clear(_hBuf);
        Array.Clear(_lBuf);
        _maxDeque.Reset();
        _minDeque.Reset();
        _count = 0;
        _index = -1;
        _s = new State(0, 0, 0, 0, 0, 0, 0, 1, 1, 1, true,
            double.NaN, double.NaN, double.NaN);
        _ps = _s;
        Last = default;
        K = default;
        D = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> kOut,
        Span<double> dOut,
        int kPeriod = 10,
        int kSmooth = 3,
        int dSmooth = 3,
        bool blau = true)
    {
        if (kPeriod <= 0)
        {
            throw new ArgumentException("kPeriod must be greater than 0", nameof(kPeriod));
        }
        if (kSmooth <= 0)
        {
            throw new ArgumentException("kSmooth must be greater than 0", nameof(kSmooth));
        }
        if (dSmooth <= 0)
        {
            throw new ArgumentException("dSmooth must be greater than 0", nameof(dSmooth));
        }
        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("Input spans must have the same length", nameof(high));
        }
        if (kOut.Length < high.Length)
        {
            throw new ArgumentException("K output span must be at least as long as input", nameof(kOut));
        }
        if (dOut.Length < high.Length)
        {
            throw new ArgumentException("D output span must be at least as long as input", nameof(dOut));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        double a1 = 2.0 / (kSmooth + 1);
        double d1 = 1.0 - a1;
        double a3 = 2.0 / (dSmooth + 1);
        double d3 = 1.0 - a3;

        const int StackallocThreshold = 256;
        double[]? rentedUpper = null;
        double[]? rentedLower = null;
        scoped Span<double> upperBuf;
        scoped Span<double> lowerBuf;

        if (len <= StackallocThreshold)
        {
            upperBuf = stackalloc double[len];
            lowerBuf = stackalloc double[len];
        }
        else
        {
            rentedUpper = ArrayPool<double>.Shared.Rent(len);
            rentedLower = ArrayPool<double>.Shared.Rent(len);
            upperBuf = rentedUpper.AsSpan(0, len);
            lowerBuf = rentedLower.AsSpan(0, len);
        }

        try
        {
            Highest.Batch(high, upperBuf, kPeriod);
            Lowest.Batch(low, lowerBuf, kPeriod);

            if (blau)
            {
                BatchBlau(close, upperBuf, lowerBuf, kOut, dOut, len, a1, d1, a3, d3);
            }
            else
            {
                BatchChandeKroll(close, upperBuf, lowerBuf, kOut, dOut, len, a1, d1, a3, d3);
            }
        }
        finally
        {
            if (rentedUpper != null)
            {
                ArrayPool<double>.Shared.Return(rentedUpper);
            }
            if (rentedLower != null)
            {
                ArrayPool<double>.Shared.Return(rentedLower);
            }
        }
    }

    public static (TSeries K, TSeries D) Batch(TBarSeries source,
        int kPeriod = 10, int kSmooth = 3, int dSmooth = 3, bool blau = true)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var kArr = new double[len];
        var dArr = new double[len];

        Batch(source.High.Values, source.Low.Values, source.Close.Values,
            kArr, dArr, kPeriod, kSmooth, dSmooth, blau);

        var tK = new List<long>(len);
        var vK = new List<double>(len);
        var tD = new List<long>(len);
        var vD = new List<double>(len);

        CollectionsMarshal.SetCount(tK, len);
        CollectionsMarshal.SetCount(vK, len);
        CollectionsMarshal.SetCount(tD, len);
        CollectionsMarshal.SetCount(vD, len);

        source.Open.Times.CopyTo(CollectionsMarshal.AsSpan(tK));
        CollectionsMarshal.AsSpan(tK).CopyTo(CollectionsMarshal.AsSpan(tD));
        kArr.AsSpan().CopyTo(CollectionsMarshal.AsSpan(vK));
        dArr.AsSpan().CopyTo(CollectionsMarshal.AsSpan(vD));

        return (new TSeries(tK, vK), new TSeries(tD, vD));
    }

    public static ((TSeries K, TSeries D) Results, Smi Indicator) Calculate(
        TBarSeries source, int kPeriod = 10, int kSmooth = 3, int dSmooth = 3, bool blau = true)
    {
        var indicator = new Smi(kPeriod, kSmooth, dSmooth, blau);
        var results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BatchBlau(
        ReadOnlySpan<double> close,
        ReadOnlySpan<double> highest,
        ReadOnlySpan<double> lowest,
        Span<double> kOut,
        Span<double> dOut,
        int len,
        double a1, double d1, double a3, double d3)
    {
        double ema1 = 0, ema2 = 0, ema3 = 0;
        double e1 = 1, e2 = 1, e3 = 1;
        bool warmup = true;

        for (int i = 0; i < len; i++)
        {
            double mid = (highest[i] + lowest[i]) * 0.5;
            double rh = (highest[i] - lowest[i]) * 0.5;
            double raw = rh > 0 ? 100.0 * (close[i] - mid) / rh : 0.0;

            ema1 = Math.FusedMultiplyAdd(ema1, d1, a1 * raw);

            double k;
            double d;
            if (warmup)
            {
                e1 *= d1;
                e2 *= d1;
                e3 *= d3;
                double c1 = 1.0 / (1.0 - e1);
                double c2 = 1.0 / (1.0 - e2);
                double c3 = 1.0 / (1.0 - e3);

                double f = ema1 * c1;
                ema2 = Math.FusedMultiplyAdd(ema2, d1, a1 * f);
                k = ema2 * c2;
                ema3 = Math.FusedMultiplyAdd(ema3, d3, a3 * k);
                d = ema3 * c3;
                warmup = Math.Max(Math.Max(e1, e2), e3) > 1e-10;
            }
            else
            {
                ema2 = Math.FusedMultiplyAdd(ema2, d1, a1 * ema1);
                k = ema2;
                ema3 = Math.FusedMultiplyAdd(ema3, d3, a3 * k);
                d = ema3;
            }

            kOut[i] = k;
            dOut[i] = d;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BatchChandeKroll(
        ReadOnlySpan<double> close,
        ReadOnlySpan<double> highest,
        ReadOnlySpan<double> lowest,
        Span<double> kOut,
        Span<double> dOut,
        int len,
        double a1, double d1, double a3, double d3)
    {
        double numEma1 = 0, numEma2 = 0, denEma1 = 0, denEma2 = 0, ema3 = 0;
        double e1 = 1, e2 = 1, e3 = 1;
        bool warmup = true;

        for (int i = 0; i < len; i++)
        {
            double mid = (highest[i] + lowest[i]) * 0.5;
            double rh = (highest[i] - lowest[i]) * 0.5;
            double num = close[i] - mid;
            double den = rh;

            numEma1 = Math.FusedMultiplyAdd(numEma1, d1, a1 * num);
            denEma1 = Math.FusedMultiplyAdd(denEma1, d1, a1 * den);

            double k;
            double d;
            if (warmup)
            {
                e1 *= d1;
                e2 *= d1;
                e3 *= d3;
                double c1 = 1.0 / (1.0 - e1);
                double c2 = 1.0 / (1.0 - e2);
                double c3 = 1.0 / (1.0 - e3);

                double nf = numEma1 * c1;
                double df = denEma1 * c1;
                numEma2 = Math.FusedMultiplyAdd(numEma2, d1, a1 * nf);
                denEma2 = Math.FusedMultiplyAdd(denEma2, d1, a1 * df);
                double sn = numEma2 * c2;
                double sd = denEma2 * c2;
                k = sd > 0 ? 100.0 * sn / sd : 0.0;
                ema3 = Math.FusedMultiplyAdd(ema3, d3, a3 * k);
                d = ema3 * c3;
                warmup = Math.Max(Math.Max(e1, e2), e3) > 1e-10;
            }
            else
            {
                numEma2 = Math.FusedMultiplyAdd(numEma2, d1, a1 * numEma1);
                denEma2 = Math.FusedMultiplyAdd(denEma2, d1, a1 * denEma1);
                k = denEma2 > 0 ? 100.0 * numEma2 / denEma2 : 0.0;
                ema3 = Math.FusedMultiplyAdd(ema3, d3, a3 * k);
                d = ema3;
            }

            kOut[i] = k;
            dOut[i] = d;
        }
    }
}
