namespace QuanTAlib;

public class NetTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var rng = new Random(42);
        TSeries series = [];
        for (int i = 0; i < count; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddSeconds(i).Ticks, 100.0 + rng.NextDouble() * 10.0));
        }
        return series;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Constructor Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_DefaultPeriod_Is14()
    {
        var net = new Net();
        Assert.Equal("Net(14)", net.Name);
    }

    [Fact]
    public void Constructor_CustomPeriod()
    {
        var net = new Net(period: 20);
        Assert.Equal("Net(20)", net.Name);
    }

    [Fact]
    public void Constructor_PeriodBelowMin_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Net(period: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_InvalidPeriods_Throw(int bad)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Net(period: bad));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Basic Calculation Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Calc_RisingSeries_PositiveOutput()
    {
        var net = new Net(period: 5);
        TValue result = default;
        for (int i = 1; i <= 10; i++)
        {
            result = net.Update(new TValue(i, i * 10.0));
        }
        Assert.True(result.Value > 0.0, $"Expected positive NET for rising series, got {result.Value}");
    }

    [Fact]
    public void Calc_FallingSeries_NegativeOutput()
    {
        var net = new Net(period: 5);
        TValue result = default;
        for (int i = 1; i <= 10; i++)
        {
            result = net.Update(new TValue(i, 100.0 - i * 10.0));
        }
        Assert.True(result.Value < 0.0, $"Expected negative NET for falling series, got {result.Value}");
    }

    [Fact]
    public void Calc_PerfectlyRising_ReturnsPositiveOne()
    {
        var net = new Net(period: 5);
        TValue result = default;
        for (int i = 1; i <= 5; i++)
        {
            result = net.Update(new TValue(i, (double)i));
        }
        Assert.Equal(1.0, result.Value, 10);
    }

    [Fact]
    public void Calc_PerfectlyFalling_ReturnsNegativeOne()
    {
        var net = new Net(period: 5);
        TValue result = default;
        for (int i = 1; i <= 5; i++)
        {
            result = net.Update(new TValue(i, 100.0 - i));
        }
        Assert.Equal(-1.0, result.Value, 10);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  State / Bar Correction Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void BarCorrection_SecondUpdateOverwritesFirst()
    {
        var net = new Net(period: 5);
        for (int i = 1; i <= 6; i++)
        {
            net.Update(new TValue(i, 50.0 + i));
        }
        // Replace last bar value (isNew=false) with same value
        var result1 = net.Update(new TValue(7, 60.0));
        var result2 = net.Update(new TValue(7, 60.0), isNew: false);
        Assert.Equal(result1.Value, result2.Value, 12);
    }

    [Fact]
    public void BarCorrection_IsNewFalseThenTrue_MatchesSingleUpdate()
    {
        // Path A: feed 10 bars normally
        var netA = new Net(period: 5);
        for (int i = 1; i <= 9; i++)
        {
            netA.Update(new TValue(i, 50.0 + i));
        }
        var resultA = netA.Update(new TValue(10, 60.0));

        // Path B: feed 9 bars, then bar 10 as tick update then new bar
        var netB = new Net(period: 5);
        for (int i = 1; i <= 9; i++)
        {
            netB.Update(new TValue(i, 50.0 + i));
        }
        netB.Update(new TValue(10, 55.0));        // initial tick
        netB.Update(new TValue(10, 58.0), false);  // correction
        netB.Update(new TValue(10, 60.0), false);  // final correction
        var resultB = netB.Last;

        Assert.Equal(resultA.Value, resultB.Value, 12);
    }

    [Fact]
    public void BarCorrection_MultipleCorrections_StableOutput()
    {
        var net = new Net(period: 10);
        for (int i = 1; i <= 20; i++)
        {
            net.Update(new TValue(i, 50.0 + i * 0.5));
        }

        double firstVal = net.Update(new TValue(21, 70.0)).Value;

        // Multiple corrections should still produce same result
        for (int t = 0; t < 5; t++)
        {
            net.Update(new TValue(21, 70.0), false);
        }
        double lastVal = net.Last.Value;
        Assert.Equal(firstVal, lastVal, 12);
    }

    [Fact]
    public void BarCorrection_DifferentValues_ChangesResult()
    {
        var net = new Net(period: 5);
        for (int i = 1; i <= 5; i++)
        {
            net.Update(new TValue(i, 50.0 + i));
        }

        double v1 = net.Update(new TValue(6, 100.0)).Value;
        double v2 = net.Update(new TValue(6, 1.0), false).Value;

        Assert.NotEqual(v1, v2);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Warmup / Convergence Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Warmup_BeforePeriod_IsNotHot()
    {
        var net = new Net(period: 10);
        for (int i = 1; i < 10; i++)
        {
            net.Update(new TValue(i, 50.0 + i));
            Assert.False(net.IsHot, $"Should not be hot at bar {i}");
        }
    }

    [Fact]
    public void Warmup_AtPeriod_BecomesHot()
    {
        var net = new Net(period: 10);
        for (int i = 1; i <= 10; i++)
        {
            net.Update(new TValue(i, 50.0 + i));
        }
        Assert.True(net.IsHot);
    }

    [Fact]
    public void Warmup_FirstBar_ReturnsZero()
    {
        var net = new Net(period: 5);
        var result = net.Update(new TValue(1, 100.0));
        Assert.Equal(0.0, result.Value);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Robustness Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Robustness_NaN_SubstitutesLastValid()
    {
        var net = new Net(period: 5);
        for (int i = 1; i <= 5; i++)
        {
            net.Update(new TValue(i, 50.0 + i));
        }
        var result = net.Update(new TValue(6, double.NaN));
        Assert.True(double.IsFinite(result.Value), "Output should be finite after NaN input");
    }

    [Fact]
    public void Robustness_Infinity_SubstitutesLastValid()
    {
        var net = new Net(period: 5);
        for (int i = 1; i <= 5; i++)
        {
            net.Update(new TValue(i, 50.0 + i));
        }
        var result = net.Update(new TValue(6, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Robustness_NegativeInfinity_SubstitutesLastValid()
    {
        var net = new Net(period: 5);
        for (int i = 1; i <= 5; i++)
        {
            net.Update(new TValue(i, 50.0 + i));
        }
        var result = net.Update(new TValue(6, double.NegativeInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Consistency Tests (all API modes must match)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Consistency_StreamVsBatch_Match()
    {
        TSeries input = MakeSeries(200);
        int period = 10;

        // Streaming mode
        var netStream = new Net(period);
        TSeries streamResult = [];
        foreach (var v in input)
        {
            streamResult.Add(netStream.Update(v));
        }

        // Batch mode
        var batchResult = Net.Batch(input, period);

        for (int i = 0; i < input.Count; i++)
        {
            Assert.Equal(streamResult[i].Value, batchResult[i].Value, 12);
        }
    }

    [Fact]
    public void Consistency_SpanVsStreaming_Match()
    {
        TSeries input = MakeSeries(200);
        int period = 10;

        // Streaming
        var netStream = new Net(period);
        TSeries streamResult = [];
        foreach (var v in input)
        {
            streamResult.Add(netStream.Update(v));
        }

        // Span
        double[] src = new double[input.Count];
        for (int i = 0; i < input.Count; i++)
        {
            src[i] = input[i].Value;
        }
        double[] dst = new double[src.Length];
        Net.Batch(src.AsSpan(), dst.AsSpan(), period);

        for (int i = 0; i < input.Count; i++)
        {
            Assert.Equal(streamResult[i].Value, dst[i], 12);
        }
    }

    [Fact]
    public void Consistency_UpdateTSeries_MatchesStreaming()
    {
        TSeries input = MakeSeries(100);
        int period = 8;

        // Streaming
        var netS = new Net(period);
        TSeries streamResult = [];
        foreach (var v in input)
        {
            streamResult.Add(netS.Update(v));
        }

        // Update(TSeries)
        var netU = new Net(period);
        TSeries updateResult = netU.Update(input);

        for (int i = 0; i < input.Count; i++)
        {
            Assert.Equal(streamResult[i].Value, updateResult[i].Value, 12);
        }
    }

    [Fact]
    public void Consistency_Calculate_MatchesStreaming()
    {
        TSeries input = MakeSeries(100);
        int period = 8;

        // Streaming
        var netS = new Net(period);
        TSeries streamResult = [];
        foreach (var v in input)
        {
            streamResult.Add(netS.Update(v));
        }

        // Calculate
        var (calcResult, _) = Net.Calculate(input, period);

        for (int i = 0; i < input.Count; i++)
        {
            Assert.Equal(streamResult[i].Value, calcResult[i].Value, 12);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Span API Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Span_DestinationTooShort_Throws()
    {
        double[] src = [1, 2, 3, 4, 5];
        double[] dst = new double[3];
        Assert.Throws<ArgumentException>(() => Net.Batch(src.AsSpan(), dst.AsSpan(), 3));
    }

    [Fact]
    public void Span_InvalidPeriod_Throws()
    {
        double[] src = [1, 2, 3, 4, 5];
        double[] dst = new double[5];
        Assert.Throws<ArgumentOutOfRangeException>(() => Net.Batch(src.AsSpan(), dst.AsSpan(), 1));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Chainability / Events Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Chain_EventFires()
    {
        var net = new Net(period: 5);
        int eventCount = 0;
        net.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        for (int i = 1; i <= 10; i++)
        {
            net.Update(new TValue(i, 50.0 + i));
        }
        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void Chain_SourceSubscription()
    {
        TSeries source = [];
        var net = new Net(source, period: 5);
        int eventCount = 0;
        net.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        for (int i = 1; i <= 10; i++)
        {
            source.Add(new TValue(i, 50.0 + i));
        }
        Assert.Equal(10, eventCount);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NET-Specific Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Net_ConstantInput_ReturnsZero()
    {
        var net = new Net(period: 5);
        for (int i = 1; i <= 10; i++)
        {
            net.Update(new TValue(i, 42.0));
        }
        Assert.Equal(0.0, net.Last.Value, 12);
    }

    [Fact]
    public void Net_OutputBounded()
    {
        var net = new Net(period: 10);
        var rng = new Random(123);
        for (int i = 0; i < 1000; i++)
        {
            net.Update(new TValue(i, rng.NextDouble() * 200.0 - 100.0));
            Assert.InRange(net.Last.Value, -1.0, 1.0);
        }
    }

    [Fact]
    public void Net_Symmetry_RisingFalling()
    {
        // τ for [1,2,3,4,5] should be -τ for [5,4,3,2,1]
        var netRise = new Net(period: 5);
        for (int i = 1; i <= 5; i++)
        {
            netRise.Update(new TValue(i, (double)i));
        }

        var netFall = new Net(period: 5);
        for (int i = 1; i <= 5; i++)
        {
            netFall.Update(new TValue(i, 6.0 - i));
        }

        Assert.Equal(netRise.Last.Value, -netFall.Last.Value, 12);
    }

    [Fact]
    public void Net_DifferentPeriods_DifferentResults()
    {
        TSeries input = MakeSeries(50);

        var net5 = new Net(period: 5);
        var net20 = new Net(period: 20);

        for (int i = 0; i < input.Count; i++)
        {
            net5.Update(input[i]);
            net20.Update(input[i]);
        }

        // Different periods should generally produce different results
        Assert.NotEqual(net5.Last.Value, net20.Last.Value);
    }

    [Fact]
    public void Net_Reset_ClearsState()
    {
        var net = new Net(period: 5);
        for (int i = 1; i <= 10; i++)
        {
            net.Update(new TValue(i, 50.0 + i));
        }
        Assert.True(net.IsHot);

        net.Reset();
        Assert.False(net.IsHot);

        var result = net.Update(new TValue(1, 100.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Net_Prime_SetsState()
    {
        var net = new Net(period: 5);
        double[] primeData = [10, 20, 30, 40, 50];
        net.Prime(primeData);
        Assert.True(net.IsHot);
        Assert.Equal(1.0, net.Last.Value, 10); // perfectly rising
    }

    [Fact]
    public void Net_KnownSequence_CorrectTau()
    {
        // For [1, 3, 2, 5, 4] (stored newest-first in X[]):
        // X[0]=4 (newest), X[1]=5, X[2]=2, X[3]=3, X[4]=1 (oldest)
        // Using Ehlers formula: for i=1 to 4, for k=0 to i-1: Num -= Sign(X[i]-X[k])
        //
        // But we feed as a time series: bar1=1, bar2=3, bar3=2, bar4=5, bar5=4
        // In RingBuffer: [0]=1, [1]=3, [2]=2, [3]=5, [4]=4
        // Mapping: X[0]=buf[4]=4, X[1]=buf[3]=5, X[2]=buf[2]=2, X[3]=buf[1]=3, X[4]=buf[0]=1
        //
        // i=1,k=0: -(Sign(5-4)) = -1
        // i=2,k=0: -(Sign(2-4)) = +1; k=1: -(Sign(2-5)) = +1
        // i=3,k=0: -(Sign(3-4)) = +1; k=1: -(Sign(3-5)) = +1; k=2: -(Sign(3-2)) = -1
        // i=4,k=0: -(Sign(1-4)) = +1; k=1: -(Sign(1-5)) = +1; k=2: -(Sign(1-2)) = +1; k=3: -(Sign(1-3)) = +1
        // Num = -1 + 1 + 1 + 1 + 1 - 1 + 1 + 1 + 1 + 1 = 6
        // Denom = 0.5 * 5 * 4 = 10
        // Tau = 6/10 = 0.6
        var net = new Net(period: 5);
        double[] seq = [1, 3, 2, 5, 4];
        for (int i = 0; i < seq.Length; i++)
        {
            net.Update(new TValue(i + 1, seq[i]));
        }
        Assert.Equal(0.6, net.Last.Value, 10);
    }

    [Fact]
    public void Net_LargeDataset_NoCrash()
    {
        var net = new Net(period: 14);
        var rng = new Random(99);
        for (int i = 0; i < 100_000; i++)
        {
            net.Update(new TValue(i, 100.0 + rng.NextDouble() * 50.0));
        }
        Assert.True(double.IsFinite(net.Last.Value));
        Assert.InRange(net.Last.Value, -1.0, 1.0);
    }
}
