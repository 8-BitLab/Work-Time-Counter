// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        DirectMessageForm.cs                                         ║
// ║  PURPOSE:     PRIVATE DIRECT MESSAGING — WHATSAPP-STYLE CHAT BUBBLES      ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  WhatsApp-style private 1-on-1 chat between two users with colored         ║
// ║  chat bubbles. Your messages on the right in your team color, their        ║
// ║  messages on the left in their team color.                                 ║
// ║                                                                            ║
// ║  CONVERSATION KEY:                                                         ║
// ║    Names are sorted alphabetically and joined with "_"                     ║
// ║    Example: Alice chatting with Bob = "Alice_Bob" (always the same)        ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Windows.Forms;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Work_Time_Counter
{
    // ════════════════════════════════════════════════════════════════════════════════
    // CLASS: DirectMessageForm — WHATSAPP-STYLE PRIVATE CHAT
    // ════════════════════════════════════════════════════════════════════════════════

    public partial class DirectMessageForm : Form
    {
        // ── WIN32 FLASH TASKBAR ICON ──
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 3;      // Flash both caption and taskbar
        private const uint FLASHW_TIMERNOFG = 12; // Flash until window gets focus

        private static readonly HttpClient client = new HttpClient();

        // CONFIGURATION
        private string firebaseBaseUrl;
        private string currentUser;
        private string otherUser;
        private bool isDarkMode;
        private string conversationId;

        // USER COLORS — from team settings
        private Color myColor;
        private Color otherColor;
        private Image _myAvatarImage;
        private string _myAvatarBase64Cache = string.Empty;
        private Image _otherAvatarImage;
        private string _otherAvatarBase64Cache = string.Empty;
        private DateTime _lastAvatarMetaCheckUtc = DateTime.MinValue;

        // UI CONTROLS
        private Panel chatScrollPanel;       // Scrollable container for bubbles
        private FlowLayoutPanel bubbleFlow;  // Flow layout for chat bubbles
        private EmojiRichTextBox messageInput; // Color emoji input (RichEdit50W)
        private Button sendButton;
        private Control emojiButton;         // Emoji picker panel (color emoji via EmojiRichTextBox)
        private Button attachButton;         // File attachment button (📎)
        private Panel inputPanel;
        private Panel headerPanel;
        private Panel headerAvatarPanel;

        // FILE TRANSFER BAR — animated progress indicator
        private Panel _transferBarPanel;
        private Panel _transferProgressFill;
        private Label _transferLabel;
        private Timer _transferAnimTimer;
        private int _transferAnimStep = 0;
        private bool _transferIsDownload = false;

        // FONT SIZE — Small/Medium/Big (matches team chat)
        private float _dmFontSize = 10.5f;  // Default: Small
        private string _dmFontSizeName = "Small";

        // EMOJI SET — 24 common emojis for the picker popup
        private static readonly string[] EmojiSet = new[]
        {
            "😀", "😂", "😍", "🥰", "😎", "😢", "😡", "🤔",
            "👍", "👎", "❤️", "🔥", "🎉", "💯", "🙏", "💪",
            "✅", "❌", "👋", "🤣", "😱", "💕", "🙌", "🎊"
        };

        // REFRESH
        private Timer refreshTimer;
        private List<DirectMessage> currentMessages = new List<DirectMessage>();
        private Dictionary<DirectMessage, string> _messageFirebaseKeys = new Dictionary<DirectMessage, string>();
        private int lastMessageCount = 0;
        private string _lastReadHash = "";  // Track read-status changes for display refresh
        private readonly Dictionary<string, Image> _imagePreviewCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _imagePreviewLoading = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ════════════════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════════════

        public DirectMessageForm(string firebaseBaseUrl, string currentUser, string otherUser, bool isDarkMode)
        {
            // Use GLOBAL Firebase URL for DMs so they work across all projects
            this.firebaseBaseUrl = UserStorage.GetGlobalFirebaseUrl();
            this.currentUser = currentUser;
            this.otherUser = otherUser;
            this.isDarkMode = isDarkMode;

            // GENERATE CONVERSATION KEY
            var names = new[] { currentUser, otherUser };
            Array.Sort(names);
            this.conversationId = string.Join("_", names);

            // LOAD USER COLORS FROM TEAM
            LoadUserColors();

            InitializeComponent();
            SetupUI();
            ColorEmojiCache.PreloadAsync(); // Pre-download color emoji PNGs
            SetupRefreshTimer();
            _ = LoadMessages();
            this.FormClosed += (s, e) =>
            {
                if (_myAvatarImage != null)
                {
                    _myAvatarImage.Dispose();
                    _myAvatarImage = null;
                }
                if (_otherAvatarImage != null)
                {
                    _otherAvatarImage.Dispose();
                    _otherAvatarImage = null;
                }
                foreach (var img in _imagePreviewCache.Values)
                {
                    img?.Dispose();
                }
                _imagePreviewCache.Clear();
                _imagePreviewLoading.Clear();
            };
        }

        // ════════════════════════════════════════════════════════════════════════════
        // LOAD USER COLORS — get team-assigned colors for both users
        // ════════════════════════════════════════════════════════════════════════════

        private void LoadUserColors()
        {
            // Default colors if no team color set
            myColor = Color.FromArgb(0, 132, 255);      // Blue (like Messenger)
            otherColor = Color.FromArgb(255, 127, 80);   // Coral orange

            try
            {
                var users = UserStorage.LoadUsers();
                if (users != null)
                {
                    var me = users.Find(u => string.Equals(u.Name, currentUser, StringComparison.OrdinalIgnoreCase));
                    var them = users.Find(u => string.Equals(u.Name, otherUser, StringComparison.OrdinalIgnoreCase));

                    if (me != null)
                        myColor = me.GetDrawingColor(myColor);
                    if (them != null)
                        otherColor = them.GetDrawingColor(otherColor);
                }
            }
            catch { }

            RefreshOtherAvatarFromTeamMeta(true);
        }

        private void RefreshOtherAvatarFromTeamMeta(bool force = false)
        {
            if (string.IsNullOrWhiteSpace(otherUser) && string.IsNullOrWhiteSpace(currentUser))
                return;

            if (!force && (DateTime.UtcNow - _lastAvatarMetaCheckUtc).TotalSeconds < 10)
                return;

            _lastAvatarMetaCheckUtc = DateTime.UtcNow;

            string otherAvatarBase64 = string.Empty;
            string myAvatarBase64 = string.Empty;
            try
            {
                var team = UserStorage.LoadTeam();
                if (team?.MembersMeta != null && team.MembersMeta.TryGetValue(otherUser, out var meta))
                {
                    otherAvatarBase64 = meta?.AvatarBase64 ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(meta?.Color))
                    {
                        try { otherColor = ColorTranslator.FromHtml(meta.Color); } catch { }
                    }
                }
                if (team?.MembersMeta != null && team.MembersMeta.TryGetValue(currentUser, out var myMeta))
                {
                    myAvatarBase64 = myMeta?.AvatarBase64 ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(myMeta?.Color))
                    {
                        try { myColor = ColorTranslator.FromHtml(myMeta.Color); } catch { }
                    }
                }
            }
            catch
            {
            }

            bool mySame = string.Equals(myAvatarBase64 ?? string.Empty, _myAvatarBase64Cache, StringComparison.Ordinal);
            bool otherSame = string.Equals(otherAvatarBase64 ?? string.Empty, _otherAvatarBase64Cache, StringComparison.Ordinal);
            if (mySame && otherSame)
            {
                headerAvatarPanel?.Invalidate();
                return;
            }

            if (!mySame)
            {
                _myAvatarBase64Cache = myAvatarBase64 ?? string.Empty;
                var oldMy = _myAvatarImage;
                _myAvatarImage = DecodeAvatarImage(_myAvatarBase64Cache);
                oldMy?.Dispose();
            }

            if (!otherSame)
            {
                _otherAvatarBase64Cache = otherAvatarBase64 ?? string.Empty;
                var oldOther = _otherAvatarImage;
                _otherAvatarImage = DecodeAvatarImage(_otherAvatarBase64Cache);
                oldOther?.Dispose();
            }

            headerAvatarPanel?.Invalidate();
        }

        private static Image DecodeAvatarImage(string avatarBase64)
        {
            if (string.IsNullOrWhiteSpace(avatarBase64))
                return null;

            try
            {
                byte[] bytes = Convert.FromBase64String(avatarBase64);
                using (var ms = new MemoryStream(bytes))
                using (var raw = Image.FromStream(ms))
                {
                    return new Bitmap(raw);
                }
            }
            catch
            {
                return null;
            }
        }

        private Image GetAvatarImageForUser(string userName)
        {
            return string.Equals(userName, currentUser, StringComparison.OrdinalIgnoreCase)
                ? _myAvatarImage
                : _otherAvatarImage;
        }

        private Color GetAvatarColorForUser(string userName)
        {
            return string.Equals(userName, currentUser, StringComparison.OrdinalIgnoreCase)
                ? myColor
                : otherColor;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // INITIALIZE COMPONENT — Form properties
        // ════════════════════════════════════════════════════════════════════════════

        private void InitializeComponent()
        {
            this.Text = $"\U0001F4AC Chat with {otherUser}";
            this.Size = new Size(500, 650);
            this.MinimumSize = new Size(380, 450);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = null;
            this.BackColor = isDarkMode ? Color.FromArgb(14, 20, 28) : Color.FromArgb(229, 221, 213);
            this.ForeColor = isDarkMode ? Color.White : Color.Black;
            this.DoubleBuffered = true;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // SETUP UI — WhatsApp-style layout
        // ════════════════════════════════════════════════════════════════════════════

        private void SetupUI()
        {
            // ── HEADER BAR — WhatsApp-style teal/dark header ──
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = isDarkMode ? Color.FromArgb(31, 41, 55) : Color.FromArgb(0, 128, 105),
                Padding = new Padding(12, 0, 12, 0)
            };

            // Avatar circle with other user's color
            headerAvatarPanel = new Panel
            {
                Size = new Size(42, 42),
                Location = new Point(14, 9),
                BackColor = Color.Transparent
            };
            headerAvatarPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                if (_otherAvatarImage != null)
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(0, 0, 40, 40);
                        var oldClip = e.Graphics.Clip;
                        e.Graphics.SetClip(path);
                        e.Graphics.DrawImage(_otherAvatarImage, new Rectangle(0, 0, 40, 40));
                        e.Graphics.Clip = oldClip;
                    }

                    using (var pen = new Pen(Color.FromArgb(130, 255, 255, 255), 1f))
                        e.Graphics.DrawEllipse(pen, 0, 0, 40, 40);
                }
                else
                {
                    using (var brush = new SolidBrush(otherColor))
                        e.Graphics.FillEllipse(brush, 0, 0, 40, 40);

                    // Draw initials
                    string initials = otherUser.Length > 0 ? otherUser[0].ToString().ToUpper() : "?";
                    using (var font = new Font("Segoe UI", 15, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.White))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        e.Graphics.DrawString(initials, font, brush, new RectangleF(0, 0, 40, 40), sf);
                    }
                }
            };
            headerPanel.Controls.Add(headerAvatarPanel);

            // Online status dot on avatar
            var headerStatusDot = new Panel
            {
                Size = new Size(12, 12),
                Location = new Point(44, 40),
                BackColor = Color.Transparent
            };
            headerStatusDot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.FromArgb(37, 211, 102)))
                    e.Graphics.FillEllipse(brush, 0, 0, 10, 10);
                using (var pen = new Pen(isDarkMode ? Color.FromArgb(31, 41, 55) : Color.FromArgb(0, 128, 105), 2))
                    e.Graphics.DrawEllipse(pen, 0, 0, 10, 10);
            };
            headerPanel.Controls.Add(headerStatusDot);

            var lblName = new Label
            {
                Text = otherUser,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(66, 8),
                AutoSize = true
            };
            headerPanel.Controls.Add(lblName);

            var lblStatus = new Label
            {
                Text = "Private chat",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(200, 235, 215),
                Location = new Point(68, 32),
                AutoSize = true
            };
            headerPanel.Controls.Add(lblStatus);

            // FONT SIZE BUTTON — cycles Small → Medium → Big
            var btnFontSize = new Button
            {
                Text = "A",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Size = new Size(36, 36),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(headerPanel.Width - 50, 12)
            };
            btnFontSize.FlatAppearance.BorderSize = 1;
            btnFontSize.FlatAppearance.BorderColor = Color.FromArgb(100, 255, 255, 255);
            btnFontSize.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 255, 255, 255);

            // Create popup menu for font sizes
            var fontMenu = new ContextMenuStrip();
            fontMenu.BackColor = isDarkMode ? Color.FromArgb(40, 50, 65) : Color.White;
            fontMenu.ForeColor = isDarkMode ? Color.White : Color.Black;

            var itemSmall = new ToolStripMenuItem("Small") { Font = new Font("Segoe UI", 9) };
            var itemMedium = new ToolStripMenuItem("Medium") { Font = new Font("Segoe UI", 11) };
            var itemBig = new ToolStripMenuItem("Big") { Font = new Font("Segoe UI", 13) };

            itemSmall.Click += (s, e) => SetDmFontSize("Small", btnFontSize);
            itemMedium.Click += (s, e) => SetDmFontSize("Medium", btnFontSize);
            itemBig.Click += (s, e) => SetDmFontSize("Big", btnFontSize);

            fontMenu.Items.Add(itemSmall);
            fontMenu.Items.Add(itemMedium);
            fontMenu.Items.Add(itemBig);

            btnFontSize.Click += (s, e) =>
            {
                fontMenu.Show(btnFontSize, new Point(0, btnFontSize.Height));
            };

            headerPanel.Controls.Add(btnFontSize);

            // Load saved DM font size preference
            string savedDmSize = UserStorage.GetDmFontSize(currentUser);
            if (savedDmSize != "Small") // Only apply if not default
            {
                SetDmFontSize(savedDmSize, btnFontSize);
            }

            // Keep font button right-aligned on resize
            headerPanel.Resize += (s, e) =>
            {
                btnFontSize.Location = new Point(headerPanel.Width - 50, 12);
            };

            // ── INPUT PANEL (BOTTOM) — WhatsApp-style with emoji + attach + send ──
            inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                BackColor = isDarkMode ? Color.FromArgb(31, 41, 55) : Color.FromArgb(240, 240, 240),
                Padding = new Padding(6, 6, 6, 6)
            };

            // EMOJI BUTTON — color emoji panel (RichEdit50W) opens emoji picker popup
            emojiButton = EmojiRichTextBox.CreateEmojiButton("\U0001F600", 38, 38, 16f,
                isDarkMode ? Color.FromArgb(31, 41, 55) : Color.FromArgb(240, 240, 240),
                isDarkMode ? Color.FromArgb(55, 65, 80) : Color.FromArgb(210, 210, 210),
                () => EmojiButton_Click(null, EventArgs.Empty));
            emojiButton.Location = new Point(6, 10);
            emojiButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

            // ATTACH FILE BUTTON — opens file dialog for file transfer
            attachButton = new Button
            {
                Text = "\U0001F4CE",
                Font = new Font("Segoe UI Emoji", 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = isDarkMode ? Color.FromArgb(210, 220, 235) : Color.FromArgb(70, 80, 95),
                Size = new Size(38, 38),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                Location = new Point(44, 10),
                TabStop = false
            };
            attachButton.FlatAppearance.BorderSize = 0;
            attachButton.FlatAppearance.MouseOverBackColor = isDarkMode ? Color.FromArgb(55, 65, 80) : Color.FromArgb(210, 210, 210);
            attachButton.Click += AttachButton_Click;

            // MESSAGE INPUT — EmojiRichTextBox (RichEdit50W) for full-color emoji in input
            messageInput = new EmojiRichTextBox
            {
                Multiline = true,
                Font = new Font("Segoe UI Emoji", 11),
                BackColor = isDarkMode ? Color.FromArgb(42, 52, 66) : Color.White,
                ForeColor = isDarkMode ? Color.White : Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = RichTextBoxScrollBars.None,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                Location = new Point(86, 10),
                Height = 38,
                ReadOnly = false
            };
            messageInput.KeyDown += MessageInput_KeyDown;

            // SEND BUTTON — WhatsApp green circle with arrow
            sendButton = new Button
            {
                Text = "➤",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 168, 132),
                ForeColor = Color.White,
                Size = new Size(44, 38),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            sendButton.FlatAppearance.BorderSize = 0;
            sendButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 140, 110);
            sendButton.Click += SendButton_Click;
            sendButton.KeyDown += MessageInput_KeyDown;

            inputPanel.Controls.Add(emojiButton);
            inputPanel.Controls.Add(attachButton);
            inputPanel.Controls.Add(messageInput);
            inputPanel.Controls.Add(sendButton);

            inputPanel.Resize += (s, e) =>
            {
                sendButton.Location = new Point(inputPanel.Width - 52, 10);
                messageInput.Width = inputPanel.Width - 144;
            };

            // ── FILE TRANSFER BAR — animated progress between input and chat ──
            _transferBarPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = isDarkMode ? Color.FromArgb(20, 28, 38) : Color.FromArgb(225, 230, 235),
                Visible = false, // Hidden by default, shown during transfer
                Padding = new Padding(4, 4, 4, 4)
            };

            // Progress fill bar (animated)
            _transferProgressFill = new Panel
            {
                Location = new Point(4, 4),
                Height = 24,
                Width = 0,
                BackColor = Color.FromArgb(0, 168, 132) // WhatsApp green
            };
            _transferProgressFill.Paint += (s, e) =>
            {
                // Draw gradient fill for visual appeal
                if (_transferProgressFill.Width > 2)
                {
                    using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                        _transferProgressFill.ClientRectangle,
                        _transferIsDownload ? Color.FromArgb(52, 152, 219) : Color.FromArgb(0, 168, 132),
                        _transferIsDownload ? Color.FromArgb(41, 128, 185) : Color.FromArgb(0, 200, 160),
                        System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                    {
                        e.Graphics.FillRectangle(brush, _transferProgressFill.ClientRectangle);
                    }
                }
            };

            // Transfer label (filename + percentage)
            _transferLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = isDarkMode ? Color.FromArgb(200, 215, 230) : Color.FromArgb(40, 40, 40),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Text = ""
            };
            _transferLabel.BringToFront();

            _transferBarPanel.Controls.Add(_transferLabel);
            _transferBarPanel.Controls.Add(_transferProgressFill);
            _transferLabel.BringToFront(); // Label on top of fill

            // Animation timer — smooth progress fill
            _transferAnimTimer = new Timer { Interval = 40 };
            _transferAnimTimer.Tick += (s, e) =>
            {
                _transferAnimStep += 2;
                if (_transferAnimStep > 90) _transferAnimStep = 90; // Cap at 90% until complete

                int maxWidth = _transferBarPanel.Width - 8;
                int targetWidth = (int)(maxWidth * (_transferAnimStep / 100.0));
                _transferProgressFill.Width = targetWidth;
                _transferProgressFill.Invalidate();
            };

            // ── CHAT AREA (CENTER) — WhatsApp wallpaper-style background ──
            chatScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = isDarkMode ? Color.FromArgb(14, 20, 28) : Color.FromArgb(229, 221, 213),
                Padding = new Padding(0)
            };

            bubbleFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                Dock = DockStyle.Top,
                Padding = new Padding(8, 8, 8, 8),
                BackColor = Color.Transparent
            };

            chatScrollPanel.Controls.Add(bubbleFlow);

            // ADD CONTROLS IN ORDER: header top, transfer bar + input bottom, chat fills center
            this.Controls.Add(chatScrollPanel);
            this.Controls.Add(_transferBarPanel); // Transfer bar sits above input
            this.Controls.Add(inputPanel);
            this.Controls.Add(headerPanel);

            // Resize bubbles when form resizes
            chatScrollPanel.Resize += (s, e) => ResizeBubbles();
        }

        // ════════════════════════════════════════════════════════════════════════════
        // EMOJI PICKER — WhatsApp-style emoji popup grid
        // ════════════════════════════════════════════════════════════════════════════

        private void EmojiButton_Click(object sender, EventArgs e)
        {
            // ╔ COLOR EMOJI PICKER — WebBrowser-based for full-color emoji rendering ╗
            // Uses HTML/IE11 engine which natively supports color emoji via Segoe UI Emoji
            string bgHex = isDarkMode ? "#1f2937" : "#ffffff";
            string hoverHex = isDarkMode ? "#374150" : "#e6e6e6";
            ColorEmojiPicker.Show(this, emojiButton, EmojiSet,
                bgHex, hoverHex, 330, 178,
                emoji =>
                {
                    messageInput.AppendText(emoji);
                    messageInput.SelectionStart = messageInput.TextLength;
                    messageInput.Focus();
                });
        }

        // ════════════════════════════════════════════════════════════════════════════
        // FILE TRANSFER — attach and send files via Firebase
        // ════════════════════════════════════════════════════════════════════════════

        private void AttachButton_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Select a file to send";
                ofd.Filter = "All Files (*.*)|*.*|Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Documents (*.pdf;*.doc;*.docx;*.txt)|*.pdf;*.doc;*.docx;*.txt";
                ofd.Multiselect = false;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string filePath = ofd.FileName;
                    string fileName = Path.GetFileName(filePath);
                    long fileSize = new FileInfo(filePath).Length;
                    string ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";

                    if (IsImageExtension(ext))
                    {
                        SendImageFromFileAsync(filePath, fileName);
                        return;
                    }

                    // Limit file size to 5 MB for Firebase base64 storage
                    if (fileSize > 5 * 1024 * 1024)
                    {
                        MessageBox.Show("File size must be under 5 MB for direct message transfer.",
                            "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Confirm send
                    var result = MessageBox.Show(
                        $"Send \"{fileName}\" ({FormatFileSize(fileSize)}) to {otherUser}?",
                        "Send File",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        SendFileAsync(filePath, fileName, fileSize);
                    }
                }
            }
        }

        private async void SendFileAsync(string filePath, string fileName, long fileSize)
        {
            try
            {

                // Show upload progress bar
                ShowTransferBar(fileName, isDownload: false);

                // Read file and convert to base64 for Firebase storage
                byte[] fileBytes = File.ReadAllBytes(filePath);
//                 DebugLogger.Log("[DirectMessage] File operation: File.ReadAllBytes");
                string base64Data = Convert.ToBase64String(fileBytes);
                string ext = Path.GetExtension(fileName).ToLower();

                // Build file message with metadata marker
                // Format: [FILE:{fileKey}:{filename}:{size}]
                // The fileKey is used to download the file from Firebase
                string fileKey = $"{conversationId}_{DateTime.UtcNow.Ticks}";
                string fileMessage = $"[FILE:{fileKey}:{fileName}:{FormatFileSize(fileSize)}]";

                var newMessage = new DirectMessage
                {
                    fromUser = currentUser,
                    toUser = otherUser,
                    message = fileMessage,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    read = false
                };

                // Store the file data in Firebase for download
                string fileUrl = $"{firebaseBaseUrl}/global_dm_files/{fileKey}.json";
                var filePayload = new
                {
                    fileName = fileName,
                    fileSize = fileSize,
                    base64Data = base64Data,
                    fromUser = currentUser,
                    toUser = otherUser,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    conversationId = conversationId
                };

                string fileJson = JsonConvert.SerializeObject(filePayload);
                var fileContent = new StringContent(fileJson, System.Text.Encoding.UTF8, "application/json");
                var uploadResponse = await client.PutAsync(fileUrl, fileContent);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.PutAsync");

                // Send the chat message referencing the file
                string url = $"{firebaseBaseUrl}/global_dm/{conversationId}.json";
                string json = JsonConvert.SerializeObject(newMessage);
                var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, httpContent);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.PostAsync");
                if (response.IsSuccessStatusCode)
                {
                    CompleteTransferBar(true);
                    lastMessageCount = 0;
                    await LoadMessages();
                }
                else
                {
                    CompleteTransferBar(false);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                CompleteTransferBar(false);
                MessageBox.Show("Could not send file. Please check your connection and try again.",
                    "File Transfer Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        // ════════════════════════════════════════════════════════════════════════════
        // FILE TRANSFER BAR — show/hide/complete animated progress
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Shows the transfer bar with animated progress fill.
        /// </summary>
        private void ShowTransferBar(string fileName, bool isDownload)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowTransferBar(fileName, isDownload)));
                return;
            }

            _transferIsDownload = isDownload;
            _transferAnimStep = 0;
            _transferProgressFill.Width = 0;
            _transferProgressFill.BackColor = isDownload ? Color.FromArgb(52, 152, 219) : Color.FromArgb(0, 168, 132);

            string icon = isDownload ? "\u2B07" : "\u2B06";
            string action = isDownload ? "Downloading" : "Uploading";
            _transferLabel.Text = $"{icon} {action}: {fileName}";

            _transferBarPanel.Visible = true;
            _transferAnimTimer.Start();
        }

        /// <summary>
        /// Completes the transfer bar — fills to 100% and hides after a short delay.
        /// </summary>
        private void CompleteTransferBar(bool success)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => CompleteTransferBar(success)));
                return;
            }

            _transferAnimTimer.Stop();
            _transferProgressFill.Width = _transferBarPanel.Width - 8;

            if (success)
            {
                _transferProgressFill.BackColor = Color.FromArgb(46, 204, 113); // Green success
                _transferLabel.Text = "\u2705 Transfer complete!";
            }
            else
            {
                _transferProgressFill.BackColor = Color.FromArgb(231, 76, 60); // Red error
                _transferLabel.Text = "\u274C Transfer failed";
            }
            _transferProgressFill.Invalidate();

            // Hide after 2.5 seconds
            var hideTimer = new Timer { Interval = 2500 };
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                hideTimer.Dispose();
                _transferBarPanel.Visible = false;
                _transferProgressFill.Width = 0;
                _transferAnimStep = 0;
            };
            hideTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════════════
        // FONT SIZE — Small / Medium / Big
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Changes the DM chat font size and rebuilds all bubbles.
        /// </summary>
        private void SetDmFontSize(string sizeName, Button btn)
        {
            _dmFontSizeName = sizeName;
            switch (sizeName)
            {
                case "Medium":
                    _dmFontSize = 12.5f;
                    btn.Text = "A";
                    break;
                case "Big":
                    _dmFontSize = 14.5f;
                    btn.Text = "A";
                    break;
                default: // "Small"
                    _dmFontSize = 10.5f;
                    btn.Text = "A";
                    break;
            }

            // Update button font to hint at current size
            btn.Font = new Font("Segoe UI", sizeName == "Big" ? 13 : sizeName == "Medium" ? 11 : 9, FontStyle.Bold);

            // Save DM font size preference for next time
            UserStorage.SaveDmFontSize(currentUser, sizeName);

            // Force rebuild all bubbles with new font size
            lastMessageCount = 0;
            DisplayMessages();
        }

        // ════════════════════════════════════════════════════════════════════════════
        // CREATE CHAT BUBBLE — WhatsApp-style rounded bubble
        // ════════════════════════════════════════════════════════════════════════════

        private Panel CreateBubble(DirectMessage msg)
        {
            bool isMe = string.Equals(msg.fromUser, currentUser, StringComparison.OrdinalIgnoreCase);
            Color bubbleColor = isMe ? myColor : otherColor;
            const int sidePadding = 4;
            const int avatarSize = 24;
            const int avatarGap = 8;

            // Make bubble lighter/darker depending on theme
            // Dark mode: vibrant, saturated colors. Light mode: soft tints.
            Color bgColor;
            if (isDarkMode)
            {
                if (isMe)
                {
                    // MY bubbles — warm orange/amber (like the queue button)
                    bgColor = Color.FromArgb(180, 95, 30); // Vibrant burnt orange
                }
                else
                {
                    // THEIR bubbles — rich teal/blue
                    bgColor = Color.FromArgb(35, 100, 140); // Vibrant deep blue
                }
            }
            else
            {
                bgColor = Color.FromArgb(
                    Math.Min(255, bubbleColor.R + (255 - bubbleColor.R) * 3 / 4),
                    Math.Min(255, bubbleColor.G + (255 - bubbleColor.G) * 3 / 4),
                    Math.Min(255, bubbleColor.B + (255 - bubbleColor.B) * 3 / 4));
            }

            int wrapperWidth = chatScrollPanel.ClientSize.Width - 24;
            int maxBubbleWidth = Math.Max(200, chatScrollPanel.ClientSize.Width - 100);
            int bubbleWidth = (int)(maxBubbleWidth * 0.72);
            int maxAllowedBubble = Math.Max(140, wrapperWidth - (sidePadding * 2 + avatarSize + avatarGap + 2));
            bubbleWidth = Math.Min(bubbleWidth, maxAllowedBubble);

            // DETECT FILE MESSAGE — new format: [FILE:{key}:{name}:{size}]
            // Also support old format: 📎 [filename] (size)
            bool isFileMessage = false;
            bool isImageMessage = false;
            string imageKey = null;
            int imageWidth = 0;
            int imageHeight = 0;
            string fileKey = null;
            string fileName = null;
            string fileSizeStr = null;
            string messageText = msg.message ?? "";
            string msgTimestamp = msg.timestamp; // Keep for old-format file search

            if (messageText.StartsWith("[IMG:"))
            {
                // New image format: [IMG:{key}:{width}:{height}:{name}:{size}]
                isImageMessage = true;
                var parts = messageText.TrimStart('[').TrimEnd(']').Split(':');
                if (parts.Length >= 6)
                {
                    imageKey = parts[1];
                    int.TryParse(parts[2], out imageWidth);
                    int.TryParse(parts[3], out imageHeight);
                    fileName = parts[4];
                    fileSizeStr = parts[5];
                }
                messageText = fileName ?? "Image";
            }
            else if (messageText.StartsWith("[FILE:"))
            {
                // New format: [FILE:{key}:{name}:{size}]
                isFileMessage = true;
                var parts = messageText.TrimStart('[').TrimEnd(']').Split(':');
                if (parts.Length >= 4)
                {
                    fileKey = parts[1];
                    fileName = parts[2];
                    fileSizeStr = parts[3];
                }
                messageText = $"\U0001F4C4 [{fileName ?? "file"}] ({fileSizeStr ?? "?"})";
            }
            else if (messageText.Contains("\U0001F4CE") || messageText.Contains("\U0001F4C4"))
            {
                // Old format: 📎 [filename] (size) — search Firebase for matching file
                isFileMessage = true;
                // Try to extract filename from old format like "📎 [filename.ext] (123 KB)"
                try
                {
                    int startBracket = messageText.IndexOf('[');
                    int endBracket = messageText.IndexOf(']');
                    if (startBracket >= 0 && endBracket > startBracket)
                    {
                        fileName = messageText.Substring(startBracket + 1, endBracket - startBracket - 1);
                    }
                }
                catch { }
            }

            string timeStr = "";
            try { timeStr = DateTime.Parse(msg.timestamp).ToLocalTime().ToString("HH:mm"); } catch { }

            // DETECT URLs in regular messages
            bool hasUrl = false;
            string firstUrl = null;
            if (!isFileMessage && !isImageMessage)
            {
                var urlMatch = Regex.Match(messageText, @"(https?://[^\s<>""]+)", RegexOptions.IgnoreCase);
                if (urlMatch.Success)
                {
                    hasUrl = true;
                    firstUrl = urlMatch.Value;
                }
            }

            // Display text for file messages — show with download hint
            string displayText = isFileMessage ? messageText + "\n⬇ Click to download" : messageText;

            int textWidth = bubbleWidth - 24;
            int imageBoxWidth = textWidth;
            int imageBoxHeight = 0;

            if (isImageMessage)
            {
                double ratio = (imageWidth > 0 && imageHeight > 0)
                    ? (double)imageWidth / imageHeight
                    : 1.2;

                imageBoxHeight = (int)Math.Round(imageBoxWidth / Math.Max(0.25, ratio));
                imageBoxHeight = Math.Max(120, Math.Min(260, imageBoxHeight));
            }

            Size textSize;
            using (var g = this.CreateGraphics())
            using (var font = new Font("Segoe UI", _dmFontSize))
            {
                textSize = TextRenderer.MeasureText(g, displayText, font, new Size(textWidth, 0),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            }

            int bubbleHeight = isImageMessage ? imageBoxHeight + 34 : textSize.Height + 36;

            // OUTER WRAPPER — controls alignment (left/right)
            var wrapper = new Panel
            {
                Width = wrapperWidth,
                Height = Math.Max(bubbleHeight, avatarSize) + 6,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 0, 2)
            };

            // BUBBLE PANEL — the actual colored bubble
            var bubble = new Panel
            {
                Size = new Size(bubbleWidth, bubbleHeight),
                BackColor = Color.Transparent,
                Cursor = (isFileMessage || isImageMessage || hasUrl) ? Cursors.Hand : Cursors.Default,
                Tag = msg
            };

            var avatarPanel = new Panel
            {
                Size = new Size(avatarSize, avatarSize),
                BackColor = Color.Transparent,
                Tag = "bubble-avatar"
            };
            avatarPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                string avatarUser = isMe ? currentUser : otherUser;
                var avatarImg = GetAvatarImageForUser(avatarUser);
                var avatarColor = GetAvatarColorForUser(avatarUser);

                if (avatarImg != null)
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(0, 0, avatarSize - 1, avatarSize - 1);
                        var oldClip = e.Graphics.Clip;
                        e.Graphics.SetClip(path);
                        e.Graphics.DrawImage(avatarImg, new Rectangle(0, 0, avatarSize - 1, avatarSize - 1));
                        e.Graphics.Clip = oldClip;
                    }

                    using (var borderPen = new Pen(Color.FromArgb(140, 255, 255, 255), 1f))
                        e.Graphics.DrawEllipse(borderPen, 0, 0, avatarSize - 1, avatarSize - 1);
                }
                else
                {
                    using (var brush = new SolidBrush(avatarColor))
                        e.Graphics.FillEllipse(brush, 0, 0, avatarSize - 1, avatarSize - 1);

                    string displayName = isMe ? currentUser : otherUser;
                    string initials = !string.IsNullOrWhiteSpace(displayName) ? displayName[0].ToString().ToUpperInvariant() : "?";
                    using (var font = new Font("Segoe UI", 10f, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.White))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        e.Graphics.DrawString(initials, font, brush, new RectangleF(0, 0, avatarSize - 1, avatarSize - 1), sf);
                    }
                }
            };

            // Position: right-aligned for my messages, left for theirs
            int avatarY = (wrapper.Height - avatarSize) / 2;
            int bubbleY = (wrapper.Height - bubbleHeight) / 2;
            if (isMe)
            {
                avatarPanel.Location = new Point(wrapper.Width - avatarSize - sidePadding, avatarY);
                bubble.Location = new Point(avatarPanel.Left - avatarGap - bubbleWidth, bubbleY);
            }
            else
            {
                avatarPanel.Location = new Point(sidePadding, avatarY);
                bubble.Location = new Point(avatarPanel.Right + avatarGap, bubbleY);
            }

            // FILE CLICK HANDLER — download file on click
            if (isImageMessage && !string.IsNullOrWhiteSpace(imageKey))
            {
                string capturedImageKey = imageKey;
                string capturedImageName = fileName;
                bubble.Click += async (s, e) => await DownloadFileFromFirebaseAsync(capturedImageKey, capturedImageName);

                var previewBox = new PictureBox
                {
                    BackColor = Color.FromArgb(42, 52, 64),
                    Location = new Point(12, 8),
                    Size = new Size(imageBoxWidth, imageBoxHeight),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Cursor = Cursors.Hand
                };
                previewBox.Click += async (s, e) => await DownloadFileFromFirebaseAsync(capturedImageKey, capturedImageName);
                bubble.Controls.Add(previewBox);
                _ = LoadImagePreviewIntoAsync(previewBox, capturedImageKey);
            }

            if (isFileMessage)
            {
                string capturedFileKey = fileKey;
                string capturedFileName = fileName;
                string capturedTimestamp = msgTimestamp;

                bubble.Click += async (s, e) =>
                {

                    if (capturedFileKey != null)
                    {
                        // New format — direct download by key
                        await DownloadFileFromFirebaseAsync(capturedFileKey, capturedFileName);
                    }
                    else
                    {
                        // Old format — search Firebase for matching file by conversationId
                        await SearchAndDownloadFileAsync(capturedFileName, capturedTimestamp);
                    }
                };
            }

            // URL CLICK HANDLER — open link in default browser
            if (hasUrl && !isFileMessage && !isImageMessage)
            {
                string capturedUrl = firstUrl;
                bubble.Click += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = capturedUrl,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                    }
                };
            }

            // PAINT HANDLER — draws rounded rectangle background
            bubble.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, bubble.Width - 1, bubble.Height - 1);
                int radius = 16;

                using (var path = CreateRoundedRectPath(rect, radius))
                using (var brush = new SolidBrush(bgColor))
                {
                    e.Graphics.FillPath(brush, path);
                }

                // Draw file icon or message text
                Color textColor = isDarkMode ? Color.FromArgb(235, 240, 248) : Color.FromArgb(20, 20, 20);

                if (isImageMessage)
                {
                    Color hintColor = Color.FromArgb(83, 189, 235);
                    using (var font = new Font("Segoe UI", _dmFontSize - 2f, FontStyle.Underline))
                    using (var brush = new SolidBrush(hintColor))
                    {
                        e.Graphics.DrawString("\u2B07 Click image to save", font, brush, 12, bubble.Height - 26);
                    }
                }
                else if (isFileMessage)
                {
                    // Draw file message with distinct styling (with color emoji support)
                    using (var font = new Font("Segoe UI", _dmFontSize, FontStyle.Regular))
                    using (var brush = new SolidBrush(textColor))
                    {
                        var textRect = new RectangleF(12, 8, bubble.Width - 24, bubble.Height - 44);
                        var sf = new StringFormat { FormatFlags = 0, Trimming = StringTrimming.Word };
                        ColorEmojiCache.DrawTextWithEmojis(e.Graphics, messageText, font, brush, textRect, sf);
                    }

                    // Draw "Click to download" hint in accent color
                    Color hintColor = Color.FromArgb(83, 189, 235); // Light blue
                    using (var font = new Font("Segoe UI", _dmFontSize - 2f, FontStyle.Underline))
                    using (var brush = new SolidBrush(hintColor))
                    {
                        e.Graphics.DrawString("\u2B07 Click to download", font, brush, 12, bubble.Height - 34);
                    }
                }
                else
                {
                    // Regular message text — with clickable URL detection
                    var urlPattern = new Regex(@"(https?://[^\s<>""]+)", RegexOptions.IgnoreCase);
                    var matches = urlPattern.Matches(messageText);

                    if (matches.Count == 0)
                    {
                        // No URLs — plain text with color emoji support
                        using (var font = new Font("Segoe UI", _dmFontSize))
                        using (var brush = new SolidBrush(textColor))
                        {
                            var textRect = new RectangleF(12, 8, bubble.Width - 24, bubble.Height - 28);
                            var sf = new StringFormat { FormatFlags = 0, Trimming = StringTrimming.Word };
                            ColorEmojiCache.DrawTextWithEmojis(e.Graphics, messageText, font, brush, textRect, sf);
                        }
                    }
                    else
                    {
                        // Has URLs — draw text segments with URLs underlined in blue
                        float drawX = 12;
                        float drawY = 8;
                        float maxW = bubble.Width - 24;
                        int lastIdx = 0;

                        using (var normalFont = new Font("Segoe UI", _dmFontSize))
                        using (var linkFont = new Font("Segoe UI", _dmFontSize, FontStyle.Underline))
                        using (var normalBrush = new SolidBrush(textColor))
                        using (var linkBrush = new SolidBrush(Color.FromArgb(100, 200, 255)))
                        {
                            foreach (Match m in matches)
                            {
                                // Draw text before URL (with color emoji support)
                                if (m.Index > lastIdx)
                                {
                                    string before = messageText.Substring(lastIdx, m.Index - lastIdx);
                                    var sz = e.Graphics.MeasureString(before, normalFont, (int)maxW);
                                    ColorEmojiCache.DrawTextWithEmojis(e.Graphics, before, normalFont, normalBrush,
                                        new RectangleF(drawX, drawY, maxW, bubble.Height - 28), null);
                                    drawY += sz.Height;
                                }
                                // Draw URL with underline + blue (URLs won't contain emojis)
                                {
                                    string urlText = m.Value;
                                    var sz = e.Graphics.MeasureString(urlText, linkFont, (int)maxW);
                                    e.Graphics.DrawString(urlText, linkFont, linkBrush,
                                        new RectangleF(drawX, drawY, maxW, bubble.Height - 28));
                                    drawY += sz.Height;
                                }
                                lastIdx = m.Index + m.Length;
                            }
                            // Draw remaining text after last URL (with color emoji support)
                            if (lastIdx < messageText.Length)
                            {
                                string after = messageText.Substring(lastIdx);
                                ColorEmojiCache.DrawTextWithEmojis(e.Graphics, after, normalFont, normalBrush,
                                    new RectangleF(drawX, drawY, maxW, bubble.Height - 28), null);
                            }
                        }
                    }
                }

                // Draw timestamp (bottom-right)
                Color timeColor = isDarkMode ? Color.FromArgb(140, 160, 175) : Color.FromArgb(100, 110, 120);
                using (var font = new Font("Segoe UI", 8f))
                using (var brush = new SolidBrush(timeColor))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Far };
                    e.Graphics.DrawString(timeStr, font, brush,
                        new RectangleF(0, bubble.Height - 20, bubble.Width - 12, 16), sf);
                }

                // Draw read receipt indicators for sent messages (my messages)
                if (isMe)
                {
                    if (msg.read)
                    {
                        // BLUE DOUBLE CHECK — message has been read by recipient
                        using (var font = new Font("Segoe UI", 8f))
                        using (var brush = new SolidBrush(Color.FromArgb(83, 189, 235)))
                        {
                            e.Graphics.DrawString("✓✓", font, brush, bubble.Width - 40, bubble.Height - 20);
                        }
                    }
                    else
                    {
                        // GRAY SINGLE CHECK — message delivered but not yet read
                        using (var font = new Font("Segoe UI", 8f))
                        using (var brush = new SolidBrush(Color.FromArgb(160, 160, 160)))
                        {
                            e.Graphics.DrawString("✓", font, brush, bubble.Width - 32, bubble.Height - 20);
                        }
                    }
                }
            };

            // ╔ EDIT BUTTON — own messages only (non-file), small pencil icon ╗
            if (isMe && !isFileMessage && !isImageMessage)
            {
                var editBtn = new Button
                {
                    Text = "✏",
                    Font = new Font("Segoe UI", 8f),
                    Size = new Size(24, 22),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = isDarkMode ? Color.FromArgb(140, 160, 180) : Color.FromArgb(120, 130, 140),
                    Cursor = Cursors.Hand,
                    Location = new Point(bubble.Width - 30, 2),
                    TabStop = false
                };
                editBtn.FlatAppearance.BorderSize = 0;
                editBtn.FlatAppearance.MouseOverBackColor = isDarkMode
                    ? Color.FromArgb(60, 70, 85)
                    : Color.FromArgb(200, 200, 200);

                // Capture variables for the click handler
                string editFirebaseKey = null;
                _messageFirebaseKeys.TryGetValue(msg, out editFirebaseKey);
                string editMsgText = messageText;

                editBtn.Click += async (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(editFirebaseKey))
                        await EditDmMessageAsync(editFirebaseKey, editMsgText, msg);
                };
                bubble.Controls.Add(editBtn);
            }

            // ╔ EDITED INDICATOR — draw "(edited)" in the paint handler ╗
            if (msg.edited)
            {
                var editedLabel = new Label
                {
                    Text = "(edited)",
                    Font = new Font("Segoe UI", 7f, FontStyle.Italic),
                    ForeColor = isDarkMode ? Color.FromArgb(120, 135, 150) : Color.FromArgb(130, 140, 150),
                    BackColor = Color.Transparent,
                    AutoSize = true,
                    Location = new Point(12, bubble.Height - 20)
                };
                bubble.Controls.Add(editedLabel);
            }

            wrapper.Controls.Add(avatarPanel);
            wrapper.Controls.Add(bubble);
            return wrapper;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // FILE DOWNLOAD — fetch base64 data from Firebase and save to disk
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Downloads a file from Firebase global_dm_files, decodes base64, and saves to user's chosen location.
        /// </summary>
        /// <summary>
        /// Downloads a file from Firebase by its direct key.
        /// Used for new format messages: [FILE:{key}:{name}:{size}]
        /// </summary>
        private async Task LoadImagePreviewIntoAsync(PictureBox pictureBox, string fileKey)
        {
            if (pictureBox == null || pictureBox.IsDisposed || string.IsNullOrWhiteSpace(fileKey))
                return;

            var image = await GetImagePreviewAsync(fileKey);
            if (image == null || pictureBox.IsDisposed)
                return;

            if (pictureBox.InvokeRequired)
            {
                pictureBox.BeginInvoke(new Action(() =>
                {
                    if (!pictureBox.IsDisposed)
                        pictureBox.Image = image;
                }));
            }
            else
            {
                pictureBox.Image = image;
            }
        }

        private async Task<Image> GetImagePreviewAsync(string fileKey)
        {
            if (string.IsNullOrWhiteSpace(fileKey))
                return null;

            lock (_imagePreviewLoading)
            {
                if (_imagePreviewCache.TryGetValue(fileKey, out var cachedImage))
                    return cachedImage;
            }

            while (true)
            {
                bool shouldLoad = false;
                lock (_imagePreviewLoading)
                {
                    if (_imagePreviewCache.TryGetValue(fileKey, out var cachedImage))
                        return cachedImage;

                    if (!_imagePreviewLoading.Contains(fileKey))
                    {
                        _imagePreviewLoading.Add(fileKey);
                        shouldLoad = true;
                    }
                }

                if (shouldLoad)
                    break;

                await Task.Delay(40);
            }

            try
            {
                string url = $"{firebaseBaseUrl}/global_dm_files/{fileKey}.json";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                    return null;

                var payload = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (payload == null || !payload.TryGetValue("base64Data", out var base64Obj))
                    return null;

                string base64 = Convert.ToString(base64Obj);
                if (string.IsNullOrWhiteSpace(base64))
                    return null;

                var bytes = Convert.FromBase64String(base64);
                using (var ms = new MemoryStream(bytes))
                using (var rawImage = Image.FromStream(ms))
                {
                    var preview = new Bitmap(rawImage);
                    lock (_imagePreviewLoading)
                    {
                        if (!_imagePreviewCache.ContainsKey(fileKey))
                        {
                            _imagePreviewCache[fileKey] = preview;
                            return preview;
                        }

                        preview.Dispose();
                        return _imagePreviewCache[fileKey];
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                return null;
            }
            finally
            {
                lock (_imagePreviewLoading)
                {
                    _imagePreviewLoading.Remove(fileKey);
                }
            }
        }

        private async Task DownloadFileFromFirebaseAsync(string fileKey, string suggestedName)
        {
//             DebugLogger.Log("[DirectMessage] DownloadFileFromFirebaseAsync() file: " + suggestedName);
            ShowTransferBar(suggestedName ?? "file", isDownload: true);
            try
            {
                string url = $"{firebaseBaseUrl}/global_dm_files/{fileKey}.json";

                var response = await client.GetAsync(url);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.GetAsync");

                if (!response.IsSuccessStatusCode)
                {
                    CompleteTransferBar(false);
                    MessageBox.Show("Could not download file. The file may no longer be available.",
                        "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    CompleteTransferBar(false);
                    MessageBox.Show("File not found on server.",
                        "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                bool saved = SaveBase64FileFromJson(json, suggestedName);
                CompleteTransferBar(saved);
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                CompleteTransferBar(false);
                MessageBox.Show($"Could not download file: {ex.Message}",
                    "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Searches Firebase global_dm_files for a file matching the conversation and timestamp.
        /// Used for old format messages that don't have a fileKey embedded.
        /// </summary>
        private async Task SearchAndDownloadFileAsync(string suggestedName, string msgTimestamp)
        {
            ShowTransferBar(suggestedName ?? "file", isDownload: true);
            try
            {
                // Search all files for this conversation
                string url = $"{firebaseBaseUrl}/global_dm_files.json?orderBy=\"conversationId\"&equalTo=\"{conversationId}\"";

                var response = await client.GetAsync(url);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.GetAsync");

                if (!response.IsSuccessStatusCode)
                {
                    // Fallback: try fetching ALL files (if indexing not set up)
                    await SearchAndDownloadFallbackAsync(suggestedName);
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    CompleteTransferBar(false);
                    MessageBox.Show("File not found. The file may have been sent with an older version.",
                        "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Parse results and find the best match by fileName
                var files = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(json);

                if (files == null || files.Count == 0)
                {
                    CompleteTransferBar(false);
                    MessageBox.Show("No files found for this conversation.",
                        "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Find best match by filename
                dynamic bestMatch = null;
                foreach (var kvp in files)
                {
                    string fn = kvp.Value.fileName?.ToString() ?? "";
                    if (suggestedName != null && fn.Contains(suggestedName))
                    {
                        bestMatch = kvp.Value;
                        break;
                    }
                    bestMatch = kvp.Value; // fallback: use last one
                }

                if (bestMatch == null)
                {
                    CompleteTransferBar(false);
                    MessageBox.Show("Could not find matching file data.",
                        "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string fileJson = JsonConvert.SerializeObject(bestMatch);
                bool saved = SaveBase64FileFromJson(fileJson, suggestedName);
                CompleteTransferBar(saved);
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                CompleteTransferBar(false);
                MessageBox.Show($"Could not search for file: {ex.Message}",
                    "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Fallback search: try fetching files by conversationId prefix in the key name.
        /// Used when Firebase indexing is not configured.
        /// </summary>
        private async Task SearchAndDownloadFallbackAsync(string suggestedName)
        {
            try
            {
                // The fileKey format is: {conversationId}_{ticks}
                // Try to get files that start with our conversationId
                string url = $"{firebaseBaseUrl}/global_dm_files.json?orderBy=\"$key\"&startAt=\"{conversationId}_\"&endAt=\"{conversationId}_~\"";

                var response = await client.GetAsync(url);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.GetAsync");

                if (!response.IsSuccessStatusCode)
                {
                    CompleteTransferBar(false);
                    MessageBox.Show("File download requires both users to have the latest version.\n\nFiles sent with older versions cannot be downloaded.\nPlease ask the sender to resend the file.",
                        "File Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    CompleteTransferBar(false);
                    MessageBox.Show("No files found. The sender may need to resend with the latest version.",
                        "File Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var files = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(json);

                if (files == null || files.Count == 0)
                {
                    CompleteTransferBar(false);
                    MessageBox.Show("No files found for this conversation.",
                        "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Find match by filename or use the most recent
                dynamic bestMatch = null;
                foreach (var kvp in files)
                {
                    string fn = kvp.Value.fileName?.ToString() ?? "";
                    if (suggestedName != null && fn.Contains(suggestedName))
                    {
                        bestMatch = kvp.Value;
                        break;
                    }
                    bestMatch = kvp.Value;
                }

                if (bestMatch != null)
                {
                    string fileJson = JsonConvert.SerializeObject(bestMatch);
                    bool saved = SaveBase64FileFromJson(fileJson, suggestedName);
                    CompleteTransferBar(saved);
                }
                else
                {
                    CompleteTransferBar(false);
                    MessageBox.Show("Could not find the file.",
                        "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                CompleteTransferBar(false);
                MessageBox.Show($"Could not download file: {ex.Message}",
                    "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Decodes base64 file data from JSON and saves to disk with SaveFileDialog.
        /// Shared helper for both direct and search-based downloads.
        /// </summary>
        private bool SaveBase64FileFromJson(string json, string suggestedName)
        {
            try
            {
                dynamic fileData = JsonConvert.DeserializeObject(json);
                string base64Data = fileData.base64Data;
                string originalName = (string)(fileData.fileName ?? suggestedName ?? "download");


                if (string.IsNullOrEmpty(base64Data))
                {
                    MessageBox.Show("File data is empty.",
                        "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Save file";
                    sfd.FileName = originalName;
                    string ext = Path.GetExtension(originalName);
                    sfd.Filter = $"Original format (*{ext})|*{ext}|All Files (*.*)|*.*";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        byte[] fileBytes = Convert.FromBase64String(base64Data);
                        File.WriteAllBytes(sfd.FileName, fileBytes);
//                         DebugLogger.Log("[DirectMessage] File operation: File.WriteAllBytes");
                        MessageBox.Show($"File saved: {Path.GetFileName(sfd.FileName)}",
                            "Download Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                MessageBox.Show($"Could not save file: {ex.Message}",
                    "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // ROUNDED RECTANGLE PATH — helper for bubble shape
        // ════════════════════════════════════════════════════════════════════════════

        private GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // RESIZE BUBBLES — when form is resized, recalculate bubble positions
        // ════════════════════════════════════════════════════════════════════════════

        private void ResizeBubbles()
        {
            if (bubbleFlow == null || currentMessages.Count == 0) return;

            const int sidePadding = 4;
            const int avatarSize = 24;
            const int avatarGap = 8;
            int panelW = chatScrollPanel.ClientSize.Width - 24;
            foreach (Control wrapper in bubbleFlow.Controls)
            {
                if (wrapper is Panel wrapperPanel && wrapperPanel.Controls.Count > 0)
                {
                    wrapperPanel.Width = panelW;
                    var bubble = wrapperPanel.Controls
                        .OfType<Panel>()
                        .FirstOrDefault(p => p.Tag is DirectMessage);
                    var avatar = wrapperPanel.Controls
                        .OfType<Panel>()
                        .FirstOrDefault(p => p.Tag is string tag && tag == "bubble-avatar");
                    if (bubble != null && bubble.Tag is DirectMessage msg)
                    {
                        bool isMe = string.Equals(msg.fromUser, currentUser, StringComparison.OrdinalIgnoreCase);
                        int maxBubbleWidth = Math.Max(200, chatScrollPanel.ClientSize.Width - 100);
                        int bubbleWidth = (int)(maxBubbleWidth * 0.72);
                        int maxAllowedBubble = Math.Max(140, wrapperPanel.Width - (sidePadding * 2 + avatarSize + avatarGap + 2));
                        bubbleWidth = Math.Min(bubbleWidth, maxAllowedBubble);
                        bubble.Width = bubbleWidth;

                        wrapperPanel.Height = Math.Max(bubble.Height, avatarSize) + 6;
                        int avatarY = (wrapperPanel.Height - avatarSize) / 2;
                        int bubbleY = (wrapperPanel.Height - bubble.Height) / 2;

                        if (isMe)
                        {
                            if (avatar != null)
                                avatar.Location = new Point(wrapperPanel.Width - avatarSize - sidePadding, avatarY);

                            int avatarLeft = avatar?.Left ?? (wrapperPanel.Width - avatarSize - sidePadding);
                            bubble.Location = new Point(avatarLeft - avatarGap - bubbleWidth, bubbleY);
                        }
                        else
                        {
                            if (avatar != null)
                                avatar.Location = new Point(sidePadding, avatarY);

                            int avatarRight = avatar?.Right ?? (sidePadding + avatarSize);
                            bubble.Location = new Point(avatarRight + avatarGap, bubbleY);
                        }

                        bubble.Invalidate();
                        avatar?.Invalidate();
                    }
                }
            }
        }

        private static bool IsImageExtension(string ext)
        {
            switch ((ext ?? "").ToLowerInvariant())
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                case ".webp":
                    return true;
                default:
                    return false;
            }
        }

        private static ImageCodecInfo GetJpegCodec()
        {
            return ImageCodecInfo.GetImageDecoders()
                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
        }

        private static byte[] EncodeImageJpegBytes(Image source, int maxSide, long quality, out int outWidth, out int outHeight)
        {
            outWidth = source.Width;
            outHeight = source.Height;

            float scale = 1f;
            int maxCurrent = Math.Max(source.Width, source.Height);
            if (maxCurrent > maxSide)
                scale = (float)maxSide / maxCurrent;

            outWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            outHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

            using (var resized = new Bitmap(outWidth, outHeight))
            using (var g = Graphics.FromImage(resized))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(source, 0, 0, outWidth, outHeight);

                using (var ms = new MemoryStream())
                {
                    var jpeg = GetJpegCodec();
                    if (jpeg != null)
                    {
                        using (var encParams = new EncoderParameters(1))
                        {
                            encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                            resized.Save(ms, jpeg, encParams);
                        }
                    }
                    else
                    {
                        resized.Save(ms, ImageFormat.Jpeg);
                    }
                    return ms.ToArray();
                }
            }
        }

        private async void SendImageFromFileAsync(string filePath, string fileName)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var img = Image.FromStream(fs))
                {
                    int w, h;
                    byte[] jpegBytes = EncodeImageJpegBytes(img, 1280, 84L, out w, out h);
                    await SendImagePayloadAsync(jpegBytes, fileName, w, h);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                MessageBox.Show("Could not send image file.", "Image Send Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void SendClipboardImageAsync()
        {
            try
            {
                if (!Clipboard.ContainsImage())
                    return;

                using (var img = Clipboard.GetImage())
                {
                    if (img == null) return;
                    int w, h;
                    byte[] jpegBytes = EncodeImageJpegBytes(img, 1280, 84L, out w, out h);
                    await SendImagePayloadAsync(jpegBytes, "pasted-image.jpg", w, h);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                MessageBox.Show("Could not send pasted image.", "Image Send Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SendImagePayloadAsync(byte[] imageBytes, string fileName, int width, int height)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return;

            if (imageBytes.Length > 5 * 1024 * 1024)
            {
                MessageBox.Show("Image is too large after resize. Try a smaller image.",
                    "Image Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowTransferBar(fileName, isDownload: false);

            try
            {
                string base64Data = Convert.ToBase64String(imageBytes);
                string fileKey = $"{conversationId}_{DateTime.UtcNow.Ticks}";
                string imageMessage = $"[IMG:{fileKey}:{width}:{height}:{fileName}:{FormatFileSize(imageBytes.LongLength)}]";

                var newMessage = new DirectMessage
                {
                    fromUser = currentUser,
                    toUser = otherUser,
                    message = imageMessage,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    read = false
                };

                string fileUrl = $"{firebaseBaseUrl}/global_dm_files/{fileKey}.json";
                var filePayload = new
                {
                    fileName = fileName,
                    fileSize = imageBytes.LongLength,
                    base64Data = base64Data,
                    fromUser = currentUser,
                    toUser = otherUser,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    conversationId = conversationId,
                    kind = "image",
                    width = width,
                    height = height,
                    mimeType = "image/jpeg"
                };

                string fileJson = JsonConvert.SerializeObject(filePayload);
                var fileContent = new StringContent(fileJson, System.Text.Encoding.UTF8, "application/json");
                var uploadResponse = await client.PutAsync(fileUrl, fileContent);
                if (!uploadResponse.IsSuccessStatusCode)
                {
                    CompleteTransferBar(false);
                    return;
                }

                string url = $"{firebaseBaseUrl}/global_dm/{conversationId}.json";
                string json = JsonConvert.SerializeObject(newMessage);
                var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, httpContent);
                if (response.IsSuccessStatusCode)
                {
                    CompleteTransferBar(true);
                    lastMessageCount = 0;
                    await LoadMessages();
                }
                else
                {
                    CompleteTransferBar(false);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                CompleteTransferBar(false);
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // DISPLAY MESSAGES — rebuild bubble list
        // ════════════════════════════════════════════════════════════════════════════

        private void DisplayMessages()
        {
            // Build a hash of message state to detect changes (read receipts, edits, content)
            string readHash = string.Join(",", currentMessages.Select(m =>
                m.fromUser + (m.read ? "R" : "U") + (m.edited ? "E" : "") + m.message?.GetHashCode()));

            // Only rebuild if message count changed OR message state changed
            if (currentMessages.Count == lastMessageCount && readHash == _lastReadHash) return;

            int previousCount = lastMessageCount;
            lastMessageCount = currentMessages.Count;
            _lastReadHash = readHash;

            // FLASH TASKBAR — when new message arrives and window is not focused
            if (previousCount > 0 && currentMessages.Count > previousCount && !this.ContainsFocus)
            {
                // Check if the newest message is from the other user (not our own)
                var newestMsg = currentMessages.LastOrDefault();
                if (newestMsg != null && !string.Equals(newestMsg.fromUser, currentUser, StringComparison.OrdinalIgnoreCase))
                {
                    FlashTaskbar();
                }
            }

            bubbleFlow.SuspendLayout();
            bubbleFlow.Controls.Clear();

            // Add date separator + bubbles
            string lastDate = "";
            foreach (var msg in currentMessages)
            {
                string msgDate = "";
                try { msgDate = DateTime.Parse(msg.timestamp).ToLocalTime().ToString("dd MMM yyyy"); } catch { }

                // DATE SEPARATOR — like WhatsApp shows "Today", "Yesterday", date
                if (msgDate != lastDate)
                {
                    lastDate = msgDate;
                    var dateLbl = new Label
                    {
                        Text = msgDate,
                        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                        ForeColor = isDarkMode ? Color.FromArgb(130, 145, 160) : Color.FromArgb(100, 110, 120),
                        BackColor = isDarkMode ? Color.FromArgb(28, 34, 44) : Color.FromArgb(220, 218, 212),
                        TextAlign = ContentAlignment.MiddleCenter,
                        AutoSize = false,
                        Size = new Size(120, 24),
                        Margin = new Padding((chatScrollPanel.ClientSize.Width - 120) / 2 - 12, 8, 0, 4),
                        Padding = new Padding(8, 2, 8, 2)
                    };
                    bubbleFlow.Controls.Add(dateLbl);
                }

                bubbleFlow.Controls.Add(CreateBubble(msg));
            }

            bubbleFlow.ResumeLayout(true);

            // SCROLL TO BOTTOM
            chatScrollPanel.ScrollControlIntoView(bubbleFlow);
            if (bubbleFlow.Controls.Count > 0)
            {
                var lastCtrl = bubbleFlow.Controls[bubbleFlow.Controls.Count - 1];
                chatScrollPanel.ScrollControlIntoView(lastCtrl);
            }
        }

        /// <summary>
        /// Flashes the taskbar icon to get the user's attention when a new message arrives.
        /// Keeps flashing until the user clicks on the window.
        /// </summary>
        private void FlashTaskbar()
        {
            var fi = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                hwnd = this.Handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = uint.MaxValue,  // Flash until focused
                dwTimeout = 0            // Use default cursor blink rate
            };
            FlashWindowEx(ref fi);
        }

        // ════════════════════════════════════════════════════════════════════════════
        // SETUP REFRESH TIMER — poll Firebase every 5 seconds
        // ════════════════════════════════════════════════════════════════════════════

        private void SetupRefreshTimer()
        {
            refreshTimer = new Timer();
            refreshTimer.Interval = 5000;
            refreshTimer.Tick += async (s, e) => { await LoadMessages(); };
            refreshTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════════════
        // LOAD MESSAGES — fetch from Firebase
        // ════════════════════════════════════════════════════════════════════════════

        private async Task LoadMessages()
        {
//             DebugLogger.Log("[DirectMessage] LoadMessages() loading messages from Firebase for conversation: " + conversationId);
            try
            {
                RefreshOtherAvatarFromTeamMeta();

                string url = $"{firebaseBaseUrl}/global_dm/{conversationId}.json";
                HttpResponseMessage response = await client.GetAsync(url);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.GetAsync");

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();

                    if (content != "null" && !string.IsNullOrEmpty(content))
                    {
                        var messages = JsonConvert.DeserializeObject<Dictionary<string, DirectMessage>>(content);
                        if (messages != null)
                        {
                            // Store Firebase keys for each message (needed for read receipts)
                            _messageFirebaseKeys.Clear();
                            var ordered = messages.OrderBy(kvp => kvp.Value.timestamp).ToList();
                            currentMessages = ordered.Select(kvp => kvp.Value).ToList();
                            foreach (var kvp in ordered)
                                _messageFirebaseKeys[kvp.Value] = kvp.Key;

                            // MARK UNREAD MESSAGES AS READ (messages sent TO me that I haven't read)
                            foreach (var kvp in ordered.Where(kvp =>
                                string.Equals(kvp.Value.toUser, currentUser, StringComparison.OrdinalIgnoreCase) && !kvp.Value.read))
                            {
                                await MarkMessageAsReadByKey(kvp.Key, kvp.Value);
                            }
                        }
                    }
                    else
                    {
                        currentMessages.Clear();
                        lastMessageCount = 0;
                    }

                    DisplayMessages();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // MARK MESSAGE AS READ — uses actual Firebase key to PATCH only the read flag
        // ════════════════════════════════════════════════════════════════════════════

        private async Task MarkMessageAsReadByKey(string firebaseKey, DirectMessage message)
        {
            try
            {
                // Only update the "read" field — not the entire message
                string url = $"{firebaseBaseUrl}/global_dm/{conversationId}/{firebaseKey}/read.json";
                message.read = true;

                var content = new StringContent("true", System.Text.Encoding.UTF8, "application/json");
                await client.PutAsync(url, content);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.PutAsync");
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // SEND MESSAGE
        // ════════════════════════════════════════════════════════════════════════════
        // EDIT DM MESSAGE — shows dialog, updates Firebase, re-renders
        // ════════════════════════════════════════════════════════════════════════════

        private async Task EditDmMessageAsync(string firebaseKey, string currentText, DirectMessage msg)
        {
            if (string.IsNullOrEmpty(firebaseKey)) return;

            // Show edit dialog
            var dlg = new Form
            {
                Text = "Edit Message",
                Size = new Size(400, 180),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = isDarkMode ? Color.FromArgb(30, 36, 48) : Color.FromArgb(248, 249, 252)
            };

            var txtEdit = new TextBox
            {
                Text = currentText,
                Location = new Point(12, 12),
                Size = new Size(360, 60),
                Multiline = true,
                Font = new Font("Segoe UI", 10),
                BackColor = isDarkMode ? Color.FromArgb(38, 46, 60) : Color.White,
                ForeColor = isDarkMode ? Color.White : Color.Black
            };
            dlg.Controls.Add(txtEdit);

            var btnSave = new Button
            {
                Text = "Save",
                Location = new Point(12, 84),
                Size = new Size(100, 34),
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnSave.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(120, 84),
                Size = new Size(80, 34),
                BackColor = isDarkMode ? Color.FromArgb(55, 60, 75) : Color.FromArgb(200, 200, 200),
                ForeColor = isDarkMode ? Color.White : Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnCancel);

            dlg.AcceptButton = btnSave;
            dlg.CancelButton = btnCancel;

            if (dlg.ShowDialog() != DialogResult.OK) return;

            string newText = txtEdit.Text.Trim();
            if (string.IsNullOrEmpty(newText) || newText == currentText) return;

            try
            {
                // 1. Update local message
                msg.message = newText;
                msg.edited = true;

                // 2. Re-render chat bubbles
                DisplayMessages();
                // Force re-render by resetting lastMessageCount
                lastMessageCount = 0;
                DisplayMessages();

                // 3. Push edit to Firebase (message text + edited flag)
                string urlMsg = $"{firebaseBaseUrl}/global_dm/{conversationId}/{firebaseKey}/message.json";
                string jsonMsg = JsonConvert.SerializeObject(newText);
                var contentMsg = new StringContent(jsonMsg, System.Text.Encoding.UTF8, "application/json");
                await client.PutAsync(urlMsg, contentMsg);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.PutAsync");

                string urlEdited = $"{firebaseBaseUrl}/global_dm/{conversationId}/{firebaseKey}/edited.json";
                string jsonEdited = JsonConvert.SerializeObject(true);
                var contentEdited = new StringContent(jsonEdited, System.Text.Encoding.UTF8, "application/json");
                await client.PutAsync(urlEdited, contentEdited);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.PutAsync");
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // SEND MESSAGE
        // ════════════════════════════════════════════════════════════════════════════

        private async void SendButton_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[DirectMessage] SendButton_Click() sending message to: " + otherUser);
            string messageText = messageInput.Text.Trim();
            if (string.IsNullOrEmpty(messageText)) return;

            try
            {
                var newMessage = new DirectMessage
                {
                    fromUser = currentUser,
                    toUser = otherUser,
                    message = messageText,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    read = false
                };

                string url = $"{firebaseBaseUrl}/global_dm/{conversationId}.json";
                string json = JsonConvert.SerializeObject(newMessage);
                var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, httpContent);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.PostAsync");

                if (response.IsSuccessStatusCode)
                {
                    messageInput.Clear();
                    lastMessageCount = 0; // Force rebuild to show new message
                    await LoadMessages();

                    // Check if the other user is offline — show friendly notification
                    bool isOtherUserOnline = await CheckUserOnlineAsync(otherUser);
                    if (!isOtherUserOnline)
                    {
                        ShowOfflineNotification($"{otherUser} is currently offline. They will receive your message as soon as they come online.");
                    }
                }
                else
                {
                    // Retry once before giving up
                    await Task.Delay(1000);
                    var retryContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    HttpResponseMessage retryResponse = await client.PostAsync(url, retryContent);
//                     DebugLogger.Log("[DirectMessage] Firebase operation: client.PostAsync");
                    if (retryResponse.IsSuccessStatusCode)
                    {
                        messageInput.Clear();
                        lastMessageCount = 0;
                        await LoadMessages();

                        bool isOtherUserOnline = await CheckUserOnlineAsync(otherUser);
                        if (!isOtherUserOnline)
                        {
                            ShowOfflineNotification($"{otherUser} is currently offline. They will receive your message as soon as they come online.");
                        }
                    }
                    else
                    {
                        ShowOfflineNotification("Message could not be delivered right now. Please check your connection and try again.");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
                ShowOfflineNotification("Message could not be delivered right now. Please check your connection and try again.");
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // CHECK USER ONLINE STATUS — query Firebase logs for the other user
        // ════════════════════════════════════════════════════════════════════════════

        private async Task<bool> CheckUserOnlineAsync(string userName)
        {
//             DebugLogger.Log("[DirectMessage] CheckUserOnlineAsync() checking if user is online: " + userName);
            try
            {
                // Check across all team logs for the user's status
                string logsUrl = UserStorage.GetFirebaseLogsUrl();
                HttpResponseMessage response = await client.GetAsync(logsUrl);
//                 DebugLogger.Log("[DirectMessage] Firebase operation: client.GetAsync");

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(content) && content != "null")
                    {
                        var logs = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(content);
                        if (logs != null)
                        {
                            foreach (var log in logs.Values)
                            {
                                string logUser = log.ContainsKey("userId") ? log["userId"]?.ToString() :
                                                 log.ContainsKey("userName") ? log["userName"]?.ToString() : "";
                                string status = log.ContainsKey("status") ? log["status"]?.ToString() : "";

                                if (string.Equals(logUser, userName, StringComparison.OrdinalIgnoreCase) &&
                                    (status == "Online" || status == "Working"))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[DirectMessage] Exception caught: " + ex.ToString());
            }

            return false;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // SHOW OFFLINE NOTIFICATION — non-blocking tooltip-style notification
        // ════════════════════════════════════════════════════════════════════════════

        private void ShowOfflineNotification(string message)
        {
            // Create a subtle notification panel at the bottom of the chat area
            var notifPanel = new Panel
            {
                Height = 36,
                Dock = DockStyle.Bottom,
                BackColor = isDarkMode ? Color.FromArgb(40, 50, 65) : Color.FromArgb(255, 248, 220),
                Padding = new Padding(8, 0, 8, 0)
            };

            var notifLabel = new Label
            {
                Text = "\u2139 " + message,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = isDarkMode ? Color.FromArgb(180, 200, 220) : Color.FromArgb(120, 100, 50),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            notifPanel.Controls.Add(notifLabel);
            this.Controls.Add(notifPanel);
            notifPanel.BringToFront();

            // Auto-dismiss after 4 seconds
            var dismissTimer = new Timer { Interval = 4000 };
            dismissTimer.Tick += (s, args) =>
            {
                dismissTimer.Stop();
                dismissTimer.Dispose();
                if (this.Controls.Contains(notifPanel))
                {
                    this.Controls.Remove(notifPanel);
                    notifPanel.Dispose();
                }
            };
            dismissTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════════════
        // KEYBOARD — Ctrl+Enter or Enter to send
        // ════════════════════════════════════════════════════════════════════════════

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V && Clipboard.ContainsImage())
            {
                e.SuppressKeyPress = true;
                SendClipboardImageAsync();
                return;
            }

            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                SendButton_Click(sender, EventArgs.Empty);
            }
        }
    }
}
