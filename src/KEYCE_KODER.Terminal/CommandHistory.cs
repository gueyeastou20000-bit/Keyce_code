using KEYCE_KODER.Core.Execution;

namespace KEYCE_KODER.Terminal;

public sealed class CommandHistory(int capacity = 500)
{
    private readonly List<string> _items = [];
    private int _cursor;

    public IReadOnlyList<string> Items => _items;

    public void Add(string command)
    {
        command = command.Trim();
        if (command.Length == 0) return;
        if (_items.Count == 0 || _items[^1] != command) _items.Add(command);
        if (_items.Count > capacity) _items.RemoveRange(0, _items.Count - capacity);
        _cursor = _items.Count;
    }

    /// <summary>Touche ↑.</summary>
    public string? Previous()
    {
        if (_items.Count == 0) return null;
        if (_cursor > 0) _cursor--;
        return _items[_cursor];
    }

    /// <summary>Touche ↓ ; renvoie "" quand on dépasse la dernière commande.</summary>
    public string Next()
    {
        if (_cursor < _items.Count - 1) return _items[++_cursor];
        _cursor = _items.Count;
        return "";
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllLines(path, _items);
    }

    public void Load(string path)
    {
        if (!File.Exists(path)) return;
        _items.Clear();
        foreach (var l in File.ReadLines(path)) Add(l);
    }
}
