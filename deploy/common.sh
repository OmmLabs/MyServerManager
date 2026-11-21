#!/usr/bin/env bash

export PATH=/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C
readonly manager_deploy_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
readonly manager_user=myservermanager
readonly manager_unit=myservermanager.service
readonly manager_config=/etc/myservermanager
readonly manager_state=/var/lib/myservermanager
readonly manager_home=/opt/myservermanager
readonly manager_sudoers=/etc/sudoers.d/myservermanager

die() {
    printf '%s\n' "$*" >&2
    exit 1
}

lock_deployment() {
    (( EUID == 0 )) || die "Run this operation as the server operator with sudo."
    exec 9>/run/myservermanager-deploy.lock
    flock -n 9 || die "Another Server App Manager deployment operation is in progress."
}

valid_service_name() {
    [[ ${#1} -le 128 && $1 =~ ^[A-Za-z0-9_][A-Za-z0-9_.-]*(@[A-Za-z0-9_.-]+)?\.service$ ]]
}

validate_application_units() {
    (( $# <= 100 )) || die "Approve at most 100 application services."
    local unit
    local -A seen=()
    for unit in "$@"; do
        valid_service_name "$unit" || die "An application unit has an invalid name."
        case "$unit" in
            nginx.service|mssql-server.service|myservermanager.service) die "Reserved service: $unit" ;;
        esac
        [[ ! -v "seen[$unit]" ]] || die "Duplicate service: $unit"
        seen["$unit"]=1
    done
}

verify_loaded_unit() {
    local unit=$1 properties
    properties=$(systemctl show --property=Id --property=LoadState -- "$unit") ||
        die "systemd could not inspect $unit."
    grep -Fxq -- "Id=$unit" <<<"$properties" && grep -Fxq -- 'LoadState=loaded' <<<"$properties" ||
        die "$unit must be a loaded, canonical systemd service."
}

nginx_binary() {
    case "$1" in
        ubuntu) printf '%s\n' /usr/sbin/nginx ;;
        aapanel) printf '%s\n' /www/server/nginx/sbin/nginx ;;
        *) die "Nginx layout must be ubuntu or aapanel." ;;
    esac
}

validate_nginx_mode() {
    [[ $1 == manage || $1 == monitor ]] || die "Nginx mode must be manage or monitor."
}

render_sudoers() {
    local binary=$1 mode=$2
    shift 2
    local unit action lines
    cat -- "$manager_deploy_dir/myservermanager.sudoers"
    for unit in "$@"; do
        for action in start stop restart; do
            printf '%s ALL=(root) TIMEOUT=30s NOPASSWD: NOSETENV: /usr/bin/systemctl %s --job-mode\\=fail -- %s\n' "$manager_user" "$action" "$unit"
        done
        for lines in 100 250 500; do
            printf '%s ALL=(root) TIMEOUT=8s NOPASSWD: NOSETENV: /usr/bin/journalctl --no-pager --quiet %s %s --unit\\=%s --lines\\=%s\n' \
                "$manager_user" '--output\=json' '--output-fields\=MESSAGE\,PRIORITY' "$unit" "$lines"
        done
    done
    if [[ $mode == manage ]]; then
        printf '%s ALL=(root) TIMEOUT=10s NOPASSWD: NOSETENV: %s -t\n' "$manager_user" "$binary"
        printf '%s ALL=(root) TIMEOUT=30s NOPASSWD: NOSETENV: /usr/bin/systemctl reload --job-mode\\=fail -- nginx.service\n' "$manager_user"
        printf '%s ALL=(root) TIMEOUT=30s NOPASSWD: NOSETENV: /usr/bin/systemctl restart --job-mode\\=fail -- nginx.service\n' "$manager_user"
    fi
}

has_sudo_rule() {
    sudo -n -l -U "$manager_user" -- "$@" >/dev/null 2>&1
}

verify_sudo_rules() {
    local binary=$1 mode=$2
    shift 2
    local unit action lines
    if has_sudo_rule /usr/bin/true ||
        has_sudo_rule /usr/bin/systemctl restart --job-mode=fail -- "$manager_unit" ||
        has_sudo_rule /usr/bin/systemctl restart --job-mode=fail -- mssql-server.service; then
        printf '%s\n' "The dedicated user has permissions beyond the manager policy." >&2
        return 1
    fi
    for unit in "$@"; do
        for action in start stop restart; do
            has_sudo_rule /usr/bin/systemctl "$action" --job-mode=fail -- "$unit" || return 1
        done
        for lines in 100 250 500; do
            has_sudo_rule /usr/bin/journalctl --no-pager --quiet --output=json --output-fields=MESSAGE,PRIORITY \
                "--unit=$unit" "--lines=$lines" || return 1
        done
        if has_sudo_rule /usr/bin/journalctl --unit="$unit" --lines=all; then
            return 1
        fi
    done
    if [[ $mode == manage ]]; then
        has_sudo_rule "$binary" -t &&
            has_sudo_rule /usr/bin/systemctl reload --job-mode=fail -- nginx.service &&
            has_sudo_rule /usr/bin/systemctl restart --job-mode=fail -- nginx.service || return 1
    elif has_sudo_rule "$binary" -t || has_sudo_rule /usr/bin/systemctl reload --job-mode=fail -- nginx.service ||
        has_sudo_rule /usr/bin/systemctl restart --job-mode=fail -- nginx.service; then
        return 1
    fi
}

install_policy() {
    local binary=$1 mode=$2
    shift 2
    local sudo_stage approved_stage previous=''
    visudo -c
    sudo_stage=$(mktemp /etc/sudoers.d/.myservermanager-policy.XXXXXXXX)
    approved_stage=$(mktemp "$manager_config/.approved-services.XXXXXXXX")
    render_sudoers "$binary" "$mode" "$@" >"$sudo_stage"
    {
        printf '%s\n' '# Managed by deploy/approve-services.sh.'
        if (( $# > 0 )); then printf '%s\n' "$@"; fi
    } >"$approved_stage"
    chown root:root -- "$sudo_stage" "$approved_stage"
    chmod 0440 -- "$sudo_stage"
    chmod 0644 -- "$approved_stage"
    visudo -c -f "$sudo_stage"
    if [[ -e $manager_sudoers ]]; then
        previous=$(mktemp /etc/sudoers.d/.myservermanager-previous.XXXXXXXX)
        cp --preserve=mode,ownership -- "$manager_sudoers" "$previous"
    fi

    mv -T -- "$sudo_stage" "$manager_sudoers"
    if ! visudo -c || ! verify_sudo_rules "$binary" "$mode" "$@" ||
        ! mv -T -- "$approved_stage" "$manager_config/approved-services"; then
        mv -T -- "$manager_sudoers" "$sudo_stage"
        if [[ -n $previous ]]; then mv -T -- "$previous" "$manager_sudoers"; fi
        visudo -c
        die "Manager sudo policy verification failed. The previous policy was restored."
    fi
    if [[ -n $previous ]]; then rm -- "$previous"; fi
}
