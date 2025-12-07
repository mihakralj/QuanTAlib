using Xunit;

namespace QuanTAlib.Tests;

public class TrimaTests
{
    [Fact]
    public void Trima_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Trima(0));
        Assert.Throws<ArgumentException>(() => new Trima(-1));

        var trima = new Trima(10);
        Assert.NotNull(trima);
    }

    [Fact]
    public void Trima_Calc_ReturnsValue()
    {
        var trima = new Trima(10);

        Assert.Equal(0, trima.Last.Value);

        TValue result = trima.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, trima.Last.Value);
    }

    [Fact]
    public void Trima_CalculatesCorrectAverage_Period4()
    {
        // Period 4 -> weights [1, 2, 2, 1], sum 6
        var trima = new Trima(4);

        trima.Update(new TValue(DateTime.UtcNow, 10));
        trima.Update(new TValue(DateTime.UtcNow, 20));
        trima.Update(new TValue(DateTime.UtcNow, 30));
        var r1 = trima.Update(new TValue(DateTime.UtcNow, 40));

        // (1*10 + 2*20 + 2*30 + 1*40) / 6 = 150 / 6 = 25
        Assert.Equal(25.0, r1.Value, 1e-10);

        var r2 = trima.Update(new TValue(DateTime.UtcNow, 50));
        // (1*20 + 2*30 + 2*40 + 1*50) / 6 = 210 / 6 = 35
        Assert.Equal(35.0, r2.Value, 1e-10);
    }

    [Fact]
    public void Trima_CalculatesCorrectAverage_Period5()
    {
        // Period 5 -> weights [1, 2, 3, 2, 1], sum 9
        var trima = new Trima(5);

        trima.Update(new TValue(DateTime.UtcNow, 10));
        trima.Update(new TValue(DateTime.UtcNow, 20));
        trima.Update(new TValue(DateTime.UtcNow, 30));
        trima.Update(new TValue(DateTime.UtcNow, 40));
        var r1 = trima.Update(new TValue(DateTime.UtcNow, 50));

        // (1*10 + 2*20 + 3*30 + 2*40 + 1*50) / 9 = (10 + 40 + 90 + 80 + 50) / 9 = 270 / 9 = 30
        Assert.Equal(30.0, r1.Value, 1e-10);
    }

    [Fact]
    public void Trima_IsHot_BecomesTrueWhenPeriodFilled()
    {
        var trima = new Trima(4);

        Assert.False(trima.IsHot);
        trima.Update(new TValue(DateTime.UtcNow, 10)); // 1
        Assert.False(trima.IsHot);
        trima.Update(new TValue(DateTime.UtcNow, 20)); // 2
        Assert.False(trima.IsHot);
        trima.Update(new TValue(DateTime.UtcNow, 30)); // 3
        Assert.False(trima.IsHot);
        trima.Update(new TValue(DateTime.UtcNow, 40)); // 4
        Assert.True(trima.IsHot);
    }

    [Fact]
    public void Trima_Update_IsNew_False_UpdatesValue()
    {
        var trima = new Trima(4);

        trima.Update(new TValue(DateTime.UtcNow, 10));
        trima.Update(new TValue(DateTime.UtcNow, 20));
        trima.Update(new TValue(DateTime.UtcNow, 30));
        
        // Update with 40
        double val1 = trima.Update(new TValue(DateTime.UtcNow, 40), isNew: true).Value;
        // Expected: 25 (as calculated above)
        Assert.Equal(25.0, val1, 1e-10);

        // Correct last value to 100 (was 40)
        // New window: 10, 20, 30, 100
        // Weights: 1, 2, 2, 1
        // (10 + 40 + 60 + 100) / 6 = 210 / 6 = 35
        double val2 = trima.Update(new TValue(DateTime.UtcNow, 100), isNew: false).Value;
        
        Assert.Equal(35.0, val2, 1e-10);
    }

    [Fact]
    public void Trima_Reset_ClearsState()
    {
        var trima = new Trima(5);

        trima.Update(new TValue(DateTime.UtcNow, 100));
        trima.Update(new TValue(DateTime.UtcNow, 105));

        trima.Reset();

        Assert.Equal(0, trima.Last.Value);
        Assert.False(trima.IsHot);

        // After reset, should accept new values
        trima.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, trima.Last.Value);
    }

    [Fact]
    public void Trima_NaN_Input_UsesLastValidValue()
    {
        var trima = new Trima(5);

        trima.Update(new TValue(DateTime.UtcNow, 100));
        trima.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = trima.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Trima_BatchCalc_MatchesIterativeCalc()
    {
        var trimaIterative = new Trima(10);
        var trimaBatch = new Trima(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Generate data
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        // Calculate iteratively
        var iterativeResults = new TSeries();
#pragma warning disable S4158 // Collection is known to be empty
        foreach (var item in series)
        {
            iterativeResults.Add(trimaIterative.Update(item));
        }
#pragma warning restore S4158

        // Calculate batch
        var batchResults = trimaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
#pragma warning disable S2583 // Condition always evaluates to false
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        }
#pragma warning restore S2583
    }

    [Fact]
    public void Trima_SpanCalc_MatchesTSeriesCalc()
    {
        var series = new TSeries();
        double[] source = new double[100];
        double[] output = new double[100];

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
            series.Add(bar.Time, bar.Close);
        }

        // Calculate with TSeries API
        var tseriesResult = Trima.Calculate(series, 10);

        // Calculate with Span API
        Trima.Calculate(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }
    [Fact]
    public void Trima_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // 1. Batch Mode
        var batchSeries = Trima.Calculate(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Trima.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Trima(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Trima(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }
}
