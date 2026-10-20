using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FormatWheel.Sdk;

namespace FormatWheel.Pandoc;

// Ищет программы. PATH читаем свежий из реестра: приложение запущено при входе в систему
// и не увидит Pandoc, поставленный через winget уже после этого.
internal static class Tools
{
    private static string? _pandoc, _edge;

    public static string? Pandoc => _pandoc ??= FindInPath("pandoc.exe")
        ?? Existing(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pandoc", "pandoc.exe"));

    // PDF печатаем через Edge: он есть в любой Windows 10/11, а LaTeX ставить ради этого тяжело
    public static string? Edge => _edge ??= Existing(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe"));

    private static string? Existing(params string[] paths) => paths.FirstOrDefault(File.Exists);

    private static string? FindInPath(string exe)
    {
        var dirs = new List<string>();
        foreach (var target in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            string? path = Environment.GetEnvironmentVariable("PATH", target);
            if (path != null) dirs.AddRange(path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        foreach (string dir in dirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                string candidate = Path.Combine(Environment.ExpandEnvironmentVariables(dir), exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }
}

// Документы через Pandoc: Word, OpenDocument, RTF, Markdown, HTML, LaTeX, EPUB и др.
public sealed class DocumentConverter : IConverter
{
    private const string FormatAxis = "pandoc.format";
    private const string TocAxis = "pandoc.toc";
    private const string NumberAxis = "pandoc.number";

    // лепесток: подпись, подсказка, расширение и писатель Pandoc (null — PDF через HTML и Edge)
    private sealed record Target(string Label, string Hint, string Extension, string? Writer);

    private static readonly Target[] Targets =
    {
        new("DOCX", "Word", "docx", "docx"),
        new("PDF", L.ForPrint, "pdf", null),
        new("ODT", "LibreOffice", "odt", "odt"),
        new("RTF", L.AnyEditor, "rtf", "rtf"),
        new("HTML", L.WebPage, "html", "html5"),
        new("MD", "Markdown", "md", "gfm"),
        new("TXT", L.PlainText, "txt", "plain"),
        new("EPUB", L.EBook, "epub", "epub3"),
        new("PPTX", L.Slides, "pptx", "pptx"),
    };

    // расширение → читатель Pandoc
    private static readonly Dictionary<string, string> Readers = new(StringComparer.OrdinalIgnoreCase)
    {
        [".docx"] = "docx", [".odt"] = "odt", [".rtf"] = "rtf", [".epub"] = "epub", [".fb2"] = "fb2",
        [".md"] = "markdown", [".markdown"] = "markdown", [".txt"] = "markdown",
        [".html"] = "html", [".htm"] = "html", [".xhtml"] = "html",
        [".tex"] = "latex", [".rst"] = "rst", [".org"] = "org", [".textile"] = "textile",
        [".ipynb"] = "ipynb", [".typ"] = "typst", [".dbk"] = "docbook", [".opml"] = "opml",
        [".wiki"] = "mediawiki", [".adoc"] = "asciidoc", [".pptx"] = "pptx", [".xlsx"] = "xlsx",
    };

    // .pptx и .xlsx Pandoc читает только с 3.8 — проверяем один раз и не в UI-потоке
    private static readonly Lazy<HashSet<string>> Supported = new(() =>
    {
        var formats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo(Tools.Pandoc!, "--list-input-formats")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using var p = Process.Start(psi)!;
            formats.UnionWith(p.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            p.WaitForExit();
        }
        catch { }
        return formats;
    });

    public DocumentConverter()
    {
        if (Tools.Pandoc != null) System.Threading.Tasks.Task.Run(() => Supported.Value);
    }

    public string Name => L.Name;
    public bool IsSlow => true; // PDF через Edge — пара секунд
    public int MaxParallel => Math.Max(1, Environment.ProcessorCount / 2);

    public bool CanHandle(string path)
    {
        if (Tools.Pandoc == null || !Readers.TryGetValue(Path.GetExtension(path), out string? reader)) return false;
        // пока список не готов, берём только то, что умеет любой Pandoc 3
        return Supported.IsValueCreated ? Supported.Value.Contains(reader) : reader is not ("pptx" or "xlsx");
    }

    public IReadOnlyList<WheelOption> MenuFor(WheelSlot slot, IReadOnlyList<string> files) => slot switch
    {
        WheelSlot.Primary => Targets.Select(t => new WheelOption(FormatAxis, t, t.Label, t.Hint)).ToList(),
        WheelSlot.Ctrl => new[]
        {
            WheelOption.Reset(TocAxis, L.NoToc, L.AsIs),
            new WheelOption(TocAxis, "toc", L.Toc, L.FromHeadings),
        },
        WheelSlot.Alt => new[]
        {
            WheelOption.Reset(NumberAxis, L.NoNumbers, L.AsIs),
            new WheelOption(NumberAxis, "number", L.Numbers, L.NumberedSections),
        },
        _ => Array.Empty<WheelOption>()
    };

    public string Convert(string source, Recipe recipe, Action<double> report, CancellationToken ct)
    {
        string pandoc = Tools.Pandoc ?? throw new InvalidOperationException("pandoc not found");
        string reader = Readers[Path.GetExtension(source)];
        // по умолчанию в Word, а сам Word — в PDF
        var target = recipe.Get<Target>(FormatAxis) ?? (reader == "docx" ? Targets[1] : Targets[0]);
        string output = OutputPath.New(source, target.Extension);
        string dir = Path.GetDirectoryName(output)!;

        bool pdf = target.Writer == null;
        string pandocOut = pdf ? Path.ChangeExtension(output, ".html") : output;
        var args = new List<string> { "-f", reader, "-t", target.Writer ?? "html5", "-o", pandocOut, source };
        if (recipe.Has(TocAxis)) args.AddRange(new[] { "--toc", "-M", "toc-title=" + L.TocTitle });
        if (recipe.Has(NumberAxis)) args.Add("--number-sections");
        if (pdf || target.Writer == "html5")
            // одна страница со всеми картинками внутри, заголовок — имя файла
            args.AddRange(new[] { "--standalone", "--embed-resources", "-M", "pagetitle=" + Path.GetFileNameWithoutExtension(source) });
        else if (target.Writer is "gfm" or "plain")
            args.Add("--extract-media=" + dir); // картинки из Word рядом с .md
        else if (target.Writer is "rtf")
            args.Add("--standalone");

        Run(pandoc, args, Path.GetDirectoryName(source)!, ct);
        report(pdf ? 0.5 : 1);

        if (pdf)
        {
            string edge = Tools.Edge ?? throw new InvalidOperationException(L.NoEdge);
            // свой профиль: иначе headless цепляется к уже открытому Edge и ничего не печатает
            string profile = Path.Combine(dir, "edge-profile");
            Run(edge, new[]
            {
                "--headless=new", "--disable-gpu", "--no-first-run", "--no-pdf-header-footer",
                "--user-data-dir=" + profile, "--print-to-pdf=" + output, new Uri(pandocOut).AbsoluteUri
            }, dir, ct);
            WaitForFile(output, TimeSpan.FromSeconds(60), ct);
            try { Directory.Delete(profile, recursive: true); File.Delete(pandocOut); } catch { }
        }

        if (!File.Exists(output)) throw new InvalidOperationException("no output file");
        report(1);
        return output;
    }

    // процесс msedge.exe, который мы запустили, выходит раньше, чем дочерний допишет PDF:
    // ждём, пока файл появится и перестанет расти
    private static void WaitForFile(string path, TimeSpan timeout, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        long lastSize = -1;
        while (sw.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(path);
            if (info.Exists && info.Length > 0 && info.Length == lastSize)
            {
                try { using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)) return; } // никто не пишет
                catch (IOException) { }
            }
            lastSize = info.Exists ? info.Length : -1;
            Thread.Sleep(150);
        }
        throw new InvalidOperationException("Edge did not produce the PDF");
    }

    private static void Run(string exe, IEnumerable<string> args, string workDir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workDir, // относительные картинки в .md и .html ищутся от папки исходника
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);

        var errors = new StringBuilder();
        using var process = new Process { StartInfo = psi };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errors.AppendLine(e.Data); };
        process.OutputDataReceived += (_, _) => { };
        process.Start();
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        using (ct.Register(() => { try { process.Kill(entireProcessTree: true); } catch { } }))
            process.WaitForExit();

        ct.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileNameWithoutExtension(exe)} exited with {process.ExitCode}: {errors.ToString().Trim()}");
    }
}
