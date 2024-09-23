using System;

namespace QuanTAlib
{
    public class Min : AbstractBase
    {
        public readonly int Period;
        private CircularBuffer _buffer;
        private readonly double _halfLife;
        private double _currentMin, _p_currentMin;
        private int _timeSinceNewMin, _p_timeSinceNewMin;

        public Min(int period, double decay = 0) : base()
        {
            if (period < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
            }
            if (decay < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(decay), "Half-life must be non-negative.");
            }
            Period = period;
            WarmupPeriod = 0;
            _buffer = new CircularBuffer(period);
            _halfLife = decay * 0.1;
            Name = $"Min(period={period}, halfLife={decay:F2})";
            Init();
        }

        public Min(object source, int period, double decay = 0) : this(period, decay)
        {
            var pubEvent = source.GetType().GetEvent("Pub");
            pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
        }

        public override void Init()
        {
            base.Init();
            _currentMin = double.MaxValue;
            _timeSinceNewMin = 0;
        }

        protected override void ManageState(bool isNew)
        {
            if (isNew)
            {
                _p_currentMin = _currentMin;
                _lastValidValue = Input.Value;
                _index++;
                _timeSinceNewMin++;
                _p_timeSinceNewMin = _timeSinceNewMin;
            }
            else
            {
                _currentMin = _p_currentMin;
                _timeSinceNewMin = _p_timeSinceNewMin;
            }
        }

        protected override double Calculation()
        {
            ManageState(Input.IsNew);
            _buffer.Add(Input.Value, Input.IsNew);

            if (Input.Value <= _currentMin)
            {
                _currentMin = Input.Value;
                _timeSinceNewMin = 0;
            }

            double decayRate = 1 - Math.Exp(-_halfLife * _timeSinceNewMin / Period);
            _currentMin = _currentMin + decayRate * (_buffer.Average() - _currentMin);
            _currentMin = Math.Max(_currentMin, _buffer.Min());

            IsHot = true;
            return _currentMin;
        }
    }
}