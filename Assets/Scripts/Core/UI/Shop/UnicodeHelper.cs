/*using UnityEngine;

using System.Text.RegularExpressions;

public static class UnicodeHelper
{
    // PUBLIC: Methods để xử lý Unicode
    public static string CleanUnicode(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return Regex.Replace(input, @"[^\u0000-\u007F]", "");
    }

    public static string PreserveUnicode(string input)
    {
        return input; // Giữ nguyên Unicode
    }

    public static bool ContainsUnicode(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;
        return Regex.IsMatch(input, @"[^\u0000-\u007F]");
    }
}
*/

/*#region ChatCompanion

using System.ComponentModel;

private bool _isChatNpc;
private bool IsChatNpc
{
    get => _isChatNpc;
    set
    {
        if (_isChatNpc == value) return;
        _isChatNpc = value;
        PropertyChangedd?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChatNpc)));
    }
}

public void OnChatNpc()
{
    IsChatNpc = true;
}

public void OnCloseChatNpc()
{
    IsChatNpc = false;
}
#endregion*/