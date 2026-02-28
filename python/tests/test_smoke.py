from __future__ import annotations

from pathlib import Path

import pytest

from quantalib._loader import native_library_path


def test_native_library_path_is_resolvable() -> None:
    path = native_library_path()
    assert isinstance(path, Path)


def test_loader_fails_with_actionable_message_when_binary_missing(
    monkeypatch,
) -> None:
    """Verify a clear OSError when the native binary is missing."""
    import quantalib._loader as loader

    # Point native_library_path to a non-existent file
    fake = Path(__file__).parent / "nonexistent" / "quantalib_native.dll"
    monkeypatch.setattr(loader, "native_library_path", lambda: fake)

    with pytest.raises(OSError) as exc:
        loader.load_native_library()

    msg = str(exc.value).lower()
    assert "native library" in msg
    assert "expected" in msg