using System.Text;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using Restrainite.RestrictionTypes.Base;

namespace Restrainite.Patches;

[HarmonyPatch]
internal static class ForceBabyTalk
{
    private static readonly Dictionary<string, string> WordMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "the", "da" }, { "you", "yu" }, { "your", "yur" }, { "with", "wif" },
        { "what", "wat" }, { "little", "widdle" }, { "love", "wuv" }, { "yes", "yah" },
        { "yeah", "yah" }, { "good", "gud" }, { "okay", "otay" }, { "dog", "doggy" },
        { "cat", "kitty" }, { "think", "fink" }, { "thanks", "fanks" }, { "thank", "fank" },
        { "thing", "fing" }, { "three", "fwee" }
    };

    internal static void Initialize()
    {
        Restrictions.ForceBabyTalk.OnChanged += OnChanged;
    }

    // Re-render existing text when the restriction toggles, so already-shown text updates.
    private static void OnChanged(IRestriction restriction)
    {
        MarkTextDirty(Engine.Current?.WorldManager?.FocusedWorld);
        MarkTextDirty(Userspace.UserspaceWorld);
    }

    private static void MarkTextDirty(World? world)
    {
        var root = world?.RootSlot;
        if (root == null) return;

        foreach (var text in root.GetComponentsInChildren<Text>())
            text?.MarkChangeDirty();
        foreach (var textRenderer in root.GetComponentsInChildren<TextRenderer>())
            textRenderer?.MarkChangeDirty();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TextRenderManager), nameof(TextRenderManager.String), MethodType.Setter)]
    private static bool TextRenderManager_String_Setter_Prefix(string value, ref string ____string)
    {
        if (value == null) return true;
        // PreventReading (cow speak) takes precedence over baby talk.
        if (Restrictions.PreventReading.IsRestricted) return true;
        if (!Restrictions.ForceBabyTalk.IsRestricted) return true;

        ____string = ToBabyTalk(value);
        return false;
    }

    private static string ToBabyTalk(string value)
    {
        var result = new StringBuilder(value.Length + 8);
        var word = new StringBuilder();
        var insideTag = false;

        foreach (var c in value)
        {
            if (insideTag)
            {
                result.Append(c);
                if (c == '>') insideTag = false;
            }
            else if (c == '<')
            {
                FlushWord(word, result);
                insideTag = true;
                result.Append(c);
            }
            else if (char.IsLetter(c))
            {
                word.Append(c);
            }
            else
            {
                FlushWord(word, result);
                result.Append(c);
            }
        }

        FlushWord(word, result);
        return result.ToString();
    }

    private static void FlushWord(StringBuilder word, StringBuilder result)
    {
        if (word.Length == 0) return;
        result.Append(TransformWord(word.ToString()));
        word.Clear();
    }

    private static string TransformWord(string word)
    {
        var allUpper = word.Length > 1 && word.All(char.IsUpper);
        var firstUpper = char.IsUpper(word[0]);
        var lower = word.ToLowerInvariant();

        string transformed;
        if (WordMap.TryGetValue(lower, out var mapped))
            transformed = mapped;
        else if (lower.Length >= 5 && lower.EndsWith("ing", StringComparison.Ordinal))
            // Drop the hard "g": running -> wunnin, etc.
            transformed = CharRules(string.Concat(lower.AsSpan(0, lower.Length - 3), "in".AsSpan()));
        else
            transformed = CharRules(lower);

        if (allUpper) return transformed.ToUpperInvariant();
        if (firstUpper && transformed.Length > 0)
            return char.ToUpperInvariant(transformed[0]) + transformed[1..];
        return transformed;
    }

    // th -> d, and l/r -> w (classic baby-talk consonant swaps).
    private static string CharRules(string s)
    {
        var result = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == 't' && i + 1 < s.Length && s[i + 1] == 'h')
            {
                result.Append('d');
                i++;
            }
            else
            {
                result.Append(c is 'l' or 'r' ? 'w' : c);
            }
        }

        return result.ToString();
    }
}
