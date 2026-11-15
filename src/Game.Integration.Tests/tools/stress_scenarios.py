"""Run scenarios many times headless to catch intermittent crashes, and sort what happens.

Each run is the same headless launch the integration tests use. A run is classified as:

  ok         exited 0 with a passing report.json
  failed     the scenario itself failed (a nonzero exit code its report.json gives)
  shutdown   report.json says the scenario passed, but the process then died (a crash on quit)
  crash      the process died before writing report.json (a crash while running)
  timeout    still running after --timeout seconds, then killed

Every run keeps its stdout, stderr, report.json, shutdown.log (the runner's quit breadcrumbs), and
with --dumps a crash dump the .NET runtime writes itself when it dies (set through environment
variables on the Godot process only; no system settings change). A summary prints at the end and
is saved as summary.json, with how many runs left C# references to Godot objects at exit (the
crash's precondition, "Leaked unsafe reference" in stderr) and the distinct crash signatures (the .NET "Fatal error" or unhandled
exception lines and the first stack frames from stderr).

Usage (from the repo root; the Godot path comes from tools.json):
    python src/Game.Integration.Tests/tools/stress_scenarios.py trucks --runs 100 --jobs 4 --dumps
    python src/Game.Integration.Tests/tools/stress_scenarios.py --all --runs 5 --jobs 4
    python src/Game.Integration.Tests/tools/stress_scenarios.py trucks --runs 20 --exit-garbage 20000

--exit-garbage forces the shutdown race (finalizers still running as the engine quits); with the
shutdown fix in place those runs come out clean.
"""
import argparse
import json
import os
import subprocess
import sys
import time
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
SCENARIOS = REPO / "src" / "Game.Integration.Tests" / "Scenarios"
GAME = REPO / "src" / "Game"

# Exit codes Windows reports for the crashes we have seen, as unsigned 32-bit values.
NAMED_CODES = {
    0xC0000005: "access violation (0xC0000005)",
    0xC000001D: "illegal instruction (0xC000001D)",
    0xC0000409: "stack buffer overrun / fail fast (0xC0000409)",
    0xE0434352: ".NET exception (0xE0434352)",
}


def godot_path():
    tools = REPO / "tools.json"
    if not tools.exists():
        sys.exit("tools.json is missing: copy tools.template.json to tools.json and fill in the Godot path.")
    return json.loads(tools.read_text(encoding="utf-8"))["godot"]["editor"]


def scenario_files(names, run_all):
    if run_all:
        return sorted(SCENARIOS.glob("*.json"))
    files = []
    for name in names:
        path = SCENARIOS / (name if name.endswith(".json") else name + ".json")
        if not path.exists():
            sys.exit(f"no scenario {path}")
        files.append(path)
    return files


def describe_code(code):
    unsigned = code & 0xFFFFFFFF
    return NAMED_CODES.get(unsigned, f"exit {code} (0x{unsigned:08X})")


def signature(stderr):
    """The lines that say what killed the process: .NET's fatal or unhandled-exception lines and the first stack frames."""
    lines = [line.rstrip() for line in stderr.splitlines()]
    picked = []
    for i, line in enumerate(lines):
        if ("Fatal error" in line or "Unhandled exception" in line or "internal error in the .NET Runtime" in line
                or line.startswith("   at ") or "CrashHandler" in line or "Program crashed" in line):
            picked.append(line.strip())
    # Keep the head of it: the message and the top frames identify the crash.
    return " | ".join(picked[:8])


def run_once(godot, scenario, out_dir, timeout, dumps, dump_type, exit_garbage):
    out_dir.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ)
    if dumps:
        env["DOTNET_DbgEnableMiniDump"] = "1"
        env["DOTNET_DbgMiniDumpType"] = str(dump_type)
        env["DOTNET_DbgMiniDumpName"] = str(out_dir / "crash-%p.dmp")
        env["DOTNET_CreateDumpDiagnostics"] = "1"
    args = [godot, "--headless", "--path", str(GAME), "--fixed-fps", "30", "--",
            f"--scenario={scenario}", f"--output={out_dir}"]
    if exit_garbage:
        args.append(f"--exit-garbage={exit_garbage}")
    started = time.time()
    try:
        proc = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace",
                              timeout=timeout, env=env)
        code, stdout, stderr = proc.returncode, proc.stdout, proc.stderr
        timed_out = False
    except subprocess.TimeoutExpired as e:
        code = None
        stdout = (e.stdout or b"").decode("utf-8", "replace") if isinstance(e.stdout, bytes) else (e.stdout or "")
        stderr = (e.stderr or b"").decode("utf-8", "replace") if isinstance(e.stderr, bytes) else (e.stderr or "")
        timed_out = True
    seconds = time.time() - started
    (out_dir / "stdout.txt").write_text(stdout, encoding="utf-8")
    (out_dir / "stderr.txt").write_text(stderr, encoding="utf-8")

    report = out_dir / "report.json"
    result = None
    report_code = None
    frames = None
    if report.exists():
        try:
            data = json.loads(report.read_text(encoding="utf-8"))
            result, report_code, frames = data.get("result"), data.get("exitCode"), data.get("frames")
        except (ValueError, OSError):
            result = "unreadable"

    if timed_out:
        kind = "timeout"
    elif code == 0 and result == "passed":
        kind = "ok"
    elif result is not None and report_code not in (None, 0) and code == report_code:
        kind = "failed"
    elif result == "passed":
        kind = "shutdown"
    else:
        kind = "crash"

    dumps_found = sorted(p.name for p in out_dir.glob("crash-*.dmp"))
    # The crash's precondition: C# wrappers still holding Godot objects when the engine shuts down.
    leaked_refs = stderr.count("Leaked unsafe reference")
    shutdown_log = out_dir / "shutdown.log"
    return {
        "scenario": scenario.stem,
        "dir": str(out_dir),
        "kind": kind,
        "exitCode": code,
        "exit": "timeout" if timed_out else describe_code(code),
        "report": result,
        "frames": frames,
        "seconds": round(seconds, 1),
        "signature": signature(stderr) if kind in ("shutdown", "crash") else "",
        "shutdownLog": shutdown_log.read_text(encoding="utf-8").splitlines()[-1] if shutdown_log.exists() else None,
        "dumps": dumps_found,
        "leakedRefs": leaked_refs,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("scenarios", nargs="*", help="scenario names (file names without .json)")
    parser.add_argument("--all", action="store_true", help="every scenario")
    parser.add_argument("--runs", type=int, default=20, help="runs of each scenario (default 20)")
    parser.add_argument("--jobs", type=int, default=4, help="runs at once (default 4)")
    parser.add_argument("--timeout", type=float, default=180, help="seconds before a run is killed (default 180)")
    parser.add_argument("--dumps", action="store_true", help="have the .NET runtime write a dump when a run dies")
    parser.add_argument("--dump-type", type=int, default=2, choices=[1, 2, 3, 4],
                        help="1 mini, 2 with heap (default; enough for managed stacks), 3 triage, 4 full")
    parser.add_argument("--exit-garbage", type=int, default=0, metavar="N",
                        help="have the runner drop N C# input events and start a collection just before it quits,"
                             " so finalizers race the engine's shutdown (makes the shutdown crash likely)")
    parser.add_argument("--keep-ok", action="store_true", help="keep the folders of runs that were fine (default: delete them)")
    args = parser.parse_args()
    if not args.scenarios and not args.all:
        parser.error("name some scenarios or pass --all")

    godot = godot_path()
    files = scenario_files(args.scenarios, args.all)
    root = REPO / "test-output" / "stress" / datetime.now().strftime("%y%m%d-%H%M%S")
    root.mkdir(parents=True)
    print(f"{len(files)} scenario(s) x {args.runs} runs, {args.jobs} at once -> {root}")

    jobs = [(f, i) for i in range(args.runs) for f in files]
    results = []
    with ThreadPoolExecutor(max_workers=args.jobs) as pool:
        futures = {pool.submit(run_once, godot, f, root / f"{f.stem}-{i:03d}", args.timeout, args.dumps, args.dump_type, args.exit_garbage): (f, i)
                   for f, i in jobs}
        for done, future in enumerate(as_completed(futures), 1):
            r = future.result()
            results.append(r)
            mark = "." if r["kind"] == "ok" else r["kind"][0].upper()
            print(mark, end="" if done % 50 else "\n", flush=True)
            if r["kind"] != "ok":
                print(f"\n  {r['scenario']} run {Path(r['dir']).name}: {r['kind']}, {r['exit']}"
                      + (f", dumps {r['dumps']}" if r["dumps"] else "")
                      + (f"\n    {r['signature']}" if r["signature"] else ""), flush=True)
            elif not args.keep_ok:
                for p in sorted(Path(r["dir"]).rglob("*"), reverse=True):
                    p.unlink() if p.is_file() else p.rmdir()
                Path(r["dir"]).rmdir()
    print()

    by_scenario = defaultdict(Counter)
    signatures = Counter()
    for r in results:
        by_scenario[r["scenario"]][r["kind"]] += 1
        if r["kind"] in ("shutdown", "crash"):
            signatures[f"{r['kind']}: {r['exit']}: {r['signature'] or '(no stderr signature)'}"] += 1
    kinds = ["ok", "failed", "shutdown", "crash", "timeout"]
    print(f"{'scenario':28} " + " ".join(f"{k:>8}" for k in kinds))
    for name in sorted(by_scenario):
        print(f"{name:28} " + " ".join(f"{by_scenario[name][k]:>8}" for k in kinds))
    total = Counter(r["kind"] for r in results)
    print(f"{'total':28} " + " ".join(f"{total[k]:>8}" for k in kinds))
    leaky = [r for r in results if r["leakedRefs"] > 0]
    print()
    print(f"runs with leaked C# references at exit: {len(leaky)} of {len(results)}"
          f" ({sum(r['leakedRefs'] for r in leaky)} references)")
    if signatures:
        print("\ncrash signatures:")
        for sig, count in signatures.most_common():
            print(f"  {count:4} x {sig}")

    (root / "summary.json").write_text(json.dumps({
        "runs": len(results),
        "totals": dict(total),
        "runsWithLeakedRefs": len(leaky),
        "byScenario": {k: dict(v) for k, v in by_scenario.items()},
        "signatures": dict(signatures),
        "problems": [r for r in results if r["kind"] != "ok"],
    }, indent=2), encoding="utf-8")
    print(f"\nsummary: {root / 'summary.json'}")
    return 1 if total["ok"] != len(results) else 0


if __name__ == "__main__":
    sys.exit(main())
