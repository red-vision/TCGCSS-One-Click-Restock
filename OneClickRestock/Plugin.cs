using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace OneClickRestock
{
    public struct RestockIndex
    {
        public EItemType itemType;
        public int index;
        public bool isBigBox;
        public RestockIndex(bool b)
        {
            itemType = EItemType.None;
            index = -1;
            isBigBox = b;
        }
        public RestockIndex(int i, bool b, EItemType t)
        {
            itemType = t;
            index = i;
            isBigBox = b;
        }
        public override String ToString()
        {
            return $"{{ itemType: {itemType}, index: {index}, isBigBox: {isBigBox} }}";
        }
    }

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency( "EnhancedPrefabLoader", BepInDependency.DependencyFlags.SoftDependency)]
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

            var go = new GameObject("PluginBehaviour_" + MyPluginInfo.PLUGIN_NAME);
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            var comp = go.AddComponent<PluginBehaviour>();
            comp.enabled = true;

            Settings.Instance.load(this);

            chectCompatibilities();

            harmony = new Harmony(MyPluginInfo.PLUGIN_NAME);
            harmony.PatchAll();

            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void chectCompatibilities ()
        {

            foreach (RestockCompatibility compatibility in Settings.compatibilityTypes)
            {
                if (Chainloader.PluginInfos.TryGetValue(compatibility.ToString(), out var prefabLoader))
                {
                    Settings.activateCompatibility(compatibility);
                    CompatibilityManager.SetCompatibilityAssembly(compatibility, prefabLoader.Instance.GetType().Assembly);
                    Logger.LogInfo($"Activated mod compatibility for: {compatibility.ToString()} {prefabLoader.Metadata.Version}");
                }
            }

            CompatibilityManager.Initialize();
        }

        public static RestockIndex GetRestockDataIndex(EItemType itemType, RestockOption option)
        {
            RestockIndex result = new RestockIndex(option.Equals(RestockOption.PrioritizeBigBox));
            result.itemType = itemType;
            if (option.Equals(RestockOption.DoNotRestock)) return result;
            for (int i = 0; i < InventoryBase.Instance.m_StockItemData_SO.m_RestockDataList.Count; i++)
            {
                if (InventoryBase.Instance.m_StockItemData_SO.m_RestockDataList[i].itemType == itemType
                    && CPlayerData.GetIsItemLicenseUnlocked(i)
                )
                {
                    result.index = i;
                    result.isBigBox = InventoryBase.Instance.m_StockItemData_SO.m_RestockDataList[i].isBigBox;
                    if (option.Equals(RestockOption.PrioritizeSmallBox) && result.isBigBox) continue;
                    if (option.Equals(RestockOption.PrioritizeBigBox) && !result.isBigBox) continue;
                    return result;
                }
            }

            return CompatibilityManager.GetModdedRestockDataIndex(itemType, option, ref result);
        }
    }
}
