using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class RsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    private Rsi? _rsi;
    protected LineSeries? RsiSeries;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"RSI({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/rsi/Rsi.Quantower.cs";

    public RsiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "RSI - Relative Strength Index";
        Description = "Measures the speed and change of price movements";

        RsiSeries = new(name: "RSI", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(RsiSeries);
    }

    protected override void OnInit()
    {
        _rsi = new Rsi(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TValue input = this.GetInputValue(args, SourceType.Close);

        TValue result = _rsi!.Update(input, isNew);

        RsiSeries!.SetValue(result.Value);
    }
}
