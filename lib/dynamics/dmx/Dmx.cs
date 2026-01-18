using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DMX – Jurik Directional Movement Index
/// A smoother, lower-lag alternative to Welles Wilder’s DMI/ADX.
/// Uses Jurik Moving Average (JMA) for smoothing directional movement components.
/// </summary>
[SkipLocalsInit]
public sealed class Dmx : ITValuePublisher
{
    private readonly int _period;
    private readonly Jma _jmaDMp;
    private readonly Jma _jmaDMm;
    private readonly Jma _jmaTR;

    private TBar _prevBar;
    private TBar _lastInput;
    private bool _isInitialized;

    // Snapshot state for bar correction
    private TBar _p_prevBar;
    private TBar _p_lastInput;
    private bool _p_isInitialized;

    public string Name { get; }
    public event TValuePublishedHandler? Pub;
    public TValue Last { get; private set; }
    public int WarmupPeriod { get; }

    public Dmx(int period)
    {
        Name = $"Dmx({period})";
        WarmupPeriod = period;
        _period = period;
        _jmaDMp = new Jma(period);
        _jmaDMm = new Jma(period);
        _jmaTR = new Jma(period);
        _isInitialized = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _jmaDMp.Reset();
        _jmaDMm.Reset();
        _jmaTR.Reset();
        _prevBar = default;
        _lastInput = default;
        _isInitialized = false;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            // Snapshot state BEFORE mutations
            _p_prevBar = _prevBar;
            _p_lastInput = _lastInput;
            _p_isInitialized = _isInitialized;

            if (_isInitialized)
            {
                _prevBar = _lastInput;
            }
            else
            {
                _isInitialized = true;
                // For the very first bar, _prevBar remains default (all zeros)
            }
        }
        else
        {
            // Restore state from snapshot
            _prevBar = _p_prevBar;
            _lastInput = _p_lastInput;
            _isInitialized = _p_isInitialized;

            if (_isInitialized)
            {
                _prevBar = _lastInput;
            }
            else
            {
                _isInitialized = true;
            }
        }

        // Update _lastInput to the current input
        _lastInput = input;

        double dmPlusRaw = 0;
        double dmMinusRaw = 0;
        double trRaw;

        // First bar check: _prevBar.Time == 0 implies uninitialized previous bar
        if (_prevBar.Time == 0)
        {
            trRaw = input.High - input.Low;
        }
        else
        {
            double upMove = input.High - _prevBar.High;
            double downMove = _prevBar.Low - input.Low;

            if (upMove > downMove && upMove > 0)
                dmPlusRaw = upMove;

            if (downMove > upMove && downMove > 0)
                dmMinusRaw = downMove;

            double tr1 = input.High - input.Low;
            double tr2 = Math.Abs(input.High - _prevBar.Close);
            double tr3 = Math.Abs(input.Low - _prevBar.Close);

            trRaw = Math.Max(tr1, Math.Max(tr2, tr3));
        }

        // Smooth with JMA
        // Note: JMA handles NaN and warm-up internally
        double dmPlusSmooth = _jmaDMp.Update(new TValue(input.Time, dmPlusRaw), isNew).Value;
        double dmMinusSmooth = _jmaDMm.Update(new TValue(input.Time, dmMinusRaw), isNew).Value;
        double atrSmooth = _jmaTR.Update(new TValue(input.Time, trRaw), isNew).Value;

        double diPlus = 0;
        double diMinus = 0;

        if (atrSmooth > 1e-12)
        {
            diPlus = (dmPlusSmooth / atrSmooth) * 100.0;
            diMinus = (dmMinusSmooth / atrSmooth) * 100.0;
        }

        double dmxValue = diPlus - diMinus;

        Last = new TValue(input.Time, dmxValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    public TSeries Update(TBarSeries source)
    {
        int count = source.Count;
        if (count == 0)
            return [];

        var t = new List<long>(count);
        var v = new List<double>(count);
        CollectionsMarshal.SetCount(t, count);
        CollectionsMarshal.SetCount(v, count);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        // Span-based batch calculation
        Calculate(source.High.Values, source.Low.Values, source.Close.Values, _period, vSpan);
        source.Close.Times.CopyTo(tSpan);

        // Restore streaming state by replaying only tail bars (JMA needs ~2*period for full warmup)
        Reset();
        int replayStart = Math.Max(0, count - (2 * _period));
        for (int i = replayStart; i < count; i++)
        {
            Update(source[i], isNew: true);
        }

        Last = new TValue(tSpan[count - 1], vSpan[count - 1]);
        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> high,
                                 ReadOnlySpan<double> low,
                                 ReadOnlySpan<double> close,
                                 int period,
                                 Span<double> destination)
    {
        int len = high.Length;
        if (len == 0)
            return;

        if (low.Length != len || close.Length != len || destination.Length != len)
            throw new ArgumentException("All input spans must have the same length", nameof(destination));

        if (period <= 0)
            throw new ArgumentException("Period must be greater than zero.", nameof(period));

        // Use single ArrayPool rent with slicing for better cache locality and fewer allocations
        // Need 6 buffers of len each: dmPlus, dmMinus, tr, dmPlusSmooth, dmMinusSmooth, trSmooth
        const int BufferCount = 6;
        const int StackallocThreshold = 42; // 42 * 6 = 252, fits in stack

        double[]? rented = null;
        scoped Span<double> buffer;

        if (len <= StackallocThreshold)
        {
            buffer = stackalloc double[len * BufferCount];
        }
        else
        {
            rented = ArrayPool<double>.Shared.Rent(len * BufferCount);
            buffer = rented.AsSpan(0, len * BufferCount);
        }

        try
        {
            // Slice the single buffer into 6 spans
            Span<double> dmPlus = buffer.Slice(0, len);
            Span<double> dmMinus = buffer.Slice(len, len);
            Span<double> tr = buffer.Slice(len * 2, len);
            Span<double> dmPlusSmooth = buffer.Slice(len * 3, len);
            Span<double> dmMinusSmooth = buffer.Slice(len * 4, len);
            Span<double> trSmooth = buffer.Slice(len * 5, len);

            // First bar: only true range from high-low, no directional movement
            tr[0] = high[0] - low[0];
            dmPlus[0] = 0.0;
            dmMinus[0] = 0.0;

            for (int i = 1; i < len; i++)
            {
                double h = high[i];
                double l = low[i];
                double ph = high[i - 1];
                double pl = low[i - 1];
                double pc = close[i - 1];

                double upMove = h - ph;
                double downMove = pl - l;

                double dmPlusRaw = 0.0;
                double dmMinusRaw = 0.0;

                if (upMove > downMove && upMove > 0.0)
                    dmPlusRaw = upMove;

                if (downMove > upMove && downMove > 0.0)
                    dmMinusRaw = downMove;

                double tr1 = h - l;
                double tr2 = Math.Abs(h - pc);
                double tr3 = Math.Abs(l - pc);
                double trRaw = Math.Max(tr1, Math.Max(tr2, tr3));

                dmPlus[i] = dmPlusRaw;
                dmMinus[i] = dmMinusRaw;
                tr[i] = trRaw;
            }

            Jma.Calculate(dmPlus, dmPlusSmooth, period);
            Jma.Calculate(dmMinus, dmMinusSmooth, period);
            Jma.Calculate(tr, trSmooth, period);

            for (int i = 0; i < len; i++)
            {
                double atr = trSmooth[i];
                double diPlus = 0.0;
                double diMinus = 0.0;

                if (atr > 1e-12)
                {
                    diPlus = (dmPlusSmooth[i] / atr) * 100.0;
                    diMinus = (dmMinusSmooth[i] / atr) * 100.0;
                }

                destination[i] = diPlus - diMinus;
            }
        }
        finally
        {
            if (rented != null)
                ArrayPool<double>.Shared.Return(rented);
        }
    }

    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        var dmx = new Dmx(period);
        return dmx.Update(source);
    }
}