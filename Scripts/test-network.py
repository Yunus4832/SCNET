#!/usr/bin/env python3
"""Run the unprivileged network gate locally or in a POSIX CI worker."""

import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time


def interrupted(*_):
    raise KeyboardInterrupt()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--no-restore", action="store_true")
    parser.add_argument("--base-port", type=int, default=39191)
    args = parser.parse_args()
    if os.name != "posix":
        parser.error("The multi-process gate currently requires POSIX.")
    if not 1024 <= args.base_port <= 65530:
        parser.error("Base port must leave six consecutive unprivileged ports.")
    root = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    signal.signal(signal.SIGTERM, interrupted)
    result = {"passed": False, "steps": [], "privilegedExperiments": False}
    started = time.monotonic()

    def run(name, command):
        before = time.monotonic()
        with (output / (name + ".log")).open("w") as log:
            process = subprocess.Popen(
                command,
                cwd=root,
                stdout=log,
                stderr=subprocess.STDOUT,
                start_new_session=True,
            )
            try:
                code = process.wait(timeout=600)
            finally:
                if process.poll() is None:
                    os.killpg(process.pid, signal.SIGTERM)
                    try:
                        process.wait(timeout=15)
                    except subprocess.TimeoutExpired:
                        os.killpg(process.pid, signal.SIGKILL)
                        process.wait()
        result["steps"].append(
            {
                "name": name,
                "exitCode": code,
                "elapsedSeconds": time.monotonic() - before,
                "command": command,
            }
        )
        if code:
            raise RuntimeError(f"{name} failed with exit code {code}; see {name}.log")
        print(name + " passed", flush=True)

    try:
        for project in [
            "Rendezvous.Protocol.Test",
            "Engine.Network.Test",
            "RendezvousServer.Test",
        ]:
            command = [
                "dotnet",
                "test",
                project + "/" + project + ".csproj",
                "--logger",
                "trx;LogFileName=" + project + ".trx",
                "--results-directory",
                str(output / "tests"),
            ]
            if args.no_restore:
                command.append("--no-restore")
            run(project, command)
        command = ["dotnet", "build", "NetworkProbe/NetworkProbe.csproj"]
        if args.no_restore:
            command.append("--no-restore")
        run("probe-build", command)
        for name, mode, cancel in [
            ("candidate", "scenario", False),
            ("relay", "scenario-relay", False),
            ("session-locator", "scenario-session", False),
            ("cancel-prepared-path", "scenario-lease", True),
            ("cancel-candidate-path", "scenario-candidate-lease", True),
        ]:
            command = [
                sys.executable,
                str(root / "NetworkProbe/scripts/local-lab.py"),
                "--output",
                str(output / name),
                "--mode",
                mode,
                "--http-port",
                str(args.base_port),
                "--udp-port",
                str(args.base_port + 1),
                "--relay-port",
                str(args.base_port + 2),
            ]
            if cancel:
                command.append("--cancel-after-path")
            run(name, command)
        result["passed"] = True
    except (Exception, KeyboardInterrupt) as error:
        result["error"] = {"type": type(error).__name__, "message": str(error)}
        print(str(error), file=sys.stderr)
    finally:
        result["elapsedSeconds"] = time.monotonic() - started
        (output / "summary.json").write_text(json.dumps(result, indent=2))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
