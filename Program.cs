using System;
using System.Drawing;
using System.Drawing.Drawing2D; // For GraphicsUnit
using System.Drawing.Text; // For TextRenderingHint
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json; // For JSON serialization/deserialization

// Definizione delle classi per la configurazione del JSON
public class CardConfig
{
    public FontSettings? FontSettings { get; set; }
    public CardData? CardData { get; set; }
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
    private const string PreviewFileName = "card_preview.png";

    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (!File.Exists(ConfigFileName))
        {
            Console.WriteLine($"Errore: File di configurazione '{ConfigFileName}' non trovato nella directory dell'applicazione.");
            Console.WriteLine("Assicurati che il file sia presente e accessibile.");
            Console.WriteLine("Premi un tasto per uscire.");
            Console.ReadKey();
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(ConfigFileName);
            CardConfig? config = JsonSerializer.Deserialize<CardConfig>(jsonString);


            if (config?.CardData == null || config.FontSettings == null || config.Images == null || config.PrinterSettings == null)
            {
                Console.WriteLine("Errore: Il file di configurazione è incompleto o malformato. Verificare tutti i campi.");
                Console.WriteLine("Premi un tasto per uscire.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Configurazione caricata con successo.");

            DoPrintJob(config);

            Console.WriteLine($"Anteprima della tessera salvata come '{PreviewFileName}'.");

            if (config.PrinterSettings.EnablePhysicalPrinting)
            {
                Console.WriteLine($"Tentativo di stampa su: {config.PrinterSettings.IpAddress}:{config.PrinterSettings.Port}");
                Console.WriteLine("Stampa fisica non implementata in questo esempio.");
            }
            else
            {
                Console.WriteLine("Stampa fisica disabilitata nella configurazione (EnablePhysicalPrinting: false).");
            }

            Console.WriteLine("Premi un tasto per uscire.");
            Console.ReadKey();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Errore di parsing del JSON nel file di configurazione: {ex.Message}");
            Console.WriteLine($"Path: {ex.Path} | LineNumber: {ex.LineNumber} | BytePositionInLine: {ex.BytePositionInLine}.");
            Console.WriteLine("Assicurati che il JSON sia ben formato e non contenga commenti.");
            Console.WriteLine("Premi un tasto per uscire.");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Si è verificato un errore: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.ReadKey();
        }
    }

    private static void DoPrintJob(CardConfig config)
    {
        // Dimensioni standard di una tessera CR80 (ID-1) in mm: 85.60 x 53.98 mm
        // Convertiamo in pixel per una risoluzione di stampa di 300 DPI (dots per inch)
        // 1 inch = 25.4 mm
        // 300 DPI = 300 pixels / 25.4 mm = 11.811 pixels/mm
        float dpi = 300f;
        float mmToPx = dpi / 25.4f;

        float cardWidthPx = 85.60f * mmToPx;
        float cardHeightPx = 53.98f * mmToPx;

        using (Bitmap cardBitmap = new Bitmap((int)cardWidthPx, (int)cardHeightPx))
        {
            // IMPOSTAZIONE FONDAMENTALE PER IL RENDERING DEI FONT IN PUNTI
            // Assicura che l'oggetto Graphics interpreti i punti tipografici
            // alla stessa risoluzione (DPI) del nostro output finale.
            cardBitmap.SetResolution(dpi, dpi); // <--- NUOVA RIGA IMPORTANTE

            using (Graphics gfx = Graphics.FromImage(cardBitmap))
            {
                // Impostazioni di rendering per una migliore qualità
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                gfx.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // Riempire lo sfondo della tessera (tipicamente bianco)
                gfx.FillRectangle(Brushes.White, 0, 0, cardWidthPx, cardHeightPx);

                // Calcolare i margini
                float marginPx = config.CardData!.CardMarginMm * mmToPx;

                // --- Riquadro per la foto (in alto a sinistra) ---
                float photoWidthPx = config.Images!.PhotoWidthCm * 10f * mmToPx; // Cm a mm
                float photoHeightPx = config.Images.PhotoHeightCm * 10f * mmToPx; // Cm a mm
                float photoX = marginPx;
                float photoY = marginPx;

                Image? photoImage = null;
                try
                {
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
                    // Placeholder se l'immagine non è disponibile
                    gfx.FillRectangle(Brushes.LightGray, photoX, photoY, photoWidthPx, photoHeightPx);
                    using (Font font = new Font("Arial", 8, FontStyle.Regular, GraphicsUnit.Point))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        gfx.DrawString("NO PHOTO", font, Brushes.Black, new RectangleF(photoX, photoY, photoWidthPx, photoHeightPx), sf);
                    }
                }
                else
                {
                    // L'immagine e il riquadro devono essere completamente contenuti
                    RectangleF destRectPhoto = new RectangleF(photoX, photoY, photoWidthPx, photoHeightPx);
                    float photoRatio = (float)photoImage.Width / photoImage.Height;
                    float rectRatio = (float)photoWidthPx / photoHeightPx;

                    float finalWidthPhoto = photoWidthPx;
                    float finalHeightPhoto = photoHeightPx;

                    if (photoRatio > rectRatio) // Immagine più larga del riquadro
                    {
                        finalHeightPhoto = photoWidthPx / photoRatio;
                        destRectPhoto.Y += (photoHeightPx - finalHeightPhoto) / 2; // Centra verticalmente
                    }
                    else // Immagine più alta del riquadro o stesse proporzioni
                    {
                        finalWidthPhoto = photoHeightPx * photoRatio;
                        destRectPhoto.X += (photoWidthPx - finalWidthPhoto) / 2; // Centra orizzontalmente
                    }
                    destRectPhoto.Width = finalWidthPhoto;
                    destRectPhoto.Height = finalHeightPhoto;

                    // Disegna l'immagine
                    gfx.DrawImage(photoImage, destRectPhoto);

                    // Disegna il bordo attorno all'immagine scalata (destRectPhoto)
                    gfx.DrawRectangle(System.Drawing.Pens.Black, destRectPhoto.X, destRectPhoto.Y, destRectPhoto.Width, destRectPhoto.Height);
                }

                // --- Riquadro per il Logo (in alto a destra) ---
                float logoWidthPx = config.Images.LogoWidthCm * 10f * mmToPx; // Cm a mm
                float logoHeightPx = config.Images.LogoHeightCm * 10f * mmToPx; // Cm a mm
                float logoX = cardWidthPx - marginPx - logoWidthPx;
                float logoY = marginPx;

                Image? logoImage = null;
                try
                {
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
                    // Placeholder se l'immagine non è disponibile
                    gfx.FillRectangle(Brushes.LightGray, logoX, logoY, logoWidthPx, logoHeightPx);
                    using (Font font = new Font("Arial", 8, FontStyle.Regular, GraphicsUnit.Point))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        gfx.DrawString("NO LOGO", font, Brushes.Black, new RectangleF(logoX, logoY, logoWidthPx, logoHeightPx), sf);
                    }
                    // Nessun bordo per il placeholder logo
                }
                else
                {
                    // Adatta l'immagine al riquadro mantenendo le proporzioni
                    RectangleF destRectLogo = new RectangleF(logoX, logoY, logoWidthPx, logoHeightPx);
                    float logoRatio = (float)logoImage.Width / logoImage.Height;
                    float rectLogoRatio = (float)logoWidthPx / logoHeightPx;

                    float finalWidthLogo = logoWidthPx;
                    float finalHeightLogo = logoHeightPx;

                    if (logoRatio > rectLogoRatio) // Immagine più larga del riquadro
                    {
                        finalHeightLogo = logoWidthPx / logoRatio;
                        destRectLogo.Y += (logoHeightPx - finalHeightLogo) / 2; // Centra verticalmente
                    }
                    else // Immagine più alta del riquadro o stesse proporzioni
                    {
                        finalWidthLogo = logoHeightPx * logoRatio;
                        destRectLogo.X += (logoWidthPx - finalWidthLogo) / 2; // Centra orizzontalmente
                    }
                    destRectLogo.Width = finalWidthLogo;
                    destRectLogo.Height = finalHeightLogo;

                    // Disegna l'immagine del logo
                    gfx.DrawImage(logoImage, destRectLogo);

                    // NESSUN BORDO attorno al logo, come richiesto
                    // gfx.DrawRectangle(System.Drawing.Pens.Black, destRectLogo.X, destRectLogo.Y, destRectLogo.Width, destRectLogo.Height);
                }

                // --- Testo ID Dipendente (sotto la foto) ---
                using (Font idFont = new Font(config.FontSettings!.BadgeFontName!, config.FontSettings.BadgeFontSizePoints, config.FontSettings.GetBadgeFontStyle(), GraphicsUnit.Point))
                // Modificato: Alignment = StringAlignment.Near per allineamento a sinistra
                using (StringFormat idFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                {
                    float idY = photoY + photoHeightPx + (1.5f * mmToPx); // Spazio sotto la foto
                    float idWidth = photoWidthPx; // Larghezza uguale alla foto
                    float idHeight = idFont.GetHeight(gfx); // Altezza automatica
                    gfx.DrawString(config.CardData.EmployeeId, idFont, Brushes.Black, new RectangleF(photoX, idY, idWidth, idHeight), idFormat);
                }

                // --- Testo Nome Completo (sotto l'ID) ---
                using (Font nameFont = new Font(config.FontSettings.NameFontName!, config.FontSettings.NameFontSizePoints, config.FontSettings.GetNameFontStyle(), GraphicsUnit.Point))
                // Modificato: Alignment = StringAlignment.Near per allineamento a sinistra
                using (StringFormat nameFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                {
                    // Ricalcolo la posizione Y basandomi sulla fine del testo ID
                    float nameY = photoY + photoHeightPx + (1.5f * mmToPx) + new Font(config.FontSettings.BadgeFontName!, config.FontSettings.BadgeFontSizePoints, GraphicsUnit.Point).GetHeight(gfx) + (1f * mmToPx); // Sotto l'ID
                    float nameWidth = photoWidthPx;
                    float nameHeight = nameFont.GetHeight(gfx);
                    gfx.DrawString(config.CardData.FullName, nameFont, Brushes.Black, new RectangleF(photoX, nameY, nameWidth, nameHeight), nameFormat);
                }

                // --- Testo Aggiuntivo (in basso, centrato) ---
                using (Font bottomFont = new Font(config.FontSettings.BottomFontName!, config.FontSettings.BottomFontSizePoints, config.FontSettings.GetBottomFontStyle(), GraphicsUnit.Point))
                // Modificato: Alignment = StringAlignment.Near per allineamento a sinistra
                // LineAlignment = StringAlignment.Far per rimanere ancorato in basso
                using (StringFormat bottomFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Far })
                {
                    // Calcola l'altezza del testo aggiuntivo per centrarlo
                    SizeF textSize = gfx.MeasureString(config.CardData.AdditionalText, bottomFont, (int)(cardWidthPx - (2 * marginPx))); // Max width for wrapping
                    float bottomTextX = marginPx;
                    float bottomTextY = cardHeightPx - marginPx - textSize.Height; // Posiziona dalla parte inferiore

                    gfx.DrawString(config.CardData.AdditionalText, bottomFont, Brushes.Black, new RectangleF(bottomTextX, bottomTextY, cardWidthPx - (2 * marginPx), textSize.Height), bottomFormat);
                }

                // Salva l'immagine di anteprima
                cardBitmap.Save(PreviewFileName, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}