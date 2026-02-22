using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// NMA: Natural Moving Average (Jim Sloman, Ocean Theory)
/// </summary>
/// <remarks>
/// Adaptive IIR filter where smoothing ratio derives from volatility-weighted
/// sqrt-kernel analysis of log-price movements over a lookback window.
///
/// Calculation: <c>ratio = Σ(oi × (√(i+1) - √i)) / Σ(oi); NMA = NMA[1] + ratio × (src - NMA[1])</c>.
/// </remarks>
/// <seealso href="Nma.md">Detailed documentation</seealso>
/// <seealso href="nma.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Nma : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _lnBuf;
    private readonly RingBuffer _p_lnBuf;
    private readonly double[] _sqrtWeights;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _pubHandler;
    private bool _isNew = true;
    private bool _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastNma, double CurrentNma,
        bool IsInitialized, int BarCount
    );
    private State _state;
    private State _p_state;

    public Nma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _lnBuf = new RingBuffer(period + 1);
        _p_lnBuf = new RingBuffer(period + 1);
        Name = $"Nma({period})";
        WarmupPeriod = period;

        // Precompute sqrt-kernel weights: phi[i] = sqrt(i+1) - sqrt(i)
        _sqrtWeights = new double[period];
        for (int i = 0; i < period; i++)
        {
            _sqrtWeights[i] = Math.Sqrt(i + 1) - Math.Sqrt(i);
        }

        InitState();
    }

    public Nma(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        _pubHandler = Handle;
        source.Pub += _pubHandler;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null && _pubHandler != null)
            {
                _source.Pub -= _pubHandler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    public bool IsNew => _isNew;
    public override bool IsHot => _state.BarCount >= _period;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        // CopyFrom pattern: ComputeRatio() reads all buffer positions,
        // so Snapshot/Restore (single-value) is insufficient — full copy required
        if (isNew)
        {
            _p_state = _state;
            _p_lnBuf.CopyFrom(_lnBuf);
        }
        else
        {
            _state = _p_state;
            _lnBuf.CopyFrom(_p_lnBuf);
        }

        _state.BarCount++;
        if (_state.IsInitialized)
        {
            _state.LastNma = _state.CurrentNma;
        }

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            if (!_state.IsInitialized)
            {
                return input;
            }
            price = Math.Exp(_lnBuf.Newest / 1000.0);
        }

        // Store scaled natural log — always Add() since CopyFrom restores pre-Add state
        double lnVal = price > 0 ? Math.Log(price) * 1000.0 : 0.0;
        _ = _lnBuf.Add(lnVal);

        if (_state.BarCount <= 1)
        {
            _state.LastNma = price;
            _state.CurrentNma = price;
            _state.IsInitialized = true;
            Last = new TValue(input.Time, price);
            PubEvent(Last);
            return Last;
        }

        // Compute volatility-weighted sqrt ratio
        double ratio = ComputeRatio();

        // Adaptive EMA: NMA = prev + ratio * (price - prev) = FMA(prev, 1-ratio, ratio*price)
        double decay = 1.0 - ratio;
        _state.CurrentNma = Math.FusedMultiplyAdd(_state.LastNma, decay, ratio * price);

        Last = new TValue(input.Time, _state.CurrentNma);
        PubEvent(Last);
        return Last;
    }

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

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Replay last _period bars to restore internal state
        Reset();
        int start = 0;
        if (len > 2 * _period)
        {
            start = len - _period;
        }

        for (int i = start; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeRatio()
    {
        int bars = Math.Min(_state.BarCount, _period);
        double num = 0;
        double denom = 0;

        // Walk backward through the log-price buffer
        // i=0 is most recent pair, i=bars-1 is oldest pair
        int bufCount = _lnBuf.Count;
        for (int i = 0; i < bars; i++)
        {
            // Current and previous log-price values
            int idx0 = bufCount - 1 - i;
            int idx1 = bufCount - 2 - i;
            if (idx1 < 0)
            {
                break;
            }

            double oi = Math.Abs(_lnBuf[idx0] - _lnBuf[idx1]);
            num += oi * _sqrtWeights[i];
            denom += oi;
        }

        return denom > 0 ? num / denom : 0;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        Reset();

        for (int i = 0; i < source.Length; i++)
        {
            double price = source[i];
            if (!double.IsFinite(price) && _state.IsInitialized)
            {
                price = Math.Exp(_lnBuf.Newest / 1000.0);
            }

            double lnVal = price > 0 ? Math.Log(price) * 1000.0 : 0.0;
            _lnBuf.Add(lnVal);
            _state.BarCount++;

            if (_state.BarCount <= 1)
            {
                _state.LastNma = price;
                _state.CurrentNma = price;
                _state.IsInitialized = true;
                continue;
            }

            // Compute ratio inline for Prime
            int bars = Math.Min(_state.BarCount, _period);
            double num = 0;
            double denom = 0;
            int bufCount = _lnBuf.Count;
            for (int j = 0; j < bars; j++)
            {
                int idx0 = bufCount - 1 - j;
                int idx1 = bufCount - 2 - j;
                if (idx1 < 0)
                {
                    break;
                }
                double oi = Math.Abs(_lnBuf[idx0] - _lnBuf[idx1]);
                num += oi * _sqrtWeights[j];
                denom += oi;
            }
            double ratio = denom > 0 ? num / denom : 0;

            double decay = 1.0 - ratio;
            double nma = Math.FusedMultiplyAdd(_state.LastNma, decay, ratio * price);

            _state.LastNma = nma;
            _state.CurrentNma = nma;
        }

        Last = new TValue(DateTime.MinValue, _state.CurrentNma);
        _p_state = _state;
    }

    public override void Reset()
    {
        _lnBuf.Clear();
        _p_lnBuf.Clear();
        InitState();
        _p_state = _state;
        Last = default;
    }

    private void InitState()
    {
        _state = new State(
            LastNma: double.NaN,
            CurrentNma: double.NaN,
            IsInitialized: false,
            BarCount: 0
        );
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var nma = new Nma(period);
        return nma.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (source.Length == 0)
        {
            return;
        }

        // Precompute sqrt weights
        double[] sqrtW = ArrayPool<double>.Shared.Rent(period);
        for (int i = 0; i < period; i++)
        {
            sqrtW[i] = Math.Sqrt(i + 1) - Math.Sqrt(i);
        }

        // Circular buffer for log-prices (size period+1)
        int bufSize = period + 1;
        double[] lnBuf = ArrayPool<double>.Shared.Rent(bufSize);
        Array.Clear(lnBuf, 0, bufSize);

        try
        {
            int head = 0;
            int count = 0;
            double lastNma = source[0];

            // Seed first value
            double lnVal = source[0] > 0 ? Math.Log(source[0]) * 1000.0 : 0.0;
            lnBuf[head] = lnVal;
            head = (head + 1) % bufSize;
            count = 1;
            output[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                double price = source[i];
                if (!double.IsFinite(price))
                {
                    price = source[i - 1];
                }

                lnVal = price > 0 ? Math.Log(price) * 1000.0 : 0.0;
                lnBuf[head] = lnVal;
                head = (head + 1) % bufSize;
                if (count < bufSize)
                {
                    count++;
                }

                // Compute volatility-weighted sqrt ratio
                int bars = Math.Min(i + 1, period);
                if (bars > count - 1)
                {
                    bars = count - 1;
                }

                double num = 0;
                double denom = 0;
                for (int j = 0; j < bars; j++)
                {
                    int idx0 = ((head - 1 - j) % bufSize + bufSize) % bufSize;
                    int idx1 = ((head - 2 - j) % bufSize + bufSize) % bufSize;
                    double oi = Math.Abs(lnBuf[idx0] - lnBuf[idx1]);
                    num += oi * sqrtW[j];
                    denom += oi;
                }

                double ratio = denom > 0 ? num / denom : 0;

                // Adaptive EMA
                double decay = 1.0 - ratio;
                double nma = Math.FusedMultiplyAdd(lastNma, decay, ratio * price);

                output[i] = nma;
                lastNma = nma;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(sqrtW);
            ArrayPool<double>.Shared.Return(lnBuf);
        }
    }

    public static (TSeries Results, Nma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Nma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
