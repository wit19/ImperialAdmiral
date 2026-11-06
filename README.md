**Imperial Admiral** is a companion app for Fantasy Flight Games' *Star Wars: Imperial Assault*. The app attempts to eliminate the need for an Imperial player by taking over the task to deploy, manage, and control Imperial figures. It is directly inspired by FFG's own app *Legends of the Alliance* and the card-based *RedJak's Automated Imperial Variant*. It can be used to play all boxed campaigns, any side mission, and even custom missions.

**Imperial Admiral** is an independent continuation of *Imperial Commander* by [GlowPuff](https://github.com/GlowPuff/ImperialCommander2) — we carry forward the tactical AI work on that code base under our own name, with GlowPuff's blessing and distance (MIT licensed, see LICENSE).

To use **Imperial Admiral**, you need a physical copy of the core game as well as any expansions you wish to use. Otherwise, you won't be able to use this app.

Star Wars, the Star Wars logo, and all other logos, characters, artwork, stories, information, names, and other elements associated with it are the sole and exclusive property of Lucasfilm Limited. Star Wars: Imperial Assault and its expansions are © Fantasy Flight Publishing.

**Imperial Admiral** is a non-profit project, available free of charge. It is in no way affiliated with or endorsed by Lucasfilm Limited, Disney, Fantasy Flight Games or any of their subsidiaries, employees, or associates.
 

---

## This fork: Tactical AI

*The intro above is the original README text of [GlowPuff/ImperialAdmiral](https://github.com/GlowPuff/ImperialAdmiral), kept word for word. Everything below describes this fork; upstream's manual and video links sit at the bottom.*

**Goal.** An optional Tactical AI for the Imperial side that plays by the printed rules and plays fair. It reads only what is already on the table (mission data, entered damage wheels, public objective state), never buffs figures and never holds dice. Difficulty means decision quality: five levels, Recruit to Thrawn, all choosing among the identical set of legal moves. The AI commands and the human at the table acts: every figure receives a concrete if-then order, and every targeting decision stays re-computable from the formula the app itself prints.

**Status.** Playable end to end and in active weekly playtests. This fork follows the playtests, not a fixed roadmap: each playtest session ships as a tranche of fixes and refinements, and every release documents itself in its release notes. Releases carry the upstream version plus a build number (upstream `v.X.Y.Z`, ours adds `#N`); the newest one always sits behind the download link below. Live today:

- Tactical AI mode, ON by default in this fork; switching it off (globally or per mission) restores the original app's behavior bit for bit
- Skill levels Recruit / Stormtrooper / Veteran / Commandant / Thrawn, changing selection quality only, never stats
- Six Imperial personas with per-mission rank, six doctrines: Advance, Ambush, Blocker, Guard, Hunter, Support
- Mid-round reactions to what the rebels just did (retaliation, protecting the objective, meeting intruders) and officers who command other figures instead of shooting
- A section-space picture of where the fight is likely, built with zero new inputs from the players; deterministic seeds: same seed, mission and level give identical orders
- UI texts in German and English (the other shipped languages currently mirror English); a parity harness of tens of thousands of automated checks plus build gates stands in front of every release

**Why a fork and not a pull request?** It started as one: [PR #194](https://github.com/GlowPuff/ImperialAdmiral/pull/194). The upstream maintainers preferred the idea to mature on a fork first, with a standing invitation to return once it is ready - so that is what this repo does, and the door stays open. The base app and its donation link belong to GlowPuff; all credit for it stays with them.

### Downloads and docs for this fork
[Latest fork release](https://github.com/wit19/ImperialAdmiral/releases/latest)<br>
[Tactical AI guide](docs/WIKI_tactical_ai.md) (in this repo)

Like upstream, this fork is a non-commercial fan project in no way affiliated with or endorsed by Lucasfilm Limited, Disney or Fantasy Flight Games.

---

## Manual and Documentation (upstream base app)
[Full documentation](https://github.com/GlowPuff/ImperialAdmiral/wiki)<br>
[YouTube channel](https://www.youtube.com/channel/UC8mzMnsLZPgzQW2X2EkCR-Q)
