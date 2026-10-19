using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MoreInjuries;

internal static class SettingsUi
{
    private static readonly HashSet<string> s_openSections = [];

    internal static void Section(Listing_Standard list, string titleKey, string tooltipKey, Action drawContents)
    {
        list.Gap();
        bool open = s_openSections.Contains(titleKey);
        if (Button(list, (open ? "[-] " : "[+] ") + titleKey.Translate(),
            tooltipKey.Translate() + "\n\n" + "MI_Settings_SectionTip".Translate()))
        {
            open = !open;
            if (open)
            {
                s_openSections.Add(titleKey);
            }
            else
            {
                s_openSections.Remove(titleKey);
            }
        }
        if (open)
        {
            drawContents();
        }
    }

    internal static bool Button(Listing_Standard list, string label, string tooltip)
    {
        Rect rect = list.GetRect(Text.LineHeight + 8f);
        // Translations can wrap at smaller window sizes.
        float extraHeight = Text.CalcHeight(label, rect.width - 16f) + 8f - rect.height;
        if (extraHeight > 0f)
        {
            list.Gap(extraHeight);
            rect.height += extraHeight;
        }
        bool clicked = Widgets.ButtonText(rect, label);
        TooltipHandler.TipRegion(rect, tooltip);
        return clicked;
    }

    internal static void Choice(Listing_Standard list, string labelKey, string tooltipKey,
        int value, string[] optionKeys, string[] optionTooltipKeys, Action<int> select)
    {
        int displayed = Mathf.Clamp(value, 0, optionKeys.Length - 1);
        string tooltip = tooltipKey.Translate() + "\n\n" + optionTooltipKeys[displayed].Translate();
        if (!Button(list, labelKey.Translate() + ": " + optionKeys[displayed].Translate() + "  [+]", tooltip))
        {
            return;
        }
        List<FloatMenuOption> options = [];
        for (int i = 0; i < optionKeys.Length; i++)
        {
            int selected = i;
            options.Add(new FloatMenuOption(optionKeys[i].Translate(), () => select(selected))
            {
                tooltip = optionTooltipKeys[i].Translate()
            });
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    internal static void Slider(Listing_Standard list, ref float value, float min, float max, string tooltipKey,
        int decimals = 2, float displayFactor = 1f, bool ceiling = false, bool floor = false)
    {
        float before = Mathf.Clamp(value * displayFactor, min, max);
        Rect rect = list.GetRect(22f);
        float after = Widgets.HorizontalSlider(rect, before, min, max);
        TooltipHandler.TipRegion(rect, tooltipKey.Translate());
        value = SettingsSliderValue.AfterEdit(value, before, after, displayFactor, decimals, ceiling, floor);
    }

    internal static void Slider(Listing_Standard list, ref int value, int min, int max, string tooltipKey)
    {
        float edited = value;
        Slider(list, ref edited, min, max, tooltipKey, decimals: 0);
        value = (int)edited;
    }
}
