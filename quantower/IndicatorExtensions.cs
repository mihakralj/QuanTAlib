using TradingPlatform.BusinessLayer;
using System.Drawing;
using System.Drawing.Drawing2D;

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
    public static readonly Color Averages = Color.FromArgb(255, 255, 128);    // #FFFF80 - Yellow
    public static readonly Color Volume = Color.FromArgb(128, 255, 128);      // #80FF80 - Green
    public static readonly Color Volatility = Color.FromArgb(255, 128, 128);  // #FF8080 - Red
    public static readonly Color Statistics = Color.FromArgb(128, 128, 255);  // #8080FF - Blue
    public static readonly Color Oscillators = Color.FromArgb(255, 128, 255); // #FF80FF - Magenta
    public static readonly Color Momentum = Color.FromArgb(128, 255, 255);    // #80FFFF - Cyan
    public static readonly Color Experiments = Color.FromArgb(255, 165, 0);   // #FFA500 - Orange

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

        TBar bar = new TBar(
            Time: historicalData.Time(),
            Open: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Open],
            High: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.High],
            Low: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Low],
            Close: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Close],
            Volume: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Volume],
            IsNew: args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar
        );

        double price = source switch
        {
            SourceType.Open => bar.Open,
            SourceType.High => bar.High,
            SourceType.Low => bar.Low,
            SourceType.Close => bar.Close,
            SourceType.HL2 => bar.HL2,
            SourceType.OC2 => bar.OC2,
            SourceType.OHL3 => bar.OHL3,
            SourceType.HLC3 => bar.HLC3,
            SourceType.OHLC4 => bar.OHLC4,
            SourceType.HLCC4 => bar.HLCC4,
            _ => bar.Close
        };

        return new TValue(bar.Time, price, bar.IsNew);
    }

    public static TBar GetInputBar(this Indicator indicator, UpdateArgs args)
    {
        var historicalData = indicator.HistoricalData;

        return new TBar(
            Time: historicalData.Time(),
            Open: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Open],
            High: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.High],
            Low: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Low],
            Close: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Close],
            Volume: historicalData[indicator.Count - 1, SeekOriginHistory.Begin][PriceType.Volume],
            IsNew: args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar
        );
    }

#pragma warning disable CA1416 // Validate platform compatibility

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
        int Y = (int)converter.GetChartY(value);
        using (pen)
        {
            gr.DrawLine(pen, new Point(leftX, Y), new Point(rightX, Y));
        }
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
        DateTime leftTime = new[] { converter.GetTime(clientRect.Left), indicator.HistoricalData.Time(indicator!.Count - 1) }.Max();
        DateTime rightTime = new[] { converter.GetTime(clientRect.Right), indicator.HistoricalData.Time(0) }.Min();

        int leftIndex = (int)indicator.HistoricalData.GetIndexByTime(leftTime.Ticks) + 1;
        int rightIndex = (int)indicator.HistoricalData.GetIndexByTime(rightTime.Ticks);

        List<Point> allPoints = new List<Point>();
        for (int i = rightIndex; i < leftIndex; i++)
        {
            int barX = (int)converter.GetChartX(indicator.HistoricalData.Time(i));
            int barY = (int)converter.GetChartY(series[i]);
            int halfBarWidth = indicator.CurrentChart.BarsWidth / 2;
            Point point = new Point(barX + halfBarWidth, barY);
            allPoints.Add(point);
        }

        if (allPoints.Count > 1)
        {
            if (allPoints.Count < 2) return;

            using (Pen defaultPen = new(series.Color, series.Width) { DashStyle = ConvertLineStyleToDashStyle(series.Style) })
            using (Pen coldPen = new(series.Color, series.Width) { DashStyle = DashStyle.Dot })
            {
                int hotCount = indicator.Count - warmupPeriod - rightIndex;
                // Draw the hot part
                if (hotCount > 0)
                {
                    var hotPoints = allPoints.Take(Math.Min(hotCount + 1, allPoints.Count)).ToArray();
                    gr.DrawCurve(defaultPen, hotPoints, 0, hotPoints.Length - 1, (float)tension);
                }

                // Draw the cold part
                if (showColdValues && hotCount < allPoints.Count)
                {
                    var coldPoints = allPoints.Skip(Math.Max(0, hotCount)).ToArray();
                    gr.DrawCurve(coldPen, coldPoints, 0, coldPoints.Length - 1, (float)tension);
                }
            }
        }
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
        DateTime leftTime = new[] { converter.GetTime(clientRect.Left), indicator.HistoricalData.Time(indicator!.Count - 1) }.Max();
        DateTime rightTime = new[] { converter.GetTime(clientRect.Right), indicator.HistoricalData.Time(0) }.Min();
        int leftIndex = (int)indicator.HistoricalData.GetIndexByTime(leftTime.Ticks) + 1;
        int rightIndex = (int)indicator.HistoricalData.GetIndexByTime(rightTime.Ticks);

        for (int i = rightIndex; i < leftIndex; i++)
        {
            int barX = (int)converter.GetChartX(indicator.HistoricalData.Time(i));
            int barY = (int)converter.GetChartY(series[i]);
            int barY0 = (int)converter.GetChartY(0);
            int HistBarWidth = indicator.CurrentChart.BarsWidth - 2;

            if (series[i] > 0)
            {
                using (Brush hist = new SolidBrush(Color.FromArgb(150, 0, 255, 0)))
                {
                    gr.FillRectangle(hist, barX, barY, HistBarWidth, Math.Abs(barY - barY0));
                }
            }
            else
            {
                using (Brush hist = new SolidBrush(Color.FromArgb(150, 255, 0, 0)))
                {
                    gr.FillRectangle(hist, barX, barY0, HistBarWidth, Math.Abs(barY0 - barY));
                }
            }
        }
    }

    public static void DrawText(this Indicator indicator, PaintChartEventArgs args, string text)
    {
        if (indicator.CurrentChart == null)
            return;

        Graphics gr = args.Graphics;
        var clientRect = indicator.CurrentChart.MainWindow.ClientRectangle;

        Font font = new Font("Inter", 8);
        SizeF textSize = gr.MeasureString(text, font);
        RectangleF textRect = new RectangleF(clientRect.Left + 5,
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
