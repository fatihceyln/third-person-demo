#!/usr/bin/env python3
"""Catch code patterns that AGENTS.md forbids.

Usage:
  Tools/agent/code-guard.py                 Scan all game scripts and shaders.
  Tools/agent/code-guard.py FILE [FILE...]  Scan only the given files.
  Tools/agent/code-guard.py --hook          Claude Code hook mode: read the edited file from
                                            the hook's JSON on stdin.

Scanned: Assets/Scripts/**/*.cs and Assets/Shaders/**/*.shader|*.hlsl. Editor tools under
Assets/Editor are not scanned, since editor code may legitimately look objects up.

A line can opt out of a rule when there is a real reason, by ending with:
  // agent-guard: allow <reason>

Exit codes: 0 = clean, 1 = violations found (2 in --hook mode, so Claude Code shows them).
"""

import json
import re
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[2]
SCANNED_FOLDERS = ("Assets/Scripts/", "Assets/Shaders/")
ALLOW_MARKER = "agent-guard: allow"

# (file suffixes, pattern, problem, fix)
RULES = [
    ((".cs",), r"\bCamera\.main\b",
     "Camera.main looks the camera up at runtime.",
     "Add a [SerializeField] Camera field and wire it in the Inspector."),
    ((".cs",), r"\b(GetComponent|GetComponents|GetComponentInChildren|GetComponentInParent|AddComponent)\b",
     "Components are looked up or created in code.",
     "Add a [SerializeField] field and wire it in the Inspector. For objects found at runtime "
     "(e.g. physics hits), use TryGetComponent."),
    ((".cs",), r"\b(FindObjectOfType|FindObjectsOfType|FindAnyObjectByType|FindFirstObjectByType|"
               r"FindObjectsByType|FindWithTag|FindGameObjectWithTag|FindGameObjectsWithTag)\b|\bGameObject\.Find\b",
     "Objects are searched for in code.",
     "Add a [SerializeField] field and wire it in the Inspector."),
    ((".cs",), r"\bInput\.(Get\w+|mousePosition|mouseScrollDelta|touches|touchCount|anyKey\w*|inputString|acceleration)\b",
     "Legacy Input Manager API; it throws because the project uses the Input System only.",
     "Use UnityEngine.InputSystem: Mouse.current, Keyboard.current or an InputAction."),
    ((".cs",), r"\.(velocity|drag|angularDrag)\b",
     "Obsolete Rigidbody API in Unity 6.",
     "Use linearVelocity, linearDamping or angularDamping."),
    ((".cs",), r"\.materials?\b",
     "renderer.material creates a hidden material copy per object.",
     "Use a MaterialPropertyBlock for per-object values, or sharedMaterial to change the asset."),
    ((".cs",), r"\bUIDocument\b",
     "UIDocument is not used for new UI.",
     "Use PanelRenderer + UXML + USS + Panel Settings."),
    ((".cs",), r"\bprivate\s+(static\s+|readonly\s+)*[\w<>\[\],.? ]+\s_\w+\s*[;=]",
     "Private field with a _ prefix.",
     "Name fields in camelCase without a prefix (e.g. damageInterval)."),
    ((".shader", ".hlsl"), r"\bCGPROGRAM\b|UnityCG\.cginc",
     "Built-in pipeline shader code; the project uses URP.",
     "Use HLSLPROGRAM and include the URP Core.hlsl."),
]


def main():
    args = sys.argv[1:]

    if args == ["--hook"]:
        sys.exit(run_hook())

    if args in (["-h"], ["--help"]):
        print(__doc__)
        sys.exit(0)

    paths = [Path(arg).resolve() for arg in args] if args else all_scanned_files()
    violations = scan(paths)
    print_violations(violations, sys.stdout)

    if not violations:
        print(f"code-guard: {sum(map(is_scanned, paths))} scanned file(s) clean")

    sys.exit(1 if violations else 0)


def run_hook():
    event = json.load(sys.stdin)
    file_path = event.get("tool_input", {}).get("file_path")

    if not file_path:
        return 0

    violations = scan([Path(file_path).resolve()])

    if not violations:
        return 0

    # Claude Code passes stderr back to the agent when a hook exits with code 2.
    print_violations(violations, sys.stderr)
    return 2


def all_scanned_files():
    files = []

    for folder in SCANNED_FOLDERS:
        files += [path for path in (PROJECT_ROOT / folder).rglob("*") if is_scanned(path)]

    return sorted(files)


def scan(paths):
    violations = []

    for path in paths:
        if not is_scanned(path):
            continue

        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()

        for number, line in enumerate(lines, start=1):
            if ALLOW_MARKER in line:
                continue

            code = strip_comment(line)

            for suffixes, pattern, problem, fix in RULES:
                if path.suffix in suffixes and re.search(pattern, code):
                    violations.append((path, number, line.strip(), problem, fix))

    return violations


def is_scanned(path):
    try:
        relative = path.relative_to(PROJECT_ROOT).as_posix()
    except ValueError:
        return False

    return relative.startswith(SCANNED_FOLDERS) and path.suffix in (".cs", ".shader", ".hlsl") and path.is_file()


def strip_comment(line):
    stripped = line.strip()

    if stripped.startswith(("//", "/*", "*")):
        return ""

    return line.split("//", 1)[0]


def print_violations(violations, stream):
    for path, number, line, problem, fix in violations:
        relative = path.relative_to(PROJECT_ROOT).as_posix()
        print(f"{relative}:{number}: {problem}", file=stream)
        print(f"    {line}", file=stream)
        print(f"    Fix: {fix}", file=stream)

    if violations:
        print(f"code-guard: {len(violations)} violation(s). See AGENTS.md. "
              f"If a line is a real exception, end it with `// {ALLOW_MARKER} <reason>`.", file=stream)


if __name__ == "__main__":
    main()
