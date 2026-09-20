using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace OneClickRestock
{
    public struct RestockIndex {
        public EItemType itemType;
        public int index;
        public bool isBigBox;
        public RestockIndex(bool b){
            itemType = EItemType.None;
            index = -1;
            isBigBox = b;
        }
        public RestockIndex(int i, bool b, EItemType t){
            itemType = t;
            index = i;
            isBigBox = b;
        }
    }

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static Plugin instance;
        internal static new ManualLogSource Logger;
        private static Harmony harmony;

        private void Awake()
        {
            instance = this;
            // Plugin startup logic
            Logger = base.Logger;

            Settings.Instance.load(this);

            harmony = new Harmony(MyPluginInfo.PLUGIN_NAME);
            harmony.PatchAll();

            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }

        public static RestockIndex GetRestockDataIndex(EItemType itemType, RestockOption option) {
            RestockIndex result = new RestockIndex(option.Equals(RestockOption.PrioritizeBigBox));
            result.itemType = itemType;
            if (option.Equals(RestockOption.DoNotRestock)) return result;
            for (int i = 0; i < InventoryBase.Instance.m_StockItemData_SO.m_RestockDataList.Count; i++) {
                if (InventoryBase.Instance.m_StockItemData_SO.m_RestockDataList[i].itemType == itemType
                    && CPlayerData.GetIsItemLicenseUnlocked(i)
                ) {
                    result.index = i;
                    result.isBigBox = InventoryBase.Instance.m_StockItemData_SO.m_RestockDataList[i].isBigBox;
                    if (option.Equals(RestockOption.PrioritizeSmallBox) && result.isBigBox) continue;
                    if (option.Equals(RestockOption.PrioritizeBigBox) && !result.isBigBox) continue;
                    return result;
                }
            }
            return result;
        }
    }

    [HarmonyPatch]
    class HarmonyPatches
    {
        private static Dictionary<EItemType, List<int>> zeroed() => Enum
            .GetValues(typeof(EItemType))
            .Cast<EItemType>()
            .ToDictionary(e => e, e => new List<int>());

        private static Dictionary<EItemType, int> zeroedInt() => Enum
            .GetValues(typeof(EItemType))
            .Cast<EItemType>()
            .ToDictionary(e => e, e => 0);
        
        [HarmonyPatch(typeof(CGameManager), "Update")]
        [HarmonyPostfix]
        public static void GameManagerUpdatePostfix()
        {
            KeyboardShortcut value = Settings.oneClickRestockKey.Value;
            
            if (!value.IsDown() && !InputManager.GetKeyDownAction(EGameAction.OpenCardAlbum)) 
                return;

            RestockItemScreenWrapper screen = new RestockItemScreenWrapper();
            if (!screen.IsScreenOpen()) 
                return;

            if(Settings.warehouseShelves.Value.Equals(RestockOption.DoNotRestock) 
                && Settings.shoppingShelves.Value.Equals(RestockOption.DoNotRestock)
            ) 
                return;

            bool doCheckIndex = !Settings.restockBehavior.Value.Equals(RestockBehavior.AllWindows);
            bool checkIndexInWindow = Settings.restockBehavior.Value.Equals(RestockBehavior.CurrentWindow);
            bool checkIndexInTab = Settings.restockBehavior.Value.Equals(RestockBehavior.CurrentTab);

            Dictionary<bool, Dictionary<EItemType, List<int>>> storedBoxes = new Dictionary<bool,Dictionary<EItemType, List<int>>>();
            Dictionary<bool,Dictionary<EItemType, List<int>>> droppedBoxes = new Dictionary<bool,Dictionary<EItemType, List<int>>>();
            storedBoxes[true] = new Dictionary<EItemType, List<int>>(zeroed());
            storedBoxes[false] = new Dictionary<EItemType, List<int>>(zeroed());

            droppedBoxes[true] = new Dictionary<EItemType, List<int>>(zeroed());
            droppedBoxes[false] = new Dictionary<EItemType, List<int>>(zeroed());

            // Existing boxes
            foreach(var box in RestockManager.GetItemPackagingBoxListWithItem(true)) {
                if(box.m_ItemCompartment.GetItemCount() <= 0) continue;
                if(!Settings.checkAllPackages.Value && !box.m_IsStored) {
                    continue;
                } else if (Settings.checkAllPackages.Value && !box.m_IsStored) {
                    droppedBoxes[box.m_IsBigBox][box.GetItemType()].Add(box.m_ItemCompartment.GetMaxItemCount());
                } else if(box.m_IsStored){
                    storedBoxes[box.m_IsBigBox][box.GetItemType()].Add(box.m_ItemCompartment.GetMaxItemCount());
                }
            }

            // string tolog = string.Join(", ", boxes.Select(outer => $"{outer.Key}: {{ {string.Join(", ", outer.Value.Select(inner => $"{inner.Key}: [{string.Join(", ", inner.Value)}]"))} }}"));
            // Plugin.Logger.LogInfo(tolog);
            // string dictString = string.Join(", ", boxes.Select(kvp => 
            //     $"{{\"{kvp.Key}\": {{{string.Join(", ", kvp.Value.Select(innerKvp => $"\"{innerKvp.Key}\": [\"{string.Join("\", \"", innerKvp.Value)}\"]"))}}}}}"
            // ));
            // Plugin.Logger.LogInfo(dictString);

            Dictionary<bool,Dictionary<EItemType, int>> warehouse = null;

            // Existing warehouse units
            if (!Settings.warehouseShelves.Value.Equals(RestockOption.DoNotRestock)) {
                warehouse = new Dictionary<bool,Dictionary<EItemType, int>>();
                warehouse[true] = new Dictionary<EItemType, int>(zeroedInt());
                warehouse[false] = new Dictionary<EItemType, int>(zeroedInt());
                foreach (WarehouseShelf shelf in ShelfManager.Instance.m_WarehouseShelfList) {
                    if (shelf && shelf.IsValidObject() && !shelf.m_ObjectType.ToString().StartsWith("Personal")) {
                        foreach (InteractableStorageCompartment isc in shelf.GetStorageCompartmentList()) {
                            RestockOption expectedSize = Settings.warehouseShelves.Value;
                            int expectedCount = expectedSize.Equals(RestockOption.PrioritizeBigBox) ? 2 : 4;
                            bool shelfIsBigBox = new ShelfCompartmentWrapper(isc.GetShelfCompartment()).IsBigBox();
                            if(isc.GetShelfCompartment().GetItemCount() > 0) {
                                expectedCount = shelfIsBigBox ? 2 : 4;
                                expectedSize = expectedCount == 2 ? RestockOption.PrioritizeBigBox : RestockOption.PrioritizeSmallBox;
                            }
                            RestockIndex restockIndex = Plugin.GetRestockDataIndex(isc.GetShelfCompartment().GetItemType(), expectedSize);
                            expectedCount = restockIndex.isBigBox ? 2 : 4;
                            expectedSize = restockIndex.isBigBox ? RestockOption.PrioritizeBigBox : RestockOption.PrioritizeSmallBox;
                            warehouse[restockIndex.isBigBox][restockIndex.itemType] += expectedCount;// - isc.GetShelfCompartment().GetItemCount();
                        }
                    }
                }
            }

            // string whlog = "{" + string.Join(", ", warehouse.Select(outer => $"\"{outer.Key}\": {{ {string.Join(", ", outer.Value.Select(inner => $"\"{inner.Key}\": {inner.Value}"))} }}")) + "}";
            // Plugin.Logger.LogInfo(whlog);


            Dictionary<EItemType, int> shopping;

            
            // string dictString = string.Join(", ", boxes.Select(kvp => 
            //     $"{{\"{kvp.Key}\": {{{string.Join(", ", kvp.Value.Select(innerKvp => $"\"{innerKvp.Key}\": [\"{string.Join("\", \"", innerKvp.Value)}\"]"))}}}}}"
            // ));
            // Plugin.Logger.LogInfo(dictString);
            
            if (!Settings.shoppingShelves.Value.Equals(RestockOption.DoNotRestock)) {
                shopping = new Dictionary<EItemType, int>(zeroedInt());
                foreach (Shelf shelf in ShelfManager.Instance.m_ShelfList) {
                    if (shelf && shelf.IsValidObject() && !shelf.m_ObjectType.ToString().StartsWith("Personal")) {
                        foreach (ShelfCompartment isc in shelf.GetItemCompartmentList()) {
                            shopping[isc.GetItemType()] += isc.GetMaxItemCount() - isc.GetItemCount();
                        }
                    }
                }

                foreach (KeyValuePair<EItemType, List<int>> entry in droppedBoxes[true]) {
                    RestockIndex restockIndex = Plugin.GetRestockDataIndex(entry.Key, RestockOption.PrioritizeBigBox);
                    if(restockIndex.index == -1) continue;
                    if(!restockIndex.isBigBox) continue;
                    for(int i = 0; i < droppedBoxes[true][entry.Key].Count; i++) {
                        if(shopping[entry.Key] <= 0) break;
                        shopping[entry.Key] -= droppedBoxes[true][entry.Key][i];
                        droppedBoxes[true][entry.Key].RemoveAt(i);
                        i--;
                    }
                }

                foreach (KeyValuePair<EItemType, List<int>> entry in droppedBoxes[false]) {
                    RestockIndex restockIndex = Plugin.GetRestockDataIndex(entry.Key, RestockOption.PrioritizeSmallBox);
                    if(restockIndex.index == -1) continue;
                    if(restockIndex.isBigBox) continue;
                    for(int i = 0; i < droppedBoxes[false][entry.Key].Count; i++) {
                        if(shopping[entry.Key] <= 0) break;
                        shopping[entry.Key] -= droppedBoxes[false][entry.Key][i];
                        droppedBoxes[false][entry.Key].RemoveAt(i);
                        i--;
                    }
                }

                // string dictString2 = string.Join(", ", shopping.Select(kvp => 
                //     $"\"{kvp.Key}\": [\"{kvp.Value}\"]")
                // );
                // Plugin.Logger.LogInfo(dictString2);

                foreach (KeyValuePair<EItemType, int> entry in shopping) {
                    try {
                        if(entry.Value <= 0) continue;
                        // RestockIndex restockIndex = Plugin.GetRestockDataIndex(entry.Key, Settings.shoppingShelves.Value);
                        RestockIndex restockIndexBig = Plugin.GetRestockDataIndex(entry.Key, RestockOption.PrioritizeBigBox);
                        RestockIndex restockIndexSmall = Plugin.GetRestockDataIndex(entry.Key, RestockOption.PrioritizeSmallBox);
                        
                        if(restockIndexBig.index == -1 && restockIndexSmall.index == -1) continue;

                        int perBoxBig = RestockManager.GetMaxItemCountInBox(restockIndexBig.itemType, restockIndexBig.isBigBox);
                        int perBoxSmall = RestockManager.GetMaxItemCountInBox(restockIndexSmall.itemType, restockIndexSmall.isBigBox);

                        if(perBoxBig <= 0 && perBoxSmall <= 0) continue;
                        if(perBoxSmall > entry.Value) continue;

                        RestockIndex restockIndex;
                        int perBox;
                        if(perBoxBig > entry.Value) {
                            restockIndex = restockIndexSmall;
                            perBox = perBoxSmall;
                        } else {
                            if(Settings.shoppingShelves.Value.Equals(RestockOption.PrioritizeBigBox)) {
                                restockIndex = restockIndexBig;
                                perBox = perBoxBig;
                            } else {
                                restockIndex = restockIndexSmall;
                                perBox = perBoxSmall;
                            }
                        }
                        
                        int expectedCount = entry.Value / perBox;
                        if(expectedCount <= 0) continue;
                        if (doCheckIndex)
                        {
                            if (checkIndexInWindow && !screen.IsRestockIndexInAnyPage(restockIndex.index)) continue;
                            if (checkIndexInTab && !screen.IsRestockIndexInPage(restockIndex.index)) continue;
                        }
                        for(int i = 0; i < expectedCount; i++) {
                            if(!screen.HasEnoughCartSlot()) break;
                            screen.AddToCartForCheckout(restockIndex.index, 1);
                        }
                    } catch (Exception) { }
                }
            }

            // string dictString2 = string.Join(", ", boxes.Select(kvp => 
            //     $"{{\"{kvp.Key}\": {{{string.Join(", ", kvp.Value.Select(innerKvp => $"\"{innerKvp.Key}\": [\"{string.Join("\", \"", innerKvp.Value)}\"]"))}}}}}"
            // ));
            // Plugin.Logger.LogInfo(dictString2);

            if (!Settings.warehouseShelves.Value.Equals(RestockOption.DoNotRestock)) {
                Dictionary<EItemType, int> copyBig = new Dictionary<EItemType, int>(warehouse[true].ToDictionary(e=>e.Key,e=>e.Value));
                Dictionary<EItemType, int> copySmall = new Dictionary<EItemType, int>(warehouse[false].ToDictionary(e=>e.Key,e=>e.Value));
                
                foreach (KeyValuePair<EItemType, int> entry in copyBig) {
                    // Plugin.Logger.LogInfo("------");
                    // Plugin.Logger.LogInfo(entry.Key);
                    RestockIndex restockIndex = Plugin.GetRestockDataIndex(entry.Key, RestockOption.PrioritizeBigBox);
                    if(restockIndex.index == -1) continue;
                    if(!restockIndex.isBigBox) continue;
                    if (doCheckIndex)
                    {
                        if (checkIndexInWindow && !screen.IsRestockIndexInAnyPage(restockIndex.index)) continue;
                        if (checkIndexInTab && !screen.IsRestockIndexInPage(restockIndex.index)) continue;
                    }
                    // Plugin.Logger.LogInfo(111);
                    for(int i = 0; i < storedBoxes[true][entry.Key].Count; i++) {
                        // Plugin.Logger.LogInfo(222);
                        if(warehouse[true][entry.Key] <= 0) break;
                        // Plugin.Logger.LogInfo(333);
                        warehouse[true][entry.Key]--;
                        storedBoxes[true][entry.Key].RemoveAt(i);
                        i--;
                    }
                    for(int i = 0; i < droppedBoxes[true][entry.Key].Count; i++) {
                        // Plugin.Logger.LogInfo(222);
                        if(warehouse[true][entry.Key] <= 0) break;
                        // Plugin.Logger.LogInfo(333);
                        warehouse[true][entry.Key]--;
                        droppedBoxes[true][entry.Key].RemoveAt(i);
                        i--;
                    }
                    // Plugin.Logger.LogInfo(444);
                    for(int i = 0; i < warehouse[true][entry.Key]; i++) {
                        // Plugin.Logger.LogInfo(555);
                        if(!screen.HasEnoughCartSlot()) break;
                        screen.AddToCartForCheckout(restockIndex.index, 1);
                    }
                }
                // Plugin.Logger.LogInfo(666777888);
                foreach (KeyValuePair<EItemType, int> entry in copySmall) {
                    // Plugin.Logger.LogInfo("------");
                    // Plugin.Logger.LogInfo(entry.Key);
                    RestockIndex restockIndex = Plugin.GetRestockDataIndex(entry.Key, RestockOption.PrioritizeSmallBox);
                    if(restockIndex.index == -1) continue;
                    if(restockIndex.isBigBox) continue;
                    if (doCheckIndex)
                    {
                        if (checkIndexInWindow && !screen.IsRestockIndexInAnyPage(restockIndex.index)) continue;
                        if (checkIndexInTab && !screen.IsRestockIndexInPage(restockIndex.index)) continue;
                    }
                    // Plugin.Logger.LogInfo(111);
                    for(int i = 0; i < storedBoxes[false][entry.Key].Count; i++) {
                        // Plugin.Logger.LogInfo(222);
                        if(warehouse[false][entry.Key] <= 0) break;
                        // Plugin.Logger.LogInfo(333);
                        warehouse[false][entry.Key]--;
                        storedBoxes[false][entry.Key].RemoveAt(i);
                        i--;
                    }
                    for(int i = 0; i < droppedBoxes[false][entry.Key].Count; i++) {
                        // Plugin.Logger.LogInfo(222);
                        if(warehouse[false][entry.Key] <= 0) break;
                        // Plugin.Logger.LogInfo(333);
                        warehouse[false][entry.Key]--;
                        droppedBoxes[false][entry.Key].RemoveAt(i);
                        i--;
                    }
                    // Plugin.Logger.LogInfo(444);
                    for(int i = 0; i < warehouse[false][entry.Key]; i++) {
                        // Plugin.Logger.LogInfo(555);
                        if(!screen.HasEnoughCartSlot()) break;
                        screen.AddToCartForCheckout(restockIndex.index, 1);
                    }
                }
            }
        }
    }
}
