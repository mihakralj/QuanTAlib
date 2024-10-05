using System.Drawing;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using System.Runtime.CompilerServices;
using System.Drawing.Drawing2D;
using QuanTAlib;
using System.Collections;
using TradingPlatform.BusinessLayer.TimeSync;

#pragma warning disable CA1416 // Validate platform compatibility
public abstract class IndicatorBase : Indicator, IWatchlistIndicator
{

    [InputParameter("Data source", sortIndex: 17, variants: [
        "Open", 1,
        "High", 2,
        "Low", 3,
        "Close", 4,
        "HL/2 (Median)", 5,
        "OC/2 (Midpoint)", 6,
        "OHL/3 (Mean)", 7,
        "HLC/3 (Typical)", 8,
        "OHLC/4 (Average)", 9,
        "HLCC/4 (Weighted)", 10
    ])]
    public int Source { get; set; } = 4;

    [InputParameter("Show cold values", sortIndex: 20)]
    public bool ShowColdValues { get; set; } = true;
    public int MinHistoryDepths { get; set; };

    // LineSeries.LineSeries(string, Color, int, LineStyle)'

    protected LineSeries? Series;
    protected string SourceName;
    protected abstract AbstractBase QuanTAlib { get; }

    int IWatchlistIndicator.MinHistoryDepths => 0;

    protected IndicatorBase()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = GetName(Source);
        Series = new(name: $"{Name}", color: Color.Yellow, width: 2, style: LineStyle.Solid);

        AddLineSeries(Series);
    }

    protected virtual void InitIndicator()
    {
        SourceName = GetName(Source);
    }

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

        double price = Source switch
        {
            1 => bar.Open,
            2 => bar.High,
            3 => bar.Low,
            4 => bar.Close,
            5 => bar.HL2,
            6 => bar.OC2,
            7 => bar.OHL3,
            8 => bar.HLC3,
            9 => bar.OHLC4,
            10 => bar.HLCC4,
            _ => bar.Close
        };

        TValue input = new TValue(bar.Time, price, bar.IsNew);
        TValue result = QuanTAlib.Calc(input);
        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent);

    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        List<Point> allPoints = new List<Point>();
        if (CurrentChart == null) { return };

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
            Point point = new Point(barX + halfBarWidth, barY);
            allPoints.Add(point);
        }

        if (allPoints.Count > 1)
        {
            DrawSmoothCombinedCurve(gr, allPoints, this.Count - QuanTAlib.WarmupPeriod - rightIndex);
        }
    }

    private void DrawSmoothCombinedCurve(Graphics gr, List<Point> allPoints, int hotCount)
    {
        if (allPoints.Count < 2) { return };

        using (Pen defaultPen = new(Series!.Color, Series.Width) { DashStyle = ConvertLineStyleToDashStyle(Series.Style) })
        using (Pen coldPen = new(Series!.Color, Series.Width) { DashStyle = DashStyle.Dot })
        {
            // Draw the hot part
            if (hotCount > 0)
            {
                var hotPoints = allPoints.Take(Math.Min(hotCount + 1, allPoints.Count)).ToArray();
                gr.DrawCurve(defaultPen, hotPoints, 0, hotPoints.Length - 1, (float)0.2);
            }

            // Draw the cold part
            if (ShowColdValues && hotCount < allPoints.Count)
            {
                var coldPoints = allPoints.Skip(Math.Max(0, hotCount)).ToArray();
                gr.DrawCurve(coldPen, coldPoints, 0, coldPoints.Length - 1, (float)0.2);
            }
        }
    }
    private DashStyle ConvertLineStyleToDashStyle(LineStyle lineStyle)
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
    protected void DrawText(Graphics gr, string text, Rectangle clientRect)
    {
        Font font = new Font("Inter", 8);
        SizeF textSize = gr.MeasureString(text, font);
        RectangleF textRect = new RectangleF(clientRect.Left + 5,
            clientRect.Bottom - textSize.Height - 10,
            textSize.Width + 10, textSize.Height + 10);
        gr.FillRectangle(SystemBrushes.ControlDarkDark, textRect);
        gr.DrawString(text, font, Brushes.White, new PointF(textRect.X + 6, textRect.Y + 5));
    }
    protected string GetName(int pType)
    {
        return pType switch
        {
            1 => "Open",
            2 => "High",
            3 => "Low",
            4 => "Close",
            5 => "Median",
            6 => "Midpoint",
            7 => "Mean",
            8 => "Typical",
            9 => "Average",
            10 => "Weighted",
            _ => "N/A"
        };
    }

}