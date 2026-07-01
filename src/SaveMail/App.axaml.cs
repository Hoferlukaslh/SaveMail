using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SaveMail.ViewModels;
using SaveMail.Views;

namespace SaveMail;

public class App : Application
{
    public override void Initialize()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };

        base.OnFrameworkInitializationCompleted();
    }
}