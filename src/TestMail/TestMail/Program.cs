namespace TestMail;

class Program
{
    static async Task Main(string[] args) 
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        Console.WriteLine("Démarrage de la conversion...\n");

        var fichierMail = new FichierMail("/home/lukas/Bureau/SaveMail/src/TestMail/TestMail/File.msg");

        try
        {
            // 1. Extraction
            var extracteur = new ExtracteurMailService();
            DonneesMail donnees = extracteur.Extraire(fichierMail);
            Console.WriteLine("✅ Email lu et extrait avec succès.");

            string repertoireSortie = AppDomain.CurrentDomain.BaseDirectory;

            // 2. Génération du PDF avec rendu HTML
            Console.WriteLine("⏳ Génération du PDF principal...");
            var generateurPdf = new GenerateurPdfHtmlService();
            string cheminPdf = await generateurPdf.GenererAsync(donnees, repertoireSortie); 

            // 3. Fusion des pièces jointes PDF
            var fusionPdf = new FusionPdfService();
            cheminPdf = fusionPdf.FusionnerPiecesJointes(cheminPdf, donnees.PiecesJointes);
            Console.WriteLine($"✅ PDF final généré : {cheminPdf}");

            // --- NOUVEAU LOGIQUE D'ARCHIVAGE ---
    
            // Définissez ici le comportement souhaité (ce sera un bouton/case à cocher dans votre future interface Avalonia)
            bool modeArchiveComplete = true; 
    
            var generateurZip = new GenerateurZipService();

            if (modeArchiveComplete)
            {
                Console.WriteLine("⏳ Création de l'archive complète (EML + PDF + Pièces jointes)...");
                string cheminZip = generateurZip.CreerArchiveComplete(donnees, fichierMail.Path, cheminPdf);
                Console.WriteLine($"✅ Archive complète générée : {cheminZip}");
        
                // Optionnel mais recommandé : Nettoyage du PDF seul puisqu'il est maintenant dans le ZIP
                if (File.Exists(cheminPdf))
                {
                    File.Delete(cheminPdf);
                    Console.WriteLine("🧹 Fichier PDF temporaire supprimé du dossier source.");
                }
            }
            else
            {
                // Mode classique : PDF + ZIP uniquement pour les fichiers complexes
                string? cheminZip = generateurZip.CreerArchive(donnees, cheminPdf);
                if (cheminZip != null)
                {
                    Console.WriteLine($"✅ Archive ZIP (pièces complexes) générée : {cheminZip}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Erreur : {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine("\nTerminé.");
    }
}