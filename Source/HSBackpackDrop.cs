using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

public static class HSBackpackDrop
{
    public const string MetaKey = "hsPack";
    public const int SlotIndex = 12;

    static MethodInfo stackWrite;
    static MethodInfo stackRead;

    public static void Tick()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        EntityPlayerLocal player = world.GetPrimaryPlayer();
        if (player == null || player.IsDead() || player.bag == null || player.equipment == null)
            return;
        TryUnpack(player);
        if (!Pressed(player))
            return;
        Drop(player);
    }

    static bool Pressed(EntityPlayerLocal player)
    {
        if (HSBackpackSettings.DropAlt
            && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
            return false;
        if (!Input.GetKeyDown(HSBackpackSettings.DropKey))
            return false;
        if (player.playerUI != null && player.playerUI.windowManager != null
            && player.playerUI.windowManager.IsInputActive())
            return false;
        return true;
    }

    static void Drop(EntityPlayerLocal player)
    {
        ItemValue worn = WornBackpack(player);
        if (worn == null || worn.IsEmpty())
        {
            GameManager.ShowTooltip(player, Localization.Get("hsBackpackNothingEquipped"));
            return;
        }
        List<ItemStack> packed = SnapshotBag(player);
        ItemValue dropped = worn.Clone();
        PackInto(dropped, packed);
        player.EmptyBackpack();
        SetEquip(player.equipment, SlotIndex, null);
        Vector3 pos = player.position;
        pos.y += 0.2f;
        float life = Mathf.Max(60f, HSBackpackSettings.DropLifetimeHours * 3600f);
        GameManager.Instance.ItemDropServer(new ItemStack(dropped, 1), pos, Vector3.zero, player.entityId, life);
        GameManager.ShowTooltip(player, Localization.Get("hsBackpackDropped"));
    }

    static void TryUnpack(EntityPlayerLocal player)
    {
        ItemValue worn = WornBackpack(player);
        string packed;
        if (worn == null || worn.IsEmpty() || !worn.TryGetMetadata(MetaKey, out packed) || string.IsNullOrEmpty(packed))
            return;
        List<ItemStack> stacks = Unpack(packed);
        if (stacks == null)
            return;
        if (player.bag.SlotCount < stacks.Count)
            return;
        for (int i = 0; i < stacks.Count; i++)
        {
            ItemStack stack = stacks[i];
            if (stack == null || stack.IsEmpty())
                continue;
            if (!player.bag.AddItem(stack) && !stack.IsEmpty())
            {
                Vector3 pos = player.position;
                pos.y += 0.2f;
                GameManager.Instance.ItemDropServer(stack, pos, Vector3.zero, player.entityId, 3600f);
            }
        }
        worn.RemoveMetaData(MetaKey);
    }

    static ItemValue WornBackpack(EntityPlayerLocal player)
    {
        if (player.equipment.GetSlotCount() <= SlotIndex)
            return null;
        ItemValue worn = player.equipment.GetSlotItem(SlotIndex);
        if (worn == null || worn.IsEmpty())
            return null;
        ItemClassArmor armor = worn.ItemClass as ItemClassArmor;
        if (armor == null)
            return null;
        if ((int)armor.EquipSlot != SlotIndex)
            return null;
        return worn;
    }

    static List<ItemStack> SnapshotBag(EntityPlayerLocal player)
    {
        List<ItemStack> packed = new List<ItemStack>();
        player.bag.PerformActionOnSlots(stack =>
        {
            if (stack != null && !stack.IsEmpty())
                packed.Add(stack.Clone());
        });
        return packed;
    }

    static void PackInto(ItemValue iv, List<ItemStack> packed)
    {
        MemoryStream ms = new MemoryStream();
        PooledBinaryWriter w = MemoryPools.poolBinaryWriter.AllocSync(true);
        try
        {
            w.SetBaseStream(ms);
            w.Write(packed != null ? packed.Count : 0);
            if (packed != null)
            {
                for (int i = 0; i < packed.Count; i++)
                    WriteStack(packed[i] ?? ItemStack.Empty, w);
            }
            iv.SetMetadata(MetaKey, Convert.ToBase64String(ms.ToArray()));
        }
        finally
        {
            w.SetBaseStream(null);
            MemoryPools.poolBinaryWriter.FreeSync(w);
        }
    }

    static List<ItemStack> Unpack(string b64)
    {
        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(b64);
        }
        catch
        {
            return null;
        }
        MemoryStream ms = new MemoryStream(raw);
        PooledBinaryReader r = MemoryPools.poolBinaryReader.AllocSync(true);
        try
        {
            r.SetBaseStream(ms);
            int n = r.ReadInt32();
            if (n < 0 || n > 256)
                return null;
            List<ItemStack> list = new List<ItemStack>(n);
            for (int i = 0; i < n; i++)
            {
                ItemStack stack = new ItemStack();
                ReadStack(stack, r);
                list.Add(stack);
            }
            return list;
        }
        catch (Exception e)
        {
            Log.Warning("[HSBackPack] Packed backpack could not be read. " + e.Message);
            return null;
        }
        finally
        {
            r.SetBaseStream(null);
            MemoryPools.poolBinaryReader.FreeSync(r);
        }
    }

    static void WriteStack(ItemStack stack, PooledBinaryWriter w)
    {
        EnsureStackIo();
        if (stackWrite != null)
            stackWrite.Invoke(stack, new object[] { w });
    }

    static void ReadStack(ItemStack stack, PooledBinaryReader r)
    {
        EnsureStackIo();
        if (stackRead != null)
            stackRead.Invoke(stack, new object[] { r });
    }

    static void EnsureStackIo()
    {
        if (stackWrite != null)
            return;
        MethodInfo[] methods = typeof(ItemStack).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            ParameterInfo[] p = m.GetParameters();
            if (p.Length != 1)
                continue;
            if (m.Name == "Write" && typeof(BinaryWriter).IsAssignableFrom(p[0].ParameterType))
                stackWrite = m;
            if (m.Name == "Read" && typeof(BinaryReader).IsAssignableFrom(p[0].ParameterType))
                stackRead = m;
        }
    }

    static void SetEquip(Equipment eq, int index, ItemValue value)
    {
        MethodInfo[] methods = typeof(Equipment).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (m.Name != "SetSlotItem")
                continue;
            ParameterInfo[] p = m.GetParameters();
            if (p.Length == 2)
            {
                m.Invoke(eq, new object[] { index, value });
                return;
            }
            if (p.Length == 3)
            {
                m.Invoke(eq, new object[] { index, value, true });
                return;
            }
        }
    }
}
