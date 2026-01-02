
namespace QuanTAlib.Tests;

public class BlmaTests
{
    private readonly GBM _gbm;

    public BlmaTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Blma(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Blma(-1));
    }

    [Fact]
    public void Constructor_ValidatesSource()
    {
        Assert.Throws<NullReferenceException>(() => new Blma(null!, 10));
    }

    [Fact]
    public void BasicCalculation_MatchesManual()
    {
        var blma = new Blma(3);
        var input = new[] { 10.0, 20.0, 30.0 };

        // Bar 1: Count=1. Weights for n=1: [1]. Result = 10.
        var r1 = blma.Update(new TValue(DateTime.UtcNow, input[0]));
        Assert.Equal(10.0, r1.Value);

        // Bar 2: Count=2. Weights for n=2 sum to 0. Fallback to average: (10+20)/2 = 15.
        var r2 = blma.Update(new TValue(DateTime.UtcNow, input[1]));
        Assert.Equal(15.0, r2.Value);

        // Bar 3: Count=3. Weights [0, 1, 0]. Sum=1. Result=20.
        var r3 = blma.Update(new TValue(DateTime.UtcNow, input[2]));
        Assert.Equal(20.0, r3.Value, 1e-6);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 10;
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = new Blma(period).Update(series);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Blma.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Blma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, 1e-9);
        Assert.Equal(expected, streamingResult, 1e-9);
    }

    [Fact]
    public void NaN_Handling()
    {
        var blma = new Blma(5);

        blma.Update(new TValue(DateTime.UtcNow, 10));
        blma.Update(new TValue(DateTime.UtcNow, 20));
        // For N=2, weights sum to 0. Fallback to average: (10+20)/2 = 15.

        var result = blma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.Equal(15.0, result.Value); // Should return last valid value
        Assert.Equal(15.0, blma.Last.Value); // Should retain last valid value
    }

    [Fact]
    public void IsNew_Behavior()
    {
        var blma = new Blma(3);

        // Bar 1
        blma.Update(new TValue(DateTime.UtcNow, 10), isNew: true);

        // Bar 2
        blma.Update(new TValue(DateTime.UtcNow, 20), isNew: true);

        // Bar 3 (Update)
        blma.Update(new TValue(DateTime.UtcNow, 30), isNew: true);
        var val1 = blma.Last.Value;

        // Bar 3 (Correction)
        blma.Update(new TValue(DateTime.UtcNow, 40), isNew: false);
        var val2 = blma.Last.Value;

        // For Blackman window, the newest value (index N-1) has weight 0.
        // So changing the newest value does NOT change the current result.
        Assert.Equal(val1, val2);

        // However, the internal buffer MUST be updated.
        // Case A: Bar 3 = 40 (current state)
        blma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        var valWith40 = blma.Last.Value;

        // Case B: Reconstruct scenario with Bar 3 = 30
        var blma2 = new Blma(3);
        blma2.Update(new TValue(DateTime.UtcNow, 10), isNew: true);
        blma2.Update(new TValue(DateTime.UtcNow, 20), isNew: true);
        blma2.Update(new TValue(DateTime.UtcNow, 30), isNew: true);
        blma2.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        var valWith30 = blma2.Last.Value;

        Assert.NotEqual(valWith30, valWith40);
    }

    [Fact]
    public void Prime_PreservesTimestamps()
    {
        var blma = new Blma(5);
        double[] input = [1, 2, 3, 4, 5];
        var timestamps = new List<DateTime>();

        blma.Pub += (object? sender, in TValueEventArgs args) => timestamps.Add(args.Value.AsDateTime);


        blma.Prime(input);

        Assert.Equal(input.Length, timestamps.Count);
        // Verify timestamps are unique and increasing
        for (int i = 1; i < timestamps.Count; i++)
        {
            Assert.True(timestamps[i] > timestamps[i - 1], $"Timestamp at {i} ({timestamps[i].Ticks}) should be greater than {i - 1} ({timestamps[i - 1].Ticks})");
        }
    }

    [Fact]
    public void Prime_Overload_UsesProvidedTimestamps()
    {
        var blma = new Blma(5);
        var now = DateTime.UtcNow;
        TValue[] input =
        [
            new(now, 1),
            new(now.AddMinutes(1), 2),
            new(now.AddMinutes(2), 3)
        ];
        var timestamps = new List<DateTime>();

        blma.Pub += (object? sender, in TValueEventArgs args) => timestamps.Add(args.Value.AsDateTime);


        blma.Prime(input);

        Assert.Equal(input.Length, timestamps.Count);
        Assert.Equal(input[0].AsDateTime, timestamps[0]);
        Assert.Equal(input[1].AsDateTime, timestamps[1]);
        Assert.Equal(input[2].AsDateTime, timestamps[2]);
    }
}
