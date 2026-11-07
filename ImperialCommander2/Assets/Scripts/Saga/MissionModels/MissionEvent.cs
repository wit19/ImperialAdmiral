using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Saga
{
	public class MissionEvent
	{
		public Guid GUID { get; set; }
		public bool isGlobal;
		public string name;
		public string eventText;
		public string allyDefeated;
		public string heroWounded;
		public string heroWithdraws;
		public string activationOf;
		public int startOfRound;
		public int endOfRound;
		public bool useStartOfRound;
		public bool useEndOfRound;
		public bool useStartOfEachRound;
		public bool useEndOfEachRound;
		public bool useAllGroupsDefeated;
		public bool useAllHeroesWounded;
		public bool useAllyDefeated;
		public bool useHeroWounded;
		public bool useHeroWithdraws;
		public bool useAnyHeroWounded;
		public bool useActivation;
		public bool behaviorAll;
		public bool isRepeatable;
		public bool isEndOfCurrentRound;
		public bool useAnyHeroDefeated;

		public List<TriggeredBy> additionalTriggers { get; set; }

		[JsonConverter( typeof( EventActionConverter ) )]
		public List<IEventAction> eventActions { get; set; }

		// LANGUAGE-BRIDGE (card t_1787135b P34-A1): authored original of
		// eventText, see EntityProperties.sourceText for the law. The event
		// NAME is never translated (authoring data, GUID-keyed injection only
		// replaces texts) - it needs no source field. Serialized like the
		// other source fields so a post-injection save still carries the
		// original on ContinueGame (capture-once then keeps the TRUE original).
		public string sourceEventText;
	}
}