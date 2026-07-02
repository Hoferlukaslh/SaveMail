using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using SaveMail.Models;
using System.Text.RegularExpressions;

namespace SaveMail.Services;

public class GenerateurPdfHtmlService : IAsyncDisposable
{
    private IBrowser? _browser;

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
    public async Task<string> GenererAsync(DonneesMail donnees, string repertoireSortie, bool includeSignatures, bool addAttachmentsToPdf, string fileNameFormat = "{yyyy}-{MM}-{dd}_{subject}")
    {
        if (_browser == null) await InitialiserNavigateurAsync();

        var sujetNettoye = NettoyerNomFichier(donnees.Header.Subject);
        if (string.IsNullOrWhiteSpace(sujetNettoye)) sujetNettoye = "Email_Sans_Sujet";
        var senderNettoye = NettoyerNomFichier(donnees.Header.From);

        var nomFichier = fileNameFormat;

        // Remplacement ultra-robuste avec Regex (tolère les espaces internes comme { yy }, ignore la casse)
        nomFichier = Regex.Replace(nomFichier, @"\{\s*yyyy\s*\}", donnees.Header.Date.ToString("yyyy"), RegexOptions.IgnoreCase);
        nomFichier = Regex.Replace(nomFichier, @"\{\s*yy\s*\}", donnees.Header.Date.ToString("yy"), RegexOptions.IgnoreCase);

        // Le mois et le jour
        nomFichier = Regex.Replace(nomFichier, @"\{\s*mm\s*\}", donnees.Header.Date.ToString("MM"), RegexOptions.IgnoreCase);
        nomFichier = Regex.Replace(nomFichier, @"\{\s*dd\s*\}", donnees.Header.Date.ToString("dd"), RegexOptions.IgnoreCase);

        // Le sujet et l'expéditeur
        nomFichier = Regex.Replace(nomFichier, @"\{\s*subject\s*\}", sujetNettoye, RegexOptions.IgnoreCase);
        nomFichier = Regex.Replace(nomFichier, @"\{\s*sender\s*\}", senderNettoye, RegexOptions.IgnoreCase);

        // Sécurité pour s'assurer qu'il y a toujours un nom et la bonne extension
        if (string.IsNullOrWhiteSpace(nomFichier.Replace(".pdf", ""))) nomFichier = $"Email_{donnees.Header.Date:yyyyMMdd}";
        if (!nomFichier.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) nomFichier += ".pdf";

        var cheminComplet = Path.Combine(repertoireSortie, nomFichier);

        await using var page = await _browser!.NewPageAsync();

        var htmlFinal = ConstruireHtml(donnees, includeSignatures, addAttachmentsToPdf);
        await page.SetContentAsync(htmlFinal);

        await page.PdfAsync(cheminComplet, new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" }
        });

        await page.CloseAsync();
        return cheminComplet;
    }

    private string ConstruireHtml(DonneesMail donnees, bool includeSignatures, bool addAttachmentsToPdf)
    {
        // Récupération et préparation du corps du message
        var contenuBody = !string.IsNullOrWhiteSpace(donnees.CorpsHtml)
            ? donnees.CorpsHtml
            : $"<pre style='font-family: sans-serif; white-space: pre-wrap;'>{donnees.CorpsTexte}</pre>";

        contenuBody = IntegrerImagesBase64(contenuBody, donnees.PiecesJointes);
        
        // 1. EN-TÊTE CLASSIQUE
        var destinataires = donnees.Header.To.Any() ? string.Join(", ", donnees.Header.To) : "Non spécifié";
        var copieCc = donnees.Header.Cc.Any() ? string.Join(", ", donnees.Header.Cc) : string.Empty;

        // Note : j'ai augmenté la largeur (width: 90px) pour faire de la place à "Répondre à :"
        var blocEntete = $@"
        <div style='font-family: Arial, sans-serif; margin-bottom: 15px;'>
            <h1 style='font-size: 20px; margin: 0 0 15px 0; color: #0f172a;'>{donnees.Header.Subject}</h1>
            <table style='width: 100%; font-size: 13px; color: #334155; border-collapse: collapse;'>
                <tr>
                    <td style='padding: 4px 0; width: 90px; font-weight: bold; color: #64748b;'>De :</td>
                    <td style='padding: 4px 0; color: #0f172a;'>{donnees.Header.From}</td>
                </tr>";

        // Ajout conditionnel du Reply-To (seulement s'il est présent ET différent du From)
        if (!string.IsNullOrWhiteSpace(donnees.Header.ReplyTo) && 
            !donnees.Header.ReplyTo.Equals(donnees.Header.From, StringComparison.OrdinalIgnoreCase))
        {
            blocEntete += $@"
                <tr>
                    <td style='padding: 4px 0; font-weight: bold; color: #64748b;'>Répondre à :</td>
                    <td style='padding: 4px 0; color: #0f172a;'>{donnees.Header.ReplyTo}</td>
                </tr>";
        }

        blocEntete += $@"
                <tr>
                    <td style='padding: 4px 0; font-weight: bold; color: #64748b;'>À :</td>
                    <td style='padding: 4px 0;'>{destinataires}</td>
                </tr>";

        if (!string.IsNullOrEmpty(copieCc))
        {
            blocEntete += $@"
                <tr>
                    <td style='padding: 4px 0; font-weight: bold; color: #64748b;'>Cc :</td>
                    <td style='padding: 4px 0;'>{copieCc}</td>
                </tr>";
        }

        blocEntete += $@"
                <tr>
                    <td style='padding: 4px 0; font-weight: bold; color: #64748b;'>Date :</td>
                    <td style='padding: 4px 0;'>{donnees.Header.Date:dd MMMM yyyy à HH:mm:ss}</td>
                </tr>
            </table>
        </div>";
        
        // 2. BLOC SIGNATURES 
        var blocSignature = string.Empty;

        if (includeSignatures)
        {
            var messageId = string.IsNullOrWhiteSpace(donnees.Header.MessageId) ? "Non disponible" : donnees.Header.MessageId;
            
            blocSignature = $@"
        <div style='background-color: #f1f5f9; border-left: 4px solid #3b82f6; padding: 12px; margin-bottom: 15px; font-family: monospace; font-size: 11px; color: #475569;'>
            <h4 style='margin: 0 0 8px 0; color: #1e293b; font-family: sans-serif; font-size: 12px;'>Traçabilité serveur</h4>
            <strong>Message-ID:</strong> {messageId}<br/>";

            if (!string.IsNullOrWhiteSpace(donnees.Header.ReturnPath))
            {
                var safeReturnPath = donnees.Header.ReturnPath.Replace("<", "&lt;").Replace(">", "&gt;");
                blocSignature += $"<strong>Return-Path:</strong> {safeReturnPath}<br/>";
            }

            if (!string.IsNullOrWhiteSpace(donnees.Header.DeliveredTo))
            {
                var safeDeliveredTo = donnees.Header.DeliveredTo.Replace("<", "&lt;").Replace(">", "&gt;");
                blocSignature += $"<strong>Delivered-To:</strong> {safeDeliveredTo}<br/>";
            }
            
            // Séparation pour la sécurité
            blocSignature += "<div style='margin-top: 8px; margin-bottom: 8px; border-top: 1px dashed #cbd5e1;'></div>";

            // Alignement avec un tableau HTML à 3 colonnes égales
            blocSignature += "<table style='width: 100%; font-family: monospace; font-size: 11px; color: #475569; border-collapse: collapse;'><tr>";

            if (!string.IsNullOrWhiteSpace(donnees.Header.Spf))
                blocSignature += $"<td style='width: 33%; text-align: left;'><strong>SPF:</strong> {donnees.Header.Spf}</td>";
            else
                blocSignature += "<td style='width: 33%;'></td>"; // Cellule vide pour conserver l'alignement

            if (!string.IsNullOrWhiteSpace(donnees.Header.Dkim))
                blocSignature += $"<td style='width: 33%; text-align: center;'><strong>DKIM:</strong> {donnees.Header.Dkim}</td>";
            else
                blocSignature += "<td style='width: 33%;'></td>";

            if (!string.IsNullOrWhiteSpace(donnees.Header.Dmarc))
                blocSignature += $"<td style='width: 33%; text-align: right;'><strong>DMARC:</strong> {donnees.Header.Dmarc}</td>";
            else
                blocSignature += "<td style='width: 33%;'></td>";

            blocSignature += "</tr></table>";

            // Sécurité de repli si rien n'a été trouvé mais qu'il y a un Auth-Results
            if (string.IsNullOrWhiteSpace(donnees.Header.Spf) && 
                string.IsNullOrWhiteSpace(donnees.Header.Dkim) && 
                string.IsNullOrWhiteSpace(donnees.Header.Dmarc) && 
                !string.IsNullOrWhiteSpace(donnees.Header.AuthenticationResults))
            {
                blocSignature += $"<div style='margin-top: 8px;'><strong>Auth-Results:</strong> {donnees.Header.AuthenticationResults}</div>";
            }
            
            blocSignature += "</div>";
        }
        
        // LIGNE DE SÉPARATION (Indépendante)
        // Cette ligne se placera toujours en dessous de ce qui précède (en-tête ou signature)
        var separateur = "<hr style='border: 0; height: 1px; background-color: #cbd5e1; margin-bottom: 25px;' />";

        // On assemble l'en-tête, la signature, la ligne de séparation puis le contenu
        var htmlFinal = $"<html><body style='margin: 0; padding: 0;'>{blocEntete}{blocSignature}{separateur}{contenuBody}";

        // Gestion de l'inclusion des pièces jointes à la suite
        if (addAttachmentsToPdf)
        {
            var piecesAides = donnees.PiecesJointes.Where(pj => pj.Compatibilite == CompatibilitePdf.FusionnerDansPdf);

            foreach (var pj in piecesAides)
                if (pj.TypeMime.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                {
                    var contenuTexte = Encoding.UTF8.GetString(pj.Contenu);
                    htmlFinal +=
                        $@"<div style='page-break-before: always; padding-top: 20px; font-family: Arial, sans-serif;'><h3 style='color: #6c757d; border-bottom: 1px solid #dee2e6; padding-bottom: 5px;'>Pièce jointe : {pj.NomFichier}</h3><pre style='background-color: #f8f9fa; border: 1px solid #dee2e6; padding: 15px; font-size: 12px; white-space: pre-wrap; word-wrap: break-word;'>{contenuTexte}</pre></div>";
                }
                else if (pj.TypeMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    var estDejaDansLeCorps = !string.IsNullOrWhiteSpace(pj.ContentId) &&
                                             !string.IsNullOrWhiteSpace(donnees.CorpsHtml) &&
                                             donnees.CorpsHtml.Contains(pj.ContentId, StringComparison.OrdinalIgnoreCase);

                    if (!estDejaDansLeCorps)
                    {
                        var base64String = Convert.ToBase64String(pj.Contenu);
                        var dataUri = $"data:{pj.TypeMime};base64,{base64String}";

                        htmlFinal +=
                            $@"<div style='page-break-before: always; padding-top: 20px; font-family: Arial, sans-serif; text-align: center;'><h3 style='color: #6c757d; border-bottom: 1px solid #dee2e6; padding-bottom: 5px; text-align: left;'>Pièce jointe : {pj.NomFichier}</h3><img src='{dataUri}' style='max-width: 100%; max-height: 950px; object-fit: contain;' alt='{pj.NomFichier}' /></div>";
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
            var base64String = Convert.ToBase64String(pj.Contenu);
            var dataUri = $"data:{pj.TypeMime};base64,{base64String}";
            html = html.Replace($"cid:{pj.ContentId}", dataUri, StringComparison.OrdinalIgnoreCase);
        }

        return html;
    }

    private string NettoyerNomFichier(string nom)
    {
        if (string.IsNullOrWhiteSpace(nom)) return string.Empty;
        foreach (var c in Path.GetInvalidFileNameChars()) nom = nom.Replace(c, '_');
        return nom;
    }
}