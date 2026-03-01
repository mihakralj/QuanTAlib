# pandas-ta validation sweep across exported Python wrapper indicators

- Total indicators scanned: **134**
- Successful (✔️): **31**
- Non-comparable / skipped (⏭️): **82**
- Failing (⚠️): **21**

| Indicator | Status | Notes |
|---|---:|---|
| `aberr` | ⏭️ | no comparable pandas-ta equivalent |
| `afirma` | ⏭️ | no comparable pandas-ta equivalent |
| `agc` | ⏭️ | no comparable pandas-ta equivalent |
| `ahrens` | ⏭️ | no comparable pandas-ta equivalent |
| `alaguerre` | ⏭️ | no comparable pandas-ta equivalent |
| `alma` | ⚠️ | max_diff=6.027e-01, n=100 |
| `aobv` | ⚠️ | max_diff=2.947e+03, n=100 |
| `apchannel` | ⏭️ | no comparable pandas-ta equivalent |
| `apo` | ✔️ | max_diff=4.263e-14, n=100 |
| `asi` | ⏭️ | no comparable pandas-ta equivalent |
| `atrbands` | ⏭️ | no comparable pandas-ta equivalent |
| `avgprice` | ✔️ | max_diff=1.421e-14, n=100 |
| `baxterking` | ⏭️ | no comparable pandas-ta equivalent |
| `bbands` | ✔️ | max_diff=7.005e-11, n=100 |
| `bbb` | ⏭️ | no comparable pandas-ta equivalent |
| `bbi` | ⏭️ | no comparable pandas-ta equivalent |
| `bbw` | ⏭️ | no comparable pandas-ta equivalent |
| `bbwn` | ⏭️ | no comparable pandas-ta equivalent |
| `bbwp` | ⏭️ | no comparable pandas-ta equivalent |
| `bessel` | ⏭️ | no comparable pandas-ta equivalent |
| `betadist` | ⏭️ | no comparable pandas-ta equivalent |
| `bias` | ⚠️ | max_diff=1.508e-02, n=100 |
| `bilateral` | ⏭️ | no comparable pandas-ta equivalent |
| `binomdist` | ⏭️ | no comparable pandas-ta equivalent |
| `blma` | ⏭️ | no comparable pandas-ta equivalent |
| `bpf` | ⏭️ | no comparable pandas-ta equivalent |
| `brar` | ✔️ | max_diff=2.842e-14, n=100 |
| `butter2` | ⏭️ | no comparable pandas-ta equivalent |
| `butter3` | ⏭️ | no comparable pandas-ta equivalent |
| `bwma` | ⏭️ | no comparable pandas-ta equivalent |
| `ccor` | ⏭️ | no comparable pandas-ta equivalent |
| `ccv` | ⏭️ | no comparable pandas-ta equivalent |
| `ccyc` | ⏭️ | no comparable pandas-ta equivalent |
| `cfb` | ⚠️ | RuntimeError: unsupported arg lengths in generic sweep |
| `cfitz` | ⏭️ | no comparable pandas-ta equivalent |
| `cfo` | ✔️ | max_diff=1.350e-09, n=100 |
| `cg` | ⚠️ | max_diff=5.675e+00, n=100 |
| `change` | ⏭️ | no comparable pandas-ta equivalent |
| `cheby1` | ⏭️ | no comparable pandas-ta equivalent |
| `cheby2` | ⏭️ | no comparable pandas-ta equivalent |
| `cma` | ⏭️ | no comparable pandas-ta equivalent |
| `cmf` | ✔️ | max_diff=4.385e-15, n=100 |
| `cmo` | ✔️ | max_diff=0.000e+00, n=100 |
| `cointegration` | ⏭️ | no comparable pandas-ta equivalent |
| `conv` | ⚠️ | RuntimeError: unsupported arg kernel in generic sweep |
| `coral` | ⏭️ | no comparable pandas-ta equivalent |
| `correlation` | ⏭️ | no comparable pandas-ta equivalent |
| `covariance` | ⏭️ | no comparable pandas-ta equivalent |
| `crma` | ⏭️ | no comparable pandas-ta equivalent |
| `crsi` | ⚠️ | max_diff=1.111e+01, n=100 |
| `cti` | ✔️ | max_diff=9.880e-10, n=100 |
| `cv` | ⏭️ | no comparable pandas-ta equivalent |
| `cvi` | ⏭️ | no comparable pandas-ta equivalent |
| `cwt` | ⏭️ | no comparable pandas-ta equivalent |
| `deco` | ⏭️ | no comparable pandas-ta equivalent |
| `decycler` | ⏭️ | no comparable pandas-ta equivalent |
| `dem` | ⏭️ | no comparable pandas-ta equivalent |
| `dema` | ✔️ | max_diff=4.263e-14, n=100 |
| `dema_alpha` | ⏭️ | no comparable pandas-ta equivalent |
| `dosc` | ⏭️ | no comparable pandas-ta equivalent |
| `dpo` | ✔️ | max_diff=5.400e-13, n=100 |
| `dsma` | ⏭️ | no comparable pandas-ta equivalent |
| `dsp` | ⏭️ | no comparable pandas-ta equivalent |
| `dwma` | ⏭️ | no comparable pandas-ta equivalent |
| `dwt` | ⏭️ | no comparable pandas-ta equivalent |
| `dymoi` | ⏭️ | no comparable pandas-ta equivalent |
| `eacp` | ⏭️ | no comparable pandas-ta equivalent |
| `ebsw` | ⚠️ | max_diff=1.846e+00, n=100 |
| `edcf` | ⏭️ | no comparable pandas-ta equivalent |
| `efi` | ✔️ | max_diff=3.411e-13, n=100 |
| `elliptic` | ⏭️ | no comparable pandas-ta equivalent |
| `ema` | ✔️ | max_diff=2.842e-14, n=100 |
| `ema_alpha` | ⏭️ | no comparable pandas-ta equivalent |
| `entropy` | ⚠️ | max_diff=2.723e+00, n=100 |
| `eom` | ⚠️ | max_diff=4.374e+00, n=100 |
| `er` | ✔️ | max_diff=0.000e+00, n=100 |
| `etherm` | ⏭️ | no comparable pandas-ta equivalent |
| `evwma` | ⏭️ | no comparable pandas-ta equivalent |
| `ewma` | ⏭️ | no comparable pandas-ta equivalent |
| `expdist` | ⏭️ | no comparable pandas-ta equivalent |
| `exptrans` | ⏭️ | no comparable pandas-ta equivalent |
| `fisher` | ⚠️ | max_diff=1.129e+00, n=100 |
| `fisher04` | ⏭️ | no comparable pandas-ta equivalent |
| `gdema` | ⏭️ | no comparable pandas-ta equivalent |
| `hanma` | ⏭️ | no comparable pandas-ta equivalent |
| `hema` | ⏭️ | no comparable pandas-ta equivalent |
| `hma` | ⚠️ | max_diff=1.438e+00, n=100 |
| `inertia` | ⚠️ | max_diff=7.771e+01, n=100 |
| `kri` | ⏭️ | no comparable pandas-ta equivalent |
| `lema` | ⏭️ | no comparable pandas-ta equivalent |
| `lsma` | ⏭️ | no comparable pandas-ta equivalent |
| `mae` | ⏭️ | no comparable pandas-ta equivalent |
| `mape` | ⏭️ | no comparable pandas-ta equivalent |
| `medprice` | ⚠️ | max_diff=4.236e+00, n=100 |
| `mfi` | ✔️ | max_diff=3.091e-13, n=100 |
| `midbody` | ⏭️ | no comparable pandas-ta equivalent |
| `mom` | ✔️ | max_diff=0.000e+00, n=100 |
| `mse` | ⏭️ | no comparable pandas-ta equivalent |
| `nvi` | ⏭️ | no comparable pandas-ta equivalent |
| `obv` | ✔️ | max_diff=0.000e+00, n=100 |
| `parzen` | ⏭️ | no comparable pandas-ta equivalent |
| `psl` | ✔️ | max_diff=0.000e+00, n=100 |
| `pvd` | ⏭️ | no comparable pandas-ta equivalent |
| `pvi` | ⏭️ | no comparable pandas-ta equivalent |
| `pvo` | ✔️ | max_diff=3.432e-14, n=100 |
| `pvr` | ⚠️ | max_diff=1.000e+00, n=100 |
| `pvt` | ✔️ | max_diff=2.728e-12, n=100 |
| `rain` | ⏭️ | no comparable pandas-ta equivalent |
| `reflex` | ⚠️ | max_diff=3.668e-01, n=100 |
| `rmse` | ⏭️ | no comparable pandas-ta equivalent |
| `roc` | ✔️ | max_diff=8.882e-16, n=100 |
| `rsi` | ✔️ | max_diff=2.842e-14, n=100 |
| `rsx` | ✔️ | max_diff=1.172e-13, n=100 |
| `sgma` | ⏭️ | no comparable pandas-ta equivalent |
| `sinema` | ⏭️ | no comparable pandas-ta equivalent |
| `sma` | ✔️ | max_diff=9.948e-14, n=100 |
| `sp15` | ⏭️ | no comparable pandas-ta equivalent |
| `stddev` | ✔️ | max_diff=4.896e-11, n=100 |
| `swma` | ✔️ | max_diff=2.842e-14, n=100 |
| `tema` | ✔️ | max_diff=9.948e-14, n=100 |
| `tr` | ✔️ | max_diff=0.000e+00, n=100 |
| `trendflex` | ⚠️ | max_diff=3.650e-01, n=100 |
| `trima` | ⚠️ | max_diff=4.349e-01, n=100 |
| `trix` | ✔️ | max_diff=2.734e-14, n=100 |
| `tsf` | ⏭️ | no comparable pandas-ta equivalent |
| `tsi` | ⚠️ | max_diff=7.203e-01, n=100 |
| `tukey_w` | ⏭️ | no comparable pandas-ta equivalent |
| `tvi` | ⏭️ | no comparable pandas-ta equivalent |
| `typprice` | ⚠️ | max_diff=9.796e-01, n=100 |
| `variance` | ✔️ | max_diff=9.209e-11, n=100 |
| `vf` | ⏭️ | no comparable pandas-ta equivalent |
| `vwma` | ✔️ | max_diff=1.137e-13, n=100 |
| `wma` | ✔️ | max_diff=5.165e-10, n=100 |
| `zscore` | ⚠️ | max_diff=6.992e-02, n=100 |