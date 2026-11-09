# Sourced by the benchmark scripts. wait_idle blocks until the machine is idle enough to measure:
#  - IDLE_BASIS=load (default): the 1-minute load average is below IDLE_LOAD (default 2);
#    IDLE_BASIS=cpu (quick profile): fewer than IDLE_LOAD cores' worth of CPU were busy over the last IDLE_SAMPLE
#    seconds (/proc/stat), so the load average left behind by the previous run does not force a pause;
#  - no `dotnet build|test|publish` is running;
#  - no VBCSCompiler, MSBuild node or node process uses more than IDLE_CPU percent of a core (default 10) over an
#    IDLE_SAMPLE-second sample (default 1; an idle compiler server or editor is fine, a busy one is not).
# It polls every IDLE_POLL seconds (default 15) and says why it waits. IDLE_TIMEOUT > 0 gives up after that many
# seconds and exits with an error instead of waiting longer. IDLE_CHECK=0 skips the wait (smoke tests only).
# load_now prints the three load averages; scripts record them at the start and the end of every run.

IDLE_LOAD=${IDLE_LOAD:-2}
IDLE_CPU=${IDLE_CPU:-10}
IDLE_POLL=${IDLE_POLL:-15}
IDLE_CHECK=${IDLE_CHECK:-1}
IDLE_BASIS=${IDLE_BASIS:-load}
IDLE_SAMPLE=${IDLE_SAMPLE:-1}
IDLE_TIMEOUT=${IDLE_TIMEOUT:-0}

load_now() { cut -d' ' -f1-3 /proc/loadavg; }

cpu_ticks() { sed 's/.*) //' "/proc/$1/stat" 2>/dev/null | awk '{print $12 + $13}'; }

system_ticks() { awk '/^cpu /{print $2 + $3 + $4 + $7 + $8 + $9, $5 + $6; exit}' /proc/stat; }

idle_reasons() {
  if [[ $IDLE_BASIS == load ]]; then
    local load; load=$(cut -d' ' -f1 /proc/loadavg)
    awk -v l="$load" -v m="$IDLE_LOAD" 'BEGIN { exit !(l >= m) }' && echo "load $load >= $IDLE_LOAD"
  fi
  local p
  for p in $(pgrep -f '(^|/)dotnet(\.exe)? (build|test|publish)( |$)'); do
    echo "dotnet $(tr '\0' ' ' < /proc/$p/cmdline 2>/dev/null | cut -d' ' -f2) (pid $p)"
  done
  local pids; pids=$(pgrep -f 'VBCSCompiler|MSBuild\.dll|(^|/)node( |$)')
  [[ -z $pids && $IDLE_BASIS == load ]] && return 0
  declare -A before
  for p in $pids; do before[$p]=$(cpu_ticks "$p"); done
  local sys0; sys0=$(system_ticks)
  sleep "$IDLE_SAMPLE"
  local sys1; sys1=$(system_ticks)
  if [[ $IDLE_BASIS == cpu ]]; then
    read -r busy0 idle0 <<< "$sys0"; read -r busy1 idle1 <<< "$sys1"
    local cores; cores=$(awk -v b=$((busy1 - busy0)) -v i=$((idle1 - idle0)) -v n="$(nproc)" 'BEGIN { printf "%.1f", (b + i) ? b / (b + i) * n : 0 }')
    awk -v c="$cores" -v m="$IDLE_LOAD" 'BEGIN { exit !(c >= m) }' && echo "$cores busy cores >= $IDLE_LOAD"
  fi
  local hz; hz=$(getconf CLK_TCK)
  for p in $pids; do
    local after; after=$(cpu_ticks "$p")
    [[ -n ${before[$p]} && -n $after ]] || continue
    local pct; pct=$(awk -v t=$((after - before[$p])) -v hz="$hz" -v s="$IDLE_SAMPLE" 'BEGIN { printf "%d", t * 100 / hz / s }')
    ((pct > IDLE_CPU)) && echo "busy $(tr '\0' ' ' < /proc/$p/cmdline 2>/dev/null | cut -c1-80) (pid $p, $pct% CPU)"
  done
  return 0
}

wait_idle() {
  [[ $IDLE_CHECK == 0 ]] && return 0
  local reasons start=$SECONDS
  while reasons=$(idle_reasons) && [[ -n $reasons ]]; do
    if (( IDLE_TIMEOUT > 0 && SECONDS - start >= IDLE_TIMEOUT )); then
      echo "error: the machine is still not idle after ${IDLE_TIMEOUT} s, aborting: $(echo "$reasons" | paste -sd ';' -)" >&2
      echo "       stop the builds, tests or other load above and run again (IDLE_CHECK=0 only for smoke tests)." >&2
      exit 3
    fi
    echo "   waiting for an idle machine: $(echo "$reasons" | paste -sd ';' -)" >&2
    sleep "$IDLE_POLL"
  done
}
