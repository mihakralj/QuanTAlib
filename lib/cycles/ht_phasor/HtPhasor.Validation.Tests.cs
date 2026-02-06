using QuanTAlib;
using TALib;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class HtPhasorValidationTests
{
    [Fact]
    public void HtPhasor_Matches_TALib_InPhase_Quadrature()
    {
        // Arrange
        const int seed = 42;
        const int length = 600;
        var gbm = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.2, seed: seed);
        long[] times = new long[length];
        double[] prices = new double[length];
        for (int i = 0; i < length; i++)
        {
            bool isNew = true;
            var bar = gbm.Next(ref isNew);
            times[i] = bar.Time;
            prices[i] = bar.Close;
        }

        // Act
        double[] talibInPhase = new double[length];
        double[] talibQuadrature = new double[length];
        var rc = TALib.Functions.HtPhasor(prices, 0..^0, talibInPhase, talibQuadrature, out var outRange);
        Assert.Equal(TALib.Core.RetCode.Success, rc);

        var qt = new HtPhasor();
        double[] qInPhase = new double[length];
        double[] qQuadrature = new double[length];
        for (int i = 0; i < length; i++)
        {
            var result = qt.Update(new TValue(times[i], prices[i]));
            qInPhase[i] = result.Value;
            qQuadrature[i] = qt.Quadrature;
        }

        // Assert
        // TALib outputs start at outBegIdx; compare overlapping region
        int start = outRange.Start.Value;
        int outLength = outRange.End.Value - outRange.Start.Value; // End is exclusive
        const double tol = 1e-9;
        for (int i = 0; i < outLength; i++)
        {
            int srcIdx = start + i;
            Assert.InRange(qInPhase[srcIdx], talibInPhase[i] - tol, talibInPhase[i] + tol);
            Assert.InRange(qQuadrature[srcIdx], talibQuadrature[i] - tol, talibQuadrature[i] + tol);
        }
    }
}
