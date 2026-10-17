using MoreInjuries.Defs.WellKnown;
using Verse;

namespace MoreInjuries.Initialization;

[StaticConstructorOnStartup]
public static class MedicalDrugGeneIcons_Initializer
{
    static MedicalDrugGeneIcons_Initializer()
    {
        if (!ModsConfig.BiotechActive)
        {
            return;
        }
        foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefsListForReading)
        {
            if (gene.chemical is null || (gene.chemical != KnownChemicalDefOf.MI_Morphine_Chemical && gene.chemical != KnownChemicalDefOf.MI_Ketamine_Chemical))
            {
                continue;
            }
            foreach (string template in new[] { "ChemicalDependency", "AddictionResistant", "AddictionImmune" })
            {
                if (gene.iconPath?.EndsWith("/" + template, StringComparison.Ordinal) is true)
                {
                    // Assets also occupy vanilla's generated paths, so they resolve before any UI access.
                    gene.iconPath = $"UI/Icons/Genes/Chemicals/{gene.chemical.defName}/{template}";
                    break;
                }
            }
        }
    }
}
