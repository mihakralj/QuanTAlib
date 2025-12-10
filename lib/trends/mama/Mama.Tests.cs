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

        Assert.True(double.IsNaN(result.Value));
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
}
