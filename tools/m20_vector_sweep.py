#!/usr/bin/env python3
# M20 sweep (superseded). Since M21 the canonical second implementation is
# tools/m21_fenn_effects_verify.py: it re-derives EVERY card-basis threat
# value of overlord_focus.json and imperial_card_effects.json under the
# CURRENT law (M19 band average, M20 hold weighting, M21 blast flat,
# M22 supporter pins, M24 healer role, M25 printed accuracy / extra-attack
# prints). Keeping a second stale model of the SAME rows here would only
# invite port-vs-port drift, so this file is now a thin forwarder.
# (M25 note: the M19 module was renamed after M20 and this import silently
# died - the tool never ran again until the M25 QA battery found the corpse.
# Forwarding restores the QA battery entry with one source of truth.)
import os, subprocess, sys

def main():
    here = os.path.dirname(os.path.abspath(__file__))
    return subprocess.call([sys.executable,
                            os.path.join(here, 'm21_fenn_effects_verify.py')])

if __name__ == '__main__':
    sys.exit(main())
