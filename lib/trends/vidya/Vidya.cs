using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VIDYA: Variable Index Dynamic Average
/// </summary>
/// <remarks>
/// VIDYA is an adaptive moving average developed by Tushar Chande.
/// It adjusts the smoothing constant of an Exponential Moving Average (EMA) based on a volatility index.
/// The volatility index used is the Chande Momentum Oscillator (CMO).
///
/// Formula:
/// alpha = 2 / (period + 1)
/// CMO = (Sum(Up) - Sum(Down)) / (Sum(Up) + Sum(Down))
/// VI = Abs(CMO)
/// DynamicAlpha = alpha * VI
/// VIDYA = DynamicAlpha * Price + (1 - DynamicAlpha) * VIDYA_prev
///
/// Key characteristics:
/// - Adapts to market volatility
/// - Flattens in ranging markets (low volatility)
/// - Reacts quickly in trending markets (high volatility)
/// </remarks>
[SkipLocalsInit]
public sealed class Vidya : ITValuePublisher
{
    private readonly double _alpha;
    private readonly RingBuffer _ups;
    private readonly RingBuffer _downs;

    private record struct State(
        double PrevClose, double LastVidya,
        double CurrentClose, double CurrentVidya,
        bool IsInitialized, int BarCount
    );
    private State _state;
    private State _p_state;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

    public TValue Last { get; private set; }

    /// <summary>
    /// Creates VIDYA with specified period.
    /// </summary>
    /// <param name="period">Period for calculation (must be > 0)</param>
    public Vidya(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _alpha = 2.0 / (period + 1);
        _ups = new RingBuffer(period);
        _downs = new RingBuffer(period);
        Name = $"Vidya({period})";
    }

    /// <summary>
    /// Creates VIDYA with specified source and period.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for calculation</param>
    public Vidya(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        _state.BarCount++;
        if (_state.IsInitialized)
        {
            _state.PrevClose = _state.CurrentClose;
            _state.LastVidya = _state.CurrentVidya;
        }

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            // Handle NaN/Infinity by using the last known valid values
            // If not initialized, we can't do much, just return input
            if (!_state.IsInitialized) return input;
            price = _state.CurrentClose; // Use last valid close
        }

        if (_state.BarCount <= 1)
        {
            _state.PrevClose = price;
            _state.LastVidya = price;
            _state.CurrentClose = price;
            _state.CurrentVidya = price;
            _state.IsInitialized = true;
            _ups.Add(0, isNew);
            _downs.Add(0, isNew);
            Last = new TValue(input.Time, _state.CurrentVidya);
            Pub?.Invoke(Last);
            return Last;
        }

        double change = price - _state.PrevClose;
        double up = change > 0 ? change : 0;
        double down = change < 0 ? -change : 0;

        _ups.Add(up, isNew);
        _downs.Add(down, isNew);

        double sumUp = _ups.Sum;
        double sumDown = _downs.Sum;
        double sum = sumUp + sumDown;

        double vi = 0;
        if (sum > double.Epsilon)
        {
            vi = Math.Abs(sumUp - sumDown) / sum;
        }

        double dynamicAlpha = _alpha * vi;
        _state.CurrentVidya = dynamicAlpha * price + (1.0 - dynamicAlpha) * _state.LastVidya;
        _state.CurrentClose = price;

        Last = new TValue(input.Time, _state.CurrentVidya);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        sourceTimes.CopyTo(tSpan);

        Reset();
        for (int i = 0; i < len; i++)
        {
            var val = Update(new TValue(sourceTimes[i], sourceValues[i]), true);
            vSpan[i] = val.Value;
        }

        return new TSeries(t, v);
    }
    /// <summary>
    /// Calculates VIDYA for the entire series.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");

        if (source.Length == 0) return;

        double alpha = 2.0 / (period + 1);

        double[] ups = new double[period];
        double[] downs = new double[period];
        int head = 0;
        double sumUp = 0;
        double sumDown = 0;

        double prevClose = source[0];
        double lastVidya = source[0];

        output[0] = source[0];

        for (int i = 1; i < source.Length; i++)
        {
            double price = source[i];
            if (!double.IsFinite(price))
            {
                price = prevClose;
            }

            double change = price - prevClose;
            double up = change > 0 ? change : 0;
            double down = change < 0 ? -change : 0;

            sumUp -= ups[head];
            sumDown -= downs[head];

            ups[head] = up;
            downs[head] = down;

            sumUp += up;
            sumDown += down;

            head = (head + 1) % period;

            double sum = sumUp + sumDown;
            double vi = 0;
            if (sum > double.Epsilon)
            {
                vi = Math.Abs(sumUp - sumDown) / sum;
            }

            double dynamicAlpha = alpha * vi;
            double currentVidya = dynamicAlpha * price + (1.0 - dynamicAlpha) * lastVidya;

            output[i] = currentVidya;

            prevClose = price;
            lastVidya = currentVidya;
        }
    }

    public void Reset()
    {
        _ups.Clear();
        _downs.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }
}
