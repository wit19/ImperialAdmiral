#!/usr/bin/env python3
# M26 surgical canon merge (M21/M25 discipline): replace ONLY the expect values
# of the drifted cases. The fixture mixes two hand/pipe eras: machine-piped
# blocks are json.dumps(indent=1) shifted +3, handcrafted M22-era blocks use
# indent 2 / different shifts. merge() therefore PROVES the byte style of the
# old block first (grid over indent x shift) and reuses the detected style for
# the new value. Untouched bytes stay byte-identical; file must reparse.
import json, sys

F = "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2/Assets/Tests/Fixtures/tacai/"


def dump(v, indent, shift):
    s = json.dumps(v, indent=indent, ensure_ascii=False)
    lines = s.split("\n")
    return "\n".join([lines[0]] + [" " * shift + ln for ln in lines[1:]])


def find_style(old_val, old_text, prefix):
    for indent in (1, 2):
        for shift in range(0, 7):
            if prefix + dump(old_val, indent, shift) == old_text:
                return indent, shift
    raise AssertionError("no byte style found for old expect block")


def merge(fname, names):
    path = F + fname
    raw = open(path, encoding="utf-8").read()
    rec = json.load(open(F + fname.replace(".json", "_record.json"), encoding="utf-8"))
    n = 0
    for name in names:
        i = raw.index('"%s"' % name)
        j = raw.index('"expect": ', i)
        k = j + len('"expect": ')
        open_ch = raw[k]
        close_ch = "]" if open_ch == "[" else "}"
        depth = 0
        p = k
        while True:
            if raw[p] == open_ch:
                depth += 1
            elif raw[p] == close_ch:
                depth -= 1
                if depth == 0:
                    break
            p += 1
        old_text = raw[j:p + 1]
        old_val = json.loads(old_text[len('"expect": '):])
        new_val = rec[name]
        assert old_val != new_val, (fname, name, "no drift in record?")
        prefix = old_text[: len('"expect": ')]
        indent, shift = find_style(old_val, old_text, prefix)   # style proof
        raw = raw[:j] + prefix + dump(new_val, indent, shift) + raw[p + 1:]
        n += 1
    open(path, "w", encoding="utf-8").write(raw)
    json.load(open(path, encoding="utf-8"))
    print(fname, "->", n, "expect values merged")


if __name__ == "__main__":
    fname = sys.argv[1] if len(sys.argv) > 1 else "overlord_focus.json"
    doc = json.load(open(F + fname, encoding="utf-8"))
    rec = json.load(open(F + fname.replace(".json", "_record.json"), encoding="utf-8"))
    diff = [c["name"] for c in doc["cases"]
            if c["name"] in rec and c.get("expect") != rec[c["name"]]]
    print("drifted:", sorted(diff))
    if "--apply" not in sys.argv:
        sys.exit(0)
    merge(fname, diff)
