using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LootValue.Globals;

namespace LootValue
{
	internal static class SlotColoring
	{
		private enum ESlotColor
		{
			Red,
			Orange,
			Yellow,
			Green,
			Lightblue,
			Pink
		}

		static Dictionary<ESlotColor, string> slotColors { get; } = new Dictionary<ESlotColor, string>()
		{
			{ ESlotColor.Red, "#ff0000" },
			{ ESlotColor.Orange, "#ffa500" },
			{ ESlotColor.Yellow, "#ffff00" },
			{ ESlotColor.Green, "#00ff00" },
			{ ESlotColor.Lightblue, "#00ffff" },
			{ ESlotColor.Pink, "#ff00ff" }
		};

		public static string GetColorFromValuePerSlots(int valuePerSlot)
		{
			if (valuePerSlot < 5000)
				return slotColors[ESlotColor.Red];
			else if (valuePerSlot < 7500)
				return slotColors[ESlotColor.Orange];
			else if (valuePerSlot < 10000)
				return slotColors[ESlotColor.Yellow];
			else if (valuePerSlot < 15000)
				return slotColors[ESlotColor.Green];
			else if (valuePerSlot < 20000)
				return slotColors[ESlotColor.Lightblue];

			return slotColors[ESlotColor.Pink];
		}
	}
}
