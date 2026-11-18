#if NETFRAMEWORK
global using MathF = BimGo.FrameworkMathF;
#endif

namespace BimGo
{
    internal static class NumericCompat
    {
        public static void Fill<T>(T[] array, T value)
        {
            for (int i = 0; i < array.Length; i++) { array[i] = value; }
        }
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static int Clamp(int value, int min, int max)
        {
            if (min > max) { throw new System.ArgumentException(); }
            return value < min ? min : value > max ? max : value;
        }
        public static long Clamp(long value, long min, long max)
        {
            if (min > max) { throw new System.ArgumentException(); }
            return value < min ? min : value > max ? max : value;
        }
        public static float Clamp(float value, float min, float max)
        {
            if (min > max) { throw new System.ArgumentException(); }
            return value < min ? min : value > max ? max : value;
        }
        public static double Clamp(double value, double min, double max)
        {
            if (min > max) { throw new System.ArgumentException(); }
            return value < min ? min : value > max ? max : value;
        }
    }
#if NETFRAMEWORK
    internal static class FrameworkMathF
    {
        public const float PI = (float)System.Math.PI;
        public const float Tau = 2 * PI;
        public static float Abs(float x) => System.Math.Abs(x);
        public static float Acos(float x) => (float)System.Math.Acos(x);
        public static float Asin(float x) => (float)System.Math.Asin(x);
        public static float Atan(float x) => (float)System.Math.Atan(x);
        public static float Atan2(float y, float x) => (float)System.Math.Atan2(y, x);
        public static float Ceiling(float x) => (float)System.Math.Ceiling(x);
        public static float Cos(float x) => (float)System.Math.Cos(x);
        public static float Exp(float x) => (float)System.Math.Exp(x);
        public static float Floor(float x) => (float)System.Math.Floor(x);
        public static float Log(float x) => (float)System.Math.Log(x);
        public static float Log10(float x) => (float)System.Math.Log10(x);
        public static float Max(float x, float y) => System.Math.Max(x, y);
        public static float Min(float x, float y) => System.Math.Min(x, y);
        public static float Pow(float x, float y) => (float)System.Math.Pow(x, y);
        public static float Round(float x) => (float)System.Math.Round(x);
        public static float Round(float x, int digits) => (float)System.Math.Round(x, digits);
        public static float Sin(float x) => (float)System.Math.Sin(x);
        public static float Sqrt(float x) => (float)System.Math.Sqrt(x);
        public static float Tan(float x) => (float)System.Math.Tan(x);
    }
#endif
}
