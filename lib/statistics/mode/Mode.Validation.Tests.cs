namespace QuanTAlib.Validation;

/// <summary>
/// Mode validation tests — self-consistency only.
/// No external library provides rolling mode calculations.
/// </summary>
public sealed class ModeValidationTests
{
    [Fact]
    public void Mode_SelfConsistency_KnownValues()
    {
        // Test with known mode values
        // {1, 2, 2, 3, 3, 3, 4, 4, 4, 4} → mode = 4 (appears 4 times)
        var mode = new Mode(10);
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        mode.Update(new TValue(DateTime.UtcNow, 4));
        mode.Update(new TValue(DateTime.UtcNow, 4));
        mode.Update(new TValue(DateTime.UtcNow, 4));
        var result = mode.Update(new TValue(DateTime.UtcNow, 4));

        Assert.Equal(4, result.Value);
    }

    [Fact]
    public void Mode_BatchAndStreaming_Match()
    {
        // Use data with known repeated values
        double[] data = [10, 20, 20, 30, 30, 30, 40, 20, 20, 20, 10, 10, 30, 30, 30];
        int period = 5;

        // Streaming
        var mode = new Mode(period);
        var streamingResults = new double[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            streamingResults[i] = mode.Update(new TValue(DateTime.UtcNow, data[i])).Value;
        }

        // Batch via spans
        var spanOutput = new double[data.Length];
        Mode.Batch(data.AsSpan(), spanOutput.AsSpan(), period);

        for (int i = 0; i < data.Length; i++)
        {
            if (double.IsNaN(streamingResults[i]))
            {
                Assert.True(double.IsNaN(spanOutput[i]), $"Index {i}: streaming=NaN but span={spanOutput[i]}");
            }
            else
            {
                Assert.Equal(streamingResults[i], spanOutput[i], precision: 10);
            }
        }
    }

    [Fact]
    public void Mode_MatchesWolframAlpha()
    {
        // Wolfram Alpha: mode of {1, 2, 2, 3, 3, 3, 4} = {3}
        var mode = new Mode(7);
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        var result = mode.Update(new TValue(DateTime.UtcNow, 4));

        Assert.Equal(3, result.Value);
    }
}
