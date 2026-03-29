// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        SyncAcceptanceForm.cs                                        ║
// ║  PURPOSE:     USER APPROVAL DIALOG FOR INCOMING FIREBASE SYNC CHANGES      ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  When Firebase sync detects significant changes (edits to YOUR logs,       ║
// ║  deleted entries, bulk changes), this form pops up so the user can         ║
// ║  review and accept/reject the changes before they overwrite local data.    ║
// ║  Small routine changes (new entries from other users, heartbeats)          ║
// ║  are auto-accepted silently.                                               ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SYNC CHANGE ITEM — describes a single change detected from Firebase
    // ═══════════════════════════════════════════════════════════════════════════
    public class SyncChangeItem
    {
        /// <summary>Type of change: "Modified", "Deleted", "New"</summary>
        public string ChangeType { get; set; }

        /// <summary>Which data category: "WorkLog", "Chat", "TeamMember", "Settings"</summary>
        public string Category { get; set; }

        /// <summary>Human-readable summary of the change.</summary>
        public string Description { get; set; }

        /// <summary>The Firebase key of the affected record (for log merging).</summary>
        public string RecordKey { get; set; }

        /// <summary>Who made the change (username), if known.</summary>
        public string ChangedBy { get; set; }

        /// <summary>When the change was detected (UTC).</summary>
        public DateTime DetectedUtc { get; set; } = DateTime.UtcNow;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SYNC CHANGE DETECTOR — compares local vs Firebase and finds differences
    // ═══════════════════════════════════════════════════════════════════════════
    public static class SyncChangeDetector
    {
        /// <summary>
        /// Compares local logs with incoming Firebase logs.
        /// Returns a list of significant changes that need user attention.
        /// Routine additions by OTHER users are auto-accepted (not listed).
        /// </summary>
        public static List<SyncChangeItem> DetectLogChanges(
            Dictionary<string, LogEntry> localLogs,
            Dictionary<string, LogEntry> firebaseLogs,
            string currentUserName)
        {
            var changes = new List<SyncChangeItem>();
            if (localLogs == null) localLogs = new Dictionary<string, LogEntry>();
            if (firebaseLogs == null) return changes;

            // 1. Detect MODIFIED entries (same key, different data — especially YOUR logs)
            foreach (var kvp in firebaseLogs)
            {
                if (!localLogs.ContainsKey(kvp.Key)) continue;

                var local = localLogs[kvp.Key];
                var remote = kvp.Value;

                // Only flag if it's YOUR log and it was changed
                bool isMyLog = string.Equals(local.userName, currentUserName, StringComparison.OrdinalIgnoreCase);
                if (!isMyLog) continue;

                bool isModified =
                    local.status != remote.status ||
                    local.workingTime != remote.workingTime ||
                    local.description != remote.description ||
                    local.startTime != remote.startTime;

                if (isModified)
                {
                    changes.Add(new SyncChangeItem
                    {
                        ChangeType = "Modified",
                        Category = "WorkLog",
                        RecordKey = kvp.Key,
                        ChangedBy = remote.userName ?? "Unknown",
                        Description = $"Your log '{local.description ?? "untitled"}' was modified remotely. " +
                                      $"Status: {local.status} → {remote.status}, " +
                                      $"Time: {local.workingTime ?? "?"} → {remote.workingTime ?? "?"}"
                    });
                }
            }

            // 2. Detect DELETED entries (in local but not in Firebase — YOUR logs only)
            foreach (var kvp in localLogs)
            {
                if (firebaseLogs.ContainsKey(kvp.Key)) continue;

                bool isMyLog = string.Equals(kvp.Value.userName, currentUserName, StringComparison.OrdinalIgnoreCase);
                if (!isMyLog) continue;

                // Only flag if key looks like a Firebase key (not local-only)
                if (kvp.Key.StartsWith("LOCAL_", StringComparison.OrdinalIgnoreCase)) continue;

                changes.Add(new SyncChangeItem
                {
                    ChangeType = "Deleted",
                    Category = "WorkLog",
                    RecordKey = kvp.Key,
                    Description = $"Your log '{kvp.Value.description ?? "untitled"}' ({kvp.Value.timestamp}) " +
                                  $"was deleted from Firebase."
                });
            }

            return changes;
        }

        /// <summary>
        /// Quick check: are there changes significant enough to show the acceptance dialog?
        /// Returns false for routine sync (only new entries from other users).
        /// </summary>
        public static bool HasSignificantChanges(List<SyncChangeItem> changes)
        {
            return changes != null && changes.Count > 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SYNC ACCEPTANCE FORM — WinForms dialog for reviewing changes
    // ═══════════════════════════════════════════════════════════════════════════
    public class SyncAcceptanceForm : Form
    {
        private ListView _listView;
        private Button _btnAcceptAll;
        private Button _btnRejectAll;
        private Button _btnAcceptSelected;
        private Label _lblSummary;
        private Label _lblInfo;

        /// <summary>After the dialog closes, this contains the keys the user ACCEPTED.</summary>
        public HashSet<string> AcceptedKeys { get; private set; } = new HashSet<string>();

        /// <summary>If true, user clicked "Accept All".</summary>
        public bool AcceptedAll { get; private set; }

        /// <summary>If true, user clicked "Reject All" (keep local data).</summary>
        public bool RejectedAll { get; private set; }

        private List<SyncChangeItem> _changes;

        public SyncAcceptanceForm(List<SyncChangeItem> changes)
        {
            _changes = changes ?? new List<SyncChangeItem>();
            InitializeUI();
            PopulateList();
        }

        private void InitializeUI()
        {
            this.Text = "Sync Review — Incoming Changes";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            bool isDark = true;
            try
            {
                var custom = CustomTheme.LoadActive();
                if (custom != null && custom.Enabled)
                {
                    // Infer light/dark by configured background luminance.
                    var bg = custom.GetBackground();
                    int lum = (bg.R * 299 + bg.G * 587 + bg.B * 114) / 1000;
                    isDark = lum < 140;
                }
            }
            catch { }
            this.BackColor = isDark ? Color.FromArgb(30, 30, 30) : Color.White;
            this.ForeColor = isDark ? Color.FromArgb(220, 220, 220) : Color.Black;

            // Info label
            _lblInfo = new Label
            {
                Text = "Firebase sync detected changes to YOUR work logs.\n" +
                       "Review below and choose to accept or keep your local version.",
                AutoSize = false,
                Size = new Size(660, 40),
                Location = new Point(15, 10),
                ForeColor = this.ForeColor
            };
            this.Controls.Add(_lblInfo);

            // Summary label
            _lblSummary = new Label
            {
                AutoSize = false,
                Size = new Size(660, 20),
                Location = new Point(15, 55),
                ForeColor = isDark ? Color.FromArgb(120, 180, 255) : Color.DarkBlue,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            this.Controls.Add(_lblSummary);

            // ListView
            _listView = new ListView
            {
                Location = new Point(15, 80),
                Size = new Size(660, 310),
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                GridLines = true,
                BackColor = isDark ? Color.FromArgb(45, 45, 45) : Color.White,
                ForeColor = this.ForeColor
            };
            _listView.Columns.Add("Type", 80);
            _listView.Columns.Add("Category", 80);
            _listView.Columns.Add("Description", 400);
            _listView.Columns.Add("By", 80);
            this.Controls.Add(_listView);

            int btnY = 400;
            Color btnColor = isDark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(230, 230, 230);

            _btnAcceptAll = new Button
            {
                Text = "✓  Accept All",
                Size = new Size(130, 35),
                Location = new Point(15, btnY),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 140, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            _btnAcceptAll.Click += (s, e) =>
            {
                AcceptedAll = true;
                foreach (var c in _changes)
                    AcceptedKeys.Add(c.RecordKey);
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(_btnAcceptAll);

            _btnAcceptSelected = new Button
            {
                Text = "Accept Selected",
                Size = new Size(130, 35),
                Location = new Point(155, btnY),
                FlatStyle = FlatStyle.Flat,
                BackColor = btnColor,
                ForeColor = this.ForeColor,
                Font = new Font("Segoe UI", 9)
            };
            _btnAcceptSelected.Click += (s, e) =>
            {
                foreach (ListViewItem item in _listView.CheckedItems)
                {
                    if (item.Tag is SyncChangeItem change)
                        AcceptedKeys.Add(change.RecordKey);
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(_btnAcceptSelected);

            _btnRejectAll = new Button
            {
                Text = "✗  Keep Local",
                Size = new Size(130, 35),
                Location = new Point(545, btnY),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            _btnRejectAll.Click += (s, e) =>
            {
                RejectedAll = true;
                AcceptedKeys.Clear();
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(_btnRejectAll);
        }

        private void PopulateList()
        {
            int modified = 0, deleted = 0;

            foreach (var change in _changes)
            {
                var item = new ListViewItem(change.ChangeType);
                item.SubItems.Add(change.Category);
                item.SubItems.Add(change.Description);
                item.SubItems.Add(change.ChangedBy ?? "");
                item.Tag = change;
                item.Checked = true; // Default: accept

                // Color-code by type
                if (change.ChangeType == "Deleted")
                {
                    item.ForeColor = Color.FromArgb(220, 80, 80);
                    deleted++;
                }
                else if (change.ChangeType == "Modified")
                {
                    item.ForeColor = Color.FromArgb(220, 180, 50);
                    modified++;
                }

                _listView.Items.Add(item);
            }

            _lblSummary.Text = $"{_changes.Count} change(s) detected: " +
                               $"{modified} modified, {deleted} deleted from Firebase";
        }
    }
}
