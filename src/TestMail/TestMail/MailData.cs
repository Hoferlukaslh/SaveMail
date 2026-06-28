namespace TestMail;

using System;
using System.Collections.Generic;

// Définit l'action à appliquer à la pièce jointe lors de la conversion
public enum CompatibilitePdf
{
    NonDefini,
    FusionnerDansPdf, // Fichiers visuels ou simples (images, textes, PDF)
    ExtraireDansZip   // Fichiers complexes (vidéos, documents Office, etc.)
}

// Représente le fichier email brut sur le disque (ce que l'utilisateur glisse-dépose)
public class FichierMail
{
    public string Path { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty; // Permettra d'identifier si c'est un .eml ou un .msg

    // Création de l'objet mail
    public FichierMail(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        Extension =  System.IO.Path.GetExtension(path);

        Console.WriteLine($"FichierMail path: {Path}");
        Console.WriteLine($"FichierMail extension: {Extension}");
    }
}

public class EnTeteMail
{
    // --- Informations Générales ---
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public DateTime Date { get; set; }

    // --- Identité et Routage (Suivi) ---
    public string MessageId { get; set; } = string.Empty;
    public List<string> ReceivedHeaders { get; set; } = new(); // Historique du chemin parcouru
    public string InReplyTo { get; set; } = string.Empty;      // Pour le suivi des fils de discussion
    
    // --- Signature et Authentification ---
    public bool EstSigne { get; set; } // Indique si le mail a été signé (DKIM, S/MIME, PGP)
    public string SignatureAlgorithm { get; set; } = string.Empty;
    public string AuthenticationResults { get; set; } = string.Empty; // Résultat SPF/DKIM/DMARC
}

public class DonneesMail
{
    // Intégration de l'en-tête dans les données de l'email
    public EnTeteMail Header { get; set; } = new();
    
    // Contenu de l'email
    public string CorpsTexte { get; set; } = string.Empty;
    public string CorpsHtml { get; set; } = string.Empty;
    
    // Liste des fichiers extraits
    public List<PieceJointe> PiecesJointes { get; set; } = new();
}

// Représente un fichier extrait du mail
public class PieceJointe
{
    public string NomFichier { get; set; } = string.Empty;
    public string TypeMime { get; set; } = string.Empty; // Utile pour identifier le type de fichier (ex: image/png, video/mp4)
    public byte[] Contenu { get; set; } = Array.Empty<byte>(); // Les données brutes du fichier
    
    // Propriété clé pour la logique de tri utilisant l'énumération
    public CompatibilitePdf Compatibilite { get; set; } = CompatibilitePdf.NonDefini;
    
    public string ContentId { get; set; } = string.Empty;
}