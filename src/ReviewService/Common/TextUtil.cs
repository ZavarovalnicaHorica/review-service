namespace Horica.ReviewService.Common;

public static class TextUtil
{
    public static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
