using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace PersonelYonetim.Infrastructure.InformationNotes;

public sealed record NoteInput(string Title, Guid? EventId, string DateText, string Organizer, string Subject, string PreparedBy, string Body);

public static class InformationNoteDocuments
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    // Fill only the variable cells; all branding, styles and fixed institution text remain in the supplied template.
    public static byte[] CreateWord(string date, string subject, string body)
    {
        using var template = typeof(InformationNoteDocuments).Assembly.GetManifestResourceStream(
            "PersonelYonetim.Infrastructure.InformationNotes.Bilgi_Notu_Bos_Sablon.docx")
            ?? throw new InvalidOperationException("Bilgi notu şablonu bulunamadı.");
        using var output = new MemoryStream();
        template.CopyTo(output);
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("word/document.xml")!;
            XDocument doc;
            using (var stream = entry.Open()) doc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var values = new Dictionary<string, string> { ["Tarih"] = date, ["Konu"] = subject, ["Açıklama"] = body };
            foreach (var row in doc.Descendants(w + "tr"))
            {
                var cells = row.Elements(w + "tc").ToArray();
                if (cells.Length != 2) continue;
                var label = string.Concat(cells[0].Descendants(w + "t").Select(t => t.Value)).Trim();
                if (!values.TryGetValue(label, out var value)) continue;
                var cell = cells[1];
                var prototype = cell.Element(w + "p")!;
                var paragraphProperties = prototype.Element(w + "pPr");
                var runProperties = prototype.Element(w + "r")?.Element(w + "rPr");
                cell.Elements(w + "p").Remove();
                foreach (var line in value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                    cell.Add(new XElement(w + "p", paragraphProperties is null ? null : new XElement(paragraphProperties),
                        new XElement(w + "r", runProperties is null ? null : new XElement(runProperties),
                            new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), line))));
            }
            using var target = entry.Open(); target.SetLength(0); doc.Save(target);
        }
        return output.ToArray();
    }

    public static string ValidateFile(byte[] data, string name)
    {
        if (data.Length is 0 or > MaxBytes) throw new ArgumentException("Dosya boş olamaz ve en fazla 5 MB olabilir.");
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (ext == ".pdf" && data.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        if (ext != ".docx") throw new ArgumentException("Word (.docx) veya PDF dosyası seçin.");
        try
        {
            using var zip = new ZipArchive(new MemoryStream(data));
            if (zip.Entries.Count > 1000 || zip.Entries.Sum(e => e.Length) > 20 * 1024 * 1024
                || zip.GetEntry("word/document.xml") is null || zip.GetEntry("[Content_Types].xml") is null
                || zip.Entries.Any(e => e.FullName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Word dosyası geçerli değil veya izin verilen boyutu aşıyor.");
            return DocxType;
        }
        catch (InvalidDataException) { throw new ArgumentException("Dosya içeriği Word biçimiyle eşleşmiyor."); }
    }

    public static NoteInput ReadWord(byte[] data, string name)
    {
        if (ValidateFile(data, name) != DocxType) throw new ArgumentException("Otomatik alan okuma için Word (.docx) yükleyin. PDF notunu metin alanlarıyla ekleyebilirsiniz.");
        using var zip = new ZipArchive(new MemoryStream(data));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 5_000_000 });
        var document = XDocument.Load(reader); XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        string Text(XElement e) => string.Join("\n", e.Descendants(w + "p").Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))).Where(s => !string.IsNullOrWhiteSpace(s)));
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in document.Descendants(w + "tr"))
        {
            var cells = row.Elements(w + "tc").ToList();
            if (cells.Count == 2) fields[Text(cells[0]).Trim()] = Text(cells[1]).Trim();
        }
        string Field(string key) => fields.GetValueOrDefault(key, "");
        var subject = Field("Konu");
        var body = Field("Açıklama");
        if (body.Length == 0) body = Text(document.Root!);
        if (body.Length > 30000) throw new ArgumentException("Bilgi notu en fazla 30.000 karakter olabilir.");
        return new NoteInput(subject.Length > 0 ? subject : Path.GetFileNameWithoutExtension(name), null, Field("Tarih"), Field("Düzenleyen Kurum"), subject, Field("Hazırlayan"), body);
    }
}
