using System;
using BepInEx.Configuration;
using UnityEngine.InputSystem;

// BepInEx's generic value list requires IEquatable<T>, which enum types do not implement.
internal sealed class TrainerShortcutKeys : AcceptableValueBase
{
    private static readonly Key[] Keys = { Key.Insert, Key.Home, Key.F1, Key.F2, Key.F3, Key.F4,
        Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12 };
    private readonly Key fallback;
    internal TrainerShortcutKeys(Key fallback) : base(typeof(Key)) { this.fallback = fallback; }
    public override bool IsValid(object value) => value is Key key && Array.IndexOf(Keys, key) >= 0;
    public override object Clamp(object value) => IsValid(value) ? value : fallback;
    public override string ToDescriptionString() => "# Acceptable values: " + string.Join(", ", Keys);
}
