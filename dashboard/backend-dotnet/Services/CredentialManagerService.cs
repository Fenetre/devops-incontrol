using System.Runtime.InteropServices;

namespace DashboardApi.Services;

/// <summary>
/// Windows Credential Manager P/Invoke — same targets as Python app/secrets.py.
/// Targets: DevOpsBacklogMonitor/AZDO_PAT, DB_PASSWORD, API_KEY, SMTP_PASSWORD.
/// </summary>
public static class CredentialManagerService
{
    private const string TargetPrefix = "DevOpsBacklogMonitor";
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    public static string? GetSecret(string name)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Environment.GetEnvironmentVariable(name);

        string target = $"{TargetPrefix}/{name}";
        if (!CredReadW(target, CredTypeGeneric, 0, out nint credPtr))
            return null;

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (cred.CredentialBlobSize > 0 && cred.CredentialBlob != nint.Zero)
                return Marshal.PtrToStringUni(cred.CredentialBlob, (int)(cred.CredentialBlobSize / 2));
            return null;
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    public static bool SetSecret(string name, string value)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        string target = $"{TargetPrefix}/{name}";
        var blob = System.Text.Encoding.Unicode.GetBytes(value);
        var cred = new CREDENTIAL
        {
            Type = CredTypeGeneric,
            TargetName = target,
            CredentialBlobSize = (uint)blob.Length,
            CredentialBlob = Marshal.AllocHGlobal(blob.Length),
            Persist = CredPersistLocalMachine,
        };

        try
        {
            Marshal.Copy(blob, 0, cred.CredentialBlob, blob.Length);
            return CredWriteW(ref cred, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(cred.CredentialBlob);
        }
    }

    public static bool DeleteSecret(string name)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        string target = $"{TargetPrefix}/{name}";
        return CredDeleteW(target, CredTypeGeneric, 0);
    }

    // P/Invoke declarations
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredReadW(string target, int type, int reservedFlag, out nint credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWriteW(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDeleteW(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(nint buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public nint CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public nint Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
}
