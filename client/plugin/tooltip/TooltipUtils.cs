namespace LootValue
{

    internal static class TooltipUtils
    {

        // TODO: add a method that adds a warning ( parenthesis with size 11 and color red )

        public static void AppendFullLineToTooltip(ref string tooltipText, string text, int? size, string color)
        {
            if (size.HasValue)
            {
                StartSizeTag(ref tooltipText, size.Value);
            }
            AppendNewLineToTooltipText(ref tooltipText);
            AppendTextToToolip(ref tooltipText, text, color);
            if (size.HasValue)
            {
                EndSizeTag(ref tooltipText);
            }
        }

        public static void AppendNewLineToTooltipText(ref string tooltipText)
        {
            tooltipText += $"<br>";
        }

        public static void AppendTextToToolip(ref string tooltipText, string addText, string color)
        {
            tooltipText += $"<color={color}>{addText}</color>";
        }

        public static void StartSizeTag(ref string tooltipText, int size)
        {
            tooltipText += $"<size={size}>";
        }

        public static void EndSizeTag(ref string tooltipText)
        {
            tooltipText += $"</size>";
        }

        public static void AppendSeparator(ref string tooltipText, string color = "#444444", bool appendNewLineAfter = true)
        {
            AppendNewLineToTooltipText(ref tooltipText);
            AppendTextToToolip(ref tooltipText, "--------------------", color);
            if (appendNewLineAfter)
            {
                AppendNewLineToTooltipText(ref tooltipText);
            }
        }

    }

}