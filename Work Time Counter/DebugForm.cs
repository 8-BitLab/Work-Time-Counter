using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    public partial class DebugForm : Form
    {
        public DebugForm()
        {
            InitializeComponent();
            this.Text = "Debug Log";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        public void AppendMessage(string message)
        {
            richTextBoxDebug.AppendText(message);
            richTextBoxDebug.ScrollToCaret();
        }

        private void richTextBoxDebug_TextChanged(object sender, EventArgs e)
        {
        }
    }
}