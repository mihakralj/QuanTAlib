
namespace QuanTAlib;

public class MamaTests
{
    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Mama(fastLimit: 0.05, slowLimit: 0.5)); // fast < slow
        Assert.Throws<ArgumentException>(() => new Mama(fastLimit: 0.5, slowLimit: -0.1)); // slow < 0
        Assert.Throws<ArgumentException>(() => new Mama(fastLimit: 0.0, slowLimit: 0.05)); // fast <= 0
    }

    [Fact]
    public void Update_ValidInput_CalculatesMamaAndFama()
    {
        var mama = new Mama(fastLimit: 0.5, slowLimit: 0.05);
        var input = new TValue(DateTime.UtcNow, 100.0);

        var result = mama.Update(input);

        Assert.Equal(100.0, result.Value); // First value should be price
        Assert.Equal(100.0, mama.Fama.Value);
    }

    [Fact]
    public void Update_NaN_HandlesGracefully()
    {
        var mama = new Mama();
        var input = new TValue(DateTime.UtcNow, double.NaN);

        var result = mama.Update(input);

        // Should return 0.0 (last valid price default) instead of NaN to avoid state corruption
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_InfinityInputs_DoesNotHang()
    {
        var mama = new Mama();

        // Warmup with valid data to get past initialization phase
        for (int i = 0; i < 60; i++)
        {
            mama.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        // Test positive infinity - should not hang
        var result1 = mama.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result1.Value), "Positive infinity should produce finite result");

        // Test negative infinity - should not hang
        var result2 = mama.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(result2.Value), "Negative infinity should produce finite result");

        // Test NaN - should not hang
        var result3 = mama.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result3.Value), "NaN should produce finite result");
    }

    [Fact]
    public void Calculate_Span_WithNonFiniteValues_DoesNotHang()
    {
        var data = new double[100];
        var gbm = new GBM(startPrice: 100, seed: 42);

        // Fill with mostly valid data
        for (int i = 0; i < 100; i++)
        {
            data[i] = gbm.Next().Close;
        }

        // Insert non-finite values at various points
        data[20] = double.NaN;
        data[40] = double.PositiveInfinity;
        data[60] = double.NegativeInfinity;
        data[80] = double.NaN;

        var output = new double[100];
        var famaOutput = new double[100];

        // This should complete without hanging
        Mama.Calculate(data, output, famaOutput: famaOutput);

        // Verify all outputs are finite (no NaN or Infinity propagation)
        for (int i = 0; i < 100; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"MAMA output at index {i} should be finite");
            Assert.True(double.IsFinite(famaOutput[i]), $"FAMA output at index {i} should be finite");
        }
    }

    [Fact]
    public void Update_Series_ReturnsSameCount()
    {
        var mama = new Mama();
        var source = new TSeries();
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        source.Add(new TValue(DateTime.UtcNow.AddMinutes(1), 101.0));

        var result = mama.Update(source);

        Assert.Equal(source.Count, result.Count);
    }

    [Fact]
    public void Chain_Update_Works()
    {
        var mama = new Mama(0.5, 0.05);

        // Manually chain for test
        bool eventFired = false;
        mama.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        mama.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void Update_Series_AppendsData()
    {
        var mama1 = new Mama();
        var mama2 = new Mama();

        var data = new TSeries();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            data.Add(new TValue(now.AddMinutes(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }

        // Case 1: Update all at once
        var result1 = mama1.Update(data);

        // Case 2: Update in chunks
        var chunk1 = new TSeries();
        var chunk2 = new TSeries();
        for (int i = 0; i < 25; i++)
        {
            chunk1.Add(data[i]);
        }

        for (int i = 25; i < 50; i++)
        {
            chunk2.Add(data[i]);
        }

        mama2.Update(chunk1);
        var result2 = mama2.Update(chunk2);

        // Verify final state is same
        Assert.Equal(mama1.Last.Value, mama2.Last.Value, 6);
        Assert.Equal(mama1.Fama.Value, mama2.Fama.Value, 6);

        // Verify the returned series from the second chunk matches the second half of the full result
        for (int i = 0; i < 25; i++)
        {
            Assert.Equal(result1[25 + i].Value, result2[i].Value, 6);
        }
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var mama = new Mama();

        // MAMA needs 50 bars to warmup (Index > 50)
        for (int i = 0; i < 50; i++)
        {
            mama.Update(new TValue(DateTime.UtcNow, 100));
            Assert.False(mama.IsHot);
        }

        mama.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(mama.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mama = new Mama();
        for (int i = 0; i < 55; i++)
        {
            mama.Update(new TValue(DateTime.UtcNow, 100));
        }
        Assert.True(mama.IsHot);

        mama.Reset();

        Assert.False(mama.IsHot);
        Assert.True(double.IsNaN(mama.Last.Value));
    }

    [Fact]
    public void Update_BarCorrection_UpdatesCorrectly()
    {
        var mama = new Mama();

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            mama.Update(new TValue(DateTime.UtcNow, 100));
        }

        // New bar
        var result1 = mama.Update(new TValue(DateTime.UtcNow, 110));

        // Update same bar with different value
        var result2 = mama.Update(new TValue(DateTime.UtcNow, 120), isNew: false);

        Assert.NotEqual(result1.Value, result2.Value);

        // Verify internal state by adding next bar
        var result3 = mama.Update(new TValue(DateTime.UtcNow, 130));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Calculate_StaticMethod_MatchesObjectInstance()
    {
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }

        var mama = new Mama();
        var series1 = mama.Update(source);
        var series2 = Mama.Batch(source);

        Assert.Equal(series1.Count, series2.Count);
        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(series1[i].Value, series2[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Calculate_Span_Matches_Update()
    {
        const int count = 100;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < count; i++)
        {
            data[i] = gbm.Next().Close;
        }

        var output = new double[count];
        Mama.Calculate(data, output);

        var mama = new Mama();
        for (int i = 0; i < count; i++)
        {
            var res = mama.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(res.Value, output[i], precision: 8);
        }
    }

    [Fact]
    public void Calculate_Span_ThrowsOnSmallOutput()
    {
        var data = new double[10];
        var output = new double[5];
        Assert.Throws<ArgumentOutOfRangeException>(() => Mama.Calculate(data, output));
    }

    [Fact]
    public void Calculate_Span_InvalidParameters_ThrowsArgumentOutOfRangeException()
    {
        var data = new double[10];
        var output = new double[10];

        // fastLimit <= 0
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Mama.Calculate(data, output, fastLimit: 0.0));
        Assert.Equal("fastLimit", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Mama.Calculate(data, output, fastLimit: -0.1));
        Assert.Equal("fastLimit", ex2.ParamName);

        // slowLimit <= 0
        var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Mama.Calculate(data, output, slowLimit: 0.0));
        Assert.Equal("slowLimit", ex3.ParamName);

        var ex4 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Mama.Calculate(data, output, slowLimit: -0.1));
        Assert.Equal("slowLimit", ex4.ParamName);

        // fastLimit > 1
        var ex5 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Mama.Calculate(data, output, fastLimit: 1.1));
        Assert.Equal("fastLimit", ex5.ParamName);

        // slowLimit > 1
        var ex6 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Mama.Calculate(data, output, slowLimit: 1.1));
        Assert.Equal("slowLimit", ex6.ParamName);

        // fastLimit <= slowLimit
        var ex7 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Mama.Calculate(data, output, fastLimit: 0.05, slowLimit: 0.5));
        Assert.Equal("fastLimit", ex7.ParamName);

        var ex8 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Mama.Calculate(data, output, fastLimit: 0.5, slowLimit: 0.5));
        Assert.Equal("fastLimit", ex8.ParamName);
    }

    [Fact]
    public void Prime_PreloadsState()
    {
        var data = new double[60];
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < 60; i++)
        {
            data[i] = gbm.Next().Close;
        }

        // 1. Prime with all but last value
        var mamaPrimed = new Mama();
        mamaPrimed.Prime(data.AsSpan().Slice(0, 59));

        // 2. Update with last value
        var resultPrimed = mamaPrimed.Update(new TValue(DateTime.UtcNow, data[59]));

        // 3. Run normal updates for comparison
        var mamaNormal = new Mama();
        TValue resultNormal = default;
        for (int i = 0; i < 60; i++)
        {
            resultNormal = mamaNormal.Update(new TValue(DateTime.UtcNow, data[i]));
        }

        Assert.True(mamaPrimed.IsHot);
        Assert.Equal(resultNormal.Value, resultPrimed.Value, precision: 9);
    }

    [Fact]
    public void Calculate_Span_WithFamaOutput_ProducesCorrectValues()
    {
        int count = 100;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < count; i++)
        {
            data[i] = gbm.Next().Close;
        }

        var mamaOutput = new double[count];
        var famaOutput = new double[count];
        Mama.Calculate(data, mamaOutput, famaOutput: famaOutput);

        var mama = new Mama();
        for (int i = 0; i < count; i++)
        {
            mama.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(mama.Last.Value, mamaOutput[i], precision: 8);
            Assert.Equal(mama.Fama.Value, famaOutput[i], precision: 8);
        }
    }

    [Fact]
    public void Calculate_Span_WithoutFamaOutput_BackwardsCompatible()
    {
        int count = 100;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < count; i++)
        {
            data[i] = gbm.Next().Close;
        }

        var output1 = new double[count];
        var output2 = new double[count];

        // Call without famaOutput parameter (backwards compatibility)
        Mama.Calculate(data, output1);

        // Call with empty famaOutput span
        Mama.Calculate(data, output2, famaOutput: Span<double>.Empty);

        // Both should produce identical MAMA results
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(output1[i], output2[i], precision: 12);
        }
    }

    [Fact]
    public void Calculate_Span_FamaOutput_ThrowsOnSmallBuffer()
    {
        var data = new double[10];
        var mamaOutput = new double[10];
        var famaOutput = new double[5];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Mama.Calculate(data, mamaOutput, famaOutput: famaOutput));
        Assert.Equal("famaOutput", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_FamaInitialization_MatchesInstanceMethod()
    {
        // Test that during initialization phase, FAMA output matches instance method behavior
        int count = 10;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < count; i++)
        {
            data[i] = gbm.Next().Close;
        }

        // Get values from span calculation
        var mamaOutput = new double[count];
        var famaOutput = new double[count];
        Mama.Calculate(data, mamaOutput, famaOutput: famaOutput);

        // Get values from instance method
        var mama = new Mama();
        for (int i = 0; i < count; i++)
        {
            mama.Update(new TValue(DateTime.UtcNow, data[i]));
            // Both MAMA and FAMA should match between span and instance methods
            Assert.Equal(mama.Last.Value, mamaOutput[i], precision: 8);
            Assert.Equal(mama.Fama.Value, famaOutput[i], precision: 8);
        }
    }

    [Fact]
    public void Calculate_Span_AllModes_ProduceSameResult()
    {
        int count = 100;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < count; i++)
        {
            data[i] = gbm.Next().Close;
        }

        // 1. Streaming Mode (instance method)
        var mama = new Mama();
        var streamingMama = new double[count];
        var streamingFama = new double[count];
        for (int i = 0; i < count; i++)
        {
            mama.Update(new TValue(DateTime.UtcNow, data[i]));
            streamingMama[i] = mama.Last.Value;
            streamingFama[i] = mama.Fama.Value;
        }

        // 2. Span Mode (static method with FAMA)
        var spanMama = new double[count];
        var spanFama = new double[count];
        Mama.Calculate(data, spanMama, famaOutput: spanFama);

        // 3. Verify MAMA matches
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingMama[i], spanMama[i], precision: 8);
        }

        // 4. Verify FAMA matches
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingFama[i], spanFama[i], precision: 8);
        }
    }
}
