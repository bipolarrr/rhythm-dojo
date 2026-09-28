# Git collaboration setup verification

Verified on Windows, 2026-09-28, with Unity 6000.3.23f1.

- GitHub CLI 2.101.0 installed from Scoop's official main bucket.
- Project-local Unity merge driver configured; scene/prefab attributes use it, meta files use text merging.
- Isolated Git merge preserved independent prefab edits and left a conflicting edit unresolved without opening a fallback GUI.
- Staged-index checks passed valid assets, paired renames, empty folder preservation and working-tree-only changes.
- Checks rejected missing asset metadata, orphan metadata, duplicate GUIDs and missing folder metadata. An actual pre-commit hook rejected an invalid commit.
- Windows PowerShell 5.1 setup, validator and integration checks passed, including executable paths containing spaces. PowerShell 7 checks also passed.
- The project index passed with 160 metadata files. Generated files, caches, private IDE repair scripts and raw logs are excluded.
- The temporary setup instructions were removed from AGENTS.md after verification. The reusable setup document remains linked from README.

Reproduce with `./Tools/Test-UnityGitSetup.ps1` and `./Tools/Test-UnityMeta.ps1`. The integration script creates isolated repositories under the system temporary directory and prints their location; it does not modify project assets.
