using System;
using System.Reflection;
using HarmonyLib;

namespace OneClickRestock
{
    internal class ShelfCompartmentWrapper{
        private ShelfCompartment instance;

        private FieldInfo fieldm_IsBigBox;

        internal ShelfCompartmentWrapper(ShelfCompartment shelfCompartment) {
            this.instance = shelfCompartment;
            Type type = typeof(ShelfCompartment);

            fieldm_IsBigBox = AccessTools.Field(type, "m_IsBigBox");
        }

        internal bool IsBigBox() {
            return (bool)fieldm_IsBigBox.GetValue(instance);
        }
    }
}