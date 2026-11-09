# CONCEPT: Interior law for Massive figures, live (v1.3, 2026-11-09)

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

v1.2 (same day, Thomas: "gehe nochmal kritisch durch, prüfe Umsetzbarkeit gegen
die aktuelle Code-Struktur, erweitere um die Umsetzung"): **§9 implementation
blueprint** — every seam opened and read on today's tree (file:line anchors);
feasibility verdict recorded (yes: no scene edits, no new player input, one Core
file + one builder + four rider-seams + gate + census); §7's biomeA/B unknown
resolved at code level (`MapTile.GetBiomeType()`/`textureName` pin the A/B = RR
tile-sides reading).

v1.3 (09.11, Thomas: "korrigiere die 4 echten Funde; prüfe: DP draußen, aber
Missionsfortschritt nur noch drin — dürfte kein AT-ST/AT-DP kaufen/deployen; erst
DP wählen, dann gucken was deploybar ist"): review against release HEAD `13c022b`
(v1.0.1). (1) all drifted file:line anchors re-pinned + anchor law (§9 intro);
(2) D3-B card-availability rail census recorded (the `card == null` callers are
existence probes, never card deploys — the filter is never card-blind on a real
deploy); (3) D1/§9.1 enums unified: tile-level vs section-level, one derivation;
(4) D6 override registration fail-closed on VERIFIED-BANNED rows only (an
override lifts the ban everywhere — it must never ride an unverified mapping
row); (5) D3-C rewritten **DP-first** (new D3-C2): outdoor DP + indoor-only
mission progress must buy nothing and must not deploy an already-held Massive to
idleness — the USEFUL-DP set is computed first, buy asks it non-empty, the DP
scorer consumes the SAME set.

Every "verified" statement below was measured on this tree (08.11, post-P36; v1.3
items re-measured 09.11 on release HEAD 13c022b) or against the shipped rules
text; every forward decision is marked as such.

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
tile level (what the table holds):     section level (what D2-D6 consume):
enum TileStatus     { Open, Banned,    enum SectionStatus { Open, Banned,
                      Ambiguous }                             Mixed, Ambiguous }
```
(Unified v1.3, finding 3: v1.2 ran ONE name `InteriorStatus` over TWO different
state sets — D1's tile enum had no MIXED, §9.1's had no tile-level set and
silently moved MIXED/Ambiguous between levels. Two enums, one derivation law.)

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
Four derived states, never two:

* **OPEN** — no BANNED tile under the section: full Massive use.
* **BANNED** — EVERY tile under the section is BANNED (a pure-interior section;
  happens in room-shaped missions): a real hole in the Massive graph.
* **MIXED** — interior tiles and exterior tiles share the section (the common
  case in street-plus-room maps): the Massive graph treats the section as
  traversable (the street half IS legal ground), but the law may not pretend
  room-tiles are fine: the group's orders carry the printed reminder that it
  must stay on exterior spaces **inside** that section (advisory line, D3-A).
* **AMBIGUOUS** — at least one tile under the section is unresolved (or no tile
  under it resolves at all): D2 policy decides its reading; never silently OPEN
  (v1.3: this state existed in the tile enum but had no section-level consumer
  law — it now has one).

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

**Card-availability rail census (v1.3, finding 2 — verified on 13c022b).** The
filter keys on the card, so every claimed rail must actually hand one over. Live
truth: EVERY real deploy carries the card into the seam — `DeploymentManager.cs`
`:411` (ovrd.Active), `:482` (ovrd==null: round popups, event reinforcements,
landing), `:534` (`DoMultipleDeployment`) all call
`GetActiveDeploymentPoint(enemyToAdd)`, and the popup's defensive re-roll passes
the card too (`SagaDeploymentPopup.cs:878`). The `card == null` callers
(`SagaController.cs:610`, `MissionEventHandler.cs:730,803`) are the M13c
**existence probes** for the optional-deploy popup ("is there ANY active DP? →
open the Landing popup / delay the deployment") — they pick no card's DP, and
`TryChooseDeploymentPoint` returns false on null (`bridge:1892`, audit FINAL-QA
law: a fixed point there would cluster every reinforcement wave). So the filter
is never card-blind on a real deploy; the probe/announce lines stay card-free BY
DESIGN (M13f "One DP truth": they name a DP that was really used, never choose
one). The authored-property `FilterDP` (`MapEntityManager.cs:480,518`, def
`:536`) sits UPSTREAM of the bridge and gates even the legacy random line —
deliberately NOT extended: touching it would break the legacy bit-identity law
(D4).

**C. Buy-time usefulness (demand 2 — the economy question).**
A Massive bought into a fight it can never reach is wasted threat — worse than
wasted: it is the AI spending the Empire's printed budget on an illegal plan.
Usefulness gate at reinforcement/purchase decision time (attack-point economics +
persona `SwapDeployCandidate`/`CostFor`, all already mission-frozen seams):

```
USEFUL-DP(card) = { DP | tile(DP) not BANNED for this card (D3-B filter law)
                       AND component(DP over the not-BANNED subgraph) satisfies
                       the op-zone predicate (D3-C2) }
Massive USEFUL  <=>  USEFUL-DP(card) is not empty
operation zone (FUTURE, v1.3) = contested/believed sections (ThreatZone, M14f)
                    UNION mission-critical objective sections (MissionCritical
                    data, authored — the mission's ENDGAME truth, not today's
                    belief) UNION (fallback, round 1 / no belief): the objective
                    neighbourhood
```
(v1.3 rewrite, finding 5: v1.2 quantified EXISTENTIALLY over "an OPEN DP" and read
TODAY's op zone — both holes are closed by the component-anchored set + the
FUTURE op zone; the full inversion is D3-C2.)

Not useful → the Massive is skipped in the buy ranking and the strongest USEFUL
alternative at the same threat cost is taken instead (the P33-H4
StrongestAlternative/DecideStage machinery already scores exactly this way — the
gate just removes dead candidates before scoring). Unknown operation zone AND
unknown reachability → treat as useful, log (fail-open at the economy layer:
buying nothing at all is also a rules-visible decision the Empire would not make).

**C2. DP first, then load (v1.3 — Thomas 09.11).** Demand in Thomas' words:
*erst den Deploypunkt auswählen und dann gucken, was kann ich da deployen.* The
scenario he named: all DPs outdoor and legal, but the mission's progress from some
point on happens ONLY indoors (heroes duck into rooms, the mission-critical
sections are interior) — an AT-ST/AT-DP then lands legally on an outdoor DP and is
pure dead threat: the DEPLOY POINT is legal, the DEPLOY is pointless. v1.2 had two
holes here: its USEFULNESS quantified over ANY open DP (the scorer could still pick
a DP in another, useless open component), and it read TODAY's op zone, not the
mission's endgame. The inversion closes both by construction:

1. **The USEFUL-DP set is the pre-selection (exact, computed once per
   snapshot + op-zone).** A DP enters the set only if its tile passes the D3-B
   hard filter AND its own component on the not-BANNED subgraph satisfies the
   op-zone predicate. The DP scorer (seam 1) may only score DPs FROM this set
   for Massive cards — a pick can no longer be useless-by-component, because
   only useful DPs are on the table. The buy question is then just
   `USEFUL-DP != {}` against the SAME set: buy-side and deploy-side can never
   disagree about which DPs exist (this IS the DP-first ordering: DP set first,
   card answer second).
2. **Held Massive, mid-mission flip (deploy-side):** interior status is STATIC
   (public map data — nothing re-pins mid-mission); what moves is the OP ZONE
   (hero-ingress facts, objective/mission-critical facts). If a Massive was
   bought while progress was outdoor and the facts later move the op zone
   exclusively into BANNED sections, its USEFUL-DP set empties and the
   reinforcement trigger DEFERS instead of spawning a group to idleness: no
   interior spawn (D3-B unchanged), no outdoor standstill deploy, forensic
   `[dp-massive] defer card=<c> reason=no_useful_dp_after=<fact>`; the trigger
   re-arms on the next replan round. A deferred group is a HELD purchase, not a
   lost one (hold-vs-refund: Thomas gate, see below).
3. **Op zone = FUTURE truth:** MissionCritical objective sections (authored,
   already public feed) carry the endgame into the purchase decision, so the
   "fight moves indoors later" case is priced in at buy time; belief fail-open
   stays for missions without that data (C unchanged).

Open predicate policy (Thomas gate before H5, H1 census informs): is an outdoor
Massive USEFUL when the WHOLE op zone is BANNED but its sections ADJOIN open ones —
the blockade doctrine of D1/D3-A (patrol the room exits)? Strict-reach says no and
would let our own economy gate kill the blockade the concept deliberately
preserves; reach-OR-adjacency says yes. **Proposed default: reach OR
adjacency-blockade** — flagged, not silently frozen.

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
  card IsMassive AND its authored DP(s) resolve to VERIFIED-BANNED tiles
      => register override (mission, card)  [forensics: massive-override::card<-mission(author-placement)]
  VERIFIED-BANNED (v1.3, finding 4): the tile's mapping row is in the PINNED,
  H1-verified table — a crosswalk row that was only machine-classified (or
  missing) resolves Ambiguous and registers NOTHING. An override switches the
  ban off for that card EVERYWHERE in the mission, so it must never ride an
  unverified mapping row: fail-closed means `[interior] override_unverified
  card=<c> dp=<d> tile=<t>` and the ban STAYS until the table is fixed. H1's
  authored-indoor census list makes exactly those tiles mandatory pinned rows,
  and §9.4 asserts runtime-override-set == census set (both directions).
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
  `massive_interior.json`: OPEN/BANNED/MIXED/AMBIGUOUS derivation (four-state,
  v1.3), not-BANNED-subgraph
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
* **H5 — economy usefulness (demand 2).** buy/swap ranking gate (C + C2,
  DP-first) + Strongest-Alternative rerouting + **D6 short-circuit (override ⇒
  USEFUL by definition)** + gate fixtures + **two Thomas gates: the blockade
  predicate (reach vs reach-OR-adjacency) and hold-vs-refund for deferred
  triggers**. Deliberately last: it needs the playtest
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
* ~~`biomeA/biomeB` semantics~~ — **RESOLVED at code level 08.11**:
  `MapTile.GetBiomeType()` picks `biomeA` when `tileSide == "A"` else `biomeB`,
  and `MapTile.textureName = "{expansion}_{tileID}{tileSide}"` — the A/B fields of
  `dimensions.json` ARE the RR tile sides (19A vs 19B). The crosswalk key is
  `(expansion, tileID, tileSide)`; the biome is the per-side label the app itself
  already uses for art and sound. H1's job shrinks to verifying the id range and
  classifying expansion biomes.
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

**Named scenario (Thomas 09.11, D3-C2):** a mission whose DPs are all outdoor and
whose progress (hero ingress + MissionCritical sections) is indoor from some point
on: the app buys NO AT-ST/AT-DP once the op zone is indoor-only
(`usefulness=skip`); an AT-ST/AT-DP bought EARLIER does not land to idleness — its
trigger defers (`[dp-massive] defer ... reason=no_useful_dp_after`) and re-arms at
the next replan; nothing ever spawns indoors. Pinned as a harness vector derived
from a real census mission (unless the blockade predicate says the outdoor patrol
IS useful there — then the buy is allowed and the explain line must say why).

## 9. Implementation blueprint (feasibility walked against the live code, 08.11)

Every seam below was opened and read on the live tree — the concept ships only on
anchors that exist. **Anchor law (v1.3, finding 1):** refs re-pinned 09.11 against
release HEAD `13c022b` (v1.0.1); line numbers drift with every commit, so the
semantic anchor (function/member name) is the contract and the line is a hint —
re-pin before editing. Known drift since v1.2: `HeroInSection` bridge reads
2818/3284/3328 → **2685 / 3180+3185 / 3694+3696 (+3738+3739)**; ActiveNow guard
1888 → **1887** (`card == null` early-exit at **1892**). Path note: `Core/` in the
rows below is shorthand for `TacticalAI/Core/`; `WorldEventsUnity.cs` lives in
`TacticalAI/`, NOT in `Core/`.

### 9.0 Verified anchors

| Need | Existing seam | Where |
|---|---|---|
| per-card legal DP set (app's own) | `MapEntityManager.GetValidDeploymentPoints(DeploymentCard)` | `Saga/Managers/MapEntityManager.cs:470` |
| DP candidate pass with GUID+section | `TryChooseDeploymentPoint(card, List<IMapEntity> dps, out Guid)` builds `DeployCandidate{Guid, SectionGuid}` rows | `TacticalAI/TacticalAIBridge.cs:1883,1925` |
| active-mode seam (legacy bit-identity) | `ActiveNow()` guards `TryChooseDeploymentPoint` entry | bridge:1887 (guard line; `card == null` early-exit :1892 — re-pinned 09.11) |
| tile identity + placement | `MapTile : IMapEntity { tileID, tileSide, expansion, entityPosition, entityRotation, mapSectionOwner, GetBiomeType() }` | `Saga/MissionModels/MapEntities/MapTile.cs` |
| tile scan pattern (no scene edits) | `Resources.FindObjectsOfTypeAll<T>()` walk (used by the M13r MedPac policy over TooltipHandler) | `Screens/SagaController.cs:1018ff` |
| entity by guid | `MapEntityManager.GetEntity(Guid)` | `:451` |
| Massive recognition (live) | Core card resolver: `IsMassive => Keywords.Contains("Massive")`, keywords injected from PRINTED card data (M13t law); bridge already resolves live→Core cards everywhere (`catCard.Keywords` uses at bridge:1173,4851) | `Core/DeploymentCard.cs:49` |
| belief/focus anchor reads (goal masking point) | `space.HeroInSection(...)` sites: focus sec, axis anchor | bridge:2685,3180/3185,3694/3696,3738/3739 (re-pinned 09.11); `Core/SectionConsumers.cs:188,279,390,676,720,816` (these held) |
| SD zone derivation (belief — NOT the masking point) | `SectionConsumers.PlanZones` (trust-gated) | bridge:653 |
| mid-round REPLAN fact channel | P33-H4b block with `trigger=` forensics line | bridge:726-744 |
| economy seams (persona) | `PersonaState.CostFor` / `SwapDeployCandidate` (both already mode-frozen, reset on mission identity change) | `PersonaState.cs:169,225,330` |
| deployment ledger authored rows (D6 source) | `SagaSession.deployLedger` (`authored` initial pointList + Specific-event DPs), `TryGetDeployEntry` | M14e rail, `TacticalAI/WorldEventsUnity.cs:1671+` (path corrected 09.11: file is in `TacticalAI/`, not `Core/`) |
| popup text rails | `ActivationPlanData.Blocks` + `ordInfo*`/`ordExplain*` keys ×11 langs, UiCompletenessGate auto-inventory | bridge:1128-1155 pattern |

**Feasibility verdict: implementable end to end with zero scene/prefab edits and
zero new player input.** One new Unity-side file, one new Core file, surgical
rider-edits at four existing seams, one gate extension, one census tool.

### 9.1 New Core file — `Core/InteriorLaw.cs` (Unity-free, harness-testable)

```csharp
enum TileStatus     { Open, Banned, Ambiguous }          // tile level  (D1 v1.3)
enum SectionStatusT { Open, Banned, Mixed, Ambiguous }   // section level (D1 v1.3)

static class InteriorLaw {
  // RR enumeration by (expansion, tileID, tileSide) - CORE only:
  //   19..36 (both sides) -> Banned;  38/39 -> Open (RR names them explicitly).
  // Expansion tiles: crosswalk row by per-side BiomeType (data table, Thomas-
  //   gated after H1; missing row -> Ambiguous).
  static TileStatus StatusForTile(string expansion, string tileId,
                                  string side, string biome);
  // section derivation over the tile->section table (D1 four-state law):
  static SectionStatusT SectionStatus(IList<TileStatus> tilesUnderSection);
  // D6 registry: derived, never stored:
  sealed class OverrideSet { bool Covers(string cardName); /* card x mission */ }
  // graph mask helper for the Massive goal/distance view:
  static Func<string,bool> NotBanned(string sectionStatusMap);  // node filter
  // seal target: nearest Open neighbour of a Banned section (BFS, fixed tie law):
  static string SealSection(...);
}
```

The crosswalk is a shipped data table in the file (H1 generates candidate rows,
Thomas gates, the table freezes) — same governance pattern as the persona JSON
law: rules change rows, not code.

### 9.2 New Unity-side file — `TacticalAI/InteriorUnity.cs` (builder)

```
MissionInteriorSnapshot BuildInteriorSnapshot(mapEntityManager):
  tiles = Resources.FindObjectsOfTypeAll<TileRenderer>()  // M13r pattern
        -> each carries its MapTile data (tileID/tileSide/expansion/
           entityPosition/mapSectionOwner) + descriptor w/h
  tileStatus  = InteriorLaw.StatusForTile per placed tile  (rotation irrelevant:
                id+side+biome do not change with rotation - census 07.11)
  sectionStatus: group tiles by mapSectionOwner -> InteriorLaw.SectionStatus
  dpStatus: per DeploymentPoint entity, point-in-rect over tile rects ->
            that tile's status (EXACT layer; rect math mirrors the existing
            space-cell mapping the planner already uses for its own cells)
  overrides: for every deployLedger 'authored' row whose card IsMassive AND
            whose dpStatus is VERIFIED-BANNED (pinned verified rows ONLY;
            Ambiguous -> NO override + `[interior] override_unverified` log;
            D6 v1.3 fail-closed law) -> register (mission, cardName)
  empty index (0 tiles / no DeploymentPoints) -> snapshot EMPTY -> every
            consumer Null-Laws (today's canon exactly, log one line)
```

Built ONCE per mission identity, cached in the bridge, re-built on identity
change (the exact re-entry pattern `SwapDeployCandidate` reset already uses,
`PersonaState.cs:330`). Built after `InstantiateEntities` on BOTH rails
(fresh + restoring) — the P33-H2 replay ordering lesson applies (snapshot must
exist before the first DP choice and before the first order render).

### 9.3 The four rider-seams (surgical, each behind ActiveNow)

1. **DP hard filter** — inside `TryChooseDeploymentPoint`, after the existing
   `usedDps` pass and before candidate rows: drop `dps` whose `dpStatus ==
   Banned` when `card IsMassive` and no D6 override covers the card. Candidate
   rows already carry GUID + section, so no new plumbing; `[dp-massive]` log
   per pass (`card,dp,status,override`). `GetValidDeploymentPoints` itself is
   NOT touched — legacy callers of it stay bit-identical; the filter is the
   tactical bridge's rider decision, consistent with the DP-anchor law already
   living on the same rail. Card availability is verified per rail (D3-B v1.3
   census): every real deploy passes the card (`DeploymentManager.cs:411,482,534`
   + popup re-roll `:878`); the only `card == null` callers are the M13c
   existence probes, which pick no card's DP. For Massive cards the candidate
   rows are additionally restricted to `InteriorLaw.UsefulDps(...)` (D3-C2):
   legality AND usefulness are decided BEFORE the score, never after.
2. **Goal/focus masking + seal** — at the `HeroInSection` anchor reads
   (bridge:2685,3180/3185,3694/3696 — re-pinned 09.11, read sites; function
   name is the contract) and wherever the move-toward zone resolves: if the
   anchor's section is Banned for this card (and no override), substitute
   `InteriorLaw.SealSection(anchor)`; explain line `ordExplainMassiveSeal`
   states the printed rule; MIXED sections need NO substitution — the order
   carries the stay-out reminder instead (`ordInfoMassiveInterior` is the
   always-on info rider naming the ban "unless the mission says otherwise").
   Belief math (PlanZones/ThreatZone/DistanceLowerBound for HERO belief) stays
   UNMASKED — the mask is a Massive-legality view, not a belief change (D3-A).
3. **REPLAN fact 4** — extend the P33-H4b fact set: `HeroInSection` anchor of a
   currently-planned Massive's target flips INTO a Banned section mid-round ->
   `trigger=InteriorIngress`, same-round re-plan through the existing machinery.
4. **Economy USEFULNESS (DP-first, v1.3/D3-C2)** — helper
   `InteriorLaw.UsefulDps(snapshot, card, opZoneFuture)` returns the USEFUL-DP
   set: DPs whose tile is not Banned for the card AND whose own component on
   the not-Banned subgraph satisfies the op-zone predicate (ThreatZone UNION
   MissionCritical — the ENDGAME sections, not today's belief; r1 fallback:
   objective neighbourhood; D6 override short-circuits to all-open). The BUY
   gates (`CostFor` ranking + `SwapDeployCandidate`, both mode-frozen, plus the
   attack-point buy loop) ask only `UsefulDps != {}`; the DP scorer (seam 1)
   consumes the SAME set for Massive cards, so buy-side and deploy-side can
   never disagree about which DPs are on the table. A held Massive whose set
   empties mid-mission DEFERS its trigger
   (`[dp-massive] defer ... reason=no_useful_dp_after=<fact>`), re-arming on
   the next replan — hold-vs-refund is the open Thomas gate (D3-C2).
   Unreachable-but-cheap never outranks reachable-alternative: reuse
   `DeploymentAnchors.StrongestAlternative` for the reroute decision.

### 9.4 Fixtures + gates

* Harness: new `SectionsInterior.cs` reading fixture `massive_interior.json` —
  tile→section derivation table, four-state law (D1 v1.3: incl. MIXED and
  AMBIGUOUS derivation), seal BFS tie
  law, D6 registration from authored rows (incl. multi-event respawn case AND
  the v1.3 fail-closed case: an authored DP resolving AMBIGUOUS registers NO
  override), Null-Law (empty snapshot ⇒ plan bit-identical to canon),
  USEFULNESS truth table incl. the D3-C2 named vector (outdoor DPs +
  indoor-only progress ⇒ no buy; held Massive ⇒ trigger defers, nothing
  spawns). Canonical fixture vectors come OUT of the H1 census (real missions),
  hand-written vectors only for edges.
* Gate: `MassiveInteriorGate.cs` in `tools/TacticalAiGate/` (zero-fixture live
  gate, UiCompletenessGate pattern): reads real `dimensions.json` + one real
  mission file, pins the RR enumeration (every 19..36 core id resolves Banned;
  38/39 resolve Open), and fails the build if the H1 verification table has an
  unmatched RR id (D1 fail-closed law as a gate, not a hope) AND asserts the
  override-set equality in BOTH directions: runtime override set == H1 census
  set == VERIFIED-BANNED authored rows only — a runtime override born from an
  unverified mapping row fails the build (D6 v1.3, finding 4).
* DeploymentFilterGate/OptionalDeployGate: extend with the Massive rows so the
  five rails' filtered DP sets are pinned alongside the existing anchor law.

### 9.5 Migration & revert story

Each stage rides its own dev tag; H3-H5 are pure rider-edits behind `ActiveNow`
— reverting any hop returns the rail to the previous hop bit-exactly (no state
migrated, no save formats touched: the snapshot is derived, D5). The concept
explicitly rejects a persisted interior ledger: mission data + dimensions.json
recompute it, and saves keep working — the P35 build-stamp law lets a playtest
log attribute the snapshot behaviour to a commit.

### 9.6 Honest residual risks (measured, not vibes)

* `GetValidDeploymentPoints` is ALSO the classic deployment UI's set — a Massive
  bought in classic mode and deployed by a human on an interior DP stays the
  human's call (legacy untouched, doctrine: app proposes, table acts).
* The point-in-rect DP→tile map assumes DeploymentPoints sit inside their tile's
  rect — the census (07.11: grid CLEAN, 0 off-grid) supports it, but H1 must
  count DPs landing on NO tile rect; those become Ambiguous DPs (log), never a
  silent OPEN.
* `TileRenderer` scan after `restoring=true`: if any mission path renders tiles
  lazily per section visibility (invisible-until-activated sections exist, census
  max 22), tiles of hidden sections still exist as entities (`ToggleSection-
  EntitiesVisibility` only flips visibility) — H1 verifies "entity exists even
  while hidden" on the 22-section outlier missions before H3 trusts the scan.
* Popup clutter law (Thomas 04.11 "zu überladen"): the stay-out reminder rides
  the existing INFO-TEXT switch; the seal explain line rides the EXPLAIN switch.
  No new always-on block.
