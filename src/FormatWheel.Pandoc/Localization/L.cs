using System;
using System.Globalization;

namespace FormatWheel.Pandoc;

// Маленькая локализация расширения. Код языка берём так же, как приложение.
internal static class L
{
    private static readonly bool Ru =
        (Environment.GetEnvironmentVariable("FORMATWHEEL_LANG") ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
        .StartsWith("ru", StringComparison.OrdinalIgnoreCase);

    private static string T(string en, string ru) => Ru ? ru : en;

    public static string Name => T("Documents (Pandoc)", "Документы (Pandoc)");
    public static string ForPrint => T("for print", "для печати");
    public static string AnyEditor => T("any editor", "любой редактор");
    public static string WebPage => T("web page", "веб-страница");
    public static string PlainText => T("plain text", "просто текст");
    public static string EBook => T("e-book", "электронная книга");
    public static string Slides => T("slides", "слайды");

    public static string AsIs => T("as is", "как есть");
    public static string NoToc => T("No contents", "Без оглавления");
    public static string Toc => T("Contents", "Оглавление");
    public static string TocTitle => T("Contents", "Содержание");
    public static string FromHeadings => T("from headings", "из заголовков");
    public static string NoNumbers => T("No numbers", "Без номеров");
    public static string Numbers => T("1. 1.1.", "1. 1.1.");
    public static string NumberedSections => T("numbered sections", "нумерация разделов");

    public static string NoEdge => T("Microsoft Edge is needed to make PDF", "Для PDF нужен Microsoft Edge");
}
