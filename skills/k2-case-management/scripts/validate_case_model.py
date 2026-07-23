#!/usr/bin/env python3
"""Validate a k2-case-management case-type design manifest.

The shipped .yaml assets use JSON-compatible YAML, so validation needs only the
Python standard library. PyYAML is used when installed to accept general YAML.
These design manifests are not native K2 deployment inputs.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any


def load_document(path: Path) -> dict[str, Any]:
    text = path.read_text(encoding="utf-8")
    try:
        value = json.loads(text)
    except json.JSONDecodeError as json_error:
        try:
            import yaml  # type: ignore
        except ImportError as exc:
            raise ValueError(
                f"{path}: not JSON-compatible YAML and PyYAML is unavailable "
                f"({json_error.msg} at line {json_error.lineno})"
            ) from exc
        value = yaml.safe_load(text)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: document root must be a mapping")
    return value


def validate(document: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    case_type = document.get("case_type")
    stages = document.get("stages")
    transitions = document.get("transitions")
    if not isinstance(case_type, dict):
        return ["case_type must be a mapping"]
    for field in ("code", "name", "initial_stage", "retention_code", "configuration_version"):
        if not case_type.get(field):
            errors.append(f"case_type.{field} is required")
    if not isinstance(stages, list) or not stages:
        errors.append("stages must be a non-empty list")
        stages = []
    if not isinstance(transitions, list):
        errors.append("transitions must be a list")
        transitions = []

    stage_by_code: dict[str, dict[str, Any]] = {}
    for index, stage in enumerate(stages):
        if not isinstance(stage, dict):
            errors.append(f"stages[{index}] must be a mapping")
            continue
        code = stage.get("code")
        if not code:
            errors.append(f"stages[{index}].code is required")
            continue
        if code in stage_by_code:
            errors.append(f"duplicate stage code: {code}")
        stage_by_code[code] = stage
        if not stage.get("workflow"):
            errors.append(f"stage {code} requires workflow")

    initial = case_type.get("initial_stage")
    if initial and initial not in stage_by_code:
        errors.append(f"initial stage does not exist: {initial}")
    if stage_by_code and not any(stage.get("terminal") is True for stage in stage_by_code.values()):
        errors.append("at least one stage must set terminal: true")

    seen_transitions: set[tuple[Any, Any, Any]] = set()
    outgoing: dict[str, int] = {code: 0 for code in stage_by_code}
    graph: dict[str, set[str]] = {code: set() for code in stage_by_code}
    sequence = {code: stage.get("sequence", 0) for code, stage in stage_by_code.items()}
    for index, transition in enumerate(transitions):
        if not isinstance(transition, dict):
            errors.append(f"transitions[{index}] must be a mapping")
            continue
        source, outcome, target = (transition.get(key) for key in ("from", "outcome", "to"))
        if not source:
            errors.append(f"transitions[{index}].from is required")
        if not outcome:
            errors.append(f"transitions[{index}].outcome is required")
        if not target:
            errors.append(f"transitions[{index}].to is required")
        identity = (source, outcome, target)
        if identity in seen_transitions:
            errors.append(f"duplicate transition: {source}/{outcome}/{target}")
        seen_transitions.add(identity)
        if source not in stage_by_code:
            errors.append(f"transition source does not exist: {source}")
        if target not in stage_by_code:
            errors.append(f"transition destination does not exist: {target}")
        if source in stage_by_code and target in stage_by_code:
            outgoing[source] += 1
            graph[source].add(target)
            if stage_by_code[source].get("terminal") is True and transition.get("reopen") is not True:
                errors.append(f"terminal stage {source} has a transition not marked reopen")
            try:
                backward = sequence[target] <= sequence[source]
            except TypeError:
                backward = False
            if backward and transition.get("reentry") is not True and transition.get("reopen") is not True:
                errors.append(f"cyclic/backward transition {source}->{target} must set reentry or reopen")

    for code, stage in stage_by_code.items():
        if stage.get("terminal") is not True and outgoing[code] == 0:
            errors.append(f"nonterminal stage has no possible exit: {code}")

    if initial in graph:
        reachable: set[str] = set()
        pending = [initial]
        while pending:
            code = pending.pop()
            if code in reachable:
                continue
            reachable.add(code)
            pending.extend(graph[code] - reachable)
        for code in sorted(set(stage_by_code) - reachable):
            errors.append(f"unreachable stage: {code}")
    return errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path, help="case-type YAML/JSON design manifest")
    args = parser.parse_args(argv)
    try:
        errors = validate(load_document(args.manifest))
    except (OSError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        print(f"Validation failed with {len(errors)} error(s).", file=sys.stderr)
        return 1
    print(f"Valid case-type design manifest: {args.manifest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
