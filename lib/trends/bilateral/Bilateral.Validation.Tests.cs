using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class BilateralValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public BilateralValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _testData.Dispose();
        }
    }

    [Fact]
    public void Validate_Reference_Batch()
    {
        int[] periods = { 5, 10, 20, 50 };
        double sigmaSRatio = 0.5;
        double sigmaRMult = 1.0;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bilateral (batch TSeries)
            var bilateral = new global::QuanTAlib.Bilateral(period, sigmaSRatio, sigmaRMult);
            var qResult = bilateral.Update(_testData.Data);

            // Calculate Reference Bilateral
            var refResult = GetReferenceData(period, sigmaSRatio, sigmaRMult);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, refResult, (s) => s, 100, 1e-8);
        }
        _output.WriteLine("Bilateral Batch(TSeries) validated successfully against Reference");
    }

    [Fact]
    public void Validate_Reference_Streaming()
    {
        int[] periods = { 5, 10, 20, 50 };
        double sigmaSRatio = 0.5;
        double sigmaRMult = 1.0;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bilateral (streaming)
            var bilateral = new global::QuanTAlib.Bilateral(period, sigmaSRatio, sigmaRMult);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(bilateral.Update(item).Value);
            }

            // Calculate Reference Bilateral
            var refResult = GetReferenceData(period, sigmaSRatio, sigmaRMult);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, refResult, (s) => s, 100, 1e-8);
        }
        _output.WriteLine("Bilateral Streaming validated successfully against Reference");
    }

    [Fact]
    public void Validate_Reference_Span()
    {
        int[] periods = { 5, 10, 20, 50 };
        double sigmaSRatio = 0.5;
        double sigmaRMult = 1.0;

        // Prepare data for Span API
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bilateral (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Bilateral.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period, sigmaSRatio, sigmaRMult);

            // Calculate Reference Bilateral
            var refResult = GetReferenceData(period, sigmaSRatio, sigmaRMult);

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, refResult, (s) => s, 100, 1e-8);
        }
        _output.WriteLine("Bilateral Span validated successfully against Reference");
    }

    private List<double> GetReferenceData(int period, double sigmaSRatio, double sigmaRMult)
    {
        var reference = new BilateralReference(period, sigmaSRatio, sigmaRMult);
        var results = new List<double>();
        
        foreach (var item in _testData.Data)
        {
            results.Add(reference.Update(item.Value));
        }
        
        return results;
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

            double sigmaS = Math.Max(_length * _sigmaSRatio, 1e-10);
            
            // Calculate StDev of current window
            double stdev = CalculateStDev(_history);
            double sigmaR = Math.Max(stdev * _sigmaRMult, 1e-10);

            double sumWeights = 0.0;
            double sumWeightedSrc = 0.0;
            double centerVal = _history[_history.Count - 1]; // Newest value

            // Iterate through history
            // i=0 is newest (index Count-1)
            int loopLen = _history.Count;
            
            for (int i = 0; i < loopLen; i++)
            {
                double valI = _history[_history.Count - 1 - i];
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
            // Population StDev to match implementation
            return Math.Sqrt(sumSqDiff / values.Count);
        }
    }
}
