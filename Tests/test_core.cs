using Xunit;

namespace QuanTAlib.Tests;

public class CoreTests
{
    #region CircularBuffer Tests

    [Fact]
    public void CircularBuffer_BasicOperations()
    {
        var buffer = new CircularBuffer(5);

        // Test initial state
        Assert.Equal(5, buffer.Capacity);
        Assert.Equal(0, buffer.Count);

        // Test adding items
        buffer.Add(1.0);
        buffer.Add(2.0);
        Assert.Equal(2, buffer.Count);
        Assert.Equal(1.0, buffer[0]);
        Assert.Equal(2.0, buffer[^1]);

        // Test overflow behavior
        buffer.Add(3.0);
        buffer.Add(4.0);
        buffer.Add(5.0);
        buffer.Add(6.0); // Should remove oldest item (1.0)
        Assert.Equal(5, buffer.Count);
        Assert.Equal(2.0, buffer[0]);
        Assert.Equal(6.0, buffer[^1]);
    }

    [Fact]
    public void CircularBuffer_UpdateBehavior()
    {
        var buffer = new CircularBuffer(3);

        // Add new values
        buffer.Add(1.0, isNew: true);
        buffer.Add(2.0, isNew: true);
        Assert.Equal(2, buffer.Count);

        // Update last value
        buffer.Add(2.5, isNew: false);
        Assert.Equal(2, buffer.Count);
        Assert.Equal(2.5, buffer[^1]);
    }

    [Fact]
    public void CircularBuffer_MinMaxSumAverage()
    {
        var buffer = new CircularBuffer(5);

        buffer.Add(1.0);
        buffer.Add(2.0);
        buffer.Add(3.0);
        buffer.Add(4.0);
        buffer.Add(5.0);

        Assert.Equal(1.0, buffer.Min());
        Assert.Equal(5.0, buffer.Max());
        Assert.Equal(15.0, buffer.Sum());
        Assert.Equal(3.0, buffer.Average());
    }

    [Fact]
    public void CircularBuffer_Enumeration()
    {
        var buffer = new CircularBuffer(3);

        buffer.Add(1.0);
        buffer.Add(2.0);
        buffer.Add(3.0);

        var list = buffer.ToList();
        Assert.Equal(3, list.Count);
        Assert.Equal(1.0, list[0]);
        Assert.Equal(3.0, list[2]);
    }

    #endregion

    #region TBar Tests

    [Fact]
    public void TBar_Construction()
    {
        // Default constructor
        var bar1 = new TBar();
        Assert.Equal(0, bar1.Open);
        Assert.True(bar1.IsNew);

        // Value constructor
        var bar2 = new TBar(10.0);
        Assert.Equal(10.0, bar2.Open);
        Assert.Equal(10.0, bar2.High);
        Assert.Equal(10.0, bar2.Low);
        Assert.Equal(10.0, bar2.Close);

        // Full constructor
        var time = DateTime.Now;
        var bar3 = new TBar(time, 10.0, 12.0, 9.0, 11.0, 1000.0, false);
        Assert.Equal(time, bar3.Time);
        Assert.Equal(10.0, bar3.Open);
        Assert.Equal(12.0, bar3.High);
        Assert.Equal(9.0, bar3.Low);
        Assert.Equal(11.0, bar3.Close);
        Assert.Equal(1000.0, bar3.Volume);
        Assert.False(bar3.IsNew);
    }

    [Fact]
    public void TBar_DerivedValues()
    {
        var bar = new TBar(DateTime.Now, 10.0, 20.0, 5.0, 15.0, 1000.0);

        Assert.Equal(12.5, bar.HL2);  // (20 + 5) / 2
        Assert.Equal(12.5, bar.OC2);  // (10 + 15) / 2
        Assert.Equal(11.67, bar.OHL3, 2);  // (10 + 20 + 5) / 3
        Assert.Equal(13.33, bar.HLC3, 2);  // (20 + 5 + 15) / 3
        Assert.Equal(12.5, bar.OHLC4);  // (10 + 20 + 5 + 15) / 4
        Assert.Equal(13.75, bar.HLCC4);  // (20 + 5 + 15 + 15) / 4
    }

    [Fact]
    public void TBarSeries_Operations()
    {
        var series = new TBarSeries();
        var time = DateTime.Now;
        var bar1 = new TBar(time, 10.0, 12.0, 9.0, 11.0, 1000.0);
        var bar2 = new TBar(time.AddMinutes(1), 11.0, 13.0, 10.0, 12.0, 1100.0);

        // Test adding bars
        series.Add(bar1);
        series.Add(bar2);
        Assert.Equal(2, series.Count);

        // Test updating last bar
        var bar2Update = new TBar(bar2.Time, 11.0, 13.5, 9.5, 12.5, 1200.0, false);
        series.Add(bar2Update);
        Assert.Equal(2, series.Count);
        Assert.Equal(12.5, series.Last.Close);

        // Test derived series
        Assert.Equal(11.0, series.Open.Last.Value);
        Assert.Equal(13.5, series.High.Last.Value);
        Assert.Equal(9.5, series.Low.Last.Value);
        Assert.Equal(12.5, series.Close.Last.Value);
        Assert.Equal(1200.0, series.Volume.Last.Value);
    }

    #endregion

    #region TValue Tests

    [Fact]
    public void TValue_Construction()
    {
        // Default constructor
        var value1 = new TValue();
        Assert.Equal(0, value1.Value);
        Assert.True(value1.IsNew);
        Assert.True(value1.IsHot);

        // Value constructor
        var value2 = new TValue(10.0);
        Assert.Equal(10.0, value2.Value);

        // Full constructor
        var time = DateTime.Now;
        var value3 = new TValue(time, 10.0, false, false);
        Assert.Equal(time, value3.Time);
        Assert.Equal(10.0, value3.Value);
        Assert.False(value3.IsNew);
        Assert.False(value3.IsHot);
    }

    [Fact]
    public void TValue_Conversions()
    {
        var value = new TValue(10.0);

        // Test implicit conversions
        double d = value;
        Assert.Equal(10.0, d);

        DateTime time = value;
        Assert.Equal(value.Time, time);

        // Test implicit conversion from double
        TValue newValue = 20.0;
        Assert.Equal(20.0, newValue.Value);
    }

    [Fact]
    public void TSeries_Operations()
    {
        var series = new TSeries();
        var time = DateTime.Now;

        // Test adding values
        series.Add(time, 10.0);
        series.Add(time.AddMinutes(1), 20.0);
        Assert.Equal(2, series.Count);

        // Test updating last value
        series.Add(new TValue(time.AddMinutes(1), 25.0, false));
        Assert.Equal(2, series.Count);
        Assert.Equal(25.0, series.Last.Value);

        // Test adding range of values
        var values = new[] { 30.0, 40.0, 50.0 };
        foreach (var value in values)
        {
            series.Add(time.AddMinutes(series.Count + 1), value);
        }
        Assert.Equal(5, series.Count);

        // Test conversions
        var doubleList = (List<double>)series;
        Assert.Equal(5, doubleList.Count);
        Assert.Equal(50.0, doubleList[^1]);

        var doubleArray = (double[])series;
        Assert.Equal(5, doubleArray.Length);
        Assert.Equal(50.0, doubleArray[^1]);
    }

    [Fact]
    public void TSeries_EventHandling()
    {
        var series = new TSeries();
        var receivedValues = new List<double>();
        var time = DateTime.Now;

        series.Pub += (object sender, in ValueEventArgs args) => receivedValues.Add(args.Tick.Value);

        series.Add(time, 10.0);
        series.Add(time.AddMinutes(1), 20.0);
        series.Add(time.AddMinutes(2), 30.0);

        Assert.Equal(3, receivedValues.Count);
        Assert.Equal(10.0, receivedValues[0]);
        Assert.Equal(20.0, receivedValues[1]);
        Assert.Equal(30.0, receivedValues[2]);
    }

    #endregion
}
