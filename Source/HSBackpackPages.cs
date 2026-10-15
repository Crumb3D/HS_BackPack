using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class HSBackpackPages
{
    public const int PageSize = 40;
    public const string PagerId = "backpackPager";

    static readonly Dictionary<XUiC_Backpack, XUiC_Paging> pagers = new Dictionary<XUiC_Backpack, XUiC_Paging>();
    static readonly HashSet<XUiC_Paging> hookedPagers = new HashSet<XUiC_Paging>();
    static bool loggedPages;

    public static int BagSlotCount(XUiC_Backpack backpack)
    {
        if (backpack != null && backpack.xui != null && backpack.xui.PlayerInventory != null && backpack.xui.PlayerInventory.Backpack != null)
            return backpack.xui.PlayerInventory.Backpack.SlotCount;
        return LocalBagSlotCount(backpack != null ? backpack.xui : null);
    }

    public static int LocalBagSlotCount(XUi xui)
    {
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (player != null && player.bag != null)
            return player.bag.SlotCount;
        return 0;
    }

    public static int Offset(XUiC_Backpack backpack)
    {
        XUiC_Paging pager = GetPager(backpack);
        if (pager == null)
            return 0;
        return pager.CurrentPageNumber * PageSize;
    }

    public static bool IsBackpackPager(XUiC_Paging pager)
    {
        if (pager == null || pager.ViewComponent == null)
            return false;
        return pager.ViewComponent.ID == PagerId;
    }

    public static XUiC_Backpack BackpackFrom(XUiController start)
    {
        XUiController cur = start;
        while (cur != null && !(cur is XUiC_BackpackWindow))
            cur = cur.Parent;
        if (cur == null)
            cur = start;
        XUiC_Backpack grid = cur.GetChildByType<XUiC_Backpack>();
        if (grid != null)
            return grid;
        return start as XUiC_Backpack;
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
        HookPager(pager, backpack);
        return pager;
    }

    public static void HookPager(XUiC_Paging pager, XUiC_Backpack backpack)
    {
        if (pager == null || !hookedPagers.Add(pager))
            return;
        XUiC_Paging hooked = pager;
        pager.OnPageChanged += () =>
        {
            XUiC_Backpack grid = backpack != null ? backpack : BackpackFrom(hooked);
            if (grid != null)
                grid.RefreshBackpackSlots();
        };
    }

    public static void BindPager(XUiC_Paging pager, int slotCount)
    {
        if (pager == null)
            return;
        int n = Mathf.Max(slotCount, 1);
        pager.SetLastPageByElementsAndPageLength(n, PageSize);
        if (!loggedPages)
        {
            loggedPages = true;
            Log.Out("[HSBackPack] backpack pages bag=" + n + " last=" + pager.LastPageNumber + " (page size " + PageSize + ").");
        }
    }

    public static void BindPager(XUiC_Backpack backpack, int slotCount)
    {
        XUiC_Paging pager = GetPager(backpack);
        if (pager == null)
            return;
        BindPager(pager, Mathf.Max(slotCount, BagSlotCount(backpack)));
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

[HarmonyPatch(typeof(XUiC_ItemStackGrid), "Init")]
static class HSPatchBackpackGridInit
{
    static void Postfix(XUiC_ItemStackGrid __instance)
    {
        XUiC_Backpack backpack = __instance as XUiC_Backpack;
        if (backpack == null)
            return;
        HSBackpackPages.GetPager(backpack);
        HSBackpackPages.BindPager(backpack, HSBackpackPages.BagSlotCount(backpack));
    }
}

[HarmonyPatch(typeof(XUiC_BackpackWindow), "Init")]
static class HSPatchBackpackWindowInit
{
    static void Postfix(XUiC_BackpackWindow __instance)
    {
        XUiC_Backpack grid = __instance.backpackGrid;
        if (grid == null)
            grid = __instance.GetChildByType<XUiC_Backpack>();
        if (grid == null)
            return;
        HSBackpackPages.GetPager(grid);
        HSBackpackPages.BindPager(grid, HSBackpackPages.BagSlotCount(grid));
    }
}

[HarmonyPatch(typeof(XUiC_Paging), "Init")]
static class HSPatchBackpackPagerInit
{
    static void Postfix(XUiC_Paging __instance)
    {
        if (!HSBackpackPages.IsBackpackPager(__instance))
            return;
        XUiC_Backpack backpack = HSBackpackPages.BackpackFrom(__instance);
        HSBackpackPages.HookPager(__instance, backpack);
        int n = backpack != null ? HSBackpackPages.BagSlotCount(backpack) : HSBackpackPages.LocalBagSlotCount(__instance.xui);
        HSBackpackPages.BindPager(__instance, n);
    }
}

[HarmonyPatch(typeof(XUiC_Paging), "Update")]
static class HSPatchBackpackPagerUpdate
{
    static void Postfix(XUiC_Paging __instance)
    {
        if (!HSBackpackPages.IsBackpackPager(__instance))
            return;
        XUiC_Backpack backpack = HSBackpackPages.BackpackFrom(__instance);
        int n = backpack != null ? HSBackpackPages.BagSlotCount(backpack) : HSBackpackPages.LocalBagSlotCount(__instance.xui);
        HSBackpackPages.BindPager(__instance, n);
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

        int carry = bagCount;
        EntityPlayerLocal player = __instance.xui != null && __instance.xui.playerUI != null ? __instance.xui.playerUI.entityPlayer : null;
        if (player != null && player.CarryCapacity > 0)
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
            bool inBag = bagIndex < bagCount;
            if (inBag && bagIndex < stackList.Length && stackList[bagIndex] != null)
                slot.ItemStack = stackList[bagIndex].Clone();
            else
                slot.ItemStack = ItemStack.Empty;
            if (changed != null)
                slot.SlotChangedEvent += changed;
            slot.SlotNumber = bagIndex;
            slot.InfoWindow = info;
            slot.StackLocation = XUiC_ItemStack.StackLocationTypes.Backpack;
            slot.AttributeLock = !inBag || bagIndex >= carry;
            if (slot.ViewComponent != null)
                slot.ViewComponent.IsVisible = true;
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
        XUiC_Backpack grid = __instance.backpackGrid;
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
        XUiC_Backpack grid = __instance.backpackGrid;
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
