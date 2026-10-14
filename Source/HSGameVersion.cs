using System;

public static class HSGameVersion
{
#if HS_33_ONLY
    public static readonly string[] Supported = { "3.3" };
#else
    public static readonly string[] Supported = { "3.2", "3.3" };
#endif

    public static string Current { get; private set; }

    public static bool Is33
    {
        get { return Current == "3.3"; }
    }

    public static bool AllowLoad(string logPrefix)
    {
        string game;
        string detail;
        try
        {
            var v = Constants.cVersionInformation;
            game = ReleaseKey(v);
            Current = game;
            detail = v != null ? v.LongString : "unknown";
        }
        catch (Exception e)
        {
            Current = "";
            Log.Error(logPrefix + " REFUSING TO LOAD: could not read 7DTD version. " + e.Message);
            return false;
        }
        if (string.IsNullOrEmpty(game) || Array.IndexOf(Supported, game) < 0)
        {
            Log.Error(logPrefix + " REFUSING TO LOAD: this build supports 7DTD "
                + string.Join(", ", Supported) + ". Game is "
                + (string.IsNullOrEmpty(game) ? "unknown" : game)
                + " (" + detail + "). Remove this folder from Mods or use the matching DLL.");
            return false;
        }
        Log.Out(logPrefix + " 7DTD " + game + " (" + detail + ").");
        return true;
    }

    public static string ReleaseKey(VersionInformation v)
    {
        if (v == null || !v.IsValid) return "";
        if (v.ReleaseType == VersionInformation.EGameReleaseType.V && v.Major >= 3)
            return v.Major + "." + (v.Minor / 10);
        return v.Major + "." + v.Minor;
    }
}
