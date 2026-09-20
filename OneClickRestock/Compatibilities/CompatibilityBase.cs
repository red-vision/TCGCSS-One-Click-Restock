
using System;
using System.Reflection;

namespace OneClickRestock {
    public abstract class CompatibilityBase
    {
        protected Assembly Assembly { get; }

        private CompatibilityBase ()
        {
            throw new NotImplementedException("Mandatory constructor.");
        }

        public CompatibilityBase(Assembly assembly)
        {
            Assembly = assembly;
        }

        public abstract RestockIndex GetRestockDataIndex(EItemType itemType, RestockOption option, ref RestockIndex result);

        public abstract bool IsModdedRestockData(EItemType itemType);


        public virtual bool GetIsItemLicenseUnlocked(RestockIndex result)
        {
            return CPlayerData.GetIsItemLicenseUnlocked(result.index);
        }

        public virtual bool IsRestockIndexInAnyPage(RestockIndex result)
        {
            return true;
        }

        public virtual bool IsRestockIndexInPage(RestockIndex result)
        {
            return true;
        }
    }
}