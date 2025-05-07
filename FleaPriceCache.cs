using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static LootValue.Globals;

namespace LootValue
{
	internal static class FleaPriceCache
	{
		static Dictionary<string, CachePrice> cache = new Dictionary<string, CachePrice>();

		public static async Task<double?> FetchPrice(string templateId)
		{
			bool fleaAvailable = Session.RagFair.Available || LootValueMod.ShowFleaPriceBeforeAccess.Value;

			if (!fleaAvailable || !LootValueMod.UseFleaPrices.Value)
				return null;

			if (cache.ContainsKey(templateId))
			{
				double secondsSinceLastUpdate = (DateTime.Now - cache[templateId].lastUpdate).TotalSeconds;
				if (secondsSinceLastUpdate > 300)
					return await QueryAndTryUpsertPrice(templateId, true);
				else
					return cache[templateId].price;
			}
			else
				return await QueryAndTryUpsertPrice(templateId, false);
		}

		private static async Task<string> QueryPrice(string templateId)
		{
			return await CustomRequestHandler.PostJsonAsync("/LootValue/GetItemLowestFleaPrice", JsonConvert.SerializeObject(new FleaPriceRequest(templateId)));
		}

		private static async Task<double?> QueryAndTryUpsertPrice(string templateId, bool update)
		{
			string response = await QueryPrice(templateId);

			if (!string.IsNullOrEmpty(response) && response != "null")
			{
				double price = double.Parse(response);

				if (price < 0)
				{
					cache.Remove(templateId);
					return null;
				}

				cache[templateId] = new CachePrice(price);

				return price;
			}

			return null;
		}
	}

	internal struct CachePrice
	{
		public double price { get; private set; }
		public DateTime lastUpdate { get; private set; }

		public CachePrice(double price)
		{
			this.price = price;
			lastUpdate = DateTime.Now;
		}
	}
}
