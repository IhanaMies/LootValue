using System;
using System.Linq;
using System.Reflection;
using Aki.Reflection.Patching;
using EFT.InventoryLogic;
using EFT.UI;
using static LootValue.TooltipUtils;

namespace LootValue
{

	internal class TooltipController
	{

		private static SimpleTooltip tooltip;

		public static void SetupTooltip(SimpleTooltip _tooltip, ref float delay)
		{
			tooltip = _tooltip;
			delay = 0;
		}

		public static void ClearTooltip()
		{
			tooltip?.Close();
			tooltip = null;
		}


		internal class ShowTooltipPatch : ModulePatch
		{

			protected override MethodBase GetTargetMethod()
			{
				return typeof(SimpleTooltip).GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(x => x.Name == "Show").ToList()[0];
			}

			[PatchPrefix]
			private static void Prefix(ref string text, ref float delay, SimpleTooltip __instance)
			{
				SetupTooltip(__instance, ref delay);

				var item = HoverItemController.hoveredItem;
				if (item == null || tooltip == null)
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

				bool isInRaid = Globals.HasRaidStarted();

				if (!shouldShowPricesTooltipwhileInRaid && isInRaid)
				{
					return;
				}
				if (ItemUtils.ItemBelongsToTraderOrFleaMarketOrMail(item))
				{
					return;
				}

				var durability = ItemUtils.GetResourcePercentageOfItem(item);
				var missingDurability = 100 - durability * 100;

				int stackAmount = item.StackObjectsCount;
				bool isItemEmpty = item.IsEmpty();
				bool applyConditionReduction = LootValueMod.ReducePriceInFleaForBrokenItem.Value;

				int finalFleaPrice = FleaUtils.GetFleaMarketUnitPriceWithModifiers(item) * stackAmount;
				bool canBeSoldToFlea = finalFleaPrice > 0;

				var finalTraderPrice = TraderUtils.GetBestTraderPrice(item);
				bool canBeSoldToTrader = finalTraderPrice > 0;

				// determine price per slot for each sale type				
				var size = item.CalculateCellSize();
				int slots = size.X * size.Y;

				int pricePerSlotTrader = finalTraderPrice / slots;
				int pricePerSlotFlea = finalFleaPrice / slots;


				bool isTraderPriceHigherThanFlea = finalTraderPrice > finalFleaPrice;
				bool isFleaPriceHigherThanTrader = finalFleaPrice > finalTraderPrice;
				bool sellToTrader = isTraderPriceHigherThanFlea;
				bool sellToFlea = !sellToTrader;

				// If both trader and flea are 0, then the item is not purchasable.
				if (!canBeSoldToTrader && !canBeSoldToFlea)
				{
					AppendFullLineToTooltip(ref text, "(Item can't be sold)", 11, "#AA3333");
					return;
				}

				if (sellToFlea && TraderUtils.ShouldSellToTraderDueToPriceOrCondition(item) && !isInRaid)
				{
					isTraderPriceHigherThanFlea = true;
					isFleaPriceHigherThanTrader = false;
					sellToTrader = true;
					sellToFlea = false;

					var reason = GetReasonForItemToBeSoldToTrader(item);
					AppendFullLineToTooltip(ref text, $"(<b>Trader</b> price selected {reason})", 11, "#AAAA33");
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
					var traderName = $"Trader: ";
					var traderNameColor = sellToTrader ? "#ffffff" : "#444444";
					var traderPricePerSlotColor = sellToTrader ? SlotColoring.GetColorFromValuePerSlots(pricePerSlotTrader) : "#444444";
					var fontSize = sellToTrader ? 14 : 10;

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
					var fleaNameColor = sellToFlea ? "#ffffff" : "#444444";
					var fleaPricePerSlotColor = sellToFlea ? SlotColoring.GetColorFromValuePerSlots(pricePerSlotFlea) : "#444444";
					var fontSize = sellToFlea ? 14 : 10;

					StartSizeTag(ref text, fontSize);

					AppendTextToToolip(ref text, fleaName, fleaNameColor);
					AppendTextToToolip(ref text, $"₽ {finalFleaPrice.FormatNumber()}", fleaPricePerSlotColor);

					if (applyConditionReduction)
					{
						if (missingDurability >= 1.0f)
						{
							var missingDurabilityText = $" (-{(int)missingDurability}%)";
							AppendTextToToolip(ref text, missingDurabilityText, "#AA1111");
						}
					}


					if (stackAmount > 1)
					{
						var unitPrice = $" (₽ {FleaUtils.GetFleaMarketUnitPriceWithModifiers(item).FormatNumber()} e.)";
						AppendTextToToolip(ref text, unitPrice, "#333333");
					}

					EndSizeTag(ref text);

					// Only show this out of raid
					if (!isInRaid && !isTraderPriceHigherThanFlea)
					{
						if (FleaUtils.ContainsNonFleableItemsInside(item))
						{
							AppendFullLineToTooltip(ref text, "(Contains banned flea items inside)", 11, "#AA3333");
							canBeSoldToFlea = false;
						}

					}

				}

				if (!isInRaid)
				{
					if (!isItemEmpty)
					{
						AppendFullLineToTooltip(ref text, "(Item is not empty)", 11, "#AA3333");
						canBeSoldToFlea = false;
						canBeSoldToTrader = false;
					}
				}

				var shouldShowFleaMarketEligibility = LootValueMod.ShowFleaMarketEligibility.Value;
				if (shouldShowFleaMarketEligibility && !item.Template.CanSellOnRagfair)
				{
					AppendFullLineToTooltip(ref text, "(Item is banned from flea market)", 11, "#AA3333");
				}

				var shouldShowPricePerSlotAndPerKgInRaid = LootValueMod.ShowPricePerKgAndPerSlotInRaid.Value;
				if (isInRaid && shouldShowPricePerSlotAndPerKgInRaid)
				{

					var pricePerSlot = sellToTrader ? pricePerSlotTrader : pricePerSlotFlea;
					var unitPrice = sellToTrader ? (finalTraderPrice / stackAmount) : FleaUtils.GetFleaMarketUnitPriceWithModifiers(item);
					var pricePerWeight = (int)(unitPrice / item.GetSingleItemTotalWeight());

					AppendSeparator(ref text, "#555555");

					StartSizeTag(ref text, 11);
					AppendTextToToolip(ref text, $"₽ / KG\t{pricePerWeight.FormatNumber()}", "#555555");
					AppendNewLineToTooltipText(ref text);
					AppendTextToToolip(ref text, $"₽ / SLOT\t{pricePerSlot.FormatNumber()}", "#555555");
					EndSizeTag(ref text);

				}


				bool quickSellEnabled = LootValueMod.EnableQuickSell.Value;
				bool quickSellUsesOneButton = LootValueMod.OneButtonQuickSell.Value;
				bool showQuickSaleCommands = quickSellEnabled && !isInRaid;

				if (showQuickSaleCommands)
				{
					if (quickSellUsesOneButton)
					{

						bool canBeSold = (sellToFlea && canBeSoldToFlea) ||
														 (sellToTrader && canBeSoldToTrader);

						if (canBeSold)
						{
							AppendSeparator(ref text);
							AppendTextToToolip(ref text, $"Sell with Alt+Shift+Click", "#888888");
							if (canBeSoldToFlea && sellToFlea)
							{
								AddMultipleItemsSaleSection(ref text, item);
							}
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
							AddMultipleItemsSaleSection(ref text, item);
						}
					}

				}


			}

			private static void AddMultipleItemsSaleSection(ref string text, Item item)
			{
				bool canSellSimilarItems = FleaUtils.CanSellMultipleOfItem(item);
				if (canSellSimilarItems)
				{
					// append only if more than 1 item will be sold due to the flea market action
					var amountOfItems = ItemUtils.CountItemsSimilarToItemWithinSameContainer(item);
					if (amountOfItems > 1)
					{
						var totalPrice = FleaUtils.GetTotalPriceOfAllSimilarItemsWithinSameContainer(item);
						AppendFullLineToTooltip(ref text, $"(Will sell {amountOfItems} similar items for ₽ {totalPrice.FormatNumber()})", 10, "#555555");
					}

				}
			}

			private static string GetReasonForItemToBeSoldToTrader(Item item)
			{
				var flags = DurabilityOrPriceConditionFlags.GetDurabilityOrPriceConditionFlagsForItem(item);
				if (flags.shouldSellToTraderDueToBeingNonOperational)
				{
					return "due to being non operation";
				}
				else if (flags.shouldSellToTraderDueToDurabilityThreshold)
				{
					return "due to low durability";
				}
				else if (flags.shouldSellToTraderDueToPriceThreshold)
				{
					return "due to low price";
				}
				return "due to no reason :)";
			}

		}



	}

}