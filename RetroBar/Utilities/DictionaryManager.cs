﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace RetroBar.Utilities
{
    public class DictionaryManager : IDisposable
    {
        private const string DICT_DEFAULT = "System";
        private const string DICT_EXT = "xaml";

        private const string LANG_DEFAULT = DICT_DEFAULT;
        private const string LANG_FALLBACK = "English";
        private const string LANG_FOLDER = "Languages";
        private const string LANG_EXT = DICT_EXT;

        public const string THEME_DEFAULT = DICT_DEFAULT;
        private const string THEME_FOLDER = "Themes";
        private const string THEME_EXT = DICT_EXT;

        public DictionaryManager()
        {
            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
        }

        public void SetThemeFromSettings()
        {
            SetTheme(THEME_DEFAULT);

            if (Settings.Instance.Theme.StartsWith(THEME_DEFAULT))
            {
                SetSystemThemeParams();
            }
            else
            {
                ClearSystemThemeParams();
            }

            if (Settings.Instance.Theme != THEME_DEFAULT)
            {
                SetTheme(Settings.Instance.Theme);
            }
        }

        private void SetSystemThemeParams()
        {
            Application.Current.Resources["GlobalFontFamily"] = SystemFonts.CaptionFontFamily;
        }

        private void ClearSystemThemeParams()
        {
            Application.Current.Resources.Remove("GlobalFontFamily");
        }

        private void SetTheme(string theme)
        {
            SetDictionary(theme, THEME_FOLDER, THEME_DEFAULT, THEME_EXT, 0);
        }

        private static Collection<ResourceDictionary> GetMergedDictionaries()
        {
            return Application.Current.Resources.MergedDictionaries;
        }

        private static ResourceDictionary GetActualThemeDictionary()
        {
            foreach (ResourceDictionary rd in GetMergedDictionaries()
                .Where(rd => rd.Source.ToString().Contains($"{THEME_FOLDER}/")))
            {
                return rd;
            }

            return null;
        }

        private void ClearPreviousThemes()
        {
            if (GetActualThemeDictionary() != null)
            {
                _ = GetMergedDictionaries().Remove(GetActualThemeDictionary());
            }
        }

        public void SetLanguageFromSettings()
        {
            SetLanguage(LANG_FALLBACK);
            if (Settings.Instance.Language == LANG_DEFAULT)
            {
                var currentUICulture = System.Globalization.CultureInfo.CurrentUICulture;
                string systemLanguageParent = currentUICulture.Parent.NativeName;
                string systemLanguage = currentUICulture.NativeName;
                ManagedShell.Common.Logging.ShellLogger.Info
                    ($"Loading system language (if available): {systemLanguageParent}, {systemLanguage}");
                SetLanguage(systemLanguageParent);
                SetLanguage(systemLanguage);
            }
            else
            {
                SetLanguage(Settings.Instance.Language);
            }
        }

        private void SetLanguage(string language)
        {
            SetDictionary(language, LANG_FOLDER, LANG_FALLBACK, LANG_EXT, 1);
        }

        private void SetDictionary(string dictionary, string dictFolder, string dictDefault, string dictExtension, int dictType)
        {
            string dictFilePath;

            if (dictionary == dictDefault)
            {
                if (dictType == 0)
                {
                    ClearPreviousThemes();
                }
                dictFilePath = Path.ChangeExtension(Path.Combine(dictFolder, dictDefault), dictExtension);
            }
            else
            {
                // Built-in dictionary
                dictFilePath = Path.ChangeExtension(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dictFolder, dictionary),
                                                    dictExtension);

                if (!File.Exists(dictFilePath))
                {
                    // Installed dictionary in AppData directory
                    dictFilePath = 
                        Path.ChangeExtension(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroBar", dictFolder, dictionary),
                                             dictExtension);

                    if (!File.Exists(dictFilePath))
                    {
                        // Custom dictionary in app directory
                        dictFilePath =
                            Path.ChangeExtension(Path.Combine(Path.GetDirectoryName(ExePath.GetExecutablePath()), dictFolder, dictionary),
                                                 dictExtension);

                        if (!File.Exists(dictFilePath))
                        {
                            return;
                        }
                    }
                }
            }

            try
            {
                GetMergedDictionaries().Add(new ResourceDictionary()
                {
                    Source = new Uri(dictFilePath, UriKind.RelativeOrAbsolute)
                });
            }
            catch (Exception e)
            {
                ManagedShell.Common.Logging.ShellLogger.Error($"Error loading dictionaries: {e.Message} {e.InnerException?.Message}");
            }
        }

        public List<string> GetThemes()
        {
            return GetDictionaries(THEME_DEFAULT, THEME_FOLDER, THEME_EXT);
        }

        public List<string> GetLanguages()
        {
            List<string> languages = new List<string> { LANG_DEFAULT };
            languages.AddRange(GetDictionaries(LANG_FALLBACK, LANG_FOLDER, LANG_EXT));
            return languages;
        }

        private List<string> GetDictionaries(string dictDefault, string dictFolder, string dictExtension)
        {
            List<string> dictionaries = new List<string> { dictDefault };

            // Built-in dictionaries
            foreach (string subStr in Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dictFolder))
                                               .Where(s => Path.GetExtension(s).Contains(dictExtension)))
            {
                dictionaries.Add(Path.GetFileNameWithoutExtension(subStr));
            }

            // Installed AppData dictionaries
            string installedDictDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroBar", dictFolder);

            if (Directory.Exists(installedDictDir))
            {
                foreach (string subStr in Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroBar", dictFolder))
                                                   .Where(s => Path.GetExtension(s).Contains(dictExtension)))
                {
                    dictionaries.Add(Path.GetFileNameWithoutExtension(subStr));
                }
            }

            // Same-folder dictionaries
            // Because RetroBar is published as a single-file app, it gets extracted to a temp directory, so custom dictionaries won't be there.
            // Get the executable path to find the custom dictionaries directory when not a debug build.
            string customDictDir = Path.Combine(Path.GetDirectoryName(ExePath.GetExecutablePath()), dictFolder);

            if (Directory.Exists(customDictDir))
            {
                foreach (string subStr in Directory.GetFiles(customDictDir)
                    .Where(s => Path.GetExtension(s).Contains(dictExtension) && !dictionaries.Contains(Path.GetFileNameWithoutExtension(s))))
                {
                    dictionaries.Add(Path.GetFileNameWithoutExtension(subStr));
                }
            }

            return dictionaries;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Language")
            {
                SetLanguageFromSettings();
            }
            if (e.PropertyName == "Theme")
            {
                SetThemeFromSettings();
            }
        }

        public void Dispose()
        {
            Settings.Instance.PropertyChanged -= Settings_PropertyChanged;
        }
    }
}