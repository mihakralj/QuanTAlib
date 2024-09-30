using System;

namespace QuanTAlib
{
    public class Max : AbstractBase
    {
        private readonly int Period;
        private readonly CircularBuffer _buffer;
        private readonly double _halfLife;
        private double _currentMax, _p_currentMax;
        private int _timeSinceNewMax, _p_timeSinceNewMax;

        public Max(int period, double decay = 0)
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
            Name = $"Max(period={period}, halfLife={decay:F2})";
            Init();
        }

        public Max(object source, int period, double decay = 0) : this(period, decay)
        {
            var pubEvent = source.GetType().GetEvent("Pub");
            pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
        }

        public override void Init()
        {
            base.Init();
            _currentMax = double.MinValue;
            _timeSinceNewMax = 0;
        }

        protected override void ManageState(bool isNew)
        {
            if (isNew)
            {
                _p_currentMax = _currentMax;
                _lastValidValue = Input.Value;
                _index++;
                _timeSinceNewMax++;
                _p_timeSinceNewMax = _timeSinceNewMax;
            }
            else
            {
                _currentMax = _p_currentMax;
                _timeSinceNewMax = _p_timeSinceNewMax;
            }
        }

        protected override double Calculation()
        {
            ManageState(Input.IsNew);
            _buffer.Add(Input.Value, Input.IsNew);

            if (Input.Value >= _currentMax)
            {
                _currentMax = Input.Value;
                _timeSinceNewMax = 0;
            }

            double decayRate = 1 - Math.Exp(-_halfLife * _timeSinceNewMax / Period);
            _currentMax = _currentMax - decayRate * (_currentMax - _buffer.Average());
            _currentMax = Math.Min(_currentMax, _buffer.Max());

            IsHot = true;
            return _currentMax;
        }
    }
}