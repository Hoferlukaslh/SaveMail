using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaveMail.Models;
using SaveMail.Services;
using SaveMail.ViewModels;

namespace SaveMail.Views;

public partial class MainWindow : Window
{
    private Border? FindParentBorderWithClass(Visual? element, string className)
    {
        while (element != null)
        {
            if (element is Border border && border.Classes.Contains(className))
            {
                return border;
            }
            element = element.GetVisualParent();
        }
        return null;
    }
    
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
    
    private void DropZone_DragOver(object? sender, DragEventArgs e)
    {
        // Indique au système d'exploitation que la zone accepte les fichiers (nouvelle API)
        if (e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
        }
    }
    
    private void DropZone_DragEnter(object? sender, DragEventArgs e)
    {
        // Si ce qu'on fait glisser contient bien des fichiers, on active l'animation
        if (e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            DropZoneBorder.Classes.Add("drag-active");
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private void DropZone_DragLeave(object? sender, DragEventArgs e)
    {
        // On retire l'animation quand le fichier sort de la zone
        DropZoneBorder.Classes.Remove("drag-active");
    }

    private async void DropZone_Drop(object? sender, DragEventArgs e)
    {
        DropZoneBorder.Classes.Remove("drag-active");
        if (ViewModel == null) return;

        if (e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files == null) return;

            int delayCount = 0; // Compteur pour limiter l'effet visuel aux 15 premiers fichiers

            foreach (var file in files)
            {
                string path = file.Path.LocalPath;

                if (path.EndsWith(".eml", StringComparison.OrdinalIgnoreCase) || 
                    path.EndsWith(".msg", StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.FilesQueue.Add(new FichierMail { Path = path });
                    ViewModel.RefreshQueue(); // Met à jour le compteur texte en direct
                
                    // Petit délai de 40ms pour l'effet cascade
                    if (delayCount < 15) { await Task.Delay(40); delayCount++; }
                }
                else if (Directory.Exists(path))
                {
                    var dirFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(s => s.EndsWith(".eml", StringComparison.OrdinalIgnoreCase) || 
                                    s.EndsWith(".msg", StringComparison.OrdinalIgnoreCase));
                
                    foreach (var f in dirFiles)
                    {
                        ViewModel.FilesQueue.Add(new FichierMail { Path = f });
                        ViewModel.RefreshQueue();
                    
                        if (delayCount < 15) { await Task.Delay(40); delayCount++; }
                    }
                }
            }
        }
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

        int delayCount = 0;
        foreach (var file in files)
        {
            ViewModel.FilesQueue.Add(new FichierMail { Path = file.Path.LocalPath });
            ViewModel.RefreshQueue();
        
            if (delayCount < 15) { await Task.Delay(40); delayCount++; }
        }
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
        
            int delayCount = 0;
            foreach (var file in files)
            {
                ViewModel.FilesQueue.Add(new FichierMail { Path = file });
                ViewModel.RefreshQueue();
            
                if (delayCount < 15) { await Task.Delay(40); delayCount++; }
            }
        }
    }
    
    private async void BtnRemoveItem_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        
        if (sender is Button btn && btn.DataContext is FichierMail file)
        {
            // 1. Trouver l'élément visuel correspondant (la bordure)
            var border = FindParentBorderWithClass(btn, "queue-item");
            
            if (border != null)
            {
                // 2. Ajouter la classe pour déclencher l'animation de sortie
                border.Classes.Add("removing");
                
                // 3. Attendre la durée de l'animation (0.3s)
                await Task.Delay(300);
            }

            // 4. Supprimer réellement l'élément
            ViewModel.FilesQueue.Remove(file);
            ViewModel.RefreshQueue();
        }
    }

    private async void BtnClearQueue_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null || !ViewModel.HasFiles) return;

        // 1. Récupérer l'ItemsControl visuel
        var itemsControl = this.FindControl<ItemsControl>("QueueItemsControl");
        
        if (itemsControl != null)
        {
            int delayCount = 0;

            // 2. Parcourir tous les éléments et déclencher les animations en cascade
            for (int i = 0; i < ViewModel.FilesQueue.Count; i++)
            {
                var container = itemsControl.ContainerFromIndex(i);
                
                if (container is Visual visualContainer)
                {
                    var border = visualContainer.GetVisualDescendants()
                        .OfType<Border>()
                        .FirstOrDefault(b => b.Classes.Contains("queue-item"));

                    if (border != null)
                    {
                        border.Classes.Add("removing");
                        
                        // On reproduit l'effet cascade différé que vous avez déjà à l'ajout
                        if (delayCount < 15) { await Task.Delay(40); delayCount++; }
                    }
                }
            }
            
            // 3. Attendre que la dernière animation en cours se termine
            await Task.Delay(300); 
        }

        // 4. Vider les données une fois que tout a disparu de l'écran
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

        // Démarrage unique de Chromium ---
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
                ViewModel.RefreshQueue();
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