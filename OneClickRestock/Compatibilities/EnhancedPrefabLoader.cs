extern alias CoreModule;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CoreModule::EnhancedPrefabLoader.API;
using CoreModule::EnhancedPrefabLoader.API.Models;
using System.Linq;
using HarmonyLib;

namespace OneClickRestock {
    public class EnhancedPrefabLoader : CompatibilityBase
    {
        private Type eplRuntimeDataType;
        private Type EnhancedPrefabLoaderRestockData;
        private PropertyInfo propertyAsset;
        private PropertyInfo propertyItemLibrary;
        private PropertyInfo propertyRestockEntries;
        private PropertyInfo propertyRestockData;
        private IList entries;


        private readonly List<UnityEngine.Object> screens = new();
        private readonly Type customShopScreenType;
        private readonly FieldInfo fieldPageItems;
        private readonly FieldInfo fieldCurrentPageIndex;
        private readonly PropertyInfo propertyIsOpen;

        public EnhancedPrefabLoader (Assembly assembly) : base(assembly)
        {
            if (!Epl.IsAvailable)
                return;

            // eplRuntimeDataType
            eplRuntimeDataType = assembly.GetTypes().FirstOrDefault(t => t.Name == "EplRuntimeData");

            propertyAsset = AccessTools.Property(eplRuntimeDataType, "Assets");
            var assets = propertyAsset?.GetValue(null);

            propertyItemLibrary = AccessTools.Property(assets?.GetType(), "ItemLibrary");
            var itemLibrary = propertyItemLibrary?.GetValue(assets);

            propertyRestockEntries = AccessTools.Property(itemLibrary?.GetType(), "RestockEntries");
            entries = propertyRestockEntries?.GetValue(itemLibrary) as IList;

            // EnhancedPrefabLoaderRestockData
            EnhancedPrefabLoaderRestockData = assembly.GetTypes().FirstOrDefault(t => t.Name == "ModRestockData");
            propertyRestockData = AccessTools.Property(EnhancedPrefabLoaderRestockData, "RestockData");

            // customShopScreenType
            customShopScreenType = assembly.GetTypes().FirstOrDefault(t => t.Name == "CustomShopScreen");

            fieldPageItems = AccessTools.Field(customShopScreenType, "pageItems");
            fieldCurrentPageIndex = AccessTools.Field(customShopScreenType, "currentPageIndex");
            propertyIsOpen = AccessTools.Property(customShopScreenType, "IsOpen");
        }

        public override  bool IsModdedRestockData(EItemType itemType)
        {
            if (!Epl.IsAvailable)
                return false;

            return Epl.Api.BundleRegistry.IsEplAsset(itemType);
        }

        public override RestockIndex GetRestockDataIndex(EItemType itemType, RestockOption option, ref RestockIndex result)
        {
            if (option.Equals(RestockOption.DoNotRestock)) return result;

            if (!Epl.IsAvailable)
                return result;
                
            ItemShopInfo shopInfo = Epl.Api.Assets.Shops.GetShopInfoFor(itemType);
            if (shopInfo == null)
                return result;
            
            RestockData restockData;

            if (option.Equals(RestockOption.PrioritizeBigBox) && shopInfo.BigBox != null)
            {
                result.isBigBox = true;
                restockData = shopInfo.BigBox;
            }
            else if (option.Equals(RestockOption.PrioritizeSmallBox) && shopInfo.SmallBox != null)
            {
                result.isBigBox = false;
                restockData = shopInfo.SmallBox;
            }
            else if (shopInfo.BigBox != null || shopInfo.SmallBox != null)
            {
                result.isBigBox = shopInfo.BigBox != null;
                restockData = option.Equals(RestockOption.PrioritizeBigBox) ? 
                    shopInfo.BigBox ?? shopInfo.SmallBox : shopInfo.SmallBox ?? shopInfo.BigBox;;
            }
            else
            {
                return result;
            }
            
            result.index = GetEplRestockIndex(restockData);
            
            return result;
        }

        private int GetEplRestockIndex(RestockData restockData)
        {
            if (!Epl.IsAvailable)
                return -1;

            if (entries == null) return -1;
            if (propertyRestockData == null) return -1;
            if (restockData == null) return -1;

            List<RestockData> vanillaRestockList = InventoryBase.Instance.m_StockItemData_SO.m_RestockDataList;

            for (int i = 0; i < entries.Count; i++)
            {
                object entry = entries[i];

                RestockData data = propertyRestockData.GetValue(entry) as RestockData;

                if (ReferenceEquals(data, restockData))
                {
                    return vanillaRestockList.Count + i;
                }
            }

            return -1;
        }

        
        private void RefreshScreens()
        {
            screens.Clear();
            var found = UnityEngine.Object.FindObjectsOfType(customShopScreenType);
            if (found != null && found.Length > 0)
                screens.AddRange(found.Where(o => o != null));

        }

        private UnityEngine.Object GetActiveInstanceOrNull()
        {
            if (!screens.Any()) RefreshScreens();
            // Prefer already-open instance; if none, try a quick rescan once
            var active = screens.FirstOrDefault(IsOpen);
            if (active != null) return active;

            RefreshScreens();
            return screens.FirstOrDefault(IsOpen);
        }

        private bool IsOpen(UnityEngine.Object obj)
        {
            try
            {
                return obj != null && (bool)propertyIsOpen.GetValue(obj);
            }
            catch { return false; }
        }

        public override bool IsRestockIndexInAnyPage(RestockIndex result)
        {
            RefreshScreens();
            var customShopScreen = GetActiveCustomShopScreen();
            if (customShopScreen == null) return false;

            var pageItems = (List<List<RestockData>>)fieldPageItems.GetValue(customShopScreen);
            if (pageItems == null) return false;

            return pageItems.Any(page => page.Any(restockData => restockData.itemType == result.itemType));
        }

        public override bool IsRestockIndexInPage(RestockIndex result)
        {
            RefreshScreens();
            var customShopScreen = GetActiveCustomShopScreen();
            if (customShopScreen == null) return false;

            var pageItems = (List<List<RestockData>>)fieldPageItems.GetValue(customShopScreen);
            var currentPageIndex = (int)fieldCurrentPageIndex.GetValue(customShopScreen);

            if (pageItems == null || currentPageIndex < 0 || currentPageIndex >= pageItems.Count) return false;

            return pageItems[currentPageIndex].Any(restockData => restockData.itemType == result.itemType);
        }

        private UnityEngine.Object GetActiveCustomShopScreen()
        {
            var screens = UnityEngine.Object.FindObjectsOfType(customShopScreenType);

            foreach (var screen in screens)
            {
                if (screen != null && (bool)propertyIsOpen.GetValue(screen))
                    return screen;
            }

            return null;
        }
    }
}