using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class HSBackpackPages
{
    public const int PageSize = 40;
    public const string PagerId = "backpackPager";

    static readonly Dictionary<XUiC_Backpack, XUiC_Paging> pagers = new Dictionary<XUiC_Backpack, XUiC_Paging>();
    static readonly HashSet<XUiC_Backpack> hooked = new HashSet<XUiC_Backpack>();

    public static int BagSlotCount(XUiC_Backpack backpack)
    {
        if (backpack == null || backpack.xui == null || backpack.xui.PlayerInventory == null)
            return 0;
        Bag bag = backpack.xui.PlayerInventory.Backpack;
        if (bag == null)
            return 0;
        return bag.SlotCount;
    }

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

        XUiController window = backpack;
        while (window != null && !(window is XUiC_BackpackWindow))
            window = window.Parent;
        if (window == null)
            window = backpack;

        XUiController named = window.GetChildById(PagerId);
        pager = named as XUiC_Paging;
        if (pager == null && named != null)
            pager = named.GetChildByType<XUiC_Paging>();
        if (pager == null)
            pager = window.GetChildByType<XUiC_Paging>();
        if (pager == null)
            return null;

        pagers[backpack] = pager;
        if (hooked.Add(backpack))
        {
            XUiC_Backpack grid = backpack;
            pager.OnPageChanged += () =>
            {
                if (grid != null)
                    grid.RefreshBackpackSlots();
            };
        }
        return pager;
    }

    public static void BindPager(XUiC_Backpack backpack, int slotCount)
    {
        XUiC_Paging pager = GetPager(backpack);
        if (pager == null)
            return;
        int n = Mathf.Max(slotCount, BagSlotCount(backpack));
        pager.SetLastPageByElementsAndPageLength(Mathf.Max(n, 1), PageSize);
    }

    public static ItemStack[] CopyBag(XUiC_Backpack backpack)
    {
        int n = BagSlotCount(backpack);
        if (n < 1)
            return ItemStack.CreateArray(0);
        ItemStack[] dest = ItemStack.CreateArray(n);
        ItemStack[] src = backpack != null ? backpack.GetSlots() : null;
        int copy = src == null ? 0 : Math.Min(dest.Length, src.Length);
        for (int i = 0; i < copy; i++)
            dest[i] = src[i] != null ? src[i].Clone() : ItemStack.Empty;
        return dest;
    }

    public static void WriteBag(XUiC_Backpack backpack, ItemStack[] stacks)
    {
        if (backpack == null || backpack.xui == null || backpack.xui.PlayerInventory == null || stacks == null)
            return;
        int n = BagSlotCount(backpack);
        if (n < 1)
            return;
        ItemStack[] dest;
        if (stacks.Length == n)
        {
            dest = new ItemStack[n];
            for (int i = 0; i < n; i++)
                dest[i] = stacks[i] != null ? stacks[i].Clone() : ItemStack.Empty;
        }
        else
        {
            dest = CopyBag(backpack);
            int offset = Offset(backpack);
            for (int i = 0; i < stacks.Length; i++)
            {
                int idx = offset + i;
                if (idx >= 0 && idx < dest.Length)
                    dest[idx] = stacks[i] != null ? stacks[i].Clone() : ItemStack.Empty;
            }
        }
        backpack.xui.PlayerInventory.SetBackpackItemStacks(dest);
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
        HSBackpackPages.BindPager(__instance, HSBackpackPages.BagSlotCount(__instance));
    }
}

[HarmonyPatch(typeof(XUiC_BackpackWindow), "Init")]
static class HSPatchBackpackWindowInit
{
    static void Postfix(XUiC_BackpackWindow __instance)
    {
        XUiC_Backpack grid = AccessTools.Field(typeof(XUiC_BackpackWindow), "backpackGrid").GetValue(__instance) as XUiC_Backpack;
        if (grid == null)
            grid = __instance.GetChildByType<XUiC_Backpack>();
        if (grid == null)
            return;
        HSBackpackPages.GetPager(grid);
        HSBackpackPages.BindPager(grid, HSBackpackPages.BagSlotCount(grid));
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

        int bagCount = HSBackpackPages.BagSlotCount(__instance);
        if (bagCount < 1)
            bagCount = stackList.Length;
        HSBackpackPages.BindPager(__instance, bagCount);

        int carry = 45;
        EntityPlayerLocal player = __instance.xui != null && __instance.xui.playerUI != null ? __instance.xui.playerUI.entityPlayer : null;
        if (player != null)
            carry = player.CarryCapacity;

        int offset = HSBackpackPages.Offset(__instance);
        AccessTools.Field(typeof(XUiC_Backpack), "maxSlotCount").SetValue(__instance, carry);
        var slotsField = AccessTools.Field(typeof(XUiC_Backpack), "backpackSlots");
        if (slotsField != null)
            slotsField.SetValue(__instance, bagCount);

        XUiC_ItemInfoWindow info = __instance.xui != null ? __instance.xui.GetChildByType<XUiC_ItemInfoWindow>() : null;
        var changed = AccessTools.Field(typeof(XUiC_ItemStackGrid), "handleSlotChangedDelegate").GetValue(__instance) as XUiEvent_SlotChangedEventHandler;

        for (int i = 0; i < controllers.Length; i++)
        {
            XUiC_ItemStack slot = controllers[i];
            int bagIndex = offset + i;
            if (changed != null)
                slot.SlotChangedEvent -= changed;
            bool inBag = bagIndex < bagCount && bagIndex < stackList.Length;
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

[HarmonyPatch(typeof(XUiC_Backpack), "Update")]
static class HSPatchBackpackUpdate
{
    static void Postfix(XUiC_Backpack __instance)
    {
        HSBackpackPages.BindPager(__instance, HSBackpackPages.BagSlotCount(__instance));
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
        int n = HSBackpackPages.BagSlotCount(backpack);
        if (n < 1 || slotNumber < 0 || slotNumber >= n)
            return false;
        ItemStack[] dest = HSBackpackPages.CopyBag(backpack);
        dest[slotNumber] = stack != null ? stack.Clone() : ItemStack.Empty;
        backpack.xui.PlayerInventory.SetBackpackItemStacks(dest);
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_Backpack), "UpdateBackend")]
static class HSPatchBackpackUpdateBackend
{
    static bool Prefix(XUiC_Backpack __instance, ItemStack[] stackList)
    {
        HSBackpackPages.WriteBag(__instance, stackList);
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
        if (bindingName != "paging_visible")
            return;
        value = "true";
        __result = true;
    }
}
