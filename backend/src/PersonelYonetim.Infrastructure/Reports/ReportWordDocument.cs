using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using PersonelYonetim.Domain.Entities;
namespace PersonelYonetim.Infrastructure.Reports;

public static class ReportWordDocument
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static XElement P(string text, string style = "Normal") => new(W + "p", new XElement(W + "pPr", new XElement(W + "pStyle", new XAttribute(W + "val", style))), new XElement(W + "r", new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
    private static XElement Table(string[] headings, IEnumerable<string[]> values)
    {
        var rows = new[] { headings }.Concat(values).Select((cells, index) => new XElement(W + "tr",
            new XElement(W + "trPr", new XElement(W + "cantSplit"), index == 0 ? new XElement(W + "tblHeader") : null),
            cells.Select((cell, i) => new XElement(W + "tc",
                new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", i == 0 ? "5400" : "2100"), new XAttribute(W + "type", "dxa")), index == 0 ? new XElement(W + "shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "fill", "EDF3F0")) : null), P(cell, index == 0 ? "TableHeading" : "Normal")))));
        return new(W + "tbl", new XElement(W + "tblPr", new XElement(W + "tblW", new XAttribute(W + "w", "0"), new XAttribute(W + "type", "auto")), new XElement(W + "tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(x => new XElement(W + x, new XAttribute(W + "w", "100"), new XAttribute(W + "type", "dxa"))))), new XElement(W + "tblGrid", headings.Select((_, i) => new XElement(W + "gridCol", new XAttribute(W + "w", i == 0 ? "5400" : "2100")))), rows);
    }
    public static byte[] Summary(ReportSummary data)
    {
        var title = data.Type == "facility" ? "Tesis raporu özeti" : "Etkinlik ve proje raporu";
        var body = new List<XElement> { P("ŞEHİTKAMİL BELEDİYESİ", "Subtitle"), P("Kültür, Sanat ve Sosyal İşler Müdürlüğü", "Subtitle"), P(title, "Title"), P($"{data.FromDate:dd.MM.yyyy} – {data.ToDate:dd.MM.yyyy}", "Subtitle"),
            P($"Seçilen dönemde {data.RecordCount.ToString("N0", Tr)} {(data.Type == "facility" ? "aylık tesis kaydında" : "gerçekleşen etkinlik ve proje kaydında")} toplam {data.Attendance.ToString("N0", Tr)} kayıtlı katılım bulunmaktadır."),
            P("Katılım, kayıtların toplamıdır; tekil vatandaş sayısı değildir. Tesis kullanımı ile etkinlik katılımı ayrı değerlendirilir."), P(data.Type == "facility" ? "Tesislere göre dağılım" : "Programlara göre dağılım", "Heading1"),
            Table([data.Type == "facility" ? "Tesis" : "Program", data.Type == "facility" ? "Aylık kayıt" : "Kayıt", "Kayıtlı katılım"], data.Rows.Select(r => new[] { r.Name, r.RecordCount.ToString("N0", Tr), r.Attendance?.ToString("N0", Tr) ?? "Belirtilmemiş" })), P("Aylık dağılım", "Heading1"),
            Table(["Dönem", "Kayıt", "Kayıtlı katılım"], data.Months.Select(m => new[] { new DateTime(m.Year, m.Month, 1).ToString("MMMM yyyy", Tr), m.RecordCount.ToString("N0", Tr), m.Attendance.ToString("N0", Tr) })) };
        if (data.RecordCount == 0) body.Add(P("Seçilen dönemde kayıt bulunmuyor."));
        body.Add(P(data.Type == "facility" ? "Kaynak: YBS tesis raporu aylık kayıtları. Aynı tesis ve ayda bulunan ayrı kaynak satırları korunmuştur." : "Gerçekleşmiş etkinlikler ve tamamlanan projeler esas alınır. Beklenen katılım, taslak ve iptal edilmiş kayıtlar toplama dahil değildir.", "Subtitle"));
        if (data.SourceUpdatedAtUtc.HasValue) body.Add(P($"Kaynak aktarımı {data.SourceUpdatedAtUtc.Value.AddHours(3):dd.MM.yyyy HH:mm}", "Subtitle"));
        return Package(body, []);
    }
    public static byte[] Activity(ActivityReportDetail detail, IReadOnlyDictionary<Guid, ActivityReportAsset> assets)
    {
        var body = new List<XElement>(); var images = new List<(string Id, byte[] Data, string Type)>();
        foreach (var page in detail.Document.Pages)
        {
            if (body.Count > 0) body.Add(new XElement(W + "p", new XElement(W + "r", new XElement(W + "br", new XAttribute(W + "type", "page")))));
            body.Add(P(detail.Document.Organization, "Subtitle")); body.Add(P(detail.Document.Department, "Subtitle")); body.Add(P(page.Title, "Title"));
            if (page.Meta.Length > 0) body.Add(P(page.Meta, "Subtitle"));
            if (page.Layout != "text") foreach (var id in page.ImageIds)
            {
                if (!assets.TryGetValue(id, out var image)) continue;
                var relId = "image" + (images.Count + 1); images.Add((relId, image.FileData, image.ContentType)); body.Add(Image(relId, image.FileName, image.FileData));
            }
            foreach (var line in page.Body.Split('\n')) body.Add(P(line.TrimEnd('\r')));
            if (page.ShowMonthlyChart)
            {
                var counts = detail.Document.Sources.GroupBy(s => (detail.Report.DateBasis == "record" ? s.RecordedAtUtc : s.DateUtc).AddHours(3).ToString("yyyy-MM")).OrderBy(g => g.Key);
                body.Add(P("Aylık kayıt dağılımı", "Heading1"));
                body.Add(Table(["Dönem", "Kayıt", "Kayıtlı katılım"], counts.Select(g => new[] { g.Key, g.Count().ToString("N0", Tr), g.Sum(s => s.Attendance ?? 0).ToString("N0", Tr) })));
            }
        }
        return Package(body, images);
    }
    private static XElement Image(string id, string name, byte[] bytes)
    {
        double ratio = 1.6;
        if (bytes.Length >= 24 && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)); var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
            if (width > 0 && height > 0) ratio = (double)width / height;
        }
        else if (bytes.Length > 10 && bytes[0] == 255 && bytes[1] == 216)
        {
            for (var at = 2; at + 8 < bytes.Length;)
            {
                if (bytes[at++] != 255) continue;
                while (at < bytes.Length && bytes[at] == 255) at++;
                if (at + 7 >= bytes.Length) break;
                var marker = bytes[at++]; if (marker is 0xD8 or 0xD9) continue;
                var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(at, 2));
                if (length < 2 || at + length > bytes.Length) break;
                if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
                {
                    var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(at + 3, 2)); var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(at + 5, 2));
                    if (height > 0 && width > 0) ratio = (double)width / height; break;
                }
                at += length;
            }
        }
        else if (bytes.Length >= 30 && bytes.AsSpan(12, 4).SequenceEqual("VP8X"u8))
        {
            var width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16); var height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16); ratio = (double)width / height;
        }
        else if (bytes.Length >= 25 && bytes.AsSpan(12, 4).SequenceEqual("VP8L"u8))
        {
            var bits = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(21, 4)); ratio = (double)((bits & 0x3FFF) + 1) / (((bits >> 14) & 0x3FFF) + 1);
        }
        else if (bytes.Length >= 30 && bytes.AsSpan(12, 4).SequenceEqual("VP8 "u8))
        {
            var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26, 2)) & 0x3FFF; var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28, 2)) & 0x3FFF; if (width > 0 && height > 0) ratio = (double)width / height;
        }
        // The extent is bounded in both directions so photos never overrun the page.
        var cy = (long)(Math.Min(2.4, 6.3 / ratio) * 914400); var cx = (long)(cy * ratio);
        var drawing = XElement.Parse($"""<w:drawing xmlns:w="{W}" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture" xmlns:r="{R}"><wp:inline><wp:extent cx="{cx}" cy="{cy}"/><wp:docPr id="{id[5..]}" name="Image" descr="Photo"/><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:nvPicPr><pic:cNvPr id="0" name="Photo"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed="{id}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing>""");
        drawing.Descendants().First(x => x.Name.LocalName == "docPr").SetAttributeValue("descr", name);
        return new(W + "p", new XElement(W + "r", drawing));
    }
    private static byte[] Package(IEnumerable<XElement> paragraphs, IReadOnlyList<(string Id, byte[] Data, string Type)> images)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Xml(string name, XElement root) { using var stream = zip.CreateEntry(name).Open(); new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(stream); }
            var content = new XElement(Ct + "Types", new XElement(Ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(Ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")), new XElement(Ct + "Override", new XAttribute("PartName", "/word/document.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml")), new XElement(Ct + "Override", new XAttribute("PartName", "/word/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")));
            var relationships = new XElement(Rel + "Relationships", new XElement(Rel + "Relationship", new XAttribute("Id", "styles"), new XAttribute("Type", R.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml")));
            foreach (var image in images)
            {
                var ext = image.Type == "image/png" ? "png" : image.Type == "image/webp" ? "webp" : "jpeg"; var path = $"media/{image.Id}.{ext}";
                using (var stream = zip.CreateEntry("word/" + path).Open()) stream.Write(image.Data);
                content.Add(new XElement(Ct + "Override", new XAttribute("PartName", "/word/" + path), new XAttribute("ContentType", image.Type)));
                relationships.Add(new XElement(Rel + "Relationship", new XAttribute("Id", image.Id), new XAttribute("Type", R.NamespaceName + "/image"), new XAttribute("Target", path)));
            }
            Xml("[Content_Types].xml", content);
            Xml("_rels/.rels", new XElement(Rel + "Relationships", new XElement(Rel + "Relationship", new XAttribute("Id", "document"), new XAttribute("Type", R.NamespaceName + "/officeDocument"), new XAttribute("Target", "word/document.xml"))));
            Xml("word/_rels/document.xml.rels", relationships);
            Xml("word/document.xml", new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), new XElement(W + "body", paragraphs, new XElement(W + "sectPr", new XElement(W + "pgSz", new XAttribute(W + "w", "11906"), new XAttribute(W + "h", "16838")), new XElement(W + "pgMar", new XAttribute(W + "top", "1080"), new XAttribute(W + "right", "1080"), new XAttribute(W + "bottom", "1080"), new XAttribute(W + "left", "1080"))))));
            XElement Style(string id, int size, bool bold, string color) => new(W + "style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", id), new XElement(W + "name", new XAttribute(W + "val", id)), id != "Normal" ? new XElement(W + "basedOn", new XAttribute(W + "val", "Normal")) : null,
                new XElement(W + "pPr", id is "Title" or "Heading1" ? new XElement(W + "keepNext") : null, new XElement(W + "spacing", new XAttribute(W + "after", id == "Title" ? "220" : "100"), new XAttribute(W + "line", "270"), new XAttribute(W + "lineRule", "auto"))),
                new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Calibri"), new XAttribute(W + "hAnsi", "Calibri")), bold ? new XElement(W + "b") : null, new XElement(W + "color", new XAttribute(W + "val", color)), new XElement(W + "sz", new XAttribute(W + "val", size)), new XElement(W + "lang", new XAttribute(W + "val", "tr-TR"))));
            Xml("word/styles.xml", new XElement(W + "styles", Style("Normal", 21, false, "24332C"), Style("Title", 34, true, "000000"), Style("Heading1", 25, true, "1F6046"), Style("Subtitle", 19, false, "65726B"), Style("TableHeading", 20, true, "24332C")));
        }
        return ms.ToArray();
    }
}
