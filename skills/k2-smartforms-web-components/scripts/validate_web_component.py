#!/usr/bin/env python3
"""Validate a Nintex Automation K2 5.9+ Web Component control source."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path


LEGACY_PATTERNS = {
    "legacy binary": re.compile(r"\.(?:dll|exe|pdb)$", re.I),
    "legacy project": re.compile(r"\.(?:cs|csproj|sln|resx)$", re.I),
    "legacy control definition": re.compile(r"(?:definition|controltype).*\.(?:xml|config)$", re.I),
}
LEGACY_TEXT = re.compile(
    r"\b(?:controlutil(?:\.exe)?|ControlTypeDefinition|"
    r"SourceCode\.Forms\.Controls\.Web\.SDK|class\s+\w+\s*:\s*BaseControl|"
    r"strong[- ]name|gacutil)\b",
    re.I,
)
TAG_RE = re.compile(r"^[a-z][a-z0-9]*(?:-[a-z0-9]+)+$")
WIDTH_RE = re.compile(r"^(?:|(?:0|[1-9]\d{0,4})|(?:0|[1-9]\d{0,4})px|(?:\d{1,2}(?:\.\d+)?|100(?:\.0+)?)%)$")
RESOURCE_KEYS = (
    "designtimeScriptFileNames",
    "runtimeScriptFileNames",
    "designtimeStyleFileNames",
    "runtimeStyleFileNames",
    "imageFileNames",
)
REQUIRED_KEYS = (
    "displayName",
    "tagName",
    "description",
    "icon",
    "designtimeScriptFileNames",
    "runtimeScriptFileNames",
)
RESERVED_PROPERTIES = {"TabIndex", "ControlExpression"}
ALLOWED_PROPERTY_TYPES = {"string", "text", "bool", "drop", "int", "listdata", "list"}


def _strings(value: object) -> list[str]:
    return [item for item in value if isinstance(item, str)] if isinstance(value, list) else []


def validate(source: Path) -> list[str]:
    errors: list[str] = []
    source = source.resolve()
    manifest_path = source / "manifest.json"
    if not source.is_dir():
        return [f"Source directory not found: {source}"]
    if not manifest_path.is_file():
        return [f"manifest.json must exist at the control root: {manifest_path}"]

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except Exception as exc:  # noqa: BLE001 - diagnostic boundary
        return [f"Invalid manifest.json: {exc}"]
    if not isinstance(manifest, dict):
        return ["manifest.json root must be an object."]

    for key in REQUIRED_KEYS:
        if key not in manifest or manifest[key] in (None, "", []):
            errors.append(f"manifest.json requires {key}.")

    tag = manifest.get("tagName")
    if not isinstance(tag, str) or not TAG_RE.fullmatch(tag):
        errors.append("tagName must be a lowercase kebab-case custom-element name containing a hyphen.")

    supports = _strings(manifest.get("supports", []))
    properties = manifest.get("properties", [])
    events = manifest.get("events", [])
    methods = manifest.get("methods", [])
    if not isinstance(properties, list):
        errors.append("properties must be an array.")
        properties = []
    if not isinstance(events, list):
        errors.append("events must be an array.")
        events = []
    if not isinstance(methods, list):
        errors.append("methods must be an array.")
        methods = []

    prop_ids: list[str] = []
    listdata = 0
    for index, prop in enumerate(properties):
        if not isinstance(prop, dict):
            errors.append(f"properties[{index}] must be an object.")
            continue
        prop_id = prop.get("id")
        prop_type = str(prop.get("type", "")).lower()
        if not isinstance(prop_id, str) or not prop_id:
            errors.append(f"properties[{index}] requires id.")
        else:
            prop_ids.append(prop_id)
            if prop_id in RESERVED_PROPERTIES:
                errors.append(f"{prop_id} is reserved and may appear only in supports.")
        if prop_type not in ALLOWED_PROPERTY_TYPES:
            errors.append(f"Property {prop_id or index} has unsupported type '{prop_type}'.")
        if prop_type == "listdata":
            listdata += 1
        if prop_type == "drop":
            items = prop.get("dropitems")
            if not isinstance(items, list) or not items:
                errors.append(f"Drop property {prop_id or index} requires non-empty dropitems.")
        if prop_id == "Width":
            initial = "" if prop.get("initialvalue") is None else str(prop.get("initialvalue"))
            if not WIDTH_RE.fullmatch(initial):
                errors.append(
                    "Width initialvalue must be empty, a whole number, <=32767px, or a percentage <=100%."
                )
            else:
                numeric = re.match(r"^(\d+)(?:px)?$", initial)
                if numeric and int(numeric.group(1)) > 32767:
                    errors.append("Width initialvalue cannot exceed 32767px.")
    if listdata > 1:
        errors.append("A K2 Web Component may declare at most one listdata property.")
    if "DataBinding" in supports and listdata != 1:
        errors.append("DataBinding support requires exactly one listdata property.")
    if "ControlExpression" in supports and "Value" not in supports:
        errors.append("ControlExpression support requires Value support.")
    for duplicate, count in Counter(prop_ids).items():
        if count > 1:
            errors.append(f"Duplicate property id: {duplicate}.")

    for collection_name, collection in (("event", events), ("method", methods)):
        ids = []
        for index, item in enumerate(collection):
            if not isinstance(item, dict) or not isinstance(item.get("id"), str) or not item["id"]:
                errors.append(f"{collection_name}s[{index}] requires id.")
            else:
                ids.append(item["id"])
        for duplicate, count in Counter(ids).items():
            if count > 1:
                errors.append(f"Duplicate {collection_name} id: {duplicate}.")

    declared: list[str] = []
    for key in RESOURCE_KEYS:
        value = manifest.get(key, [])
        if value is None:
            continue
        if not isinstance(value, list) or any(not isinstance(item, str) or not item for item in value):
            errors.append(f"{key} must be an array of non-empty filenames.")
            continue
        declared.extend(value)
    icon = manifest.get("icon")
    if isinstance(icon, str) and icon:
        declared.append(icon)

    normalized_declared: list[str] = []
    for relative in declared:
        rel = Path(relative)
        if rel.is_absolute() or ".." in rel.parts:
            errors.append(f"Unsafe resource path: {relative}.")
            continue
        full = (source / rel).resolve()
        try:
            full.relative_to(source)
        except ValueError:
            errors.append(f"Resource escapes the control root: {relative}.")
            continue
        if not full.is_file():
            errors.append(f"Declared resource not found: {relative}.")
        normalized_declared.append(relative.replace("\\", "/").lower())
    for duplicate, count in Counter(normalized_declared).items():
        if count > 1:
            errors.append(f"Resource is declared more than once: {duplicate}.")

    files = [path for path in source.rglob("*") if path.is_file()]
    leaf_counts = Counter(path.name.lower() for path in files)
    for leaf, count in leaf_counts.items():
        if count > 1:
            errors.append(f"Duplicate filename anywhere in the package: {leaf}.")
    for path in files:
        relative = path.relative_to(source).as_posix()
        for label, pattern in LEGACY_PATTERNS.items():
            if pattern.search(path.name):
                errors.append(f"{label} is forbidden in a modern Web Component package: {relative}.")
        if path.suffix.lower() in {".js", ".css", ".json", ".md", ".xml", ".config"}:
            try:
                text = path.read_text(encoding="utf-8-sig")
            except UnicodeDecodeError:
                continue
            match = LEGACY_TEXT.search(text)
            if match:
                errors.append(f"Legacy custom-control token '{match.group(0)}' is forbidden: {relative}.")

    scripts = _strings(manifest.get("designtimeScriptFileNames", [])) + _strings(
        manifest.get("runtimeScriptFileNames", [])
    )
    script_text = ""
    for relative in scripts:
        path = source / relative
        if path.is_file():
            script_text += "\n" + path.read_text(encoding="utf-8-sig", errors="replace")
    if "extends K2BaseControl" not in script_text:
        errors.append("Design/runtime JavaScript must define a class that extends K2BaseControl.")
    if "customElements.define" not in script_text:
        errors.append("JavaScript must register the custom element with customElements.define.")
    if isinstance(tag, str) and tag and tag not in script_text:
        errors.append("JavaScript does not contain the manifest tagName.")
    if "DataBinding" in supports and "listItemsChangedCallback" not in script_text:
        errors.append("DataBinding support requires listItemsChangedCallback().")
    for support in ("Value", "Width", "Height", "IsVisible", "IsEnabled", "IsReadOnly", "TabIndex"):
        if support in supports and not re.search(rf"\b(?:get|set)\s+{re.escape(support)}\b", script_text):
            errors.append(f"{support} support requires a JavaScript getter/setter implementation.")
    if "K2.RaiseEvent" in script_text:
        errors.append("K2.RaiseEvent is not part of the modern client API; dispatch a declared DOM Event.")
    if any(isinstance(item, dict) and item.get("id") for item in events) and "dispatchEvent(new Event(" not in script_text:
        errors.append("Declared events require DOM Event dispatch.")
    if _strings(manifest.get("designtimeStyleFileNames", [])) or _strings(manifest.get("runtimeStyleFileNames", [])):
        if "SourceCode.Forms.ControlStyles" not in script_text or "loadStyleResources" not in script_text:
            errors.append("Declared CSS resources require SourceCode.Forms.ControlStyles.loadStyleResources().")
        if "ensureShadow()" in script_text and not (
            "RuntimeStyleFileNames =" in script_text and "DesigntimeStyleFileNames =" in script_text
        ):
            errors.append("Shadow-DOM controls must supply array resource metadata fallbacks for generated placements.")
    if methods and not re.search(r"\bexecute\s*\(", script_text):
        errors.append("Declared methods require execute(objInfo).")

    return sorted(set(errors))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    args = parser.parse_args()
    errors = validate(args.source)
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 2
    manifest = json.loads((args.source / "manifest.json").read_text(encoding="utf-8-sig"))
    print(f"Modern K2 Web Component: OK ({manifest['tagName']}, {manifest['displayName']})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
