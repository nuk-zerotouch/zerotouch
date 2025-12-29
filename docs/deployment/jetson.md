# Deploying ZeroTouch.UI on Jetson Orin Nano (ARM64)

## Problem

Running the Avalonia frontend on Jetson Orin Nano may fail with errors like:

- undefined symbol: `uuid_parse`
- undefined symbol: `FT_Get_BDF_Property`

These errors originate from SkiaSharp native bindings on ARM64.

## Root Cause

- SkiaSharp bundles native binaries that expect certain system libraries
- On Jetson (Ubuntu ARM64), these symbols exist but are not automatically resolved
- Dynamic linker fails to load required symbols at runtime

## Solution

Explicitly preload required system libraries using LD_PRELOAD:

- libuuid.so.1
- libfreetype.so.6

A helper script is provided:

```bash
./scripts/run-jetson.sh
```

If this is your first time running the script, make sure it is executable:

```bash
chmod +x scripts/run-jetson.sh
```
