using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;
using System.Collections.Generic; 

// Definizione delle classi per la configurazione del JSON
public class CardConfig
{
    public FontSettings? FontSettings { get; set; }
    // CAMBIATO: Da CardData? a List<CardData>?
    public List<CardData>? CardDataList { get; set; }
    public Images? Images { get; set; }
    public PrinterSettings? PrinterSettings { get; set; }
}

public class FontSettings
{
    public string? BadgeFontName { get; set; }
    public float BadgeFontSizePoints { get; set; }
    public string? BadgeFontStyle { get; set; }
    public string? NameFontName { get; set; }
    public float NameFontSizePoints { get; set; }
    public string? NameFontStyle { get; set; }
    public string? BottomFontName { get; set; }
    public float BottomFontSizePoints { get; set; }
    public string? BottomFontStyle { get; set; }

    public FontStyle GetBadgeFontStyle() => ParseFontStyle(BadgeFontStyle);
    public FontStyle GetNameFontStyle() => ParseFontStyle(NameFontStyle);
    public FontStyle GetBottomFontStyle() => ParseFontStyle(BottomFontStyle);

    private FontStyle ParseFontStyle(string? fontStyle)
    {
        if (Enum.TryParse(fontStyle, out FontStyle style))
        {
            return style;
        }
        return FontStyle.Regular; // Default in case of parsing error
    }
}

public class CardData
{
    public string? EmployeeId { get; set; }
    public string? FullName { get; set; }
    public string? AdditionalText { get; set; }
    public float CardMarginMm { get; set; }
}

public class Images
{
    public string? PhotoPath { get; set; }
    public float PhotoWidthCm { get; set; }
    public float PhotoHeightCm { get; set; }
    public string? LogoPath { get; set; }
    public float LogoWidthCm { get; set; }
    public float LogoHeightCm { get; set; }
}

public class PrinterSettings
{
    public bool EnablePhysicalPrinting { get; set; }
    public string? IpAddress { get; set; }
    public int Port { get; set; }
}


public class Program
{
    private const string ConfigFileName = "card_config.json";
    // Rimosso PreviewFileName costante, ora è dinamico

    [SupportedOSPlatform("windows")]
    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (!File.Exists(ConfigFileName))
        {
            Console.WriteLine($"Errore: File di configurazione '{ConfigFileName}' non trovato nella directory dell'applicazione.");
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(ConfigFileName);

            CardConfig? config = JsonSerializer.Deserialize<CardConfig>(jsonString);

            // CAMBIATO: Ora controlliamo CardDataList
            if (config?.CardDataList == null || config.FontSettings == null || config.Images == null || config.PrinterSettings == null)
            {
                Console.WriteLine("Errore: Il file di configurazione è incompleto o malformato. Verificare tutti i campi, inclusa la sezione 'CardDataList'.");
                return;
            }

            Console.WriteLine("Configurazione caricata con successo.");
            Console.WriteLine($"Trovate {config.CardDataList.Count} tessere da generare.");

            // CAMBIATO: Iteriamo su ogni elemento in CardDataList
            foreach (var cardDataItem in config.CardDataList)
            {
                Console.WriteLine($"Generazione tessera per: {cardDataItem.EmployeeId}");
                DoPrintJob(config, cardDataItem); // Passiamo il singolo cardDataItem
            }

            Console.WriteLine("Generazione tessere completata.");

            if (config.PrinterSettings.EnablePhysicalPrinting)
            {
                Console.WriteLine($"Tentativo di stampa su: {config.PrinterSettings.IpAddress}:{config.PrinterSettings.Port}");
                Console.WriteLine("Stampa fisica non implementata in questo esempio.");
            }
            else
            {
                Console.WriteLine("Stampa fisica disabilitata nella configurazione (EnablePhysicalPrinting: false).");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Errore di parsing del JSON nel file di configurazione: {ex.Message}");
            Console.WriteLine($"Path: {ex.Path} | LineNumber: {ex.LineNumber} | BytePositionInLine: {ex.BytePositionInLine}.");
            Console.WriteLine("Assicurati che il JSON sia ben formato e non contenga commenti.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Si è verificato un errore: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }

    // CAMBIATO: Aggiunto cardData come parametro specifico per la singola tessera
    [SupportedOSPlatform("windows")]
    private static void DoPrintJob(CardConfig config, CardData cardData)
    {
        float dpi = 300f;
        float mmToPx = dpi / 25.4f;

        float cardWidthPx = 85.60f * mmToPx;
        float cardHeightPx = 53.98f * mmToPx;

        using (Bitmap cardBitmap = new Bitmap((int)cardWidthPx, (int)cardHeightPx))
        {
            cardBitmap.SetResolution(dpi, dpi);

            using (Graphics gfx = Graphics.FromImage(cardBitmap))
            {
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                gfx.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;

                gfx.FillRectangle(Brushes.White, 0, 0, cardWidthPx, cardHeightPx);

                // Ora usiamo il cardData specifico passato al metodo
                float marginPx = cardData.CardMarginMm * mmToPx;

                float photoWidthPx = config.Images!.PhotoWidthCm * 10f * mmToPx;
                float photoHeightPx = config.Images.PhotoHeightCm * 10f * mmToPx;
                float photoX = marginPx;
                float photoY = marginPx;

                Image? photoImage = null;
                try
                {
                    // La PhotoPath è ancora globale per tutte le tessere se non specificato diversamente
                    if (!string.IsNullOrEmpty(config.Images.PhotoPath) && File.Exists(config.Images.PhotoPath))
                    {
                        photoImage = Image.FromFile(config.Images.PhotoPath);
                    }
                    else
                    {
                        Console.WriteLine($"Avviso: Immagine foto non trovata o percorso non valido: {config.Images.PhotoPath}. Verrà usato un un placeholder.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore caricamento foto: {ex.Message}. Verrà usato un placeholder.");
                }

                if (photoImage == null)
                {
                    gfx.FillRectangle(Brushes.LightGray, photoX, photoY, photoWidthPx, photoHeightPx);
                    using (Font font = new Font("Arial", 8, FontStyle.Regular, GraphicsUnit.Point))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        gfx.DrawString("NO PHOTO", font, Brushes.Black, new RectangleF(photoX, photoY, photoWidthPx, photoHeightPx), sf);
                    }
                }
                else
                {
                    RectangleF destRectPhoto = new RectangleF(photoX, photoY, photoWidthPx, photoHeightPx);
                    float photoRatio = (float)photoImage.Width / photoImage.Height;
                    float rectRatio = (float)photoWidthPx / photoHeightPx;

                    float finalWidthPhoto = photoWidthPx;
                    float finalHeightPhoto = photoHeightPx;

                    if (photoRatio > rectRatio)
                    {
                        finalHeightPhoto = photoWidthPx / photoRatio;
                    }
                    else
                    {
                        finalWidthPhoto = photoHeightPx * photoRatio;
                    }
                    destRectPhoto.Width = finalWidthPhoto;
                    destRectPhoto.Height = finalHeightPhoto;

                    gfx.DrawImage(photoImage, destRectPhoto);

                    gfx.DrawRectangle(System.Drawing.Pens.Black, destRectPhoto.X, destRectPhoto.Y, destRectPhoto.Width, destRectPhoto.Height);
                }

                float logoWidthPx = config.Images.LogoWidthCm * 10f * mmToPx;
                float logoHeightPx = config.Images.LogoHeightCm * 10f * mmToPx;
                float logoX = cardWidthPx - marginPx - logoWidthPx;
                float logoY = marginPx;

                Image? logoImage = null;
                try
                {
                    // La LogoPath è ancora globale per tutte le tessere se non specificato diversamente
                    if (!string.IsNullOrEmpty(config.Images.LogoPath) && File.Exists(config.Images.LogoPath))
                    {
                        logoImage = Image.FromFile(config.Images.LogoPath);
                    }
                    else
                    {
                        Console.WriteLine($"Avviso: Immagine logo non trovata o percorso non valido: {config.Images.LogoPath}. Verrà usato un placeholder.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore caricamento logo: {ex.Message}. Verrà usato un placeholder.");
                }

                if (logoImage == null)
                {
                    gfx.FillRectangle(Brushes.LightGray, logoX, logoY, logoWidthPx, logoHeightPx);
                    using (Font font = new Font("Arial", 8, FontStyle.Regular, GraphicsUnit.Point))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        gfx.DrawString("NO LOGO", font, Brushes.Black, new RectangleF(logoX, logoY, logoWidthPx, logoHeightPx), sf);
                    }
                }
                else
                {
                    RectangleF destRectLogo = new RectangleF(logoX, logoY, logoWidthPx, logoHeightPx);
                    float logoRatio = (float)logoImage.Width / logoImage.Height;
                    float rectLogoRatio = (float)logoWidthPx / logoHeightPx;

                    float finalWidthLogo = logoWidthPx;
                    float finalHeightLogo = logoHeightPx;

                    if (logoRatio > rectLogoRatio)
                    {
                        finalHeightLogo = logoWidthPx / logoRatio;
                        destRectLogo.X = logoX + (logoWidthPx - finalWidthLogo);
                    }
                    else
                    {
                        finalWidthLogo = logoHeightPx * logoRatio;
                    }
                    destRectLogo.Width = finalWidthLogo;
                    destRectLogo.Height = finalHeightLogo;

                    gfx.DrawImage(logoImage, destRectLogo);
                }

                // Usiamo cardData per i testi specifici della tessera
                using (Font idFont = new Font(config.FontSettings!.BadgeFontName!, config.FontSettings.BadgeFontSizePoints, config.FontSettings.GetBadgeFontStyle(), GraphicsUnit.Point))
                using (StringFormat idFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                {
                    float idY = photoY + photoHeightPx + (1.5f * mmToPx);
                    float idWidth = photoWidthPx;
                    float idHeight = idFont.GetHeight(gfx);
                    gfx.DrawString(cardData.EmployeeId, idFont, Brushes.Black, new RectangleF(photoX, idY, idWidth, idHeight), idFormat);
                }

                using (Font nameFont = new Font(config.FontSettings.NameFontName!, config.FontSettings.NameFontSizePoints, config.FontSettings.GetNameFontStyle(), GraphicsUnit.Point))
                using (StringFormat nameFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                {
                    float nameY = photoY + photoHeightPx + (1.5f * mmToPx) + new Font(config.FontSettings.BadgeFontName!, config.FontSettings.BadgeFontSizePoints, GraphicsUnit.Point).GetHeight(gfx) + (1f * mmToPx);
                    float nameWidth = photoWidthPx;
                    float nameHeight = nameFont.GetHeight(gfx);
                    gfx.DrawString(cardData.FullName, nameFont, Brushes.Black, new RectangleF(photoX, nameY, nameWidth, nameHeight), nameFormat);
                }

                using (Font bottomFont = new Font(config.FontSettings.BottomFontName!, config.FontSettings.BottomFontSizePoints, config.FontSettings.GetBottomFontStyle(), GraphicsUnit.Point))
                using (StringFormat bottomFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Far })
                {
                    SizeF textSize = gfx.MeasureString(cardData.AdditionalText, bottomFont, (int)(cardWidthPx - (2 * marginPx)));
                    float bottomTextX = marginPx;
                    float bottomTextY = cardHeightPx - marginPx - textSize.Height;

                    gfx.DrawString(cardData.AdditionalText, bottomFont, Brushes.Black, new RectangleF(bottomTextX, bottomTextY, cardWidthPx - (2 * marginPx), textSize.Height), bottomFormat);
                }

                // Salva l'immagine di anteprima con il numero di badge nel nome
                string outputFileName = $"card_preview-{cardData.EmployeeId}.png";
                cardBitmap.Save(outputFileName, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine($"Tessera salvata come '{outputFileName}'.");
            }
        }
    }
}
