using System.Globalization;
using System.Reflection;
using System.Text;
using ClosedXML.Excel;
using GestionPedidos.Application.Reportes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionPedidos.Infrastructure.Reportes;

/// <summary>
/// Convierte un <see cref="TablaReporte"/> en PDF (QuestPDF), Excel (ClosedXML) o CSV. El PDF y el Excel llevan el logo de la tienda y el
/// color de acento que la persona tiene elegido en la aplicación; el CSV es solo datos (sin colores ni logo).
/// </summary>
public sealed class ExportadorDeReportes : IExportadorDeReportes
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-SV");
    private static readonly Lazy<byte[]> Logo = new(CargarLogo);   // se lee del ensamblado una sola vez

    static ExportadorDeReportes() => QuestPDF.Settings.License = LicenseType.Community;   // licencia gratuita para uso educativo y proyectos pequeños

    /// <inheritdoc/>
    public ArchivoReporte Exportar(TablaReporte tabla, FormatoReporte formato, string? acento = null)
    {
        var color = AcentosDeReporte.Color(acento);
        return formato switch
        {
            FormatoReporte.Pdf => new ArchivoReporte(Pdf(tabla, color), "application/pdf", "reporte.pdf"),
            FormatoReporte.Excel => new ArchivoReporte(Excel(tabla, color), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "reporte.xlsx"),
            _ => new ArchivoReporte(Csv(tabla), "text/csv; charset=utf-8", "reporte.csv"),
        };
    }

    // El logo vive dentro del ensamblado (EmbeddedResource) para que el reporte no dependa de ningún archivo en disco.
    private static byte[] CargarLogo()
    {
        using var flujo = typeof(ExportadorDeReportes).Assembly.GetManifestResourceStream("logo-reporte.png")
            ?? throw new InvalidOperationException("Falta el logo de los reportes (logo-reporte.png) en el ensamblado.");
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return memoria.ToArray();
    }

    private static string Texto(object? valor) => valor switch
    {
        null => "",
        decimal d => d.ToString("N2", Cultura),
        int i => i.ToString("N0", Cultura),
        long l => l.ToString("N0", Cultura),
        _ => valor.ToString() ?? "",
    };

    private static string Generado(TablaReporte t) =>
        "Generado el " + t.GeneradoEn.ToOffset(TimeSpan.FromHours(-6)).ToString("dd/MM/yyyy HH:mm", Cultura) + " (hora de El Salvador)";

    // Cantidad de columnas de la tabla más ancha del reporte: decide si el PDF va apaisado y cuántas columnas ocupa la hoja de Excel.
    private static int AnchoMaximo(TablaReporte t) =>
        Math.Max(t.Columnas.Count, t.Secciones?.Select(s => s.Columnas.Count).DefaultIfEmpty(0).Max() ?? 0);

    // ---------------- CSV ----------------

    /// <summary>
    /// CSV con BOM UTF-8 (para que Excel muestre bien las tildes), valores entre comillas cuando hace falta y neutralización de
    /// «inyección de fórmulas»: una celda de texto que empieza por = + - @ se escribe con un apóstrofo delante, así Excel no la ejecuta.
    /// Un reporte de una sola tabla sale solo con su encabezado y sus filas (listo para importar); un reporte con bloques sale como
    /// documento: título, ficha, indicadores, la tabla principal y cada bloque con su título.
    /// </summary>
    private static byte[] Csv(TablaReporte t)
    {
        var sb = new StringBuilder();
        // Separador de línea fijo \r\n (RFC 4180): no depende del sistema operativo donde corre la API.
        void Linea(params string[] celdas) => sb.Append(string.Join(',', celdas.Select(c => Campo(c, false)))).Append("\r\n");

        void Tabla(IReadOnlyList<ColumnaReporte> columnas, IReadOnlyList<IReadOnlyList<object?>> filas)
        {
            sb.Append(string.Join(',', columnas.Select(c => Campo(c.Titulo, false)))).Append("\r\n");
            foreach (var fila in filas)
                sb.Append(string.Join(',', fila.Select((v, i) => Campo(ValorCsv(v), columnas[i].Numerica)))).Append("\r\n");
        }

        var esDocumento = t.Secciones is { Count: > 0 } || t.Columnas.Count == 0;
        if (esDocumento)
        {
            Linea(t.Titulo);
            Linea(t.Descripcion);
            Linea(Generado(t));
            if (t.Ficha is { Count: > 0 }) { Linea(); foreach (var d in t.Ficha) Linea(d.Etiqueta, d.Valor); }
            if (t.Indicadores is { Count: > 0 }) { Linea(); foreach (var d in t.Indicadores) Linea(d.Etiqueta, d.Valor); }
            if (t.Columnas.Count > 0) { Linea(); Tabla(t.Columnas, t.Filas); }
            foreach (var s in t.Secciones ?? Array.Empty<SeccionReporte>())
            {
                Linea();
                Linea(s.Titulo);
                if (!string.IsNullOrWhiteSpace(s.Subtitulo)) Linea(s.Subtitulo!);
                if (s.Filas.Count == 0) Linea(s.TextoVacio); else Tabla(s.Columnas, s.Filas);
            }
            if (t.Nota is not null) { Linea(); Linea(t.Nota); }
        }
        else Tabla(t.Columnas, t.Filas);
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

    private static byte[] Excel(TablaReporte t, string color)
    {
        var claro = XLColor.FromHtml(AcentosDeReporte.Aclarar(color, 0.88));
        var acento = XLColor.FromHtml(color);
        var columnas = Math.Max(AnchoMaximo(t), 2);

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add(NombreDeHoja(t.Titulo));
        hoja.ShowGridLines = false;
        var fila = 1;
        var ancho = new double[columnas + 1];   // ancho (en caracteres) de cada columna, según lo más largo que contenga

        void Medir(int columna, string? texto) => ancho[columna] = Math.Max(ancho[columna], Math.Min((texto ?? "").Length + 2, 60));

        // Logo arriba a la izquierda (tres filas altas) y, debajo, la banda con el título.
        hoja.Row(1).Height = 22; hoja.Row(2).Height = 22; hoja.Row(3).Height = 8;
        using (var logo = new MemoryStream(Logo.Value))
            hoja.AddPicture(logo).MoveTo(hoja.Cell(1, 1), 4, 4).WithSize(150, 46);
        fila = 4;

        var banda = hoja.Range(fila, 1, fila, columnas);
        banda.Merge().Style.Fill.BackgroundColor = acento;
        banda.Style.Font.FontColor = XLColor.White;
        banda.Style.Font.Bold = true;
        banda.Style.Font.FontSize = 16;
        banda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hoja.Cell(fila, 1).SetValue(t.Titulo);
        hoja.Row(fila).Height = 30;
        fila++;
        hoja.Cell(fila, 1).SetValue(t.Descripcion).Style.Font.FontColor = XLColor.FromHtml("#4B5563");
        hoja.Range(fila, 1, fila, columnas).Merge().Style.Alignment.WrapText = true;
        hoja.Row(fila).Height = Math.Max(16, 15 * Math.Ceiling(t.Descripcion.Length / (columnas * 14.0)));
        fila++;
        hoja.Cell(fila, 1).SetValue(Generado(t)).Style.Font.FontColor = XLColor.FromHtml("#6B7280");
        hoja.Cell(fila, 1).Style.Font.FontSize = 9;
        fila += 2;

        // Ficha: pares «etiqueta / valor».
        if (t.Ficha is { Count: > 0 } ficha)
        {
            foreach (var d in ficha)
            {
                var e = hoja.Cell(fila, 1); e.SetValue(d.Etiqueta); e.Style.Font.Bold = true; e.Style.Fill.BackgroundColor = claro;
                var v = hoja.Cell(fila, 2); v.SetValue(d.Valor); v.Style.NumberFormat.Format = "@";
                Medir(1, d.Etiqueta); Medir(2, d.Valor);
                fila++;
            }
            fila++;
        }

        // Indicadores: un bloque de «etiqueta / valor» con el valor resaltado.
        if (t.Indicadores is { Count: > 0 } indicadores)
        {
            foreach (var d in indicadores)
            {
                var e = hoja.Cell(fila, 1); e.SetValue(d.Etiqueta); e.Style.Font.Bold = true; e.Style.Fill.BackgroundColor = claro;
                var v = hoja.Cell(fila, 2); v.SetValue(d.Valor); v.Style.NumberFormat.Format = "@";
                v.Style.Font.Bold = true; v.Style.Font.FontColor = acento; v.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                Medir(1, d.Etiqueta); Medir(2, d.Valor);
                fila++;
            }
            fila++;
        }

        // Tabla principal.
        var encabezadoPrincipal = fila;   // se usa para congelar el encabezado cuando el reporte es de una sola tabla
        if (t.Columnas.Count > 0)
        {
            fila = EscribirTabla(hoja, fila, t.Columnas, t.Filas, acento, Medir);
            fila++;
        }

        // Bloques: título sobre fondo suave, su tabla (o el aviso de que está vacío).
        foreach (var s in t.Secciones ?? Array.Empty<SeccionReporte>())
        {
            var cabecera = hoja.Range(fila, 1, fila, columnas);
            cabecera.Merge().Style.Fill.BackgroundColor = claro;
            cabecera.Style.Font.Bold = true; cabecera.Style.Font.FontColor = acento; cabecera.Style.Font.FontSize = 12;
            hoja.Cell(fila, 1).SetValue(s.Titulo);
            hoja.Row(fila).Height = 22;
            fila++;
            if (!string.IsNullOrWhiteSpace(s.Subtitulo))
            {
                hoja.Cell(fila, 1).SetValue(s.Subtitulo!).Style.Font.FontColor = XLColor.FromHtml("#6B7280");
                hoja.Cell(fila, 1).Style.Font.FontSize = 9;
                fila++;
            }
            if (s.Filas.Count == 0) { hoja.Cell(fila, 1).SetValue(s.TextoVacio).Style.Font.Italic = true; fila++; }
            else fila = EscribirTabla(hoja, fila, s.Columnas, s.Filas, acento, Medir);
            fila++;
        }

        if (t.Nota is not null) { hoja.Cell(fila, 1).SetValue(t.Nota).Style.Font.Italic = true; }

        for (var c = 1; c <= columnas; c++) hoja.Column(c).Width = Math.Max(ancho[c], 10);
        // Para imprimir: apaisado si hay columnas de sobra y ajustado al ancho de una hoja.
        hoja.PageSetup.PageOrientation = columnas > 4 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        hoja.PageSetup.FitToPages(1, 0);
        if (t.Secciones is not { Count: > 0 } && t.Columnas.Count > 0) hoja.SheetView.FreezeRows(encabezadoPrincipal);

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    private static int EscribirTabla(IXLWorksheet hoja, int fila, IReadOnlyList<ColumnaReporte> columnas,
        IReadOnlyList<IReadOnlyList<object?>> filas, XLColor acento, Action<int, string?> medir)
    {
        for (var c = 0; c < columnas.Count; c++)
        {
            var celda = hoja.Cell(fila, c + 1);
            celda.SetValue(columnas[c].Titulo);
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = acento;
            celda.Style.Font.FontColor = XLColor.White;
            if (columnas[c].Numerica) celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            medir(c + 1, columnas[c].Titulo);
        }
        for (var f = 0; f < filas.Count; f++)
            for (var c = 0; c < columnas.Count; c++)
            {
                var celda = hoja.Cell(fila + 1 + f, c + 1);
                switch (filas[f][c])
                {
                    case decimal d: celda.SetValue(d); celda.Style.NumberFormat.Format = "#,##0.00"; medir(c + 1, d.ToString("N2", Cultura)); break;
                    case int i: celda.SetValue(i); celda.Style.NumberFormat.Format = "#,##0"; medir(c + 1, i.ToString("N0", Cultura)); break;
                    case long l: celda.SetValue(l); celda.Style.NumberFormat.Format = "#,##0"; medir(c + 1, l.ToString("N0", Cultura)); break;
                    case var otro:
                        // Texto siempre como texto: así una celda que empiece por «=» nunca se interpreta como fórmula.
                        var texto = otro?.ToString() ?? "";
                        celda.SetValue(texto);
                        celda.Style.NumberFormat.Format = "@";
                        medir(c + 1, texto);
                        break;
                }
                if (f % 2 == 1) celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");
            }
        return fila + filas.Count + 1;
    }

    // Excel limita el nombre de la hoja a 31 caracteres y prohíbe ciertos símbolos.
    private static string NombreDeHoja(string titulo) => new string(titulo.Where(ch => !"[]:*?/\\".Contains(ch)).Take(31).ToArray());

    // ---------------- PDF ----------------

    private static byte[] Pdf(TablaReporte t, string color)
    {
        var claro = AcentosDeReporte.Aclarar(color, 0.9);
        var apaisado = AnchoMaximo(t) > 4;
        return Document.Create(doc => doc.Page(pagina =>
        {
            pagina.Size(apaisado ? PageSizes.A4.Landscape() : PageSizes.A4);
            pagina.Margin(28);
            pagina.DefaultTextStyle(x => x.FontSize(9));
            pagina.Header().ShowOnce().Element(c => Encabezado(c, t, color));
            pagina.Content().PaddingTop(10).Column(col =>
            {
                col.Spacing(12);
                if (t.Ficha is { Count: > 0 } ficha) col.Item().Element(c => Ficha(c, ficha, color, claro));
                if (t.Indicadores is { Count: > 0 } indicadores) col.Item().Element(c => Indicadores(c, indicadores, color, claro));
                if (t.Columnas.Count > 0) col.Item().Element(c => TablaPdf(c, t.Columnas, t.Filas, color, "No hay datos para mostrar."));
                foreach (var s in t.Secciones ?? Array.Empty<SeccionReporte>())
                    col.Item().Element(c => SeccionPdf(c, s, color, claro));
                if (t.Nota is not null) col.Item().Text(t.Nota).Italic().FontColor(Colors.Grey.Darken2);
            });
            pagina.Footer().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text("NoviXtore · " + Generado(t)).FontSize(8).FontColor(Colors.Grey.Darken1);
                r.AutoItem().Text(x =>
                {
                    x.Span("Página ").FontSize(8);
                    x.CurrentPageNumber().FontSize(8);
                    x.Span(" de ").FontSize(8);
                    x.TotalPages().FontSize(8);
                });
            });
        })).GeneratePdf();
    }

    // Banda de color con el título y la descripción, y el logo a la derecha (como una hoja de reporte de oficina).
    private static void Encabezado(IContainer contenedor, TablaReporte t, string color) =>
        contenedor.Row(fila =>
        {
            fila.RelativeItem().Background(color).PaddingVertical(10).PaddingHorizontal(14).Column(col =>
            {
                col.Item().Text(t.Titulo).FontSize(19).Bold().FontColor(Colors.White);
                col.Item().PaddingTop(2).Text(t.Descripcion).FontSize(8.5f).FontColor(Colors.White);
            });
            fila.ConstantItem(120).PaddingLeft(10).AlignMiddle().AlignRight().Height(38).Image(Logo.Value).FitArea();
        });

    // Ficha: datos de lo que se reporta, de dos en dos, con la etiqueta sobre fondo suave.
    private static void Ficha(IContainer contenedor, IReadOnlyList<DatoReporte> datos, string color, string claro) =>
        contenedor.Border(0.5f).BorderColor(claro).Table(tabla =>
        {
            tabla.ColumnsDefinition(c => { c.ConstantColumn(80); c.RelativeColumn(); c.ConstantColumn(80); c.RelativeColumn(); });
            foreach (var d in datos)
            {
                tabla.Cell().Background(claro).Padding(4).Text(d.Etiqueta).Bold().FontColor(color);
                tabla.Cell().Padding(4).Text(d.Valor);
            }
            // Si la cantidad es impar, se completa la última fila para que la tabla quede cuadrada.
            if (datos.Count % 2 == 1) { tabla.Cell().Background(claro).Padding(4).Text(""); tabla.Cell().Padding(4).Text(""); }
        });

    // Indicadores: tarjetas con la cifra grande en el color de acento, hasta cinco por fila (todas del mismo ancho).
    private static void Indicadores(IContainer contenedor, IReadOnlyList<DatoReporte> datos, string color, string claro) =>
        contenedor.Column(col =>
        {
            col.Spacing(6);
            var porFila = Math.Min(datos.Count, 5);
            foreach (var grupo in datos.Chunk(porFila))
                col.Item().Row(fila =>
                {
                    fila.Spacing(6);
                    foreach (var d in grupo)
                        fila.RelativeItem().Background(claro).BorderLeft(3).BorderColor(color).Padding(7).Column(tarjeta =>
                        {
                            tarjeta.Item().Text(d.Valor).FontSize(15).Bold().FontColor(color);
                            tarjeta.Item().Text(d.Etiqueta).FontSize(8).FontColor(Colors.Grey.Darken2);
                        });
                    for (var i = grupo.Length; i < porFila; i++) fila.RelativeItem();   // relleno: las tarjetas siempre del mismo ancho
                });
        });

    // Bloque con título (franja suave con borde de acento), subtítulo y su tabla.
    private static void SeccionPdf(IContainer contenedor, SeccionReporte s, string color, string claro) =>
        contenedor.EnsureSpace(90).Column(col =>
        {
            col.Item().Background(claro).BorderLeft(4).BorderColor(color).PaddingVertical(5).PaddingHorizontal(8).Column(titulo =>
            {
                titulo.Item().Text(s.Titulo).FontSize(12).Bold().FontColor(color);
                if (!string.IsNullOrWhiteSpace(s.Subtitulo)) titulo.Item().Text(s.Subtitulo!).FontSize(8.5f).FontColor(Colors.Grey.Darken2);
            });
            col.Item().PaddingTop(4).Element(c => TablaPdf(c, s.Columnas, s.Filas, color, s.TextoVacio));
        });

    private static void TablaPdf(IContainer contenedor, IReadOnlyList<ColumnaReporte> columnas,
        IReadOnlyList<IReadOnlyList<object?>> filas, string color, string textoVacio)
    {
        if (filas.Count == 0) { contenedor.Text(textoVacio).Italic().FontColor(Colors.Grey.Darken1); return; }
        contenedor.Table(tabla =>
        {
            // Las columnas de texto largo (detalle, descripción, opciones) reciben más ancho que las demás.
            tabla.ColumnsDefinition(c =>
            {
                foreach (var columna in columnas)
                    c.RelativeColumn(columna.Numerica ? 0.8f : EsTextoLargo(columna.Titulo) ? 3f : 1.4f);
            });
            tabla.Header(h =>
            {
                foreach (var columna in columnas)
                    Alinear(h.Cell().Background(color).Padding(4), columna.Numerica).Text(columna.Titulo).Bold().FontColor(Colors.White);
            });
            for (var f = 0; f < filas.Count; f++)
                for (var c = 0; c < columnas.Count; c++)
                    Alinear(tabla.Cell().Background(f % 2 == 0 ? Colors.White : Colors.Grey.Lighten4)
                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4), columnas[c].Numerica).Text(Texto(filas[f][c]));
        });
    }

    private static bool EsTextoLargo(string titulo) =>
        titulo.Contains("Detalle", StringComparison.OrdinalIgnoreCase) || titulo.Contains("Descripción", StringComparison.OrdinalIgnoreCase)
        || titulo.Contains("Opciones", StringComparison.OrdinalIgnoreCase);

    // La alineación va DESPUÉS del fondo y del relleno: así el color cubre toda la celda y no solo el ancho del texto.
    private static IContainer Alinear(IContainer c, bool numerica) => numerica ? c.AlignRight() : c.AlignLeft();
}
