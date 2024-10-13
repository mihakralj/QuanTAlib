using TradingPlatform.BusinessLayer;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace QuanTAlib;

public enum SourceType
{
    Open, High, Low, Close, HL2, OC2, OHL3, HLC3, OHLC4, HLCC4
}
public static class IndicatorExtensions
{
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

    public static void PaintSmoothCurve(this Indicator indicator, PaintChartEventArgs args, LineSeries series, int warmupPeriod, bool showColdValues = true, double tension = 0.2)
    {
        if (!series.Visible || indicator.CurrentChart == null)
            return;

        Graphics gr = args.Graphics;
        var mainWindow = indicator.CurrentChart.MainWindow;
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




