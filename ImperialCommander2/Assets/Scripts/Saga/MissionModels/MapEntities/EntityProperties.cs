using System.Collections.Generic;

namespace Saga
{
	public class EntityProperties
	{
		public string name { get; set; }
		public bool isActive { get; set; }
		public string theText { get; set; }
		public string entityColor;
		public List<ButtonAction> buttonActions { get; set; } = new List<ButtonAction>();

		// LANGUAGE-BRIDGE (card t_1787135b P34-A1, Thomas 08.11): the authored
		// ORIGINAL of theText, preserved by the TranslationController chokepoint
		// BEFORE injection overwrites theText. Producers (TacticalAI feed) read
		// ONLY this field (fallback theText when never injected = EN, i.e. the
		// identical text); the UI keeps reading theText. Serialized on purpose:
		// a save written after injection still carries the original on restore.
		public string sourceText;

		public EntityProperties()
		{
			isActive = true;
		}
	}
}