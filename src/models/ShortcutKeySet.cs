using System.Windows.Input;

namespace ScreenLookup.src.models
{
    public class ShortcutKeySet : IEquatable<ShortcutKeySet>
    {
        public HashSet<ModifierKeys> Modifiers { get; set; } = [];
        public Key NonModifierKey { get; set; } = Key.None;

        public bool Equals(ShortcutKeySet? other)
        {
            if (other is null)
                return false;

            if (GetHashCode() == other.GetHashCode())
                return true;

            return Modifiers.SetEquals(other.Modifiers) && NonModifierKey == other.NonModifierKey;
        }

        public override bool Equals(object? obj) => Equals(obj as ShortcutKeySet);

        public override int GetHashCode()
        {
            int hash = NonModifierKey.GetHashCode();
            foreach (var mod in Modifiers)
            {
                hash ^= mod.GetHashCode();
            }
            return hash;
        }
    }
}
