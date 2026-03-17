// Convexity: Beta Convexity (Markowitz Up/Down Beta asymmetry measure)
// Measures the squared difference between Upside Beta and Downside Beta.
// Based on Skender's GetBeta(BetaType.All) implementation.
// Popularized by Harry M. Markowitz in portfolio theory.

using System.Runtime.CompilerServices;
using static System.Math;

namespace QuanTAlib;

/// <summary>
/// Beta Convexity: Measures the asymmetry between upside and downside beta
/// of an asset relative to a market benchmark.
/// </summary>
/// <remarks>
/// Algorithm (5 outputs):
///   1. Standard Beta = Cov(Ra, Rm) / Var(Rm)           — all bars
///   2. BetaUp (β⁺)  = Cov(Ra, Rm) / Var(Rm)           — only market up bars (Rm > 0)
///   3. BetaDown (β⁻) = Cov(Ra, Rm) / Var(Rm)          — only market down bars (Rm &lt; 0)
///   4. Ratio          = β⁺ / β⁻
///   5. Convexity       = (β⁺ - β⁻)²
///
/// Returns are simple percentage returns: R[i] = (P[i] - P[i-1]) / P[i-1]
///
/// Standard Beta uses O(1) Kahan compensated running sums.
/// Up/Down Beta uses O(period) window scan per update (clean, correct for typical periods 20-60).
///
/// Reference: Skender.Stock.Indicators GetBeta() with BetaType.All
/// https://dotnet.stockindicators.dev/indicators/Beta/
/// </remarks>
[SkipLocalsInit]
public sealed class Convexity : AbstractBase
{
    private readonly RingBuffer _returnsAsset;
    private readonly RingBuffer _returnsMarket;

    private double _prevAsset;
    private double _prevMarket;
    private double _p_prevAsset;
    private double _p_prevMarket;
    private bool _isInitialized;

    // O(1) Kahan compensated running sums for standard beta
    private double _sumRa, _sumRm, _sumRaRm, _sumRm2;
    private double _sumRaComp, _sumRmComp, _sumRaRmComp, _sumRm2Comp;

    // Previous compensation state for bar correction (match Beta.cs pattern)
    private double _p_sumRaComp, _p_sumRmComp, _p_sumRaRmComp, _p_sumRm2Comp;

    private const double Epsilon = 1e-10;

    /// <summary>True when the lookback window is fully populated.</summary>
    public override bool IsHot => _returnsAsset.IsFull;

    /// <summary>Lookback period.</summary>
    public int Period => _returnsAsset.Capacity;

    /// <summary>Standard beta coefficient (all bars).</summary>
    public double BetaStd { get; private set; }

    /// <summary>Upside beta — computed from market up bars only (Rm &gt; 0).</summary>
    public double BetaUp { get; private set; }

    /// <summary>Downside beta — computed from market down bars only (Rm &lt; 0).</summary>
    public double BetaDown { get; private set; }

    /// <summary>Beta ratio = BetaUp / BetaDown.</summary>
    public double Ratio { get; private set; }

    /// <summary>Beta convexity = (BetaUp - BetaDown)². Always ≥ 0.</summary>
    public double ConvexityValue { get; private set; }

    /// <param name="period">Lookback period (must be ≥ 2). Institutions use 60 for 5-year monthly data.</param>
    public Convexity(int period = 20)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be at least 2.");
        }

        _returnsAsset = new RingBuffer(period);
        _returnsMarket = new RingBuffer(period);
        Name = $"Convexity({period})";
        WarmupPeriod = period + 1; // Need 1 extra bar for first return
    }

    /// <summary>
    /// Updates with new asset and market prices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue asset, TValue market, bool isNew = true)
    {
        if (isNew)
        {
            return ProcessNewBar(asset, market);
        }
        else
        {
            return ProcessBarCorrection(asset, market);
        }
    }

    /// <summary>
    /// Updates with raw double values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double asset, double market, bool isNew = true)
    {
        var now = DateTime.UtcNow;
        return Update(new TValue(now, asset), new TValue(now, market), isNew);
    }

    /// <inheritdoc />
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Convexity requires two inputs (asset and market). Use Update(asset, market).");
    }

    /// <inheritdoc />
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Convexity requires two inputs (asset and market). Use Batch(assetSeries, marketSeries, period).");
    }

    /// <inheritdoc />
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Convexity requires two inputs (asset and market).");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue ProcessNewBar(TValue asset, TValue market)
    {
        if (!_isInitialized)
        {
            _prevAsset = asset.Value;
            _prevMarket = market.Value;
            _isInitialized = true;
            Last = new TValue(asset.Time, 0);
            PubEvent(Last);
            return Last;
        }

        // Snapshot compensation state for bar correction (match Beta.cs pattern)
        _p_prevAsset = _prevAsset;
        _p_prevMarket = _prevMarket;
        _p_sumRaComp = _sumRaComp;
        _p_sumRmComp = _sumRmComp;
        _p_sumRaRmComp = _sumRaRmComp;
        _p_sumRm2Comp = _sumRm2Comp;

        // Calculate returns
        double ra = ComputeReturn(asset.Value, _prevAsset);
        double rm = ComputeReturn(market.Value, _prevMarket);
        _prevAsset = asset.Value;
        _prevMarket = market.Value;

        // Evict oldest from running sums if buffer full
        if (_returnsAsset.IsFull)
        {
            double oldRa = _returnsAsset.Oldest;
            double oldRm = _returnsMarket.Oldest;
            KahanSubtract(ref _sumRa, ref _sumRaComp, oldRa);
            KahanSubtract(ref _sumRm, ref _sumRmComp, oldRm);
            KahanSubtract(ref _sumRaRm, ref _sumRaRmComp, oldRa * oldRm);
            KahanSubtract(ref _sumRm2, ref _sumRm2Comp, oldRm * oldRm);
        }

        _returnsAsset.Add(ra);
        _returnsMarket.Add(rm);

        // Add new to running sums
        KahanAdd(ref _sumRa, ref _sumRaComp, ra);
        KahanAdd(ref _sumRm, ref _sumRmComp, rm);
        KahanAdd(ref _sumRaRm, ref _sumRaRmComp, ra * rm);
        KahanAdd(ref _sumRm2, ref _sumRm2Comp, rm * rm);

        ComputeOutputs(asset.Time);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue ProcessBarCorrection(TValue asset, TValue market)
    {
        if (!_isInitialized)
        {
            _prevAsset = asset.Value;
            _prevMarket = market.Value;
            _isInitialized = true;
            Last = new TValue(asset.Time, 0);
            PubEvent(Last, false);
            return Last;
        }

        if (_returnsAsset.Count == 0)
        {
            _prevAsset = asset.Value;
            _prevMarket = market.Value;
            _p_prevAsset = asset.Value;
            _p_prevMarket = market.Value;
            Last = new TValue(asset.Time, 0);
            PubEvent(Last, false);
            return Last;
        }

        // Restore only compensation state (match Beta.cs pattern)
        // Sums already contain the old bar's values — delta will swap them
        _sumRaComp = _p_sumRaComp;
        _sumRmComp = _p_sumRmComp;
        _sumRaRmComp = _p_sumRaRmComp;
        _sumRm2Comp = _p_sumRm2Comp;

        double oldRa = _returnsAsset.Newest;
        double oldRm = _returnsMarket.Newest;

        // Calculate new returns from restored previous prices
        double newRa = ComputeReturn(asset.Value, _p_prevAsset);
        double newRm = ComputeReturn(market.Value, _p_prevMarket);
        _prevAsset = asset.Value;
        _prevMarket = market.Value;

        _returnsAsset.UpdateNewest(newRa);
        _returnsMarket.UpdateNewest(newRm);

        // Kahan delta update: subtract old + add new (sums still contain old values)
        KahanDelta(ref _sumRa, ref _sumRaComp, oldRa, newRa);
        KahanDelta(ref _sumRm, ref _sumRmComp, oldRm, newRm);
        KahanDelta(ref _sumRaRm, ref _sumRaRmComp, oldRa * oldRm, newRa * newRm);
        KahanDelta(ref _sumRm2, ref _sumRm2Comp, oldRm * oldRm, newRm * newRm);

        ComputeOutputs(asset.Time);
        return Last;
    }

    /// <summary>
    /// Computes all 5 outputs from current state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputeOutputs(long time)
    {
        int n = _returnsAsset.Count;
        if (n < 2)
        {
            BetaStd = 0;
            BetaUp = 0;
            BetaDown = 0;
            Ratio = 0;
            ConvexityValue = 0;
            Last = new TValue(time, 0);
            PubEvent(Last);
            return;
        }

        // Standard Beta — O(1) from running sums
        BetaStd = ComputeBetaFromSums(n, _sumRa, _sumRm, _sumRaRm, _sumRm2);

        // Up/Down Beta — O(period) scan
        ComputeFilteredBetas();

        // Derived outputs
        if (Abs(BetaDown) > Epsilon)
        {
            Ratio = BetaUp / BetaDown;
        }
        else
        {
            Ratio = 0;
        }

        double diff = BetaUp - BetaDown;
        ConvexityValue = diff * diff;

        Last = new TValue(time, ConvexityValue);
        PubEvent(Last);
    }

    /// <summary>
    /// Computes beta from Kahan running sums using FMA.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeBetaFromSums(int n, double sumRa, double sumRm, double sumRaRm, double sumRm2)
    {
        // Beta = (N * Σ(Ra*Rm) - ΣRa * ΣRm) / (N * Σ(Rm²) - (ΣRm)²)
        double denom = FusedMultiplyAdd(n, sumRm2, -sumRm * sumRm);
        if (Abs(denom) <= Epsilon)
        {
            return 0;
        }

        double numer = FusedMultiplyAdd(n, sumRaRm, -sumRa * sumRm);
        return numer / denom;
    }

    /// <summary>
    /// Scans ring buffers to compute Up Beta and Down Beta.
    /// O(period) per call — clean and correct for typical lookback windows.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputeFilteredBetas()
    {
        int n = _returnsAsset.Count;

        double sumRaUp = 0, sumRmUp = 0, sumRaRmUp = 0, sumRm2Up = 0;
        double sumRaDn = 0, sumRmDn = 0, sumRaRmDn = 0, sumRm2Dn = 0;
        int countUp = 0, countDn = 0;

        for (int i = 0; i < n; i++)
        {
            double ra = _returnsAsset[i];
            double rm = _returnsMarket[i];

            if (rm > 0)
            {
                sumRaUp += ra;
                sumRmUp += rm;
                sumRaRmUp += ra * rm;
                sumRm2Up += rm * rm;
                countUp++;
            }
            else if (rm < 0)
            {
                sumRaDn += ra;
                sumRmDn += rm;
                sumRaRmDn += ra * rm;
                sumRm2Dn += rm * rm;
                countDn++;
            }
            // rm == 0 bars excluded from both (same as Skender)
        }

        BetaUp = countUp >= 2
            ? ComputeBetaFromSums(countUp, sumRaUp, sumRmUp, sumRaRmUp, sumRm2Up)
            : 0;

        BetaDown = countDn >= 2
            ? ComputeBetaFromSums(countDn, sumRaDn, sumRmDn, sumRaRmDn, sumRm2Dn)
            : 0;
    }

    /// <summary>
    /// Computes simple return with division-by-zero and NaN/Infinity guards.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeReturn(double current, double previous)
    {
        if (Abs(previous) < Epsilon)
        {
            return 0;
        }

        double r = (current - previous) / previous;
        return double.IsFinite(r) ? r : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void KahanAdd(ref double sum, ref double comp, double value)
    {
        double y = value - comp;
        double t = sum + y;
        comp = (t - sum) - y;
        sum = t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void KahanSubtract(ref double sum, ref double comp, double value)
    {
        double y = -value - comp;
        double t = sum + y;
        comp = (t - sum) - y;
        sum = t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void KahanDelta(ref double sum, ref double comp, double oldVal, double newVal)
    {
        double y = (newVal - oldVal) - comp;
        double t = sum + y;
        comp = (t - sum) - y;
        sum = t;
    }

    // --- Static batch API ---

    /// <summary>
    /// Batch computation of Convexity from two price series (TSeries).
    /// Returns tuple of (BetaStd, BetaUp, BetaDown, Ratio, Convexity) series.
    /// </summary>
    public static (TSeries BetaStd, TSeries BetaUp, TSeries BetaDown, TSeries Ratio, TSeries Convexity) Batch(
        TSeries assetPrices, TSeries marketPrices, int period = 20)
    {
        if (assetPrices.Count != marketPrices.Count)
        {
            throw new ArgumentException("Asset and market series must have the same length.", nameof(marketPrices));
        }

        int len = assetPrices.Count;
        var indicator = new Convexity(period);

        var betaStdList = new TSeries(len);
        var betaUpList = new TSeries(len);
        var betaDownList = new TSeries(len);
        var ratioList = new TSeries(len);
        var convexityList = new TSeries(len);

        for (int i = 0; i < len; i++)
        {
            TValue asset = assetPrices[i];
            TValue market = marketPrices[i];
            indicator.Update(asset, market, isNew: true);

            long t = asset.Time;
            betaStdList.Add(new TValue(t, indicator.BetaStd));
            betaUpList.Add(new TValue(t, indicator.BetaUp));
            betaDownList.Add(new TValue(t, indicator.BetaDown));
            ratioList.Add(new TValue(t, indicator.Ratio));
            convexityList.Add(new TValue(t, indicator.ConvexityValue));
        }

        return (betaStdList, betaUpList, betaDownList, ratioList, convexityList);
    }

    /// <summary>
    /// Span-based batch computation for NativeAOT bridge.
    /// Writes 5 output spans: betaStd, betaUp, betaDown, ratio, convexity.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> asset, ReadOnlySpan<double> market,
        Span<double> betaStd, Span<double> betaUp, Span<double> betaDown,
        Span<double> ratio, Span<double> convexity, int period = 20)
    {
        int len = asset.Length;
        var indicator = new Convexity(period);
        for (int i = 0; i < len; i++)
        {
            indicator.Update(asset[i], market[i]);
            betaStd[i] = indicator.BetaStd;
            betaUp[i] = indicator.BetaUp;
            betaDown[i] = indicator.BetaDown;
            ratio[i] = indicator.Ratio;
            convexity[i] = indicator.ConvexityValue;
        }
    }

    public override void Reset()
    {
        _returnsAsset.Clear();
        _returnsMarket.Clear();
        _sumRa = 0; _sumRm = 0; _sumRaRm = 0; _sumRm2 = 0;
        _sumRaComp = 0; _sumRmComp = 0; _sumRaRmComp = 0; _sumRm2Comp = 0;
        _p_sumRaComp = 0; _p_sumRmComp = 0; _p_sumRaRmComp = 0; _p_sumRm2Comp = 0;
        _prevAsset = 0; _prevMarket = 0;
        _p_prevAsset = 0; _p_prevMarket = 0;
        _isInitialized = false;
        BetaStd = 0; BetaUp = 0; BetaDown = 0; Ratio = 0; ConvexityValue = 0;
        Last = default;
    }
}
