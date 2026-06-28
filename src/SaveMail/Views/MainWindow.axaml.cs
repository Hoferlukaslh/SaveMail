using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaveMail.Models;
using SaveMail.Services;
using SaveMail.ViewModels;

namespace SaveMail.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
    
    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void BtnAddFiles_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Sélectionner des emails",
            AllowMultiple = true,
            FileTypeFilter = new[] 
            { 
                new FilePickerFileType("Emails") { Patterns = new[] { "*.eml", "*.msg" } } 
            }
        });

        foreach (var file in files)
        {
            ViewModel.FilesQueue.Add(new FichierMail { Path = file.Path.LocalPath });
        }
        ViewModel.RefreshQueue();
    }

    private async void BtnAddFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Sélectionner un dossier",
            AllowMultiple = false
        });

        if (folders.Any())
        {
            var path = folders.First().Path.LocalPath;
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                                 .Where(s => s.EndsWith(".eml", StringComparison.OrdinalIgnoreCase) || 
                                             s.EndsWith(".msg", StringComparison.OrdinalIgnoreCase));
            
            foreach (var file in files)
            {
                ViewModel.FilesQueue.Add(new FichierMail { Path = file });
            }
            ViewModel.RefreshQueue();
        }
    }
    
    private void BtnRemoveItem_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        
        // On récupère le bouton qui a été cliqué, puis on extrait le FichierMail associé à sa ligne
        if (sender is Button btn && btn.DataContext is FichierMail file)
        {
            ViewModel.FilesQueue.Remove(file);
            ViewModel.RefreshQueue();
        }
    }

    private void BtnClearQueue_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        ViewModel.FilesQueue.Clear();
        ViewModel.RefreshQueue();
    }

    private async void BtnSelectOutput_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Dossier de sortie"
        });

        if (folders.Any())
        {
            ViewModel.OutputDirectory = folders.First().Path.LocalPath;
        }
    }

    private async void BtnProcess_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null || !ViewModel.HasFiles) return;

        var extracteur = new ExtracteurMailService();
        var fusionPdf = new FusionPdfService();
        var generateurZip = new GenerateurZipService();

        // On instancie le générateur PDF avec 'await using' pour qu'il ferme Chromium tout seul à la fin
        await using var generateurPdf = new GenerateurPdfHtmlService();

        var filesToProcess = ViewModel.FilesQueue.Where(f => !f.IsCompleted).ToList();
        if (!filesToProcess.Any()) return;

        // --- NOUVEAUTÉ : Démarrage unique de Chromium ---
        // On met à jour l'UI du premier fichier pour montrer qu'on prépare le moteur PDF
        var premierFichier = filesToProcess.First();
        premierFichier.IsProcessing = true;
        premierFichier.StatusText = "Initialisation du moteur PDF...";
        
        await generateurPdf.InitialiserNavigateurAsync();
        // ------------------------------------------------

        foreach (var fichierMail in filesToProcess)
        {
            try
            {
                fichierMail.HasError = false; 
                fichierMail.IsCompleted = false;
                fichierMail.IsProcessing = true;
                fichierMail.Progress = 10;
                fichierMail.StatusText = "Lecture du fichier...";

                var donnees = await Task.Run(() => extracteur.Extraire(fichierMail));
                fichierMail.Progress = 40;
                fichierMail.StatusText = "Génération du PDF...";

                // L'appel est le même, mais il utilise l'instance Chromium déjà ouverte
                string pdfPath = await generateurPdf.GenererAsync(donnees, ViewModel.OutputDirectory);
                fichierMail.Progress = 70;

                if (ViewModel.ExtractAttachments)
                {
                    fichierMail.StatusText = "Fusion des pièces jointes...";
                    pdfPath = await Task.Run(() => fusionPdf.FusionnerPiecesJointes(pdfPath, donnees.PiecesJointes));
                }
                
                fichierMail.Progress = 85;

                if (ViewModel.ZipEverything)
                {
                    fichierMail.StatusText = "Création de l'archive...";
                    await Task.Run(() => generateurZip.CreerArchiveComplete(donnees, fichierMail.Path, pdfPath));
                }
                else if (ViewModel.ArchiveUnsupported && ViewModel.ExtractAttachments)
                {
                    fichierMail.StatusText = "Archivage des fichiers complexes...";
                    await Task.Run(() => generateurZip.CreerArchive(donnees, pdfPath));
                }

                fichierMail.Progress = 100;
                fichierMail.IsProcessing = false;
                fichierMail.IsCompleted = true;
                fichierMail.StatusText = "Terminé";
            }
            catch (Exception ex)
            {
                fichierMail.IsProcessing = false;
                fichierMail.IsCompleted = false;
                fichierMail.HasError = true;
                fichierMail.StatusText = ex.Message.Length > 25 ? "Erreur de conversion" : ex.Message;
                Console.WriteLine($"Erreur avec le fichier {fichierMail.Name} : {ex.Message}");
            }
        }

        if (ViewModel.OpenFolderAtEnd && Directory.Exists(ViewModel.OutputDirectory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ViewModel.OutputDirectory,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }
}