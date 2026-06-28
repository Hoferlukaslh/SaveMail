namespace TestMail;

using System;
using System.IO;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;

public class GenerateurPdfHtmlService
{
    public async Task<string> GenererAsync(DonneesMail donnees, string repertoireSortie)
    {
        // 1. Préparation du nom de fichier
        string sujetNettoye = NettoyerNomFichier(donnees.Header.Subject);
        if (string.IsNullOrWhiteSpace(sujetNettoye)) sujetNettoye = "Email_Sans_Sujet";
        
        string nomFichier = $"{donnees.Header.Date:yyyy-MM-dd}_{sujetNettoye}.pdf";
        string cheminComplet = Path.Combine(repertoireSortie, nomFichier);

        // 2. Initialisation de Chromium (téléchargement transparent la première fois)
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        // 3. Lancement du navigateur en mode invisible (Headless)
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        await using var page = await browser.NewPageAsync();

        // 4. Construction du rendu visuel
        string htmlFinal = ConstruireHtml(donnees);

        // 5. Injection du code dans la page
        await page.SetContentAsync(htmlFinal);

        // 6. Génération du PDF avec conservation des styles d'arrière-plan
        await page.PdfAsync(cheminComplet, new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true, // Indispensable pour garder les couleurs CSS
            MarginOptions = new MarginOptions 
            { 
                Top = "1cm", 
                Bottom = "1cm", 
                Left = "1cm", 
                Right = "1cm" 
            }
        });

        return cheminComplet;
    }

    private string ConstruireHtml(DonneesMail donnees)
    {
        // 1. Détermination du corps principal (HTML ou Texte brut)
        string contenuBody = !string.IsNullOrWhiteSpace(donnees.CorpsHtml) 
            ? donnees.CorpsHtml 
            : $"<pre style='font-family: sans-serif; white-space: pre-wrap;'>{donnees.CorpsTexte}</pre>";

        // 2. Remplacement des images CID par du Base64 (Images intégrées au corps)
        contenuBody = IntegrerImagesBase64(contenuBody, donnees.PiecesJointes);

        // Préparation de la liste des destinataires
        string destinataires = donnees.Header.To.Any() ? string.Join(", ", donnees.Header.To) : "Non spécifié";

        // 3. Construction de l'en-tête visuel du mail
        string enTete = $@"
            <div style='background-color: #f8f9fa; border: 1px solid #dee2e6; border-radius: 5px; padding: 15px; margin-bottom: 20px; font-family: Arial, sans-serif;'>
                <h2 style='margin: 0 0 15px 0; color: #212529;'>{donnees.Header.Subject}</h2>
                <table style='width: 100%; font-size: 13px; color: #495057; border-collapse: collapse;'>
                    <tr>
                        <td style='width: 100px; padding-bottom: 5px;'><strong>De :</strong></td>
                        <td style='padding-bottom: 5px;'>{donnees.Header.From}</td>
                    </tr>
                    <tr>
                        <td style='padding-bottom: 5px;'><strong>À :</strong></td>
                        <td style='padding-bottom: 5px;'>{destinataires}</td>
                    </tr>
                    <tr>
                        <td style='padding-bottom: 5px;'><strong>Date :</strong></td>
                        <td style='padding-bottom: 5px;'>{donnees.Header.Date:dd.MM.yyyy HH:mm}</td>
                    </tr>
                    <tr>
                        <td style='padding-bottom: 5px;'><strong>Message-ID :</strong></td>
                        <td style='padding-bottom: 5px; font-family: monospace; font-size: 12px;'>{donnees.Header.MessageId}</td>
                    </tr>";

        if (!string.IsNullOrWhiteSpace(donnees.Header.AuthenticationResults))
        {
            enTete += $@"
                    <tr>
                        <td style='padding-bottom: 5px; color: #198754;'><strong>Sécurité :</strong></td>
                        <td style='padding-bottom: 5px; font-family: monospace; font-size: 12px; color: #198754;'>
                            {donnees.Header.AuthenticationResults}
                        </td>
                    </tr>";
        }

        enTete += @"
                </table>
            </div>
            <hr style='border: 0; height: 1px; background: #dee2e6; margin-bottom: 20px;' />";

        // 4. Assemblage initial
        string htmlFinal = $"<html><body style='margin: 0; padding: 0;'>{enTete}{contenuBody}";

        // 5. Ajout des pièces jointes à la fin du document (Textes ET Images)
        var piecesAides = donnees.PiecesJointes
            .Where(pj => pj.Compatibilite == CompatibilitePdf.FusionnerDansPdf);

        foreach (var pj in piecesAides)
        {
            // Traitement des pièces jointes textuelles
            if (pj.TypeMime.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                string contenuTexte = System.Text.Encoding.UTF8.GetString(pj.Contenu);
                htmlFinal += $@"
                    <div style='page-break-before: always; padding-top: 20px; font-family: Arial, sans-serif;'>
                        <h3 style='color: #6c757d; border-bottom: 1px solid #dee2e6; padding-bottom: 5px;'>Pièce jointe : {pj.NomFichier}</h3>
                        <pre style='background-color: #f8f9fa; border: 1px solid #dee2e6; padding: 15px; font-size: 12px; white-space: pre-wrap; word-wrap: break-word;'>{contenuTexte}</pre>
                    </div>";
            }
            // Traitement des pièces jointes images (qui ne sont pas des logos intégrés)
            else if (pj.TypeMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                // On vérifie si l'image n'a pas déjà été intégrée dans le corps via son CID
                bool estDejaDansLeCorps = !string.IsNullOrWhiteSpace(pj.ContentId) && 
                                          !string.IsNullOrWhiteSpace(donnees.CorpsHtml) && 
                                          donnees.CorpsHtml.Contains(pj.ContentId, StringComparison.OrdinalIgnoreCase);

                if (!estDejaDansLeCorps)
                {
                    string base64String = Convert.ToBase64String(pj.Contenu);
                    string dataUri = $"data:{pj.TypeMime};base64,{base64String}";

                    htmlFinal += $@"
                        <div style='page-break-before: always; padding-top: 20px; font-family: Arial, sans-serif; text-align: center;'>
                            <h3 style='color: #6c757d; border-bottom: 1px solid #dee2e6; padding-bottom: 5px; text-align: left;'>Pièce jointe : {pj.NomFichier}</h3>
                            <img src='{dataUri}' style='max-width: 100%; max-height: 950px; object-fit: contain;' alt='{pj.NomFichier}' />
                        </div>";
                }
            }
        }

        // 6. Fermeture propre des balises HTML
        htmlFinal += "</body></html>";

        return htmlFinal;
    }

    // Transforme les pièces jointes en images lisibles par le navigateur
    private string IntegrerImagesBase64(string html, List<PieceJointe> piecesJointes)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        foreach (var pj in piecesJointes)
        {
            // On ne traite que les fichiers qui ont un Content-ID
            if (string.IsNullOrWhiteSpace(pj.ContentId)) continue;

            // Conversion des bytes en chaîne Base64
            string base64String = Convert.ToBase64String(pj.Contenu);
            
            // Création de l'URI de données lisible par HTML/Puppeteer
            string dataUri = $"data:{pj.TypeMime};base64,{base64String}";

            // Remplacement de "cid:identifiant" par les données réelles de l'image
            html = html.Replace($"cid:{pj.ContentId}", dataUri, StringComparison.OrdinalIgnoreCase);
        }

        return html;
    }

    private string NettoyerNomFichier(string nom)
    {
        if (string.IsNullOrWhiteSpace(nom)) return string.Empty;
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            nom = nom.Replace(c, '_');
        }
        return nom;
    }
}