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
    public void NeedsPull_when_either_material_is_short()
    {
        Assert.False(ChestPay.NeedsPull(0, 0));
        Assert.True(ChestPay.NeedsPull(1, 0));
        Assert.True(ChestPay.NeedsPull(0, 1));
    }

    [Fact]
    public void CanCover_requires_each_missing_amount()
    {
        Assert.True(ChestPay.CanCover(6, 0, 6, 0));
        Assert.True(ChestPay.CanCover(2, 4, 2, 4));
        Assert.False(ChestPay.CanCover(6, 1, 6, 2));
        Assert.False(ChestPay.CanCover(1, 4, 2, 4));
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
    public void AddCost_sums_only_positive_wood_and_stone()
    {
        Assert.Equal("$item_wood", ChestPay.Wood);
        Assert.Equal("$item_stone", ChestPay.Stone);
        Assert.Equal("Wood", ChestPay.WoodPrefab);
        Assert.Equal("Stone", ChestPay.StonePrefab);

        int wood = 0;
        int stone = 0;
        ChestPay.AddCost(ChestPay.Wood, 10, ref wood, ref stone);
        ChestPay.AddCost(ChestPay.Wood, 2, ref wood, ref stone);
        ChestPay.AddCost(ChestPay.Stone, 4, ref wood, ref stone);
        ChestPay.AddCost("FineWood", 8, ref wood, ref stone);
        ChestPay.AddCost(ChestPay.Wood, 0, ref wood, ref stone);
        ChestPay.AddCost("", 5, ref wood, ref stone);
        ChestPay.AddCost(null, 5, ref wood, ref stone);
        Assert.Equal(12, wood);
        Assert.Equal(4, stone);

        ChestPay.AddCost("Wood", 5, ref wood, ref stone);
        Assert.Equal(12, wood);
        ChestPay.AddCost("$item_wood", 5, ref wood, ref stone);
        Assert.Equal(17, wood);
        ChestPay.AddCost("$item_stone", 3, ref wood, ref stone);
        Assert.Equal(7, stone);
        ChestPay.AddCost("Stone", 3, ref wood, ref stone);
        Assert.Equal(7, stone);
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

    private static ChestCandidate Chest(int id, float distance, bool canOpen)
    {
        return new ChestCandidate
        {
            Id = id,
            Distance = distance,
            CanOpen = canOpen,
            WoodCount = 0,
            StoneCount = 0
        };
    }
}
