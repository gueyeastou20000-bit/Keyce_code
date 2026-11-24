using System.IO;
using System.Windows.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinPdf = Windows.Data.Pdf;

namespace KEYCE_KODER.App.Pdf;

/// <summary>
/// Affiche les pages d'un PDF avec le moteur PDF intégré à Windows (Windows.Data.Pdf) :
/// aucun programme externe, aucune bibliothèque à installer.
/// </summary>
public sealed class PdfRenderer
{
    // Code d'erreur Windows « mot de passe incorrect » (ERROR_WRONG_PASSWORD)
    private const int WrongPasswordHResult = unchecked((int)0x8007052B);

    private readonly WinPdf.PdfDocument _document;

    private PdfRenderer(WinPdf.PdfDocument document)
    {
        _document = document;
    }

    public int PageCount => (int)_document.PageCount;

    /// <summary>Charge un PDF (avec son mot de passe s'il est protégé).</summary>
    public static async Task<PdfRenderer> LoadAsync(string path, string? password)
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));

        WinPdf.PdfDocument document = string.IsNullOrEmpty(password)
            ? await WinPdf.PdfDocument.LoadFromFileAsync(file)
            : await WinPdf.PdfDocument.LoadFromFileAsync(file, password);

        return new PdfRenderer(document);
    }

    /// <summary>L'erreur signifie-t-elle « mot de passe nécessaire ou incorrect » ?</summary>
    public static bool IsPasswordError(Exception ex) => ex.HResult == WrongPasswordHResult;

    /// <summary>Taille d'une page en pixels logiques WPF (rotation comprise).</summary>
    public System.Windows.Size GetPageSize(int index)
    {
        using WinPdf.PdfPage page = _document.GetPage((uint)index);
        return new System.Windows.Size((double)page.Size.Width, (double)page.Size.Height);
    }

    /// <summary>Dessine une page en image, à la largeur demandée (en pixels réels de l'écran).</summary>
    public async Task<BitmapSource> RenderAsync(int index, double widthInPixels)
    {
        using WinPdf.PdfPage page = _document.GetPage((uint)index);
        using var stream = new InMemoryRandomAccessStream();

        var options = new WinPdf.PdfPageRenderOptions
        {
            DestinationWidth = (uint)Math.Clamp(Math.Round(widthInPixels), 1, 8000)
        };

        await page.RenderToStreamAsync(stream, options);
        stream.Seek(0);

        // Image PNG produite par Windows → image WPF
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream.AsStream();
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }
}
