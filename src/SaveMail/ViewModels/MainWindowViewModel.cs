using ReactiveUI;

namespace SaveMail.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private bool _extractAttachments = true;
    private bool _archiveUnsupported = false;
    private bool _zipEverything = true;
    private bool _keepOriginalEmail = true;

    public bool ExtractAttachments
    {
        get => _extractAttachments;
        set
        {
            // On vérifie manuellement si la valeur change
            if (_extractAttachments == value) return;

            this.RaiseAndSetIfChanged(ref _extractAttachments, value);
            
            // On notifie l'interface
            this.RaisePropertyChanged(nameof(IsArchiveSectionEnabled));
            this.RaisePropertyChanged(nameof(IsArchiveUnsupportedEnabled));

            // Règles de sécurité
            if (!value)
            {
                ZipEverything = false;
                KeepOriginalEmail = false;
                ArchiveUnsupported = false;
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

            if (value)
            {
                ArchiveUnsupported = false;
            }
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

    // Propriétés calculées pour griser les lignes
    public bool IsArchiveSectionEnabled => ExtractAttachments;
    
    public bool IsArchiveUnsupportedEnabled => ExtractAttachments && !ZipEverything;
}   