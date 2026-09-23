
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneClickRestock
{

    public class PluginBehaviour : MonoBehaviour
    {
        
        // ---------- NEW: helpers to be tolerant to unknown enum values ----------
        private static List<int> GetOrAddList(Dictionary<int, List<int>> dict, int key)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<int>();
                dict[key] = list;
            }
            return list;
        }

        private static void AddToCount(Dictionary<int, int> dict, int key, int delta)
        {
            if (dict.TryGetValue(key, out var cur))
                dict[key] = cur + delta;
            else
                dict[key] = delta;
        }

        private static void EnsureKey(Dictionary<int, int> dict, int key)
        {
            if (!dict.ContainsKey(key)) dict[key] = 0;
        }

        // ---------- REPLACED: no more Enum.GetValues seeding ----------
        private static Dictionary<int, List<int>> NewBoxDict() => new Dictionary<int, List<int>>();
        private static Dictionary<int, int> NewCountDict() => new Dictionary<int, int>();
        
        private void Awake()
        {

        }

        private void Update()
        {
            var value = Settings.oneClickRestockKey.Value;

            if (!value.IsDown() && !InputManager.GetKeyDownAction(EGameAction.OpenCardAlbum))
                return;

            var screen = new RestockItemScreenWrapper();
            if (!screen.IsScreenOpen())
                return;

            if (Settings.warehouseShelves.Value.Equals(RestockOption.DoNotRestock)
                && Settings.shoppingShelves.Value.Equals(RestockOption.DoNotRestock))
                return;

            bool doCheckIndex      = !Settings.restockBehavior.Value.Equals(RestockBehavior.AllWindows);
            bool checkIndexInWindow=  Settings.restockBehavior.Value.Equals(RestockBehavior.CurrentWindow);
            bool checkIndexInTab   =  Settings.restockBehavior.Value.Equals(RestockBehavior.CurrentTab);

            // Use int keys so we survive unknown EItemType values from other mods.
            var storedBoxes  = new Dictionary<bool, Dictionary<int, List<int>>>
            {
                [true]  = NewBoxDict(),
                [false] = NewBoxDict()
            };
            var droppedBoxes = new Dictionary<bool, Dictionary<int, List<int>>>
            {
                [true]  = NewBoxDict(),
                [false] = NewBoxDict()
            };

            // Existing boxes 
            // (if GetItemPackagingBoxListWithItem doesnt work anymore, try if GetPackageBoxCandidateList exists)
            foreach (var box in RestockManager.GetItemPackagingBoxListWithItem(true))
            {
                if (box.m_ItemCompartment.GetItemCount() <= 0) continue;

                int key = (int)box.GetItemType();

                if (!Settings.checkAllPackages.Value && !box.m_IsStored)
                {
                    continue;
                }
                else if (Settings.checkAllPackages.Value && !box.m_IsStored)
                {
                    GetOrAddList(droppedBoxes[box.m_IsBigBox], key)
                        .Add(box.m_ItemCompartment.GetMaxItemCount());
                }
                else if (box.m_IsStored)
                {
                    GetOrAddList(storedBoxes[box.m_IsBigBox], key)
                        .Add(box.m_ItemCompartment.GetMaxItemCount());
                }
            }

            // Warehouse plan (expected counts per type/size)
            Dictionary<bool, Dictionary<int, int>> warehouse = null;

            if (!Settings.warehouseShelves.Value.Equals(RestockOption.DoNotRestock))
            {
                warehouse = new Dictionary<bool, Dictionary<int, int>>
                {
                    [true]  = NewCountDict(),
                    [false] = NewCountDict()
                };

                foreach (var shelf in ShelfManager.Instance.m_WarehouseShelfList)
                {
                    if (shelf && shelf.IsValidObject() && !shelf.m_ObjectType.ToString().StartsWith("Personal"))
                    {
                        foreach (var isc in shelf.GetStorageCompartmentList())
                        {
                            var expectedSize = Settings.warehouseShelves.Value;
                            int expectedCount = expectedSize.Equals(RestockOption.PrioritizeBigBox) ? 2 : 4;

                            var sc = isc.GetShelfCompartment();
                            bool shelfIsBigBox = new ShelfCompartmentWrapper(sc).IsBigBox();

                            if (sc.GetItemCount() > 0)
                            {
                                expectedCount = shelfIsBigBox ? 2 : 4;
                                expectedSize  = expectedCount == 2 ? RestockOption.PrioritizeBigBox : RestockOption.PrioritizeSmallBox;
                            }

                            var type = sc.GetItemType();
                            var restockIndex = Plugin.GetRestockDataIndex(type, expectedSize);

                            expectedCount = restockIndex.isBigBox ? 2 : 4;
                            AddToCount(warehouse[restockIndex.isBigBox], (int)restockIndex.itemType, expectedCount);
                        }
                    }
                }
            }

            // Shopping deficits per item type
            if (!Settings.shoppingShelves.Value.Equals(RestockOption.DoNotRestock))
            {
                var shopping = NewCountDict();

                foreach (var shelf in ShelfManager.Instance.m_ShelfList)
                {
                    if (shelf && shelf.IsValidObject() && !shelf.m_ObjectType.ToString().StartsWith("Personal"))
                    {
                        foreach (var isc in shelf.GetItemCompartmentList())
                        {
                            int key = (int)isc.GetItemType();
                            int deficit = isc.GetMaxItemCount() - isc.GetItemCount();
                            if (deficit > 0) AddToCount(shopping, key, deficit);
                            else EnsureKey(shopping, key); // ensure presence
                        }
                    }
                }

                // First “pay off” shopping deficit using already dropped/stored boxes on the ground (if allowed)
                foreach (var kv in droppedBoxes[true].ToList())
                {
                    int key = kv.Key;
                    var restockIndex = Plugin.GetRestockDataIndex((EItemType)key, RestockOption.PrioritizeBigBox);
                    if (restockIndex.index == -1 || !restockIndex.isBigBox) continue;
                    if (!shopping.TryGetValue(key, out var needed) || needed <= 0) continue;

                    var list = droppedBoxes[true][key];
                    for (int i = 0; i < list.Count && shopping[key] > 0; i++)
                    {
                        shopping[key] -= list[i];
                        list.RemoveAt(i);
                        i--;
                    }
                }

                foreach (var kv in droppedBoxes[false].ToList())
                {
                    int key = kv.Key;
                    var restockIndex = Plugin.GetRestockDataIndex((EItemType)key, RestockOption.PrioritizeSmallBox);
                    if (restockIndex.index == -1 || restockIndex.isBigBox) continue;
                    if (!shopping.TryGetValue(key, out var needed) || needed <= 0) continue;

                    var list = droppedBoxes[false][key];
                    for (int i = 0; i < list.Count && shopping[key] > 0; i++)
                    {
                        shopping[key] -= list[i];
                        list.RemoveAt(i);
                        i--;
                    }
                }

                // Now order new boxes to fill remaining deficit
                foreach (var entry in shopping.ToList())
                {
                    int key = entry.Key;
                    int needed = entry.Value;
                    if (needed <= 0) continue;

                    var restockIndexBig   = Plugin.GetRestockDataIndex((EItemType)key, RestockOption.PrioritizeBigBox);
                    var restockIndexSmall = Plugin.GetRestockDataIndex((EItemType)key, RestockOption.PrioritizeSmallBox);

                    bool canBuyBig = restockIndexBig.index != -1 && restockIndexBig.isBigBox && restockIndexBig.isLicensed;
                    bool canBuySmall = restockIndexSmall.index != -1 && !restockIndexSmall.isBigBox && restockIndexSmall.isLicensed;

                    int perBoxBig = canBuyBig
                        ? RestockManager.GetMaxItemCountInBox(restockIndexBig.itemType, restockIndexBig.isBigBox) : 0;
                    int perBoxSmall = canBuySmall
                        ? RestockManager.GetMaxItemCountInBox(restockIndexSmall.itemType, restockIndexSmall.isBigBox) : 0;

                    if (perBoxBig <= 0 && perBoxSmall <= 0) continue;

                    RestockIndex restockIndex;
                    int perBox;

                    if (perBoxBig <= 0 || perBoxBig > needed)
                    {
                        if (perBoxSmall <= 0 || perBoxSmall > needed) continue;
                        restockIndex = restockIndexSmall;
                        perBox = perBoxSmall;
                    }
                    else if (perBoxSmall <= 0)
                    {
                        restockIndex = restockIndexBig;
                        perBox = perBoxBig;
                    }
                    else
                    {
                        if (Settings.shoppingShelves.Value.Equals(RestockOption.PrioritizeBigBox))
                        {
                            restockIndex = restockIndexBig;
                            perBox = perBoxBig;
                        }
                        else
                        {
                            restockIndex = restockIndexSmall;
                            perBox = perBoxSmall;
                        }
                    }

                    int boxesToBuy = perBox > 0 ? needed / perBox : 0;
                    if (boxesToBuy <= 0) continue;

                    if (doCheckIndex)
                    {
                        if (checkIndexInWindow && !screen.IsRestockIndexInAnyPage(restockIndex.index)
                            && !CompatibilityManager.IsRestockIndexInAnyPage(restockIndex)) continue;
                        if (checkIndexInTab    && !screen.IsRestockIndexInPage(restockIndex.index)
                            && !CompatibilityManager.IsRestockIndexInPage(restockIndex))   continue;
                    }

                    for (int i = 0; i < boxesToBuy; i++)
                    {
                        if (!screen.HasEnoughCartSlot()) break;
                        screen.AddToCartForCheckout(restockIndex.index, 1);
                    }
                }
            }

            // Warehouse refills
            if (!Settings.warehouseShelves.Value.Equals(RestockOption.DoNotRestock))
            {
                var copyBig   = warehouse[true].ToDictionary(kv => kv.Key,   kv => kv.Value);
                var copySmall = warehouse[false].ToDictionary(kv => kv.Key,  kv => kv.Value);

                foreach (var entry in copyBig)
                {
                    int key = entry.Key;
                    var restockIndex = Plugin.GetRestockDataIndex((EItemType)key, RestockOption.PrioritizeBigBox);
                    if (restockIndex.index == -1 || !restockIndex.isBigBox || !restockIndex.isLicensed) continue;

                    if (doCheckIndex)
                    {
                        if (checkIndexInWindow && !screen.IsRestockIndexInAnyPage(restockIndex.index)
                            && !CompatibilityManager.IsRestockIndexInAnyPage(restockIndex)) continue;
                        if (checkIndexInTab    && !screen.IsRestockIndexInPage(restockIndex.index)
                            && !CompatibilityManager.IsRestockIndexInPage(restockIndex))   continue;
                    }

                    // Use boxes already stored/dropped first
                    if (storedBoxes[true].TryGetValue(key, out var storedList))
                    {
                        for (int i = 0; i < storedList.Count && warehouse[true][key] > 0; i++)
                        {
                            warehouse[true][key]--;
                            storedList.RemoveAt(i);
                            i--;
                        }
                    }
                    if (droppedBoxes[true].TryGetValue(key, out var droppedList))
                    {
                        for (int i = 0; i < droppedList.Count && warehouse[true][key] > 0; i++)
                        {
                            warehouse[true][key]--;
                            droppedList.RemoveAt(i);
                            i--;
                        }
                    }

                    // Order remaining
                    for (int i = 0; i < warehouse[true][key]; i++)
                    {
                        if (!screen.HasEnoughCartSlot()) break;
                        screen.AddToCartForCheckout(restockIndex.index, 1);
                    }
                }

                foreach (var entry in copySmall)
                {
                    int key = entry.Key;
                    var restockIndex = Plugin.GetRestockDataIndex((EItemType)key, RestockOption.PrioritizeSmallBox);
                    if (restockIndex.index == -1 || restockIndex.isBigBox || !restockIndex.isLicensed) continue;

                    if (doCheckIndex)
                    {
                        if (checkIndexInWindow && !screen.IsRestockIndexInAnyPage(restockIndex.index)
                            && !CompatibilityManager.IsRestockIndexInAnyPage(restockIndex)) continue;
                        if (checkIndexInTab    && !screen.IsRestockIndexInPage(restockIndex.index)
                            && !CompatibilityManager.IsRestockIndexInPage(restockIndex))   continue;
                    }

                    if (storedBoxes[false].TryGetValue(key, out var storedList))
                    {
                        for (int i = 0; i < storedList.Count && warehouse[false][key] > 0; i++)
                        {
                            warehouse[false][key]--;
                            storedList.RemoveAt(i);
                            i--;
                        }
                    }
                    if (droppedBoxes[false].TryGetValue(key, out var droppedList))
                    {
                        for (int i = 0; i < droppedList.Count && warehouse[false][key] > 0; i++)
                        {
                            warehouse[false][key]--;
                            droppedList.RemoveAt(i);
                            i--;
                        }
                    }

                    for (int i = 0; i < warehouse[false][key]; i++)
                    {
                        if (!screen.HasEnoughCartSlot()) break;
                        screen.AddToCartForCheckout(restockIndex.index, 1);
                    }
                }
            }
        }
    }
}
