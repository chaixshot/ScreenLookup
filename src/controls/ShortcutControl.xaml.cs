using ScreenLookup.src.models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ScreenLookup.src.controls;

public partial class ShortcutControl : UserControl
{
    private readonly Brush BadBrush = new SolidColorBrush(Colors.Red);
    private readonly Brush GoodBrush = new SolidColorBrush(Colors.Transparent);

    private bool HasErrorWithKeySet { get; set; } = false;
    public bool HasConflictingError { get; set; } = false;

    private bool isRecording = false;
    private string previousSequence = string.Empty;
    public bool HasModifier { get; set; } = false;
    public bool HasLetter { get; set; } = false;

    private ShortcutKeySet _keySet = new();

    public ShortcutKeySet KeySet
    {
        get => _keySet;
        set
        {
            if (value == _keySet)
                return;

            _keySet = value;

            if (_keySet.Modifiers.Contains(ModifierKeys.Windows))
                WinKey.Visibility = Visibility.Visible;
            else
                WinKey.Visibility = Visibility.Collapsed;

            if (_keySet.Modifiers.Contains(ModifierKeys.Shift))
                ShiftKey.Visibility = Visibility.Visible;
            else
                ShiftKey.Visibility = Visibility.Collapsed;

            if (_keySet.Modifiers.Contains(ModifierKeys.Control))
                CtrlKey.Visibility = Visibility.Visible;
            else
                CtrlKey.Visibility = Visibility.Collapsed;

            if (_keySet.Modifiers.Contains(ModifierKeys.Alt))
                AltKey.Visibility = Visibility.Visible;
            else
                AltKey.Visibility = Visibility.Collapsed;

            KeyLetterTextBlock.Text = _keySet.NonModifierKey.ToString();
            KeySetChanged?.Invoke(this, EventArgs.Empty);

            isRecording = false;
            RecordingToggleButton.IsChecked = isRecording;
            RecordText.Text = isRecording ? "Recording..." : "Record";
        }
    }

    public event EventHandler? KeySetChanged;

    public ShortcutControl()
    {
        InitializeComponent();
        GoIntoNormalMode();
    }

    public void GoIntoErrorMode(string errorMessage = "")
    {
        BorderBrush = BadBrush;

        if (!string.IsNullOrEmpty(errorMessage))
            ErrorText.Text = errorMessage;

        ErrorText.Visibility = Visibility.Visible;
    }

    public void GoIntoNormalMode()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        ErrorText.Text = string.Empty;
        BorderBrush = GoodBrush;
    }

    private void ShortcutControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!isRecording)
            return;

        e.Handled = true;

        HashSet<Key> downKeys = GetDownKeys();

        bool containsWin = downKeys.Contains(Key.LWin) || downKeys.Contains(Key.RWin);
        bool containsShift = downKeys.Contains(Key.LeftShift) || downKeys.Contains(Key.RightShift);
        bool containsCtrl = downKeys.Contains(Key.LeftCtrl) || downKeys.Contains(Key.RightCtrl);
        bool containsAlt = downKeys.Contains(Key.LeftAlt) || downKeys.Contains(Key.RightAlt);

        HashSet<Key> justLetterKeys = RemoveModifierKeys(downKeys);

        HasLetter = justLetterKeys.Count != 0;
        HasModifier = containsWin || containsShift || containsCtrl || containsAlt;

        HashSet<ModifierKeys> modifierKeys = [];

        if (HasLetter)
            KeyKey.Visibility = Visibility.Visible;
        else
            KeyKey.Visibility = Visibility.Collapsed;

        if (containsWin)
        {
            WinKey.Visibility = Visibility.Visible;
            modifierKeys.Add(ModifierKeys.Windows);
        }
        else
            WinKey.Visibility = Visibility.Collapsed;

        if (containsShift)
        {
            ShiftKey.Visibility = Visibility.Visible;
            modifierKeys.Add(ModifierKeys.Shift);
        }
        else
            ShiftKey.Visibility = Visibility.Collapsed;

        if (containsCtrl)
        {
            CtrlKey.Visibility = Visibility.Visible;
            modifierKeys.Add(ModifierKeys.Control);
        }
        else
            CtrlKey.Visibility = Visibility.Collapsed;

        if (containsAlt)
        {
            AltKey.Visibility = Visibility.Visible;
            modifierKeys.Add(ModifierKeys.Alt);
        }
        else
            AltKey.Visibility = Visibility.Collapsed;

        List<string> keyStrings = [];
        foreach (Key key in justLetterKeys)
            keyStrings.Add(key.ToString());

        string currentSequence = string.Join('+', keyStrings);

        if (HasLetter && HasModifier)
        {
            HasErrorWithKeySet = false;
            ShortcutKeySet newKeySet = new()
            {
                Modifiers = modifierKeys,
                NonModifierKey = justLetterKeys.FirstOrDefault(),
            };
            KeySet = newKeySet;
        }
        else
        {
            HasErrorWithKeySet = true;
            ErrorText.Text = "Need to have at least one modifier and one non-modifier key";
        }

        if (string.IsNullOrEmpty(currentSequence) || currentSequence.Equals(previousSequence))
            return;

        KeyLetterTextBlock.Text = justLetterKeys.FirstOrDefault().ToString();
        previousSequence = currentSequence;
    }

    private void ShortcutControl_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!isRecording)
            return;

        CheckForErrors();
    }

    public void CheckForErrors()
    {
        if (HasErrorWithKeySet || HasConflictingError)
            GoIntoErrorMode();
        else
            GoIntoNormalMode();
    }

    private static HashSet<Key> RemoveModifierKeys(HashSet<Key> downKeys)
    {
        HashSet<Key> filteredKeys = new(downKeys);

        filteredKeys.Remove(Key.LWin);
        filteredKeys.Remove(Key.RWin);

        filteredKeys.Remove(Key.LeftShift);
        filteredKeys.Remove(Key.RightShift);

        filteredKeys.Remove(Key.LeftCtrl);
        filteredKeys.Remove(Key.RightCtrl);

        filteredKeys.Remove(Key.LeftAlt);
        filteredKeys.Remove(Key.RightAlt);

        return filteredKeys;
    }

    private static readonly byte[] DistinctVirtualKeys = Enumerable
        .Range(0, 256)
        .Select(KeyInterop.KeyFromVirtualKey)
        .Where(item => item != Key.None)
        .Distinct()
        .Select(item => (byte)KeyInterop.VirtualKeyFromKey(item))
        .ToArray();

    public static HashSet<Key> GetDownKeys()
    {
        byte[] keyboardState = new byte[256];
        NativeMethods.GetKeyboardState(keyboardState);

        HashSet<Key> downKeys = [];
        for (int index = 0; index < DistinctVirtualKeys.Length; index++)
        {
            byte virtualKey = DistinctVirtualKeys[index];
            if ((keyboardState[virtualKey] & 0x80) != 0)
                downKeys.Add(KeyInterop.KeyFromVirtualKey(virtualKey));
        }

        return downKeys;
    }

    private void RecordingToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton recordingToggleButton)
            return;

        isRecording = recordingToggleButton.IsChecked ?? false;
        RecordText.Text = isRecording ? "Recording..." : "Record";
    }
}
