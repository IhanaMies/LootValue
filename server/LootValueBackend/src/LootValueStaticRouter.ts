import { DependencyContainer } from "tsyringe";
import { RagfairOfferService } from "@spt-aki/services/RagfairOfferService";
import { ItemHelper } from "@spt-aki/helpers/ItemHelper";
import { IRagfairOffer } from "@spt-aki/models/eft/ragfair/IRagfairOffer";
import { TradeHelper } from "@spt-aki/helpers/TradeHelper";
import { ProfileHelper } from "@spt-aki/helpers/ProfileHelper";
import { IProcessSellTradeRequestData } from "@spt-aki/models/eft/trade/IProcessSellTradeRequestData";
import { IItemEventRouterResponse } from "@spt-aki/models/eft/itemEvent/IItemEventRouterResponse";
import { SaveServer } from '@spt-aki/servers/SaveServer';

import type { IPreAkiLoadMod } from "@spt-aki/models/external/IPreAkiLoadMod";
import type { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import type { StaticRouterModService} from "@spt-aki/services/mod/staticRouter/StaticRouterModService";

import { RagfairPriceService } from "@spt-aki/services/RagfairPriceService";
import { ConfigServer } from "@spt-aki/servers/ConfigServer";
import { IRagfairConfig } from "@spt-aki/models/spt/config/IRagfairConfig";
import { ConfigTypes } from "@spt-aki/models/enums/ConfigTypes";

class Mod implements IPreAkiLoadMod
{
	private itemHelper: ItemHelper;
	private offerService: RagfairOfferService;
	private tradeHelper: TradeHelper;
	private profileHelper: ProfileHelper;
	private saveServer: SaveServer;
	private priceService: RagfairPriceService;
	private ragfairConfig: IRagfairConfig;

	private logger: ILogger;
	
    public preAkiLoad(container: DependencyContainer): void {
        const logger = container.resolve<ILogger>("WinstonLogger");
		this.logger = logger;
		
        const staticRouterModService = container.resolve<StaticRouterModService>("StaticRouterModService");
		
		//HELPERS
		this.itemHelper = container.resolve<ItemHelper>("ItemHelper");
		this.offerService = container.resolve<RagfairOfferService>("RagfairOfferService");
		this.tradeHelper = container.resolve<TradeHelper>("TradeHelper");
		this.profileHelper = container.resolve<ProfileHelper>("ProfileHelper");
		this.saveServer = container.resolve<SaveServer>("SaveServer");
		this.priceService = container.resolve<RagfairPriceService>("RagfairPriceService");
		const config = container.resolve<ConfigServer>("ConfigServer");
		this.ragfairConfig = config.getConfig(ConfigTypes.RAGFAIR);

        // Hook up a new static route
        staticRouterModService.registerStaticRouter(
            "LootValueRoutes",
            [
				{
					url: "/LootValue/GetItemLowestFleaPrice",
					//info is the payload from client in json
					//output is the response back to client
					action: (url, info, sessionID, output) => {
						return(JSON.stringify(this.getItemLowestFleaPrice(info.templateId)));
					}
				},
				{
					url: "/LootValue/SellItemToTrader",
					//info is the payload from client in json
					//output is the response back to client
					action: (url, info, sessionID, output) => {			
						let response = this.sellItemToTrader(sessionID, info.ItemId, info.TraderId, info.Price);			
						return(JSON.stringify(response));
					}
				}
            ],
            "custom-static-LootValueRoutes"
        );        
    }

	private getItemLowestFleaPrice(templateId: string): number {
		const singleItemPrice = this.getFleaSingleItemPriceForTemplate(templateId);

		if(singleItemPrice > 0)
			return Math.floor(singleItemPrice);

		return null;
	}

	private getFleaSingleItemPriceForTemplate(templateId: string): number {

		// https://dev.sp-tarkov.com/SPT/Server/src/branch/master/project/src/controllers/RagfairController.ts#L411
		// const name = this.itemHelper.getItemName(templateId);
		const offers: IRagfairOffer[] = this.offerService.getOffersOfType(templateId);
		if(!offers || !offers.length)
			return null;

		const offersByPlayers = [...offers.filter(a => a.user.memberType != 4)];
		if(!offersByPlayers || !offersByPlayers.length)
			return null;


		let fleaPriceForItem = this.priceService.getFleaPriceForItem(templateId);
		//console.log(`Item ${name} price per unit: ${fleaPriceForItem}`);

		const itemPriceModifer = this.ragfairConfig.dynamic.itemPriceMultiplier[templateId];
		//console.log(`Item price modifier: ${itemPriceModifer || "No modifier in place"}`);

		if (itemPriceModifer)
			fleaPriceForItem *= itemPriceModifer;

		return fleaPriceForItem;
	}

	private sellItemToTrader(sessionId: string, itemId: string, traderId: string, price: number): boolean {
		let pmcData = this.profileHelper.getPmcProfile(sessionId)
		if (!pmcData) {
			this.logger.error("pmcData was null");
			return false;
		}

		let item = pmcData.Inventory.items.find(x => x._id === itemId)
		if (!item) {
			this.logger.error("item was null");
			return false;
		}

		let sellRequest: IProcessSellTradeRequestData = {
            Action: "sell_to_trader",
            type: "sell_to_trader",
            tid: traderId,
            price: price,
            items: [{
				id: itemId,
				count: item.upd ? item.upd.StackObjectsCount ? item.upd.StackObjectsCount : 1 : 1,
				scheme_id: 0
			}]
		};

		let response: IItemEventRouterResponse = this.tradeHelper.sellItem(pmcData, pmcData, sellRequest, sessionId);
		this.saveServer.saveProfile(sessionId);
		return true;
	}
}

module.exports = {mod: new Mod()}