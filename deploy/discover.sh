#!/usr/bin/env bash
set -uo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/common.sh"

validate_application_units "$@"

section() { printf '\n## %s\n' "$1"; }

section "Operating system and .NET runtimes"
awk -F= '/^(PRETTY_NAME|VERSION_ID)=/' /etc/os-release
uname -r
if command -v dotnet >/dev/null; then dotnet --list-runtimes; fi

section "Service units"
systemctl list-units --type=service --all --no-pager --plain
systemctl list-unit-files --type=service --no-pager

section "Selected service metadata and resource counters"
for unit in "$@" nginx.service mssql-server.service; do
    printf '\n%s\n' "$unit"
    systemctl show --no-pager --all \
        --property=Id,LoadState,ActiveState,SubState,CanReload,FragmentPath,DropInPaths,WorkingDirectory,User,MainPID,ControlGroup,MemoryCurrent,MemoryPeak,TasksCurrent,CPUUsageNSec \
        -- "$unit"
done

section "Listening sockets"
ss -tulpn

section "Cgroups, memory and local disks"
stat -fc %T /sys/fs/cgroup
findmnt -n -t cgroup2 --output TARGET
free -h
df -h -l

section "Installed Nginx layouts and candidate site files"
for layout in ubuntu aapanel; do
    binary=$(nginx_binary "$layout")
    if [[ -x $binary ]]; then
        printf '%s\n' "$binary"
        "$binary" -v
    fi
done
for dir in /etc/nginx/sites-enabled /etc/nginx/conf.d /www/server/panel/vhost/nginx; do
    if [[ -d $dir ]]; then find "$dir" -maxdepth 1 \( -type f -o -type l \) -print; fi
done

printf '\n%s\n' "Review the referenced units and site files locally to verify assemblies, deployment paths, ports and URLs."
