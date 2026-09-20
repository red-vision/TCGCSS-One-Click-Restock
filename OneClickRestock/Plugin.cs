using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

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
            return result;
        }
    }
}
