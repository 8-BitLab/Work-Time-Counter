// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        CustomTheme.cs                                               ║
// ║  PURPOSE:     CUSTOM THEME MODEL — PALETTE COLORS, FONTS, PRESETS          ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Stores all custom theme settings: background, text, accent, card, button  ║
// ║  colors, font family, font size, bold/italic. Supports save/load of named  ║
// ║  presets to local JSON storage. Custom theme overrides both dark and light  ║
// ║  modes when activated.                                                     ║
// ║                                                                            ║
// ║  STORAGE LOCATION:                                                         ║
// ║    %AppData%\WorkTimeCounter\themes\                                       ║
// ║    - custom_theme.json  → active theme + enabled flag                      ║
// ║    - presets\*.json     → saved named presets                              ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    /// <summary>
    /// Custom theme data model — holds all palette colors and font settings.
    /// When Enabled == true, this overrides both dark and light mode.
    /// Supports save/load of presets to JSON files in AppData\WorkTimeCounter\themes\.
    /// </summary>
    public class CustomTheme
    {
        // ═══════════════════════════════════════════════════════════════════════
        // PALETTE COLORS (stored as hex strings "#RRGGBB")
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Main background color (form, main area)</summary>
        public string BackgroundColor { get; set; } = "#181C24";

        /// <summary>Panel/card background color (sidebar, cards, panels)</summary>
        public string CardColor { get; set; } = "#1E242E";

        /// <summary>Input field background color (textboxes, combo boxes)</summary>
        public string InputColor { get; set; } = "#262C38";

        /// <summary>Primary text color (main labels, titles)</summary>
        public string TextColor { get; set; } = "#DCE0E6";

        /// <summary>Secondary text color (descriptions, hints)</summary>
        public string SecondaryTextColor { get; set; } = "#788291";

        /// <summary>Accent color (buttons, highlights, active elements)</summary>
        public string AccentColor { get; set; } = "#FF7F50";

        /// <summary>Start/working button color (green by default)</summary>
        public string StartColor { get; set; } = "#22C55E";

        /// <summary>Stop button color (red by default)</summary>
        public string StopColor { get; set; } = "#EF4444";

        /// <summary>Button text color</summary>
        public string ButtonTextColor { get; set; } = "#FFFFFF";

        /// <summary>Sidebar/status panel background</summary>
        public string SidebarColor { get; set; } = "#1E242E";

        /// <summary>Chat message card background</summary>
        public string ChatCardColor { get; set; } = "#262C38";

        /// <summary>DataGridView grid line color</summary>
        public string GridLineColor { get; set; } = "#323844";

        /// <summary>Selection/highlight color</summary>
        public string SelectionColor { get; set; } = "#375A8C";

        // ═══════════════════════════════════════════════════════════════════════
        // FONT SETTINGS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Font family name (e.g., "Segoe UI", "Consolas", "Arial")</summary>
        public string FontFamily { get; set; } = "Segoe UI";

        /// <summary>Base font size in points (e.g., 9, 10, 11, 12)</summary>
        public float FontSize { get; set; } = 9f;

        /// <summary>Bold text for labels/titles</summary>
        public bool FontBold { get; set; } = false;

        /// <summary>Italic text for descriptions</summary>
        public bool FontItalic { get; set; } = false;

        // ═══════════════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Whether this custom theme is currently active (overrides dark/light)</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Name of this theme preset</summary>
        public string PresetName { get; set; } = "Custom";

        // ═══════════════════════════════════════════════════════════════════════
        // COLOR HELPER METHODS — Parse hex strings to System.Drawing.Color
        // Each method includes fallback color in case hex parsing fails
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Get the background color from hex string</summary>
        public Color GetBackground() => ParseHex(BackgroundColor, Color.FromArgb(24, 28, 36));
        /// <summary>Get the card/panel color from hex string</summary>
        public Color GetCard() => ParseHex(CardColor, Color.FromArgb(30, 36, 46));
        /// <summary>Get the input field color from hex string</summary>
        public Color GetInput() => ParseHex(InputColor, Color.FromArgb(38, 44, 56));
        /// <summary>Get the primary text color from hex string</summary>
        public Color GetText() => ParseHex(TextColor, Color.FromArgb(220, 224, 230));
        /// <summary>Get the secondary text color from hex string</summary>
        public Color GetSecondaryText() => ParseHex(SecondaryTextColor, Color.FromArgb(120, 130, 145));
        /// <summary>Get the accent/highlight color from hex string</summary>
        public Color GetAccent() => ParseHex(AccentColor, Color.FromArgb(255, 127, 80));
        /// <summary>Get the "start/working" button color from hex string</summary>
        public Color GetStart() => ParseHex(StartColor, Color.FromArgb(34, 197, 94));
        /// <summary>Get the "stop" button color from hex string</summary>
        public Color GetStop() => ParseHex(StopColor, Color.FromArgb(239, 68, 68));
        /// <summary>Get the button text color from hex string</summary>
        public Color GetButtonText() => ParseHex(ButtonTextColor, Color.White);
        /// <summary>Get the sidebar color from hex string</summary>
        public Color GetSidebar() => ParseHex(SidebarColor, Color.FromArgb(30, 36, 46));
        /// <summary>Get the chat message card color from hex string</summary>
        public Color GetChatCard() => ParseHex(ChatCardColor, Color.FromArgb(38, 44, 56));
        /// <summary>Get the DataGrid line color from hex string</summary>
        public Color GetGridLine() => ParseHex(GridLineColor, Color.FromArgb(50, 56, 68));
        /// <summary>Get the selection/highlight color from hex string</summary>
        public Color GetSelection() => ParseHex(SelectionColor, Color.FromArgb(55, 90, 140));

        /// <summary>
        /// Creates a Font object with configured family and size, with optional style overrides.
        /// Falls back to "Segoe UI" if the configured font family is unavailable.
        /// </summary>
        /// <param name="sizeOverride">Font size override (0 = use FontSize property)</param>
        /// <param name="boldOverride">Bold override (null = use FontBold property)</param>
        /// <param name="italicOverride">Italic override (null = use FontItalic property)</param>
        public Font GetFont(float sizeOverride = 0, bool? boldOverride = null, bool? italicOverride = null)
        {
//             DebugLogger.Log("[CustomTheme] GetFont called — size=" + sizeOverride + ", bold=" + boldOverride + ", italic=" + italicOverride);

            float size = sizeOverride > 0 ? sizeOverride : FontSize;
            bool bold = boldOverride ?? FontBold;
            bool italic = italicOverride ?? FontItalic;
            FontStyle style = FontStyle.Regular;
            if (bold) style |= FontStyle.Bold;
            if (italic) style |= FontStyle.Italic;

            try
            {
//                 DebugLogger.Log("[CustomTheme] Creating font — family=" + FontFamily + ", size=" + size + ", style=" + style);
                return new Font(FontFamily, size, style);
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[CustomTheme] Font creation failed, using fallback: " + ex.Message);
                return new Font("Segoe UI", size, style);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BUILT-IN THEME PRESETS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Dark theme preset (default dark mode colors)</summary>
        public static CustomTheme DarkDefault()
        {
            return new CustomTheme
            {
                PresetName = "Dark (Default)",
                BackgroundColor = "#181C24", CardColor = "#1E242E", InputColor = "#262C38",
                TextColor = "#DCE0E6", SecondaryTextColor = "#788291", AccentColor = "#FF7F50",
                StartColor = "#22C55E", StopColor = "#EF4444", ButtonTextColor = "#FFFFFF",
                SidebarColor = "#1E242E", ChatCardColor = "#262C38", GridLineColor = "#323844",
                SelectionColor = "#375A8C", FontFamily = "Segoe UI", FontSize = 9f,
                FontBold = false, FontItalic = false
            };
        }

        /// <summary>Light theme preset (default light mode colors)</summary>
        public static CustomTheme LightDefault()
        {
//             DebugLogger.Log("[CustomTheme] Loading LightDefault preset");
            return new CustomTheme
            {
                PresetName = "Light (Default)",
                BackgroundColor = "#F5F7FA", CardColor = "#FFFFFF", InputColor = "#F8F9FC",
                TextColor = "#1E293B", SecondaryTextColor = "#64748B", AccentColor = "#FF7F50",
                StartColor = "#22C55E", StopColor = "#EF4444", ButtonTextColor = "#FFFFFF",
                SidebarColor = "#FFFFFF", ChatCardColor = "#F0F3F8", GridLineColor = "#E1E4EB",
                SelectionColor = "#D2E4FF", FontFamily = "Segoe UI", FontSize = 9f,
                FontBold = false, FontItalic = false
            };
        }

        public static CustomTheme OceanBlue()
        {
            return new CustomTheme
            {
                PresetName = "Ocean Blue",
                BackgroundColor = "#0D1B2A", CardColor = "#1B2838", InputColor = "#243447",
                TextColor = "#C8D6E5", SecondaryTextColor = "#7F9BB5", AccentColor = "#48DBFB",
                StartColor = "#22C55E", StopColor = "#EF4444", ButtonTextColor = "#FFFFFF",
                SidebarColor = "#152232", ChatCardColor = "#1B2838", GridLineColor = "#2C3E50",
                SelectionColor = "#2980B9", FontFamily = "Segoe UI", FontSize = 9f
            };
        }

        public static CustomTheme ForestGreen()
        {
            return new CustomTheme
            {
                PresetName = "Forest Green",
                BackgroundColor = "#1A2E1A", CardColor = "#223A22", InputColor = "#2B4A2B",
                TextColor = "#C8E6C8", SecondaryTextColor = "#7DAF7D", AccentColor = "#66BB6A",
                StartColor = "#43A047", StopColor = "#E53935", ButtonTextColor = "#FFFFFF",
                SidebarColor = "#1E3320", ChatCardColor = "#223A22", GridLineColor = "#2E5030",
                SelectionColor = "#388E3C", FontFamily = "Segoe UI", FontSize = 9f
            };
        }

        public static CustomTheme SunsetWarm()
        {
            return new CustomTheme
            {
                PresetName = "Sunset Warm",
                BackgroundColor = "#2D1B1B", CardColor = "#3A2424", InputColor = "#4A2E2E",
                TextColor = "#F0D0B0", SecondaryTextColor = "#B08060", AccentColor = "#FF8C42",
                StartColor = "#4CAF50", StopColor = "#D32F2F", ButtonTextColor = "#FFFFFF",
                SidebarColor = "#332020", ChatCardColor = "#3A2424", GridLineColor = "#503030",
                SelectionColor = "#BF360C", FontFamily = "Segoe UI", FontSize = 9f
            };
        }

        public static CustomTheme CyberPurple()
        {
            return new CustomTheme
            {
                PresetName = "Cyber Purple",
                BackgroundColor = "#1A0A2E", CardColor = "#231240", InputColor = "#2E1A50",
                TextColor = "#D4C0F0", SecondaryTextColor = "#9B7FCC", AccentColor = "#BB86FC",
                StartColor = "#00E676", StopColor = "#FF1744", ButtonTextColor = "#FFFFFF",
                SidebarColor = "#1E0E35", ChatCardColor = "#231240", GridLineColor = "#3A2060",
                SelectionColor = "#7C4DFF", FontFamily = "Segoe UI", FontSize = 9f
            };
        }

        public static CustomTheme HighContrast()
        {
            return new CustomTheme
            {
                PresetName = "High Contrast",
                BackgroundColor = "#000000", CardColor = "#1A1A1A", InputColor = "#2A2A2A",
                TextColor = "#FFFFFF", SecondaryTextColor = "#CCCCCC", AccentColor = "#FFFF00",
                StartColor = "#00FF00", StopColor = "#FF0000", ButtonTextColor = "#000000",
                SidebarColor = "#0D0D0D", ChatCardColor = "#1A1A1A", GridLineColor = "#404040",
                SelectionColor = "#0066FF", FontFamily = "Segoe UI", FontSize = 10f,
                FontBold = true
            };
        }

        /// <summary>Returns all built-in theme presets (Dark, Light, OceanBlue, etc.)</summary>
        public static List<CustomTheme> GetBuiltInPresets()
        {
//             DebugLogger.Log("[CustomTheme] GetBuiltInPresets called");
            return new List<CustomTheme>
            {
                DarkDefault(), LightDefault(), OceanBlue(),
                ForestGreen(), SunsetWarm(), CyberPurple(), HighContrast()
            };
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PERSISTENCE: SAVE / LOAD TO LOCAL JSON FILES
        // Storage location: %AppData%\WorkTimeCounter\themes\
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Base theme directory in AppData</summary>
        private static string ThemeDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorkTimeCounter", "themes");

        /// <summary>Path to active theme JSON file</summary>
        private static string ActiveThemePath => Path.Combine(ThemeDir, "custom_theme.json");
        /// <summary>Directory containing saved preset JSON files</summary>
        private static string PresetsDir => Path.Combine(ThemeDir, "presets");

        /// <summary>
        /// Saves this theme as the active custom theme to disk.
        /// Creates theme directory if it doesn't exist.
        /// </summary>
        public void SaveActive()
        {
//             DebugLogger.Log("[CustomTheme] SaveActive called — preset=" + PresetName);
            try
            {
                Directory.CreateDirectory(ThemeDir);
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ActiveThemePath, json);
//                 DebugLogger.Log("[CustomTheme] Active theme saved to " + ActiveThemePath);
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[CustomTheme] SaveActive failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Loads the active custom theme from disk.
        /// Returns null if no active theme file exists or if JSON deserialization fails.
        /// </summary>
        public static CustomTheme LoadActive()
        {
//             DebugLogger.Log("[CustomTheme] LoadActive called");
            try
            {
                if (File.Exists(ActiveThemePath))
                {
                    string json = File.ReadAllText(ActiveThemePath);
                    var theme = JsonConvert.DeserializeObject<CustomTheme>(json);
//                     DebugLogger.Log("[CustomTheme] Loaded active theme: " + (theme?.PresetName ?? "null"));
                    return theme;
                }
                else
                {
//                     DebugLogger.Log("[CustomTheme] No active theme file found");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[CustomTheme] LoadActive failed: " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Saves this theme as a named preset file.
        /// Preset files are stored in the presets subdirectory.
        /// </summary>
        public void SavePreset(string name)
        {
//             DebugLogger.Log("[CustomTheme] SavePreset called — name=" + name);
            try
            {
                Directory.CreateDirectory(PresetsDir);
                string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
                this.PresetName = name;
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                string path = Path.Combine(PresetsDir, safeName + ".json");
                File.WriteAllText(path, json);
//                 DebugLogger.Log("[CustomTheme] Preset saved to " + path);
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[CustomTheme] SavePreset failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Loads all saved user presets from the presets directory.
        /// Returns empty list if directory doesn't exist or JSON loading fails.
        /// </summary>
        public static List<CustomTheme> LoadPresets()
        {
//             DebugLogger.Log("[CustomTheme] LoadPresets called");
            var presets = new List<CustomTheme>();
            try
            {
                if (Directory.Exists(PresetsDir))
                {
                    foreach (string file in Directory.GetFiles(PresetsDir, "*.json"))
                    {
                        try
                        {
                            string json = File.ReadAllText(file);
                            var preset = JsonConvert.DeserializeObject<CustomTheme>(json);
                            if (preset != null)
                            {
                                presets.Add(preset);
//                                 DebugLogger.Log("[CustomTheme] Loaded preset: " + preset.PresetName);
                            }
                        }
                        catch (Exception fileEx)
                        {
                            DebugLogger.Log("[CustomTheme] Failed to load preset from " + file + ": " + fileEx.Message);
                        }
                    }
                }
                else
                {
//                     DebugLogger.Log("[CustomTheme] Presets directory does not exist");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[CustomTheme] LoadPresets failed: " + ex.Message);
            }
//             DebugLogger.Log("[CustomTheme] Loaded " + presets.Count + " presets");
            return presets;
        }

        /// <summary>
        /// Deletes a saved preset by name.
        /// Preset file name is derived from the preset name by removing invalid filename characters.
        /// </summary>
        public static void DeletePreset(string name)
        {
//             DebugLogger.Log("[CustomTheme] DeletePreset called — name=" + name);
            try
            {
                if (Directory.Exists(PresetsDir))
                {
                    string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
                    string path = Path.Combine(PresetsDir, safeName + ".json");
                    if (File.Exists(path))
                    {
                        File.Delete(path);
//                         DebugLogger.Log("[CustomTheme] Preset deleted: " + path);
                    }
                    else
                    {
//                         DebugLogger.Log("[CustomTheme] Preset file not found: " + path);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[CustomTheme] DeletePreset failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Creates a deep copy of this theme using JSON serialization/deserialization.
        /// </summary>
        public CustomTheme Clone()
        {
//             DebugLogger.Log("[CustomTheme] Clone called — preset=" + PresetName);
            try
            {
                string json = JsonConvert.SerializeObject(this);
                return JsonConvert.DeserializeObject<CustomTheme>(json);
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[CustomTheme] Clone failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Converts a Color object to hex string format (#RRGGBB)</summary>
        public static string ColorToHex(Color c)
        {
            string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
//             DebugLogger.Log("[CustomTheme] ColorToHex — color=" + c.Name + " => " + hex);
            return hex;
        }

        /// <summary>
        /// Parses a hex color string (#RRGGBB) to System.Drawing.Color.
        /// Returns fallback color if hex is null, empty, or malformed.
        /// </summary>
        private static Color ParseHex(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && hex.StartsWith("#") && hex.Length == 7)
            {
                try
                {
                    return ColorTranslator.FromHtml(hex);
                }
                catch
                {
                    DebugLogger.Log("[CustomTheme] ParseHex failed for " + hex + " — using fallback");
                }
            }
            return fallback;
        }
    }
}
