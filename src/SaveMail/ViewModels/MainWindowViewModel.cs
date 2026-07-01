using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using SaveMail.Models;


namespace SaveMail.ViewModels;

public enum AppProcessingState
{
    Idle,
    Processing,
    Paused
}

public class MainWindowViewModel : ViewModelBase
{
    private bool _addAttachmentsToPdf = true;
    private bool _archiveUnsupported;
    private bool _extractAttachments = true;
    private bool _hasCheckedUpdate;
    private bool _isInfoModalOpen;
    private bool _isNewVersionAvailable;
    private bool _isUpdateError;

    private bool _isUpToDate;
    private bool _keepOriginalEmail = true;
    private bool _openFolderAtEnd = true;
    private string _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    private AppProcessingState _processingState = AppProcessingState.Idle;

    private string _versionStatus = "Vérification...";
    private bool _zipEverything = true;
    
    private bool _includeSignatures = true;
    private string _fileNameFormat = "{yyyy}-{mm}-{dd}_{subject}";
    
    public bool IncludeSignatures
    {
        get => _includeSignatures;
        set => this.RaiseAndSetIfChanged(ref _includeSignatures, value);
    }

    public string FileNameFormat
    {
        get => _fileNameFormat;
        set => this.RaiseAndSetIfChanged(ref _fileNameFormat, value);
    }


    public bool IsUpToDate
    {
        get => _isUpToDate;
        set => this.RaiseAndSetIfChanged(ref _isUpToDate, value);
    }

    public bool IsUpdateError
    {
        get => _isUpdateError;
        set => this.RaiseAndSetIfChanged(ref _isUpdateError, value);
    }


    public int QueueCount => FilesQueue.Count;
    public int PendingCount => FilesQueue.Count(f => !f.IsCompleted);

    public bool HasFiles => FilesQueue.Any();
    public bool HasPendingFiles => PendingCount > 0;


    public ObservableCollection<FichierMail> FilesQueue { get; } = new();

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => this.RaiseAndSetIfChanged(ref _outputDirectory, value);
    }

    public AppProcessingState ProcessingState
    {
        get => _processingState;
        set
        {
            this.RaiseAndSetIfChanged(ref _processingState, value);
            this.RaisePropertyChanged(nameof(ProcessButtonText));
            this.RaisePropertyChanged(nameof(IsProcessingRed));
        }
    }

    public string ProcessButtonText => ProcessingState switch
    {
        AppProcessingState.Processing => "Arrêter",
        AppProcessingState.Paused => "Reprendre",
        _ => "Convertir"
    };

    // Vrai si on est en train de traiter (pour le bouton rouge)
    public bool IsProcessingRed => ProcessingState == AppProcessingState.Processing;

    public bool ExtractAttachments
    {
        get => _extractAttachments;
        set
        {
            if (_extractAttachments == value) return;
            this.RaiseAndSetIfChanged(ref _extractAttachments, value);
            this.RaisePropertyChanged(nameof(IsArchiveSectionEnabled));
            this.RaisePropertyChanged(nameof(IsArchiveUnsupportedEnabled));
            this.RaisePropertyChanged(nameof(IsAddAttachmentsToPdfEnabled)); // Mise à jour UI

            if (!value)
            {
                ZipEverything = false;
                KeepOriginalEmail = false;
                ArchiveUnsupported = false;
                AddAttachmentsToPdf = false; // Désactivé en cascade
            }
        }
    }

    public bool ZipEverything
    {
        get => _zipEverything;
        set
        {
            if (_zipEverything == value) return;
            this.RaiseAndSetIfChanged(ref _zipEverything, value);
            this.RaisePropertyChanged(nameof(IsArchiveUnsupportedEnabled));

            if (value) ArchiveUnsupported = false;
        }
    }

    public bool ArchiveUnsupported
    {
        get => _archiveUnsupported;
        set => this.RaiseAndSetIfChanged(ref _archiveUnsupported, value);
    }

    public bool KeepOriginalEmail
    {
        get => _keepOriginalEmail;
        set => this.RaiseAndSetIfChanged(ref _keepOriginalEmail, value);
    }
    

    public bool AddAttachmentsToPdf
    {
        get => _addAttachmentsToPdf;
        set => this.RaiseAndSetIfChanged(ref _addAttachmentsToPdf, value);
    }

    public bool OpenFolderAtEnd
    {
        get => _openFolderAtEnd;
        set => this.RaiseAndSetIfChanged(ref _openFolderAtEnd, value);
    }

    public bool IsArchiveSectionEnabled => ExtractAttachments;
    public bool IsArchiveUnsupportedEnabled => ExtractAttachments && !ZipEverything;
    public bool IsAddAttachmentsToPdfEnabled => ExtractAttachments;

    public string AppVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "x.x.x";
        }
    }

    public string VersionStatus
    {
        get => _versionStatus;
        set => this.RaiseAndSetIfChanged(ref _versionStatus, value);
    }

    public bool IsNewVersionAvailable
    {
        get => _isNewVersionAvailable;
        set => this.RaiseAndSetIfChanged(ref _isNewVersionAvailable, value);
    }

    public bool IsInfoModalOpen
    {
        get => _isInfoModalOpen;
        set
        {
            this.RaiseAndSetIfChanged(ref _isInfoModalOpen, value);
            // Déclenche la vérification une seule fois à l'ouverture du modal
            if (value && !_hasCheckedUpdate)
            {
                _hasCheckedUpdate = true;
                Task.Run(() => CheckForUpdatesAsync());
            }
        }
    }

    public void ResetProcessState()
    {
        ProcessingState = AppProcessingState.Idle;
    }

    // 3. Modifiez RefreshQueue pour réinitialiser le bouton quand la liste se vide
    public void RefreshQueue()
    {
        this.RaisePropertyChanged(nameof(QueueCount));
        this.RaisePropertyChanged(nameof(HasFiles));
        this.RaisePropertyChanged(nameof(PendingCount));
        this.RaisePropertyChanged(nameof(HasPendingFiles));

        // Retour automatique au bouton "Convertir" si tout est fini ou vidé
        if (PendingCount == 0) ResetProcessState();
    }

    private async Task CheckForUpdatesAsync()
    {
        // État neutre pendant le chargement
        Dispatcher.UIThread.Post(() =>
        {
            IsUpToDate = false;
            IsNewVersionAvailable = false;
            IsUpdateError = false;
            VersionStatus = "Vérification en cours...";
        });

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SaveMail-App");

            var response = await client.GetAsync("https://api.github.com/repos/Hoferlukaslh/SaveMail/releases/latest");

            if (!response.IsSuccessStatusCode)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        VersionStatus = "Aucune version publiée en ligne";
                    else if (response.StatusCode == HttpStatusCode.Forbidden)
                        VersionStatus = "Limite d'API GitHub atteinte";
                    else
                        VersionStatus = $"Erreur serveur ({response.StatusCode})";

                    IsUpdateError = true; // Active la pastille rouge
                });
                return;
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);

            if (doc.RootElement.TryGetProperty("tag_name", out var tagProperty))
            {
                var tag = tagProperty.GetString() ?? "";
                var cleanTag = tag.TrimStart('v', 'V');

                var localVersion = Assembly.GetExecutingAssembly().GetName().Version;

                if (Version.TryParse(cleanTag, out var remoteVersion) && localVersion != null)
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (remoteVersion > localVersion)
                        {
                            VersionStatus = $"Mise à jour disponible ({tag}) - Cliquez ici";
                            IsNewVersionAvailable = true; // Active la pastille jaune
                        }
                        else
                        {
                            VersionStatus = "Application à jour";
                            IsUpToDate = true; // Active la pastille verte
                        }
                    });
                else
                    Dispatcher.UIThread.Post(() =>
                    {
                        VersionStatus = $"Format de version non reconnu ({tag})";
                        IsUpdateError = true;
                    });
            }
        }
        catch (Exception)
        {
            Dispatcher.UIThread.Post(() =>
            {
                VersionStatus = "Hors ligne ou erreur réseau";
                IsUpdateError = true; // Active la pastille rouge
            });
        }
    }
}