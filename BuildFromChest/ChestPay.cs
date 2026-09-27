using System.Collections.Generic;

namespace BuildFromChest
{
    public sealed class ChestCandidate
    {
        public int Id;
        public float Distance;
        public bool CanOpen;
        public int WoodCount;
        public int StoneCount;
    }

    public static class ChestPay
    {
        public const string Wood = "$item_wood";
        public const string Stone = "$item_stone";
        public const string WoodPrefab = "Wood";
        public const string StonePrefab = "Stone";

        public static int Missing(int cost, int inInventory)
        {
            int missing = cost - inInventory;
            return missing > 0 ? missing : 0;
        }

        public static bool NeedsPull(int missingWood, int missingStone)
        {
            return missingWood > 0 || missingStone > 0;
        }

        public static bool CanCover(int chestWood, int chestStone, int missingWood, int missingStone)
        {
            return chestWood >= missingWood && chestStone >= missingStone;
        }

        public static bool IsInRange(float distance, float radius)
        {
            return radius > 0f && distance <= radius;
        }

        public static bool IsChestPrefab(string name)
        {
            return name == "piece_chest_wood"
                || name == "piece_chest"
                || name == "piece_chest_private"
                || name == "piece_chest_blackmetal";
        }

        public static bool IsHammer(string prefabName)
        {
            return prefabName == "Hammer";
        }

        public static string PrefabKey(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            const string suffix = "(Clone)";
            if (name.EndsWith(suffix))
            {
                return name.Substring(0, name.Length - suffix.Length);
            }

            return name;
        }

        public static void AddCost(string itemName, int amount, ref int wood, ref int stone)
        {
            if (string.IsNullOrEmpty(itemName) || amount <= 0)
            {
                return;
            }

            if (itemName == Wood)
            {
                wood += amount;
            }
            else if (itemName == Stone)
            {
                stone += amount;
            }
        }

        public static int? PickNearest(IList<ChestCandidate> candidates, float radius)
        {
            int? bestId = null;
            float bestDistance = 0f;
            if (candidates == null)
            {
                return null;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                ChestCandidate candidate = candidates[i];
                if (candidate == null || !candidate.CanOpen || !IsInRange(candidate.Distance, radius))
                {
                    continue;
                }

                if (bestId == null || candidate.Distance < bestDistance || (candidate.Distance == bestDistance && candidate.Id < bestId.Value))
                {
                    bestId = candidate.Id;
                    bestDistance = candidate.Distance;
                }
            }

            return bestId;
        }
    }
}
