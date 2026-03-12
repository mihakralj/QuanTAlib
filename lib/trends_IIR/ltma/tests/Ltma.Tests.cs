namespace QuanTAlib;

public class LtmaTests
{
    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ltma(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodNegative_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ltma(-5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsName()
    {
        var ltma = new Ltma(14);
        Assert.Equal("Ltma(14)", ltma.Name);
    }

    [Fact]
    public void Update_ReturnsValue()
    {
        var ltma = new Ltma(10);
        var result = ltma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var ltma = new Ltma(10);
        ltma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(ltma.Last.Value));
    }

    [Fact]
    public void IsHot_FalseInitially()
    {
        var ltma = new Ltma(10);
        ltma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.False(ltma.IsHot);
    }

    [Fact]
    public void IsHot_TrueAfterWarmup()
    {
        const int period = 5;
        var ltma = new Ltma(period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        bool wasHot = false;
        for (int i = 0; i < 500; i++)
        {
            var bar = gbm.Next(isNew: true);
            ltma.Update(new TValue(bar.Time, bar.Close));
            if (ltma.IsHot)
            {
                wasHot = true;
                break;
            }
        }

        Assert.True(wasHot);
    }

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var ltma = new Ltma(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        double prev = double.NaN;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = ltma.Update(new TValue(bar.Time, bar.Close), isNew: true);
            if (i > 0)
            {
                Assert.NotEqual(prev, result.Value);
            }
            prev = result.Value;
        }
    }

    [Fact]
    public void IsNew_False_RewritesCurrentBar()
    {
        var ltma = new Ltma(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed some bars
        for (int i = 0; i < 15; i++)
        {
            var bar = gbm.Next(isNew: true);
            ltma.Update(new TValue(bar.Time, bar.Close));
        }

        // New bar
        var newBar = gbm.Next(isNew: true);
        var first = ltma.Update(new TValue(newBar.Time, newBar.Close), isNew: true);

        // Correct same bar with different value
        var corrected = ltma.Update(new TValue(newBar.Time, newBar.Close + 5.0), isNew: false);
        Assert.NotEqual(first.Value, corrected.Value);

        // Correct again with original value — should restore
        var restored = ltma.Update(new TValue(newBar.Time, newBar.Close), isNew: false);
        Assert.Equal(first.Value, restored.Value, 1e-10);
    }

    [Fact]
    public void IterativeCorrection_RestoresState()
    {
        var ltma = new Ltma(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed bars
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            ltma.Update(new TValue(bar.Time, bar.Close));
        }

        // New bar
        var baseBar = gbm.Next(isNew: true);
        var baseResult = ltma.Update(new TValue(baseBar.Time, baseBar.Close), isNew: true);

        // Multiple corrections
        for (int c = 0; c < 5; c++)
        {
            ltma.Update(new TValue(baseBar.Time, baseBar.Close + (c + 1) * 2.0), isNew: false);
        }

        // Final correction with original → must restore
        var final_ = ltma.Update(new TValue(baseBar.Time, baseBar.Close), isNew: false);
        Assert.Equal(baseResult.Value, final_.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ltma = new Ltma(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            ltma.Update(new TValue(bar.Time, bar.Close));
        }

        ltma.Reset();
        Assert.Equal(default, ltma.Last);
        Assert.False(ltma.IsHot);
    }

    [Fact]
    public void NaN_UsesLastValid()
    {
        var ltma = new Ltma(10);

        var v1 = ltma.Update(new TValue(DateTime.UtcNow, 100.0));
        var v2 = ltma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(v1.Value));
        Assert.True(double.IsFinite(v2.Value));
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var ltma = new Ltma(10);

        ltma.Update(new TValue(DateTime.UtcNow, 100.0));
        var inf = ltma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(inf.Value));

        var negInf = ltma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(negInf.Value));
    }

    [Fact]
    public void AllNaN_ReturnsNaN()
    {
        var ltma = new Ltma(10);
        var result = ltma.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void ModeConsistency_StreamingMatchesBatch()
    {
        const int period = 10;
        const int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        var source = new TSeries();
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streaming = new Ltma(period);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // Batch (TSeries)
        var batchResults = Ltma.Batch(source, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void ModeConsistency_StreamingMatchesSpan()
    {
        const int period = 10;
        const int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        var values = new double[count];
        var source = new TSeries();
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            values[i] = bar.Close;
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streaming = new Ltma(period);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // Span batch
        var spanOutput = new double[count];
        Ltma.Batch(values, spanOutput, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void ModeConsistency_EventDrivenMatchesStreaming()
    {
        const int period = 10;
        const int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        var source = new TSeries();
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streaming = new Ltma(period);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // Event-driven
        var pubSource = new TSeries();
        var eventDriven = new Ltma(pubSource, period);
        var eventResults = new double[count];
        int idx = 0;

        static void OnPub(object? sender, in TValueEventArgs args)
        {
            // no-op: we read Last after each Update
        }
        eventDriven.Pub += OnPub;

        for (int i = 0; i < count; i++)
        {
            pubSource.Add(source[i], true);
            eventResults[idx++] = eventDriven.Last.Value;
        }

        eventDriven.Pub -= OnPub;

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_ValidatesLength()
    {
        var src = new double[] { 1, 2, 3 };
        var output = new double[] { 0, 0 };

        var ex = Assert.Throws<ArgumentException>(() => Ltma.Batch(src, output, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod()
    {
        var src = new double[] { 1, 2, 3 };
        var output = new double[] { 0, 0, 0 };

        var ex = Assert.Throws<ArgumentException>(() => Ltma.Batch(src, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_EmptyInput()
    {
        Ltma.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, 10);
        Assert.True(true);
    }

    [Fact]
    public void SpanBatch_NaN_Handled()
    {
        var src = new double[] { 100, double.NaN, 102, double.NaN, 104 };
        var output = new double[5];
        Ltma.Batch(src, output, 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void Period1_Identity()
    {
        // period=1 → alpha=1 → EMA1=source, EMA2=source → LTMA=2*src-src=src
        var ltma = new Ltma(1);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = ltma.Update(new TValue(bar.Time, bar.Close));
            Assert.Equal(bar.Close, result.Value, 1e-9);
        }
    }

    [Fact]
    public void LargeData_NoOverflow()
    {
        var ltma = new Ltma(14);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 10_000; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = ltma.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Dispose_UnsubscribesEvent()
    {
        var source = new TSeries();
        var ltma = new Ltma(source, 10);
        ltma.Dispose();

        // After dispose, adding to source should not throw
        source.Add(new TValue(DateTime.UtcNow, 100.0), true);
        Assert.True(double.IsNaN(ltma.Last.Value) || double.IsFinite(ltma.Last.Value));
    }

    [Fact]
    public void PubEvent_Fires()
    {
        var ltma = new Ltma(10);
        int pubCount = 0;

        // skipcq: CS-R1140 - non-static lambda intentionally captures pubCount
        ltma.Pub += (object? sender, in TValueEventArgs args) => { pubCount++; };

        ltma.Update(new TValue(DateTime.UtcNow, 100.0));
        ltma.Update(new TValue(DateTime.UtcNow, 101.0));

        Assert.Equal(2, pubCount);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 50; i++)
        {
            source.Add(new TValue(gbm.Next(isNew: true).Time, gbm.Next(isNew: true).Close));
        }

        var (results, indicator) = Ltma.Calculate(source, 10);

        Assert.Equal(source.Count, results.Count);
        Assert.NotNull(indicator);
        Assert.Equal("Ltma(10)", indicator.Name);
    }

    [Fact]
    public void MatchesDema_SameFormula()
    {
        // LTMA uses same 2·EMA1 - EMA2 formula as DEMA
        const int period = 10;
        var ltma = new Ltma(period);
        var dema = new Dema(period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tVal = new TValue(bar.Time, bar.Close);

            var ltmaVal = ltma.Update(tVal);
            var demaVal = dema.Update(tVal);

            Assert.Equal(demaVal.Value, ltmaVal.Value, 1e-9);
        }
    }
}
