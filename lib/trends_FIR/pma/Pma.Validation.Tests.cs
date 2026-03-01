using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class PmaValidationTests : IDisposable
{
    private const int DefaultPeriod = 7;
    private const double ValidationTolerance = 2e-8;
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public PmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _testData.Dispose();
        }
    }

    // PMA has no direct external library equivalent, so we validate
    // by verifying PMA = 2*WMA - DWMA (component consistency)

    [Fact]
    public void Validate_PmaEqualsComponentFormula_Batch()
    {
        int period = DefaultPeriod;
        var source = _testData.Data;

        // Compute WMA and DWMA separately
        var wmaResult = Wma.Batch(source, period);
        var dwmaResult = Dwma.Batch(source, period);

        // Compute PMA
        var pmaResult = Pma.Batch(source, period);

        // PMA = 2*WMA - DWMA
        int count = pmaResult.Count;
        int warmup = (period * 2) - 1;
        for (int i = warmup; i < count; i++)
        {
            double expected = Math.FusedMultiplyAdd(2.0, wmaResult[i].Value, -dwmaResult[i].Value);
            Assert.Equal(expected, pmaResult[i].Value, ValidationTolerance);
        }

        _output.WriteLine($"PMA({period}) component consistency: PASS ({count - warmup} bars validated at {ValidationTolerance})");
    }

    [Fact]
    public void Validate_PmaEqualsComponentFormula_Streaming()
    {
        int period = DefaultPeriod;
        var source = _testData.Data;

        var wma = new Wma(period);
        var dwma = new Dwma(period);
        var pma = new Pma(period);

        int warmup = (period * 2) - 1;
        int validated = 0;

        for (int i = 0; i < source.Count; i++)
        {
            var wmaVal = wma.Update(source[i]);
            var dwmaVal = dwma.Update(source[i]);
            var pmaVal = pma.Update(source[i]);

            if (i >= warmup)
            {
                double expected = Math.FusedMultiplyAdd(2.0, wmaVal.Value, -dwmaVal.Value);
                Assert.Equal(expected, pmaVal.Value, ValidationTolerance);
                validated++;
            }
        }

        _output.WriteLine($"PMA({period}) streaming component consistency: PASS ({validated} bars validated)");
    }

    [Fact]
    public void Validate_PmaEqualsComponentFormula_Span()
    {
        int period = DefaultPeriod;
        var rawData = _testData.RawData;

        var wmaOutput = new double[rawData.Length];
        var dwmaOutput = new double[rawData.Length];
        var pmaOutput = new double[rawData.Length];

        Wma.Batch(rawData.Span, wmaOutput.AsSpan(), period);
        Dwma.Batch(rawData.Span, dwmaOutput.AsSpan(), period);
        Pma.Batch(rawData.Span, pmaOutput.AsSpan(), period);

        int warmup = (period * 2) - 1;
        int validated = 0;

        for (int i = warmup; i < rawData.Length; i++)
        {
            double expected = Math.FusedMultiplyAdd(2.0, wmaOutput[i], -dwmaOutput[i]);
            Assert.Equal(expected, pmaOutput[i], ValidationTolerance);
            validated++;
        }

        _output.WriteLine($"PMA({period}) span component consistency: PASS ({validated} bars validated)");
    }

    [Fact]
    public void Validate_TriggerEqualsComponentFormula_Streaming()
    {
        int period = DefaultPeriod;
        var source = _testData.Data;

        var wma = new Wma(period);
        var dwma = new Dwma(period);
        var pma = new Pma(period);

        int warmup = (period * 2) - 1;
        int validated = 0;

        for (int i = 0; i < source.Count; i++)
        {
            var wmaVal = wma.Update(source[i]);
            var dwmaVal = dwma.Update(source[i]);
            _ = pma.Update(source[i]);

            if (i >= warmup)
            {
                // Trigger = (4*WMA - DWMA) / 3
                double expected = Math.FusedMultiplyAdd(4.0, wmaVal.Value, -dwmaVal.Value) / 3.0;
                Assert.Equal(expected, pma.Trigger.Value, ValidationTolerance);
                validated++;
            }
        }

        _output.WriteLine($"PMA({period}) trigger component consistency: PASS ({validated} bars validated)");
    }

    [Fact]
    public void Validate_TriggerEqualsComponentFormula_Span()
    {
        int period = DefaultPeriod;
        var rawData = _testData.RawData;

        var wmaOutput = new double[rawData.Length];
        var dwmaOutput = new double[rawData.Length];
        var pmaOutput = new double[rawData.Length];
        var triggerOutput = new double[rawData.Length];

        Wma.Batch(rawData.Span, wmaOutput.AsSpan(), period);
        Dwma.Batch(rawData.Span, dwmaOutput.AsSpan(), period);
        Pma.Batch(rawData.Span, pmaOutput.AsSpan(), triggerOutput.AsSpan(), period);

        int warmup = (period * 2) - 1;
        int validated = 0;

        for (int i = warmup; i < rawData.Length; i++)
        {
            double expected = Math.FusedMultiplyAdd(4.0, wmaOutput[i], -dwmaOutput[i]) / 3.0;
            Assert.Equal(expected, triggerOutput[i], ValidationTolerance);
            validated++;
        }

        _output.WriteLine($"PMA({period}) trigger span consistency: PASS ({validated} bars validated)");
    }

    [Fact]
    public void Validate_BatchMatchesStreaming()
    {
        int period = DefaultPeriod;
        var source = _testData.Data;

        var batchResult = Pma.Batch(source, period);
        var pma = new Pma(period);

        for (int i = 0; i < source.Count; i++)
        {
            pma.Update(source[i]);
        }

        Assert.Equal(batchResult.Last.Value, pma.Last.Value, ValidationTolerance);
        _output.WriteLine($"PMA({period}) batch-streaming equivalence: PASS");
    }

    [Fact]
    public void Validate_SpanMatchesStreaming()
    {
        int period = DefaultPeriod;
        var rawData = _testData.RawData;

        var spanOutput = new double[rawData.Length];
        Pma.Batch(rawData.Span, spanOutput.AsSpan(), period);

        var pma = new Pma(period);
        for (int i = 0; i < rawData.Length; i++)
        {
            pma.Update(new TValue(DateTime.MinValue, rawData.Span[i]));
        }

        Assert.Equal(pma.Last.Value, spanOutput[^1], ValidationTolerance);
        _output.WriteLine($"PMA({period}) span-streaming equivalence: PASS");
    }
}
