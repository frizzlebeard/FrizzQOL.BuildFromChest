using System.Collections.Generic;
using BuildFromChest;
using Xunit;

public class ChestPayTests
{
    [Fact]
    public void Missing_floors_at_zero()
    {
        Assert.Equal(6, ChestPay.Missing(10, 4));
        Assert.Equal(0, ChestPay.Missing(2, 5));
        Assert.Equal(0, ChestPay.Missing(0, 0));
    }

    [Fact]
    public void AddCost_sums_every_positive_material()
    {
        var costs = new List<ItemAmount>();
        ChestPay.AddCost(costs, "$item_wood", "Wood", 10);
        ChestPay.AddCost(costs, "$item_wood", "Wood", 2);
        ChestPay.AddCost(costs, "$item_stone", "Stone", 4);
        ChestPay.AddCost(costs, "$item_finewood", "FineWood", 8);
        ChestPay.AddCost(costs, "$item_iron", "Iron", 1);
        ChestPay.AddCost(costs, "$item_wood", "Wood", 0);
        ChestPay.AddCost(costs, "", "Wood", 5);
        ChestPay.AddCost(costs, "$item_wood", "", 5);
        ChestPay.AddCost(costs, null, "Wood", 5);
        ChestPay.AddCost(costs, "Wood", "Wood", 5);

        Assert.Equal(5, costs.Count);
        Assert.Equal(12, Amount(costs, "$item_wood"));
        Assert.Equal("Wood", Prefab(costs, "$item_wood"));
        Assert.Equal(4, Amount(costs, "$item_stone"));
        Assert.Equal(8, Amount(costs, "$item_finewood"));
        Assert.Equal("FineWood", Prefab(costs, "$item_finewood"));
        Assert.Equal(1, Amount(costs, "$item_iron"));
        Assert.Equal("Iron", Prefab(costs, "$item_iron"));
        Assert.Equal(5, Amount(costs, "Wood"));
        Assert.Null(Find(costs, "Iron"));
    }

    [Fact]
    public void MissingItems_subtracts_only_what_the_player_already_has()
    {
        var costs = new List<ItemAmount>();
        ChestPay.AddCost(costs, "$item_wood", "Wood", 10);
        ChestPay.AddCost(costs, "$item_iron", "Iron", 4);
        ChestPay.AddCost(costs, "$item_stone", "Stone", 2);

        var have = new Dictionary<string, int>
        {
            ["$item_wood"] = 3,
            ["$item_stone"] = 2
        };

        List<ItemAmount> missing = ChestPay.MissingItems(costs, have);
        Assert.Equal(2, missing.Count);
        Assert.Equal(7, Amount(missing, "$item_wood"));
        Assert.Equal("Wood", Prefab(missing, "$item_wood"));
        Assert.Equal(4, Amount(missing, "$item_iron"));
        Assert.Equal("Iron", Prefab(missing, "$item_iron"));
    }

    [Fact]
    public void NeedsPull_is_false_when_nothing_is_missing()
    {
        Assert.False(ChestPay.NeedsPull(null));
        Assert.False(ChestPay.NeedsPull(new List<ItemAmount>()));
        Assert.False(ChestPay.NeedsPull(new List<ItemAmount>
        {
            new ItemAmount { SharedName = "$item_wood", PrefabName = "Wood", Amount = 0 }
        }));
        Assert.True(ChestPay.NeedsPull(new List<ItemAmount>
        {
            new ItemAmount { SharedName = "$item_resin", PrefabName = "Resin", Amount = 1 }
        }));
    }

    [Fact]
    public void PlanPulls_uses_the_nearest_chest_when_it_has_every_material()
    {
        var missing = new List<ItemAmount>
        {
            new ItemAmount { SharedName = "$item_wood", PrefabName = "Wood", Amount = 6 },
            new ItemAmount { SharedName = "$item_surtlingcore", PrefabName = "SurtlingCore", Amount = 1 }
        };
        var chests = new List<ChestCandidate>
        {
            Chest(1, 8f, true, ("$item_wood", 20), ("$item_surtlingcore", 2)),
            Chest(2, 3f, true, ("$item_wood", 6), ("$item_surtlingcore", 1)),
            Chest(3, 1f, false, ("$item_wood", 50), ("$item_surtlingcore", 5))
        };

        List<PlannedTake> plan = ChestPay.PlanPulls(chests, missing, 10f);

        Assert.Equal(2, plan.Count);
        Assert.All(plan, take => Assert.Equal(2, take.ChestId));
        Assert.Equal(6, Amount(plan, "$item_wood"));
        Assert.Equal(1, Amount(plan, "$item_surtlingcore"));
        Assert.Equal("SurtlingCore", plan.Find(take => take.SharedName == "$item_surtlingcore").PrefabName);
        Assert.Equal(20, chests[0].Counts["$item_wood"]);
        Assert.Equal(6, chests[1].Counts["$item_wood"]);
    }

    [Fact]
    public void PlanPulls_combines_chests_when_the_nearest_is_short()
    {
        var missing = new List<ItemAmount>
        {
            new ItemAmount { SharedName = "$item_wood", PrefabName = "Wood", Amount = 10 },
            new ItemAmount { SharedName = "$item_iron", PrefabName = "Iron", Amount = 4 }
        };
        var chests = new List<ChestCandidate>
        {
            Chest(1, 1f, true, ("$item_wood", 10), ("$item_iron", 0)),
            Chest(2, 4f, true, ("$item_wood", 0), ("$item_iron", 4))
        };

        List<PlannedTake> plan = ChestPay.PlanPulls(chests, missing, 10f);

        Assert.Equal(2, plan.Count);
        Assert.Equal(1, plan[0].ChestId);
        Assert.Equal("$item_wood", plan[0].SharedName);
        Assert.Equal(10, plan[0].Amount);
        Assert.Equal(2, plan[1].ChestId);
        Assert.Equal("$item_iron", plan[1].SharedName);
        Assert.Equal(4, plan[1].Amount);
    }

    [Fact]
    public void PlanPulls_skips_a_closer_chest_that_has_none_of_the_cost()
    {
        var missing = new List<ItemAmount>
        {
            new ItemAmount { SharedName = "$item_finewood", PrefabName = "FineWood", Amount = 5 }
        };
        var chests = new List<ChestCandidate>
        {
            Chest(1, 1f, true, ("$item_wood", 100)),
            Chest(2, 6f, true, ("$item_finewood", 5))
        };

        List<PlannedTake> plan = ChestPay.PlanPulls(chests, missing, 10f);

        Assert.Single(plan);
        Assert.Equal(2, plan[0].ChestId);
        Assert.Equal(5, plan[0].Amount);
        Assert.Equal("FineWood", plan[0].PrefabName);
    }

    [Fact]
    public void PlanPulls_takes_nothing_when_the_area_is_short()
    {
        var missing = new List<ItemAmount>
        {
            new ItemAmount { SharedName = "$item_wood", PrefabName = "Wood", Amount = 10 }
        };
        var shortChests = new List<ChestCandidate>
        {
            Chest(1, 1f, true, ("$item_wood", 3)),
            Chest(2, 2f, true, ("$item_wood", 4))
        };

        Assert.Null(ChestPay.PlanPulls(shortChests, missing, 10f));

        var tooFar = new List<ChestCandidate> { Chest(9, 11f, true, ("$item_wood", 10)) };
        Assert.Null(ChestPay.PlanPulls(tooFar, missing, 10f));
        Assert.Null(ChestPay.PlanPulls(null, missing, 10f));
    }

    [Fact]
    public void PlanPulls_breaks_distance_ties_by_smaller_id()
    {
        var missing = new List<ItemAmount>
        {
            new ItemAmount { SharedName = "$item_wood", PrefabName = "Wood", Amount = 2 }
        };
        var chests = new List<ChestCandidate>
        {
            Chest(5, 4f, true, ("$item_wood", 1)),
            Chest(2, 4f, true, ("$item_wood", 1))
        };

        List<PlannedTake> plan = ChestPay.PlanPulls(chests, missing, 10f);

        Assert.Equal(2, plan[0].ChestId);
        Assert.Equal(5, plan[1].ChestId);
    }

    [Fact]
    public void IsInRange_includes_the_edge_and_rejects_non_positive_radius()
    {
        Assert.True(ChestPay.IsInRange(10f, 10f));
        Assert.False(ChestPay.IsInRange(10.01f, 10f));
        Assert.False(ChestPay.IsInRange(0f, 0f));
        Assert.False(ChestPay.IsInRange(0f, -1f));
    }

    [Fact]
    public void IsChestPrefab_accepts_only_the_four_chest_names()
    {
        Assert.True(ChestPay.IsChestPrefab("piece_chest_wood"));
        Assert.True(ChestPay.IsChestPrefab("piece_chest"));
        Assert.True(ChestPay.IsChestPrefab("piece_chest_private"));
        Assert.True(ChestPay.IsChestPrefab("piece_chest_blackmetal"));
        Assert.False(ChestPay.IsChestPrefab("Cart"));
        Assert.False(ChestPay.IsChestPrefab("Karve"));
        Assert.False(ChestPay.IsChestPrefab("piece_barrel"));
        Assert.False(ChestPay.IsChestPrefab(""));
        Assert.False(ChestPay.IsChestPrefab(null));
    }

    [Fact]
    public void PrefabKey_strips_a_trailing_clone_suffix()
    {
        Assert.Equal("piece_chest_wood", ChestPay.PrefabKey("piece_chest_wood(Clone)"));
        Assert.Equal("Hammer", ChestPay.PrefabKey("Hammer"));
        Assert.Equal("", ChestPay.PrefabKey(null));
        Assert.Equal("", ChestPay.PrefabKey(""));
    }

    [Fact]
    public void IsHammer_accepts_only_Hammer()
    {
        Assert.True(ChestPay.IsHammer("Hammer"));
        Assert.False(ChestPay.IsHammer("Cultivator"));
        Assert.False(ChestPay.IsHammer("Hoe"));
        Assert.False(ChestPay.IsHammer(null));
    }

    [Fact]
    public void PickNearest_chooses_the_closest_open_chest()
    {
        var chests = new List<ChestCandidate>
        {
            Chest(1, 8f, true),
            Chest(2, 3f, true),
            Chest(3, 1f, false)
        };

        Assert.Equal(2, ChestPay.PickNearest(chests, 10f));
    }

    [Fact]
    public void PickNearest_breaks_distance_ties_by_smaller_id()
    {
        var chests = new List<ChestCandidate>
        {
            Chest(5, 4f, true),
            Chest(2, 4f, true)
        };

        Assert.Equal(2, ChestPay.PickNearest(chests, 10f));
    }

    [Fact]
    public void PickNearest_ignores_closed_and_out_of_range_chests()
    {
        var closedNear = new List<ChestCandidate>
        {
            Chest(1, 1f, false),
            Chest(2, 6f, true)
        };
        Assert.Equal(2, ChestPay.PickNearest(closedNear, 10f));

        var tooFar = new List<ChestCandidate> { Chest(9, 11f, true) };
        Assert.Null(ChestPay.PickNearest(tooFar, 10f));
        Assert.Null(ChestPay.PickNearest(new List<ChestCandidate>(), 10f));
        Assert.Null(ChestPay.PickNearest(null, 10f));
    }

    private static ChestCandidate Chest(int id, float distance, bool canOpen, params (string name, int count)[] stacks)
    {
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < stacks.Length; i++)
        {
            counts[stacks[i].name] = stacks[i].count;
        }

        return new ChestCandidate
        {
            Id = id,
            Distance = distance,
            CanOpen = canOpen,
            Counts = counts
        };
    }

    private static ItemAmount Find(IList<ItemAmount> items, string sharedName)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].SharedName == sharedName)
            {
                return items[i];
            }
        }

        return null;
    }

    private static int Amount(IList<ItemAmount> items, string sharedName)
    {
        ItemAmount found = Find(items, sharedName);
        return found == null ? 0 : found.Amount;
    }

    private static string Prefab(IList<ItemAmount> items, string sharedName)
    {
        ItemAmount found = Find(items, sharedName);
        return found == null ? null : found.PrefabName;
    }

    private static int Amount(IList<PlannedTake> items, string sharedName)
    {
        int total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].SharedName == sharedName)
            {
                total += items[i].Amount;
            }
        }

        return total;
    }
}
