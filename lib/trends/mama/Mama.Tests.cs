using System;
using Xunit;

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
        mama.Pub += (item) => eventFired = true;

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
        for (int i = 0; i < 25; i++) chunk1.Add(data[i]);
        for (int i = 25; i < 50; i++) chunk2.Add(data[i]);

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
}
