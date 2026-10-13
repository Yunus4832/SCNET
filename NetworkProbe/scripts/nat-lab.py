#!/usr/bin/env python3
"""Calibrated IPv4 NAT scenarios; run through nat-lab.sh in an isolated namespace."""

import argparse
import hashlib
import shutil
import subprocess, os, time, json, threading, socket, uuid, signal, tempfile
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--output", required=True)
parser.add_argument(
    "--case",
    choices=[
        "punch",
        "address-filter",
        "public-endpoints",
        "peer-blocked",
        "udp-blocked",
        "beacon-blocked",
        "relay-blocked",
        "endpoint-dependent",
        "double-nat",
        "hairpin",
        "hairpin-blocked",
        "hairpin-local",
    ],
    default="punch",
)
parser.add_argument(
    "--relay-policy", choices=["Disabled", "Allowed", "Required"], default="Allowed"
)
parser.add_argument("--link-mtu", type=int, default=1500)
parser.add_argument("--burst-loss", action="store_true")
parser.add_argument("--seed", type=int, default=20261012)
parser.add_argument("--recovery", action="store_true", help="Verify 80 ordered 1024-byte echoes and receipts")
parser.add_argument("--probe-dir", type=Path)
parser.add_argument("--server-dir", type=Path)
parser.add_argument(
    "--runtime-image",
    help="Optional cached Podman .NET/ASP.NET runtime image; never pulled",
)
args = parser.parse_args()
if not 0 <= args.seed <= 4294967295:
    parser.error("--seed must be an unsigned 32-bit integer")
if args.burst_loss and (not args.recovery or args.case != "punch"):
    parser.error("--burst-loss requires --recovery --case punch")
if not 576 <= args.link_mtu <= 9000:
    parser.error("--link-mtu must be between 576 and 9000 for the IPv4 lab")
original = os.environ.get("SCNET_LAB_HOST_NETNS")
if not original or os.readlink("/proc/self/ns/net") == original:
    raise SystemExit("Refusing to modify host networking; use nat-lab.sh")
root = Path(__file__).resolve().parents[2]
binaries = {
    "probe": (args.probe_dir or root / "NetworkProbe/bin/Debug/net10.0").resolve(),
    "signal": (
        args.server_dir or root / "RendezvousServer/bin/Debug/net10.0"
    ).resolve(),
}
for role, dll in [("probe", "NetworkProbe.dll"), ("signal", "RendezvousServer.dll")]:
    if not (binaries[role] / dll).is_file():
        raise SystemExit("Build or supply " + dll)
for tool in [
    "ip",
    "nft",
    "nsenter",
    "unshare",
    "sysctl",
    "openssl",
    "tcpdump",
    *(["tc"] if args.burst_loss else []),
    "podman" if args.runtime_image else "dotnet",
]:
    if not shutil.which(tool):
        raise SystemExit("Missing required tool: " + tool)
out = Path(args.output).resolve()
out.mkdir(parents=True, exist_ok=False)
tls_holder = tempfile.TemporaryDirectory(prefix="scnet-nat-tls-")
tls = Path(tls_holder.name)
subprocess.run(
    [
        "openssl",
        "req",
        "-x509",
        "-newkey",
        "rsa:2048",
        "-nodes",
        "-days",
        "1",
        "-subj",
        "/CN=scnet-nat-lab",
        "-addext",
        "subjectAltName=IP:10.200.0.1",
        "-addext",
        "basicConstraints=critical,CA:TRUE",
        "-keyout",
        str(tls / "key.pem"),
        "-out",
        str(tls / "cert.pem"),
    ],
    check=True,
    capture_output=True,
)
(tls / "key.pem").chmod(0o600)
shutil.copyfile(tls / "cert.pem", out / "lab-public-certificate.pem")
children = []
namespace_pids = []
containers = []
logs = []
summary = {"passed": False}
name = "scnet-nat-" + uuid.uuid4().hex[:6]


def run(args, **kw):
    try:
        return subprocess.run(args, check=True, **kw)
    except subprocess.CalledProcessError as e:
        print(e.stderr or "", flush=True)
        raise


def ip(*args):
    run(["ip", *args], stdout=subprocess.DEVNULL)


def ns():
    p = subprocess.Popen(["unshare", "--net", "sleep", "300"])
    children.append(p)
    for _ in range(100):
        if p.poll() is not None:
            raise RuntimeError("Namespace holder exited")
        if os.readlink(f"/proc/{p.pid}/ns/net") != os.readlink("/proc/self/ns/net"):
            namespace_pids.append(p.pid)
            return p.pid
        time.sleep(0.01)
    raise TimeoutError("Namespace creation")


def at(pid, *args):
    return ["nsenter", "--net=/proc/" + str(pid) + "/ns/net", *args]


def nip(pid, *args):
    run(at(pid, "ip", *args), stdout=subprocess.DEVNULL)


def nft(pid, text):
    run(at(pid, "nft", "-f", "-"), input=text, text=True)


def application(role, pid, app, argv, env=None):
    if app == "probe":
        argv = [*argv, "--output", str(out / role)]
    local_env = os.environ.copy()
    local_env["SSL_CERT_FILE"] = str(tls / "cert.pem")
    local_env["NO_PROXY"] = "10.200.0.1"
    local_env.update(env or {})
    if args.runtime_image:
        cname = name + "-" + role
        containers.append(cname)
        cmd = [
            "podman",
            "run",
            "--rm",
            "--name",
            cname,
            "--pull=never",
            "--network",
            "ns:/proc/" + str(pid) + "/ns/net",
            "-v",
            str(binaries[app]) + ":/app:ro",
            "-v",
            str(tls) + ":/tls:ro",
            "-v",
            str(out) + ":" + str(out) + ":rw",
            "-w",
            "/app",
            "-e",
            "SSL_CERT_FILE=/tls/cert.pem",
        ]
        for key, value in (env or {}).items():
            cmd += ["-e", key + "=" + value.replace(str(tls), "/tls")]
        cmd += [args.runtime_image, "dotnet", *argv]
    else:
        cmd = at(pid, "dotnet", str(binaries[app] / argv[0]), *argv[1:])
    f = (out / (role + ".log")).open("w")
    logs.append(f)
    process = subprocess.Popen(cmd, env=local_env, stdout=f, stderr=subprocess.STDOUT)
    children.append(process)
    return process


signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
ra = rb = ca = cb = None
try:
    ip("link", "set", "lo", "up")
    ip("link", "add", "wan", "type", "bridge")
    ip("addr", "add", "10.200.0.1/24", "dev", "wan")
    ip("addr", "add", "10.200.0.10/24", "dev", "wan")
    ip("link", "set", "wan", "up")
    ra, rb, ca, cb = [ns() for _ in range(4)]
    for label, pid, public, client, subnet in [
        ("a", ra, "10.200.0.2", ca, "10.201.0"),
        ("b", rb, "10.200.0.3", cb, "10.202.0"),
    ]:
        ip("link", "add", "w" + label, "type", "veth", "peer", "name", "r" + label)
        ip("link", "set", "r" + label, "netns", str(pid))
        ip("link", "set", "w" + label, "master", "wan")
        ip("link", "set", "w" + label, "up")
        nip(pid, "link", "set", "lo", "up")
        nip(pid, "addr", "add", public + "/24", "dev", "r" + label)
        nip(pid, "link", "set", "r" + label, "up")
        run(
            at(
                pid,
                "ip",
                "link",
                "add",
                "lan",
                "type",
                "veth",
                "peer",
                "name",
                "client",
            )
        )
        nip(pid, "link", "set", "client", "netns", str(client))
        nip(pid, "addr", "add", subnet + ".1/24", "dev", "lan")
        nip(pid, "link", "set", "lan", "up")
        nip(client, "link", "set", "lo", "up")
        nip(client, "addr", "add", subnet + ".2/24", "dev", "client")
        nip(client, "link", "set", "client", "up")
        nip(client, "route", "add", "default", "via", subnet + ".1")
        run(at(pid, "sysctl", "-w", "net.ipv4.ip_forward=1"), stdout=subprocess.DEVNULL)
        mapping = (
            f'oifname "r{label}" meta l4proto udp ip daddr 10.200.0.1 udp dport {{ 9521, 40001 }} snat to {public}:50000; '
            f'oifname "r{label}" meta l4proto udp snat to {public}:50001; '
            if args.case == "endpoint-dependent"
            else f'oifname "r{label}" meta l4proto udp snat to {public}:50000; '
        )
        rules = (
            'table ip scnet { chain input { type filter hook input priority 0; policy accept; ct state established,related accept; meta l4proto udp drop; } chain forward { type filter hook forward priority 0; policy drop; ct state established,related accept; iifname "lan" accept; } chain post { type nat hook postrouting priority srcnat; oifname "r'
            + label
            + '" masquerade; } }'
        )
        rules = rules.replace(
            f'oifname "r{label}" masquerade;',
            mapping + f'oifname "r{label}" masquerade;',
        )
        rules = rules.replace("} chain", "}\n chain").replace(" } }", " }\n}\n")
        if args.case == "address-filter":
            rules = f"""table ip scnet {{
 set allowed {{ type ipv4_addr; flags dynamic,timeout; timeout 30s; }}
 chain input {{ type filter hook input priority 0; policy accept; ct state established,related accept; meta l4proto udp drop; }}
 chain remember {{ type filter hook forward priority -10; iifname "lan" meta l4proto udp update @allowed {{ ip daddr timeout 30s }}; }}
 chain pre {{ type nat hook prerouting priority dstnat; iifname "r{label}" ip saddr @allowed udp dport 50000 dnat to {subnet}.2:41000; }}
 chain forward {{ type filter hook forward priority 0; policy drop; ct state established,related accept; iifname "lan" accept; iifname "r{label}" ip saddr @allowed accept; }}
 chain post {{ type nat hook postrouting priority srcnat; oifname "r{label}" meta l4proto udp snat to {public}:50000; oifname "r{label}" masquerade; }}
}}
"""
        nft(pid, rules)
        (out / ("router-" + label + ".nft")).write_text(rules)
    if args.case == "public-endpoints":
        for label, router, client, public in [
            ("a", ra, ca, "10.200.0.2"),
            ("b", rb, cb, "10.200.0.3"),
        ]:
            nip(client, "route", "del", "default")
            nip(router, "link", "set", "r" + label, "netns", str(client))
            nip(client, "addr", "replace", public + "/24", "dev", "r" + label)
            nip(client, "link", "set", "r" + label, "up")
            nip(client, "route", "add", "default", "via", "10.200.0.1")
            (out / ("public-client-" + label + "-addresses.json")).write_text(
                run(
                    at(client, "ip", "-j", "address"), capture_output=True, text=True
                ).stdout
            )
    if args.case.startswith("hairpin"):
        nip(rb, "link", "set", "lan", "name", "lanb")
        nip(rb, "link", "set", "lanb", "netns", str(ra))
        nip(ra, "addr", "replace", "10.202.0.1/24", "dev", "lanb")
        nip(ra, "link", "set", "lanb", "up")
        nft(ra, "delete table ip scnet")
        rules = """table ip scnet {
 chain input { type filter hook input priority 0; policy accept; ct state established,related accept; meta l4proto udp drop; }
 chain pre { type nat hook prerouting priority dstnat; ip daddr 10.200.0.2 udp dport 50000 dnat to 10.201.0.2:41000; ip daddr 10.200.0.2 udp dport 50001 dnat to 10.202.0.2:41000; }
 chain forward { type filter hook forward priority 0; policy drop; ct state established,related accept; iifname { "lan", "lanb" } ct original daddr 10.200.0.2 accept; iifname { "lan", "lanb" } oifname "ra" accept; }
 chain post { type nat hook postrouting priority srcnat; ip saddr 10.201.0.2 meta l4proto udp snat to 10.200.0.2:50000; ip saddr 10.202.0.2 meta l4proto udp snat to 10.200.0.2:50001; oifname "ra" masquerade; }
}
"""
        if args.case in ["hairpin-blocked", "hairpin-local"]:
            rules = rules.replace(
                "ct original daddr 10.200.0.2 accept",
                "ct original daddr 10.200.0.2 drop",
            )
        if args.case == "hairpin-local":
            rules = rules.replace(
                'iifname { "lan", "lanb" } oifname "ra" accept;',
                'iifname { "lan", "lanb" } oifname { "lan", "lanb" } accept; iifname { "lan", "lanb" } oifname "ra" accept;',
            )
        nft(ra, rules)
        (out / "hairpin-router.nft").write_text(rules)
        for interface in ["lan", "lanb"]:
            headers = (out / ("hairpin-" + interface + "-udp-headers.log")).open("w")
            logs.append(headers)
            process = subprocess.Popen(
                at(
                    ra,
                    "tcpdump",
                    "-nn",
                    "-l",
                    "-tttt",
                    "-Z",
                    "root",
                    "-i",
                    interface,
                    "udp",
                ),
                stdout=headers,
                stderr=subprocess.STDOUT,
            )
            children.append(process)
    if args.case == "double-nat":
        for label, client, transit, subnet in [
            ("a", ca, "10.201.0", "10.211.0"),
            ("b", cb, "10.202.0", "10.212.0"),
        ]:
            inner = ns()
            nip(client, "link", "set", "client", "netns", str(inner))
            nip(inner, "link", "set", "client", "name", "outer")
            nip(inner, "link", "set", "lo", "up")
            nip(inner, "addr", "replace", transit + ".2/24", "dev", "outer")
            nip(inner, "link", "set", "outer", "up")
            nip(inner, "route", "add", "default", "via", transit + ".1")
            nip(
                inner,
                "link",
                "add",
                "innerlan",
                "type",
                "veth",
                "peer",
                "name",
                "client",
            )
            nip(inner, "link", "set", "client", "netns", str(client))
            nip(inner, "addr", "add", subnet + ".1/24", "dev", "innerlan")
            nip(inner, "link", "set", "innerlan", "up")
            nip(client, "addr", "add", subnet + ".2/24", "dev", "client")
            nip(client, "link", "set", "client", "up")
            nip(client, "route", "add", "default", "via", subnet + ".1")
            run(
                at(inner, "sysctl", "-w", "net.ipv4.ip_forward=1"),
                stdout=subprocess.DEVNULL,
            )
            rules = (
                "table ip scnet_inner { chain input { type filter hook input priority 0; policy accept; ct state established,related accept; meta l4proto udp drop; } "
                'chain forward { type filter hook forward priority 0; policy drop; ct state established,related accept; iifname "innerlan" accept; } '
                f'chain post {{ type nat hook postrouting priority srcnat; oifname "outer" meta l4proto udp snat to {transit}.2:45000; oifname "outer" masquerade; }} }}'
            )
            rules = rules.replace("} chain", "}\n chain").replace(" } }", " }\n}\n")
            nft(inner, rules)
            (out / ("inner-router-" + label + ".nft")).write_text(rules)
            headers = (out / ("inner-" + label + "-udp-headers.log")).open("w")
            logs.append(headers)
            process = subprocess.Popen(
                at(
                    inner,
                    "tcpdump",
                    "-nn",
                    "-l",
                    "-tttt",
                    "-Z",
                    "root",
                    "-i",
                    "outer",
                    "udp",
                ),
                stdout=headers,
                stderr=subprocess.STDOUT,
            )
            children.append(process)
            summary["inner-router-" + label] = {
                "transitAddress": transit + ".2",
                "udpPort": 45000,
                "clientAddress": subnet + ".2",
            }
    for label, pid in [("wan", None), *[(str(pid), pid) for pid in namespace_pids]]:
        command = ["ip", "-j", "link"] if pid is None else at(pid, "ip", "-j", "link")
        interfaces = json.loads(run(command, capture_output=True, text=True).stdout)
        for interface in interfaces:
            if interface["ifname"] == "lo":
                continue
            arguments = ["link", "set", "dev", interface["ifname"], "mtu", str(args.link_mtu)]
            if pid is None:
                ip(*arguments)
            else:
                nip(pid, *arguments)
        (out / ("link-mtu-" + label + ".json")).write_text(
            run(command, capture_output=True, text=True).stdout
        )
    capture = (out / "udp-headers.log").open("w")
    logs.append(capture)
    cap = subprocess.Popen(
        ["tcpdump", "-nn", "-l", "-tttt", "-Z", "root", "-i", "wan", "udp or (ip[6:2] & 0x1fff != 0)"],
        stdout=capture,
        stderr=subprocess.STDOUT,
    )
    children.append(cap)
    # Independent UDP calibration, no Engine traffic or credentials.
    socks = [socket.socket(socket.AF_INET, socket.SOCK_DGRAM) for _ in range(3)]
    socks[0].bind(("10.200.0.1", 40001))
    socks[1].bind(("10.200.0.10", 40002))
    socks[2].bind(("10.200.0.1", 40003))

    def reflect(index):
        while True:
            try:
                data, ep = socks[index].recvfrom(1024)
            except OSError:
                return
            if data == b"other-port":
                socks[2].sendto(b"changed-port", ep)
            elif data == b"foreign":
                socks[1].sendto(b"unsolicited", ep)
            else:
                socks[index].sendto(json.dumps(ep).encode(), ep)

    for index in range(3):
        threading.Thread(target=reflect, args=(index,), daemon=True).start()
    calibration = """import socket,json
s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM);s.bind(('0.0.0.0',0));s.settimeout(1)
s.sendto(b'echo',('10.200.0.1',40001));a=json.loads(s.recv(1024))
s.sendto(b'other-port',('10.200.0.1',40001))
try:changed=s.recv(1024)==b'changed-port'
except socket.timeout:changed=False
s.sendto(b'foreign',('10.200.0.1',40001))
try:s.recv(1024);blocked=False
except socket.timeout:blocked=True
s.sendto(b'echo',('10.200.0.10',40002));b=json.loads(s.recv(1024))
if (blocked!=EXPECTED_BLOCKED) or (changed!=EXPECTED_CHANGED) or ((a==b)!=EXPECTED_SAME):raise RuntimeError(str((a,b,blocked,changed)))
print(json.dumps({'endpoint1':a,'endpoint2':b,'unsolicitedDifferentSourceBlocked':blocked,'sameAddressChangedPortAllowed':changed,'localPort':s.getsockname()[1]}))"""
    calibration = calibration.replace(
        "EXPECTED_SAME", str(args.case != "endpoint-dependent")
    )
    calibration = calibration.replace(
        "EXPECTED_BLOCKED", str(args.case != "public-endpoints")
    ).replace(
        "EXPECTED_CHANGED", str(args.case in ["address-filter", "public-endpoints"])
    )
    if args.case.startswith("hairpin") or args.case in [
        "address-filter",
        "public-endpoints",
    ]:
        calibration = calibration.replace(
            "s.bind(('0.0.0.0',0))", "s.bind(('0.0.0.0',41000))"
        )
    for label, pid in [("a", ca), ("b", cb)]:
        r = run(at(pid, "python3", "-c", calibration), capture_output=True, text=True)
        summary["calibration-" + label] = json.loads(r.stdout)
    if args.case.startswith("hairpin"):
        a = summary["calibration-a"]["endpoint1"]
        b = summary["calibration-b"]["endpoint1"]
        if a[0] != b[0] or a[1] == b[1]:
            raise RuntimeError(
                "Hairpin peers must share a public IP with distinct mappings"
            )
    service = application(
        "signal",
        os.getpid(),
        "signal",
        [
            "RendezvousServer.dll",
            "--urls",
            "https://0.0.0.0:9520",
            "--Rendezvous:UdpPort",
            "9521",
            "--Rendezvous:UdpAdvertisedHost",
            "10.200.0.1",
            "--Rendezvous:RelayAdvertisedAddress",
            "10.200.0.1",
            "--Rendezvous:RelayFirstPort",
            "9522",
            "--Rendezvous:RelayPortCount",
            "4",
        ],
        {
            "Kestrel__Certificates__Default__Path": str(tls / "cert.pem"),
            "Kestrel__Certificates__Default__KeyPath": str(tls / "key.pem"),
        },
    )
    end = time.monotonic() + 20
    while "Now listening on" not in (out / "signal.log").read_text():
        if service.poll() is not None:
            raise RuntimeError("Signal service exited before readiness")
        if time.monotonic() > end:
            raise TimeoutError("service")
        time.sleep(0.1)
    if args.burst_loss:
        for label, pid in [("a", ra), ("b", rb)]:
            run(at(pid, "tc", "qdisc", "add", "dev", "r" + label, "root", "netem",
                   "loss", "gemodel", "2%", "25%", "100%", "0%", "seed", str(args.seed)))
            (out / ("burst-qdisc-" + label + "-before.json")).write_text(
                run(at(pid, "tc", "-s", "-j", "qdisc", "show", "dev", "r" + label),
                    capture_output=True, text=True).stdout
            )
    bind_arguments = (
        ["--local-port", "41000"]
        if args.case.startswith("hairpin")
        or args.case in ["address-filter", "public-endpoints"]
        else []
    )
    for case in [args.case]:
        expected = {
            "punch": "HolePunch",
            "address-filter": "HolePunch",
            "public-endpoints": "HolePunch",
            "double-nat": "HolePunch",
            "hairpin": "HolePunch",
            "hairpin-blocked": "Relay",
            "hairpin-local": "HolePunch",
            "peer-blocked": "Relay",
            "udp-blocked": "Failure",
            "beacon-blocked": "Relay",
            "relay-blocked": "HolePunch",
            "endpoint-dependent": "Relay",
        }[case]
        if args.relay_policy == "Required" and case not in [
            "udp-blocked",
            "relay-blocked",
        ]:
            expected = "Relay"
        elif args.relay_policy == "Required" and case == "relay-blocked":
            expected = "Failure"
        elif args.relay_policy == "Disabled" and case in [
            "peer-blocked",
            "endpoint-dependent",
            "hairpin-blocked",
            "beacon-blocked",
        ]:
            expected = "Failure"
        failure_code = (
            "preparation_timeout (Timeout)"
            if case == "udp-blocked"
            else "HolePunchUnavailable"
        )
        if case == "beacon-blocked":
            failure_code = "UdpUnavailable"
            nft(
                ra,
                "insert rule ip scnet forward ip daddr 10.200.0.1 udp dport 9521 counter drop",
            )
        if case == "relay-blocked":
            failure_code = "RelayUnavailable"
            for router in [ra, rb]:
                nft(
                    router,
                    "insert rule ip scnet forward ip daddr 10.200.0.1 udp dport 9522-9525 counter drop",
                )
        if case == "peer-blocked":
            for pid, other in [(ra, "10.200.0.3"), (rb, "10.200.0.2")]:
                nft(
                    pid,
                    "insert rule ip scnet forward ip saddr "
                    + other
                    + " meta l4proto udp drop",
                )
        if case == "udp-blocked":
            nft(ra, "insert rule ip scnet forward meta l4proto udp drop")
        for label, pid in [("a", ra), ("b", rb)]:
            (out / (case + "-router-" + label + ".nft")).write_text(
                run(
                    at(pid, "nft", "list", "ruleset"), capture_output=True, text=True
                ).stdout
            )
        host = application(
            case + "-host",
            cb,
            "probe",
            [
                "NetworkProbe.dll",
                "host",
                "https://10.200.0.1:9520/",
                "auto",
                *bind_arguments,
            ],
        )
        end = time.monotonic() + 20
        while "READY joinCode=" not in (out / (case + "-host.log")).read_text():
            if host.poll() is not None:
                raise RuntimeError("Host exited before invitation readiness")
            if time.monotonic() > end:
                raise TimeoutError("host ready")
            time.sleep(0.1)
        code = (out / (case + "-host.log")).read_text().split("joinCode=")[1].split()[0]
        started = time.monotonic()
        client = application(
            case + "-client",
            ca,
            "probe",
            [
                "NetworkProbe.dll",
                "client-recovery" if args.recovery else "client",
                "https://10.200.0.1:9520/",
                "auto",
                code,
                "0",
                args.relay_policy,
                *bind_arguments,
            ],
        )
        client.wait(timeout=35)
        text = (out / (case + "-client.log")).read_text()
        summary[case] = {
            "clientExit": client.returncode,
            "seconds": round(time.monotonic() - started, 3),
            "expected": expected,
        }
        if expected == "Failure":
            if client.returncode != 1 or failure_code not in text:
                raise RuntimeError(text)

            if args.runtime_image:
                run(
                    ["podman", "stop", "-t", "8", name + "-" + case + "-host"],
                    stdout=subprocess.DEVNULL,
                )
            else:
                host.terminate()
            host.wait(timeout=10)
        else:
            if client.returncode != 0 or "path=" + expected not in text:
                raise RuntimeError(text)
            if args.recovery and "PASS recovery echoed=80 pendingReliableBytes=0" not in text:
                raise RuntimeError("Reliable recovery workload did not converge")
            host.wait(timeout=10)
            if host.returncode != 0:
                raise RuntimeError("Host did not confirm reliable completion")
        for role in ["host", "client"]:
            rows = (
                (out / (case + "-" + role) / "samples.jsonl").read_text().splitlines()
            )
            final = json.loads(rows[-1])["Details"]
            if not final["HostStopped"] or any(final["Resources"].values()):
                raise RuntimeError("Probe resources did not converge: " + role)
        summary[case]["resourcesClosed"] = True
        summary[case]["passed"] = True
    if args.burst_loss:
        for label, pid in [("a", ra), ("b", rb)]:
            statistics = run(at(pid, "tc", "-s", "-j", "qdisc", "show", "dev", "r" + label),
                             capture_output=True, text=True).stdout
            (out / ("burst-qdisc-" + label + "-after.json")).write_text(statistics)
            if not any(row.get("drops", 0) > 0 for row in json.loads(statistics)):
                raise RuntimeError("Burst loss was not observed on router " + label)
    summary["passed"] = True
except BaseException as error:
    summary["error"] = f"{type(error).__name__}: {error}"
    raise
finally:
    for cname in reversed(containers):
        subprocess.run(
            ["podman", "rm", "-f", cname],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
    for p in reversed(children):
        if p.poll() is None:
            p.send_signal(signal.SIGINT if p is locals().get("cap") else signal.SIGTERM)
        try:
            p.wait(timeout=5)
        except subprocess.TimeoutExpired:
            p.kill()
            p.wait()
    for f in logs:
        f.close()
    tls_holder.cleanup()
    (out / "summary.json").write_text(json.dumps(summary, indent=2))
    (out / "manifest.json").write_text(
        json.dumps(
            {
                "case": args.case,
                "linkMtu": args.link_mtu,
                "burstLoss": {"model": "gemodel", "p": "2%", "r": "25%", "badLoss": "100%", "goodLoss": "0%", "seed": args.seed} if args.burst_loss else None,
                "recoveryWorkload": args.recovery,
                "relayPolicy": args.relay_policy,
                "topology": "2 endpoints directly attached to simulated WAN, no NAT"
                if args.case == "public-endpoints"
                else "2IPv4LANs behind one shared SNAT router with controlled hairpin; simulated WAN"
                if args.case.startswith("hairpin")
                else "2IPv4LANs behind independent SNAT routers with conntrack source filtering; simulated WAN",
                "filterProfile": "source-address dynamic set"
                if args.case == "address-filter"
                else "none"
                if args.case == "public-endpoints"
                else "conntrack address+port",
                "sharedRouter": args.case.startswith("hairpin"),
                "natDepthPerClient": 0
                if args.case == "public-endpoints"
                else 2
                if args.case == "double-nat"
                else 1,
                "mapping": "none; bound UDP port 41000"
                if args.case == "public-endpoints"
                else "destination-dependent ports 50000/50001"
                if args.case == "endpoint-dependent"
                else "shared public IP with UDP ports 50000/50001 and static DNAT"
                if args.case.startswith("hairpin")
                else "fixed UDP port 50000",
                "runtimeImage": args.runtime_image,
                "sha256": {
                    str(path): hashlib.sha256(path.read_bytes()).hexdigest()
                    for folder in binaries.values()
                    for path in folder.glob("*.dll")
                },
            },
            indent=2,
        )
    )
