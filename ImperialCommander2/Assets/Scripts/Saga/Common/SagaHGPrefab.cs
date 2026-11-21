using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Saga
{
	public class SagaHGPrefab : MonoBehaviour
	{
		public Toggle woundToggle, activationToggle1, activationToggle2;
		public Image iconImage;
		public Image outline;
		public Color eliteColor;
		public GameObject exhaustedOverlay;
		public DeploymentCard Card { get { return cardDescriptor; } }

		DeploymentCard cardDescriptor;

		[HideInInspector]
		public bool isConfirming = false;//set by ConfirmPopup to block further input while visible

		private void Awake()
		{
			Transform tf = transform.GetChild( 0 );
			tf.localScale = Vector3.zero;
		}

		/// <summary>
		/// When restoring state, set the hero health (hState) so the state object can keep tracking it
		/// </summary>
		public void Init( DeploymentCard cd, HeroState hState = null )
		{
			Debug.Log( $"DEPLOYED: {cd.name}, {cd.characterType}" );
			cardDescriptor = cd;

			if ( !cd.isDummy )//if NOT the "bonus" token for 3 player games
			{
				var ovrd = DataStore.sagaSessionData.gameVars.GetDeploymentOverride( cd.id );
				if ( ovrd != null && ovrd.isCustomDeployment )
					cardDescriptor = ovrd.customCard;
				//icon thumbnail
				iconImage.sprite = Resources.Load<Sprite>( cardDescriptor.mugShotPath );
				//outline color
				outline.color = Utils.String2UnityColor( cardDescriptor.deploymentOutlineColor );
			}
			else
			{
				iconImage.sprite = Resources.Load<Sprite>( "CardThumbnails/bonus" );
				woundToggle.gameObject.SetActive( false );
				outline.color = Utils.String2UnityColor( "Black" );
			}

			cd.heroState = hState;

			if ( cd.heroState == null )
			{
				cd.heroState = new HeroState();
				cd.heroState.Init();
			}

			SetHealth( cd.heroState );
			SetActivation();
			WireChipTooltip();

			Transform tf = transform.GetChild( 0 );
			tf.localScale = Vector3.zero;
			// P40 (Player-39 DOTween safe-mode note): the card rails rebuild
			// on every refresh - a pop-in tween still running when the widget
			// dies would be captured as a missing-target error. SetLink kills
			// it WITH the widget; the pop itself is unchanged.
			tf.DOScale( 1, 1f ).SetEase( Ease.OutBounce ).SetLink( tf.gameObject );
		}

		/// <summary>
		/// T. 07.11: the activation-chip golden rule on its OWN widget - the
		/// hero's activation checkbox must be flipped at the END of the hero's
		/// turn (after damage, kills and interactions are announced), because the
		/// flip is the ONLY carrier of HeroActivated (M6s-B7) and every tactical
		/// attribution law keys on that event. The hover tooltip is attached at
		/// runtime (Init runs on BOTH the fresh-deploy and the restore rail),
		/// tactical missions ONLY - a legacy mission never grows the component,
		/// so classic behaviour stays bit-identical.
		/// </summary>
		private void WireChipTooltip()
		{
			if ( cardDescriptor.isDummy )
				return;
			if ( !TacticalAI.TacticalAIBridge.TacticalMissionActive() )
				return;
			foreach ( var t in new[] { activationToggle1, activationToggle2 } )
			{
				if ( t == null )
					continue;
				if ( t.gameObject.GetComponent<TooltipHandler>() == null )
					t.gameObject.AddComponent<TooltipHandler>().tooltip = "HeroChipFlip";
			}
		}

		public void OnCount1( Toggle t )
		{
			if ( !woundToggle.gameObject.activeInHierarchy )
				return;
			OnClickSelf();

			//cardDescriptor.heroState.isHealthy = t.isOn;
			//if ( cardDescriptor.heroState.isHealthy )
			//{
			//	cardDescriptor.heroState.heroHealth = HeroHealth.Healthy;
			//	exhaustedOverlay.SetActive( false );
			//}
			//else
			//{
			//	if ( isHero )
			//	{
			//		cardDescriptor.heroState.heroHealth = HeroHealth.Wounded;

			//		FindObjectOfType<SagaController>().eventManager.CheckIfEventsTriggered();
			//	}
			//	else
			//	{
			//		exhaustedOverlay.SetActive( !cardDescriptor.heroState.isHealthy );
			//		cardDescriptor.heroState.heroHealth = HeroHealth.Defeated;
			//	}
			//}

			//if it's an ally, mark it defeated
			//if ( cardDescriptor.id[0] == 'A' )
			//{
			//	exhaustedOverlay.SetActive( !cardDescriptor.heroState.isHealthy );
			//	cardDescriptor.heroState.heroHealth = HeroHealth.Defeated;
			//}

			//Debug.Log( "HEALTHY: " + cardDescriptor.isHealthy );
		}

		public void OnActivation1()
		{
			if ( !activationToggle1.gameObject.activeInHierarchy )
				return;

			// P41 (T. ruling 21.11): the bonus token is a MARKER, not a hero.
			// Its flip is only a turn-close once the players have chosen WHO
			// owns this extra activation; unassigned, the flip asks instead of
			// logging "Dummy Hero" (a phantom hero never existed at the table
			// and never enters the mission log or the tactical feed).
			if ( cardDescriptor.isDummy )
			{
				BonusChipFlip();
				return;
			}

			cardDescriptor.heroState.hasActivated[0] = activationToggle1.isOn;

			// Player-19 M6s-B7: the checkbox flip is the table's OWN "his
			// turn is over" signal (players switch the hero status at the END
			// of the activation, never at its start). Writing it into the
			// mission log makes hero turn order a causal public fact with the
			// exact same line kind the imperial popups already write - the
			// tactical feed's objective/strike attribution reads table order
			// from table data instead of guessing it from poll timing.
			if ( activationToggle1.isOn && DataStore.sagaSessionData != null )
				DataStore.sagaSessionData.missionLogger.LogEvent( MissionLogType.GroupActivation, cardDescriptor.name );
		}

		public void OnActivation2()
		{
			if ( !activationToggle2.gameObject.activeInHierarchy )
				return;
			cardDescriptor.heroState.hasActivated[1] = activationToggle2.isOn;

			// Player-19 M6s-B7: see OnActivation1.
			if ( activationToggle2.isOn && DataStore.sagaSessionData != null )
				DataStore.sagaSessionData.missionLogger.LogEvent( MissionLogType.GroupActivation, cardDescriptor.name );
		}

		// ------------------------------------------------------------------
		// P41 (T. ruling 21.11): BONUS ACTIVATION TOKEN (3-hero missions).
		// Three heroes plus the printed rule "four activation markers, at
		// most two on one hero" - the app ships the fourth marker as this
		// bonus token card. WHO carries the extra activation is the Rebel
		// players' decision and can change; the token remembers it
		// (HeroState.bonusHolderId/Name, JSON-persisted with the card).
		// The flip is the same M6s-B7 turn-close carrier law as a hero's
		// own chip - so it must name the CHOSEN HERO, never "Dummy Hero":
		// a name no figure answers to would hand the tactical feed a
		// phantom hero and strike credit to nobody (Null-Law).
		// ------------------------------------------------------------------
		void BonusChipFlip()
		{
			var hs = cardDescriptor.heroState;
			if ( hs == null )
				return;

			if ( !string.IsNullOrEmpty( hs.bonusHolderId ) )
			{
				// assigned: the flip closes the chosen hero's extra turn
				hs.hasActivated[0] = activationToggle1.isOn;
				if ( activationToggle1.isOn && DataStore.sagaSessionData != null )
					DataStore.sagaSessionData.missionLogger.LogEvent( MissionLogType.GroupActivation, hs.bonusHolderName );
				return;
			}

			if ( !activationToggle1.isOn )
				return;   // programmatic reset (round end) - nothing to ask

			// unassigned: revert FIRST, then ask - the checkbox only ever
			// flips ON once a real hero owns this marker.
			activationToggle1.isOn = false;
			AskBonusHolder( completeFlip: true );
		}

		/// <summary>Opens the chooser popup (same runtime PromptBox lane the
		/// entity "ProcessQuestionPrompt" uses - MapEntityManager precedent).
		/// completeFlip = the player's chip click was the intent; the flip is
		/// completed once a hero is chosen. Cancel leaves the token idle and
		/// the assignment untouched (no guess).</summary>
		void AskBonusHolder( bool completeFlip )
		{
			var sess = DataStore.sagaSessionData;
			if ( sess == null || sess.MissionHeroes == null || sess.MissionHeroes.Count == 0 )
				return;
			var evm = FindObjectOfType<SagaEventManager>();
			if ( evm == null )
				return;

			var prompt = new QuestionPrompt();
			prompt.theText = BonusChipPromptText();
			prompt.includeCancel = true;
			foreach ( var h in sess.MissionHeroes )
			{
				if ( h == null || string.IsNullOrEmpty( h.name ) )
					continue;
				prompt.buttonList.Add( new ButtonAction() { buttonText = h.name } );
			}
			if ( prompt.buttonList.Count == 0 )
				return;

			var pb = evm.ShowPromptBox( prompt, null );
			if ( pb == null )
				return;   // no text to show - stays idle, honest no-op
			var heroes = sess.MissionHeroes;
			pb.onChoice = i =>
			{
				if ( i < 0 || i >= heroes.Count )
					return;
				var pick = heroes[i];
				if ( pick == null || string.IsNullOrEmpty( pick.id ) )
					return;
				cardDescriptor.heroState.bonusHolderId = pick.id;
				cardDescriptor.heroState.bonusHolderName = pick.name;
				var qm = GlowEngine.FindUnityObject<QuickMessage>();
				if ( qm != null )
					qm.Show( BonusAssignedText( pick.name ) );
				if ( completeFlip && !activationToggle1.isOn )
					activationToggle1.isOn = true;   // re-enters OnActivation1 -> assigned path logs the hero
			};
		}

		static string BonusChipPromptText()
		{
			var tl = DataStore.uiLanguage != null ? DataStore.uiLanguage.uiTacticalAI : null;
			return tl != null && !string.IsNullOrEmpty( tl.bonusChipPrompt )
				? tl.bonusChipPrompt
				: "Which hero receives the bonus activation?";
		}

		static string BonusAssignedText( string heroName )
		{
			var tl = DataStore.uiLanguage != null ? DataStore.uiLanguage.uiTacticalAI : null;
			if ( tl != null && !string.IsNullOrEmpty( tl.bonusChipAssigned ) )
			{
				try { return string.Format( tl.bonusChipAssigned, heroName ); }
				catch { return tl.bonusChipAssigned; }
			}
			return "Bonus activation: " + heroName;
		}

		//popup menu for wound/defeat
		public void OnClickSelf()
		{
			// P41: the bonus token has no health to confirm - a click on it
			// (re)assigns which hero carries the extra activation.
			if ( cardDescriptor.isDummy )
			{
				if ( !isConfirming && !FindObjectOfType<SagaEventManager>().IsUIHidden )
					AskBonusHolder( completeFlip: false );
				return;
			}

			if ( cardDescriptor.heroState.isDefeated
				|| FindObjectOfType<SagaEventManager>().IsUIHidden )
				return;

			if ( !isConfirming && FindObjectOfType<ConfirmPopup>() == null )
			{
				FindObjectOfType<SagaController>().ToggleNavAndEntitySelection( false );
				FindObjectOfType<SagaEventManager>()?.transform.Find( "HGConfirmPopup" )
					.GetComponent<ConfirmPopup>().ShowRight(
					transform,
					this,
					cardDescriptor.name,
					cardDescriptor.characterType == CharacterType.Hero,
					cardDescriptor.heroState.isWounded,
					OnDefeatCheck,
					OnDefeatCheck );
			}
			else
			{
				FindObjectOfType<SagaController>().ToggleNavAndEntitySelection( true );
				FindObjectOfType<ConfirmPopup>()?.Hide();
			}
		}

		//right click popup card view, excluding Heroes
		public void OnPointerClick()
		{
			//don't show popup for dummy heroes, generic allies, or stock heroes (no data for them)
			if ( !cardDescriptor.isDummy
				&& !cardDescriptor.mugShotPath.Contains( "genericAlly" )
				&& cardDescriptor.id[0] != 'H' )
			{
				CardViewPopup cardViewPopup = GlowEngine.FindUnityObject<CardViewPopup>();
				cardViewPopup.Show( cardDescriptor );
			}
		}

		public void SetHealth( HeroState heroState )
		{
			if ( cardDescriptor.isDummy )
				return;

			cardDescriptor.heroState = heroState;
			woundToggle.gameObject.SetActive( false );//skip callback

			if ( heroState.isWounded || heroState.isDefeated )
				woundToggle.isOn = false;

			if ( cardDescriptor.heroState.isDefeated )
				exhaustedOverlay.SetActive( true );

			woundToggle.gameObject.SetActive( true );
		}

		/// <summary>
		/// Display 1 or 2 activation buttons, depending on # of players and character type
		/// </summary>
		private void SetActivation()
		{
			//skip callbacks
			activationToggle1.gameObject.SetActive( false );
			activationToggle2.gameObject.SetActive( false );

			if ( DataStore.sagaSessionData.MissionHeroes.Count <= 2 && cardDescriptor.characterType == CharacterType.Hero )
			{
				activationToggle1.isOn = cardDescriptor.heroState.hasActivated[0];
				activationToggle2.isOn = cardDescriptor.heroState.hasActivated[1];
				activationToggle1.gameObject.SetActive( true );
				activationToggle2.gameObject.SetActive( true );
			}
			else
			{
				activationToggle1.isOn = cardDescriptor.heroState.hasActivated[0];
				activationToggle1.gameObject.SetActive( true );
			}
		}

		public void ResetActivation()
		{
			//skip callbacks
			activationToggle1.gameObject.SetActive( false );
			activationToggle2.gameObject.SetActive( false );

			activationToggle1.isOn = false;
			cardDescriptor.heroState.hasActivated[0] = false;
			activationToggle1.gameObject.SetActive( true );

			if ( DataStore.sagaSessionData.MissionHeroes.Count <= 2 && cardDescriptor.characterType == CharacterType.Hero )
			{
				activationToggle2.isOn = false;
				cardDescriptor.heroState.hasActivated[1] = false;
				activationToggle2.gameObject.SetActive( true );
			}
		}

		/// <summary>
		/// When a player clicks DEFEAT, this method decides if it's an initial WOUND or a second DEFEAT
		/// </summary>
		public void OnDefeatCheck()
		{
			//initial wound
			if ( cardDescriptor.characterType != CharacterType.Ally
				&& !cardDescriptor.heroState.isWounded
				&& !cardDescriptor.heroState.isDefeated )
			{
				OnWound();
			}
			else if ( cardDescriptor.characterType == CharacterType.Ally || cardDescriptor.heroState.isWounded )
			{
				OnDefeat();
			}
		}

		public void OnDefeat()
		{
			var ovrd = DataStore.sagaSessionData.gameVars.GetDeploymentOverride( cardDescriptor.id );
			FindObjectOfType<SagaController>().ToggleNavAndEntitySelection( true );

			FindObjectOfType<ConfirmPopup>().Hide();

			if ( ovrd != null && !ovrd.canBeDefeated )
			{
				GlowEngine.FindUnityObject<QuickMessage>().Show( "This Ally cannot be defeated." );
				return;
			}

			//play defeated sound
			FindObjectOfType<Sound>().PlayDefeatedSound();

			DataStore.sagaSessionData.missionLogger.LogEvent( MissionLogType.GroupDefeated, cardDescriptor.name );

			cardDescriptor.heroState.isDefeated = true;
			cardDescriptor.heroState.isWounded = true;

			woundToggle.gameObject.SetActive( false );
			woundToggle.isOn = false;
			woundToggle.gameObject.SetActive( true );
			woundToggle.interactable = false;
			activationToggle1.interactable = false;
			activationToggle2.interactable = false;
			exhaustedOverlay.SetActive( true );

			FindObjectOfType<SagaController>().eventManager.CheckIfEventsTriggered();

			//trigger on defeated Event/Trigger, if it exists
			//not really necessary to check isAlly - heroes do not have an override
			if ( ovrd != null && cardDescriptor.characterType != CharacterType.Hero )
			{
				FindObjectOfType<SagaController>().triggerManager.FireTrigger( ovrd.setTrigger );
				FindObjectOfType<SagaController>().eventManager.DoEvent( ovrd.setEvent );
				DataStore.deployedHeroes.Remove( Card );
				RemoveSelf();
			}
		}

		public void OnWound()
		{
			//play defeated sound
			FindObjectOfType<Sound>().PlayDefeatedSound();

			FindObjectOfType<SagaController>().ToggleNavAndEntitySelection( true );

			cardDescriptor.heroState.isWounded = true;

			FindObjectOfType<ConfirmPopup>().Hide();
			woundToggle.gameObject.SetActive( false );
			woundToggle.isOn = false;
			woundToggle.gameObject.SetActive( true );

			FindObjectOfType<SagaController>().eventManager.CheckIfEventsTriggered();
		}

		/// <summary>
		/// Visually remove the group
		/// </summary>
		public void RemoveSelf()
		{
			Transform tf = transform.GetChild( 0 );
			tf.DOScale( 0, .35f ).SetEase( Ease.InCirc ).OnComplete( () =>
			{
				UnityEngine.Object.Destroy( gameObject );
			} );
		}
	}
}