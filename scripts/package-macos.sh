#!/bin/bash
set -euo pipefail
if [ "$#" -ne 2 ]; then
  echo "Usage: $0 /path/to/BossIsComing.app /path/to/BossIsComing-macOS-Universal.dmg" >&2
  exit 2
fi
app_path="$1"
dmg_path="$2"
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
if [ ! -d "$app_path/Contents" ]; then echo "Application bundle not found" >&2; exit 2; fi
stage_dir="$(mktemp -d /tmp/boss-is-coming-dmg.XXXXXX)"
trap 'rm -rf "$stage_dir"' EXIT
/usr/bin/ditto "$app_path" "$stage_dir/BossIsComing.app"
ln -s /Applications "$stage_dir/Applications"
cp "$repo_root/Docs/MACOS.md" "$stage_dir/README.md"
/usr/bin/hdiutil create -volname "Boss Is Coming" -srcfolder "$stage_dir" -fs HFS+ -format UDZO -ov "$dmg_path"
/usr/bin/hdiutil verify "$dmg_path"
