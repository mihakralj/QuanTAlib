using System;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MovingAverageSlope_chart : Indicator {
    #region Parameters
    [InputParameter("MA1: Type:", 0, variants: new object[]
        { "SMA", 0, "EMA", 1,  "WMA", 2,  "T3", 3,  "SMMA", 4,  "TRIMA", 5, "DWMA", 6,  "FWMA", 7,  "DEMA", 8,  "TEMA", 9,
            "ALMA", 10, "HMA", 11,  "HEMA", 12,  "MAMA", 13, "KAMA", 14, "ZLEMA", 15,  "JMA", 16})]
    private int MA1type = 16;

    [InputParameter("MA1: Smoothing period:", 1, 1, 999, 1, 1)]
    private int MA1Period = 10;

    [InputParameter("MA1: Data source:", 2, variants: new object[]
        { "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
            "OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
    private int MA1DataSource = 3;

    [InputParameter("MA2: Type:", 3, variants: new object[]
    { "SMA", 0, "EMA", 1,  "WMA", 2,  "T3", 3,  "SMMA", 4,  "TRIMA", 5, "DWMA", 6,  "FWMA", 7,  "DEMA", 8,  "TEMA", 9,
            "ALMA", 10, "HMA", 11,  "HEMA", 12,  "MAMA", 13, "KAMA", 14, "ZLEMA", 15,  "JMA", 16})]
    private int MA2type = 6;

    [InputParameter("MA2: Smoothing period:", 4, 1, 999, 1, 1)]
    private int MA2Period = 50;

    [InputParameter("MA2: Data source:", 5, variants: new object[]
        { "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
            "OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
    private int MA2DataSource = 8;

    [InputParameter("Data required for slope calc:", 6, 2, 10, 1, 1)]
    private int SlopePeriod = 3;

    [InputParameter("Long trades", 7)]
    private bool LongTrades = true;

    [InputParameter("Short trades", 8)]
    private bool ShortTrades;

    #endregion Parameters

    protected HistoricalData History;
    private TBars bars;

    ///////
    private TSeries MA1, MA2;
    private SLOPE_Series sMA1, sMA2;
    private CROSS_Series sig1, sig2;

    private bool inLong, inShort;
    ///////

    public MovingAverageSlope_chart() {
        this.SeparateWindow = false;
        this.Name = "Slopes convergence";
        this.AddLineSeries("MA1", Color.DarkSlateGray, 2, LineStyle.Solid);
        this.AddLineSeries("MA2", Color.DarkSlateGray, 2, LineStyle.Solid);
    }

    protected override void OnInit() {
        this.bars = new();
        this.History = this.Symbol.GetHistory(period: this.HistoricalData.Period, fromTime: HistoricalData.FromTime);
        for (int i = this.History.Count - 1; i >= 0; i--) {
            var rec = this.History[i, SeekOriginHistory.Begin];
            bars.Add(rec.TimeLeft, rec[PriceType.Open],
            rec[PriceType.High], rec[PriceType.Low],
            rec[PriceType.Close], rec[PriceType.Volume]);
        }
        this.Name = "Slopes convergence: [ ";
        switch (MA1type) {
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

        switch (MA2type) {
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

        sMA1 = new(MA1, SlopePeriod);
        sMA2 = new(MA2, SlopePeriod);
        sig1 = new(sMA1, 0);
        sig2 = new(sMA2, 0);

        int maxKeep = Math.Max(Math.Max(this.MA1Period, this.MA2Period), 100);

        MA1.Keep = maxKeep;
        MA2.Keep = maxKeep;
        sMA1.Keep = maxKeep;
        sMA2.Keep = maxKeep;
        sig1.Keep = maxKeep;
        sig2.Keep = maxKeep;
    }

    protected override void OnUpdate(UpdateArgs args) {
        bool update = !(args.Reason == UpdateReason.NewBar ||
                                        args.Reason == UpdateReason.HistoricalBar);
        this.bars.Add(this.Time(), this.Open(), this.High(), this.Low(), this.Close(), this.Volume(), update);
        this.SetValue(this.MA1[^1].v, lineIndex: 0);
        this.SetValue(this.MA2[^1].v, lineIndex: 1);

        Color s1Color = (this.sMA1[^1].v > 0) ? Color.LimeGreen : Color.OrangeRed;
        Color s2Color = (this.sMA2[^1].v > 0) ? Color.LimeGreen : Color.OrangeRed;

        this.LinesSeries[0].SetMarker(0, s1Color);
        this.LinesSeries[1].SetMarker(0, s2Color);

        if (sig1[^1].v > 0 || sig2[^1].v > 0) {
            if (sMA1[^1].v >= 0 && sMA2[^1].v >= 0 && LongTrades) {
                inLong = true;
                this.BeginCloud(0, 1, Color.FromArgb(127, Color.DarkGreen));
                this.LinesSeries[(this.MA1[^1].v < this.MA2[^1].v) ? 0 : 1].SetMarker(0, new IndicatorLineMarker(Color.LimeGreen, bottomIcon: IndicatorLineMarkerIconType.UpArrow));
            } else {
                this.EndCloud(0, 1, Color.Empty);
                if (inShort && this.Count > 1) {
                    this.LinesSeries[(this.MA1[^1].v < this.MA2[^1].v) ? 1 : 0].SetMarker(1, new IndicatorLineMarker(Color.OrangeRed, upperIcon: IndicatorLineMarkerIconType.DownArrow));
                    inShort = false;
                }
            }
        }

        if (sig1[^1].v < 0 || sig2[^1].v < 0) {
            if (sMA1[^1].v <= 0 && sMA2[^1].v <= 0 && ShortTrades) {
                inShort = true;
                this.BeginCloud(0, 1, Color.FromArgb(100, Color.Red));
                this.LinesSeries[(this.MA1[^1].v > this.MA2[^1].v) ? 0 : 1].SetMarker(0, new IndicatorLineMarker(Color.OrangeRed, upperIcon: IndicatorLineMarkerIconType.UpArrow));
            } else {
                this.EndCloud(0, 1, Color.Empty);
                if (inLong && this.Count > 1) {
                    LinesSeries[(this.MA1[^1].v > this.MA2[^1].v) ? 1 : 0].SetMarker(1, new IndicatorLineMarker(Color.LimeGreen, bottomIcon: IndicatorLineMarkerIconType.DownArrow));
                    inLong = false;
                }
            }
        }
    }
    public override void OnPaintChart(PaintChartEventArgs args) {
        base.OnPaintChart(args);
        if (this.CurrentChart == null) { return; }
        Graphics graphics = args.Graphics;
        var mainWindow = this.CurrentChart.MainWindow;
        int leftIndex = (int)mainWindow.CoordinatesConverter.GetBarIndex(mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Left));
        int rightIndex = (int)Math.Ceiling(mainWindow.CoordinatesConverter.GetBarIndex(mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Right)));
        /*
                int historycount = HistoricalData.Count;
                int ymax = mainWindow.ClientRectangle.Height;


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
