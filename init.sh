#!/usr/bin/env bash
#
# Single verification entrypoint for agents and humans.
# This is the gate the harness requires before any "done" claim:
# restore tools, then restore + build + test through Cake — the exact
# pipeline CI runs (CONTRIBUTING.md §3/§4). Fails fast on the first error.
#
# Usage:
#   ./init.sh            # restore + build + test (the definition-of-done gate)
#   ./init.sh publish    # also produce ./artifacts/publish/
#
# Clean restart: this script is idempotent and restartable — safe to re-run
# from a fresh checkout at any time. It changes no source files; a red run
# means fix the root cause, never weaken the check (AGENTS.md hard rules).

set -euo pipefail

cd "$(dirname "$0")"

echo "==> dotnet tool restore"
dotnet tool restore

echo "==> dotnet cake --target=Test --configuration=Release (restore + build + test)"
dotnet cake --target=Test --configuration=Release

if [[ "${1:-}" == "publish" ]]; then
	echo "==> dotnet cake --target=Publish (build artifacts -> ./artifacts/publish/)"
	dotnet cake --target=Publish --configuration=Release
fi

echo
echo "==> Verification Evidence: build + tests are GREEN."
echo "    Record the command and output in progress.md before claiming a feature done."
echo
echo "Next steps:"
echo "  - Docs changed? cd website && npm ci && npm run build  (onBrokenLinks: throw)"
echo "  - Update CHANGELOG.md [Unreleased] for any user-visible change."
echo "  - Update progress.md / session-handoff.md before ending the session."
