namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Polyfit against manual OLS computations and mathematical identities.
/// No external library (Skender/TA-Lib/Tulip/Ooples) implements polynomial regression of
/// variable degree, so validation is against closed-form solutions and known identities.
/// </summary>
public class PolyfitValidationTests
{
    // ── 1. Streaming vs Batch vs Span consistency ─────────────────────────────

    [Fact]
    public void Streaming_Batch_Span_Consistent()
    {
        int period = 10;
        int degree = 2;
        int dataLen = 50;
        var gbm = new GBM(100, 0.05, 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < dataLen; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streaming = new Polyfit(period, degree);
        double[] streamVals = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            streaming.Update(series[i]);
            streamVals[i] = streaming.Last.Value;
        }

        // Batch TSeries
        var batchResult = Polyfit.Batch(series, period, degree);

        // Span
        double[] spanOut = new double[dataLen];
        Polyfit.Batch(series.Values, spanOut.AsSpan(), period, degree);

        // All modes must agree at every hot position
        for (int i = period - 1; i < dataLen; i++)
        {
            Assert.Equal(streamVals[i], batchResult[i].Value, 1e-9);
            Assert.Equal(streamVals[i], spanOut[i], 1e-9);
        }
    }

    // ── 2. Known values: degree=1 matches closed-form linear regression ────────

    [Fact]
    public void Degree1_KnownValues_MatchOlsLinearRegression()
    {
        // For y = [1,2,3,4,5] with x_norm = [0, 0.25, 0.5, 0.75, 1.0]:
        // Linear fit: b1=(n*Σxy-Σx*Σy)/(n*Σx²-Σx²), b0=Ȳ-b1*x̄
        // P(1.0) for y=1..5 → value at the endpoint = 5 (perfect linear fit)
        var p = new Polyfit(5, 1);
        for (int i = 1; i <= 5; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), (double)i));
        }
        Assert.Equal(5.0, p.Last.Value, 1e-9);
    }

    [Fact]
    public void Degree1_ReverseLinear_MatchesEndpoint()
    {
        // y = 5,4,3,2,1 → P(1.0) = 1.0 (last value)
        var p = new Polyfit(5, 1);
        for (int i = 5; i >= 1; i--)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(5 - i), (double)i));
        }
        Assert.Equal(1.0, p.Last.Value, 1e-9);
    }

    // ── 3. Degree=2 exact quadratic recovery ──────────────────────────────────

    [Fact]
    public void Degree2_ExactQuadratic_RecoverCoefficients()
    {
        // y = 3 + 2*x + x^2 with x_norm in [0,1] over 5 points
        // P(1) = 3 + 2 + 1 = 6
        int n = 5;
        var p = new Polyfit(n, 2);
        for (int i = 0; i < n; i++)
        {
            double x = i / (double)(n - 1);
            double y = Math.FusedMultiplyAdd(x, x, Math.FusedMultiplyAdd(2.0, x, 3.0));
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), y));
        }
        Assert.Equal(6.0, p.Last.Value, 1e-9);
    }

    [Fact]
    public void Degree2_PureQuadratic_RecoverEndpoint()
    {
        // y = x^2, n=11, x in [0,1] step 0.1 → P(1.0) = 1.0
        int n = 11;
        var p = new Polyfit(n, 2);
        for (int i = 0; i < n; i++)
        {
            double x = i / (double)(n - 1);
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), x * x));
        }
        Assert.Equal(1.0, p.Last.Value, 1e-9);
    }

    // ── 4. Degree=3 exact cubic recovery ──────────────────────────────────────

    [Fact]
    public void Degree3_ExactCubic_RecoverEndpoint()
    {
        // y = x^3 with x_norm in [0,1], n=10 → P(1.0) = 1.0
        int n = 10;
        var p = new Polyfit(n, 3);
        for (int i = 0; i < n; i++)
        {
            double x = i / (double)(n - 1);
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), x * x * x));
        }
        Assert.Equal(1.0, p.Last.Value, 1e-9);
    }

    // ── 5. Constant data trivially correct for all degrees ────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ConstantData_AllDegrees_ReturnsConstant(int degree)
    {
        var p = new Polyfit(10, degree);
        for (int i = 0; i < 10; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }
        Assert.Equal(100.0, p.Last.Value, 1e-9);
    }

    // ── 6. Degree=1 matches Lsma (offset=0) exactly ───────────────────────────

    [Fact]
    public void Degree1_MatchesLsma_MultiBar()
    {
        int period = 10;
        var poly = new Polyfit(period, 1);
        var lsma = new Lsma(period);

        var gbm = new GBM(100, 0.05, 0.2, seed: 123);
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next();
            var tv = new TValue(bar.Time, bar.Close);
            poly.Update(tv);
            lsma.Update(tv);

            if (poly.IsHot)
            {
                // Polyfit(degree=1) == LSMA(offset=0): both are the lin-reg endpoint
                Assert.Equal(lsma.Last.Value, poly.Last.Value, 1e-6);
            }
        }
    }

    // ── 7. Higher degree fits better for polynomial data ──────────────────────

    [Fact]
    public void Degree2_FitsBetterThanDegree1_ForQuadraticSignal()
    {
        // Quadratic signal: degree=2 should recover the endpoint more accurately
        int n = 20;
        var series = new TSeries();
        for (int i = 0; i < n; i++)
        {
            double x = i / (double)(n - 1);
            double y = x * x;
            series.Add(new TValue(DateTime.UtcNow.AddSeconds(i), y));
        }

        var poly1 = new Polyfit(n, 1);
        var poly2 = new Polyfit(n, 2);
        for (int i = 0; i < n; i++)
        {
            poly1.Update(series[i]);
            poly2.Update(series[i]);
        }

        // Degree=2 should exactly reproduce y=1.0 for pure quadratic
        Assert.Equal(1.0, poly2.Last.Value, 1e-9);

        // Degree=1 approximates but can't exactly match a quadratic
        double err1 = Math.Abs(poly1.Last.Value - 1.0);
        double err2 = Math.Abs(poly2.Last.Value - 1.0);
        Assert.True(err2 <= err1 + 1e-12);
    }

    // ── 8. Rolling window correctness ─────────────────────────────────────────

    [Fact]
    public void RollingWindow_StreamingMatchesBatchAtEachBar()
    {
        int period = 6;
        int degree = 2;
        var gbm = new GBM(100, 0.05, 0.2, seed: 321);
        double[] allData = new double[25];
        DateTime[] allTimes = new DateTime[25];
        for (int i = 0; i < 25; i++)
        {
            var bar = gbm.Next();
            allData[i] = bar.Close;
            allTimes[i] = DateTime.UtcNow.AddSeconds(i);
        }

        var streaming = new Polyfit(period, degree);
        for (int i = 0; i < 25; i++)
        {
            streaming.Update(new TValue(allTimes[i], allData[i]));

            // At each bar, manually compute polyfit over the window ending at bar i
            int windowStart = Math.Max(0, i - period + 1);
            int windowLen = i - windowStart + 1;
            double[] window = allData[windowStart..(i + 1)];

            double manualResult = Polyfit.ComputePolyfit(window, Math.Min(degree, windowLen - 1));
            Assert.Equal(manualResult, streaming.Last.Value, 1e-9);
        }
    }

    // ── 9. Multiple periods with GBM data ─────────────────────────────────────

    [Theory]
    [InlineData(5, 1)]
    [InlineData(10, 2)]
    [InlineData(20, 3)]
    [InlineData(14, 2)]
    public void GBMData_AllFinite(int period, int degree)
    {
        var gbm = new GBM(100, 0.05, 0.2, seed: (period * 10) + degree);
        var p = new Polyfit(period, degree);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next();
            p.Update(new TValue(bar.Time, bar.Close));
            if (p.IsHot)
            {
                Assert.True(double.IsFinite(p.Last.Value),
                    $"Got non-finite at i={i}: {p.Last.Value}");
            }
        }
    }

    // ── 10. Batch TSeries vs streaming at last value ───────────────────────────

    [Fact]
    public void BatchFinalValue_MatchesStreamingFinalValue()
    {
        int period = 8;
        int degree = 2;
        var gbm = new GBM(100, 0.05, 0.2, seed: 999);
        var series = new TSeries();
        var streaming = new Polyfit(period, degree);

        for (int i = 0; i < 40; i++)
        {
            var bar = gbm.Next();
            var tv = new TValue(bar.Time, bar.Close);
            series.Add(tv);
            streaming.Update(tv);
        }

        var batchResult = Polyfit.Batch(series, period, degree);
        Assert.Equal(batchResult[39].Value, streaming.Last.Value, 1e-9);
    }
}
