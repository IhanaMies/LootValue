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
			if (item == null)
			{
				return 1.0f;
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

				// some barter items are considered resources, although they have no max value / value or anything. Must be a leftover from BSG
				if (item.GetItemComponent<ResourceComponent>().MaxResource.ApproxEquals(0.0f))
				{
					return 1.0f;
				}

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

		public static bool IsItemFleaMarketPriceBelow(Item item, int priceThreshold, bool considerMultipleItems = false)
		{
			var unitPrice = FleaUtils.GetFleaMarketUnitPriceWithModifiers(item);
			if (considerMultipleItems)
			{
				var items = GetItemsSimilarToItemWithinSameContainer(item);
				var price = items.Select(i => unitPrice * i.StackObjectsCount).Sum();
				return price < priceThreshold;
			}
			else
			{
				var price = unitPrice * item.StackObjectsCount;
				return price < priceThreshold;
			}
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


}