using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using SaveMail.Models;

namespace SaveMail.Services;

public class GenerateurPdfHtmlService : IAsyncDisposable
{
    private IBrowser? _browser;
    
    public async Task InitialiserNavigateurAsync()
    {
        // On évite d'initialiser deux fois
        if (_browser != null) return;

        //  Utilisation de DefaultRevision à la place de DefaultChromiumRevision
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        // Configuration pour une furtivité absolue (élimine la fenêtre blanche et la console)
        var launchOptions = new LaunchOptions
        {
            Headless = true,
            EnqueueAsyncMessages = true, 
            Args = new[]
            {
                // Contournement géométrique : on dessine la fenêtre en dehors de l'écran
                "--window-position=-2400,-2400", 
                "--window-size=1280,720",
                "--screen-info={1280x720}", // Pour les versions récentes de Chromium (m135+)

                // Désactivation du rendu matériel et logiciel
                "--disable-gpu",                 
                "--disable-software-rasterizer", 
                
                // Sécurité et stabilité
                "--no-sandbox",                  
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",       
                
                // Suppression des éléments d'interface inutiles
                "--hide-scrollbars",             
                "--mute-audio",                  
                "--disable-notifications",       
                
                // Désactivation des services Google
                "--disable-features=Translate,OptimizationHints,MediaRouter",
                "--disable-background-networking",
                "--disable-sync",
                "--disable-default-apps",
                "--no-default-browser-check",
                "--no-first-run"
            }
        };

        // On assigne l'instance de navigateur ici
        _browser = await Puppeteer.LaunchAsync(launchOptions);
    }
    
    // 2. La méthode de génération ne lance plus le navigateur, elle utilise celui déjà ouvert
    public async Task<string> GenererAsync(DonneesMail donnees, string repertoireSortie)
    {
        if (_browser == null)
        {
            // Sécurité : si la méthode est appelée avant l'initialisation explicite, on initialise
            await InitialiserNavigateurAsync();
        }

        string sujetNettoye = NettoyerNomFichier(donnees.Header.Subject);
        if (string.IsNullOrWhiteSpace(sujetNettoye)) sujetNettoye = "Email_Sans_Sujet";
        
        string nomFichier = $"{donnees.Header.Date:yyyy-MM-dd}_{sujetNettoye}.pdf";
        string cheminComplet = Path.Combine(repertoireSortie, nomFichier);

        // On ouvre juste un nouvel onglet
        await using var page = await _browser!.NewPageAsync();

        string htmlFinal = ConstruireHtml(donnees);
        await page.SetContentAsync(htmlFinal);

        await page.PdfAsync(cheminComplet, new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" }
        });

        // On ferme l'onglet après le PDF
        await page.CloseAsync();

        return cheminComplet;
    }

    // 3. Nettoyage : On ferme le navigateur quand on a fini de traiter tous les mails
    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            await _browser.DisposeAsync();
            _browser = null;
        }
    }

    private string ConstruireHtml(DonneesMail donnees)
    {
        string contenuBody = !string.IsNullOrWhiteSpace(donnees.CorpsHtml) 
            ? donnees.CorpsHtml 
            : $"<pre style='font-family: sans-serif; white-space: pre-wrap;'>{donnees.CorpsTexte}</pre>";

        contenuBody = IntegrerImagesBase64(contenuBody, donnees.PiecesJointes);

        string destinataires = donnees.Header.To.Any() ? string.Join(", ", donnees.Header.To) : "Non spécifié";

        string enTete = $@"
            <div style='background-color: #f8f9fa; border: 1px solid #dee2e6; border-radius: 5px; padding: 15px; margin-bottom: 20px; font-family: Arial, sans-serif;'>
                <h2 style='margin: 0 0 15px 0; color: #212529;'>{donnees.Header.Subject}</h2>
                <table style='width: 100%; font-size: 13px; color: #495057; border-collapse: collapse;'>
                    <tr><td style='width: 100px; padding-bottom: 5px;'><strong>De :</strong></td><td style='padding-bottom: 5px;'>{donnees.Header.From}</td></tr>
                    <tr><td style='padding-bottom: 5px;'><strong>À :</strong></td><td style='padding-bottom: 5px;'>{destinataires}</td></tr>
                    <tr><td style='padding-bottom: 5px;'><strong>Date :</strong></td><td style='padding-bottom: 5px;'>{donnees.Header.Date:dd.MM.yyyy HH:mm}</td></tr>
                </table>
            </div><hr style='border: 0; height: 1px; background: #dee2e6; margin-bottom: 20px;' />";

        string htmlFinal = $"<html><body style='margin: 0; padding: 0;'>{enTete}{contenuBody}";

        var piecesAides = donnees.PiecesJointes.Where(pj => pj.Compatibilite == CompatibilitePdf.FusionnerDansPdf);

        foreach (var pj in piecesAides)
        {
            if (pj.TypeMime.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                string contenuTexte = System.Text.Encoding.UTF8.GetString(pj.Contenu);
                htmlFinal += $@"<div style='page-break-before: always; padding-top: 20px; font-family: Arial, sans-serif;'><h3 style='color: #6c757d; border-bottom: 1px solid #dee2e6; padding-bottom: 5px;'>Pièce jointe : {pj.NomFichier}</h3><pre style='background-color: #f8f9fa; border: 1px solid #dee2e6; padding: 15px; font-size: 12px; white-space: pre-wrap; word-wrap: break-word;'>{contenuTexte}</pre></div>";
            }
            else if (pj.TypeMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                bool estDejaDansLeCorps = !string.IsNullOrWhiteSpace(pj.ContentId) && 
                                          !string.IsNullOrWhiteSpace(donnees.CorpsHtml) && 
                                          donnees.CorpsHtml.Contains(pj.ContentId, StringComparison.OrdinalIgnoreCase);

                if (!estDejaDansLeCorps)
                {
                    string base64String = Convert.ToBase64String(pj.Contenu);
                    string dataUri = $"data:{pj.TypeMime};base64,{base64String}";

                    htmlFinal += $@"<div style='page-break-before: always; padding-top: 20px; font-family: Arial, sans-serif; text-align: center;'><h3 style='color: #6c757d; border-bottom: 1px solid #dee2e6; padding-bottom: 5px; text-align: left;'>Pièce jointe : {pj.NomFichier}</h3><img src='{dataUri}' style='max-width: 100%; max-height: 950px; object-fit: contain;' alt='{pj.NomFichier}' /></div>";
                }
            }
        }

        return htmlFinal + "</body></html>";
    }

    private string IntegrerImagesBase64(string html, List<PieceJointe> piecesJointes)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;
        foreach (var pj in piecesJointes)
        {
            if (string.IsNullOrWhiteSpace(pj.ContentId)) continue;
            string base64String = Convert.ToBase64String(pj.Contenu);
            string dataUri = $"data:{pj.TypeMime};base64,{base64String}";
            html = html.Replace($"cid:{pj.ContentId}", dataUri, StringComparison.OrdinalIgnoreCase);
        }
        return html;
    }

    private string NettoyerNomFichier(string nom)
    {
        if (string.IsNullOrWhiteSpace(nom)) return string.Empty;
        foreach (char c in Path.GetInvalidFileNameChars()) nom = nom.Replace(c, '_');
        return nom;
    }
}