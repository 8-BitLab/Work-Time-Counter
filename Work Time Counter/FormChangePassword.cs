// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        FormChangePassword.cs                                        ║
// ║  PURPOSE:     PASSWORD CHANGE DIALOG FORM                                  ║
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
    /// Dialog form for changing a user password.
    /// Modal dialog with new password and confirmation fields.
    /// Validates password requirements and displays errors inline.
    /// </summary>
    public class FormChangePassword : Form
    {
        // ─── UI CONTROLS ───
        private TextBox textBoxNewPassword;          // Password input field (masked)
        private TextBox textBoxConfirmPassword;      // Confirmation field (masked)
        private Button buttonUpdate;                  // Apply changes button
        private Button buttonCancel;                  // Close without saving
        private Label labelTitle;                     // Form title
        private Label labelSubtitle;                  // Subtitle with username
        private Label labelNewPassword;               // "NEW PASSWORD" label
        private Label labelConfirmPassword;           // "CONFIRM PASSWORD" label
        private Label labelError;                     // Error message display

        /// <summary>The new password entered by user (set after validation)</summary>
        public string NewPassword { get; private set; }

        /// <summary>
        /// Initializes the password change dialog.
        /// Displays the username and sets up validation UI.
        /// </summary>
        public FormChangePassword(string currentUserName)
        {
//             DebugLogger.Log("[ChangePassword] Constructor called — user=" + currentUserName);

            // ─── FORM SETTINGS ───
            this.Text = "Change Password";
            this.Width = 440;
            this.Height = 400;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(24, 28, 36);
            this.ForeColor = Color.FromArgb(220, 224, 230);

            // ─── TITLE LABEL (WITH EMOJI) ───
            labelTitle = new Label
            {
                Text = "🔑 Change Password",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 127, 80),
                Location = new Point(20, 20),
                AutoSize = true
            };
            this.Controls.Add(labelTitle);

            // ─── SUBTITLE (USERNAME) ───
            labelSubtitle = new Label
            {
                Text = $"Enter a new password for user: {currentUserName}",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(120, 130, 145),
                Location = new Point(22, 54),
                Size = new Size(380, 40)
            };
            this.Controls.Add(labelSubtitle);

            // ─── NEW PASSWORD LABEL ───
            labelNewPassword = new Label
            {
                Text = "NEW PASSWORD",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 130, 145),
                Location = new Point(20, 105),
                AutoSize = true
            };
            this.Controls.Add(labelNewPassword);

            // ─── NEW PASSWORD TEXTBOX (MASKED) ───
            textBoxNewPassword = new TextBox
            {
                Location = new Point(20, 128),
                Size = new Size(380, 32),
                Font = new Font("Segoe UI", 12),
                BackColor = Color.FromArgb(30, 36, 46),
                ForeColor = Color.FromArgb(220, 224, 230),
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true // Show as dots
            };
            this.Controls.Add(textBoxNewPassword);

            // ─── CONFIRM PASSWORD LABEL ───
            labelConfirmPassword = new Label
            {
                Text = "CONFIRM PASSWORD",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 130, 145),
                Location = new Point(20, 175),
                AutoSize = true
            };
            this.Controls.Add(labelConfirmPassword);

            // ─── CONFIRM PASSWORD TEXTBOX (MASKED) ───
            textBoxConfirmPassword = new TextBox
            {
                Location = new Point(20, 198),
                Size = new Size(380, 32),
                Font = new Font("Segoe UI", 12),
                BackColor = Color.FromArgb(30, 36, 46),
                ForeColor = Color.FromArgb(220, 224, 230),
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true // Show as dots
            };
            // Allow Enter key to submit form
            textBoxConfirmPassword.KeyPress += (s, e) =>
            {
                if (e.KeyChar == (char)Keys.Enter)
                {
//                     DebugLogger.Log("[ChangePassword] Enter pressed in confirm field");
                    e.Handled = true;
                    DoUpdate();
                }
            };
            this.Controls.Add(textBoxConfirmPassword);

            // ─── ERROR MESSAGE LABEL (INITIALLY HIDDEN) ───
            labelError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 53, 69), // Red color for errors
                Location = new Point(20, 245),
                Size = new Size(380, 40),
                Visible = false
            };
            this.Controls.Add(labelError);

            // ─── UPDATE BUTTON ───
            buttonUpdate = new Button
            {
                Text = "🔑  Update",
                Location = new Point(20, 295),
                Size = new Size(180, 44),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 127, 80), // Orange accent
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            buttonUpdate.FlatAppearance.BorderSize = 0;
            buttonUpdate.Click += (s, e) => DoUpdate();
            this.Controls.Add(buttonUpdate);

            // ─── CANCEL BUTTON ───
            buttonCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(220, 295),
                Size = new Size(180, 44),
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 44, 56),
                ForeColor = Color.FromArgb(160, 170, 180),
                Cursor = Cursors.Hand
            };
            buttonCancel.FlatAppearance.BorderSize = 0;
            buttonCancel.Click += (s, e) =>
            {
//                 DebugLogger.Log("[ChangePassword] Cancel button clicked");
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(buttonCancel);

            // ─── AUTO-FOCUS NEW PASSWORD FIELD WHEN FORM IS SHOWN ───
            this.Shown += (s, e) =>
            {
//                 DebugLogger.Log("[ChangePassword] Form shown — focusing on password field");
                textBoxNewPassword.Focus();
            };

//             DebugLogger.Log("[ChangePassword] Constructor complete");
        }

        /// <summary>
        /// Validates password fields and applies the change if valid.
        /// Validation rules:
        /// - Password cannot be empty
        /// - Password must be at least 6 characters
        /// - Passwords must match
        /// </summary>
        private void DoUpdate()
        {
            string newPass = textBoxNewPassword.Text;
            string confirmPass = textBoxConfirmPassword.Text;

//             DebugLogger.Log("[ChangePassword] DoUpdate called");

            // ─── VALIDATION: NOT EMPTY ───
            if (string.IsNullOrEmpty(newPass))
            {
                DebugLogger.Log("[ChangePassword] Validation failed — password is empty");
                ShowError("❌ Password cannot be empty");
                return;
            }

            // ─── VALIDATION: MINIMUM LENGTH ───
            if (newPass.Length < 6)
            {
                DebugLogger.Log("[ChangePassword] Validation failed — password too short (length=" + newPass.Length + ")");
                ShowError("❌ Password must be at least 6 characters");
                return;
            }

            // ─── VALIDATION: PASSWORDS MATCH ───
            if (newPass != confirmPass)
            {
                DebugLogger.Log("[ChangePassword] Validation failed — passwords do not match");
                ShowError("❌ Passwords do not match");
                textBoxConfirmPassword.Clear();
                textBoxConfirmPassword.Focus();
                return;
            }

            // ─── SUCCESS: APPLY CHANGE ───
//             DebugLogger.Log("[ChangePassword] Validation passed — password accepted");
            NewPassword = newPass;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Displays an error message in the error label and makes it visible.
        /// </summary>
        private void ShowError(string message)
        {
            DebugLogger.Log("[ChangePassword] ShowError: " + message);
            labelError.Text = message;
            labelError.Visible = true;
        }
    }
}
