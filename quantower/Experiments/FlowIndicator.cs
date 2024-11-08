using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class FlowIndicator : Indicator, IWatchlistIndicator
{
    protected string? SourceName;
    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public FlowIndicator()
    {
        Name = "Flow Visualization";
        SeparateWindow = false;
    }

    protected override void OnInit()
    {
        // placeholder
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        // placeholder
    }

#pragma warning disable CA1416 // Validate platform compatibility

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        Graphics gr = args.Graphics;
        gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var mainWindow = this.CurrentChart.Windows[args.WindowIndex];
        var converter = mainWindow.CoordinatesConverter;
        var clientRect = mainWindow.ClientRectangle;
        gr.SetClip(clientRect);
        DateTime leftTime = new[] { converter.GetTime(clientRect.Left), this.HistoricalData.Time(this!.Count - 1) }.Max();
        DateTime rightTime = new[] { converter.GetTime(clientRect.Right), this.HistoricalData.Time(0) }.Min();

        int leftIndex = (int)this.HistoricalData.GetIndexByTime(leftTime.Ticks) + 1;
        int rightIndex = (int)this.HistoricalData.GetIndexByTime(rightTime.Ticks);
        int width = this.CurrentChart.BarsWidth;

        for (int i = rightIndex; i < leftIndex; i++)
        {
            int barX1 = (int)converter.GetChartX(this.HistoricalData.Time(i));
            int barY1 = (int)converter.GetChartY(this.HistoricalData.Open(i));
            int barYHigh = (int)converter.GetChartY(this.HistoricalData.High(i));
            int barYLow = (int)converter.GetChartY(this.HistoricalData.Low(i));
            int barX2 = barX1 + width;
            int barY2 = (int)converter.GetChartY(this.HistoricalData.Close(i));
            using (Brush transparentBrush = new SolidBrush(Color.FromArgb(250, 70, 70, 70)))
            {
                gr.FillRectangle(transparentBrush, barX1, barYHigh - 1, CurrentChart.BarsWidth, Math.Abs(barYLow - barYHigh) + 2);
            }
            using (Brush circ = new SolidBrush(Color.FromArgb(100, 255, 255, 0)))
            {
                int size = 3;
                gr.FillEllipse(circ, barX1 - size, barY1 - size, 2 * size, 2 * size);
                gr.FillEllipse(circ, barX2 - size, barY2 - size, 2 * size, 2 * size);
            }
            using (Pen defaultPen = new(Color.Yellow, 3))
            {
                defaultPen.StartCap = LineCap.Round;
                defaultPen.EndCap = LineCap.Round;
                gr.DrawLine(defaultPen, barX1, barY1, barX2, barY2);
            }
            if (i > 0)
            {
                int barX0 = (int)converter.GetChartX(this.HistoricalData.Time(i - 1));
                int barY0 = (int)converter.GetChartY(this.HistoricalData.Open(i - 1));
                using (Pen dottedPen = new(Color.Yellow, 1))
                {
                    dottedPen.DashStyle = DashStyle.Dot;
                    gr.DrawLine(dottedPen, barX2, barY2, barX0, barY0);
                }
            }
        }
    }
}
