<div align="center">
  <img src="assets/app_icon.png" alt="RADYJKO PRO Logo" width="128" height="128" />
  <h1>📻 RADYJKO PRO 5.0.0 FINAL • OGŚ</h1>
  <p><b>Nowoczesne, wielojęzyczne centrum radia cyfrowego dla Windows 10/11 (.NET 8 WPF + NAudio DSP)</b></p>
  <p>
    <img src="https://img.shields.io/badge/.NET-8.0_Windows-512BD4?logo=dotnet" alt=".NET 8" />
    <img src="https://img.shields.io/badge/Platform-Windows_10%2F11_x64-0078D6?logo=windows" alt="Windows" />
    <img src="https://img.shields.io/badge/Version-5.0.0_FINAL-10B981" alt="Version" />
    <img src="https://img.shields.io/badge/Brand-OGŚ_2026-38BDF8" alt="OGŚ" />
  </p>
</div>

---

## ✨ Główne funkcje (v5.0.0 FINAL)

* **💿 Inteligentny Sklep Płytowy (Allegro • eBay • iMusic.pl):**
  * Automatyczne wykrywanie aktualnie granego artysty ze strumienia ICY i wyszukiwanie jego płyt winylowych oraz CD.
  * **Priorytetowe sortowanie:** Najpierw wyświetlane są **najtańsze nowe płyty**, a w dalszej kolejności wydania używane.
  * Dwukrotne kliknięcie otwiera bezpośrednią ofertę w przeglądarce.
* **⏪ Time-Shifting (Buforowanie audycji na żywo):**
  * Pierścieniowy bufor PCM w pamięci (do 5 minut wstecz) pozwala zatrzymać transmisję (`⏸ Buforuj`), cofać o `-15s` / `-30s`, przewijać do przodu `+15s` oraz wrócić jednym kliknięciem do transmisji `NA ŻYWO`.
* **🎙 Rozpoznawanie utworów (`WASAPI Song ID`):**
  * Przechwytywanie sygnału z karty dźwiękowej (WASAPI Loopback) i błyskawiczne dopasowywanie metadanych oraz okładki albumu z katalogu iTunes dla stacji bez RDS.
* **🎚 12-pasmowy korektor DSP EQ + Tryb Nocny (Limiter):**
  * 12 wbudowanych profili brzmieniowych (*Płaski, Electro, Wokal, Rock, Bass+, Pop, Jazz, Metal, Klasyka, Hip-Hop, Akustyk, Klub*) oraz możliwość zapisywania własnych presetów użytkownika (`💾 Zapisz własny`).
  * `Auto-Leveler` oraz **🌙 Tryb Nocny (Limiter)** wyrównujący głośność między cichymi rozmowami w studiu a głośnymi reklamami.
* **📊 Analizator widma FFT + Stereo LED VU Meter:**
  * 12-pasmowy wizualizer częstotliwości oraz dwukanałowy wskaźnik wysterowania L/R w czasie rzeczywistym.
* **⏰ Inteligentny Budzik Radiowy & Cykliczny Harmonogram DVR:**
  * Automatyczne nagrywanie audycji do plików `.mp3` według dnia tygodnia (lub codziennie) i godziny, z funkcją automatycznego dzielenia plików przy zmianie utworu na antenie.
* **🌍 Radio-Browser Pro HD:**
  * Wyszukiwarka światowych stacji z filtrowaniem po kraju (*PL, GB, DE, LT, US*), gatunku, minimalnym bitrate (`≥128/192/256 kbps`) i wskaźnikiem statusu serwera (`🟢`/`🟡`).
* **📱 Mobilny Pilot Wi-Fi & Integracja z Windows 11 (SMTC):**
  * Wbudowany responsywny serwer HTTP umożliwiający sterowanie radiem, głośnością i listą stacji z przeglądarki smartfona w sieci domowej.
  * Pełna integracja z systemową nakładką multimedialną Windows 11 (OSD) i fizycznymi klawiszami multimedialnymi klawiatury.
* **🛡️ OGŚ Secure Shield (`SecurityManager.cs`):**
  * Wymuszenie szyfrowania **TLS 1.2 / TLS 1.3**, walidacja certyfikatów SSL, ochrona przed przepełnieniem bufora metadanych ICY, sanityzacja ciągów znaków oraz blokada dostępu spoza lokalnej sieci LAN dla pilota Wi-Fi.
* **🌐 4 języki interfejsu + Motywy Ciemny / Jasny:**
  * Pełna lokalizacja: **Polski (PL)**, **English (EN)**, **Lietuvių (LT)**, **Deutsch (DE)** oraz tryby *Full*, *Mini* i pływający *Micro*.

---

## 🚀 Instalacja (Dla użytkowników)

1. Pobierz najnowszy instalator **`RADYJKO_PRO_Setup_v5.0.0_FINAL.exe`** z sekcji [**Releases**](../../releases).
2. Uruchom instalator, opcjonalnie zaznacz **„Utwórz ikonę na pulpicie”** i kliknij **Zainstaluj**.

---

## 🛠 Budowanie ze źródeł (Dla deweloperów)

Wymagane: **.NET 8.0 SDK (Windows)** oraz **Inno Setup 6/7**.

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\deploy.ps1
```
Skrypt automatycznie skompiluje samodzielny plik wykonywalny (`win-x64 --self-contained`) w katalogu `publish/` oraz zbuduje instalator `dist\RADYJKO_PRO_Setup_v5.0.0_FINAL.exe`.

---
*Wyprodukowano przez **OGŚ** (2026) • GitHub: [mcogs-prog](https://github.com/mcogs-prog)*
