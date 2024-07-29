using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class TrailingStop_chart : Indicator
{
    #region Parameters

    [InputParameter("Period", 0, 1, 100, 1, 1)]
    protected int _period = 30;

    [InputParameter("Factor", 1, 1, 100, 0.1, 1)]
    protected double _factor = 10;

    [InputParameter("Long TS", 2)]
    private bool _LongTS = true;

    [InputParameter("Short TS", 3)]
    private bool _ShortTS = true;

    #endregion Parameters

    ///////
    private HistoricalData History;
    private TBars bars;
    private ATR_Series _atr;
    private double _tslineL, _ratchetL, _tslineS, _ratchetS;

    ///////

    public TrailingStop_chart()
    {
        Name = $"ATR Trailing Stop";
        AddLineSeries(lineName: "TrailingATR Long", lineColor: Color.Yellow, lineWidth: 1, lineStyle: LineStyle.Dot);
        AddLineSeries(lineName: "Ratchet Long", lineColor: Color.Yellow, lineWidth: 3, lineStyle: LineStyle.Solid);

        AddLineSeries(lineName: "TrailingATR Short", lineColor: Color.Yellow, lineWidth: 1, lineStyle: LineStyle.Dot);
        AddLineSeries(lineName: "Ratchet Short", lineColor: Color.Yellow, lineWidth: 3, lineStyle: LineStyle.Solid);

        SeparateWindow = false;
    }


    protected override void OnInit()
    {
        this.Name = $"Trailing Stop (ATR:{_period}, Mult:{_factor:f2})";
        this.bars = new();

        this.History = this.Symbol.GetHistory(period: this.HistoricalData.Period, fromTime: HistoricalData.FromTime);
        for (int i = this.History.Count - 1; i >= 0; i--)
        {
            var rec = this.History[i, SeekOriginHistory.Begin];
            bars.Add(rec.TimeLeft, rec[PriceType.Open],
            rec[PriceType.High], rec[PriceType.Low],
            rec[PriceType.Close], rec[PriceType.Volume]);
        }
        _atr = new(source: bars, _period, useNaN: true);
        _ratchetL = Double.NegativeInfinity;
        _ratchetS = Double.PositiveInfinity;

        this.LinesSeries[0].Visible = _LongTS;
        this.LinesSeries[1].Visible = _LongTS;
        this.LinesSeries[2].Visible = _ShortTS;
        this.LinesSeries[3].Visible = _ShortTS;
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

        _tslineL = bars.High[^1].v - (_factor * _atr[^1].v);
        _ratchetL = Math.Max(_tslineL, _ratchetL);
        if (_ratchetL > bars.Low[^1].v)
        {
            this.LinesSeries[1].SetMarker(0, new IndicatorLineMarker(Color.Yellow, bottomIcon: IndicatorLineMarkerIconType.DownArrow));
            _ratchetL = _tslineL;
        }

        _tslineS = bars.High[^1].v + (_factor * _atr[^1].v);
        _ratchetS = Math.Min(_tslineS, _ratchetS);
        if (_ratchetS < bars.High[^1].v)
        {
            this.LinesSeries[3].SetMarker(0, new IndicatorLineMarker(Color.Yellow, upperIcon: IndicatorLineMarkerIconType.UpArrow));
            _ratchetS = _tslineS;
        }

        this.SetValue(_tslineL, lineIndex: 0);
        this.SetValue(_ratchetL, lineIndex: 1);
        this.SetValue(_tslineS, lineIndex: 2);
        this.SetValue(_ratchetS, lineIndex: 3);
    }
}

