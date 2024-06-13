using System.Reflection;
using Aki.Reflection.Patching;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine.EventSystems;


namespace LootValue
{

    internal class HoverItemController
    {
        public static Item hoveredItem;

        public static void ClearHoverItem() {
            hoveredItem = null;
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
                }
            }
        }

        internal class GridItemOnPointerExitPatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod() => typeof(GridItemView).GetMethod("OnPointerExit", BindingFlags.Instance | BindingFlags.Public);

            [PatchPrefix]
            static void Prefix(GridItemView __instance, PointerEventData eventData)
            {
                hoveredItem = null;
            }

        }

    }

}