using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace BuildFromChest
{
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    internal static class PlacePatch
    {
        private static readonly MethodInfo CheckAccessMethod = AccessTools.Method(typeof(Container), "CheckAccess");
        private static readonly MethodInfo LoadMethod = AccessTools.Method(typeof(Container), "Load", System.Type.EmptyTypes);
        private static readonly MethodInfo SaveMethod = AccessTools.Method(typeof(Container), "Save");
        private static readonly FieldInfo NViewField = AccessTools.Field(typeof(Container), "m_nview");

        private static bool _busy;
        private static bool _loggedAccess;
        private static bool _loggedSave;
        private static readonly List<HeldTake> Held = new List<HeldTake>();

        private static void Prefix(Player __instance, Piece piece)
        {
            // UpdatePlacement already pulled on this click. Putting the items back here
            // would fail the place that HaveRequirements just allowed.
            if (_busy || Held.Count > 0)
            {
                return;
            }

            Prepare(__instance, piece);
        }

        internal static void Prepare(Player player, Piece piece)
        {
            if (_busy || Held.Count > 0)
            {
                return;
            }

            if (player == null || piece == null || player != Player.m_localPlayer)
            {
                return;
            }

            if (!CanSaveChests())
            {
                return;
            }

            ItemDrop.ItemData tool = player.RightItem;
            string toolName = tool != null && tool.m_dropPrefab != null
                ? ChestPay.PrefabKey(tool.m_dropPrefab.name)
                : "";
            if (!ChestPay.IsHammer(toolName))
            {
                return;
            }

            if (IsFreeBuild(piece))
            {
                return;
            }

            var costs = new List<ItemAmount>();
            SumCosts(piece.m_resources, costs);

            Inventory inventory = player.GetInventory();
            if (inventory == null)
            {
                return;
            }

            List<ItemAmount> missing = ChestPay.MissingItems(costs, CountHave(inventory, costs));
            if (!ChestPay.NeedsPull(missing))
            {
                return;
            }

            float radius = Plugin.CurrentRadius();
            var chests = new List<ChestCandidate>();
            var byId = new Dictionary<int, Container>();
            FindChests(player, radius, missing, chests, byId);
            List<PlannedTake> plan = ChestPay.PlanPulls(chests, missing, radius);
            if (plan == null || plan.Count == 0)
            {
                return;
            }

            _busy = true;
            try
            {
                ApplyPlan(inventory, plan, byId);
            }
            catch (System.Exception ex)
            {
                ReturnHeld(player);
                Plugin.LogWarning("Chest pull failed: " + ex.Message);
            }
            finally
            {
                _busy = false;
                byId.Clear();
            }
        }

        // HaveRequirements failed, or TryPlacePiece never ran. A successful place already cleared this.
        internal static void Finish(Player player)
        {
            ReturnHeld(player);
        }

        private static bool IsFreeBuild(Piece piece)
        {
            ZoneSystem zone = ZoneSystem.instance;
            if (piece == null || zone == null)
            {
                return false;
            }

            return zone.GetGlobalKey(piece.FreeBuildKey());
        }

        private static void Postfix(Player __instance, bool __result)
        {
            try
            {
                if (__result)
                {
                    Held.Clear();
                    return;
                }

                ReturnHeld(__instance);
            }
            finally
            {
                _busy = false;
            }
        }

        private static void SumCosts(Piece.Requirement[] requirements, List<ItemAmount> costs)
        {
            if (requirements == null)
            {
                return;
            }

            for (int i = 0; i < requirements.Length; i++)
            {
                Piece.Requirement requirement = requirements[i];
                if (requirement == null || requirement.m_resItem == null || requirement.m_resItem.m_itemData == null || requirement.m_resItem.m_itemData.m_shared == null)
                {
                    continue;
                }

                if (requirement.m_amount <= 0 || requirement.m_resItem.gameObject == null)
                {
                    continue;
                }

                string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;
                string prefabName = ChestPay.PrefabKey(requirement.m_resItem.gameObject.name);
                ChestPay.AddCost(costs, sharedName, prefabName, requirement.m_amount);
            }
        }

        private static Dictionary<string, int> CountHave(Inventory inventory, List<ItemAmount> costs)
        {
            var have = new Dictionary<string, int>();
            for (int i = 0; i < costs.Count; i++)
            {
                string sharedName = costs[i].SharedName;
                if (have.ContainsKey(sharedName))
                {
                    continue;
                }

                have[sharedName] = inventory.CountItems(sharedName, -1, true);
            }

            return have;
        }

        private static void FindChests(Player player, float radius, List<ItemAmount> missing, List<ChestCandidate> chests, Dictionary<int, Container> byId)
        {
            if (radius <= 0f)
            {
                return;
            }

            Vector3 origin = player.transform.position;
            // Local on purpose. A reused buffer kept destroyed colliders alive and, once it
            // filled, stopped seeing chests in a built-up area.
            Collider[] hits = Physics.OverlapSphere(origin, radius + 2f, Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                hits[i] = null;
                if (hit == null)
                {
                    continue;
                }

                Container container = hit.GetComponentInParent<Container>();
                if (container == null)
                {
                    continue;
                }

                int id = container.GetInstanceID();
                if (byId.ContainsKey(id))
                {
                    continue;
                }

                string key = ChestPay.PrefabKey(container.gameObject.name);
                if (!ChestPay.IsChestPrefab(key))
                {
                    continue;
                }

                bool canOpen = CanOpen(container, player);
                Inventory inventory = canOpen ? container.GetInventory() : null;
                if (canOpen && inventory != null)
                {
                    RefreshChest(container);
                    inventory = container.GetInventory();
                }

                var counts = new Dictionary<string, int>();
                if (inventory != null)
                {
                    for (int n = 0; n < missing.Count; n++)
                    {
                        string sharedName = missing[n].SharedName;
                        if (counts.ContainsKey(sharedName))
                        {
                            continue;
                        }

                        counts[sharedName] = inventory.CountItems(sharedName, -1, true);
                    }
                }

                chests.Add(new ChestCandidate
                {
                    Id = id,
                    Distance = Vector3.Distance(origin, container.transform.position),
                    CanOpen = canOpen && inventory != null,
                    Counts = counts
                });
                byId[id] = container;
            }
        }

        private static void ApplyPlan(Inventory playerInventory, List<PlannedTake> plan, Dictionary<int, Container> chests)
        {
            var moved = new List<HeldTake>();
            HeldTake current = null;
            int currentId = 0;
            try
            {
                for (int i = 0; i < plan.Count; i++)
                {
                    PlannedTake take = plan[i];
                    if (take == null || take.Amount <= 0)
                    {
                        continue;
                    }

                    if (current == null || take.ChestId != currentId)
                    {
                        if (current != null)
                        {
                            SaveChest(current.Chest);
                            moved.Add(current);
                        }

                        if (!chests.TryGetValue(take.ChestId, out Container next) || next == null || next.GetInventory() == null)
                        {
                            UndoMoved(playerInventory, moved);
                            return;
                        }

                        current = new HeldTake { Chest = next };
                        currentId = take.ChestId;
                    }

                    Inventory chestInventory = current.Chest.GetInventory();
                    int got = Take(chestInventory, take.SharedName, take.Amount);
                    int put = got == take.Amount ? Give(playerInventory, take.PrefabName, take.SharedName, take.Amount) : 0;
                    current.Items.Add(new MovedStack
                    {
                        SharedName = take.SharedName,
                        PrefabName = take.PrefabName,
                        TakenFromChest = got,
                        GivenToPlayer = put
                    });
                    if (got != take.Amount || put != take.Amount)
                    {
                        SaveChest(current.Chest);
                        moved.Add(current);
                        current = null;
                        UndoMoved(playerInventory, moved);
                        return;
                    }
                }

                if (current != null)
                {
                    SaveChest(current.Chest);
                    moved.Add(current);
                }

                Held.AddRange(moved);
            }
            catch (System.Exception ex)
            {
                if (current != null && !moved.Contains(current))
                {
                    moved.Add(current);
                }

                UndoMoved(playerInventory, moved);
                Held.Clear();
                Plugin.LogWarning("Chest pull failed, restoring items: " + ex.Message);
            }
        }

        private static void ReturnHeld(Player player)
        {
            if (Held.Count == 0)
            {
                _busy = false;
                return;
            }

            var copy = new List<HeldTake>(Held);
            Held.Clear();
            _busy = false;
            if (player == null || player.GetInventory() == null)
            {
                Plugin.LogWarning("Could not return pulled materials.");
                return;
            }

            UndoMoved(player.GetInventory(), copy);
        }

        private static void UndoMoved(Inventory playerInventory, List<HeldTake> moved)
        {
            for (int i = 0; i < moved.Count; i++)
            {
                HeldTake held = moved[i];
                if (held == null || held.Chest == null || held.Chest.GetInventory() == null || playerInventory == null)
                {
                    Plugin.LogWarning("Could not return pulled materials.");
                    continue;
                }

                Inventory chestInventory = held.Chest.GetInventory();
                for (int n = 0; n < held.Items.Count; n++)
                {
                    MovedStack stack = held.Items[n];
                    int back = Take(playerInventory, stack.SharedName, stack.GivenToPlayer);
                    int returning = stack.TakenFromChest - stack.GivenToPlayer + back;
                    int stored = Give(chestInventory, stack.PrefabName, stack.SharedName, returning);
                    int left = returning - stored;
                    if (left > 0)
                    {
                        Give(playerInventory, stack.PrefabName, stack.SharedName, left);
                    }

                    if (back != stack.GivenToPlayer || left > 0)
                    {
                        Plugin.LogWarning("Pulled materials could not all return to the chest.");
                    }
                }

                SaveChest(held.Chest);
            }
        }

        private static bool CanOpen(Container container, Player player)
        {
            if (CheckAccessMethod == null)
            {
                if (!_loggedAccess)
                {
                    _loggedAccess = true;
                    Plugin.LogWarning("Container.CheckAccess is missing. Chest pull is off.");
                }

                return false;
            }

            object result = CheckAccessMethod.Invoke(container, new object[] { player.GetPlayerID() });
            if (!(result is bool open) || !open || container.IsInUse())
            {
                return false;
            }

            if (NViewField == null)
            {
                return false;
            }

            object viewObj = NViewField.GetValue(container);
            if (!(viewObj is ZNetView view) || view == null || !view.IsValid() || !view.IsOwner())
            {
                return false;
            }

            return true;
        }

        private static bool CanSaveChests()
        {
            if (SaveMethod != null)
            {
                return true;
            }

            if (!_loggedSave)
            {
                _loggedSave = true;
                Plugin.LogWarning("Container.Save is missing. Chest pull is off.");
            }

            return false;
        }

        private static void RefreshChest(Container container)
        {
            if (container != null && LoadMethod != null)
            {
                LoadMethod.Invoke(container, null);
            }
        }

        private static void SaveChest(Container container)
        {
            if (container != null && SaveMethod != null)
            {
                SaveMethod.Invoke(container, null);
            }
        }

        private static int Take(Inventory inventory, string sharedName, int amount)
        {
            if (inventory == null || amount <= 0 || string.IsNullOrEmpty(sharedName))
            {
                return 0;
            }

            int before = inventory.CountItems(sharedName, -1, true);
            inventory.RemoveItem(sharedName, amount, -1, true);
            return before - inventory.CountItems(sharedName, -1, true);
        }

        private static int Give(Inventory inventory, string prefabName, string sharedName, int amount)
        {
            if (inventory == null || amount <= 0 || string.IsNullOrEmpty(prefabName) || string.IsNullOrEmpty(sharedName))
            {
                return 0;
            }

            int before = inventory.CountItems(sharedName, -1, true);
            inventory.AddItem(prefabName, amount, 1, 0, 0L, "", false, false);
            return inventory.CountItems(sharedName, -1, true) - before;
        }

        private sealed class HeldTake
        {
            public Container Chest;
            public readonly List<MovedStack> Items = new List<MovedStack>();
        }

        private sealed class MovedStack
        {
            public string SharedName;
            public string PrefabName;
            public int TakenFromChest;
            public int GivenToPlayer;
        }
    }

    // Called from Player.UpdatePlacement, so this type and method stay public.
    public static class PlacementClick
    {
        public static void PullBeforeRequirements(Player player, Piece piece)
        {
            PlacePatch.Prepare(player, piece);
        }

        [HarmonyPatch(typeof(Player), "UpdatePlacement")]
        private static class UpdatePlacementPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var list = new List<CodeInstruction>(instructions);
                MethodInfo pull = AccessTools.Method(typeof(PlacementClick), nameof(PullBeforeRequirements));
                for (int i = 3; i < list.Count; i++)
                {
                    if (!IsPieceRequirementCheck(list[i]))
                    {
                        continue;
                    }

                    CodeInstruction pieceLoad = list[i - 2];
                    if (list[i - 3].opcode != OpCodes.Ldarg_0 || !IsLoadLocal(pieceLoad.opcode) || list[i - 1].opcode != OpCodes.Ldc_I4_0)
                    {
                        continue;
                    }

                    int at = i - 3;
                    list.Insert(at, new CodeInstruction(OpCodes.Ldarg_0));
                    list.Insert(at + 1, new CodeInstruction(pieceLoad.opcode, pieceLoad.operand));
                    list.Insert(at + 2, new CodeInstruction(OpCodes.Call, pull));
                    return list;
                }

                Plugin.LogWarning("UpdatePlacement does not check requirements before place. A short hammer click will not pull.");
                return list;
            }

            private static void Postfix(Player __instance)
            {
                PlacePatch.Finish(__instance);
            }

            private static System.Exception Finalizer(Player __instance, System.Exception __exception)
            {
                PlacePatch.Finish(__instance);
                return __exception;
            }

            private static bool IsPieceRequirementCheck(CodeInstruction instruction)
            {
                if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
                {
                    return false;
                }

                MethodInfo method = instruction.operand as MethodInfo;
                if (method == null || method.Name != "HaveRequirements")
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 2 && parameters[0].ParameterType == typeof(Piece);
            }

            private static bool IsLoadLocal(OpCode opcode)
            {
                return opcode == OpCodes.Ldloc
                    || opcode == OpCodes.Ldloc_S
                    || opcode == OpCodes.Ldloc_0
                    || opcode == OpCodes.Ldloc_1
                    || opcode == OpCodes.Ldloc_2
                    || opcode == OpCodes.Ldloc_3;
            }
        }
    }
}
