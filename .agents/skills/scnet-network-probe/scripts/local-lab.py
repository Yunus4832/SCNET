#!/usr/bin/env python3
"""Run an owned loopback signaling service and separate probe processes on POSIX."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import sys
import time
import uuid
import urllib.request


def stop(process):
    """Reap the owned process group, including children of the scenario driver."""
    try:
        os.killpg(process.pid, signal.SIGTERM)
    except ProcessLookupError:
        pass
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGKILL)
        process.wait()
    # The parent can exit before one of its children; kill any remaining group.
    try:
        os.killpg(process.pid, signal.SIGKILL)
    except ProcessLookupError:
        pass


def interrupt(signum, frame):
    raise KeyboardInterrupt()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument(
        "--mode",
        choices=["scenario", "scenario-relay", "scenario-session", "scenario-lease", "scenario-candidate-lease"],
        default="scenario",
    )
    parser.add_argument(
        "--cancel-after-path",
        action="store_true",
        help="Cancel a lease scenario after path preparation and verify child cleanup",
    )
    parser.add_argument("--family", choices=["ipv4", "ipv6", "dual"], default="ipv4")
    parser.add_argument("--isolated", action="store_true")
    parser.add_argument("--blocked-beacon-family", choices=["ipv4", "ipv6"])
    parser.add_argument("--beacon-block-hook", choices=["input", "output"], default="input")
    parser.add_argument("--http-port", type=int, default=31991)
    parser.add_argument("--udp-port", type=int, default=31990)
    parser.add_argument("--relay-port", type=int, default=32000)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument(
        "--runtime-image", help="Cached Podman runtime image; never pulled"
    )
    root = Path(__file__).resolve().parents[4]
    parser.add_argument(
        "--probe",
        type=Path,
        default=root / "NetworkProbe/bin/Debug/net10.0/NetworkProbe.dll",
    )
    parser.add_argument(
        "--server",
        type=Path,
        default=root / "RendezvousServer/bin/Debug/net10.0/RendezvousServer.dll",
    )
    options = parser.parse_args()
    if options.blocked_beacon_family and (not options.isolated or options.family != "dual"):
        parser.error("--blocked-beacon-family requires --isolated --family dual")
    if options.cancel_after_path and options.mode not in ["scenario-lease", "scenario-candidate-lease"]:
        parser.error("--cancel-after-path requires a lease scenario mode")
    if options.cancel_after_path and options.runtime_image:
        parser.error(
            "Parent-only cancellation verification requires native process IDs"
        )
    if os.name != "posix":
        parser.error("This lab requires POSIX process groups.")
    if (
        not all(
            1 <= port <= 65535
            for port in [options.http_port, options.udp_port, options.relay_port]
        )
        or options.relay_port > 65532
    ):
        parser.error(
            "Ports must be valid; the relay pool needs four consecutive ports."
        )
    if options.udp_port in range(options.relay_port, options.relay_port + 4):
        parser.error("UDP beacon and relay ports must not overlap.")
    for path in [options.probe, options.server]:
        if not path.is_file():
            parser.error(f"Build the project first; missing assembly: {path}")
    if options.isolated:
        original = os.environ.get("SCNET_LAB_HOST_NETNS")
        if not original or os.readlink("/proc/self/ns/net") == original:
            parser.error("Refusing to change host networking; use local-lab.sh")
        subprocess.run(["ip", "link", "set", "lo", "up"], check=True)
        if options.family != "dual" or options.blocked_beacon_family:
            blocked = options.blocked_beacon_family or ("ipv6" if options.family == "ipv4" else "ipv4")
            selector = f"udp dport {options.udp_port} " if options.blocked_beacon_family else ""
            hook = options.beacon_block_hook if options.blocked_beacon_family else "output"
            rules = f"table inet scnet_local {{\n chain {hook} {{ type filter hook {hook} priority 0; policy accept; meta nfproto {blocked} {selector}counter drop; }}\n}}\n"
            subprocess.run(["nft", "-f", "-"], input=rules, text=True, check=True)
            # Prove the excluded family actually hits the drop rule.
            family = socket.AF_INET6 if blocked == "ipv6" else socket.AF_INET
            address = "::1" if blocked == "ipv6" else "127.0.0.1"
            with socket.socket(family, socket.SOCK_DGRAM) as probe:
                try:
                    probe.sendto(b"family-filter-calibration", (address, options.udp_port if options.blocked_beacon_family else 39999))
                except PermissionError:
                    pass  # The nft output drop can be reported synchronously.
    output = options.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    signal.signal(signal.SIGTERM, interrupt)
    processes = []
    containers = []
    summary = {
        "passed": False,
        "topology": "loopback; no NAT traversal claim",
        "mode": options.mode,
        "family": options.family,
        "isolated": options.isolated,
        "blockedBeaconFamily": options.blocked_beacon_family,
        "beaconBlockHook": options.beacon_block_hook if options.blocked_beacon_family else None,
    }
    started = time.monotonic()
    control_host = {"ipv4": "127.0.0.1", "ipv6": "[::1]", "dual": "localhost"}[
        options.family
    ]
    transport_host = "127.0.0.1" if options.family == "ipv4" else "::1"
    url = f"http://{control_host}:{options.http_port}"
    manifest = {
        "mode": options.mode,
        "family": options.family,
        "isolated": options.isolated,
        "blockedBeaconFamily": options.blocked_beacon_family,
        "beaconBlockHook": options.beacon_block_hook if options.blocked_beacon_family else None,
        "runtimeImage": options.runtime_image,
        "controlUrl": url,
        "udpPort": options.udp_port,
        "relayPorts": list(range(options.relay_port, options.relay_port + 4)),
        "assemblies": {
            str(path.resolve()): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in [options.probe, options.server]
        },
    }
    (output / "lab-manifest.json").write_text(json.dumps(manifest, indent=2))
    if options.isolated:
        (output / "interfaces.json").write_text(
            subprocess.check_output(["ip", "-j", "address"], text=True)
        )
        if options.family != "dual" or options.blocked_beacon_family:
            rules = subprocess.check_output(
                ["nft", "-j", "list", "table", "inet", "scnet_local"], text=True
            )
            (output / "family-filter.json").write_text(rules)
            counters = [
                expression["counter"]["packets"]
                for entry in json.loads(rules)["nftables"]
                if "rule" in entry
                for expression in entry["rule"]["expr"]
                if "counter" in expression
            ]
            if not counters or sum(counters) < 1:
                raise RuntimeError(
                    "Excluded address-family drop rule was not calibrated"
                )

    def command(role, arguments):
        if not options.runtime_image:
            return arguments
        name = "scnet-family-" + uuid.uuid4().hex[:8] + "-" + role
        containers.append(name)
        mounts = []
        for path in {options.probe.resolve().parent, options.server.resolve().parent}:
            mounts += ["-v", f"{path}:{path}:ro"]
        return [
            "podman",
            "run",
            "--rm",
            "--pull=never",
            "--name",
            name,
            "--network",
            f"ns:/proc/{os.getpid()}/ns/net",
            *mounts,
            "-v",
            f"{output}:{output}:rw",
            "-w",
            str(options.server.resolve().parent),
            options.runtime_image,
            *arguments,
        ]

    try:
        with (
            (output / "server.log").open("w") as server_log,
            (output / "probe.log").open("w") as probe_log,
        ):
            server = subprocess.Popen(
                command(
                    "server",
                    [
                        options.dotnet,
                        str(options.server.resolve()),
                        "--urls",
                        url,
                        "--Rendezvous:UdpPort",
                        str(options.udp_port),
                        "--Rendezvous:UdpAdvertisedHost",
                        "localhost" if options.family == "dual" else transport_host,
                        "--Rendezvous:RelayAdvertisedAddress",
                        transport_host,
                        "--Rendezvous:RelayFirstPort",
                        str(options.relay_port),
                        "--Rendezvous:RelayPortCount",
                        "4",
                    ],
                ),
                stdout=server_log,
                stderr=subprocess.STDOUT,
                start_new_session=True,
            )
            processes.append(server)
            deadline = time.monotonic() + 15
            while True:
                if server.poll() is not None:
                    raise RuntimeError(
                        f"Owned signaling service exited: {server.returncode}"
                    )
                if time.monotonic() >= deadline:
                    raise TimeoutError("Owned signaling service readiness timed out")
                if "Now listening on: " + url in (output / "server.log").read_text(
                    errors="replace"
                ):
                    with urllib.request.build_opener(
                        urllib.request.ProxyHandler({})
                    ).open(url + "/health", timeout=2) as response:
                        if response.status == 200:
                            break
                time.sleep(0.1)
            probe = subprocess.Popen(
                command(
                    "probe",
                    [
                        options.dotnet,
                        str(options.probe.resolve()),
                        options.mode,
                        url,
                        "auto",
                        "--bind-ipv4",
                        "127.0.0.1",
                        "--bind-ipv6",
                        "::1",
                        "--output",
                        str(output / "probe"),
                    ],
                ),
                stdout=probe_log,
                stderr=subprocess.STDOUT,
                start_new_session=True,
            )
            processes.append(probe)
            if options.cancel_after_path:
                deadline = time.monotonic() + 25
                events = output / "probe/client/events.jsonl"
                while not (
                    events.exists() and '"Phase":"path-confirmed"' in events.read_text()
                ):
                    if probe.poll() is not None or time.monotonic() >= deadline:
                        raise RuntimeError(
                            "Probe did not prepare its path before cancellation"
                        )
                    time.sleep(0.1)
                confirmed = next(
                    json.loads(line)
                    for line in events.read_text().splitlines()
                    if json.loads(line)["Phase"] == "path-confirmed"
                )
                expected_path = "HolePunch" if options.mode == "scenario-candidate-lease" else "Relay"
                if confirmed["Details"]["Path"] != expected_path:
                    raise RuntimeError("Prepared cancellation path did not match its scenario")
                summary["cancelledPath"] = expected_path
                probe.terminate()
            probe.wait(timeout=135 if options.mode in ["scenario-lease", "scenario-candidate-lease"] else 60)
            summary["probeExit"] = probe.returncode
            expected_exit = 1 if options.cancel_after_path else 0
            if probe.returncode != expected_exit:
                raise RuntimeError(f"Probe scenario failed: {probe.returncode}")
            for role in ["", "host", "client"]:
                result = json.loads(
                    (output / "probe" / role / "summary.json").read_text()
                )
                if result["Passed"] == options.cancel_after_path:
                    raise RuntimeError(f"Probe role failed: {role or 'scenario'}")
            for role in ["host", "client"]:
                final = json.loads(
                    (output / "probe" / role / "samples.jsonl")
                    .read_text()
                    .splitlines()[-1]
                )["Details"]
                if not final["HostStopped"] or any(final["Resources"].values()):
                    raise RuntimeError("Probe role resources did not converge: " + role)
            summary["cancelledAfterPath"] = options.cancel_after_path
            summary["passed"] = True
    except (Exception, KeyboardInterrupt) as error:
        summary["error"] = {"type": type(error).__name__, "message": str(error)}
    finally:
        for name in reversed(containers):
            subprocess.run(
                ["podman", "rm", "-f", name],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            )
        for process in reversed(processes):
            stop(process)
        summary["processGroups"] = [process.pid for process in processes]
        summary["elapsedSeconds"] = time.monotonic() - started
        summary["ownedParentsReaped"] = all(
            process.poll() is not None for process in processes
        )
        (output / "lab-summary.json").write_text(json.dumps(summary, indent=2))
    print(json.dumps(summary))
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
