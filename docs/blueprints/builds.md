# Builds

## Overview

Playable builds are exported for Windows (x64 and arm64) and macOS (universal) by one script,
`ci/export.py`, which runs the same on a developer machine and in GitHub Actions. The workflow
tests every push and pull request and exports builds as downloadable artifacts for release tags
and manual runs.

## Current state

### Versions

- **A release is a git tag** `vMAJOR.MINOR.PATCH` (`v0.2.0`; a prerelease such as `v0.3.0-beta.1`
  works too). Releasing is tagging a commit and pushing the tag, which runs the export job.
- **Every C# project** (`Telaelus`, `Telaelus.Sim`, and the test projects) takes its version from
  the tags at build time through [MinVer](https://github.com/adamralph/minver), added to all of
  them by `Directory.Build.props` at the repo root (tag prefix `v`, prerelease identifier `dev`):

  | Build | Version |
  |---|---|
  | On a tagged commit | the tag: `0.2.0` |
  | Commits after a tag | the next patch, `dev`, and the commit count: `0.2.1-dev.5` |
  | No tag reachable | `0.0.0-dev.0` (with a MinVer warning) |

  The SDK adds the commit to the informational version (`0.2.1-dev.5+3b42f27...`). `dotnet
  build`, the Godot editor's build, and the export's build all stamp the same version, which the
  game shows on its main menu and writes into saves (presentation.md, saving.md).
- **The Godot project** carries no version of its own: `project.godot` has no
  `application/config/version` and the presets keep `0.0.0.0` and `0.0.0`. The export stamps them
  (below).

### Export presets

`src/Telaelus/export_presets.cfg`:

| Preset | Platform | Output |
| --- | --- | --- |
| Windows x64 | Windows Desktop, x86_64 | `Telaelus.exe` (the `.pck` embedded) and `data_Telaelus_windows_x86_64/` |
| Windows arm64 | Windows Desktop, arm64 | `Telaelus.exe` and `data_Telaelus_windows_arm64/` |
| macOS | macOS, universal | `Telaelus.zip` holding `Telaelus.app`, with .NET data for x86_64 and arm64 |

- Release exports leave out the debug-only scenario runner and control channel
  (`OS.IsDebugBuild`).
- The Windows presets edit the executable's resources (`application/modify_resources`), which
  Godot 4.7 does itself, with no `rcedit` or Wine: the exe's file and product version are the
  stamped version (`0.2.1.5`) and its product name and description "Telaelus". The project has no
  icon of its own, so the exe keeps Godot's.
- The macOS bundle identifier is `com.telaelus.game`; its version is stamped at export.
- Every preset includes `assets/saves/*.save` (`include_filter`): the demo save (saving.md), a file
  Godot does not import and would otherwise leave out.
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
- `export [--platform windows|macos|all]` imports, reads the version from MinVer
  (`dotnet msbuild src/Telaelus/Telaelus.csproj -t:MinVer -getProperty:MinVerVersion`), exports each
  preset with `--export-release`, and zips each into `dist/Telaelus-<version>-<platform>.zip`
  (`Telaelus-0.2.1-dev.5-windows-x64.zip`). For each export it stamps the version into
  `project.godot` (`application/config/version`) and the presets (Windows `file_version` and
  `product_version` as four numbers, `0.2.1.5`, the last the dev commit count or 0; macOS
  `short_version` and `version` as `0.2.1`, since Apple allows at most three numbers), then puts
  both files back as they were, even on failure, so an export leaves no change in the working
  tree.
- `selftest` checks the four- and three-number forms and the stamping, without Godot. The macOS export is already a zip, which keeps the app's executable
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

- Both jobs check out the whole history and its tags (`fetch-depth: 0`), which the version needs.
- **test** (`ubuntu-latest`), on pushes to `main`, `v*` tags, pull requests, and by hand: run the
  export script's self-test, set up Godot, `dotnet build Telaelus.slnx`, import, then the sim tests and the integration tests (with
  `GODOT_EDITOR` from setup).
- **export** (after test, only on `v*` tags and manual runs from the Actions tab, so pushes to
  `main` and pull requests only test): `export.py export --platform all`, then each build's zip
  uploaded as is (`archive: false`), so the artifact is named after the zip
  (`Telaelus-<version>-<platform>.zip`) and a download is that zip itself, kept 14 days.
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

- testing.md (the tests the workflow runs), presentation.md (`AppRoot.GameVersion` and the main
  menu's version label), saving.md (the version in a save's header).
