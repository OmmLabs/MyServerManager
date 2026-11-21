#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/common.sh"

if [[ ${1:-} == --print ]]; then
    (( $# >= 3 )) || die "Usage: approve-services.sh --print ubuntu|aapanel manage|monitor [UNIT ...]"
    binary=$(nginx_binary "$2")
    mode=$3
    shift 3
    validate_nginx_mode "$mode"
    validate_application_units "$@"
    render_sudoers "$binary" "$mode" "$@"
    exit 0
fi

lock_deployment
[[ -f $manager_config/manager.env && -f $manager_sudoers ]] ||
    die "Install Server App Manager before updating approvals."
validate_application_units "$@"
for unit in "$@"; do verify_loaded_unit "$unit"; done

layout=$(sed -n 's/^Manager__NginxLayout=//p' "$manager_config/manager.env")
binary=$(nginx_binary "$layout")
mode=monitor
if grep -Fxq 'Manager__EnableNginxControls=true' "$manager_config/manager.env"; then mode=manage; fi
install_policy "$binary" "$mode" "$@"
printf '%s\n' "Operator approvals updated. Use Settings to register newly approved units."
