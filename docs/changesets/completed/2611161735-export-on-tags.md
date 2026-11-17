# Export Builds Only on Release Tags

- Status: Implemented
- Id: 2611161735-export-on-tags
- Depends-on: 2611151728-ci-builds

## Summary

The workflow's export job runs only for `v*` tags and manual runs. Pushes to `main` and pull
requests run the tests alone.

## Motivation

Exporting on every push to `main` builds three zips nobody asked for and spends runner time on
each commit. Builds are wanted when a release is tagged, or on request.

## Design

- **test**: unchanged. It runs on pushes to `main`, `v*` tags, pull requests, and by hand.
- **export**: runs after test when the ref is a `v*` tag or the run was started by hand
  (`workflow_dispatch`). A manual run exports whichever branch or tag it was started on.

## Code changes

- `.github/workflows/build.yml`: the export job's condition is
  `startsWith(github.ref, 'refs/tags/v') || github.event_name == 'workflow_dispatch'`, replacing
  `github.event_name != 'pull_request'`.

## Blueprint updates

- builds.md: the overview and the export job's bullet say when exports run.

## Verification

- `act -l` parses the workflow and lists both jobs.
- On GitHub, a push to `main` shows only the test job; pushing a `v*` tag, or a manual run, runs
  both.
