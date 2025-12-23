using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QuanTAlib;

public class BilateralValidationTests
{
    [Fact]
    public void MatchesReferenceImplementation()
    {
        int period = 10;
        double sigmaSRatio = 0.5;
        double sigmaRMult = 1.0;
        
        var indicator = new Bilateral(period, sigmaSRatio, sigmaRMult);
        var reference = new BilateralReference(period, sigmaSRatio, sigmaRMult);
        
        var random = new Random(123);
        var data = new List<double>();
        
        for (int i = 0; i < 100; i++)
        {
            double price = 100 + Math.Sin(i * 0.1) * 10 + random.NextDouble() * 5;
            data.Add(price);
            
            var tValue = new TValue(DateTime.UtcNow, price);
            var actual = indicator.Update(tValue);
            var expected = reference.Update(price);
            
            Assert.Equal(expected, actual.Value, 8);
        }
    }

    private class BilateralReference
    {
        private readonly int _length;
        private readonly double _sigmaSRatio;
        private readonly double _sigmaRMult;
        private readonly List<double> _history = new();

        public BilateralReference(int length, double sigmaSRatio, double sigmaRMult)
        {
            _length = length;
            _sigmaSRatio = sigmaSRatio;
            _sigmaRMult = sigmaRMult;
        }

        public double Update(double val)
        {
            _history.Add(val);
            if (_history.Count > _length)
            {
                _history.RemoveAt(0);
            }

            if (_history.Count == 0) return double.NaN;

            // PineScript: src is the series. src[0] is newest.
            // _history: last element is newest.
            // So src[i] corresponds to _history[_history.Count - 1 - i]

            double sigmaS = Math.Max(_length * _sigmaSRatio, 1e-10);
            
            // Calculate StDev of current window
            double stdev = CalculateStDev(_history);
            double sigmaR = Math.Max(stdev * _sigmaRMult, 1e-10);

            double sumWeights = 0.0;
            double sumWeightedSrc = 0.0;
            double centerVal = _history[_history.Count - 1]; // src[0]

            // PineScript: for i = 0 to length - 1
            // If history is shorter than length, we iterate up to history count
            int loopLen = _history.Count; // PineScript usually handles shorter history by returning NaN or partial? 
            // The snippet assumes src has length.
            // We will iterate available history.
            
            for (int i = 0; i < loopLen; i++)
            {
                double valI = _history[_history.Count - 1 - i]; // src[i]
                double diffSpatial = i;
                double diffRange = centerVal - valI;
                
                double weightSpatial = Math.Exp(-(diffSpatial * diffSpatial) / (2.0 * sigmaS * sigmaS));
                double weightRange = Math.Exp(-(diffRange * diffRange) / (2.0 * sigmaR * sigmaR));
                
                double weight = weightSpatial * weightRange;
                
                sumWeights += weight;
                sumWeightedSrc += weight * valI;
            }

            return sumWeights == 0.0 ? centerVal : sumWeightedSrc / sumWeights;
        }

        private static double CalculateStDev(List<double> values)
        {
            if (values.Count < 2) return 0;
            
            double avg = values.Average();
            double sumSqDiff = values.Sum(d => (d - avg) * (d - avg));
            // PineScript stdev is population? Or sample?
            // "ta.stdev" is population standard deviation (biased).
            return Math.Sqrt(sumSqDiff / values.Count);
        }
    }
}
