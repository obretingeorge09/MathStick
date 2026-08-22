using UnityEngine;
using System.Collections.Generic;

/// <summary>One shipping language.</summary>
public struct LanguageInfo
{
    public string code;         // column name in the table
    public string nativeName;   // what the picker shows — always in that language
    public SystemLanguage system;

    public LanguageInfo(string code, string nativeName, SystemLanguage system)
    {
        this.code = code; this.nativeName = nativeName; this.system = system;
    }
}

/// <summary>
/// The game's string table.
///
/// Keys ARE the English text, so a call site reads as the sentence it renders
/// and an untranslated string degrades to English rather than to a key. The
/// table lives in Resources/i18n.txt, tab separated, one column per language.
/// </summary>
public static class Loc
{
    // Chosen the way other mobile titles pick a first twenty: the largest
    // mobile-gaming markets, plus Romanian because it is the studio's own.
    //
    // Thai, Hindi and Arabic are deliberately absent. Unity's legacy uGUI Text
    // does no complex-script shaping or bidi reordering, so Devanagari
    // conjuncts and Arabic joining render as loose glyphs in the wrong order —
    // shipping that is worse than not offering the language. They need a
    // shaping library, which is its own piece of work.
    public static readonly LanguageInfo[] Languages = {
        new LanguageInfo("en",      "English",          SystemLanguage.English),
        new LanguageInfo("zh-Hans", "简体中文",           SystemLanguage.ChineseSimplified),
        new LanguageInfo("zh-Hant", "繁體中文",           SystemLanguage.ChineseTraditional),
        new LanguageInfo("ja",      "日本語",             SystemLanguage.Japanese),
        new LanguageInfo("ko",      "한국어",             SystemLanguage.Korean),
        new LanguageInfo("es",      "Español",          SystemLanguage.Spanish),
        new LanguageInfo("pt",      "Português",        SystemLanguage.Portuguese),
        new LanguageInfo("ru",      "Русский",          SystemLanguage.Russian),
        new LanguageInfo("de",      "Deutsch",          SystemLanguage.German),
        new LanguageInfo("fr",      "Français",         SystemLanguage.French),
        new LanguageInfo("it",      "Italiano",         SystemLanguage.Italian),
        new LanguageInfo("tr",      "Türkçe",           SystemLanguage.Turkish),
        new LanguageInfo("vi",      "Tiếng Việt",       SystemLanguage.Vietnamese),
        new LanguageInfo("id",      "Bahasa Indonesia", SystemLanguage.Indonesian),
        new LanguageInfo("pl",      "Polski",           SystemLanguage.Polish),
        new LanguageInfo("nl",      "Nederlands",       SystemLanguage.Dutch),
        new LanguageInfo("uk",      "Українська",       SystemLanguage.Ukrainian),
        new LanguageInfo("cs",      "Čeština",          SystemLanguage.Czech),
        new LanguageInfo("sv",      "Svenska",          SystemLanguage.Swedish),
        new LanguageInfo("ro",      "Română",           SystemLanguage.Romanian),
    };

    const string PREF_KEY = "Lang";

    static Dictionary<string, string[]> table;   // english key -> one cell per language
    static int current = -1;
    static bool loaded;

    public static int Current
    {
        get { EnsureLoaded(); return current; }
    }

    public static LanguageInfo CurrentInfo => Languages[Current];

    /// <summary>English keeps the seven-segment lettering; nothing else can.</summary>
    public static bool IsEnglish => Current == 0;

    /// <summary>
    /// Translate. Returns the key itself when there is no cell for it, and the
    /// key is the English text — so a missing translation shows English,
    /// never a key.
    /// </summary>
    public static string T(string english)
    {
        if (string.IsNullOrEmpty(english)) return english;

        EnsureLoaded();
        if (current == 0 || table == null) return english;

        string[] row;
        if (!table.TryGetValue(english, out row)) return english;
        if (current >= row.Length) return english;

        return string.IsNullOrEmpty(row[current]) ? english : row[current];
    }

    public static void SetLanguage(int index)
    {
        EnsureLoaded();

        index = Mathf.Clamp(index, 0, Languages.Length - 1);
        if (index == current) return;

        current = index;
        PlayerPrefs.SetString(PREF_KEY, Languages[index].code);
        PlayerPrefs.Save();

        Messenger.Broadcast(Message.OnLanguageChanged);
    }

    // ── Font ────────────────────────────────────────────────────────────

    static Font systemFont;

    /// <summary>
    /// The font translated text has to use, or null to keep whichever the
    /// screen was built with.
    ///
    /// DSEG7Classic-Bold carries 69 glyphs — ASCII and nothing else — and
    /// Orbitron 183, which covers Spanish and German but not Romanian, Turkish,
    /// Polish, Czech, Vietnamese, Cyrillic or CJK. So every language but
    /// English is drawn in the platform font, whose fallback chain covers all
    /// of them. English keeps the segment lettering the game is built around.
    /// </summary>
    public static Font UIFont => IsEnglish ? null : PlatformFont;

    /// <summary>
    /// The platform's own font, regardless of the language in use. The
    /// language list needs it even while the game is in English, because every
    /// row names its language in that language.
    /// </summary>
    public static Font PlatformFont
    {
        get
        {
            if (systemFont == null)
            {
                // Android resolves the first of these it has and falls back
                // through the system chain for anything that face is missing.
                // The Windows names are here so the Editor preview is not a
                // row of boxes while working on a CJK language.
                systemFont = Font.CreateDynamicFontFromOSFont(new[] {
                    "Noto Sans CJK SC", "Noto Sans CJK JP", "Noto Sans CJK KR",
                    "Noto Sans", "Droid Sans Fallback", "Roboto",
                    "Microsoft YaHei", "Meiryo", "Malgun Gothic",
                    "Segoe UI", "Arial",
                }, 32);
            }

            return systemFont;
        }
    }

    // ── Loading ─────────────────────────────────────────────────────────

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        LoadTable();
        current = ResolveStartingLanguage();
    }

    static void LoadTable()
    {
        var asset = Resources.Load<TextAsset>("i18n");
        if (asset == null)
        {
            Debug.LogWarning("Loc: Resources/i18n.txt is missing — running in English.");
            return;
        }

        // Tab separated rather than comma: the strings carry commas and middle
        // dots of their own, and a tab is the one character none of them have.
        var lines = asset.text.Split('\n');
        table = new Dictionary<string, string[]>(lines.Length);

        // Row 0 is the header. Its order has to match Languages, so check
        // rather than trust — a column inserted in the file would otherwise
        // silently hand every language its neighbour's text.
        var header = lines.Length > 0 ? lines[0].TrimEnd('\r').Split('\t') : new string[0];
        for (int i = 0; i < Languages.Length; i++)
        {
            if (i < header.Length && header[i] == Languages[i].code) continue;

            Debug.LogError("Loc: i18n.txt column " + i + " is '" +
                           (i < header.Length ? header[i] : "<missing>") +
                           "' but Languages[" + i + "] is '" + Languages[i].code +
                           "'. Running in English.");
            table = null;
            return;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;

            var cells = line.Split('\t');
            if (cells.Length < 2) continue;

            // A newline inside a string is written as a two-character escape,
            // so one row stays one line
            for (int c = 0; c < cells.Length; c++)
                if (cells[c].IndexOf("\\n") >= 0) cells[c] = cells[c].Replace("\\n", "\n");

            table[cells[0]] = cells;
        }
    }

    static int ResolveStartingLanguage()
    {
        string saved = PlayerPrefs.GetString(PREF_KEY, "");
        if (!string.IsNullOrEmpty(saved))
            for (int i = 0; i < Languages.Length; i++)
                if (Languages[i].code == saved) return i;

        // First run follows the device rather than making them go and find the
        // setting. Some devices report Chinese without telling the two scripts
        // apart, and Simplified is the larger market of the two.
        var sys = Application.systemLanguage;
        for (int i = 0; i < Languages.Length; i++)
            if (Languages[i].system == sys) return i;

        if (sys == SystemLanguage.Chinese) return 1;

        return 0;
    }
}
