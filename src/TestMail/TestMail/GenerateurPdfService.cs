namespace TestMail;

using System;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public class GenerateurPdfService
{
    public GenerateurPdfService()
    {
        // Requis par QuestPDF pour l'utilisation gratuite (Community)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Generer(DonneesMail donnees, string repertoireSortie)
    {
        // 1. Définir le nom du fichier et son chemin
        string sujetNettoye = NettoyerNomFichier(donnees.Header.Subject);
        if (string.IsNullOrWhiteSpace(sujetNettoye)) sujetNettoye = "Email_Sans_Sujet";
        
        string nomFichier = $"{donnees.Header.Date:yyyy-MM-dd}_{sujetNettoye}.pdf";
        string cheminComplet = Path.Combine(repertoireSortie, nomFichier);

        // 2. Création du document PDF
        Document.Create(container =>
        {
            container.Page(page =>
            {
                // Configuration de la page
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial));

                // Structure : En-tête, Corps, Pied de page
                page.Header().Element(c => ComposerEnTete(c, donnees.Header));
                page.Content().Element(c => ComposerContenu(c, donnees));
                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        })
        .GeneratePdf(cheminComplet);

        return cheminComplet;
    }

    private void ComposerEnTete(IContainer container, EnTeteMail header)
    {
        container.Background(Colors.Grey.Lighten4).Padding(10).Column(column =>
        {
            column.Item().Text(header.Subject).FontSize(16).SemiBold().FontColor(Colors.Blue.Darken3);
            column.Spacing(3);
            column.Item().PaddingTop(5).Text($"De : {header.From}");
            
            if (header.To.Any())
                column.Item().Text($"À : {string.Join(", ", header.To)}");
                
            if (header.Cc.Any())
                column.Item().Text($"Cc : {string.Join(", ", header.Cc)}").FontSize(10).FontColor(Colors.Grey.Darken2);

            column.Item().Text($"Date : {header.Date:f}");
        });
    }

    private void ComposerContenu(IContainer container, DonneesMail donnees)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            // 1. Insertion du texte du mail
            string texteAffiche = !string.IsNullOrWhiteSpace(donnees.CorpsTexte) 
                ? donnees.CorpsTexte 
                : "[Le message ne contient pas de texte brut. Le rendu du code HTML complexe est ignoré dans cette version.]";
                
            column.Item().Text(texteAffiche);

            // 2. Traitement des pièces jointes visuelles
            var piecesVisuelles = donnees.PiecesJointes
                .Where(p => p.Compatibilite == CompatibilitePdf.FusionnerDansPdf)
                .ToList();

            if (piecesVisuelles.Any())
            {
                column.Item().PaddingTop(25).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                column.Item().PaddingVertical(10).Text("Pièces jointes incluses :").SemiBold();

                foreach (var pj in piecesVisuelles)
                {
                    column.Item().PaddingBottom(5).Text($"- {pj.NomFichier}").FontSize(10).Italic();

                    // Si c'est une image, QuestPDF peut la dessiner directement
                    if (pj.TypeMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            column.Item().PaddingBottom(15).Image(pj.Contenu);
                        }
                        catch
                        {
                            column.Item().Text("[Erreur lors du rendu de l'image]").FontColor(Colors.Red.Medium);
                        }
                    }
                }
            }
        });
    }

    // Utilitaire pour éviter que des caractères interdits (comme : ou /) fassent planter la création du fichier
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