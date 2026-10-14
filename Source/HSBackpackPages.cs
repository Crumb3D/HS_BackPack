using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class HSBackpackPages
{
    public const int PageSize = 40;

    static readonly Dictionary<XUiC_Backpack, XUiC_Paging> pagers = new Dictionary<XUiC_Backpack, XUiC_Paging>();

    public static int Offset(XUiC_Backpack backpack)
    {
        XUiC_Paging pager = GetPager(backpack);
        if (pager == null)
            return 0;
        return pager.CurrentPageNumber * PageSize;
    }

    public static XUiC_Paging GetPager(XUiC_Backpack backpack)
    {
        if (backpack == null)
            return null;
        XUiC_Paging pager;
        if (pagers.TryGetValue(backpack, out pager) && pager != null)
            return pager;
        XUiController cur = backpack;
        while (cur != null)
        {
            pager = cur.GetChildByType<XUiC_Paging>();
            if (pager != null)
            {
                pagers[backpack] = pager;
                pager.OnPageChanged += () => backpack.RefreshBackpackSlots();
                return pager;
            }
            cur = cur.Parent;
        }
        return null;
    }

    public static void BindPager(XUiC_Backpack backpack, int slotCount)
    {
        XUiC_Paging pager = GetPager(backpack);
        if (pager == null)
            return;
        pager.SetLastPageByElementsAndPageLength(Mathf.Max(slotCount, 1), PageSize);
    }

    public static XUiC_ItemStack[] Controllers(XUiC_ItemStackGrid grid)
    {
        return grid.GetItemStackControllers();
    }
}

[HarmonyPatch(typeof(XUiC_Backpack), "Init")]
static class HSPatchBackpackInit
{
    static void Postfix(XUiC_Backpack __instance)
    {
        HSBackpackPages.GetPager(__instance);
    }
}

[HarmonyPatch(typeof(XUiC_Backpack), "SetStacks")]
static class HSPatchBackpackSetStacks
{
    static bool Prefix(XUiC_Backpack __instance, ItemStack[] stackList)
    {
        if (stackList == null)
            return false;
        XUiC_ItemStack[] controllers = HSBackpackPages.Controllers(__instance);
        if (controllers == null || controllers.Length == 0)
            return true;

        int carry = 45;
        EntityPlayerLocal player = __instance.xui != null ? __instance.xui.playerUI.entityPlayer : null;
        if (player != null)
            carry = player.CarryCapacity;

        HSBackpackPages.BindPager(__instance, stackList.Length);
        int offset = HSBackpackPages.Offset(__instance);
        AccessTools.Field(typeof(XUiC_Backpack), "maxSlotCount").SetValue(__instance, carry);

        XUiC_ItemInfoWindow info = __instance.xui.GetChildByType<XUiC_ItemInfoWindow>();
        var changed = AccessTools.Field(typeof(XUiC_ItemStackGrid), "handleSlotChangedDelegate").GetValue(__instance) as XUiEvent_SlotChangedEventHandler;

        for (int i = 0; i < controllers.Length; i++)
        {
            XUiC_ItemStack slot = controllers[i];
            int bagIndex = offset + i;
            if (changed != null)
                slot.SlotChangedEvent -= changed;
            bool inBag = bagIndex < stackList.Length;
            slot.ItemStack = inBag ? stackList[bagIndex].Clone() : ItemStack.Empty;
            if (changed != null)
                slot.SlotChangedEvent += changed;
            slot.SlotNumber = bagIndex;
            slot.InfoWindow = info;
            slot.StackLocation = XUiC_ItemStack.StackLocationTypes.Backpack;
            slot.AttributeLock = !inBag || bagIndex >= carry;
            if (slot.ViewComponent != null)
                slot.ViewComponent.IsVisible = inBag;
        }
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_ItemStackGrid), "HandleSlotChangedEvent")]
static class HSPatchBackpackSlotChanged
{
    static bool Prefix(XUiC_ItemStackGrid __instance, int slotNumber, ItemStack stack)
    {
        XUiC_Backpack backpack = __instance as XUiC_Backpack;
        if (backpack == null)
            return true;
        ItemStack[] all = backpack.GetSlots();
        if (all == null || slotNumber < 0 || slotNumber >= all.Length)
            return false;
        all[slotNumber] = stack != null ? stack.Clone() : ItemStack.Empty;
        backpack.xui.PlayerInventory.SetBackpackItemStacks(all);
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_BackpackWindow), "ApplyLockedSlotStates")]
static class HSPatchBackpackApplyLocks
{
    static bool Prefix(XUiC_BackpackWindow __instance, PackedBoolArray _lockedSlots)
    {
        XUiC_Backpack grid = AccessTools.Field(typeof(XUiC_BackpackWindow), "backpackGrid").GetValue(__instance) as XUiC_Backpack;
        if (grid == null)
            return true;
        int offset = HSBackpackPages.Offset(grid);
        XUiC_ItemStack[] controllers = grid.GetItemStackControllers();
        for (int i = 0; i < controllers.Length; i++)
        {
            int bagIndex = offset + i;
            controllers[i].UserLockedSlot = _lockedSlots != null && bagIndex < _lockedSlots.Length && _lockedSlots[bagIndex];
        }
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_BackpackWindow), "UpdateLockedSlots")]
static class HSPatchBackpackUpdateLocks
{
    static bool Prefix(XUiC_BackpackWindow __instance, XUiC_ContainerStandardControls _csc)
    {
        if (_csc == null)
            return false;
        XUiC_Backpack grid = AccessTools.Field(typeof(XUiC_BackpackWindow), "backpackGrid").GetValue(__instance) as XUiC_Backpack;
        if (grid == null)
            return true;
        int slotCount = __instance.xui.PlayerInventory.Backpack.SlotCount;
        PackedBoolArray locked = _csc.LockedSlots ?? new PackedBoolArray(slotCount);
        if (locked.Length != slotCount)
            locked.Length = slotCount;
        int offset = HSBackpackPages.Offset(grid);
        XUiC_ItemStack[] controllers = grid.GetItemStackControllers();
        for (int i = 0; i < controllers.Length; i++)
        {
            int bagIndex = offset + i;
            if (bagIndex < locked.Length)
                locked[bagIndex] = controllers[i].UserLockedSlot;
        }
        _csc.LockedSlots = locked;
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_BackpackWindow), "GetBindingValueInternal")]
static class HSPatchBackpackPagingVisible
{
    static void Postfix(ref string value, string bindingName, ref bool __result)
    {
        if (__result || bindingName != "paging_visible")
            return;
        value = "true";
        __result = true;
    }
}
