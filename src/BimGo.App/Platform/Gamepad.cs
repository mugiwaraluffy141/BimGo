using BimGo.Scene;

// The class belongs to the Platform namespace
namespace BimGo.Platform
{
    /// <summary>
    /// Reads and writes legacy controller settings so existing user preferences remain compatible. Controller input is
    /// no longer polled or dispatched.
    /// </summary>
    internal static class Gamepad
    {
        public static float LookSensitivity { get; set; } = 1f;
        public static bool InvertY { get; set; }
        public static float DeadZone { get; set; } = LaunchSettings.DEFAULT_PAD_DEAD_ZONE;
        public static float TriggerThreshold { get; set; } = LaunchSettings.DEFAULT_PAD_TRIGGER;
        public static GenericPadMap GenericMap { get; set; } = GenericPadMap.XboxStyleGuess();

        /// <summary>Kept for the F1 help sheet; no controller is connected by this build.</summary>
        public static bool Connected => false;

        public static void Read(LaunchSettings settings)
        {
            if (settings == null) { return; }
            LookSensitivity = settings.PadLookSensitivity;
            InvertY = settings.PadInvertY;
            DeadZone = settings.PadDeadZone;
            TriggerThreshold = settings.PadTriggerThreshold;
            var problems = new List<string>();
            GenericMap = GenericPadMap.FromSettings(settings.GenericPadMap, problems);
            foreach (string problem in problems) { Utilities.Log_Utils.Write($"Gamepad map: skipped {problem}."); }
        }

        public static void Write(LaunchSettings settings)
        {
            if (settings == null) { return; }
            settings.PadLookSensitivity = LookSensitivity;
            settings.PadInvertY = InvertY;
            settings.PadDeadZone = DeadZone;
            settings.PadTriggerThreshold = TriggerThreshold;
            settings.GenericPadMap = GenericMap.ToSettings();
        }
    }
}
