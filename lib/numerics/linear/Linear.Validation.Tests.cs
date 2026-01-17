using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for LINEAR transformer.
/// Validates against direct mathematical computation and algebraic properties.
/// </summary>
public class LinearValidationTests
{
    private readonly GBM _gbm = new(sigma: 0.5, mu: 0.0, seed: 42);
    private const double Tolerance = 1e-10;

    [Fact]
    public void Linear_Batch_MatchesMathFormula()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        double slope = 2.5;
        double intercept = -15.0;

        var result = Linear.Calculate(series, slope, intercept);

        for (int i = 0; i < series.Count; i++)
        {
            double expected = slope * series[i].Value + intercept;
            Assert.Equal(expected, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Linear_Streaming_MatchesMathFormula()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        double slope = 0.5;
        double intercept = 100.0;

        var linear = new Linear(slope, intercept);

        for (int i = 0; i < series.Count; i++)
        {
            var result = linear.Update(series[i], true);
            double expected = slope * series[i].Value + intercept;
            Assert.Equal(expected, result.Value, Tolerance);
        }
    }

    [Fact]
    public void Linear_Span_MatchesMathFormula()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        ReadOnlySpan<double> source = bars.Close.Values;
        Span<double> output = stackalloc double[source.Length];
        double slope = -1.5;
        double intercept = 50.0;

        Linear.Calculate(source, output, slope, intercept);

        for (int i = 0; i < source.Length; i++)
        {
            double expected = slope * source[i] + intercept;
            Assert.Equal(expected, output[i], Tolerance);
        }
    }

    [Fact]
    public void Linear_Identity_YEqualsX()
    {
        // slope=1, intercept=0 should give y=x
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var result = Linear.Calculate(series, slope: 1.0, intercept: 0.0);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(series[i].Value, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Linear_Constant_YEqualsIntercept()
    {
        // slope=0 should give y=intercept regardless of x
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        double intercept = 42.0;

        var result = Linear.Calculate(series, slope: 0.0, intercept: intercept);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(intercept, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Linear_Composition_IsLinear()
    {
        // Applying Linear(a,b) then Linear(c,d) should equal Linear(a*c, b*c+d)
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        double a = 2.0, b = 5.0;  // First transform
        double c = 3.0, d = -10.0; // Second transform

        // Compose sequentially
        var step1 = Linear.Calculate(series, a, b);
        var composed = Linear.Calculate(step1, c, d);

        // Direct composed transform: y = c*(a*x + b) + d = (a*c)*x + (b*c + d)
        double composedSlope = a * c;
        double composedIntercept = b * c + d;
        var direct = Linear.Calculate(series, composedSlope, composedIntercept);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(direct[i].Value, composed[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Linear_Inverse_RecoverOriginal()
    {
        // Applying Linear(a,b) then Linear(1/a, -b/a) should recover original
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        double a = 2.5, b = -15.0;

        var transformed = Linear.Calculate(series, a, b);
        var recovered = Linear.Calculate(transformed, 1.0 / a, -b / a);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(series[i].Value, recovered[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Linear_Distributive_OverAddition()
    {
        // Linear(a,0)(x + y) = Linear(a,0)(x) + Linear(a,0)(y) - not exactly true for full linear
        // But for pure scaling: a*(x+y) = a*x + a*y
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double a = 3.0;
        double offset = 10.0;

        var series = bars.Close;

        // Create shifted series
        var shifted = new TSeries();
        for (int i = 0; i < series.Count; i++)
            shifted.Add(new TValue(series[i].Time, series[i].Value + offset), true);

        // a * (x + offset) should equal a*x + a*offset
        var scaledSum = Linear.Calculate(shifted, a, 0.0);
        var sumOfScaled = Linear.Calculate(series, a, a * offset);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(scaledSum[i].Value, sumOfScaled[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Linear_Negation_Property()
    {
        // Linear(-1, 0) should negate values
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var negated = Linear.Calculate(series, slope: -1.0, intercept: 0.0);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(-series[i].Value, negated[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Linear_DoubleNegation_RecoverOriginal()
    {
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var negated = Linear.Calculate(series, slope: -1.0, intercept: 0.0);
        var recovered = Linear.Calculate(negated, slope: -1.0, intercept: 0.0);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(series[i].Value, recovered[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Linear_KnownValues()
    {
        var series = new TSeries();
        var time = DateTime.UtcNow;
        series.Add(new TValue(time, 0.0), true);
        series.Add(new TValue(time.AddSeconds(1), 1.0), true);
        series.Add(new TValue(time.AddSeconds(2), -1.0), true);
        series.Add(new TValue(time.AddSeconds(3), 100.0), true);

        // y = 2x + 3
        var result = Linear.Calculate(series, slope: 2.0, intercept: 3.0);

        Assert.Equal(3.0, result[0].Value, Tolerance);    // 2*0+3
        Assert.Equal(5.0, result[1].Value, Tolerance);    // 2*1+3
        Assert.Equal(1.0, result[2].Value, Tolerance);    // 2*(-1)+3
        Assert.Equal(203.0, result[3].Value, Tolerance);  // 2*100+3
    }

    [Fact]
    public void Linear_PreservesRelativeDifferences()
    {
        // For any x1, x2: Linear(x2) - Linear(x1) = slope * (x2 - x1)
        var series = new TSeries();
        var time = DateTime.UtcNow;
        series.Add(new TValue(time, 10.0), true);
        series.Add(new TValue(time.AddSeconds(1), 30.0), true);
        series.Add(new TValue(time.AddSeconds(2), 25.0), true);

        double slope = 2.5;
        double intercept = 100.0;

        var result = Linear.Calculate(series, slope, intercept);

        // Difference between consecutive values should be scaled by slope
        double diff_01_input = series[1].Value - series[0].Value;  // 20
        double diff_01_output = result[1].Value - result[0].Value; // should be 50

        double diff_12_input = series[2].Value - series[1].Value;  // -5
        double diff_12_output = result[2].Value - result[1].Value; // should be -12.5

        Assert.Equal(slope * diff_01_input, diff_01_output, Tolerance);
        Assert.Equal(slope * diff_12_input, diff_12_output, Tolerance);
    }

    [Fact]
    public void Linear_FMA_Accuracy()
    {
        // Verify FMA produces accurate results for edge cases
        var series = new TSeries();
        var time = DateTime.UtcNow;

        // Use values that might cause precision issues without FMA
        series.Add(new TValue(time, 1e15), true);
        series.Add(new TValue(time.AddSeconds(1), 1e-15), true);
        series.Add(new TValue(time.AddSeconds(2), 1.0 + 1e-15), true);

        double slope = 1.0 + 1e-10;
        double intercept = -1e15;

        var result = Linear.Calculate(series, slope, intercept);

        // Verify each result matches direct computation
        for (int i = 0; i < series.Count; i++)
        {
            double expected = Math.FusedMultiplyAdd(slope, series[i].Value, intercept);
            Assert.Equal(expected, result[i].Value, 1e-5);
        }
    }
}
