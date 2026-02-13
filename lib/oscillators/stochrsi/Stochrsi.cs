// STOCHRSI: Stochastic RSI Oscillator
// Applies the Stochastic formula to RSI values instead of price,
// producing a more sensitive overbought/oversold indicator.
// Tushar Chande & Stanley Kroll, 1994.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// STOCHRSI: Stochastic RSI Oscillator
/// </summary>
/// <remarks>
/// Applies the Stochastic oscillator formula to RSI values.
/// K = SMA(100 × (RSI - minRSI) / (maxRSI - minRSI), kSmooth)
/// D = SMA(K, dSmooth)
/// Range: 0-100. More sensitive than RSI alone.
/// </remarks>
[SkipLocalsInit]
public sealed class Stochrsi : AbstractBase
{
    private const int DefaultRsiLength = 14;
    private const int DefaultStochLength = 14;
    private const int DefaultKSmooth = 3;
    private const int DefaultDSmooth = 3;

    private readonly int _stochLength;
    private readonly int _kSmooth;
    private readonly int _dSmooth;

    private readonly Rsi _rsi;
    private readonly double[] _rsiBuf;
    private readonly double[] _kBuf;
    private readonly double[] _dBuf;
    private readonly MonotonicDeque _maxDeque;
    private readonly MonotonicDeque _minDeque;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        long Count,
        double KSum,
        int KHead,
        double DSum,
        int DHead,
        double LastValidValue,
        double K,
        double D,
        double PrevRsiBufVal,
        double PrevKBufVal,
        double PrevDBufVal);

    private State _s;
    private State _ps;

    /// <summary>Current %K value (SMA-smoothed raw stochastic of RSI).</summary>
    public double K => _s.K;

    /// <summary>Current %D value (SMA of %K signal line).</summary>
    public double D => _s.D;

    public override bool IsHot => _s.Count >= _rsi.WarmupPeriod + _stochLength - 1 + _kSmooth - 1;

    /// <summary>
    /// Creates StochRSI with specified parameters.
    /// </summary>
    /// <param name="rsiLength">Period for RSI calculation (default: 14).</param>
    /// <param name="stochLength">Stochastic lookback over RSI values (default: 14).</param>
    /// <param name="kSmooth">SMA smoothing for %K (default: 3).</param>
    /// <param name="dSmooth">SMA smoothing for %D (default: 3).</param>
    public Stochrsi(int rsiLength = DefaultRsiLength, int stochLength = DefaultStochLength,
                    int kSmooth = DefaultKSmooth, int dSmooth = DefaultDSmooth)
    {
        if (rsiLength <= 0)
        {
            throw new ArgumentException("RSI length must be greater than 0", nameof(rsiLength));
        }
        if (stochLength <= 0)
        {
            throw new ArgumentException("Stochastic length must be greater than 0", nameof(stochLength));
        }
        if (kSmooth <= 0)
        {
            throw new ArgumentException("K smoothing must be greater than 0", nameof(kSmooth));
        }
        if (dSmooth <= 0)
        {
            throw new ArgumentException("D smoothing must be greater than 0", nameof(dSmooth));
        }

        _stochLength = stochLength;
        _kSmooth = kSmooth;
        _dSmooth = dSmooth;

        _rsi = new Rsi(rsiLength);
        _rsiBuf = new double[stochLength];
        _kBuf = new double[kSmooth];
        _dBuf = new double[dSmooth];
        _maxDeque = new MonotonicDeque(stochLength);
        _minDeque = new MonotonicDeque(stochLength);

        _s = new State(0, 0, 0, 0, 0, double.NaN, double.NaN, double.NaN, 0, 0, 0);
        _ps = _s;

        Name = $"StochRsi({rsiLength},{stochLength},{kSmooth},{dSmooth})";
        WarmupPeriod = _rsi.WarmupPeriod + stochLength - 1 + kSmooth - 1 + dSmooth - 1;
    }

    /// <summary>
    /// Creates StochRSI subscribed to a source publisher.
    /// </summary>
    public Stochrsi(ITValuePublisher source, int rsiLength = DefaultRsiLength,
                    int stochLength = DefaultStochLength, int kSmooth = DefaultKSmooth,
                    int dSmooth = DefaultDSmooth)
        : this(rsiLength, stochLength, kSmooth, dSmooth)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            // Save buffer slot values that will be overwritten (for future rollback)
            int idx = (int)(_s.Count % _stochLength);
            _s.PrevRsiBufVal = _rsiBuf[idx];
            if (_kSmooth > 1)
            {
                _s.PrevKBufVal = _kBuf[_s.KHead];
            }
            if (_dSmooth > 1)
            {
                _s.PrevDBufVal = _dBuf[_s.DHead];
            }

            _ps = _s;
        }
        else
        {
            // Restore buffer slots that were overwritten by previous call
            int idx = (int)(_ps.Count % _stochLength);
            _rsiBuf[idx] = _ps.PrevRsiBufVal;

            if (_kSmooth > 1)
            {
                _kBuf[_ps.KHead] = _ps.PrevKBufVal;
            }
            if (_dSmooth > 1)
            {
                _dBuf[_ps.DHead] = _ps.PrevDBufVal;
            }

            _s = _ps;
        }

        var s = _s;

        // NaN/Infinity guard
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(s.LastValidValue) ? s.LastValidValue : 0;
        }
        else
        {
            s.LastValidValue = val;
        }

        // Step 1: Compute RSI (RSI handles its own bar correction via isNew)
        double rsiVal = _rsi.Update(new TValue(input.Time, val), isNew).Value;

        // Step 2: Store RSI in circular buffer, then update deques
        int bufIdx = (int)(s.Count % _stochLength);
        _rsiBuf[bufIdx] = rsiVal;

        if (isNew)
        {
            _maxDeque.PushMax(s.Count, rsiVal, _rsiBuf);
            _minDeque.PushMin(s.Count, rsiVal, _rsiBuf);
        }
        else
        {
            // Rebuild deques from buffer (buffer now has correct value at current index)
            int bufCount = (int)Math.Min(s.Count + 1, _stochLength);
            _maxDeque.RebuildMax(_rsiBuf, s.Count, bufCount);
            _minDeque.RebuildMin(_rsiBuf, s.Count, bufCount);
        }

        double highestRsi = _maxDeque.GetExtremum(_rsiBuf);
        double lowestRsi = _minDeque.GetExtremum(_rsiBuf);
        double rsiRange = highestRsi - lowestRsi;

        // Step 3: Raw stochastic of RSI
        double kRaw = rsiRange > 1e-10 ? 100.0 * (rsiVal - lowestRsi) / rsiRange : 50.0;

        // Step 4: SMA smooth kRaw → K
        double kSmoothed;
        if (_kSmooth <= 1)
        {
            kSmoothed = kRaw;
        }
        else
        {
            // Circular buffer SMA for K
            s.KSum -= _kBuf[s.KHead];
            _kBuf[s.KHead] = kRaw;
            s.KSum += kRaw;
            s.KHead = (s.KHead + 1) % _kSmooth;

            long kCount = s.Count + 1 - (_rsi.WarmupPeriod + _stochLength - 1);
            int kFilled = (int)Math.Min(Math.Max(kCount, 1), _kSmooth);
            kSmoothed = s.KSum / kFilled;
        }

        // Step 5: SMA smooth K → D
        double dSmoothed;
        if (_dSmooth <= 1)
        {
            dSmoothed = kSmoothed;
        }
        else
        {
            s.DSum -= _dBuf[s.DHead];
            _dBuf[s.DHead] = kSmoothed;
            s.DSum += kSmoothed;
            s.DHead = (s.DHead + 1) % _dSmooth;

            long dCount = s.Count + 1 - (_rsi.WarmupPeriod + _stochLength - 1 + _kSmooth - 1);
            int dFilled = (int)Math.Min(Math.Max(dCount, 1), _dSmooth);
            dSmoothed = s.DSum / dFilled;
        }

        s.K = kSmoothed;
        s.D = dSmoothed;

        s.Count++;
        _s = s;

        Last = new TValue(input.Time, kSmoothed);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a full series, returning K values.
    /// Use the K and D properties or Batch method for both outputs.
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);
        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        // Use streaming replay to ensure consistency with Update(TValue)
        Reset();
        for (int i = 0; i < len; i++)
        {
            var result = Update(new TValue(source.Times[i], source.Values[i]));
            tSpan[i] = source.Times[i];
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Returns both K and D series from source.
    /// </summary>
    public (TSeries K, TSeries D) UpdateKD(TSeries source)
    {
        if (source.Count == 0)
        {
            return ([], []);
        }

        int len = source.Count;
        var tK = new List<long>(len);
        var vK = new List<double>(len);
        var tD = new List<long>(len);
        var vD = new List<double>(len);
        CollectionsMarshal.SetCount(tK, len);
        CollectionsMarshal.SetCount(vK, len);
        CollectionsMarshal.SetCount(tD, len);
        CollectionsMarshal.SetCount(vD, len);

        Reset();
        var tKSpan = CollectionsMarshal.AsSpan(tK);
        var vKSpan = CollectionsMarshal.AsSpan(vK);
        var tDSpan = CollectionsMarshal.AsSpan(tD);
        var vDSpan = CollectionsMarshal.AsSpan(vD);

        for (int i = 0; i < len; i++)
        {
            _ = Update(new TValue(source.Times[i], source.Values[i]));
            long time = source.Times[i];
            tKSpan[i] = time;
            vKSpan[i] = _s.K;
            tDSpan[i] = time;
            vDSpan[i] = _s.D;
        }

        return (new TSeries(tK, vK), new TSeries(tD, vD));
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public override void Reset()
    {
        _rsi.Reset();
        _maxDeque.Reset();
        _minDeque.Reset();
        Array.Clear(_rsiBuf);
        Array.Clear(_kBuf);
        Array.Clear(_dBuf);
        _s = new State(0, 0, 0, 0, 0, double.NaN, double.NaN, double.NaN, 0, 0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Computes StochRSI %K for an entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int rsiLength = DefaultRsiLength,
                                int stochLength = DefaultStochLength, int kSmooth = DefaultKSmooth,
                                int dSmooth = DefaultDSmooth)
    {
        var ind = new Stochrsi(rsiLength, stochLength, kSmooth, dSmooth);
        return ind.Update(source);
    }

    /// <summary>
    /// High-performance span-based StochRSI %K calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
                             int rsiLength, int stochLength, int kSmooth, int dSmooth)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (rsiLength <= 0)
        {
            throw new ArgumentException("RSI length must be greater than 0", nameof(rsiLength));
        }
        if (stochLength <= 0)
        {
            throw new ArgumentException("Stochastic length must be greater than 0", nameof(stochLength));
        }
        if (kSmooth <= 0)
        {
            throw new ArgumentException("K smoothing must be greater than 0", nameof(kSmooth));
        }
        if (dSmooth <= 0)
        {
            throw new ArgumentException("D smoothing must be greater than 0", nameof(dSmooth));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Use streaming instance to guarantee consistency with Update(TValue)
        var ind = new Stochrsi(rsiLength, stochLength, kSmooth, dSmooth);
        for (int i = 0; i < len; i++)
        {
            output[i] = ind.Update(new TValue(DateTime.MinValue, source[i])).Value;
        }
    }

    /// <summary>
    /// Runs batch calculation and returns a hot indicator ready for streaming.
    /// </summary>
    public static (TSeries Results, Stochrsi Indicator) Calculate(TSeries source,
        int rsiLength = DefaultRsiLength, int stochLength = DefaultStochLength,
        int kSmooth = DefaultKSmooth, int dSmooth = DefaultDSmooth)
    {
        var indicator = new Stochrsi(rsiLength, stochLength, kSmooth, dSmooth);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }
}
