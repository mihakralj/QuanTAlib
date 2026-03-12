
namespace QuanTAlib.Tests;

public class MavpTests
{
    [Fact]
    public void Mavp_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mavp(minPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Mavp(minPeriod: 5, maxPeriod: 3));

        var mavp = new Mavp(2, 30);
        Assert.NotNull(mavp);
        Assert.Equal(2, mavp.MinPeriod);
        Assert.Equal(30, mavp.MaxPeriod);
    }

    [Fact]
    public void Mavp_Calc_ReturnsValue()
    {
        var mavp = new Mavp(2, 30);
        mavp.Period = 10;
        TValue result = mavp.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Mavp_IsHot_BecomesTrueAfterWarmup()
    {
        var mavp = new Mavp(2, 30);
        mavp.Period = 10;

        Assert.False(mavp.IsHot);

        // Feed enough data points for warmup compensator to converge
        // With period=10, alpha=2/11≈0.182, beta≈0.818
        // E = 0.818^n; E <= 0.05 when n >= log(0.05)/log(0.818) ≈ 15
        for (int i = 0; i < 20; i++)
        {
            mavp.Update(new TValue(DateTime.UtcNow, 100));
        }

        Assert.True(mavp.IsHot);
    }

    [Fact]
    public void Mavp_StreamingMatchesBatch_FixedPeriod()
    {
        var mavpStreaming = new Mavp(2, 30);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        // Streaming with fixed period
        var streamingResults = new TSeries();
        mavpStreaming.Period = 10;
        foreach (var item in series)
        {
            streamingResults.Add(mavpStreaming.Update(item));
        }

        // Batch with fixed period
        var mavpBatch = new Mavp(2, 30);
        mavpBatch.Period = 10;
        var batchResults = mavpBatch.Update(series);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        foreach (var (stream, batch) in streamingResults.Zip(batchResults))
        {
            Assert.Equal(stream.Value, batch.Value, 1e-9);
        }
    }

    [Fact]
    public void Mavp_StreamingMatchesBatch_VariablePeriod()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        var periodSeries = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
            // Variable period: oscillate between 5 and 20
            double p = 5 + (15.0 * (0.5 + (0.5 * Math.Sin(i * 0.1))));
            periodSeries.Add(bar.Time, p);
        }

        // Streaming
        var mavpStreaming = new Mavp(2, 30);
        var streamingResults = new TSeries();
        for (int i = 0; i < series.Count; i++)
        {
            mavpStreaming.Period = periodSeries[i].Value;
            streamingResults.Add(mavpStreaming.Update(series[i]));
        }

        // Batch
        var batchResults = Mavp.Batch(series, periodSeries, 2, 30);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        foreach (var (stream, batch) in streamingResults.Zip(batchResults))
        {
            Assert.Equal(stream.Value, batch.Value, 1e-9);
        }
    }

    [Fact]
    public void Mavp_SpanCalc_MatchesInstance()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        var periodSeries = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
            periodSeries.Add(bar.Time, 10.0);
        }

        var instanceResults = Mavp.Batch(series, periodSeries, 2, 30);
        var staticOutput = new double[series.Count];
        var periodsArray = periodSeries.Values.ToArray();
        Mavp.Batch(series.Values.ToArray().AsSpan(), periodsArray.AsSpan(), staticOutput.AsSpan(), 2, 30);

        for (int i = 0; i < instanceResults.Count; i++)
        {
            Assert.Equal(instanceResults[i].Value, staticOutput[i], 1e-9);
        }
    }

    [Fact]
    public void Mavp_Update_IsNewFalse_CorrectsValue()
    {
        var mavp = new Mavp(2, 30);
        mavp.Period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed initial data
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            mavp.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Commit a new bar
        var newBar = gbm.Next(isNew: true);
        mavp.Update(new TValue(newBar.Time, newBar.Close), isNew: true);
        double valueAfterCommit = mavp.Last.Value;

        // Correct with a different value
        mavp.Update(new TValue(newBar.Time, newBar.Close + 10.0), isNew: false);
        double valueAfterCorrection = mavp.Last.Value;

        Assert.NotEqual(valueAfterCommit, valueAfterCorrection);

        // Restore original value
        mavp.Update(new TValue(newBar.Time, newBar.Close), isNew: false);
        Assert.Equal(valueAfterCommit, mavp.Last.Value, 1e-9);
    }

    [Fact]
    public void Mavp_NaN_Input_UsesLastValidValue()
    {
        var mavp = new Mavp(2, 30);
        mavp.Period = 10;

        mavp.Update(new TValue(DateTime.UtcNow, 100));
        mavp.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = mavp.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Mavp_Infinity_Input_UsesLastValidValue()
    {
        var mavp = new Mavp(2, 30);
        mavp.Period = 5;

        mavp.Update(new TValue(DateTime.UtcNow, 100));
        var resultAfterInf = mavp.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));

        Assert.True(double.IsFinite(resultAfterInf.Value));
    }

    [Fact]
    public void Mavp_Reset_ClearsState()
    {
        var mavp = new Mavp(2, 30);
        mavp.Period = 10;
        mavp.Update(new TValue(DateTime.UtcNow, 100));
        mavp.Update(new TValue(DateTime.UtcNow, 110));

        Assert.True(mavp.Last.Value > 0);

        mavp.Reset();

        Assert.Equal(0, mavp.Last.Value);
        Assert.False(mavp.IsHot);
    }

    [Fact]
    public void Mavp_FlatLine_ReturnsSameValue()
    {
        var mavp = new Mavp(2, 30);
        mavp.Period = 10;
        for (int i = 0; i < 50; i++)
        {
            mavp.Update(new TValue(DateTime.UtcNow, 100));
        }

        Assert.Equal(100, mavp.Last.Value, 1e-6);
    }

    [Fact]
    public void Mavp_IterativeCorrections_RestoreToOriginalState()
    {
        var mavp = new Mavp(2, 30);
        mavp.Period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue lastInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            mavp.Update(lastInput, isNew: true);
        }

        double valueAfter = mavp.Last.Value;

        // Generate 5 corrections with isNew=false
        for (int i = 0; i < 5; i++)
        {
            var bar = gbm.Next(isNew: false);
            mavp.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed remembered last input again
        TValue finalValue = mavp.Update(lastInput, isNew: false);

        Assert.Equal(valueAfter, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Mavp_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] periods = [10, 10, 10, 10, 10];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];
        double[] wrongSizePeriods = new double[3];

        Assert.Throws<ArgumentException>(() => Mavp.Batch(source.AsSpan(), periods.AsSpan(), wrongSizeOutput.AsSpan(), 2, 30));
        Assert.Throws<ArgumentException>(() => Mavp.Batch(source.AsSpan(), wrongSizePeriods.AsSpan(), output.AsSpan(), 2, 30));
        Assert.Throws<ArgumentException>(() => Mavp.Batch(source.AsSpan(), periods.AsSpan(), output.AsSpan(), 0, 30));
        Assert.Throws<ArgumentException>(() => Mavp.Batch(source.AsSpan(), periods.AsSpan(), output.AsSpan(), 10, 5));
    }

    [Fact]
    public void Mavp_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] periods = [10, 10, 10, 10, 10];
        double[] output = new double[5];

        Mavp.Batch(source.AsSpan(), periods.AsSpan(), output.AsSpan(), 2, 30);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void Mavp_PeriodClamp_RespectsMinMax()
    {
        var mavp = new Mavp(5, 20);

        // Period below minimum
        mavp.Period = 1;
        mavp.Update(new TValue(DateTime.UtcNow, 100));
        // Should not crash; period is clamped to 5

        // Period above maximum
        mavp.Period = 100;
        mavp.Update(new TValue(DateTime.UtcNow, 110));
        // Should not crash; period is clamped to 20

        Assert.True(double.IsFinite(mavp.Last.Value));
    }

    [Fact]
    public void Mavp_VariablePeriod_ProducesDifferentResults()
    {
        // Fixed period=10
        var mavpFixed = new Mavp(2, 30);
        mavpFixed.Period = 10;

        // Variable periods
        var mavpVar = new Mavp(2, 30);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        double lastFixed = 0;
        double lastVar = 0;

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);

            mavpFixed.Period = 10;
            lastFixed = mavpFixed.Update(tv).Value;

            // Alternate between fast and slow periods
            mavpVar.Period = (i % 2 == 0) ? 3 : 25;
            lastVar = mavpVar.Update(tv).Value;
        }

        Assert.NotEqual(lastFixed, lastVar);
    }

    [Fact]
    public void Mavp_WithPeriodOverload_MatchesPeriodProperty()
    {
        var mavp1 = new Mavp(2, 30);
        var mavp2 = new Mavp(2, 30);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);
            double period = 5 + (15.0 * (0.5 + (0.5 * Math.Sin(i * 0.1))));

            // Method 1: Set period, then call Update
            mavp1.Period = period;
            double v1 = mavp1.Update(tv).Value;

            // Method 2: Use overload
            double v2 = mavp2.Update(tv, period).Value;

            Assert.Equal(v1, v2, 1e-12);
        }
    }

    [Fact]
    public void Mavp_AllModes_ProduceSameResult()
    {
        const double fixedPeriod = 10.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Build period series
        var periodSeries = new TSeries();
        foreach (var item in series)
        {
            periodSeries.Add(item.Time, fixedPeriod);
        }

        // 1. Batch Mode (TSeries with periods)
        var batchSeries = Mavp.Batch(series, periodSeries, 2, 30);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var pValues = periodSeries.Values.ToArray();
        var spanOutput = new double[tValues.Length];
        Mavp.Batch(new ReadOnlySpan<double>(tValues), new ReadOnlySpan<double>(pValues), spanOutput, 2, 30);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Mavp(2, 30);
        streamingInd.Period = fixedPeriod;
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Mavp(pubSource, 2, 30);
        eventingInd.Period = fixedPeriod;
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

    [Fact]
    public void Mavp_FixedPeriodSpan_MatchesVariablePeriodSpan()
    {
        double[] source = [100, 105, 110, 108, 112, 115, 113, 118, 120, 117];
        double[] outputFixed = new double[source.Length];
        double[] outputVar = new double[source.Length];
        double[] periods = new double[source.Length];
        Array.Fill(periods, 5.0);

        Mavp.Batch(source.AsSpan(), outputFixed.AsSpan(), 5.0, 2, 30);
        Mavp.Batch(source.AsSpan(), periods.AsSpan(), outputVar.AsSpan(), 2, 30);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(outputFixed[i], outputVar[i], 1e-12);
        }
    }
}
