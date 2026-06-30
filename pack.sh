#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
ARTIFACTS_DIR="$ROOT_DIR/artifacts"
PACKAGES_DIR="$ARTIFACTS_DIR/packages"
CONFIGURATION="${CONFIGURATION:-Release}"

rm -rf "$ARTIFACTS_DIR"
mkdir -p "$PACKAGES_DIR"

echo "==> Restoring..."
dotnet restore "$ROOT_DIR/AuditLog.slnx"

echo "==> Building..."
dotnet build "$ROOT_DIR/AuditLog.slnx" \
  --configuration "$CONFIGURATION" \
  --no-restore

echo "==> Packing core projects..."
CORE_PROJECTS=(
  "src/AuditLog.Abstractions/AuditLog.Abstractions.csproj"
  "src/AuditLog.EntityFrameworkCore/AuditLog.EntityFrameworkCore.csproj"
  "src/AuditLog.Generator/AuditLog.Generator.csproj"
)

for project in "${CORE_PROJECTS[@]}"; do
  echo "    Packing $project..."
  dotnet pack "$ROOT_DIR/$project" \
    --configuration "$CONFIGURATION" \
    --no-build \
    --output "$PACKAGES_DIR"
done

echo ""
echo "==> Packages generated in $PACKAGES_DIR:"
ls -lh "$PACKAGES_DIR"/*.nupkg 2>/dev/null || echo "    (no packages found)"
