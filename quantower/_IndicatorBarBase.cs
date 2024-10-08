using System.Drawing;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using System.Runtime.CompilerServices;
using System.Drawing.Drawing2D;
using System.Collections;
using TradingPlatform.BusinessLayer.TimeSync;

namespace QuanTAlib;

#pragma warning disable CA1416 // Validate platform compatibility
public abstract class IndicatorBarBase : Indicator, IWatchlistIndicator
{

    [InputParameter("Show cold values", sortIndex: 20)]
    public bool ShowColdValues { get; set; } = true;
    public int MinHistoryDepths { get; set; }

    // LineSeries.LineSeries(string, Color, int, LineStyle)'

    protected LineSeries? Series;
    protected abstract AbstractBase QuanTAlib { get; }

    int IWatchlistIndicator.MinHistoryDepths => 0;

    protected IndicatorBarBase()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Series = new(name: $"{Name}", color: Color.RoyalBlue, width: 2, style: LineStyle.Solid);

        AddLineSeries(Series);
    }

    protected abstract void InitIndicator();

    protected override void OnInit()
    {
        InitIndicator();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = new(Time: Time(),
            Open: GetPrice(PriceType.Open),
            High: GetPrice(PriceType.High),
            Low: GetPrice(PriceType.Low),
            Close: GetPrice(PriceType.Close),
            Volume: GetPrice(PriceType.Volume),
            IsNew: args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar);

        TValue result = QuanTAlib.Calc(bar);
        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent);

    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        List<Point> allPoints = new List<Point>();
        if (CurrentChart == null) { return; }

        Graphics gr = args.Graphics;

        var mainWindow = this.CurrentChart.Windows[args.WindowIndex];
        var converter = mainWindow.CoordinatesConverter;
        var clientRect = mainWindow.ClientRectangle;

        gr.SetClip(clientRect);
        DateTime leftTime = new[] { converter.GetTime(clientRect.Left), Time(this.Count - 1) }.Max();
        DateTime rightTime = new[] { converter.GetTime(clientRect.Right), Time(0) }.Min();

        int leftIndex = (int)HistoricalData.GetIndexByTime(leftTime.Ticks) + 1;
        int rightIndex = (int)HistoricalData.GetIndexByTime(rightTime.Ticks);

        for (int i = rightIndex; i < leftIndex; i++)
        {
            int barX = (int)converter.GetChartX(Time(i));
            int barY = (int)converter.GetChartY(Series![i]);
            int halfBarWidth = CurrentChart.BarsWidth / 2;
            Point point = new(barX + halfBarWidth, barY);
            allPoints.Add(point);
        }

        if (allPoints.Count > 1)
        {
            DrawSmoothCombinedCurve(gr, allPoints, this.Count - QuanTAlib.WarmupPeriod - rightIndex);
        }
    }

    private void DrawSmoothCombinedCurve(Graphics gr, List<Point> allPoints, int hotCount)
    {
        if (allPoints.Count < 2) { return; }

        using Pen defaultPen = new(Series!.Color, Series.Width) { DashStyle = ConvertLineStyleToDashStyle(Series.Style) };
        using Pen coldPen = new(Series!.Color, Series.Width) { DashStyle = DashStyle.Dot };

        // Draw the hot part
        if (hotCount > 0)
        {
            var hotPoints = allPoints.Take(Math.Min(hotCount + 1, allPoints.Count)).ToArray();
            gr.DrawCurve(defaultPen, hotPoints, 0, hotPoints.Length - 1, (float)0.1);
        }

        // Draw the cold part
        if (ShowColdValues && hotCount < allPoints.Count)
        {
            var coldPoints = allPoints.Skip(Math.Max(0, hotCount)).ToArray();
            gr.DrawCurve(coldPen, coldPoints, 0, coldPoints.Length - 1, (float)0.1);
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
    protected static void DrawText(Graphics gr, string text, Rectangle clientRect)
    {
        Font font = new("Inter", 8);
        SizeF textSize = gr.MeasureString(text, font);
        RectangleF textRect = new(clientRect.Left + 5,
            clientRect.Bottom - textSize.Height - 10,
            textSize.Width + 10, textSize.Height + 10);
        gr.FillRectangle(SystemBrushes.ControlDarkDark, textRect);
        gr.DrawString(text, font, Brushes.White, new PointF(textRect.X + 6, textRect.Y + 5));
    }
}