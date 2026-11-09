# CONCEPT: Interior law for Massive figures, live (v1, 2026-11-08)

Status: CONCEPT — nothing in this document is implemented yet. Thomas asked for a
feasibility concept (08.11: "Erstelle mal ein Konzept wie das realisierbar wäre"),
with two binding application demands from him:

1. the law must hold when the AI **steers/rejects movement** for a Massive figure, and
2. it must hold at **buy/deploy** time — the AI must not spend threat on a Massive
   that can never operate **in the area where the rebels are currently acting**.

Every "verified" statement below was measured on this tree (08.11, post-P36) or
against the shipped rules text; every forward decision is marked as such.

---

## 1. The printed law (authoritative, quoted)

Rules Reference Guide, "Interior Spaces" (FFG, SWI01):

> Some spaces are considered interior spaces and are affected by some mission rules
> or keywords such as Massive. Interior spaces form the following tiles: 19A–36A and
> 19B–36B.
> • During a campaign, figures with the Massive keyword **cannot enter interior
> spaces.**
> • Any space that is not an interior space is an exterior space.
> • All spaces on tiles 38A, 39A, 38B and 39B are exterior spaces.

Rules Reference Guide, "Massive":

> During a campaign, Massive figures cannot enter interior spaces.
> Massive figures cannot enter spaces containing other Massive figures.

Two structural facts fall out of this text and they carry the whole design:

* **Interior is tile-property, not geometry.** The RR enumerates tile ids; every
  space on a listed tile is interior, every other space is exterior. There is no
  half-interior tile and no wall/door knowledge needed — this is NOT the
  opening-vs-wall unknowability the 07.11 geometry census hit (`SECTIONS_census.md`).
* **It is public, static map data.** A human Imperial player reads it off the tiles
  on the table; the app reading the same table breaks no fairness invariant.

## 2. State on this tree (verified, not guessed)

| Layer | Fact | Evidence |
|---|---|---|
| Core law | `SpaceLegality.FootprintPlaceLegal/EnterLegal` return `massive_interior_ban` via `board.IsInterior(cell)` | `Core/SpaceLegality.cs:36,81`, `Core/Board.cs:101` |
| Flag producer | `SetFlag("interior")` exists ONLY inside `Board.FromJson` (fixture path). No live adapter ever populates it | grep: zero `SetFlag` callers outside `Core/Board.cs` |
| Live rail | the live planner (SectionSpace → SectionConsumers → OverlordFocus, DeploymentAnchors) never builds a Core `Board` and never calls `SpaceLegality` | grep: zero `SpaceLegality.` callers in `TacticalAIBridge.cs` |
| Conclusion | the ban is **fixture-live, table-dead** — the same sim-theater class as `Core/Belief.cs` (CONCEPT v3 addendum) | |
| WIKI | "printed restrictions enforced: no interiors" therefore currently over-claims for the live rail; must be reworded at hop H4 | `docs/WIKI_tactical_ai.md` legacy-diff table |

## 3. What the shipped data actually offers (verified)

* `Assets/Resources/dimensions.json` → `TileDescriptor{ expansion, id, width,
  height, biomeA, biomeB }`; `BiomeType { Desert, Forest, Halls, Ice, Imperial,
  Interior, Jabba, Plains, Snow, Streets, None, Menu }`
  (`GameCore/Interfaces-Enums.cs:86`). This is live app data — the app itself
  selects map art and ambient sound from it (`MapTile.biomeTypeA/B`).
* A placed tile is a `MapTile` entity in `mapEntityManager`; entities carry
  `mapSectionOwner` (already used by the M14e entry ledger: DP → section).
* Deployment points, start groups, event DPs: all resolve to sections through the
  existing `deployLedger` / `EntityManager.GetEntity(...).mapSectionOwner` path.

So the runtime crosswalk **section → tile → biome** needs zero new player input:
it is the same entity query the entry ledger already performs.

## 4. Design decisions

### D1 — the law is an enumeration + a biome crosswalk, pinned in Core

New Unity-free `Core/InteriorLaw.cs` (mirror of `TrackerBindLaw.cs` shape):

```
enum InteriorStatus { OPEN, BANNED, AMBIGUOUS }
```

A tile id is BANNED iff it is in the RR enumeration (core: 19A–36A, 19B–36B);
38A/39A/38B/39B are pinned OPEN by the RR text itself. For tiles the RR text
predates (expansion ids), the biome label is the crosswalk
(`Interior`→BANNED; `Streets/Desert/Plains/Snow/Ice/Forest`→OPEN;
`Halls/Jabba/Imperial/None`→**open decision, census first** — see D2).
The crosswalk table is data, shipped in the Core file, covered by a harness
section — so a later ruling changes a row, not a code path.

A SECTION's status derives from its owning tile(s): every tile under the section
OPEN → OPEN; any tile BANNED → BANNED (a section the app cannot fully occupy with
a Massive is not a Massive destination); no tile data / ambiguous tile → AMBIGUOUS.

### D2 — AMBIGUOUS policy (Thomas gate, census decides)

Two candidate policies:
* **fail-open + log** (never over-restrict; ambiguous sections usable; the
  forensic line names each one) — keeps Massives playable in expansion campaigns
  if the expansion enumeration is incomplete;
* **fail-closed** (never wrongly-allow; Massive treats ambiguous like interior) —
  strict to the printed ban but can silently neuter Massives in whole campaigns.

The 07.11 census tool pattern (`tools/section_census.py`, fail-closed output) is
repeated: **H1 measures, Thomas rules, then the policy is frozen in code.** The
census will quantify exactly how many sections/DPer per campaign land in
AMBIGUOUS under each candidate row — the decision is cheap once the numbers exist.

### D3 — application points (Thomas' two demands + the two the design forces)

**A. Steering / movement (demand 1).**
* Goal candidates for a Massive group are filtered to OPEN sections; the
  reachability/distance math (DistanceLowerBound over the observed graph) gains a
  Massive variant computed on the OPEN-only subgraph. BANNED sections are holes in
  the graph — never "unknown=permeable": the permeability law is for HERO belief
  (hidden movement); interior status is static public map data, so holes are facts.
* A Massive's target/focus that sits in a BANNED section (hero hides in a room —
  RR-legal for heroes) must never produce "move into the room" orders. Doctrine
  fallback: **seal the approach** — take/hold the nearest OPEN section adjacent to
  the target's section (blocker-law analogy already in the pattern tree), stated
  honestly in the explain line: the printed rule, not a hiding of it.
* Mid-round: hero observation inside a BANNED section while a Massive is
  advancing = a plan-relevant fact INSIDE the round → rides the existing P33-H4b
  same-round REPLAN channel (fourth trigger fact).

**B. Deployment / landing (before the demand 2 economy question).**
`DeploymentAnchors.ChooseScored` runs the tier table over candidate DPs
(`TacticalAIBridge.cs:1935,2159`). The interior law sits **before** the tiers as a
hard filter (a legality, not a preference): for a Massive card, candidate DPs on
BANNED sections are removed; if the candidate set empties, the deployment fails
honest (no spawn — the group simply does not arrive that stage) and the forensic
line says why. Rails to cover (all of them, one seam each): mission-start is
mission-authored and untouchable (mission wins — warn only, D4), attack/reinforce
DP choice, landing d1/d2, onslaught loop, M13c optional-deploy window.
`[dp-massive]` forensics row: `card,dp,section,status` per candidate pass.

**C. Buy-time usefulness (demand 2 — the economy question).**
A Massive bought into a fight it can never reach is wasted threat — worse than
wasted: it is the AI spending the Empire's printed budget on an illegal plan.
Usefulness gate at reinforcement/purchase decision time (attack-point economics +
persona `SwapDeployCandidate`/`CostFor`, all already mission-frozen seams):

```
Massive USEFUL  <=>  exists a non-BANNED DP  AND
                     the operation zone is reachable from an OPEN DP over the
                     OPEN subgraph
operation zone   =  contested/believed sections (ThreatZone, M14f) UNION
                    mission-critical objective sections (MissionCritical data,
                    authored) UNION (fallback, round 1 / no belief): the objective
                    neighbourhood
```

Not useful → the Massive is skipped in the buy ranking and the strongest USEFUL
alternative at the same threat cost is taken instead (the P33-H4
StrongestAlternative/DecideStage machinery already scores exactly this way — the
gate just removes dead candidates before scoring). Unknown operation zone AND
unknown reachability → treat as useful, log (fail-open at the economy layer:
buying nothing at all is also a rules-visible decision the Empire would not make).

**D. Two Massives one space.**
RR defines it at "space" granularity; the app plans at section granularity and
cannot compute open-space regions — same honest limit as D1's geometry lesson.
Live coverage is therefore: (a) deployment diversity already keeps two figures off
one anchor section, (b) the popup INFO block carries the printed reminder line.
Full table enforcement stays human, and the docs must say so (it already says
"every proposed move is legality-checked first" — true in sim, see H4).

### D4 — what the app must NOT do

* Never refuse a mission-authored initial Massive (mission rules override; Golden
  Rules). It may log + show a passive warning line when such a group's belief
  neighbourhood is BANNED-only — the mission author owns that contradiction.
* Never touch the legacy rail: every consumer sits behind `TacticalMissionActive()`
  (standing law), legacy bit-identical, Null-Law: no biome data for a mission →
  bit-identical to today's canon (pinned by fixture like `nahe_plan.null_eq_*`).

### D5 — persistence

No new ledger. The section-status map is derived at mission start from
mission/static data (deterministic replay, like the SectionGraph); saves keep
working because everything recomputes from `dimensions.json` + entity state.
Forensic lines carry the derived statuses, so a save can be audited after the fact.

## 5. Stages (each hop = full QA trio + its own commit/tag)

* **H1 — census (decision input, zero behaviour change).**
  `tools/massive_interior_census.py`, fail-closed like the geometry census:
  over all shipped missions: Massive-bearing groups (keyword field in
  `wiki_data/imperial_units.json` cross-checked against live `card.IsMassive`
  source), each DP's section status under candidate crosswalks, AMBIGUOUS share
  per expansion, per-mission "Massive has zero usable DP" count. Output
  `docs/MASSIVE_interior_census.md` (EN, generated). **Thomas gate: D2 policy +
  Halls/Jabba/Imperial rows.**
* **H2 — Core law + fixtures.** `InteriorLaw.cs`, harness section
  `massive_interior.json`: status derivation, OPEN-subgraph distance law, Null-Law
  bit-identity, seal-the-approach adjacency law. No Unity calls.
* **H3 — live plumbing + deployment rails.** section→tile→status map in the bridge
  (ActiveNow seam), hard DP filter on all five rails (B), `[dp-massive]` forensics,
  DeploymentFilterGate extension (or new MassiveInteriorGate) pinning the rails.
* **H4 — steering + player words.** Massive subgraph goals + seal-the-approach
  doctrine branch (A), REPLAN trigger (A), explain/info keys ×11 languages
  (`ordExplainMassiveSeal`, `ordInfoMassiveInterior`; UiCompletenessGate), WIKI +
  MECHANICS sections, and the legacy-diff table row corrected to what the live
  rail actually enforces at that point.
* **H5 — economy usefulness (demand 2).** buy/swap ranking gate (C) + Strongest-
  Alternative rerouting + gate fixtures. Deliberately last: it needs the playtest
  data H3/H4's forensic lines produce.

## 6. Fairness invariants (each one gets a test)

1. Interior status is static public map data — no new input from players, no
   hidden information, no dice.
2. Legacy bit-identical (seam law).
3. Null-Law: no biome/tile data → today's canon exactly.
4. Mission-authored content outranks the app's opinion (warn, never veto).
5. Every rejection of a DP/goal/buy is forensic-loggable and human-readable
   (status + section + rule), like every other law on this rail.

## 7. Known unknowns (honest list — census shrinks this)

* Expansion interior enumerations beyond the core RR text (H1 measures; the biome
  crosswalk may be enough or may need the expansion rulebooks — open).
* `biomeA/biomeB` semantics (per-variant of a double-sided tile vs per-half) —
  H1 reads `dimensions.json` + placed tile ids and pins what the A/B fields mean;
  the RR enumeration is the identity check.
* Whether `Halls`/`Jabba`/`Imperial` biomes are interiors in their campaigns —
  Thomas rules after H1.
* Section-level coarseness: a section that spans a full interior tile is BANNED
  wholesale; the RR "space" granularity is finer than our planning layer. The
  consequence is conservative at deployment (a Massive skips a whole room section
  — correct) and conservative at movement goals (never sends it inside). The
  inverse error (declaring an interior section OPEN) cannot occur for enumerated
  tiles because the whole tile is interior by RR text.

## 8. Playtest validation (definition of done)

Next playtests' forensic lines carry the evidence: `[dp-massive]` rows on every
reinforcement decision with non-empty status; `SD-ZONES`/explain lines showing a
Massive sealing instead of chasing when a hero ducks indoors; zero `massive into
BANNED` orders across all missions of the session. That, not a green gate alone,
closes the chain.
