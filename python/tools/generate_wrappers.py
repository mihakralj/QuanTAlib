#!/usr/bin/env python3
"""Generate per-category Python wrapper modules from C# export signatures.

Reads Exports.Generated.cs, maps exports to lib/ categories,
and generates one .py file per category under python/quantalib/.

Run from repo root:
    python python/tools/generate_wrappers.py
"""
from __future__ import annotations
import os
import re
import textwrap
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
CS_FILE = ROOT / "python" / "src" / "Exports.Generated.cs"
LIB_DIR = ROOT / "lib"
OUT_DIR = ROOT / "python" / "quantalib"

# ── Category mapping ──────────────────────────────────────────────────────
# Scan lib/ subdirs to build export→category map
def build_category_map() -> dict[str, str]:
    """Map indicator name (lowercase) → category folder name."""
    m: dict[str, str] = {}
    for cat_dir in sorted(LIB_DIR.iterdir()):
        if not cat_dir.is_dir() or cat_dir.name.startswith("."):
            continue
        cat = cat_dir.name
        for ind_dir in sorted(cat_dir.iterdir()):
            if ind_dir.is_dir() and not ind_dir.name.startswith("_"):
                m[ind_dir.name.lower()] = cat
    return m

CAT_MAP = build_category_map()

# Manual overrides for export names that differ from lib/ dir names
EXPORT_TO_LIB = {
    "abber": "aberr",
    "htdcperiod": "ht_dcperiod",
    "htdcphase": "ht_dcphase",
    "htphasor": "ht_phasor",
    "htsine": "ht_sine",
    "httrendmode": "ht_trendmode",
    "htit": "htit",
    "ttmlrc": "ttm_lrc",
    "ttmscalper": "ttm_scalper",
    "ttmsqueeze": "ttm_squeeze",
    "ttmtrend": "ttm_trend",
    "ttmwave": "ttm_wave",
}

def get_category(export_name: str) -> str:
    """Return category for an export name."""
    lib_name = EXPORT_TO_LIB.get(export_name, export_name)
    if lib_name in CAT_MAP:
        return CAT_MAP[lib_name]
    # Some exports have _ removed vs lib dir (e.g. td_seq → tdseq)
    for k, v in CAT_MAP.items():
        if k.replace("_", "") == export_name.replace("_", ""):
            return v
    return "uncategorized"


# ── Parse C# exports ─────────────────────────────────────────────────────
def parse_exports() -> list[dict]:
    """Parse all exports from Exports.Generated.cs."""
    cs = CS_FILE.read_text(encoding="utf-8")
    pattern = r'\[UnmanagedCallersOnly\(EntryPoint\s*=\s*"qtl_(\w+)"\)\]\s+public static int \w+\(([^)]+)\)'
    exports = []
    for name, params_str in re.findall(pattern, cs):
        params = []
        for p in params_str.split(","):
            p = p.strip()
            tokens = p.split()
            if len(tokens) >= 2:
                ptype = tokens[0]
                pname = tokens[1]
                params.append({"type": ptype, "name": pname})
        exports.append({
            "name": name,
            "params": params,
            "category": get_category(name),
        })
    return exports


# ── Classify param roles ─────────────────────────────────────────────────
def classify_params(params):
    """Identify inputs, outputs, scalars in a param list."""
    inputs = []
    outputs = []
    n_idx = None
    scalars = []
    
    for i, p in enumerate(params):
        name = p["name"]
        ptype = p["type"]
        
        if name == "n":
            n_idx = i
            continue
        
        if ptype == "double*":
            # Heuristic: if name contains output/dst/destination/Out/middle/upper/lower etc.
            out_names = {"output", "dst", "destination", "middle", "upper", "lower",
                        "haOpenOut", "haHighOut", "haLowOut", "haCloseOut",
                        "dstMiddle", "dstUpper", "dstLower", "dstTenkan", "dstKijun",
                        "dstSenkouA", "dstSenkouB", "dstChikou",
                        "kOut", "dOut", "jOut", "kstOut", "sigOut",
                        "rvgiOutput", "signalOutput", "signalOutput",
                        "momOut", "sqOut", "trend", "strength",
                        "sine", "leadSine", "inPhase", "quadrature", "ppOutput",
                        "upOutput", "downOutput", "highOutput", "lowOutput",
                        "pmaOutput", "triggerOutput", "famaOutput",
                        "upper1", "lower1", "upper2", "lower2", "vwap", "stdDev",
                        "viPlus", "viMinus", "midline",
                        "signal"}
            if name in out_names or name.endswith("Out") or name.endswith("Output"):
                outputs.append(p)
            else:
                inputs.append(p)
        elif ptype == "int" or ptype == "double":
            scalars.append(p)
    
    return inputs, outputs, n_idx, scalars


# ── Generate wrapper function ────────────────────────────────────────────

# Description map for well-known indicators  
DESCRIPTIONS = {
    # Core
    "avgprice": "Average Price = (O+H+L+C)/4",
    "ha": "Heikin-Ashi Candles",
    "medprice": "Median Price = (H+L)/2",
    "midbody": "Mid Body = (O+C)/2",
    "midpoint": "Midpoint = src[i] over period",
    "midprice": "Mid Price = (High+Low)/2 over period",
    "typprice": "Typical Price = (H+L+C)/3",
    "wclprice": "Weighted Close Price = (H+L+2*C)/4",
    # Momentum
    "asi": "Accumulative Swing Index",
    "bias": "Bias Indicator",
    "bop": "Balance of Power",
    "cci": "Commodity Channel Index",
    "cfb": "Composite Fractal Behavior",
    "cmo": "Chande Momentum Oscillator",
    "macd": "Moving Average Convergence Divergence",
    "mom": "Momentum",
    "pmo": "Price Momentum Oscillator",
    "ppo": "Percentage Price Oscillator",
    "prs": "Price Relative Strength",
    "roc": "Rate of Change",
    "rocp": "Rate of Change (Percentage)",
    "rocr": "Rate of Change (Ratio)",
    "rsi": "Relative Strength Index",
    "rsx": "Relative Strength Xtra",
    "sam": "Simple Alpha Momentum",
    "tsi": "True Strength Index",
    "vel": "Velocity",
    # Oscillators
    "ac": "Accelerator Oscillator",
    "ao": "Awesome Oscillator",
    "apo": "Absolute Price Oscillator",
    "bbb": "Bollinger Band Bounce",
    "bbi": "Bull Bear Index",
    "bbs": "Bollinger Band Squeeze",
    "brar": "Bull-Bear Ratio",
    "cfo": "Chande Forecast Oscillator",
    "coppock": "Coppock Curve",
    "crsi": "Connors RSI",
    "cti": "Correlation Trend Indicator",
    "deco": "DECO Oscillator",
    "dem": "DeMarker",
    "dosc": "Derivative Oscillator",
    "dpo": "Detrended Price Oscillator",
    "dymoi": "Dynamic Momentum Index",
    "er": "Efficiency Ratio",
    "eri": "Elder Ray Index",
    "fi": "Force Index",
    "fisher": "Fisher Transform",
    "fisher04": "Fisher Transform (0.4 variant)",
    "gator": "Gator Oscillator",
    "imi": "Intraday Momentum Index",
    "inertia": "Inertia",
    "kdj": "KDJ Indicator",
    "kri": "Kairi Relative Index",
    "kst": "Know Sure Thing",
    "lrsi": "Laguerre RSI",
    "marketfi": "Market Facilitation Index",
    "mstoch": "Modified Stochastic",
    "pgo": "Pretty Good Oscillator",
    "psl": "Psychological Line",
    "qqe": "Quantitative Qualitative Estimation",
    "reflex": "Reflex",
    "reverseema": "Reverse EMA",
    "rvgi": "Relative Vigor Index",
    "smi": "Stochastic Momentum Index",
    "squeeze": "Squeeze Momentum",
    "stc": "Schaff Trend Cycle",
    "stoch": "Stochastic Oscillator",
    "stochf": "Fast Stochastic",
    "stochrsi": "Stochastic RSI",
    "td_seq": "Tom DeMark Sequential",
    "trendflex": "Trendflex",
    "trix": "Triple EMA Rate of Change",
    "ttmwave": "TTM Wave",
    "ultosc": "Ultimate Oscillator",
    "willr": "Williams %R",
    # Trends FIR
    "alma": "Arnaud Legoux Moving Average",
    "blma": "Blackman Moving Average",
    "bwma": "Butterworth-weighted Moving Average",
    "conv": "Convolution Filter",
    "crma": "Cosine-Ramp Moving Average",
    "dwma": "Double Weighted Moving Average",
    "fwma": "Fibonacci Weighted Moving Average",
    "gwma": "Gaussian Weighted Moving Average",
    "hamma": "Hamming Moving Average",
    "hanma": "Hann Moving Average",
    "hend": "Henderson Moving Average",
    "hma": "Hull Moving Average",
    "ilrs": "Integral of Linear Regression Slope",
    "kaiser": "Kaiser Window Moving Average",
    "lanczos": "Lanczos Moving Average",
    "lsma": "Least Squares Moving Average",
    "nlma": "Non-Lag Moving Average",
    "nyqma": "Nyquist Moving Average",
    "parzen": "Parzen Moving Average",
    "pma": "Predictive Moving Average",
    "pwma": "Pascal Weighted Moving Average",
    "qrma": "Quick Reaction Moving Average",
    "rain": "RAIN Moving Average",
    "rwma": "Range Weighted Moving Average",
    "sgma": "Savitzky-Golay Moving Average",
    "sinema": "Sine Weighted Moving Average",
    "sma": "Simple Moving Average",
    "sp15": "SP-15 Moving Average",
    "swma": "Symmetric Weighted Moving Average",
    "trima": "Triangular Moving Average",
    "tsf": "Time Series Forecast",
    "tukey_w": "Tukey-windowed Moving Average",
    "wma": "Weighted Moving Average",
    # Trends IIR
    "adxvma": "ADX Variable Moving Average",
    "ahrens": "Ahrens Moving Average",
    "coral": "CORAL Trend",
    "decycler": "Simple Decycler",
    "dema": "Double Exponential Moving Average",
    "dsma": "Deviation-Scaled Moving Average",
    "ema": "Exponential Moving Average",
    "frama": "Fractal Adaptive Moving Average",
    "gdema": "Generalized Double EMA",
    "hema": "Henderson EMA",
    "holt": "Holt Exponential Smoothing",
    "htit": "Hilbert Transform Instantaneous Trendline",
    "hwma": "Holt-Winter Moving Average",
    "jma": "Jurik Moving Average",
    "kama": "Kaufman Adaptive Moving Average",
    "lema": "Laguerre EMA",
    "ltma": "Low-Lag Triple Moving Average",
    "mama": "MESA Adaptive Moving Average",
    "mavp": "Moving Average Variable Period",
    "mcnma": "McNicholl Moving Average",
    "mgdi": "McGinley Dynamic",
    "mma": "Modified Moving Average",
    "nma": "Normalized Moving Average",
    "qema": "Quadruple EMA",
    "rema": "Regularized EMA",
    "rgma": "Recursive Gaussian Moving Average",
    "rma": "Rolling Moving Average",
    "t3": "Tillson T3",
    "tema": "Triple Exponential Moving Average",
    "trama": "Triangular Adaptive Moving Average",
    "vama": "Volume Adjusted Moving Average",
    "vidya": "Variable Index Dynamic Average",
    "yzvama": "Yang Zhang Volatility Adaptive MA",
    "zldema": "Zero-Lag Double EMA",
    "zlema": "Zero-Lag EMA",
    "zltema": "Zero-Lag Triple EMA",
    # Channels
    "abber": "Aberration Bands",
    "accbands": "Acceleration Bands",
    "apchannel": "Average Price Channel",
    "apz": "Adaptive Price Zone",
    "atrbands": "ATR Bands",
    "bbands": "Bollinger Bands",
    "dchannel": "Donchian Channel",
    "decaychannel": "Decay Channel",
    "fcb": "Fractal Chaos Bands",
    "jbands": "J-Line Bands",
    "kchannel": "Keltner Channel",
    "maenv": "Moving Average Envelope",
    "mmchannel": "Min-Max Channel",
    "pchannel": "Price Channel",
    "regchannel": "Regression Channel",
    "sdchannel": "Standard Deviation Channel",
    "starchannel": "Stoller Average Range Channel (STARC)",
    "stbands": "SuperTrend Bands",
    "ttmlrc": "TTM Linear Regression Channel",
    "ubands": "Upper/Lower Bands",
    "uchannel": "Ulcer Channel",
    "vwapbands": "VWAP Bands",
    "vwapsd": "VWAP Standard Deviation",
    # Volatility
    "adr": "Average Daily Range",
    "atr": "Average True Range",
    "atrn": "Normalized ATR",
    "bbw": "Bollinger Band Width",
    "bbwn": "Bollinger Band Width Normalized",
    "bbwp": "Bollinger Band Width Percentile",
    "ccv": "Close-to-Close Volatility",
    "cv": "Coefficient of Variation",
    "cvi": "Chaikin Volatility Index",
    "etherm": "Elder Thermometer",
    "ewma": "Exponentially Weighted Moving Average Volatility",
    "gkv": "Garman-Klass Volatility",
    "hlv": "High-Low Volatility",
    "hv": "Historical Volatility",
    "jvolty": "Jurik Volatility",
    "jvoltyn": "Jurik Volatility Normalized",
    "massi": "Mass Index",
    "natr": "Normalized ATR",
    "rsv": "Rogers-Satchell Volatility",
    "rv": "Realized Volatility",
    "rvi": "Relative Volatility Index",
    "tr": "True Range",
    "ui": "Ulcer Index",
    "vov": "Volatility of Volatility",
    "vr": "Volatility Ratio",
    "yzv": "Yang-Zhang Volatility",
    # Volume
    "adl": "Accumulation/Distribution Line",
    "adosc": "Accumulation/Distribution Oscillator",
    "aobv": "Archer On-Balance Volume",
    "cmf": "Chaikin Money Flow",
    "efi": "Elder Force Index",
    "eom": "Ease of Movement",
    "evwma": "Elastic Volume Weighted Moving Average",
    "iii": "Intraday Intensity Index",
    "kvo": "Klinger Volume Oscillator",
    "mfi": "Money Flow Index",
    "nvi": "Negative Volume Index",
    "obv": "On-Balance Volume",
    "pvd": "Price Volume Divergence",
    "pvi": "Positive Volume Index",
    "pvo": "Percentage Volume Oscillator",
    "pvr": "Price Volume Rank",
    "pvt": "Price Volume Trend",
    "tvi": "Trade Volume Index",
    "twap": "Time Weighted Average Price",
    "va": "Volume Accumulation",
    "vf": "Volume Flow",
    "vo": "Volume Oscillator",
    "vroc": "Volume Rate of Change",
    "vwad": "Volume Weighted Accumulation/Distribution",
    "vwap": "Volume Weighted Average Price",
    "vwma": "Volume Weighted Moving Average",
    "wad": "Williams Accumulation/Distribution",
    # Statistics
    "acf": "Autocorrelation Function",
    "beta": "Beta Coefficient",
    "cma": "Cumulative Moving Average",
    "cointegration": "Cointegration",
    "correlation": "Pearson Correlation",
    "covariance": "Covariance",
    "entropy": "Shannon Entropy",
    "geomean": "Geometric Mean",
    "granger": "Granger Causality",
    "harmean": "Harmonic Mean",
    "hurst": "Hurst Exponent",
    "iqr": "Interquartile Range",
    "jb": "Jarque-Bera Test",
    "kendall": "Kendall Rank Correlation",
    "kurtosis": "Kurtosis",
    "linreg": "Linear Regression",
    "meandev": "Mean Deviation",
    "median": "Rolling Median",
    "mode": "Rolling Mode",
    "pacf": "Partial Autocorrelation Function",
    "percentile": "Rolling Percentile",
    "polyfit": "Polynomial Fit",
    "quantile": "Rolling Quantile",
    "skew": "Skewness",
    "spearman": "Spearman Rank Correlation",
    "stddev": "Standard Deviation",
    "stderr": "Standard Error",
    "sum": "Rolling Sum",
    "theil": "Theil U Statistic",
    "trim": "Trimmed Mean",
    "variance": "Variance",
    "wavg": "Weighted Average",
    "wins": "Winsorized Mean",
    "zscore": "Z-Score",
    "ztest": "Z-Test",
    # Errors
    "huber": "Huber Loss",
    "logcosh": "Log-Cosh Loss",
    "maape": "Mean Arctangent Absolute Percentage Error",
    "mae": "Mean Absolute Error",
    "mapd": "Mean Absolute Percentage Deviation",
    "mape": "Mean Absolute Percentage Error",
    "mase": "Mean Absolute Scaled Error",
    "mdae": "Median Absolute Error",
    "mdape": "Median Absolute Percentage Error",
    "me": "Mean Error",
    "mpe": "Mean Percentage Error",
    "mrae": "Mean Relative Absolute Error",
    "mse": "Mean Squared Error",
    "msle": "Mean Squared Logarithmic Error",
    "pseudohuber": "Pseudo-Huber Loss",
    "quantileloss": "Quantile Loss (Pinball Loss)",
    "rae": "Relative Absolute Error",
    "rmse": "Root Mean Squared Error",
    "rmsle": "Root Mean Squared Logarithmic Error",
    "rse": "Relative Squared Error",
    "rsquared": "R-Squared (Coefficient of Determination)",
    "smape": "Symmetric Mean Absolute Percentage Error",
    "theilu": "Theil U Statistic (Error)",
    "tukeybiweight": "Tukey Biweight Loss",
    "wmape": "Weighted Mean Absolute Percentage Error",
    "wrmse": "Weighted Root Mean Squared Error",
    # Filters
    "agc": "Automatic Gain Control",
    "alaguerre": "Adaptive Laguerre Filter",
    "baxterking": "Baxter-King Filter",
    "bessel": "Bessel Filter",
    "bilateral": "Bilateral Filter",
    "bpf": "Bandpass Filter",
    "butter2": "2nd-Order Butterworth Filter",
    "butter3": "3rd-Order Butterworth Filter",
    "cfitz": "Christiano-Fitzgerald Filter",
    "cheby1": "Chebyshev Type I Filter",
    "cheby2": "Chebyshev Type II Filter",
    "edcf": "Ehlers Distance Coefficient Filter",
    "elliptic": "Elliptic (Cauer) Filter",
    "gauss": "Gaussian Filter",
    "hann": "Hann Filter",
    "hp": "Hodrick-Prescott Filter",
    "hpf": "High-Pass Filter",
    "kalman": "Kalman Filter",
    "laguerre": "Laguerre Filter",
    "lms": "Least Mean Squares Filter",
    "loess": "LOESS Smoother",
    "modf": "Modified Filter",
    "notch": "Notch Filter",
    "nw": "Nadaraya-Watson Filter",
    "oneeuro": "1€ Filter",
    "rls": "Recursive Least Squares Filter",
    "rmed": "Running Median Filter",
    "roofing": "Roofing Filter",
    "sgf": "Savitzky-Golay Filter",
    "spbf": "Short-Period Bandpass Filter",
    "ssf2": "Super Smoother (2-pole)",
    "ssf3": "Super Smoother (3-pole)",
    "usf": "Universal Smoother Filter",
    "voss": "Voss Predictor",
    "wavelet": "Wavelet Filter",
    "wiener": "Wiener Filter",
    # Cycles
    "ccor": "Circular Correlation",
    "ccyc": "Cyber Cycle",
    "cg": "Center of Gravity",
    "dsp": "Dominant Cycle Period",
    "eacp": "Ehlers Autocorrelation Periodogram",
    "ebsw": "Even Better Sinewave",
    "homod": "Homodyne Discriminator",
    "ht_dcperiod": "Hilbert Transform Dominant Cycle Period",
    "ht_dcphase": "Hilbert Transform Dominant Cycle Phase",
    "ht_phasor": "Hilbert Transform Phasor",
    "ht_sine": "Hilbert Transform Sine",
    "lunar": "Lunar Cycle",
    "solar": "Solar Cycle",
    "ssfdsp": "Supersmoother DSP",
    # Dynamics
    "adx": "Average Directional Index",
    "adxr": "ADX Rating",
    "alligator": "Williams Alligator",
    "amat": "Archer Moving Average Trends",
    "aroon": "Aroon",
    "aroonosc": "Aroon Oscillator",
    "chop": "Choppiness Index",
    "dmx": "Directional Movement Extended",
    "dx": "Directional Movement Index",
    "ghla": "Gann Hi-Lo Activator",
    "ht_trendmode": "Hilbert Transform Trend Mode",
    "ichimoku": "Ichimoku Cloud",
    "impulse": "Elder Impulse System",
    "pfe": "Polarized Fractal Efficiency",
    "qstick": "QStick",
    "ravi": "Range Action Verification Index",
    "super": "SuperTrend",
    "ttmsqueeze": "TTM Squeeze",
    "ttmtrend": "TTM Trend",
    "vhf": "Vertical Horizontal Filter",
    "vortex": "Vortex Indicator",
    # Reversals
    "chandelier": "Chandelier Exit",
    "ckstop": "Chuck LeBeau Stop",
    "fractals": "Williams Fractals",
    "pivot": "Pivot Points (Traditional)",
    "pivotcam": "Camarilla Pivot Points",
    "pivotdem": "DeMark Pivot Points",
    "pivotext": "Extended Pivot Points",
    "pivotfib": "Fibonacci Pivot Points",
    "pivotwood": "Woodie Pivot Points",
    "psar": "Parabolic SAR",
    "swings": "Swing High/Low",
    "ttmscalper": "TTM Scalper",
    # Forecasts
    "afirma": "Adaptive FIR Moving Average",
    # Numerics
    "accel": "Acceleration",
    "betadist": "Beta Distribution",
    "binomdist": "Binomial Distribution",
    "change": "Price Change",
    "cwt": "Continuous Wavelet Transform",
    "dwt": "Discrete Wavelet Transform",
    "expdist": "Exponential Distribution",
    "exptrans": "Exponential Transform",
    "fdist": "F-Distribution",
    "fft": "Fast Fourier Transform",
    "gammadist": "Gamma Distribution",
    "highest": "Highest Value",
    "ifft": "Inverse FFT",
    "jerk": "Jerk (3rd derivative)",
    "lineartrans": "Linear Transform",
    "lognormdist": "Log-Normal Distribution",
    "logtrans": "Logarithmic Transform",
    "lowest": "Lowest Value",
    "normalize": "Normalization",
    "normdist": "Normal Distribution",
    "poissondist": "Poisson Distribution",
    "relu": "ReLU Activation",
    "sigmoid": "Sigmoid Transform",
    "slope": "Slope (1st derivative)",
    "sqrttrans": "Square Root Transform",
    "tdist": "Student's t-Distribution",
    "weibulldist": "Weibull Distribution",
}

# Python function name overrides (export_name → python_name)
PY_NAME = {
    "abber": "aberr",   # fix typo in C# export 
    "htdcperiod": "ht_dcperiod",
    "htdcphase": "ht_dcphase",
    "htphasor": "ht_phasor",
    "htsine": "ht_sine",
    "httrendmode": "ht_trendmode",
    "ttmlrc": "ttm_lrc",
    "ttmscalper": "ttm_scalper",
    "ttmsqueeze": "ttm_squeeze",
    "ttmtrend": "ttm_trend",
    "ttmwave": "ttm_wave",
}


def gen_wrapper(export: dict) -> str | None:
    """Generate a Python wrapper function for one export."""
    name = export["name"]
    params = export["params"]
    py_name = PY_NAME.get(name, name)
    label = py_name.upper()
    cat = export["category"]
    desc = DESCRIPTIONS.get(name, DESCRIPTIONS.get(py_name, f"{label} indicator"))
    
    inputs, outputs, n_idx, scalars = classify_params(params)
    
    # Build Python function signature and body
    lines = []
    
    # Determine input pattern and generate accordingly
    input_names = [p["name"] for p in inputs]
    output_names = [p["name"] for p in outputs]
    scalar_specs = [(p["name"], p["type"]) for p in scalars]
    
    # Build Python params
    py_params = []
    py_body = []
    
    # Categorize input types
    has_ohlcv = all(x in [p["name"] for p in inputs] for x in ["sourceOpen", "sourceHigh", "sourceLow", "sourceClose", "sourceVolume"])
    has_ohlc = all(x in [p["name"] for p in inputs] for x in ["open", "high", "low", "close"]) and not has_ohlcv
    has_hlc = all(x in [p["name"] for p in inputs] for x in ["high", "low", "close"]) and not has_ohlc and not has_ohlcv
    has_hl = {"high", "low"}.issubset(set(input_names)) and "close" not in input_names and not has_ohlcv
    has_actual_predicted = {"actual", "predicted"}.issubset(set(input_names))
    has_xy = {"seriesX", "seriesY"}.issubset(set(input_names)) or {"x", "y"}.issubset(set(input_names))
    has_src_vol = (len(inputs) == 2 and any("volume" in p["name"].lower() or p["name"] == "volume" for p in inputs))
    has_price_vol = (len(inputs) == 2 and any(p["name"] == "price" for p in inputs) and any(p["name"] == "volume" for p in inputs))
    single_src = len(inputs) == 1 and inputs[0]["type"] == "double*"
    
    # Generate function
    # Decide function signature
    sig_params = []
    
    # Add input params
    if has_ohlcv:
        sig_params.extend([
            "open: object", "high: object", "low: object",
            "close: object", "volume: object",
        ])
    elif has_ohlc:
        sig_params.extend([
            "open: object", "high: object", "low: object", "close: object",
        ])
    elif has_hlc:
        sig_params.extend(["high: object", "low: object", "close: object"])
    elif has_hl:
        sig_params.extend(["high: object", "low: object"])
    elif has_actual_predicted:
        sig_params.extend(["actual: object", "predicted: object"])
    elif has_xy:
        sig_params.extend(["x: object", "y: object"])
    elif has_price_vol:
        sig_params.extend(["price: object", "volume: object"])
    elif has_src_vol:
        # Figure out which is source, which is volume
        src_name = [p["name"] for p in inputs if p["name"] != "volume"][0] if inputs else "source"
        sig_params.extend([f"close: object", "volume: object"])
    elif single_src:
        src_name = inputs[0]["name"] if inputs else "source"
        py_input_name = "close" if src_name in ("source", "src", "prices", "price") else src_name
        sig_params.append(f"{py_input_name}: object")
    elif len(inputs) == 2:
        # Two inputs (e.g. prs: baseSeries, compSeries)
        for p in inputs:
            pn = p["name"]
            if pn.startswith("source") or pn.startswith("base"):
                pn = "x"
            elif pn.startswith("comp"):
                pn = "y"
            sig_params.append(f"{pn}: object")
    elif len(inputs) == 0 and len(outputs) == 0:
        # Weird case
        return None
    else:
        for p in inputs:
            sig_params.append(f"{p['name']}: object")
    
    # Add scalar params with defaults
    scalar_defaults = {
        "period": 14, "length": 14, "hpLength": 40, "ssLength": 10,
        "fastPeriod": 12, "slowPeriod": 26, "acPeriod": 5,
        "bbPeriod": 20, "bbMult": 2.0, "kcPeriod": 10, "kcMult": 1.5,
        "multiplier": 2.0, "factor": 2.0, "sigma": 6.0,
        "rsiPeriod": 14, "smoothFactor": 5, "qqeFactor": 4.236,
        "kPeriod": 14, "dPeriod": 3, "kSmooth": 3, "dSmooth": 3,
        "kLength": 14, "windowSize": 256, "minPeriod": 6, "maxPeriod": 48,
        "longRoc": 14, "shortRoc": 11, "wmaPeriod": 10,
        "r1": 10, "r2": 15, "r3": 20, "r4": 30,
        "s1": 10, "s2": 10, "s3": 10, "s4": 15, "sigPeriod": 9,
        "jawPeriod": 13, "jawShift": 8, "jawOffset": 8,
        "teethPeriod": 8, "teethShift": 5, "teethOffset": 5,
        "lipsPeriod": 5, "lipsShift": 3, "lipsOffset": 3,
        "tenkanPeriod": 9, "kijunPeriod": 26, "senkouBPeriod": 52, "displacement": 26,
        "emaPeriod": 13, "macdFast": 12, "macdSlow": 26, "macdSignal": 9,
        "signalPeriod": 9, "signal": 3,
        "numHarmonics": 10,
        "atrPeriod": 22, "stopPeriod": 3,
        "alpha": 2.0, "beta": 2.0, "gamma": 0.7, "k": 2.0,
        "lambda": 1600.0, "mu": 0.01, "mu0": 0.0,
        "delta": 1.35, "c": 4.685,
        "q": 0.3, "r": 1.0,
        "vfactor": 0.7, "vovPeriod": 20, "volatilityPeriod": 20,
        "d1": 10, "d2": 20, "nu": 10,
        "order": 3, "polyOrder": 3, "feedback": 0, "fbWeight": 0.5,
        "annualize": 1, "annualPeriods": 252, "isPopulation": 0,
        "predict": 3, "bandwidth": 0.25,
        "nanValue": 0.0, "initialLastValid": 0.0, "initialLast": 0.0,
        "x0": 0.0, "intercept": 0.0, "slope_val": 1.0,
        "minCutoff": 1.0, "dCutoff": 1.0,
        "method": 0, "maType": 0,
        "percentage": 2.5, "percent": 50.0,
        "quantileLevel": 0.5, "quantile": 0.5,
        "trimPct": 0.1, "winPct": 0.05,
        "offset": 0,
        "shortPeriod": 12, "longPeriod": 26, "sumLength": 25,
        "emaLength": 9, "rmaLength": 14, "stdevLength": 10,
        "stochLength": 14, "rsiLength": 14,
        "fastLength": 23, "slowLength": 50, "smoothing": 10,
        "lookback": 5, "useCloses": 0,
        "levels": 4, "threshMult": 1.0, "smoothPeriod": 5,
        "blau": 3, "phase": 0, "power": 1.0,
        "rmsPeriod": 20,
        "nyquistPeriod": 2, "passes": 3,
        "cumulative": 0, "usePercent": 1, "useEma": 0,
        "base": 2.0, "degree": 2,
        "minLength": 5, "maxLength": 50,
        "yzvShortPeriod": 10, "yzvLongPeriod": 100, "percentileLookback": 252,
        "baseLength": 20, "shortAtrPeriod": 14, "longAtrPeriod": 50,
        "strPeriod": 14, "centerPeriod": 20,
        "stPeriod": 14, "momPeriod": 12,
        "scale": 10.0, "omega": 6.0,
        "trials": 20, "threshold": 10,
        "lam": 3.0, "afStart": 0.02, "afIncrement": 0.02, "afMax": 0.2,
        "cutoff": 10, "fastLimit": 0.5, "slowLimit": 0.05,
        "minVol": 0.2, "maxVol": 0.7,
        "friction": 0.4,
        "avgLength": 3, "enhance": 1,
        "numDevs": 2.0,
        "window_type": 0, "use_simd": 0,
        "hpLength_val": 40, "ssfLength": 10,
    }
    
    for sname, stype in scalar_specs:
        # Get reasonable default
        default = scalar_defaults.get(sname)
        if default is None:
            # Try to infer
            if "period" in sname.lower() or "length" in sname.lower():
                default = 14
            elif "mult" in sname.lower() or "factor" in sname.lower():
                default = 2.0
            elif stype == "double":
                default = 1.0
            else:
                default = 10
        
        if stype == "double":
            sig_params.append(f"{sname}: float = {default}")
        else:
            sig_params.append(f"{sname}: int = {int(default)}")
    
    sig_params.append("offset: int = 0")
    sig_params.append("**kwargs")
    
    # Build function body
    body = []
    
    # Sanitize scalars
    for sname, stype in scalar_specs:
        if stype == "double":
            body.append(f"    {sname} = float({sname})")
        else:
            body.append(f"    {sname} = int({sname})")
    body.append("    offset = int(offset)")
    
    # Convert inputs
    if has_ohlcv:
        body.append("    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)")
        body.append("    c, _ = _arr(close); v, _ = _arr(volume)")
        body.append("    n = len(o)")
    elif has_ohlc:
        body.append("    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)")
        body.append("    n = len(o)")
    elif has_hlc:
        body.append("    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)")
        body.append("    n = len(h)")
    elif has_hl:
        body.append("    h, idx = _arr(high); l, _ = _arr(low)")
        body.append("    n = len(h)")
    elif has_actual_predicted:
        body.append("    a, idx = _arr(actual); p, _ = _arr(predicted)")
        body.append("    n = len(a)")
    elif has_xy:
        body.append("    xarr, idx = _arr(x); yarr, _ = _arr(y)")
        body.append("    n = len(xarr)")
    elif has_price_vol:
        body.append("    pr, idx = _arr(price); v, _ = _arr(volume)")
        body.append("    n = len(pr)")
    elif has_src_vol:
        body.append("    src, idx = _arr(close); v, _ = _arr(volume)")
        body.append("    n = len(src)")
    elif single_src:
        py_input_name = "close" if inputs[0]["name"] in ("source", "src", "prices", "price") else inputs[0]["name"]
        body.append(f"    src, idx = _arr({py_input_name})")
        body.append("    n = len(src)")
    elif len(inputs) == 2:
        body.append(f"    xarr, idx = _arr(x); yarr, _ = _arr(y)")
        body.append("    n = len(xarr)")
    
    # Allocate outputs
    for p in outputs:
        body.append(f"    {p['name']} = _out(n)")
    
    # Build native call arguments in original order
    call_args = []
    for p in params:
        pname = p["name"]
        ptype = p["type"]
        if pname == "n":
            call_args.append("n")
        elif ptype == "double*":
            if p in outputs:
                call_args.append(f"_ptr({pname})")
            else:
                # Map to our local var names
                if has_ohlcv:
                    vmap = {"sourceOpen": "o", "sourceHigh": "h", "sourceLow": "l", "sourceClose": "c", "sourceVolume": "v"}
                    call_args.append(f"_ptr({vmap.get(pname, pname)})")
                elif has_ohlc:
                    vmap = {"open": "o", "high": "h", "low": "l", "close": "c"}
                    call_args.append(f"_ptr({vmap.get(pname, pname)})")
                elif has_hlc:
                    vmap = {"high": "h", "low": "l", "close": "c"}
                    call_args.append(f"_ptr({vmap.get(pname, pname)})")
                elif has_hl:
                    vmap = {"high": "h", "low": "l"}
                    call_args.append(f"_ptr({vmap.get(pname, pname)})")
                elif has_actual_predicted:
                    vmap = {"actual": "a", "predicted": "p"}
                    call_args.append(f"_ptr({vmap.get(pname, pname)})")
                elif has_xy:
                    vmap = {"seriesX": "xarr", "seriesY": "yarr", "x": "xarr", "y": "yarr"}
                    call_args.append(f"_ptr({vmap.get(pname, pname)})")
                elif has_price_vol:
                    vmap = {"price": "pr", "volume": "v"}
                    call_args.append(f"_ptr({vmap.get(pname, pname)})")
                elif has_src_vol:
                    if pname == "volume":
                        call_args.append("_ptr(v)")
                    else:
                        call_args.append("_ptr(src)")
                elif single_src:
                    call_args.append("_ptr(src)")
                elif len(inputs) == 2:
                    vmap = {}
                    for ip in inputs:
                        if ip["name"].startswith("source") or ip["name"].startswith("base"):
                            vmap[ip["name"]] = "xarr"
                        else:
                            vmap[ip["name"]] = "yarr"
                    call_args.append(f"_ptr({vmap.get(pname, pname)})")
                else:
                    call_args.append(f"_ptr({pname})")
        else:
            call_args.append(pname)
    
    call_str = ", ".join(call_args)
    body.append(f'    _check(_lib.qtl_{name}({call_str}))')
    
    # Wrap output
    if len(outputs) == 1:
        out_name = outputs[0]["name"]
        # Decide label
        has_period_scalar = any("period" in s[0].lower() or "length" in s[0].lower() for s in scalar_specs)
        if has_period_scalar:
            # Use first period-like scalar for label
            period_var = next(s[0] for s in scalar_specs if "period" in s[0].lower() or "length" in s[0].lower())
            body.append(f'    return _wrap({out_name}, idx, f"{label}_{{{period_var}}}", "{cat}", offset)')
        else:
            body.append(f'    return _wrap({out_name}, idx, "{label}", "{cat}", offset)')
    elif len(outputs) > 1:
        # Multi-output
        out_dict_parts = []
        for p in outputs:
            out_dict_parts.append(f'"{p["name"]}": {p["name"]}')
        out_dict = ", ".join(out_dict_parts)
        body.append(f'    return _wrap_multi({{{out_dict}}}, idx, "{cat}", offset)')
    else:
        body.append("    return None  # no output detected")
    
    # Assemble
    sig = ", ".join(sig_params)
    
    func = f'def {py_name}({sig}) -> object:\n'
    func += f'    """{desc}."""\n'
    func += "\n".join(body) + "\n"
    
    return func


def generate_category_file(category: str, exports: list[dict]) -> str:
    """Generate a full category module."""
    # Map category to Python module name
    mod_name = category.replace("-", "_")
    
    header = f'"""quantalib {category} indicators.\n\nAuto-generated — DO NOT EDIT.\n"""\n'
    header += "from __future__ import annotations\n\n"
    header += "from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib\n\n\n"
    
    functions = []
    all_names = []
    
    for exp in sorted(exports, key=lambda e: e["name"]):
        func = gen_wrapper(exp)
        if func:
            py_name = PY_NAME.get(exp["name"], exp["name"])
            all_names.append(py_name)
            functions.append(func)
    
    # __all__
    all_str = "__all__ = [\n"
    for n in all_names:
        all_str += f'    "{n}",\n'
    all_str += "]\n"
    
    return header + all_str + "\n\n" + "\n\n".join(functions)


def main():
    exports = parse_exports()
    
    # Group by category
    by_cat: dict[str, list[dict]] = {}
    for exp in exports:
        cat = exp["category"]
        by_cat.setdefault(cat, []).append(exp)
    
    print(f"Parsed {len(exports)} exports in {len(by_cat)} categories:")
    for cat, exps in sorted(by_cat.items()):
        print(f"  {cat}: {len(exps)} indicators")
    
    # Generate files
    for cat, exps in sorted(by_cat.items()):
        if cat == "uncategorized":
            continue
        mod_name = cat.replace("-", "_")
        # Map category dirs to Python module names
        py_mod = {
            "trends_FIR": "trends_fir",
            "trends_IIR": "trends_iir",
        }.get(mod_name, mod_name)
        
        outpath = OUT_DIR / f"{py_mod}.py"
        content = generate_category_file(cat, exps)
        outpath.write_text(content, encoding="utf-8")
        print(f"  Generated {outpath.name} ({len(exps)} indicators)")
    
    # List uncategorized
    if "uncategorized" in by_cat:
        print(f"\n  UNCATEGORIZED: {[e['name'] for e in by_cat['uncategorized']]}")


if __name__ == "__main__":
    main()
