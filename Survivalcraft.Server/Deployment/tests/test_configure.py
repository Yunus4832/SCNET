# /// script
# requires-python = ">=3.10"
# dependencies = ["pytest>=8,<10"]
# ///
# Run: uv run --with pytest python -m pytest Survivalcraft.Server/Deployment/tests
"""Exercise deployment through the real configuration CLI and isolated files."""

import json
import os
import subprocess
import sys
import threading
import xml.etree.ElementTree as ET
from collections.abc import Iterator
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
from typing import Final

import pytest

SCRIPT: Final = Path(__file__).resolve().parents[1] / "configure.py"
SESSION_ID: Final = "1" * 32
PACKAGE_HASH: Final = "a" * 64


@pytest.fixture
def content_server() -> Iterator[str]:
    class CatalogHandler(BaseHTTPRequestHandler):
        def do_GET(self) -> None:
            if not self.path.startswith("/api/v1/mods?"):
                self.send_error(404)
                return
            body = json.dumps({
                "success": True,
                "data": {"items": [{
                    "modId": "template.mod",
                    "version": "2.0.0",
                    "packageHash": PACKAGE_HASH,
                }]},
            }).encode()
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

    with HTTPServer(("127.0.0.1", 0), CatalogHandler) as server:
        worker = threading.Thread(target=server.serve_forever)
        worker.start()
        try:
            yield f"http://127.0.0.1:{server.server_port}"
        finally:
            server.shutdown()
            worker.join()


def run_configure(instance: Path, content_server: str, mods: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(SCRIPT)],
        env={
            **os.environ,
            "SCNET_INSTANCE_PATH": str(instance),
            "SCNET_SESSION": "survival",
            "SCNET_WORLD": "World",
            "CONTENT_SERVER": content_server,
            "MODS": mods,
        },
        capture_output=True,
        text=True,
        check=False,
        timeout=30,
    )


def write_session(instance: Path) -> Path:
    path = instance / "Config" / "SessionInfo.xml"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        f'<Sessions><SessionInfo SessionId="{SESSION_ID}" Name="survival" '
        'Target="World" World="World" Seed="saved-seed" /></Sessions>',
        encoding="utf-8",
    )
    return path


@pytest.mark.parametrize(
    ("empty_world", "directory_name"),
    [(False, "World"), (True, "World"), (False, "OpaqueDirectory")],
)
def test_redeploy_preserves_world_and_prepared_session_bytes(
    tmp_path: Path, content_server: str, empty_world: bool, directory_name: str,
) -> None:
    worlds = tmp_path / "Data" / "Worlds"
    world = worlds / directory_name
    world.mkdir(parents=True)
    (world / "Project.xml").write_text(
        '<Project><Subsystems><Values Name="GameInfo">'
        '<Value Name="WorldName" Type="string" Value="World" />'
        '</Values></Subsystems></Project>',
        encoding="utf-8",
    )
    package = "" if empty_world else (
        f'<Package ModId="world.mod" Version="1.0.0" PackageHash="{"b" * 64}" />'
    )
    (world / "WorldModProfile.xml").write_text(
        f'<ModProfile Id="{directory_name}"><Packages>{package}</Packages></ModProfile>',
        encoding="utf-8",
    )
    (world / "Chunks.dat").write_bytes(b"saved-world-content")
    sessions = write_session(tmp_path)
    profiles = tmp_path / "Config" / "SessionProfiles"
    profiles.mkdir()
    (profiles / f"{SESSION_ID}.xml").write_text(
        '<ModProfile Id="prepared"><Packages>'
        f'<Package ModId="prepared.mod" Version="3.0.0" PackageHash="{"c" * 64}" />'
        '</Packages></ModProfile>',
        encoding="utf-8",
    )
    original_worlds = {path.relative_to(worlds): path.read_bytes() for path in worlds.rglob("*") if path.is_file()}
    original_profiles = {path.name: path.read_bytes() for path in profiles.iterdir()}
    original_sessions = sessions.read_bytes()

    result = run_configure(tmp_path, content_server, "template.mod 2.0.0")

    assert result.returncode == 0, result.stderr
    assert {path.relative_to(worlds): path.read_bytes() for path in worlds.rglob("*") if path.is_file()} == original_worlds
    assert {path.name: path.read_bytes() for path in profiles.iterdir()} == original_profiles
    assert sessions.read_bytes() == original_sessions
    template = ET.parse(tmp_path / "Config" / "ModProfile.xml").getroot()
    assert [item.attrib for item in template.findall("./Packages/Package")] == [{
        "ModId": "template.mod", "Version": "2.0.0", "PackageHash": PACKAGE_HASH,
    }]


def test_creation_template_resolves_exact_hash_without_creating_world_or_session_profile(
    tmp_path: Path, content_server: str,
) -> None:
    sessions = write_session(tmp_path)
    original_sessions = sessions.read_bytes()

    result = run_configure(tmp_path, content_server, "template.mod 2.0.0")

    assert result.returncode == 0, result.stderr
    profile = ET.parse(tmp_path / "Config" / "ModProfile.xml").getroot()
    assert profile.get("Id") == "default"
    assert [item.attrib for item in profile.findall("./Packages/Package")] == [{
        "ModId": "template.mod", "Version": "2.0.0", "PackageHash": PACKAGE_HASH,
    }]
    repository = ET.parse(tmp_path / "Config" / "ContentRepositories.xml").find("Repository")
    assert repository is not None
    assert repository.get("BaseUrl") == content_server
    assert repository.get("IsEnabled") == "true"
    assert sessions.read_bytes() == original_sessions
    assert not (tmp_path / "Data" / "Worlds").exists()
    assert not (tmp_path / "Config" / "SessionProfiles").exists()


def test_empty_template_keeps_existing_repository_available_for_saved_worlds(tmp_path: Path) -> None:
    config = tmp_path / "Config"
    config.mkdir()
    (config / "ModProfile.xml").write_text(
        '<ModProfile Id="default"><Packages>'
        f'<Package ModId="old.mod" Version="1.0.0" PackageHash="{PACKAGE_HASH}" />'
        '</Packages></ModProfile>',
        encoding="utf-8",
    )
    repositories = config / "ContentRepositories.xml"
    repositories.write_text(
        '<ContentRepositories><Repository Id="saved" BaseUrl="https://packages.example" '
        'IsEnabled="true" /></ContentRepositories>',
        encoding="utf-8",
    )
    original_repository = repositories.read_bytes()

    result = run_configure(tmp_path, "", "")

    assert result.returncode == 0, result.stderr
    packages = ET.parse(config / "ModProfile.xml").find("Packages")
    assert packages is not None
    assert list(packages) == []
    assert repositories.read_bytes() == original_repository


def test_unresolved_template_leaves_persistent_configuration_unchanged(
    tmp_path: Path, content_server: str,
) -> None:
    config = tmp_path / "Config"
    config.mkdir()
    (config / "ModProfile.xml").write_text(
        '<ModProfile Id="default"><Packages /></ModProfile>', encoding="utf-8",
    )
    original = {path.relative_to(tmp_path): path.read_bytes() for path in tmp_path.rglob("*") if path.is_file()}

    result = run_configure(tmp_path, content_server, "missing.mod 1.0.0")

    assert result.returncode == 1
    assert "Published mods not found: missing.mod@1.0.0" in result.stderr
    assert {path.relative_to(tmp_path): path.read_bytes() for path in tmp_path.rglob("*") if path.is_file()} == original
