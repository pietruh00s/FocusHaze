# WinDimmer

Odpowiednik [HazeOver](https://hazeover.com/) dla Windows 11, napisany w WinUI 3: przyciemnia wszystkie okna poza aktywnym, żeby łatwiej było się skupić.

## Funkcje

- Półprzezroczysta zasłona pod aktywnym oknem, przepuszczająca kliknięcia
- Regulowana intensywność i kolor zasłony
- Płynne przejścia przy zmianie okna (z regulowanym czasem trwania)
- Przyciemnianie wszystkich monitorów albo tylko tego z aktywnym oknem
- Ikona w zasobniku systemowym: lewy klik otwiera ustawienia, prawy otwiera menu
- Globalny skrót **Ctrl+Alt+H** włącza i wyłącza przyciemnianie
- Opcjonalne uruchamianie razem z Windows (startuje od razu w zasobniku)
- Działa tylko jedna instancja; ponowne uruchomienie otwiera ustawienia działającej aplikacji
- 14 języków interfejsu, przełączanych na żywo: angielski, polski, niemiecki, francuski, hiszpański, włoski, portugalski, niderlandzki, ukraiński, rosyjski, turecki, japoński, koreański i chiński uproszczony. Domyślnie aplikacja używa języka Windows.

## Tłumaczenia

Teksty są w `Localization/Strings.resx` (angielski, język zapasowy) i w `Localization/Strings.<kod>.resx`. Nowy język dodajesz tak:

1. Skopiuj `Strings.resx` jako `Strings.<kod>.resx` (np. `Strings.cs.resx`) i przetłumacz wartości.
2. Dopisz kod języka do `Loc.SupportedLanguages` w `Localization/Loc.cs`.

W XAML tekst podpinasz przez `loc:Localize.Key="NazwaKlucza"`, a w kodzie przez `Loc.Get("NazwaKlucza")`.

## Wymagania

- Windows 10 19041+ / Windows 11
- .NET 10 SDK

## Budowanie i uruchamianie

```bash
dotnet build -c Release
```

```bash
dotnet run
```

Aplikacja jest „unpackaged” i ma dołączony Windows App SDK, więc katalog `bin\Release\net10.0-windows10.0.22621.0\win-x64\` można po prostu skopiować w inne miejsce. Wersję na ARM64 zbudujesz przez `-p:Platform=ARM64`.

Paczki z wydaniem (zip dla x64 i ARM64 oraz plik `SHA256SUMS.txt`) budujesz poleceniem:

```bash
pwsh ./build-release.ps1
```

Gotowe pliki trafią do `artifacts\`. Historia zmian jest w [CHANGELOG.md](CHANGELOG.md).

Ustawienia są zapisywane w `%LOCALAPPDATA%\WinDimmer\settings.json`.

## Jak to działa

- `Core/DimOverlay.cs`: natywne okno Win32 (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`) wypełnione jednolitym kolorem, z przezroczystością ustawianą przez `SetLayeredWindowAttributes`.
- `Core/DimController.cs`: przez `SetWinEventHook` nasłuchuje zmian okna pierwszoplanowego, minimalizacji, zamknięcia i ukrycia okien, a potem `SetWindowPos` wstawia zasłonę w Z-order **bezpośrednio pod** aktywnym oknem. Wszystko, co leży pod spodem, zostaje przyciemnione. Gdy aktywny jest pulpit, zasłona znika. Pasek zadań, przełącznik Alt+Tab i menu Start są ignorowane.
- `Core/TrayHost.cs`: ukryte okno, które obsługuje ikonę w zasobniku, menu kontekstowe, skrót globalny i sygnał od kolejnej instancji.
- `MainWindow.xaml`: okno ustawień w WinUI 3 (Mica, własny pasek tytułu).

## Licencja

Projekt jest udostępniony na licencji [MIT](LICENSE).

