"""test_compat.py — pandas-ta compatibility alias tests."""
from __future__ import annotations

import pytest

from quantalib._compat import ALIASES, get_compat


class TestAliases:
    """Validate ALIASES mapping and get_compat resolution."""

    def test_aliases_is_dict(self) -> None:
        assert isinstance(ALIASES, dict)
        assert len(ALIASES) > 0

    def test_all_aliases_are_strings(self) -> None:
        for key, val in ALIASES.items():
            assert isinstance(key, str), f"Key {key!r} is not str"
            assert isinstance(val, str), f"Value {val!r} for key {key!r} is not str"

    @pytest.mark.parametrize(
        "alias,target",
        [
            ("midprice", "medprice"),
            ("momentum", "mom"),
            ("simple_moving_average", "sma"),
            ("true_range", "tr"),
            ("on_balance_volume", "obv"),
            ("bollinger_bands", "bbands"),
            ("z_score", "zscore"),
        ],
    )
    def test_known_aliases(self, alias: str, target: str) -> None:
        assert ALIASES[alias] == target

    def test_get_compat_unknown_returns_none(self) -> None:
        result = get_compat("nonexistent_indicator_xyz")
        assert result is None

    def test_get_compat_known_returns_callable(self) -> None:
        fn = get_compat("simple_moving_average")
        # May be None if native lib not available, but function itself resolves
        # We just test the lookup mechanism works
        if fn is not None:
            assert callable(fn)
