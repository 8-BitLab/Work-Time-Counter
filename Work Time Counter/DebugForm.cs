// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        DebugForm.cs                                                 ║
// ║  PURPOSE:     DEBUG INFORMATION PANEL FOR TROUBLESHOOTING                  ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  Github:      https://github.com/8BitLabEngineering                        ║
// ║                                                                            ║
// ║  COLOR PALETTE:                                                            ║
// ║    🔴 ERROR   — Red (#FF4444)        — Exceptions, failures, crashes       ║
// ║    🟡 WARNING — Orange (#FFA500)     — Warnings, retries, fallbacks        ║
// ║    🟢 SUCCESS — Green (#44FF44)      — Completed operations, confirmations ║
// ║    🔵 INFO    — Cyan (#00CCFF)       — General information, state changes  ║
// ║    🟣 FIREBASE— Magenta (#DD88FF)    — Firebase read/write operations      ║
// ║    🔵 NETWORK — DodgerBlue (#1E90FF) — HTTP, TCP, LAN, P2P operations     ║
// ║    ⚪ DEBUG   — Gray (#AAAAAA)       — Verbose debug, variable dumps       ║
// ║    🟡 TIMER   — Gold (#FFD700)       — Timer events, heartbeats            ║
// ║    🟢 UI      — LimeGreen (#32CD32)  — UI changes, panel visibility        ║
// ║    ⬜ DEFAULT — White (#E0E0E0)      — Everything else                     ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    /// <summary>
    /// DebugForm: A dark-themed, color-coded debug logging panel.
    /// Features: Category-based coloring, search/filter, line trimming, clear, category filter buttons.
    /// </summary>
    public partial class DebugForm : Form
    {
        // ── CONSTANTS ──
        private const int MAX_LINES = 5000;  // Trim oldest messages beyond this to prevent memory issues

        // ── COLOR PALETTE — Dark theme, high-contrast colors for each log category ──
        private static readonly Color BG_COLOR = Color.FromArgb(18, 18, 24);          // Deep dark background
        private static readonly Color BG_TOOLBAR = Color.FromArgb(28, 30, 38);        // Toolbar background
        private static readonly Color BG_SEARCH = Color.FromArgb(36, 38, 48);         // Search box background
        private static readonly Color FG_DEFAULT = Color.FromArgb(200, 204, 210);     // Default text (light gray)

        private static readonly Color CLR_ERROR = Color.FromArgb(255, 68, 68);        // 🔴 Red — errors, exceptions
        private static readonly Color CLR_WARNING = Color.FromArgb(255, 165, 0);      // 🟡 Orange — warnings
        private static readonly Color CLR_SUCCESS = Color.FromArgb(68, 255, 68);      // 🟢 Bright green — success
        private static readonly Color CLR_INFO = Color.FromArgb(0, 204, 255);         // 🔵 Cyan — info
        private static readonly Color CLR_FIREBASE = Color.FromArgb(221, 136, 255);   // 🟣 Magenta — Firebase ops
        private static readonly Color CLR_NETWORK = Color.FromArgb(30, 144, 255);     // 🔵 DodgerBlue — network/HTTP
        private static readonly Color CLR_DEBUG = Color.FromArgb(140, 140, 150);      // ⚪ Gray — verbose debug
        private static readonly Color CLR_TIMER = Color.FromArgb(255, 215, 0);        // 🟡 Gold — timer/heartbeat
        private static readonly Color CLR_UI = Color.FromArgb(50, 205, 50);           // 🟢 LimeGreen — UI changes
        private static readonly Color CLR_STARTUP = Color.FromArgb(255, 180, 100);    // 🟠 Warm orange — startup/init

        // ── MESSAGE STORAGE ──
        private List<DebugMessage> allMessages = new List<DebugMessage>();

        // Structure to hold a message with its display color and category tag
        private struct DebugMessage
        {
            public string Text;
            public Color ForeColor;
            public string Category;  // "ERROR", "WARNING", "SUCCESS", "INFO", "FIREBASE", etc.
        }

        // ── UI CONTROLS ──
        private Button btnClear;
        private TextBox txtSearch;
        private Label lblSearch;
        private Label lblCount;         // Shows message count
        private ComboBox cmbCategory;   // Filter by category

        public DebugForm()
        {
            InitializeComponent();
            this.Text = "🔍 WorkFlow Debug Console";
            this.Size = new Size(850, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Visible = false;

            // Apply dark theme to the entire form
            this.BackColor = BG_COLOR;
            this.ForeColor = FG_DEFAULT;

            // Setup the debug console UI
            InitializeControlsPanel();
        }

        /// <summary>
        /// Builds the toolbar with search box, category filter, clear button, and message counter.
        /// Dark themed to match the console aesthetic.
        /// </summary>
        private void InitializeControlsPanel()
        {
            // ── TOOLBAR PANEL — docked at top ──
            Panel panelControls = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = BG_TOOLBAR,
                Padding = new Padding(6, 6, 6, 6)
            };

            // ── SEARCH LABEL ──
            lblSearch = new Label
            {
                Text = "🔍",
                AutoSize = true,
                Location = new Point(8, 12),
                ForeColor = FG_DEFAULT,
                Font = new Font("Segoe UI", 10)
            };

            // ── SEARCH TEXTBOX — filters messages in real-time ──
            txtSearch = new TextBox
            {
                Location = new Point(30, 9),
                Width = 220,
                Height = 24,
                BackColor = BG_SEARCH,
                ForeColor = FG_DEFAULT,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f)
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;
            new ToolTip().SetToolTip(txtSearch, "Type to filter debug messages (searches all text)");

            // ── CATEGORY FILTER DROPDOWN ──
            cmbCategory = new ComboBox
            {
                Location = new Point(260, 9),
                Width = 130,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BG_SEARCH,
                ForeColor = FG_DEFAULT,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            cmbCategory.Items.AddRange(new object[] {
                "ALL", "ERROR", "WARNING", "SUCCESS", "INFO",
                "FIREBASE", "NETWORK", "TIMER", "UI", "STARTUP", "DEBUG"
            });
            cmbCategory.SelectedIndex = 0;  // Default to ALL
            cmbCategory.SelectedIndexChanged += CmbCategory_SelectedIndexChanged;
            new ToolTip().SetToolTip(cmbCategory, "Filter by message category");

            // ── CLEAR BUTTON ──
            btnClear = new Button
            {
                Text = "🗑 Clear",
                Location = new Point(400, 7),
                Width = 80,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 20, 20),
                ForeColor = CLR_ERROR,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClear.FlatAppearance.BorderColor = Color.FromArgb(100, 40, 40);
            btnClear.FlatAppearance.BorderSize = 1;
            btnClear.Click += BtnClear_Click;

            // ── MESSAGE COUNTER LABEL ──
            lblCount = new Label
            {
                Text = "0 messages",
                AutoSize = true,
                Location = new Point(500, 12),
                ForeColor = CLR_DEBUG,
                Font = new Font("Consolas", 9)
            };

            // ── COLOR LEGEND LABEL ──
            Label lblLegend = new Label
            {
                Text = "🔴ERR 🟡WARN 🟢OK 🔵INFO 🟣FB ⚪DBG",
                AutoSize = true,
                Location = new Point(620, 12),
                ForeColor = Color.FromArgb(100, 104, 110),
                Font = new Font("Segoe UI", 7.5f)
            };

            // Add all controls to toolbar
            panelControls.Controls.Add(lblSearch);
            panelControls.Controls.Add(txtSearch);
            panelControls.Controls.Add(cmbCategory);
            panelControls.Controls.Add(btnClear);
            panelControls.Controls.Add(lblCount);
            panelControls.Controls.Add(lblLegend);
            this.Controls.Add(panelControls);

            // ── CONFIGURE RICHTEXTBOX — dark console style ──
            richTextBoxDebug.Dock = DockStyle.None;
            richTextBoxDebug.Location = new Point(0, panelControls.Height);
            richTextBoxDebug.Width = this.ClientSize.Width;
            richTextBoxDebug.Height = this.ClientSize.Height - panelControls.Height;
            richTextBoxDebug.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            richTextBoxDebug.BackColor = BG_COLOR;
            richTextBoxDebug.ForeColor = FG_DEFAULT;
            richTextBoxDebug.Font = new Font("Consolas", 9.5f);
            richTextBoxDebug.BorderStyle = BorderStyle.None;
            richTextBoxDebug.ReadOnly = true;
            richTextBoxDebug.WordWrap = false;
        }

        /// <summary>
        /// Appends a message to the debug log with automatic category detection and color coding.
        /// Thread-safe: marshals to UI thread via BeginInvoke if needed.
        /// </summary>
        public void AppendMessage(string message)
        {
            // Debug window logging is intentionally disabled in runtime builds.
        }

        /// <summary>
        /// Detects the log category by analyzing keywords in the message.
        /// Priority order: ERROR > WARNING > SUCCESS > FIREBASE > NETWORK > TIMER > UI > STARTUP > INFO > DEBUG
        /// </summary>
        private string DetectCategory(string message)
        {
            string upper = message.ToUpperInvariant();

            // ── ERROR: Exceptions, failures, crashes ──
            if (upper.Contains("ERROR") || upper.Contains("❌") || upper.Contains("EXCEPTION") ||
                upper.Contains("CRASH") || upper.Contains("FAILED") || upper.Contains("FAILURE"))
                return "ERROR";

            // ── WARNING: Retries, fallbacks, unexpected states ──
            if (upper.Contains("WARNING") || upper.Contains("⚠") || upper.Contains("WARN") ||
                upper.Contains("RETRY") || upper.Contains("FALLBACK") || upper.Contains("TIMEOUT") ||
                upper.Contains("MISMATCH") || upper.Contains("SKIP"))
                return "WARNING";

            // ── SUCCESS: Completed operations ──
            if (upper.Contains("✅") || upper.Contains("SUCCESS") || upper.Contains("COMPLETE") ||
                upper.Contains("SAVED") || upper.Contains("DONE") || upper.Contains("CONNECTED"))
                return "SUCCESS";

            // ── FIREBASE: Database operations ──
            if (upper.Contains("FIREBASE") || upper.Contains("GETASYNC") || upper.Contains("POSTASYNC") ||
                upper.Contains("PUTASYNC") || upper.Contains("PATCHASYNC") || upper.Contains("DELETEASYNC") ||
                upper.Contains("[FIREBASE") || upper.Contains("FIREBASETRAFFIC"))
                return "FIREBASE";

            // ── NETWORK: HTTP, TCP, LAN, P2P ──
            if (upper.Contains("[LANSYNC") || upper.Contains("[NETWORK") || upper.Contains("TCP") ||
                upper.Contains("UDP") || upper.Contains("HTTP") || upper.Contains("PEER") ||
                upper.Contains("BROADCAST") || upper.Contains("SOCKET"))
                return "NETWORK";

            // ── TIMER: Heartbeats, ticks, intervals ──
            if (upper.Contains("TIMER") || upper.Contains("HEARTBEAT") || upper.Contains("TICK") ||
                upper.Contains("[POMODORO") || upper.Contains("[ALARM"))
                return "TIMER";

            // ── UI: Panel changes, theme, visibility ──
            if (upper.Contains("PANEL") || upper.Contains("THEME") || upper.Contains("VISIBLE") ||
                upper.Contains("HIDDEN") || upper.Contains("LAYOUT") || upper.Contains("RESIZE") ||
                upper.Contains("DOCK") || upper.Contains("APPLYTHEME"))
                return "UI";

            // ── STARTUP: Initialization, constructor, setup ──
            if (upper.Contains("INITIALIZ") || upper.Contains("CONSTRUCTOR") || upper.Contains("STARTUP") ||
                upper.Contains("SETUP") || upper.Contains("BUILDUI") || upper.Contains("CREATED") ||
                upper.Contains("WIRED UP") || upper.Contains("DEBUGLOGGER"))
                return "STARTUP";

            // ── INFO: General information (catch-all for tagged messages) ──
            if (upper.Contains("INFO") || upper.Contains("LOADING") || upper.Contains("LOADED") ||
                upper.Contains("STARTING") || upper.Contains("STARTED") || upper.Contains("CHECKING") ||
                upper.Contains("REFRESHING") || upper.Contains("SENDING") || upper.Contains("FETCHING"))
                return "INFO";

            // ── DEBUG: Everything else ──
            return "DEBUG";
        }

        /// <summary>
        /// Maps a category string to its display color.
        /// </summary>
        private Color GetCategoryColor(string category)
        {
            switch (category)
            {
                case "ERROR": return CLR_ERROR;
                case "WARNING": return CLR_WARNING;
                case "SUCCESS": return CLR_SUCCESS;
                case "INFO": return CLR_INFO;
                case "FIREBASE": return CLR_FIREBASE;
                case "NETWORK": return CLR_NETWORK;
                case "TIMER": return CLR_TIMER;
                case "UI": return CLR_UI;
                case "STARTUP": return CLR_STARTUP;
                case "DEBUG": return CLR_DEBUG;
                default: return FG_DEFAULT;
            }
        }

        /// <summary>
        /// Checks if a message passes the current search text and category filter.
        /// </summary>
        private bool PassesFilter(string text, string category)
        {
            // Category filter
            string selectedCategory = cmbCategory?.SelectedItem?.ToString() ?? "ALL";
            if (selectedCategory != "ALL" && category != selectedCategory)
                return false;

            // Text search filter
            string searchFilter = txtSearch?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(searchFilter) && text.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            return true;
        }

        /// <summary>
        /// Appends a single colored message line to the RichTextBox.
        /// </summary>
        private void AppendColoredMessage(string message, Color color)
        {
            richTextBoxDebug.SelectionStart = richTextBoxDebug.TextLength;
            richTextBoxDebug.SelectionLength = 0;
            richTextBoxDebug.SelectionColor = color;
            richTextBoxDebug.AppendText(message + "\r\n");
            richTextBoxDebug.SelectionColor = FG_DEFAULT;
        }

        /// <summary>
        /// Rebuilds the entire RichTextBox from the allMessages list,
        /// applying both search and category filters.
        /// </summary>
        private void RebuildRichTextBox()
        {
            richTextBoxDebug.Clear();

            foreach (var msg in allMessages)
            {
                if (PassesFilter(msg.Text, msg.Category))
                    AppendColoredMessage(msg.Text, msg.ForeColor);
            }

            // Scroll to bottom after rebuild
            if (richTextBoxDebug.TextLength > 0)
            {
                richTextBoxDebug.SelectionStart = richTextBoxDebug.TextLength;
                richTextBoxDebug.ScrollToCaret();
            }

            UpdateMessageCount();
        }

        /// <summary>
        /// Updates the message counter label showing total and visible counts.
        /// </summary>
        private void UpdateMessageCount()
        {
            if (lblCount == null) return;

            string selectedCategory = cmbCategory?.SelectedItem?.ToString() ?? "ALL";
            string searchText = txtSearch?.Text?.Trim() ?? "";

            if (selectedCategory == "ALL" && string.IsNullOrEmpty(searchText))
            {
                lblCount.Text = $"{allMessages.Count} messages";
            }
            else
            {
                int visible = allMessages.Count(m => PassesFilter(m.Text, m.Category));
                lblCount.Text = $"{visible}/{allMessages.Count} shown";
            }
        }

        // ── EVENT HANDLERS ──

        private void BtnClear_Click(object sender, EventArgs e)
        {
            allMessages.Clear();
            richTextBoxDebug.Clear();
            UpdateMessageCount();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            RebuildRichTextBox();
        }

        private void CmbCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            RebuildRichTextBox();
        }

        /// <summary>
        /// Required by designer — keep as empty handler.
        /// </summary>
        private void richTextBoxDebug_TextChanged(object sender, EventArgs e)
        {
        }
    }
}
