using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ReactiveUI;

namespace SaveMail.Services;

public class TranslationService : ReactiveObject
{
    private static readonly Lazy<TranslationService> _instance = new(() => new TranslationService());
    public static TranslationService Instance => _instance.Value;

    private Dictionary<string, string> _translations = new();

    private TranslationService()
    {
        // Charge la langue sauvegardée au démarrage
        LoadLanguage(AppSettingsService.Instance.Current.Language);
    }

    public void LoadLanguage(string langCode)
    {
        // En .NET, le nom d'une ressource embarquée suit ce format : EspaceDeNom.Dossier.Fichier.Extension
        var resourceName = $"SaveMail.Langs.{langCode}.json";
        var assembly = Assembly.GetExecutingAssembly();

        try
        {
            // Tente d'ouvrir le fichier depuis l'intérieur de l'exécutable compilé
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            
            if (stream != null)
            {
                using StreamReader reader = new StreamReader(stream);
                string json = reader.ReadToEnd();
                _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            else
            {
                Console.WriteLine($"[Translation] Langue introuvable : {langCode}. Retour en anglais.");
                // Si le fichier n'existe pas (ex: l'utilisateur a mis "es" dans ses settings mais tu n'as pas compilé es.json)
                // On force le retour sur la langue par défaut (en) pour éviter une interface vide.
                if (langCode != "en")
                {
                    LoadLanguage("en");
                    return;
                }
                _translations = new Dictionary<string, string>();
            }

            // Met à jour les paramètres
            AppSettingsService.Instance.Current.Language = langCode;
            _ = AppSettingsService.Instance.SaveAsync();

            // Notifie l'interface Avalonia
            this.RaisePropertyChanged("Item");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Translation] Erreur de chargement : {ex.Message}");
        }
    }

    // Indexeur permettant de faire TranslationService.Instance["MaCle"]
    public string this[string key] => _translations.TryGetValue(key, out var val) ? val : $"[{key}]";
}