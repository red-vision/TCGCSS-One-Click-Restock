
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OneClickRestock {
    
    public class CompatibilityManager
    {
        private static Dictionary<RestockCompatibility, CompatibilityBase> compatibilities = new Dictionary<RestockCompatibility, CompatibilityBase>();
        private static Dictionary<RestockCompatibility, Assembly> compatibilityAssemblies = new Dictionary<RestockCompatibility, Assembly>();

        public static void Initialize() {
            Assembly assembly = Assembly.GetExecutingAssembly();

            foreach (RestockCompatibility compatibility in Settings.compatibilityTypes)
            {
                if (!Settings.IsCompatibilityActive(compatibility)) continue;
                Type type = assembly.GetType($"OneClickRestock.{compatibility}");

                try {
                    compatibilities[compatibility] = (CompatibilityBase)Activator.CreateInstance(type, new object[] { compatibilityAssemblies[compatibility] });
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError(e);
                }
            }
        }

        public static RestockIndex GetModdedRestockDataIndex(EItemType itemType, RestockOption option, ref RestockIndex result)
        {
            foreach (RestockCompatibility compatibility in Settings.compatibilityTypes)
            {
                if (!Settings.IsCompatibilityActive(compatibility)) continue;
                try {
                    if (!compatibilities[compatibility].IsModdedRestockData(itemType)) continue;
                    RestockIndex moddedResult = result;
                    moddedResult = compatibilities[compatibility].GetRestockDataIndex(itemType, option, ref moddedResult);
                    if (moddedResult.index != -1)
                    {
                        moddedResult.isLicensed = compatibilities[compatibility].GetIsItemLicenseUnlocked(moddedResult);
                        result = moddedResult;
                    }
                    return result;
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError(e);
                }
            }
            return result;
        }

        public static bool IsRestockIndexInAnyPage(RestockIndex result)
        {
            foreach (RestockCompatibility compatibility in Settings.compatibilityTypes)
            {
                if (!Settings.IsCompatibilityActive(compatibility)) continue;
                try 
                {
                    if (!compatibilities[compatibility].IsModdedRestockData(result.itemType)) continue;
                    return compatibilities[compatibility].IsRestockIndexInAnyPage(result);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError(e);
                }
            }
            return false;
        }

        public static bool IsRestockIndexInPage(RestockIndex result)
        {
            foreach (RestockCompatibility compatibility in Settings.compatibilityTypes)
            {
                if (!Settings.IsCompatibilityActive(compatibility)) continue;
                try
                {
                    if (!compatibilities[compatibility].IsModdedRestockData(result.itemType)) continue;
                    return compatibilities[compatibility].IsRestockIndexInPage(result);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError(e);
                }
            }
            return false;
        }

        public static void SetCompatibilityAssembly(RestockCompatibility compatibility, Assembly assembly)
        {
            if (!compatibilityAssemblies.ContainsKey(compatibility))
            {
                compatibilityAssemblies.Add(compatibility, assembly);
            }
            compatibilityAssemblies[compatibility] = assembly;
        }
    }
}
