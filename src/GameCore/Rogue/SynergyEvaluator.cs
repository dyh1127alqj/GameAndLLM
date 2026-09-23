using System.Collections.Generic;
using System.Linq;

namespace GameCore.Rogue;

public readonly record struct ActivatedSynergy(AffixTag Tag, int Tier, bool IsTeamWide);

public sealed class SynergyEvaluationResult
{
    public IReadOnlyDictionary<AffixTag, int> TagCounts { get; }
    public IReadOnlyList<ActivatedSynergy> ActiveSynergies { get; }

    public SynergyEvaluationResult(Dictionary<AffixTag, int> counts, List<ActivatedSynergy> synergies)
    {
        TagCounts = counts;
        ActiveSynergies = synergies;
    }

    public int GetCount(AffixTag tag) => TagCounts.TryGetValue(tag, out int c) ? c : 0;
    public bool HasSynergy(AffixTag tag, int tier) => ActiveSynergies.Any(s => s.Tag == tag && s.Tier == tier);
    public bool IsSynergyActive(AffixTag tag, int tier) => HasSynergy(tag, tier);
}

public static class SynergyEvaluator
{
    public static SynergyEvaluationResult Evaluate(IDictionary<int, VesselSockets> dict) => Evaluate(dict.Values);

    public static SynergyEvaluationResult Evaluate(IEnumerable<VesselSockets> teamSockets)
    {
        var counts = new Dictionary<AffixTag, int>();

        foreach (var s in teamSockets)
        {
            foreach (var core in s.CoreAffixes)
            {
                foreach (var tag in core.Definition.Tags)
                {
                    counts[tag] = counts.GetValueOrDefault(tag, 0) + 1;
                }
            }
        }

        var activeSynergies = new List<ActivatedSynergy>();
        foreach (var (tag, count) in counts)
        {
            bool isClass = tag is not (AffixTag.Shushan or AffixTag.Cyber);

            if (isClass)
            {
                if (count >= 2) activeSynergies.Add(new ActivatedSynergy(tag, 2, IsTeamWide: false));
                if (count >= 4) activeSynergies.Add(new ActivatedSynergy(tag, 4, IsTeamWide: false));
                if (count >= 6) activeSynergies.Add(new ActivatedSynergy(tag, 6, IsTeamWide: true));
            }
            else
            {
                if (count >= 2) activeSynergies.Add(new ActivatedSynergy(tag, 2, IsTeamWide: false));
                if (count >= 4) activeSynergies.Add(new ActivatedSynergy(tag, 4, IsTeamWide: true));
            }
        }

        return new SynergyEvaluationResult(counts, activeSynergies);
    }
}
