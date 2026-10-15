<p align="center">
  <img src="docs/banner.png" alt="FocusHaze – odpowiednik HazeOver dla Windows 11" width="100%">
</p>

<p align="center">
  <a href="https://apps.microsoft.com/detail/9P3J7F0RK3RT"><img src="https://get.microsoft.com/images/pl%20dark.svg" alt="Pobierz z Microsoft" width="200"></a>
</p>

<p align="center">
  <a href="CHANGELOG.md"><img src="https://img.shields.io/github/v/release/pietruh00s/FocusHaze?style=for-the-badge&label=wersja&color=0078D4" alt="Wersja"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/pietruh00s/FocusHaze?style=for-the-badge&label=licencja&color=0078D4" alt="Licencja: MIT"></a>
  <a href="https://buymeacoffee.com/piotrosika"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-ffdd00?style=for-the-badge&logo=buymeacoffee&logoColor=black" alt="Buy Me a Coffee"></a>
</p>

<p align="center"><a href="README.md">English</a> | <b>Polski</b></p>

**FocusHaze** (dawniej **WinDimmer**) to odpowiednik [HazeOver](https://hazeover.com/) dla Windows 11, napisany w WinUI 3: przyciemnia wszystkie okna poza aktywnym, żeby łatwiej było się skupić.

<p align="center">
  <img src="docs/screenshot.png" alt="Okno ustawień FocusHaze" width="480">
</p>

## Pobieranie

FocusHaze jest dostępny w **[Microsoft Store](https://apps.microsoft.com/detail/9P3J7F0RK3RT)**: instalacja jednym kliknięciem i automatyczne aktualizacje, na komputery x64 i ARM64.

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

Historia zmian jest w [CHANGELOG.md](CHANGELOG.md).

Ustawienia są zapisywane w `%LOCALAPPDATA%\FocusHaze\settings.json`.


## Jak to działa

- `Core/DimOverlay.cs`: natywne okno Win32 (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`) wypełnione jednolitym kolorem, z przezroczystością ustawianą przez `SetLayeredWindowAttributes`.
- `Core/DimController.cs`: przez `SetWinEventHook` nasłuchuje zmian okna pierwszoplanowego, minimalizacji, zamknięcia i ukrycia okien, a potem `SetWindowPos` wstawia zasłonę w Z-order **bezpośrednio pod** aktywnym oknem. Wszystko, co leży pod spodem, zostaje przyciemnione. Gdy aktywny jest pulpit, zasłona znika. Pasek zadań, przełącznik Alt+Tab i menu Start są ignorowane.
- `Core/TrayHost.cs`: ukryte okno, które obsługuje ikonę w zasobniku, menu kontekstowe, skrót globalny i sygnał od kolejnej instancji.
- `MainWindow.xaml`: okno ustawień w WinUI 3 (Mica, własny pasek tytułu).

## Wsparcie

Jeśli FocusHaze pomaga Ci się skupić, możesz postawić mi kawę ☕

<a href="https://buymeacoffee.com/piotrosika"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me a Coffee" height="48"></a>

## Prywatność

FocusHaze nie zbiera żadnych danych i nie łączy się z internetem. Szczegóły: [PRIVACY.md](PRIVACY.md).

## Licencja

Projekt jest udostępniony na licencji [MIT](LICENSE).

