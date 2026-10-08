using System;
using System.Globalization;

// Numeric checks are independent of Unity so they can also run in the verification harness.
internal static class TrainerNumbers
{
    internal static bool TryAmount(string input, out double amount)
    {
        // No thousands separator: a decimal comma must never silently become a different amount.
        bool parsed = double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out amount) ||
            double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
        return parsed && IsFinite(amount) && amount >= 0.0;
    }

    internal static bool TryBalance(double current, double amount, bool add, out double result)
    {
        result = add ? current + amount : amount;
        return IsFinite(current) && IsFinite(amount) && amount >= 0 && IsFinite(result) && result >= 0;
    }

    internal static float ClampSpeed(float speed) => (float)ClampFinite(speed, 0.1, 50, 1);
    internal static float ClampScale(float scale) => (float)ClampFinite(scale, 0.75, 2, 1);
    internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static double ClampFinite(double value, double minimum, double maximum, double fallback) =>
        IsFinite(value) ? Math.Max(minimum, Math.Min(maximum, value)) : fallback;
}
