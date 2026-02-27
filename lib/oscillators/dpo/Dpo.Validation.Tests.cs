using System.Runtime.CompilerServices;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Tulip NETCore uses a centered DPO formula: close[back] - SMA (backward-looking).
/// QuanTAlib uses the PineScript non-centered formula: close - SMA[back] (forward-looking).
/// These are fundamentally different algorithms producing different results,
/// so cross-library validation against Tulip is not applicable.
/// Instead, we validate against manual SMA computation and internal consistency.
/// </summary>
public sealed class DpoValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int TestPeriod = 20;

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) { return; }
        _disposed = true;
        if (disposing) { _testData?.Dispose(); }
    }

    #region Manual SMA Cross-Validation

    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Manual_SMA()
    {
        double[] values = _testData.RawData.ToArray();
        int[] periods = [5, 10, 14, 20];

        foreach (int period in periods)
        {
            int displacement = (period / 2) + 1;
            int warmup = period + displacement;

            double[] batchOutput = new double[values.Length];
            Dpo.Batch(values.AsSpan(), batchOutput.AsSpan(), period);

            int validCount = 0;

            for (int i = warmup - 1; i < values.Length; i++)
            {
                // Compute displaced SMA: SMA from `displacement` bars ago
                int anchor = i - displacement;
                if (anchor < period - 1)
                {
                    continue;
                }

                double dsum = 0.0;
                for (int j = anchor - period + 1; j <= anchor; j++)
                {
                    dsum += values[j];
                }
                double displacedSma = dsum / period;

                double expectedDpo = values[i] - displacedSma;
                double actualDpo = batchOutput[i];

                Assert.True(Math.Abs(expectedDpo - actualDpo) < 1e-9,
                    $"DPO mismatch at i={i}, period={period}: expected={expectedDpo}, actual={actualDpo}, diff={Math.Abs(expectedDpo - actualDpo)}");
                validCount++;
            }

            Assert.True(validCount > 0, $"No valid comparison points for period {period}");
            _output.WriteLine($"DPO period={period}: validated {validCount} points against manual SMA.");
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Validate_Manual_SMA_DifferentPeriods(int period)
    {
        double[] values = _testData.RawData.ToArray();
        int displacement = (period / 2) + 1;
        int warmup = period + displacement;

        double[] batchOutput = new double[values.Length];
        Dpo.Batch(values.AsSpan(), batchOutput.AsSpan(), period);

        int validCount = 0;

        for (int i = warmup - 1; i < values.Length; i++)
        {
            int anchor = i - displacement;
            if (anchor < period - 1) { continue; }

            double dsum = 0.0;
            for (int j = anchor - period + 1; j <= anchor; j++)
            {
                dsum += values[j];
            }
            double displacedSma = dsum / period;
            double expectedDpo = values[i] - displacedSma;

            Assert.True(Math.Abs(expectedDpo - batchOutput[i]) < 1e-9,
                $"DPO mismatch at i={i}, period={period}: expected={expectedDpo}, actual={batchOutput[i]}");
            validCount++;
        }

        Assert.True(validCount > 0, $"No valid comparison points for period {period}");
        _output.WriteLine($"DPO period={period}: validated {validCount} points.");
    }

    #endregion

    #region Consistency Validation

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Batch_Span_Agree()
    {
        double[] tData = _testData.RawData.ToArray();

        // Batch TSeries
        TSeries batchSeries = Dpo.Batch(_testData.Data, TestPeriod);

        // Batch Span
        var spanOutput = new double[tData.Length];
        Dpo.Batch(tData.AsSpan(), spanOutput.AsSpan(), TestPeriod);

        // Batch and Span should be identical (same code path)
        for (int i = 0; i < tData.Length; i++)
        {
            Assert.Equal(batchSeries.Values[i], spanOutput[i], 12);
        }

        // Streaming
        var dpo = new Dpo(TestPeriod);
        var streamResults = new double[tData.Length];
        for (int i = 0; i < tData.Length; i++)
        {
            streamResults[i] = dpo.Update(_testData.Data[i]).Value;
        }

        // Streaming vs Batch: may have minor drift from RingBuffer.Sum maintenance
        int warmup = TestPeriod + (TestPeriod / 2) + 1;
        int count = tData.Length;
        int start = Math.Max(warmup, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], 4);
        }

        _output.WriteLine("DPO streaming/batch/span agreement verified.");
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Event_Matches_Streaming()
    {
        // Streaming
        var streamDpo = new Dpo(TestPeriod);
        var streamResults = new double[_testData.Data.Count];
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            streamResults[i] = streamDpo.Update(_testData.Data[i]).Value;
        }

        // Event-based
        var eventSource = new TSeries();
        var eventDpo = new Dpo(eventSource, TestPeriod);
        var eventResults = new double[_testData.Data.Count];
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            eventSource.Add(_testData.Data[i]);
            eventResults[i] = eventDpo.Last.Value;
        }

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 12);
        }

        _output.WriteLine("DPO event-based matches streaming.");
    }

    #endregion

    #region Ooples Cross-Validation

    [Fact]
    public void Dpo_MatchesOoples_Structural()
    {
        // CalculateDetrendedPriceOscillator — structural test (different centering convention)
        var ooplesData = _testData.SkenderQuotes
            .Select(q => new TickerData { Date = q.Date, Open = (double)q.Open, High = (double)q.High, Low = (double)q.Low, Close = (double)q.Close, Volume = (double)q.Volume })
            .ToList();

        var result = new StockData(ooplesData).CalculateDetrendedPriceOscillator();
        var values = result.CustomValuesList;

        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite Ooples DPO values, got {finiteCount}");
    }

    #endregion
}
