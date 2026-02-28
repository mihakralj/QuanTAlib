# pandas-ta validation sweep across exported Python wrapper indicators

- Total indicators scanned: **133**
- Successful (✔️): **10**
- Failing (⚠️): **123**

| Indicator | Status | Notes |
|---|---:|---|
| `afirma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `agc` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `ahrens` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `alaguerre` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `alma` | ⚠️ | max_diff=1.784e+00, n=100 |
| `aobv` | ⚠️ | max_diff=2.947e+03, n=100 |
| `apchannel` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `apo` | ✔️ | max_diff=4.263e-14, n=100 |
| `asi` | ⚠️ | QtlInternalError: quantalib native call failed (status=4) |
| `atrbands` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `avgprice` | ⚠️ | TypeError: ohlc4() missing 1 required positional argument: 'close' |
| `baxterking` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `bbands` | ⚠️ | max_diff=1.097e+01, n=100 |
| `bbb` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `bbi` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `bbw` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `bbwn` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `bbwp` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `bessel` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `betadist` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `bias` | ⚠️ | max_diff=2.498e-02, n=100 |
| `bilateral` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `binomdist` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `blma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `bpf` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `brar` | ⚠️ | TypeError: brar() missing 1 required positional argument: 'close' |
| `butter2` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `butter3` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `bwma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `ccor` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `ccv` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `ccyc` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `cfb` | ⚠️ | RuntimeError: unsupported arg lengths in generic sweep |
| `cfitz` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `cfo` | ✔️ | max_diff=1.350e-09, n=100 |
| `cg` | ⚠️ | max_diff=7.695e+00, n=100 |
| `change` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `cheby1` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `cheby2` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `cma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `cmf` | ⚠️ | max_diff=2.728e-01, n=100 |
| `cmo` | ✔️ | max_diff=0.000e+00, n=100 |
| `cointegration` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `conv` | ⚠️ | RuntimeError: unsupported arg kernel in generic sweep |
| `coral` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `correlation` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `covariance` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `crma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `crsi` | ⚠️ | max_diff=1.111e+01, n=100 |
| `cti` | ⚠️ | max_diff=4.631e-01, n=100 |
| `cv` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `cvi` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `cwt` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `deco` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `decycler` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `dem` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `dema` | ⚠️ | max_diff=9.200e-01, n=100 |
| `dema_alpha` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `dosc` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `dpo` | ✔️ | max_diff=5.400e-13, n=100 |
| `dsma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `dsp` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `dwma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `dwt` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `dymoi` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `eacp` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `ebsw` | ⚠️ | max_diff=1.846e+00, n=100 |
| `edcf` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `efi` | ⚠️ | max_diff=8.233e+01, n=100 |
| `elliptic` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `ema` | ⚠️ | max_diff=7.039e-01, n=100 |
| `ema_alpha` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `entropy` | ⚠️ | max_diff=3.206e+00, n=100 |
| `eom` | ⚠️ | max_diff=4.374e+04, n=100 |
| `er` | ⚠️ | max_diff=5.113e-01, n=100 |
| `etherm` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `evwma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `ewma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `expdist` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `exptrans` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `fisher` | ⚠️ | max_diff=1.666e+00, n=100 |
| `fisher04` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `gdema` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `hanma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `hema` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `hma` | ⚠️ | max_diff=2.519e+00, n=100 |
| `inertia` | ⚠️ | max_diff=7.827e+01, n=100 |
| `kri` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `lema` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `lsma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `mae` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `mape` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `medprice` | ⚠️ | max_diff=4.236e+00, n=100 |
| `mfi` | ✔️ | max_diff=3.091e-13, n=100 |
| `midbody` | ⚠️ | AttributeError: module 'pandas_ta' has no attribute 'mid_body' |
| `mom` | ⚠️ | TypeError: <module 'pandas_ta.momentum' from 'C:\\Users\\miha\\AppData\\Local\\Programs\\Python\\Python313\\Lib\\site-packages\\pandas_ta\\momentum\\__init__.py'> is not a callable object |
| `mse` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `nvi` | ⚠️ | max_diff=9.000e+02, n=100 |
| `obv` | ✔️ | max_diff=0.000e+00, n=100 |
| `parzen` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `psl` | ⚠️ | max_diff=1.190e+01, n=100 |
| `pvd` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `pvi` | ⚠️ | max_diff=2.050e+01, n=100 |
| `pvo` | ✔️ | max_diff=3.432e-14, n=100 |
| `pvr` | ⚠️ | max_diff=1.000e+00, n=100 |
| `pvt` | ⚠️ | max_diff=1.182e+05, n=100 |
| `rain` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `reflex` | ⚠️ | max_diff=1.572e+00, n=100 |
| `rmse` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `roc` | ⚠️ | max_diff=7.233e+00, n=100 |
| `rsi` | ⚠️ | max_diff=1.351e+01, n=100 |
| `rsx` | ✔️ | max_diff=1.172e-13, n=100 |
| `sgma` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `sinema` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `sma` | ⚠️ | max_diff=1.729e+00, n=100 |
| `sp15` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `stddev` | ⚠️ | max_diff=1.874e+00, n=100 |
| `swma` | ⚠️ | max_diff=1.583e+00, n=100 |
| `tema` | ⚠️ | max_diff=8.867e-01, n=100 |
| `tr` | ✔️ | max_diff=0.000e+00, n=100 |
| `trendflex` | ⚠️ | max_diff=4.575e-01, n=100 |
| `trima` | ⚠️ | max_diff=1.885e+00, n=100 |
| `trix` | ✔️ | max_diff=2.734e-14, n=100 |
| `tsf` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `tsi` | ⚠️ | max_diff=7.203e-01, n=100 |
| `tukey_w` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `tvi` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `typprice` | ⚠️ | max_diff=9.796e-01, n=100 |
| `variance` | ⚠️ | max_diff=7.699e+00, n=100 |
| `vf` | ⚠️ | RuntimeError: no pandas-ta mapping |
| `vwma` | ⚠️ | max_diff=1.750e+00, n=100 |
| `wma` | ⚠️ | max_diff=9.918e-01, n=100 |
| `zscore` | ⚠️ | max_diff=1.077e+00, n=100 |