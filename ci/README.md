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
`export` writes `dist/Telaelus-<version>-<commit>-windows-x64.zip`, `...-windows-arm64.zip`, and
`...-macos.zip`. Add `--platform windows` or `--platform macos` for one, or `--editor <path>` to use
an installed editor instead (its templates go where that editor looks for them).

## The workflow in Docker

With [act](https://nektosact.com) and Docker Desktop running, from the repo root:

```bash
act push -j test
act push -j export --artifact-server-path test-output/act-artifacts
```

`-j test` runs the test job and `-j export` the test job then the export job, in a container like
GitHub's Ubuntu runner. The repo's `.actrc` picks the runner image
(`catthehacker/ubuntu:act-latest`) and runs .NET without ICU
(`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`), which that image lacks and GitHub's runner has. The
first run downloads Godot inside the container; `act` caches it for later runs as GitHub does. On
an ARM64 machine the container is arm64 Linux, so the script takes the arm64 Linux editor;
GitHub's runner is x64. The export job's zips land under `test-output/act-artifacts`.
