using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace KEYCE_KODER.App.Pdf;

/// <summary>Une modification à appliquer à un document PDF (PDFsharp).</summary>
public delegate void PdfEdit(PdfDocument document);

/// <summary>Couleurs proposées pour le texte ajouté.</summary>
public enum PdfTextColor
{
    Black,
    Red,
    Blue
}

/// <summary>
/// Modifie un PDF avec la bibliothèque PDFsharp : pivoter, supprimer, déplacer, insérer des pages,
/// fusionner un autre PDF, ajouter du texte, surligner ou masquer une zone.
/// Chaque modification lit un fichier et en écrit un nouveau : on peut ainsi annuler.
/// </summary>
public static class PdfEditor
{
    static PdfEditor()
    {
        // PDFsharp doit savoir où trouver les polices : celles de Windows (C:\Windows\Fonts)
        try
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
        catch (Exception)
        {
            // Déjà réglé (ce réglage ne peut être fait qu'une fois) : rien à faire
        }
    }

    /// <summary>Ouvre source, applique la modification, enregistre le résultat dans destination.</summary>
    public static Task ApplyAsync(string source, string destination, string? password, PdfEdit edit) =>
        Task.Run(() =>
        {
            using PdfDocument document = string.IsNullOrEmpty(password)
                ? PdfReader.Open(source, PdfDocumentOpenMode.Modify)
                : PdfReader.Open(source, password, PdfDocumentOpenMode.Modify);

            edit(document);
            document.Save(destination);
        });

    // ---------------- Pages ----------------

    /// <summary>Pivote une page (90 = sens des aiguilles d'une montre, -90 = sens inverse).</summary>
    public static PdfEdit RotatePage(int index, int degrees) => document =>
    {
        PdfPage page = document.Pages[index];
        page.Rotate = ((page.Rotate + degrees) % 360 + 360) % 360;
    };

    public static PdfEdit DeletePage(int index) => document =>
    {
        if (document.PageCount <= 1)
            throw new InvalidOperationException("Impossible de supprimer la dernière page du document.");

        document.Pages.RemoveAt(index);
    };

    public static PdfEdit MovePage(int index, int newIndex) => document =>
        document.Pages.MovePage(index, newIndex);

    /// <summary>Insère une page blanche (de même taille) après la page indiquée.</summary>
    public static PdfEdit InsertBlankPageAfter(int index) => document =>
    {
        PdfPage model = document.Pages[index];
        PdfPage blank = document.InsertPage(index + 1);
        blank.Width = model.Width;
        blank.Height = model.Height;
    };

    /// <summary>Ajoute toutes les pages d'un autre PDF à la fin du document.</summary>
    public static PdfEdit AppendDocument(string otherPath) => document =>
    {
        using PdfDocument other = PdfReader.Open(otherPath, PdfDocumentOpenMode.Import);

        for (int i = 0; i < other.PageCount; i++)
            document.AddPage(other.Pages[i]);
    };

    // ---------------- Dessin sur une page ----------------
    // Les positions sont des fractions de la page (0 = bord gauche / haut, 1 = bord droit / bas).

    /// <summary>Écrit du texte sur la page (plusieurs lignes possibles).</summary>
    public static PdfEdit AddText(int index, double fx, double fy, string text, double size, PdfTextColor color) => document =>
    {
        using XGraphics graphics = OpenGraphics(document, index);

        var font = new XFont("Arial", size);
        XBrush brush = color switch
        {
            PdfTextColor.Red => XBrushes.Red,
            PdfTextColor.Blue => XBrushes.Blue,
            _ => XBrushes.Black
        };

        double x = fx * graphics.PageSize.Width;
        double y = fy * graphics.PageSize.Height;

        foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
        {
            graphics.DrawString(line, font, brush, x, y, XStringFormats.TopLeft);
            y += size * 1.25;
        }
    };

    /// <summary>Surligne une zone en jaune transparent.</summary>
    public static PdfEdit Highlight(int index, double fx, double fy, double fw, double fh) =>
        FillRectangle(index, fx, fy, fw, fh, XColor.FromArgb(90, 255, 230, 0));

    /// <summary>Recouvre une zone en blanc (cache visuellement le contenu).</summary>
    public static PdfEdit Mask(int index, double fx, double fy, double fw, double fh) =>
        FillRectangle(index, fx, fy, fw, fh, XColor.FromArgb(255, 255, 255, 255));

    private static PdfEdit FillRectangle(int index, double fx, double fy, double fw, double fh, XColor color) => document =>
    {
        using XGraphics graphics = OpenGraphics(document, index);

        double width = graphics.PageSize.Width;
        double height = graphics.PageSize.Height;

        graphics.DrawRectangle(new XSolidBrush(color), fx * width, fy * height, fw * width, fh * height);
    };

    private static XGraphics OpenGraphics(PdfDocument document, int index)
    {
        PdfPage page = document.Pages[index];

        // Sur une page pivotée, les positions à l'écran ne correspondent plus à celles du PDF
        if (page.Rotate % 360 != 0)
            throw new InvalidOperationException(
                "Cette page est pivotée : remettez-la droite (⟲ / ⟳) avant d'y ajouter du texte ou une zone.");

        return XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
    }
}
