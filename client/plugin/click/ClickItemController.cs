using System;
using System.Collections.Generic;
using System.Reflection;
using Aki.Reflection.Patching;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LootValue
{

    internal class ClickItemController
    {

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
                    TooltipController.ClearTooltip();
                    HoverItemController.ClearHoverItem();
                    return runOriginalMethod;
                }

                Item item = __instance.Item;

                if (!ItemUtils.IsItemInPlayerInventory(item))
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
                            Globals.logger.LogInfo($"Quicksell item");

                            int traderPrice = TraderUtils.GetBestTraderPrice(item);
                            int fleaValue = FleaUtils.GetFleaValue(item);

                            if (traderPrice == 0 && fleaValue == 0)
                            {
                                return false;
                            }

                            //One button quicksell
                            if (LootValueMod.OneButtonQuickSell.Value)
                            {

                                if (button == PointerEventData.InputButton.Left)
                                {

                                    int priceOnFlea = FleaUtils.GetFleaMarketUnitPriceWithModifiers(item) * item.StackObjectsCount;
                                    if (TraderUtils.ShouldSellToTraderDueToPriceOrCondition(item))
                                    {
                                        priceOnFlea = 0;
                                    }

                                    if (priceOnFlea > traderPrice)
                                    {
                                        runOriginalMethod = false;
                                        FleaUtils.SellFleaItemOrMultipleItemsIfEnabled(item);
                                    }
                                    else
                                    {
                                        runOriginalMethod = false;
                                        TraderUtils.SellToTrader(item);
                                    }
                                }
                            }
                            else //Two button quicksell
                            {
                                if (button == PointerEventData.InputButton.Left)
                                {
                                    runOriginalMethod = false;
                                    TraderUtils.SellToTrader(item);
                                }
                                else if (button == PointerEventData.InputButton.Right)
                                {
                                    runOriginalMethod = false;
                                    FleaUtils.SellFleaItemOrMultipleItemsIfEnabled(item);
                                }
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    Globals.logger.LogError(ex.Message);

                    if (ex.InnerException != null)
                    {
                        Globals.logger.LogError(ex.InnerException.Message);
                    }
                }
                finally
                {
                    itemSells.Remove(item.Id);
                }

                return runOriginalMethod;
            }

        }

    }

}