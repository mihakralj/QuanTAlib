namespace QuanTAlib.Tests;

public class YzvamaTests
{
    [Fact]
    public void Yzvama_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Yzvama(yzvShortPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Yzvama(yzvLongPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Yzvama(percentileLookback: 0));
        Assert.Throws<ArgumentException>(() => new Yzvama(minLength: 0));
        Assert.Throws<ArgumentException>(() => new Yzvama(maxLength: 0));
        Assert.Throws<ArgumentException>(() => new Yzvama(minLength: 50, maxLength: 10));

        var yzvama = new Yzvama(3, 50, 100, 5, 100);
        Assert.NotNull(yzvama);
    }

    [Fact]
    public void Yzvama_Calc_ReturnsValue()
    {
        var yzvama = new Yzvama();

        Assert.Equal(0, yzvama.Last.Value);

        TValue result = yzvama.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, yzvama.Last.Value);
    }

    [Fact]
    public void Yzvama_Calc_IsNew_False_UpdatesValue()
    {
        var yzvama = new Yzvama();

        yzvama.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        yzvama.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = yzvama.Last.Value;

        yzvama.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = yzvama.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Yzvama_Reset_ClearsState()
    {
        var yzvama = new Yzvama();

        yzvama.Update(new TValue(DateTime.UtcNow, 100));
        yzvama.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = yzvama.Last.Value;

        yzvama.Reset();

        Assert.Equal(0, yzvama.Last.Value);

        yzvama.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, yzvama.Last.Value);
        Assert.NotEqual(valueBefore, yzvama.Last.Value);
    }

    [Fact]
    public void Yzvama_IsHot_BecomesTrueWithSufficientData()
    {
        var yzvama = new Yzvama(minLength: 5, maxLength: 50);

        Assert.False(yzvama.IsHot);

        for (int i = 0; i < 5; i++)
        {
            yzvama.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        Assert.True(yzvama.IsHot);
    }

    [Fact]
    public void Yzvama_Update_WithBars_ProducesFiniteValues()
    {
        var yzvama = new Yzvama();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(250, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = yzvama.Update(bar, isNew: true);
            Assert.True(double.IsFinite(result.Value));
        }
    }
}

