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
        private static readonly MethodInfo SaveMethod = AccessTools.Method(typeof(Container), "Save");
        private static readonly FieldInfo NViewField = AccessTools.Field(typeof(Container), "m_nview");

        private static bool _busy;
        private static bool _loggedAccess;
        private static bool _loggedSave;
        private static Container _chest;
        private static int _wood;
        private static int _stone;

        private static void Prefix(Player __instance, Piece piece)
        {
            // UpdatePlacement already pulled on this click. Putting the items back here
            // would fail the place that HaveRequirements just allowed.
            if (_busy || _chest != null)
            {
                return;
            }

            Prepare(__instance, piece);
        }

        internal static void Prepare(Player player, Piece piece)
        {
            if (_busy || _chest != null)
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

            int costWood = 0;
            int costStone = 0;
            SumCosts(piece.m_resources, ref costWood, ref costStone);

            Inventory inventory = player.GetInventory();
            if (inventory == null)
            {
                return;
            }

            int missingWood = ChestPay.Missing(costWood, inventory.CountItems(ChestPay.Wood, -1, true));
            int missingStone = ChestPay.Missing(costStone, inventory.CountItems(ChestPay.Stone, -1, true));
            if (!ChestPay.NeedsPull(missingWood, missingStone))
            {
                return;
            }

            float radius = Plugin.CurrentRadius();
            Container chosen = FindChest(player, radius, out int chestWood, out int chestStone);
            if (chosen == null || !ChestPay.CanCover(chestWood, chestStone, missingWood, missingStone))
            {
                return;
            }

            _busy = true;
            try
            {
                if (!MoveToPlayer(chosen, inventory, missingWood, missingStone))
                {
                    return;
                }

                _chest = chosen;
                _wood = missingWood;
                _stone = missingStone;
            }
            catch (System.Exception ex)
            {
                _chest = null;
                _wood = 0;
                _stone = 0;
                Plugin.LogWarning("Chest pull failed: " + ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        // HaveRequirements failed, or TryPlacePiece never ran. A successful place already cleared this.
        internal static void Finish(Player player)
        {
            if (_chest == null)
            {
                _busy = false;
                return;
            }

            Container chest = _chest;
            int wood = _wood;
            int stone = _stone;
            _chest = null;
            _wood = 0;
            _stone = 0;
            _busy = false;
            ReturnToChest(player, chest, wood, stone);
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
                if (_chest == null)
                {
                    return;
                }

                Container chest = _chest;
                int wood = _wood;
                int stone = _stone;
                _chest = null;
                _wood = 0;
                _stone = 0;
                if (!__result)
                {
                    ReturnToChest(__instance, chest, wood, stone);
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private static void SumCosts(Piece.Requirement[] requirements, ref int wood, ref int stone)
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

                ChestPay.AddCost(requirement.m_resItem.m_itemData.m_shared.m_name, requirement.m_amount, ref wood, ref stone);
            }
        }

        private static Collider[] _hits = new Collider[256];
        private static readonly List<ChestCandidate> Candidates = new List<ChestCandidate>();
        private static readonly Dictionary<int, Container> ById = new Dictionary<int, Container>();

        private static Container FindChest(Player player, float radius, out int wood, out int stone)
        {
            wood = 0;
            stone = 0;
            if (radius <= 0f)
            {
                return null;
            }

            Vector3 origin = player.transform.position;
            int count = Overlap(origin, radius + 2f);
            Candidates.Clear();
            ById.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i];
                _hits[i] = null;
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
                if (ById.ContainsKey(id))
                {
                    continue;
                }

                string key = ChestPay.PrefabKey(container.gameObject.name);
                if (!ChestPay.IsChestPrefab(key))
                {
                    continue;
                }

                Inventory inventory = container.GetInventory();
                bool canOpen = inventory != null && CanOpen(container, player);
                var candidate = new ChestCandidate
                {
                    Id = id,
                    Distance = Vector3.Distance(origin, container.transform.position),
                    CanOpen = canOpen,
                    WoodCount = canOpen ? inventory.CountItems(ChestPay.Wood, -1, true) : 0,
                    StoneCount = canOpen ? inventory.CountItems(ChestPay.Stone, -1, true) : 0
                };
                Candidates.Add(candidate);
                ById[id] = container;
            }

            int? picked = ChestPay.PickNearest(Candidates, radius);
            if (picked == null || !ById.TryGetValue(picked.Value, out Container chosen))
            {
                return null;
            }

            for (int i = 0; i < Candidates.Count; i++)
            {
                if (Candidates[i].Id == picked.Value)
                {
                    wood = Candidates[i].WoodCount;
                    stone = Candidates[i].StoneCount;
                    break;
                }
            }

            return chosen;
        }

        private static int Overlap(Vector3 origin, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, radius, _hits, Physics.AllLayers, QueryTriggerInteraction.Collide);
            while (count == _hits.Length && _hits.Length < 4096)
            {
                _hits = new Collider[_hits.Length * 2];
                count = Physics.OverlapSphereNonAlloc(origin, radius, _hits, Physics.AllLayers, QueryTriggerInteraction.Collide);
            }

            return count;
        }

        private static bool MoveToPlayer(Container chest, Inventory playerInventory, int missingWood, int missingStone)
        {
            Inventory chestInventory = chest.GetInventory();
            if (chestInventory == null)
            {
                return false;
            }

            int gotWood = 0;
            int gotStone = 0;
            int putWood = 0;
            int putStone = 0;
            try
            {
                gotWood = Take(chestInventory, ChestPay.Wood, missingWood);
                if (gotWood != missingWood)
                {
                    UndoPull(chest, chestInventory, playerInventory, gotWood, gotStone, putWood, putStone);
                    return false;
                }

                gotStone = Take(chestInventory, ChestPay.Stone, missingStone);
                if (gotStone != missingStone)
                {
                    UndoPull(chest, chestInventory, playerInventory, gotWood, gotStone, putWood, putStone);
                    return false;
                }

                putWood = Give(playerInventory, ChestPay.WoodPrefab, ChestPay.Wood, missingWood);
                putStone = Give(playerInventory, ChestPay.StonePrefab, ChestPay.Stone, missingStone);
                if (putWood != missingWood || putStone != missingStone)
                {
                    UndoPull(chest, chestInventory, playerInventory, gotWood, gotStone, putWood, putStone);
                    return false;
                }

                SaveChest(chest);
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.LogWarning("Chest pull failed, restoring items: " + ex.Message);
                UndoPull(chest, chestInventory, playerInventory, gotWood, gotStone, putWood, putStone);
                return false;
            }
        }

        // Items taken from the chest but never added to the player are returned too,
        // so only the amounts measured back out of the player count as player items.
        private static void UndoPull(Container chest, Inventory chestInventory, Inventory playerInventory, int gotWood, int gotStone, int putWood, int putStone)
        {
            int backWood = Take(playerInventory, ChestPay.Wood, putWood);
            int backStone = Take(playerInventory, ChestPay.Stone, putStone);
            Give(chestInventory, ChestPay.WoodPrefab, ChestPay.Wood, gotWood - putWood + backWood);
            Give(chestInventory, ChestPay.StonePrefab, ChestPay.Stone, gotStone - putStone + backStone);
            SaveChest(chest);
        }

        private static void ReturnToChest(Player player, Container chest, int wood, int stone)
        {
            if (player == null || chest == null || chest.GetInventory() == null || player.GetInventory() == null)
            {
                Plugin.LogWarning("Could not return pulled wood and stone.");
                return;
            }

            Inventory playerInventory = player.GetInventory();
            Inventory chestInventory = chest.GetInventory();
            int backWood = Take(playerInventory, ChestPay.Wood, wood);
            int backStone = Take(playerInventory, ChestPay.Stone, stone);
            int storedWood = Give(chestInventory, ChestPay.WoodPrefab, ChestPay.Wood, backWood);
            int storedStone = Give(chestInventory, ChestPay.StonePrefab, ChestPay.Stone, backStone);
            int leftWood = backWood - storedWood;
            int leftStone = backStone - storedStone;
            if (leftWood > 0)
            {
                Give(playerInventory, ChestPay.WoodPrefab, ChestPay.Wood, leftWood);
            }

            if (leftStone > 0)
            {
                Give(playerInventory, ChestPay.StonePrefab, ChestPay.Stone, leftStone);
            }

            SaveChest(chest);
            if (backWood != wood || backStone != stone || leftWood > 0 || leftStone > 0)
            {
                Plugin.LogWarning("Pulled wood or stone could not all return to the chest.");
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

        private static void SaveChest(Container container)
        {
            if (container != null && SaveMethod != null)
            {
                SaveMethod.Invoke(container, null);
            }
        }

        private static int Take(Inventory inventory, string sharedName, int amount)
        {
            if (inventory == null || amount <= 0)
            {
                return 0;
            }

            int before = inventory.CountItems(sharedName, -1, true);
            inventory.RemoveItem(sharedName, amount, -1, true);
            return before - inventory.CountItems(sharedName, -1, true);
        }

        private static int Give(Inventory inventory, string prefabName, string sharedName, int amount)
        {
            if (inventory == null || amount <= 0)
            {
                return 0;
            }

            int before = inventory.CountItems(sharedName, -1, true);
            inventory.AddItem(prefabName, amount, 1, 0, 0L, "", false, false);
            return inventory.CountItems(sharedName, -1, true) - before;
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
