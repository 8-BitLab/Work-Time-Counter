// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        FormWorkingTimeInput.cs                                      ║
// ║  PURPOSE:     MANUAL WORK TIME ENTRY DIALOG                                ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    public partial class FormWorkingTimeInput : Form
    {
        /// <summary>Returns the validated working time string entered by user (hh:mm:ss format).</summary>
        public string WorkingTime { get; private set; }

        private TextBox textBoxWorkingTime;
        private Button buttonOK;
        private Button buttonCancel;
        private Label labelPrompt;

        /// <summary>
        /// Initializes the Working Time Input form with UI controls.
        /// Creates a dialog with:
        /// - Text box for time input (default "00:00:00")
        /// - OK button to validate and submit
        /// - Cancel button to dismiss
        /// </summary>
        public FormWorkingTimeInput()
        {
//             DebugLogger.Log("[WorkingTimeInput] Constructor: Initializing working time input form");

            this.Width = 360;
            this.Height = 170;
            this.Text = "Enter Working Time";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;

            // Create UI controls with layout positioning
            labelPrompt = new Label() { Left = 16, Top = 15, Width = 300, Text = "Enter working time (hh:mm:ss):" };
            textBoxWorkingTime = new TextBox() { Left = 16, Top = 40, Width = 200, Text = "00:00:00" };
            buttonOK = new Button() { Text = "OK", Left = 16, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            buttonCancel = new Button() { Text = "Cancel", Left = 110, Width = 80, Top = 80, DialogResult = DialogResult.Cancel };

            buttonOK.Click += buttonOK_Click;
            buttonCancel.Click += buttonCancel_Click;

            // Add all controls to form
            this.Controls.Add(labelPrompt);
            this.Controls.Add(textBoxWorkingTime);
            this.Controls.Add(buttonOK);
            this.Controls.Add(buttonCancel);

//             DebugLogger.Log("[WorkingTimeInput] Constructor: Form initialized with default time 00:00:00");
        }

        /// <summary>
        /// Handles OK button click.
        /// Validates the input time string and closes dialog if valid.
        /// Shows warning if format is incorrect.
        /// </summary>
        private void buttonOK_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[WorkingTimeInput] OK_Click: Validating user input");

            WorkingTime = textBoxWorkingTime.Text.Trim();
//             DebugLogger.Log($"[WorkingTimeInput] OK_Click: User entered: '{WorkingTime}'");

            // Attempt to parse as TimeSpan to validate format
            if (TimeSpan.TryParse(WorkingTime, out _))
            {
//                 DebugLogger.Log("[WorkingTimeInput] OK_Click: Input validation passed");
                DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
//                 DebugLogger.Log("[WorkingTimeInput] OK_Click: Invalid time format detected");
                MessageBox.Show("Please enter time in format hh:mm:ss", "Invalid input",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Handles Cancel button click.
        /// Closes the dialog without processing any input.
        /// </summary>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[WorkingTimeInput] Cancel_Click: User cancelled input");
            DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
