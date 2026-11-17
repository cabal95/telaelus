# Builds

## Overview

Playable builds are exported for Windows (x64 and arm64) and macOS (universal) by one script,
`ci/export.py`, which runs the same on a developer machine and in GitHub Actions. The workflow
tests every push and pull request and exports builds as downloadable artifacts for release tags
and manual runs.

## Current state

### Export presets

`src/Telaelus/export_presets.cfg`:

| Preset | Platform | Output |
| --- | --- | --- |
| Windows x64 | Windows Desktop, x86_64 | `Telaelus.exe` (the `.pck` embedded) and `data_Telaelus_windows_x86_64/` |
| Windows arm64 | Windows Desktop, arm64 | `Telaelus.exe` and `data_Telaelus_windows_arm64/` |
| macOS | macOS, universal | `Telaelus.zip` holding `Telaelus.app`, with .NET data for x86_64 and arm64 |

- Release exports leave out the debug-only scenario runner and control channel
  (`OS.IsDebugBuild`).
- The Windows presets do not edit the executable's resources (no `rcedit`), so the exe has
  Godot's icon and version info.
- The macOS bundle identifier is `com.telaelus.game`, version 0.1.0.
- The project imports both S3TC/BPTC textures (`rendering/textures/vram_compression/import_s3tc_bptc`,
  which x86_64 macOS needs) and ETC2/ASTC textures (`import_etc2_astc`, which arm64 macOS needs).
  Both are set explicitly: Godot's defaults follow the machine running the editor (an x64 editor
  leaves ETC2/ASTC off, an ARM64 one turns it on), so an unset value passes on an ARM64 machine
  and fails on GitHub's x64 runner.
- Godot finds the C# solution at the repo root (`dotnet/project/solution_directory="../.."`, where
  `Telaelus.slnx` is); without it an export skips the C# build and still exits 0.

### Signing

- Windows builds are unsigned: SmartScreen warns on first launch ("More info", then "Run
  anyway").
- macOS builds are ad-hoc signed by Godot's built-in signer and not notarized: Gatekeeper blocks
  the first launch of a downloaded app; right-click and Open, or System Settings > Privacy &
  Security > Open Anyway.

### The export script

`ci/export.py` (Python 3, standard library only; `ci/README.md` has the commands):

- `setup` downloads the Godot .NET editor for the host (the version in `ci/godot-version.txt`,
  4.7.2-stable) from Godot's GitHub releases into `.ci-cache/`, makes it self-contained (`_sc_`,
  so its settings and templates stay beside it), and extracts only the templates the presets need
  (`windows_release_x86_64.exe`, `windows_release_arm64.exe`, `macos.zip`) from the template
  archive. On GitHub it writes `GODOT_EDITOR` for later steps.
- `import` imports the project headless.
- `export [--platform windows|macos|all]` imports, exports each preset with `--export-release`,
  and zips each into `dist/Telaelus-<version>-<commit>-<platform>.zip` (the version is
  `AppRoot.GameVersion`). The macOS export is already a zip, which keeps the app's executable
  flag. A build without `Telaelus.dll` fails.
- Godot's output is read from its `--log-file`, since the Windows editor prints nothing when its
  output is redirected.
- `--editor <path>` uses an installed editor instead of the downloaded one.
- `.ci-cache/` and `dist/` are git-ignored.

### The workflow

`.github/workflows/build.yml` uses standard GitHub actions (checkout v7, setup-dotnet v6 with
.NET 8 and 9, setup-python v7, cache v6 of `.ci-cache/` keyed on the Godot version and the script,
upload-artifact v7), all on Node.js 24, and the script for everything Godot; no third-party Godot
actions.

- **test** (`ubuntu-latest`), on pushes to `main`, `v*` tags, pull requests, and by hand: set up
  Godot, `dotnet build Telaelus.slnx`, import, then the sim tests and the integration tests (with
  `GODOT_EDITOR` from setup).
- **export** (after test, only on `v*` tags and manual runs from the Actions tab, so pushes to
  `main` and pull requests only test): `export.py export --platform all`, then each build's zip
  uploaded as is (`archive: false`), so the artifact is named after the zip
  (`Telaelus-<version>-<commit>-<platform>.zip`) and a download is that zip itself, kept 14 days.
  The macOS zip must be ours: a zip GitHub makes drops the app's executable flag.
- The build has no warnings. `.editorconfig` sets `end_of_line = lf` for C#, matching
  `.gitattributes`; without it the formatter expects CRLF on Windows and warns on Windows only.

### Running it locally

- The script directly: `python ci/export.py setup`, then `python ci/export.py export`.
- The workflow in Docker with `act`: `act push -j test`, or `act workflow_dispatch -j export`
  (a manual run, since a push to `main` skips the export job). Under `act` (which sets `ACT`) the
  export job skips its uploads: `act`'s artifact server cannot take `upload-artifact@v7` yet
  (nektos/act#6022). The repo's `.actrc` sets the runner image
  (`catthehacker/ubuntu:act-latest`) and `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`, since that
  image has no ICU library (so the test projects' build prints `NETSDK1188` locale warnings
  there only). `act` keeps its own cache, so only its first run downloads Godot. On
  an ARM64 machine the container is arm64 Linux and the script takes the arm64 Linux editor.

## Related

- testing.md (the tests the workflow runs), presentation.md (`AppRoot.GameVersion`).
