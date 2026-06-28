using System.IO;
using System.IO.Compression;
using System.Linq;
using SaveMail.Models;

namespace SaveMail.Services;

public class GenerateurZipService
{
    public string? CreerArchive(DonneesMail donnees, string cheminSortiePdf)
    {
        var piecesAZipper = donnees.PiecesJointes.Where(pj => pj.Compatibilite == CompatibilitePdf.ExtraireDansZip).ToList();
        if (!piecesAZipper.Any()) return null;

        string cheminZip = Path.ChangeExtension(cheminSortiePdf, ".zip");

        using (var fileStream = new FileStream(cheminZip, FileMode.Create))
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
        {
            foreach (var pj in piecesAZipper)
            {
                var entry = archive.CreateEntry(pj.NomFichier, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(pj.Contenu, 0, pj.Contenu.Length);
            }
        }
        return cheminZip;
    }
    
    public string CreerArchiveComplete(DonneesMail donnees, string cheminFichierOriginal, string cheminPdf)
    {
        string nomBase = Path.GetFileNameWithoutExtension(cheminPdf);
        string cheminDossier = Path.GetDirectoryName(cheminPdf) ?? string.Empty;
        string cheminZip = Path.Combine(cheminDossier, $"{nomBase}_Archive_Complete.zip");

        using (var fileStream = new FileStream(cheminZip, FileMode.Create))
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
        {
            if (File.Exists(cheminFichierOriginal))
            {
                archive.CreateEntryFromFile(cheminFichierOriginal, Path.GetFileName(cheminFichierOriginal), CompressionLevel.Optimal);
            }

            if (File.Exists(cheminPdf))
            {
                archive.CreateEntryFromFile(cheminPdf, Path.GetFileName(cheminPdf), CompressionLevel.Optimal);
            }

            if (donnees.PiecesJointes.Any())
            {
                foreach (var pj in donnees.PiecesJointes)
                {
                    var entry = archive.CreateEntry($"Pieces_Jointes/{pj.NomFichier}", CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    entryStream.Write(pj.Contenu, 0, pj.Contenu.Length);
                }
            }
        }
        return cheminZip;
    }
}