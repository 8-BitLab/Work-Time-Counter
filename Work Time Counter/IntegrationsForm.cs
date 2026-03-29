// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  FILE:        IntegrationsForm.cs                                           ║
// ║  PURPOSE:     Multi-platform integration manager — connect external         ║
// ║               project management tools and import tasks into Project Stages  ║
// ║  PLATFORMS:   Jira, ClickUp, Linear, GitHub Issues, Trello, Asana,          ║
// ║               monday.com, Notion                                            ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    public class IntegrationsForm : Form
    {
        private readonly bool _isDarkMode;
        private readonly string _teamName;
        private readonly Color _bgColor;
        private readonly Color _cardBg;
        private readonly Color _fgColor;
        private readonly Color _dimColor;
        private readonly Color _fieldBg;
        private readonly Color _accentColor = Color.FromArgb(255, 127, 80);

        // ════════════════════════════════════════════════════════════════════════
        // INTEGRATION PLATFORM DEFINITIONS
        // ════════════════════════════════════════════════════════════════════════
        private readonly List<IntegrationPlatform> _platforms = new List<IntegrationPlatform>
        {
            new IntegrationPlatform("Jira", "Atlassian Jira Cloud — import issues and epics",
                Color.FromArgb(0, 82, 204), "🔵",
                new[] { "Project URL", "Email", "API Token" },
                new[] { "https://yourteam.atlassian.net/projects/KEY", "you@company.com", "Jira API token from id.atlassian.com" }),

            new IntegrationPlatform("ClickUp", "ClickUp — import tasks, lists, and folders",
                Color.FromArgb(123, 104, 238), "🟣",
                new[] { "Workspace URL", "API Token" },
                new[] { "https://app.clickup.com/12345", "pk_xxxxxxx (Settings → Apps)" }),

            new IntegrationPlatform("Linear", "Linear — import issues and cycles",
                Color.FromArgb(88, 101, 242), "🔷",
                new[] { "Workspace Slug", "API Key" },
                new[] { "your-workspace", "lin_api_xxxxxxx (Settings → API)" }),

            new IntegrationPlatform("GitHub", "GitHub Issues & Projects — import issues and project boards",
                Color.FromArgb(36, 41, 47), "⚫",
                new[] { "Repository (owner/repo)", "Personal Access Token" },
                new[] { "owner/repo-name", "ghp_xxxxxxx (Settings → Developer → PAT)" }),

            new IntegrationPlatform("Trello", "Trello — import cards and boards",
                Color.FromArgb(0, 121, 191), "🟦",
                new[] { "Board URL", "API Key", "Token" },
                new[] { "https://trello.com/b/xxxxx/board-name", "Trello API key", "Trello token from trello.com/app-key" }),

            new IntegrationPlatform("Asana", "Asana — import tasks and projects",
                Color.FromArgb(246, 119, 98), "🟠",
                new[] { "Project URL", "Personal Access Token" },
                new[] { "https://app.asana.com/0/project-id", "Asana PAT from app.asana.com/developer-console" }),

            new IntegrationPlatform("monday.com", "monday.com — import items and boards",
                Color.FromArgb(108, 59, 232), "🟪",
                new[] { "Board URL", "API Token" },
                new[] { "https://your-team.monday.com/boards/123", "API v2 token from monday.com/developers" }),

            new IntegrationPlatform("Notion", "Notion — import database entries and pages",
                Color.FromArgb(55, 53, 47), "⬛",
                new[] { "Database URL", "Integration Token" },
                new[] { "https://notion.so/workspace/database-id", "secret_xxxxxxx (Settings → Integrations)" }),
        };

        private Panel _detailPanel;
        private Panel _listPanel;
        private IntegrationPlatform _selectedPlatform;

        // ════════════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Initializes the Integrations Form with theme colors and builds the UI.
        /// Sets up platform list (left) and connection form (right).
        /// </summary>
        public IntegrationsForm(bool isDarkMode, string teamName)
        {
//             DebugLogger.Log($"[Integrations] Constructor: Initializing for team '{teamName}', dark mode: {isDarkMode}");

            _isDarkMode = isDarkMode;
            _teamName = teamName;

            // Apply theme colors based on dark mode setting
            _bgColor = _isDarkMode ? Color.FromArgb(18, 20, 28) : Color.FromArgb(240, 242, 245);
            _cardBg = _isDarkMode ? Color.FromArgb(28, 32, 42) : Color.White;
            _fgColor = _isDarkMode ? Color.FromArgb(220, 225, 235) : Color.FromArgb(30, 35, 45);
            _dimColor = _isDarkMode ? Color.FromArgb(100, 110, 130) : Color.FromArgb(130, 140, 155);
            _fieldBg = _isDarkMode ? Color.FromArgb(36, 40, 52) : Color.FromArgb(248, 249, 252);

//             DebugLogger.Log("[Integrations] Constructor: Colors applied, initializing form");

            InitializeForm();
            BuildUI();

//             DebugLogger.Log("[Integrations] Constructor: Form initialization complete");
        }

        /// <summary>
        /// Configures basic form properties (size, border style, appearance).
        /// </summary>
        private void InitializeForm()
        {
//             DebugLogger.Log("[Integrations] InitializeForm: Setting up form properties");

            this.Text = "🔗 Integrations — " + _teamName;
            this.Width = 820;
            this.Height = 560;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = _bgColor;
            this.ForeColor = _fgColor;
            this.Font = new Font("Segoe UI", 9);

//             DebugLogger.Log("[Integrations] InitializeForm: Form properties configured");
        }

        // ════════════════════════════════════════════════════════════════════════
        // BUILD UI
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Builds the main UI layout with:
        /// - Header with title and instructions
        /// - Left panel with list of integration platforms
        /// - Right panel for connection form (initially shows placeholder)
        /// </summary>
        private void BuildUI()
        {
//             DebugLogger.Log("[Integrations] BuildUI: Starting UI construction");

            // ── HEADER ──
            var lblTitle = new Label
            {
                Text = "🔗 Integrations",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = _fgColor,
                Location = new Point(20, 12),
                AutoSize = true
            };
            this.Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text = "Connect external project management tools — tasks will import into your Project Stages",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = _dimColor,
                Location = new Point(22, 38),
                AutoSize = true
            };
            this.Controls.Add(lblSub);

//             DebugLogger.Log("[Integrations] BuildUI: Header created");

            // ── LEFT: PLATFORM LIST ──
            _listPanel = new Panel
            {
                Location = new Point(14, 62),
                Size = new Size(250, 450),
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(_listPanel);

            int py = 0;
            foreach (var plat in _platforms)
            {
                var card = CreatePlatformCard(plat, py);
                _listPanel.Controls.Add(card);
                py += 54;
            }

//             DebugLogger.Log($"[Integrations] BuildUI: Created {_platforms.Count} platform cards");

            // ── RIGHT: DETAIL / CONNECTION FORM ──
            _detailPanel = new Panel
            {
                Location = new Point(274, 62),
                Size = new Size(524, 450),
                BackColor = _cardBg,
                Padding = new Padding(20)
            };
            // Rounded corners
            _detailPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            };
            this.Controls.Add(_detailPanel);

//             DebugLogger.Log("[Integrations] BuildUI: Detail panel created");

            // Show placeholder
            ShowPlaceholder();

//             DebugLogger.Log("[Integrations] BuildUI: UI construction complete");
        }

        // ════════════════════════════════════════════════════════════════════════
        // CREATE PLATFORM CARD (left list item)
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Creates a clickable platform card for the left list.
        /// Shows icon, name, and description. Highlights on select and hover.
        /// </summary>
        private Panel CreatePlatformCard(IntegrationPlatform plat, int yPos)
        {
//             DebugLogger.Log($"[Integrations] CreatePlatformCard: Creating card for {plat.Name}");

            var card = new Panel
            {
                Location = new Point(0, yPos),
                Size = new Size(240, 50),
                BackColor = _cardBg,
                Cursor = Cursors.Hand,
                Tag = plat
            };

            // Color accent bar on left
            var accentBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(4, 50),
                BackColor = plat.BrandColor
            };
            card.Controls.Add(accentBar);

            // Icon
            var lblIcon = new Label
            {
                Text = plat.Icon,
                Font = new Font("Segoe UI Emoji", 14),
                Location = new Point(10, 8),
                AutoSize = true
            };
            card.Controls.Add(lblIcon);

            // Name
            var lblName = new Label
            {
                Text = plat.Name,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = _fgColor,
                Location = new Point(42, 6),
                AutoSize = true
            };
            card.Controls.Add(lblName);

            // Description (short)
            var lblDesc = new Label
            {
                Text = plat.Description.Length > 40 ? plat.Description.Substring(0, 40) + "…" : plat.Description,
                Font = new Font("Segoe UI", 7),
                ForeColor = _dimColor,
                Location = new Point(42, 26),
                AutoSize = true
            };
            card.Controls.Add(lblDesc);

            // Click handler for all child controls
            Action selectAction = () =>
            {
//                 DebugLogger.Log($"[Integrations] CreatePlatformCard: {plat.Name} selected");

                _selectedPlatform = plat;
                // Highlight selected card
                foreach (Control c in _listPanel.Controls)
                {
                    if (c is Panel p)
                        p.BackColor = _cardBg;
                }
                card.BackColor = _isDarkMode ? Color.FromArgb(38, 44, 58) : Color.FromArgb(230, 238, 250);
                ShowConnectionForm(plat);
            };

            card.Click += (s, e) => selectAction();
            foreach (Control child in card.Controls)
            {
                child.Click += (s, e) => selectAction();
            }

            // Hover effect
            Action<bool> setHover = (hover) =>
            {
                if (_selectedPlatform == plat) return;
                card.BackColor = hover
                    ? (_isDarkMode ? Color.FromArgb(34, 38, 50) : Color.FromArgb(240, 244, 252))
                    : _cardBg;
            };
            card.MouseEnter += (s, e) => setHover(true);
            card.MouseLeave += (s, e) => setHover(false);
            foreach (Control child in card.Controls)
            {
                child.MouseEnter += (s, e) => setHover(true);
                child.MouseLeave += (s, e) => setHover(false);
            }

            return card;
        }

        // ════════════════════════════════════════════════════════════════════════
        // SHOW PLACEHOLDER (no platform selected)
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Clears the detail panel and shows an instruction message.
        /// Used when no platform is selected yet.
        /// </summary>
        private void ShowPlaceholder()
        {
//             DebugLogger.Log("[Integrations] ShowPlaceholder: Showing placeholder message");

            _detailPanel.Controls.Clear();
            var lbl = new Label
            {
                Text = "👈  Select a platform to connect",
                Font = new Font("Segoe UI", 12),
                ForeColor = _dimColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            _detailPanel.Controls.Add(lbl);
        }

        // ════════════════════════════════════════════════════════════════════════
        // SHOW CONNECTION FORM (right panel)
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Displays the connection form for the selected platform.
        /// Includes header, description, input fields, and action buttons.
        /// Supports password masking for token/key fields.
        /// </summary>
        private void ShowConnectionForm(IntegrationPlatform plat)
        {
//             DebugLogger.Log($"[Integrations] ShowConnectionForm: Building form for {plat.Name}");

            _detailPanel.Controls.Clear();

            int y = 10;
            int px = 16;

            // ── Platform header with brand color ──
            var headerBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(_detailPanel.Width, 52),
                BackColor = plat.BrandColor
            };

            var lblHeader = new Label
            {
                Text = plat.Icon + "  " + plat.Name,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(px, 12),
                AutoSize = true
            };
            headerBar.Controls.Add(lblHeader);
            _detailPanel.Controls.Add(headerBar);
            y = 62;

            // ── Description ──
            var lblDesc = new Label
            {
                Text = plat.Description,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = _dimColor,
                Location = new Point(px, y),
                AutoSize = true
            };
            _detailPanel.Controls.Add(lblDesc);
            y += 26;

            // ── Sync target info ──
            var lblSyncInfo = new Label
            {
                Text = "📥 Tasks will be imported into your team's Project Stages",
                Font = new Font("Segoe UI", 8),
                ForeColor = _isDarkMode ? Color.FromArgb(80, 200, 120) : Color.FromArgb(22, 163, 74),
                Location = new Point(px, y),
                AutoSize = true
            };
            _detailPanel.Controls.Add(lblSyncInfo);
            y += 28;

            // ── Separator ──
            var sep = new Panel
            {
                Location = new Point(px, y),
                Size = new Size(_detailPanel.Width - 40, 1),
                BackColor = _isDarkMode ? Color.FromArgb(50, 56, 68) : Color.FromArgb(220, 225, 235)
            };
            _detailPanel.Controls.Add(sep);
            y += 12;

            // ── Connection fields ──
            var textBoxes = new List<TextBox>();
//             DebugLogger.Log($"[Integrations] ShowConnectionForm: Creating {plat.FieldNames.Length} input fields");

            for (int i = 0; i < plat.FieldNames.Length; i++)
            {
                var lblField = new Label
                {
                    Text = plat.FieldNames[i].ToUpper(),
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    ForeColor = _dimColor,
                    Location = new Point(px, y),
                    AutoSize = true
                };
                _detailPanel.Controls.Add(lblField);
                y += 18;

                var txtField = new TextBox
                {
                    Location = new Point(px, y),
                    Size = new Size(_detailPanel.Width - 60, 28),
                    Font = new Font("Segoe UI", 9),
                    BackColor = _fieldBg,
                    ForeColor = _fgColor,
                    BorderStyle = BorderStyle.FixedSingle
                };

                // Placeholder text workaround
                string placeholder = plat.FieldPlaceholders[i];
                txtField.Text = placeholder;
                txtField.ForeColor = Color.Gray;
                txtField.GotFocus += (s, e) =>
                {
                    if (txtField.Text == placeholder)
                    {
                        txtField.Text = "";
                        txtField.ForeColor = _fgColor;
                    }
                };
                txtField.LostFocus += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(txtField.Text))
                    {
                        txtField.Text = placeholder;
                        txtField.ForeColor = Color.Gray;
                    }
                };

                // If it's a token/key field, use password char
                if (plat.FieldNames[i].ToLower().Contains("token") ||
                    plat.FieldNames[i].ToLower().Contains("key") ||
                    plat.FieldNames[i].ToLower().Contains("secret"))
                {
//                     DebugLogger.Log($"[Integrations] ShowConnectionForm: Applying password masking to {plat.FieldNames[i]}");
                    // Don't mask placeholder, but mask real input
                    txtField.GotFocus += (s, e) => { txtField.UseSystemPasswordChar = true; };
                    txtField.LostFocus += (s, e) =>
                    {
                        if (txtField.ForeColor == Color.Gray)
                            txtField.UseSystemPasswordChar = false;
                    };
                }

                _detailPanel.Controls.Add(txtField);
                textBoxes.Add(txtField);
                y += 36;
            }

            y += 8;

            // ── Buttons row ──
            int bx = px;

            var btnTest = new Button
            {
                Text = "✅ Test Connection",
                Location = new Point(bx, y),
                Size = new Size(150, 36),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnTest.FlatAppearance.BorderSize = 0;
            btnTest.Click += (s, e) =>
            {
//                 DebugLogger.Log($"[Integrations] ShowConnectionForm: Test connection clicked for {plat.Name}");
                MessageBox.Show(
                    $"🔗 {plat.Name} integration is not yet implemented.\n\n" +
                    "This will test the connection using the credentials you provided " +
                    "and verify access to your project data.\n\n" +
                    "Feature coming in the next update!",
                    $"{plat.Name} — Test Connection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _detailPanel.Controls.Add(btnTest);
            bx += 158;

            var btnImport = new Button
            {
                Text = "📥 Import to Stages",
                Location = new Point(bx, y),
                Size = new Size(150, 36),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnImport.FlatAppearance.BorderSize = 0;
            btnImport.Click += (s, e) =>
            {
//                 DebugLogger.Log($"[Integrations] ShowConnectionForm: Import clicked for {plat.Name}");
                MessageBox.Show(
                    $"📥 {plat.Name} import is not yet implemented.\n\n" +
                    $"This will pull tasks/issues from {plat.Name} and map them " +
                    "into your team's Project Stages.\n\n" +
                    "Feature coming in the next update!",
                    $"{plat.Name} — Import",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _detailPanel.Controls.Add(btnImport);
            bx += 158;

            var btnDisconnect = new Button
            {
                Text = "🔌 Disconnect",
                Location = new Point(bx, y),
                Size = new Size(130, 36),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnDisconnect.FlatAppearance.BorderSize = 0;
            btnDisconnect.Click += (s, e) =>
            {
//                 DebugLogger.Log($"[Integrations] ShowConnectionForm: Disconnect clicked for {plat.Name}");
                var confirm = MessageBox.Show(
                    $"Disconnect {plat.Name} integration?\n\nThis will remove saved credentials.",
                    "Disconnect", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm == DialogResult.Yes)
                {
//                     DebugLogger.Log($"[Integrations] ShowConnectionForm: Clearing credentials for {plat.Name}");
                    foreach (var tb in textBoxes)
                    {
                        tb.Text = "";
                        tb.ForeColor = _fgColor;
                    }
                }
            };
            _detailPanel.Controls.Add(btnDisconnect);

            y += 48;

            // ── Status label ──
            var lblStatus = new Label
            {
                Text = "⚪ Not connected",
                Font = new Font("Segoe UI", 8),
                ForeColor = _dimColor,
                Location = new Point(px, y),
                AutoSize = true
            };
            _detailPanel.Controls.Add(lblStatus);

//             DebugLogger.Log($"[Integrations] ShowConnectionForm: Form complete for {plat.Name}");
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // DATA CLASS: IntegrationPlatform
    // ════════════════════════════════════════════════════════════════════════════
    public class IntegrationPlatform
    {
        public string Name { get; }
        public string Description { get; }
        public Color BrandColor { get; }
        public string Icon { get; }
        public string[] FieldNames { get; }
        public string[] FieldPlaceholders { get; }

        public IntegrationPlatform(string name, string description, Color brandColor, string icon,
            string[] fieldNames, string[] fieldPlaceholders)
        {
            Name = name;
            Description = description;
            BrandColor = brandColor;
            Icon = icon;
            FieldNames = fieldNames;
            FieldPlaceholders = fieldPlaceholders;
        }
    }
}
