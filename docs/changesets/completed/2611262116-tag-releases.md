# Tag Releases

- Status: Implemented
- Id: 2611262116-tag-releases
- Depends-on: 2611262051-git-tag-versioning

## Summary

A `v*` tag push makes a GitHub Release for that tag with the three build zips attached, so a
release can be downloaded from the repository's Releases page instead of from a workflow run's
artifacts, which expire after 14 days.

## Motivation

Tagging is now how a version is released (2611262051-git-tag-versioning), but its builds land
only as workflow artifacts: found through the Actions tab, and gone after 14 days. A Release
keeps them with the tag for good, under a stable page per version.

## Design

- **When:** only for a tag push (`refs/tags/v*`), after the export succeeds. A manual run
  (`workflow_dispatch`) keeps making artifacts only, as today, since it has no tag to release.
  Under `act` the release step is skipped, as the uploads are.
- **What:** a release named "Telaelus 0.2.0" for tag `v0.2.0`, with
  `Telaelus-0.2.0-windows-x64.zip`, `Telaelus-0.2.0-windows-arm64.zip`, and
  `Telaelus-0.2.0-macos.zip` attached as they are (the same zips as the artifacts).
  - **Notes:** GitHub's generated notes (the commits and pull requests since the previous
    release), which can be edited on the release page afterwards.
  - **Prerelease:** a tag with a prerelease part (`v0.3.0-beta.1`) is marked a prerelease, so it
    is not shown as the latest release.
  - **Published, not a draft:** the release is public as soon as the export finishes.
- **Re-runs:** if the release for the tag already exists (a re-run of the job), the zips are
  uploaded again over the old ones (`--clobber`) and the notes are left alone.
- **How:** the GitHub CLI (`gh`), preinstalled on GitHub's runners, with the workflow's own
  token; the export job gets `contents: write` permission for it. No third-party action.
- **Artifacts stay:** the export job keeps uploading its artifacts (14 days), so a manual run and
  a tag run both still have them.

## Code changes

- `.github/workflows/build.yml`, the export job:
  - `permissions: contents: write`;
  - a final step, only when `github.ref` starts with `refs/tags/v` and not under `act`, running
    `python ci/export.py release` with `GH_TOKEN` set to the workflow token.
- `ci/export.py`: a `release` command that, for the tag in `GITHUB_REF_NAME`, creates the release
  (`gh release create <tag> dist/*.zip --title "Telaelus <version>" --generate-notes`, with
  `--prerelease` for a prerelease tag) or, when one exists, uploads the zips to it
  (`gh release upload <tag> dist/*.zip --clobber`). It checks that the tag's version matches the
  zips' version, so a release never carries the wrong build.
- `selftest`: the prerelease rule (`v0.2.0` not, `v0.3.0-beta.1` yes) and the title.

## Blueprint updates

- builds.md: the export job's release step, its permission, the release's name, notes,
  prerelease rule, and re-run behavior; artifacts kept as well.
- `ci/README.md`: releasing ends with the Release page.

## Verification

- `python ci/export.py selftest` passes.
- The workflow, by tagging the next version and pushing the tag: the Release appears with its
  three zips and generated notes, and the zips match the run's artifacts. A re-run of the export
  job replaces the zips without a second release.
- `act` cannot exercise the release step (it is skipped there), so the first tag push is the
  real check.

## Notes

- **v0.1.1 is already pushed** at a commit without this change, so its run makes artifacts only,
  and re-running it would not make a Release either (a re-run uses the tag's own workflow). The
  first Release comes from the next tag. A Release for v0.1.1 could still be made by hand from
  its artifacts on the Releases page.
- **Draft releases** are the alternative to publishing at once: the release would wait on the
  Releases page for you to publish it. Publishing at once matches "a release is a tag".

## Implementation notes

- **Tested without GitHub:** the `release` command was run with `gh` replaced by a fake that only
  recorded its commands. It refused a `dist/` holding another version's zips or none, created the
  release with `--generate-notes` (and `--prerelease` for `v0.3.0-beta.1`), and on an existing
  release uploaded with `--clobber`. `act -l` parses the workflow. The real release is first made
  by the next tag push.
- **The step's condition** is the same `refs/tags/v` test as the job's, plus `!env.ACT`, so a
  manual run and `act` skip it.
