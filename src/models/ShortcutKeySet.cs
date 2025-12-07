using System.Windows.Input;

namespace ScreenLookup.src.models
{
    public class ShortcutKeySet : IEquatable<ShortcutKeySet>
    {
        public HashSet<ModifierKeys> Modifiers { get; set; } = new();
        public Key NonModifierKey { get; set; } = Key.None;

        public bool Equals(ShortcutKeySet? other)
        {
            if (other is null)
                return false;

            if (GetHashCode() == other.GetHashCode())
                return true;

            return false;
        }
    }
}
