#!/usr/bin/env python3
"""Validate a reusable case UX design contract."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any


def load(path: Path) -> dict[str, Any]:
    text = path.read_text(encoding="utf-8")
    try:
        value = json.loads(text)
    except json.JSONDecodeError as json_error:
        try:
            import yaml  # type: ignore
        except ImportError as exc:
            raise ValueError(f"{path}: invalid JSON-compatible YAML and PyYAML is unavailable ({json_error})") from exc
        value = yaml.safe_load(text)
    if not isinstance(value, dict):
        raise ValueError("document root must be a mapping")
    return value


def validate(doc: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    pages = doc.get("pages")
    components = doc.get("components")
    journeys = doc.get("journeys")
    shell = doc.get("shell")
    visual = doc.get("visual_acceptance")
    if not isinstance(shell, dict): errors.append("shell must be a mapping")
    if not isinstance(pages, list) or not pages: errors.append("pages must be a non-empty list"); pages = []
    if not isinstance(components, list) or not components: errors.append("components must be a non-empty list"); components = []
    if not isinstance(journeys, list) or not journeys: errors.append("journeys must be a non-empty list"); journeys = []

    def index(items: list[Any], label: str) -> dict[str, dict[str, Any]]:
        found: dict[str, dict[str, Any]] = {}
        for n, item in enumerate(items):
            if not isinstance(item, dict): errors.append(f"{label}[{n}] must be a mapping"); continue
            ident = item.get("id")
            if not ident: errors.append(f"{label}[{n}].id is required"); continue
            if ident in found: errors.append(f"duplicate {label} id: {ident}")
            found[ident] = item
        return found

    page_by_id = index(pages, "pages")
    component_by_id = index(components, "components")
    journey_by_id = index(journeys, "journeys")
    role_codes = {r.get("code") for r in doc.get("roles", []) if isinstance(r, dict)}

    if isinstance(shell, dict):
        for item in shell.get("navigation", []):
            target = item.get("target") if isinstance(item, dict) else None
            if target not in page_by_id: errors.append(f"navigation target does not exist: {target}")
        create = shell.get("primary_create_action")
        nav_ids = {n.get("id") for n in shell.get("navigation", []) if isinstance(n, dict)}
        if create not in nav_ids: errors.append("shell.primary_create_action must name a navigation item")

    for page_id, page in page_by_id.items():
        for role in page.get("roles", []):
            if role not in role_codes: errors.append(f"page {page_id} references unknown role: {role}")
        for component in page.get("components", []):
            if component not in component_by_id: errors.append(f"page {page_id} references unknown component: {component}")
        for key in ("component", "persistent_header", "lifecycle", "action_panel"):
            value = page.get(key)
            if value and value not in component_by_id: errors.append(f"page {page_id}.{key} references unknown component: {value}")
        journey = page.get("journey")
        if journey and journey not in journey_by_id: errors.append(f"page {page_id} references unknown journey: {journey}")

    for component_id, component in component_by_id.items():
        kind = component.get("kind")
        if kind in {"kpi", "chart"}:
            for field in ("measure", "drilldown"):
                if not component.get(field): errors.append(f"{kind} {component_id} requires {field}")
        if kind == "chart" and component.get("table_alternative") is not True:
            errors.append(f"chart {component_id} requires table_alternative: true")
        if kind in {"chart", "queue", "timeline"} and not component.get("empty_state"):
            errors.append(f"{kind} {component_id} requires empty_state")
        drilldown = component.get("drilldown")
        if drilldown and drilldown not in component_by_id and drilldown not in page_by_id:
            errors.append(f"component {component_id} drilldown target does not exist: {drilldown}")

    for journey_id, journey in journey_by_id.items():
        steps = journey.get("steps")
        if not isinstance(steps, list) or len(steps) < 2: errors.append(f"journey {journey_id} requires at least two steps"); continue
        if journey.get("review_before_submit") is not True: errors.append(f"journey {journey_id} requires review_before_submit: true")
        if not any(isinstance(step, dict) and step.get("summary") is True for step in steps): errors.append(f"journey {journey_id} requires a summary step")
        target = journey.get("success_target")
        if target not in page_by_id: errors.append(f"journey {journey_id} success target does not exist: {target}")

    if not isinstance(visual, dict):
        errors.append("visual_acceptance must be a mapping")
    else:
        viewport_names = {v.get("name") for v in visual.get("viewports", []) if isinstance(v, dict)}
        for required in ("desktop", "tablet", "mobile"):
            if required not in viewport_names: errors.append(f"visual_acceptance requires {required} viewport")
        states = set(visual.get("states", []))
        for required in ("populated", "empty", "validation-error", "long-content", "read-only"):
            if required not in states: errors.append(f"visual_acceptance requires {required} state")
    return errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    args = parser.parse_args(argv)
    try:
        errors = validate(load(args.manifest))
    except (OSError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr); return 2
    if errors:
        for error in errors: print(f"ERROR: {error}", file=sys.stderr)
        print(f"Validation failed with {len(errors)} error(s).", file=sys.stderr); return 1
    print(f"Valid case UX design manifest: {args.manifest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
