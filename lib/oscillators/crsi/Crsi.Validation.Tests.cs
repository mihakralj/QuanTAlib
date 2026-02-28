using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// CRSI validation:
/// - Internal consistency (streaming/batch/span/eventing)
/// - Native Skender GetConnorsRsi cross-validation (batch, streaming, span)
/// - Native Ooples CalculateConnorsRelativeStrengthIndex cross-validation (batch, streaming, span)
/// - External structural cross-validation via RSI components from Skender/TA-Lib/Tulip/Ooples
/// </summary>
public sealed class CrsiValidationTests(ITestOutputHelper output) : IDisposable
{
    private const double Tolerance = 1e-10;
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _data.Dispose();
    }

    [Fact]
    public void Streaming_MatchesBatch_DefaultParams()
    {
        var source = _data.Data;

        var streaming = new Crsi(3, 2, 100);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        TSeries batchTs = Crsi.Batch(source, 3, 2, 100);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], batchTs.Values[i], Tolerance);
        }
    }

    [Fact]
    public void Span_MatchesBatch_DefaultParams()
    {
        var source = _data.Data;

        TSeries batchTs = Crsi.Batch(source, 3, 2, 100);

        var spanOut = new double[source.Count];
        Crsi.Batch(source.Values, spanOut, 3, 2, 100);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOut[i], Tolerance);
        }
    }

    [Fact]
    public void Eventing_MatchesStreaming()
    {
        var source = _data.Data;

        var streaming = new Crsi(3, 2, 50);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        var eventTs = new TSeries();
        var eventCrsi = new Crsi(eventTs, 3, 2, 50);
        var eventVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventTs.Add(source[i]);
            eventVals[i] = eventCrsi.Last.Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], eventVals[i], Tolerance);
        }
    }

    [Fact]
    public void Output_AlwaysInRange0To100()
    {
        var source = _data.Data;
        var crsi = new Crsi(3, 2, 100);

        for (int i = 0; i < source.Count; i++)
        {
            double v = crsi.Update(source[i]).Value;
            Assert.True(v >= 0.0 && v <= 100.0, $"CRSI={v} at i={i}");
        }
    }

    [Fact]
    public void Reset_ThenReplay_MatchesFreshRun()
    {
        var source = _data.Data;

        var crsi1 = new Crsi(3, 2, 30);
        for (int i = 0; i < source.Count; i++)
        {
            crsi1.Update(source[i]);
        }

        double finalVal1 = crsi1.Last.Value;

        crsi1.Reset();
        for (int i = 0; i < source.Count; i++)
        {
            crsi1.Update(source[i]);
        }

        Assert.Equal(finalVal1, crsi1.Last.Value, Tolerance);
    }

    [Fact]
    public void DifferentPeriods_ProduceDistinctResults()
    {
        var source = _data.Data;

        TSeries r1 = Crsi.Batch(source, 3, 2, 50);
        TSeries r2 = Crsi.Batch(source, 5, 3, 50);

        bool anyDiff = false;
        for (int i = 0; i < source.Count; i++)
        {
            if (Math.Abs(r1.Values[i] - r2.Values[i]) > 1e-6)
            {
                anyDiff = true;
                break;
            }
        }

        Assert.True(anyDiff, "Different periods should produce different results");
    }

    [Fact]
    public void Validate_Skender_StructuralComposite()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        double[] close = _data.ClosePrices.ToArray();
        double[] streak = ComputeStreak(close);
        double[] pctRank = ComputePercentRank(close, rankPeriod);

        var closeRsi = _data.SkenderQuotes.GetRsi(rsiPeriod).Select(x => x.Rsi.HasValue ? x.Rsi.Value : double.NaN).ToArray();

        var streakQuotes = BuildSyntheticQuotes(_data.SkenderQuotes, streak);
        var streakRsi = streakQuotes.GetRsi(streakPeriod).Select(x => x.Rsi.HasValue ? x.Rsi.Value : double.NaN).ToArray();

        var expected = ComposeCrsi(closeRsi, streakRsi, pctRank);
        var actual = Crsi.Batch(_data.Data, rsiPeriod, streakPeriod, rankPeriod);

        ValidationHelper.VerifyData(actual, expected, x => x, skip: 200, tolerance: ValidationHelper.SkenderTolerance);
        _output.WriteLine("CRSI validated against Skender structural composite (RSI + RSI(streak) + %Rank).");
    }

    [Fact]
    public void Validate_Talib_StructuralComposite()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        double[] close = _data.ClosePrices.ToArray();
        double[] streak = ComputeStreak(close);
        double[] pctRank = ComputePercentRank(close, rankPeriod);

        var closeRsi = ComputeTalibRsiFull(close, rsiPeriod);
        var streakRsi = ComputeTalibRsiFull(streak, streakPeriod);

        var expected = ComposeCrsi(closeRsi, streakRsi, pctRank);
        var actual = Crsi.Batch(_data.Data, rsiPeriod, streakPeriod, rankPeriod);

        ValidationHelper.VerifyData(actual, expected, x => x, skip: 200, tolerance: ValidationHelper.TalibTolerance);
        _output.WriteLine("CRSI validated against TA-Lib structural composite (RSI + RSI(streak) + %Rank).");
    }

    [Fact]
    public void Validate_Tulip_StructuralComposite()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        double[] close = _data.ClosePrices.ToArray();
        double[] streak = ComputeStreak(close);
        double[] pctRank = ComputePercentRank(close, rankPeriod);

        var closeRsi = ComputeTulipRsiFull(close, rsiPeriod);
        var streakRsi = ComputeTulipRsiFull(streak, streakPeriod);

        var expected = ComposeCrsi(closeRsi, streakRsi, pctRank);
        var actual = Crsi.Batch(_data.Data, rsiPeriod, streakPeriod, rankPeriod);

        ValidationHelper.VerifyData(actual, expected, x => x, skip: 200, tolerance: ValidationHelper.TulipTolerance);
        _output.WriteLine("CRSI validated against Tulip structural composite (RSI + RSI(streak) + %Rank).");
    }

    [Fact]
    public void Validate_Ooples_StructuralComposite()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        double[] close = _data.ClosePrices.ToArray();
        double[] streak = ComputeStreak(close);
        double[] pctRank = ComputePercentRank(close, rankPeriod);

        var closeRsi = ComputeOoplesRsiFull(BuildOoplesTickerData(close), rsiPeriod);
        var streakRsi = ComputeOoplesRsiFull(BuildOoplesTickerData(streak), streakPeriod);

        var expected = ComposeCrsi(closeRsi, streakRsi, pctRank);
        var actual = Crsi.Batch(_data.Data, rsiPeriod, streakPeriod, rankPeriod);

        ValidationHelper.VerifyData(actual, expected, x => x, skip: 200, tolerance: ValidationHelper.OoplesTolerance);
        _output.WriteLine("CRSI validated against Ooples structural composite (RSI + RSI(streak) + %Rank).");
    }

    [Fact]
    public void Validate_Ooples_NativeConnorsRsi_Batch()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        var ooplesData = BuildOoplesTickerData(_data.ClosePrices.ToArray());
        var expected = ComputeOoplesConnorsRsiFull(ooplesData, rsiPeriod, streakPeriod, rankPeriod);

        TSeries actual = Crsi.Batch(_data.Data, rsiPeriod, streakPeriod, rankPeriod);
        AssertOoplesNativeComparable(actual.Values.ToArray(), expected, "batch");

        _output.WriteLine("CRSI batch structurally validated against Ooples native CalculateConnorsRelativeStrengthIndex.");
    }

    [Fact]
    public void Validate_Ooples_NativeConnorsRsi_Streaming()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        var ooplesData = BuildOoplesTickerData(_data.ClosePrices.ToArray());
        var expected = ComputeOoplesConnorsRsiFull(ooplesData, rsiPeriod, streakPeriod, rankPeriod);

        var crsi = new Crsi(rsiPeriod, streakPeriod, rankPeriod);
        var streamVals = new double[_data.Data.Count];
        for (int i = 0; i < _data.Data.Count; i++)
        {
            streamVals[i] = crsi.Update(_data.Data[i]).Value;
        }

        AssertOoplesNativeComparable(streamVals, expected, "streaming");
        _output.WriteLine("CRSI streaming structurally validated against Ooples native CalculateConnorsRelativeStrengthIndex.");
    }

    [Fact]
    public void Validate_Ooples_NativeConnorsRsi_Span()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        var ooplesData = BuildOoplesTickerData(_data.ClosePrices.ToArray());
        var expected = ComputeOoplesConnorsRsiFull(ooplesData, rsiPeriod, streakPeriod, rankPeriod);

        var spanOut = new double[_data.Data.Count];
        Crsi.Batch(_data.Data.Values, spanOut, rsiPeriod, streakPeriod, rankPeriod);

        AssertOoplesNativeComparable(spanOut, expected, "span");
        _output.WriteLine("CRSI span structurally validated against Ooples native CalculateConnorsRelativeStrengthIndex.");
    }

    [Fact]
    public void Validate_Skender_NativeConnorsRsi_Batch()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        var skenderResults = _data.SkenderQuotes
            .GetConnorsRsi(rsiPeriod, streakPeriod, rankPeriod)
            .ToList();

        TSeries actual = Crsi.Batch(_data.Data, rsiPeriod, streakPeriod, rankPeriod);

        ValidationHelper.VerifyData(
            actual,
            skenderResults,
            x => x.ConnorsRsi,
            skip: 200,
            tolerance: ValidationHelper.SkenderTolerance);

        _output.WriteLine("CRSI batch validated against Skender native GetConnorsRsi.");
    }

    [Fact]
    public void Validate_Skender_NativeConnorsRsi_Streaming()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        var skenderResults = _data.SkenderQuotes
            .GetConnorsRsi(rsiPeriod, streakPeriod, rankPeriod)
            .ToList();

        var crsi = new Crsi(rsiPeriod, streakPeriod, rankPeriod);
        var streamVals = new double[_data.Data.Count];
        for (int i = 0; i < _data.Data.Count; i++)
        {
            streamVals[i] = crsi.Update(_data.Data[i]).Value;
        }

        int count = _data.Data.Count;
        int start = Math.Max(0, count - 200);
        for (int i = start; i < count; i++)
        {
            double? expected = skenderResults[i].ConnorsRsi;
            if (!expected.HasValue)
            {
                continue;
            }

            Assert.True(
                Math.Abs(streamVals[i] - expected.Value) <= ValidationHelper.SkenderTolerance,
                $"Streaming mismatch at i={i}: QuanTAlib={streamVals[i]:G17}, Skender={expected.Value:G17}");
        }

        _output.WriteLine("CRSI streaming validated against Skender native GetConnorsRsi.");
    }

    [Fact]
    public void Validate_Skender_NativeConnorsRsi_Span()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        var skenderResults = _data.SkenderQuotes
            .GetConnorsRsi(rsiPeriod, streakPeriod, rankPeriod)
            .ToList();

        var spanOut = new double[_data.Data.Count];
        Crsi.Batch(_data.Data.Values, spanOut, rsiPeriod, streakPeriod, rankPeriod);

        ValidationHelper.VerifyData(
            spanOut,
            skenderResults,
            x => x.ConnorsRsi,
            skip: 200,
            tolerance: ValidationHelper.SkenderTolerance);

        _output.WriteLine("CRSI span validated against Skender native GetConnorsRsi.");
    }

    [Fact]
    public void Validate_Skender_NativeConnorsRsi_Components()
    {
        const int rsiPeriod = 3;
        const int streakPeriod = 2;
        const int rankPeriod = 100;

        var skenderResults = _data.SkenderQuotes
            .GetConnorsRsi(rsiPeriod, streakPeriod, rankPeriod)
            .ToList();

        // Verify all 3 sub-components are populated for converged bars
        int count = _data.Data.Count;
        int start = Math.Max(0, count - 100);
        for (int i = start; i < count; i++)
        {
            var r = skenderResults[i];
            Assert.True(r.Rsi.HasValue, $"Skender Rsi null at {i}");
            Assert.True(r.RsiStreak.HasValue, $"Skender RsiStreak null at {i}");
            Assert.True(r.PercentRank.HasValue, $"Skender PercentRank null at {i}");
            Assert.True(r.ConnorsRsi.HasValue, $"Skender ConnorsRsi null at {i}");
            Assert.InRange(r.ConnorsRsi!.Value, 0.0, 100.0);
        }

        _output.WriteLine("Skender ConnorsRsi components all present and in [0,100] for converged bars.");
    }

    private static double[] ComputeStreak(ReadOnlySpan<double> close)
    {
        int n = close.Length;
        var streak = new double[n];

        int s = 0;
        streak[0] = 0.0;
        for (int i = 1; i < n; i++)
        {
            if (close[i] > close[i - 1])
            {
                s = s >= 0 ? s + 1 : 1;
            }
            else if (close[i] < close[i - 1])
            {
                s = s <= 0 ? s - 1 : -1;
            }
            else
            {
                s = 0;
            }

            streak[i] = s;
        }

        return streak;
    }

    private static double[] ComputePercentRank(ReadOnlySpan<double> close, int rankPeriod)
    {
        int n = close.Length;
        var pct = new double[n];

        var rocBuf = new double[rankPeriod];
        int head = 0;
        int count = 0;
        double prev = double.NaN;

        for (int i = 0; i < n; i++)
        {
            double roc = 0.0;
            if (!double.IsNaN(prev) && prev != 0.0)
            {
                roc = (close[i] - prev) / prev * 100.0;
            }

            prev = close[i];

            // Scan BEFORE writing current roc (compare against historical values only)
            int lessCount = 0;
            for (int j = 0; j < count; j++)
            {
                if (rocBuf[j] < roc)
                {
                    lessCount++;
                }
            }

            pct[i] = count > 0 ? (double)lessCount / count * 100.0 : 50.0;

            // Store current ROC after rank scan
            rocBuf[head] = roc;
            head = (head + 1) % rankPeriod;
            if (count < rankPeriod)
            {
                count++;
            }
        }

        return pct;
    }

    private static double[] ComposeCrsi(double[] priceRsi, double[] streakRsi, double[] pctRank)
    {
        int n = priceRsi.Length;
        var result = new double[n];

        for (int i = 0; i < n; i++)
        {
            double a = priceRsi[i];
            double b = streakRsi[i];
            double c = pctRank[i];

            if (!double.IsFinite(a) || !double.IsFinite(b) || !double.IsFinite(c))
            {
                result[i] = double.NaN;
                continue;
            }

            double v = (a + b + c) / 3.0;
            result[i] = Math.Clamp(v, 0.0, 100.0);
        }

        return result;
    }

    private void AssertOoplesNativeComparable(double[] actual, double[] expected, string mode)
    {
        int count = Math.Min(actual.Length, expected.Length);
        int start = Math.Max(0, count - 300);

        var a = new List<double>(300);
        var b = new List<double>(300);

        for (int i = start; i < count; i++)
        {
            double x = actual[i];
            double y = expected[i];

            if (double.IsFinite(x) && double.IsFinite(y))
            {
                Assert.InRange(x, 0.0, 100.0);
                Assert.InRange(y, 0.0, 100.0);
                a.Add(x);
                b.Add(y);
            }
        }

        Assert.True(a.Count >= 150, $"Insufficient overlapping finite values for Ooples {mode} validation.");

        double mae = 0.0;
        for (int i = 0; i < a.Count; i++)
        {
            mae += Math.Abs(a[i] - b[i]);
        }

        mae /= a.Count;

        Assert.True(
            mae <= 20.0,
            $"Ooples {mode} MAE too large for structural agreement: {mae:G17}");
        _output.WriteLine($"CRSI {mode} vs Ooples native: finite={a.Count}, MAE={mae:G6}");
    }

    private static Quote[] BuildSyntheticQuotes(IReadOnlyList<Quote> baseQuotes, double[] values)
    {
        var quotes = new Quote[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            decimal v = (decimal)values[i];
            quotes[i] = new Quote
            {
                Date = baseQuotes[i].Date,
                Open = v,
                High = v,
                Low = v,
                Close = v,
                Volume = baseQuotes[i].Volume
            };
        }

        return quotes;
    }

    private static double[] ComputeTalibRsiFull(double[] input, int period)
    {
        var output = new double[input.Length];
        var ret = TALib.Functions.Rsi<double>(input, 0..^0, output, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, ret);

        var full = Enumerable.Repeat(double.NaN, input.Length).ToArray();
        var (offset, length) = outRange.GetOffsetAndLength(output.Length);
        for (int i = 0; i < length && (offset + i) < full.Length; i++)
        {
            full[offset + i] = output[i];
        }

        return full;
    }

    private static double[] ComputeTulipRsiFull(double[] input, int period)
    {
        var indicator = Tulip.Indicators.rsi;
        double[][] inputs = { input };
        double[] options = { period };

        int lookback = indicator.Start(options);
        double[][] outputs = { new double[input.Length - lookback] };
        indicator.Run(inputs, options, outputs);

        var full = Enumerable.Repeat(double.NaN, input.Length).ToArray();
        var rsi = outputs[0];
        for (int i = 0; i < rsi.Length; i++)
        {
            full[i + lookback] = rsi[i];
        }

        return full;
    }

    private static double[] ComputeOoplesConnorsRsiFull(List<TickerData> data, int rsiPeriod, int streakPeriod, int rankPeriod)
    {
        var stockData = new StockData(data);

        // Ooples uses extension methods declared on static Calculations class.
        var method = typeof(Calculations).GetMethods()
            .FirstOrDefault(m =>
                string.Equals(m.Name, "CalculateConnorsRelativeStrengthIndex", StringComparison.Ordinal) &&
                m.GetParameters().Length > 0 &&
                m.GetParameters()[0].ParameterType == typeof(StockData));

        Assert.NotNull(method);

        var parameters = method!.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = stockData; // extension target

        int idx = 0;
        int[] periods = [rsiPeriod, streakPeriod, rankPeriod];

        for (int i = 1; i < parameters.Length; i++)
        {
            var p = parameters[i];

            if ((p.ParameterType == typeof(int) || p.ParameterType == typeof(int?)) && idx < periods.Length)
            {
                args[i] = periods[idx++];
            }
            else if (p.HasDefaultValue)
            {
                args[i] = p.DefaultValue;
            }
            else
            {
                args[i] = Type.Missing;
            }
        }

        var result = method.Invoke(null, args) as StockData;
        Assert.NotNull(result);

        var outputValues = result!.OutputValues as System.Collections.IDictionary;
        Assert.NotNull(outputValues);
        Assert.NotEmpty(outputValues!.Keys);

        object? firstSeries = outputValues.Values.Cast<object?>().FirstOrDefault(v => v is IEnumerable<double>);
        Assert.NotNull(firstSeries);

        return ((IEnumerable<double>)firstSeries!).ToArray();
    }

    private static List<TickerData> BuildOoplesTickerData(double[] values)
    {
        var list = new List<TickerData>(values.Length);
        var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < values.Length; i++)
        {
            double v = values[i];
            list.Add(new TickerData
            {
                Date = start.AddMinutes(i),
                Open = v,
                High = v,
                Low = v,
                Close = v,
                Volume = 1.0
            });
        }

        return list;
    }

    private static double[] ComputeOoplesRsiFull(List<TickerData> data, int period)
    {
        var stockData = new StockData(data);
        var result = stockData.CalculateRelativeStrengthIndex(length: period);
        return result.OutputValues.Values.First().ToArray();
    }
}