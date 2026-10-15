using System.Reflection;
using HarmonyLib;

public class HSBackPackMod : IModApi
{
    public const string Version = "1.0.2";
    public static string ModPath;

    public void InitMod(Mod _modInstance)
    {
        if (!HSGameVersion.AllowLoad("[HSBackPack]"))
            return;
        ModPath = _modInstance.Path;
        HSBackpackSettings.Load(ModPath);
        try
        {
            new Harmony("HSBackPack").PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (System.Exception e)
        {
            Log.Error("[HSBackPack] Harmony patch failed. " + e);
            return;
        }
        ModEvents.GameUpdate.RegisterHandler(OnGameUpdate);
#if HS_33_ONLY
        Log.Out("[HSBackPack] v" + Version + " 3.3 paging. Alt+G drops the worn backpack as a pickup. Install on the host and on every client.");
#else
        if (HSGameVersion.Is33)
            Log.Out("[HSBackPack] v" + Version + " backpack pages. Alt+G drops the worn backpack as a pickup. Install on the host and on every client.");
        else
            Log.Out("[HSBackPack] v" + Version + " 3.2 backpacks plus pages. Alt+G drops the worn backpack as a pickup. Install on the host and on every client.");
#endif
    }

    static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        try
        {
            HSBackpackDrop.Tick();
        }
        catch (System.Exception e)
        {
            Log.Error("[HSBackPack] Drop tick failed. " + e);
        }
    }
}
