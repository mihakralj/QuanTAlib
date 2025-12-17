using System;
using System.Runtime.CompilerServices;
using QuanTAlib;

namespace QuanTAlib;

/// <summary>
/// DMX – Jurik Directional Movement Index
/// A smoother, lower-lag alternative to Welles Wilder’s DMI/ADX.
/// Uses Jurik Moving Average (JMA) for smoothing directional movement components.
/// </summary>
[SkipLocalsInit]
public sealed class Dmx : ITValuePublisher
{
    private readonly Jma _jmaDMp;
    private readonly Jma _jmaDMm;
    private readonly Jma _jmaTR;

    private TBar _prevBar;
    private TBar _lastInput;
    private bool _isInitialized;

    public string Name { get; }
    public event Action<TValue>? Pub;
    public TValue Last { get; private set; }
    public int WarmupPeriod { get; }

    public Dmx(int period)
    {
        Name = $"Dmx({period})";
        WarmupPeriod = period;
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
            if (_isInitialized)
            {
                _prevBar = _lastInput;
            }
            else
            {
                _isInitialized = true;
                // For the very first bar, _prevBar remains default (all zeros)
                // But we want to handle the first bar logic specifically
            }
        }

        // We always update _lastInput to the current input
        _lastInput = input;

        double dmPlusRaw = 0;
        double dmMinusRaw = 0;
        double trRaw = 0;

        if (!_isInitialized || _prevBar.Time == 0) // First bar or uninitialized
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
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        var dmx = new Dmx(period);
        return dmx.Update(source);
    }
}
