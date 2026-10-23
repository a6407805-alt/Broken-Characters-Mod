// ITEMS tab: search + tier filter, give/remove items, set equipment, gold. Singleplayer host only (panel is hidden otherwise).
#pragma warning disable CS0618 // GiveItem/RemoveItem/SetEquipmentIndex overloads are "obsolete" but still work
using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BCMDevTools
{
    public partial class DevToolsPlugin
    {
        string _itemSearch = "";
        int _tierFilter;
        Vector2 _itemScroll;
        static readonly string[] TierNames = { "All", "White", "Green", "Red", "Boss", "Lunar", "Void", "Equipment" };

        static bool TierMatch(ItemTier tier, int filter)
        {
            if (tier == ItemTier.NoTier) return false;
            switch (filter)
            {
                case 1: return tier == ItemTier.Tier1;
                case 2: return tier == ItemTier.Tier2;
                case 3: return tier == ItemTier.Tier3;
                case 4: return tier == ItemTier.Boss;
                case 5: return tier == ItemTier.Lunar;
                case 6: return tier.ToString().StartsWith("Void");
                default: return true;
            }
        }

        static string TierColor(ItemTier tier)
        {
            switch (tier)
            {
                case ItemTier.Tier1: return "#ffffff";
                case ItemTier.Tier2: return "#7CFC9A";
                case ItemTier.Tier3: return "#ff6060";
                case ItemTier.Boss: return "#FFC928";
                case ItemTier.Lunar: return "#6fa8ff";
                default: return tier.ToString().StartsWith("Void") ? "#d46cff" : "#cccccc";
            }
        }

        void DrawItems(CharacterBody me)
        {
            if (!me || !me.inventory) { GUILayout.Label("<color=#aaaaaa>Spawn in first (TEST LEVEL), then give yourself items here.</color>", _label); return; }
            Inventory inv = me.inventory;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(50));
            _itemSearch = GUILayout.TextField(_itemSearch ?? "");
            if (GUILayout.Button("x", GUILayout.Width(24))) _itemSearch = "";
            GUILayout.EndHorizontal();
            _tierFilter = GUILayout.SelectionGrid(_tierFilter, TierNames, 4);

            string q = (_itemSearch ?? "").Trim().ToLowerInvariant();
            _itemScroll = GUILayout.BeginScrollView(_itemScroll, GUILayout.Height(330));
            if (_tierFilter == 7)
            {
                for (int i = 0; i < EquipmentCatalog.equipmentCount; i++)
                {
                    EquipmentDef d = EquipmentCatalog.GetEquipmentDef((EquipmentIndex)i);
                    if (!d || string.IsNullOrEmpty(d.nameToken)) continue;
                    string name = Language.GetString(d.nameToken);
                    if (q.Length > 0 && !name.ToLowerInvariant().Contains(q)) continue;
                    bool have = inv.currentEquipmentIndex == d.equipmentIndex;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<color={(d.isLunar ? "#6fa8ff" : "#ff9d5c")}>{name}</color>{(have ? "  <b>(equipped)</b>" : "")}", _label);
                    if (GUILayout.Button("Equip", GUILayout.Width(60)) && NetworkServer.active) inv.SetEquipmentIndex(d.equipmentIndex);
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                for (int i = 0; i < ItemCatalog.itemCount; i++)
                {
                    ItemDef d = ItemCatalog.GetItemDef((ItemIndex)i);
                    if (!d || d.hidden || !TierMatch(d.tier, _tierFilter)) continue;
                    string name = Language.GetString(d.nameToken);
                    if (q.Length > 0 && !name.ToLowerInvariant().Contains(q)) continue;
                    int have = inv.GetItemCount(d.itemIndex);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<color={TierColor(d.tier)}>{name}</color>{(have > 0 ? $"  <b>x{have}</b>" : "")}", _label);
                    if (GUILayout.Button("+1", GUILayout.Width(36))) GiveItem(inv, d, 1);
                    if (GUILayout.Button("+10", GUILayout.Width(40))) GiveItem(inv, d, 10);
                    if (GUILayout.Button("-1", GUILayout.Width(32)) && have > 0) GiveItem(inv, d, -1);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear all items")) ClearItems(inv);
            if (GUILayout.Button("+ $1000 gold") && NetworkServer.active && me.master) me.master.GiveMoney(1000u);
            GUILayout.EndHorizontal();
        }

        static void GiveItem(Inventory inv, ItemDef d, int n)
        {
            if (!NetworkServer.active) return;
            if (n > 0) inv.GiveItem(d, n);
            else inv.RemoveItem(d, -n);
        }

        static void ClearItems(Inventory inv)
        {
            if (!NetworkServer.active) return;
            for (int i = 0; i < ItemCatalog.itemCount; i++)
            {
                ItemDef d = ItemCatalog.GetItemDef((ItemIndex)i);
                if (!d) continue;
                int n = inv.GetItemCount(d.itemIndex);
                if (n > 0) inv.RemoveItem(d, n);
            }
            inv.SetEquipmentIndex(EquipmentIndex.None);
        }
    }
}
