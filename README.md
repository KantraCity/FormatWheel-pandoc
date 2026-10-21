# Format Wheel — расширение Pandoc

Конвертация документов для [Format Wheel](https://github.com/KantraCity/FormatWheel) через [Pandoc](https://pandoc.org).

- **Вход:** DOCX, ODT, RTF, Markdown (MD, TXT), HTML, LaTeX, EPUB, FB2, reStructuredText, Org, Textile, AsciiDoc, MediaWiki, Typst, DocBook, OPML, Jupyter (IPYNB). PPTX и XLSX — если Pandoc 3.8 или новее.
- **Shift — формат:** DOCX, PDF, ODT, RTF, HTML, MD, TXT, EPUB, PPTX. Без выбора всё уходит в DOCX, а сам DOCX — в PDF.
- **Ctrl+Shift — оглавление** из заголовков.
- **Alt+Shift — нумерация разделов** (1., 1.1.).

## Детали

- **PDF** делается без LaTeX: Pandoc собирает HTML со встроенными картинками, а Microsoft Edge (есть в любой Windows 10/11) печатает его в PDF.
- **HTML** — одна страница, картинки и стили внутри файла.
- **MD и TXT из Word:** картинки сохраняются в папку `media` рядом с файлом во временной папке; перетаскивается только сам текст.
- **Таблицы:** CSV и TSV забирает расширение «Данные» (оно раньше по алфавиту). Таблицы внутри DOCX, HTML и Markdown конвертируются как часть документа.

## Установка

1. Сначала поставьте саму программу Format Wheel.
2. Запустите `FormatWheel-Pandoc-Setup-x.y.z.exe` из релизов. Если Pandoc ещё не установлен, установщик поставит его через winget (`JohnMacFarlane.Pandoc`).
3. В Format Wheel откройте меню трея → «Расширения» → «Перезагрузить расширения» (или перезапустите программу).

Если winget недоступен: `winget install JohnMacFarlane.Pandoc`, затем перезагрузите расширения.

## Сборка

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Нужны [.NET 10 SDK](https://dotnet.microsoft.com/download) и пакет `FormatWheel.Sdk`. На nuget.org его пока нет: возьмите `FormatWheel.Sdk.x.y.z.nupkg` из [релизов FormatWheel-app](https://github.com/KantraCity/FormatWheel-app/releases) (или соберите `dotnet pack src/FormatWheel.Sdk` в репозитории программы) и подключите папку с ним как источник: `dotnet nuget add source C:\путь\к\папке -n formatwheel-sdk`. GitHub Actions делает это сам. Inno Setup скрипт поставит через winget, если его нет.

Лицензия: MIT. Pandoc ставится отдельно и распространяется под GPL.
