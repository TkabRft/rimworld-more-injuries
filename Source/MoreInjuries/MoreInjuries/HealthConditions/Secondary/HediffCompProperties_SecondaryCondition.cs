using MoreInjuries.HealthConditions.Secondary.Handlers;
using MoreInjuries.Roslyn.SourceGen.XmlBinding.Attributes;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace MoreInjuries.HealthConditions.Secondary;

[XmlBindable]
public partial class HediffCompProperties_SecondaryCondition : HediffCompProperties<HediffComp_SecondaryCondition>
{
    private static readonly List<IHediffCompHandler<HediffComp_SecondaryCondition>> s_emptyHandlers = [];
    // Null is uncomputed even when an XML materializer bypasses field initializers.
    // Zero is reserved for a schedule that has actually been found to be empty.
    private int? _tickWakeInterval;

    [XmlBinding("severityCurve")]
    public partial SimpleCurve? SeverityCurve { get; }

    [XmlBinding("handlers", DefaultValueFrom = nameof(s_emptyHandlers))]
    public partial IReadOnlyList<IHediffCompHandler<HediffComp_SecondaryCondition>> Handlers { get; }

    public IReadOnlyList<IHediffComp_SecondaryCondition_TickHandler> TickHandlers => field ??=
    [
        .. Handlers.OfType<IHediffComp_SecondaryCondition_TickHandler>()
    ];

    public IReadOnlyList<IHediffComp_SecondaryCondition_PostMakeHandler> PostMakeHandlers => field ??=
    [
        .. Handlers.OfType<IHediffComp_SecondaryCondition_PostMakeHandler>()
    ];

    internal int TickWakeInterval
    {
        get
        {
            return _tickWakeInterval ??= ComputeTickWakeInterval();
        }
    }

    private int ComputeTickWakeInterval()
    {
        IReadOnlyList<IHediffComp_SecondaryCondition_TickHandler> handlers = TickHandlers;
        if (handlers.Count == 0)
        {
            return 0;
        }
        int wakeInterval = 0;
        for (int i = 0; i < handlers.Count; i++)
        {
            int interval = handlers[i].TickInterval;
            if (interval <= 0)
            {
                return -1;
            }
            wakeInterval = wakeInterval == 0 ? interval : GreatestCommonDivisor(wakeInterval, interval);
        }
        return wakeInterval;
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
        {
            int remainder = left % right;
            left = right;
            right = remainder;
        }
        return left;
    }
}
