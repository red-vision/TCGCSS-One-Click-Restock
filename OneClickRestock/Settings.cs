using System;
using BepInEx.Configuration;
using UnityEngine;

namespace OneClickRestock {
    public class Settings {
        private static Settings m_instance = null;
        public static Settings Instance {
            get {
                if (m_instance == null) {
                    m_instance = new Settings();
                }
                return m_instance;
            }
        }
        private Plugin m_plugin = null;

        // General
        public static ConfigEntry<RestockBehavior> restockBehavior;
        public static ConfigEntry<RestockOption> warehouseShelves;
        public static ConfigEntry<RestockOption> shoppingShelves;
        public static ConfigEntry<bool> checkAllPackages;

        public static ConfigEntry<KeyboardShortcut> oneClickRestockKey;

        public void load(Plugin plugin) {
            this.m_plugin = plugin;

            // General
            restockBehavior = this.m_plugin.Config.Bind<RestockBehavior>("Restock options", "Restock Behavior", RestockBehavior.AllWindows, new ConfigDescription("Restock only the currently opened tab, or the whole window and tabs in it, or restock across all windows and tabs?", null, [new ConfigurationManagerAttributes{Order=4}]));
            warehouseShelves = this.m_plugin.Config.Bind<RestockOption>("Restock options", "Restock Warehouse Shelves", RestockOption.PrioritizeBigBox, new ConfigDescription("Should the Warehouse Shelves be checked for missing items?", null, [new ConfigurationManagerAttributes{Order=3}]));
            shoppingShelves = this.m_plugin.Config.Bind<RestockOption>("Restock options", "Restock Shopping Shelves", RestockOption.DoNotRestock, new ConfigDescription("Should the Shopping Shelves be checked for missing items?", null, [new ConfigurationManagerAttributes{Order=2}]));
            checkAllPackages = this.m_plugin.Config.Bind<bool>("Restock options", "Check packages dropped or being carried", true, new ConfigDescription("Should other packages which are not currently stored be accounted for to be excluded from the shopping cart?", null, [new ConfigurationManagerAttributes{Order=1}]));

            // Hotkeys
            oneClickRestockKey = this.m_plugin.Config.Bind<KeyboardShortcut>("Keybinds", "One Click Restock Key", new KeyboardShortcut(KeyCode.R, Array.Empty<KeyCode>()), "Keyboard Shortcut to automatically fill market cart with missing warehouse items.");
        }
    }
}