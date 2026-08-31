using System.Globalization;
using System.IO;
using System.Windows;

namespace Plana.Desktop.Localization;

internal static class LocalizationCatalog
{
    private static ResourceDictionary? englishDictionary;
    private static ResourceDictionary? currentDictionary;

    public static void ApplyCurrentCulture()
    {
        ApplyCulture(CultureInfo.CurrentUICulture.Name);
    }

    public static void ApplyCulture(string? cultureName)
    {
        var requested = string.IsNullOrWhiteSpace(cultureName) ? "en" : cultureName;
        var culture = CultureInfo.GetCultureInfo(requested);
        englishDictionary ??= Load("en")
            ?? throw new InvalidOperationException("The English localization resources could not be loaded.");
        if (!System.Windows.Application.Current.Resources.MergedDictionaries.Contains(englishDictionary))
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(englishDictionary);
        }

        if (currentDictionary is not null)
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Remove(currentDictionary);
            currentDictionary = null;
        }
        if (!string.Equals(culture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase))
        {
            currentDictionary = Load(culture.Name) ?? Load(culture.TwoLetterISOLanguageName);
            if (currentDictionary is not null)
            {
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(currentDictionary);
            }
        }
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }

    public static string Text(string key, params object[] arguments)
    {
        var value = System.Windows.Application.Current.TryFindResource(key) as string ?? key;
        return arguments.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, arguments);
    }

    private static ResourceDictionary? Load(string cultureName)
    {
        try
        {
            var dictionary = new ResourceDictionary
            {
                Source = new Uri($"/Plana.Desktop;component/Localization/Strings.{cultureName}.xaml", UriKind.RelativeOrAbsolute),
            };
            _ = dictionary.Count;
            return dictionary;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
