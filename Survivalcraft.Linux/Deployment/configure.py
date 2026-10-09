#!/usr/bin/env python3
"""Resolve configured mod versions and update the persistent game instance."""

import json
import os
import pathlib
import re
import sys
import tempfile
import urllib.parse
import urllib.request
import uuid
import xml.etree.ElementTree as ET


def fail(message):
    raise ValueError(message)


def parse_mods(value):
    result = []
    seen = set()
    for line_number, line in enumerate(value.splitlines(), 1):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        fields = line.split()
        if len(fields) != 2:
            fail(f"MODS line {line_number} must contain a mod ID and version")
        mod_id, version = fields
        if not re.fullmatch(r"[a-z0-9][a-z0-9_-]*(?:\.[a-z0-9][a-z0-9_-]*)*", mod_id):
            fail(f"Invalid mod ID: {mod_id}")
        if mod_id in seen:
            fail(f"Duplicate mod ID: {mod_id}")
        seen.add(mod_id)
        result.append((mod_id, version))
    if len(result) > 256:
        fail("Too many mods")
    return result


def resolve_mods(base_url, requested):
    if not requested:
        return []
    if not base_url.startswith(("http://", "https://")):
        fail("CONTENT_SERVER must be an HTTP(S) URL when MODS is set")
    found = {}
    for page in range(1, 101):
        url = f"{base_url.rstrip('/')}/api/v1/mods?pageIndex={page}&pageSize=100"
        with urllib.request.urlopen(url, timeout=20) as response:
            payload = json.load(response)
        if payload.get("success") is not True:
            fail(f"ContentServer rejected catalog request: {url}")
        data = payload.get("data") or {}
        items = data.get("items")
        if not isinstance(items, list):
            fail("ContentServer response has no mod list")
        for item in items:
            key = (item.get("modId"), item.get("version"))
            if key in requested:
                value = item.get("packageHash", "")
                if not re.fullmatch(r"[0-9a-f]{64}", value):
                    fail(f"Invalid PackageHash for {key[0]}@{key[1]}")
                if key in found and found[key] != value:
                    fail(f"Conflicting package hashes for {key[0]}@{key[1]}")
                found[key] = value
        if len(found) == len(requested) or len(items) < 100:
            break
    missing = [f"{mod_id}@{version}" for mod_id, version in requested if (mod_id, version) not in found]
    if missing:
        fail("Published mods not found: " + ", ".join(missing))
    return [(mod_id, version, found[(mod_id, version)]) for mod_id, version in requested]


def write_xml(path, root):
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(mode="wb", dir=path.parent, prefix=".scnet-", delete=False) as output:
        temporary = pathlib.Path(output.name)
        ET.ElementTree(root).write(output, encoding="utf-8", xml_declaration=True)
    os.replace(temporary, path)


def profile_xml(profile_id, packages):
    profile = ET.Element("ModProfile", {"Id": profile_id})
    package_nodes = ET.SubElement(profile, "Packages")
    for mod_id, version, package_hash in packages:
        ET.SubElement(package_nodes, "Package", {
            "ModId": mod_id,
            "Version": version,
            "PackageHash": package_hash,
        })
    return profile


def main():
    instance_path = pathlib.Path(os.environ["SCNET_INSTANCE_PATH"])
    session_name = os.environ["SCNET_SESSION"]
    world_name = os.environ["SCNET_WORLD"]
    base_url = os.environ.get("CONTENT_SERVER", "").strip()
    if base_url:
        parsed_url = urllib.parse.urlsplit(base_url)
        if parsed_url.scheme not in ("http", "https") or not parsed_url.netloc or \
                parsed_url.username or parsed_url.password or parsed_url.query or parsed_url.fragment:
            fail("CONTENT_SERVER must be an HTTP(S) URL without credentials, query or fragment")
    requested = parse_mods(os.environ.get("MODS", ""))
    packages = resolve_mods(base_url, requested)

    config_path = instance_path / "Config"
    settings_path = config_path / "Settings.xml"
    settings = ET.parse(settings_path).getroot() if settings_path.exists() else ET.Element("Settings")
    if settings.tag != "Settings":
        fail(f"Unexpected Settings.xml root: {settings.tag}")
    repositories = settings.find("ContentRepositories")
    if base_url:
        if repositories is None:
            repositories = ET.SubElement(settings, "ContentRepositories")
        existing = repositories.find("Repository")
        repository_id = existing.get("Id") if existing is not None else str(uuid.uuid4())
        repositories.clear()
        ET.SubElement(repositories, "Repository", {
            "Id": repository_id,
            "Name": "ContentServer",
            "BaseUrl": base_url.rstrip("/"),
            "IsEnabled": "true",
            "Priority": "0",
        })

    world_path = instance_path / "Data" / "Worlds" / world_name
    sessions_path = config_path / "SessionInfo.xml"
    session_id = None
    if sessions_path.exists():
        sessions = ET.parse(sessions_path).getroot()
        for session in sessions.findall("SessionInfo"):
            if session.get("Name") == session_name:
                session_id = session.get("SessionId", "")
                if not re.fullmatch(r"[0-9a-f]{32}", session_id):
                    fail(f"Invalid session ID for {session_name}")
                break

    if base_url or settings_path.exists():
        write_xml(settings_path, settings)
    write_xml(config_path / "ModProfile.xml", profile_xml("default", packages))
    if world_path.is_dir():
        write_xml(world_path / "WorldModProfile.xml", profile_xml(world_name, packages))
    if session_id:
        write_xml(config_path / "SessionProfiles" / f"{session_id}.xml",
                  profile_xml(session_id, packages))
    print(f"Configured {len(packages)} mods in {instance_path}")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, ET.ParseError, urllib.error.URLError) as error:
        print(f"[SCNET] Configuration failed: {error}", file=sys.stderr)
        sys.exit(1)
