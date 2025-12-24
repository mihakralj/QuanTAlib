using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

#nullable disable
#pragma warning disable CA1416 // Validate platform compatibility

namespace QuanTAlib;

public enum SourceType
{
    Open, High, Low, Close, HL2, OC2, OHL3, HLC3, OHLC4, HLCC4
}

public static class IndicatorExtensions
{
    public static readonly Color Averages = Color.FromArgb(255, 255, 128); // #FFFF80 - Yellow
    public static readonly Color Volume = Color.FromArgb(128, 255, 128); // #80FF80 - Green
    public static readonly Color Volatility = Color.FromArgb(255, 128, 128); // #FF8080 - Red
    public static readonly Color Statistics = Color.FromArgb(128, 128, 255); // #8080FF - Blue
    public static readonly Color Oscillators = Color.FromArgb(255, 128, 255); // #FF80FF - Magenta
    public static readonly Color Momentum = Color.FromArgb(128, 255, 255); // #80FFFF - Cyan
    public static readonly Color Experiments = Color.FromArgb(255, 165, 0); // #FFA500 - Orange

    [AttributeUsage(AttributeTargets.Property)]
    public class DataSourceInputAttribute : InputParameterAttribute
    {
        public DataSourceInputAttribute(string label = "Data source", int sortIndex = 20)
            : base(label, sortIndex, variants: new object[]
            {
                "Open", SourceType.Open,
                "High", SourceType.High,
                "Low", SourceType.Low,
                "Close", SourceType.Close,
                "HL/2 (Median)", SourceType.HL2,
                "OC/2 (Midpoint)", SourceType.OC2,
                "OHL/3 (Mean)", SourceType.OHL3,
                "HLC/3 (Typical)", SourceType.HLC3,
                "OHLC/4 (Average)", SourceType.OHLC4,
                "HLCC/4 (Weighted)", SourceType.HLCC4
            })
        { }
    }

    public static TBar GetInputBar(this Indicator indicator, UpdateArgs args)
    {
        var historicalData = indicator.HistoricalData;
        return new TBar(
            time: historicalData.Time(),
            open: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Open],
            high: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.High],
            low: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Low],
            close: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Close],
            volume: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Volume]
        );
    }

    public static Func<IHistoryItem, double> GetPriceSelector(this SourceType source)
    {
        return source switch
        {
            SourceType.Open => item => item[PriceType.Open],
            SourceType.High => item => item[PriceType.High],
            SourceType.Low => item => item[PriceType.Low],
            SourceType.Close => item => item[PriceType.Close],
            SourceType.HL2 => item => (item[PriceType.High] + item[PriceType.Low]) * 0.5,
            SourceType.OC2 => item => (item[PriceType.Open] + item[PriceType.Close]) * 0.5,
            SourceType.OHL3 => item => (item[PriceType.Open] + item[PriceType.High] + item[PriceType.Low]) * 0.333333333333333333,
            SourceType.HLC3 => item => (item[PriceType.High] + item[PriceType.Low] + item[PriceType.Close]) * 0.333333333333333333,
            SourceType.OHLC4 => item => (item[PriceType.Open] + item[PriceType.High] + item[PriceType.Low] + item[PriceType.Close]) * 0.25,
            SourceType.HLCC4 => item => (item[PriceType.High] + item[PriceType.Low] + item[PriceType.Close] + item[PriceType.Close]) * 0.25,
            _ => item => item[PriceType.Close]
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNewBar(this UpdateArgs args)
    {
        return args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue(this LineSeries series, double value, bool isHot, bool showColdValues)
    {
        if (!showColdValues && !isHot)
        {
            series.SetValue(double.NaN);
            return;
        }
        series.SetValue(value);
    }

    public static Point[] GetSmoothCurvePoints(Indicator indicator, IChartWindowCoordinatesConverter converter, Rectangle clientRect, LineSeries series)
    {
        ArgumentNullException.ThrowIfNull(indicator);
        ArgumentNullException.ThrowIfNull(converter);
        var data = indicator.HistoricalData;
        if (data == null) return Array.Empty<Point>();

        var lastTime = data.Time(data.Count - 1);
        var firstTime = data.Time(0);

        IChartWindowCoordinatesConverter safeConverter = converter!;
        DateTime tLeft = safeConverter.GetTime(clientRect.Left);
        DateTime leftTime = tLeft > lastTime ? tLeft : lastTime;

        DateTime tRight = safeConverter.GetTime(clientRect.Right);
        DateTime rightTime = tRight < firstTime ? tRight : firstTime;

        int leftIndex = (int)data.GetIndexByTime(leftTime.Ticks) + 1;
        int rightIndex = (int)data.GetIndexByTime(rightTime.Ticks);

        int count = leftIndex - rightIndex;
        if (count <= 0) return Array.Empty<Point>();

        Point[] allPoints = new Point[count];

        for (int i = 0; i < count; i++)
        {
            int dataIndex = rightIndex + i;
            int barX = (int)converter.GetChartX(data.Time(dataIndex));
            int barY = (int)converter.GetChartY(series[dataIndex]);
            int halfBarWidth = indicator.CurrentChart.BarsWidth / 2;
            allPoints[i] = new Point(barX + halfBarWidth, barY);
        }
        return allPoints;
    }

    public static void PaintSmoothCurve(this Indicator indicator, PaintChartEventArgs args, LineSeries series, int warmupPeriod, bool showColdValues = true, double tension = 0.5)
    {
        if (!series.Visible || indicator.CurrentChart == null)
            return;

        Graphics gr = args.Graphics;
        gr.SmoothingMode = SmoothingMode.AntiAlias;
        var mainWindow = indicator.CurrentChart.Windows[args.WindowIndex];
        var converter = mainWindow.CoordinatesConverter;
        var clientRect = mainWindow.ClientRectangle;

        gr.SetClip(clientRect);

        var data = indicator.HistoricalData;
        if (data == null) return;

        var lastTime = data.Time(data.Count - 1);
        var firstTime = data.Time(0);

        IChartWindowCoordinatesConverter safeConverter = converter!;
        DateTime tLeft = safeConverter.GetTime(clientRect.Left);
        DateTime leftTime = tLeft > lastTime ? tLeft : lastTime;

        DateTime tRight = safeConverter.GetTime(clientRect.Right);
        DateTime rightTime = tRight < firstTime ? tRight : firstTime;

        int leftIndex = (int)data.GetIndexByTime(leftTime.Ticks) + 1;
        int rightIndex = (int)data.GetIndexByTime(rightTime.Ticks);

        int count = leftIndex - rightIndex;
        if (count <= 0) return;

        // Use ArrayPool to avoid allocations
        Point[] allPoints = System.Buffers.ArrayPool<Point>.Shared.Rent(count);
        try
        {
            int halfBarWidth = indicator.CurrentChart.BarsWidth / 2;
            for (int i = 0; i < count; i++)
            {
                int dataIndex = rightIndex + i;
                int barX = (int)converter.GetChartX(data.Time(dataIndex));
                int barY = (int)converter.GetChartY(series[dataIndex]);
                allPoints[i] = new Point(barX + halfBarWidth, barY);
            }

            if (count > 1)
            {
                using Pen defaultPen = new(series.Color, series.Width) { DashStyle = ConvertLineStyleToDashStyle(series.Style) };
                using Pen coldPen = new(series.Color, series.Width) { DashStyle = DashStyle.Dot };

                int hotCount = (warmupPeriod >= 0) ? (indicator.Count - warmupPeriod - rightIndex) : 0;

                // Draw the hot part
                int hotSegments = Math.Min(hotCount, count - 1);
                if (hotSegments > 0)
                {
                    gr.DrawCurve(defaultPen, allPoints, 0, hotSegments, (float)tension);
                }

                // Draw the cold part
                if (showColdValues)
                {
                    int coldStart = Math.Max(0, hotCount);
                    int coldSegments = (count - 1) - coldStart;

                    if (coldSegments > 0)
                    {
                        gr.DrawCurve(coldPen, allPoints, coldStart, coldSegments, (float)tension);
                    }
                }
            }
        }
        finally
        {
            System.Buffers.ArrayPool<Point>.Shared.Return(allPoints);
        }
    }

    private static DashStyle ConvertLineStyleToDashStyle(LineStyle lineStyle)
    {
        return lineStyle switch
        {
            LineStyle.Solid => DashStyle.Solid,
            LineStyle.Dash => DashStyle.Dash,
            LineStyle.Dot => DashStyle.Dot,
            LineStyle.DashDot => DashStyle.DashDot,
            _ => DashStyle.Solid,
        };
    }
}
