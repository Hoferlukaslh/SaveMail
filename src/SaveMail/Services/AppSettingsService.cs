/*
    Fichier      :  AppSettingsService.cs

    Description  :
        Gère la persistance des préférences utilisateur dans un fichier JSON
        situé dans le répertoire de configuration propre à chaque OS :
            - Windows : %APPDATA%\SaveMail\settings.json
            - macOS   : ~/Library/Application Support/SaveMail/settings.json // pas sûr ...
            - Linux   : ~/.config/SaveMail/settings.json
*/


using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SaveMail.Services;

public class AppSettingsService
{
    // Singleton optimisé avec Lazy pour la thread-safety
    private static readonly Lazy<AppSettingsService> _instance = new(() => new AppSettingsService());
    public static AppSettingsService Instance => _instance.Value;

    /// <summary> Settings chargés en mémoire. </summary>
    public AppSettings Current { get; private set; }

    /// <summary>
    /// Dossier de config spécifique à l'OS.
    /// </summary>
    public static string ConfigDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SaveMail"
        );

    private static string SettingsFilePath =>
        Path.Combine(ConfigDirectory, "settings.json");

    private AppSettingsService()
    {
        // Créer le dossier au démarrage si nécessaire
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }
        
        Load();
    }

    /// <summary> Charge les paramètres depuis le fichier JSON </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                Current = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
            }
            else
            {
                Current = new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] Impossible de lire les settings : {ex.Message}");
            Current = new AppSettings();
        }
    }


    /// <summary> Sauvegarde les paramètres de manière asynchrone </summary>
    public async Task SaveAsync()
    {
        try
        {
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNameCaseInsensitive = true 
            };
            
            var json = JsonSerializer.Serialize(Current, options);
            await File.WriteAllTextAsync(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] Impossible de sauvegarder les settings : {ex.Message}");
        }
    }
}

// Classe de données pour les paramètres
public class AppSettings
{
    // Langue par défaut
    public string Language { get; set; } = "fr"; 
    
    // Dossier de sortie
    public string OutputDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    
    // Contenu du PDF
    public string FileNameFormat { get; set; } = "{yyyy}-{mm}-{dd}_{subject}";
    public bool IncludeSignatures { get; set; } = true;
    public bool AddAttachmentsToPdf { get; set; } = true;

    // Gestion des archives
    public bool ExtractAttachments { get; set; } = true;
    public bool ZipEverything { get; set; } = true;
    public bool KeepOriginalEmail { get; set; } = true;

    // Post-Traitement
    public bool OpenFolderAtEnd { get; set; } = true;
}