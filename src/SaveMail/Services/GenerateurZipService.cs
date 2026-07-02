using System.IO;
using System.IO.Compression;
using System.Linq;
using SaveMail.Models;

namespace SaveMail.Services;

public class GenerateurZipService
{
    public string? CreerArchive(DonneesMail donnees, string cheminSortiePdf)
    {
        var piecesAZipper = donnees.PiecesJointes.Where(pj => pj.Compatibilite == CompatibilitePdf.ExtraireDansZip)
            .ToList();
        if (!piecesAZipper.Any()) return null;

        var cheminZip = Path.ChangeExtension(cheminSortiePdf, ".zip");

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

    public string CreerArchiveComplete(DonneesMail donnees, string cheminFichierOriginal, string cheminPdf,
        bool keepOriginalEmail)
    {
        var nomBase = Path.GetFileNameWithoutExtension(cheminPdf);
        var cheminDossier = Path.GetDirectoryName(cheminPdf) ?? string.Empty;
        var cheminZip = Path.Combine(cheminDossier, $"{nomBase}{TranslationService.Instance["ZipArchiveSuffix"]}.zip"); 

        using (var fileStream = new FileStream(cheminZip, FileMode.Create))
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
        {
            // ajoute l'email d'origine uniquement si demandé
            if (keepOriginalEmail && File.Exists(cheminFichierOriginal))
                archive.CreateEntryFromFile(cheminFichierOriginal, Path.GetFileName(cheminFichierOriginal),
                    CompressionLevel.Optimal);

            if (File.Exists(cheminPdf))
                archive.CreateEntryFromFile(cheminPdf, Path.GetFileName(cheminPdf), CompressionLevel.Optimal);

            if (donnees.PiecesJointes.Any())
                foreach (var pj in donnees.PiecesJointes)
                {
                    var entry = archive.CreateEntry($"{TranslationService.Instance["ZipAttachmentFolder"]}/{pj.NomFichier}", CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    entryStream.Write(pj.Contenu, 0, pj.Contenu.Length);
                }
        }

        return cheminZip;
    }
}