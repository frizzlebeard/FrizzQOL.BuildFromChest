using System.Collections.Generic;

namespace BuildFromChest
{
    public sealed class ItemAmount
    {
        public string SharedName;
        public string PrefabName;
        public int Amount;
    }

    public sealed class PlannedTake
    {
        public int ChestId;
        public string SharedName;
        public string PrefabName;
        public int Amount;
    }

    public sealed class ChestCandidate
    {
        public int Id;
        public float Distance;
        public bool CanOpen;
        public Dictionary<string, int> Counts;
    }

    public static class ChestPay
    {
        public static int Missing(int cost, int inInventory)
        {
            int missing = cost - inInventory;
            return missing > 0 ? missing : 0;
        }

        public static void AddCost(IList<ItemAmount> costs, string sharedName, string prefabName, int amount)
        {
            if (costs == null || amount <= 0 || string.IsNullOrEmpty(sharedName) || string.IsNullOrEmpty(prefabName))
            {
                return;
            }

            for (int i = 0; i < costs.Count; i++)
            {
                ItemAmount cost = costs[i];
                if (cost != null && cost.SharedName == sharedName)
                {
                    cost.Amount += amount;
                    return;
                }
            }

            costs.Add(new ItemAmount
            {
                SharedName = sharedName,
                PrefabName = prefabName,
                Amount = amount
            });
        }

        public static List<ItemAmount> MissingItems(IList<ItemAmount> costs, IDictionary<string, int> have)
        {
            var missing = new List<ItemAmount>();
            if (costs == null)
            {
                return missing;
            }

            for (int i = 0; i < costs.Count; i++)
            {
                ItemAmount cost = costs[i];
                if (cost == null || cost.Amount <= 0 || string.IsNullOrEmpty(cost.SharedName) || string.IsNullOrEmpty(cost.PrefabName))
                {
                    continue;
                }

                int got = 0;
                if (have != null)
                {
                    have.TryGetValue(cost.SharedName, out got);
                }

                int need = Missing(cost.Amount, got);
                if (need <= 0)
                {
                    continue;
                }

                missing.Add(new ItemAmount
                {
                    SharedName = cost.SharedName,
                    PrefabName = cost.PrefabName,
                    Amount = need
                });
            }

            return missing;
        }

        public static bool NeedsPull(IList<ItemAmount> missing)
        {
            if (missing == null)
            {
                return false;
            }

            for (int i = 0; i < missing.Count; i++)
            {
                ItemAmount item = missing[i];
                if (item != null && item.Amount > 0 && !string.IsNullOrEmpty(item.SharedName) && !string.IsNullOrEmpty(item.PrefabName))
                {
                    return true;
                }
            }

            return false;
        }

        public static List<PlannedTake> PlanPulls(IList<ChestCandidate> chests, IList<ItemAmount> missing, float radius)
        {
            var need = new Dictionary<string, int>();
            var prefabs = new Dictionary<string, string>();
            if (missing != null)
            {
                for (int i = 0; i < missing.Count; i++)
                {
                    ItemAmount item = missing[i];
                    if (item == null || item.Amount <= 0 || string.IsNullOrEmpty(item.SharedName) || string.IsNullOrEmpty(item.PrefabName))
                    {
                        continue;
                    }

                    if (need.ContainsKey(item.SharedName))
                    {
                        need[item.SharedName] += item.Amount;
                    }
                    else
                    {
                        need[item.SharedName] = item.Amount;
                        prefabs[item.SharedName] = item.PrefabName;
                    }
                }
            }

            if (need.Count == 0)
            {
                return new List<PlannedTake>();
            }

            if (chests == null)
            {
                return null;
            }

            var open = new List<ChestCandidate>();
            for (int i = 0; i < chests.Count; i++)
            {
                ChestCandidate candidate = chests[i];
                if (candidate == null || !candidate.CanOpen || !IsInRange(candidate.Distance, radius))
                {
                    continue;
                }

                open.Add(candidate);
            }

            open.Sort(CompareChests);

            var plan = new List<PlannedTake>();
            for (int i = 0; i < open.Count; i++)
            {
                ChestCandidate candidate = open[i];
                foreach (string sharedName in new List<string>(need.Keys))
                {
                    int still = need[sharedName];
                    if (still <= 0)
                    {
                        continue;
                    }

                    int have = 0;
                    if (candidate.Counts != null)
                    {
                        candidate.Counts.TryGetValue(sharedName, out have);
                    }

                    int take = have < still ? have : still;
                    if (take <= 0)
                    {
                        continue;
                    }

                    plan.Add(new PlannedTake
                    {
                        ChestId = candidate.Id,
                        SharedName = sharedName,
                        PrefabName = prefabs[sharedName],
                        Amount = take
                    });
                    need[sharedName] = still - take;
                }
            }

            foreach (int left in need.Values)
            {
                if (left > 0)
                {
                    return null;
                }
            }

            return plan;
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

        private static int CompareChests(ChestCandidate a, ChestCandidate b)
        {
            int byDistance = a.Distance.CompareTo(b.Distance);
            if (byDistance != 0)
            {
                return byDistance;
            }

            return a.Id.CompareTo(b.Id);
        }
    }
}
