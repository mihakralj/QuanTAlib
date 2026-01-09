using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class BopIndicator : Indicator, IWatchlistIndicator
{
    private Bop? _bop;
    private readonly LineSeries? _bopSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "BOP";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/bop/Bop.Quantower.cs";

    public BopIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "BOP - Balance of Power";
        Description = "Measures the strength of buyers vs sellers";

        _bopSeries = new LineSeries(name: "BOP", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_bopSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _bop = new Bop();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _bop!.Update(this.GetInputBar(args), args.IsNewBar());

        _bopSeries!.SetValue(result.Value);
    }
}
