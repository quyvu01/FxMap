using FxMap.Fluent;
using Service4.Models;
using Shared.DistributedKeys;

namespace Service4.Configs;

/// <summary>Visits are looked up by Mrn: one patient, many visits.</summary>
public class VisitConfig : EntityConfigureOf<Visit>
{
    protected override void Configure()
    {
        Id(x => x.Mrn);
        DefaultProperty(x => x.Status);
        UseDistributedKey<VisitDistributedKey>();
    }
}
