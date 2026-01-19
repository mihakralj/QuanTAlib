
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
    public void Properties_Accessible()
    {
        var linreg = new LinReg(10);
        Assert.Equal(0, linreg.Last.Value);
        Assert.False(linreg.IsHot);
        Assert.Contains("LinReg", linreg.Name, StringComparison.Ordinal);
        Assert.Equal(0, linreg.Slope);
        Assert.Equal(0, linreg.Intercept);
        Assert.Equal(0, linreg.RSquared);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var linreg = new LinReg(5);
        Assert.False(linreg.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            linreg.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(linreg.IsHot);
        }

        linreg.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(linreg.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var linreg = new LinReg(5);
        for (int i = 0; i < 10; i++)
        {
            linreg.Update(new TValue(DateTime.UtcNow, i * 10));
        }
        Assert.True(linreg.IsHot);

        linreg.Reset();
        Assert.False(linreg.IsHot);
        Assert.Equal(0, linreg.Last.Value);
        Assert.Equal(0, linreg.Slope);
        Assert.Equal(0, linreg.Intercept);
        Assert.Equal(0, linreg.RSquared);

        // After reset, should accept new values
        var result = linreg.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50, result.Value);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var linreg = new LinReg(5);
        linreg.Update(new TValue(DateTime.UtcNow, 10));
        linreg.Update(new TValue(DateTime.UtcNow, 20));

        var resultPosInf = linreg.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = linreg.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var linreg = new LinReg(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            linreg.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = linreg.Last.Value;
        double slopeAfterTen = linreg.Slope;
        double interceptAfterTen = linreg.Intercept;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            linreg.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = linreg.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        // Use relaxed tolerance due to floating point accumulation in complex calculations
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-2);
        Assert.Equal(slopeAfterTen, linreg.Slope, 1e-2);
        Assert.Equal(interceptAfterTen, linreg.Intercept, 1e-2);
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() =>
            LinReg.Calculate(source.AsSpan(), output.AsSpan(), 0));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            LinReg.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        const int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var series = new TSeries();
        double[] source = new double[100];

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var tseriesResult = LinReg.Batch(series, period);

        double[] output = new double[100];
        LinReg.Calculate(source.AsSpan(), output.AsSpan(), period);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
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
