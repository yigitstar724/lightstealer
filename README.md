# Light Stealer 🚀

Light Stealer, Windows ortamlarında tarayıcı ve Discord verilerini toplamak için geliştirilmiş bir bilgi sızdırma aracı örneğidir. Proje, kullanıcı profilinden veriler çıkarmak ve bunları webhook ve dosya yükleme servisleri üzerinden göndermek için tasarlanmıştır.

## Neler yapar? 🔍

- Chromium tabanlı tarayıcılardan (Chrome, Edge, Brave, Opera, Vivaldi, Yandex ve diğerleri) çerez, giriş bilgisi, tarayıcı geçmişi, yer imleri ve kayıtlı kredi kartı bilgilerini çıkartır.
- Firefox ve Firefox tabanlı tarayıcı profillerinden veri toplar.
- Discord masaüstü uygulaması ve tarayıcı oturumlarından token arar.
- Bulunan tokenlerle Discord kullanıcı bilgisi, Nitro/badge durumu, faturalandırma ve nadir arkadaşları raporlar.
- Sistem bilgisi, IP ve konum bilgisi toplar.
- Telegram, Steam, Epic Games ve kripto cüzdan verilerini yedekleyip yükler.
- Webhook ve dosya yükleme servisleri üzerinden raporlama yapar.

## Proje yapısı 🧱

- `cookie.py`: Projenin ana Python betiği. Tüm veri çıkarma mantığını içerir.
- `AppStealers.cs`, `DiscordStealer.cs`, `BrowserExtractor.cs`: C# tarafında farklı veri toplama ve sızdırma işlemlerini içeren kaynaklar.
- `CryptoHelper.cs`, `NativeInterop.cs`: Şifre çözme ve yerel Windows API çağrılarını yöneten yardımcı sınıflar.
- `Program.cs`: C# uygulamasının giriş noktası.

## Kurulum ve kullanım ⚙️

1. `cookie.py` içindeki `WEBHOOK_URL` değerini kendi Discord webhook URL’inizle değiştirin.
2. Python 3 ortamında aşağıdaki komutu çalıştırın:

```bash
python cookie.py
```

3. Makine üzerinde çalıştırıldığında, geçici bir klasör içinde tarayıcı verilerini çıkarır, sıkıştırır ve belirlenen webhook adresine gönderir.

## Güvenlik ve uyarılar ⚠️

- Bu araç yalnızca yasal ve etik amaçlarla, açıkça izin verilen sistemlerde kullanılmalıdır.
- Kötü amaçlı kullanım ciddi hukuki sonuçlar doğurabilir.
- Projeyi kendi sorumluluğunuzda kullandığınızdan emin olun.

## Notlar 📝

- `cookie.py` içinde farklı tarayıcılar ve platformlar için özel anahtar çözme ve veri erişim yöntemleri bulunur.
- Kod, Firefox için NSS kitaplığı ve Chrome tabanlı tarayıcılar için Windows DPAPI/CNG çözücü mekanizmalarını kullanır.
- Bu proje bir güvenlik araştırma veya pen-test senaryosu olarak değerlendirilebilir.

## Lisans 📜

Bu proje [MIT Lisansı](https://opensource.org/licenses/MIT) altında lisanslanmıştır.

> Bu araç, yalnızca izin verilen ortamlarda ve etik amaçlarla kullanılmalıdır.

---

## English Version

# Light Stealer 🚀

Light Stealer is an information-stealing tool developed for Windows environments. The project is designed to extract sensitive profile data and send it through webhook or file upload services.

## What it does 🔍

- Extracts cookies, login credentials, browser history, bookmarks, and stored credit card data from Chromium-based browsers (Chrome, Edge, Brave, Opera, Vivaldi, Yandex, and others).
- Collects data from Firefox and Firefox-based browser profiles.
- Searches for tokens from Discord desktop application and browser sessions.
- Reports Discord user information, Nitro/badge status, billing details, and rare friends based on discovered tokens.
- Collects system information, public IP, and location data.
- Backs up and uploads Telegram, Steam, Epic Games, and crypto wallet data.
- Reports data through webhooks and file upload services.

## Project structure 🧱

- `cookie.py`: Main Python script of the project. Contains the core data extraction logic.
- `AppStealers.cs`, `DiscordStealer.cs`, `BrowserExtractor.cs`: C# sources for different data collection and exfiltration actions.
- `CryptoHelper.cs`, `NativeInterop.cs`: Helper classes managing decryption and local Windows API calls.
- `Program.cs`: Entry point for the C# application.

## Setup and usage ⚙️

1. Change the `WEBHOOK_URL` value in `cookie.py` to your Discord webhook URL.
2. Run the following command in a Python 3 environment:

```bash
python cookie.py
```

3. When executed on a machine, it extracts browser data to a temporary folder, compresses it, and sends it to the configured webhook.

## Security and warnings ⚠️

- This tool should only be used for legal and ethical purposes on systems where you have explicit permission.
- Malicious usage can lead to serious legal consequences.
- Use the project at your own risk.

## Notes 📝

- `cookie.py` contains specialized key decryption and data access methods for different browsers and platforms.
- The code uses the NSS library for Firefox and Windows DPAPI/CNG decryption mechanisms for Chrome-based browsers.
- This project can be considered a security research or pen-test scenario.

## License 📜

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT).

> This tool should only be used in permitted environments and for ethical purposes.
