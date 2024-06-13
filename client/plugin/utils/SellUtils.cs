using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aki.Reflection.Utils;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using CurrencyUtil = GClass2517;
using FleaRequirement = GClass1844;

namespace LootValue
{

    internal static class FleaUtils
    {

        public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();

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
            if (!item.Template.CanSellOnRagfair)
            {
                return 0;
            }

            int unitPrice = GetFleaValue(item);
            return unitPrice;
        }

        public static int GetFleaMarketUnitPriceWithModifiers(Item item)
        {
            int price = GetFleaMarketUnitPrice(item);

            bool applyConditionReduction = LootValueMod.ReducePriceInFleaForBrokenItem.Value;
            if (applyConditionReduction)
            {
                price = (int)(price * ItemUtils.GetResourcePercentageOfItem(item));
            }

            return price;
        }


        public static bool ContainsNonFleableItemsInside(Item item)
		{
			return item.GetAllItems().Any(i => i.Template.CanSellOnRagfair == false);
		}

        public static bool CanBeSoldInFleaRightNow(Item item, bool displayWarning = true)
		{

			if (!Session.RagFair.Available)
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Flea market is not available yet.");

				return false;
			}

			// we need to check if the base item is sellable
			if (!item.Template.CanSellOnRagfair)
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item is banned from flea market.");

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
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item contains banned fleamarket items.");

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

        public static bool CanSellMultipleOfItem(Item item)
		{

			bool sellMultipleEnabled = LootValueMod.SellSimilarItems.Value;
			bool sellMultipleOnlyFiR = LootValueMod.SellOnlySimilarItemsFiR.Value;
			bool isItemFindInRaid = item.MarkedAsSpawnedInSession;

			if (!sellMultipleEnabled)
			{
				return false;
			}


			if (sellMultipleOnlyFiR && !isItemFindInRaid)
			{
				return false;
			}

			return true;
		}


        public static void SellFleaItemOrMultipleItemsIfEnabled(Item item)
		{
			if (!CanSellMultipleOfItem(item))
			{
				SellToFlea(item);
				return;
			}

			var itemsSimilarToTheOneImSelling = ItemUtils.GetItemsSimilarToItemWithinSameContainer(item);
			SellToFlea(item, itemsSimilarToTheOneImSelling);
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
			Session.RagFair.AddOffer(false, itemIds, offer, null);
		}


    }

    internal static class TraderUtils
    {

        public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();

        public static int GetBestTraderPrice(Item item)
        {
            var offer = GetBestTraderOffer(item);
            if (offer == null)
            {
                return 0;
            }
            return offer.Price;
        }

        public static TraderOffer GetBestTraderOffer(Item item)
        {
            if (!Session.Profile.Examined(item))
            {
                return null;
            }

            // this seems to work to Everything but armored rigs
            // If the item is not empty, it will not properly calculate the offer
            // For some reason it works for armors, weapons, helmets, but not for armored rigs
            var clone = item.CloneVisibleItem();
            clone.UnlimitedCount = false;

            var bestOffer =
                Session.Traders
                    .Where(trader => trader.Info.Available && !trader.Info.Disabled && trader.Info.Unlocked)
                    .Select(trader => GetTraderOffer(clone, trader))
                    .Where(offer => offer != null)
                    .OrderByDescending(offer => offer.Price)
                    .FirstOrDefault();

            return bestOffer;
        }

        private static TraderOffer GetTraderOffer(Item item, TraderClass trader)
        {
            var result = trader.GetUserItemPrice(item);

            if (result == null)
            {
                return null;
            }

            // TODO: try to see if we can convert non rubles to rubles

            return new TraderOffer(
                trader.Id,
                trader.LocalizedName,
                result.Value.Amount,
                CurrencyUtil.GetCurrencyCharById(result.Value.CurrencyId),
                trader.GetSupplyData().CurrencyCourses[result.Value.CurrencyId],
                item.StackObjectsCount
            );
        }

        public static bool ShouldSellToTraderDueToPriceOrCondition(Item item)
        {
            var flags = DurabilityOrPriceConditionFlags.GetDurabilityOrPriceConditionFlagsForItem(item);
            return flags.shouldSellToTraderDueToBeingNonOperational || flags.shouldSellToTraderDueToDurabilityThreshold || flags.shouldSellToTraderDueToPriceThreshold;
        }

        public static void SellToTrader(Item item)
		{
			try
			{
				TraderOffer bestTraderOffer = GetBestTraderOffer(item);

				if (bestTraderOffer == null)
				{
					NotificationManagerClass.DisplayWarningNotification("Quicksell Error: No trader will purchase this item.");
					return;
				}

				if(item.IsNotEmpty()) {
					NotificationManagerClass.DisplayWarningNotification("Quicksell: item is not empty.");
					return;
				}

				SellToTrader(item, bestTraderOffer);
			}
			catch (Exception ex)
			{
				Globals.logger.LogInfo($"Something fucked up: {ex.Message}");
				Globals.logger.LogInfo($"{ex.InnerException.Message}");
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

    public class FleaPriceRequest
    {
        public string templateId;
        public FleaPriceRequest(string templateId) => this.templateId = templateId;
    }

}