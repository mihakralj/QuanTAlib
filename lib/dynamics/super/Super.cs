using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SuperTrend Indicator
/// A trend-following indicator that uses ATR to define upper and lower bands.
/// </summary>
[SkipLocalsInit]
public sealed class Super : ITValuePublisher
{
    private readonly double _multiplier;
    private readonly int _period;
    private TBar _prevBar;
    private TBar _lastInput;
    private TBar _p_prevBar;
    private TBar _p_lastInput;
    private int _sampleCount;
    private int _p_sampleCount;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public bool IsBullish;
        public double UpperBand;
        public double LowerBand;
        public bool IsInitialized;
        public double Atr;
        public double SumTr;
    }

    private State _state;
    private State _p_state;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name => $"Super({_period},{_multiplier})";

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current SuperTrend value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Current Upper Band value.
    /// </summary>
    public TValue UpperBand { get; private set; }

    /// <summary>
    /// Current Lower Band value.
    /// </summary>
    public TValue LowerBand { get; private set; }

    /// <summary>
    /// True if the current trend is bullish.
    /// </summary>
    public bool IsBullish => _state.IsBullish;

    /// <summary>
    /// True if the indicator has enough data to be valid.
    /// </summary>
    public bool IsHot => _sampleCount > _period;

    public int WarmupPeriod => _period + 1;

    public Super(int period = 10, double multiplier = 3.0)
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0.");
        }
        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be greater than 0.");
        }
        _period = period;
        _multiplier = multiplier;
        _state = new State { IsBullish = true, IsInitialized = false };
        _sampleCount = 0;
    }

    public void Reset()
    {
        _state = new State { IsBullish = true, IsInitialized = false };
        _p_state = default;
        _prevBar = default;
        _lastInput = default;
        _p_prevBar = default;
        _p_lastInput = default;
        _sampleCount = 0;
        _p_sampleCount = 0;
        Last = default;
        UpperBand = default;
        LowerBand = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _p_prevBar = _prevBar;
            _p_lastInput = _lastInput;
            _p_sampleCount = _sampleCount;
            if (_sampleCount > 0)
            {
                _prevBar = _lastInput;
            }
            _sampleCount++;
        }
        else
        {
            _state = _p_state;
            _prevBar = _p_prevBar;
            _lastInput = _p_lastInput;
            _sampleCount = _p_sampleCount;
            if (_sampleCount > 0)
            {
                _prevBar = _lastInput;
            }
        }
        _lastInput = input;

        // Calculate True Range with NaN/Infinity guards
        double safeHigh = double.IsFinite(input.High) ? input.High : _prevBar.High;
        double safeLow = double.IsFinite(input.Low) ? input.Low : _prevBar.Low;
        double safePrevClose = double.IsFinite(_prevBar.Close) ? _prevBar.Close : safeHigh;

        double tr;
        if (_sampleCount <= 1)
        {
            tr = safeHigh - safeLow;
        }
        else
        {
            double h_l = safeHigh - safeLow;
            double h_pc = Math.Abs(safeHigh - safePrevClose);
            double l_pc = Math.Abs(safeLow - safePrevClose);
            tr = Math.Max(h_l, Math.Max(h_pc, l_pc));
        }

        // Update ATR using RMA (Wilder's smoothing)
        // Note: Skender's implementation skips the first bar's TR for the initial SMA calculation.
        double atr;
        if (_sampleCount == 1)
        {
            atr = 0;
        }
        else if (_sampleCount <= _period + 1)
        {
            _state.SumTr += tr;
            if (_sampleCount == _period + 1)
            {
                _state.Atr = _state.SumTr / _period;
            }
            atr = _state.Atr;
        }
        else
        {
            // RMA: (prevAtr * (period - 1) + tr) / period
            // Rewritten as FMA: prevAtr * decay + tr * alpha where decay = (period-1)/period, alpha = 1/period
            double invPeriod = 1.0 / _period;
            _state.Atr = Math.FusedMultiplyAdd(_state.Atr, 1.0 - invPeriod, tr * invPeriod);
            atr = _state.Atr;
        }

        double superTrend = double.NaN;
        double upperBand = double.NaN;
        double lowerBand = double.NaN;

        if (_sampleCount > _period)
        {
            double mid = (input.High + input.Low) * 0.5;
            // Use FMA for band calculations: mid + multiplier * atr
            double upperEval = Math.FusedMultiplyAdd(_multiplier, atr, mid);
            double lowerEval = Math.FusedMultiplyAdd(-_multiplier, atr, mid);

            if (!_state.IsInitialized)
            {
                _state.IsBullish = true; // Skender seems to default to Bullish (or determines it dynamically)
                _state.UpperBand = upperEval;
                _state.LowerBand = lowerEval;
                _state.IsInitialized = true;
            }

            double prevUpperBand = _state.UpperBand;
            double prevLowerBand = _state.LowerBand;
            double prevClose = _prevBar.Close;

            // New upper band
            if (upperEval < prevUpperBand || prevClose > prevUpperBand)
            {
                _state.UpperBand = upperEval;
            }

            // New lower band
            if (lowerEval > prevLowerBand || prevClose < prevLowerBand)
            {
                _state.LowerBand = lowerEval;
            }

            // SuperTrend
            if (_state.IsBullish)
            {
                if (input.Close < _state.LowerBand)
                {
                    _state.IsBullish = false;
                    superTrend = _state.UpperBand;
                }
                else
                {
                    superTrend = _state.LowerBand;
                }
            }
            else
            {
                if (input.Close > _state.UpperBand)
                {
                    _state.IsBullish = true;
                    superTrend = _state.LowerBand;
                }
                else
                {
                    superTrend = _state.UpperBand;
                }
            }

            upperBand = _state.UpperBand;
            lowerBand = _state.LowerBand;
        }

        Last = new TValue(input.Time, superTrend);
        UpperBand = new TValue(input.Time, upperBand);
        LowerBand = new TValue(input.Time, lowerBand);

        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    public TSeries Update(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    public static TSeries Batch(TBarSeries source, int period = 10, double multiplier = 3.0)
    {
        var indicator = new Super(period, multiplier);
        return indicator.Update(source);
    }
}
