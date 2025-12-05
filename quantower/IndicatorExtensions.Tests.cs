using Xunit;
using TradingPlatform.BusinessLayer;
using System.Drawing;
using System.Reflection;

namespace QuanTAlib.Tests;

public class IndicatorExtensionsTests
{
    private sealed class TestIndicator : Indicator
    {
        public TestIndicator()
        {
            Name = "Test Indicator";
        }
    }

    private sealed class TestCoordinatesConverter : ICoordinatesConverter
    {
        private readonly DateTime _time;
        public TestCoordinatesConverter(DateTime time) => _time = time;

        public DateTime GetTime(int x) => _time;
        public double GetChartX(DateTime time) => 0;
        public double GetChartY(double value) => 0;
    }

    [Fact]
    public void DataSourceInputAttribute_HasCorrectDefaults()
    {
        IndicatorExtensions.DataSourceInputAttribute attr = new();

        Assert.Equal("Data source", attr.Name);
        Assert.Equal(20, attr.SortIndex);
        Assert.NotNull(attr.Variants);
        Assert.NotEmpty(attr.Variants);
    }

    [Fact]
    public void GetInputValue_ReturnsCorrectValues_ForSourceTypes()
    {
        TestIndicator indicator = new();
        DateTime now = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Open=100, High=110, Low=90, Close=105, Volume=1000
        const double open = 100;
        const double high = 110;
        const double low = 90;
        const double close = 105;
        const double volume = 1000;

        indicator.HistoricalData.AddBar(now, open, high, low, close, volume);

        // Ensure Count is updated (mock implementation detail)
        // The mock HistoricalData.Count reflects added items.
        // Indicator.Count => HistoricalData.Count.

        UpdateArgs args = new(UpdateReason.NewBar);

        // Test each SourceType
        Assert.Equal(open, IndicatorExtensions.GetInputValue(indicator, args, SourceType.Open).Value);
        Assert.Equal(high, IndicatorExtensions.GetInputValue(indicator, args, SourceType.High).Value);
        Assert.Equal(low, IndicatorExtensions.GetInputValue(indicator, args, SourceType.Low).Value);
        Assert.Equal(close, IndicatorExtensions.GetInputValue(indicator, args, SourceType.Close).Value);

        // HL2 = (110 + 90) / 2 = 100
        Assert.Equal(100, IndicatorExtensions.GetInputValue(indicator, args, SourceType.HL2).Value);

        // OC2 = (100 + 105) / 2 = 102.5
        Assert.Equal(102.5, IndicatorExtensions.GetInputValue(indicator, args, SourceType.OC2).Value);

        // OHL3 = (100 + 110 + 90) / 3 = 100
        Assert.Equal(100, IndicatorExtensions.GetInputValue(indicator, args, SourceType.OHL3).Value);

        // HLC3 = (110 + 90 + 105) / 3 = 101.666...
        Assert.Equal(101.66666666666667, IndicatorExtensions.GetInputValue(indicator, args, SourceType.HLC3).Value, 5);

        // OHLC4 = (100 + 110 + 90 + 105) / 4 = 101.25
        Assert.Equal(101.25, IndicatorExtensions.GetInputValue(indicator, args, SourceType.OHLC4).Value);

        // HLCC4 = (110 + 90 + 105 + 105) / 4 = 102.5
        Assert.Equal(102.5, IndicatorExtensions.GetInputValue(indicator, args, SourceType.HLCC4).Value);
    }

    [Fact]
    public void GetInputBar_ReturnsCorrectBar()
    {
        TestIndicator indicator = new();
        DateTime now = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        const double open = 100;
        const double high = 110;
        const double low = 90;
        const double close = 105;
        const double volume = 1000;

        indicator.HistoricalData.AddBar(now, open, high, low, close, volume);
        UpdateArgs args = new(UpdateReason.NewBar);

        var bar = IndicatorExtensions.GetInputBar(indicator, args);

        Assert.Equal(now, bar.AsDateTime);
        Assert.Equal(open, bar.Open);
        Assert.Equal(high, bar.High);
        Assert.Equal(low, bar.Low);
        Assert.Equal(close, bar.Close);
        Assert.Equal(volume, bar.Volume);
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void PaintMethods_DoNotThrow_WithValidGraphics()
    {
        // This test attempts to verify that paint methods don't crash.
        // It requires System.Drawing.Common to be functional.
        
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            // Skip on non-Windows if System.Drawing is not fully supported (GDI+)
            return; 
        }

        using var bitmap = new Bitmap(100, 100);
        using var graphics = Graphics.FromImage(bitmap);
        
        var indicator = new TestIndicator();
        indicator.CurrentChart = new MockChart();
        
        // Add some data
        var now = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105);
        }

        // Setup converter to return a time that exists in our data (e.g. the middle bar)
        // We added bars at now, now+1min, ..., now+19min.
        // Let's return now+10min.
        var validTime = now.AddMinutes(10);
        indicator.CurrentChart.MainWindow.CoordinatesConverter = new TestCoordinatesConverter(validTime);

        var args = new PaintChartEventArgs(graphics, new Rectangle(0, 0, 100, 100));
        using var pen = new Pen(Color.Red);
        
        // Test PaintHLine
        IndicatorExtensions.PaintHLine(indicator, args, 100, pen);

        // Test PaintSmoothCurve
        var series = new LineSeries("Test", Color.Blue, 1, LineStyle.Solid);
        for (int i = 0; i < 20; i++) series.AddValue(); // Fill with NaNs or values
        for (int i = 0; i < 20; i++) series.SetValue(100 + i, i); // Set some values
        
        IndicatorExtensions.PaintSmoothCurve(indicator, args, series, 0);

        // Test PaintHistogram
        IndicatorExtensions.PaintHistogram(indicator, args, series, 0);

        // Test DrawText
        IndicatorExtensions.DrawText(indicator, args, "Test Text");

        // Assert that we reached the end without throwing
        // If we got here, no exception was thrown
    }
}
