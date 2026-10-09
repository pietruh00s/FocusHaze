# Changelog

## [Unreleased]

### Zmienione
- Nowa nazwa: **FocusHaze** (dawniej WinDimmer), bo nazwa WinDimmer jest już zajęta w Microsoft Store. Ustawienia i autostart z WinDimmera są przenoszone automatycznie przy pierwszym uruchomieniu.

## [1.0.3] – 2026-10-08

### Poprawione
- Płynne przejścia: przy zmianie okna przyciemnia się płynnie tylko okno, które straciło fokus. Reszta ekranu nie miga już od zera do ustawionej wartości.

## [1.0.2] – 2026-10-08

### Zmienione
- Uporządkowana paczka zip: w głównym folderze są tylko `WinDimmer.exe` (mały launcher) i `LICENSE`, a cała aplikacja (DLL, .NET, Windows App SDK, języki) jest w podfolderze `app`.

## [1.0.1] – 2026-10-08

### Zmienione
- Domyślna intensywność to teraz 50%, a domyślny czas przejścia 500 ms (dotyczy nowych instalacji)

### Poprawione
- Otwarcie okna ustawień resetowało intensywność do 5% i czas przejścia do 50 ms

## [1.0.0] – 2026-10-08

Pierwsze wydanie.

### Dodane
- Przyciemnianie wszystkich okien poza aktywnym półprzezroczystą zasłoną, przez którą przechodzą kliknięcia
- Regulacja intensywności (5–90%) i koloru zasłony, z gotowymi kolorami do wyboru
- Płynne przejścia przy zmianie okna, z regulowanym czasem trwania
- Przyciemnianie wszystkich monitorów albo tylko tego z aktywnym oknem
- Ikona w zasobniku systemowym z menu kontekstowym
- Globalny skrót Ctrl+Alt+H do włączania i wyłączania przyciemniania
- Opcjonalne uruchamianie razem z Windows (od razu w zasobniku)
- Działa tylko jedna kopia aplikacji
- 14 języków interfejsu przełączanych na żywo: EN, PL, DE, FR, ES, IT, PT, NL, UK, RU, TR, JA, KO, ZH-Hans
- Wersje dla x64 i ARM64, nie wymagają instalowania .NET ani Windows App SDK
- Licencja MIT
