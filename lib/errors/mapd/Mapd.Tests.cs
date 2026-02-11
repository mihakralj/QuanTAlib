namespace QuanTAlib.Tests;

public class MapdTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mapd(0));
        Assert.Throws<ArgumentException>(() => new Mapd(-1));

        var mapd = new Mapd(10);
        Assert.NotNull(mapd);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var mapd = new Mapd(10);

        Assert.Equal(0, mapd.Last.Value);
        Assert.False(mapd.IsHot);
        Assert.Contains("Mapd", mapd.Name, StringComparison.Ordinal);

        mapd.Update(100, 105);
        Assert.NotEqual(0, mapd.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var mapd = new Mapd(period);

        for (int i = 0; i < period - 1; i++)
        {
            Assert.False(mapd.IsHot, $"IsHot should be false at index {i}");
            mapd.Update(100 + i, 105 + i);
        }

        mapd.Update(104, 109);
        Assert.True(mapd.IsHot, "IsHot should be true after period updates");
    }

    [Fact]
    public void Mapd_CalculatesCorrectly()
    {
        var mapd = new Mapd(3);

        // |100 - 110| / 110 * 100 = 9.0909...%
        var res1 = mapd.Update(100, 110);
        Assert.Equal(100.0 * 10.0 / 110.0, res1.Value, 10);

        // |200 - 220| / 220 * 100 = 9.0909...%, Mean = same
        var res2 = mapd.Update(200, 220);
        Assert.Equal(100.0 * 10.0 / 110.0, res2.Value, 10);

        // |50 - 60| / 60 * 100 = 16.666...%
        var res3 = mapd.Update(50, 60);
        double expected = (100.0 * 10 / 110 + 100.0 * 20 / 220 + 100.0 * 10 / 60) / 3;
        Assert.Equal(expected, res3.Value, 10);
    }

    [Fact]
    public void Mapd_PerfectPrediction_ReturnsZero()
    {
        var mapd = new Mapd(5);

        for (int i = 1; i <= 10; i++)
        {
            mapd.Update(i * 10, i * 10); // Perfect prediction
        }

        Assert.Equal(0.0, mapd.Last.Value, 10);
    }

    [Fact]
    public void Mapd_DividesbyPredicted_NotActual()
    {
        var mape = new Mape(1);
        var mapd = new Mapd(1);

        // actual=100, predicted=200
        mape.Update(100, 200);
        mapd.Update(100, 200);

        // MAPE: |100-200|/100 * 100 = 100%
        // MAPD: |100-200|/200 * 100 = 50%
        Assert.Equal(100.0, mape.Last.Value, 10);
        Assert.Equal(50.0, mapd.Last.Value, 10);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var mapd = new Mapd(10);

        mapd.Update(100, 110, isNew: true);
        double value1 = mapd.Last.Value;

        mapd.Update(100, 120, isNew: true);
        double value2 = mapd.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var mapd = new Mapd(10);

        mapd.Update(100, 110);
        mapd.Update(100, 120, isNew: true);
        double beforeUpdate = mapd.Last.Value;

        mapd.Update(100, 130, isNew: false);
        double afterUpdate = mapd.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var mapd = new Mapd(5);

        double tenthActual = 0;
        double tenthPredicted = 0;

        // Feed 10 updates
        for (int i = 1; i <= 10; i++)
        {
            tenthActual = i * 10;
            tenthPredicted = i * 10 + 5;
            mapd.Update(tenthActual, tenthPredicted);
        }

        double stateAfterTen = mapd.Last.Value;

        // Apply 5 corrections with isNew=false
        for (int i = 0; i < 5; i++)
        {
            mapd.Update(100 + i, 200 + i, isNew: false);
        }

        // Restore to original values
        mapd.Update(tenthActual, tenthPredicted, isNew: false);

        Assert.Equal(stateAfterTen, mapd.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mapd = new Mapd(5);

        for (int i = 1; i <= 10; i++)
        {
            mapd.Update(i * 10, i * 10 + 5);
        }

        Assert.True(mapd.IsHot);

        mapd.Reset();

        Assert.False(mapd.IsHot);
        Assert.Equal(0, mapd.Last.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var mapd = new Mapd(5);

        mapd.Update(100, 110);
        mapd.Update(110, 120);
        mapd.Update(120, 130);

        var result = mapd.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var mapd = new Mapd(5);

        mapd.Update(100, 110);
        mapd.Update(110, 120);

        var result = mapd.Update(double.PositiveInfinity, double.NegativeInfinity);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var mapd = new Mapd(5);

        mapd.Update(100, 110);
        mapd.Update(110, 120);
        mapd.Update(120, 130);

        var r1 = mapd.Update(double.NaN, double.NaN);
        var r2 = mapd.Update(double.NaN, double.NaN);
        var r3 = mapd.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Mapd_Throws_On_Single_Input()
    {
        var mapd = new Mapd(10);
        Assert.Throws<NotSupportedException>(() => mapd.Update(new TValue(DateTime.UtcNow, 1)));
        Assert.Throws<NotSupportedException>(() => mapd.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => mapd.Prime([1, 2, 3]));
    }

    [Fact]
    public void BatchSpan_MatchesStreaming()
    {
        int period = 5;
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        double[] actual = new double[count];
        double[] predicted = new double[count];
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            actual[i] = bar.Close;
            predicted[i] = bar.Close * 1.05 + 2; // Offset prediction
        }

        // Streaming
        var mapd = new Mapd(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = mapd.Update(actual[i], predicted[i]).Value;
        }

        // Batch
        double[] batchResults = new double[count];
        Mapd.Batch(actual, predicted, batchResults, period);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i], 9);
        }
    }

    [Fact]
    public void BatchSpan_ValidatesInput()
    {
        double[] actual = [1, 2, 3, 4, 5];
        double[] predicted = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];
        double[] wrongSizePredicted = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() =>
            Mapd.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Mapd.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            Mapd.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));

        // Predicted must be same length as actual
        Assert.Throws<ArgumentException>(() =>
            Mapd.Batch(actual.AsSpan(), wrongSizePredicted.AsSpan(), output.AsSpan(), 3));
    }

    [Fact]
    public void Calculate_Works()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 1; i <= 10; i++)
        {
            actual.Add(now.AddMinutes(i), 100);
            predicted.Add(now.AddMinutes(i), 110);
        }

        var results = Mapd.Batch(actual, predicted, 3);

        Assert.Equal(10, results.Count);
        // |100-110|/110 * 100 = 9.0909...%
        Assert.Equal(100.0 * 10 / 110, results.Last.Value, 10);
    }

    [Fact]
    public void Calculate_ValidatesMismatchedLengths()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        for (int i = 0; i < 10; i++)
        {
            actual.Add(DateTime.UtcNow, i + 1);
        }

        for (int i = 0; i < 5; i++)
        {
            predicted.Add(DateTime.UtcNow, i + 1);
        }

        Assert.Throws<ArgumentException>(() => Mapd.Batch(actual, predicted, 3));
    }

    [Fact]
    public void BatchSpan_HandlesNaN()
    {
        double[] actual = [100, 110, double.NaN, 130, 140];
        double[] predicted = [105, 115, 125, double.NaN, 145];
        double[] output = new double[5];

        Mapd.Batch(actual, predicted, output, 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Mapd_Resync_Works()
    {
        var mapd = new Mapd(5);

        // Force many updates to trigger resync (ResyncInterval = 1000)
        for (int i = 0; i < 1100; i++)
        {
            mapd.Update(100, 110);
        }

        // |100-110|/110 * 100 = 9.0909...%
        Assert.Equal(100.0 * 10 / 110, mapd.Last.Value, 10);
    }

    [Fact]
    public void Mapd_ZeroPredicted_HandlesGracefully()
    {
        var mapd = new Mapd(3);

        mapd.Update(100, 110);
        mapd.Update(100, 110);

        // Zero predicted should not cause division by zero
        var result = mapd.Update(10, 0);
        Assert.True(double.IsFinite(result.Value));
    }
}
