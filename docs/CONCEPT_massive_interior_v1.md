# CONCEPT: Interior law for Massive figures, live (v1.1, 2026-11-08)

Status: CONCEPT — nothing in this document is implemented yet. Thomas asked for a
feasibility concept (08.11: "Erstelle mal ein Konzept wie das realisierbar wäre"),
with two binding application demands from him:

1. the law must hold when the AI **steers/rejects movement** for a Massive figure, and
2. it must hold at **buy/deploy** time — the AI must not spend threat on a Massive
   that can never operate **in the area where the rebels are currently acting**.

v1.1 (same day, Thomas: "lese kritisch gegen; finde eine Logik für Missionen mit
erlaubten Massive-Figuren"): adds **D6 (mission override law)**, corrects D1
(section coarseness — the any-tile-bans-section rule was rejected), rewords the
seal-the-approach and inverse-error claims to what the data can actually back.

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

A SECTION's status derives from its owning tile(s) — **but the section is coarse**
(07.11 section census: median 2, bulk 1-6 sections per mission while a mission
carries ~17 tiles on average), so a section is NOT an interior property carrier.
Three derived states, never two:

* **OPEN** — no BANNED tile under the section: full Massive use.
* **BANNED** — EVERY tile under the section is BANNED (a pure-interior section;
  happens in room-shaped missions): a real hole in the Massive graph.
* **MIXED** — interior tiles and exterior tiles share the section (the common
  case in street-plus-room maps): the Massive graph treats the section as
  traversable (the street half IS legal ground), but the law may not pretend
  room-tiles are fine: the group's orders carry the printed reminder that it
  must stay on exterior spaces **inside** that section (advisory line, D3-A).

Consequence the concept must state plainly (self-correction 08.11): **enforcement
is exact only where the app touches a tile — deployment points** (a DP resolves to
its tile; BANNED tile → hard filter). **Movement lives at section granularity and
can therefore only be exact against pure-interior (BANNED) sections; against MIXED
sections it is advisory** (goal allowed + printed stay-out reminder). An earlier
draft claimed "any BANNED tile bans the section" — that would have banned half the
map over one interior tile; rejected.

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
* Goal candidates for a Massive group are filtered to OPEN **and MIXED** sections
  (D1: only pure-BANNED sections are holes; MIXED is legal ground with a printed
  stay-out reminder riding the order); the reachability/distance math
  (DistanceLowerBound over the observed graph) gains a Massive variant computed
  on the not-BANNED subgraph. BANNED sections are holes in the graph — never
  "unknown=permeable": the permeability law is for HERO belief (hidden movement);
  interior status is static public map data, so holes are facts.
  A registered D6 override on the card disables the filter for that card entirely.
* A Massive's target/focus that sits in a BANNED section (hero hides in a room —
  RR-legal for heroes) must never produce "move into the room" orders. Doctrine
  fallback: **seal the approach** — take/hold the nearest OPEN section adjacent to
  the target's section (blocker-law analogy already in the pattern tree), stated
  honestly in the explain line: the printed rule, not a hiding of it. Honest
  caveat (07.11 geometry census): which edge of the section carries the actual
  door is UNKNOWABLE to the app — "seal" is a soft goal (hold the nearest lawful
  neighbour), never a claim of door-blocking legality.
* Mid-round: hero observation inside a BANNED section while a Massive is
  advancing = a plan-relevant fact INSIDE the round → rides the existing P33-H4b
  same-round REPLAN channel (fourth trigger fact).

**B. Deployment / landing (before the demand 2 economy question).**
`DeploymentAnchors.ChooseScored` runs the tier table over candidate DPs
(`TacticalAIBridge.cs:1935,2159`). The interior law sits **before** the tiers as a
hard filter (a legality, not a preference): **first the D6 override check (a
registered override skips the filter for that card),** then for a Massive card,
candidate DPs whose own tile resolves BANNED are removed — this layer is EXACT,
because a DP sits on a known tile (no section coarseness involved); if the
candidate set empties, the deployment fails
honest (no spawn — the group simply does not arrive that stage) and the forensic
line says why. Rails to cover (all of them, one seam each): mission-start is
mission-authored and untouchable (its indoor placement registers the D6 override), attack/reinforce
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

* Never refuse a mission-authored Massive placement — but an authored indoor
  placement is not a mere warning case: it **registers the mission override**
  (D6). Warning + veto-free behaviour without the override would paralyse the
  mission's own figure (an indoor-starting Massive under an unlifted ban has no
  legal re-entry and, in a pure-interior section, no lawful chase at all — the
  app would fight its own deployment).
* Never touch the legacy rail: every consumer sits behind `TacticalMissionActive()`
  (standing law), legacy bit-identical, Null-Law: no biome data for a mission →
  bit-identical to today's canon (pinned by fixture like `nahe_plan.null_eq_*`).

### D6 — mission-permitted Massives (the "erlaubte Massive" law, Thomas 08.11)

RR Golden Rules: *"Mission rules can override both card abilities and rules from
this guide"* — the interior ban is a guide rule, so a mission may lift it. (The
absoluteness clause covers "cannot" printed on **cards or missions**, not guide
rules; and nothing on a card may lift the ban — card abilities are not an
override source here.)

The design insight: **the override does not need text understanding.** Mission
authors express "here Massives may go indoors" **structurally** — by placing the
Massive indoors themselves. Both placement channels are already structured,
authored and persisted on this tree:

1. **Initial deployment**: the `authored` origin of the M14e `deployLedger`
   (mission file's own pointList, 293/297 start groups).
2. **Event reinforcements**: the authored Specific-event DPs written at deploy
   time (same ledger).

**Override law (deterministic, fail-closed, zero text parsing):**

```
at mission start, per group the mission authors:
  card IsMassive AND its authored DP(s) resolve to BANNED tiles
      => register override (mission, card)  [forensics: massive-override::card<-mission(author-placement)]
for a registered (mission, card):
  the interior ban is INACTIVE for that card's groups in that mission
  (BANNED sections are treated OPEN by goal choice, DP filter and USEFULNESS)
```

Semantics decisions (each one a candidate Thomas gate; default = the conservative
middle):

* **Scope: card × mission.** The narrowest defensible scope is the single group;
  but the mission's statement is about the figure type in its spaces, and keeping
  two behaviours alive for two groups of one card is a law the printed rules do
  not contain. Default: card × mission; other missions keep the ban untouched.
* **Full lift vs. leave-only.** RR only forbids *entering*; a figure already
  indoors could legally leave even without any override. A "leave-only" variant
  (indoor start tolerated, indoor re-entry still banned) is mechanically
  constructible, but it is an invented halfway state no mission text uses —
  an authored indoor placement plus event-respawn indoors (e.g., throne-room
  guards) would break under it. Default: full lift for that (mission, card).
* **Two-Massive rule untouched**: the override is permission for *interior*, not
  a general keyword suspension — `two_massive_same_space` and the push rules stay.
* **Economy interacts cleanly**: a registered override makes the card USEFUL by
  definition in that mission (D3-C gate short-circuits — the mission itself
  proved indoor relevance by placing it there).

**Residual case that stays honest (documented, not silently swallowed):** a
mission that *textually* permits indoor Massive use **without ever placing one
indoors** (e.g., "in this mission the AT-ST may enter the compound"). That
permission lives only in free multilingual text; auto-applying a phrase-scan
risks false-positive rule violations. v1 stance: a conservative audit pattern
(interior-noun AND permission-verb, per language) emits a **log line + census
entry only** — no behaviour change; the table human decides (the orders are
advisory by doctrine: the AI proposes, the human acts, and the popup INFO line
will say the ban holds *unless the mission says otherwise*). H1 census measures
how many shipped missions hit text-permit-without-indoor-placement — if the list
is empty or tiny, this residual can be closed by hand-inspection into the pinned
crosswalk data instead of by any parser.

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
  source), each DP's TILE status under candidate crosswalks, MIXED share per
  expansion, AMBIGUOUS share per expansion, per-mission "Massive has zero usable
  DP" count, **D6 counts: authored-indoor Massive placements (initial + event DPs)
  = override-candidate list, and the text-permit-without-placement residual list**
  (audit pattern hits only, no behaviour). Output
  `docs/MASSIVE_interior_census.md` (EN, generated). **Thomas gate: D2 policy +
  Halls/Jabba/Imperial rows + D6 semantics (scope, full-lift).**
* **H2 — Core law + fixtures.** `InteriorLaw.cs`, harness section
  `massive_interior.json`: OPEN/BANNED/MIXED derivation, not-BANNED-subgraph
  distance law, **D6 override law (register from authored placements; card×mission
  scoping; override survives resync/replay as derived-not-stored)** , Null-Law
  bit-identity, seal-the-approach adjacency law (soft-goal wording pinned). No
  Unity calls.
* **H3 — live plumbing + deployment rails.** section→tile→status map in the bridge
  (ActiveNow seam), override registry built from the deployLedger authored rows,
  hard DP filter on all five rails (B, override-first), `[dp-massive]` +
  `massive-override::` forensics,
  DeploymentFilterGate extension (or new MassiveInteriorGate) pinning the rails.
* **H4 — steering + player words.** Massive subgraph goals + seal-the-approach
  doctrine branch (A), MIXED-section stay-out reminder on the order, REPLAN
  trigger (A), explain/info keys ×11 languages
  (`ordExplainMassiveSeal`, `ordInfoMassiveInterior` — the info text states the
  ban "unless the mission says otherwise"; UiCompletenessGate), WIKI +
  MECHANICS sections, and the legacy-diff table row corrected to what the live
  rail actually enforces at that point.
* **H5 — economy usefulness (demand 2).** buy/swap ranking gate (C) + Strongest-
  Alternative rerouting + **D6 short-circuit (override ⇒ USEFUL by definition)** +
  gate fixtures. Deliberately last: it needs the playtest
  data H3/H4's forensic lines produce.

## 6. Fairness invariants (each one gets a test)

1. Interior status is static public map data — no new input from players, no
   hidden information, no dice.
2. Legacy bit-identical (seam law).
3. Null-Law: no biome/tile data → today's canon exactly.
4. Mission-authored content outranks the app's opinion (register the D6 override
   from authored placements; never veto).
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
* Section-level coarseness (revised by the D1 self-correction): MIXED sections
  stay traversable — so the app CAN place a Massive goal in a section that also
  contains interior tiles. That is a deliberate, disclosed trade: the ORDER names
  the section, the printed stay-out reminder governs the tile-level detail, and
  the human at the board executes (advisory doctrine). What the law guarantees
  exactly is the DP layer (tile-known) and pure-BANNED sections (whole-section
  interior). The earlier draft's claim "the inverse error cannot occur" was wrong
  under the any-tile rule it assumed; it is replaced by this honest split.
* The whole crosswalk stands on the tile-id mapping (`dimensions.json` ids vs the
  RR "19A–36A" numbering vs expansion id spaces). A wrong mapping row is the ONE
  error class that could wrongly-allow interiors even at the DP layer — H1 must
  not just generate the mapping, it must VERIFY it against every RR-enumerated id
  and fail closed (refusing to ship the law) if any enumerated id is unmatched.

## 8. Playtest validation (definition of done)

Next playtests' forensic lines carry the evidence: `[dp-massive]` rows on every
reinforcement decision with non-empty status; `massive-override::` rows for every
mission that authored an indoor Massive (the D6 list must match the H1 census
prediction exactly — an override that appears at runtime but was not in the
census means the census and the live data disagree); `SD-ZONES`/explain lines
showing a Massive sealing instead of chasing when a hero ducks indoors; zero
`massive into BANNED` orders across all missions of the session. That, not a
green gate alone, closes the chain.
