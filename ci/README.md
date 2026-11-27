# CI builds

`export.py` sets up Godot, imports the project, and exports the Windows and macOS builds. The
GitHub workflow (`.github/workflows/build.yml`) calls it, and so can you, so a build can be proven
on your machine before it is pushed. The blueprint is `docs/blueprints/builds.md`.

## On your machine

```bash
python ci/export.py setup
python ci/export.py export
```

`setup` downloads the Godot .NET editor for your machine (version in `godot-version.txt`) into
`.ci-cache/`, set up self-contained so it never touches your own Godot settings, and pulls the
three export templates the presets need out of Godot's template archive (a 1.1 GB download, once).
`export` writes `dist/Telaelus-<version>-windows-x64.zip`, `...-windows-arm64.zip`, and
`...-macos.zip`. Add `--platform windows` or `--platform macos` for one, or `--editor <path>` to use
an installed editor instead (its templates go where that editor looks for them).

## Versions and releasing

The version comes from the latest `vMAJOR.MINOR.PATCH` git tag (MinVer, set up in
`Directory.Build.props`): `0.2.0` on a tagged commit, `0.2.1-dev.5` five commits after it. Every
C# build stamps it, and `export` writes it into the Godot project and presets for the export only,
then restores them. To release, tag the commit and push the tag:

```bash
git tag -a v0.2.0 -m "Telaelus 0.2.0"
git push origin v0.2.0
```

The tag push runs the export job, which publishes the tag's release on the repository's Releases
page with the three zips attached (and keeps them as run artifacts for 14 days). `python ci/export.py version` prints the version a build gets,
and `python ci/export.py selftest` checks the version helpers.

## The workflow in Docker

With [act](https://nektosact.com) and Docker Desktop running, from the repo root:

```bash
act push -j test
act workflow_dispatch -j export
```

The first runs the test job as a push to `main` does. The second is a manual run, which runs the
test job then the export job (a push to `main` skips the export). Both run in a container like
GitHub's Ubuntu runner. The repo's `.actrc` picks the runner image
(`catthehacker/ubuntu:act-latest`) and runs .NET without ICU
(`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`), which that image lacks and GitHub's runner has. The
first run downloads Godot inside the container; `act` caches it for later runs as GitHub does. On
an ARM64 machine the container is arm64 Linux, so the script takes the arm64 Linux editor;
GitHub's runner is x64. The export job skips its uploads under `act`, whose artifact server cannot
take `upload-artifact@v7` yet ([nektos/act#6022](https://github.com/nektos/act/issues/6022)); the
export step's `built ...` lines and its `Telaelus.dll` check show the builds were made. Without
ICU, the test projects' build also prints about 130 `NETSDK1188` locale warnings that GitHub's
runner does not.
