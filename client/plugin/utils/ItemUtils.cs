using System;
using System.Linq;
using EFT.InventoryLogic;
using System.Collections.Generic;

namespace LootValue
{

    internal class ItemUtils
    {

        public static float GetResourcePercentageOfItem(Item item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

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
            else if (item.GetItemComponent<ResourceComponent>() != null)
            {

                return item.GetItemComponent<ResourceComponent>().RelativeValue;

            }
            else if (item.GetItemComponent<RepairKitComponent>() != null)
            {

                var component = item.GetItemComponent<RepairKitComponent>();
                var currentResource = component.Resource;
                // method 0 returns max value of template
                var maxResource = component.method_0();
                return currentResource / maxResource;

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

        public static bool IsWeaponNonOperational(Item item)
		{
			if (!(item is Weapon weapon))
			{
				return false;
			}
			return weapon.MissingVitalParts.Any<Slot>();
		}

		public static bool IsItemBelowDurability(Item hoveredItem, int durabilityThreshold)
		{
			var currentDurability = GetResourcePercentageOfItem(hoveredItem) * 100;
			return currentDurability < durabilityThreshold;
		}

		public static bool IsItemFleaMarketPriceBelow(Item hoveredItem, int priceThreshold)
		{
			var price = FleaUtils.GetFleaMarketUnitPriceWithModifiers(hoveredItem) * hoveredItem.StackObjectsCount;
			return price < priceThreshold;
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
			return itemsOfParent.Where(o => item.Compare(o) && o.MarkedAsSpawnedInSession == item.MarkedAsSpawnedInSession);
		}

		/**
		* Includes original item!
		*/
		public static int CountItemsSimilarToItemWithinSameContainer(Item item)
		{
			return GetItemsSimilarToItemWithinSameContainer(item).Count();
		}

    }

    internal class DurabilityOrPriceConditionFlags
    {

        public bool shouldSellToTraderDueToBeingNonOperational;
        public bool shouldSellToTraderDueToDurabilityThreshold;
        public bool shouldSellToTraderDueToPriceThreshold;


        public DurabilityOrPriceConditionFlags(
            bool shouldSellToTraderDueToBeingNonOperational,
            bool shouldSellToTraderDueToDurabilityThreshold,
            bool shouldSellToTraderDueToPriceThreshold
        )
        {
            this.shouldSellToTraderDueToBeingNonOperational = shouldSellToTraderDueToBeingNonOperational;
            this.shouldSellToTraderDueToDurabilityThreshold = shouldSellToTraderDueToDurabilityThreshold;
            this.shouldSellToTraderDueToPriceThreshold = shouldSellToTraderDueToPriceThreshold;
        }

        public static DurabilityOrPriceConditionFlags GetDurabilityOrPriceConditionFlagsForItem(Item item)
		{
			bool sellNonOperationalWeaponsToTraderEnabled = LootValueMod.SellToTraderIfWeaponIsNonOperational.Value;
			bool sellItemToTraderBelowCertainFleaPriceEnabled = LootValueMod.SellToTraderBelowPriceThresholdEnabled.Value;
			int priceThreshold = LootValueMod.SellToTraderPriceThreshold.Value;
			bool sellItemToTraderBelowCertainDurabilityEnabled = LootValueMod.SellToTraderBelowDurabilityThresholdEnabled.Value;
			int durabilityThreshold = LootValueMod.SellToTraderDurabilityThreshold.Value;

			bool shouldSellToTraderDueToBeingNonOperational = ItemUtils.IsWeaponNonOperational(item) && sellNonOperationalWeaponsToTraderEnabled;
			bool shouldSellToTraderDueToDurabilityThreshold = ItemUtils.IsItemBelowDurability(item, durabilityThreshold) && sellItemToTraderBelowCertainDurabilityEnabled;
			
			// Known issue: if you have multiple stacks of the item, and one of the stacks costs less than the threshold, this will be true
			// i.e: if you have two stacks of bullets, and you hover over the small one, if the small one is below threshold, it will be sold to trader
			// to fix: consider getting TOTAL value of ALL items that will be sold, instead of the item itself.
			bool shouldSellToTraderDueToPriceThreshold = ItemUtils.IsItemFleaMarketPriceBelow(item, priceThreshold) && sellItemToTraderBelowCertainFleaPriceEnabled;
			return new DurabilityOrPriceConditionFlags(shouldSellToTraderDueToBeingNonOperational, shouldSellToTraderDueToDurabilityThreshold, shouldSellToTraderDueToPriceThreshold);
		}

    }


}