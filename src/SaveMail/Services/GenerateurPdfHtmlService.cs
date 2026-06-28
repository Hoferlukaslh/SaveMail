using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using SaveMail.Models;

namespace SaveMail.Services;

public class GenerateurPdfHtmlService
{
    public async Task<string> GenererAsync(DonneesMail donnees, string repertoireSortie)
    {
        string sujetNettoye = NettoyerNomFichier(donnees.Header.Subject);
        if (string.IsNullOrWhiteSpace(sujetNettoye)) sujetNettoye = "Email_Sans_Sujet";
        
        string nomFichier = $"{donnees.Header.Date:yyyy-MM-dd}_{sujetNettoye}.pdf";
        string cheminComplet = Path.Combine(repertoireSortie, nomFichier);

        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        await using var page = await browser.NewPageAsync();

        string htmlFinal = ConstruireHtml(donnees);

        await page.SetContentAsync(htmlFinal);

        await page.PdfAsync(cheminComplet, new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" }
        });

        return cheminComplet;
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