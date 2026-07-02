using System;
using System.IO;
using System.Linq;
using MimeKit;
using MsgReader.Outlook;
using SaveMail.Models;
using System.Text.RegularExpressions;

namespace SaveMail.Services;

public class ExtracteurMailService
{
    public DonneesMail Extraire(FichierMail fichierMail)
    {
        if (!File.Exists(fichierMail.Path))
            throw new FileNotFoundException($"Le fichier {fichierMail.Path} est introuvable.");

        var extension = fichierMail.Extension.ToLowerInvariant();

        return extension switch
        {
            ".eml" => ExtraireEml(fichierMail.Path),
            ".msg" => ExtraireMsg(fichierMail.Path),
            _ => throw new NotSupportedException(
                $"Le format {extension} n'est pas pris en charge. Veuillez utiliser .eml ou .msg.")
        };
    }

    private DonneesMail ExtraireEml(string cheminFichier)
    {
        using var stream = File.OpenRead(cheminFichier);
        using var mimeMessage = MimeMessage.Load(stream);

        var donnees = new DonneesMail();

        donnees.Header.MessageId = mimeMessage.MessageId ?? string.Empty;
        donnees.Header.Subject = mimeMessage.Subject ?? string.Empty;
        donnees.Header.Date = mimeMessage.Date.DateTime;
        donnees.Header.From = Enumerable.FirstOrDefault(mimeMessage.From.Mailboxes)?.Address ?? string.Empty;
        donnees.Header.ReplyTo = Enumerable.FirstOrDefault(mimeMessage.ReplyTo.Mailboxes)?.Address ?? string.Empty;
        donnees.Header.To = Enumerable.ToList(Enumerable.Select(mimeMessage.To.Mailboxes, m => m.Address));
        donnees.Header.Cc = Enumerable.ToList(Enumerable.Select(mimeMessage.Cc.Mailboxes, m => m.Address));
        donnees.Header.InReplyTo = mimeMessage.InReplyTo ?? string.Empty;
        
        donnees.Header.ReturnPath = mimeMessage.Headers["Return-Path"]?.Trim() ?? string.Empty;
        donnees.Header.DeliveredTo = mimeMessage.Headers["Delivered-To"]?.Trim() ?? string.Empty;

        donnees.CorpsTexte = mimeMessage.TextBody ?? string.Empty;
        donnees.CorpsHtml = mimeMessage.HtmlBody ?? string.Empty;
        
        // extraire les résultats d'authentification
        donnees.Header.AuthenticationResults = mimeMessage.Headers["Authentication-Results"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(donnees.Header.AuthenticationResults))
        {
            // Parfois stocké dans ARC-Authentication-Results par Google/Microsoft
            donnees.Header.AuthenticationResults = mimeMessage.Headers["ARC-Authentication-Results"]?.Trim() ?? string.Empty;
        }
        
        var authResults = donnees.Header.AuthenticationResults;
        if (!string.IsNullOrWhiteSpace(authResults))
        {
            // Cherche le statut de SPF (ex: spf=pass)
            var matchSpf = Regex.Match(authResults, @"spf=([a-z]+)", RegexOptions.IgnoreCase);
            if (matchSpf.Success) donnees.Header.Spf = matchSpf.Groups[1].Value.ToUpper();

            // Cherche le statut de DKIM (ex: dkim=pass)
            var matchDkim = Regex.Match(authResults, @"dkim=([a-z]+)", RegexOptions.IgnoreCase);
            if (matchDkim.Success) donnees.Header.Dkim = matchDkim.Groups[1].Value.ToUpper();

            // Cherche le statut de DMARC (ex: dmarc=pass)
            var matchDmarc = Regex.Match(authResults, @"dmarc=([a-z]+)", RegexOptions.IgnoreCase);
            if (matchDmarc.Success) donnees.Header.Dmarc = matchDmarc.Groups[1].Value.ToUpper();
        }
        
        foreach (var attachment in mimeMessage.Attachments)
        {
            if (attachment is MimePart part)
            {
                var pieceJointe = new PieceJointe
                {
                    NomFichier = part.FileName ?? TranslationService.Instance["UntitledFile"],
                    TypeMime = part.ContentType.MimeType,
                    ContentId = part.ContentId ?? string.Empty
                };

                using var memoryStream = new MemoryStream();
                part.Content?.DecodeTo(memoryStream);
                pieceJointe.Contenu = memoryStream.ToArray();
                pieceJointe.Compatibilite = DeterminerCompatibilite(pieceJointe.TypeMime);

                donnees.PiecesJointes.Add(pieceJointe);
            }
        }

        return donnees;
    }

    private DonneesMail ExtraireMsg(string cheminFichier)
    {
        var donnees = new DonneesMail();
        using var msg = new Storage.Message(cheminFichier);

        donnees.Header.Subject = msg.Subject ?? string.Empty;
        donnees.Header.Date = (msg.SentOn ?? msg.ReceivedOn ?? DateTimeOffset.Now).DateTime;
        donnees.Header.From = msg.Sender?.Email ?? msg.Sender?.DisplayName ?? string.Empty;
        

        foreach (var recipient in msg.Recipients)
        {
            if (string.IsNullOrWhiteSpace(recipient.Email)) continue;

            if (recipient.Type == RecipientType.To)
                donnees.Header.To.Add(recipient.Email);
            else if (recipient.Type == RecipientType.Cc)
                donnees.Header.Cc.Add(recipient.Email);
        }

        donnees.CorpsTexte = msg.BodyText ?? string.Empty;
        donnees.CorpsHtml = msg.BodyHtml ?? string.Empty;

        foreach (var attachment in msg.Attachments)
            if (attachment is Storage.Attachment msgAttachment)
            {
                var nomFichier = msgAttachment.FileName ?? TranslationService.Instance["UntitledFile"];
                var typeMimeDevine = MimeTypes.GetMimeType(nomFichier);

                var pieceJointe = new PieceJointe
                {
                    NomFichier = nomFichier,
                    TypeMime = typeMimeDevine,
                    Contenu = msgAttachment.Data,
                    ContentId = msgAttachment.ContentId ?? string.Empty
                };

                pieceJointe.Compatibilite = DeterminerCompatibilite(pieceJointe.TypeMime);
                donnees.PiecesJointes.Add(pieceJointe);
            }

        return donnees;
    }

    private CompatibilitePdf DeterminerCompatibilite(string typeMime)
    {
        if (string.IsNullOrWhiteSpace(typeMime)) return CompatibilitePdf.ExtraireDansZip;

        var typeMimeNettoye = typeMime.ToLowerInvariant().Trim();

        var typesImages = new[]
        {
            "image/jpeg", "image/jpg", "image/png", "image/gif",
            "image/bmp", "image/webp", "image/tiff", "image/svg+xml"
        };

        var typesTextes = new[]
        {
            "text/plain", "text/csv", "text/html",
            "text/xml", "text/markdown", "application/json"
        };

        var typesDocuments = new[] { "application/pdf" };

        if (Enumerable.Contains(typesImages, typeMimeNettoye) ||
            Enumerable.Contains(typesTextes, typeMimeNettoye) ||
            Enumerable.Contains(typesDocuments, typeMimeNettoye))
            return CompatibilitePdf.FusionnerDansPdf;

        return CompatibilitePdf.ExtraireDansZip;
    }
}