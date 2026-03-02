from __future__ import annotations

import ctypes
import platform
from pathlib import Path


_PLATFORM_MAP: dict[tuple[str, str], str] = {
    ("Windows", "AMD64"): "win_amd64/quantalib_native.dll",
    ("Windows", "ARM64"): "win_arm64/quantalib_native.dll",
    ("Linux", "x86_64"): "linux_x86_64/quantalib_native.so",
    ("Linux", "aarch64"): "linux_arm64/quantalib_native.so",
    ("Darwin", "arm64"): "macosx_arm64/quantalib_native.dylib",
    ("Darwin", "x86_64"): "macosx_x86_64/quantalib_native.dylib",
}


def _native_root() -> Path:
    return Path(__file__).resolve().parent / "native"


def _native_relative_path() -> str:
    key = (platform.system(), platform.machine())
    if key not in _PLATFORM_MAP:
        raise OSError(
            f"Unsupported platform/architecture: system={key[0]!r}, arch={key[1]!r}"
        )
    return _PLATFORM_MAP[key]


def native_library_path() -> Path:
    return _native_root() / _native_relative_path()


def load_native_library() -> ctypes.CDLL:
    path = native_library_path()
    if not path.exists():
        raise OSError(
            "quantalib native library not found. "
            f"Expected: {path} "
            f"(system={platform.system()}, arch={platform.machine()})"
        )
    return ctypes.CDLL(str(path))