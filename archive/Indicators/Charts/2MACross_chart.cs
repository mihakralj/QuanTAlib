using System;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MovingAverage_chart : Indicator
{
    #region Parameters
    [InputParameter("MA1: Type:", 0, variants: new object[]
        { "SMA", 0, "EMA", 1,  "WMA", 2,  "T3", 3,  "SMMA", 4,  "TRIMA", 5, "DWMA", 6,  "FWMA", 7,  "DEMA", 8,  "TEMA", 9,
            "ALMA", 10, "HMA", 11,  "HEMA", 12,  "MAMA", 13, "KAMA", 14, "ZLEMA", 15,  "JMA", 16})]
    private int MA1type = 15;

    [InputParameter("MA1: Smoothing period:", 1, 1, 999, 1, 1)]
    private int MA1Period = 10;

    [InputParameter("MA1: Data source:", 2, variants: new object[]
        { "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
            "OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
    private int MA1DataSource = 3;

    [InputParameter("MA2: Type:", 3, variants: new object[]
    { "SMA", 0, "EMA", 1,  "WMA", 2,  "T3", 3,  "SMMA", 4,  "TRIMA", 5, "DWMA", 6,  "FWMA", 7,  "DEMA", 8,  "TEMA", 9,
            "ALMA", 10, "HMA", 11,  "HEMA", 12,  "MAMA", 13, "KAMA", 14, "ZLEMA", 15,  "JMA", 16})]
    private int MA2type = 16;

    [InputParameter("MA2: Smoothing period:", 4, 1, 999, 1, 1)]
    private int MA2Period = 50;

    [InputParameter("MA2: Data source:", 5, variants: new object[]
        { "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
            "OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
    private int MA2DataSource = 8;

    [InputParameter("Long trades", 6)]
    private bool LongTrades = true;

    [InputParameter("Short trades", 6)]
    private bool ShortTrades = true;

    #endregion Parameters

    protected HistoricalData History;
    private TBars bars;

    ///////
    private TSeries MA1, MA2;
    private CROSS_Series trades;
    private COMPARE_Series overunder;

    ///////

    public MovingAverage_chart()
    {
        this.SeparateWindow = false;
        this.Name = "MAs Crossover";
        this.AddLineSeries("MA1", Color.LimeGreen, 2, LineStyle.Solid);
        this.AddLineSeries("MA2", Color.OrangeRed, 2, LineStyle.Solid);
    }

    protected override void OnInit()
    {
        this.bars = new();
        this.History = this.Symbol.GetHistory(period: this.HistoricalData.Period, fromTime: HistoricalData.FromTime);
        for (int i = this.History.Count - 1; i >= 0; i--)
        {
            var rec = this.History[i, SeekOriginHistory.Begin];
            bars.Add(rec.TimeLeft, rec[PriceType.Open],
            rec[PriceType.High], rec[PriceType.Low],
            rec[PriceType.Close], rec[PriceType.Volume]);
        }
        this.Name = "MAs Cross: [ ";
        switch (MA1type)
        {
            case 0:
                MA1 = new SMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"SMA";
                break;
            case 1:
                MA1 = new EMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"EMA";
                break;
            case 2:
                MA1 = new WMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"WMA";
                break;
            case 3:
                MA1 = new T3_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"T3";
                break;
            case 4:
                MA1 = new SMMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"SMMA";
                break;
            case 5:
                MA1 = new TRIMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"TRIMA";
                break;
            case 6:
                MA1 = new DWMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"DWMA";
                break;
            case 7:
                MA1 = new FWMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period);
                this.Name += $"FWMA";
                break;
            case 8:
                MA1 = new DEMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"DEMA";
                break;
            case 9:
                MA1 = new TEMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"TEMA";
                break;
            case 10:
                MA1 = new ALMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"ALMA";
                break;
            case 11:
                MA1 = new HMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"HMA";
                break;
            case 12:
                MA1 = new HEMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"HEMA";
                break;
            case 13:
                double factor = 1.015 * Math.Exp(-0.043 * (double)this.MA1Period);
                MA1 = new MAMA_Series(source: bars.Select(this.MA1DataSource), fastlimit: factor, slowlimit: factor * 0.1, useNaN: false);
                this.Name += $"MAMA";
                break;
            case 14:
                MA1 = new KAMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"KAMA";
                break;
            case 15:
                MA1 = new ZLEMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"ZLEMA";
                break;
            default:
                MA1 = new JMA_Series(source: bars.Select(this.MA1DataSource), period: this.MA1Period, useNaN: false);
                this.Name += $"JMA";
                break;
        }

        this.Name = this.Name + $" ({MA1Period}:{TBars.SelectStr(this.MA1DataSource)}) : ";

        switch (MA2type)
        {
            case 0:
                MA2 = new SMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"SMA";
                break;
            case 1:
                MA2 = new EMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"EMA";
                break;
            case 2:
                MA2 = new WMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"WMA";
                break;
            case 3:
                MA2 = new T3_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"T3";
                break;
            case 4:
                MA2 = new SMMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"SMMA";
                break;
            case 5:
                MA2 = new TRIMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"TRIMA";
                break;
            case 6:
                MA2 = new DWMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"DWMA";
                break;
            case 7:
                MA2 = new FWMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period);
                this.Name += $"FWMA";
                break;
            case 8:
                MA2 = new DEMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"DEMA";
                break;
            case 9:
                MA2 = new TEMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"TEMA";
                break;
            case 10:
                MA2 = new ALMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"ALMA";
                break;
            case 11:
                MA2 = new HMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"HMA";
                break;
            case 12:
                MA2 = new HEMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"HEMA";
                break;
            case 13:
                double factor = 1.015 * Math.Exp(-0.043 * (double)this.MA2Period);
                MA2 = new MAMA_Series(source: bars.Select(this.MA2DataSource), fastlimit: factor, slowlimit: factor * 0.1, useNaN: false);
                this.Name += $"MAMA";
                break;
            case 14:
                MA2 = new KAMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"KAMA";
                break;
            case 15:
                MA2 = new ZLEMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"ZLEMA";
                break;
            default:
                MA2 = new JMA_Series(source: bars.Select(this.MA2DataSource), period: this.MA2Period, useNaN: false);
                this.Name += $"JMA";
                break;
        }
        this.Name += $"({MA2Period}:{TBars.SelectStr(this.MA2DataSource)}) ]";

        int maxKeep = Math.Max(Math.Max(this.MA1Period, this.MA2Period), 100);
        MA1.Keep = maxKeep;
        MA2.Keep = maxKeep;
        trades.Keep = maxKeep;
        overunder.Keep = maxKeep;

        overunder = new(MA1, MA2);
        trades = new(MA1, MA2);
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool update = !(args.Reason == UpdateReason.NewBar ||
                                        args.Reason == UpdateReason.HistoricalBar);
        this.bars.Add(this.Time(), this.GetPrice(PriceType.Open),
                                    this.GetPrice(PriceType.High),
                                    this.GetPrice(PriceType.Low),
                                    this.GetPrice(PriceType.Close),
                                    this.GetPrice(PriceType.Volume), update);
        this.SetValue(this.MA1[^1].v, lineIndex: 0);
        this.SetValue(this.MA2[^1].v, lineIndex: 1);

        if (trades[^1].v == 1)
        {
            this.EndCloud(0, 1, Color.Empty);
            if (LongTrades)
            {
                this.LinesSeries[0].SetMarker(0, new IndicatorLineMarker(Color.LimeGreen, bottomIcon: IndicatorLineMarkerIconType.UpArrow));
                this.BeginCloud(0, 1, Color.FromArgb(127, Color.Green));
            }
            if (ShortTrades)
            {
                this.LinesSeries[1].SetMarker(0, new IndicatorLineMarker(Color.OrangeRed, upperIcon: IndicatorLineMarkerIconType.DownArrow));
            }
        }
        if (trades[^1].v == -1)
        {
            this.EndCloud(0, 1, Color.Empty);
            if (ShortTrades)
            {
                this.LinesSeries[1].SetMarker(0, new IndicatorLineMarker(Color.OrangeRed, upperIcon: IndicatorLineMarkerIconType.UpArrow));
                this.BeginCloud(0, 1, Color.FromArgb(127, Color.Red));
            }
            if (LongTrades)
            {
                this.LinesSeries[0].SetMarker(0, new IndicatorLineMarker(Color.LimeGreen, bottomIcon: IndicatorLineMarkerIconType.DownArrow));
            }
        }
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        if (this.CurrentChart == null) { return; }
        Graphics graphics = args.Graphics;
        var mainWindow = this.CurrentChart.MainWindow;
        int leftIndex = (int)mainWindow.CoordinatesConverter.GetBarIndex(mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Left));
        int rightIndex = (int)Math.Ceiling(mainWindow.CoordinatesConverter.GetBarIndex(mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Right)));
        int historycount = HistoricalData.Count;
        int ymax = mainWindow.ClientRectangle.Height;
        int xmax = mainWindow.ClientRectangle.Width;

        /*
				for (int i = leftIndex; i <= rightIndex; i++) {
					int xi = (int)Math.Round(mainWindow.CoordinatesConverter.GetChartX(Time(Count - 1 - i)));
					int width = this.CurrentChart.BarsWidth;
					int height = (int)((equity[i+historycount].v) *proportion);

					Brush bb = Brushes.DarkSlateGray;
					bb = (overunder[i+historycount].v>0 && LongTrades)? Brushes.Green : bb;
					bb = (overunder[i + historycount].v < 0 && ShortTrades) ? Brushes.Red : bb;

					graphics.FillRectangle(bb, xi, ymax - height, width, height);
				}
		*/
    }
}
