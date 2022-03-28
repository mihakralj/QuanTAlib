using System.Drawing;
using TradingPlatform.BusinessLayer;
using QuantLib;

public class HEMA_chart : Indicator
{
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
    private int Period = 9;

    [InputParameter("Data source", 1, variants: new object[]{
            "Open", 0,
            "High", 1,
            "Low", 2,
            "Close", 3,
            "HL2", 4,
            "OC2", 5,
            "OHL3", 6,
            "HLC3", 7,
            "OHLC4", 8,
            "Weighted (HLCC4)", 9
        })]
    private int DataSource = 3;

    [InputParameter("Debug info", 3)]
    private bool debug;

    #endregion Parameters

    private HistoricalData History;

    private readonly QuantLib.TBars bars = new();
    private QuantLib.HEMA_Series hema;

    public HEMA_chart() : base()
    {
        this.SeparateWindow = false;
        this.Name = "HEMA - Hull-EMA Moving Average";
        this.Description = "Hull-EMA Moving Average description";

        this.AddLineSeries("HEMA", Color.SkyBlue, 2, LineStyle.Solid);
    }

    protected override void OnInit()
    {
        TSeries source = this.GetSource(this.DataSource);

        /////////////////////////////////////////////////////////
        this.hema = new(source: source, period: this.Period, useNaN: true);
        /////////////////////////////////////////////////////////

        this.GetHistory(this.Period, this.bars);
        this.ShortName = "HEMA (" + this.SourceStr(this.DataSource) + ", " + this.Period + ")";
    }

    protected void OnNewData(bool update = false)
    {
        /////////////////////////////////////////////////////////
        this.hema.Add(update);
        /////////////////////////////////////////////////////////
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool update = !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar);
        this.bars.Add(this.Time(), this.GetPrice(PriceType.Open), this.GetPrice(PriceType.High), this.GetPrice(PriceType.Low), this.GetPrice(PriceType.Close), this.GetPrice(PriceType.Volume), update);
        this.OnNewData(update);

        /////////////////////////////////////////////////////////
        double result = this.hema[this.hema.Count - 1].v;
        /////////////////////////////////////////////////////////

        this.SetValue(result);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        Graphics gr = args.Graphics;
        if (this.debug)
        {
            int y = 25;
            gr.FillRectangle(Brushes.Black, 1, y + 25, 170, y + 53);
            gr.DrawRectangle(Pens.DarkGray, 1, y + 25, 170, y + 53);

            gr.DrawString($"History bars: {this.History.Count}", new Font("Tahoma", 10), (this.History.Count >= this.Period) ? Brushes.YellowGreen : Brushes.Red, 3, y + 30);
            gr.DrawString($"Bars on chart: {this.hema.Count - this.History.Count}", new Font("Tahoma", 10), Brushes.Gray, 3, y + 45);
            gr.DrawString($"Last value: {(double)this.hema:f3}", new Font("Tahoma", 10), Brushes.Gray, 3, y + 65);
            gr.DrawString($"Timespan: {(this.hema[this.hema.Count - 1].t - this.hema[this.History.Count - 1].t).ToString()}", new Font("Tahoma", 10), Brushes.Gray, 3, y + 80);
        }
    }
    protected void GetHistory(int HistoryBars, QuantLib.TBars bars)
    {
        for (int i = 1; i < 50; i++)
        {
            this.History = this.Symbol.GetHistory(this.HistoricalData.Period, this.HistoricalData.FromTime.AddSeconds(-this.HistoricalData.Period.Duration.TotalSeconds * (HistoryBars + i)), this.HistoricalData.FromTime);
            if (this.History.Count >= HistoryBars)
            {
                break;
            }
        }
        for (int i = this.History.Count - 1; i >= 0; i--)
        {
            bars.Add(this.History[i].TimeLeft, ((HistoryItemBar)this.History[i]).Open, ((HistoryItemBar)this.History[i]).High, ((HistoryItemBar)this.History[i]).Low, ((HistoryItemBar)this.History[i]).Close, ((HistoryItemBar)this.History[i]).Volume);
            OnNewData(update: false);
        }
    }
    protected QuantLib.TSeries GetSource(int DataSource)
    {
        switch (DataSource)
        {
            case 0: return this.bars.open;
            case 1: return this.bars.high;
            case 2: return this.bars.low;
            case 3: return this.bars.close;
            case 4: return this.bars.hl2;
            case 5: return this.bars.oc2;
            case 6: return this.bars.ohl3;
            case 7: return this.bars.hlc3;
            case 8: return this.bars.ohlc4;
            default: return this.bars.hlcc4;
        }
    }





    private string SourceStr(int DataSource)
    {
        switch (DataSource)
        {
            case 0: return "Open";
            case 1: return "High";
            case 2: return "Low";
            case 3: return "Close";
            case 4: return "HL2";
            case 5: return "OC2";
            case 6: return "OHL3";
            case 7: return "HLC3";
            case 8: return "OHLC4";
            default: return "Weighted";
        }
    }
}
