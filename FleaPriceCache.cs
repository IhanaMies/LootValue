using Aki.Common.Http;
using Aki.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static LootValue.Globals;
using static System.Collections.Specialized.BitVector32;

namespace LootValue
{
	internal static class FleaPriceCache
	{
		static Dictionary<string, CachePrice> cache = new Dictionary<string, CachePrice>();

		public static int? FetchPrice(string templateId)
		{
			bool fleaAvailable = Session.RagFair.Available || LootValueMod.ShowFleaPriceBeforeAccess.Value;

			if (!fleaAvailable)
				return null;

			if (cache.ContainsKey(templateId))
			{
				double secondsSinceLastUpdate = (DateTime.Now - cache[templateId].lastUpdate).TotalSeconds;
				if (secondsSinceLastUpdate > 300)
					return QueryAndTryUpsertPrice(templateId, true);
				else
					return cache[templateId].price;
			}
			else
				return QueryAndTryUpsertPrice(templateId, false);
		}

		private static string QueryPrice(string templateId)
		{
			return RequestHandler.PostJson("/LootValue/GetItemLowestFleaPrice", JsonConvert.SerializeObject(new FleaPriceRequest(templateId)));
		}

		private static int? QueryAndTryUpsertPrice(string templateId, bool update)
		{
			string response = QueryPrice(templateId);
			bool hasPlayerFleaPrice = !(string.IsNullOrEmpty(response) || response == "null");

			int price;
			if (hasPlayerFleaPrice)
			{
				price = int.Parse(response);
			}
			else
			{
				price = 0;
			}

			if (update)
				cache[templateId].Update(price);
			else
				cache[templateId] = new CachePrice(price);

			return price;
		}
	}

	internal class CachePrice
	{
		public int price { get; private set; }
		public DateTime lastUpdate { get; private set; }

		public CachePrice(int price)
		{
			this.price = price;
			lastUpdate = DateTime.Now;
		}

		public void Update(int price)
		{
			this.price = price;
			lastUpdate = DateTime.Now;
		}
	}
}
