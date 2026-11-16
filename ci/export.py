"""Sets up Godot, imports the project, and exports Telaelus builds.

The same script runs on GitHub Actions and on a developer machine, so a build can be proven
locally before it is pushed. See ci/README.md.

  python ci/export.py setup                  download the Godot .NET editor and the templates
  python ci/export.py import                 import the project headless (after a C# build)
  python ci/export.py export [--platform P]  export builds into dist/ (P: windows, macos, all)

Every command takes --editor PATH to use an installed editor instead of the downloaded one.
"""

import argparse
import os
import platform
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PROJECT = ROOT / "src" / "Telaelus"
CACHE = ROOT / ".ci-cache"
DIST = ROOT / "dist"
VERSION = (ROOT / "ci" / "godot-version.txt").read_text().strip()
RELEASES = f"https://github.com/godotengine/godot/releases/download/{VERSION}"
TEMPLATE_FOLDER = VERSION.replace("-", ".") + ".mono"

# Preset name, the template it needs, the output's file name, and the zip's platform suffix.
PRESETS = {
    "windows": [
        ("Windows x64", "windows_release_x86_64.exe", "Telaelus.exe", "windows-x64"),
        ("Windows arm64", "windows_release_arm64.exe", "Telaelus.exe", "windows-arm64"),
    ],
    "macos": [
        ("macOS", "macos.zip", "Telaelus.zip", "macos"),
    ],
}


def host_build():
    """The editor download for this machine: (zip name, executable inside it)."""
    machine = platform.machine().lower()
    arm = machine in ("arm64", "aarch64")
    base = f"Godot_v{VERSION}_mono"
    if sys.platform == "win32":
        name = f"{base}_windows_arm64" if arm else f"{base}_win64"
        return name, f"{name}/{name}.exe"
    if sys.platform.startswith("linux"):
        name = f"{base}_linux_{'arm64' if arm else 'x86_64'}"
        return name, f"{name}/{base}_linux.{'arm64' if arm else 'x86_64'}"
    if sys.platform == "darwin":
        name = f"{base}_macos.universal"
        return name, "Godot_mono.app/Contents/MacOS/Godot"
    sys.exit(f"no Godot download for {sys.platform}")


def download(url, target):
    print(f"downloading {url}", flush=True)
    with urllib.request.urlopen(url) as response, open(target, "wb") as out:
        shutil.copyfileobj(response, out, 1 << 20)


def editor_dir():
    return CACHE / f"godot-{VERSION}"


def downloaded_editor():
    _, exe = host_build()
    return editor_dir() / exe


def templates_dir(editor):
    """Where an editor looks for its export templates."""
    if (editor.parent / "_sc_").exists():
        return editor.parent / "editor_data" / "export_templates" / TEMPLATE_FOLDER
    if sys.platform == "win32":
        return Path(os.environ["APPDATA"]) / "Godot" / "export_templates" / TEMPLATE_FOLDER
    if sys.platform == "darwin":
        return Path.home() / "Library" / "Application Support" / "Godot" / "export_templates" / TEMPLATE_FOLDER
    data = Path(os.environ.get("XDG_DATA_HOME", Path.home() / ".local" / "share"))
    return data / "godot" / "export_templates" / TEMPLATE_FOLDER


def resolve_editor(arg):
    editor = Path(arg) if arg else downloaded_editor()
    if not editor.exists():
        sys.exit(f"no Godot editor at {editor}: run 'python ci/export.py setup' first")
    return editor


def setup(arg):
    """Downloads the editor (self-contained, in .ci-cache) and the templates the presets need."""
    if arg:
        editor = Path(arg)
    else:
        editor = downloaded_editor()
        if not editor.exists():
            name, _ = host_build()
            editor_dir().mkdir(parents=True, exist_ok=True)
            with tempfile.TemporaryDirectory() as tmp:
                archive = Path(tmp) / f"{name}.zip"
                download(f"{RELEASES}/{name}.zip", archive)
                with zipfile.ZipFile(archive) as z:
                    z.extractall(editor_dir())
            editor.chmod(editor.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)
        # Self-contained: the editor keeps its settings and templates beside it, not in the home folder.
        (editor.parent / "_sc_").touch()

    templates = templates_dir(editor)
    wanted = {template for presets in PRESETS.values() for _, template, _, _ in presets} | {"version.txt"}
    missing = {name for name in wanted if not (templates / name).exists()}
    if missing:
        templates.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory() as tmp:
            archive = Path(tmp) / "templates.tpz"
            download(f"{RELEASES}/Godot_v{VERSION}_mono_export_templates.tpz", archive)
            with zipfile.ZipFile(archive) as z:
                for name in missing:
                    with z.open(f"templates/{name}") as src, open(templates / name, "wb") as out:
                        shutil.copyfileobj(src, out, 1 << 20)
    print(f"editor: {editor}")
    print(f"templates: {templates}")
    github_env = os.environ.get("GITHUB_ENV")
    if github_env:
        with open(github_env, "a") as env:
            env.write(f"GODOT_EDITOR={editor}\n")


def run_godot(editor, *args):
    # The Windows editor prints nothing when its output is redirected, so Godot's own log file is
    # what is shown.
    with tempfile.TemporaryDirectory() as tmp:
        log = Path(tmp) / "godot.log"
        command = [str(editor), "--headless", "--log-file", str(log), "--path", str(PROJECT), *args]
        print("> " + " ".join(command), flush=True)
        result = subprocess.run(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if log.exists():
            print(log.read_text(encoding="utf-8", errors="replace"), flush=True)
    if result.returncode != 0:
        sys.exit(f"Godot exited with {result.returncode}")


def import_project(editor):
    run_godot(editor, "--import")


def game_version():
    source = (PROJECT / "Presentation" / "AppRoot.cs").read_text(encoding="utf-8")
    match = re.search(r'GameVersion = "([^"]+)"', source)
    return match.group(1) if match else "0.0.0"


def commit():
    try:
        return subprocess.run(["git", "rev-parse", "--short", "HEAD"], cwd=ROOT, capture_output=True,
                              text=True, check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return "local"


def export(editor, platforms):
    import_project(editor)
    DIST.mkdir(exist_ok=True)
    stem = f"Telaelus-{game_version()}-{commit()}"
    for key in platforms:
        for preset, _, output, suffix in PRESETS[key]:
            folder = DIST / suffix
            shutil.rmtree(folder, ignore_errors=True)
            folder.mkdir()
            run_godot(editor, "--export-release", preset, str(folder / output))
            archive = DIST / f"{stem}-{suffix}.zip"
            archive.unlink(missing_ok=True)
            if output.endswith(".zip"):
                # The macOS export is already a zip (it keeps the app's executable flag).
                shutil.move(folder / output, archive)
            else:
                shutil.make_archive(str(archive.with_suffix("")), "zip", folder)
            shutil.rmtree(folder)
            check_build(archive)
            print(f"built {archive}", flush=True)


def check_build(archive):
    """Fails a build without the game's assembly: Godot exports one, and exits 0, when the C#
    build is skipped (as when it finds no solution)."""
    with zipfile.ZipFile(archive) as z:
        if not any(name.endswith("/Telaelus.dll") for name in z.namelist()):
            sys.exit(f"{archive.name} has no Telaelus.dll: the C# build did not run (see Godot's log above)")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("command", choices=["setup", "import", "export"])
    parser.add_argument("--platform", choices=["windows", "macos", "all"], default="all")
    parser.add_argument("--editor", help="an installed Godot .NET editor to use instead of the downloaded one")
    args = parser.parse_args()
    if args.command == "setup":
        setup(args.editor)
    elif args.command == "import":
        import_project(resolve_editor(args.editor))
    else:
        platforms = list(PRESETS) if args.platform == "all" else [args.platform]
        export(resolve_editor(args.editor), platforms)


if __name__ == "__main__":
    main()
