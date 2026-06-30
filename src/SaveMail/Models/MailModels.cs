using System;
using System.Collections.Generic;
using System.IO;
using ReactiveUI;

namespace SaveMail.Models;

public class FichierMail : ReactiveObject
{
    public string Path { get; set; } = string.Empty;
    public string Name => System.IO.Path.GetFileName(Path);
    public string Extension => System.IO.Path.GetExtension(Path);
    
    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => this.RaiseAndSetIfChanged(ref _hasError, value);
    }
    
    private bool _hasWarning;
    public bool HasWarning
    {
        get => _hasWarning;
        set => this.RaiseAndSetIfChanged(ref _hasWarning, value); // Ou la méthode de notification utilisée par votre modèle
    }
    
    public string Size
    {
        get
        {
            if (!File.Exists(Path)) return "Inconnu";
            long length = new FileInfo(Path).Length;
            if (length >= 1048576) return $"{length / 1048576.0:F1} MB";
            return $"{length / 1024.0:F1} KB";
        }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    private string _statusText = "En attente";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
    }

    private bool _isCompleted;
    public bool IsCompleted
    {
        get => _isCompleted;
        set => this.RaiseAndSetIfChanged(ref _isCompleted, value);
    }
}

public class DonneesMail
{
    public EnTeteMail Header { get; set; } = new();
    public string CorpsTexte { get; set; } = string.Empty;
    public string CorpsHtml { get; set; } = string.Empty;
    public List<PieceJointe> PiecesJointes { get; set; } = new();
}

public class EnTeteMail
{
    public string MessageId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public string InReplyTo { get; set; } = string.Empty;
    public string AuthenticationResults { get; set; } = string.Empty;
}

public class PieceJointe
{
    public string NomFichier { get; set; } = string.Empty;
    public string TypeMime { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public byte[] Contenu { get; set; } = Array.Empty<byte>();
    public CompatibilitePdf Compatibilite { get; set; }
}

public enum CompatibilitePdf
{
    FusionnerDansPdf,
    ExtraireDansZip
}