// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        HelperPanel.cs                                               ║
// ║  PURPOSE:     TEAM WIKI / KNOWLEDGE BASE PANEL                             ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    // ================================================================
    // HelperPanel — Team Wiki / Knowledge Base panel
    // Admin (Blagoy) can add/edit/delete items.  Everyone else: read-only.
    // Data stored in Firebase under /helper.json
    // ================================================================
    public class HelperPanel : UserControl
    {
        // ── Note color palette ──
        private static readonly Color[] NoteColors = new Color[]
        {
            Color.FromArgb(255, 235, 59),   // Yellow
            Color.FromArgb(129, 212, 250),  // Light Blue
            Color.FromArgb(165, 214, 167),  // Light Green
            Color.FromArgb(255, 171, 145),  // Light Orange
            Color.FromArgb(206, 147, 216),  // Light Purple
            Color.FromArgb(239, 83, 80),    // Red
            Color.FromArgb(255, 255, 255),  // White
        };

        private static readonly string[] NoteColorNames = new string[]
        {
            "Yellow", "Blue", "Green", "Orange", "Purple", "Red", "White"
        };

        // Default categories that always exist
        private static readonly string[] DefaultCategories = new string[]
        {
            "Useful Links", "Sticky Notes", "Datasheets", "Project Links"
        };

        private static readonly HttpClient _http = new HttpClient();
        private readonly string _firebaseBaseUrl;
        private readonly string _currentUserName;
        private bool _isAdmin;

        private FlowLayoutPanel flowContent;
        private ComboBox cmbCategory;
        private Button btnAdd;
        private Button btnAddCategory;
        private Label lblTitle;
        private List<HelperEntry> _entries = new List<HelperEntry>();
        private List<string> _customCategories = new List<string>();
        private bool _isDarkMode = true;

        /// <summary>Constructor: Initialize Helper panel with Firebase URL and user context.</summary>
        public HelperPanel(string firebaseBaseUrl, string currentUserName, bool isAdmin = false)
        {
//             DebugLogger.Log($"[Helper] Initializing HelperPanel for user: {currentUserName}, Admin: {isAdmin}");

            _firebaseBaseUrl = firebaseBaseUrl.TrimEnd('/');
            _currentUserName = currentUserName;
            _isAdmin = isAdmin;

            // Configure panel
            this.Width = 320;
            this.Dock = DockStyle.Right;
            this.BackColor = Color.FromArgb(30, 36, 46);
            this.Padding = new Padding(6);
            this.BorderStyle = BorderStyle.FixedSingle;

            BuildUI();

//             DebugLogger.Log("[Helper] HelperPanel initialized");
        }

        private void BuildUI()
        {
            // ── Header ──
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = _isAdmin ? 100 : 66,
                BackColor = Color.Transparent,
                Padding = new Padding(6, 4, 6, 4)
            };

            lblTitle = new Label
            {
                Text = "📖 HELPER WIKI",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 255),
                Location = new Point(6, 4),
                AutoSize = true
            };
            panelHeader.Controls.Add(lblTitle);

            // Category filter
            cmbCategory = new ComboBox
            {
                Location = new Point(6, 32),
                Size = new Size(180, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(38, 44, 56),
                ForeColor = Color.FromArgb(220, 224, 230)
            };
            cmbCategory.SelectedIndexChanged += (s, e) => RenderEntries();
            panelHeader.Controls.Add(cmbCategory);

            if (_isAdmin)
            {
                // Add entry button
                btnAdd = new Button
                {
                    Text = "+ Add",
                    Size = new Size(55, 24),
                    Location = new Point(192, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(100, 200, 255),
                    ForeColor = Color.FromArgb(20, 20, 30),
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnAdd.FlatAppearance.BorderSize = 0;
                btnAdd.Click += BtnAdd_Click;
                panelHeader.Controls.Add(btnAdd);

                // Add text note button (quick text-only entry)
                var btnAddText = new Button
                {
                    Text = "+ Text",
                    Size = new Size(55, 24),
                    Location = new Point(252, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(34, 197, 94),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnAddText.FlatAppearance.BorderSize = 0;
                btnAddText.Click += BtnAddText_Click;
                panelHeader.Controls.Add(btnAddText);

                // Add custom category button
                btnAddCategory = new Button
                {
                    Text = "+ Category",
                    Size = new Size(80, 24),
                    Location = new Point(6, 64),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(70, 80, 100),
                    ForeColor = Color.FromArgb(200, 210, 220),
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnAddCategory.FlatAppearance.BorderSize = 0;
                btnAddCategory.Click += BtnAddCategory_Click;
                panelHeader.Controls.Add(btnAddCategory);
            }

            this.Controls.Add(panelHeader);

            // ── Scrollable content ──
            flowContent = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(2)
            };
            this.Controls.Add(flowContent);
        }

        // ═══════════════════════════════════════════
        // PUBLIC: Load / Refresh
        // ═══════════════════════════════════════════
        /// <summary>Fetch entries and categories from Firebase asynchronously.</summary>
        public async Task RefreshAsync()
        {
//             DebugLogger.Log("[Helper] Refreshing entries and categories from Firebase");
            var localEntries = TeamLocalCacheStore.LoadList<HelperEntry>("helper_local.json");
            var localCategories = TeamLocalCacheStore.LoadList<string>("helper_categories_local.json");

            if (_entries.Count == 0 && localEntries.Count > 0)
            {
                _entries = localEntries;
                _customCategories = localCategories;
                RebuildCategoryCombo();
                RenderEntries();
            }

            try
            {
                // Fetch all entries from Firebase
                string url = _firebaseBaseUrl + "/helper.json";
//                 DebugLogger.Log($"[Helper] Fetching from: {url}");

                var response = await _http.GetAsync(url + "?_cb=" + DateTime.UtcNow.Ticks);
                if (!response.IsSuccessStatusCode)
                {
                    DebugLogger.Log($"[Helper] HTTP error: {response.StatusCode}");
                    if (localEntries.Count > 0)
                    {
                        _entries = localEntries;
                        _customCategories = localCategories;
                        RebuildCategoryCombo();
                        RenderEntries();
                    }
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    if (localEntries.Count > 0)
                        _entries = localEntries;
                    else
                        _entries.Clear();
                }
                else
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, HelperEntry>>(json);
                    if (dict != null)
                    {
                        _entries = dict.Select(kv =>
                        {
                            kv.Value.Key = kv.Key;
                            return kv.Value;
                        }).ToList();
                        TeamLocalCacheStore.SaveList("helper_local.json", _entries);
//                         DebugLogger.Log($"[Helper] Loaded {_entries.Count} entries");
                    }
                    else
                    {
                        DebugLogger.Log("[Helper] Failed to deserialize entries");
                        _entries = localEntries;
                    }
                }

                // Fetch custom categories
                string catUrl = _firebaseBaseUrl + "/helperCategories.json";
//                 DebugLogger.Log($"[Helper] Fetching categories from: {catUrl}");

                var catResponse = await _http.GetAsync(catUrl + "?_cb=" + DateTime.UtcNow.Ticks);
                if (catResponse.IsSuccessStatusCode)
                {
                    string catJson = await catResponse.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(catJson) && catJson != "null")
                    {
                        var catDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(catJson);
                        _customCategories = catDict?.Values.ToList() ?? new List<string>();
                        TeamLocalCacheStore.SaveList("helper_categories_local.json", _customCategories);
//                         DebugLogger.Log($"[Helper] Loaded {_customCategories.Count} custom categories");
                    }
                    else
                    {
                        _customCategories = localCategories;
                    }
                }
                else
                {
                    _customCategories = localCategories;
                }

                // Rebuild UI with fetched data
                RebuildCategoryCombo();
                RenderEntries();

//                 DebugLogger.Log("[Helper] Refresh complete");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Helper] ERROR during refresh: {ex.Message}");
                if (localEntries.Count > 0)
                {
                    _entries = localEntries;
                    _customCategories = localCategories;
                    RebuildCategoryCombo();
                    RenderEntries();
                }
            }
        }

        private void RebuildCategoryCombo()
        {
            string previousSelection = cmbCategory.SelectedItem?.ToString();
            cmbCategory.Items.Clear();
            cmbCategory.Items.Add("All");
            foreach (var cat in DefaultCategories)
                cmbCategory.Items.Add(cat);
            foreach (var cat in _customCategories)
            {
                if (!cmbCategory.Items.Contains(cat))
                    cmbCategory.Items.Add(cat);
            }

            if (previousSelection != null && cmbCategory.Items.Contains(previousSelection))
                cmbCategory.SelectedItem = previousSelection;
            else
                cmbCategory.SelectedIndex = 0;
        }

        // ═══════════════════════════════════════════
        // RENDER ENTRIES
        // ═══════════════════════════════════════════
        /// <summary>Render entries in the flow panel, with optional category filtering and grouping.</summary>
        private void RenderEntries()
        {
//             DebugLogger.Log("[Helper] Rendering entries");

            flowContent.SuspendLayout();
            flowContent.Controls.Clear();

            string filter = cmbCategory?.SelectedItem?.ToString() ?? "All";
//             DebugLogger.Log($"[Helper] Filter: {filter}");

            // Apply category filter
            var filtered = filter == "All"
                ? _entries
                : _entries.Where(e => e.category == filter).ToList();

//             DebugLogger.Log($"[Helper] Filtered to {filtered.Count()} entries");

            // Group by category for "All" view
            var grouped = filtered
                .OrderBy(e => e.category)
                .ThenByDescending(e => e.createdAt)
                .GroupBy(e => e.category ?? "Uncategorized");

            int cardCount = 0;

            foreach (var group in grouped)
            {
                // Draw category header only in "All" view
                if (filter == "All")
                {
                    var catLabel = new Label
                    {
                        Text = "── " + group.Key + " ──",
                        Font = new Font("Segoe UI", 9, FontStyle.Bold),
                        ForeColor = Color.FromArgb(100, 200, 255),
                        AutoSize = false,
                        Width = this.Width - 30,
                        Height = 24,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(4, 4, 0, 0)
                    };
                    flowContent.Controls.Add(catLabel);
                }

                // Render entries in this group
                foreach (var entry in group)
                {
                    var card = CreateEntryCard(entry);
                    flowContent.Controls.Add(card);
                    cardCount++;
                }
            }

            // Show empty state if no entries
            if (cardCount == 0)
            {
//                 DebugLogger.Log("[Helper] No entries to display, showing empty message");
                var emptyLabel = new Label
                {
                    Text = _isAdmin ? "No entries yet.\nClick '+ Add' to create one." : "No entries yet.",
                    Font = new Font("Segoe UI", 9, FontStyle.Italic),
                    ForeColor = Color.FromArgb(130, 140, 160),
                    AutoSize = false,
                    Width = this.Width - 30,
                    Height = 60,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                flowContent.Controls.Add(emptyLabel);
            }
            else
            {
//                 DebugLogger.Log($"[Helper] Rendered {cardCount} entry cards");
            }

            flowContent.ResumeLayout(true);
        }

        // ═══════════════════════════════════════════
        // CREATE CARD
        // ═══════════════════════════════════════════
        private Panel CreateEntryCard(HelperEntry entry)
        {
            Color noteColor = GetNoteColor(entry.color);
            bool isLink = !string.IsNullOrWhiteSpace(entry.url);
            bool isNote = (entry.category == "Sticky Notes");

            // Card background
            Color cardBg;
            if (isNote)
            {
                // Sticky notes use the note color with transparency
                cardBg = _isDarkMode
                    ? Color.FromArgb(noteColor.R / 4 + 20, noteColor.G / 4 + 20, noteColor.B / 4 + 20)
                    : Color.FromArgb(
                        Math.Min(255, noteColor.R + 40),
                        Math.Min(255, noteColor.G + 40),
                        Math.Min(255, noteColor.B + 40));
            }
            else
            {
                cardBg = _isDarkMode ? Color.FromArgb(40, 46, 58) : Color.FromArgb(248, 249, 252);
            }

            var card = new Panel
            {
                Width = this.Width - 30,
                AutoSize = true,
                MinimumSize = new Size(this.Width - 30, 40),
                BackColor = cardBg,
                Margin = new Padding(2, 2, 2, 4),
                Padding = new Padding(8, 6, 8, 6),
                Cursor = isLink ? Cursors.Hand : Cursors.Default
            };

            // Round corners
            card.Paint += (s, e) =>
            {
                var rect = card.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                using (var path = RoundRect(rect, 8))
                using (var pen = new Pen(
                    isNote ? Color.FromArgb(80, noteColor) : Color.FromArgb(50, 100, 200, 255), 1))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawPath(pen, path);
                }

                // Color strip on the left for notes
                if (isNote)
                {
                    using (var brush = new SolidBrush(noteColor))
                    {
                        e.Graphics.FillRectangle(brush, 0, 6, 4, card.Height - 12);
                    }
                }
            };

            int yPos = 4;

            // Title / Name
            var lblTitle = new Label
            {
                Text = (isLink ? "🔗 " : isNote ? "📌 " : "📄 ") + (entry.title ?? "Untitled"),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = isLink
                    ? Color.FromArgb(100, 200, 255)
                    : (_isDarkMode ? Color.FromArgb(230, 235, 245) : Color.FromArgb(30, 40, 60)),
                AutoSize = true,
                Location = new Point(10, yPos),
                MaximumSize = new Size(this.Width - 60, 0),
                Cursor = isLink ? Cursors.Hand : Cursors.Default,
                BackColor = Color.Transparent
            };

            if (isLink)
            {
                lblTitle.Click += (s, e) => OpenUrl(entry.url);
                lblTitle.MouseEnter += (s, e) => lblTitle.Font = new Font(lblTitle.Font, FontStyle.Bold | FontStyle.Underline);
                lblTitle.MouseLeave += (s, e) => lblTitle.Font = new Font(lblTitle.Font, FontStyle.Bold);
            }
            card.Controls.Add(lblTitle);
            yPos += lblTitle.Height + 2;

            // Description / Content
            if (!string.IsNullOrWhiteSpace(entry.description))
            {
                var lblDesc = new Label
                {
                    Text = entry.description,
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = _isDarkMode ? Color.FromArgb(180, 190, 210) : Color.FromArgb(60, 70, 90),
                    AutoSize = true,
                    Location = new Point(10, yPos),
                    MaximumSize = new Size(this.Width - 60, 0),
                    BackColor = Color.Transparent
                };
                card.Controls.Add(lblDesc);
                yPos += lblDesc.Height + 2;
            }

            // URL display for links
            if (isLink)
            {
                var lblUrl = new Label
                {
                    Text = TruncateUrl(entry.url, 45),
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                    ForeColor = Color.FromArgb(80, 160, 220),
                    AutoSize = true,
                    Location = new Point(10, yPos),
                    MaximumSize = new Size(this.Width - 60, 0),
                    Cursor = Cursors.Hand,
                    BackColor = Color.Transparent
                };
                lblUrl.Click += (s, e) => OpenUrl(entry.url);
                card.Controls.Add(lblUrl);
                yPos += lblUrl.Height + 2;
            }

            // Category tag + author
            var lblMeta = new Label
            {
                Text = $"{entry.category ?? ""}  •  {entry.createdBy ?? ""}",
                Font = new Font("Segoe UI", 7, FontStyle.Italic),
                ForeColor = Color.FromArgb(110, 120, 140),
                AutoSize = true,
                Location = new Point(10, yPos),
                BackColor = Color.Transparent
            };
            card.Controls.Add(lblMeta);
            yPos += lblMeta.Height + 4;

            card.Height = yPos + 6;

            // Admin: right-click context menu for edit/delete
            if (_isAdmin)
            {
                var ctxMenu = new ContextMenuStrip();
                ctxMenu.Items.Add("✏ Edit", null, (s, e) => EditEntry(entry));
                ctxMenu.Items.Add("🗑 Delete", null, async (s, e) => await DeleteEntry(entry));
                card.ContextMenuStrip = ctxMenu;

                // Also apply to child labels
                foreach (Control c in card.Controls)
                {
                    if (c.ContextMenuStrip == null)
                        c.ContextMenuStrip = ctxMenu;
                }
            }

            // Click on card opens link
            if (isLink)
            {
                card.Click += (s, e) => OpenUrl(entry.url);
            }

            return card;
        }

        // ═══════════════════════════════════════════
        // ADD / EDIT / DELETE
        // ═══════════════════════════════════════════
        /// <summary>Add button: open full entry dialog.</summary>
        private void BtnAdd_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[Helper] Add button clicked, opening entry dialog");
            ShowEntryDialog(null);
        }

        /// <summary>Add text button: open quick text-only entry dialog.</summary>
        private void BtnAddText_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[Helper] Add text button clicked, opening quick text dialog");
            ShowTextEntryDialog();
        }

        /// <summary>Quick dialog to add a text-only wiki entry (title + multi-line text).</summary>
        private void ShowTextEntryDialog()
        {
            var dlg = new Form
            {
                Text = "\U0001f4dd  Add Text Note",
                Size = new Size(440, 440),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 36, 46)
            };

            var font = new Font("Segoe UI", 9);
            Color lblColor = Color.FromArgb(200, 210, 225);
            Color inputBg = Color.FromArgb(44, 50, 64);
            Color fg = Color.White;
            int y = 15, x = 15, w = 390;

            // Category
            var lblCat = new Label { Text = "Category:", Location = new Point(x, y), ForeColor = lblColor, Font = font, AutoSize = true };
            dlg.Controls.Add(lblCat);
            y += 22;

            var cmbCat = new ComboBox
            {
                Location = new Point(x, y), Size = new Size(w, 24),
                DropDownStyle = ComboBoxStyle.DropDown, Font = font,
                BackColor = inputBg, ForeColor = fg
            };
            foreach (var cat in DefaultCategories) cmbCat.Items.Add(cat);
            cmbCat.Items.Add("Documentation");
            cmbCat.Items.Add("Project Plan");
            foreach (var cat in _customCategories)
            {
                if (!cmbCat.Items.Contains(cat)) cmbCat.Items.Add(cat);
            }
            cmbCat.Text = "Sticky Notes";
            dlg.Controls.Add(cmbCat);
            y += 32;

            // Title
            var lblTitle = new Label { Text = "Title:", Location = new Point(x, y), ForeColor = lblColor, Font = font, AutoSize = true };
            dlg.Controls.Add(lblTitle);
            y += 22;

            var txtTitle = new TextBox
            {
                Location = new Point(x, y), Size = new Size(w, 26), Font = font,
                BackColor = inputBg, ForeColor = fg, BorderStyle = BorderStyle.FixedSingle
            };
            dlg.Controls.Add(txtTitle);
            y += 34;

            // Text content (large multiline)
            var lblText = new Label { Text = "Text content:", Location = new Point(x, y), ForeColor = lblColor, Font = font, AutoSize = true };
            dlg.Controls.Add(lblText);
            y += 22;

            var txtContent = new TextBox
            {
                Location = new Point(x, y), Size = new Size(w, 160), Font = font,
                BackColor = inputBg, ForeColor = fg, BorderStyle = BorderStyle.FixedSingle,
                Multiline = true, ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true
            };
            dlg.Controls.Add(txtContent);
            y += 170;

            // Save / Cancel
            var btnSave = new Button
            {
                Text = "\U0001f4be Save",
                Size = new Size(120, 36),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += async (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                {
                    MessageBox.Show("Title is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var entry = new HelperEntry
                {
                    title = txtTitle.Text.Trim(),
                    description = txtContent.Text.Trim(),
                    url = "",  // no URL for text entries
                    category = cmbCat.Text.Trim(),
                    color = "Yellow",
                    createdBy = _currentUserName,
                    createdAt = DateTime.UtcNow.ToString("o")
                };

                await SaveEntry(entry);
                dlg.Close();
                await RefreshAsync();
            };
            dlg.Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(90, 36),
                Location = new Point(x + 130, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 80, 100),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, ev) => dlg.Close();
            dlg.Controls.Add(btnCancel);

            dlg.ShowDialog();
        }

        private void EditEntry(HelperEntry existing)
        {
            ShowEntryDialog(existing);
        }

        private void ShowEntryDialog(HelperEntry existing)
        {
            bool isEdit = existing != null;
            var dlg = new Form
            {
                Text = isEdit ? "Edit Helper Entry" : "Add Helper Entry",
                Size = new Size(420, 430),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 36, 46)
            };

            int y = 15;
            var font = new Font("Segoe UI", 9);
            var lblColor = Color.FromArgb(200, 210, 225);

            // Category
            var lblCat = new Label { Text = "Category:", Location = new Point(15, y), ForeColor = lblColor, Font = font, AutoSize = true };
            dlg.Controls.Add(lblCat);
            y += 22;

            var cmbCat = new ComboBox
            {
                Location = new Point(15, y), Size = new Size(370, 24),
                DropDownStyle = ComboBoxStyle.DropDownList, Font = font,
                BackColor = Color.FromArgb(44, 50, 64), ForeColor = Color.White
            };
            foreach (var cat in DefaultCategories) cmbCat.Items.Add(cat);
            foreach (var cat in _customCategories)
            {
                if (!cmbCat.Items.Contains(cat)) cmbCat.Items.Add(cat);
            }
            cmbCat.SelectedItem = existing?.category ?? cmbCat.Items[0];
            dlg.Controls.Add(cmbCat);
            y += 32;

            // Title
            var lblTitleDlg = new Label { Text = "Title:", Location = new Point(15, y), ForeColor = lblColor, Font = font, AutoSize = true };
            dlg.Controls.Add(lblTitleDlg);
            y += 22;

            var txtTitle = new TextBox
            {
                Location = new Point(15, y), Size = new Size(370, 24), Font = font,
                BackColor = Color.FromArgb(44, 50, 64), ForeColor = Color.White,
                Text = existing?.title ?? ""
            };
            dlg.Controls.Add(txtTitle);
            y += 32;

            // URL (optional)
            var lblUrlDlg = new Label { Text = "URL (optional):", Location = new Point(15, y), ForeColor = lblColor, Font = font, AutoSize = true };
            dlg.Controls.Add(lblUrlDlg);
            y += 22;

            var txtUrl = new TextBox
            {
                Location = new Point(15, y), Size = new Size(370, 24), Font = font,
                BackColor = Color.FromArgb(44, 50, 64), ForeColor = Color.White,
                Text = existing?.url ?? ""
            };
            dlg.Controls.Add(txtUrl);
            y += 32;

            // Description
            var lblDescDlg = new Label { Text = "Description / Notes:", Location = new Point(15, y), ForeColor = lblColor, Font = font, AutoSize = true };
            dlg.Controls.Add(lblDescDlg);
            y += 22;

            var txtDesc = new TextBox
            {
                Location = new Point(15, y), Size = new Size(370, 80), Font = font,
                BackColor = Color.FromArgb(44, 50, 64), ForeColor = Color.White,
                Multiline = true, ScrollBars = ScrollBars.Vertical,
                Text = existing?.description ?? ""
            };
            dlg.Controls.Add(txtDesc);
            y += 90;

            // Color picker (for sticky notes)
            var lblColorPick = new Label { Text = "Note Color:", Location = new Point(15, y), ForeColor = lblColor, Font = font, AutoSize = true };
            dlg.Controls.Add(lblColorPick);
            y += 22;

            var colorPanel = new FlowLayoutPanel
            {
                Location = new Point(15, y),
                Size = new Size(370, 32),
                FlowDirection = FlowDirection.LeftToRight
            };

            int selectedColorIdx = 0;
            if (existing != null)
            {
                for (int i = 0; i < NoteColorNames.Length; i++)
                {
                    if (NoteColorNames[i] == existing.color) { selectedColorIdx = i; break; }
                }
            }

            var colorButtons = new List<Button>();
            for (int i = 0; i < NoteColors.Length; i++)
            {
                int idx = i;
                var btn = new Button
                {
                    Size = new Size(28, 28),
                    BackColor = NoteColors[i],
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(2),
                    Cursor = Cursors.Hand,
                    Text = idx == selectedColorIdx ? "✓" : "",
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = Color.FromArgb(40, 40, 40)
                };
                btn.FlatAppearance.BorderSize = idx == selectedColorIdx ? 2 : 0;
                btn.FlatAppearance.BorderColor = Color.White;
                btn.Click += (s, e) =>
                {
                    selectedColorIdx = idx;
                    foreach (var b in colorButtons)
                    {
                        b.Text = "";
                        b.FlatAppearance.BorderSize = 0;
                    }
                    btn.Text = "✓";
                    btn.FlatAppearance.BorderSize = 2;
                };
                colorButtons.Add(btn);
                colorPanel.Controls.Add(btn);
            }
            dlg.Controls.Add(colorPanel);
            y += 40;

            // Save / Cancel
            var btnSave = new Button
            {
                Text = isEdit ? "💾 Update" : "💾 Save",
                Size = new Size(120, 36),
                Location = new Point(120, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += async (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                {
                    MessageBox.Show("Title is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var entry = existing ?? new HelperEntry();
                entry.title = txtTitle.Text.Trim();
                entry.url = txtUrl.Text.Trim();
                entry.description = txtDesc.Text.Trim();
                entry.category = cmbCat.SelectedItem?.ToString() ?? "Useful Links";
                entry.color = NoteColorNames[selectedColorIdx];
                entry.createdBy = _currentUserName;
                if (!isEdit) entry.createdAt = DateTime.UtcNow.ToString("o");

                await SaveEntry(entry);
                dlg.Close();
                await RefreshAsync();
            };
            dlg.Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(90, 36),
                Location = new Point(250, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 80, 100),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, ev) => dlg.Close();
            dlg.Controls.Add(btnCancel);

            dlg.ShowDialog();
        }

        private void BtnAddCategory_Click(object sender, EventArgs e)
        {
            var dlg = new Form
            {
                Text = "Add Custom Category",
                Size = new Size(340, 160),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 36, 46)
            };

            var lblName = new Label
            {
                Text = "Category Name:",
                Location = new Point(15, 15),
                ForeColor = Color.FromArgb(200, 210, 225),
                Font = new Font("Segoe UI", 9),
                AutoSize = true
            };
            dlg.Controls.Add(lblName);

            var txtName = new TextBox
            {
                Location = new Point(15, 38),
                Size = new Size(290, 24),
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(44, 50, 64),
                ForeColor = Color.White
            };
            dlg.Controls.Add(txtName);

            var btnSave = new Button
            {
                Text = "💾 Save",
                Size = new Size(100, 32),
                Location = new Point(80, 75),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += async (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await SaveCategory(txtName.Text.Trim());
                dlg.Close();
                await RefreshAsync();
            };
            dlg.Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 32),
                Location = new Point(190, 75),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 80, 100),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, ev) => dlg.Close();
            dlg.Controls.Add(btnCancel);

            dlg.ShowDialog();
        }

        // ═══════════════════════════════════════════
        // FIREBASE CRUD
        // ═══════════════════════════════════════════
        /// <summary>Save entry to Firebase (create new or update existing).</summary>
        private async Task SaveEntry(HelperEntry entry)
        {
//             DebugLogger.Log($"[Helper] Saving entry: {entry.title}");

            try
            {
                string json = JsonConvert.SerializeObject(entry);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                if (!string.IsNullOrWhiteSpace(entry.Key))
                {
                    // Update existing entry
                    string url = _firebaseBaseUrl + $"/helper/{entry.Key}.json";
//                     DebugLogger.Log($"[Helper] Updating entry at: {url}");
                    await _http.PutAsync(url, content);
//                     DebugLogger.Log("[Helper] Entry update successful");
                }
                else
                {
                    // Create new entry
                    string url = _firebaseBaseUrl + "/helper.json";
//                     DebugLogger.Log($"[Helper] Creating new entry at: {url}");
                    await _http.PostAsync(url, content);
//                     DebugLogger.Log("[Helper] Entry creation successful");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Helper] ERROR saving entry: {ex.Message}");
            }
        }

        /// <summary>Delete entry from Firebase with user confirmation.</summary>
        private async Task DeleteEntry(HelperEntry entry)
        {
//             DebugLogger.Log($"[Helper] Delete entry requested: {entry.title}");

            if (string.IsNullOrWhiteSpace(entry.Key))
            {
//                 DebugLogger.Log("[Helper] Entry has no Key, cannot delete");
                return;
            }

            var result = MessageBox.Show(
                $"Delete '{entry.title}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
//                 DebugLogger.Log("[Helper] Delete cancelled by user");
                return;
            }

            try
            {
                string url = _firebaseBaseUrl + $"/helper/{entry.Key}.json";
//                 DebugLogger.Log($"[Helper] Deleting entry at: {url}");
                await _http.DeleteAsync(url);
//                 DebugLogger.Log("[Helper] Entry deleted successfully");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Helper] ERROR deleting entry: {ex.Message}");
            }
        }

        /// <summary>Save new custom category to Firebase.</summary>
        private async Task SaveCategory(string name)
        {
//             DebugLogger.Log($"[Helper] Saving custom category: {name}");

            try
            {
                string json = JsonConvert.SerializeObject(name);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                string url = _firebaseBaseUrl + "/helperCategories.json";
//                 DebugLogger.Log($"[Helper] Posting to: {url}");
                await _http.PostAsync(url, content);
//                 DebugLogger.Log("[Helper] Category saved successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Helper] ERROR saving category: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════
        private Color GetNoteColor(string colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName)) return NoteColors[0];
            for (int i = 0; i < NoteColorNames.Length; i++)
            {
                if (NoteColorNames[i].Equals(colorName, StringComparison.OrdinalIgnoreCase))
                    return NoteColors[i];
            }
            return NoteColors[0];
        }

        private string TruncateUrl(string url, int max)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            return url.Length > max ? url.Substring(0, max) + "..." : url;
        }

        /// <summary>Open URL in default browser.</summary>
        private void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
//                 DebugLogger.Log("[Helper] OpenUrl: empty URL");
                return;
            }

//             DebugLogger.Log($"[Helper] Opening URL: {url}");

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
//                 DebugLogger.Log("[Helper] URL opened successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Helper] ERROR opening URL: {ex.Message}");
            }
        }

        private GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ═══════════════════════════════════════════
        // THEME APPLICATION
        // ═══════════════════════════════════════════

        /// <summary>Apply theme colors to panel and controls.</summary>
        public void ApplyTheme(bool darkMode, CustomTheme customTheme = null)
        {
//             DebugLogger.Log($"[Helper] Applying theme: darkMode={darkMode}, customTheme={customTheme?.PresetName ?? "none"}");

            _isDarkMode = darkMode;

            if (customTheme != null && customTheme.Enabled)
            {
                // Use custom theme colors
                this.BackColor = customTheme.GetCard();
                lblTitle.ForeColor = customTheme.GetAccent();
                if (cmbCategory != null)
                {
                    cmbCategory.BackColor = customTheme.GetInput();
                    cmbCategory.ForeColor = customTheme.GetText();
                }
//                 DebugLogger.Log("[Helper] Custom theme applied");
            }
            else
            {
                // Use default dark/light theme
                this.BackColor = darkMode ? Color.FromArgb(30, 36, 46) : Color.FromArgb(245, 247, 252);
                lblTitle.ForeColor = darkMode ? Color.FromArgb(100, 200, 255) : Color.FromArgb(20, 80, 160);
                if (cmbCategory != null)
                {
                    cmbCategory.BackColor = darkMode ? Color.FromArgb(38, 44, 56) : Color.White;
                    cmbCategory.ForeColor = darkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 40, 60);
                }
//                 DebugLogger.Log($"[Helper] Default theme applied ({(darkMode ? "dark" : "light")})");
            }

            // Refresh rendering with new theme
            RenderEntries();
        }
    }

    // ================================================================
    // DATA MODEL
    // ================================================================
    public class HelperEntry
    {
        [JsonIgnore]
        public string Key { get; set; }

        public string title { get; set; }
        public string description { get; set; }
        public string url { get; set; }
        public string category { get; set; }
        public string color { get; set; }
        public string createdBy { get; set; }
        public string createdAt { get; set; }
    }
}
