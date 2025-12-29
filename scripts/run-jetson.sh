#!/usr/bin/env bash
set -e

# Workaround for SkiaSharp native symbol resolution on ARM64 (Jetson)
export LD_PRELOAD="/lib/aarch64-linux-gnu/libuuid.so.1:/lib/aarch64-linux-gnu/libfreetype.so.6"

dotnet run --project ZeroTouch.UI
