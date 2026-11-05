using System.Collections.Generic;

namespace BedrockLauncher.Localization.Language
{
    /// <summary>Strings for the Linux-only parts of the launcher (runtimes, GDK import). Not on Crowdin yet.</summary>
    internal static class LinuxStrings
    {
        public static readonly Dictionary<string, Dictionary<string, string>> Tables = new()
        {
            ["en-US"] = new()
            {
                ["Linux_GdkFilesRequired"] = "Minecraft {0} is a GDK build. GDK packages are encrypted and only Windows can install them, so its game files have to be copied from a Windows install and imported.",
                ["Linux_GdkPickFolder"] = "Select the game folder of Minecraft {0} (the folder that holds Minecraft.Windows.exe)",
                ["Linux_GdkMismatch_Title"] = "Different version",
                ["Linux_GdkMismatch_Text"] = "The selected folder holds {0}, but this version needs {1}. Import it anyway?",
                ["Linux_UwpRuntimeMissing"] = "wine-uwp was not found. Build it with runtime/wine-uwp/build.sh (it installs into {0}) or set its folder in Settings > Linux.",
                ["Linux_GdkRuntimeMissing"] = "No GDK runtime was found. Extract a GDK-Proton release into ~/.steam/root/compatibilitytools.d or {0}, or set its folder in Settings > Linux.",
                ["Linux_SettingsTab"] = "Linux",
                ["Linux_Settings_UwpTitle"] = "UWP versions (before 1.21.120)",
                ["Linux_Settings_UwpText"] = "Run with wine-uwp, a Wine fork that implements the UWP/WinRT APIs Minecraft needs.",
                ["Linux_Settings_GdkTitle"] = "GDK versions (1.21.120 and later)",
                ["Linux_Settings_GdkText"] = "Run with GDK-Proton or WineGDK. The game files must be imported from a Windows install: GDK packages are encrypted.",
                ["Linux_Settings_RuntimePath"] = "Runtime folder (empty = detect automatically)",
                ["Linux_Settings_Runtime"] = "Runtime",
                ["Linux_Settings_Detected"] = "Detected runtimes",
                ["Linux_Settings_NoneDetected"] = "None found",
                ["Linux_Settings_Graphics"] = "Graphics and tools",
                ["Linux_Settings_UseDxvk"] = "Use DXVK / vkd3d (Vulkan)",
                ["Linux_Settings_GameMode"] = "Run with Feral GameMode (gamemoderun)",
                ["Linux_Settings_MangoHud"] = "Show MangoHud",
                ["Linux_Settings_Env"] = "Extra environment variables (NAME=value, one per line)",
                ["Linux_Settings_WineDebug"] = "WINEDEBUG",
                ["Linux_Settings_Browse"] = "Browse",
                ["Linux_Settings_OpenRuntimes"] = "Open runtimes folder",
                ["Linux_Settings_OpenLogs"] = "Open logs folder",
                ["Linux_ImportGdk"] = "Import GDK game folder",
                ["Linux_ImportPackage"] = "Import .appx package",
            },
            ["it-IT"] = new()
            {
                ["Linux_GdkFilesRequired"] = "Minecraft {0} è una build GDK. I pacchetti GDK sono cifrati e solo Windows può installarli: i file del gioco vanno copiati da un'installazione Windows e importati.",
                ["Linux_GdkPickFolder"] = "Seleziona la cartella del gioco di Minecraft {0} (quella con Minecraft.Windows.exe)",
                ["Linux_GdkMismatch_Title"] = "Versione diversa",
                ["Linux_GdkMismatch_Text"] = "La cartella scelta contiene {0}, ma questa versione richiede {1}. Importarla comunque?",
                ["Linux_UwpRuntimeMissing"] = "wine-uwp non trovato. Compilalo con runtime/wine-uwp/build.sh (si installa in {0}) oppure indica la cartella in Impostazioni > Linux.",
                ["Linux_GdkRuntimeMissing"] = "Nessun runtime GDK trovato. Estrai una release di GDK-Proton in ~/.steam/root/compatibilitytools.d o in {0}, oppure indica la cartella in Impostazioni > Linux.",
                ["Linux_SettingsTab"] = "Linux",
                ["Linux_Settings_UwpTitle"] = "Versioni UWP (prima della 1.21.120)",
                ["Linux_Settings_UwpText"] = "Avviate con wine-uwp, un fork di Wine che implementa le API UWP/WinRT usate da Minecraft.",
                ["Linux_Settings_GdkTitle"] = "Versioni GDK (1.21.120 e successive)",
                ["Linux_Settings_GdkText"] = "Avviate con GDK-Proton o WineGDK. I file del gioco vanno importati da un'installazione Windows: i pacchetti GDK sono cifrati.",
                ["Linux_Settings_RuntimePath"] = "Cartella del runtime (vuota = rilevamento automatico)",
                ["Linux_Settings_Runtime"] = "Runtime",
                ["Linux_Settings_Detected"] = "Runtime rilevati",
                ["Linux_Settings_NoneDetected"] = "Nessuno trovato",
                ["Linux_Settings_Graphics"] = "Grafica e strumenti",
                ["Linux_Settings_UseDxvk"] = "Usa DXVK / vkd3d (Vulkan)",
                ["Linux_Settings_GameMode"] = "Avvia con Feral GameMode (gamemoderun)",
                ["Linux_Settings_MangoHud"] = "Mostra MangoHud",
                ["Linux_Settings_Env"] = "Variabili d'ambiente extra (NOME=valore, una per riga)",
                ["Linux_Settings_WineDebug"] = "WINEDEBUG",
                ["Linux_Settings_Browse"] = "Sfoglia",
                ["Linux_Settings_OpenRuntimes"] = "Apri cartella runtime",
                ["Linux_Settings_OpenLogs"] = "Apri cartella log",
                ["Linux_ImportGdk"] = "Importa cartella gioco GDK",
                ["Linux_ImportPackage"] = "Importa pacchetto .appx",
            },
        };
    }
}
