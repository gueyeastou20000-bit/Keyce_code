using System.Runtime.InteropServices;

namespace KEYCE_KODER.Terminal;

/// <summary>
/// Trouve les processus lancés par un autre (ex. git.exe lancé par powershell.exe),
/// pour pouvoir les arrêter avec Ctrl+C sans fermer le terminal. Windows uniquement.
/// </summary>
internal static class ProcessTree
{
    private const uint SnapProcess = 0x00000002;
    private static readonly IntPtr InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>Identifiants des processus dont le parent est parentId.</summary>
    public static IReadOnlyList<int> ChildrenOf(int parentId)
    {
        var children = new List<int>();
        if (!OperatingSystem.IsWindows())
            return children;

        IntPtr snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == InvalidHandle)
            return children;

        try
        {
            var entry = new ProcessEntry { dwSize = (uint)Marshal.SizeOf<ProcessEntry>() };

            if (!Process32FirstW(snapshot, ref entry))
                return children;

            do
            {
                if (entry.th32ParentProcessID == (uint)parentId && entry.th32ProcessID != (uint)parentId)
                    children.Add((int)entry.th32ProcessID);
            }
            while (Process32NextW(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return children;
    }
}
