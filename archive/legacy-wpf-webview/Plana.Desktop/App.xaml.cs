using System.Configuration;
using System.Data;
using System.Windows;
using Plana.Desktop.Localization;

namespace Plana.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private void OnStartup(object sender, StartupEventArgs e) => LocalizationCatalog.ApplyCurrentCulture();
}

