namespace MyServerManager.Security;

internal static class RemoteMetricCommands
{
    public static CommandRequest Cpu() => Shell("sh", """
        for file in /proc/stat /proc/loadavg /proc/uptime; do
            /usr/bin/cat -- "$file" 2>/dev/null
            printf '\0'
        done
        /usr/bin/env -u TZ /usr/bin/date -Iseconds
        """, 256 * 1024);

    public static CommandRequest Memory() => new("/usr/bin/cat", TimeSpan.FromSeconds(5), 16384, "--", "/proc/meminfo");

    public static CommandRequest Identity() => Shell("sh", """
        /usr/bin/hostname
        printf '\0'
        /usr/bin/sed -n 's/^PRETTY_NAME=//p' /etc/os-release
        printf '\0'
        /usr/bin/uname -r
        printf '\0'
        if command -v dotnet >/dev/null; then dotnet --list-runtimes | /usr/bin/awk '{ print $1, $2 }'; fi
        """, 16384);

    public static CommandRequest Cgroups(string[] groups)
    {
        if (groups.Length > 100 || !groups.All(IsCgroupPath))
            throw new ArgumentException("Invalid cgroup path list.", nameof(groups));

        return Shell("bash", """
            shopt -s nullglob dotglob
            mapfile -t mounts < <(/usr/bin/awk 'index($0, " - cgroup2 ") { print $4, $5 }' /proc/self/mountinfo)

            decode_mount() {
                decoded=$1
                decoded=${decoded//\\040/ }
                decoded=${decoded//\\011/$'\t'}
                decoded=${decoded//\\012/$'\n'}
                decoded=${decoded//\\134/\\}
            }

            print_scalar() {
                local value
                if IFS= read -r value 2>/dev/null < "$1"; then printf '%s' "$value"; fi
                printf '\0'
            }

            print_usage() {
                local key value
                while read -r key value; do
                    if [[ $key == usage_usec ]]; then printf '%s' "$value"; break; fi
                done 2>/dev/null < "$1"
                printf '\0'
            }

            print_process_count() {
                local -a groups=("$1")
                local -A pids=()
                local i path pid child
                for (( i=0; i<${#groups[@]}; i++ )); do
                    path=${groups[i]%/}
                    while IFS= read -r pid; do
                        [[ $pid =~ ^[1-9][0-9]*$ ]] || return
                        pids["$pid"]=1
                        (( ${#pids[@]} <= 20000 )) || return
                    done 2>/dev/null < "$path/cgroup.procs" || return
                    for child in "$path"/*/; do
                        (( ${#groups[@]} < 128 )) || return
                        groups+=("$child")
                    done
                done
                printf '%s' "${#pids[@]}"
            }

            printf '%s\0' "$(/usr/bin/date +%s)"
            for group in "$@"; do
                directory=
                best=-1
                for mount in "${mounts[@]}"; do
                    read -r raw_root raw_point <<< "$mount"
                    decode_mount "$raw_root"
                    root=${decoded%/}
                    if [[ "$group" != "$root" && "$group" != "$root/"* ]] || (( ${#root} <= best )); then continue; fi
                    decode_mount "$raw_point"
                    candidate="${decoded%/}${group#"$root"}"
                    if [[ -d "$candidate" ]]; then directory=$candidate; best=${#root}; fi
                done

                printf '%s\0' "$group"
                if [[ -z "$directory" ]]; then
                    printf '\0\0\0\0\0'
                    continue
                fi
                print_scalar "$directory/memory.current"
                print_scalar "$directory/memory.peak"
                print_scalar "$directory/pids.current"
                print_usage "$directory/cpu.stat"
                print_process_count "$directory"
                printf '\0'
            done
            """, 128 * 1024, groups);
    }

    public static bool IsCgroupPath(string? path) => path is { Length: > 1 and <= 512 } && path[0] == '/' &&
        !path.Any(char.IsControl) && path[1..].Split('/').All(part => part is not ("" or "." or ".."));

    private static CommandRequest Shell(string shell, string script, int outputLimit, params string[] arguments) =>
        new("/bin/" + shell, TimeSpan.FromSeconds(8), outputLimit, ["-c", script, "manager-sample", .. arguments]);
}
