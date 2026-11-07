using System.Runtime.CompilerServices;
using UnityEngine;
using TacticalAI.Core;

public class HPTrackerContainer : MonoBehaviour
{
	public HPTracker[] trackers;

	// P33-H3 (Player-33 §2c): every bind goes through the board-instance law
	// (TrackerBindLaw, Core - the same function the harness pins). The wheels
	// are ONLY ever bound to the card instance sitting in deployedEnemies;
	// a same-id copy handed in by a popup rail is replaced by the board card
	// and re-initialised FROM it, never the other way. caller = forensic tag
	// naming the rail that bound (ShowSaga / CardView.Show / InitCard).
	// SHARED legacy+tactical rail: pure bugfix, no mode gate (declared).
	public void ResetTracker( DeploymentCard card, string caller = "?" )
	{
		var d = TrackerBindLaw.ResolveBoard( DataStore.deployedEnemies, card, c => c.id );
		if ( d.Rebound )
			Debug.LogWarning( "TRACKER_REBIND::caller=" + caller +
				" popup id=" + ( card.id ?? "?" ) + " hash=" + RuntimeHelpers.GetHashCode( card ) +
				" -> BOARD card=" + d.Bound.name + " id=" + d.Bound.id +
				" hash=" + RuntimeHelpers.GetHashCode( d.Bound ) +
				" (wheels re-read FROM BOARD)" );

		//show it
		gameObject.SetActive( true );
		// P33-H3 law 2: an OPEN SESSION arms the next commit - and ONLY a
		// ResetTracker (fresh read from a card) opens one. NOT OnEnable:
		// a plain parent re-enable (e.g. CardViewPopup.ShowMissionCard) used
		// to re-arm a commit of the stale, already-committed wheels.
		wasVisible = true;   //also covers the already-active container

		for ( int i = 0; i < trackers.Length; i++ )
		{
			//hide trackers and set them to 0
			trackers[i].ResetTracker();
		}

		//show trackers for the # of enemies in the group
		for ( int i = 0; i < trackers.Length; i++ )
		{
			trackers[i].SetValue( d.Bound, i, i < d.Bound.size );
		}

		Debug.Log( "TRACKER_BIND::caller=" + caller + " card=" + d.Bound.name +
			" id=" + d.Bound.id + " hash=" + RuntimeHelpers.GetHashCode( d.Bound ) +
			" wounds=[" + string.Join( ",", d.Bound.woundTrackerValue ?? new int[0] ) + "]" +
			" figs=[" + string.Join( ",", d.Bound.trackerNumbers ?? new int[0] ) + "]" +
			" reason=" + d.Reason );
	}

	// P33-H3 close transaction (law 2): exactly ONE commit per open session.
	// The old unconditional commit is the Player-33 Z746 root cause: a SECOND
	// close (OK double-click / Space during the 0.5 s fade - CardViewPopup
	// has no close guard) wrote the already-reset zero wheels back onto the
	// card: [2,5,0] -> [0,0,0]. Deactivation happens LAST now, so OnDisable
	// sees the consumed flag and cannot double-commit either.
	public void UpdateAndHide()
	{
		if ( trackers == null || trackers.Length == 0 )
		{
			gameObject.SetActive( false );
			return;
		}
		if ( TrackerBindLaw.TryBeginCommit( ref wasVisible ) )
		{
			for ( int i = 0; i < trackers.Length; i++ )
				trackers[i].UpdateWoundValue();
			CommitLog( "close" );
			for ( int i = 0; i < trackers.Length; i++ )
				trackers[i].ResetTracker();
		}
		else
		{
			// stray close: NOTHING is written to the card; the line stays in
			// the log so the next Player-N.log names the rail that double-fired.
			CommitLog( "stray-close" );
		}
		gameObject.SetActive( false );
	}

	//any hide path (UI toggle, scene teardown, SetActive(false) without the
	//OK button) now commits instead of silently discarding the wheel values
	//(Player-31: "values vanished when we clicked elsewhere"); P33-H3: same
	//single-commit transaction, same forensic line.
	private void OnDisable()
	{
		if ( trackers == null || trackers.Length == 0 )
			return;
		if ( TrackerBindLaw.TryBeginCommit( ref wasVisible ) )
		{
			for ( int i = 0; i < trackers.Length; i++ )
				trackers[i].UpdateWoundValue();
			CommitLog( "hide" );
			for ( int i = 0; i < trackers.Length; i++ )
				trackers[i].ResetTracker();
		}
	}

	void CommitLog( string via )
	{
		//forensic trail (Player-31, extended P33-H3): what the tracker windows
		//commit to the card - id + instance hash + figure numbers decide which
		//DeploymentCard OBJECT the rail really wrote to.
		if ( trackers != null && trackers.Length > 0 && trackers[0] != null && trackers[0].GetCard() != null )
		{
			var c = trackers[0].GetCard();
			Debug.Log( "TRACKER_COMMIT::via=" + via + "::card=" + c.name +
				" id=" + c.id + " hash=" + RuntimeHelpers.GetHashCode( c ) +
				" wounds=[" + string.Join( ",", c.woundTrackerValue ?? new int[0] ) + "]" +
				" figs=[" + string.Join( ",", c.trackerNumbers ?? new int[0] ) + "]" );
		}
	}

	bool wasVisible = false;
}
