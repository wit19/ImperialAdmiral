using System.Collections.Generic;

public class UILanguage
{
	public UITitle uiTitle;
	public UISetup uiSetup;
	public SagaUISetup sagaUISetup;
	public UIMainApp uiMainApp;
	public SagaMainApp sagaMainApp;
	public UISettings uiSettings;
	public UIExpansions uiExpansions;
	public UIDeploymentGroups uiDeploymentGroups;
	public List<MissionCard> uiMissionCards;
	public UICampaign uiCampaign;
	public UILogger uiLogger;
	public UITacticalAI uiTacticalAI;
	public UIHelpOverlay uiHelpOverlay;

	public UIHelpOverlay uiHelpOverlayBackup;//the English version to fall back to for missing entries in translations
}

// Player-facing texts of the tactical AI layer (activation popups, doctrine
// orders, focus explanations). All keys live in Languages/<Lang>/ui.json.
public class UITacticalAI
{
	public string doctrineHeader, docAggressive, docGuard, docBlocker, docHunter, docSupport, docAmbusher;
	// Aufgabe 4 (Thomas 02.11): the fixed "Aktion 1/2" pair and ALL per-block
	// move/attack trees (actionFormat, actionSoloFormat, moveFocus*, moveGuard,
	// moveBlocker, moveSupport, moveAmbusher, attackFocus/NoTarget, attack
	// Guard*, attackAmbush*, attackSupport*, attack2Move*, attackSecondIntro/
	// Tail, assaultSecondAttack, tripod trees) are RETIRED with the universal
	// action block - fields and ui.json keys removed in the same commit
	// (M13g rule). New frame: apHead/apHeadAssault = detected AP counter
	// ({0}/{1}), apRule = reading rule, apTree* = per-doctrine multi-line
	// decision trees ({0} = printed speed), apEnd = loop-trap marker. TMP
	// tags are language-owned (ui.json law); solo/numbered labelling and the
	// doctrineTreeLead/"Grundregel" removals of M13a/M13c stay in force.
	public string apHead, apHeadAssault, apRule, apTreeAggressive, apTreeGuard, apTreeBlocker, apTreeHunter, apTreeSupport, apTreeAmbush, apTreeTripod, apTreeNoTarget, apEnd, apCustomHeader;
	// P37c (card t_e80246ed, concept §1b G3/G4): the printed Duty-Ast rows.
	// apDutyEscortTree = the Wenn-Dann movement guidance ({0} = duty anchor
	// NAME, §1b Gesetz-3 text incl. the §9.4 Reichweiten-Drohungsdefinition,
	// multi-line like every apTree* value); apDutyQuotaLine = the G4 quota
	// disclosure ({0} = the raw Explain string: k, cap, cohort ranking).
	// KEY DEFINITION ONLY — the 11-language VALUES are hop D's job (its card
	// reads these two names from the P37c handoff). Until then the keys are
	// null everywhere and the bridge renders NOTHING (Null-Law: popups
	// byte-identical to pre-P37c, fail-closed like the O-C key guard).
	public string apDutyEscortTree, apDutyQuotaLine;
	// P38 (card t_11ebb843, playtest Empire3): the NEUTRALE-Beute rows.
	// apPreyQuotaLine = the OBJEKTJAGD quota disclosure ({0}=k, {1}=n —
	// 'Objektjagd: k von n Einheiten'); rides every Muster-cohort member's
	// popup while the mission carries bystander prey. apPreyThreatLine = the
	// Bedrohungs-why ({0} = the section's AUTHORED name, §8.4-trusted);
	// apPreyThreatRel = the relative fallback when the name does not inform
	// (no {0} — never an id). Values ship in the 11 Languages/<L>/ui.json;
	// missing key => no row (fail-closed, popup byte-identical).
	public string apPreyQuotaLine, apPreyThreatLine, apPreyThreatRel;
	// Thomas 02.11: command trees for cards that PRINT a command ability on
	// the ignored lane - PER DOCTRINE (Thomas' correction: an officer who
	// rolled "guard" still commands, so every pattern keeps its own action
	// wording AND the Order branch). Plain "Order" -> apTreeOfficer<Pattern>,
	// elite "Executive Order" -> apTreeOfficerElite<Pattern>; a missing elite
	// row falls back to the plain officer row, then to the pattern tree.
	public string apTreeOfficerAggressive, apTreeOfficerGuard, apTreeOfficerBlocker, apTreeOfficerHunter, apTreeOfficerSupport, apTreeOfficerAmbush;
	public string apTreeOfficerEliteAggressive, apTreeOfficerEliteGuard, apTreeOfficerEliteBlocker, apTreeOfficerEliteHunter, apTreeOfficerEliteSupport, apTreeOfficerEliteAmbush;
	// O-C (Thomas 04.11., card t_a2d88c50): supporter-codex ersatzzweig - shown
	// ONLY for printed-order cards WITHOUT a proven emergency (skill >= S2;
	// decision in Core OfficerDoctrine.TreeKindFor). Keep = give the Order,
	// group leash, no self-initiated attack. apTreeOfficerNotfall wraps the
	// PROVEN OfficerEmergency reason ({0} = Core why-line) as the tree-head
	// line for the Emergency variant. Missing rows degrade to the old officer
	// tree via FirstNonEmpty (stale language asset = old behaviour, never a
	// blank popup).
	public string apTreeOfficerNotfall;
	public string apTreeOfficerKeepAggressive, apTreeOfficerKeepGuard, apTreeOfficerKeepBlocker, apTreeOfficerKeepHunter, apTreeOfficerKeepSupport, apTreeOfficerKeepAmbush;
	public string apTreeOfficerEliteKeepAggressive, apTreeOfficerEliteKeepGuard, apTreeOfficerEliteKeepBlocker, apTreeOfficerEliteKeepHunter, apTreeOfficerEliteKeepSupport, apTreeOfficerEliteKeepAmbush;
	public string overlordFallback;
	// M13c (Thomas 30.10): shuffleNote DELETED ("(afterwards: shuffle this
	// group's deck)" does not exist in this app's flow - honest removal).
	public string specialAsPrinted, restLine, interactLine;
	public string focusPrefix, focusWounded, focusFirepower;
	// R-D (Konzept eventreaktionen v1.2 §2.3, Thomas-Punkt 3a): clause on
	// the focus line when the reaktion key decided the pick; {0} = the
	// activated figure's name. Pronoun-free wording is deliberate (the
	// 21-hero roster carries female heroes).
	public string ordFocusReaktion;
	// S-D (card t_c52dd34b, concept positionierung v1 13.4): the same
	// tail-clause law for the two section bases; {0} = the activated
	// figure's name, pronoun-free like the reaktion tail above.
	public string ordFocusWachzone, ordFocusVergeltung, ordFocusFokuszone;
	// S-E (card t_9dd56737, concept positionierung v1 Z6/D4): the supporter's
	// mask-trust focus tail ({0} = activated figure's name, pronoun-free),
	// its basis label, and the two EXPLAIN why-lines (strike: {0} group +
	// {1} authored section name; support: {0} group). ui.json-owned, no
	// coordinates (coord-free-Law).
	public string ordFocusMaskenvertrauen, ordFocusBasisMaskenvertrauen,
		ordExplainSupportMaske, ordExplainSupportHalten;
	// M6 overlord explain parts (public threat economy + aim marks + risk).
	// M13c (Thomas 30.10): ordRoundFocus + focusNoChance REMOVED from the
	// focus-target WHY line (the "all groups converge...; no chance" tail).
	public string ordThreat, ordMarked;
	// M15 F2: the EXPLAIN threat CALCULATION (operands in the message:
	// {0}=threat {1}=damage exchange {2}=mobility {3}=dice pool).
	public string ordThreatCalcRunner;
	public string ordThreatEnergy;
	public string ordThreatExplosion;
	// M25: flat extra-attack / Verstaerkung clause
	public string ordThreatBonus;
	// M11d: light strain-pressure note in the round-focus WHY line
	public string ordStrainPressure;
	// M11h: fuzzy strain-usage prior in the round-focus WHY line - args
	// {0} = completed round, {1} = reachable buffer; wording says
	// "wahrscheinlich/probably" (a prior, never marker knowledge).
	public string ordStrainFuzzy;
	// M11e: heading line for the printed {A} option block in the activation popup
	public string ordOptionPrinted;
	// M13n (Thomas 29.10): general RR reminder block printed under the action
	// blocks, BEFORE the gated explanation: attack = weapon/ability use, and
	// the door law (RR DOORS: opening a door is an INTERACT - one of the five
	// action types - so it costs an action; locked doors only per mission).
	// M13n + M13c: the general RR info block under the action blocks - ONE
	// merged paragraph: basic attack rule, attack line, door law.
	public string ordInfoRule, ordInfoAttack, ordInfoDoor;
	// M6s (Thomas 03.11., B1): guard-zone definition line in the same INFO
	// block - the bridge adds it ONLY when plan.Pattern == "guard" (the
	// Wachschutz popup); the zone extent itself is board knowledge the
	// player reads off the mission (wiki Kalt-Lesetest: the popup knows
	// no zone tiles), which is exactly what this line says.
	public string ordInfoGuardZone;
	// Thomas 07.11: the activation-chip golden rule - the END-of-turn hero chip
	// flip is the ONLY carrier of HeroActivated (M6s-B7), so the popup INFO
	// block states its timing (after damage/kills/interactions announced) on
	// EVERY tactical activation popup, and the hero card's own checkbox
	// carries the same rule as a hover tooltip (tooltipHeroChipUC below).
	public string ordInfoFlip;
	public string ordRiskLabel, ordRiskPress, ordRiskHold, ordRiskGuardHold, ordRiskDisengage;
	public string whyPrefix, whyRank;
	public string setHeading, setMode, setSkill, setExplain, setInfo, setOn, setOff, setRowFormat;
	// M13h (Thomas 28.10): the setCardSkills note field is REMOVED - the note
	// line it fed is gone from the settings screen (stale 'S4' wording).

	// skill ladder DISPLAY names (dev 18: recruit..5-star general, Thomas).
	// Stored/logged token stays s1..s5 - these are labels only (PR rule:
	// display via language files).
	public string skillS1, skillS2, skillS3, skillS4, skillS5;
	public string badgeAuto, badgeOff;
	// M11f (Einsatzkarte): plain-text mission badge that replaces the legacy
	// difficulty toggle while the tactical planner is in charge. {0} = ladder
	// rung number, {1} = skillS name. The (auto) variant marks ""/AUTO tokens
	// that resolve to the global ladder rung.
	public string badgeMissionFormat, badgeMissionAutoFormat, badgeMissionOff;
	// Personas (M1/M3, Thomas 02.11.): the persona REPLACED the class-deck
	// and agenda-card system - its ui.json keys (impBuyLog/agendaBuy*/imp*)
	// are removed with it, 11-language parity re-checked. Vocabulary:
	// picker words, rank chips (activation-card line), QM announcements,
	// the persona name/motto/signature rows (keys are the asset's
	// nameKey/mottoKey/signaturText), settings row heading.
	// M6l (Delta-0, M6g QA-FUND C2): the gift-era chip/QM keys
	// personaBonusCost, personaBonusSubsidy and personaSubsidyQM are REMOVED
	// together with their ui.json lines and the PersonaState consumer
	// branches in the same commit (Sprachgesetz 5b) - the kinds behind them
	// occur 0x in the shipped v2.1 asset.
	public string personaFollowDefault, personaNone, personaRank;
	public string personaBonusDoctrine, personaBonusGuard;
	public string personaRankUpQM, personaRankDownQM;
	public string personaButton, personaPopupHeader, setPersona;
	public string personaPlainNote, personaRankColumn, personaCycleNote;
	public string personaCaptTrooper, personaCaptTrooperMotto, personaCaptIsb, personaCaptIsbMotto;
	public string personaCaptHutt, personaCaptHuttMotto;
	// M6l (RR-B rewrite + v2.1 fleet): the three pre-Delta-0 signatures are
	// REWRITTEN onto v2.1 reality (elite_swap as 'seine Elite', no gift or
	// discount wording); the three NEW personas get their signature keys so
	// the asset's signaturText effects finally have a channel (they were
	// dead data: Localized() found no field). personaDelta0Hint is the
	// once-per-popup sentence explaining the Delta-0 law (his elite
	// replaces the regular figure, cost and figure count stay equal).
	public string personaCaptSignature, personaIsbSignature, personaHuttSignature;
	public string personaFeldherrSignature, personaDroidenDirektorSignature, personaGrossmoffSignature;
	public string personaDelta0Hint;
	// v2.1 fleet (M6c, CONCEPT_personas_v2.md Sec.2, Planpunkt 3): the three
	// NEW personas (P2 Feldherr, P5 Droiden-Direktor, P6 Grossmoff) get
	// name/motto keys; the three Bestands personas REUSE their Alt-fleet keys
	// (personaCaptTrooper/Isb/Hutt + Mottos - keine Dubletten, Key-Matrix im
	// M6c-Abschlusskommentar). personaRankCounter is the 'Rang n/max' display
	// (Beschluss 5, Sec.3): the use site lands with M6d, the legacy
	// single-number personaRank chip stays UNTOUCHED (live {0} callers).
	public string personaCaptFeldherr, personaCaptFeldherrMotto;
	public string personaCaptDroidenDirektor, personaCaptDroidenDirektorMotto;
	public string personaCaptGrossmoff, personaCaptGrossmoffMotto;
	public string personaRankCounter;
	// Noldorion review 2.11 (PR #194): popup note for non-villain groups
	// whose printed abilities stay in the human lane (the mode never
	// pre-decides "may" abilities it cannot see the board for). (The card
	// announce line ordCardEffect is GONE - personas own that slot now.)
	public string ordAbilitiesLane;
	// Noldorion direction reply 2.11: "the power score needs to be very
	// transparent" - EXPLAIN=VOLL discloses the real focus computation:
	// score + what the number measures (posture-dependent) + wound economy
	// + decision basis + runner-up. Numbers are the fixture-pinned sim
	// values (FocusPick.AsDict), every input is a public table artifact.
	public string ordFocusDetail;
	// Basis/measure labels for the disclosure line (code maps the machine
	// vocabulary; unknown basis falls through as the raw token).
	public string ordFocusMeasureDamage, ordFocusMeasureRunner;
	public string ordFocusBasisMark, ordFocusBasisChip, ordFocusBasisConditioned;
	public string ordFocusBasisObjactor;
	public string ordFocusBasisEconomy, ordFocusBasisPrinted;
	// R-D: basis label when the reaktion key decided the focus (EXPLAIN
	// disclosure line "Grundlage: ...").
	public string ordFocusBasisReaktion;
	// S-D (card t_c52dd34b): the two section-space basis labels (EXPLAIN
	// "Grundlage:" disclosure), same vocabulary slot as reaktion.
	public string ordFocusBasisWachzone, ordFocusBasisVergeltung;
	// Punkt 2 (Thomas 01.11.): doctrine phase disclosure - one-shot QM at
	// the defend->hunt flip and the EXPLAIN line naming both phases.
	public string ordPhaseShiftQM, ordPhaseExplain, ordPhaseDefend, ordPhaseHunt;
	public string ordPatternRoll;
	// Thomas 02.11 (popup declutter): EXPLAIN section header + the focus
	// target reasoning line that moved out of the action area.
	public string ordExplainHead, ordExplainFocusWhy;
	// S-D (card t_c52dd34b): the section-space EXPLAIN chain lines
	// (Ereignis -> Entscheidung). Wachzone {0}=hero {1}=section {2}=group;
	// ThreeC {0}=hero {1}=section {2}=group; Vergeltung {0}=group {1}=hero;
	// Clamp carries no argument. All figure-/object-naming (Loch-Gesetz:
	// never a coordinate, never a section number).
	public string ordExplainWachzone, ordExplainThreeC, ordExplainVergeltung, ordExplainClamp, ordExplainFokuszone;
	// M14f Naehesuch: {0}=group, {1}=cleaned reaction-zone name; the Rel
	// variant carries only {0} (junk name -> relative wording, §8.4 law).
	public string ordExplainNahe, ordExplainNaheRel;
	// P33-H7 follow-up (H7 finding 1, card t_f7bec904): the object-target
	// player lines ride ui.json in ALL 11 languages - the target-field
	// prefix (every explain level), the EXPLAIN why-line ({0}=printed
	// Health, {1}=polarity word, {2}=derivation code P1..P4) and the three
	// polarity display words. De renders byte-identical to the raw-token
	// line it replaces (findings diffs stay clean); the internal
	// ziel=OBJEKT:<name> log notation stays language-neutral.
	public string ordTargetObjekt, ordExplainObjekt,
		ordPolarJagt, ordPolarSchuetzt, ordPolarNeutral;
	// expNone/expPostHoc/expFull REMOVED (Thomas 02.11): the explain switch
	// is a plain AN/AUS toggle reusing setOn/setOff; NACHHER entfällt.
}

public class UISettings
{
	public string settingsHeading, music, sound, bloom, vignette, quit, returnBtn, ok, quickClose, ambient, zoomButtons, topdownView, roundLimitOn, roundLimitOff, roundLimitDangerous, roundLimitLabel, skipWarpIntroLabel, enemyGroupsColor, colorRegular, colorElite, colorVillain, audioHeader, uiHeader, graphicsHeader, mapperHeader, mapActivateImperialsLabel, mapToggleCamViewLabel, mapToggleMapVisibilityLabel, mapNavForwardLabel, mapNavBackLabel, mapNavLeftLabel, mapNavRightLabel, mapNavCWLabel, mapNavCCWLabel, reset, pressKeyMessage;
}

public class UITitle
{
	public string menuHeading, newGameBtn, continueBtn, campaignsBtn, optionsBtn, supportUC, docsUC, newCampaign, loadCampaign, confirmDelete, delete, expansions, tutorialUC, saga, campaigns, classic;
}

public class UISetup
{
	public string settingsHeading, chooseMission, viewCardBtn, missionInfoBtn, threatLevelHeading, addtlThreatHeading, deploymentHeading, yes, no, back, difficulty, easy, normal, hard, imperials, mercenaries, adaptive, groupsHeading, choose, zoom, initialHeading, reservedHeading, villainsHeading, ignoredHeading, addHero, addAlly, threatCostHeading, cancel, continueBtn, saved, loaded, selected, enemyChooser, missionChooser, heroAllyChooser, adaptiveInfoUC, chooseHeroesHeading, appMissionFormat;
}

public class SagaUISetup
{
	public string groupsText, villainsBtn, tilesBtn, setupStartBtn, officialBtn, customBtn, missionCardBtn, campaignJournalUC, importBtn;
}

public class SagaMainApp
{
	// M13c optional-deployment revival keys (last field block): ui.json sagaMainApp, 11 languages.
	public string tooltipHideUIUC, roundIncreasedUC, endOfMissionUC, deployMessageUC, noDPWarningUC, mmAddTilesUC, mmRemoveTilesUC, mmCoreTileNameUC, mmTwinTileNameUC, mmHothTileNameUC, mmBespinTileNameUC, mmJabbaTileNameUC, mmEmpireTileNameUC, mmLothalTileNameUC, mmAddEntitiesUC, groupsReadyUC, groupsExhaustUC, repositionTargetUC, doorsUC, cratesUC, terminalsUC, tokensUC, woundUC, withdrawUC, exhaustUC, defeatUC, imperialMenu, medpacInfoUC, cannotDefeatUC, missionLogTitle, optionalDeployTitleUC, optionalDeployPickGroupUC, optionalDeployPickDpUC, optionalDeployNoGroupsUC, optionalDeployCostFormatUC, optionalDeployDpFormatUC, optionalDeployConfirmUC, optionalTacticalAnnounceUC, tooltipHeroChipUC;
}

public class UIMainApp
{
	public string eliteUpgradeMsgUC, eliteDowngradeMsgUC, restoredMsgUC, restoreErrorMsgUC, pauseDepMsgUC, unPauseDepMsgUC, pauseThreatMsgUC, UnPauseThreatMsgUC, deploymentHeading, reservedBtn, allyBtn, enemyBtn, randomBtn, modThreatHeading, applyBtn, roundHeading, depTypeHeading, eventHeading, randomHeading, maxThreatHeading, endRoundBtn, fameHeading, awardsHeading, fame1UC, fameItem1UC, fameItem2UC, fame2UC, continueBtn, debugThreatUC, debugDepModUC, debugDepHandUC, tooltipRulesUC, tooltipInfoUC, tooltipPauseDepUC, tooltipPauseThreatUC, tooltipOpDepUC, tooltipSettingsUC, tooltipImpHandUC, tooltipActivateUC, tooltipFameUC, tooltipDashboardUC, tooltipMedPacUC, confirm, cancel, deploy, threatIncreasedUC, reinforceWarningUC, deploymentWarningUC, calmMessageUC, close, deployModeCalm, deployModeReinforcements, deployModeLanding, deployModeOnslaught, fameIncreasedUC, noRandomMatchesUC, depCostUC, noAbilitiesUC, ignoredAbilitiesUC, noKeywordsUC, noneUC, rewardUC, pageUC, optionalDeployment;
}

public class UIExpansions
{
	public string core, twin, hoth, bespin, jabba, empire, lothal, other, figurepacks;
}

public class UIDeploymentGroups
{
	public List<CardLanguage> enemyCards;
	public List<CardLanguage> villainCards;
	public List<CardLanguage> allyCards;
	public List<CardLanguage> heroCards;
}

public class UICampaign
{
	public string threatInfoUC, modeIntroductionUC, modeStoryUC, modeSideUC, forcedUC, modeInterludeUC, modeFinaleUC, campaignNameUC, customCampaign, addForcedMissionUC, tierUC, selectMissionUC, customUC, creditsUC, fameUC, awardsUC, campaignSetup, itemsUC, rewardsUC, villainsUC, alliesUC, threat, agendaUC, modifyUC, removeUC, otherUC, campaignUC, generalUC, heroUC, personalUC, campaignSetupUC, campaignDescriptionUC, sagaDescriptionUC, classicDescriptionUC, agendaMission, agendaImperialUC, agendaRebelUC, xpUC, costUC, missionTypeMockupUC, missionNameMockupUC, itemMockupUC;

	public string[] missionTypeStrings;

	public void BuildMissionTypeStrings()
	{
		missionTypeStrings = new string[6];
		missionTypeStrings[0] = modeStoryUC;
		missionTypeStrings[1] = modeSideUC;
		missionTypeStrings[2] = forcedUC;
		missionTypeStrings[3] = modeIntroductionUC;
		missionTypeStrings[4] = modeInterludeUC;
		missionTypeStrings[5] = modeFinaleUC;
	}
}

public class UILogger
{
	public string textLabel, inputPromptLabel, selectionPromptLabel, selectionLabel, groupActivationLabel, groupDeployedLabel, groupRemovedLabel, groupDefeatedLabel, deploymentEventLabel, inputValueLabel;
}

public class UIHelpOverlay
{
	public HelpOverlayPanel[] helpOverlayPanels;
}

public class HelpOverlayPanel
{
	public string panelHelpID;
	public HelpOverlayItem[] helpItems;
}

public class HelpOverlayItem
{
	public string id;
	public string helpText;
}