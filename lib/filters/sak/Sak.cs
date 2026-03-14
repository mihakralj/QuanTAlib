using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Sak : AbstractBase
{
    // ── coefficient fields (precomputed, readonly) ─────────────────────────
    private readonly double _c0, _b0, _b1, _b2, _a1, _a2;

    // ── SMA-mode fields ────────────────────────────────────────────────────
    private readonly RingBuffer? _smaBuf;  // null for non-SMA modes
    private readonly double _oneDivN;      // 1/n, precomputed for SMA

    // ── publisher / handler ────────────────────────────────────────────────
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;

    // ── mode flag ──────────────────────────────────────────────────────────
    private readonly bool _isSma;

    // ── scalar state ──────────────────────────────────────────────────────
    //   IIR path: x1=x[t-1], x2=x[t-2], y1=y[t-1], y2=y[t-2]
    //   SMA path: y1 = running sum (replaces the standard y1 slot)
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double X1, double X2,
        double Y1, double Y2,
        double LastValidValue,
        int Count,
        bool IsHot)
    {
        public static State New() => new(0, 0, 0, 0, 0, 0, false);
    }

    private State _state = State.New();
    private State _p_state = State.New();

    // ──────────────────────────────────────────────────────────────────────
    // Constructors
    // ──────────────────────────────────────────────────────────────────────

    public Sak(string filterType = "BP", int period = 20, int n = 10, double delta = 0.1)
    {
        if (n < 1)
        {
            throw new ArgumentException("n must be >= 1", nameof(n));
        }

        string mode = filterType.Trim().ToUpperInvariant();

        // Period validation: SMA only requires n; non-SMA modes need period > 2
        if (!string.Equals(mode, "SMA", StringComparison.Ordinal) && period <= 2)
        {
            throw new ArgumentException("Period must be > 2 for non-SMA modes", nameof(period));
        }

        _isSma = string.Equals(mode, "SMA", StringComparison.Ordinal);

        if (_isSma)
        {
            // SMA special path: coefficients unused; RingBuffer drives computation
            _c0 = 0; _b0 = 0; _b1 = 0; _b2 = 0; _a1 = 0; _a2 = 0;
            _oneDivN = 1.0 / n;
            _smaBuf = new RingBuffer(n);
            Name = $"Sak({filterType},{period})";
            WarmupPeriod = n;
            _handler = Handle;
            return;
        }

        // ── alpha / coefficient derivation ───────────────────────────────
        double theta = 2.0 * Math.PI / period;
        double cosTheta = Math.Cos(theta);
        double sinTheta = Math.Sin(theta);
        double alpha, beta = 0;

        switch (mode)
        {
            case "EMA":
            case "HP":
            case "SMOOTH":
            {
                // Group 1
                alpha = (cosTheta + sinTheta - 1.0) / cosTheta;
                break;
            }

            case "GAUSS":
            case "BUTTER":
            case "2PHP":
            {
                // Group 2
                double betaG = 2.415 * (1.0 - cosTheta);
                alpha = -betaG + Math.Sqrt(Math.FusedMultiplyAdd(betaG, betaG, 2.0 * betaG));
                break;
            }

            case "BP":
            case "BS":
            {
                // Group 3: validate delta/period <= 0.25
                if (delta / period > 0.25)
                {
                    throw new ArgumentException(
                        $"delta/period must be <= 0.25 for BP/BS modes (got {delta / period:G4})",
                        nameof(delta));
                }

                double gamma = 1.0 / Math.Cos(2.0 * Math.PI * delta / period);
                double gammaSquaredMinus1 = Math.FusedMultiplyAdd(gamma, gamma, -1.0);
                if (gammaSquaredMinus1 < 0)
                {
                    throw new ArgumentException(
                        $"BP/BS: gamma^2 - 1 < 0 (delta/period = {delta / period:G4}). Reduce delta.",
                        nameof(delta));
                }

                alpha = gamma - Math.Sqrt(gammaSquaredMinus1);
                beta = cosTheta;   // used in BP/BS coefficient table as β
                break;
            }

            default:
                throw new ArgumentException(
                    $"Unknown filterType '{filterType}'. Valid: EMA, SMA, Gauss, Butter, Smooth, HP, 2PHP, BP, BS",
                    nameof(filterType));
        }

        // ── build coefficient table ───────────────────────────────────────
        double decay = 1.0 - alpha;          // (1-α)
        double decaySq = decay * decay;      // (1-α)²
        double alphaSq = alpha * alpha;      // α²

        switch (mode)
        {
            case "EMA":
                _c0 = 1.0;          _b0 = alpha;  _b1 = 0;               _b2 = 0;
                _a1 = decay;        _a2 = 0;
                break;

            case "GAUSS":
                _c0 = alphaSq;      _b0 = 1;      _b1 = 0;               _b2 = 0;
                _a1 = 2.0 * decay;  _a2 = -decaySq;
                break;

            case "BUTTER":
                _c0 = alphaSq / 4.0; _b0 = 1;    _b1 = 2;               _b2 = 1;
                _a1 = 2.0 * decay;   _a2 = -decaySq;
                break;

            case "SMOOTH":
                _c0 = alphaSq / 4.0; _b0 = 1;    _b1 = 2;               _b2 = 1;
                _a1 = 0;             _a2 = 0;
                break;

            case "HP":
                _c0 = 1.0 - (alpha / 2.0); _b0 = 1; _b1 = -1;            _b2 = 0;
                _a1 = decay;             _a2 = 0;
                break;

            case "2PHP":
            {
                double halfAlpha = alpha / 2.0;
                _c0 = (1.0 - halfAlpha) * (1.0 - halfAlpha);
                _b0 = 1;  _b1 = -2;  _b2 = 1;
                _a1 = 2.0 * decay;  _a2 = -decaySq;
                break;
            }

            case "BP":
                // β (beta) = cos(2π/P) — named 'beta' here, stored in local 'beta'
                _c0 = (1.0 - alpha) / 2.0;
                _b0 = 1;  _b1 = 0;  _b2 = -1;
                _a1 = beta * (1.0 + alpha);  _a2 = -alpha;
                break;

            case "BS":
                _c0 = (1.0 + alpha) / 2.0;
                _b0 = 1;  _b1 = -2.0 * beta;  _b2 = 1;
                _a1 = beta * (1.0 + alpha);  _a2 = -alpha;
                break;
        }

        Name = $"Sak({filterType},{period})";
        WarmupPeriod = 3;  // 2nd-order IIR transient clears after 3 bars
        _oneDivN = 0;
        _handler = Handle;
    }

    public Sak(ITValuePublisher src, string filterType = "BP", int period = 20, int n = 10, double delta = 0.1)
        : this(filterType, period, n, delta)
    {
        _publisher = src;
        src.Pub += _handler;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Event handler
    // ──────────────────────────────────────────────────────────────────────

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    // ──────────────────────────────────────────────────────────────────────
    // Properties
    // ──────────────────────────────────────────────────────────────────────

    public override bool IsHot => _isSma ? (_smaBuf!.IsFull) : _state.IsHot;

    // ──────────────────────────────────────────────────────────────────────
    // Update (TValue) — hot path
    // ──────────────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _smaBuf?.Snapshot();
        }
        else
        {
            _state = _p_state;
            _smaBuf?.Restore();
        }

        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = _state.LastValidValue;
        }
        else
        {
            _state.LastValidValue = val;
        }

        double y;

        if (_isSma)
        {
            // SMA running-sum path — O(1) per bar
            // y[t] = (1/n)*x[t] + y[t-1] - (1/n)*x[t-n]
            double oldest = _smaBuf!.IsFull ? _smaBuf.Oldest : 0.0;
            _smaBuf.Add(val, isNew);
            // _state.Y1 holds the running sum
            y = Math.FusedMultiplyAdd(_oneDivN, val, _state.Y1 - (_oneDivN * oldest));
            _state.Y1 = y;
        }
        else
        {
            // Standard IIR path — use local copy for JIT register promotion
            var s = _state;

            // feedforward: c0 * (b0*x + b1*x1 + b2*x2)
            double ff = _c0 * Math.FusedMultiplyAdd(_b0, val,
                            Math.FusedMultiplyAdd(_b1, s.X1, _b2 * s.X2));

            // feedback: a1*y1 + a2*y2
            double fb = Math.FusedMultiplyAdd(_a1, s.Y1, _a2 * s.Y2);

            y = ff + fb;

            s.X2 = s.X1;
            s.X1 = val;
            s.Y2 = s.Y1;
            s.Y1 = y;

            _state = s;
        }

        if (isNew)
        {
            _state.Count++;
        }

        if (!_state.IsHot && _state.Count >= WarmupPeriod)
        {
            _state.IsHot = true;
        }

        Last = new TValue(input.Time, y);
        PubEvent(Last, isNew);
        return Last;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Batch via TSeries
    // ──────────────────────────────────────────────────────────────────────

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        CalculateCore(sourceValues, vSpan, _c0, _b0, _b1, _b2, _a1, _a2,
                      _isSma, _oneDivN, _smaBuf?.Capacity ?? 0, WarmupPeriod, ref _state, _smaBuf);

        sourceTimes.CopyTo(tSpan);
        _p_state = _state;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Static Calculate (TSeries)
    // ──────────────────────────────────────────────────────────────────────

    public static (TSeries Results, Sak Indicator) Calculate(
        TSeries source, string filterType = "BP", int period = 20, int n = 10, double delta = 0.1)
    {
        var sak = new Sak(filterType, period, n, delta);
        TSeries results = sak.Update(source);
        return (results, sak);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Static Calculate (Span)
    // ──────────────────────────────────────────────────────────────────────

    public static void Calculate(
        ReadOnlySpan<double> src, Span<double> output,
        string filterType = "BP", int period = 20, int n = 10, double delta = 0.1)
    {
        if (src.Length != output.Length)
        {
            throw new ArgumentException("src and output must have the same length", nameof(output));
        }

        if (src.Length == 0)
        {
            return;
        }

        // Build a temporary instance to compute coefficients, then run the core loop
        var tmp = new Sak(filterType, period, n, delta);
        var state = State.New();
        RingBuffer? smaBuf = tmp._isSma ? new RingBuffer(n) : null;

        CalculateCore(src, output, tmp._c0, tmp._b0, tmp._b1, tmp._b2, tmp._a1, tmp._a2,
                      tmp._isSma, tmp._oneDivN, n, tmp.WarmupPeriod, ref state, smaBuf);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CalculateCore — shared by Update(TSeries) and Calculate(Span)
    // ──────────────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateCore(
        ReadOnlySpan<double> source,
        Span<double> output,
        double c0, double b0, double b1, double b2, double a1, double a2,
        bool isSma, double oneDivN, int smaN, int warmupPeriod,
        ref State state,
        RingBuffer? smaBuf)
    {
        int len = source.Length;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                val = state.LastValidValue;
            }
            else
            {
                state.LastValidValue = val;
            }

            double y;

            if (isSma)
            {
                double oldest = (smaBuf != null && smaBuf.IsFull) ? smaBuf.Oldest : 0.0;
                smaBuf?.Add(val);
                y = Math.FusedMultiplyAdd(oneDivN, val, state.Y1 - (oneDivN * oldest));
                state.Y1 = y;
            }
            else
            {
                double ff = c0 * Math.FusedMultiplyAdd(b0, val,
                                Math.FusedMultiplyAdd(b1, state.X1, b2 * state.X2));
                double fb = Math.FusedMultiplyAdd(a1, state.Y1, a2 * state.Y2);
                y = ff + fb;

                state.X2 = state.X1;
                state.X1 = val;
                state.Y2 = state.Y1;
                state.Y1 = y;
            }

            output[i] = y;
            state.Count++;
        }

        if (!state.IsHot && state.Count >= warmupPeriod)
        {
            state.IsHot = true;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Prime
    // ──────────────────────────────────────────────────────────────────────

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }
        Reset();
        foreach (double v in source)
        {
            Update(new TValue(DateTime.UtcNow, v), isNew: true);
        }
        _p_state = _state;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Reset
    // ──────────────────────────────────────────────────────────────────────

    public override void Reset()
    {
        _state = State.New();
        _p_state = State.New();
        _smaBuf?.Clear();
        Last = default;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}
