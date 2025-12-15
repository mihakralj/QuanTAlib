using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AdxIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adx? _adx;
    protected LineSeries? AdxSeries;
    protected LineSeries? DiPlusSeries;
    protected LineSeries? DiMinusSeries;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ADX {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/adx/Adx.Quantower.cs";

    public AdxIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ADX - Average Directional Index";
        Description = "Measures the strength of a trend";
        
        AdxSeries = new(name: "ADX", color: Color.Blue, width: 2, style: LineStyle.Solid);
        DiPlusSeries = new(name: "+DI", color: Color.Green, width: 1, style: LineStyle.Solid);
        DiMinusSeries = new(name: "-DI", color: Color.Red, width: 1, style: LineStyle.Solid);
        
        AddLineSeries(AdxSeries);
        AddLineSeries(DiPlusSeries);
        AddLineSeries(DiMinusSeries);
    }

    protected override void OnInit()
    {
        _adx = new Adx(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TBar bar = this.GetInputBar(args);

        TValue result = _adx!.Update(bar, isNew);
        
        if (!_adx.IsHot && !ShowColdValues)
        {
            return;
        }

        AdxSeries!.SetValue(result.Value);
        DiPlusSeries!.SetValue(_adx.DiPlus.Value);
        DiMinusSeries!.SetValue(_adx.DiMinus.Value);
    }
}
