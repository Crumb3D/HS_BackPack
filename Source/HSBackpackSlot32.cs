#if HS_SLOT32
using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

// 3.2 EquipmentSlots.Count/None is 12. 3.3's Backpack slot is that same index.
public static class HSBackpackSlot32
{
    public const int Index = 12;
    public const byte SaveVersion = 5;
    public const int MaxBagSize = 96;
    public static readonly EquipmentSlots Slot = (EquipmentSlots)Index;
}

[HarmonyPatch(typeof(Equipment), MethodType.Constructor, new Type[0])]
static class HSPatchEquipmentCtor
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static void Postfix(Equipment __instance)
    {
        HSPatchEquipmentCtor.Resize(__instance);
    }

    public static void Resize(Equipment equipment)
    {
        Traverse t = Traverse.Create(equipment);
        ItemValue[] slots = t.Field("m_slots").GetValue<ItemValue[]>();
        if (slots == null || slots.Length >= 13)
            return;
        Array.Resize(ref slots, 13);
        t.Field("m_slots").SetValue(slots);
        ItemClass[] cosmetics = t.Field("m_cosmeticSlots").GetValue<ItemClass[]>();
        if (cosmetics != null && cosmetics.Length < 13)
        {
            Array.Resize(ref cosmetics, 13);
            t.Field("m_cosmeticSlots").SetValue(cosmetics);
        }
        int[] preferred = t.Field("preferredItemSlots").GetValue<int[]>();
        if (preferred != null && preferred.Length < 13)
        {
            Array.Resize(ref preferred, 13);
            t.Field("preferredItemSlots").SetValue(preferred);
        }
    }
}

[HarmonyPatch(typeof(Equipment), "Write")]
static class HSPatchEquipmentWrite
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static bool Prefix(Equipment __instance, BinaryWriter writer)
    {
        HSPatchEquipmentCtor.Resize(__instance);
        Traverse t = Traverse.Create(__instance);
        ItemValue[] slots = t.Field("m_slots").GetValue<ItemValue[]>();
        ItemClass[] cosmetics = t.Field("m_cosmeticSlots").GetValue<ItemClass[]>();
        List<int> unlocked = t.Field("m_unlockedCosmetics").GetValue<List<int>>();
        writer.Write(HSBackpackSlot32.SaveVersion);
        for (int i = 0; i < 13; i++)
            ItemValue.Write(i < slots.Length ? slots[i] : null, writer);
        for (int j = 0; j < 13; j++)
        {
            ItemClass cosmetic = (cosmetics != null && j < cosmetics.Length) ? cosmetics[j] : null;
            if (cosmetic == null)
                writer.Write(0);
            else
                writer.Write(Equipment.CosmeticMappingStringID[cosmetic.GetItemName()]);
        }
        writer.Write(unlocked != null ? unlocked.Count : 0);
        if (unlocked != null)
        {
            for (int k = 0; k < unlocked.Count; k++)
                writer.Write(unlocked[k]);
        }
        return false;
    }
}

[HarmonyPatch(typeof(Equipment), "Read")]
static class HSPatchEquipmentRead
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static bool Prefix(BinaryReader reader, ref Equipment __result)
    {
        int version = reader.ReadByte();
        Equipment equipment = new Equipment();
        HSPatchEquipmentCtor.Resize(equipment);
        Traverse t = Traverse.Create(equipment);
        ItemValue[] slots = t.Field("m_slots").GetValue<ItemValue[]>();
        ItemClass[] cosmetics = t.Field("m_cosmeticSlots").GetValue<ItemClass[]>();
        int slotCount = version >= HSBackpackSlot32.SaveVersion ? 13 : slots.Length;
        if (version <= 2)
            slotCount = 5;
        else if (version <= 3)
            slotCount = 8;
        else if (version == 4)
            slotCount = 12;
        for (int i = 0; i < slotCount; i++)
            slots[i] = ItemValue.ReadOrNull(reader);
        if (version >= 2)
        {
            for (int j = 0; j < slotCount; j++)
            {
                int id = reader.ReadInt32();
                if (id == 0)
                    cosmetics[j] = null;
                else
                    cosmetics[j] = ItemClass.GetItemClass(Equipment.CosmeticMappingIDString[id]);
            }
            List<int> unlocked = t.Field("m_unlockedCosmetics").GetValue<List<int>>();
            unlocked.Clear();
            int n = reader.ReadInt32();
            for (int k = 0; k < n; k++)
                unlocked.Add(reader.ReadInt32());
        }
        t.Field("m_slots").SetValue(slots);
        t.Field("m_cosmeticSlots").SetValue(cosmetics);
        __result = equipment;
        return false;
    }
}

[HarmonyPatch(typeof(ItemClassArmor), "Init")]
static class HSPatchArmorBackpackSlot
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static void Prefix(ItemClassArmor __instance)
    {
        string slot;
        if (__instance.Properties.Values.TryGetValue("EquipSlot", out slot)
            && string.Equals(slot, "Backpack", StringComparison.OrdinalIgnoreCase))
        {
            __instance.Properties.Values["EquipSlot"] = "None";
        }
    }
}

[HarmonyPatch(typeof(XUiC_EquipmentStack), "ParseAttribute")]
static class HSPatchEquipStackBackpackAttr
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static bool Prefix(XUiC_EquipmentStack __instance, string _name, string _value, ref bool __result)
    {
        if (_name != "slot" || !string.Equals(_value, "Backpack", StringComparison.OrdinalIgnoreCase))
            return true;
        AccessTools.Field(typeof(XUiC_EquipmentStack), "equipSlot").SetValue(__instance, HSBackpackSlot32.Slot);
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_EquipmentStackGrid), "RegisterExtraSlots")]
static class HSPatchRegisterExtraSlots
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static bool Prefix(XUiC_EquipmentStackGrid __instance, IEnumerable<XUiC_EquipmentStack> _slots)
    {
        var map = (EnumDictionary<EquipmentSlots, XUiC_EquipmentStack>)AccessTools.Field(typeof(XUiC_EquipmentStackGrid), "itemControllers").GetValue(__instance);
        foreach (XUiC_EquipmentStack slot in _slots)
        {
            map[slot.EquipSlot] = slot;
            slot.SlotChangedEvent += __instance.HandleSlotChangedEvent;
        }
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_EquipmentStackGrid), "GetSlots")]
static class HSPatchEquipmentGridGetSlots
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static bool Prefix(XUiC_EquipmentStackGrid __instance, ref ItemValue[] __result)
    {
        Equipment equipment = __instance.xui.PlayerEquipment.Equipment;
        ItemValue[] array = new ItemValue[13];
        for (int i = 0; i < array.Length; i++)
            array[i] = equipment.GetSlotItem(i) ?? ItemValue.None;
        __result = array;
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_CharacterFrameWindow), "Init")]
static class HSPatchCharacterBackpackTab
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static void Postfix(XUiC_CharacterFrameWindow __instance)
    {
        XUiC_EquipmentStackGrid grid = __instance.GetChildByType<XUiC_EquipmentStackGrid>();
        XUiController backpackSlots = __instance.GetChildById("backpackSlots");
        if (grid != null && backpackSlots != null)
            grid.RegisterExtraSlots(backpackSlots.GetChildrenByType<XUiC_EquipmentStack>());
    }
}

[HarmonyPatch(typeof(XUiM_PlayerEquipment), "IsWearing")]
static class HSPatchIsWearingBackpack
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static bool Prefix(XUiM_PlayerEquipment __instance, ItemValue _itemValue, ref bool __result)
    {
        ItemClassArmor armor = _itemValue.ItemClass as ItemClassArmor;
        if (armor == null || armor.EquipSlot != HSBackpackSlot32.Slot)
            return true;
        __result = __instance.GetStackFromSlot(HSBackpackSlot32.Slot).itemValue.type == _itemValue.type;
        return false;
    }
}

[HarmonyPatch(typeof(XUiM_PlayerEquipment), "IsEquipmentTypeWorn")]
static class HSPatchIsTypeWornBackpack
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static bool Prefix(XUiM_PlayerEquipment __instance, EquipmentSlots _slot, ref bool __result)
    {
        if (_slot != EquipmentSlots.Count && _slot != HSBackpackSlot32.Slot)
            return true;
        if ((int)_slot >= __instance.Equipment.GetSlotCount())
        {
            __result = false;
            return false;
        }
        __result = !__instance.GetStackFromSlot(_slot).itemValue.IsEmpty();
        return false;
    }
}

[HarmonyPatch(typeof(EntityPlayer), "OnUpdateEntity")]
static class HSPatchBagResize32
{
    static bool Prepare()
    {
        return !HSGameVersion.Is33;
    }

    static void Postfix(EntityPlayer __instance)
    {
        if (__instance.isEntityRemote || __instance.bag == null)
            return;
        int wanted = Mathf.Clamp(
            (int)EffectManager.GetValue(PassiveEffects.BagSize, null, 45f, __instance, null, default(FastTags<TagGroup.Global>), true, false),
            1, HSBackpackSlot32.MaxBagSize);
        ItemStack[] slots = __instance.bag.GetSlots();
        if (slots == null || slots.Length == wanted)
            return;
        List<ItemStack> kept = new List<ItemStack>();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && !slots[i].IsEmpty())
                kept.Add(slots[i]);
        }
        ItemStack[] next = ItemStack.CreateArray(wanted);
        int copy = Math.Min(wanted, kept.Count);
        for (int i = 0; i < copy; i++)
            next[i] = kept[i];
        __instance.bag.SetSlots(next);
        if (kept.Count > wanted)
        {
            ItemStack[] drop = new ItemStack[kept.Count - wanted];
            for (int i = wanted; i < kept.Count; i++)
                drop[i - wanted] = kept[i];
            Vector3 pos = __instance.position;
            pos.y += 0.9f;
            GameManager.Instance.DropContentInLootContainerServer(__instance.entityId, "DroppedLootContainer", pos, drop, true);
        }
    }
}
#endif
