using System;
using System.Collections.Generic;

namespace QuanTAlib
{
    public class Curvature : AbstractBase
    {
        private readonly int _period;
        private readonly Slope _slopeCalculator;
        private readonly CircularBuffer _slopeBuffer;

        public double? Intercept { get; private set; }
        public double? StdDev { get; private set; }
        public double? RSquared { get; private set; }
        public double? Line { get; private set; }

        public Curvature(int period)
        {
            if (period <= 2)
            {
                throw new ArgumentOutOfRangeException(nameof(period), period,
                    "Period must be greater than 2 for Curvature calculation.");
            }
            _period = period;
            WarmupPeriod = period * 2 - 1; // We need this many points to get period number of slopes
            _slopeCalculator = new Slope(period);
            _slopeBuffer = new CircularBuffer(period);
            Name = $"Curvature(period={period})";

            Init();
        }

        public Curvature(object source, int period) : this(period)
        {
            var pubEvent = source.GetType().GetEvent("Pub");
            pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
        }

        public override void Init()
        {
            base.Init();
            _slopeBuffer.Clear();
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

            // Calculate slope
            var slopeResult = _slopeCalculator.Calc(Input);
            _slopeBuffer.Add(slopeResult.Value, Input.IsNew);

            double curvature = 0;

            if (_slopeBuffer.Count < 2)
            {
                return curvature; // Return 0 when there are fewer than 2 slope points
            }

            int count = Math.Min(_slopeBuffer.Count, _period);
            var slopes = _slopeBuffer.GetSpan().ToArray();

            // Calculate averages
            double sumX = 0, sumY = 0;
            for (int i = 0; i < count; i++)
            {
                sumX += i + 1;
                sumY += slopes[i];
            }
            double avgX = sumX / count;
            double avgY = sumY / count;

            // Least squares method
            double sumSqX = 0, sumSqY = 0, sumSqXY = 0;
            for (int i = 0; i < count; i++)
            {
                double devX = (i + 1) - avgX;
                double devY = slopes[i] - avgY;
                sumSqX += devX * devX;
                sumSqY += devY * devY;
                sumSqXY += devX * devY;
            }

            if (sumSqX > 0)
            {
                curvature = sumSqXY / sumSqX;
                Intercept = avgY - (curvature * avgX);

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
                Line = (curvature * count) + Intercept;
            }
            else
            {
                Intercept = null;
                StdDev = null;
                RSquared = null;
                Line = null;
            }

            IsHot = _slopeBuffer.Count == _period;
            return curvature;
        }
    }
}