using Xunit;

namespace QuanTAlib.Tests;

public class LinRegTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new LinReg(0));
        Assert.Throws<ArgumentException>(() => new LinReg(-1));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var linreg = new LinReg(10);
        var result = linreg.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var linreg = new LinReg(5);
        for (int i = 0; i < 5; i++)
        {
            linreg.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(4, linreg.Last.Value); // Linear 0,1,2,3,4 -> LinReg at 4 is 4
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var linreg = new LinReg(5);
        for (int i = 0; i < 5; i++)
        {
            linreg.Update(new TValue(DateTime.UtcNow, i));
        }
        // Last value is 4.
        // Update with isNew=false to 5.
        // Series becomes 0,1,2,3,5.
        // Regression line will change.
        linreg.Update(new TValue(DateTime.UtcNow, 5), isNew: false);
        Assert.NotEqual(4, linreg.Last.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var linreg = new LinReg(5);
        linreg.Update(new TValue(DateTime.UtcNow, 10));
        linreg.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(10, linreg.Last.Value);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = LinReg.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        LinReg.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new LinReg(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new LinReg(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        Assert.Equal(expected, spanResult, precision: 8);
        Assert.Equal(expected, streamingResult, precision: 8);
        Assert.Equal(expected, eventingResult, precision: 8);
    }

    [Fact]
    public void Slope_Intercept_RSquared_Calculated()
    {
        // Perfect linear series: 0, 1, 2, 3, 4
        // y = 1*x + 0 (if x starts at 0 and increases)
        // In LinReg, x=0 is current (4), x=4 is oldest (0).
        // So points are (0,4), (1,3), (2,2), (3,1), (4,0).
        // y = -1*x + 4.
        // Slope should be -(-1) = 1 (since we inverted slope in implementation to match time direction?)
        // Wait, implementation says: Slope = -m.
        // m for (0,4)...(4,0) is -1.
        // So Slope = 1.
        // Intercept (at x=0) is 4.
        // RSquared should be 1.

        var linreg = new LinReg(5);
        for (int i = 0; i < 5; i++)
        {
            linreg.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.Equal(1.0, linreg.Slope, precision: 6);
        Assert.Equal(4.0, linreg.Intercept, precision: 6);
        Assert.Equal(1.0, linreg.RSquared, precision: 6);
    }
}
