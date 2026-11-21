public class HeroState
{
	public bool[] hasActivated;

	// P41 (T. ruling 21.11): the 3-hero squad carries FOUR activation
	// markers, but the fourth sits on the BONUS token (the "dummy" card),
	// not on a hero - the physical rule lets the Rebel player choose which
	// hero owns that extra turn, and the choice can change. The token
	// stores the chosen hero here (id + public name); empty = not chosen
	// yet. The token's chip flip then writes the CHOSEN HERO's name into
	// the mission log (M6s-B7 carrier law), never "Dummy Hero" - the feed
	// sees the real hero's second activation (the P40 occurrence dedup
	// already handles a hero closing two turns in one round). JSON-
	// serialized with the card: persists save/restore; old saves read
	// empty = unassigned, same law as a fresh mission.
	public string bonusHolderId;
	public string bonusHolderName;

	public bool isHealthy
	{
		get
		{
			return !isWounded && !isDefeated;
		}
	}
	public bool isWounded;
	public bool isDefeated;//a defeated hero is also marked as wounded

	public void Init()
	{
		//Debug.Log( $"Init Hero State with {playerCount} heroes" );
		//heroHealth = HeroHealth.Healthy;
		isWounded = false;
		isDefeated = false;
		hasActivated = new bool[2] { false, false };
	}
}
