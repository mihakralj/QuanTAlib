using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MfiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 500, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Mfi _mfi = null!;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => Period;

    public override string ShortName => $"MFI({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/mfi/Mfi.Quantower.cs";

    public MfiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "MFI - Money Flow Index";
        Description = "Money Flow Index is a volume-weighted RSI that measures buying and selling pressure";

        _series = new LineSeries(name: "MFI", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _mfi = new Mfi(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _mfi.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _mfi.IsHot, ShowColdValues);
    }
}