using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saga
{
	public class SagaSession
	{
		public int stateManagementVersion = 2;
		public SagaSetupOptions setupOptions;
		public DeploymentCard selectedAlly, fixedAlly;
		public SagaGameVars gameVars;
		public Guid campaignGUID;

		public List<DeploymentCard> MissionStarting;
		public List<DeploymentCard> MissionReserved;
		public List<DeploymentCard> EarnedVillains;
		public List<DeploymentCard> MissionIgnored;
		public List<DeploymentCard> MissionHeroes;
		public List<string> BannedAllies;

		public MissionLogger missionLogger;

		/// <summary>
		/// global imported characters for this session
		/// </summary>
		public List<DeploymentCard> globalImportedCharacters;

		public HashSet<string> CannotRedeployList;
		//list of heroes that finish taking part in an Event with "any hero wounded" and "any hero withdraws"
		//makes sure they don't keep firing said Event
		public HashSet<string> AnyHeroWoundedEventDone;
		public HashSet<string> AnyHeroWithdrawnEventDone;

		/// <summary>
		/// M14e (CONCEPT_standort_ledger_v2, Thomas 07.11): the ENTRY-POINT LEDGER -
		/// per card id, the DP(s) a group or hero entered through, with origin.
		/// Populated from THREE honest sources, last write wins (the current entry
		/// is the physical truth):
		///   "authored"  - ParseMission: the mission file's own pointList for every
		///                 initialDeploymentGroups group (the file says exactly
		///                 where the Imperator places them - Specific flows never
		///                 reached the old in-memory recorder, 293/297 start groups
		///                 were invisible to it) and the authored DPs of every
		///                 Specific event deployment at deploy time.
		///   "active"    - DeploymentManager.HandleMapDeployment: the DP the Active
		///                 DP choice actually used (ovrd.Active and ovrd==null).
		///   "hero-entry"- DeployHeroAlly: the mission's blue hero-entry field(s)
		///                 (taktische_hero_entries table, re-resolved live).
		/// A deployment-spot of None (Imperator places freely) gets NO row: honest
		/// unknown, no proximity claim, ever. Survives save/load (unlike the old
		/// in-memory map - the CONCEPT_reinforce Sec.6 restore gap is closed).
		/// </summary>
		public Dictionary<string, DeployLedgerRow> deployLedger;

		/// <summary>
		/// M14e: the live GUIDs of this mission's hero-entry highlight fields
		/// (empty = the mission carries none = every hero consumer stays inert).
		/// Resolved by SagaController from the mined taktische_hero_entries table.
		/// </summary>
		public List<string> heroEntryDps;

		/// <summary>
		/// Record the entry point(s) of a card; last write wins. Null-safe and
		/// never throws (a ledger failure must never break a deployment).
		/// </summary>
		public void RecordDeployEntry( string cardId, List<string> dps, string origin )
		{
			try
			{
				if ( string.IsNullOrEmpty( cardId ) || dps == null || dps.Count == 0 )
					return;
				if ( deployLedger == null )
					deployLedger = new Dictionary<string, DeployLedgerRow>();
				deployLedger[cardId] = new DeployLedgerRow { dps = new List<string>( dps ), origin = origin };
			}
			catch ( System.Exception e )
			{
				UnityEngine.Debug.LogWarning( "RecordDeployEntry()::" + e.Message );
			}
		}

		public bool TryGetDeployEntry( string cardId, out DeployLedgerRow row )
		{
			row = null;
			return cardId != null && deployLedger != null && deployLedger.TryGetValue( cardId, out row ) && row != null
				&& row.dps != null && row.dps.Count > 0;
		}

		public string missionStringified;

		public SagaSession( SagaSetupOptions opts )
		{
			//DataStore.InitData() has already been called at this point to load data
			setupOptions = opts;

			MissionStarting = new List<DeploymentCard>();
			MissionReserved = new List<DeploymentCard>();
			EarnedVillains = new List<DeploymentCard>();
			MissionIgnored = new List<DeploymentCard>();
			MissionHeroes = new List<DeploymentCard>();
			BannedAllies = new List<string>();
			globalImportedCharacters = new List<DeploymentCard>();
			CannotRedeployList = new HashSet<string>();
			AnyHeroWoundedEventDone = new HashSet<string>();
			AnyHeroWithdrawnEventDone = new HashSet<string>();
			deployLedger = new Dictionary<string, DeployLedgerRow>();
			heroEntryDps = new List<string>();
			selectedAlly = null;
			fixedAlly = null;
			campaignGUID = Guid.Empty;
			missionLogger = new MissionLogger();

			gameVars = new SagaGameVars();
		}

		public void InitGameVars()
		{
			gameVars.round = 1;
			gameVars.eventsTriggered = 0;
			gameVars.fame = 0;

			gameVars.currentThreat = 0;
			gameVars.currentThreat += setupOptions.addtlThreat;

			gameVars.deploymentModifier = 0;
			if ( setupOptions.difficulty == Difficulty.Hard )
				gameVars.deploymentModifier = 2;

			gameVars.pauseDeployment = false;
			gameVars.pauseThreatIncrease = false;

			gameVars.roundLimitEvent = DataStore.mission.missionProperties.roundLimitEvent;
			gameVars.roundLimit = DataStore.mission.missionProperties.roundLimit;

			Debug.Log( $"InitGameVars()::Round Limit set to [{gameVars.roundLimit}]" );
			Debug.Log( $"InitGameVars()::Round Limit Event set to [{gameVars.roundLimitEvent}]" );
		}

		/// <summary>
		/// Positive or negative number to add/decrease. force(TRUE)=don't use difficulty scale
		/// </summary>
		public void ModifyThreat( int amount, bool force = false )
		{
			int old = gameVars.currentThreat;
			//the only time ModifyThreat has "force=true" is when the user applies a custom amount of threat
			//or threat is added at (Saga) mission start and the mission type = Side
			//in that case, do NOT apply the difficulty modifier - apply the direct amount requested
			//force=true is also used for the ModifyThreat event action
			if ( amount > 0 && !force )
			{
				//round to nearest whole number, with X.5 rounding UP
				if ( setupOptions.difficulty == Difficulty.Easy )
					amount = (int)Math.Round( amount * .7f, 0, MidpointRounding.AwayFromZero );
				else if ( setupOptions.difficulty == Difficulty.Hard )
					amount = (int)Math.Round( amount * 1.3f, 0, MidpointRounding.AwayFromZero );
			}

			//only pause modification of threat when "amount" is POSITIVE
			//threat COSTS (negative) should ALWAYS modify (subtract) threat
			if ( amount > 0 && gameVars.pauseThreatIncrease && !force )
			{
				Debug.Log( "ModifyThreat()::THREAT PAUSED, NO MODIFICATION" );
				return;
			}

			gameVars.currentThreat = Math.Max( 0, gameVars.currentThreat + amount );

			Debug.Log( $"UpdateThreat()::CURRENT = {gameVars.currentThreat}, OLD = {old}, MODIFIER = {amount}" );
		}

		public void UpdateDeploymentModifier( int amount )
		{
			gameVars.deploymentModifier += amount;
			Debug.Log( "Update DeploymentModifier: " + gameVars.deploymentModifier );
		}

		public void SetDeploymentModifier( int amount )
		{
			gameVars.deploymentModifier = amount;
			Debug.Log( "Set DeploymentModifier: " + gameVars.deploymentModifier );
		}

		public void SaveState()
		{
			if ( setupOptions.isTutorial || setupOptions.isDebugging )
			{
				Debug.Log( "SaveState()::Canceled SaveState() - this is a tutorial or debug session" );
				return;
			}

			campaignGUID = RunningCampaign.sagaCampaignGUID;

			if ( RunningCampaign.sagaCampaignGUID == Guid.Empty )
				StateManager.SaveState( SessionMode.Saga );
			else
				StateManager.SaveState( SessionMode.Campaign );
		}

		public bool LoadState( StateManager sm )
		{
			if ( setupOptions.isTutorial )
			{
				Debug.Log( "SaveState()::Canceled LoadState() - this is a tutorial" );
				return false;
			}

			if ( RunningCampaign.sagaCampaignGUID == Guid.Empty )
				return sm.LoadState( SessionMode.Saga );
			else
				return sm.LoadState( SessionMode.Campaign );
		}

		public void RemoveState()
		{
			if ( setupOptions.isTutorial )
			{
				Debug.Log( "RemoveState()::Canceled RemoveState() - this is a tutorial" );
				return;
			}

			if ( RunningCampaign.sagaCampaignGUID == Guid.Empty )
				StateManager.RemoveState( SessionMode.Saga );
			else
				StateManager.RemoveState( SessionMode.Campaign );

			//now recreate the folder we just removed
			FileManager.SetupDefaultFolders();
		}

		/// <summary>
		/// Called after a "modify xp/credits/reward/fame/next mission" event action
		/// </summary>
		public void SaveCampaignState()
		{
			if ( RunningCampaign.sagaCampaignGUID != Guid.Empty )
			{
				Debug.Log( "SaveCampaignState()::Campaign state SAVED" );
				StateManager.SaveState( SessionMode.Campaign );
			}
			else
				Debug.Log( "SaveCampaignState()::NOT in a Campaign, skipped" );
		}
	}

	/// <summary>
	/// M14e: one ledger row - the DP(s) a card entered through and where the
	/// knowledge came from ("authored" | "active" | "hero-entry"). GUIDs as
	/// strings (the session save is Json.NET; Guid lists round-trip, strings
	/// keep the format stable across saves - the Loch-Gesetz law of the feed
	/// layer: identities, never coordinates).
	/// </summary>
	public class DeployLedgerRow
	{
		public List<string> dps = new List<string>();
		public string origin = "";
	}
}
