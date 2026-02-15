namespace HotsPatchNotes.Shared;

/// <summary>
/// Application-wide constants.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Talent-related constants.
    /// </summary>
    public static class Talents
    {
        /// <summary>
        /// Number of talent tiers in Heroes of the Storm.
        /// </summary>
        public const int TierCount = 7;

        /// <summary>
        /// Minimum talent choice value (1-indexed).
        /// </summary>
        public const char MinChoice = '1';

        /// <summary>
        /// Maximum talent choice value.
        /// </summary>
        public const char MaxChoice = '5';

        /// <summary>
        /// Levels at which talent tiers unlock.
        /// </summary>
        public static readonly int[] TierLevels = [1, 4, 7, 10, 13, 16, 20];
    }

    /// <summary>
    /// Gamestrings-related constants for parsing Heroes of the Storm game files.
    /// </summary>
    public static class Gamestrings
    {
        /// <summary>
        /// Default locale for gamestrings (English US).
        /// </summary>
        public const string DefaultLocale = "enus";

        /// <summary>
        /// All supported locales in Heroes of the Storm.
        /// </summary>
        public static readonly string[] SupportedLocales =
        [
            "enus", // English (US)
            "dede", // German
            "eses", // Spanish (Spain)
            "esmx", // Spanish (Mexico)
            "frfr", // French
            "itit", // Italian
            "kokr", // Korean
            "plpl", // Polish
            "ptbr", // Portuguese (Brazil)
            "ruru", // Russian
            "zhcn", // Chinese (Simplified)
            "zhtw"  // Chinese (Traditional)
        ];
    }

    /// <summary>
    /// Pagination constants.
    /// </summary>
    public static class Pagination
    {
        /// <summary>
        /// Default page number.
        /// </summary>
        public const int DefaultPage = 1;

        /// <summary>
        /// Default page size.
        /// </summary>
        public const int DefaultPageSize = 20;

        /// <summary>
        /// Maximum page size allowed.
        /// </summary>
        public const int MaxPageSize = 100;
    }

    /// <summary>
    /// Error messages.
    /// </summary>
    public static class ErrorMessages
    {
        public const string HeroNotFound = "Hero not found";
        public const string PatchNotFound = "Patch not found";
        public const string BattlegroundNotFound = "Battleground not found";
        public const string InvalidTalentCode = "Invalid talent code";
        public const string BuildCodeRequired = "Build code required";
        public const string InvalidBuildCodeFormat = "Invalid build code format";
        public const string DatabaseUnavailable = "Database is temporarily unavailable";
        public const string InternalServerError = "An unexpected error occurred";
        public const string RequestCancelled = "Request was cancelled";
        public const string GamestringsFetchFailed = "Failed to fetch gamestrings";
        public const string GamestringsParsingFailed = "Failed to parse gamestrings";
    }

    /// <summary>
    /// Section types for patch content.
    /// </summary>
    public static class SectionTypes
    {
        public const string Hero = "Hero";
        public const string Battleground = "Battleground";
        public const string General = "General";
    }

    /// <summary>
    /// API route prefixes.
    /// </summary>
    public static class ApiRoutes
    {
        public const string Heroes = "api/heroes";
        public const string Patches = "api/patches";
        public const string Battlegrounds = "api/battlegrounds";
        public const string Admin = "api/admin";
    }
}
