namespace TestMail;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

public class FusionPdfService
{
    public string FusionnerPiecesJointes(string cheminPdfPrincipal, List<PieceJointe> piecesJointes)
    {
        // On filtre pour ne garder que les pièces jointes qui sont de vrais PDF
        var piecesPdf = piecesJointes
            .Where(pj => pj.Compatibilite == CompatibilitePdf.FusionnerDansPdf && 
                         pj.TypeMime.Contains("pdf", System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        // S'il n'y a pas de PDF à fusionner, on retourne le chemin intact
        if (!piecesPdf.Any())
        {
            return cheminPdfPrincipal;
        }

        string cheminTemporaire = cheminPdfPrincipal.Replace(".pdf", "_temp.pdf");

        // On ouvre le PDF principal généré par l'email
        using (PdfDocument documentFinal = PdfReader.Open(cheminPdfPrincipal, PdfDocumentOpenMode.Modify))
        {
            foreach (var pj in piecesPdf)
            {
                // On lit la pièce jointe depuis ses données brutes (byte[])
                using var stream = new MemoryStream(pj.Contenu);
                using PdfDocument documentAJoindre = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                
                // On copie chaque page du PDF joint vers le document final
                for (int i = 0; i < documentAJoindre.PageCount; i++)
                {
                    documentFinal.AddPage(documentAJoindre.Pages[i]);
                }
            }
            
            // On sauvegarde le résultat fusionné
            documentFinal.Save(cheminTemporaire);
        }

        // On remplace l'ancien fichier par le nouveau fichier fusionné
        File.Delete(cheminPdfPrincipal);
        File.Move(cheminTemporaire, cheminPdfPrincipal);

        return cheminPdfPrincipal;
    }
}