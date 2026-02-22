using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TRAMA: Trend Regularity Adaptive Moving Average
/// </summary>
/// <remarks>
/// LuxAlgo's adaptive EMA using HH/LL frequency as smoothing factor.
/// Flat in ranging markets, responsive in trending conditions.
///
/// Calculation: <c>tc = SMA(HH_or_LL ? 1 : 0, N)²; TRAMA = TRAMA[1] + tc × (src - TRAMA[1])</c>.
/// </remarks>
/// <seealso href="Trama.md">Detailed documentation</seealso>
/// <seealso href="trama.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Trama : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _prices;
    private readonly RingBuffer _events;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _pubHandler;
    private bool _isNew = true;
    private bool _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevHighest, double PrevLowest,
        double LastTrama, double CurrentTrama,
        bool IsInitialized, int BarCount
    );
    private State _state;
    private State _p_state;

    public Trama(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _prices = new RingBuffer(period);
        _events = new RingBuffer(period);
        Name = $"Trama({period})";
        WarmupPeriod = period;
        InitState();
    }

    public Trama(ITValuePublisher source, int period) : this(period)
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
        if (isNew)
        {
            _state.BarCount++;
            if (_state.IsInitialized)
            {
                _state.PrevHighest = _prices.Max();
                _state.PrevLowest = _prices.Min();
                _state.LastTrama = _state.CurrentTrama;
            }
            _p_state = _state;
            _prices.Snapshot();
            _events.Snapshot();
        }
        else
        {
            _state = _p_state;
            _prices.Restore();
            _events.Restore();
        }

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            if (!_state.IsInitialized)
            {
                return input;
            }
            price = _prices.Newest;
        }

        _prices.Add(price, isNew);

        if (_state.BarCount <= 1)
        {
            _state.PrevHighest = price;
            _state.PrevLowest = price;
            _state.LastTrama = price;
            _state.CurrentTrama = price;
            _state.IsInitialized = true;
            _events.Add(0, isNew);
            Last = new TValue(input.Time, price);
            PubEvent(Last);
            return Last;
        }

        double currentHighest = _prices.Max();
        double currentLowest = _prices.Min();

        // Detect new highest-high or lowest-low
        double hh = currentHighest > _state.PrevHighest ? 1.0 : 0.0;
        double ll = currentLowest < _state.PrevLowest ? 1.0 : 0.0;

        // Binary event: did HH or LL occur?
        double evt = (hh != 0.0 || ll != 0.0) ? 1.0 : 0.0;
        _events.Add(evt, isNew);

        // tc = SMA(events, period)² = Average²
        double avg = _events.Average;
        double tc = avg * avg;

        // Adaptive EMA: TRAMA = prev + tc * (price - prev) = FMA(prev, 1-tc, tc*price)
        double decay = 1.0 - tc;
        _state.CurrentTrama = Math.FusedMultiplyAdd(_state.LastTrama, decay, tc * price);

        Last = new TValue(input.Time, _state.CurrentTrama);
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

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        Reset();

        // Process all bars to build state
        for (int i = 0; i < source.Length; i++)
        {
            double price = source[i];
            if (!double.IsFinite(price) && _state.IsInitialized)
            {
                price = _prices.Newest;
            }

            _prices.Add(price);
            _state.BarCount++;

            if (_state.BarCount <= 1)
            {
                _state.PrevHighest = price;
                _state.PrevLowest = price;
                _state.LastTrama = price;
                _state.CurrentTrama = price;
                _state.IsInitialized = true;
                _events.Add(0);
                continue;
            }

            double prevHighest = _state.PrevHighest;
            double prevLowest = _state.PrevLowest;

            double currentHighest = _prices.Max();
            double currentLowest = _prices.Min();

            double hh = currentHighest > prevHighest ? 1.0 : 0.0;
            double ll = currentLowest < prevLowest ? 1.0 : 0.0;
            double evt = (hh != 0.0 || ll != 0.0) ? 1.0 : 0.0;
            _events.Add(evt);

            double avg = _events.Average;
            double tc = avg * avg;
            double decay = 1.0 - tc;
            double trama = Math.FusedMultiplyAdd(_state.LastTrama, decay, tc * price);

            _state.PrevHighest = currentHighest;
            _state.PrevLowest = currentLowest;
            _state.LastTrama = trama;
            _state.CurrentTrama = trama;
        }

        Last = new TValue(DateTime.MinValue, _state.CurrentTrama);
        _p_state = _state;
    }

    public override void Reset()
    {
        _prices.Clear();
        _events.Clear();
        InitState();
        _p_state = _state;
        Last = default;
    }

    private void InitState()
    {
        _state = new State(
            PrevHighest: double.NaN,
            PrevLowest: double.NaN,
            LastTrama: double.NaN,
            CurrentTrama: double.NaN,
            IsInitialized: false,
            BarCount: 0
        );
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var trama = new Trama(period);
        return trama.Update(source);
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

        // Rent circular buffers from ArrayPool
        double[] pricesBuf = ArrayPool<double>.Shared.Rent(period);
        double[] eventsBuf = ArrayPool<double>.Shared.Rent(period);
        Array.Clear(pricesBuf, 0, period);
        Array.Clear(eventsBuf, 0, period);

        try
        {
            int priceHead = 0;
            int eventHead = 0;
            int priceCount = 0;
            int eventCount = 0;
            double eventSum = 0;
            double prevHighest = source[0];
            double prevLowest = source[0];
            double lastTrama = source[0];

            output[0] = source[0];

            // Seed first value into price buffer
            pricesBuf[priceHead] = source[0];
            priceHead = (priceHead + 1) % period;
            priceCount = 1;

            // First event is 0 (no previous to compare)
            eventsBuf[eventHead] = 0;
            eventHead = (eventHead + 1) % period;
            eventCount = 1;
            // eventSum stays 0

            for (int i = 1; i < source.Length; i++)
            {
                double price = source[i];
                if (!double.IsFinite(price))
                {
                    price = source[i - 1]; // last valid substitution
                }

                // Add price to circular buffer
                pricesBuf[priceHead] = price;
                priceHead = (priceHead + 1) % period;
                if (priceCount < period)
                {
                    priceCount++;
                }

                // Compute max/min over price buffer
                double currentHighest = double.MinValue;
                double currentLowest = double.MaxValue;
                int start = priceCount < period ? 0 : priceHead;
                for (int j = 0; j < priceCount; j++)
                {
                    double val = pricesBuf[(start + j) % period];
                    if (val > currentHighest)
                    {
                        currentHighest = val;
                    }
                    if (val < currentLowest)
                    {
                        currentLowest = val;
                    }
                }

                // Detect HH/LL
                double hh = currentHighest > prevHighest ? 1.0 : 0.0;
                double ll = currentLowest < prevLowest ? 1.0 : 0.0;
                double evt = (hh != 0.0 || ll != 0.0) ? 1.0 : 0.0;

                // Add event to circular buffer, maintain running sum
                if (eventCount >= period)
                {
                    int oldIdx = eventHead;
                    eventSum -= eventsBuf[oldIdx];
                }
                eventsBuf[eventHead] = evt;
                eventHead = (eventHead + 1) % period;
                if (eventCount < period)
                {
                    eventCount++;
                }
                eventSum += evt;

                // tc = (eventSum / eventCount)²
                double avg = eventSum / eventCount;
                double tc = avg * avg;

                // Adaptive EMA
                double decay = 1.0 - tc;
                double trama = Math.FusedMultiplyAdd(lastTrama, decay, tc * price);

                output[i] = trama;
                prevHighest = currentHighest;
                prevLowest = currentLowest;
                lastTrama = trama;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(pricesBuf);
            ArrayPool<double>.Shared.Return(eventsBuf);
        }
    }

    public static (TSeries Results, Trama Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Trama(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
