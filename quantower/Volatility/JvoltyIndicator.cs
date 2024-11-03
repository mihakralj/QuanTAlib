using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class JvoltyIndicator : Indicator, IWatchlistIndicator
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

    private Jma? jma;
    protected LineSeries? JvoltySeries;
    public static int MinHistoryDepths => 2;


    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public JvoltyIndicator()
    {
        Name = "JVOLTY - Mark Jurik's Volatility";
        Description = "Measures market volatility according to Mark Jurik.";
        SeparateWindow = true;

        JvoltySeries = new("JVOLTY", Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(JvoltySeries);
    }

    protected override void OnInit()
    {
        jma = new(Periods);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        jma!.Calc(input);

        JvoltySeries!.SetValue(jma.Volty);
    }

    public override string ShortName => $"JVOLTY ({Periods})";
}
