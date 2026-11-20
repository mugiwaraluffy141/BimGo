namespace BimGo.Platform
{
    internal static class CopyValueAvailability
    {
        public static bool IsAvailable(string value) => !string.IsNullOrEmpty(value);
    }
}
