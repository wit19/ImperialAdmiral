#!/usr/bin/env bash
# R-B mutation probes: each mutation must turn the harness RED in exactly its
# pin family; reverted by REVERSE sed (the file carries uncommitted work).
set -u
cd /home/open/work/ia-ai/ImperialCommander2
VEC=ImperialCommander2/Assets/Scripts/TacticalAI/Core/WorldEventVector.cs
FIX=ImperialCommander2/Assets/Tests/Fixtures/tacai

probe() { # $1=name $2=old $3=new
  local name="$1" old="$2" new="$3"
  grep -F -q -- "$old" "$VEC" || { echo "$name: ANCHOR NOT FOUND"; exit 1; }
  python3 - "$VEC" "$old" "$new" <<'EOF'
import sys
p, old, new = sys.argv[1], sys.argv[2], sys.argv[3]
t = open(p, encoding='utf-8').read()
assert t.count(old) == 1, "anchor not unique"
open(p, 'w', encoding='utf-8').write(t.replace(old, new))
EOF
  dotnet run --project tools/TacticalAiHarness -- "$FIX" > /home/open/.hermes/cache/scratch/rb_probe_$name.log 2>&1
  rc=$?
  fails=$(grep -oE "fails=[0-9]+" /home/open/.hermes/cache/scratch/rb_probe_$name.log | head -1)
  fam=$(grep -oE "^\s+- [a-z0-9_.]+" /home/open/.hermes/cache/scratch/rb_probe_$name.log | sed 's/^ *- //' | sed 's/\..*//' | sort | uniq -c | tr '\n' ' ')
  echo "PROBE $name: rc=$rc $fails | failing cases: $fam"
  python3 - "$VEC" "$old" "$new" <<'EOF'
import sys
p, old, new = sys.argv[1], sys.argv[2], sys.argv[3]
t = open(p, encoding='utf-8').read()
assert t.count(new) == 1, "mutation not unique on restore"
open(p, 'w', encoding='utf-8').write(t.replace(new, old))
EOF
}

probe hypothesis 'anchor.LastSeenZone = "at:" + g.GroupId;' 'anchor.LastSeenZone = "at_none";'
probe objfact      'actor.LastSeenZone = "obj:" + v.Objective;' 'actor.LastSeenZone = "ob:" + v.Objective;'
probe doorfact     'near.LastSeenZone = "door:" + e.Str("door");' 'near.LastSeenZone = "dor:" + e.Str("door");'
probe inwindow     'g.SpawnedInHeroWindow = true;' 'g.SpawnedInHeroWindow = false;'

dotnet run --project tools/TacticalAiHarness -- "$FIX" 2>&1 | tail -1
echo "restored git diff sha check:"
git diff -- "$VEC" | sha256sum | cut -c1-16
