using System;
using EFT.UI.DragAndDrop;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using BepInEx;
using Aki.Reflection.Patching;
using Aki.Reflection.Utils;

using CurrencyUtil = GClass2334;
using static LootValue.Globals;
using Comfort.Common;
using EFT;
using System.Collections.Generic;
using EFT.HealthSystem;
using BepInEx.Logging;
using static System.Collections.Specialized.BitVector32;
using UnityEngine;
using UnityEngine.EventSystems;
using Aki.Common.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using EFT.UI.Ragfair;

namespace LootValue
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class LootValueMod : BaseUnityPlugin
    {
        // BepinEx
        public const string pluginGuid = "IhanaMies.LootValue";
        public const string pluginName = "LootValue";
        public const string pluginVersion = "1.0.0";

		private void Awake()
		{
			new TraderPatch().Enable();
			//new ItemShowTooltipPatch().Enable();
			new ShowTooltipPatch().Enable();
			new GridItemOnPointerEnterPatch().Enable();
			new GridItemOnPointerExitPatch().Enable();
			new ItemViewOnClickPatch().Enable();
		}
	}

	internal class TraderPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(TraderClass).GetConstructors()[0];

		[PatchPostfix]
		private static void PatchPostfix(ref TraderClass __instance)
		{
			__instance.UpdateSupplyData();
			logger = Logger;
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
		public static ManualLogSource logger;
		public static Item hoveredItem;

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
				foreach (Mod mod in weapon.Mods)
				{
					TraderOffer tempHighestOffer = null;

					foreach (TraderClass trader in Session.Traders)
					{
						if (GetTraderOffer(mod, trader) is TraderOffer offer)
						{
							if (tempHighestOffer == null || offer.Price > tempHighestOffer.Price)
								tempHighestOffer = offer;

							//Item might be part of a weapon
							//Try to get an offer from the template
							if (tempHighestOffer == null)
							{
								Item tempItem = new Item(mod.Id, mod.Template);

								if (GetTraderOffer(tempItem, trader) is TraderOffer offer2)
									if (tempHighestOffer == null || offer2.Price > tempHighestOffer.Price)
										tempHighestOffer = offer2;
							}
						}
					}

					if (tempHighestOffer != null)
					{
						if (highestOffer == null)
							highestOffer = tempHighestOffer;
						else
							highestOffer.Price += tempHighestOffer.Price;
					}
				}
			}
			else
			{
				foreach (TraderClass trader in Session.Traders)
					if (GetTraderOffer(item, trader) is TraderOffer offer)
						if (highestOffer == null || offer.Price > highestOffer.Price)
							highestOffer = offer;
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
	}

	public class ItemShowTooltipPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(GridItemView).GetMethod("ShowTooltip", BindingFlags.Instance | BindingFlags.NonPublic);

		[PatchPrefix]
		static void Prefix(GridItemView __instance)
		{
			if (__instance.Item != null)
				hoveredItem = __instance.Item;
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
		protected override MethodBase GetTargetMethod() => typeof(GridItemView).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);

		[PatchPrefix]
		static async void Prefix(GridItemView __instance, PointerEventData.InputButton button, Vector2 position, bool doubleClick)
		{
			Item item = __instance.Item;

			if (button == PointerEventData.InputButton.Left
				&& Input.GetKey(KeyCode.LeftShift)
				&& Input.GetKey(KeyCode.LeftAlt)
				&& !GClass1716.InRaid
				&& item != null)
			{
				try
				{
					TraderOffer bestTraderOffer = GetBestTraderOffer(item);
					double? fleaPrice = FleaPriceCache.FetchPrice(item.TemplateId);

					if (bestTraderOffer != null)
					{
						if (fleaPrice.HasValue && fleaPrice.Value > bestTraderOffer.Price)
						{
							var g = new GClass1711();
							g.count = fleaPrice.Value - 1; //undercut by 1 ruble
							g._tpl = "5449016a4bdc2d6f028b456f"; //id of ruble

							GClass1711[] gs = new GClass1711[1];
							gs[0] = g;
							Globals.Session.RagFair.AddOffer(false, new string[1] { item.Id }, gs, null);
						}
						else
						{
							TraderClass traderClass = Globals.Session.GetTrader(bestTraderOffer.TraderId);
							await traderClass.RefreshAssortment(true, true);

							TraderAssortmentControllerClass tacc = traderClass.CurrentAssortment;
							tacc.PrepareToSell(__instance.Item, new LocationInGrid(2, 3, ItemRotation.Horizontal));
							tacc.Sell();
						}
					}
				}
				catch (Exception ex)
				{
					logger.LogInfo($"Something fucked up: {ex.Message}");
					logger.LogInfo($"{ex.InnerException.Message}");
				}
			}
		}
	}

	internal class ShowTooltipPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(SimpleTooltip).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);

		[PatchPrefix]
		private static void Prefix(ref string text, ref float delay, SimpleTooltip __instance)
		{
			delay = 0;

			bool isFleaEligible = false;
			double lowestFleaOffer = 0;

			if (hoveredItem != null)
			{
				TraderOffer bestTraderOffer = GetBestTraderOffer(hoveredItem);

				//For weapons we want to fetch each mods flea price, if eligible
				if (hoveredItem is Weapon weapon)
				{
					double totalFleaPrice = 0;

					foreach (Mod mod in weapon.Mods)
					{
						if (mod.MarkedAsSpawnedInSession)
						{
							double? fleaPrice = FleaPriceCache.FetchPrice(mod.TemplateId);

							if (fleaPrice.HasValue)
							{
								isFleaEligible = true;
								totalFleaPrice += fleaPrice.Value * mod.StackObjectsCount;
							}
						}
					}

					if (totalFleaPrice > 0)
						lowestFleaOffer = totalFleaPrice;
				}
				else if (hoveredItem.MarkedAsSpawnedInSession)
				{
					double? fleaPrice = FleaPriceCache.FetchPrice(hoveredItem.TemplateId);

					if (fleaPrice.HasValue)
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

				hoveredItem = null;
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

			text += $"<br>{highlightText}: <color={perSlotColor}>{valuePerSlotA.FormatNumber()}</color>";

			if (slots > 1)
				text += $" Total: {totalValue.FormatNumber()}";
		}
	}
}
