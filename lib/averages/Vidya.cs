using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

public class Vidya : AbstractBase
{
    private readonly int _longPeriod;
    private readonly double _alpha;
    private double _lastVIDYA, _p_lastVIDYA;
    private readonly CircularBuffer? _shortBuffer;
    private readonly CircularBuffer? _longBuffer;

    public Vidya(int shortPeriod, int longPeriod = 0, double alpha = 0.2)
    {
        if (shortPeriod < 1)
        {
            throw new ArgumentException("Short period must be greater than or equal to 1.", nameof(shortPeriod));
        }
        _longPeriod = (longPeriod == 0) ? shortPeriod * 4 : longPeriod;
        _alpha = alpha;
        WarmupPeriod = _longPeriod;
        Name = $"Vidya({shortPeriod},{_longPeriod})";
        _shortBuffer = new CircularBuffer(shortPeriod);
        _longBuffer = new CircularBuffer(_longPeriod);
        Init();
    }

    public Vidya(object source, int shortPeriod, int longPeriod = 0, double alpha = 0.2)
        : this(shortPeriod, longPeriod, alpha)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _lastVIDYA = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _p_lastVIDYA = _lastVIDYA;
        }
        else
        {
            _lastVIDYA = _p_lastVIDYA;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _shortBuffer!.Add(Input.Value, Input.IsNew);
        _longBuffer!.Add(Input.Value, Input.IsNew);

        double vidya;
        if (_index <= _longPeriod)
        {
            vidya = _shortBuffer.Average();
        }
        else
        {
            double shortStdDev = CalculateStdDev(_shortBuffer);
            double longStdDev = CalculateStdDev(_longBuffer);
            double s = _alpha * (shortStdDev / longStdDev);
            vidya = (s * Input.Value) + ((1 - s) * _lastVIDYA);
        }

        _lastVIDYA = vidya;
        IsHot = _index >= WarmupPeriod;

        return vidya;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateStdDev(CircularBuffer buffer)
    {
        double mean = buffer.Average();
        double sumSquaredDiff = buffer.Sum(x => Math.Pow(x - mean, 2));
        return Math.Sqrt(sumSquaredDiff / buffer.Count);
    }
}