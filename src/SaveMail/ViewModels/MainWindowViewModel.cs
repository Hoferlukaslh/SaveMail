using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using SaveMail.Models;

namespace SaveMail.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private bool _extractAttachments = true;
    private bool _archiveUnsupported = false;
    private bool _zipEverything = true;
    private bool _keepOriginalEmail = true;
    private bool _includeHeader = true;
    private bool _addAttachmentsToPdf = true;
    private bool _openFolderAtEnd = true;
    private string _outputDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
    
    public int QueueCount => FilesQueue.Count;
    public int PendingCount => FilesQueue.Count(f => !f.IsCompleted);

    public bool HasFiles => FilesQueue.Any();
    public bool HasPendingFiles => PendingCount > 0;

    public void RefreshQueue()
    {
        this.RaisePropertyChanged(nameof(QueueCount));
        this.RaisePropertyChanged(nameof(HasFiles));
        this.RaisePropertyChanged(nameof(PendingCount));
        this.RaisePropertyChanged(nameof(HasPendingFiles));
    }
    
    public ObservableCollection<FichierMail> FilesQueue { get; } = new();

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => this.RaiseAndSetIfChanged(ref _outputDirectory, value);
    }

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

    public bool IncludeHeader
    {
        get => _includeHeader;
        set => this.RaiseAndSetIfChanged(ref _includeHeader, value);
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
}