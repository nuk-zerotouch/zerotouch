# macOS Deployment Notes

This document describes known limitations when running ZeroTouch.UI
on macOS, specifically related to media player using **libVLC**.

---

## libVLC on macOS

Currently, **libVLC-based media player is not supported on macOS**
in this project.

<br>

### Observed Issues

- Native library loading failures at runtime
- Incompatible or missing VLC frameworks
- Unreliable behavior when loading libVLC via .NET bindings

These issues occur even when VLC is installed system-wide using Homebrew.

<br>

## Root Cause

The problem is **not caused by ZeroTouch.UI itself**, but by a combination of:

- macOS dynamic library loading rules
- VLC framework distribution on macOS
- Limitations of libVLCSharp bindings on macOS

Due to these constraints, libVLC cannot be safely or consistently loaded
at runtime in the current architecture.

<br>

## Current Status

- ✅ **Windows**: Supported
- ✅ **Linux**: Supported
- ❌ **macOS**: Not supported (libVLC disabled)

On macOS, media-related features depending on libVLC are currently
disabled or unavailable.

---

## Notes

This limitation is documented to prevent confusion during development
and deployment.
