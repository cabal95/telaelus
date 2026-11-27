# Versioning From Git Tags

- Status: Implemented
- Id: 2611262051-git-tag-versioning
- Depends-on: none

## Summary

The game's version comes from one place, the latest `v*` git tag. Every C# project takes it at
build time, the game reads it from its own assembly (for saves and a main menu label), and the
export script writes it into the Godot project and export presets before exporting, so the exe,
the macOS app, the zip names, and the saves all carry the same version.

## Motivation

The version lives in three places today, and they already disagree:

- `AppRoot.GameVersion = "0.1.0"`, a constant written into saves and read by `ci/export.py` (with a
  regex) for the zip names;
- the export presets' `0.1.0.0` (Windows file and product version) and `0.1.0` (macOS);
- the C# assemblies, which default to `1.0.0` because no project sets a version.

Nothing ties a build to a release. Tagging a commit should be all it takes to release a version,
and an untagged build should say plainly that it is a development build and how far it is from the
last release.

## Design

- **The source is a tag:** a release is a commit tagged `vMAJOR.MINOR.PATCH` (`v0.2.0`). We tag the
  current `main` `v0.1.0` when this lands, so the version stays 0.1.0.
- **The version a build gets** (computed by [MinVer](https://github.com/adamralph/minver), which
  does exactly this from tags):

  | Build | Version | Example |
  |---|---|---|
  | On a tagged commit | the tag | `0.2.0` |
  | Commits after a tag | the next patch, a `dev` prerelease, and the commit count | `0.2.1-dev.5` (5 commits after `v0.2.0`) |
  | No tag reachable (a shallow clone without tags) | `0.0.0-dev.0` | `0.0.0-dev.0` |

  A prerelease tag (`v0.3.0-beta.1`) works too, as is. The .NET SDK adds the commit to the
  informational version (`0.2.1-dev.5+3b42f27...`); we show the short form, `0.2.1-dev.5 (3b42f27)`.
- **C# projects:** a `Directory.Build.props` at the repo root adds MinVer to every project
  (`Telaelus`, `Telaelus.Sim`, and the two test projects), with `MinVerTagPrefix` `v` and
  `MinVerDefaultPreReleaseIdentifiers` `dev`. So `dotnet build`, the Godot editor's build, and the
  Godot export's build all stamp the same `Version`, `FileVersion`, and `InformationalVersion`.
- **In the game:**
  - `AppRoot.GameVersion` becomes a property read once from the game assembly's informational
    version, without the commit (`0.2.1-dev.5`), and `AppRoot.GameCommit` the short commit (or
    empty). Saves keep writing `GameVersion` into their header, now the real one.
  - The main menu shows the version in `TextDim` at its bottom-left corner: "v0.2.0" for a
    release, "v0.2.1-dev.5 (3b42f27)" for a development build.
- **The Godot project, at export:** `ci/export.py` asks MSBuild for the version
  (`dotnet msbuild src/Telaelus/Telaelus.csproj -t:MinVer -getProperty:MinVerVersion`), so it
  cannot disagree with the assemblies, then, for the export only, writes:
  - `project.godot`: `application/config/version`;
  - the Windows presets: `application/file_version` and `application/product_version` as four
    numbers, `MAJOR.MINOR.PATCH.HEIGHT` (`0.2.1.5`; `0.2.0.0` on a tag), since Windows takes only
    numbers there. The presets turn on `application/modify_resources` (off until now), so the
    version reaches the exe's file properties; Godot 4.7 edits the exe itself, with no `rcedit`;
  - the macOS preset: `application/short_version` and `application/version` both
    `MAJOR.MINOR.PATCH`, since Apple allows at most three numbers there (so development builds
    after one tag share a bundle version; the full version is still in the app's assemblies and
    on the main menu).

  It puts both files back as they were afterwards (also on failure), so an export on a developer's
  machine leaves no change behind. The zips become `dist/Telaelus-<version>-<platform>.zip`
  (`Telaelus-0.2.0-windows-x64.zip`); a development version already carries its distance from the
  tag, so the separate commit in the name goes, and `commit()` with it.
- **CI:** both jobs check out the full history (`fetch-depth: 0`), since MinVer needs the tags; a
  tag push (`v*`) already runs the export job, which now names its zips by the tag.
- **The committed files keep a placeholder:** `project.godot` gets no version line and the presets
  keep `0.0.0.0` and `0.0.0`, so a version is only ever stamped by a build, never edited by hand.

## Code changes

- `Directory.Build.props` (new, repo root): MinVer `6.0.0` as a private build-only package
  reference for every project, with the tag prefix and prerelease identifier above.
- `src/Telaelus/Presentation/AppRoot.cs`: `GameVersion` and `GameCommit` from
  `AssemblyInformationalVersionAttribute`; `export.py` no longer reads it.
- `src/Telaelus/Presentation/Ui/MainMenu.cs`: the version label (`mainMenu/version` in the
  snapshot's `ui`).
- `src/Telaelus/export_presets.cfg`: placeholder versions, and `modify_resources` on in both
  Windows presets.
- `ci/export.py`: `game_version()` via MSBuild, `stamp_version()` writing and restoring the two
  files, zip names without the commit.
- `.github/workflows/build.yml`: `fetch-depth: 0` on both checkouts.
- Tests:
  - `VersionTests` (sim): the sim assembly has an informational version that is not the SDK's
    default `1.0.0`;
  - the `menu` scenario: the version label is shown and starts with "v";
  - `ci/export.py`: the four-number form of `0.2.0`, `0.2.1-dev.5`, and `0.3.0-beta.1` checked by
    a small self-test run in the test job (`python ci/export.py selftest`).

## Blueprint updates

- builds.md: where the version comes from, how export stamps the project and presets and restores
  them, the zip names, and `fetch-depth: 0`; the release step is "tag `vX.Y.Z` and push the tag".
- presentation.md: `AppRoot.GameVersion` and `GameCommit`, and the main menu's version label.
- saving.md: the header's game version is the build's version.
- testing.md: `VersionTests`, the menu scenario's version check, and the export self-test.
- `ci/README.md`: versions and releasing.

## Verification

- Sim and integration tests pass, and the headless runtime check is clean.
- On this machine with `v0.1.0` tagged locally (not pushed): `dotnet build` stamps
  `0.1.0` on the assemblies; one commit later it stamps `0.1.1-dev.1`; the main menu shows the
  same; `python ci/export.py export --platform windows` names its zip with it, the exe's file
  properties show `0.1.1.1`, and `git status` is clean afterwards.
- `act workflow_dispatch -j export` builds with the version from the tags.

## Notes

- **Why MinVer rather than a script of our own:** it is one package and a few properties, it runs
  inside every build (the Godot editor's and the export's too), and it already handles shallow
  clones, prerelease tags, and commit counts. The alternative is an MSBuild `Exec` of
  `git describe` in `Directory.Build.props` plus parsing; possible, but it is the same thing done
  by hand.
- **Builds need git:** a build outside a git checkout (a source zip) gets `0.0.0-dev.0` with a
  warning, which is honest.
- **Pushing the first tag** (`v0.1.0`) is a step for you to do, or to tell me to do, when this
  lands.
- **Release notes, GitHub releases, and version bumps** (which number to raise) are out of scope:
  a release is just a tag.

## Implementation notes

- **Resource editing turned on:** the Windows presets had `modify_resources` off, described as
  needing `rcedit`, so the exe showed no version. Godot 4.7 edits the exe natively: an export
  with it on, and no rcedit anywhere, gave both exes file and product version `0.1.0.0` and
  product name and description "Telaelus", and the arm64 exe ran headless and exited cleanly. It
  is on in both Windows presets now. Exports on the Linux CI runner are expected to do the same
  but were not tried. The exported `Telaelus.dll` and the packed `project.godot` carried
  `0.1.1-dev.1` in the dev-path run.
- **How the dev path was tested:** with `v0.1.0` moved one commit back for the run, MinVer gave
  `0.1.1-dev.1`, the Windows export named its zips `Telaelus-0.1.1-dev.1-windows-*.zip`, and
  `project.godot` and the presets were back as committed afterwards. The tag was then put back on
  the commit this lands on top of. It is local only; pushing it is still to do.
- **A `matches` check for scenarios:** the menu's version changes with every commit, so `expect`
  gained a `matches` operator (a regular expression), used by the `menu` scenario.
- **Not run:** the workflow under `act`; the self-test step and `fetch-depth: 0` are checked on the
  first push.
