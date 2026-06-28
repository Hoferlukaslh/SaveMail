using System.IO.Compression;

namespace TestMail;

public class GenerateurZipService
{
    // Crée une archive ZIP à côté du fichier PDF s'il y a des pièces jointes complexes
    public string? CreerArchive(DonneesMail donnees, string cheminSortiePdf)
    {
        var piecesAZipper = donnees.PiecesJointes
            .Where(pj => pj.Compatibilite == CompatibilitePdf.ExtraireDansZip)
            .ToList();

        if (!piecesAZipper.Any())
            return null; // Rien à zipper

        // On crée le chemin du zip en se basant sur le nom du PDF
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
    
    // Mode "Tout-en-un"
    public string CreerArchiveComplete(DonneesMail donnees, string cheminFichierOriginal, string cheminPdf)
    {
        // On crée le nom du ZIP en se basant sur le nom du PDF
        string nomBase = Path.GetFileNameWithoutExtension(cheminPdf);
        string cheminDossier = Path.GetDirectoryName(cheminPdf) ?? string.Empty;
        string cheminZip = Path.Combine(cheminDossier, $"{nomBase}_Archive_Complete.zip");

        using (var fileStream = new FileStream(cheminZip, FileMode.Create))
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
        {
            // 1. Ajout de l'email original (.eml ou .msg)
            if (File.Exists(cheminFichierOriginal))
            {
                string nomFichierOriginal = Path.GetFileName(cheminFichierOriginal);
                archive.CreateEntryFromFile(cheminFichierOriginal, nomFichierOriginal, CompressionLevel.Optimal);
            }

            // 2. Ajout du PDF généré (qui contient déjà le corps et les images)
            if (File.Exists(cheminPdf))
            {
                string nomPdf = Path.GetFileName(cheminPdf);
                archive.CreateEntryFromFile(cheminPdf, nomPdf, CompressionLevel.Optimal);
            }

            // 3. Ajout de TOUTES les pièces jointes brutes
            if (donnees.PiecesJointes.Any())
            {
                foreach (var pj in donnees.PiecesJointes)
                {
                    // On place les pièces jointes dans un sous-dossier virtuel dans le ZIP pour que ce soit propre
                    string cheminDansZip = $"Pieces_Jointes/{pj.NomFichier}";
                    var entry = archive.CreateEntry(cheminDansZip, CompressionLevel.Optimal);
                    
                    using var entryStream = entry.Open();
                    entryStream.Write(pj.Contenu, 0, pj.Contenu.Length);
                }
            }
        }

        return cheminZip;
    }
}