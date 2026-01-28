using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class EfiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 500, 1, 0)]
    public int Period { get; set; } = 13;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Efi _efi = null!;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => Period;

    public override string ShortName => $"EFI({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/efi/Efi.Quantower.cs";

    public EfiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "EFI - Elder's Force Index";
        Description = "Elder's Force Index measures buying and selling pressure by combining price change with volume";

        _series = new LineSeries(name: "EFI", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _efi = new Efi(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _efi.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _efi.IsHot, ShowColdValues);
    }
}