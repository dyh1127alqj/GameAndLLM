using System;
using System.Collections.Generic;
using System.Linq;

namespace GameCore.Rogue;

public enum RogueNodeType
{
    NormalBattle,
    EliteBattle,
    Merchant,
    Safehouse,
    Event,
    Rift,
    Boss
}

public sealed class RogueMapNode
{
    public string NodeId { get; }
    public int StepIndex { get; }
    public int Step => StepIndex;
    public int TrackIndex { get; }
    public int Track => TrackIndex;
    public RogueNodeType Type { get; }
    public RogueNodeType NodeType => Type;
    public List<string> NextNodeIds { get; } = new();

    public RogueMapNode(string nodeId, int stepIndex, int trackIndex, RogueNodeType type)
    {
        NodeId = nodeId;
        StepIndex = stepIndex;
        TrackIndex = trackIndex;
        Type = type;
    }
}

public sealed class RogueFloorMap
{
    public int FloorIndex { get; }
    public Dictionary<string, RogueMapNode> Nodes { get; } = new();
    public int TotalSteps => 6;
    public IEnumerable<RogueMapNode> AllNodes => Nodes.Values;

    public RogueFloorMap(int floorIndex)
    {
        FloorIndex = floorIndex;
    }
}

public static class RogueMapGenerator
{
    public static RogueFloorMap Generate(int seed, int floorIndex = 1) => GenerateFloor(seed, floorIndex);

    public static RogueFloorMap GenerateFloor(int seed, int floorIndex)
    {
        var map = new RogueFloorMap(floorIndex);
        var rng = new Random(seed ^ (floorIndex * 7919));

        for (int step = 0; step < 5; step++)
        {
            for (int track = 0; track < 3; track++)
            {
                string id = $"f{floorIndex}_s{step}_t{track}";
                var type = RollNodeType(step, rng);
                map.Nodes[id] = new RogueMapNode(id, step, track, type);
            }
        }

        // 跨界裂隙保底：每层至少出现 1 个 Rift 节点 (D-22 / 审查 2.2 规范)
        if (!map.Nodes.Values.Any(n => n.Type == RogueNodeType.Rift))
        {
            var candidates = map.Nodes.Values
                .Where(n => n.StepIndex >= 1 && n.StepIndex <= 3 && n.Type != RogueNodeType.Safehouse)
                .OrderBy(n => n.NodeId)
                .ToList();

            if (candidates.Count > 0)
            {
                var pick = candidates[rng.Next(candidates.Count)];
                map.Nodes[pick.NodeId] = new RogueMapNode(pick.NodeId, pick.StepIndex, pick.TrackIndex, RogueNodeType.Rift);
            }
        }

        string bossId = $"f{floorIndex}_s5_boss";
        map.Nodes[bossId] = new RogueMapNode(bossId, 5, 1, RogueNodeType.Boss);

        for (int step = 0; step < 4; step++)
        {
            for (int track = 0; track < 3; track++)
            {
                string curId = $"f{floorIndex}_s{step}_t{track}";
                var cur = map.Nodes[curId];
                cur.NextNodeIds.Add($"f{floorIndex}_s{step + 1}_t{track}");
                if (track > 0) cur.NextNodeIds.Add($"f{floorIndex}_s{step + 1}_t{track - 1}");
                if (track < 2) cur.NextNodeIds.Add($"f{floorIndex}_s{step + 1}_t{track + 1}");
            }
        }

        for (int track = 0; track < 3; track++)
        {
            string curId = $"f{floorIndex}_s4_t{track}";
            map.Nodes[curId].NextNodeIds.Add(bossId);
        }

        return map;
    }

    private static RogueNodeType RollNodeType(int step, Random rng)
    {
        if (step == 0) return RogueNodeType.NormalBattle;
        if (step == 3) return RogueNodeType.Safehouse;

        int roll = rng.Next(100);
        return roll switch
        {
            < 40 => RogueNodeType.NormalBattle,
            < 65 => RogueNodeType.EliteBattle,
            < 80 => RogueNodeType.Event,
            < 90 => RogueNodeType.Merchant,
            < 95 => RogueNodeType.Safehouse,
            _ => RogueNodeType.Rift
        };
    }
}
