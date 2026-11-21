#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/common.sh"

(( $# >= 6 )) || die "Usage: install.sh PUBLISH_DIR REVIEWED_REGISTRY HOSTNAME RELEASE ubuntu|aapanel manage|monitor [UNIT ...]"
lock_deployment
publish=$(realpath -e -- "$1")
reviewed_registry=$(realpath -e -- "$2")
hostname=$3
release=$4
layout=$5
nginx_mode=$6
shift 6
validate_application_units "$@"
validate_nginx_mode "$nginx_mode"
binary=$(nginx_binary "$layout")

[[ ${#hostname} -le 253 && $hostname =~ ^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$ ]] ||
    die "Use a lowercase DNS hostname, such as servermanager.example.com."
IFS=. read -ra labels <<<"$hostname"
for label in "${labels[@]}"; do
    (( ${#label} <= 63 )) || die "Hostname labels must be at most 63 characters."
done
[[ $release =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$ ]] || die "Use a simple release identifier."
[[ -t 0 ]] || die "Run interactively to create the initial administrator."
[[ -f $publish/MyServerManager.dll && -f $publish/MyServerManager.runtimeconfig.json &&
    -d $publish/wwwroot && -f $reviewed_registry ]] || die "Provide a complete publish directory and a reviewed registry JSON file."
[[ -z $(find "$publish" ! -type f ! -type d -print -quit) ]] ||
    die "Publish output must contain only regular files and directories."

for tool in /usr/bin/dotnet /usr/bin/systemctl /usr/bin/journalctl /usr/bin/sudo /usr/bin/ss /usr/bin/curl /usr/sbin/visudo \
    /usr/sbin/runuser /usr/sbin/useradd "$binary"; do
    [[ -x $tool ]] || die "Required executable is unavailable: $tool"
done
[[ -d /run/systemd/system && -d /etc/sudoers.d ]] || die "An Ubuntu systemd and sudo installation is required."
runtimes=$(/usr/bin/dotnet --list-runtimes)
grep -q '^Microsoft.NETCore.App 10\.' <<<"$runtimes" &&
    grep -q '^Microsoft.AspNetCore.App 10\.' <<<"$runtimes" || die "Install the .NET 10 ASP.NET Core runtime first."

unit_file=/etc/systemd/system/$manager_unit
for target in "$manager_home" "$manager_state" "$manager_config" "$unit_file" "$manager_sudoers"; do
    [[ ! -e $target && ! -L $target ]] || die "Existing path: $target. Use the release or recovery procedure."
done
if getent passwd "$manager_user" >/dev/null || getent group "$manager_user" >/dev/null; then
    die "The dedicated account or group already exists."
fi
manager_load=$(systemctl show --property=LoadState --value -- "$manager_unit")
[[ $manager_load == not-found ]] || die "The manager unit name already exists."
listener=$(ss -H -ltn 'sport = :5300')
[[ -z $listener ]] || die "TCP port 5300 is already in use."
for unit in "$@"; do verify_loaded_unit "$unit"; done
visudo -c

enable_nginx=false
if [[ $nginx_mode == manage ]]; then
    verify_loaded_unit nginx.service
    [[ $(systemctl show --property=CanReload --value -- nginx.service) == yes ]] ||
        die "nginx.service does not support reload. Use monitor mode."
    main_pid=$(systemctl show --property=MainPID --value -- nginx.service)
    if [[ $main_pid =~ ^[1-9][0-9]*$ ]]; then
        [[ $(readlink -f -- "/proc/$main_pid/exe") == "$(readlink -f -- "$binary")" ]] ||
            die "The tracked Nginx process does not match the selected binary."
    fi
    enable_nginx=true
fi

umask 077
useradd --system --user-group --no-create-home --home-dir "$manager_state" --shell /usr/sbin/nologin "$manager_user"
release_dir=$manager_home/releases/$release
install -d -o root -g root -m 0755 -- "$manager_home" "$manager_home/releases" "$release_dir" "$manager_config"
install -d -o "$manager_user" -g "$manager_user" -m 0700 -- "$manager_state" "$manager_state/config"
cp -R --no-preserve=all -- "$publish/." "$release_dir/"
find "$release_dir" -type d -exec chmod 0755 {} +
find "$release_dir" -type f -exec chmod 0644 {} +
ln -s -- "$release_dir" "$manager_home/current"
install -o "$manager_user" -g "$manager_user" -m 0600 -- "$reviewed_registry" "$manager_state/config/applications.json"

(
    cd -- "$release_dir"
    runuser -u "$manager_user" -- /usr/bin/dotnet MyServerManager.dll --verify-registry "$manager_state/config/applications.json"
    umask 077
    runuser -u "$manager_user" -- env -i PATH="$PATH" TERM="${TERM:-dumb}" LANG=C.UTF-8 DOTNET_ENVIRONMENT=Production \
        ASPNETCORE_ENVIRONMENT=Production Manager__Transport=linux Manager__DataDirectory="$manager_state" \
        Manager__ApprovedServicesPath="$manager_config/approved-services" /usr/bin/dotnet MyServerManager.dll --set-admin
)

cat >"$manager_config/manager.env" <<EOF
Manager__DataDirectory=$manager_state
Manager__ApprovedServicesPath=$manager_config/approved-services
Manager__NginxLayout=$layout
Manager__EnableNginxControls=$enable_nginx
AllowedHosts=$hostname;localhost;127.0.0.1
EOF
chmod 0644 -- "$manager_config/manager.env"
install_policy "$binary" "$nginx_mode" "$@"
install -o root -g root -m 0644 -- "$manager_deploy_dir/myservermanager.service" "$unit_file"
systemctl daemon-reload
systemctl enable --now "$manager_unit"
for attempt in {1..15}; do
    if systemctl is-active --quiet "$manager_unit" &&
        [[ $(curl --noproxy '*' --silent --output /dev/null --write-out '%{http_code}' --max-time 2 \
            --header "Host: $hostname" --header 'X-Forwarded-For: 127.0.0.1' --header 'X-Forwarded-Proto: https' \
            http://127.0.0.1:5300/Account/Login) == 200 ]]; then
        printf '%s\n' "Server App Manager is running on 127.0.0.1:5300. Stage its separate HTTPS vhost using the deployment guide."
        exit 0
    fi
    sleep 1
done
die "The manager unit was installed but its login did not become ready. Inspect journalctl -u myservermanager.service."
