using System.Reflection;
using EFT;
using Aki.Reflection.Patching;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using Comfort.Common;
using static LootValue.Globals;

namespace LootValue
{
	[BepInPlugin(pluginGuid, pluginName, pluginVersion)]
	public class LootValueMod : BaseUnityPlugin
	{
		// BepinEx
		public const string pluginGuid = "IhanaMies.LootValue";
		public const string pluginName = "LootValue";
		public const string pluginVersion = "3.0.0";

		private void Awake()
		{
			Config.SaveOnConfigSet = true;

			logger = Logger;

			SetupConfig();

			new TraderPatch().Enable();
			new TooltipController.ShowTooltipPatch().Enable();
			new HoverItemController.GridItemOnPointerEnterPatch().Enable();
			new HoverItemController.GridItemOnPointerExitPatch().Enable();
			new ClickItemController.ItemViewOnClickPatch().Enable();
			
			SlotColoring.UseDefaultColors();
		}

		internal static ConfigEntry<bool> EnableQuickSell;
		internal static ConfigEntry<bool> SellSimilarItems;
		internal static ConfigEntry<bool> SellOnlySimilarItemsFiR;
		internal static ConfigEntry<bool> OneButtonQuickSell;
		internal static ConfigEntry<bool> SellToTraderIfWeaponIsNonOperational;
		internal static ConfigEntry<bool> SellToTraderBelowProfitThresholdEnabled;
		internal static ConfigEntry<int> SellToTraderProfitThreshold;

		internal static ConfigEntry<bool> SellToTraderBelowDurabilityThresholdEnabled;
		internal static ConfigEntry<int> SellToTraderDurabilityThreshold;

		internal static ConfigEntry<bool> ShowFleaPricesInRaid;
		internal static ConfigEntry<bool> ShowPrices;

		internal static ConfigEntry<bool> IgnoreFleaMaxOfferCount;
		internal static ConfigEntry<bool> ShowFleaPriceBeforeAccess;
		internal static ConfigEntry<bool> ShowPricePerKgAndPerSlotInRaid;

		internal static ConfigEntry<bool> HideLowerPrice;
		internal static ConfigEntry<bool> HideLowerPriceInRaid;

		internal static ConfigEntry<bool> ReducePriceInFleaForBrokenItem;
		internal static ConfigEntry<bool> ShowFleaMarketEligibility;
		internal static ConfigEntry<bool> ShowNonVitalWeaponPartsFleaPrice;

		private void SetupConfig()
		{

			// General: Show Tooltip
			ShowPrices = Config.Bind("0. Item Prices", "0. Show prices when hovering item", true);
			ShowFleaPricesInRaid = Config.Bind("0. Item Prices", "1. Show prices while in raid", true);
			ShowFleaPriceBeforeAccess = Config.Bind("0. Item Prices", "2. Show flea prices before access to it", false);
			ShowPricePerKgAndPerSlotInRaid = Config.Bind("0. Item Prices", "3. Show price per KG & price per slot in raid", false);
			HideLowerPrice = Config.Bind("0. Item Prices", "4. Hide lower price", false);
			HideLowerPriceInRaid = Config.Bind("0. Item Prices", "5. Hide lower price in raid", false);
			ShowFleaMarketEligibility = Config.Bind("0. Item Prices", "6. Show if item is banned from flea market", true);
			ShowNonVitalWeaponPartsFleaPrice = Config.Bind("0. Item Prices", "7. Show flea market price of non vital parts on weapons", false, "This will make the flea market price always appear if the mods prices are higher than the trader price");

			// General: Quick Sell
			EnableQuickSell = Config.Bind("1. Quick Sell", "0. Enable quick sell", true, "Sell any item(s) instantly using the key combination described in 'One button quick sell'.");

			// General -> Quick Sell:
			OneButtonQuickSell = Config.Bind("1. Quick Sell", "1. One button quick sell", true,
@"If disabled: 
[Alt + Shift + Left Click] sells to trader, 
[Alt + Shift + Right Click] sells to flea market

If enabled:
[Alt + Shift + Left Click] sells to either depending on who pays more");

			SellSimilarItems = Config.Bind("1. Quick Sell", "2. Sell all similar items in one go", false, "If you sell one item and have multiple of the same, they will all be sold simultaneously.");
			SellOnlySimilarItemsFiR = Config.Bind("1. Quick Sell", "3. Only sell similar items if they are all FiR", false, "If this is enabled, sell multiple will only work select FiR items.");
			IgnoreFleaMaxOfferCount = Config.Bind("1. Quick Sell", "4. Ignore flea max offer count", false);
			SellToTraderIfWeaponIsNonOperational = Config.Bind("1. Quick Sell", "5. Sell to trader if item is weapon and non operational", false);
			SellToTraderBelowProfitThresholdEnabled = Config.Bind("1. Quick Sell", "6. Sell to trader if flea market price below threshold enabled", false);
			SellToTraderProfitThreshold = Config.Bind("1. Quick Sell", "6.1. Flea market profit threshold", 0,
				"This means that if the flea market profit for this item is below this number, it will be sold to the traders instead. Useful to not clutter offers with minimal impact. If sell multiple at once is enabled, the total profit of the entire offer will be used instead.");
			SellToTraderBelowDurabilityThresholdEnabled = Config.Bind("1. Quick Sell", "7. Sell to trader if below durability threshold enabled", false);
			SellToTraderDurabilityThreshold = Config.Bind("1. Quick Sell", "7.1. Durability threshold %", 0,
				"This means that any item that is below this % of durability, will be sold to trader regardless of flea market price. This means if this value is configured at 50%, an ifak with 150/300 would be sold to the traders instead the flea market.");
			ReducePriceInFleaForBrokenItem = Config.Bind("1. Quick Sell", "8. Reduce flea market offer relative to missing durability of item", true,
				"This means if any item that has durability, i.e: IFAK, has 200/300 (66% durability remaining), it's flea market price will be reduced by 33%. This applies to most of things that have a durability bar.");

		}
	}

	internal class TraderPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(TraderClass).GetConstructors()[0];

		[PatchPostfix]
		private static void PatchPostfix(ref TraderClass __instance)
		{
			__instance.UpdateSupplyData();
		}
	}

	internal static class PlayerExtensions
	{
		private static readonly FieldInfo InventoryControllerField =
			typeof(Player).GetField("_inventoryController", BindingFlags.NonPublic | BindingFlags.Instance);

		public static InventoryControllerClass GetInventoryController(this Player player) =>
			InventoryControllerField.GetValue(player) as InventoryControllerClass;
	}

	internal static class Globals
	{
		public static ManualLogSource logger { get; set; }
	
		public static bool HasRaidStarted()
		{
			bool? inRaid = Singleton<AbstractGame>.Instance?.InRaid;
			return inRaid.HasValue && inRaid.Value;
		}
		
	}

	
}
