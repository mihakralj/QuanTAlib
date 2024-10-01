using System;
using System.Collections.Generic;

namespace QuanTAlib
{
    public class Slope : AbstractBase
    {
        private readonly int _period;
        private readonly CircularBuffer _buffer;
        private readonly CircularBuffer _timeBuffer;

        public double? Intercept { get; private set; }
        public double? StdDev { get; private set; }
        public double? RSquared { get; private set; }
        public double? Line { get; private set; }

        public Slope(int period)
        {
            if (period <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(period), period,
                    "Period must be greater than 1 for Slope/Linear Regression.");
            }
            _period = period;
            WarmupPeriod = period;
            _buffer = new CircularBuffer(period);
            _timeBuffer = new CircularBuffer(period);
            Name = $"Slope(period={period})";

            Init();
        }

        public Slope(object source, int period) : this(period)
        {
            var pubEvent = source.GetType().GetEvent("Pub");
            pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
        }

        public override void Init()
        {
            base.Init();
            _buffer.Clear();
            _timeBuffer.Clear();
            Intercept = null;
            StdDev = null;
            RSquared = null;
            Line = null;
        }

        protected override void ManageState(bool isNew)
        {
            if (isNew)
            {
                _lastValidValue = Input.Value;
                _index++;
            }
        }

        protected override double Calculation()
        {
            ManageState(Input.IsNew);

            _buffer.Add(Input.Value, Input.IsNew);
            _timeBuffer.Add(Input.Time.Ticks, Input.IsNew);

            double slope = 0;

            if (_buffer.Count < 2)
            {
                return slope; // Return 0 when there are fewer than 2 points
            }

            int count = Math.Min(_buffer.Count, _period);
            var values = _buffer.GetSpan().ToArray();

            // Calculate averages
            double sumX = 0, sumY = 0;
            for (int i = 0; i < count; i++)
            {
                sumX += i + 1;
                sumY += values[i];
            }
            double avgX = sumX / count;
            double avgY = sumY / count;

            // Least squares method
            double sumSqX = 0, sumSqY = 0, sumSqXY = 0;
            for (int i = 0; i < count; i++)
            {
                double devX = (i + 1) - avgX;
                double devY = values[i] - avgY;
                sumSqX += devX * devX;
                sumSqY += devY * devY;
                sumSqXY += devX * devY;
            }

            if (sumSqX > 0)
            {
                slope = sumSqXY / sumSqX;
                Intercept = avgY - (slope * avgX);

                // Calculate Standard Deviation and R-Squared
                double stdDevX = Math.Sqrt(sumSqX / count);
                double stdDevY = Math.Sqrt(sumSqY / count);
                StdDev = stdDevY;

                if (stdDevX * stdDevY != 0)
                {
                    double r = sumSqXY / (stdDevX * stdDevY) / count;
                    RSquared = r * r;
                }

                // Calculate last Line value (y = mx + b)
                Line = (slope * count) + Intercept;
            }
            else
            {
                Intercept = null;
                StdDev = null;
                RSquared = null;
                Line = null;
            }

            IsHot = _buffer.Count == _period;
            return slope;
        }
    }
}