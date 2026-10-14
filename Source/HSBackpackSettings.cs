using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class HSBackpackSettings
{
    public static KeyCode DropKey = KeyCode.G;
    public static bool DropAlt = true;
    public static float DropLifetimeHours = 24f;

    public static void Load(string modPath)
    {
        DropKey = KeyCode.G;
        DropAlt = true;
        DropLifetimeHours = 24f;
        if (!string.IsNullOrEmpty(modPath))
            TryRead(Path.Combine(modPath, "HSBackPackSettings.json"));
        try
        {
            string user = GameIO.GetUserGameDataDir();
            if (!string.IsNullOrEmpty(user))
                TryRead(Path.Combine(user, "HSBackPack", "HSBackPackSettings.json"));
        }
        catch { }
        Log.Out("[HSBackPack] Drop backpack: " + (DropAlt ? "Alt+" : "") + DropKey
            + ", lifetime " + DropLifetimeHours.ToString(CultureInfo.InvariantCulture) + "h.");
    }

    static void TryRead(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;
        string text = File.ReadAllText(path);
        string key = JsonString(text, "DropKey");
        if (!string.IsNullOrEmpty(key))
        {
            KeyCode parsed;
            if (Enum.TryParse(key, true, out parsed))
                DropKey = parsed;
        }
        bool alt;
        if (JsonBool(text, "DropAlt", out alt))
            DropAlt = alt;
        float hours;
        if (JsonFloat(text, "DropLifetimeHours", out hours) && hours > 0f)
            DropLifetimeHours = hours;
        Log.Out("[HSBackPack] Read " + path);
    }

    static string JsonString(string text, string name)
    {
        int i = text.IndexOf("\"" + name + "\"", StringComparison.Ordinal);
        if (i < 0)
            return null;
        int colon = text.IndexOf(':', i);
        if (colon < 0)
            return null;
        int q1 = text.IndexOf('"', colon + 1);
        if (q1 < 0)
            return null;
        int q2 = text.IndexOf('"', q1 + 1);
        if (q2 < 0)
            return null;
        return text.Substring(q1 + 1, q2 - q1 - 1);
    }

    static bool JsonBool(string text, string name, out bool value)
    {
        value = false;
        int i = text.IndexOf("\"" + name + "\"", StringComparison.Ordinal);
        if (i < 0)
            return false;
        int colon = text.IndexOf(':', i);
        if (colon < 0)
            return false;
        string rest = text.Substring(colon + 1).TrimStart();
        if (rest.StartsWith("true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }
        if (rest.StartsWith("false", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }
        return false;
    }

    static bool JsonFloat(string text, string name, out float value)
    {
        value = 0f;
        int i = text.IndexOf("\"" + name + "\"", StringComparison.Ordinal);
        if (i < 0)
            return false;
        int colon = text.IndexOf(':', i);
        if (colon < 0)
            return false;
        string rest = text.Substring(colon + 1).TrimStart();
        int n = 0;
        while (n < rest.Length && (char.IsDigit(rest[n]) || rest[n] == '.' || rest[n] == '-'))
            n++;
        return float.TryParse(rest.Substring(0, n), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
