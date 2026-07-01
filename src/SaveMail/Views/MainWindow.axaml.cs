using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using SaveMail.Models;
using SaveMail.Services;
using SaveMail.ViewModels;

namespace SaveMail.Views;

public partial class MainWindow : Window
{
    private bool _isProcessingLoopActive; // Sécurité pour empêcher les doublons

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private Border? FindParentBorderWithClass(Visual? element, string className)
    {
        while (element != null)
        {
            if (element is Border border && border.Classes.Contains(className)) return border;
            element = element.GetVisualParent();
        }

        return null;
    }

    private void DropZone_DragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(DataFormat.File)) e.DragEffects = DragDropEffects.Copy;
    }

    private void DropZone_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            DropZoneBorder.Classes.Add("drag-active");
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private void DropZone_DragLeave(object? sender, DragEventArgs e)
    {
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

            var delayCount = 0;

            foreach (var file in files)
            {
                var path = file.Path.LocalPath;

                if (path.EndsWith(".eml", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".msg", StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.FilesQueue.Add(new FichierMail { Path = path });
                    ViewModel.RefreshQueue();

                    if (delayCount < 15)
                    {
                        await Task.Delay(40);
                        delayCount++;
                    }
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

                        if (delayCount < 15)
                        {
                            await Task.Delay(40);
                            delayCount++;
                        }
                    }
                }
            }
        }
    }

    private async void BtnAddFiles_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var topLevel = GetTopLevel(this);
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

        var delayCount = 0;
        foreach (var file in files)
        {
            ViewModel.FilesQueue.Add(new FichierMail { Path = file.Path.LocalPath });
            ViewModel.RefreshQueue();

            if (delayCount < 15)
            {
                await Task.Delay(40);
                delayCount++;
            }
        }
    }

    private async void BtnAddFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var topLevel = GetTopLevel(this);
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

            var delayCount = 0;
            foreach (var file in files)
            {
                ViewModel.FilesQueue.Add(new FichierMail { Path = file });
                ViewModel.RefreshQueue();

                if (delayCount < 15)
                {
                    await Task.Delay(40);
                    delayCount++;
                }
            }
        }
    }

    private async void BtnRemoveItem_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        if (sender is Button btn && btn.DataContext is FichierMail file)
        {
            var border = FindParentBorderWithClass(btn, "queue-item");

            if (border != null)
            {
                border.Classes.Add("removing");
                await Task.Delay(300);
            }

            ViewModel.FilesQueue.Remove(file);
            ViewModel.RefreshQueue();
        }
    }

    private async void BtnClearQueue_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null || !ViewModel.HasFiles) return;

        var itemsControl = this.FindControl<ItemsControl>("QueueItemsControl");

        if (itemsControl != null)
        {
            var delayCount = 0;

            for (var i = 0; i < ViewModel.FilesQueue.Count; i++)
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

                        if (delayCount < 15)
                        {
                            await Task.Delay(40);
                            delayCount++;
                        }
                    }
                }
            }

            await Task.Delay(300);
        }

        ViewModel.FilesQueue.Clear();
        ViewModel.RefreshQueue();
    }

    private async void BtnSelectOutput_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Dossier de sortie"
        });

        if (folders.Any()) ViewModel.OutputDirectory = folders.First().Path.LocalPath;
    }

    private async void BtnProcess_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null || !ViewModel.HasFiles) return;

        // 1. On vérifie d'ABORD si l'état actuel est déjà sur "Processing"
        if (ViewModel.ProcessingState == AppProcessingState.Processing)
        {
            ViewModel.ProcessingState = AppProcessingState.Paused;
            return; // On sort pour laisser la boucle s'arrêter
        }

        // 2. Si ce n'est pas le cas, on passe en mode Processing
        ViewModel.ProcessingState = AppProcessingState.Processing;

        // 3. Sécurité pour éviter de lancer plusieurs boucles en même temps
        if (_isProcessingLoopActive) return;
        _isProcessingLoopActive = true;

        // Flag pour savoir si l'utilisateur a interrompu le processus
        bool wasInterrupted = false;

        try
        {
            var extracteur = new ExtracteurMailService();
            var fusionPdf = new FusionPdfService();
            var generateurZip = new GenerateurZipService();

            await using var generateurPdf = new GenerateurPdfHtmlService();

            var filesToProcess = ViewModel.FilesQueue.Where(f => !f.IsCompleted).ToList();
            if (!filesToProcess.Any()) return;

            var extractAttachments = ViewModel.ExtractAttachments;
            var zipEverything = ViewModel.ZipEverything;
            var archiveUnsupported = ViewModel.ArchiveUnsupported;
            var keepOriginalEmail = ViewModel.KeepOriginalEmail;
            var includeSignatures = ViewModel.IncludeSignatures;
            var fileNameFormat = ViewModel.FileNameFormat;
            var addAttachmentsToPdf = ViewModel.AddAttachmentsToPdf;
            var outputDirectory = ViewModel.OutputDirectory;
            var openFolderAtEnd = ViewModel.OpenFolderAtEnd;

            var premierFichier = filesToProcess.First();
            premierFichier.IsProcessing = true;
            premierFichier.StatusText = "Initialisation du moteur PDF...";

            await generateurPdf.InitialiserNavigateurAsync();

            foreach (var fichierMail in filesToProcess)
            {
                // Vérification à chaque nouveau fichier : l'utilisateur a-t-il mis en pause ?
                if (ViewModel.ProcessingState != AppProcessingState.Processing)
                {
                    wasInterrupted = true; // On marque comme interrompu

                    if (fichierMail.IsProcessing && !fichierMail.IsCompleted)
                    {
                        fichierMail.IsProcessing = false;
                        fichierMail.StatusText = "En attente (stoppé)";
                        fichierMail.Progress = 0;
                    }

                    break;
                }

                try
                {
                    fichierMail.HasError = false;
                    fichierMail.IsCompleted = false;
                    fichierMail.HasWarning = false;
                    fichierMail.IsProcessing = true;
                    fichierMail.Progress = 10;
                    fichierMail.StatusText = "Lecture du fichier...";

                    var filePath = fichierMail.Path;

                    var donnees = await Task.Run(() => extracteur.Extraire(fichierMail));
                    fichierMail.Progress = 40;
                    fichierMail.StatusText = "Génération du PDF...";

                    var piecesIgnorees = 0;

                    if (!extractAttachments)
                        piecesIgnorees = donnees.PiecesJointes.Count;
                    else if (!zipEverything)
                        piecesIgnorees = donnees.PiecesJointes.Count(pj =>
                            (pj.Compatibilite == CompatibilitePdf.FusionnerDansPdf && !addAttachmentsToPdf) ||
                            (pj.Compatibilite == CompatibilitePdf.ExtraireDansZip && !archiveUnsupported));

                    if (piecesIgnorees > 0) fichierMail.HasWarning = true;

                    var pdfPath = await generateurPdf.GenererAsync(donnees, outputDirectory, includeSignatures,
                        addAttachmentsToPdf, fileNameFormat);
                    fichierMail.Progress = 70;

                    if (extractAttachments && addAttachmentsToPdf)
                    {
                        fichierMail.StatusText = "Fusion des pièces jointes...";
                        pdfPath = await Task.Run(() =>
                            fusionPdf.FusionnerPiecesJointes(pdfPath, donnees.PiecesJointes));
                    }

                    fichierMail.Progress = 85;

                    if (zipEverything)
                    {
                        fichierMail.StatusText = "Création de l'archive...";
                        await Task.Run(() =>
                            generateurZip.CreerArchiveComplete(donnees, filePath, pdfPath, keepOriginalEmail));

                        if (File.Exists(pdfPath)) File.Delete(pdfPath);
                    }
                    else if (archiveUnsupported && extractAttachments)
                    {
                        fichierMail.StatusText = "Archivage des fichiers complexes...";
                        await Task.Run(() => generateurZip.CreerArchive(donnees, pdfPath));
                    }

                    fichierMail.Progress = 100;
                    fichierMail.IsProcessing = false;
                    fichierMail.IsCompleted = true;

                    if (fichierMail.HasWarning)
                        fichierMail.StatusText =
                            $"Terminé (Avertissement : {piecesIgnorees} pièce(s) ignorée(s) avec ces paramètres).";
                    else
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

            // Ouverture du dossier si terminé proprement (et non interrompu)
            if (!wasInterrupted && openFolderAtEnd)
            {
                try
                {
                    var fullPath = Path.GetFullPath(outputDirectory);

                    if (Directory.Exists(fullPath))
                    {
                        ProcessStartInfo startInfo;
                        
                        if (OperatingSystem.IsLinux())
                        {
                            startInfo = new ProcessStartInfo("xdg-open", $"\"{fullPath}\"") { UseShellExecute = true };
                        }
                        else
                        {
                            startInfo = new ProcessStartInfo(fullPath) { UseShellExecute = true };
                        }

                        Process.Start(startInfo);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Impossible d'ouvrir le dossier de sortie : {ex.Message}");
                }
            }
        }
        finally
        {
            _isProcessingLoopActive = false;

            // On repasse à l'état de base si on a tout fini
            if (ViewModel.ProcessingState == AppProcessingState.Processing) ViewModel.ResetProcessState();
        }
    }

    private void BtnInfo_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null) ViewModel.IsInfoModalOpen = true;
    }

    private void BtnCloseInfo_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null) ViewModel.IsInfoModalOpen = false;
    }

    private void LinkRepo_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OuvrirLienWeb("https://github.com/Hoferlukaslh/SaveMail/");
    }

    private void LinkLicense_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OuvrirLienWeb("https://www.gnu.org/licenses/gpl-3.0.html");
    }

    private void OuvrirLienWeb(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Impossible d'ouvrir le lien : {ex.Message}");
        }
    }

    private void LinkReleases_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OuvrirLienWeb("https://github.com/Hoferlukaslh/SaveMail/releases");
    }
    
    private void LinkSupport_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OuvrirLienWeb("https://buymeacoffee.com/hoferlukaslh");
    }
}