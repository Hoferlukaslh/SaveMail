using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SaveMail.Models;

namespace SaveMail.Services;

public class FusionPdfService
{
    public string FusionnerPiecesJointes(string cheminPdfPrincipal, List<PieceJointe> piecesJointes)
    {
        var piecesPdf = piecesJointes
            .Where(pj => pj.Compatibilite == CompatibilitePdf.FusionnerDansPdf && 
                         pj.TypeMime.Contains("pdf", System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!piecesPdf.Any()) return cheminPdfPrincipal;

        string cheminTemporaire = cheminPdfPrincipal.Replace(".pdf", "_temp.pdf");

        using (PdfDocument documentFinal = PdfReader.Open(cheminPdfPrincipal, PdfDocumentOpenMode.Modify))
        {
            foreach (var pj in piecesPdf)
            {
                using var stream = new MemoryStream(pj.Contenu);
                using PdfDocument documentAJoindre = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                
                for (int i = 0; i < documentAJoindre.PageCount; i++)
                {
                    documentFinal.AddPage(documentAJoindre.Pages[i]);
                }
            }
            documentFinal.Save(cheminTemporaire);
        }

        File.Delete(cheminPdfPrincipal);
        File.Move(cheminTemporaire, cheminPdfPrincipal);

        return cheminPdfPrincipal;
    }
}