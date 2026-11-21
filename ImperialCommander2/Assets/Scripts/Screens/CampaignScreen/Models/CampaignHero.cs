using System.Collections.Generic;

namespace Saga
{
	public class CampaignHero
	{
		public string heroID;
		public string mugShotPath;
		public List<CampaignItem> campaignItems = new List<CampaignItem>();
		public List<CampaignSkill> campaignSkills = new List<CampaignSkill>();
		// P42 (T. ruling 21.11): personal side-mission REWARD cards assigned
		// to this hero (Diala's 'Shu Yen's Lightsaber' belongs to Diala -
		// printed on the card as RewardType.Personal with extra = hero name).
		// Reward ids (rewards.json), moved out of the shared team tray.
		// Old saves read an empty list; the tray keeps those cards untouched
		// (Null-Law, nothing lost).
		public List<string> campaignRewards = new List<string>();
		public int xpAmount;
	}
}
