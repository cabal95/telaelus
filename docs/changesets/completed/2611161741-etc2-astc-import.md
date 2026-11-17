# Import ETC2/ASTC Textures for macOS

- Status: Implemented
- Id: 2611161741-etc2-astc-import
- Depends-on: 2611161735-export-on-tags

## Summary

The project turns on ETC2/ASTC texture import explicitly, so the macOS export works on GitHub's
x64 runner.

## Motivation

The first tagged export on GitHub failed: "Cannot export for universal or arm64 if ETC2 ASTC
texture format is disabled." The setting was unset, and Godot's default depends on the machine
running the editor. It is on for an ARM64 editor and off for an x64 one. Every earlier export ran
on ARM64 (this machine and the arm64 act container), so they all passed.

## Design

- `rendering/textures/vram_compression/import_etc2_astc=true`, beside the explicit
  `import_s3tc_bptc=true`. The universal macOS build needs both.
- Textures already carried both formats on this machine, so no `.import` file changes.

## Code changes

- `src/Telaelus/project.godot`: adds `textures/vram_compression/import_etc2_astc=true`.

## Blueprint updates

- builds.md: both texture formats are set explicitly, and why.

## Verification

- With the x64 Windows editor (run under emulation here): without the setting, the macOS export
  fails with GitHub's error; with it, the export passes the preset check. That editor then cannot
  build C# here, because it loads the ARM64 .NET SDK's MSBuild. GitHub's runner has an x64 SDK.
- `python ci/export.py export` with the ARM64 editor builds all three zips.
- The next tagged run on GitHub exports all three.
