# Tactical AI — Mechanics Behind the Scenes

Mechanics reference for the Tactical AI mode, written in the style of the IC2 concept
docs. Every value in this document is read out of the shipped code and data files — file
and class names are given so anything can be checked. Diagrams are Mermaid (render
directly on GitHub).

- Code: `ImperialCommander2/Assets/Scripts/TacticalAI/` (pure-C# decision core in
  `Core/`, Unity adapters at the top level)
- Data: `ImperialCommander2/Assets/Resources/Languages/En/tactical_*.json`
- Tests/verification: `tools/TacticalAiHarness/` (parity harness), `Assets/Tests/` (NUnit)
- Player-facing doc: `docs/WIKI_tactical_ai.md`

---

## 1. Design principles (what the mode is allowed to be)

1. **Zero new input.** No feature may ever ask the human to report positions or states
   into the app. Everything the AI reads is either already in the app's mission data or
   already entered while playing. This is enforced in code, not just on paper (§5).
2. **Fair information.** The AI sees only public table knowledge. Hidden information
   (hero cards, dice, hands, villain deck) has no representation in the AI's vocabulary;
   the event feed **rejects unknown payload keys fail-loud** (`WorldEventFeed.Append`).
   The legacy hidden bonus-effect rolls never fire while a tactical mission is active.
3. **Human as actuator.** The app plans and orders; the board is the rules engine. Every
   order is an if–then tree whose conditions the human evaluates on the real board
   (range, line of sight). The app never decides "the hero stands on tile X".
4. **Determinism.** Same campaign seed + same mission + same difficulty ⇒ identical
   orders, purchases and market draws. The skill layer uses its OWN RNG stream derived
   from the mission seed; the legacy shuffle/dice RNG is never touched, so the
   "switch off" path stays bit-identical to the original app.
5. **Difficulty = decision quality, never power.** All five levels choose among the
   IDENTICAL set of legal options; a level changes only the selection (rank band,
   filters, delays, lookahead). Stats, ranges, action counts and option legality are
   never touched — verified by an option-set diff test that must always be empty.

## 2. Architecture

```mermaid
flowchart LR
  subgraph App [IC2 app - existing classes]
    SE[SagaSetup / CampaignStructure\nper-mission badge]
    SC[SagaController\nMedPac policy, round start]
    ME[MapEntityManager\ndeployment point pick]
    DGP[DeploymentGroupPopup\nactivation popup]
    EH[MissionEventHandler\nXP / reward hooks]
    CM[CampaignScreen\npersona chip + badges]
  end
  subgraph Bridge [Unity adapters - TacticalAI/]
    BR[TacticalAIBridge]
    WE[WorldEventsUnity\npassive capture]
    UI[TacticalAISettingsUI\n4 settings rows + badges]
  end
  subgraph Core [Pure C# decision core - TacticalAI/Core/]
    SET[TacticalAISettings]
    WF[WorldEvents feed+projection]
    HP[HeroProfiles]
    FC[Focus / Doctrine]
    OP[OrderPlanner / ReferencePlanner]
    TL[ThreatLedger]
    SK[SkillProfile S1..S5]
  end
  SE --> BR
  SC --> BR
  ME --> BR
  EH --> BR
  BR --> WF
  BR --> OP
  WE --> WF
  UI --> SET
  OP --> SK
  OP --> FC
  OP --> TL
  FC --> HP
  WF --> OP
  OP --> DGP
```

The core is Unity-free (it is compiled as a standalone project by
`tools/TacticalAI.Core/` for the parity harness), so the exact decision code that ships
in the app is the code the tests replay.

## 3. Where settings live

`TacticalAISettings` (serialized like other app settings; since M6p the FRESH defaults are
master switch **ON** at difficulty **S5/Thrawn** — a stored player value always wins):

| Setting | Values | Consumed by |
|---|---|---|
| Master switch | OFF / ON (default ON) | every hook; OFF means the legacy code path executes verbatim |
| Difficulty | S1..S5 named Recruit…Thrawn (default S5) | `SkillProfile.For()` (§8) |
| Explain | NONE / FULL (default NONE) | popup "why" lines |
| Info text | ON / OFF | rules reminder block on popups |

Per mission, a campaign-mission badge cycles `AUTO → OFF → S1..S5 → AUTO`; `AUTO`
follows the global difficulty. Mission-level choice is stored on the mission item and
resolved at mission start (`TacticalAIBridge.TacticalMissionActive()`).

## 4. What the app actually knows (the world feed)

All of this is ALREADY digital in the app when playing a Saga mission; the mode reads it,
writes nothing back:

| Knowledge | Source in existing app data |
|---|---|
| Map geometry: tiles, walls, doors, room/region layout | mission `mapSections` / tile entities |
| Objective entity positions (terminals, crates, tokens, DPs) | entity list with `entityPosition` + type; ~1,660 deployment-point entities carry position + side |
| Round number, round limit (incl. runtime re-wires), objectives announced | `gameVars`, `OnChangeObjective` |
| Which deployment points were chosen, spawn groups | DeploymentManager state |
| Own group positions at spawn and own movement (the AI placed them) | the mode's own orders (spawn DP + activation outcomes) |
| Own group damage | the OPTIONAL per-group wound-tracker dial next to the deployment card (it is not tied to individual figures; if a table doesn't use the dial, the model only learns losses through Death/Removal events) |
| Hero wounded / defeated status | hero card toggles (`heroState.isWounded/isDefeated`) |
| Which hero activates when (public activation order) | activation events |
| Threat spend/replacement | `gameVars.threat`, threat ledger hooks |
| Campaign bookkeeping: hero items, known skills, XP | CampaignManager state (read-only) |

**What NOBODY in the app knows, by design:** figure positions on the physical board, line
of sight, hero hand/exhaustion state. These remain human calls at every difficulty.

### Event vocabulary (the complete surface)

`WorldEvents.cs` — append-only, per-mission, sequence-verified and hashable. Kinds:

`MissionStart, Spawn, Move, Death, Removal, ObjectiveChange, TileChange, FocusChange,
ThreatSpend, ActivationStart, ActivationEnd, HeroActivated, HeroWounded, HeroDefeated,
GroupDamage, RoundEnd, RoundLimitChanged`

Payloads carry only public values (names, zone labels, rounds, wheel damage numbers,
threat balance). Unknown keys are rejected.

```mermaid
flowchart LR
  A[app emits event\nwhile playing] --> B[WorldEventFeed.Append\npublic-keys only, fail-loud]
  B --> C[project to planner view\nWorldEventVector]
  C -->|delayed by beta rounds| D[planner belief state\nrings, zones, heat]
  D --> E[order planning]
```

Hero positions are modeled as **zones and rings, never points**: when a hero activates
the model widens a reachable ring around the last public location; a
`HeroWounded/HeroDefeated` flip during imperial activation says "a group could reach and
shoot that hero" and narrows the area. Difficulty controls how fresh this feed is (§8,
beta).

## 5. Focus selection (who gets shot at)

`Focus.cs` + `OverlordFocus.cs` + `HeroProfiles.cs`. The round's focus target is
computed once per round, announced publicly at round start (and re-ranked each
activation from the same keys). The shipped comparator (`Compare`, keys in order):

0. **Aim mark** (public designation — the mission text or the Imperial feed names
   a hero; most recent designation first). This is the ONLY human input that
   ranks above everything else.
0b. **Reaction keys** (chain R-D/S-D/S-E): `Reaktion` (the hero who shot THIS
   activating figure inside an open window this round), then `Wachzone`, then
   `Vergeltung`, then `Maskenvertrauen` — fact-track before suspicion-track,
   each null-inert when its card/module is not in play.
0c. **Objective actor** (M10b): the hero credited with the last mission-objective
   progress (activation-window attribution from the world feed, never a position
   read) OUTRANKS the entire counter/role doctrine below; two objective actors
   tie-break by most recent engagement (Player-19). The +1 threat bump is
   per-tier and rides on top of the number.
0d. **Wound-objective law** (`woundAll` counter class, wounded-focus fix 03.11,
   final phrasing 06.11): when the mission's printed END demands wounded heroes
   ("all heroes wounded"), a hit on an already-wounded hero earns ZERO counter
   progress — so healthy heroes rank first, the ×2 finisher economy is OFF, and
   within the healthy the printed chip vector orders: `[healer-kit 3, supporter
   2, plain 1, identity-healer role 0]` → mobility → damage-EV (lexicographic,
   `HeroProfiles.ChipKey`). Without the counter none of this exists — the
   null-counter canon stays bit-identical.
1. **Threat value per hero** — current law (M15–M25, Thomas rulings 05.–06.11): the
   hero's TRUE standard-weapon dice from `tactical_hero_cards.json` (never the
   printed attribute pool — "auf der Heldenkarte stehen nur Attribute") plus the
   weapon's price-energy (`weapon_ev_per_1000_cost × tier × cost / 1000`, M17:
   E-11 +2/5, lightsaber +3, gloves 0). The score itself is the **band average**
   (M19, Player-28 DL-44 finding; two-economy law since M27→M29, see those
   sections): every distance 1..reach is paired once, and each distance is
   ONE attack with the ONE strongest weapon that can physically fight there
   (M29, Thomas ruling "Zwei-Ökonomien-Bande"). Ranged candidates compete
   for the gun slot ONCE — `score(gun) = Σ_d≤gate ev(d)+price` over the whole
   band, argmax wins, no per-distance gun switching (the M19 free switch let
   a strictly weaker printed pistol win single cells; the M26 replace decree
   and the M27 single-weapon rule were both shortcuts, see their sections).
   Melee options (a P hero's printed attack, melee items, attribute-bound
   pools) score their OWN coverage cells under the melee economy — no
   distance accuracy spend (M20 distance-inert rule) — and take a cell only
   with a STRICTLY greater own-economy value (dice + price); a tie keeps the
   gun, reach wins ties. Distances no option can fight at count as 0. The
   operand is a mean over distances, never a sum over weapons: no cell ever
   combines two weapons, one attack = one weapon (Thomas, 06.11.). In hold
   postures
   (protect/stall) that average is percent-weighted — and since M20 (Thomas
   melee ruling, 05.11) WITHOUT a range cell:
   `threat = 100 × (60% × score/(8/3) + 40% × band/3)`
   with fixed anchors (max exchange EV 8/3 = RR item weapon, band 3). Range
   is counted exactly once — inside the band average (wide weak bands dilute,
   melee pays nothing); the old 10% × range/7 cell double-counted reach and
   only taxed printed melee heroes. In kill/wound postures the band average
   stands raw. Wound rides purely
   through the printed `tempoVerwundet` (no percent malus — proven redundant).
   Since M21 (Thomas ruling, 06.11) the printed ability effects count too, as
   language-independent SKILL-ID pins in the hero's `effekte` asset block (schema
   `tacai.hero_cards/5`): a hero whose ability prints a Blast/Explosion adds a flat
   hold-posture increment of half the printed value in damage-EV terms
   (`1/2 × H × 2/6 per round` — Fenn: 1H healthy, 2H once his printed upgrade card
   `fenn07` is equipped); tempo cards (`fenn01` "Taktisches Vorrücken") raise the
   hero's band tempo in the healthy AND the wounded state (speed 5 / 4; since
   M22 also `diala05` "Kunst der Bewegung" for Diala Passil). Unknown skill ids
   never match — an absent `effekte` block is the null law, every pre-M21 hero
   parses bit-identical. Both summands print as their own EXPLAIN clause.
   Since M22 (Thomas ruling, 06.11.) the same block can print a SUPPORTER
   role: `supporter.always` pins identity (Davith Elso the commander, MHD-19),
   `supporter.skills` pins chosen cards — Diala Passil is a supporter while she
   carries `diala02` "Machtadept" (friendly reroll) or `diala03`
   "Kampfmeditation" (friendly Focused); her `diala05` "Kunst der Bewegung"
   ("Apply +1 Speed") rides the tempo-pin law (72→85 healthy, 58→72 wounded).
   The supporter flag is read ONLY by the woundAll chip law (supporters are
   chipped first — `HeroCard.SupporterEffektiv` into `ChipKey`); it never
   moves a threat number: eliminate stays pure dice (findings 06.11. §1).
   M24 (Thomas ruling, 06.11.) added the HEALER pin (`healer.skills` — printed
   repair kits like the MHD-19 sets): a hero who can repair wounds is chipped
   BEFORE the supporter class ("Heiler mit Priorität ausschalten"). M25
   (same day) extended the block with `angriffs_bonus` (printed extra attacks —
   Gideon's `gideon07` "Hammer und Amboss" and `gideon08` "Meisterleistung",
   each HALF of one neutral attack EV, flat on the damage operand like the
   blast), `genauigkeit_pins` (equipped cards printing static accuracy —
   `loku08`, `vinto04`, `drokkata03`) and the hero-row prints
   `waffenGenauigkeit` (weapon line, rides BOTH states: CT +2, Loku +1,
   Saska +1), `genauigkeitGesund` / `verstaerkungGesund` (healthy-side prints
   only — the side law: `wiki_heros_raw` models wounded-side abilities for 16
   of 21 heroes, Loku has NO 'b ability' print, so his "Clear-Minded" +2 AND
   his recon-token synergy end with the wound; his All-Weather Rifle's +1
   Accuracy rides on). Accuracy consumption is the EXISTING keyword seam of
   `Dice.BuildAttackStats` — the M12d pairing rule couples the pips with the
   damage exactly like item accuracy. Schema `tacai.hero_cards/5`.
   Campaign purchases still count twice: the bought weapon moves the score
   (M17), the XP-ability index stays the economic basis (M4) — the hero who is
   really strongest on THIS campaign, not from a tier list.
2. **Wound economy** (`EconOf`, non-woundAll only): ×2 in the removing races when
   the figure can actually finish the wounded hero (printed wounded bar fits the
   figure's damage EV — the sim's ERLEDBAR law, M10a part 2). Protect/stall rows
   carry no doubling (the penalty there is already the printed tempo).
3. Deterministic parity + tie chain: for no-profile rows wounded first, then
   EquipmentIndex (tier × price), AbilityIndex (XP sum), attack dice, health;
   then squad row left-to-right, then hero id.

The popup's "why" line states which key decided ("the mission objective names
him", "highest firepower among the remaining heroes", "finishing him counts
double — except when the mission demands new wounds"). No hidden dice, no
randomness except the documented skill-layer fumble draw.

## 6. Doctrines (how a group plays the mission)

`Doctrine.cs` + `PatternWeights.cs` (M13u). Each group gets one of the named patterns
per round from **one single rule engine for every mission**:

1. **Printed movement law first** (no dice): a deployed Stativ/Tripod can only Guard —
   its printed rule forbids moving while set up; Massive groups never roll Hunter.
2. **Public weight row**: every group matches a row in
   `Resources/Languages/En/tactical_pattern_weights.json` (printed-flavour distribution,
   e.g. Stormtrooper 55 Advance / 10 Hunter / 25 Guard / 10 Block; bounty hunters
   60 Hunter; E-Web 100 Guard). First match wins; a catch-all row covers the roster.
   The file is reviewable in the repo — fairness rule: no hidden modifiers, ever.
3. **Mission-structure modifier as DATA, not code**: when the mission structurally
   defends (a Terminal entity or an escort priority target is in play), the authored
   defend row shifts it: Advance −25, Hunter −10, Guard +20, Block +15 (clamped at 0;
   a positive shift can open a pattern the row never carried). No second code branch —
   the mission clock's final third switches the row off (hunt phase), announced once.
4. **Per-round draw**: seed = mission session key ^ Fnv1a64(card id + round), so the
   doctrine is stable within a round, differs per mission, replays identically. The rolled
   doctrine NAME stands permanently on the doctrine line directly under the focus line of
   every activation popup (independent of EXPLAIN, since M6p); EXPLAIN=FULL adds only the
   draw value as "Muster-Wurf: N/100", plus the full effective distribution on the Debug
   channel.
5. On top of the roll the two persona layers still apply unchanged: the captain's guard
   band pins the back half of the advancing squad in defend deployments (S4/S5), and
   persona pattern multipliers bias flips.

No asset / no matching row -> the legacy M4 classification branch runs bit-identical
(the pre-M13u fixtures stay green either way).

## 7. Round and activation pipeline

```mermaid
flowchart TD
  A[IncreaseRound] --> B[Bridge.OnNewRound]
  B --> C[project world feed\ndelay per beta]
  C --> D[plan round:\nfocus, doctrines, activation order\nthreat spend per economy]
  D --> E[order list shown\nas plain-language line]
  E --> F[human activates a group\nlegacy spinner still exists OFF-mode only]
  F --> G[Bridge.TryBuildActivationPlan]
  G --> H[popup: focus line,\ndoctrine line, action tree,\nwhy lines, optional villain option]
  H --> I[human executes on board:\nevaluates range/LoS conditions]
  I --> J[GroupDamage / Move events\nback into the feed]
  J --> F
```

Key classes: `TacticalAIBridge.OnNewRound` (planner trigger), `TryPeekNextActivation`
(order preview for the UI), `TryBuildActivationPlan` (popup content),
`TryChooseDeploymentPoint` (reinforcement placement, §11).

Action economics on the popup follow the printed rules: at most two action slots, one
attack unless the card prints more (attack cap; IG-88-style multi-attack cards print
their own exception), doors cost an interaction action.

## 8. Difficulty = the SkillProfile table

`Core/SkillProfile.cs`. Each level is a parameter set; NOTHING else differs. The legal
option set is identical at every level (enforced by test).

| Param (meaning) | S1 Recruit | S2 Stormtrooper | S3 Veteran | S4 Commandant | S5 Thrawn |
|---|---|---|---|---|---|
| RankLo/Hi (focus rank band) | 2–4 | 1–2 | 1–2 | 1 | 1 |
| MissRate (fumble widens band) | — | 0.30→3 | 0.10→3 | 0.05→2 | — |
| CounterBlind (ignores wounded/killable) | **yes** | **yes** | no | no | no |
| Horizon (lookahead: 1 reactive / 2 race window 1 / 3 window 2) | 1 | 2 | 2 | 3 | 3 |
| FeedDelay (world feed staleness in rounds) | 2 (halved) | 2 | 1 | 0 | 0 |
| Positioning quality | first legal | hillclimb | shortest path, no lane-avoid | full planner | full planner |
| Economy (threat saving) | dump everything now | save 1 round | save 2, ±12% noise | save 2 | save 2 |
| Rho (risk judgement depth) | 0 always press | 1 only extreme | 2 thresholds | 2 thresholds | 3 full incl. threat replacement value |

Notes:
- All fumbles/draws come from a per-(seed, level) RNG stream — a higher level never
  gets luckier, only better.
- S1/S2 being `CounterBlind` is why "Veteran is the turning point": from S3 the wounded
  marks actually drive focus order.
- "Plans two rounds ahead" (S4/S5) is a **race computation against the heroes'
  reachable area** (movement envelope per ring), not a prediction of player decisions —
  it scores positions by how much they shrink the heroes' options.

## 9. Threat economy

`ThreatLedger.cs` + `RiskPosture.cs`. The Imperium's spendable threat is the mission
threat budget the app already tracks; the ledger models what is on the table (spawned
groups, their damage state) and what replacing a wounded group would cost. The economy
parameters (save rounds / force-after-idle / dump) decide when new groups may spawn;
risk posture (rho) decides between press / hold / disengage per group using own group
damage state (from the optional dial when the table uses it, plus death/removal events in
any case) and ring distance — with the fallback clauses printed on the popup.
`ThreatSpend` events flow back into the feed so the human sees the ledger reasoning in
the explain texts.

## 10. Villain special abilities and keyword rules

From S4 up, a third popup block can propose ONE special action per activation — only
from the `{A}` abilities **printed on the shipped villain card data** (17 printed
abilities: e.g. Darth Vader's Force Choke, General Sorin's Bombardment, Bossk's Wildfire;
Vader/Thrawn print none, so they get no block). Conditions shown are the card's own
printed conditions; the human judges whether they are met. Keyword mechanics that come
from unit keywords rather than cards (e.g. Stativ/Tripod "movement OR attack, never
both", attack caps) are enforced as printed text on the popup, never as hidden
modifiers. One example with a visible effect: a card that **prints Assault**
("multiple attack actions each activation" — the shipped roster prints it on
E-Web Engineer, E-Web Elite, AT-DP and IG-88) gets both action slots rendered as
attacks; every other figure keeps the printed one-attack-per-activation cap.
The keyword is read from the card's own printed ability line in every shipped
language, never from a hidden table.

## 11. Reinforcement deployment

`OptionalDeploy.cs` + `TryChooseDeploymentPoint`. Where a newly spawned group enters is
chosen deterministically from the plan among the mission's legal deployment points
instead of a random DP pick. Since M14c the deterministic chooser has a **mission
objective anchor** (Missionsziel-Anker): the mined table
`Resources/Data/taktische_mission_critical.json` (schema `tacai.dp_anchor/1`,
regenerated by `tools/dp_anchor_mine.py`, never hand-edited) lists per mission FILE
the scenario-critical entities (Terminal/Token/Crate) as GUID + coordinates only —
language-neutral, no names (the M14b trait-filter law holds for the whole DP path).
At decision time `TacticalAIBridge.MissionDpAnchors()` resolves each row by GUID
against the LIVE map entities — live position and live section owner are the truth,
a row the live map does not carry drops (data-exists, never an invented anchor).
Ranking is a sequential total order: (a) DP section == anchor section first; (b)
distance to the nearest live anchor INSIDE that section (world distance across
section borders is never compared — foreign DPs rank behind every (a) candidate);
(c) every remaining tie falls onto the PRE-EXISTING seed chain
(sessionSeed^round → name → GUID). An empty anchor list keeps the legacy chooser
line bit-identical (harness pin `dp_anchors.json` D1); the winning anchor is logged
(`[deterministic, anchor=<entity-GUID>/<section-GUID> dist=<n>]`). Fairness: the
mission objective's position is public mission knowledge — the human knows it from
the mission brief, so anchoring spawns near it adds no hidden information. The
printed optional-deployment rule is honored for eligible cards. Deployment remains
public: the human could have made the same choice with the same information.

## 12. Integration hooks (the complete touch list)

Every place the app was touched, all guarded so OFF = legacy verbatim:

| Hook | App class | What it does when ON |
|---|---|---|
| Settings rows + badges | SettingsPanel (UI tab), campaign mission rows | master/difficulty/explain/info |
| Round start | `SagaController` IncreaseRound | planner round plan + feed advance |
| Activation popup | `EnemyActivationPopup` / `DeploymentGroupPopup` | plan-based popup, MedPac/bonus suppression |
| MedPac policy | `SagaController.OnMedPac/ApplyTacticalMedPacPolicy` | MedPac fully off (reference rules) |
| Reward text | `MissionEventHandler` + `ScrubMedpacRewardText` | crate MedPac rewards skipped & text scrubbed |
| Deployment point | `MapEntityManager` | deterministic reinforcement entry |
| XP hooks | `MissionEventHandler` CM6/CM7 | anchors present, **retired**: hook logs and skips (personas decision) |
| Campaign load/save | `SagaCampaign` | unchanged in release — Imperial deck/agenda state models exist but are not persisted/wired |
| Campaign UI | badge chips + persona display | no IMPERIUM panel in this release |

## 13. Fairness invariants (each one has a test)

1. Master switch OFF ⇒ byte-identical legacy behavior (no hook executes).
2. Hidden bonus-effect rolls cannot fire in a tactical mission.
3. All difficulties share one legal option set (option-set diff must be empty).
4. Event feed has no vocabulary for hidden information; unknown keys rejected.
5. The skill RNG is a side stream; legacy shuffle/dice RNG untouched.
6. Agendas' secret text never renders before resolution requires it.
7. Same seed + level ⇒ identical plan hash (`WorldEventFeed.HashOf`, log hash).

## 14. Verification (reproducible by anyone)

- **Parity harness** (`tools/TacticalAiHarness`, plain dotnet console): replays
  machine-generated golden vectors exported from a Python reference model the C# core was
  ported from; full run **45,266 checks / 0 fails** (count after the section-space chain closed 2026-11-04; 44,788 pre-chain + 478 additive section checks). Two ~250k-line vectors are not
  shipped in this PR (the harness SKIPs them; they regenerate on request).
- **NUnit**: 45 tests in `Assets/Tests/TacticalAI/Editor/` (fairness invariants above,
  settings/badge cycling, hook isolation, vector mirrors).
- Run locally: `dotnet run --project tools/TacticalAiHarness -- ImperialCommander2/Assets/Tests/Fixtures/tacai`

## 15. Known limits (honest list)

- No point-position world model and no LoS model — by design (§1); the human resolves
  sight lines at the board.
- One group per enemy card id (planner state keys on card id).
- Higher difficulties fall back to the reference brain on missions without the tactical
  world feed — logged loudly, never silent.
- Announcement-only card effects (see §12) require the human to apply them — deliberate,
  so no hidden engine behavior can appear.

---

## Reactive layer — as shipped (2026-11-04)

- **Feed attribution (RA, 7d1dcd8).** `GroupDamage` carries victim+index;
  the shooter is resolved against the open hero-activation window
  (lastActivator closes it; `strikePending` at RoundEnd) in
  `WorldEventsUnity.cs`. Doors enter as GUID events.
- **Strike = objective work (Player-21-b + -22, Thomas 04.11).** Two laws in
  `WorldEventVector`. VOCABULARY LAW first (Player-22 incident, Thomas'
  #1002 EMPIRE2 log): the feed's `Validate` throws on ANY payload key not
  in `WorldEventFeed.PayloadKeys`, and the old seen-ledger marked a group
  consumed BEFORE the Append — so the freshly added `critical` key silently
  killed every garrison Spawn (poll error, spawn lost forever), starved the
  strike law and Davith stayed focus. Triple fix: `critical` registered in
  `PayloadKeys`; PollGroups marks seen ONLY after a successful Append and
  quarantines a failing spawn for honest retry (warn-once); the harness
  pre-validates every fixture payload through the now-public
  `WorldEventFeed.Validate` — the Core-only harness had shipped past exactly
  this class of bug, so fixtures now trip the SAME law the poll loop enforces.
  GARRISON, TWO-FACTOR (Player-22, Thomas' objective-
  panel screenshot law: "Truppen, die NICHT Missionskritisch sind, würden
  den Missionsmarker auf sich ziehen - dicker Bug"): (1) the Spawn carries
  a `critical` FACT - the emitter reads the group's deployment override
  (`gameVars.dgOverrides`, `setTrigger`/`setEvent != Guid.Empty`: the
  mission file wires THIS group's defeat to scenario machinery; EMPIRE2's
  diner garrison DG006/DG047 are wired, the mid-war Stormtrooper DG002 and
  probe droids DG012 are NOT); and (2) the group is the CURRENT phase's own
  spawn (deployed during/after the last ObjectiveChange). Rounds alone
  dragged the marker onto ambient mid-phase waves; the flag alone would
  credit a last phase's kill during this one - only AND arms it. EMPIRE2's
  "Sucht im Diner nach dem Duros." never names the garrison (the naming
  bullet in the objective PANEL is not the feed payload), yet fighting the
  garrison IS the phase's objective work. CLOSED-WINDOW CREDITS: the closer
  who finishes a `strikePending` window also takes the marker. The door-opener
  Davith case: no attack = no marker (he lost it to CT-1701 who shot the
  phase troops). BEFORE the first ObjectiveChange the garrison channel is
  closed (initial vectors byte-identical). `engages` = named-hero canon
  branch OR two-factor garrison branch; pinned `p21_garrison_strike_window`
  (critical=true) + `p22_ambient_spawn_inert` (critical=false stays inert) +
  `p21_prechange_spawn_inert`. Canon prints `|crit=0|1` ONLY where the
  Spawn carried the key (data-existence law, old vectors byte-identical).
- **Ledger (RB, b893b58).** `WorldEventVector.cs` stores `obj:` /
  `door-GUID:` / `at:` facts with `fact|hypothesis` grading — symbolic
  last-seen anchors; the projection stays coordinate-free outside the hole.
- **Order (RC, 2bbf4d8).** `ReactionOrder.SelectNext` re-evaluates the
  remaining pool at EVERY activation click; no interrupt feature, no flag.
- **Focus (RD, fc8200e).** Reaction key in `OverlordFocus.Rank`
  (`reactionHero` resolved via `ReactionHeroFor`; null => every pre-R-D
  canon vector byte-identical). Compare-slot pinned.
- **Officer (OA–OC, 4772efe/a91232a/e9ac6a6).** Supporter flag +
  `OfficerEmergency` cascade; `TreeKindFor` picks Keep/Emergency popup
  trees (11 languages); supporter pattern rows split; advance/leash laws.
- **Section space (S-A..S-E, 20c283a -> 3463c41, 2026-11-04).** Positioning
  layer shipped: section graph, mask beliefs, trust table, exceptions and
  consumers — full description in the "Section-space module" section below.
  Still true after S: the feed remains a state-differ, there is STILL no
  line-of-sight wall model (the section graph models section-to-section
  passability, not sight lines), and the old R-chain `Belief.cs` remains
  harness-only (zero app call sites, grep-pinned by the law scan).

---

## Section-space module (chain S-A..S-E — 2026-11-04, player-visible)

Positioning layer per `CONCEPT_positionierung_v1.md` (work dir). Landed as
S-A..S-E; closing QA (fresh-worktree re-run on the full chain) closed PASS:
L1/L2, Z1–Z6, law audit and language gates all green, canon proof
44788 + 478 additions = 45266 with zero existing-fixture touches
(`findings_sektionsraum_qa_2026-11-04.md`, local QA record):

- **S-A (20c283a).** Feed producers, state-diff pattern like doors
  (`WorldEventsUnity.cs`): `SectionActivated` (hook at
  `TileManager.ActivateMapSection`, caller MissionEventHandler),
  `EntityInteract` (entity isActive diff keyed by GUID + PlayerSelection
  logger join), mission-critical table as data file.
- **S-B (7f4c191).** Pure Core module: `SectionGraph` (tri-state edges —
  unknown edges are ALWAYS passable, no blocking conclusions ever),
  `sec:` ledger facts next to `obj:`/`door-GUID:`/`at:`, `SectionMask`
  per group (deploy-fact seeding), speed trace (section changes per
  activation window), disjunctives (anonymous victim, HeroWounded).
  Mandatory fixtures: guard/objective-section case, new-section-with-
  intruder case. Section census documented.
- **S-C (1611aeb/1dc3b99).** Mask update rules wired into a
  SectionSpace projection per computation in the app lifecycle; trust
  threshold as DATA table (rational constant); skill curve — exception
  rules run at ALL skill levels (fact-reaction is fairness, not an
  intelligence bonus), mask refinement / posture clamps / support
  navigation gate by skill; calibration rail (audit channel only).
- **S-D (cef788b + 0b9e7f6).** Consumer layer `SectionConsumers.cs`:
  guard-zone exception order (mission-critical object works first),
  C(shooter) retaliation key, 3c fresh-intrusion push priority,
  `PlanZones` feeds the planner's ringZones/contestZone, posture clamp
  (S3+, gated at the consumer). The **null law**: every entry point takes
  an absent belief/fact and returns the INERT answer — fallback ladder
  concept §10, "key OFF = today's canon", machine-pinned
  (`section_consumers.json`, inert pair: same stream, space null ->
  today's pick). Event-driven exceptions (Wachzone, Vergeltung, 3c) fire
  at ALL skill levels; ordering reaction > guard > retaliation
  (`law.slot_basis`).
- **S-E (4d4852e + 3463c41).** Z6 consumer: supporter MASK TRUST
  (`SupporterTargetFor`) — trusted own section with exactly one fresh hero
  anchor = strike behind the pinned fact-chain (focus slot 1e), diffuse /
  below-threshold / ambiguous = support with NO named target; the bridge
  only consults it at emergency stage None (fact beats mask, no second
  cascade). Dead S-D-era hook `SupportNavigationEnabled` renamed to
  `SupporterMaskTrustEnabled` and given its real consumer (zero dead keys
  remain, grep-pinned). `section_support.json` (9 cases, 92 checks) pins
  Z6 monotonicity numerically (support share 3/3 diffuse > 2/3 medium >
  0/3 tight) and the F3 zone order-flips decision-side (S2 and S4). 4 new
  player keys x 11 languages. F2 resolved as wording fix: calibration
  runs audit-channel against fixture reality, no live-rail promise.
- **Player-20-B (Thomas 04.11, "Go").** Focus-zone lock
  `SectionConsumers.FokuszoneHeadFor` (Baustein 7): the hunter brand — S3+
  gate INSIDE the entry point; zone = the critical sections the Wachzone key
  already consumes (never a guess); the first canon-rank hero factually
  anchored inside takes the head (no new ranking, permutation law intact);
  null law pair everywhere else. The bridge consumes it only while ALL fact
  keys are silent (reaktion/wachzone/vergeltung beat the brand — the shot
  back stays a fact, not a chase). Popup tree family `apTreeHunter` +
  officer/elite/keep variants (DE/EN, stale-asset fallback to the aggressive
  rows) rides the SAME rung via `SkillRungNow()`, so plan and popup cannot
  diverge; disclosed on the focus line (`ordFocusFokuszone`) and the EXPLAIN
  line (`ordExplainFokuszone`), Debug channel `fokuszone`. 7 hand-pinned
  laws (`fokuszone.*` checks) over the real feed path. Companion law from
  the same playtest: `buff_before_receiver` EXEMPTS support groups whose
  printed command is the plain +2-movement order
  (`GroupFacts.MovementBuffOnly`, bridge fills it from the printed ability
  law). Player-21 closed the loophole from the other side (Thomas log r1:
  the plain officer reached PlanPos 0 on RAW guard-fit score): the
  HEAD-SLOT law now also guards the base order — a plain-buff group never
  activates first; it swaps with the first non-plain group (an all-plain
  order keeps its head — the law cannot outvote what it cannot replace).
  Runs after chains, finisher-last and the missa step so nothing can undo
  it; S1 index canon untouched; the elite officer (executive command) is
  NOT demoted — early starts are his; reaction pushes (R2 Schutzpflicht)
  still outrank the order law. Executive command and plain support keep
  the chain slot (`order_plan.player20.*`, `order_plan.player21.*`).
  Canon: 45266 → 45282 checks, additions only, no existing fixture
  touched.
- **Befehlszeitpunkt law (Player-24, Thomas 04.11 „Go").** The blind
  `buff_before_receiver` treated commands like stat buffs — the printed
  cards (De enemies.json, DG004/DG005/DG006, verified 04.11) say otherwise:
  the plain Befehl is a campaign RE-ORDER (the receiver interrupts for A
  TURN — zero net action gain, so the Player-21 head demotion stays its
  whole story and the new law never speaks for it); the elite
  Vollstreckungsbefehl is the only command that CREATES an action
  (interrupt: one movement OR attack). Only the elite's slot is therefore a
  timing question at all, and `CommandTiming.ForRound` decides it per round
  on zone facts: named receiver = the living attack-pattern group with the
  highest PrintedEvTotal x VitalityFrac (finisher key; unknown card = never
  bet on), slot follows the PAYLOAD — a FRESH hero anchor inside the
  receiver's zone means the troop activates first and the interrupt pays
  the second attack (tag `receiver_before_buff`, direct adjacency survives
  `finisher_last` through a quiet follow-law re-anchor); no anchor, the
  supporter goes first and the command's movement leg doubles the troop's
  reach this round. The printed 2-field ring stays TABLE knowledge: same
  zone = zone-fact claim in the why-line, anything else hands the check to
  the table (E2 discipline, the app never sees cells). The bridge feeds it
  from the `SupporterMaskTrustEnabled` rung (S4/S5); below and on every
  failure the blind chain runs bit-identical. `officer_timing.json`: 11
  decision vectors on the real feed path (recorded + reviewed line by line,
  silence laws hand-pinned) + 4 plan-consumption cases (before-relation,
  adjacency after the finisher pull, engaged-weaker adjacency, blind canon
  intact). **Player-24.log review follow-up (Thomas 04.11):** the raw-EV
  receiver pick had sent the plan across the map (Diner garrison engaged,
  receiver = far stormtroopers) — receiver choice is now TIERED: groups
  whose zone carries a fresh same-round hero anchor compete first
  (`engaged_first_over_global_ev`), the global strongest is the
  no-contact fallback (all pre-review vectors untouched — record diff
  verified additions-only). **Review 2 (same day, Thomas):** a command is
  given to ONE named figure, never to a group — the ordered stormtrooper
  attacks with ITS dice. Receiver ranking therefore switched from
  WithEv.PrintedEvTotal (GROUP sum, overrates crowds) to the card's
  PRINTED PER-FIGURE attack EV x VitalityFrac (`FigurePayloadKey`);
  `command_pays_one_figure` pins the deliberate flip (4-figure squad vs
  1-figure squad of the same card: per-figure EV ties, lower Index wins —
  the old key would have picked the crowd). Every pre-review vector runs
  n_alive=1, so all of them are untouched by the key change.
  Same review: the decision now also prints per
  activation in the Player log (`TacticalAIBridge::timing DGxxx:` + the
  public-safe why line; silent where the law never spoke).
  Canon: 45312 → 45370 checks, additions only + the two law flips pinned
  in `engaged_first_over_global_ev` / `command_pays_one_figure`.
- **Canon through the whole chain:** 45174 -> 45312 checks. Additions
  only, with ONE deliberate law-flip exception: `p19_causal_strike_closer`
  flips its expected objective_actor from empty to Jyn Odan because the
  Player-21-b closed-window credit now also moves the marker (commented in
  the fixture). Every new behaviour fires only on a present fact or a
  trusted belief, never on absence; Player-22's `|crit=` column prints
  only where the Spawn carried the key, so every pre-Player-22 vector
  stays byte-identical.
- **Player-25 laws (M14a–M14d, 2026-11-04).** Three facts, one text hop —
  all pinned on the real feed path.
  **Closer attribution (Closer-Zuschreibung, M14a, `WorldEventVector.cs`).**
  The window path was dead in the live game: `HeroActivationStart` never
  fires there and `LastShotHero` was never set, so the whole Vergeltung
  lane slept (Thomas 04.11; the strike channel has proven the same closing
  law live since M6s-B7). New key `shotPending` (group -> {hit seq, hit
  round}): a `GroupDamage` WITHOUT an open window arms the pending credit
  for THAT group — hit-gain: the first hit of the round counts, groups are
  independent. The next `HeroActivated` of the SAME round is the **closer**
  (Schliesser): the hero whose activation closes the damage credit becomes
  the suspected shooter. `RoundEnd` deletes the credit (history !=
  causation) — the table discipline reads *mark your damage before the
  round ends*: a hit after the round's last hero activation credits
  nobody. The window path stays untouched and takes precedence: a later
  in-window hit (higher seq) is never overwritten by the older pending
  credit (Seq-Guard; fixture `p25_window_vorrang` pins both paths in one
  vector). Consumers unchanged — `OverlordFocus.ReactionHeroFor` (S2 lane),
  `ReactionOrder` R1-push and `OfficerDoctrine` E1 read the same fields and
  wake without any new event.
  **Spawn visibility (M14a, `WorldEventsUnity.cs`).** `SessionStart` now
  restates already-deployed enemy groups as `Spawn {resync:true}` — payload
  identical to the mid-session `Resync()` (group/size/name/critical/resync,
  every key already in the vocabulary register, no new vocabulary; the
  Player-22 append-before-seen law applies verbatim). These are resync
  FACTS, not claimed events. Consequence: start troops (DG015/DG047 in the
  Player-25 log) are visible to the wake zone, the mask and EXPLAIN from
  the first round. Honest boundary: the SessionStart resync is adapter
  code (Unity glue) — compile-pinned + projection-pinned
  (`p25_session_start_resync`); the live-board proof follows in the next
  playtest.
  **Deployment anchors (M14c, `Core/DeploymentAnchors.cs` + bridge):**
  mission-objective anchors on the deterministic DP choice — full law in §11.
  **Hunter text (M14d, ui.json only):** the shipped `apTreeHunter` stage 1,
  the hunter's creed and `ordExplainFokuszone` now name the hero *moving
  into* the focus zone as a candidate next to the hero working inside
  (several candidates -> the higher threat value, EXPLAIN shows it). Pure
  text hop: `FokuszoneHeadFor` is UNCHANGED — the plan-side lock still
  heads on the hero factually anchored inside; the widened candidate set is
  the board-level doctrine the human evaluates from the popup.
  Canon: 45370 → 45426 → 45485 checks, additions only. Three existing
  canon vectors gain the `|shot=|` column per law (`p19_causal_strike_closer`
  `|shot=Jyn Odan|3|2`, `sd_vergeltung_mask_track` `|shot=JY|8|2`,
  `sd_vergeltung_ambiguous` `|shot=JY|5|2`) — record → review → pin; all
  readouts stayed byte-identical (mask-channel retaliation never reads the
  fields).

---

## Activation planning, in depth (Noldorion question, 2026-11-04)

Source of truth: `OrderPlan.cs` (port of sim/order_plan.py, concept 3.4.1),
`Patterns.cs` (concept 3.5), `SkillProfile.cs`, `ThreatEconomy.cs`,
`ReactionOrder.cs`. Everything below is stated code behaviour with file
anchors — no design wishes.

### The contract

`OrderPlanner.PlanRound(roundNo, groups, cards, posture, skill, rng, ...)`
(OrderPlan.cs:308) computes the round's activation order as a **pure
function**: no wall-clock, no private RNG instance — the caller-injected
skill rng is the only randomness, seeded per round via
`sessionSeed ^ round * GOLDEN` (the same formula the legacy dice used, so a
recomputation reproduces identical draws; ReactionOrder.cs header,
SectionsReactionOrder.cs:150 pins it). The result must satisfy
`AssertPermutation` (OrderPlan.cs:464): every living group appears exactly
once in the plan — enforced at runtime and exercised by the harness
(SectionsB.cs:396).

### Inputs the planner may legally know

`GroupFacts` (OrderPlan.cs header + class docs) carries four kinds of
information only:

1. **Printed strength** — DeploymentCard dice/HP/speed/size (cards are
   public data).
2. **Public posture** — mission doctrine state (public per M2a.1).
3. **AppView-sanctioned beliefs** — zone bands (ring zones, contested
   zone). Zone granularity only; hero coordinates never enter a plan.
   *App status: fed since S-D — `SectionConsumers.PlanZones` derives the
   bands from the section belief (objective section + overlaps); the
   decision-side order flips are pinned for S2 and S4 in
   `section_support.json`.*
4. **Instruktions-Ledger** — the App as command authority owns the groups'
   own Soll-Positions (own orders are own knowledge, 3.3.3 Nr. 2b).

### Posture and patterns (the "posture" half of the wiki line)

Two posture vocabularies coexist **by design** and answer different questions.
The **doctrine posture** (`hunt`/`defend`, Debug line `doctrines posture=…`) is
the structural mission state behind `ClassifyPattern`'s dice rows and the guard
band (the "Punkt-2" phase rule); it has no word for stall. The **planner
posture** (`eliminate`/`stall`/`protect`/`deny`, Debug line `posture=… round=N`)
is the round stance derived from the public feed and drives formation, focus
measure and clamps. An escort/infiltration mission legitimately prints `hunt`
and `stall` side by side in the same round — different layers, not a
misclassification.

Posture is derived per round from public feed state, not chosen secretly:
the objective-verb law (`WorldEventVector.DerivePosture`: protect words win,
then — since M23 — imperial attack verbs, then the round clock, then stall
verbs, fallback eliminate) sets the stance; the bridge's doctrine phase rule
(defend → hunt phase flip,
TacticalAIBridge.cs "Punkt-2") feeds `ClassifyPattern`, which assigns each
card a pattern and discloses the roll in the EXPLAIN / Debug channel as
`pattern|drawPct|dist` tokens (bridge `_patternRoll`) — "what did the AI
draw" is auditable. Patterns (Patterns.cs, concept 3.5) are **tiebreak
overlays and movement shackles only, never legality changes**: aggressive
(shortest attack path), guard (guard-zone clamp until threat), blocker
(holds the path valley of the heroes' terrain-cost field), hunter
(from tier 3 the **Fokuszone lock** — target is the hero factually anchored
in the focus zone, pass-by heroes outside it are ignored, retaliation
outranks the brand; the plan-side pick is unchanged since M14d, while the
shipped popup text additionally names the hero *moving into* the zone as a
board-level candidate — see the Player-25 laws entry; below tier 3 the
hunter plays the aggressive rows
verbatim — popup trees `apTreeHunter*` ride the identical rung gate),
support (distance ≥ 2
from heroes, keeps its most wounded ally in LOS), ambusher (inert until a
hero is inside AMBUSH_RADIUS).

### Printed threat budget (the "threat budget" half)

Two distinct printed economies, both in ThreatEconomy.cs:

- **Mission threat budget / reinforcements** (`ThreatLedger`): income per
  round scales with living Imperials (`ProjectIncome(aliveImperials)` /
  `Tick`). Reserve groups carry the card's printed cost (cost reductions
  only via printed Imperial card effects, `EffectiveCost`). Release is
  deterministic: cheapest affordable first, ties by group id; a saving rule
  (`balance - cost >= save_rounds * projected income`) holds money for
  later waves; an anti-stall rule forces the cheapest affordable group
  after long idling. The ledger is private data — its entries never reach
  the public log (ThreatEconomy.cs header).
- **Printed strength as the planner's currency**: the strength-efficiency
  matrix multiplies printed damage EV x remaining vitality (attrition) x
  posture-fit of the pattern x proximity of known rebels (the zone bands
  of point 3 above — fed by `PlanZones` since S-D).

### From matrix to order: hard slots and laws

On top of the efficiency order, named synergy chains move pairs
(OrderPlan.cs:241): `buff_before_receiver` and `receiver_before_buff`
(the Player-24 Befehlszeitpunkt law made them ONE payload decision —
`CommandTiming` names which side the elite supporter earns: Bewegung vor
dem Empfänger, Angriff direkt nach ihm),
`blocker_before_attached_attack`, `deploy_before_intercept`,
`finisher_last` (highest damage potential activates last; with the
woundAll VictoryCounter law, fresh damage stays last regardless,
OrderPlan.cs:306). The tempo law weights heavies/swarms (OrderPlan.cs:262,
286). Every deviation is logged per slot: `OrderSlot.Why` is a
public-safe German reason string carried in the order record
(OrderPlan.cs:108, 127).

### Skill binding S1-S5 (quality, not hidden luck)

Per OrderPlan.cs header and SkillProfile.cs: S1 = chance-free but
plan-less index order (old-app level); S2 = efficiency order, chains off;
S3 = chains + rough efficiency with seeded missers; S4 = full efficiency +
finisher-last, rare missers; S5 = full chain optimisation, no missers,
tempo law at full weight. Missers are implemented as a seeded draw against
`SkillProfile.MissRate` (OrderPlan.cs:417) and annotated in the plan
(`MissNote`, surfaced as "miss" in the order record). Skill levels share
one rng stream — no separate luck pot.

### Liveness: the order is re-planned every click

R-C (Thomas' 04.11 decision, ReactionOrder.cs): at EVERY press of
"activate Imperials" the order is re-scored from the REMAINING figures —
basis = `PlanRound` over exactly the pending list (App:
`GetNonExhaustedGroups()` per click), plus a reaction-push overlay from the
public event ledger. The round-seed formula is reused VERBATIM, so
mid-round recomputations roll the same seeded misses as the round start
(SectionsReactionOrder.cs:150). Determinism law pinned: same pending set +
same seed + same event stream -> identical pick AND identical account
(:130). The permutation invariant of the walk is checked as
`walk_multiset` (:95). No interruption, no skipping: every pending figure
stays pickable until it acted.

### Precise attribution of the fairness pins

The **id-multiset-equal-across-skill-levels** pin belongs to TARGET
selection (OverlordFocus.Rank; harness skill-21/22 fixtures) — the planner
side is pinned by `AssertPermutation` (runtime) and the RC walk multiset
check. Both sides share the principle (skill changes quality of choice,
never who is choosable), but the tests are different — keep the
attribution straight.

### Concept-level layering (where each piece sits)

legality (never touched by planning) -> pattern overlay (public roll,
shackles only) -> strength-efficiency matrix (printed value x belief x
posture fit) -> synergy chains and laws (named, why-logged) -> skill
binding (draw budget + miss rate) -> per-click re-plan over the remaining
pool (RC). Each layer is separately testable; each layer's inputs are
public, App-sanctioned, or the group's own orders.


## Printed-card law, wound objective, and data governance (2026-11-06, M24+M25)

What the 06.11. card audit changed under the hood, beyond §5:

- **Print honesty is the law.** Every effect enters exactly as the physical card
  prints it — verified against the `wiki_data` raw fields (`wiki_heros_raw.json`,
  `wiki_class_cards_raw.json`, `hero_weapons.json`), never against a paraphrase.
  Surge-prefixed (`{{Z}}`) and exhaustion-gated lines stay OUT (they are options,
  not prints); static lines are in. Three first-pass "damage buffs" were
  misreadings caught by this rule ({{V}} is the Evasion pip, there is no orange
  attack die in this system).
- **The side law.** Hero-card abilities live on the side that prints them.
  `wiki_heros_raw` models wounded-side ("b ability") prints for 16 of 21 heroes;
  heroes whose back side prints nothing (Loku, Fenn, Jyn, Biv, Davith) lose ALL
  card abilities when wounded. Loku's recon-token marker AND "Clear-Minded" +2
  end with the wound; only his weapon's static "+1 Accuracy" rides on. A missing
  b-print means "ends" — never assume a default.
- **Naming discipline (Thomas rulings, same evening).** No invented in-game
  words in player-facing texts: the woundAll counter class is presented with the
  mission's own words — **"Missionsziel 'alle Helden verwunden'"** — in DE and EN
  ui texts, the asset conventions and the code comments ("Wundzähler",
  "Wunden-Zähler", "Wunden-Tracker", "Battle Vision" all removed; residue-checked
  across all 11 ui.json). The basis label "mark" was corrected too: it is the
  *aim mark the mission objective names* ("Zielmarke dieser Runde: das
  Missionsziel nennt ihn"), not a wound marker — the old wording contradicted the
  shipped `ordMarked` text.
- **Generator ownership restored.** `wiki_data/tools/curate_hero_cards.py` had
  silently fallen behind (schema /2 since M21 while the asset grew M22–M25 by
  hand) — a regeneration would have wiped the curated pins. It is now the asset's
  OWNER again: SKILL_EFFECTS carries every pin (including the late `drokkata03`
  "Bank Shot" backfill — its raw `effect1: "+1 Accuracy"` print was audited from
  the start but never made it into the asset), `ROW_PRINTS` carries the hero-row
  prints, the key order matches the shipped asset, and regeneration is
  **idempotent** (regenerate + `cmp` is now a supported verification step).
- **QA battery updates.** `tools/m20_vector_sweep.py` was a corpse (its import of
  the renamed M19 module had died in the M21 rename — nobody noticed because the
  M21 sweep covered the same rows); it is now a thin forwarder to the canonical
  second implementation, `tools/m21_fenn_effects_verify.py`, which re-derives
  every card-basis row under the CURRENT law. `language_m13c_texts.py` is a
  one-time migration validator, not a battery entry. `m24_healer_verify.py`
  anchors follow schema bumps (/5).
- **Final 06.11. battery:** harness ALL PASS 45591 checks ×2 (deterministic),
  gate ALL PASS (incl. 11-language key parity), m21 sweep 71 rows + anchors
  0 drift, m22 ALL PASS, m24 anchors OK, m19 drift check empty, compile_check
  APP rc=0 errors=0 (285 files) / TESTS rc=0 (10 files).

## The carried weapon replaces the standard — M26 (Thomas ruling 06.11., Player-29 log)

**Trigger.** In the Player-29 session log the planner scored Jyn Odan with her
printed Green+Green pistol because the M19 per-distance switch let the strongest
pool at d=1 win that cell ("1:Green+Green"). Thomas' ruling: "Jyn hat den DL-44 …
Ein Blaster mit Reichweite 1 ist totaler Quatsch. Jyn ist kein Nahkämpfer." The
rule text backs it: for a hero the attack value *is found on his weapon item
card* — the hero attacks with what she CARRIES.

**Law (HeroCards.cs, band loop).**
1. A carried {{O}} (ranged) item weapon OCCUPIES the hero's ranged slot: the
   printed standard blaster never competes at ANY distance (`baseReplaced`),
   not even where its green dice would score higher at d=1. A {{P}} hero's
   printed melee value is never replaced (fists are the body, not a stored
   weapon); {{P}} items merely ADD a melee option at reach 1.
2. Every pool scores only inside its OWN coverage (`poolGate[i]`): base =
   printed Reichweite, a ranged item = its own accuracy gate (the same gate
   law that extends `RunnerRange`), a melee item = one cell. The free
   per-distance switch across the whole band is dead — Verena's gauntlets can
   no longer "shoot" at 4.
3. The display energy operand is the winners' ACTUAL surcharge share:
   `energy = Σ(winner's price-energy over the band) / reach` — a mixed band
   (Verena: melee item at 1, blaster at 2..) stays hand-re-addable.
4. `best_pool` (print) = the pool that won the most distances of the band.

**Verified shifts (record 06.11., port m21 re-derivation 0 fails).** Jyn Odan
(DL-44 Blue+Yellow, acc 4, green bonus 1/2): band cells 2 + 2 + 1.5 + 0.5 →
damage operand 19/12 → **3/2**, stall threat 76 → **74**. MHD-19 stays Blue+Yellow
printed — p19 tie now rides entirely on the price share (Verena 73/80 vs MHD
13/20, eqIndex holds the dearer gear first). item303 "Einsicht" (melee-only
Blue+Yellow, reach 1) became a REAL option at d=1 without buying phantom range
(m19_band_melee_inert: 78 → 54, energy 3/40 — paid as price share, never as
reach). Loku/Fenn without gear keep the M25 printed-bonus numbers (8/3 =
13/6 + 1/2).

**QA battery, M26 (06.11.):** harness ALL PASS 45579 checks ×2 (deterministic);
`m19_band_average_verify.py` re-ported to the new candidate law (20 drifted rows
0 fails before merge, 0 drift after); `m21_fenn_effects_verify.py` (canonical
second engine) 63 card-basis rows 0 fails; m22 ALL PASS; m23 rows=43 fails=0;
m24 anchors OK; fixture `overlord_focus.json` rebased via
`tools/m26_merge_drift.py` (style-proven surgical expect merge, 10 cases) with
fresh M26 explain-comments per case; compile_check APP rc=0 (285 files).

## The best weapon is a result, not a decree — M27 (Thomas ruling 06.11.)

**Trigger.** Thomas on the M26 law: "Hart auf die getragene Waffe zu gehen ist
nicht logisch, sondern einfach. Was ist, wenn der Held 2 Waffen gekauft hat?
Die Waffenrechnung muss bestimmen, was die beste Waffe ist. Und der
Standard-Blaster von Jyn ist schlechter als der DL-44 — das muss unsere
Rechnung auch ergeben." M26's `baseReplaced` was a rule, not arithmetic, and
its per-cell leftovers still "computed with" reach-1 weapons nobody would
equip (item301/item101 class).

**Law (HeroCards.cs, two passes).**
1. Options = every attack the hero owns: printed standard, each carried
   ranged weapon, each attribute-bound weapon pool. Coverage as under M26
   (`poolGate[i]`: base = printed Reichweite, ranged item = its own
   acc-gate, melee item = one cell).
2. `score(option) = Σ_{d=1..reach} (d ≤ poolGate ? ev(d) + price : 0)` —
   what the option delivers AS THE ONLY WEAPON over the hero's whole band.
   3. argmax equips; that single weapon then decides EVERY band cell
   (`threat = Σ cells(equipped)/reach + price-share`, price only on cells
   the weapon can fire). Ties keep the printed weapon (earlier pool).
   No per-distance switching (M19 artifact: "1:Green+Green"), no replace
   decree (M26 shortcut).

**What the arithmetic now shows (all re-derived, record 06.11.).** Jyn: GG
pistol band score 4/12·6=… → average 1, DL-44 average 3/2 — the computation
itself retires the standard (M26 demanded it by decree; same threat 74,
different justification). Two bought weapons simply compete (p19: Verena
item108 and MHD item109 - both GY guns - LOSE to the printed Blue+Yellow
standard in the pairing average; both heroes equip the standard, threats
tie at 1, the eqIndex price key still leads the dearer investment).
Diala Passil ({{P}}, carries item303 'Einsicht'): the argmax legitimately
EQUIPS her three-dice attribute pool weapon (printed Blue+Green+Yellow
healthy / Blue+Red+Green wounded — printed dice swap with the wound state):
operand 35/12 → 92 healthy, 43/12 → 94 wounded (was 116/115 while per-cell
switching mixed attribute-at-1 with base-at-2). Fenn's Fenn-with-Einsicht
row drops 83 → 69 for the same reason. m19_band_melee_inert: a
reach-1 melee option is now MATHEMATICALLY always worse than Verena's own
full-band blaster — "mit einem Blaster mit Reichweite 1 rechnet man nicht"
holds by construction; it only wins where the printed attribute pool really
out-scores everything else (Diala), and then it wins over the WHOLE band as
her one weapon.

**Direction note (honesty):** the single-weapon law is never more optimistic
than the old per-cell switch (the switch was an envelope). Where it lowers
threat (Diala 116→94, Fenn 83→69), the old number had been reading two
weapons into one attack. Jyn's "never underestimate" stays intact — her
DL-44 rides with full price surcharge.

**QA, M27 (06.11.):** harness ALL PASS 45579 checks ×2 (deterministic);
second engines re-ported (m19 band-law reference + the drift oracle now
delegates to `m21.threat21`, the full-law implementation; both tools gained
proper `__main__` guards — import = helpers only, run = verification);
m21 sweep 63 rows 0 fails, m22 ALL PASS, m23 43/0, m24 anchors OK; 16
drifted rows C# == port before the fixture merge (m26_merge_drift.py, 4
cases rebased + all six affected explain-comments rewritten); compile APP
rc=0 (285 files) / TESTS rc=0; gate ALL PASS. Lesson logged: two uncommitted
patches (fixture M25 rows, m19 position-key rank branch + layers) died to
`git checkout` the same day — the drift-oracle delegation exists so such a
loss can never masquerade as a passing check again.

## Two economies, one attack per distance — M29 (Thomas ruling 06.11.)

**Trigger.** M27's single-weapon law answered "which weapon is best" but
broke Thomas' lightsaber question ("Ich habe ein Lichtschwert und einen
Blaster — das Lichtschwert sollte gewinnen und nicht der Blaster wegen der
Distanz"): the band average punished melee options with cells they can
never fight at. The M28 "dominance override" patch that grew from it died
the same day — Thomas asked for the CONCEPT instead: "es muss unterschieden
werden, ob eine Nahkampf- oder eine Fernkampfwaffe bewertet wird … die
Panzerhandschuhe sind wirklich gut."

**Law (HeroCards.cs).** Two weapon economies, scored under their OWN laws:

1. **Gun slot (ranged economy, pays the distance accuracy spend).** All
   ranged candidates (printed O standard, every carried gun) compete over
   the whole band exactly as under M27 — argmax wins ONCE and holds every
   distance it covers. Multiple carried guns are legal and encouraged:
   the best one wins by arithmetic, per-distance gun switching stays dead
   ("mit einem Blaster mit Reichweite 1 rechnet man nicht").
2. **Melee economy (no distance spend — the M20 distance-inert rule).**
   A P hero's printed attack, every melee item, and attribute-bound pools
   score their own coverage (melee items: cell 1; P base: 1..printed
   reach) with their own dice law plus price.
3. **Cell law.** At each distance the value is the strongest weapon that
   can physically fight there; melee takes a cell only on a STRICTLY
   greater own-economy value — a tie keeps the gun ("Gleichstand +
   Distanz = die Waffe mit Coverage"). Distances nothing can fight at: 0.

**What it is NOT (Thomas, same ruling):** never two weapons in one
attack — each cell is one hypothetical attack with one weapon. The
operand is a MEAN OVER DISTANCES, the empire's "expected damage per
attack if we land somewhere in her band" — never a sum over weapons.

**Direction property:** M27 ≤ M29 ≤ M26. The single-weapon rule never
over-reads a mixed loadout; the free-switch envelope never under-reads
it. Both bounds were wrong for opposite reasons and both are now gone.

**What the arithmetic prints (all re-recorded, harness 06.11.).**
Verena Talos + Panzerhandschuhe: cell 1 = gloves 2 + 3/10 (strictly
above the blaster's 3/2), cells 2–4 stay blaster → operand 6/5, threat
54, energy 3/40 (case renamed m19_band_melee_inert →
**m29_band_melee_overlay** — "melee never wins anything" was overruled
by Thomas' "die sind wirklich gut"). Diala + Lichtschwert: saber cell 1,
printed fists cell 2 → 55/12 wounded (116) / 47/12 healthy (115).
m29_saber_and_blaster (new case): item303 + item110 → saber head (d1,
operand 59/24, threat 82, energy 9/8, headline Blue+Green+Yellow) AND
the DL-44 beyond it — "saber wins over blaster" no longer means the
blaster is deleted. Fenn + saber: 83 / 5/2 — exactly the old M26
number, now derived cleanly (M26 had the same mixed cells but also kept
the illegal gun switching). p19/Jyn/Loku vectors: untouched (no melee
in them). The M28 dominance-override (defender-invariant strip test) is
removed from all three implementations — under M29 the saber wins with
dice, no decree needed.

## Where does the distance come from? (Thomas question 06.11.)

The model never sees the board. The distance axis is 100 % printed data:
the hero's `reichweite` from the card table, extended only by verified
ranged items whose own accuracy justifies a longer band (RunnerRange —
Jyn's printed 3 + DL-44 gate 4 = band 1..4, and the asset value is
`"reichweite": 3` in `tactical_hero_cards.json` — her 5 is TEMPO, and
Fenn has range 5). Averaging the whole band WITHOUT board knowledge is
the M19 ruling's point: the empire does not choose where it stands, and
runners run — assuming "we stay at 4" is exactly the optimistic bias
M19 removed. Actual board position enters one layer DOWN (target
legality, movement, blocking), so the chip order (who gets wounded
first) does not wobble whenever a figure steps closer. Static weapon
profile vs. tactics of the moment — deliberately separate.

**QA, M29 (06.11.):** harness ALL PASS 45582 ×2 (deterministic, +3 checks
= new case); m21 full-law sweep 64 card rows 0 fails; m22 ALL PASS; m23
43/0; m24 anchors OK; gate ALL PASS; compile APP rc=0 (285) / TESTS rc=0;
three drifted cases merged via m26_merge_drift.py (C# == port == the
throwaway concept probe — three independent implementations agreed on
every number BEFORE the fixture moved). Lesson logged: when a ruling
changes the LAW, prototype it in a throwaway probe with real numbers
first — M29 was argued and accepted on engine output, not on prose.
