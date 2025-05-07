using System;
using System.Reflection;
using EFT;
using EFT.UI.DragAndDrop;
using EFT.InventoryLogic;
using EFT.UI;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using CurrencyUtil = GClass2934;
using FleaRequirement = GClass2102;
using static LootValue.Globals;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using UnityEngine;
using System.Collections.Generic;

using System.Threading.Tasks;
using Comfort.Common;
using System.Linq;
using UnityEngine.EventSystems;
using static EFT.Player;
using System.Collections.Concurrent;

namespace LootValue
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class LootValueMod : BaseUnityPlugin
    {
        // BepinEx
        public const string pluginGuid = "IhanaMies.LootValue";
        public const string pluginName = "LootValue";
        public const string pluginVersion = "5.0.2";

		private void Awake()
		{
			Config.SaveOnConfigSet = true;

			logger = Logger;

			SetupConfig();

			new TraderPatch().Enable();
			new ShowTooltipPatch().Enable();
			new GridItemOnPointerEnterPatch().Enable();
			new GridItemOnPointerExitPatch().Enable();
			new ItemViewOnClickPatch().Enable();

			Config.SettingChanged += Config_SettingChanged;
		}

		private void Config_SettingChanged(object sender, SettingChangedEventArgs e)
		{
			ConfigEntryBase entry = e.ChangedSetting;

			logger.LogInfo($"Settings changed - {entry.Definition.Section}:{entry.Definition.Key}");

			if (entry.Definition.Key == "Custom colours")
			{
				if (UseCustomColours.Value)
				{
					logger.LogInfo($"Read colors");
					SlotColoring.ReadColors(CustomColours.Value);
				}
			}

			if (entry.Definition.Key == "Custom colours" || entry.Definition.Key == "Use custom colours")
			{
				if (UseCustomColours.Value)
				{
					SlotColoring.ReadColors(CustomColours.Value);
				}
				else
				{
					SlotColoring.UseDefaultColors();
				}
			}

			if (entry.Definition.Key == "Traders to ignore") {
                blacklistedTraders.Clear();
                blacklistedTraders.AddRange(TraderBlacklist.Value.ToLower().Split(','));
			}
		}

		internal static ConfigEntry<bool> UseFleaPrices;
		internal static ConfigEntry<bool> UseAsyncPrices;
		internal static ConfigEntry<bool> UseCustomColours;
		internal static ConfigEntry<string> CustomColours;
		internal static ConfigEntry<bool> EnableQuickSell;
		internal static ConfigEntry<bool> EnableFleaQuickSell;
		internal static ConfigEntry<bool> OneButtonQuickSell;
		internal static ConfigEntry<bool> OneButtonQuickSellFlea;

		internal static ConfigEntry<bool> showFleaPricesInRaid;
		internal static ConfigEntry<bool> showPrices;

		internal static ConfigEntry<bool> OnlyShowTotalValue;
		internal static ConfigEntry<bool> ShowFleaPriceBeforeAccess;
		internal static ConfigEntry<bool> IgnoreFleaMaxOfferCount;

		internal static ConfigEntry<string> TraderBlacklist;

		internal static ConfigEntry<KeyboardShortcut> QuicksellModifier;

		private void SetupConfig()
		{

			UseFleaPrices = Config.Bind("Flea", "Enable Flea Prices", true); 
			UseAsyncPrices = Config.Bind("Flea", "Use a parallel price acquisition", false, "(Fika compatible)");
			OneButtonQuickSell = Config.Bind("Quick Sell", "One button quick sell", false, "Selling is done using LMB only. Attempts to sell to flea and then to trader if the option is enabled");
			OneButtonQuickSellFlea = Config.Bind("Quick Sell", "Sell to trader if no flea slots left", true, "Does nothing if 'Ignore flea max offer count' is enabled");
			OnlyShowTotalValue = Config.Bind("Quick Sell", "Only show total value", false);
			EnableQuickSell = Config.Bind("Quick Sell", "Enable quick selling", true);
			EnableFleaQuickSell = Config.Bind("Quick Sell", "Enable quick selling to flea", true, "Does nothing if quick selling is disabled");
			ShowFleaPriceBeforeAccess = Config.Bind("Flea", "Show flea price before having access to flea (character level 15)", false);
			IgnoreFleaMaxOfferCount = Config.Bind("Flea", "Ignore flea max offer count", false);

			UseCustomColours = Config.Bind("Colours", "Use custom colours", false);
			CustomColours = Config.Bind("Colours", "Custom colours", "[5000:#ff0000],[10000:#ffff00],[:#ffffff]",
@"Colouring bound is marked as [int:hexcolor] e.q. [lower than this value : will be this hexcolor]
The values should incremental from lower to higher and last value should be valueless.
For example [5000:#ff0000],[10000:#ffff00],[:#ffffff] means three different bounds.
Anything under 5000 rubles, will be red.
Anything under 10000 rubles, will be yellow.
The third is marked as the ultimate color. Anything over 10000 rubles would be white.
"
			);

			showFleaPricesInRaid = Config.Bind("Quick Sell", "Show flea prices in raid", true);
			showPrices = Config.Bind("Quick Sell", "Show prices", true);

			TraderBlacklist = Config.Bind("Traders", "Traders to ignore", "", "Separate values by comma, must use trader's id which is usually their name. The trader Id can also be found in user/mods/%trader_name%/db/base.json");

            blacklistedTraders.AddRange(TraderBlacklist.Value.ToLower().Split(','));

            if (UseCustomColours.Value)
				SlotColoring.ReadColors(CustomColours.Value);

			QuicksellModifier = Config.Bind("General", "Modifier to enable quickselling", new KeyboardShortcut(KeyCode.LeftAlt, new KeyCode[1] { KeyCode.LeftShift }), "Modifier to enable quickselling. Two button mode: Left Mouse Button sell to trader. Right Mouse Button sell to flea. One button mode: Left Mouse Button both");
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

		public static PlayerInventoryController GetInventoryController(this Player player) =>
			InventoryControllerField.GetValue(player) as PlayerInventoryController;
	}

	internal static class Globals
	{
		public static bool isStashItemHovered = false;
		public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();
		public static ManualLogSource logger { get; set; }
		public static Item hoveredItem;
		public static SimpleTooltip tooltip;
		public static List<string> blacklistedTraders = new List<string>();
		public static ConcurrentDictionary<string, Task<double?>> _activeRequests = new();

		public static bool HasRaidStarted()
		{			
			bool? inRaid = Singleton<AbstractGame>.Instance?.InRaid;
			return inRaid.HasValue && inRaid.Value;
		}

		public static TraderOffer GetBestTraderOffer(Item item)
		{
			if (!Session.Profile.Examined(item))
				return null;

			switch (item.Owner?.OwnerType)
			{
				case EOwnerType.RagFair:
				case EOwnerType.Trader:
					if (item.StackObjectsCount > 1 || item.UnlimitedCount)
					{
						item = item.CloneItem();
						item.StackObjectsCount = 1;
						item.UnlimitedCount = false;
					}
					break;
			}

			TraderOffer highestOffer = null;

			if (item is Weapon weapon)
			{
				foreach (TraderClass trader in Session.Traders)
				{
					if (blacklistedTraders.Contains(trader.Id.ToLower()))
						continue;

                    if (!trader.Info.Available || trader.Info.Disabled || !trader.Info.Unlocked || trader.Settings.AvailableInRaid)
						continue;

					if (GetTraderOffer(weapon, trader) is TraderOffer offer)
					{
						if (highestOffer == null || offer.Price > highestOffer.Price)
							highestOffer = offer;
					}
				}
			}
			else
			{
				foreach (TraderClass trader in Session.Traders)
				{
					if (blacklistedTraders.Contains(trader.Id.ToLower()))
						continue;

                    if (!trader.Info.Available || trader.Info.Disabled || !trader.Info.Unlocked || trader.Settings.AvailableInRaid)
						continue;

					if (GetTraderOffer(item, trader) is TraderOffer offer)
						if (highestOffer == null || offer.Price > highestOffer.Price)
							highestOffer = offer;
				}
			}

			return highestOffer;
		}

		public class FleaPriceRequest
		{
			public string templateId;
			public FleaPriceRequest(string templateId) => this.templateId = templateId;
		}

		private static TraderOffer GetTraderOffer(Item item, TraderClass trader)
		{
			var result = trader.GetUserItemPrice(item);

			return result.HasValue ? new TraderOffer(
				trader.Id,
				trader.LocalizedName,
				result.Value.Amount,
				CurrencyUtil.GetCurrencyCharById(result.Value.CurrencyId.Value),
				trader.GetSupplyData().CurrencyCourses[result.Value.CurrencyId.Value],
				item.StackObjectsCount
			) : null;
		}

		public sealed class TraderOffer
		{
			public string TraderId;
			public string TraderName;
			public int Price;
			public string Currency;
			public double Course;
			public int Count;

			public TraderOffer(string traderId, string traderName, int price, string currency, double course, int count)
			{
				TraderId = traderId;
				TraderName = traderName;
				Price = price;
				Currency = currency;
				Course = course;
				Count = count;
			}
		}
	}

	internal class GridItemOnPointerEnterPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(GridItemView).GetMethod("OnPointerEnter", BindingFlags.Instance | BindingFlags.Public);

		[PatchPrefix]
		static void Prefix(GridItemView __instance, PointerEventData eventData)
		{
			if (__instance.Item != null)
			{
				hoveredItem = __instance.Item;
				Globals.isStashItemHovered = true;
			}
		}
	}

	internal class GridItemOnPointerExitPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(GridItemView).GetMethod("OnPointerExit", BindingFlags.Instance | BindingFlags.Public);

		[PatchPrefix]
		static void Prefix(GridItemView __instance, PointerEventData eventData)
		{
			Globals.isStashItemHovered = false;
			hoveredItem = null;
		}
	}

	public class SellItemToTraderRequest
	{
		public string ItemId;
		public string TraderId;
		public int Price;

		public SellItemToTraderRequest(string itemId, string traderId, int price)
		{
			this.ItemId = itemId;
			this.TraderId = traderId;
			this.Price = price;
		}
	}

	internal class ItemViewOnClickPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(GridItemView).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.Public);

		private static HashSet<string> itemSells = new HashSet<string>();

		private static DateTime lastFleaSellNotification = DateTime.MinValue;
		private static readonly int minSecondsBetweenNotifications = 30;

		[PatchPrefix]
		static bool Prefix(GridItemView __instance, PointerEventData.InputButton button, Vector2 position, bool doubleClick)
		{
			bool runOriginalMethod = true;
			if (__instance == null || __instance.Item == null || itemSells.Contains(__instance.Item.Id))
			{
				if (tooltip != null)
				{
					tooltip.Close();
					tooltip = null;
					hoveredItem = null;
				}

				return runOriginalMethod;
			}

			Item item = __instance.Item;
			try
			{
				itemSells.Add(item.Id);

				if (LootValueMod.EnableQuickSell.Value && !Globals.HasRaidStarted())
				{
					if (IsKeyPressed(LootValueMod.QuicksellModifier.Value))
					{

						if (LootValueMod.OneButtonQuickSell.Value)
						{
							if (button == PointerEventData.InputButton.Left)
							{
								TraderOffer bestTraderOffer = GetBestTraderOffer(item);
								double? fleaPrice = null;

								if (item.CanSellOnRagfair)
									fleaPrice = Task.Run(() => FleaPriceCache.FetchPrice(item.TemplateId)).Result;					

								if (bestTraderOffer != null)
								{
									if (fleaPrice.HasValue && fleaPrice.Value > bestTraderOffer.Price)
									{
										if (!HasFleaSlotToSell())
										{
											itemSells.Remove(item.Id);
											if (LootValueMod.OneButtonQuickSellFlea.Value)
											{
												NotificationManagerClass.DisplayWarningNotification("Maximum number of flea offers reached. Sell to trader");
												SellToTrader(item, bestTraderOffer);
											}
											else
											{
												NotificationManagerClass.DisplayWarningNotification("Maximum number of flea offers reached");
											}

											return false;
										}

										TryAddOfferToFlea(item, fleaPrice.Value);
									}
									else
									{
										SellToTrader(item, bestTraderOffer);
									}
								}
							}
						}
						else //Two button quicksell
						{
							if (button == PointerEventData.InputButton.Left)
							{
								runOriginalMethod = false;
								SellToTrader(item);
							}
							else if (button == PointerEventData.InputButton.Right)
							{
								runOriginalMethod = false;
								SellToFlea(item);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex.Message);

				if (ex.InnerException != null)
				{
					logger.LogError(ex.InnerException.Message);
				}
			}
			finally
			{
				itemSells.Remove(item.Id);
			}

			return runOriginalMethod;
		}

		static bool IsItemInStashAndNotInContainer(Item item)
		{
			return true;
			//if (item.Parent.Container.ParentItem is StashClass)
			//	return true;

			//if (lastFleaSellNotification.AddSeconds(minSecondsBetweenNotifications) < DateTime.Now)
			//{
			//	lastFleaSellNotification = DateTime.Now;
			//	NotificationManagerClass.DisplayWarningNotification("Flea quicksell from a container or character is broken. Wait for SPT fix");
			//}

			//return false;
		}

		static void SellToTrader(Item item)
		{
			string itemId = item.Id;
			try
			{
				TraderOffer bestTraderOffer = GetBestTraderOffer(item);

				if (bestTraderOffer != null)
					SellToTrader(item, bestTraderOffer);

				itemSells.Remove(itemId);
			}
			catch (Exception ex)
			{
				itemSells.Remove(itemId);

				logger.LogInfo($"Something fucked up: {ex.Message}");
				logger.LogInfo($"{ex.InnerException.Message}");
			}
		}

		private static void SellToTrader(Item item, TraderOffer bestTraderOffer)
		{
			TraderClass tc = Session.GetTrader(bestTraderOffer.TraderId);

			var @class = new GClass2332.Class1936();
			@class.source = new TaskCompletionSource<bool>();

			var itemRef = new EFT.Trading.TradingItemReference
			{
				Item = item,
				Count = item.StackObjectsCount
			};

			Session.ConfirmSell(
				tc.Id,
				new EFT.Trading.TradingItemReference[1] { itemRef },
				bestTraderOffer.Price,
				new Callback(@class.method_0)
			);
			Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.TradeOperationComplete);
		}

		static bool HasFleaSlotToSell()
		{
			return LootValueMod.IgnoreFleaMaxOfferCount.Value || Session.RagFair.MyOffersCount < Session.RagFair.GetMaxOffersCount(Session.RagFair.MyRating);
		}

		static void SellToFlea(Item item)
		{
			if (!Session.RagFair.Available)
				return;

			double? fleaPrice = null;

			if (item.CanSellOnRagfair)
				fleaPrice = Task.Run(() => FleaPriceCache.FetchPrice(item.TemplateId)).Result;

			if (Session.RagFair.Available && fleaPrice.HasValue)
			{
				if (!HasFleaSlotToSell())
				{
					NotificationManagerClass.DisplayWarningNotification("Maximum number of flea offers reached");
					return;
				}

				TryAddOfferToFlea(item, fleaPrice.Value);
			}
		}

		static void TryAddOfferToFlea(Item item, double unadjustedPrice)
		{
			if (IsItemInStashAndNotInContainer(item))
			{
				var g = new FleaRequirement()
				{
					count = unadjustedPrice - 1, //undercut by 1 ruble
					_tpl = "5449016a4bdc2d6f028b456f" //id of ruble
				};

				FleaRequirement[] gs = new FleaRequirement[1] { g };
				Session.RagFair.AddOffer(false, new string[1] { item.Id }, gs, null);
			}
		}

		//Credit to DrakiaXYZ. Modified by me
		static bool IsKeyPressed(KeyboardShortcut key)
		{
			if (!Input.GetKey(key.MainKey))
				return false;

			foreach (var modifier in key.Modifiers)
				if (!Input.GetKey(modifier))
					return false;

			return true;
		}
	}

	internal class ShowTooltipPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(SimpleTooltip).GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(x => x.Name == "Show").ToList()[0];
		}

		[PatchPrefix]
		private static void Prefix(ref string text, ref Vector2? offset, ref float delay, SimpleTooltip __instance)
		{
			delay = 0;
			tooltip = __instance;
			if (LootValueMod.UseAsyncPrices.Value)
			{
				if (isValidItem(hoveredItem))
					text += "\nLoading...";
				return;
			}

			bool isFleaEligible = false;
			double lowestFleaOffer = 0;

			bool inRaidAndCanShowInRaid = HasRaidStarted() && LootValueMod.showFleaPricesInRaid.Value;

			if (hoveredItem != null && Session.Profile.Examined(hoveredItem) && LootValueMod.showPrices.Value && (!HasRaidStarted() || inRaidAndCanShowInRaid))
			{
				if (hoveredItem.Owner.OwnerType != EOwnerType.Profile && hoveredItem.Owner.GetType() == typeof(TraderControllerClass))
					return;

				TraderOffer bestTraderOffer = GetBestTraderOffer(hoveredItem);

				//For weapons we want to fetch each mods flea price, if eligible
				if (hoveredItem is Weapon weapon)
				{
					double totalFleaPrice = 0;

					foreach (Mod mod in weapon.Mods)
					{
						double? fleaPrice = Task.Run(() => FleaPriceCache.FetchPrice(mod.TemplateId)).Result;

						if (fleaPrice.HasValue && fleaPrice.Value > 0)
						{
							isFleaEligible = true;
							totalFleaPrice += fleaPrice.Value * mod.StackObjectsCount;
						}
					}

					if (totalFleaPrice > 0)
						lowestFleaOffer = totalFleaPrice;
				}
				else
				{
					double? fleaPrice = Task.Run(() => FleaPriceCache.FetchPrice(hoveredItem.TemplateId)).Result;

					if (fleaPrice.HasValue && fleaPrice.Value > 0)
					{
						isFleaEligible = true;
						lowestFleaOffer = fleaPrice.Value * hoveredItem.StackObjectsCount;
					}
				}

				int fleaPricePerSlot = 0, traderPricePerSlot = 0;

				var size = hoveredItem.CalculateCellSize();
				int slots = size.X * size.Y;

				if (isFleaEligible)
					fleaPricePerSlot = (int)Math.Round(lowestFleaOffer / slots);

				if (bestTraderOffer != null)
				{
					double totalTraderPrice = bestTraderOffer.Price;
					traderPricePerSlot = (int)Math.Round(totalTraderPrice / slots);

					SetText(traderPricePerSlot, fleaPricePerSlot, totalTraderPrice, slots, ref text, bestTraderOffer.TraderName);
				}

				if (isFleaEligible)
					SetText(fleaPricePerSlot, traderPricePerSlot, lowestFleaOffer, slots, ref text, "Flea");
			}
		}

		private static void SetText(int valuePerSlotA, int valuePerSlotB, double totalValue, int slots, ref string text, string buyer)
		{
			string perSlotColor = SlotColoring.GetColorFromValuePerSlots(valuePerSlotA);
			string highlightText;

			if (valuePerSlotA > valuePerSlotB)
				highlightText = $"<color=#ffffff>{buyer}</color>";
			else
				highlightText = buyer;

			if (LootValueMod.OnlyShowTotalValue.Value)
			{
				text += $"<br>{highlightText}: <color={perSlotColor}>{totalValue.FormatNumber()}</color>";
			}
			else
			{
				text += $"<br>{highlightText}: <color={perSlotColor}>{valuePerSlotA.FormatNumber()}</color>";

				if (slots > 1)
					text += $" Total: {totalValue.FormatNumber()}";
			}
		}

                private static bool isValidItem(Item hovered_Item)
		{
			if (hoveredItem == null) return false;
			if (hoveredItem.TemplateId != hovered_Item?.TemplateId || hoveredItem.Id != hovered_Item?.Id) return false;
			bool inRaidAndCanShowInRaid = HasRaidStarted() && LootValueMod.showFleaPricesInRaid.Value;
			if (Session.Profile.Examined(hoveredItem) && LootValueMod.showPrices.Value && (!HasRaidStarted() || inRaidAndCanShowInRaid))
			{
				if (hoveredItem.Owner.OwnerType != EOwnerType.Profile && hoveredItem.Owner.GetType() == typeof(TraderControllerClass))
					return false;
				return true;
			}
			return false;
		} 

		[PatchPostfix]
		private static async void Postfix(string text)
		{
			Item hovered_Item = hoveredItem;
			try
			{
				if (!LootValueMod.UseAsyncPrices.Value)
					return;
				bool isFleaEligible = false;
				double lowestFleaOffer = 0;              

				if (!isValidItem(hovered_Item))
					return;            

				TraderOffer bestTraderOffer = GetBestTraderOffer(hovered_Item);

				//For weapons we want to fetch each mods flea price, if eligible
				if (hovered_Item is Weapon weapon)
				{
					double totalFleaPrice = 0;

					var eligibleMods = weapon.Mods.Where(mod => mod.CanSellOnRagfair).ToList();

					var results = await Task.WhenAll(
						eligibleMods.Select(async mod =>
						{
							var price = await _activeRequests.GetOrAdd(
								mod.TemplateId,
								key => FleaPriceCache.FetchPrice(key) // running loading if no key
							);
							_activeRequests.TryRemove(mod.TemplateId, out _);
							return (mod, price);
						})
					);                 

					foreach (var (mod, price) in results)
					{
						if (price.HasValue && price.Value > 0)
						{
							isFleaEligible = true;
							totalFleaPrice += price.Value * mod.StackObjectsCount;
						}
					}                  

					if (totalFleaPrice > 0)
						lowestFleaOffer = totalFleaPrice;
				}
				else
				{
					double? fleaPrice = 0;

					fleaPrice = await _activeRequests.GetOrAdd(
						hovered_Item.TemplateId,
						key => FleaPriceCache.FetchPrice(key) // running loading if no key
					);

					_activeRequests.TryRemove(hovered_Item.TemplateId, out _);

					if (fleaPrice.HasValue && fleaPrice.Value > 0)
					{
						isFleaEligible = true;
						lowestFleaOffer = fleaPrice.Value * hovered_Item.StackObjectsCount;
					}
				}

				int fleaPricePerSlot = 0, traderPricePerSlot = 0;

				var size = hovered_Item.CalculateCellSize();
				int slots = size.X * size.Y;

				if (isFleaEligible)
					fleaPricePerSlot = (int)Math.Round(lowestFleaOffer / slots);

				if (bestTraderOffer != null)
				{
					double totalTraderPrice = bestTraderOffer.Price;
					traderPricePerSlot = (int)Math.Round(totalTraderPrice / slots);

					text += customSetText(traderPricePerSlot, fleaPricePerSlot, totalTraderPrice, slots, bestTraderOffer.TraderName);
				}

				if (isFleaEligible)
					text += customSetText(fleaPricePerSlot, traderPricePerSlot, lowestFleaOffer, slots, "Flea");

			}
			catch (Exception ex)
			{
				logger.LogInfo($"Error due getting prices async (Postfix): {ex.Message}");
			}
			finally
			{
				if (isValidItem(hovered_Item) && tooltip != null)
					tooltip.SetText(text.Replace("\nLoading...", ""));
			}
		}

		private static string customSetText(int valuePerSlotA, int valuePerSlotB, double totalValue, int slots, string buyer)
		{
			string text = "";
			string perSlotColor = SlotColoring.GetColorFromValuePerSlots(valuePerSlotA);
			string highlightText;

			if (valuePerSlotA > valuePerSlotB)
				highlightText = $"<color=#ffffff>{buyer}</color>";
			else
				highlightText = buyer;

			if (LootValueMod.OnlyShowTotalValue.Value)
			{
				text += $"<br>{highlightText}: <color={perSlotColor}>{totalValue.FormatNumber()}</color>";
			}
			else
			{
				text += $"<br>{highlightText}: <color={perSlotColor}>{valuePerSlotA.FormatNumber()}</color>";

				if (slots > 1)
					text += $" Total: {totalValue.FormatNumber()}";
			}
			return text;
		}			
	}
}
