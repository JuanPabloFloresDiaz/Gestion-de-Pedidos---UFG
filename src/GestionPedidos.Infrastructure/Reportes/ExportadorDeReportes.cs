using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using GestionPedidos.Application.Reportes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionPedidos.Infrastructure.Reportes;

/// <summary>Convierte un <see cref="TablaReporte"/> en PDF (QuestPDF), Excel (ClosedXML) o CSV.</summary>
public sealed class ExportadorDeReportes : IExportadorDeReportes
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-SV");

    static ExportadorDeReportes() => QuestPDF.Settings.License = LicenseType.Community;   // licencia gratuita para uso educativo y proyectos pequeños

    /// <inheritdoc/>
    public ArchivoReporte Exportar(TablaReporte tabla, FormatoReporte formato) => formato switch
    {
        FormatoReporte.Pdf => new ArchivoReporte(Pdf(tabla), "application/pdf", "reporte.pdf"),
        FormatoReporte.Excel => new ArchivoReporte(Excel(tabla), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "reporte.xlsx"),
        _ => new ArchivoReporte(Csv(tabla), "text/csv; charset=utf-8", "reporte.csv"),
    };

    private static string Texto(object? valor) => valor switch
    {
        null => "",
        decimal d => d.ToString("N2", Cultura),
        int i => i.ToString("N0", Cultura),
        long l => l.ToString("N0", Cultura),
        _ => valor.ToString() ?? "",
    };

    // ---------------- CSV ----------------

    /// <summary>
    /// CSV con BOM UTF-8 (para que Excel muestre bien las tildes), valores entre comillas cuando hace falta y neutralización de
    /// «inyección de fórmulas»: una celda de texto que empieza por = + - @ se escribe con un apóstrofo delante, así Excel no la ejecuta.
    /// </summary>
    private static byte[] Csv(TablaReporte t)
    {
        var sb = new StringBuilder();
        // Separador de línea fijo \r\n (RFC 4180): no depende del sistema operativo donde corre la API.
        sb.Append(string.Join(',', t.Columnas.Select(c => Campo(c.Titulo, false)))).Append("\r\n");
        foreach (var fila in t.Filas)
            sb.Append(string.Join(',', fila.Select((v, i) => Campo(ValorCsv(v), t.Columnas[i].Numerica)))).Append("\r\n");
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // Los números van con punto decimal y sin separador de miles, para que cualquier programa los lea como número.
    private static string ValorCsv(object? v) => v switch
    {
        null => "",
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        int or long => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "",
        _ => v.ToString() ?? "",
    };

    private static string Campo(string valor, bool numerico)
    {
        if (!numerico && valor.Length > 0 && "=+-@\t\r".Contains(valor[0])) valor = "'" + valor;
        return valor.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + valor.Replace("\"", "\"\"") + "\"" : valor;
    }

    // ---------------- Excel ----------------

    private static byte[] Excel(TablaReporte t)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add(NombreDeHoja(t.Titulo));
        hoja.Cell(1, 1).Value = t.Titulo;
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;
        hoja.Cell(2, 1).Value = t.Descripcion;
        hoja.Cell(3, 1).Value = "Generado el " + t.GeneradoEn.ToOffset(TimeSpan.FromHours(-6)).ToString("dd/MM/yyyy HH:mm", Cultura) + " (hora de El Salvador)";

        const int filaEncabezado = 5;
        for (var c = 0; c < t.Columnas.Count; c++)
        {
            var celda = hoja.Cell(filaEncabezado, c + 1);
            celda.Value = t.Columnas[c].Titulo;
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F5BEA");
            celda.Style.Font.FontColor = XLColor.White;
            if (t.Columnas[c].Numerica) celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }
        for (var f = 0; f < t.Filas.Count; f++)
            for (var c = 0; c < t.Columnas.Count; c++)
            {
                var celda = hoja.Cell(filaEncabezado + 1 + f, c + 1);
                switch (t.Filas[f][c])
                {
                    case decimal d: celda.Value = d; celda.Style.NumberFormat.Format = "#,##0.00"; break;
                    case int i: celda.Value = i; celda.Style.NumberFormat.Format = "#,##0"; break;
                    case long l: celda.Value = l; celda.Style.NumberFormat.Format = "#,##0"; break;
                    case var otro:
                        // Texto siempre como texto: así una celda que empiece por «=» nunca se interpreta como fórmula.
                        celda.SetValue(otro?.ToString() ?? "");
                        celda.Style.NumberFormat.Format = "@";
                        break;
                }
            }
        if (t.Nota is not null) hoja.Cell(filaEncabezado + t.Filas.Count + 2, 1).Value = t.Nota;
        hoja.Columns().AdjustToContents(1, filaEncabezado + Math.Min(t.Filas.Count, 200), 8, 60);
        hoja.SheetView.FreezeRows(filaEncabezado);

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    // Excel limita el nombre de la hoja a 31 caracteres y prohíbe ciertos símbolos.
    private static string NombreDeHoja(string titulo) => new string(titulo.Where(ch => !"[]:*?/\\".Contains(ch)).Take(31).ToArray());

    // ---------------- PDF ----------------

    private static byte[] Pdf(TablaReporte t)
    {
        var apaisado = t.Columnas.Count > 4;
        return Document.Create(doc => doc.Page(pagina =>
        {
            pagina.Size(apaisado ? PageSizes.A4.Landscape() : PageSizes.A4);
            pagina.Margin(28);
            pagina.DefaultTextStyle(x => x.FontSize(9));
            pagina.Header().Column(col =>
            {
                col.Item().Text(t.Titulo).FontSize(16).Bold().FontColor("#2F5BEA");
                col.Item().Text(t.Descripcion).FontColor(Colors.Grey.Darken2);
                col.Item().PaddingBottom(6).Text("Generado el " + t.GeneradoEn.ToOffset(TimeSpan.FromHours(-6)).ToString("dd/MM/yyyy HH:mm", Cultura) + " (hora de El Salvador)")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            pagina.Content().Column(col =>
            {
                col.Item().Table(tabla =>
                {
                    tabla.ColumnsDefinition(c => { foreach (var columna in t.Columnas) c.RelativeColumn(columna.Numerica ? 0.8f : 1.4f); });
                    tabla.Header(h =>
                    {
                        foreach (var columna in t.Columnas)
                            Alinear(h.Cell(), columna.Numerica).Background("#2F5BEA").Padding(4).Text(columna.Titulo).Bold().FontColor(Colors.White);
                    });
                    for (var f = 0; f < t.Filas.Count; f++)
                        for (var c = 0; c < t.Columnas.Count; c++)
                            Alinear(tabla.Cell(), t.Columnas[c].Numerica).Background(f % 2 == 0 ? Colors.White : Colors.Grey.Lighten4)
                                .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(Texto(t.Filas[f][c]));
                });
                if (t.Filas.Count == 0) col.Item().PaddingTop(10).Text("No hay datos para mostrar.").Italic();
                if (t.Nota is not null) col.Item().PaddingTop(8).Text(t.Nota).Italic().FontColor(Colors.Grey.Darken2);
            });
            pagina.Footer().AlignCenter().Text(x =>
            {
                x.Span("NoviXtore · página ").FontSize(8);
                x.CurrentPageNumber().FontSize(8);
                x.Span(" de ").FontSize(8);
                x.TotalPages().FontSize(8);
            });
        })).GeneratePdf();
    }

    private static IContainer Alinear(IContainer c, bool numerica) => numerica ? c.AlignRight() : c.AlignLeft();
}
