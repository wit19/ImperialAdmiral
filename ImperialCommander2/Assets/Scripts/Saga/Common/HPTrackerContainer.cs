using UnityEngine;

public class HPTrackerContainer : MonoBehaviour
{
	public HPTracker[] trackers;

	public void ResetTracker( DeploymentCard card )
	{
		//show it
		gameObject.SetActive( true );
		wasVisible = true;   //also covers the already-active container

		for ( int i = 0; i < trackers.Length; i++ )
		{
			//hide trackers and set them to 0
			trackers[i].ResetTracker();
		}

		//show trackers for the # of enemies in the group
		for ( int i = 0; i < trackers.Length; i++ )
		{
			trackers[i].SetValue( card, i, i < card.size );
		}
	}

	public void UpdateAndHide()
	{
		gameObject.SetActive( false );
		for ( int i = 0; i < 3; i++ )
			trackers[i].UpdateWoundValue();

		//forensic trail (Player-31): what the tracker windows commit to the card
		if ( trackers.Length > 0 && trackers[0].GetCard() != null )
		{
			var c = trackers[0].GetCard();
			Debug.Log( "TRACKER_COMMIT::card=" + c.name +
				" wounds=[" + string.Join( ",", c.woundTrackerValue ?? new int[0] ) + "]" );
		}

		for ( int i = 0; i < trackers.Length; i++ )
			trackers[i].ResetTracker();
	}

	//any hide path (UI toggle, scene teardown, SetActive(false) without the
	//OK button) now commits instead of silently discarding the wheel values
	//(Player-31: "values vanished when we clicked elsewhere")
	private void OnDisable()
	{
		if ( trackers == null || trackers.Length == 0 || !wasVisible )
			return;
		for ( int i = 0; i < trackers.Length; i++ )
			trackers[i].UpdateWoundValue();
		wasVisible = false;
	}

	private void OnEnable()
	{
		wasVisible = true;
	}

	bool wasVisible = false;
}
