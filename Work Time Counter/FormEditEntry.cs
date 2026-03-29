// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        FormEditEntry.cs                                             ║
// ║  PURPOSE:     ADMIN EDIT DIALOG FOR TIME LOG ENTRIES                       ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    /// <summary>
    /// Dialog form for admins to edit a work time log entry.
    /// Allows editing: Description, Project, Start Time, Duration (Working Time), Status.
    /// Read-only display of: User, Date.
    /// Validates time format before saving.
    /// </summary>
    public class FormEditEntry : Form
    {
        // ─── PUBLIC PROPERTIES — EDITED VALUES AFTER VALIDATION ───
        /// <summary>Edited entry description</summary>
        public string EntryDescription { get; private set; }
        /// <summary>Edited project name</summary>
        public string EntryProject { get; private set; }
        /// <summary>Edited start time (HH:mm:ss format)</summary>
        public string EntryStartTime { get; private set; }
        /// <summary>Edited working time/duration (HH:mm:ss format)</summary>
        public string EntryWorkingTime { get; private set; }
        /// <summary>Edited status (Stopped/Working/Complete)</summary>
        public string EntryStatus { get; private set; }

        // ─── UI CONTROLS ───
        private TextBox txtDescription;              // Description input
        private TextBox txtProject;                  // Project name input
        private TextBox txtStartTime;                // Start time input
        private TextBox txtWorkingTime;              // Duration input
        private ComboBox cmbStatus;                  // Status dropdown
        private Label lblUser;                       // Read-only username display
        private Label lblDate;                       // Read-only date display
        private Button btnSave;                      // Save changes button
        private Button btnCancel;                    // Cancel/close button

        /// <summary>
        /// Initializes the edit entry dialog with current entry data.
        /// Displays read-only user and date, editable fields for the rest.
        /// </summary>
        public FormEditEntry(string userName, string description, string project,
                             string startTime, string workingTime, string status, string date)
        {
//             DebugLogger.Log("[EditEntry] Constructor called — user=" + userName + ", date=" + date);

            // ─── FORM SETTINGS ───
            this.Text = "✏ Edit Entry — " + userName;
            this.Width = 420;
            this.Height = 360;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 38, 50);
            this.ForeColor = Color.White;

            // ─── LAYOUT PARAMETERS ───
            int leftLabel = 16;        // X position of labels
            int leftField = 130;       // X position of input fields
            int fieldWidth = 250;      // Width of full-width fields
            int y = 15;                // Current Y position
            int rowHeight = 38;        // Vertical spacing between rows

            // ─── USER (READ-ONLY INFO) ───
            AddLabel("User:", leftLabel, y);
            lblUser = new Label
            {
                Text = userName,
                Left = leftField, Top = y, Width = fieldWidth,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 255)
            };
            this.Controls.Add(lblUser);
            y += rowHeight;
//             DebugLogger.Log("[EditEntry] Added user label: " + userName);

            // ─── DATE (READ-ONLY INFO) ───
            AddLabel("Date:", leftLabel, y);
            lblDate = new Label
            {
                Text = date,
                Left = leftField, Top = y, Width = fieldWidth,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(180, 190, 200)
            };
            this.Controls.Add(lblDate);
            y += rowHeight;

            // ─── DESCRIPTION (EDITABLE) ───
            AddLabel("Description:", leftLabel, y);
            txtDescription = CreateTextBox(leftField, y, fieldWidth, description);
            this.Controls.Add(txtDescription);
            y += rowHeight;

            // ─── PROJECT (EDITABLE) ───
            AddLabel("Project:", leftLabel, y);
            txtProject = CreateTextBox(leftField, y, fieldWidth, project);
            this.Controls.Add(txtProject);
            y += rowHeight;

            // ─── START TIME (EDITABLE) ───
            // Format: HH:mm:ss
            AddLabel("Start Time:", leftLabel, y);
            txtStartTime = CreateTextBox(leftField, y, 120, startTime);
            this.Controls.Add(txtStartTime);
            y += rowHeight;

            // ─── DURATION/WORKING TIME (EDITABLE) ───
            // Format: HH:mm:ss (e.g., "02:30:15" for 2h 30m 15s)
            AddLabel("Duration:", leftLabel, y);
            txtWorkingTime = CreateTextBox(leftField, y, 120, workingTime);
            this.Controls.Add(txtWorkingTime);
            y += rowHeight;

            // ─── STATUS (DROPDOWN) ───
            // Options: Stopped, Working, Complete
            AddLabel("Status:", leftLabel, y);
            cmbStatus = new ComboBox
            {
                Left = leftField, Top = y, Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 55, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            cmbStatus.Items.AddRange(new[] { "Stopped", "Working", "Complete" });
            cmbStatus.SelectedItem = status ?? "Stopped";
            if (cmbStatus.SelectedIndex < 0) cmbStatus.SelectedIndex = 0;
            this.Controls.Add(cmbStatus);
//             DebugLogger.Log("[EditEntry] Status set to: " + (cmbStatus.SelectedItem?.ToString() ?? "null"));
            y += rowHeight + 10;

            // ─── BUTTONS ───
            btnSave = new Button
            {
                Text = "💾  Save",
                Left = leftField, Top = y, Width = 100, Height = 32,
                BackColor = Color.FromArgb(46, 204, 113), // Green
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnCancel = new Button
            {
                Text = "Cancel",
                Left = leftField + 110, Top = y, Width = 80, Height = 32,
                BackColor = Color.FromArgb(60, 70, 85),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) =>
            {
//                 DebugLogger.Log("[EditEntry] Cancel button clicked");
                DialogResult = DialogResult.Cancel;
                Close();
            };
            this.Controls.Add(btnCancel);

            // ─── SET ACCEPT/CANCEL BUTTONS ───
            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;

//             DebugLogger.Log("[EditEntry] Constructor complete");
        }

        /// <summary>
        /// Validates all input fields and saves changes if valid.
        /// Validates time format (HH:mm:ss) for Start Time and Duration.
        /// Shows error messages via MessageBox if validation fails.
        /// </summary>
        private void BtnSave_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[EditEntry] Save button clicked");

            // ─── VALIDATE START TIME FORMAT ───
            string st = txtStartTime.Text.Trim();
            if (!string.IsNullOrEmpty(st) && !TimeSpan.TryParse(st, out _))
            {
//                 DebugLogger.Log("[EditEntry] Invalid start time: " + st);
                MessageBox.Show("Start Time must be in HH:mm:ss format.",
                    "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ─── VALIDATE WORKING TIME FORMAT ───
            string wt = txtWorkingTime.Text.Trim();
            if (!string.IsNullOrEmpty(wt) && !TimeSpan.TryParse(wt, out _))
            {
//                 DebugLogger.Log("[EditEntry] Invalid working time: " + wt);
                MessageBox.Show("Duration must be in HH:mm:ss format.",
                    "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ─── CAPTURE EDITED VALUES ───
            EntryDescription = txtDescription.Text.Trim();
            EntryProject = txtProject.Text.Trim();
            EntryStartTime = st;
            EntryWorkingTime = wt;
            EntryStatus = cmbStatus.SelectedItem?.ToString() ?? "Stopped";

//             DebugLogger.Log("[EditEntry] Values accepted — status=" + EntryStatus);

            // ─── CLOSE WITH OK RESULT ───
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Helper method to create a styled label control.
        /// Used for field labels in the dialog.
        /// </summary>
        private void AddLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Left = x, Top = y + 2, AutoSize = true,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(180, 190, 200)
            };
            this.Controls.Add(lbl);
        }

        /// <summary>
        /// Helper method to create a styled text box control.
        /// Used for all text input fields (Description, Project, times).
        /// </summary>
        private TextBox CreateTextBox(int x, int y, int width, string value)
        {
            return new TextBox
            {
                Left = x, Top = y, Width = width,
                Text = value ?? "",
                BackColor = Color.FromArgb(45, 55, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.FixedSingle
            };
        }
    }
}
