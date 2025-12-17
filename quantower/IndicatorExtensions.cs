using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using System.Drawing;
using System.Drawing.Drawing2D;

#nullable disable

namespace QuanTAlib;

public enum SourceType
{
    Open, High, Low, Close, HL2, OC2, OHL3, HLC3, OHLC4, HLCC4
}

public enum MaType
{
    Alma, Dema, Dsma, Dwma, Ema, Epma, Frama, Fwma, Gma, Hma, Hwma, Jma, Kama, Maaf, Mgdi, MMa, Pwma, Rema, Rma, Sinema, Sma, Smma, T3, Tema, Trima, Vidya, Wma, Zlema
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

    public static TValue GetInputValue(this Indicator indicator, UpdateArgs args, SourceType source)
    {
        var historicalData = indicator.HistoricalData;
        var item = historicalData[indicator.Count - 1, SeekOriginHistory.Begin];
        double price = item.GetPrice(source);
        return new TValue(item.TimeLeft.Ticks, price);
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

    public static double GetPrice(this IHistoryItem item, SourceType source)
    {
        return source switch
        {
            SourceType.Open => item[PriceType.Open],
            SourceType.High => item[PriceType.High],
            SourceType.Low => item[PriceType.Low],
            SourceType.Close => item[PriceType.Close],
            SourceType.HL2 => (item[PriceType.High] + item[PriceType.Low]) * 0.5,
            SourceType.OC2 => (item[PriceType.Open] + item[PriceType.Close]) * 0.5,
            SourceType.OHL3 => (item[PriceType.Open] + item[PriceType.High] + item[PriceType.Low]) * 0.333333333333333333,
            SourceType.HLC3 => (item[PriceType.High] + item[PriceType.Low] + item[PriceType.Close]) * 0.333333333333333333,
            SourceType.OHLC4 => (item[PriceType.Open] + item[PriceType.High] + item[PriceType.Low] + item[PriceType.Close]) * 0.25,
            SourceType.HLCC4 => (item[PriceType.High] + item[PriceType.Low] + item[PriceType.Close] + item[PriceType.Close]) * 0.25,
            _ => item[PriceType.Close]
        };
    }

    public static void FillValues(this HistoricalData history, Span<double> destination, SourceType source)
    {
        int count = Math.Min(history.Count, destination.Length);

        // Hoist switch to avoid per-iteration branching
        switch (source)
        {
            case SourceType.Open:
                for (int i = 0; i < count; i++) destination[i] = history[i, SeekOriginHistory.Begin][PriceType.Open];
                break;
            case SourceType.High:
                for (int i = 0; i < count; i++) destination[i] = history[i, SeekOriginHistory.Begin][PriceType.High];
                break;
            case SourceType.Low:
                for (int i = 0; i < count; i++) destination[i] = history[i, SeekOriginHistory.Begin][PriceType.Low];
                break;
            case SourceType.Close:
                for (int i = 0; i < count; i++) destination[i] = history[i, SeekOriginHistory.Begin][PriceType.Close];
                break;
            case SourceType.HL2:
                for (int i = 0; i < count; i++)
                {
                    var item = history[i, SeekOriginHistory.Begin];
                    destination[i] = (item[PriceType.High] + item[PriceType.Low]) * 0.5;
                }
                break;
            case SourceType.OC2:
                for (int i = 0; i < count; i++)
                {
                    var item = history[i, SeekOriginHistory.Begin];
                    destination[i] = (item[PriceType.Open] + item[PriceType.Close]) * 0.5;
                }
                break;
            case SourceType.OHL3:
                for (int i = 0; i < count; i++)
                {
                    var item = history[i, SeekOriginHistory.Begin];
                    destination[i] = (item[PriceType.Open] + item[PriceType.High] + item[PriceType.Low]) * 0.333333333333333333;
                }
                break;
            case SourceType.HLC3:
                for (int i = 0; i < count; i++)
                {
                    var item = history[i, SeekOriginHistory.Begin];
                    destination[i] = (item[PriceType.High] + item[PriceType.Low] + item[PriceType.Close]) * 0.333333333333333333;
                }
                break;
            case SourceType.OHLC4:
                for (int i = 0; i < count; i++)
                {
                    var item = history[i, SeekOriginHistory.Begin];
                    destination[i] = (item[PriceType.Open] + item[PriceType.High] + item[PriceType.Low] + item[PriceType.Close]) * 0.25;
                }
                break;
            case SourceType.HLCC4:
                for (int i = 0; i < count; i++)
                {
                    var item = history[i, SeekOriginHistory.Begin];
                    destination[i] = (item[PriceType.High] + item[PriceType.Low] + item[PriceType.Close] + item[PriceType.Close]) * 0.25;
                }
                break;
            default:
                for (int i = 0; i < count; i++) destination[i] = history[i, SeekOriginHistory.Begin][PriceType.Close];
                break;
        }
    }

    public static void SetValues(this LineSeries series, ReadOnlySpan<double> values)
    {
        int count = values.Length;
        for (int i = 0; i < count; i++)
        {
            series.SetValue(values[i], i, SeekOriginHistory.Begin);
        }
    }

#pragma warning disable CA1416 // Validate platform compatibility
    public static int GetHLineY(IChartWindowCoordinatesConverter converter, double value)
    {
        return (int)converter.GetChartY(value);
    }

    public static void PaintHLine(this Indicator indicator, PaintChartEventArgs args, double value, Pen pen)
    {
        if (indicator.CurrentChart == null)
            return;

        Graphics gr = args.Graphics;
        var mainWindow = indicator.CurrentChart.Windows[args.WindowIndex];
        var converter = mainWindow.CoordinatesConverter;
        var clientRect = mainWindow.ClientRectangle;

        gr.SetClip(clientRect);
        int leftX = clientRect.Left;
        int rightX = clientRect.Right;
        int Y = GetHLineY(converter, value);

        using (pen)
        {
            gr.DrawLine(pen, new Point(leftX, Y), new Point(rightX, Y));
        }
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

    public static void PaintSmoothCurve(this Indicator indicator, PaintChartEventArgs args, LineSeries series, int warmupPeriod, bool showColdValues = true, double tension = 0.2)
    {
        if (!series.Visible || indicator.CurrentChart == null)
            return;

        Graphics gr = args.Graphics;
        gr.SmoothingMode = SmoothingMode.AntiAlias;
        var mainWindow = indicator.CurrentChart.Windows[args.WindowIndex];
        var converter = mainWindow.CoordinatesConverter;
        var clientRect = mainWindow.ClientRectangle;

        gr.SetClip(clientRect);

        Point[] allPoints = GetSmoothCurvePoints(indicator, converter, clientRect, series);

        if (allPoints.Length > 1)
        {
            DateTime tRight = converter.GetTime(clientRect.Right);
            DateTime tZero = indicator.HistoricalData.Time(0);
            DateTime rightTime = tRight < tZero ? tRight : tZero;

            int rightIndex = (int)indicator.HistoricalData.GetIndexByTime(rightTime.Ticks);

            using Pen defaultPen = new(series.Color, series.Width) { DashStyle = ConvertLineStyleToDashStyle(series.Style) };
            using Pen coldPen = new(series.Color, series.Width) { DashStyle = DashStyle.Dot };

            int hotCount = (warmupPeriod >= 0) ? (indicator.Count - warmupPeriod - rightIndex) : 0;

            // Draw the hot part
            int hotSegments = Math.Min(hotCount, allPoints.Length - 1);
            if (hotSegments > 0)
            {
                gr.DrawCurve(defaultPen, allPoints, 0, hotSegments, (float)tension);
            }

            // Draw the cold part
            if (showColdValues)
            {
                int coldStart = Math.Max(0, hotCount);
                int coldSegments = (allPoints.Length - 1) - coldStart;

                if (coldSegments > 0)
                {
                    gr.DrawCurve(coldPen, allPoints, coldStart, coldSegments, (float)tension);
                }
            }
        }
    }

    public static void PaintLine(this Indicator indicator, PaintChartEventArgs args, LineSeries series, int warmupPeriod, bool showColdValues = true)
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
                    gr.DrawCurve(defaultPen, allPoints, 0, hotSegments, tension: 0);
                }

                // Draw the cold part
                if (showColdValues)
                {
                    int coldStart = Math.Max(0, hotCount);
                    int coldSegments = (count - 1) - coldStart;

                    if (coldSegments > 0)
                    {
                        gr.DrawCurve(coldPen, allPoints, coldStart, coldSegments, tension: 0);
                    }
                }
            }
        }
        finally
        {
            System.Buffers.ArrayPool<Point>.Shared.Return(allPoints);
        }
    }

    public static List<(Rectangle Rect, Color Color)> GetHistogramRectangles(Indicator indicator, IChartWindowCoordinatesConverter converter, Rectangle clientRect, LineSeries series)
    {
        ArgumentNullException.ThrowIfNull(indicator);
        ArgumentNullException.ThrowIfNull(converter);
        var data = indicator.HistoricalData;
        if (data == null) return new List<(Rectangle, Color)>();

        var lastTime = data.Time(data.Count - 1);
        var firstTime = data.Time(0);

        IChartWindowCoordinatesConverter safeConverter = converter!;
        DateTime tLeft = safeConverter.GetTime(clientRect.Left);
        DateTime leftTime = tLeft > lastTime ? tLeft : lastTime;

        DateTime tRight = safeConverter.GetTime(clientRect.Right);
        DateTime rightTime = tRight < firstTime ? tRight : firstTime;

        int leftIndex = (int)data.GetIndexByTime(leftTime.Ticks) + 1;
        int rightIndex = (int)data.GetIndexByTime(rightTime.Ticks);

        var result = new List<(Rectangle, Color)>();

        for (int i = rightIndex; i < leftIndex; i++)
        {
            int barX = (int)converter.GetChartX(data.Time(i));
            int barY = (int)converter.GetChartY(series[i]);
            int barY0 = (int)converter.GetChartY(0);
            int HistBarWidth = indicator.CurrentChart.BarsWidth - 2;

            if (series[i] > 0)
            {
                result.Add((new Rectangle(barX, barY, HistBarWidth, Math.Abs(barY - barY0)), Color.FromArgb(150, 0, 255, 0)));
            }
            else
            {
                result.Add((new Rectangle(barX, barY0, HistBarWidth, Math.Abs(barY0 - barY)), Color.FromArgb(150, 255, 0, 0)));
            }
        }
        return result;
    }

    public static void PaintHistogram(this Indicator indicator, PaintChartEventArgs args, LineSeries series, int warmupPeriod, bool showColdValues = true)
    {
        if (!series.Visible || indicator.CurrentChart == null)
            return;

        Graphics gr = args.Graphics;
        gr.SmoothingMode = SmoothingMode.AntiAlias;
        var mainWindow = indicator.CurrentChart.Windows[args.WindowIndex];
        var converter = mainWindow.CoordinatesConverter;
        var clientRect = mainWindow.ClientRectangle;

        gr.SetClip(clientRect);

        var rects = GetHistogramRectangles(indicator, converter, clientRect, series);

        foreach (var (rect, color) in rects)
        {
            using Brush hist = new SolidBrush(color);
            gr.FillRectangle(hist, rect);
        }
    }

    public static void DrawText(this Indicator indicator, PaintChartEventArgs args, string text)
    {
        if (indicator.CurrentChart == null)
            return;

        Graphics gr = args.Graphics;
        var clientRect = indicator.CurrentChart.MainWindow.ClientRectangle;
        var font = new Font("Inter", 8);
        SizeF textSize = gr.MeasureString(text, font);
        var textRect = new RectangleF(clientRect.Left + 5,
                                           clientRect.Bottom - textSize.Height - 10,
                                           textSize.Width + 10, textSize.Height + 10);

        gr.FillRectangle(Brushes.DarkBlue, textRect);
        gr.DrawString(text, font, Brushes.White, new PointF(textRect.X + 6, textRect.Y + 5));
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
