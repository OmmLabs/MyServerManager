namespace MyServerManager.Security;

internal static class DotNetDiscoveryCommand
{
    public static CommandRequest Create() => new("/bin/bash", TimeSpan.FromSeconds(25), 256 * 1024, "-c", """
        loaded=$(/usr/bin/systemctl list-units --type=service --all --plain --no-legend --no-pager) || exit 1
        installed=$(/usr/bin/systemctl list-unit-files --type=service --no-legend --no-pager 2>/dev/null) || installed=
        mapfile -t units < <(printf '%s\n' "$loaded" "$installed" |
            /usr/bin/awk 'length($1) <= 128 && $1 ~ /^[A-Za-z0-9_][A-Za-z0-9_.-]*(@[A-Za-z0-9_.-]+)?[.]service$/ { print $1 }' |
            /usr/bin/sort -u | /usr/bin/head -n 501)

        limited=0
        if (( ${#units[@]} > 500 )); then
            limited=1
            units=("${units[@]:0:500}")
        fi
        printf '%s\0' "$limited"

        declare -A ports=()
        while read -r pid port; do
            previous=${ports[$pid]-}
            if [[ -z $previous || $previous == "$port" ]]; then ports[$pid]=$port; else ports[$pid]=multiple; fi
        done < <(/usr/bin/ss -H -ltnp 2>/dev/null | /usr/bin/awk '
            match($4, /:[0-9]+$/) {
                port = substr($4, RSTART + 1, RLENGTH - 1)
                rest = $0
                while (match(rest, /pid=[0-9]+/)) {
                    print substr(rest, RSTART + 4, RLENGTH - 4), port
                    rest = substr(rest, RSTART + RLENGTH)
                }
            }')

        emit_candidate() {
            [[ $load == loaded && -n $id && -n $exec_start ]] || return 0
            local path_pattern='[[:space:]]path=([^[:space:];]+)'
            local dll_pattern='[[:space:]]([^[:space:];]+[.]dll)([[:space:];]|$)'
            [[ $exec_start =~ $path_pattern ]] || return 0
            local executable=${BASH_REMATCH[1]}
            local assembly= directory=$workdir argument= port=

            if [[ ${executable##*/} == dotnet ]]; then
                local invocation=${exec_start#*argv\[\]=}
                invocation=${invocation%% ;*}
                if [[ $invocation =~ $dll_pattern ]]; then
                    argument=${BASH_REMATCH[1]}
                    assembly=${argument##*/}
                fi
            elif [[ $executable == /* && -f $executable.runtimeconfig.json ]]; then
                argument=$executable
            else
                return 0
            fi

            if [[ ( -z $directory || $directory == / ) && $argument == /*/* ]]; then
                directory=${argument%/*}
            fi
            if [[ $pid =~ ^[1-9][0-9]*$ ]]; then port=${ports[$pid]-}; fi
            printf '%s\0%s\0%s\0%s\0%s\0' "$id" "$active" "$directory" "$assembly" "$port"
        }

        # Keep full command lines on Ubuntu. Return only service metadata.
        for (( offset=0; offset<${#units[@]}; offset+=64 )); do
            /usr/bin/systemctl show --no-pager --all \
                --property=Id,LoadState,ActiveState,MainPID,WorkingDirectory,ExecStart -- "${units[@]:offset:64}" 2>/dev/null
            printf '\n\n'
        done | while IFS= read -r line; do
            case "$line" in
                Id=*) id=${line#Id=} ;;
                LoadState=*) load=${line#LoadState=} ;;
                ActiveState=*) active=${line#ActiveState=} ;;
                MainPID=*) pid=${line#MainPID=} ;;
                WorkingDirectory=*) workdir=${line#WorkingDirectory=} ;;
                ExecStart=*) exec_start=${line#ExecStart=} ;;
                '') emit_candidate; id= load= active= pid= workdir= exec_start= ;;
            esac
        done
        """);
}
