using System;
using System.Reflection;
using EFT;
using EFT.UI.DragAndDrop;
using EFT.InventoryLogic;
using EFT.UI;
using Aki.Reflection.Patching;
using Aki.Reflection.Utils;
using CurrencyUtil = GClass2517;
using FleaRequirement = GClass1844;
using static LootValue.Globals;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Threading.Tasks;
using Comfort.Common;
using System.Linq;

namespace LootValue
{
	[BepInPlugin(pluginGuid, pluginName, pluginVersion)]
	public class LootValueMod : BaseUnityPlugin
	{
		// BepinEx
		public const string pluginGuid = "IhanaMies.LootValue";
		public const string pluginName = "LootValue";
		public const string pluginVersion = "2.1.0";

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

		}

		internal static ConfigEntry<bool> UseCustomColours;
		internal static ConfigEntry<string> CustomColours;
		internal static ConfigEntry<bool> EnableQuickSell;
		internal static ConfigEntry<bool> SellAllItemsFindInRaid;
		internal static ConfigEntry<bool> OneButtonQuickSell;

		internal static ConfigEntry<bool> ShowFleaPricesInRaid;
		internal static ConfigEntry<bool> ShowPrices;

		internal static ConfigEntry<bool> IgnoreFleaMaxOfferCount;
		internal static ConfigEntry<bool> ShowFleaPriceBeforeAccess;
		internal static ConfigEntry<bool> ShowPricePerKgAndPerSlotInRaid;

		internal static ConfigEntry<bool> HideLowerPrice;
		internal static ConfigEntry<bool> HideLowerPriceInRaid;

		internal static ConfigEntry<bool> ReducePriceInFleaForBrokenItem;

		private void SetupConfig()
		{

			// General: Show Tooltip
			ShowPrices = Config.Bind("0. Item Prices", "0. Show prices when hovering item", true);
			ShowFleaPricesInRaid = Config.Bind("0. Item Prices", "1. Show prices while in raid", true);
			ShowFleaPriceBeforeAccess = Config.Bind("0. Item Prices", "2. Show flea prices before access to it", false);
			ShowPricePerKgAndPerSlotInRaid = Config.Bind("0. Item Prices", "3. Show price per KG & price per slot in raid", false);
			HideLowerPrice = Config.Bind("0. Item Prices", "4. Hide lower price", false);
			HideLowerPriceInRaid = Config.Bind("0. Item Prices", "5. Hide lower price in raid", false);
			ReducePriceInFleaForBrokenItem = Config.Bind("0. Item Prices", "6. Lower price for items that have missing durability", true);

			// General: Quick Sell
			EnableQuickSell = Config.Bind("1. Quick Sell", "0. Enable quick sell", true, "Sell any item(s) instantly using the key combination described in 'One button quick sell'.");

			// General -> Quick Sell:
			OneButtonQuickSell = Config.Bind("1. Quick Sell", "1. One button quick sell", true,
@"If disabled: 
[Alt + Shift + Left Click] sells to trader, 
[Alt + Shift + Right Click] sells to flea market

If enabled:
[Alt + Shift + Left Click] sells to either depending on who pays more");

			SellAllItemsFindInRaid = Config.Bind("1. Quick Sell", "2. Sell all similar FiR items in one go", false, "If you sell one FiR item and have multiple of the same, they will all be sold simultaneously.");
			IgnoreFleaMaxOfferCount = Config.Bind("1. Quick Sell", "3. Ignore flea max offer count", false);

			// Prices custom colors
			UseCustomColours = Config.Bind("3. Price Per Slot Colours", "0. Use custom colours per slot", false);
			CustomColours = Config.Bind("3. Price Per Slot Colours", "1. Custom colours per slot",
				"[5000:#ff0000],[10000:#ffff00],[:#ffffff]",
				@"Colouring bound is marked as [int:hexcolor] e.q. [lower than this value : will be this hexcolor]
The values should incremental from lower to higher and last value should be valueless.
For example [5000:#ff0000],[10000:#ffff00],[:#ffffff] means three different bounds.
Anything under 5000 rubles, will be red.
Anything under 10000 rubles, will be yellow.
The third is marked as the ultimate color. Anything over 10000 rubles would be white.");

			if (UseCustomColours.Value)
				SlotColoring.ReadColors(CustomColours.Value);
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
		public static bool isStashItemHovered = false;
		public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();
		public static ManualLogSource logger { get; set; }
		public static Item hoveredItem;
		public static SimpleTooltip tooltip;

		public static bool HasRaidStarted()
		{
			bool? inRaid = Singleton<AbstractGame>.Instance?.InRaid;
			return inRaid.HasValue && inRaid.Value;
		}

		public static TraderOffer GetBestTraderOffer(Item item)
		{
			if (!Session.Profile.Examined(item))
				return null;

			// TODO: Always clone item and empty it (?) so we can reliably check base item price on trader
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

			// TODO refactor, arent they equal
			if (item is Weapon weapon)
			{
				foreach (TraderClass trader in Session.Traders)
				{

					if (!trader.Info.Available || trader.Info.Disabled || !trader.Info.Unlocked)
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

					if (!trader.Info.Available || trader.Info.Disabled || !trader.Info.Unlocked)
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
			if (result == null)
				return null;

			return new TraderOffer(
				trader.Id,
				trader.LocalizedName,
				result.Value.Amount,
				CurrencyUtil.GetCurrencyCharById(result.Value.CurrencyId),
				trader.GetSupplyData().CurrencyCourses[result.Value.CurrencyId],
				item.StackObjectsCount
			);
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


		public static bool ContainsNonFleableItemsInside(Item item)
		{
			return item.GetAllItems().Any(i => i.Template.CanSellOnRagfair == false);
		}

		public static bool WillBePurchasableByAtLeastOneTrader(Item item)
		{
			return GetBestTraderOffer(item) != null;
		}


		public static bool IsContainerEmpty(Item item)
		{

			if (!item.IsContainer)
			{
				return false;
			}

			if (item.GetFirstLevelItems().Count > 1)
			{
				return false;
			}

			return true;
		}

		public static bool CanBeSoldInFleaRightNow(Item item, bool displayWarning = true)
		{

			if (!Session.RagFair.Available)
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Flea market is not enabled.");

				return false;
			}

			// we need to check if the base item is sellable
			if (!item.Template.CanSellOnRagfair)
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item can't be sold on flea.");

				return false;
			}

			if (!HasFleaSlotToSell())
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Maximum number of flea offers reached.");

				return false;
			}

			if (item.IsNotEmpty())
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item is not empty.");

				return false;

			}

			if (ContainsNonFleableItemsInside(item))
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item contains forbidden fleamarket items.");

				return false;
			}

			// fallback as any other reason will get caught here
			if (!item.CanSellOnRagfair)
			{
				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item can't be sold right now.");

				return false;
			}


			return true;
		}

		public static bool HasFleaSlotToSell()
		{
			return LootValueMod.IgnoreFleaMaxOfferCount.Value || Session.RagFair.MyOffersCount < Session.RagFair.GetMaxOffersCount(Session.RagFair.MyRating);
		}

		public static int GetFleaValue(Item item)
		{

			var price = FleaPriceCache.FetchPrice(item.TemplateId);
			if (!price.HasValue)
			{
				return 0;
			}

			return (int)price.Value;
		}

		public static int GetFleaMarketUnitPrice(Item item)
		{
			if(!item.Template.CanSellOnRagfair) {
				return 0;
			}

			int unitPrice = GetFleaValue(item);
			return unitPrice;
		}

		public static int GetFleaMarketUnitPriceWithModifiers(Item item) 
		{
			int price = GetFleaMarketUnitPrice(item);

			bool applyConditionReduction = LootValueMod.ReducePriceInFleaForBrokenItem.Value;
			if(applyConditionReduction) 
			{
				price = (int) (price * GetResourcePercentageOfItem(hoveredItem));
			}

			return price;
		}

		public static float GetResourcePercentageOfItem(Item item)
		{

			// TODO support more things: fuel & repair kits

			if (item.GetItemComponent<RepairableComponent>() != null)
			{
				var repairable = item.GetItemComponent<RepairableComponent>();

				var actualMax = repairable.TemplateDurability;
				var currentDurability = repairable.Durability;
				var currentPercentage = currentDurability / actualMax;
				return currentPercentage;

			}
			else if (item.GetItemComponent<MedKitComponent>() != null)
			{

				return item.GetItemComponent<MedKitComponent>().RelativeValue;

			}
			else if (item.GetItemComponent<FoodDrinkComponent>() != null)
			{

				return item.GetItemComponent<FoodDrinkComponent>().RelativeValue;

			}
			else if (item.GetItemComponent<ArmorHolderComponent>() != null)
			{
				var component = item.GetItemComponent<ArmorHolderComponent>();

				if (component.LockedArmorPlates.Count() == 0)
				{
					return 1.0f;
				}

				var maxDurabilityOfAllBasePlates = component.LockedArmorPlates.Sum(plate => plate.Armor.Repairable.TemplateDurability);
				var currentDurabilityOfAllBasePlates = component.LockedArmorPlates.Sum(plate => plate.Armor.Repairable.Durability);
				var currentPercentage = currentDurabilityOfAllBasePlates / maxDurabilityOfAllBasePlates;
				return currentPercentage;
			}

			return 1.0f;

		}

		public static bool ItemBelongsToTraderOrFleaMarketOrMail(Item item)
		{

			if (item == null)
			{
				return false;
			}

			if (item.Owner == null)
			{
				return false;
			}


			var ownerType = item.Owner.OwnerType;
			if (EOwnerType.Trader.Equals(ownerType))
			{
				return true;
			}
			else if (EOwnerType.RagFair.Equals(ownerType))
			{
				return true;
			}
			else if (EOwnerType.Mail.Equals(ownerType))
			{
				return true;
			}

			return false;

		}

		public static bool IsItemInPlayerInventory(Item item)
		{
			var ownerType = item.Owner.OwnerType;
			return EOwnerType.Profile.Equals(ownerType);
		}

		/**
		* Includes original item!
		*/
		public static IEnumerable<Item> GetItemsSimilarToItemWithinSameContainer(Item item)
		{

			if (item == null)
			{
				return Enumerable.Empty<Item>();
			}

			if (item.Parent == null)
			{
				return Enumerable.Empty<Item>();
			}

			if (item.Parent.Container == null)
			{
				return Enumerable.Empty<Item>();
			}

			var itemsOfParent = item.Parent.Container.Items;
			return itemsOfParent.Where(o => item.Compare(o) && o.MarkedAsSpawnedInSession);
		}

		/**
		* Includes original item!
		*/
		public static int CountItemsSimilarToItemWithinSameContainer(Item item)
		{
			return GetItemsSimilarToItemWithinSameContainer(item).Count();
		}

		public static void SellFleaItemOrMultipleItemsIfEnabled(Item item)
		{

			// If I click on one FiR item, it will attempt to sell all the same items (FiR) in the same flea offer
			if (LootValueMod.SellAllItemsFindInRaid.Value && item.MarkedAsSpawnedInSession)
			{
				logger.Log(LogLevel.Info, $"1 Selling multiple items");
				var itemsSimilarToTheOneImSelling = GetItemsSimilarToItemWithinSameContainer(item);
				SellToFlea(item, itemsSimilarToTheOneImSelling);
			}
			else
			{
				logger.Log(LogLevel.Info, $"1 Selling one item");
				SellToFlea(item);
			}

		}

		public static void SellToTrader(Item item)
		{
			string itemId = item.Id;
			try
			{
				TraderOffer bestTraderOffer = GetBestTraderOffer(item);

				if (bestTraderOffer == null)
				{
					NotificationManagerClass.DisplayWarningNotification("No trader will purchase this item.");
					return;
				}

				SellToTrader(item, bestTraderOffer);
			}
			catch (Exception ex)
			{
				logger.LogInfo($"Something fucked up: {ex.Message}");
				logger.LogInfo($"{ex.InnerException.Message}");
			}
		}

		public static void SellToTrader(Item item, TraderOffer bestTraderOffer)
		{
			TraderClass tc = Session.GetTrader(bestTraderOffer.TraderId);

			GClass2047.Class1737 @class = new GClass2047.Class1737();
			@class.source = new TaskCompletionSource<bool>();

			var itemRef = new EFT.Trading.TradingItemReference
			{
				Item = item,
				Count = item.StackObjectsCount
			};

			Session.ConfirmSell(tc.Id, new EFT.Trading.TradingItemReference[1] { itemRef }, bestTraderOffer.Price, new Callback(@class.method_0));
			Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.TradeOperationComplete);
		}



		public static void SellToFlea(Item itemToCheck, IEnumerable<Item> itemsToSell)
		{
			if (!CanBeSoldInFleaRightNow(itemToCheck))
			{
				return;
			}

			var price = GetFleaMarketUnitPriceWithModifiers(itemToCheck);
			var ids = itemsToSell.Select(i => i.Id).ToArray();

			ApplyFleaOffer(price, ids);
		}

		public static void SellToFlea(Item itemToSell)
		{
			if (!CanBeSoldInFleaRightNow(itemToSell))
			{
				return;
			}

			var price = GetFleaMarketUnitPriceWithModifiers(itemToSell);
			var ids = new string[1] { itemToSell.Id };

			ApplyFleaOffer(price, ids);
		}

		private static void ApplyFleaOffer(int price, string[] itemIds)
		{
			var offerRequeriment = new FleaRequirement()
			{
				count = price - 1, //undercut by 1 ruble
				_tpl = "5449016a4bdc2d6f028b456f" //id of ruble
			};

			FleaRequirement[] offer = new FleaRequirement[1] { offerRequeriment };
			Globals.Session.RagFair.AddOffer(false, itemIds, offer, null);
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

		[PatchPrefix]
		static bool Prefix(GridItemView __instance, PointerEventData.InputButton button, Vector2 position, bool doubleClick)
		{
			bool runOriginalMethod = true;
			if (__instance == null || __instance.Item == null || itemSells.Contains(__instance.Item.Id))
			{
				if (tooltip != null)
				{
					logger.Log(LogLevel.Info, $"Close tooltip - Prefix start");
					tooltip.Close();
					tooltip = null;
					hoveredItem = null;
				}

				return runOriginalMethod;
			}

			Item item = __instance.Item;

			if (!IsItemInPlayerInventory(item))
			{
				return true;
			}

			try
			{
				itemSells.Add(item.Id);

				if (LootValueMod.EnableQuickSell.Value && !Globals.HasRaidStarted())
				{
					if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftAlt))
					{
						logger.LogInfo($"Quicksell item");

						TraderOffer bestTraderOffer = GetBestTraderOffer(item);
						int fleaValue = GetFleaValue(item);

						if (bestTraderOffer == null && fleaValue == 0)
						{
							return false;
						}

						//One button quicksell
						if (LootValueMod.OneButtonQuickSell.Value)
						{

							if (button == PointerEventData.InputButton.Left)
							{

								int priceOnFlea = GetFleaMarketUnitPriceWithModifiers(item) * item.StackObjectsCount;
								int priceOnTrader = bestTraderOffer.Price;

								if (priceOnFlea > priceOnTrader)
								{
									runOriginalMethod = false;
									SellFleaItemOrMultipleItemsIfEnabled(item);
								}
								else
								{
									runOriginalMethod = false;
									SellToTrader(item, bestTraderOffer);
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
								SellFleaItemOrMultipleItemsIfEnabled(item);
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



	}


	internal class ShowTooltipPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(SimpleTooltip).GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(x => x.Name == "Show").ToList()[0];
			//return typeof(SimpleTooltip).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
		}

		[PatchPrefix]
		private static void Prefix(ref string text, ref float delay, SimpleTooltip __instance)
		{
			delay = 0;
			tooltip = __instance;

			if (hoveredItem == null)
			{
				return;
			}

			bool pricesTooltipEnabled = LootValueMod.ShowPrices.Value;
			if (pricesTooltipEnabled == false)
			{
				return;
			}

			bool shouldShowPricesTooltipwhileInRaid = LootValueMod.ShowFleaPricesInRaid.Value;
			bool hideLowerPrice = LootValueMod.HideLowerPrice.Value;
			bool hideLowerPriceInRaid = LootValueMod.HideLowerPriceInRaid.Value;

			bool isInRaid = HasRaidStarted();

			if (!shouldShowPricesTooltipwhileInRaid && isInRaid)
			{
				return;
			}
			if (ItemBelongsToTraderOrFleaMarketOrMail(hoveredItem))
			{
				return;
			}


			int stackAmount = hoveredItem.StackObjectsCount;
			bool isItemEmpty = hoveredItem.IsEmpty();
			bool applyConditionReduction = LootValueMod.ReducePriceInFleaForBrokenItem.Value;

			int finalFleaPrice = GetFleaMarketUnitPriceWithModifiers(hoveredItem) * hoveredItem.StackObjectsCount;
			bool canBeSoldToFlea = finalFleaPrice > 0;

			// trader offer might be null because the item can't be sold (for any reason whatsoever), so if it's null we consider 0 trader value to avoid null checks
			// TODO: refactor GetBestTraderOffer and decouple and just get number (0 if no trader buys it)
			int finalTraderPrice = 0;
			var bestTraderOffer = GetBestTraderOffer(hoveredItem);
			if (bestTraderOffer != null)
			{
				finalTraderPrice = bestTraderOffer.Price;
			}
			bool canBeSoldToTrader = finalTraderPrice > 0;

			// determine price per slot for each sale type				
			var size = hoveredItem.CalculateCellSize();
			int slots = size.X * size.Y;

			int pricePerSlotTrader = finalTraderPrice / slots;
			int pricePerSlotFlea = finalFleaPrice / slots;

			// so we determine which one is non highlighted
			bool isTraderPriceHigherThanFlea = finalTraderPrice > finalFleaPrice;
			bool isFleaPriceHigherThanTrader = finalFleaPrice > finalTraderPrice;
			bool sellToTrader = isTraderPriceHigherThanFlea;

			// If both trader and flea are 0, then the item is not purchasable.
			if (!canBeSoldToTrader && !canBeSoldToFlea)
			{
				// TODO: trader price should be given even if the object is empty, so we can remove this edge case entirely
				if (!isItemEmpty)
				{
					StartSizeTag(ref text, 11);
					AppendNewLineToTooltipText(ref text);
					AppendTextToToolip(ref text, "(Can't get trader price: not empty)", "#AA3333");
					EndSizeTag(ref text);
				}
				else
				{
					StartSizeTag(ref text, 11);
					AppendNewLineToTooltipText(ref text);
					AppendTextToToolip(ref text, "(Item can't be sold)", "#AA3333");
					EndSizeTag(ref text);
				}
				return;
			}

			var showTraderPrice = true;
			if (hideLowerPrice && isFleaPriceHigherThanTrader)
			{
				showTraderPrice = false;
			}
			if (hideLowerPriceInRaid && isInRaid && isFleaPriceHigherThanTrader)
			{
				showTraderPrice = false;
			}
			if (finalTraderPrice == 0)
			{
				showTraderPrice = false;
			}

			if (canBeSoldToTrader || canBeSoldToFlea)
			{
				AppendSeparator(ref text, appendNewLineAfter: false);
			}

			// append trader price on tooltip
			if (showTraderPrice)
			{
				AppendNewLineToTooltipText(ref text);

				// append trader price
				var traderName = $"{bestTraderOffer.TraderName}: ";
				var traderNameColor = sellToTrader ? "#ffffff" : "#444444";
				var traderPricePerSlotColor = isTraderPriceHigherThanFlea ? SlotColoring.GetColorFromValuePerSlots(pricePerSlotTrader) : "#444444";
				var fontSize = isTraderPriceHigherThanFlea ? 14 : 10;

				StartSizeTag(ref text, fontSize);

				AppendTextToToolip(ref text, traderName, traderNameColor);
				AppendTextToToolip(ref text, $"₽ {finalTraderPrice.FormatNumber()}", traderPricePerSlotColor);

				if (stackAmount > 1)
				{
					var unitPrice = $" (₽ {(finalTraderPrice / stackAmount).FormatNumber()} e.)";
					AppendTextToToolip(ref text, unitPrice, "#333333");
				}

				EndSizeTag(ref text);

			}


			var showFleaPrice = true;
			if (hideLowerPrice && isTraderPriceHigherThanFlea)
			{
				showFleaPrice = false;
			}
			if (hideLowerPriceInRaid && isInRaid && isTraderPriceHigherThanFlea)
			{
				showFleaPrice = false;
			}
			if (finalFleaPrice == 0)
			{
				showFleaPrice = false;
			}

			// append flea price on the tooltip
			if (showFleaPrice)
			{
				AppendNewLineToTooltipText(ref text);

				// append flea price
				var fleaName = $"Flea: ";
				var fleaNameColor = sellToTrader ? "#444444" : "#ffffff";
				var fleaPricePerSlotColor = isTraderPriceHigherThanFlea ? "#444444" : SlotColoring.GetColorFromValuePerSlots(pricePerSlotFlea);
				var fontSize = isTraderPriceHigherThanFlea ? 10 : 14;

				StartSizeTag(ref text, fontSize);

				AppendTextToToolip(ref text, fleaName, fleaNameColor);
				AppendTextToToolip(ref text, $"₽ {finalFleaPrice.FormatNumber()}", fleaPricePerSlotColor);

				if(applyConditionReduction) 
				{
						var durability = GetResourcePercentageOfItem(hoveredItem);
						var missingDurability = 100 - durability * 100;
						if ((int) missingDurability > 0)
						{
							var missingDurabilityText = $" (-{(int)missingDurability}%)";
							AppendTextToToolip(ref text, missingDurabilityText, "#AA1111");
						}
				}

				

				if (stackAmount > 1)
				{
					var unitPrice = $" (₽ {GetFleaMarketUnitPriceWithModifiers(hoveredItem).FormatNumber()} e.)";
					AppendTextToToolip(ref text, unitPrice, "#333333");
				}

				EndSizeTag(ref text);

				// Only show this out of raid
				if (!isInRaid && !isTraderPriceHigherThanFlea)
				{

					if (!isItemEmpty)
					{
						// If the item can be sold on flea, but no trader will buy it, then most likely the item is not empty, as even Fence will accept broken items
						StartSizeTag(ref text, 11);
						AppendNewLineToTooltipText(ref text);
						AppendTextToToolip(ref text, "(Item is not empty)", "#AA3333");
						EndSizeTag(ref text);
						canBeSoldToFlea = false;
					}

					if (ContainsNonFleableItemsInside(hoveredItem))
					{
						// We can already know if any item inside this item wont be able to be sold on the flea market
						StartSizeTag(ref text, 11);
						AppendNewLineToTooltipText(ref text);
						AppendTextToToolip(ref text, "(Incompatible items Inside)", "#AA3333");
						EndSizeTag(ref text);
						canBeSoldToFlea = false;
					}

				}

			}

			// TODO: add a CFG message if item is banned from fleamarket using hoveredItem.Template.CanSellOnRagfair

			var shouldShowPricePerSlotAndPerKgInRaid = LootValueMod.ShowPricePerKgAndPerSlotInRaid.Value;
			if (isInRaid && shouldShowPricePerSlotAndPerKgInRaid)
			{

				var pricePerSlot = sellToTrader ? pricePerSlotTrader : pricePerSlotFlea;
				var unitPrice = sellToTrader ? (finalTraderPrice / stackAmount) : GetFleaMarketUnitPriceWithModifiers(hoveredItem);
				var pricePerWeight = (int)(unitPrice / hoveredItem.GetSingleItemTotalWeight());

				AppendSeparator(ref text, "#555555");

				StartSizeTag(ref text, 11);
				AppendTextToToolip(ref text, $"₽ / KG\t{pricePerWeight.FormatNumber()}", "#555555");
				AppendNewLineToTooltipText(ref text);
				AppendTextToToolip(ref text, $"₽ / SLOT\t{pricePerSlot.FormatNumber()}", "#555555");
				EndSizeTag(ref text);

			}


			bool quickSellEnabled = LootValueMod.EnableQuickSell.Value;
			bool quickSellUsesOneButton = LootValueMod.OneButtonQuickSell.Value;
			bool showQuickSaleCommands = quickSellEnabled && !isInRaid && !ItemBelongsToTraderOrFleaMarketOrMail(hoveredItem);

			if (showQuickSaleCommands)
			{
				if (quickSellUsesOneButton)
				{

					bool willBeSoldAfterClicking = (!sellToTrader && canBeSoldToFlea) ||
													 (sellToTrader && canBeSoldToTrader);

					if (willBeSoldAfterClicking)
					{
						AppendSeparator(ref text);
						AppendTextToToolip(ref text, $"Sell with Alt+Shift+Click", "#888888");
					}
				}
				else
				{
					if (canBeSoldToFlea || canBeSoldToTrader)
					{
						AppendSeparator(ref text);
					}

					if (canBeSoldToTrader)
					{
						AppendTextToToolip(ref text, $"Sell to Trader with Alt+Shift+Left Click", "#888888");
					}

					if (canBeSoldToFlea && canBeSoldToTrader)
					{
						AppendNewLineToTooltipText(ref text);
					}

					if (canBeSoldToFlea)
					{
						AppendTextToToolip(ref text, $"Sell to Flea with Alt+Shift+Right Click", "#888888");
					}
				}

				if (!sellToTrader && canBeSoldToFlea)
				{

					// append only if more than 1 item will be sold due to the flea market action
					var amountOfItems = CountItemsSimilarToItemWithinSameContainer(hoveredItem);
					if (amountOfItems > 1)
					{
						AppendNewLineToTooltipText(ref text);
						StartSizeTag(ref text, 10);
						AppendTextToToolip(ref text, $"(Will sell {amountOfItems} similar items)", "#555555");
						EndSizeTag(ref text);
					}

				}

			}


		}

		private static void AppendNewLineToTooltipText(ref string tooltipText)
		{
			tooltipText += $"<br>";
		}

		private static void AppendTextToToolip(ref string tooltipText, string addText, string color)
		{
			tooltipText += $"<color={color}>{addText}</color>";
		}

		private static void StartSizeTag(ref string tooltipText, int size)
		{
			tooltipText += $"<size={size}>";
		}

		private static void EndSizeTag(ref string tooltipText)
		{
			tooltipText += $"</size>";
		}

		private static void AppendSeparator(ref string tooltipText, string color = "#444444", bool appendNewLineAfter = true)
		{
			AppendNewLineToTooltipText(ref tooltipText);
			AppendTextToToolip(ref tooltipText, "--------------------", color);
			if (appendNewLineAfter)
			{
				AppendNewLineToTooltipText(ref tooltipText);
			}
		}

		// TODO: add a method that adds a warning ( parenthesis with size 11 and color red )
		// TODO: add a method that appends a whole -line- with color and size

	}
}
