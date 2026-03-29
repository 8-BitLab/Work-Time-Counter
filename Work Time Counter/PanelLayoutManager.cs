// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                                 ║
// ║                     WORKFLOW - TEAM TIME TRACKER                              ║
// ║                                                                              ║
// ║  FILE:        PanelLayoutManager.cs                                          ║
// ║  PURPOSE:     SAVE/LOAD/RESET PANEL POSITIONS & SIZES TO LOCAL FILE          ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                           ║
// ║  LICENSE:     OPEN SOURCE                                                    ║
// ║                                                                              ║
// ║  DESCRIPTION:                                                                ║
// ║  Manages persistent panel layouts so users can freely drag, resize,          ║
// ║  and reposition panels (sticker board, chat, file share, user panel).        ║
// ║  The work counter panel stays fixed. Positions are saved to a JSON file      ║
// ║  in %AppData%\WorkFlow\ and restored on next app launch.                     ║
// ║                                                                              ║
// ║  GitHub: https://github.com/8BitLabEngineering                               ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    // ═══ DATA CLASS: Stores position & size for a single panel ═══
    public class PanelLayoutData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Visible { get; set; } = true;
    }

    // ═══ DATA CLASS: All panel layouts in one file ═══
    public class LayoutConfig
    {
        public Dictionary<string, PanelLayoutData> Panels { get; set; }
            = new Dictionary<string, PanelLayoutData>();

        // Splitter positions
        public int HorizSplitterY { get; set; } = -1;
        public int VertSplitterX { get; set; } = -1;

        // Form size
        public int FormWidth { get; set; } = 1600;
        public int FormHeight { get; set; } = 950;
    }

    // ═══ MANAGER: Save, Load, Reset panel layouts ═══
    public static class PanelLayoutManager
    {
        /// <summary>Returns the full path to the panel layout JSON file.</summary>
        private static string GetLayoutFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "WorkFlow");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, "panel_layout.json");
        }

        /// <summary>Save current panel positions to disk.</summary>
        public static void SaveLayout(LayoutConfig config)
        {
//             DebugLogger.Log("[PanelLayout] SaveLayout: Saving layout with " + config.Panels.Count + " panels");

            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(GetLayoutFilePath(), json);

//                 DebugLogger.Log("[PanelLayout] SaveLayout: Layout saved successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[PanelLayout] SaveLayout: Error saving layout - " + ex.Message);
            }
        }

        /// <summary>Load saved layout from disk. Returns null if no saved layout exists.</summary>
        public static LayoutConfig LoadLayout()
        {
//             DebugLogger.Log("[PanelLayout] LoadLayout: Loading layout from disk");

            try
            {
                string path = GetLayoutFilePath();
                if (!File.Exists(path))
                {
//                     DebugLogger.Log("[PanelLayout] LoadLayout: Layout file does not exist");
                    return null;
                }

                string json = File.ReadAllText(path);
                var config = JsonConvert.DeserializeObject<LayoutConfig>(json);

                if (config != null)
                {
//                     DebugLogger.Log("[PanelLayout] LoadLayout: Loaded layout with " + config.Panels.Count + " panels");
                }
                else
                {
//                     DebugLogger.Log("[PanelLayout] LoadLayout: Deserialization returned null");
                }

                return config;
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[PanelLayout] LoadLayout: Error loading layout - " + ex.Message);
                return null;
            }
        }

        /// <summary>Delete saved layout file — resets to default positions.</summary>
        public static void ResetLayout()
        {
//             DebugLogger.Log("[PanelLayout] ResetLayout: Resetting layout to defaults");

            try
            {
                string path = GetLayoutFilePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
//                     DebugLogger.Log("[PanelLayout] ResetLayout: Layout file deleted");
                }
                else
                {
//                     DebugLogger.Log("[PanelLayout] ResetLayout: Layout file does not exist");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[PanelLayout] ResetLayout: Error resetting layout - " + ex.Message);
            }
        }

        /// <summary>Capture current state of a Control into PanelLayoutData.</summary>
        public static PanelLayoutData CapturePanel(Control panel)
        {
            if (panel == null)
            {
//                 DebugLogger.Log("[PanelLayout] CapturePanel: Panel is null, returning null");
                return null;
            }

            var data = new PanelLayoutData
            {
                X = panel.Left,
                Y = panel.Top,
                Width = panel.Width,
                Height = panel.Height,
                Visible = panel.Visible
            };

//             DebugLogger.Log($"[PanelLayout] CapturePanel: Captured {panel.Name ?? "unnamed"} at ({data.X},{data.Y}) size {data.Width}x{data.Height}");
            return data;
        }

        /// <summary>Apply saved layout data to a Control.</summary>
        public static void ApplyToPanel(Control panel, PanelLayoutData data)
        {
            if (panel == null || data == null)
            {
//                 DebugLogger.Log("[PanelLayout] ApplyToPanel: Panel or data is null, skipping");
                return;
            }

            panel.Location = new Point(data.X, data.Y);
            panel.Size = new Size(data.Width, data.Height);
            panel.Visible = data.Visible;

//             DebugLogger.Log($"[PanelLayout] ApplyToPanel: Applied layout to {panel.Name ?? "unnamed"} at ({data.X},{data.Y}) size {data.Width}x{data.Height}");
        }

        /// <summary>
        /// Make a panel draggable by its top edge (title area).
        /// Drag the top 24px to move the panel freely.
        /// Drag the bottom-right corner (16x16) to resize.
        /// Calls onLayoutChanged callback when drag/resize completes.
        /// </summary>
        public static void MakeDraggableAndResizable(Control panel, Action onLayoutChanged)
        {
            if (panel == null)
            {
//                 DebugLogger.Log("[PanelLayout] MakeDraggableAndResizable: Panel is null, skipping");
                return;
            }

//             DebugLogger.Log($"[PanelLayout] MakeDraggableAndResizable: Setting up drag/resize for {panel.Name ?? "unnamed"}");

            bool dragging = false;
            Point dragStart = Point.Empty;
            Point origLocation = Point.Empty;

            bool resizing = false;
            Point resizeStart = Point.Empty;
            Size origSize = Size.Empty;

            int titleHeight = 24;  // drag zone at top
            int resizeGrip = 12;   // resize zone at bottom-right corner

            panel.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;

                // Check if in resize grip area (bottom-right corner)
                if (e.X >= panel.Width - resizeGrip && e.Y >= panel.Height - resizeGrip)
                {
//                     DebugLogger.Log($"[PanelLayout] MakeDraggableAndResizable: Starting resize");
                    resizing = true;
                    resizeStart = e.Location;
                    origSize = panel.Size;
                }
                // Check if in title drag area (top 24px)
                else if (e.Y <= titleHeight)
                {
//                     DebugLogger.Log($"[PanelLayout] MakeDraggableAndResizable: Starting drag");
                    dragging = true;
                    dragStart = e.Location;
                    origLocation = panel.Location;
                }
            };

            panel.MouseMove += (s, e) =>
            {
                // Change cursor based on position
                if (e.X >= panel.Width - resizeGrip && e.Y >= panel.Height - resizeGrip)
                    panel.Cursor = Cursors.SizeNWSE;
                else if (e.Y <= titleHeight)
                    panel.Cursor = Cursors.SizeAll;
                else
                    panel.Cursor = Cursors.Default;

                if (dragging)
                {
                    int dx = e.X - dragStart.X;
                    int dy = e.Y - dragStart.Y;
                    panel.Location = new Point(origLocation.X + dx, origLocation.Y + dy);
                }
                else if (resizing)
                {
                    int dx = e.X - resizeStart.X;
                    int dy = e.Y - resizeStart.Y;
                    int newW = Math.Max(200, origSize.Width + dx);
                    int newH = Math.Max(100, origSize.Height + dy);
                    panel.Size = new Size(newW, newH);
                }
            };

            panel.MouseUp += (s, e) =>
            {
                if (dragging || resizing)
                {
                    if (dragging)
                    {
//                         DebugLogger.Log($"[PanelLayout] MakeDraggableAndResizable: Drag complete, new position ({panel.Left},{panel.Top})");
                    }
                    else if (resizing)
                    {
//                         DebugLogger.Log($"[PanelLayout] MakeDraggableAndResizable: Resize complete, new size {panel.Width}x{panel.Height}");
                    }

                    dragging = false;
                    resizing = false;
                    onLayoutChanged?.Invoke();
                }
            };

//             DebugLogger.Log($"[PanelLayout] MakeDraggableAndResizable: Setup complete for {panel.Name ?? "unnamed"}");
        }
    }
}
