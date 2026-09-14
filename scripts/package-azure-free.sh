#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 /absolute/path/to/yonetim-bilgi-azure.zip" >&2
  exit 2
fi

output_zip="$1"
if [[ "$output_zip" != /* ]]; then
  echo "Output path must be absolute" >&2
  exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_bin="${DOTNET_BIN:-dotnet}"
for runtime in Microsoft.NETCore.App Microsoft.AspNetCore.App; do
  patch_version="$("$dotnet_bin" --list-runtimes | awk -v runtime="$runtime" '$1 == runtime && $2 ~ /^9[.]0[.]/ { split($2, version, "."); if (version[3] > max) max = version[3] } END { print max + 0 }')"
  if (( patch_version < 20 )); then
    echo "$runtime 9.0.20 or newer is required for a security-patched self-contained package" >&2
    exit 1
  fi
done
build_dir="$(mktemp -d)"
trap 'rm -rf "$build_dir"' EXIT

(
  cd "$repo_root/frontend"
  npm ci
  npm run build
)

"$dotnet_bin" publish "$repo_root/backend/src/PersonelYonetim.Api/PersonelYonetim.Api.csproj" \
  --configuration Release --runtime win-x64 --self-contained true \
  --output "$build_dir/publish"

mkdir -p "$build_dir/publish/wwwroot" "$(dirname "$output_zip")"
cp -R "$repo_root/frontend/dist/." "$build_dir/publish/wwwroot/"

(
  cd "$build_dir/publish"
  zip -q -r "$build_dir/package.zip" .
)
mv -f "$build_dir/package.zip" "$output_zip"

echo "Azure App Service package: $output_zip"
