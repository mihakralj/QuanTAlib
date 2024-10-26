using System;
using System.Runtime.CompilerServices;

namespace QuanTAlib
{
    public class Zlema : AbstractBase
    {
        private readonly CircularBuffer _buffer;
        private readonly int _lag;
        private readonly Ema _ema;
        private double _lastZLEMA, _p_lastZLEMA;

        public Zlema(int period)
        {
            if (period < 1)
            {
                throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
            }
            WarmupPeriod = period;
            _lag = (int)(0.5 * (period - 1));
            _buffer = new CircularBuffer(_lag + 1);
            _ema = new Ema(period, useSma: false);
            Name = $"Zlema({period})";
            Init();
        }

        public Zlema(object source, int period) : this(period)
        {
            var pubEvent = source.GetType().GetEvent("Pub");
            pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
        }

        public override void Init()
        {
            base.Init();
            _buffer.Clear();
            _ema.Init();
            _lastZLEMA = 0;
            _p_lastZLEMA = 0;
        }

        protected override void ManageState(bool isNew)
        {
            if (isNew)
            {
                _lastValidValue = Input.Value;
                _index++;
                _p_lastZLEMA = _lastZLEMA;
            }
            else
            {
                _lastZLEMA = _p_lastZLEMA;
            }
        }

        protected override double Calculation()
        {
            ManageState(Input.IsNew);

            _buffer.Add(Input.Value, Input.IsNew);

            double lagValue = _buffer[Math.Max(0, _buffer.Count - 1 - _lag)];
            double errorCorrection = 2 * Input.Value - lagValue;
            double zlema = _ema.Calc(new TValue(errorCorrection, Input.IsNew)).Value;

            _lastZLEMA = zlema;
            IsHot = _index >= WarmupPeriod;

            return zlema;
        }
    }
}
