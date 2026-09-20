using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OneClickRestock
{
    internal enum EScreenType
    { 
        DEFAULT, BOARD_GAME, NONE
    }

    internal class RestockItemScreenWrapper
    {
        private readonly List<UnityEngine.Object> instances = new();

        private readonly FieldInfo fieldm_IsScreenOpen;
        private readonly FieldInfo fieldm_PageIndex;
        private readonly FieldInfo fieldm_CartItemList;
        private readonly FieldInfo fieldm_RestockItemCheckoutScreen;
        private readonly FieldInfo fieldm_CurrentRestockDataIndexList;
        private readonly FieldInfo fieldm_PageButtonHighlightList;

        private readonly MethodInfo methodHasEnoughCartSlot;
        private readonly MethodInfo methodAddToCartForCheckout;
        private readonly MethodInfo methodOnPressChangePageButton;

        private readonly List<int> allItemsInWindow = new List<int>();

        internal RestockItemScreenWrapper()
        {
            var baseType = typeof(RestockItemScreen);

            // Cache base members (valid for derived instances)
            methodHasEnoughCartSlot = AccessTools.Method(baseType, "HasEnoughCartSlot");
            methodAddToCartForCheckout = AccessTools.Method(baseType, "AddToCartForCheckout");
            methodOnPressChangePageButton = AccessTools.Method(baseType, "OnPressChangePageButton");
            fieldm_IsScreenOpen = AccessTools.Field(baseType, "m_IsScreenOpen");
            fieldm_PageIndex = AccessTools.Field(baseType, "m_PageIndex");
            fieldm_CartItemList = AccessTools.Field(baseType, "m_CartItemList");
            fieldm_RestockItemCheckoutScreen = AccessTools.Field(baseType, "m_RestockItemCheckoutScreen");
            fieldm_CurrentRestockDataIndexList = AccessTools.Field(baseType, "m_CurrentRestockDataIndexList");
            fieldm_PageButtonHighlightList = AccessTools.Field(baseType, "m_PageButtonHighlightList");

            RefreshScreens();
        }

        /// <summary>Rescan the scene for Restock screens (base + derived).</summary>
        internal void RefreshScreens()
        {
            instances.Clear();
            var found = UnityEngine.Object.FindObjectsOfType(typeof(RestockItemScreen));
            if (found != null && found.Length > 0)
                instances.AddRange(found.Where(o => o != null));

            FillAllItemsInWindow();
        }

        /// <summary>True if any screen in the list is currently open.</summary>
        internal bool IsScreenOpen()
        {
            // Be tolerant of dynamic UI: if nothing looks open, rescan once.
            if (!instances.Any())
            {
                RefreshScreens();
            }
            return instances.Any(IsOpen);
        }

        /// <summary>Return the type of the currently open screen; NONE if nothing open.</summary>
        internal EScreenType GetScreenType()
        {
            var active = GetActiveInstanceOrNull();
            if (active == null) return EScreenType.NONE;

            var name = active.GetType().Name;
            if (name == "RestockItemBoardGameScreen") return EScreenType.BOARD_GAME;
            return EScreenType.DEFAULT;
        }

        /// <summary>Base API — all act on the first open screen.</summary>
        internal bool HasEnoughCartSlot()
        {
            var inst = GetActiveInstanceOrNull();
            if (inst == null) return false;
            return (bool)methodHasEnoughCartSlot.Invoke(inst, null);
        }

        internal Dictionary<int, int> getCartItemList()
        {
            var inst = GetActiveInstanceOrNull();
            if (inst == null) return new();
            return (Dictionary<int, int>)fieldm_CartItemList.GetValue(inst);
        }

        internal void AddToCartForCheckout(int index, int boxCount)
        {
            if(index < 0) return;
            var inst = GetActiveInstanceOrNull();
            if (inst == null) return;
            methodAddToCartForCheckout.Invoke(inst, new object[] { index, boxCount });
        }

        internal void OnPressChangePageButton(int pageIndex)
        {
            var inst = GetActiveInstanceOrNull();
            if (inst == null) return;
            methodOnPressChangePageButton.Invoke(inst, new object[] { pageIndex });
        }

        internal bool IsRestockIndexInPage(int restockIndex)
        {
            var list = GetRestockIndicesInTab();
            return list.Contains(restockIndex);
        }

        internal List<int> GetRestockIndicesInTab()
        {
            var inst = GetActiveInstanceOrNull();
            if (inst == null) return new();
            var list = (List<int>)fieldm_CurrentRestockDataIndexList.GetValue(inst);
            return list != null ? list : new List<int>();
        }

        internal int GetPageIndex()
        {
            var inst = GetActiveInstanceOrNull();
            if (inst == null) return 0;
            var pageIndex = (int)fieldm_PageIndex.GetValue(inst);
            return pageIndex;
        }


        // --- Helpers ---
        private void FillAllItemsInWindow()
        {
            if (!instances.Any()) return;
            if (!instances.Any(IsOpen)) return;
            
            allItemsInWindow.Clear();
            var oldPage = GetPageIndex();
            for (int p = 0; p < CountPages(); p++)
            {
                OnPressChangePageButton(p);
                allItemsInWindow.AddRange(GetRestockIndicesInTab());
            }
            OnPressChangePageButton(oldPage);
        }

        private bool IsOpen(UnityEngine.Object obj)
        {
            try
            {
                return obj != null && (bool)fieldm_IsScreenOpen.GetValue(obj);
            }
            catch { return false; }
        }

        private int CountPages()
        {
            var inst = GetActiveInstanceOrNull();
            if (inst == null) return 0;
            var list = (List<GameObject>)fieldm_PageButtonHighlightList.GetValue(inst);
            return list.Count();
        }

        private UnityEngine.Object GetActiveInstanceOrNull()
        {
            if (!instances.Any()) RefreshScreens();
            // Prefer already-open instance; if none, try a quick rescan once
            var active = instances.FirstOrDefault(IsOpen);
            if (active != null) return active;

            RefreshScreens();
            return instances.FirstOrDefault(IsOpen);
        }

        // internals

        // Still available if you ever need an exact-type search
        internal T FindExactType<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsOfType<T>()
                        .FirstOrDefault(obj => obj.GetType() == typeof(T));
        }

        internal bool IsRestockIndexInAnyPage(int restockIndex)
        {
            return allItemsInWindow.Contains(restockIndex);
        }
    }
}
