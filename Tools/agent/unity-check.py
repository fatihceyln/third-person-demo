#!/usr/bin/env python3
"""Verify the project after an agent change.

Usage:
  Tools/agent/unity-check.py mark    Run before a change. Remembers the current Console
                                     position and the files that are already modified.
  Tools/agent/unity-check.py check   Run after a change. Waits for Unity to compile, then
                                     reports compile errors, new Console errors/warnings,
                                     the files changed since `mark`, code-guard
                                     violations in the changed code files and the
                                     Unity project validator's findings.

Exit codes: 0 = clean, 1 = problems found, 2 = Unity Editor not reachable or no mark.
"""

import hashlib
import json
import subprocess
import sys
import time
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[2]
STATE_FILE = PROJECT_ROOT / "Library" / "AgentCheck" / "state.json"
COMPILE_TIMEOUT_SECONDS = 180

# Messages the Editor or the CLI bridge log on their own; they are not caused by our changes.
IGNORED_MESSAGES = (
    "Account API did not become accessible",
    "Failed to handle /api/exec request",
)

# Files that Unity tends to re-save as a side effect. A change here is usually unintended.
WATCHED_FILES = (
    "Assets/Settings/",
    "-SDF.asset",
    "ProjectSettings/",
    "Packages/",
)


def main():
    command = sys.argv[1] if len(sys.argv) > 1 else ""

    if command == "mark":
        sys.exit(mark())

    if command == "check":
        sys.exit(check())

    print(__doc__)
    sys.exit(2)


def mark():
    console = run_unity("console", "--tail", "1")

    if console is None:
        return 2

    state = {"cursor": console["cursor"], "files": dirty_file_hashes()}
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    STATE_FILE.write_text(json.dumps(state, indent=2))
    print(f"Marked. Console cursor: {state['cursor']}, already modified files: {len(state['files'])}")
    return 0


def check():
    if not STATE_FILE.exists():
        print("No mark found. Run `Tools/agent/unity-check.py mark` before making a change.")
        return 2

    state = json.loads(STATE_FILE.read_text())
    problems = 0

    # Unity only notices files edited outside the Editor after a refresh. The refresh can start a
    # script reload that drops the connection, so a failed call here is left to wait_for_unity.
    run_eval("UnityEditor.AssetDatabase.Refresh(); return true;", quiet=True)

    if not wait_for_unity():
        return 2

    editor = run_eval(
        "return UnityEditor.EditorUtility.scriptCompilationFailed + \"|\""
        " + UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;"
    )

    if editor is None:
        return 2

    compile_failed, playing = (value == "True" for value in editor.split("|"))

    print("== Compile")
    print("FAILED" if compile_failed else "OK")
    problems += compile_failed

    if playing:
        print("\nNote: the Editor is in Play Mode; scene changes made now are lost when it exits.")

    errors, warnings = new_console_entries(state["cursor"])

    if not compile_failed:
        # Compile errors logged earlier in the task were fixed by a later successful compile.
        errors = [entry for entry in errors if ": error CS" not in entry["message"]]
    print(f"\n== Console since mark: {len(errors)} error(s), {len(warnings)} warning(s)")

    for entry in errors + warnings:
        print(f"[{entry['level']}] {first_line(entry['message'])}")
        location = first_project_frame(entry.get("stackTrace", ""))

        if location:
            print(f"    at {location}")

    problems += len(errors)

    changed = changed_files(state["files"])
    print(f"\n== Files changed since mark: {len(changed)}")

    for status, path in changed:
        watched = any(pattern in path for pattern in WATCHED_FILES)
        print(f"{status} {path}{'   <- check: Unity may have re-saved this' if watched else ''}")

    # The Claude Code hook only sees its own edits; this also covers shell edits and other agents.
    code_files = [path for _, path in changed if path.endswith((".cs", ".shader", ".hlsl"))]
    print(f"\n== Code guard on changed code files: {len(code_files)}", flush=True)

    if code_files:
        guard = subprocess.run([sys.executable, str(PROJECT_ROOT / "Tools/agent/code-guard.py"), *code_files], cwd=PROJECT_ROOT)
        problems += guard.returncode != 0

    # Setup mistakes (empty Inspector fields, missing scripts, broken references) only show up in Unity.
    print("\n== Project validator (Tools > ArrowGame > Validate Project)")

    if compile_failed or playing:
        print("Skipped: needs a successful compile and Edit Mode.")
    else:
        report = run_eval("return ArrowGame.EditorTools.ProjectValidator.RunAsText();", timeout=120)
        print(report if report is not None else "Could not run the validator.")
        problems += report is None or report.count("ERROR ") > 0

    print("\nRESULT:", "PROBLEMS FOUND" if problems else "CLEAN")
    return 1 if problems else 0


def wait_for_unity():
    deadline = time.time() + COMPILE_TIMEOUT_SECONDS

    while time.time() < deadline:
        # The CLI can drop out while Unity reloads scripts, so a failed call here means "keep waiting".
        busy = run_eval(
            "return UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating;",
            quiet=True,
        )

        if busy is False:
            return True

        time.sleep(2)

    print(f"Unity was still busy after {COMPILE_TIMEOUT_SECONDS} seconds.")
    return False


def new_console_entries(cursor):
    console = run_unity("console", "--since", str(cursor)) or {"entries": []}
    entries = [
        entry for entry in console["entries"]
        if not any(ignored in entry["message"] for ignored in IGNORED_MESSAGES)
    ]
    errors = [entry for entry in entries if entry["level"] in ("error", "exception", "assert")]
    warnings = [entry for entry in entries if entry["level"] == "warn"]
    return errors, warnings


def changed_files(files_at_mark):
    changed = []

    for status, path in git_status():
        if files_at_mark.get(path) != file_hash(path):
            changed.append((status, path))

    return changed


def dirty_file_hashes():
    return {path: file_hash(path) for _, path in git_status()}


def git_status():
    output = subprocess.run(
        ["git", "status", "--porcelain", "--untracked-files=all"],
        cwd=PROJECT_ROOT, capture_output=True, text=True, check=True,
    ).stdout
    return [(line[:2].strip(), line[3:]) for line in output.splitlines()]


def file_hash(path):
    full_path = PROJECT_ROOT / path
    return hashlib.sha1(full_path.read_bytes()).hexdigest() if full_path.is_file() else "deleted"


def run_eval(code, quiet=False, timeout=15):
    response = run_unity("eval", code, quiet=quiet, timeout=timeout)
    return None if response is None else response.get("result")


def run_unity(*args, quiet=False, timeout=15):
    process = subprocess.run(
        ["unity", "command", "--result-only", "--timeout", str(timeout), *args],
        cwd=PROJECT_ROOT, capture_output=True, text=True,
    )

    try:
        response = json.loads(process.stdout)
    except json.JSONDecodeError:
        response = None

    if response is None or response.get("success") is False:
        if not quiet:
            print("Could not reach the Unity Editor. Is the project open in Unity?")
            print((process.stdout + process.stderr).strip()[:500])
        return None

    return response


def first_line(text):
    return text.strip().splitlines()[0] if text.strip() else ""


def first_project_frame(stack_trace):
    for line in stack_trace.splitlines():
        if "(at Assets/" in line:
            return line.split("(at ", 1)[1].rstrip(")")

    return ""


if __name__ == "__main__":
    main()
