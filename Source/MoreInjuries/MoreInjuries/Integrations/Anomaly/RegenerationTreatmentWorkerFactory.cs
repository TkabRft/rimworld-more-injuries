using MoreInjuries.HealthConditions;

namespace MoreInjuries.Integrations.Anomaly;

public sealed class RegenerationTreatmentWorkerFactory : IInjuryWorkerFactory
{
    public InjuryWorker Create(MoreInjuryComp parent) => new RegenerationTreatmentWorker(parent);
}
