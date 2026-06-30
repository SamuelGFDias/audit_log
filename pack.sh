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

pack_project() {
  echo "    Packing $1..."
  dotnet pack "$ROOT_DIR/$1" \
    --configuration "$CONFIGURATION" \
    --no-build \
    --output "$PACKAGES_DIR"
}

echo ""
echo "==> Packing core (AuditLog)..."

pack_project "src/AuditLog.Abstractions/AuditLog.Abstractions.csproj"
pack_project "src/AuditLog.EntityFrameworkCore/AuditLog.EntityFrameworkCore.csproj"
pack_project "src/AuditLog.Generator/AuditLog.Generator.csproj"

echo ""
echo "==> Packing optional (SoftDelete)..."

pack_project "src/AuditLog.EntityFrameworkCore.SoftDelete/AuditLog.EntityFrameworkCore.SoftDelete.csproj"
pack_project "src/AuditLog.Generator.SoftDelete/AuditLog.Generator.SoftDelete.csproj"

echo ""
echo "==> Packages generated in $PACKAGES_DIR:"
ls -lh "$PACKAGES_DIR"/*.nupkg 2>/dev/null || echo "    (no packages found)"
