# Tactical AI — The Fair Imperial AI

**Tactical AI** turns the Imperial side of Imperial Admiral from a random
automation into a *deterministic, rule-following opponent*. The printed game's own
activation law carries over: every Imperial figure acts every round — the plan is
always a permutation, never a selection. What changes is the ORDER and the choices
inside it: a deterministic activation plan built from the figures' printed values
and the mission posture replaces the legacy "who activates next" spinner, and
printed rules decide whom the Imperials target and how they move. No drawn
instruction cards, no hidden dice behind a screen — and every rule is visible, so
you can learn it, predict it, and argue with your opponent.
(The behaviour *patterns* — guard, blocker, hunter, support, ambusher — follow the
monster-logic tradition of games like Gloomhaven. The activation *mechanism*
deliberately does not: Gloomhaven draws a random initiative card each round to
decide which monsters activate; here every figure activates, in a planned,
reproducible order.)

Before anything else, one obligation belongs to the hero players: **flip each hero's
activation chip at the END of that hero's turn** — after damage, kills and
interactions have been announced. The whole tactical machinery keys on that flip;
see the [golden rule under Activation](#activation).

This page documents the mode as it is actually implemented. Every section names the
code or data file behind the claim, so anything here can be checked. The deep
mechanics reference (planner internals, tables, formulas) lives in
[`MECHANICS_tactical_ai.md`](MECHANICS_tactical_ai.md) next to this file;
how to build the Android test APK (modules, signing, app id, save migration)
in [`ANDROID_build.md`](ANDROID_build.md).

> **Where this came from:** the app's classic behaviour (the "legacy" mode) automates
> the Imperial player: a random activation draw, simple per-group behaviour, and
> power sources that the printed card does not show — activation bonus rolls the app
> grants beyond the card, and chance-based difficulty adjustments (figures silently
> upgrading or downgrading at deploy). None of that is unusual for an app balancing
> itself against human players, but none of it is printed on a card either. Tactical
> AI keeps the *automation* of that bookkeeping and limits every decision to printed,
> published rules — figures do exactly what their cards print, no more, no less. See
> "Every difference from the legacy mode" below.

---

## Table of contents

1. [The goal: fair, clever, never random](#the-goal)
2. [The four design promises](#the-four-design-promises)
3. [Where to find it — settings and mission badges](#where-to-find-it)
4. [Activation — how the round runs](#activation)
5. [The activation pop-up — how to read and execute an order](#the-activation-pop-up)
6. [Instructions: the if–then trees](#instructions)
7. [Officers and their commands](#officers)
8. [The glossary — new words on the table](#the-glossary)
9. [The doctrines — every role and what it does](#the-doctrines)
10. [Difficulty — five levels, no hidden power](#difficulty)
11. [Command Personas — six Imperial commanders](#command-personas)
12. [Every difference from the legacy mode](#every-difference)
13. [What does NOT change](#what-does-not-change)
14. [What this release does NOT include](#not-included)
15. [Tips, limits and honest admissions](#tips-and-limits)
16. [Links to the original IC2 documentation](#links)
17. [The reactive layer (November 2026)](#reactive-layer)
18. [The section space (November 2026)](#section-space)
19. [Procurement — buying is a decision (November 2026)](#procurement)
20. [The mission axis — win conditions, objectives, polarity (November 2026)](#mission-axis)

---

<a name="the-goal"></a>
## The goal: fair, clever, never random

Three words were the whole brief behind this mode:

- **Fair.** The Imperial side may only know what a human Imperial player at your
  table can know: the board, the printed cards, the announced mission objectives,
  and the wound/defeat status you mark on the hero cards. It never peeks at hidden
  event data, hero hands, villain decks or dice results. The legacy hidden
  bonus-effect rolls **never fire** in Tactical mode — the printed stat card is the
  only source of enemy power.
- **Clever.** The Imperials should feel like a competent squad with a plan: announce
  a focus, hold objectives, block lanes, finish wounded heroes, budget their
  activations — decisions a good human would make, made *well* at the top difficulty.
- **Never random.** Same campaign seed, same mission, same difficulty ⇒ the same
  activation order, the same orders, the same purchases, every time. Chance enters
  only where the printed game has chance: your dice. The AI has no dice of its own —
  and no rubber-banding, ever.

The design method comes from the printed games' own logic: Imperial Assault's
activation law (every figure acts) combined with simple, printed behaviour rules in
the spirit of Gloomhaven's monster logic. Instead of "the computer outthinks you",
you get *rules you can learn*.
A human Imperial player racing your options with these rules is beatable — and
beating them feels earned, because nothing was hidden.

---

<a name="the-four-design-promises"></a>
## The four design promises

Enforced in code, not just on paper (each pointer verified against the shipped tree):

1. **Fair information.** The planner's world model is the public world feed. The
   feed's event vocabulary contains no key for hidden information — an event
   carrying an unknown payload key is **rejected fail-loud**
   (`Core/WorldEvents.cs`: `Append` → `Validate` throws on any unknown event kind or payload key). Hero cards, hands and dice have no
   representation in the AI's vocabulary at all.
2. **Zero new bookkeeping.** The mode never asks you to report positions into the
   app. Everything it reads is either already in the mission data or already entered
   while you play (deaths and activations come from the mission log; wound status
   comes from the hero-card marks you already make in the printed game).
3. **Human as actuator.** The app *plans and orders*; **the board is the rules
   engine**. Every order is an if–then tree whose conditions you evaluate on the
   real board (range, line of sight, occupation). The app never decides "the hero
   stands on tile X" and never rolls your dice.
4. **Determinism.** The decision core is pure C# with its own deterministic RNG
   stream derived from the mission seed; the legacy dice/shuffle stream is never
   touched (`Core/World.cs` — `DeterministicRandom`, a mission-seeded SplitMix64 stream
   dedicated to planning; the legacy dice/shuffle stream is never touched).
   Reproduced by ~44,000 regression vectors that run on every change; the harness
   cross-checks mechanics against Python-exported draw streams via replay
   (`ScriptedRandom`) rather than a ported generator.

---

<a name="where-to-find-it"></a>
## Where to find it — settings and mission badges

Options window → **UI** tab, bottom section — exactly **five rows**:

| Row | Values | What it does |
|---|---|---|
| **TACTICAL AI** | OFF / ON (**default: ON**) | Master switch. OFF = the app behaves exactly as classic Imperial Admiral, including the random activation draw — bit for bit. |
| **DIFFICULTY** | Recruit → Stormtrooper → Veteran → Commandant → Thrawn (**default: Thrawn**) | How well the Imperials *choose* between their legal actions. It never changes stats, ranges, action counts or dice — only decisions. |
| **EXPLAIN** | NONE (**default**) / FULL | The AI's reasoning texts: FULL prints the "why" lines (target ranking, risk posture, focus change) on the activation pop-ups; NONE keeps the pop-ups purely operational. |
| **INFO TEXT** | ON (default) / OFF | The short rules reminder under the action texts on every pop-up (one attack per activation; opening a door costs an action; guard pop-ups additionally define the guard zone). |
| **PERSONA** | none + six commanders | Picks your Imperial commander doctrine for all campaigns (can also be set per campaign at setup; see [personas](#command-personas)). |

Defaults live in `Core/TacticalAISettings.cs` (`DefaultEnabled`, `DefaultSkillLevel`,
`DefaultExplainLevel`, `DefaultInfoText`). A default only applies **when nothing is
saved yet** — your stored settings (PlayerPrefs) always win, so an existing setup
never flips underneath you. Turn Tactical AI off and the app is classic again: the
legacy code paths are kept verbatim, not patched around.

On top of the global rows, every mission in a campaign list and the mission setup
panel carries a teal **level badge**. Clicking it cycles:

**AUTO → OFF → Recruit → Stormtrooper → Veteran → Commandant → Thrawn → AUTO**

- `AUTO` (default): the mission follows the global DIFFICULTY row.
- A named level pins **this mission** to that difficulty regardless of the global row.
- `OFF` forces this single mission back to the classic random behaviour — handy for
  an A/B comparison round.

*(Per-mission selection: the badge cycle lives in `MissionItemPrefab.OnAiModeClicked`,
stored in the campaign's `aiMode` field and honoured at setup (`SagaSetupOptions.cs`);
badge labels ship in all 11 app languages.)*

---

<a name="activation"></a>
## Activation — how the round runs

Legacy IC2 asks: "roll the activation spinner, and draw an instruction for whoever
comes up". Tactical mode replaces both halves with planning
(`Core/OrderPlan.cs`, driven through `TacticalAIBridge.cs`):

- At mission/round start the planner computes the **activation order** for the round
  — a deterministic sequence that fits the mission posture (e.g. heavy groups
  before their targets can slip away, blockers before the attacks they enable,
  would-be finishers last).
- The plain officer's "Order" (+2 movement only) never earns the head of that
  order — neither through the buff chain nor through its own score. Someone must
  hand out the movement points *before* the attackers run, but the plain +2 is
  too thin an order to open the round; the executive officer's order is not that
  strong until it can also grant an attack, so he *may* lead. (Reaction duties —
  dashing to an objective a hero is working — still outrank both, whoever holds them.)
- Which group activates when is **reproducible and inspectable**: the same seed
  gives the same plan, and the pop-ups run exactly that plan — "the deck hated me"
  is no longer a thing.
- Reinforcement and attack-point economics follow the printed threat budget of the
  mission; the skill level decides how wisely the budget is spent, never how big it
  is (see [Difficulty](#difficulty)).

### The golden rule of the table: flip the activation chip at the END of the hero's turn

> **This is the one thing the hero side must get right — every mechanism in
> Imperial Admiral's tactical mode depends on it.** A hero's turn ends like
> this: move, act, roll the dice, **announce the damage, remove the kills,
> announce every interaction** (doors opened, terminals used, objects touched,
> saves made, order effects taken) — and **only then flip that hero's
> activation chip** (the activation checkbox on the hero's squad-HUD card).
> Not at the start of the turn. Not halfway through. Never forgotten.
>
> **Why:** the flip is the single public signal that says *"this hero's turn
> is over now."* The app writes exactly one mission-log line when you flip it,
> and the tactical AI reads the mission log the way a human Imperial player
> reads the table — events written *before* your flip belong to that hero.
> That is how the AI knows: who opened which door (the door credit), who shot
> the objective's troops (the strike credit — and that hero then becomes the
> one the plan focuses on: the marker follows the fighting, not the errands),
> who the suspected shooter of a retaliating unit is (the closer law), where
> the hero is likely to be (the section belief), and when a hero's activation
> window closes.
>
> **If you flip too early**, the AI credits the events of that turn to the
> *wrong* hero and it will genuinely look "very dumb" — it only knows what the
> log says. **If you never flip**, the AI literally does not see that the hero
> acted: no retaliation attribution, no section observation, no focus shift.
> One flip per hero, per turn, at the end — that is the whole deal.
>
> The flips also appear as regular lines in the in-game mission log panel
> (same kind of line the imperial activation pop-ups have always written
> there), so you can always check what the AI saw. The game reminds you at
> both ends of the turn: every tactical activation pop-up's INFO line states
> this rule (hideable with the INFO-TEXT setting), and hovering a hero's
> activation checkbox shows the same hint; legacy missions show neither.

Mission-authored figures — the special ones a scenario spawns for you (a named
villain like Ivan Talos, custom bosses, mission troopers) — are planned
tactically too: the mission file states which printed card each of them
represents, and the planner uses that role for its doctrine dice while all
values, orders and text stay exactly the figure's own printed card. Their
activation pop-up also shows their mission-authored rule lines (bonus
sentences and printed abilities, in your mission language) under a
"Sonderregeln dieser Figur" header, right below the action tree — public
sheet text, so the fairness rule against hidden bonus rolls stays untouched.
Only truly
foreign groups (your own imported fan characters) still fall back to the legacy
instruction draw; the rest of the mission still runs tactically around them.

---

<a name="the-activation-pop-up"></a>
## The activation pop-up — how to read and execute an order

Each activation shows one pop-up, assembled from printed facts
(`TacticalAIBridge.cs`; texts from `Languages/<lang>/ui.json`, keys
`uiTacticalAI.*`). Top to bottom:

1. **Focus line** — which hero the Imperials are concentrating on this round, by
   name, on **every** activation pop-up at every difficulty. With EXPLAIN = FULL
   you also get the *reason* ("highest firepower among the remaining heroes",
   "the mission objective names him", "finishing him counts double", "mission
   goal 'wound all heroes' orders the healthy").
   Deliberately public: a human Imperial player has this information too.
2. **Doctrine line** — the role this figure plays in this mission, named in plain
   words directly under the focus line, e.g. `Wachschutz (Zone halten, Wege
   versperren, nie herauslocken)`. Shown always, independent of the EXPLAIN level.
3. **The action blocks — an if–then decision tree** (see
   [Instructions](#instructions)). Two action slots per activation, at most one an
   attack unless the figure card prints more. The tree is *printed rules + priorities*, rendered as
   conditions you evaluate at the board.
4. **Optional third block — printed villain abilities** (Commandant and up). When a
   villain card prints a special action (Force Choke, Bombardment…), it appears as
   its own block with the printed condition text. The block proposes; it never
   overrides the printed rules, and you confirm the condition at the board.
5. **Explanation line** (EXPLAIN = FULL): the reasoning behind this activation —
   target ranking, risk posture, focus change — right on the pop-up.

**How to execute it well** (the mode assumes you play *for the Empire* exactly as
printed):

- **Work the tree top-down.** The first condition that is true on your real board
  **is** the order; everything below it only applies if the lines above are false.
- **One attack per activation** unless the figure card prints more — and give it to
  the announced focus target, not to whoever is closest.
- **Movement serves the fire plan.** Advance figures close until the focus target
  is in range; Guard figures mostly *don't* move — holding the objective zone or a
  chokepoint beats one more step.
- **Doors cost an interaction action** — factor that into who goes where.
- **Emplacement (Stativ/Tripod) figures** (e.g. E-Web, DG007) follow their printed
  exclusivity: movement *or* attack per activation. They lead with the attack and
  remind you that moving forfeits the shot.

### The threat numbers next to the heroes — what exactly is computed

The focus banner's `threat=` value is **not** the printed card cost and not a
standing score. It is one freshly computed number per round, per hero:

The number is the **weapon score** of the hero's *best single attack*: the
expected damage exchange of his best weapon's dice (card dice **plus every
attack die his bought campaign weapons print** — dice from `
tactical_hero_cards.json`, never the printed attribute pools), **plus a
price-energy surcharge**: the weapon's printed shop **tier × price**, at the
campaign curve's own rate of 1/1000 (an E-11 at 400 CR/tier 1 adds 2/5 of a
die per round; gloves and armour add nothing — they add no damage). Price is
100% public data (shop + purchase log), and it is the signal that catches
what the dice tables cannot see: energy abilities and bonus dice that are
priced into the weapon but not printed on it. The model evaluates each
candidate weapon die by die against the current defense and takes the
highest-scoring one — the Empire may never under-estimate an equipped hero.
Since M29 (06.11., "Zwei-Ökonomien-Bande") each distance is ONE attack with
the ONE strongest weapon that can fight there: ranged candidates compete for
the gun slot once (argmax over the band — the best carried gun wins, no
per-distance gun switching), while melee options (P-hero base attack, melee
items, attribute-bound pools like the lightsaber's Einsicht pool) score
their own coverage cells under the melee law (no distance accuracy spend)
and take a cell only when strictly stronger. The operand is a mean over
distances — never a sum over weapons.
- **Plus exactly +1** for the *objective actor*: the hero who finished the
  activation in which the mission objective moved (public event credit).
- **Mission posture decides the formula.** When the Imperium is ordered to
  *hold* (Guard/Block-style postures), threat is the percent law:
  `100 × (60% × score ÷ 8/3 + 40% × speed-band ÷ 3)` — damage first, then
  tempo, each over the game's own printed maximum (the strongest printed
  exchange EV is 8/3 — an item weapon; band 3 is the fastest band). Reach
  is not a third factor: it is already priced into the score as the
  **band average** — every distance 1..reach is paired once, each distance
  with the strongest weapon that can fight there (M29: gun slot by argmax,
  melee overlay where strictly stronger), and every distance nothing can
  fight at counts as 0, so wide weak bands dilute while melee pools pay no
  per-distance accuracy toll.
  In *kill* postures the band average stands raw. Same heroes,
  same score, different emphasis — the focus call names the formula it used.
- **Printed card effects count exactly as printed.** A blast/explosion card
  adds half its printed value per round in hold postures (Fenn's Havoc Shot,
  raised by its printed upgrade card); printed tempo cards raise the hero's
  speed band in the healthy AND the wounded state; cards printing an extra
  attack (Gideon's "Hammer und Amboss" / "Meisterleistung") and Loku's
  recon-token synergy ride on as half of one neutral attack each; static
  accuracy prints (Loku's All-Weather Rifle +1 and "Clear-Minded" +2,
  CT-1701 +2, Saska +1, plus the equippable "Spezialeinsatzkräfte von Mon
  Cala", "Meisterschütze" and "Schuss über Bande") pair with the dice
  through the same keyword seam items use. State-honest: an ability printed
  only on the healthy card side ends with the wound — the wounded side of
  Loku's card prints no abilities at all, so his recon marker AND his +2
  accuracy are gone, while his weapon's +1 rides in both states. The
  reference print decides, never a guess — and every one of these effects
  prints as its own clause in the EXPLAIN cell.
- Wounded heroes are evaluated with their wounded dice faces **and** their
  printed wounded tempo (a wound usually drops the speed band — no separate
  penalty is applied; the printed rule already carries it).
- **Under a wound objective the printed roles order the healthy heroes.**
  When the mission demands *all heroes wounded*, the Empire first reorders
  the still-healthy candidates: heroes carrying a printed healer kit first,
  then printed supporters, then the fastest movers, then raw damage. These
  role flags never enter the threat number — they only order it (see the
  glossary entry "Wound objective").
- **When two heroes land on the same number**, the better-invested hero
  still leads: the tie-break compares what the campaign actually *spent* on
  each — every bought item weighted by its printed **tier × price**, then
  the XP sum of bought abilities — before any squad-order fallback.

Since this build, the game log (`Player-N.log`) accounts every ranked hero on
its own line: final threat, the exact formula with the real fractions
("55% × 7/3 per round (Green+Green + energy bonus 2/5) ÷ 8/3 + 35% × 3/3 +
10% × 5/7"), his dice pool, his equipment items and the flags
(mark/wound/strain) that moved the number — so you can re-check each sum
yourself instead of trusting it.

---

<a name="instructions"></a>
## Instructions: the if–then trees

The order text of every activation comes from a **pattern tree** keyed by the
figure's doctrine (`TacticalAIBridge.PatternTree` → `ui.json` keys
`apTreeGuard`, `apTreeBlocker`, `apTreeSupport`, `apTreeAmbush`,
`apTreeAggressive`, `apTreeTripod`, `apTreeNoTarget`). Three rules govern them:

- **Conditions, not commands.** Every line is "on your board, if X then Y". The app
  never claims X is true — you check X on the board.
- **Printed limits only.** Move distance, range and legality come from the card and
  the mission's threat budget. Trees never ask for more than the figure prints.
- **Guard-zone discipline.** Guard trees keep figures inside or entering the guard
  zone ("Wachzone" — the mission's objective area, see the glossary; *you* identify
  it on the board from the mission, the app never claims its tiles); they offer
  blocking another hero's path to the objective as an alternative to chasing.
  Every guard popup opens with a one-line Wachzone *definition* header (Playtest
  Player-19: the plain tree printed the term unexplained when INFO-TEXT is off —
  it now carries the same definition line the officer trees always did; the INFO
  line still adds the "extent comes from the board" note when INFO-TEXT is on).

Under the hood the same priorities drive the planner itself (which candidate targets
a group may consider — `Core/Patterns.cs`), so popup and plan agree; the popup is
the plan translated back into board language.

---

<a name="officers"></a>
## Officers and their commands

Officers are the Empire's remote control — their orders go to *another* figure
(`apTreeOfficer<Pattern>` / `apTreeOfficerElite<Pattern>` trees):

- **Officer (DG004/DG005)** — "Order": choose another friendly figure within 2
  spaces. In skirmish it gains 2 movement points; in campaign the shipped prints
  differ (De: perform *a turn*; EN: perform *a move*) — an open wording question,
  and one reason the AI never plays the plain command as a timing instrument. The
  head-slot law stands either way: a plain-command officer never leads the round.
- **Executive Officer (DG006, elite)** — "Executive Order": choose a friendly
  figure within 2 spaces. It interrupts immediately to perform **one move OR one
  attack** (both prints agree: a full move action or a full attack — the older
  "2 movement points" wording here was wrong, corrected 04.11) — one order per
  command, never both; the order consumes the officer's own attack point.
- Which figure, and which target / which direction, is chosen at the board through
  the pop-up questions. The officer's own fragility (and its cover trick of
  re-rolling a defense die next to a friendly figure) is printed on its card and is
  *your* consideration — the orders never lecture you about it.

**Command timing (level 4/5).** Since Player-24 the app slots the executive
officer's activation exactly where the command pays: a hero stands in the chosen
receiver's area (fresh fact anchor) → the troop activates first and the command
follows, paying the interrupt as a **second attack**; no hero there → the officer
leads and the command's move leg extends the troop's reach this round. The chosen
receiver is the troop *in contact* whenever the app has seen a hero next to one of
them this round (playtest law after Player-24: the commander orders the troops he
sees fighting — the globally strongest troop only gets the order when nobody is
engaged). The plan ranks by the SINGLE FIGURE you would actually order: the
ordered figure attacks with its own dice — a four-figure squad does not attack
with four dice under one command — so a crowd of weak figures no longer outscores
one strong card. The named
receiver and the reason ride on the officer's order card ("Befehlszeitpunkt …"),
and the card keeps reminding you: the command only reaches figures within 2
spaces — that check stays yours at the table. Since #1007 the decision is also
printed in the play log at every elite-officer activation (`timing DG…:` line).

A machine-enforced text gate (`tools/TacticalAiGate/OfficerTextGate.cs`, ~1,900
checks with mutation self-tests) makes it *impossible* to ship an order text that
contradicts these printed rules — an order asking for movement and attack in one
command turns the build gate red.

---

<a name="the-glossary"></a>
## The glossary — new words on the table

**Guard zone ("Wachzone").** The area of the mission in which the rebels must
fulfil their objectives or take their actions — revealing mission cards, interacting
with terminals, opening doors, touching objects; whatever this mission asks of them.
*That* is what the Empire guards: the Imperials know the rebels have to come there.
Guard-role figures hold it; blockers cover its approaches. (Do not confuse with the
figure keyword *Guard*, which pins an individual figure to a printed radius around
itself — related idea, different mechanism; both are on the cards.)

**Focus zone ("Fokuszone").** The area the rebels' *current* mission
objective points at — the sector with their main effort. Hunter-doctrine figures
treat it as their hunting ground: from tier 3 up they lock onto the hero who is
factually working inside it **or who is moving into it** — several candidates
competing? the one with the higher threat value takes it (EXPLAIN names him) —
take their movement paths there, and let heroes outside the zone *and not headed
into it* lie — even when one stands right on their path (the *hunter's
creed*; whoever attacks the hunter may still reap one shot back that round, but
nobody is pursued for it). Below tier 3 the hunter behaves like the old
Vorstoß — the lock is an earned discipline, not a printed constant. Unlike the
guard zone the focus zone is not held — nobody stands in it to deny it; it is
where the Empire goes hunting. It follows the mission's current objective, so it
can move when the objective moves. (Shown on the doctrine line of the activation
pop-up; see also the Guard zone above.)

**Focus (target).** The hero the Imperials concentrate on this round, named on
every activation pop-up (its reason is shown with EXPLAIN = FULL). Target choice
follows public criteria in a fixed, published order: the human's aim mark first;
then the **axis key** — when the mission's own win wiring names a rebel figure the
Imperium must defeat, he leads right behind the mark, explicit and independent of
what the objective text happens to word (the *mission axis*, November 2026; the log
names this basis `achse` — see [the mission axis](#mission-axis));
then the reaction keys (who shot the activating figure this round, the guard-zone
and retaliation keys, the supporter's trust section); then the **objective actor**
— a hero who is actually working the mission objective outranks every wound and
role law; then the wound-objective law below (healthy before wounded, healer-kit
carriers, supporters, movers, damage); then the threat number itself. Only when
all of that ties does the published fallback chain decide (campaign investment,
squad row, hero id) — deterministic down to the last tie, and nothing from
another game's rulebook decides your target.

**Wound objective ("alle Helden verwunden").** Some missions end the moment no
healthy hero is left — the printed mission text names it, the AI only reads that
text. While such an objective is in play, a hit on an ALREADY-wounded hero
advances the mission not at all, so the Empire attacks the healthy ones first and
the usual "wounded — finish him" economy is switched off; the EXPLAIN cell says
exactly which law ordered the row ("Missionsziel 'alle Helden verwunden'"). An
objective actor under fire stays the exception: work on the mission objective
still outranks the wound law.

**Threat budget.** The mission's printed resource for Imperial activations and
reinforcements. Difficulty decides how *wisely* it is spent (burst vs. held
reserves), never how much there is.

**Mission posture.** Every mission is classified from structured mission data only:
**defend** (the mission puts something to guard on the board — terminals, protected
targets) or **hunt** (the Imperials must find and destroy the rebels; classic
open-field missions). Posture tilts the doctrine dice (the defend
modifier row) and sets what the planner optimises (see
[doctrines](#the-doctrines)). Since the November 2026 mission-axis hop the
classification reads the mission's public **win wiring** first (the log's
round-stance line, `feedPosture=`, now starts from it — see
[the mission axis](#mission-axis)); the two stance words that share the log
(`feedPosture=` for the planner's round stance, `planPosture=` for the doctrine
dice layer) were deliberately renamed apart so one word never means two things.

**World feed.** The app's event log distilled into public information: activations,
deaths, removals, objective progress, the round clock. This is the only lens the AI
has; unknown event payload is rejected, not guessed (`Core/WorldEvents.cs`).

**Delta-0 (personas).** A persona's elite **replaces** its regular figure and is
booked at the regular's printed cost. Same figure count, same threat budget — the
persona changes *quality and behaviour*, never *quantity* (see
[personas](#command-personas)).

**Legacy mode.** Everything the app did before Tactical AI: random activation draw,
drawn instruction behaviour, hidden bonus rolls. Still fully present — switch or
badge back to it any time.

---

<a name="the-doctrines"></a>
## The doctrines — every role and what it does

Every Imperial figure — officers included, who just get their own order trees —
carries a battlefield **doctrine**. The live assignment law is a *printed dice
law*: once per round each figure rolls into a doctrine from the **public weight
table** `Resources/Languages/En/tactical_pattern_weights.json` (every shipped
card is pinned there by its **group id** — the table rolls the same
distribution in every language; the name-matching net only catches custom
imports; seeded deterministically from mission + card + round; the defend
posture is the one modifier row, and the emplacement print rule is the single
pre-roll exception; one protect law sits over the dice: while the mission
objective names a deployed Imperial figure — "Defeat Ivan Talos." — guard-role
groups hold their post and roll their row under the defend modifier, a
bodyguard at the mission figure does not march away) — shifting round to round like a real battlefield, yet
identical every time for the same seed. A final mix in the dice hash makes sure
the round number actually moves each figure's roll. Stance text
`Core/Doctrine.StanceText`; popup label `doc*` keys — the label appears in the
**doctrine line** directly under the focus line on every activation pop-up; German
labels as shipped):

| Doctrine | Popup label (DE, shipped) | Behaviour at the board | Planner fit (eliminate / stall / protect / deny) |
|---|---|---|---|
| **Vorstoß** (aggressive) | "Vorstoß (zum Kampf vorrücken)" | Press toward the fight; the workhorse of hunt missions | 1.5 / 0.8 / 0.7 / 1.1 |
| **Wachschutz** (guard) | "Wachschutz (Zone halten, Wege versperren, nie herauslocken)" | Hold the guard zone; clear only its own line, never be lured out; block heroes' paths to the objective (heavily tilted by the defend modifier) | 0.6 / 1.4 / 1.5 / 1.0 |
| **Blockierer** (blocker) | "Blockierer (Engstelle halten)" | Hold a chokepoint/lane, attach to and interrupt attackers (the dice rows favour large bases) | 0.8 / 1.5 / 1.3 / 0.9 |
| **Jäger** (hunter) | "Jäger (Fokuszone abkämmen — …)" | Sweep the focus zone; from tier 3 the Fokuszone lock: attack the hero working inside the zone OR moving into it (several candidates? the higher threat value), ignore heroes outside it who are not headed into it, move toward the zone (pop-up label carries its own definition — see the glossary) | 1.4 / 0.9 / 0.8 / **1.5** |
| **Support** | "Support (Distanz halten, aus der Deckung)" | Keep distance, fire from cover | 0.9 / 1.0 / 1.3 / 1.0 |
| **Hinterhalt** (ambusher) | "Hinterhalt (ungesehen bis zum ersten Schuss)" | Deny the point until a target enters range | 1.1 / 1.1 / 0.9 / 1.3 |

Reading the table (full mechanics in `MECHANICS_tactical_ai.md` §6):

- The defend-posture modifier tilts the dice toward Wachschutz/Blockierer (the one
  posture row in the table); hunt missions roll from the unmodified rows.
- Figure rows override the catch-all: IG-88 rolls 80% hunter / 20% Vorstoß
  ("Relentless" on the dice, not a hard pin).
- **Personas reshape the mix** (pattern weights ×1.15–1.35, guard reserves) — see
  [personas](#command-personas).

Two honest notes from the shipped code:

- **Two posture words, two layers (both public, both renamed P33-H4 so the log
  never shows one word with two meanings):** the doctrine dice layer runs on
  a structural *hunt/defend* state (logged as `doctrines planPosture=…` in Debug),
  the planner layer on a per-round *eliminate / stall /
  protect / deny* stance derived from the feed (logged as `feedPosture=… round=N`). A
  escort/infiltration mission can therefore honestly show `hunt` on one line and
  `stall` on the other — same mission, different layers, no contradiction.
- The **ambusher** is implemented (tree + planner weights) but currently unassigned:
  no shipped figure prints an Ambush keyword, so `ClassifyPattern` never hands it
  out. It is ready for custom cards that do.
- **Hunter vs. Vorstoß:** below tier 3 they deliberately share one tree (same
  board behaviour, the honest S1/S2 brawler); from tier 3 the hunter plays its
  own pop-up tree and the **Fokuszone lock** (`SectionConsumers.FokuszoneHeadFor`):
  target = a hero factually anchored in the focus zone **or moving into it**
  (the shipped M14d doctrine text names both candidates; several competing?
  the higher threat value takes it, EXPLAIN shows him), the ignoring covers
  only heroes outside the zone who are not headed into it, retaliation
  outranks the lock (fact beats brand). The plan-side head pick itself is
  unchanged: it still pins the head on the hero factually anchored *inside*.
  The old
  planner-side target override (`Core/Patterns.TargetOverride`) stays exactly
  what it always was — a sim-level preference.

---

<a name="difficulty"></a>
## Difficulty — five levels, no hidden power

Difficulty in Tactical AI is **play strength, never hidden power**. Every level
plays with the exact same legal options, the same printed stats and the same threat
budget the mission gives the Imperials. What changes is *how well the Imperials
pick* among those options. In board terms: same army, different general.

*(Implementation: one decision brain, five decision-quality layers —
`Core/SkillProfile.cs` (skill layer) + `Core/OrderPlan.cs`; internally S1–S3 run the
"reference" profile and S4–S5 the "overlord" profile. The DIFFICULTY row and the
badges are the only knob; the profile split is not a setting. Default: **Thrawn**.)*

**Recruit (S1)** — the old "automaton". Focuses on whoever is closest or first in
its own bookkeeping; ignores the wound marks on your hero cards (so it rarely
finishes a weakened hero) and concentrates fire only by accident. Spends the whole
threat budget immediately, moves to the first legal position, reacts to what the
rebels did two rounds ago. Honest and forgiving — first runs and younger players.

**Stormtrooper (S2)** — still ignores wound marks, but usually picks one of the
two *sensible* targets, with a fumble about one activation in three. Movement
becomes greedy approach; the budget is mostly held for later activations. Reliable
pressure, no crossfires.

**Veteran (S3)** — the turning point: **reads the wound/defeat marks on your hero
cards**. Deliberate focus fire unlocks: wounded heroes get finished first, the
highest-firepower hero draws shots, and the named focus target appears on every
activation pop-up. One-round reaction delay, shortest-route approaches without dodging
fire lanes. From here the Imperials feel like a competent squad.

**Commandant (S4)** — professional tactics: two-round planning horizon on the
movement envelope (they race your *options*, they don't read your mind), rare
fumbles, full planner movement (zone control, covering doors and lanes, threat
budgeting). Villain special abilities now appear as third popup blocks when their
printed conditions fit.

**Thrawn (S5)** — everything above plus the complete risk judgement: every option
is weighed against the full threat model (the Imperium's internal **threat
currency** — budget spent on the table, replacement value of wounded groups; a
planning number, not between-mission money). Expect zone
control *before* you reach the objective, kept-sight-lanes for fire, wounded heroes
hunted to the death, and no wasted actions. "Play it like the books say" — the top
of the ladder, and the shipped default.

Difficulty never adds figures, never cheapens costs, never touches dice. A Recruit
mission and a Thrawn mission contain the same Empire — one plays it sloppily.

---

<a name="command-personas"></a>
## Command Personas — six Imperial commanders

A **persona** is a named Imperial commander whose doctrine bends *how* the Empire
plays a mission — priorities, guard behaviour and its signature elite. Pick one
globally (Options → UI tab, "PERSONA" row) or per campaign (campaign setup); the
badge chip shows the rank (rank and signature surface in the settings row, the
campaign persona popup, and the mission setup badge). Persona *texts* ship in all 11 languages (keys
`personaCapt*` etc. in each `Languages/*/ui.json`); the persona *structure* (ranks
and effects) is one shared file (`Languages/En/tactical_personas.json`).

| Persona (DE name keys `personaCapt*`) | Doctrine flavour |
|---|---|
| **Sturmtruppen-Kapitän** | disciplined lines, forward cover, defensive pinning |
| **Feldherr** (Warlord) | relentless pressure, guard reserve that still advances |
| **ISB-Kommandant** | surveillance patterns, shock, sharp target priorities |
| **Hutt-Kriegsfürst** | bounty/beast priority doctrine — pattern pressure, escalation as business |
| **Droiden-Direktor** | suppressive droid fire, "steel obeys"; late elite escalation |
| **Großmoff der Unterdrückung** | garrisons hold first, veterans enforce, fear closes in |

**Ranks rise with danger.** Each persona has **ranks 1–4**, unlocked by the
mission's threat level at thresholds **2 / 4 / 6 / 8** — the display shows
"Rang n/4". Effects unlock rank by rank:

| Rank (threat ≥) | Unlocks |
|---|---|
| 1 (≥2) | signature **Delta-0 elite swap** (exceptions: Hutt opens with extra pattern pressure, Großmoff with a guard reserve) |
| 2 (≥4) | stronger **pattern weights** (×1.15–1.35 toward the persona's doctrine) |
| 3 (≥6) | **guard reserve** shifts and/or more weight (Großmoff: his first elite swap here) |
| 4 (≥8) | the persona's **signature channel** fires (some add a further elite swap) |

The exact per-rank effect mix is data, not prose: `tactical_personas.json` is the
source, pinned by a 144-vector parity canon (`tools/TacticalAiHarness/SectionsPersonasSwap.cs`
+ fixture `personas_swap.json`).

**How a rank works (the law, from `Core/Personas.cs` `Resolve`).** A persona's rank
is the **highest authored rank whose threat threshold the mission's threat pool has
reached** — and the effects are **cumulative**: you always play the union of ALL
unlocked ranks' effects, in authored order (a later guard split supersedes the
earlier one it escalates). Threat pool below 2 means **rank 0: the persona is
silent** — plain rules, no persona effect at all. All six personas unlock at threat
**2 / 4 / 6 / 8**. The matrix below is the shipped fleet, read back against
`tactical_personas.json` (swap targets named by their deck group, as the table
promises):

**Sturmtruppen-Kapitän** — ranks 1–4 at threat 2/4/6/8.
- **R1:** Elite swap — one regular Stormtrooper becomes **Stormtrooper (Elite)**,
  at the regular's printed cost.
- **R2:** Aggressive pattern weight ×1.2 (Stormtroopers).
- **R3:** Guard split 25 % (Stormtroopers) — a guard reserve forms.
- **R4:** Guard split escalates to 40 % (Stormtroopers) + the persona's signature
  text fires.

**Feldherr** (Warlord) — ranks 1–4 at threat 2/4/6/8.
- **R1:** Elite swap — an Imperial Officer becomes **Imperial Officer (Elite)**.
- **R2:** Aggressive weight ×1.25 (Imperial Officers).
- **R3:** Aggressive weight ×1.15 (ALL Imperial figures) + guard split 20 %.
- **R4:** Guard split escalates to 30 % + signature text.

**ISB-Kommandant** — ranks 1–4 at threat 2/4/6/8.
- **R1:** Elite swap — a Probe Droid becomes **Probe Droid (Elite)**.
- **R2:** Ambusher (Hinterhalt) weight ×1.35 (all Imperial figures).
- **R3:** Guard split 30 % + blocker weight ×1.2 (E-Web Engineers and the droid
  roster: probe/sentry/assassin droids).
- **R4:** Aggressive weight ×1.25 + signature text.

**Hutt-Kriegsfürst** — ranks 1–4 at threat 2/4/6/8.
- **R1:** Hunter weight ×1.15 on the persona's *notables* list (the bounty,
  villain and officer names the JSON names: Boba Fett, Bossk, Dengar, Greedo,
  IG-88, Jabba, Vader, Thrawn and kin).
- **R2:** Hunter weight ×1.3 on the *beasts & hired guns* list (Rancor, Wampa,
  Clawdite, Gamorrean, Trandoshan, Tusken, Hired Gun and kin).
- **R3:** Hunter weight ×1.2 (all Imperial figures).
- **R4:** Ambusher weight ×1.2 + signature text — escalation as business.

**Droiden-Direktor** — ranks 1–4 at threat 2/4/6/8.
- **R1:** Elite swap — a Probe Droid becomes **Probe Droid (Elite)**.
- **R2:** Guard split 25 % + blocker weight ×1.2 (E-Web Engineers and the droid
  roster).
- **R3:** Aggressive weight ×1.3 + guard split 30 % (the droid & heavy roster:
  probe/sentry/assassin droids, E-Web Engineers, AT-ST/AT-DP, jet and heavy
  Stormtroopers, repulsor tank).
- **R4:** Elite swap — an E-Web Engineer becomes **E-Web Engineer (Elite)** +
  signature text (the late elite escalation of the doctrine).

**Großmoff der Unterdrückung** — ranks 1–4 at threat 2/4/6/8.
- **R1:** Guard split 30 % — garrisons hold first.
- **R2:** Guard pattern weight ×1.2 (all Imperial figures).
- **R3:** Elite swap — one regular Stormtrooper becomes **Stormtrooper (Elite)**
  — veterans enforce.
- **R4:** Elite swap — an E-Web Engineer becomes **E-Web Engineer (Elite)**,
  guard split escalates to 40 % + signature text — fear closes in.

Rules that always hold (verified in `Core/PersonaSwaps.cs` + the swap canon):

- **Delta-0:** the elite *replaces* the regular figure; cost stays the regular's
  printed cost. No extra figures, no discounts — missions stay exactly as balanced.
  Every elite swap in the matrix above works under exactly this law: the swap
  *replaces*, never adds, and never rolls a hidden second copy into existence.
- **One elite per name (persona swaps):** the swap never puts a second copy of
  the same elite on the board; the swap
  only happens if the elite card exists in your activated collection and is in the
  deployment hand; unmet conditions deploy the plain regular — harmless.
- **No expansion is required to be available:** all six personas draw on base-card
  pools; missing expansions simply make swaps unmet (regular deploys).
- The persona never adds figures, never rolls hidden bonuses, never sees a hero
  card. It changes *priorities*, exactly like a different human commander would.

---

<a name="every-difference"></a>
## Every difference from the legacy mode

| Aspect | Legacy (classic IC2) | Tactical AI |
|---|---|---|
| Activation | Random spinner per round; who moves next is luck | Deterministic planned order from the mission posture and threat budget; reproducible per seed |
| Orders per activation | Drawn simple instructions | Printed if–then trees keyed to the figure's doctrine, evaluated by you on the board; focus + doctrine shown up top |
| Difficulty | One behaviour for everyone | S1–S5 ladder = decision quality only; per-mission badges |
| Hidden power | Legacy hidden bonus-effect rolls can fire | Never fire in tactical missions; printed stat cards only |
| MedPac | App convenience HUD available | Switched off for tactical missions (wounded stay wounded in-mission; between-mission recovery unchanged); crates/rewards stop dispensing them |
| Massive figures | Some printed restrictions relaxed | Printed restrictions enforced: no interiors, no two Massives in one space; every proposed move is legality-checked first |
| Reinforcement entry | Rolled/drawn deployment points | Deterministic **scored** deployment choice: mission anchors (the mission file's own points *plus* sections the mission activates while playing), battle-zone distance, a friendly leash toward the Imperium's own located groups, and DP-diversity so landings stop piling on one point (same seed → same landing zones) |
| Imperial growth | Baked-in convenience growth | Unchanged — no Imperial purchases in this release (see [what this release does not include](#not-included)) |
| Wound status | Cosmetic in places | Machine-grade input: from Veteran up the AI *reads your marks* and finishes wounded heroes |
| Officers | Passive stat figures | Command figures: 2 movement points (Officer) or movement **or** attack (Executive Officer), gated by a machine-checked rule text |
| Personas | — | Six commanders, ranks 1–4 by mission threat, doctrine bending + Delta-0 signature elites |
| Explanations | — | Optional reasoning lines on the pop-ups (EXPLAIN = FULL) |

### What does NOT change

- **Classic mode is untouched, forever.** With the master switch (or a mission
  badge) OFF, the app runs the legacy paths verbatim — the same activation draw, the
  same instructions, bit for bit. (Tactical ON is the new *default*, so you see the
  new mode first; one click returns to classic.)
- All printed mission rules, events, XP, credits and the campaign manager work as
  before.
- **Custom/embedded fan cards** outside the shipped set keep the legacy pop-up and
  legacy activation draw; the rest of the mission still runs tactically.
- Dice remain *your* dice: the AI never touches them (its own RNG stream is for
  planning-internal decisions only, seeded per campaign).

---

<a name="not-included"></a>
## What this release does NOT include

- **No Imperial purchases between missions.** The printed-campaign growth machinery
  (Imperial class deck, agenda market, influence ledger) exists in the code and is
  machine-tested (`Core/ImperialDecks.cs`, `Core/AgendaMarket.cs`,
  `Core/ImperialPurchases.cs`) — but it is **not wired into this release**: there is
  no IMPERIUM panel, no upgrade stage, no Imperial XP income
  (`MissionEventHandler`: the hook logs *"imperial XP retired with the persona
  system, skipped"*). The Empire you face grows only from what the mission and the
  campaign data provide — its flexibility between missions comes from the
  [personas](#command-personas), not from purchases.

---

<a name="tips-and-limits"></a>
## Tips, limits and honest admissions

- **Don't flip the master toggle mid-mission** — planning is per round; a clean mode
  change belongs between missions.
- **Deploy one card as one group.** Planning state is keyed per card id; the same
  card deployed as two separate groups is planned twice (known limitation). Normal
  squad sizes on one card (2+3 stormtroopers) are fine.
- **No line-of-sight model** — by design. The AI never peeks through walls, but
  it also cannot verify your sight lines. That is *why* you enforce sight lines at
  the board; the pop-up asks, the board decides.
- **Hero status stays manual.** No HP tracker: you mark wounded/defeated on the hero
  cards as the printed game does; the AI reads exactly those marks — nothing else.
- **The Imperial damage dials keep their numbers (07.11, fixed).** Earlier builds
  could snap an Imperial group's wound dial back to zero mid-fight when a popup
  re-showed a card while a dial container closed and wrote its empty wheels back —
  a fallen figure "came back" and the numbers shuffled onto the wrong slots. The
  dial now commits exactly once per opening and always reads back from the board's
  own card object (`Core/TrackerBindLaw.cs`). The bug was equally real in classic
  mode, so classic mode got the fix too — that is a bugfix there, not rule drift.
- **Feed fallback.** On the rare missions where the world feed isn't active, high
  levels fall back to the reference brain; the log says so instead of faking.
- **Ambusher doctrine** is implemented but unassigned in shipped cards (no printed
  Ambush keyword yet); ready for custom groups.
- **Hunter vs aggressive** share the board instructions on purpose; the difference
  lives in the planner (see [doctrines](#the-doctrines)).

---

<a name="links"></a>
## Links to the original IC2 documentation

Tactical AI only changes *how the Imperial side is run*. Everything else — installs,
mission play, the campaign manager, the editor — is documented upstream:

- **IC2 Wiki (Home):** <https://github.com/GlowPuff/ImperialCommander2/wiki>
- **Getting Started & Basic Settings:**
  <https://github.com/GlowPuff/ImperialCommander2/wiki/0.-Getting-Started-and-Basic-Settings>
- **FAQ:** <https://github.com/GlowPuff/ImperialCommander2/wiki/7.-FAQ>
- **Tips & Tricks:** <https://github.com/GlowPuff/ImperialCommander2/wiki/8.-Tips-and-Tricks>
- **Editor documentation:** <https://github.com/GlowPuff/ICEditor/wiki>

Within this fork: the mechanics reference
[`MECHANICS_tactical_ai.md`](MECHANICS_tactical_ai.md) (planner internals) and the
concept document [`CONCEPT_fair_imperial_ai.md`](CONCEPT_fair_imperial_ai.md)
(the design rules this mode must never break).

---

*Page status: written against the shipped `main` tree of this fork (product branch
since November 2026), current through the P33 chain of 07./08.11 (mission axis and
object polarity, procurement ranking, scored landings, entry ledger, reaction zones);
every claim
carries a file pointer so reviewers can verify it. If something here and something on
a printed card disagree — the card wins; report it to us.*

---

<a name="reactive-layer"></a>
## The reactive layer (November 2026)

The Fair Imperial AI now *reacts* to what happens inside a round instead of
only executing a standing plan. What players feel, with the file behind each
claim:

- **Shots are remembered.** When an Imperial group is hit, the shooter is
  attributed from public facts, never a readout of positions: the damage
  arms a credit on the struck group, and the hero whose activation closes
  that credit in the same round is the shooter (the *closer* law, M14a;
  the older activation-window attribution survives with precedence where
  it fires). That hero is ranked ahead for the attacked group's next shot
  — the *reaction key* in the target ranking
  (`WorldEventsUnity.cs`, `OverlordFocus.cs`; 11-language player texts).
- **Every click re-decides.** Pressing "activate Imperials" re-ranks the
  REMAINING figures from what has happened since the last click. No
  interruption feature, no switch — always on (`ReactionOrder.cs`).
- **Last-seen ledger.** The AI keeps symbolic anchors — where a door was
  opened, where an objective was touched — as memory, never as hidden
  coordinates (`WorldEventVector.cs`).
- **Officers under pressure.** Supporter-flagged Imperials run an emergency
  cascade that picks Keep vs. Notfall (emergency) behaviour trees with a
  visible why-line; their pattern dice split into supporter rows, and
  advance/leash laws bound how far they push and when they fall back
  (`OfficerDoctrine.cs`, pattern tables, popup trees).

All of it is deterministic keys in the existing ranking: at the lowest skill
levels the behaviour is bit-for-bit the pre-November canon (golden vectors
pin this). No new switches exist — the skill level alone decides how much of
the reactive brain is used.

---

<a name="section-space"></a>
## The section space (November 2026)

The Imperials now also think in **sections of the board**. Every figure that
enters the game belongs to a section of the map, and the AI keeps a
belief — built only from public events — about which sections heroes are in.
What players feel, with the file behind each claim:

- **Mission-critical work goes first.** When a unit is working at a
  mission-critical object (the mission's deployment-objective data,
  `tactical_mission_critical.json`), it activates early in the round and the
  focus line reads "— X guards the mission-critical object"
  (`SectionConsumers.cs`, popup key `ordExplainWachzone`). This fires at
  **every skill level** — reacting to facts is fairness, not an
  intelligence bonus.
- **Retaliation.** A unit that got shot at this round makes the suspected
  shooter its priority target: "moves against the suspected
  shooter" (`ordExplainVergeltung`). Every skill level. The attribution is
  the **closer** law (M14a): windowless damage arms a credit on the struck
  group, and the hero whose activation first closes that credit in the same
  round is the suspected shooter (`RoundEnd` clears it — table discipline:
  mark your damage *before* the round ends, a hit after the last hero
  activation of the round credits nobody). The old activation-window
  attribution is legacy: where it fires it still takes precedence, but in
  the live game that window never opened.
- **Fresh intrusions react first.** When a section activates with a hero
  already inside, the units placed there push immediately ("X pushed into
  the newly activated section — Y reacts first", `ordExplainThreeC`).
  Every skill level. When several exceptions compete, the order is fixed:
  reaction before guard duty before retaliation (`SectionConsumers.cs`,
  pinned by `section_consumers.json`).
- **The planner gets a map sense.** Its proximity bonus (see
  "Activation planning, in depth" in the mechanics doc) is now fed by the
  belief zones (`PlanZones`, `SectionConsumers.cs`) instead of standing
  empty as it did since day one.
- **Supporters stop guessing** at the higher levels (S3+). If the belief in
  a supporter's own section is trustworthy, it takes aim at the hero
  reported there ("— X aims into its own section", `ordExplainSupportMaske`).
  If the picture is diffuse, it **holds its section and names no target**
  ("holds its own section instead of guessing a target",
  `ordExplainSupportHalten`) — a supporter never fires at a guess. What
  counts as trustworthy is public reviewable data
  (`tactical_section_trust.json`, default 2/3), and a diffuse belief
  changes nothing at all: same behaviour, no new penalty (`SectionSkill.cs`).
- **Guard over chase** at S3+: while a hero works a mission-critical
  object, the posture clamp switches the units from hunting heroes to
  guarding the objective (`ordExplainClamp`).
- **Reaction zones** (added 07.11, skill 3+). The belief distils the hero
  sightings into one **threat zone**: the contested section if there is one,
  otherwise exactly one fresh ring around the latest sighting (several fresh
  rings without a contest = no goal, neutral). A unit whose believed position
  sits right at the edge of that zone gets a single fixed boost in the
  activation plan (×11/10, public in `Core/OrderPlan.cs`) and says so in its
  explain line: "moves toward *the named zone*" — or a purely relative
  wording when the zone has no clean printed name (never a section id, never
  a coordinate: `ordExplainNahe` / `ordExplainNaheRel`, cleaned section
  names per the name law). Missing distance knowledge is **neutral, never
  "far"**, and where the data is absent the behaviour is bit-identical to the
  previous canon (Null-Law, pinned by fixtures).
- **Entry points are remembered** (added 07.11). Every imperial figure rides
  a saved, load-safe placement ledger: start groups use the mission file's
  own deployment points, event reinforcements use the point their event
  names, and the heroes' blue entry field is mined per mission (134 of 138
  shipped missions carry a usable entry field; multi-field missions stay a
  *set* of possible entry sections — no guessing). A unit's spawn belief
  seeds on its own entry section, and placements the mission leaves as
  "None" stay honest unknowns. Player-visible effect: reinforcements arrive
  from the mission's printed deployment points, deterministic per seed
  (`SagaSession.deployLedger`, `SectionSpace.cs`; fixtures
  `deploy_ledger.json` + `dp_anchors.json`).
- **The landing point is scored, not guessed** (added 07.11, P33-H4). Which
  deployment point a new group enters through now reads the mission's live
  situation: points near a *mission anchor* lead — and besides the mission
  file's static anchors, every section the mission **activates while you
  play** anchors its whole section while it stays live. Inside that tier the
  plan scores distance to the anchor, distance to the battle zone (anchor
  sections plus the reaction zones above), then prefers a point whose section
  already holds Imperial groups (the friendly leash — concentrate, don't split
  a landing), then prefers an *unused* point over one the ledger already
  recorded (the Green-1 pile-up of the Player-33 playtest cannot recur). An
  unmeasured route is neutral, never "far", and every remaining tie falls
  onto the old deterministic name chain — a mission with no facts at all
  picks exactly as it always did (log: the `[dp-score …]` line names every
  candidate's parts).
- **Hero start anchors** (added 07.11, P33-H2). The heroes' own starting
  placement is public briefing data — everyone reads it off the same mission
  page. The app now records it as a ledger row the moment the live map exists
  (and back-fills saves that predate the law), and the heroes enter the AI's
  view right at their entry sections — healthy, having acted nothing yet,
  exactly what the table can see. First effect, and it is a big one: in round
  1 the ring and threat-zone machinery above finally has inputs for the
  *initial* heroes, so reaction zones and "moves toward the named zone"
  behaviour can fire from the first round — in the Player-33 log the whole
  machinery saw exactly one ring in the entire mission because only a
  mid-mission joiner had an anchor.
- **Nothing ever knows your coordinates.** The belief is section-granular:
  names, objects, time windows. A section log line containing a coordinate
  is a machine-checked bug (coord-free law, `section_trust.json`).

Honest edges: the data says which section a deploy point is in, not which
group occupies it — the belief seeds a *candidate set*, never an
accusation; section labels are app-authored names, not printed rules text;
and at S1/S2 the belief carries raw facts only, so behaviour there is
bit-for-bit the pre-section canon (pinned by fixtures).

---

<a name="procurement"></a>
## Procurement: buying is a decision now (November 2026, P33-H5)

The Imperium's money moments — the **Landings** and the **Reinforcements**
popups between rounds — used to follow a rigid ritual: always reinforce
first if any group legally can, then spend the rest on the most expensive
affordable card. Your playtests showed both halves of that ritual
misfire: fresh groups landed while veterans bled out in the fight, and
the treasury bought a big card down to an unusable remainder while a
bigger card stayed one round out of reach. The AI now *chooses*, from
public facts only (same army, same cards, same dice — only better
decisions):

- **Reinforcement or new landing?** Whichever act serves the fight better
  right now: a wounded group *in* the contested section is restored first
  (each lost figure it suffered counts while it stands near the fight); a
  fresh group that lands *into* reach of the fight leads while the
  doctrine is hunting. Distance is measured in sections over the observed
  section graph — the mission's win-condition axis counts highest, then
  the reaction zones; an unmeasured route is never treated as far.
- **The Imperium saves money — but only with a plan.** A big purchase
  that would leave less than the price of the cheapest still-buyable card
  is deferred when the saved threat provably reaches a *bigger* card next
  round. Saving aims at something or it doesn't happen at all — and it
  **never happens in the final-round threat dump**, where keeping
  unspent threat would break the printed mission rules. A round saved on
  purpose shows no warning: the coins are held, not lost.
- **Nothing changed for the rules you play.** At most two acts per round,
  the printed reinforcement and landing costs, the canReinforce mission
  overrides, the coin-flip optional deployments and the final-round dump
  all work exactly as before — the AI only reorders and optionally
  *declines* among the acts the rules already allow. With Tactical AI off,
  or in missions where the app has no battle-zone facts yet, the old
  ritual runs untouched.
- **When a purchase and a reinforcement share one round,** the popup
  shows both as a landing (new figures land — that is a landing); the
  reinforced veteran shows its current figures, and the debug log names
  every act with its score (the BUDGET line in Player.log).
- **A landing says LANDING** (P33-H1, Player-33). The Imperium may only
  reinforce units that legally still stand on the board. When the
  round-end *Reinforcements* popup finds none (nothing wounded enough, no
  group below its printed size), the printed fallback buys a fresh group
  instead — and that used to wear the upgrade outfit: an empty upgrade slot
  leaked placeholder art and an "enemy name" filler text onto the popup. The
  fallback now always presents as a **landing**: landing title, threat line,
  the bought group in a landing slot, and both upgrade containers hidden
  before anything becomes visible. No placeholder ever reaches your screen.

Honest edges: the ranking only ever speaks when the app actually holds
section facts — no facts, no opinion (this is the same Null-Law that kept
every earlier feature honest); the saving rule estimates next round's
income from the difficulty setting, and uses that estimate for nothing
except deciding whether saving is worth it; and a card the Imperium
declines today is a card it may well buy tomorrow — watch for it.

---

<a name="mission-axis"></a>
## The mission axis — win conditions, objectives, polarity (November 2026)

The Player-33 playtest caught the Empire sleepwalking through an escort
mission: the objective said *help this NPC escape*, the escape field was
right there, and the troops stood where the round-start plan had left them.
The honest diagnosis: **the app never read the mission's win conditions at
all** — it only ever read the objective *text*, and the text is written for
the rebels. That gap is now a law (P33-H4b/H4c; Core: `WorldEventVector.cs`,
`OverlordFocus.cs`, `SectionConsumers.cs`; adapter: `WorldEventsUnity.cs`).

**Win conditions are public data.** Your mission file already states, in the
app's own fields, which named figure's death wins the mission for which side,
which button at which escape field ends it, and which escort figure belongs
to the objective. The AI reads exactly those fields — reads, never guesses —
and missions whose wiring is not parseable behave exactly as before (the
Null-Law again). The round-stance line (`feedPosture=`, see
[the glossary](#the-glossary)) now derives from this wiring **first**, so an
escort-or-escape mission no longer misreports itself as a clock-stall mission.

**The hunt axis.** When the mission's own wiring says *the Imperium wins when
this named rebel figure dies*, that figure leads the focus ranking directly
behind the human player's aim mark — explicit mission knowledge, no longer a
lucky coincidence of the objective text naming him (the log's focus basis
`achse`). And a mission NPC with no card of his own (the joining allies some
scenarios hand the rebels) is now **priced from his real printed values** —
damage, health, tempo — instead of standing outside the threat economy with a
blank number, which is how Player-33's Draylen turned out unpriceable.

**Polarity — the objective text is the rebels' view.** "*Help* Draylen
escape" is what the rebel players read. Negated, it is the Imperial
doctrine: hunt the escorted figure, block his escape field. On an
Imperial-authored objective the same wiring flips the other way — the named
figure is *our* escort, and hunting him would be the polarity misread this
law exists to prevent.

**The block axis.** The zone the rebels must reach (escape/evacuation field,
its section as the mission file names it) is a battle zone. Guard and blocker
figures hold its **interface** — the connecting section of shortest *known*
distance between the axis figure and that zone, measured on the observed
section graph (an unmeasured route is neutral, never "far"). Stand-guards hold
the interface instead of trailing the fight.

**The plan updates mid-round.** An objective change, newly parsed win wiring,
or a hero/ally entering the board mid-round rebuilds focus *and* activation
order once, inside the same round, at the next activation (log line
`REPLAN rN trigger=…`). The reaction law — a unit that was just shot still
reacts first, that's a board rule — keeps precedence. With EXPLAIN = FULL the
Player.log row `ACHSE::` shows exactly what the engine read: axis type
(`jage:`/`block:`), actor, polarity, block zones, the resolved interface.

**Mission objects — and why nobody shoots the terminal.** Many mission texts
print board rules of their own: *"A figure can attack the barricade (Health:
5, Defense: …)"*. That marks a **potentially** attackable object — never
automatically an Imperial target. An object enters the Empire's decisions only
when the mission's own wiring derives **which side needs it destroyed or kept
alive**: claim/rescue buttons and wired event names, the objective channel, or
the outcome sentence at the wired end-event. Nothing derivable ⇒ the object is
**neutral**: nobody attacks it, nobody guards it — deliberately, that is not a
bug. (The editor's `failImperial`/`failRebel` flags are dead data — zero true
rows in the entire shipped collection — so they are never read.) This is why
your troops attack the protesters but walk past the terminal — and why the
Royal Guard stands next to *that* terminal instead: different polarity, same
law mirrored. The answer to the playtest question "why do they shoot X but
not Y" is always the wiring, and EXPLAIN names the rung that decided.

**An object can be the plan target.** When a *polar attack* object wins the
per-activation target slot, that slot reads `OBJEKT:<name>` (the reason line
explaining it appears with EXPLAIN = FULL) — only
for the chase doctrines (Jäger/Vorstoß), and only while no fact-key spoke
(reacting to incoming fire always outranks mission geometry). The round-wide
**focus** stays figures-only; objects never replace a named hero there. Among
candidates, a door **on the best route** rides first: *Tür sprengen* —
attacking the door — is offered as the route alternative to opening it, and a
visibly open door is never offered again. Otherwise: nearest known object,
ties by name order. Deterministic, no chance.

**The honesty boundary: the damage counter stays on the board.** Objects carry
a physical damage counter at your table, and the app cannot see any object's
running damage. So the recommendation only ever says *attack it* or *blow the
door* — never "almost destroyed", never a remaining-health calculation. You
report progress the printed ways: the door opens, the mission event fires.
Non-door objects' state is honestly reported as *unknown* in the log rather
than assumed.

**The mission's flashpoints become reaction zones.** Sections holding a
*polar* object (attacked or protected — the mission wiring says contested)
feed the belief zones described in [the section
space](#section-space), so guard, retaliation and proximity behaviour
concentrate where the mission's own machinery says the fight is.

Honest edges: the whole axis speaks only where the mission file's wiring is
parseable — missions without it run the pre-November canon bit-identical
(pinned by fixtures); the object texts are read from the canonical English
mission files only (the ten other language files are display text, and display
text never decides a law); and the `OBJEKT:` name plus its reason line are
currently raw dev tokens — a follow-up pass moves them into the eleven
language files like every other popup text (the internal `ziel=OBJEKT:<name>`
notation itself stays a log/debug vocabulary, not player prose).
