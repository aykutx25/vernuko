using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Vernuko.Application;
using Vernuko.Core;

namespace Vernuko.Desktop.Services;

/// <summary>
/// Lays out a service ticket's kabul belgesi or servis fişi as a FlowDocument and prints it through the Windows print
/// dialog (any printer, or "Microsoft Print to PDF"). The unlock code is never part of <see cref="ServiceSlipDto"/>.
/// </summary>
internal static class ServiceSlipPrinter
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static bool Print(ServiceSlipDto slip)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var doc = Build(slip);
        doc.PageWidth = dialog.PrintableAreaWidth;
        doc.PageHeight = dialog.PrintableAreaHeight;
        doc.ColumnWidth = dialog.PrintableAreaWidth;
        dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"{Title(slip)} {slip.Number}");
        return true;
    }

    internal static FlowDocument Build(ServiceSlipDto s)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            PagePadding = new Thickness(48),
        };

        // Business header
        var head = new Paragraph { Margin = new Thickness(0, 0, 0, 2) };
        head.Inlines.Add(new Run(s.BusinessName) { FontSize = 16, FontWeight = FontWeights.SemiBold });
        doc.Blocks.Add(head);
        var contact = string.Join("  ", new[] { s.BusinessAddress, s.BusinessPhone, s.BusinessTaxText }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (contact.Length > 0)
        {
            doc.Blocks.Add(new Paragraph(new Run(contact)) { Margin = new Thickness(0, 0, 0, 10), Foreground = Brushes.DimGray });
        }

        var title = new Paragraph { Margin = new Thickness(0, 4, 0, 10), BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 4) };
        title.Inlines.Add(new Run(Title(s)) { FontSize = 14, FontWeight = FontWeights.Bold });
        title.Inlines.Add(new Run($"    No: {s.Number}") { FontSize = 12 });
        doc.Blocks.Add(title);

        var info = Table(2);
        Row(info, "Müşteri", s.CustomerName);
        Row(info, "Telefon", s.CustomerPhone);
        Row(info, "Adres", s.CustomerAddress);
        Row(info, "Cihaz", s.DeviceText);
        Row(info, "Seri / IMEI", s.SerialNumber);
        Row(info, "Arıza (müşterinin beyanı)", s.Complaint);
        Row(info, "Aksesuarlar", s.Accessories);
        Row(info, "Teslim alınırken durumu", s.Condition);
        Row(info, "Getiren", s.BroughtBy);
        Row(info, "Kabul tarihi", s.ReceivedAtLocal.ToString("d MMMM yyyy HH:mm", Tr));
        Row(info, "Teslim alan personel", s.ReceivedBy);
        Row(info, "Tahmini teslim", s.DueDate.ToString("d MMMM yyyy", Tr));
        if (s.Kind == SlipKind.Delivery)
        {
            Row(info, "Tespit", s.Diagnosis);
            Row(info, "Yapılan işlem", s.WorkDone);
            Row(info, "Sonuç", s.Repaired ? "Onarıldı" : $"Onarılmadı{(string.IsNullOrWhiteSpace(s.UnrepairableReason) ? string.Empty : ": " + s.UnrepairableReason)}");
            Row(info, "Teslim tarihi", s.DeliveredAtLocal?.ToString("d MMMM yyyy HH:mm", Tr));
            Row(info, "Teslim alan", s.DeliveredTo);
        }

        doc.Blocks.Add(info);

        if (s.Lines.Count > 0)
        {
            var lines = Table(4);
            lines.Columns[0].Width = new GridLength(3, GridUnitType.Star);
            lines.Columns[1].Width = new GridLength(0.6, GridUnitType.Star);
            lines.Columns[2].Width = new GridLength(1, GridUnitType.Star);
            lines.Columns[3].Width = new GridLength(1, GridUnitType.Star);
            var group = lines.RowGroups[0];
            group.Rows.Add(Cells(true, "Parça / işçilik", "Adet", "Birim (KDV dahil)", "Tutar"));
            foreach (var l in s.Lines)
            {
                group.Rows.Add(Cells(false, l.Description, l.Quantity.ToString(Tr), Amount(l.UnitPriceMinor), Amount(l.TotalMinor)));
            }

            group.Rows.Add(Cells(true, "Toplam", string.Empty, string.Empty, Amount(s.TotalMinor)));
            foreach (var row in group.Rows)
            {
                // Quantities and amounts right-aligned, so the digits line up.
                for (var c = 1; c < row.Cells.Count; c++)
                {
                    row.Cells[c].TextAlignment = TextAlignment.Right;
                }
            }
            doc.Blocks.Add(new Paragraph(new Run(s.Kind == SlipKind.Intake ? "Fiyat bilgisi" : "Ücret dökümü") { FontWeight = FontWeights.SemiBold }) { Margin = new Thickness(0, 12, 0, 4) });
            doc.Blocks.Add(lines);
        }

        if (s.SameFaultWarrantyUntil is not null || s.PartsWarrantyUntil is not null)
        {
            var w = new Paragraph { Margin = new Thickness(0, 10, 0, 0) };
            w.Inlines.Add(new Run("Garanti: ") { FontWeight = FontWeights.SemiBold });
            if (s.SameFaultWarrantyUntil is { } a)
            {
                w.Inlines.Add(new Run($"aynı arıza {a.ToString("d MMMM yyyy", Tr)} tarihine kadar ücretsiz onarılır. "));
            }

            if (s.PartsWarrantyUntil is { } b)
            {
                w.Inlines.Add(new Run($"Değiştirilen parçalar {b.ToString("d MMMM yyyy", Tr)} tarihine kadar garantilidir."));
            }

            doc.Blocks.Add(w);
        }

        if (s.Notes.Count > 0)
        {
            var notes = new List { Margin = new Thickness(0, 12, 0, 0), FontSize = 9.5, Foreground = Brushes.DimGray, MarkerStyle = TextMarkerStyle.Disc };
            foreach (var n in s.Notes)
            {
                notes.ListItems.Add(new ListItem(new Paragraph(new Run(n)) { Margin = new Thickness(0) }));
            }

            doc.Blocks.Add(notes);
        }

        var sign = Table(2);
        sign.Margin = new Thickness(0, 28, 0, 0);
        sign.RowGroups[0].Rows.Add(Cells(false, "Müşteri adı, imza", "Servis yetkilisi, imza / kaşe"));
        sign.RowGroups[0].Rows.Add(Cells(false, "\n\n______________________", "\n\n______________________"));
        doc.Blocks.Add(sign);
        return doc;
    }

    private static string Title(ServiceSlipDto s) => s.Kind == SlipKind.Intake ? "Cihaz Kabul Belgesi" : "Servis Fişi";

    private static string Amount(long minor) => new Money(minor).ToDisplayString();

    private static Table Table(int columns)
    {
        var t = new Table { CellSpacing = 0, Margin = new Thickness(0) };
        for (var i = 0; i < columns; i++)
        {
            // Two columns: label | value (the value gets the room); other tables set their own widths.
            t.Columns.Add(new TableColumn { Width = new GridLength(columns == 2 && i == 1 ? 2.2 : 1, GridUnitType.Star) });
        }

        t.RowGroups.Add(new TableRowGroup());
        return t;
    }

    private static void Row(Table t, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run(label)) { Foreground = Brushes.DimGray, Margin = new Thickness(0, 1, 8, 1) }));
        row.Cells.Add(new TableCell(new Paragraph(new Run(value)) { Margin = new Thickness(0, 1, 0, 1) }));
        t.RowGroups[0].Rows.Add(row);
    }

    private static TableRow Cells(bool bold, params string[] values)
    {
        var row = new TableRow();
        foreach (var v in values)
        {
            row.Cells.Add(new TableCell(new Paragraph(new Run(v)) { Margin = new Thickness(0, 2, 6, 2) })
            {
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                BorderBrush = Brushes.Silver,
                BorderThickness = new Thickness(0, 0, 0, 0.5),
            });
        }

        return row;
    }
}
