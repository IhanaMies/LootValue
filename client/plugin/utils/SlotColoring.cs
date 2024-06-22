using System.Collections.Generic;
using static LootValue.Globals;

namespace LootValue
{
	public struct LootValueConfigColor
	{
		public int UpperBoundPerSlot { get; set; }
		public int UpperBoundTotal { get; set; }
		public string HexColor { get; set; }


		public LootValueConfigColor(int upperBoundPerSlot, int upperBoundTotal, string hexColor)
		{
			UpperBoundPerSlot = upperBoundPerSlot;
			UpperBoundTotal = upperBoundTotal;
			HexColor = hexColor;
		}

	}

	internal static class SlotColoring
	{
		static Dictionary<Colors, string> SlotColors { get; } = new Dictionary<Colors, string>()
		{
			{ Colors.Red, "#dd0000" },
			{ Colors.Orange, "#dda500" },
			{ Colors.Yellow, "#dddd00" },
			{ Colors.Green, "#00dd00" },
			{ Colors.Lightblue, "#00dddd" },
			{ Colors.Purple, "#dd00dd" },
			{ Colors.Pink, "#dd66dd" },
		};

		private enum Colors
		{
			Red,
			Orange,
			Yellow,
			Green,
			Lightblue,
			Purple,
			Pink
		}


		public static readonly List<LootValueConfigColor> DefaultColors = new List<LootValueConfigColor>()
		{
			new LootValueConfigColor(5000, 25000, SlotColors[Colors.Red]),
			new LootValueConfigColor(7500, 50000, SlotColors[Colors.Orange]),
			new LootValueConfigColor(10000, 100000, SlotColors[Colors.Yellow]),
			new LootValueConfigColor(15000, 200000, SlotColors[Colors.Green]),
			new LootValueConfigColor(20000, 300000, SlotColors[Colors.Lightblue]),
			new LootValueConfigColor(25000, 400000, SlotColors[Colors.Purple]),
			new LootValueConfigColor(int.MaxValue, int.MaxValue, SlotColors[Colors.Pink])
		};

		public static List<LootValueConfigColor> ColorConfig = new List<LootValueConfigColor>();

		public static void UseDefaultColors()
		{
			ColorConfig.Clear();
			ColorConfig.AddRange(DefaultColors);
		}
		
		public static string GetColorFromValuePerSlots(int valuePerSlot)
		{
			foreach (var bound in ColorConfig)
			{
				if (valuePerSlot < bound.UpperBoundPerSlot)
				{
					return bound.HexColor;
				}
			}

			return SlotColors[Colors.Pink];
		}

		public static string GetColorFromTotalValue(int totalValue)
		{
			foreach (var bound in ColorConfig)
			{
				if (totalValue < bound.UpperBoundTotal)
				{
					return bound.HexColor;
				}
			}

			return SlotColors[Colors.Pink];
		}
	}
}
