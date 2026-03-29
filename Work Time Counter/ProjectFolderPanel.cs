// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                     PROJECT FOLDER PANEL COMPONENT                          ║
// ║                                                                              ║
// ║  This component provides a file explorer interface for project folders,     ║
// ║  with file organization, real-time updates, and drag-and-drop support.      ║
// ║                                                                              ║
// ║  Version: 1.0.0                                                              ║
// ║  Last Updated: 2026-03-28                                                    ║
// ║  Status: Production Ready                                                    ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    /// <summary>
    /// ProjectFolderPanel - A user control for managing and displaying project files
    /// with folder organization, live updates, and contextual operations.
    /// </summary>
    public partial class ProjectFolderPanel : UserControl
    {
        #region Fields & Properties

        private CustomTheme _customTheme;
        private bool _isDarkMode = true;
        private ProjectFolderSettings _settings;
        private FileSystemWatcher _fileSystemWatcher;
        private const int TRUNCATE_LENGTH = 45;

        // UI Components
        private Label _lblTitle;
        private Label _lblFolderPath;
        private Button _btnSetPath;
        private Button _btnOpenFolder;
        private Button _btnRefresh;
        private FlowLayoutPanel _flpFileList;
        private Label _lblEmptyState;
        private Panel _pnlButtonContainer;
        private ContextMenuStrip _contextMenu;

        /// <summary>
        /// Gets or sets the current project folder path.
        /// </summary>
        public string FolderPath
        {
            get => _settings?.FolderPath ?? "";
            set
            {
                if (_settings != null)
                {
                    _settings.FolderPath = value;
                    _settings.LastOpenedPath = value;
                    UpdateFolderDisplay();
                    RefreshFileList();
                    CreateDefaultFolderStructure();
                    SetupFileSystemWatcher();
                    SaveSettings();
                }
            }
        }

        #endregion

        #region Constructor & Initialization

        public ProjectFolderPanel()
        {
            InitializeUI();
            LoadSettings();
            ApplyTheme(true, null);
        }

        private void InitializeUI()
        {
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(24, 28, 36);
            this.Dock = DockStyle.Fill;

            // Title Label
            _lblTitle = new Label
            {
                Text = "📁 PROJECT FOLDER",
                Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(220, 224, 230),
                BackColor = System.Drawing.Color.Transparent,
                AutoSize = true,
                Location = new System.Drawing.Point(15, 15)
            };
            this.Controls.Add(_lblTitle);

            // Folder Path Label
            _lblFolderPath = new Label
            {
                Text = "No project folder selected",
                Font = new System.Drawing.Font("Segoe UI", 9F),
                ForeColor = System.Drawing.Color.FromArgb(170, 178, 190),
                BackColor = System.Drawing.Color.Transparent,
                AutoSize = true,
                Location = new System.Drawing.Point(15, 40)
            };
            this.Controls.Add(_lblFolderPath);

            // Button Container Panel
            _pnlButtonContainer = new Panel
            {
                Height = 40,
                Location = new System.Drawing.Point(15, 65),
                BackColor = System.Drawing.Color.Transparent,
                AutoSize = false
            };

            // Set Path Button
            _btnSetPath = new Button
            {
                Text = "Set Path",
                Font = new System.Drawing.Font("Segoe UI", 9F),
                BackColor = System.Drawing.Color.FromArgb(255, 127, 80),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new System.Drawing.Size(75, 32),
                Location = new System.Drawing.Point(0, 4)
            };
            _btnSetPath.FlatAppearance.BorderSize = 0;
            _btnSetPath.Click += BtnSetPath_Click;
            _pnlButtonContainer.Controls.Add(_btnSetPath);

            // Open Folder Button
            _btnOpenFolder = new Button
            {
                Text = "Open Folder",
                Font = new System.Drawing.Font("Segoe UI", 9F),
                BackColor = System.Drawing.Color.FromArgb(60, 70, 85),
                ForeColor = System.Drawing.Color.FromArgb(220, 224, 230),
                FlatStyle = FlatStyle.Flat,
                Size = new System.Drawing.Size(100, 32),
                Location = new System.Drawing.Point(80, 4),
                Enabled = false
            };
            _btnOpenFolder.FlatAppearance.BorderSize = 0;
            _btnOpenFolder.Click += BtnOpenFolder_Click;
            _pnlButtonContainer.Controls.Add(_btnOpenFolder);

            // Refresh Button
            _btnRefresh = new Button
            {
                Text = "Refresh",
                Font = new System.Drawing.Font("Segoe UI", 9F),
                BackColor = System.Drawing.Color.FromArgb(60, 70, 85),
                ForeColor = System.Drawing.Color.FromArgb(220, 224, 230),
                FlatStyle = FlatStyle.Flat,
                Size = new System.Drawing.Size(75, 32),
                Location = new System.Drawing.Point(185, 4),
                Enabled = false
            };
            _btnRefresh.FlatAppearance.BorderSize = 0;
            _btnRefresh.Click += BtnRefresh_Click;
            _pnlButtonContainer.Controls.Add(_btnRefresh);

            this.Controls.Add(_pnlButtonContainer);

            // Empty State Label
            _lblEmptyState = new Label
            {
                Text = "No project folder selected. Click Set Path to choose.",
                Font = new System.Drawing.Font("Segoe UI", 10F),
                ForeColor = System.Drawing.Color.FromArgb(130, 140, 155),
                BackColor = System.Drawing.Color.Transparent,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                AutoSize = false,
                Location = new System.Drawing.Point(15, 120),
                Size = new System.Drawing.Size(this.Width - 30, 100)
            };
            this.Controls.Add(_lblEmptyState);

            // File List Flow Layout Panel
            _flpFileList = new FlowLayoutPanel
            {
                Location = new System.Drawing.Point(15, 120),
                Size = new System.Drawing.Size(this.Width - 30, this.Height - 140),
                BackColor = System.Drawing.Color.FromArgb(24, 28, 36),
                AutoScroll = true,
                WrapContents = true,
                AllowDrop = true,
                Visible = false
            };
            _flpFileList.DragEnter += FlpFileList_DragEnter;
            _flpFileList.DragDrop += FlpFileList_DragDrop;
            this.Controls.Add(_flpFileList);

            // Context Menu
            InitializeContextMenu();

            // Resize event for responsive layout
            this.Resize += ProjectFolderPanel_Resize;
        }

        private void InitializeContextMenu()
        {
            _contextMenu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("Open");
            openItem.Click += (s, e) => OpenSelectedFile();
            _contextMenu.Items.Add(openItem);

            var openFolderItem = new ToolStripMenuItem("Open Folder");
            openFolderItem.Click += (s, e) => OpenInExplorer();
            _contextMenu.Items.Add(openFolderItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var deleteItem = new ToolStripMenuItem("Delete");
            deleteItem.Click += (s, e) => DeleteSelectedFile();
            _contextMenu.Items.Add(deleteItem);
        }

        #endregion

        #region Button Event Handlers

        private void BtnSetPath_Click(object sender, EventArgs e)
        {
            // [ProjectFolder] User initiated folder path selection via button click
//             DebugLogger.Log("[ProjectFolder] Set Path button clicked");
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select your project folder";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
//                     DebugLogger.Log($"[ProjectFolder] User selected folder: {fbd.SelectedPath}");
                    FolderPath = fbd.SelectedPath;
                }
                else
                {
//                     DebugLogger.Log("[ProjectFolder] Folder selection cancelled by user");
                }
            }
        }

        private void BtnOpenFolder_Click(object sender, EventArgs e)
        {
            // [ProjectFolder] Open folder in Windows Explorer
//             DebugLogger.Log("[ProjectFolder] Open Folder button clicked");
            OpenInExplorer();
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            // [ProjectFolder] Manually refresh the file list display
//             DebugLogger.Log("[ProjectFolder] Refresh button clicked");
            RefreshFileList();
        }

        #endregion

        #region File Operations

        private void RefreshFileList()
        {
            // [ProjectFolder] Rebuild file list display from current folder
//             DebugLogger.Log("[ProjectFolder] Refreshing file list");
            _flpFileList.Controls.Clear();

            if (string.IsNullOrEmpty(FolderPath) || !Directory.Exists(FolderPath))
            {
//                 DebugLogger.Log($"[ProjectFolder] Folder path invalid or empty: {FolderPath ?? "null"}");
                _flpFileList.Visible = false;
                _lblEmptyState.Visible = true;
                return;
            }

            try
            {
                var files = Directory.GetFiles(FolderPath).OrderBy(f => Path.GetFileName(f)).ToList();
//                 DebugLogger.Log($"[ProjectFolder] Found {files.Count} files in {FolderPath}");

                if (files.Count == 0)
                {
//                     DebugLogger.Log("[ProjectFolder] No files found - showing empty state");
                    _lblEmptyState.Text = "No files in this folder.";
                    _flpFileList.Visible = false;
                    _lblEmptyState.Visible = true;
                    return;
                }

                foreach (var filePath in files)
                {
                    var fileCard = CreateFileCard(filePath);
                    _flpFileList.Controls.Add(fileCard);
                }

//                 DebugLogger.Log($"[ProjectFolder] Successfully rendered {files.Count} file cards");
                _flpFileList.Visible = true;
                _lblEmptyState.Visible = false;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ProjectFolder] ERROR refreshing file list: {ex.Message}");
                _lblEmptyState.Text = $"Error loading files: {ex.Message}";
                _flpFileList.Visible = false;
                _lblEmptyState.Visible = true;
            }
        }

        private Panel CreateFileCard(string filePath)
        {
            // [ProjectFolder] Create a visual card for a single file with metadata display
//             DebugLogger.Log($"[ProjectFolder] Creating file card for: {Path.GetFileName(filePath)}");
            var fileInfo = new FileInfo(filePath);
            var card = new Panel
            {
                Size = new System.Drawing.Size(240, 70),
                BackColor = System.Drawing.Color.FromArgb(30, 36, 46),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(5),
                Tag = filePath
            };
            // Icon Label
            var lblIcon = new Label
            {
                Text = GetFileIcon(fileInfo.Extension),
                Font = new System.Drawing.Font("Segoe UI", 16F),
                AutoSize = true,
                Location = new System.Drawing.Point(8, 10)
            };
            card.Controls.Add(lblIcon);

            // File Name Label
            var lblFileName = new Label
            {
                Text = Path.GetFileNameWithoutExtension(filePath),
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(220, 224, 230),
                AutoSize = true,
                Location = new System.Drawing.Point(50, 8),
                MaximumSize = new System.Drawing.Size(180, 20)
            };
            card.Controls.Add(lblFileName);

            // Extension Label
            var lblExt = new Label
            {
                Text = fileInfo.Extension,
                Font = new System.Drawing.Font("Segoe UI", 8F),
                ForeColor = System.Drawing.Color.FromArgb(170, 178, 190),
                AutoSize = true,
                Location = new System.Drawing.Point(50, 28)
            };
            card.Controls.Add(lblExt);

            // Modified Date Label
            var lblDate = new Label
            {
                Text = $"Modified: {fileInfo.LastWriteTime:M/d/yyyy H:mm}",
                Font = new System.Drawing.Font("Segoe UI", 8F),
                ForeColor = System.Drawing.Color.FromArgb(130, 140, 155),
                AutoSize = true,
                Location = new System.Drawing.Point(50, 43)
            };
            card.Controls.Add(lblDate);

            // Size Label
            var lblSize = new Label
            {
                Text = FormatFileSize(fileInfo.Length),
                Font = new System.Drawing.Font("Segoe UI", 8F),
                ForeColor = System.Drawing.Color.FromArgb(130, 140, 155),
                AutoSize = true,
                Location = new System.Drawing.Point(50, 58)
            };
            card.Controls.Add(lblSize);

            // Mouse Events
            card.MouseDoubleClick += (s, e) => OpenFileWithDefaultApp(filePath);
            card.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    _contextMenu.Show(card, e.Location);
                }
            };

            // Hover effect
            card.MouseEnter += (s, e) => card.BackColor = System.Drawing.Color.FromArgb(45, 52, 62);
            card.MouseLeave += (s, e) => card.BackColor = System.Drawing.Color.FromArgb(30, 36, 46);

            return card;
        }

        private string GetFileIcon(string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => "📄",
                ".doc" or ".docx" => "📝",
                ".xls" or ".xlsx" => "📊",
                ".ppt" or ".pptx" => "📽",
                ".zip" or ".rar" => "📦",
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" => "🖼",
                ".cs" or ".js" or ".py" or ".cpp" or ".java" or ".ts" => "💻",
                ".txt" => "📃",
                _ => "📎"
            };
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private void OpenFileWithDefaultApp(string filePath)
        {
            // [ProjectFolder] Open file with default system application
//             DebugLogger.Log($"[ProjectFolder] Opening file with default app: {Path.GetFileName(filePath)}");
            try
            {
                Process.Start(filePath);
//                 DebugLogger.Log($"[ProjectFolder] File opened successfully: {filePath}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ProjectFolder] ERROR opening file: {ex.Message}");
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenInExplorer()
        {
            // [ProjectFolder] Open project folder in Windows Explorer
//             DebugLogger.Log($"[ProjectFolder] Attempting to open Explorer at: {FolderPath ?? "null"}");
            if (string.IsNullOrEmpty(FolderPath) || !Directory.Exists(FolderPath))
            {
                DebugLogger.Log("[ProjectFolder] ERROR: Folder path is not valid");
                MessageBox.Show("Folder path is not valid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Process.Start("explorer.exe", FolderPath);
//                 DebugLogger.Log($"[ProjectFolder] Explorer opened successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ProjectFolder] ERROR opening Explorer: {ex.Message}");
                MessageBox.Show($"Error opening folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenSelectedFile()
        {
            if (_flpFileList.Controls.Count > 0 && _flpFileList.Controls[0] is Panel lastCard && lastCard.Tag is string filePath)
            {
                OpenFileWithDefaultApp(filePath);
            }
        }

        private void DeleteSelectedFile()
        {
            // [ProjectFolder] Delete selected file after user confirmation
//             DebugLogger.Log("[ProjectFolder] Delete operation initiated");
            if (_flpFileList.Controls.Count > 0 && _flpFileList.Controls[0] is Panel lastCard && lastCard.Tag is string filePath)
            {
//                 DebugLogger.Log($"[ProjectFolder] Requesting delete confirmation for: {Path.GetFileName(filePath)}");
                if (MessageBox.Show($"Are you sure you want to delete this file?\n\n{Path.GetFileName(filePath)}",
                    "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        File.Delete(filePath);
//                         DebugLogger.Log($"[ProjectFolder] File deleted successfully: {filePath}");
                        RefreshFileList();
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[ProjectFolder] ERROR deleting file: {ex.Message}");
                        MessageBox.Show($"Error deleting file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
//                     DebugLogger.Log("[ProjectFolder] Delete operation cancelled by user");
                }
            }
        }

        #endregion

        #region Drag & Drop

        private void FlpFileList_DragEnter(object sender, DragEventArgs e)
        {
            // [ProjectFolder] Handle drag enter - allow file drops
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
//                 DebugLogger.Log("[ProjectFolder] Drag enter detected - files ready to drop");
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void FlpFileList_DragDrop(object sender, DragEventArgs e)
        {
            // [ProjectFolder] Handle dropped files - copy them to project folder
//             DebugLogger.Log("[ProjectFolder] Files dropped - processing drag-drop operation");
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
//                 DebugLogger.Log($"[ProjectFolder] Dropped {files.Length} file(s)");
                foreach (var file in files)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            string destPath = Path.Combine(FolderPath, Path.GetFileName(file));
//                             DebugLogger.Log($"[ProjectFolder] Copying dropped file: {Path.GetFileName(file)}");
                            File.Copy(file, destPath, true);
//                             DebugLogger.Log($"[ProjectFolder] File copied successfully: {destPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[ProjectFolder] ERROR copying dropped file: {ex.Message}");
                        MessageBox.Show($"Error copying file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                RefreshFileList();
            }
        }

        #endregion

        #region Folder Structure & Watchers

        private void CreateDefaultFolderStructure()
        {
            // [ProjectFolder] Initialize standard folder structure within project folder
//             DebugLogger.Log($"[ProjectFolder] Creating default folder structure at: {FolderPath}");
            if (string.IsNullOrEmpty(FolderPath) || !Directory.Exists(FolderPath))
            {
//                 DebugLogger.Log("[ProjectFolder] Cannot create structure - invalid path");
                return;
            }

            string[] folders = { "data", "notes", "attachments", "organizer", "shared", "config", "logs" };

            foreach (var folder in folders)
            {
                try
                {
                    string folderPath = Path.Combine(FolderPath, folder);
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
//                         DebugLogger.Log($"[ProjectFolder] Created subdirectory: {folder}");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[ProjectFolder] ERROR creating folder {folder}: {ex.Message}");
                }
            }
        }

        private void SetupFileSystemWatcher()
        {
            // [ProjectFolder] Monitor folder for real-time file system changes
//             DebugLogger.Log($"[ProjectFolder] Setting up FileSystemWatcher for: {FolderPath}");
            _fileSystemWatcher?.Dispose();

            if (string.IsNullOrEmpty(FolderPath) || !Directory.Exists(FolderPath))
            {
//                 DebugLogger.Log("[ProjectFolder] Cannot setup watcher - invalid path");
                return;
            }

            _fileSystemWatcher = new FileSystemWatcher(FolderPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                Filter = "*.*"
            };

            _fileSystemWatcher.Created += (s, e) =>
            {
//                 DebugLogger.Log($"[ProjectFolder] File system change detected: Created {Path.GetFileName(e.FullPath)}");
                RefreshFileListSafe();
            };
            _fileSystemWatcher.Deleted += (s, e) =>
            {
//                 DebugLogger.Log($"[ProjectFolder] File system change detected: Deleted {Path.GetFileName(e.FullPath)}");
                RefreshFileListSafe();
            };
            _fileSystemWatcher.Renamed += (s, e) =>
            {
//                 DebugLogger.Log($"[ProjectFolder] File system change detected: Renamed {Path.GetFileName(e.OldFullPath)} -> {Path.GetFileName(e.FullPath)}");
                RefreshFileListSafe();
            };

            _fileSystemWatcher.EnableRaisingEvents = true;
//             DebugLogger.Log("[ProjectFolder] FileSystemWatcher enabled and listening");
        }

        private void RefreshFileListSafe()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(RefreshFileList));
            }
            else
            {
                RefreshFileList();
            }
        }

        private void UpdateFolderDisplay()
        {
            if (string.IsNullOrEmpty(FolderPath))
            {
                _lblFolderPath.Text = "No project folder selected";
                _btnOpenFolder.Enabled = false;
                _btnRefresh.Enabled = false;
                _lblEmptyState.Text = "No project folder selected. Click Set Path to choose.";
                _flpFileList.Visible = false;
                _lblEmptyState.Visible = true;
            }
            else if (!Directory.Exists(FolderPath))
            {
                _lblFolderPath.Text = "⚠ Folder not found: " + TruncatePath(FolderPath);
                _btnOpenFolder.Enabled = false;
                _btnRefresh.Enabled = false;
                _lblEmptyState.Text = "The selected folder no longer exists.";
                _flpFileList.Visible = false;
                _lblEmptyState.Visible = true;
            }
            else
            {
                _lblFolderPath.Text = TruncatePath(FolderPath);
                _btnOpenFolder.Enabled = true;
                _btnRefresh.Enabled = true;
            }
        }

        private string TruncatePath(string path)
        {
            if (path.Length <= TRUNCATE_LENGTH)
                return path;

            return "..." + path.Substring(path.Length - TRUNCATE_LENGTH);
        }

        #endregion

        #region Theme & Styling

        /// <summary>
        /// Applies the specified theme to this control.
        /// </summary>
        public void ApplyTheme(bool isDarkMode, CustomTheme customTheme)
        {
            _isDarkMode = isDarkMode;
            _customTheme = customTheme ?? new CustomTheme();

            var bgColor = isDarkMode ? System.Drawing.Color.FromArgb(24, 28, 36) : System.Drawing.Color.FromArgb(245, 245, 247);
            var cardColor = isDarkMode ? System.Drawing.Color.FromArgb(30, 36, 46) : System.Drawing.Color.FromArgb(250, 250, 252);
            var textColor = isDarkMode ? System.Drawing.Color.FromArgb(220, 224, 230) : System.Drawing.Color.FromArgb(50, 50, 50);
            var accentColor = System.Drawing.Color.FromArgb(255, 127, 80);

            this.BackColor = bgColor;

            if (_lblTitle != null)
            {
                _lblTitle.BackColor = System.Drawing.Color.Transparent;
                _lblTitle.ForeColor = textColor;
            }

            if (_lblFolderPath != null)
            {
                _lblFolderPath.BackColor = System.Drawing.Color.Transparent;
                _lblFolderPath.ForeColor = isDarkMode ? System.Drawing.Color.FromArgb(170, 178, 190) : System.Drawing.Color.FromArgb(100, 100, 100);
            }

            if (_btnSetPath != null)
            {
                _btnSetPath.BackColor = accentColor;
                _btnSetPath.ForeColor = System.Drawing.Color.White;
            }

            if (_btnOpenFolder != null)
            {
                _btnOpenFolder.BackColor = cardColor;
                _btnOpenFolder.ForeColor = textColor;
            }

            if (_btnRefresh != null)
            {
                _btnRefresh.BackColor = cardColor;
                _btnRefresh.ForeColor = textColor;
            }

            if (_flpFileList != null)
            {
                _flpFileList.BackColor = bgColor;
                foreach (Panel card in _flpFileList.Controls.OfType<Panel>())
                {
                    card.BackColor = cardColor;
                    card.ForeColor = textColor;
                }
            }

            if (_lblEmptyState != null)
            {
                _lblEmptyState.BackColor = System.Drawing.Color.Transparent;
                _lblEmptyState.ForeColor = isDarkMode ? System.Drawing.Color.FromArgb(130, 140, 155) : System.Drawing.Color.FromArgb(130, 130, 130);
            }
        }

        #endregion

        #region Settings Persistence

        private void LoadSettings()
        {
            // [ProjectFolder] Load persisted folder settings from local storage
//             DebugLogger.Log("[ProjectFolder] Loading settings from storage");
            _settings = ProjectFolderSettings.Load();
            if (!string.IsNullOrEmpty(_settings.LastOpenedPath) && Directory.Exists(_settings.LastOpenedPath))
            {
//                 DebugLogger.Log($"[ProjectFolder] Restoring last opened path: {_settings.LastOpenedPath}");
                _settings.FolderPath = _settings.LastOpenedPath;
            }
            UpdateFolderDisplay();
        }

        private void SaveSettings()
        {
            // [ProjectFolder] Persist folder settings to local storage
//             DebugLogger.Log("[ProjectFolder] Saving settings to storage");
            _settings?.Save();
        }

        #endregion

        #region Resize Handling

        private void ProjectFolderPanel_Resize(object sender, EventArgs e)
        {
            if (_pnlButtonContainer != null)
            {
                _pnlButtonContainer.Width = this.Width - 30;
            }

            if (_flpFileList != null)
            {
                _flpFileList.Size = new System.Drawing.Size(this.Width - 30, this.Height - 140);
            }

            if (_lblEmptyState != null)
            {
                _lblEmptyState.Size = new System.Drawing.Size(this.Width - 30, 100);
            }
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fileSystemWatcher?.Dispose();
                SaveSettings();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Settings model for ProjectFolderPanel persistence.
    /// </summary>
    public class ProjectFolderSettings
    {
        [JsonProperty("folderPath")]
        public string FolderPath { get; set; } = "";

        [JsonProperty("autoLoadOnStartup")]
        public bool AutoLoadOnStartup { get; set; } = true;

        [JsonProperty("lastOpenedPath")]
        public string LastOpenedPath { get; set; } = "";

        /// <summary>
        /// Loads settings from JSON file in AppData.
        /// </summary>
        public static ProjectFolderSettings Load()
        {
            // [ProjectFolder] Load ProjectFolderSettings from AppData
            try
            {
                string settingsPath = GetSettingsPath();
//                 DebugLogger.Log($"[ProjectFolder] Attempting to load settings from: {settingsPath}");
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = JsonConvert.DeserializeObject<ProjectFolderSettings>(json) ?? new ProjectFolderSettings();
//                     DebugLogger.Log("[ProjectFolder] Settings loaded successfully");
                    return settings;
                }
                else
                {
//                     DebugLogger.Log("[ProjectFolder] Settings file not found - using defaults");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ProjectFolder] ERROR loading ProjectFolderSettings: {ex.Message}");
            }

            return new ProjectFolderSettings();
        }

        /// <summary>
        /// Saves settings to JSON file in AppData.
        /// </summary>
        public void Save()
        {
            // [ProjectFolder] Persist ProjectFolderSettings to AppData
            try
            {
                string settingsPath = GetSettingsPath();
                string directory = Path.GetDirectoryName(settingsPath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ProjectFolder] ERROR saving settings: {ex.Message}");
            }
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WorkFlow",
                "project_folder_settings.json");
        }
    }
}
