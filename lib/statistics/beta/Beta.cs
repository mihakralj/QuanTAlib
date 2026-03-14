using System.Runtime.CompilerServices;
using static System.Math;

namespace QuanTAlib;

/// <summary>
/// Beta Coefficient: Measures the volatility of an asset in relation to the overall market.
/// </summary>
/// <remarks>
/// Beta is calculated as the covariance of the asset's returns and the market's returns,
/// divided by the variance of the market's returns.
///
/// Formula:
/// Beta = Cov(Ra, Rm) / Var(Rm)
///
/// Where:
/// Ra = Return of Asset
/// Rm = Return of Market
///
/// This implementation uses the O(1) slope formula for linear regression of Ra vs Rm
/// with Kahan compensated summation for numerical stability over long streams:
/// Beta = (N * Sum(Ra*Rm) - Sum(Ra) * Sum(Rm)) / (N * Sum(Rm^2) - Sum(Rm)^2)
/// </remarks>
[SkipLocalsInit]
public sealed class Beta : AbstractBase
{
    private readonly RingBuffer _returnsAsset;
    private readonly RingBuffer _returnsMarket;

    private double _prevAsset;
    private double _prevMarket;
    private double _p_prevAsset;
    private double _p_prevMarket;
    private bool _isInitialized;

    private double _sumRa;
    private double _sumRm;
    private double _sumRaRm;
    private double _sumRm2;

    // Kahan compensation terms
    private double _sumRaComp;
    private double _sumRmComp;
    private double _sumRaRmComp;
    private double _sumRm2Comp;

    // Previous compensation state for rollback
    private double _p_sumRaComp;
    private double _p_sumRmComp;
    private double _p_sumRaRmComp;
    private double _p_sumRm2Comp;

    private const double Epsilon = 1e-10;

    public override bool IsHot => _returnsAsset.IsFull;

    public Beta(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _returnsAsset = new RingBuffer(period);
        _returnsMarket = new RingBuffer(period);
        Name = $"Beta({period})";
        WarmupPeriod = period + 1; // Need 1 extra for first return
        _isInitialized = false;
    }

    /// <summary>
    /// Updates the Beta indicator with new asset and market prices.
    /// </summary>
    /// <param name="asset">The asset price (TValue).</param>
    /// <param name="market">The market price (TValue).</param>
    /// <param name="isNew">Whether this is a new bar.</param>
    /// <returns>The calculated Beta value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue asset, TValue market, bool isNew = true)
    {
        if (isNew)
        {
            if (!_isInitialized)
            {
                _prevAsset = asset.Value;
                _prevMarket = market.Value;
                _isInitialized = true;
                return new TValue(asset.Time, 0);
            }

            _p_prevAsset = _prevAsset;
            _p_prevMarket = _prevMarket;
            _p_sumRaComp = _sumRaComp;
            _p_sumRmComp = _sumRmComp;
            _p_sumRaRmComp = _sumRaRmComp;
            _p_sumRm2Comp = _sumRm2Comp;

            // Calculate returns with division-by-zero and NaN/Infinity guards
            double ra, rm;
            if (Abs(_prevAsset) < Epsilon)
            {
                ra = 0;
            }
            else
            {
                ra = (asset.Value - _prevAsset) / _prevAsset;
                if (!double.IsFinite(ra))
                {
                    ra = 0;
                }
            }

            if (Abs(_prevMarket) < Epsilon)
            {
                rm = 0;
            }
            else
            {
                rm = (market.Value - _prevMarket) / _prevMarket;
                if (!double.IsFinite(rm))
                {
                    rm = 0;
                }
            }

            _prevAsset = asset.Value;
            _prevMarket = market.Value;

            // Update buffers and sums
            if (_returnsAsset.IsFull)
            {
                double oldRa = _returnsAsset.Oldest;
                double oldRm = _returnsMarket.Oldest;

                // Kahan subtract old values
                { double y = -oldRa - _sumRaComp; double t = _sumRa + y; _sumRaComp = (t - _sumRa) - y; _sumRa = t; }
                { double y = -oldRm - _sumRmComp; double t = _sumRm + y; _sumRmComp = (t - _sumRm) - y; _sumRm = t; }
                { double y = -(oldRa * oldRm) - _sumRaRmComp; double t = _sumRaRm + y; _sumRaRmComp = (t - _sumRaRm) - y; _sumRaRm = t; }
                { double y = -(oldRm * oldRm) - _sumRm2Comp; double t = _sumRm2 + y; _sumRm2Comp = (t - _sumRm2) - y; _sumRm2 = t; }
            }

            _returnsAsset.Add(ra);
            _returnsMarket.Add(rm);

            // Kahan add new values
            { double y = ra - _sumRaComp; double t = _sumRa + y; _sumRaComp = (t - _sumRa) - y; _sumRa = t; }
            { double y = rm - _sumRmComp; double t = _sumRm + y; _sumRmComp = (t - _sumRm) - y; _sumRm = t; }
            { double y = (ra * rm) - _sumRaRmComp; double t = _sumRaRm + y; _sumRaRmComp = (t - _sumRaRm) - y; _sumRaRm = t; }
            { double y = (rm * rm) - _sumRm2Comp; double t = _sumRm2 + y; _sumRm2Comp = (t - _sumRm2) - y; _sumRm2 = t; }
        }
        else
        {
            if (!_isInitialized)
            {
                _prevAsset = asset.Value;
                _prevMarket = market.Value;
                _isInitialized = true;
                return new TValue(asset.Time, 0);
            }

            if (_returnsAsset.Count == 0)
            {
                _prevAsset = asset.Value;
                _prevMarket = market.Value;
                _p_prevAsset = asset.Value;
                _p_prevMarket = market.Value;
                return new TValue(asset.Time, 0);
            }

            // Restore compensation state
            _sumRaComp = _p_sumRaComp;
            _sumRmComp = _p_sumRmComp;
            _sumRaRmComp = _p_sumRaRmComp;
            _sumRm2Comp = _p_sumRm2Comp;

            double oldRa = _returnsAsset.Newest;
            double oldRm = _returnsMarket.Newest;

            // Calculate new returns with zero-guard for division
            double newRa, newRm;
            if (Abs(_p_prevAsset) < Epsilon)
            {
                newRa = 0;
            }
            else
            {
                newRa = (asset.Value - _p_prevAsset) / _p_prevAsset;
                if (!double.IsFinite(newRa))
                {
                    newRa = 0;
                }
            }

            if (Abs(_p_prevMarket) < Epsilon)
            {
                newRm = 0;
            }
            else
            {
                newRm = (market.Value - _p_prevMarket) / _p_prevMarket;
                if (!double.IsFinite(newRm))
                {
                    newRm = 0;
                }
            }

            _prevAsset = asset.Value;
            _prevMarket = market.Value;

            _returnsAsset.UpdateNewest(newRa);
            _returnsMarket.UpdateNewest(newRm);

            // Kahan subtract old + add new
            { double y = (-oldRa + newRa) - _sumRaComp; double t = _sumRa + y; _sumRaComp = (t - _sumRa) - y; _sumRa = t; }
            { double y = (-oldRm + newRm) - _sumRmComp; double t = _sumRm + y; _sumRmComp = (t - _sumRm) - y; _sumRm = t; }
            { double y = (-(oldRa * oldRm) + (newRa * newRm)) - _sumRaRmComp; double t = _sumRaRm + y; _sumRaRmComp = (t - _sumRaRm) - y; _sumRaRm = t; }
            { double y = (-(oldRm * oldRm) + (newRm * newRm)) - _sumRm2Comp; double t = _sumRm2 + y; _sumRm2Comp = (t - _sumRm2) - y; _sumRm2 = t; }
        }

        double beta = 0;
        int n = _returnsAsset.Count;
        if (n > 0)
        {
            // Use FMA for better numerical stability
            double denominator = FusedMultiplyAdd(n, _sumRm2, -_sumRm * _sumRm);
            if (Abs(denominator) > Epsilon)
            {
                double numerator = FusedMultiplyAdd(n, _sumRaRm, -_sumRa * _sumRm);
                beta = numerator / denominator;
            }
        }

        Last = new TValue(asset.Time, beta);
        PubEvent(Last);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double asset, double market, bool isNew = true)
    {
        var now = DateTime.UtcNow;
        return Update(new TValue(now, asset), new TValue(now, market), isNew);
    }

    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Beta requires two inputs (asset and market). Use Update(asset, market).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Beta requires two inputs (asset and market). Use Update(asset, market).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Beta requires two inputs (asset and market). Use Update(asset, market).");
    }

    public override void Reset()
    {
        _returnsAsset.Clear();
        _returnsMarket.Clear();
        _sumRa = 0;
        _sumRm = 0;
        _sumRaRm = 0;
        _sumRm2 = 0;
        _sumRaComp = 0;
        _sumRmComp = 0;
        _sumRaRmComp = 0;
        _sumRm2Comp = 0;
        _isInitialized = false;
        _prevAsset = 0;
        _prevMarket = 0;
        _p_prevAsset = 0;
        _p_prevMarket = 0;
    }
}
