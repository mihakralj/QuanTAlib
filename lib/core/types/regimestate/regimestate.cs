namespace QuanTAlib;

/// <summary>
/// Directional axis of a <c>regimes/</c> classifier. Orthogonal to <see cref="VolState"/>: a
/// market can trend while volatility compresses, and the squeeze-release setup is exactly
/// <see cref="Range"/> + <see cref="VolState.Compression"/> turning into <see cref="Up"/> (or
/// <see cref="Down"/>) + <see cref="VolState.Expansion"/>.
/// </summary>
public enum TrendState : sbyte
{
    /// <summary>Trending down.</summary>
    Down = -1,

    /// <summary>Not yet classified (cold, or between the enter/exit thresholds of every state).</summary>
    Unknown = 0,

    /// <summary>Trending up.</summary>
    Up = 1,

    /// <summary>Range-bound, no directional trend.</summary>
    Range = 2,
}

/// <summary>
/// Volatility axis of a <c>regimes/</c> classifier. Orthogonal to <see cref="TrendState"/>.
/// </summary>
public enum VolState : sbyte
{
    /// <summary>Not yet classified (cold, or between the enter/exit thresholds of every state).</summary>
    Unknown = 0,

    /// <summary>Volatility is compressed relative to its own recent history.</summary>
    Compression = 1,

    /// <summary>Volatility is within its normal recent range.</summary>
    Normal = 2,

    /// <summary>Volatility is expanded relative to its own recent history.</summary>
    Expansion = 3,
}
