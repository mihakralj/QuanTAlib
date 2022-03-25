using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;


public class WMA_chart : Indicator
    {
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
        public int Period = 9;

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
        public int DataSource = 3;
    
    [InputParameter("Debug info", 3)]
        public bool debug = false;

    #endregion Parameters

    private HistoricalData History;

    private QuantLib.TBars bars = new();
    private QuantLib.TSeries source = new();

    private QuantLib.WMA_Series wma;

    public WMA_chart() : base()
    {
        SeparateWindow = false;
        Name = "WMA - Weighted Moving Average";
        Description = "Weighted Moving Average description";

        AddLineSeries("WMA", Color.Yellow, 2, LineStyle.Solid);
    }

    protected override void OnInit()
    {
        source = GetSource(DataSource);

        /////////////////////////////////////////////////////////
        wma = new(source: this.source, period: this.Period, useNaN: true);
        /////////////////////////////////////////////////////////

        GetHistory(Period, bars);
        ShortName = "WMA (" + SourceStr(DataSource) + ", " + Period + ")";
    }

    protected void OnNewData(bool update = false)
    {
        /////////////////////////////////////////////////////////
        wma.Add(update);
        /////////////////////////////////////////////////////////
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool update= !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar);
        bars.Add(Time(), GetPrice(PriceType.Open), GetPrice(PriceType.High), GetPrice(PriceType.Low), GetPrice(PriceType.Close), GetPrice(PriceType.Volume), update);
        OnNewData(update);

        /////////////////////////////////////////////////////////
        double result = wma[wma.Count - 1].v;
        /////////////////////////////////////////////////////////

        SetValue(result);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        Graphics gr = args.Graphics;
        if (debug)
        {
            int y = 25;
            gr.FillRectangle(Brushes.Black, 1, y + 25, 170, y + 53);
            gr.DrawRectangle(Pens.DarkGray, 1, y + 25, 170, y + 53);

            gr.DrawString($"History bars: {History.Count}", new Font("Tahoma", 10), (History.Count >= Period) ? Brushes.YellowGreen : Brushes.Red, 3, y + 30);
            gr.DrawString($"Bars on chart: {wma.Count - History.Count}", new Font("Tahoma", 10), Brushes.Gray, 3, y + 45);
            gr.DrawString($"Last value: {(double)wma:f3}", new Font("Tahoma", 10), Brushes.Gray, 3, y + 65);
            gr.DrawString($"Timespan: {(wma[wma.Count - 1].t - wma[History.Count - 1].t).ToString()}", new Font("Tahoma", 10), Brushes.Gray, 3, y + 80);
        }
    }
    protected void GetHistory(int HistoryBars, QuantLib.TBars bars)
    {
        for (int i = 1; i < 50; i++)
        {
            History = Symbol.GetHistory(HistoricalData.Period, HistoricalData.FromTime.AddSeconds(-HistoricalData.Period.Duration.TotalSeconds * (HistoryBars + i)), HistoricalData.FromTime);
            if (History.Count >= HistoryBars) break;
        }
        for (int i = History.Count - 1; i >= 0; i--)
        {
            bars.Add(History[i].TimeLeft, ((HistoryItemBar)History[i]).Open, ((HistoryItemBar)History[i]).High, ((HistoryItemBar)History[i]).Low, ((HistoryItemBar)History[i]).Close, ((HistoryItemBar)History[i]).Volume);
            OnNewData(update: false);
        }
    }
    protected QuantLib.TSeries GetSource(int DataSource)
    {
        switch (DataSource)
        {
            case 0: return bars.open;
            case 1: return bars.high; 
            case 2: return bars.low; 
            case 3: return bars.close; 
            case 4: return bars.hl2; 
            case 5: return bars.oc2; 
            case 6: return bars.ohl3; 
            case 7: return bars.hlc3; 
            case 8: return bars.ohlc4; 
            default: return bars.hlcc4; 
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
