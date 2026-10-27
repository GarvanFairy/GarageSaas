using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using PdfSharp.Drawing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using GarageSaas.Configuration;

namespace GarageSaas.Services
{
    public sealed class CustomerDocumentPdfRenderer : ICustomerDocumentPdfRenderer
    {
        private static readonly CultureInfo Irish = CultureInfo.GetCultureInfo("en-IE");
        private readonly string _logoDirectory;
        public CustomerDocumentPdfRenderer(IWebHostEnvironment environment = null, IConfiguration configuration = null)
        { _logoDirectory = DeploymentStorage.LogoDirectory(environment, configuration); }
        static CustomerDocumentPdfRenderer() { GlobalFontSettings.FontResolver = new DocumentFontResolver(); }

        public byte[] Generate(CustomerDocumentModel model)
        {
            var document = new Document();
            document.Info.Title = $"{model.Title} {model.Number}";
            document.Info.Author = model.Garage.GarageBusinessName ?? "Garage";
            var normal = document.Styles[StyleNames.Normal];
            normal.Font.Name = "GarageSans"; normal.Font.Size = 10;
            normal.Font.Color = Colors.DarkSlateGray;
            normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(5);
            normal.ParagraphFormat.KeepTogether = false;
            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.TopMargin = Unit.FromCentimeter(2);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(2);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(.8);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(1);
            var header = section.Headers.Primary.AddParagraph($"{model.Title.ToUpperInvariant()}  /  {Clean(model.Number)}");
            header.Format.Font.Size = 8; header.Format.Font.Color = Colors.Gray;
            var footer = section.Footers.Primary.AddParagraph();
            footer.Format.Alignment = ParagraphAlignment.Right; footer.Format.Font.Size = 8;
            footer.AddText("Page "); footer.AddPageField(); footer.AddText(" of "); footer.AddNumPagesField();

            var title = AddTitle(section, model);
            title.Format.Font.Size = 26; title.Format.Font.Bold = true; title.Format.Font.Color = Colors.DarkSlateBlue;
            var garage = model.Garage;
            Heading(section, garage.GarageBusinessName ?? "Garage");
            Details(section, garage.GarageAddressLine1, garage.GarageAddressLine2, garage.GarageAddressLine3,
                garage.GarageAddressLine4, garage.Postcode);
            Details(section, garage.GaragePhoneNumber, garage.GarageMobileNumber, garage.GarageEmailAddress);
            if (!string.IsNullOrWhiteSpace(garage.VatNumber)) Text(section, "VAT registration: " + garage.VatNumber);
            if (!string.IsNullOrWhiteSpace(garage.BusinessRegistrationNumber)) Text(section, "Business registration: " + garage.BusinessRegistrationNumber);
            Heading(section, $"{model.Title} reference: {model.Number}");
            Text(section, "Date: " + Date(model.Date));
            if (model.DueDate.HasValue) Text(section, "Payment due: " + Date(model.DueDate));
            if (!string.IsNullOrWhiteSpace(model.Status)) Text(section, "Payment status: " + model.Status);
            Heading(section, "Customer");
            var customer = model.Customer;
            if (customer == null) Text(section, "Customer details not recorded.");
            else
            {
                Details(section, customer.GarageCustomerForename + " " + customer.GarageCustomerSurname);
                Details(section, customer.GarageCustomerAddressline1, customer.GarageCustomerAddressline2,
                    customer.GarageCustomerAddressline3, customer.GarageCustomerAddressline4, customer.GarageCustomerPostcode);
                Details(section, customer.GarageCustomerEmailAddress, customer.GarageCustomerPhoneNumber, customer.GarageCustomerMobileNumber);
            }
            Heading(section, "Vehicle");
            if (model.Vehicle == null) Text(section, "Vehicle details not recorded.");
            else Details(section, model.Vehicle.VehicleRegistration, model.Vehicle.VehicleMake?.Make, model.Vehicle.VehicleModel?.Model);
            Heading(section, "Work descriptions");
            var descriptions = model.WorkDescriptions.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (descriptions.Count == 0) Text(section, "No work description recorded.");
            foreach (var description in descriptions) Text(section, description);
            Heading(section, "Itemised charges (EUR)");
            var table = section.AddTable();
            table.Borders.Color = Colors.LightGray; table.Borders.Width = .5;
            table.TopPadding = 6; table.BottomPadding = 6; table.LeftPadding = 8; table.RightPadding = 8;
            table.AddColumn(Unit.FromCentimeter(12)); table.AddColumn(Unit.FromCentimeter(5));
            var labels = table.AddRow(); labels.HeadingFormat = true; labels.Shading.Color = Colors.WhiteSmoke;
            labels.Cells[0].AddParagraph("Charge"); labels.Cells[1].AddParagraph("Amount"); labels.Format.Font.Bold = true;
            foreach (var charge in model.Charges) Amount(table, charge.Description, charge.Amount);
            Amount(table, "Subtotal", model.SubTotal);
            var vatLabel = model.SubTotal > 0 && model.Vat.HasValue
                ? $"VAT ({(model.Vat.Value / model.SubTotal.Value * 100).ToString("0.##", Irish)}%)" : "VAT";
            Amount(table, vatLabel, model.Vat);
            var total = Amount(table, "Total (EUR)", model.Total);
            total.Shading.Color = Colors.WhiteSmoke; total.Format.Font.Bold = true;
            if (!string.IsNullOrWhiteSpace(model.Notes)) { Heading(section, "Notes"); Text(section, model.Notes); }
            if (model.Kind == "quote") Text(section, "This is a quote, not an invoice.");
            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();
            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            return stream.ToArray();
        }

        private Paragraph AddTitle(Section section, CustomerDocumentModel model)
        {
            var logo = ReadLogo(model.Garage.Id, model.Garage.LogoImage);
            if (logo == null) return section.AddParagraph(model.Title.ToUpperInvariant());
            var heading = section.AddTable();
            heading.AddColumn(Unit.FromCentimeter(12)); heading.AddColumn(Unit.FromCentimeter(5));
            heading.LeftPadding = 0; heading.RightPadding = 0;
            var row = heading.AddRow();
            var title = row.Cells[0].AddParagraph(model.Title.ToUpperInvariant());
            title.Format.SpaceAfter = 0;
            var imageParagraph = row.Cells[1].AddParagraph();
            imageParagraph.Format.SpaceAfter = 0;
            imageParagraph.Format.Alignment = ParagraphAlignment.Right;
            var image = imageParagraph.AddImage("base64:" + Convert.ToBase64String(logo.Bytes));
            image.LockAspectRatio = true;
            image.Width = Unit.FromPoint(logo.Width);
            image.Height = Unit.FromPoint(logo.Height);
            return title;
        }

        private Logo ReadLogo(int garageId, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(_logoDirectory) || string.IsNullOrWhiteSpace(imagePath)) return null;
            // Only this garage's existing upload format is eligible. Never fetch URLs
            // or resolve arbitrary paths from a stored image reference.
            var pattern = @"^/uploads/garage-logos/(garage-" + garageId.ToString(CultureInfo.InvariantCulture) + @"-[a-fA-F0-9]{32}\.(?:png|jpe?g))$";
            var match = Regex.Match(imagePath, pattern, RegexOptions.IgnoreCase);
            if (!match.Success) return null;
            var path = Path.Combine(_logoDirectory, match.Groups[1].Value);
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 2 * 1024 * 1024) return null;
                var bytes = File.ReadAllBytes(path);
                using var stream = new MemoryStream(bytes);
                using var image = XImage.FromStream(stream);
                var scale = Math.Min(113.38 / image.PointWidth, 28.35 / image.PointHeight); // 4 cm x 1 cm
                return new Logo { Bytes = bytes, Width = image.PointWidth * scale, Height = image.PointHeight * scale };
            }
            catch (Exception) { return null; } // Optional missing/unreadable logo must not block customer documents.
        }

        private sealed class Logo
        {
            public byte[] Bytes { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }

        private static Row Amount(Table table, string label, decimal? amount)
        {
            var row = table.AddRow(); row.Cells[0].AddParagraph(label);
            row.Cells[1].AddParagraph(amount.HasValue ? amount.Value.ToString("C2", Irish) : "Not recorded");
            row.Cells[1].Format.Alignment = ParagraphAlignment.Right;
            return row;
        }
        private static string Date(DateTime? date) => date?.ToString("dd MMM yyyy", Irish) ?? "Not recorded";
        private static void Details(Section section, params string[] values)
        {
            var text = string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
            if (text.Length > 0) Text(section, text);
        }
        private static void Heading(Section section, string text)
        {
            var paragraph = section.AddParagraph(Clean(text));
            paragraph.Format.SpaceBefore = 12; paragraph.Format.Font.Bold = true;
            paragraph.Format.Font.Size = 12; paragraph.Format.KeepWithNext = true;
        }
        private static void Text(Section section, string text) => section.AddParagraph(Clean(text));
        private static string Clean(string text)
        {
            text = new string((text ?? "").Where(c => !char.IsControl(c) || c == '\n' || c == '\t').ToArray());
            // Break unusually long unspaced tokens so pasted URLs/part codes cannot run off the page.
            return Regex.Replace(text, @"\S{61,}", m => string.Join(" ", Enumerable.Range(0, (m.Length + 59) / 60)
                .Select(i => m.Value.Substring(i * 60, Math.Min(60, m.Length - i * 60)))));
        }

        private sealed class DocumentFontResolver : IFontResolver
        {
            public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new FontResolverInfo(bold ? "Bold" : "Regular");
            public byte[] GetFont(string faceName)
            {
                using var resource = typeof(CustomerDocumentPdfRenderer).Assembly.GetManifestResourceStream($"GarageSaas.Resources.Fonts.NotoSans-{faceName}.ttf");
                if (resource == null) throw new InvalidOperationException("Embedded document font is missing.");
                using var stream = new MemoryStream(); resource.CopyTo(stream); return stream.ToArray();
            }
        }
    }
}
