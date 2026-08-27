namespace CareComply.Application.Features.AccessManagement.NavigationManagement;

internal static class NavigationManagementValidation
{
    public const int MaximumKeyLength = 100;
    public const int MaximumNameLength = 100;
    public const int MaximumRouteLength = 200;
    public const int MaximumIconKeyLength = 100;
    public const int MaximumSortOrder = 100_000;

    public static bool IsValidKey(string value) =>
        IsValidSlug(value, MaximumKeyLength);

    public static bool IsValidIconKey(string value) =>
        IsValidSlug(value, MaximumIconKeyLength);

    public static bool IsValidInternalRoute(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaximumRouteLength
            || value[0] != '/'
            || value.Length > 1 && value[^1] == '/'
            || value.Contains("//", StringComparison.Ordinal)
            || value.Any(character => char.IsControl(character)
                || char.IsWhiteSpace(character)
                || character is '\\' or '?' or '#' or ':'))
        {
            return false;
        }

        return value == "/"
            || value[1..]
                .Split('/', StringSplitOptions.None)
                .All(segment => IsValidSlug(segment, MaximumKeyLength));
    }

    private static bool IsValidSlug(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximumLength
            || !char.IsAsciiLetterOrDigit(value[0])
            || !char.IsAsciiLetterOrDigit(value[^1])
            || value.Contains("--", StringComparison.Ordinal))
        {
            return false;
        }

        return value.All(character =>
            char.IsAsciiLetterLower(character)
            || char.IsAsciiDigit(character)
            || character == '-');
    }
}
