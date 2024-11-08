using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class JbandsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 2000, 1, 0)]
    public int Periods { get; set; } = 14;

    [InputParameter("Data source", sortIndex: 5, variants: [
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
       ])]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("vShort", sortIndex: 6, -100, 100, 1, 0)]
    public int Phase { get; set; } = 10;

        private Jma? jmaUp;
        private Jma? jmaLo;
    protected LineSeries? UbSeries;
    protected LineSeries? LbSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public JbandsIndicator()
    {
        Name = "JBANDS - Mark Jurik's Bands";
        Description = "Upper and Lower Bands.";
        SeparateWindow = false;

        UbSeries = new("UB", color: IndicatorExtensions.Volatility, 2, LineStyle.Solid);
        LbSeries = new("LB", color: IndicatorExtensions.Volatility, 2, LineStyle.Solid);
        AddLineSeries(UbSeries);
        AddLineSeries(LbSeries);
    }

    protected override void OnInit()
    {
        jmaUp = new(Periods, phase: Phase);
        jmaLo = new(Periods, phase: Phase);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        jmaUp!.Calc(input.High);
        jmaLo!.Calc(input.Low);

        UbSeries!.SetValue(jmaUp.UpperBand);
        LbSeries!.SetValue(jmaLo.LowerBand);
    }

    public override string ShortName => $"JBands ({Periods}:{Phase})";
}
