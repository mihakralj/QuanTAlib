using System;
using System.Runtime.CompilerServices;

namespace QuanTAlib {

public class Rma : AbstractBase {
    private readonly int _period;
    private double _alpha;
    private double _lastRMA;
    private double _savedLastRMA;

    public Rma(int period)
    {
        if (period < 1) {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        WarmupPeriod = period * 2;
        _alpha = 1.0 / _period;  // Wilder's smoothing factor
        Name = $"Rma({_period})";
        Init();
    }

    public Rma(object source, int period) : this(period) {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init() {
        base.Init();
        _lastRMA = 0;
        _savedLastRMA = 0;
    }

    protected override void ManageState(bool isNew) {
        if (isNew) {
            _savedLastRMA = _lastRMA;
            _lastValidValue = Input.Value;
            _index++;
        } else {
            _lastRMA = _savedLastRMA;
        }
    }

    protected override double Calculation() {
        ManageState(Input.IsNew);

        double rma;

        if (_index == 1) {
            rma = Input.Value;
        } else if (_index <= _period) {
            // Simple average during initial period
            rma = (_lastRMA * (_index - 1) + Input.Value) / _index;
        } else {
            // Wilder's smoothing method
            rma = _alpha * (Input.Value - _lastRMA) + _lastRMA;
        }

        _lastRMA = rma;
        IsHot = _index >= WarmupPeriod;

        return rma;
    }
}
}