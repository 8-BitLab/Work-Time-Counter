// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                                 ║
// ║                     WORKFLOW - TEAM TIME TRACKER                              ║
// ║                                                                              ║
// ║  FILE:        ChatPanel.cs                                                   ║
// ║  PURPOSE:     REAL-TIME TEAM CHAT WITH REACTIONS, LIKES, TIPS & EMOJI        ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                           ║
// ║  LICENSE:     OPEN SOURCE                                                    ║
// ║                                                                              ║
// ║  DESCRIPTION:                                                                ║
// ║  Full-featured team chat panel using Firebase Realtime Database.              ║
// ║  Supports emoji reactions (toggle on/off), likes, tipping system,            ║
// ║  emoji picker, admin message deletion, and mute enforcement.                 ║
// ║                                                                              ║
// ║  FEATURES:                                                                   ║
// ║  - REAL-TIME MESSAGES: Auto-refresh from Firebase every 15 seconds           ║
// ║  - EMOJI REACTIONS: 5 quick-reaction emojis below each message               ║
// ║  - LIKES: Heart button with counter per message                              ║
// ║  - TIPPING: Send 1-100 point tips to other users' messages                   ║
// ║  - EMOJI PICKER: Popup grid with 16 common emojis to insert                 ║
// ║  - ADMIN DELETE: Right-click to delete any message (admin only)              ║
// ║  - MUTE CHECK: Muted users see warning and cannot send messages              ║
// ║  - SOUND EFFECTS: Chat send/receive sounds via SoundManager                 ║
// ║                                                                              ║
// ║  FIREBASE STRUCTURE:                                                         ║
// ║    /chat/{key}/user        --> "Alice"                                        ║
// ║    /chat/{key}/message     --> "Hello team!"                                  ║
// ║    /chat/{key}/timestamp   --> "2024-01-15T10:30:00Z"                         ║
// ║    /chat/{key}/reactions   --> { "👍": ["Alice","Bob"] }                      ║
// ║    /chat/{key}/likes       --> ["Alice", "Charlie"]                           ║
// ║    /chat/{key}/tips        --> { "Alice": 50 }                                ║
// ║                                                                              ║
// ║  GitHub: https://github.com/8BitLabEngineering                               ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    // ═══ CLASS: CHATPANEL — WINFORMS USERCONTROL FOR TEAM CHAT ═══
    public class ChatPanel : UserControl
    {
        // ═══ FIREBASE & AUTHENTICATION CONFIGURATION ═══
        // HTTP client used for all Firebase REST API calls
        private static readonly HttpClient _http = new HttpClient();
        // Base URL to Firebase Realtime Database (e.g., "https://myapp.firebaseio.com")
        private readonly string _firebaseBaseUrl;
        // Currently logged-in username for message ownership & reaction tracking
        private readonly string _currentUserName;
        // Admin flag: allows deletion of ANY message from any user
        private readonly bool _isAdmin;
        // Callback function to check if a user is muted (prevents sending messages)
        private readonly Func<string, bool> _isMutedCheck;
        // All team users — used for looking up per-user colors for chat name labels
        private List<UserInfo> _allUsers;

        // ═══ UI COMPONENTS ═══
        // FlowLayoutPanel that displays all chat messages as vertical card stack
        // Uses WrapContents=false and FlowDirection.TopDown for proper message ordering
        private FlowLayoutPanel flowMessages;
        // Text input for user to type their message (EmojiRichTextBox for color emoji rendering)
        private EmojiRichTextBox txtInput;
        // Button to send the message (or press Enter)
        private Button btnSend;
        // Button/panel to open emoji picker popup window (Panel with EmojiRichTextBox for color rendering)
        private Control btnEmoji;
        // Header label showing "💬 TEAM CHAT"
        private Label lblTitle;

        // ═══ MESSAGE DATA STORAGE ═══
        // Ordered list of chat messages, sorted by timestamp (oldest first)
        private List<ChatMessage> _messages = new List<ChatMessage>();
        // Dictionary mapping Firebase auto-generated keys to ChatMessage objects
        // Used for operations like reactions, likes, tips, and deletion (which require the Firebase key)
        private Dictionary<string, ChatMessage> _messagesByKey = new Dictionary<string, ChatMessage>();
        // Tracks previous message count to detect new incoming messages for sound effects
        private int _previousMessageCount = 0;
        // Remembers the timestamp of the last message to avoid duplicate sound triggers
        private string _lastKnownMsgTimestamp = "";
        // Theme mode: true = dark (default), false = light mode
        private bool _isDarkMode = true;
        // Custom theme reference (null = use dark/light default)
        private CustomTheme _customTheme = null;

        // ═══ MENTION / PING TRACKING ═══
        // Tracks which mention notifications the user has already clicked/dismissed.
        // Key = FirebaseKey of the message. Once clicked, the yellow highlight is removed.
        private HashSet<string> _dismissedMentions = new HashSet<string>();

        // ═══ SMART REFRESH — ONLY RE-RENDER WHEN DATA ACTUALLY CHANGES ═══
        // Stores a hash of all message data from the last render.
        // If the hash matches on next refresh, skip the expensive RenderChat() call.
        private string _lastRenderHash = "";

        // ═══ CHAT FONT SIZE SETTING ═══
        // User can choose: "Small" (8.5f), "Medium" (10f), "Big" (12f)
        // Affects name, message text, and card height in RenderChat().
        private string _chatFontSizeName = "Small";
        private float _nameFontSize = 8.5f;
        private float _msgFontSize = 8.5f;
        private int _cardBaseHeight = 22;

        // ═══ EMOJI SETS ═══
        // 16 common emojis available in the emoji picker popup when user clicks 😊 button
        private static readonly string[] EmojiSet = new[]
        {
            "😀", "😂", "😍", "👍", "👎", "❤️", "🔥", "🎉", "💯", "🙏", "😢", "😡", "🤔", "💪", "✅", "❌"
        };

        // ═══ URL DETECTION ═══
        // Regex to find http/https URLs in message text for making them clickable
        private static readonly Regex UrlRegex = new Regex(
            @"(https?://[^\s<>""]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ═══ QUICK REACTIONS ═══
        // 5 reaction emojis shown as buttons below each message (clickable for toggle)
        private static readonly string[] ReactionEmojis = new[]
        {
            "😀", "👍", "❤️", "🎉", "😢"
        };

        /// <summary>
        /// Constructor: Initialize ChatPanel with Firebase connection and current user info.
        /// Sets up UI components and begins background emoji preloading.
        /// </summary>
        public ChatPanel(string firebaseBaseUrl, string currentUserName, bool isAdmin, Func<string, bool> isMutedCheck, List<UserInfo> allUsers = null)
        {
//             DebugLogger.Log($"[ChatPanel] Constructor: user='{currentUserName}', isAdmin={isAdmin}, fbUrl='{firebaseBaseUrl}'");

            _firebaseBaseUrl = firebaseBaseUrl.TrimEnd('/');
            _currentUserName = currentUserName;
            _isAdmin = isAdmin;
            _isMutedCheck = isMutedCheck;
            _allUsers = allUsers;

            this.Height = 280;
            this.BackColor = ThemeConstants.Dark.BgElevated;
            this.BorderStyle = BorderStyle.None;
            this.Padding = new Padding(ThemeConstants.SpaceS);

            // Enable double-buffering to reduce flicker during chat refresh
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            // Draw subtle top border line for panel separation
            this.Paint += (s, e) =>
            {
                ThemeConstants.DrawTopBorder(e, this.Width, _isDarkMode);
            };

            BuildUI();

            // Start pre-downloading color emoji PNGs from Twemoji CDN (background thread)
//             DebugLogger.Log("[ChatPanel] Starting emoji cache preload");
            ColorEmojiCache.CacheReady += OnEmojiCacheReady;
            ColorEmojiCache.PreloadAsync();

//             DebugLogger.Log("[ChatPanel] Constructor complete");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ColorEmojiCache.CacheReady -= OnEmojiCacheReady;
            }
            base.Dispose(disposing);
        }

        private void OnEmojiCacheReady()
        {
            try
            {
                if (IsDisposed)
                    return;

                if (InvokeRequired)
                {
                    BeginInvoke(new Action(OnEmojiCacheReady));
                    return;
                }

                // Force one repaint so already loaded messages switch from mono glyphs
                // to color emoji rendering immediately after cache warm-up.
                _lastRenderHash = "";
                RenderChat();
            }
            catch { }
        }

        // ═══ BUILD UI — CREATES THE CHAT INTERFACE LAYOUT ═══
        // Constructs three main sections: header (title), content area (message cards), input bar (textbox + buttons)
        // Called once in constructor; layout uses WinForms docking for responsive sizing
        private void BuildUI()
        {
//             DebugLogger.Log("[ChatPanel] BuildUI: Starting UI construction");

            // ── Header bar ──
//             DebugLogger.Log("[ChatPanel] BuildUI: Creating header panel");
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.Transparent
            };

            lblTitle = new Label
            {
                Text = "💬 TEAM CHAT",
                Font = new Font("Segoe UI Emoji", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 127, 80),
                Location = new Point(6, 4),
                AutoSize = true
            };
            panelHeader.Controls.Add(lblTitle);
            this.Controls.Add(panelHeader);

            // ── Separator line above input ──
//             DebugLogger.Log("[ChatPanel] BuildUI: Creating separator line");
            var separator = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Color.FromArgb(60, 68, 80)
            };
            this.Controls.Add(separator);

            // ── Input bar (bottom) ──
//             DebugLogger.Log("[ChatPanel] BuildUI: Creating input bar with send button and emoji picker");
            var panelInput = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                BackColor = Color.FromArgb(22, 26, 34),
                Padding = new Padding(4, 4, 4, 4)
            };

            btnSend = new Button
            {
                Text = "Send",
                Dock = DockStyle.Right,
                Width = 70,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 127, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.Click += BtnSend_Click;
            panelInput.Controls.Add(btnSend);

            // ╔ COLOR EMOJI BUTTON — Uses EmojiRichTextBox (RichEdit50W) for full-color emoji icon ╗
            btnEmoji = EmojiRichTextBox.CreateEmojiButton("😊", 40, 34, 14f,
                Color.FromArgb(48, 54, 66), Color.FromArgb(66, 80, 100),
                () => BtnEmoji_Click(null, EventArgs.Empty));
            btnEmoji.Dock = DockStyle.Right;
            btnEmoji.Margin = new Padding(0, 0, 4, 0);
            panelInput.Controls.Add(btnEmoji);

            txtInput = new EmojiRichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Emoji", 10),
                BackColor = Color.FromArgb(38, 44, 56),
                ForeColor = Color.FromArgb(220, 224, 230),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.None,
                Multiline = true,
                ReadOnly = false
            };
            txtInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
//                     DebugLogger.Log("[ChatPanel] User pressed Enter to send message");
                    e.SuppressKeyPress = true;
                    BtnSend_Click(s, e);
                }
            };
            panelInput.Controls.Add(txtInput);
            this.Controls.Add(panelInput);

            // ── Chat messages area (FlowLayoutPanel) ──
            // FlowLayoutPanel automatically arranges message cards top-to-bottom in a vertical stack.
            // WrapContents=true allows cards to wrap if needed, but FlowDirection.TopDown ensures vertical ordering.
            // AutoScroll=true enables scrollbars when content exceeds visible area.
            // Each message is a Panel control added dynamically in RenderChat().
//             DebugLogger.Log("[ChatPanel] BuildUI: Creating message FlowLayoutPanel");
            flowMessages = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 28, 36),
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown
            };
            // HANDLE RESIZE — MAKE MESSAGE CARDS FILL THE WIDTH WHEN PANEL IS RESIZED
            flowMessages.Resize += (s, ev) =>
            {
//                 DebugLogger.Log($"[ChatPanel] Chat panel resized: width={flowMessages.ClientSize.Width}");
                flowMessages.SuspendLayout();
                foreach (Control c in flowMessages.Controls)
                {
                    if (c is Panel || c is FlowLayoutPanel)
                        c.Width = flowMessages.ClientSize.Width - 20;
                }
                flowMessages.ResumeLayout(true);
            };
            this.Controls.Add(flowMessages);

//             DebugLogger.Log("[ChatPanel] BuildUI: UI construction complete");
        }

        /// <summary>
        /// BtnEmoji_Click: Open emoji picker popup window. When user selects emoji, insert into txtInput.
        /// </summary>
        private void BtnEmoji_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[ChatPanel] Emoji picker button clicked");

            // ╔ COLOR EMOJI PICKER — WebBrowser-based for full-color emoji rendering ╗
            // Uses HTML/IE11 engine which natively supports color emoji via Segoe UI Emoji
            ColorEmojiPicker.Show(this, btnEmoji, EmojiSet,
                "#1e242e", "#42a5f5", 290, 148,
                emoji =>
                {
//                     DebugLogger.Log($"[ChatPanel] Emoji selected: {emoji}");
                    txtInput.Select(txtInput.TextLength, 0);
                    txtInput.SelectionFont = new Font("Segoe UI Emoji", 10f);
                    txtInput.SelectedText = emoji;
                    txtInput.SelectionFont = txtInput.Font;
                    txtInput.SelectionStart = txtInput.TextLength;
                    txtInput.SelectionLength = 0;
                    txtInput.Focus();
                });

//             DebugLogger.Log("[ChatPanel] Emoji picker window shown");
        }

        /// <summary>
        /// RefreshAsync: Fetch latest messages from Firebase, merge with local storage, and re-render if changed.
        /// Implements smart refresh with hash comparison to avoid unnecessary UI updates.
        /// Detects new incoming messages and plays sound effect for notifications.
        /// </summary>
        public async Task RefreshAsync()
        {
            try
            {
//                 DebugLogger.Log("[ChatPanel] RefreshAsync: Starting message refresh");

                // ╔ LOCAL-FIRST: Load from local storage immediately ╗
                // This ensures chat is visible even if Firebase is down or empty.
                Dictionary<string, ChatMessage> mergedDict;

                // Try to fetch from Firebase
                Dictionary<string, ChatMessage> firebaseDict = null;
                try
                {
                    string url = _firebaseBaseUrl + "/chat.json";
//                     DebugLogger.Log($"[ChatPanel] RefreshAsync: Fetching from Firebase: {url}");
                    var response = await _http.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json) && json != "null")
                        {
                            firebaseDict = JsonConvert.DeserializeObject<Dictionary<string, ChatMessage>>(json);
//                             DebugLogger.Log($"[ChatPanel] RefreshAsync: Firebase fetch successful, {firebaseDict?.Count ?? 0} messages");
                        }
                        else
                        {
//                             DebugLogger.Log("[ChatPanel] RefreshAsync: Firebase returned empty or null");
                        }
                    }
                    else
                    {
                        DebugLogger.Log($"[ChatPanel] RefreshAsync: Firebase HTTP error: {response.StatusCode}");
                    }
                }
                catch (Exception fbEx) {
                    DebugLogger.Log($"[ChatPanel] RefreshAsync: Firebase fetch exception: {fbEx.Message}");
                }

                // Merge Firebase + Local (local fills gaps when Firebase is empty)
                if (firebaseDict != null && firebaseDict.Count > 0)
                {
//                     DebugLogger.Log("[ChatPanel] RefreshAsync: Merging Firebase data with local cache");
                    mergedDict = LocalChatStore.MergeWithFirebase(firebaseDict);
                }
                else
                {
                    // Firebase empty or unavailable — use local data
//                     DebugLogger.Log("[ChatPanel] RefreshAsync: Using local cache only");
                    mergedDict = LocalChatStore.LoadLocalChat();
                }

                // Build ordered message list
                _messagesByKey = new Dictionary<string, ChatMessage>();
                foreach (var kvp in mergedDict)
                {
                    kvp.Value.FirebaseKey = kvp.Key;
                    _messagesByKey[kvp.Key] = kvp.Value;
                }

                _messages = mergedDict.Values
                    .OrderBy(m => m.timestamp)
                    .ToList();

//                 DebugLogger.Log($"[ChatPanel] RefreshAsync: Total messages loaded: {_messages.Count}");

                // Keep last 80 messages for display
                if (_messages.Count > 80)
                {
//                     DebugLogger.Log($"[ChatPanel] RefreshAsync: Trimming messages from {_messages.Count} to 80");
                    _messages = _messages.Skip(_messages.Count - 80).ToList();
                }

                // Detect new incoming messages from OTHER users → play sound
                if (_messages.Count > 0)
                {
                    var lastMsg = _messages.Last();
                    if (_previousMessageCount > 0
                        && _messages.Count > _previousMessageCount
                        && lastMsg.user != _currentUserName
                        && lastMsg.timestamp != _lastKnownMsgTimestamp)
                    {
//                         DebugLogger.Log($"[ChatPanel] RefreshAsync: New incoming message from {lastMsg.user}, playing sound");
                        SoundManager.PlayChatReceive();
                    }
                    _lastKnownMsgTimestamp = lastMsg.timestamp ?? "";
                }
                _previousMessageCount = _messages.Count;

                // ╔ SMART REFRESH: Only re-render if data actually changed ╗
                string currentHash = BuildMessageHash();
                if (currentHash == _lastRenderHash)
                {
//                     DebugLogger.Log("[ChatPanel] RefreshAsync: No changes detected, skipping render");
                    return;
                }
                _lastRenderHash = currentHash;

//                 DebugLogger.Log("[ChatPanel] RefreshAsync: Changes detected, calling RenderChat");
                RenderChat();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ChatPanel] RefreshAsync: Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// BuildMessageHash: Create hash fingerprint of all messages including reactions, likes, tips.
        /// Used for smart refresh to detect if data changed since last render.
        /// </summary>
        private string BuildMessageHash()
        {
            var sb = new StringBuilder();
            foreach (var msg in _messages)
            {
                sb.Append(msg.FirebaseKey ?? "");
                sb.Append(msg.user ?? "");
                sb.Append(msg.message ?? "");
                sb.Append(msg.timestamp ?? "");
                if (msg.edited) sb.Append("E");
                if (msg.reactions != null)
                {
                    foreach (var r in msg.reactions)
                        sb.Append(r.Key).Append(r.Value?.Count ?? 0);
                }
                sb.Append(msg.likes?.Count ?? 0);
                if (msg.tips != null)
                {
                    foreach (var t in msg.tips)
                        sb.Append(t.Key).Append(t.Value);
                }
            }
            string hash = sb.ToString();
//             DebugLogger.Log($"[ChatPanel] BuildMessageHash: Generated hash for {_messages.Count} messages");
            return hash;
        }

        /// <summary>
        /// RenderChat: Clear all message controls and rebuild from _messages list.
        /// Creates card-based display with username, timestamp, message text, reactions, likes, tips.
        /// Includes mention detection, admin delete button, and right-click context menus.
        /// Auto-scrolls to bottom after rendering.
        /// </summary>
        private void RenderChat()
        {
//             DebugLogger.Log("[ChatPanel] RenderChat: Starting render of chat messages");

            if (flowMessages == null)
            {
//                 DebugLogger.Log("[ChatPanel] RenderChat: flowMessages is null, skipping render");
                return;
            }

            flowMessages.SuspendLayout();
//             DebugLogger.Log($"[ChatPanel] RenderChat: Clearing {flowMessages.Controls.Count} existing controls");
            flowMessages.Controls.Clear();

            Color myNameColorDefault = Color.FromArgb(255, 127, 80);
            Color otherNameColorDefault = Color.FromArgb(66, 165, 245);
            Color timeColor = (_customTheme != null && _customTheme.Enabled)
                ? _customTheme.GetSecondaryText() : Color.FromArgb(100, 110, 120);
            Color msgColor = (_customTheme != null && _customTheme.Enabled)
                ? _customTheme.GetText()
                : (_isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59));
            Color cardBg = (_customTheme != null && _customTheme.Enabled)
                ? _customTheme.GetChatCard()
                : (_isDarkMode ? Color.FromArgb(38, 44, 56) : Color.FromArgb(240, 243, 248));
            Color hoverBg = (_customTheme != null && _customTheme.Enabled)
                ? _customTheme.GetSelection()
                : (_isDarkMode ? Color.FromArgb(46, 52, 66) : Color.FromArgb(230, 234, 240));

            // ── SPACER: Push messages to bottom of panel ──
            int estimatedCardHeight = _messages.Count * (_cardBaseHeight + 1);
            int spacerHeight = Math.Max(0, flowMessages.ClientSize.Height - estimatedCardHeight);
            if (spacerHeight > 0)
            {
                var spacer = new Panel
                {
                    Width = flowMessages.ClientSize.Width - 20,
                    Height = spacerHeight,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0)
                };
                flowMessages.Controls.Add(spacer);
            }

            foreach (var msg in _messages)
            {
                string time = DateTime.TryParse(msg.timestamp, out var dt)
                    ? dt.ToLocalTime().ToString("HH:mm")
                    : "";

                bool isMe = msg.user == _currentUserName;
                string firebaseKey = msg.FirebaseKey ?? "";

                // ╔ MENTION/PING DETECTION ╗
                // Check if this message contains @currentUserName (case-insensitive).
                // If yes and not yet dismissed, show yellow highlight + alarm icon on name.
                bool isMentioned = !isMe
                    && !string.IsNullOrEmpty(msg.message)
                    && msg.message.IndexOf("@" + _currentUserName, StringComparison.OrdinalIgnoreCase) >= 0
                    && !_dismissedMentions.Contains(firebaseKey);

                // ╔ EXISTING REACTIONS SUMMARY ╗
                // Build a short text like "👍2 ❤️1" to show inline if reactions exist
                string reactionSummary = "";
                if (msg.reactions != null)
                {
                    foreach (var kvp in msg.reactions)
                    {
                        if (kvp.Value != null && kvp.Value.Count > 0)
                            reactionSummary += kvp.Key + kvp.Value.Count + " ";
                    }
                }
                int likeCount = msg.likes != null ? msg.likes.Count : 0;
                if (likeCount > 0)
                    reactionSummary += "♥" + likeCount + " ";
                reactionSummary = reactionSummary.Trim();

                // ╔ MESSAGE CARD PANEL — SINGLE ROW, CLEANER LOOK ╗
                var cardPanel = new Panel
                {
                    Width = flowMessages.ClientSize.Width - 20,
                    Height = _cardBaseHeight,
                    BackColor = isMentioned ? Color.FromArgb(80, 255, 255, 0) : cardBg,
                    Margin = new Padding(0, 0, 0, 1),
                    Padding = new Padding(4, 2, 4, 2),
                    Cursor = Cursors.Default
                };

                // ── Hover effect on the whole card ──
                Color normalBg = cardPanel.BackColor;
                cardPanel.MouseEnter += (s, e) => { if (!isMentioned) cardPanel.BackColor = hoverBg; };
                cardPanel.MouseLeave += (s, e) => { cardPanel.BackColor = normalBg; };

                int xOffset = 0;

                // ╔ ALARM ICON FOR MENTIONS ╗
                if (isMentioned)
                {
                    var alarmLabel = new Label
                    {
                        Text = "\U0001F514",  // 🔔 bell emoji
                        Font = new Font("Segoe UI Emoji", 8),
                        ForeColor = Color.FromArgb(200, 50, 50),
                        AutoSize = true,
                        Location = new Point(xOffset, 1),
                        BackColor = Color.Transparent,
                        Cursor = Cursors.Hand
                    };
                    // Click alarm to dismiss the mention highlight
                    string dismissKey = firebaseKey;
                    alarmLabel.Click += (s, e) =>
                    {
                        _dismissedMentions.Add(dismissKey);
                        RenderChat();
                    };
                    cardPanel.Controls.Add(alarmLabel);
                    xOffset += 18;
                }

                // ╔ USERNAME LABEL — uses per-user color from MembersMeta ╗
                Color nameColor;
                if (isMe)
                {
                    var meInfo = _allUsers?.FirstOrDefault(u => u.Name.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase));
                    nameColor = meInfo?.GetDrawingColor(myNameColorDefault) ?? myNameColorDefault;
                }
                else
                {
                    var senderInfo = _allUsers?.FirstOrDefault(u => u.Name.Equals(msg.user, StringComparison.OrdinalIgnoreCase));
                    nameColor = senderInfo?.GetDrawingColor(otherNameColorDefault) ?? otherNameColorDefault;
                }

                var nameLabel = new Label
                {
                    Text = (msg.user ?? "?") + ":",
                    Font = new Font("Segoe UI", _nameFontSize, FontStyle.Bold),
                    ForeColor = nameColor,
                    BackColor = isMentioned ? Color.FromArgb(255, 255, 100) : Color.Transparent,
                    AutoSize = true,
                    Location = new Point(xOffset, 2),
                    Cursor = isMe ? Cursors.Default : Cursors.Hand
                };

                // ── CLICK on name: dismiss mention highlight ──
                if (isMentioned)
                {
                    string dismissKey2 = firebaseKey;
                    nameLabel.Click += (s, e) =>
                    {
                        _dismissedMentions.Add(dismissKey2);
                        RenderChat();
                    };
                }

                // ── DOUBLE-CLICK on name: open Direct Message ──
                if (!isMe)
                {
                    string dmUser = msg.user;
                    nameLabel.DoubleClick += (s, e) =>
                    {
                        // Also dismiss mention if applicable
                        if (!string.IsNullOrEmpty(firebaseKey))
                            _dismissedMentions.Add(firebaseKey);
                        var dmForm = new DirectMessageForm(_firebaseBaseUrl, _currentUserName, dmUser, _isDarkMode);
                        dmForm.Show();
                    };
                }

                // ── Right-click context menu on user name — Add to Favorites ──
                if (!isMe)
                {
                    var nameContextMenu = new ContextMenuStrip();
                    nameContextMenu.BackColor = _isDarkMode ? Color.FromArgb(30, 34, 42) : Color.FromArgb(245, 247, 250);
                    nameContextMenu.ForeColor = _isDarkMode ? Color.White : Color.FromArgb(30, 30, 30);
                    nameContextMenu.Renderer = new ToolStripProfessionalRenderer(
                        new ProfessionalColorTable { UseSystemColors = false });

                    string clickedUser = msg.user;
                    var currentFavs = UserStorage.GetFavoriteUsers(_currentUserName);
                    bool alreadyFav = currentFavs.Any(f => f.Equals(clickedUser, StringComparison.OrdinalIgnoreCase));

                    var favItem = new ToolStripMenuItem(alreadyFav ? "\u2b50 Remove from Favorites" : "\u2b50 Add to Favorites");
                    favItem.Click += (s, e) =>
                    {
                        var favs = UserStorage.GetFavoriteUsers(_currentUserName);
                        if (favs.Any(f => f.Equals(clickedUser, StringComparison.OrdinalIgnoreCase)))
                        {
                            favs.RemoveAll(f => f.Equals(clickedUser, StringComparison.OrdinalIgnoreCase));
                            UserStorage.SaveFavoriteUsers(_currentUserName, favs);
                        }
                        else
                        {
                            favs.Add(clickedUser);
                            UserStorage.SaveFavoriteUsers(_currentUserName, favs);
                        }
                    };
                    nameContextMenu.Items.Add(favItem);
                    nameLabel.ContextMenuStrip = nameContextMenu;
                }

                cardPanel.Controls.Add(nameLabel);
                int nameW = TextRenderer.MeasureText(nameLabel.Text, nameLabel.Font).Width + 2;
                xOffset += nameW;

                // ╔ MESSAGE TEXT LABEL ╗
                int timeW = TextRenderer.MeasureText(time, new Font("Segoe UI", 7)).Width + 8;
                int reactionSummaryW = string.IsNullOrEmpty(reactionSummary) ? 0
                    : TextRenderer.MeasureText(reactionSummary, new Font("Segoe UI Emoji", 7)).Width + 6;

                // Reserve space for action buttons: Edit (own msgs only) + Delete (own or admin)
                bool canDelete = _isAdmin || isMe;
                bool canEdit = isMe;  // Only the author can edit, even admin cannot edit others
                int editBtnW = canEdit ? 28 : 0;
                int deleteBtnW = canDelete ? 22 : 0;

                // Detect edited status — strip old " (edited)" suffix if present
                string msgText = msg.message ?? "";
                bool isEdited = msg.edited;
                if (msgText.EndsWith(" (edited)"))
                {
                    msgText = msgText.Substring(0, msgText.Length - " (edited)".Length);
                    isEdited = true;
                }

                // Reserve space for "(edited)" label
                int editedLabelW = isEdited ? 52 : 0;
                int availMsgW = cardPanel.Width - xOffset - timeW - editedLabelW - reactionSummaryW - editBtnW - deleteBtnW - 16;

                // ╔ MESSAGE TEXT — COLOR EMOJI RICHTEXTBOX (RichEdit50W) ╗
                // Uses EmojiRichTextBox for full-color WhatsApp-style emoji rendering
                // Also natively handles clickable URLs via DetectUrls
                bool hasCachedEmoji = ColorEmojiCache.HasCachedEmojis(msgText);
                bool hasUrl = UrlRegex.IsMatch(msgText);

                // ╔ RIGHT-CLICK ON MESSAGE → SHOW REACTION POPUP ╗
                string popupFirebaseKey = firebaseKey;
                string popupMsgUser = msg.user;
                bool popupIsMe = isMe;

                Control msgControl;
                // Stable architecture: always use custom image-based renderer for emoji messages.
                // This avoids RichEdit50W class-registration crashes on some Windows/.NET combos.
                if (hasCachedEmoji)
                {
                    var emojiPanel = new Panel
                    {
                        Font = new Font("Segoe UI", _msgFontSize),
                        ForeColor = msgColor,
                        BackColor = isMentioned ? Color.FromArgb(80, 255, 255, 0) : cardBg,
                        Location = new Point(xOffset, 0),
                        Size = new Size(Math.Max(30, availMsgW), _cardBaseHeight),
                        Cursor = hasUrl ? Cursors.Hand : Cursors.Default
                    };

                    var msgSize = TextRenderer.MeasureText(msgText, emojiPanel.Font, new Size(availMsgW, 0),
                        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                    if (msgSize.Height > _cardBaseHeight - 2)
                    {
                        emojiPanel.Height = msgSize.Height + 4;
                        cardPanel.Height = Math.Max(cardPanel.Height, msgSize.Height + 6);
                    }

                    emojiPanel.Paint += (s, e) =>
                    {
                        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        using (var brush = new SolidBrush(msgColor))
                        {
                            ColorEmojiCache.DrawTextWithEmojis(
                                e.Graphics,
                                msgText,
                                emojiPanel.Font,
                                brush,
                                new RectangleF(0, 0, emojiPanel.Width - 2, emojiPanel.Height - 1),
                                null);
                        }
                    };

                    emojiPanel.MouseUp += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Right)
                        {
                            ShowReactionPopup(popupFirebaseKey, popupMsgUser, popupIsMe, emojiPanel);
                            return;
                        }

                        // Left click opens first URL if present in emoji-rendered message.
                        if (e.Button == MouseButtons.Left && hasUrl)
                        {
                            try
                            {
                                var match = UrlRegex.Match(msgText);
                                if (match.Success)
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = match.Value,
                                        UseShellExecute = true
                                    });
                                }
                            }
                            catch { }
                        }
                    };

                    msgControl = emojiPanel;
                }
                else
                {
                    var rtb = new EmojiRichTextBox
                    {
                        Text = msgText,
                        Font = new Font("Segoe UI Emoji", _msgFontSize),
                        ForeColor = msgColor,
                        BackColor = isMentioned ? Color.FromArgb(80, 255, 255, 0) : cardBg,
                        ReadOnly = true,
                        BorderStyle = BorderStyle.None,
                        ScrollBars = RichTextBoxScrollBars.None,
                        TabStop = false,
                        DetectUrls = true,
                        Cursor = Cursors.Hand,
                        Location = new Point(xOffset, 0),
                        Size = new Size(Math.Max(30, availMsgW), _cardBaseHeight),
                        WordWrap = true
                    };

                    var msgSize = TextRenderer.MeasureText(msgText, rtb.Font, new Size(availMsgW, 0),
                        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                    if (msgSize.Height > _cardBaseHeight - 2)
                    {
                        rtb.Height = msgSize.Height + 4;
                        cardPanel.Height = Math.Max(cardPanel.Height, msgSize.Height + 6);
                    }

                    rtb.LinkClicked += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = e.LinkText,
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    };

                    rtb.MouseUp += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Right)
                            ShowReactionPopup(popupFirebaseKey, popupMsgUser, popupIsMe, rtb);
                    };

                    msgControl = rtb;
                }

                cardPanel.Controls.Add(msgControl);

                // ╔ COLOR EMOJI — NATIVE RENDERING VIA RICHEDIT50W ╗
                // RichEdit50W with "Segoe UI Emoji" font and advanced typography enabled
                // renders color emojis natively via DirectWrite. No PNG insertion needed.
                // The InsertColorEmojis RTF approach was removed because it replaced emoji
                // text with PNG blobs that failed to render, making emojis invisible.
                xOffset += Math.Max(30, availMsgW) + 4;

                // ╔ REACTION SUMMARY (inline, small) ╗
                if (!string.IsNullOrEmpty(reactionSummary))
                {
                    var reactSumLabel = new Label
                    {
                        Text = reactionSummary,
                        Font = new Font("Segoe UI Emoji", 7),
                        ForeColor = Color.FromArgb(150, 160, 170),
                        AutoSize = true,
                        Location = new Point(xOffset, 3),
                        BackColor = Color.Transparent,
                        Cursor = Cursors.Hand
                    };
                    reactSumLabel.Click += (s, e) => ShowReactionPopup(popupFirebaseKey, popupMsgUser, popupIsMe, reactSumLabel);
                    cardPanel.Controls.Add(reactSumLabel);
                    xOffset += reactionSummaryW;
                }

                // ╔ TIMESTAMP + EDITED LABEL (right side) ╗
                int rightX = cardPanel.Width - timeW - editedLabelW - editBtnW - deleteBtnW - 8;

                var timeLabel = new Label
                {
                    Text = time,
                    Font = new Font("Segoe UI", 7),
                    ForeColor = timeColor,
                    AutoSize = true,
                    Location = new Point(rightX, 4),
                    BackColor = Color.Transparent
                };
                cardPanel.Controls.Add(timeLabel);
                rightX += timeW;

                // ╔ "(edited)" LABEL — gray, next to timestamp ╗
                if (isEdited)
                {
                    var editedLabel = new Label
                    {
                        Text = "(edited)",
                        Font = new Font("Segoe UI", 6.5f, FontStyle.Italic),
                        ForeColor = Color.FromArgb(110, 120, 135),
                        AutoSize = true,
                        Location = new Point(rightX, 5),
                        BackColor = Color.Transparent
                    };
                    cardPanel.Controls.Add(editedLabel);
                    rightX += editedLabelW;
                }

                // ╔ EDIT BUTTON — own messages only (author only, admin cannot edit others) ╗
                if (canEdit)
                {
                    string editKey = firebaseKey;
                    string editMsgText = msgText; // Use cleaned text (without "(edited)" suffix)
                    var editBtn = new Label
                    {
                        Text = "\u270f",
                        Font = new Font("Segoe UI", 8),
                        ForeColor = Color.FromArgb(100, 160, 220),
                        AutoSize = true,
                        Location = new Point(rightX, 2),
                        BackColor = Color.Transparent,
                        Cursor = Cursors.Hand
                    };
                    editBtn.MouseEnter += (s, e) => editBtn.ForeColor = Color.FromArgb(80, 200, 255);
                    editBtn.MouseLeave += (s, e) => editBtn.ForeColor = Color.FromArgb(100, 160, 220);
                    editBtn.Click += async (s, e) =>
                    {
                        await EditMessageAsync(editKey, editMsgText);
                    };
                    cardPanel.Controls.Add(editBtn);
                }

                // ╔ DELETE BUTTON (X) — own messages for everyone, all messages for admin ╗
                if (canDelete)
                {
                    string delKey = firebaseKey;
                    string delMsgUser = msg.user;
                    var deleteBtn = new Label
                    {
                        Text = "\u2715",
                        Font = new Font("Segoe UI", 8, FontStyle.Bold),
                        ForeColor = Color.FromArgb(180, 60, 60),
                        AutoSize = true,
                        Location = new Point(rightX + editBtnW, 2),
                        BackColor = Color.Transparent,
                        Cursor = Cursors.Hand
                    };
                    deleteBtn.MouseEnter += (s, e) => deleteBtn.ForeColor = Color.FromArgb(255, 80, 80);
                    deleteBtn.MouseLeave += (s, e) => deleteBtn.ForeColor = Color.FromArgb(180, 60, 60);
                    deleteBtn.Click += async (s, e) =>
                    {
                        if (MessageBox.Show("Delete this message?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            await DeleteMessageAsync(delKey, delMsgUser);
                        }
                    };
                    cardPanel.Controls.Add(deleteBtn);
                }

                flowMessages.Controls.Add(cardPanel);
            }

            // ── Bottom padding: ensures the very last message is not clipped ──
            // Must be tall enough so scrolling to bottom shows the last message
            // fully above the input bar
            if (_messages.Count > 0)
            {
                var bottomPad = new Panel
                {
                    Width = flowMessages.ClientSize.Width - 20,
                    Height = _cardBaseHeight + 8,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0)
                };
                flowMessages.Controls.Add(bottomPad);
            }

            flowMessages.ResumeLayout(true);
//             DebugLogger.Log("[ChatPanel] RenderChat: Layout resumed");

            // Force scroll to very bottom — ensure last message is fully visible
            if (flowMessages.Controls.Count > 0)
            {
//                 DebugLogger.Log($"[ChatPanel] RenderChat: Scrolling to bottom ({flowMessages.Controls.Count} controls)");
                flowMessages.PerformLayout();
                // Use ScrollControlIntoView on the very last control (the bottom padding)
                var lastCtrl = flowMessages.Controls[flowMessages.Controls.Count - 1];
                flowMessages.ScrollControlIntoView(lastCtrl);
                // Belt-and-suspenders: also set absolute scroll position
                int totalHeight = 0;
                foreach (Control c in flowMessages.Controls)
                    totalHeight += c.Height + c.Margin.Vertical;
                flowMessages.AutoScrollPosition = new Point(0, totalHeight);
            }

//             DebugLogger.Log("[ChatPanel] RenderChat: Render complete");
        }

        /// <summary>
        /// ShowReactionPopup: Display compact popup with reaction buttons, like button, and tip button.
        /// Appears when user clicks on message text.
        /// </summary>
        private void ShowReactionPopup(string firebaseKey, string msgUser, bool isMe, Control anchor)
        {
//             DebugLogger.Log($"[ChatPanel] ShowReactionPopup: firebaseKey={firebaseKey}, user={msgUser}, isMe={isMe}");

            if (string.IsNullOrEmpty(firebaseKey))
            {
//                 DebugLogger.Log("[ChatPanel] ShowReactionPopup: firebaseKey is empty, returning");
                return;
            }

            // ╔ COMPACT REACTION STRIP ╗
            // Small inline popup — just a thin row of tiny emoji buttons.
            // Total width: 7 buttons × 22px = ~170px, height: 20px
            int btnSize = 22;
            int btnCount = ReactionEmojis.Length + 1 + (isMe ? 0 : 1); // reactions + heart + tip(if not me)
            int popupW = (btnCount * (btnSize + 1)) + 6;
            int popupH = btnSize + 4;

            Color popupBg = _isDarkMode ? Color.FromArgb(42, 48, 60) : Color.FromArgb(240, 242, 246);

            var popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                BackColor = popupBg,
                Size = new Size(popupW, popupH),
                TopMost = true,
                Padding = new Padding(2, 1, 2, 1)
            };

            // Position right above the clicked label
            var screenPos = anchor.PointToScreen(new Point(0, -popupH));
            popup.Location = screenPos;

            int xPos = 2;

            // ── Reaction emoji buttons (tiny) ──
            foreach (var emoji in ReactionEmojis)
            {
                string em = emoji;
                var btn = new Button
                {
                    Text = em,
                    Width = btnSize,
                    Height = btnSize,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = popupBg,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI Emoji", 8),
                    Cursor = Cursors.Hand,
                    Location = new Point(xPos, 1),
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(66, 165, 245);
                btn.Click += async (s, e) =>
                {
                    popup.Close();
                    await AddReactionAsync(firebaseKey, em);
                };
                popup.Controls.Add(btn);
                xPos += btnSize + 1;
            }

            // ── Like (heart) button ──
            var likeBtn = new Button
            {
                Text = "♥",
                Width = btnSize,
                Height = btnSize,
                FlatStyle = FlatStyle.Flat,
                BackColor = popupBg,
                ForeColor = Color.FromArgb(255, 127, 80),
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Location = new Point(xPos, 1),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            likeBtn.FlatAppearance.BorderSize = 0;
            likeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 127, 80);
            likeBtn.Click += async (s, e) =>
            {
                popup.Close();
                await AddLikeAsync(firebaseKey);
            };
            popup.Controls.Add(likeBtn);
            xPos += btnSize + 1;

            // ── Tip button (only for other users' messages) ──
            if (!isMe)
            {
                string tipUser = msgUser;
                var tipBtn = new Button
                {
                    Text = "\U0001F4B0",
                    Width = btnSize,
                    Height = btnSize,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = popupBg,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI Emoji", 8),
                    Cursor = Cursors.Hand,
                    Location = new Point(xPos, 1),
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };
                tipBtn.FlatAppearance.BorderSize = 0;
                tipBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(66, 165, 245);
                tipBtn.Click += (s, e) =>
                {
                    popup.Close();
                    ShowTipDialog(firebaseKey, tipUser);
                };
                popup.Controls.Add(tipBtn);
            }

            // Close popup when it loses focus
            popup.Deactivate += (s, e) => popup.Close();
            popup.Show(this);

//             DebugLogger.Log("[ChatPanel] ShowReactionPopup: Popup shown");
        }

        /// <summary>
        /// AddReactionAsync: Toggle emoji reaction on message. If already reacted, remove; else add.
        /// Updates /chat/{firebaseKey}/reactions/{emoji} list in Firebase.
        /// </summary>
        private async Task AddReactionAsync(string firebaseKey, string emoji)
        {
//             DebugLogger.Log($"[ChatPanel] AddReactionAsync: firebaseKey={firebaseKey}, emoji={emoji}, user={_currentUserName}");

            if (string.IsNullOrEmpty(firebaseKey))
            {
//                 DebugLogger.Log("[ChatPanel] AddReactionAsync: firebaseKey is empty, returning");
                return;
            }

            try
            {
                string url = _firebaseBaseUrl + $"/chat/{firebaseKey}/reactions/{emoji}.json";
//                 DebugLogger.Log($"[ChatPanel] AddReactionAsync: Reading current reactions from {url}");
                var response = await _http.GetAsync(url);

                List<string> users = new List<string>();
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json) && json != "null")
                        users = JsonConvert.DeserializeObject<List<string>>(json);
//                     DebugLogger.Log($"[ChatPanel] AddReactionAsync: Current reaction count: {users?.Count ?? 0}");
                }

                // ╔ TOGGLE REACTION ╗
                // Fetch current list of users who reacted with this emoji.
                // If current user is in the list, remove them (un-react).
                // If current user is NOT in the list, add them (react).
                // Then PUT the updated list back to Firebase.
                if (users.Contains(_currentUserName))
                {
//                     DebugLogger.Log($"[ChatPanel] AddReactionAsync: Removing {_currentUserName} from reaction");
                    users.Remove(_currentUserName);
                }
                else
                {
//                     DebugLogger.Log($"[ChatPanel] AddReactionAsync: Adding {_currentUserName} to reaction");
                    users.Add(_currentUserName);
                }

                string updateJson = JsonConvert.SerializeObject(users);
//                 DebugLogger.Log($"[ChatPanel] AddReactionAsync: Sending updated reaction to Firebase");
                var content = new StringContent(updateJson, Encoding.UTF8, "application/json");
                await _http.PutAsync(url, content);

//                 DebugLogger.Log("[ChatPanel] AddReactionAsync: Refreshing chat");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ChatPanel] AddReactionAsync: Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// AddLikeAsync: Toggle like on message. If already liked, remove; else add.
        /// Updates /chat/{firebaseKey}/likes list in Firebase.
        /// </summary>
        private async Task AddLikeAsync(string firebaseKey)
        {
//             DebugLogger.Log($"[ChatPanel] AddLikeAsync: firebaseKey={firebaseKey}, user={_currentUserName}");

            if (string.IsNullOrEmpty(firebaseKey))
            {
//                 DebugLogger.Log("[ChatPanel] AddLikeAsync: firebaseKey is empty, returning");
                return;
            }

            try
            {
                string url = _firebaseBaseUrl + $"/chat/{firebaseKey}/likes.json";
//                 DebugLogger.Log($"[ChatPanel] AddLikeAsync: Reading current likes from {url}");
                var response = await _http.GetAsync(url);

                List<string> users = new List<string>();
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json) && json != "null")
                        users = JsonConvert.DeserializeObject<List<string>>(json);
//                     DebugLogger.Log($"[ChatPanel] AddLikeAsync: Current like count: {users?.Count ?? 0}");
                }

                // ╔ TOGGLE LIKE ╗
                // If current user already in likes list, remove them (unlike).
                // Otherwise, add them to likes list.
                if (users.Contains(_currentUserName))
                {
//                     DebugLogger.Log($"[ChatPanel] AddLikeAsync: Removing {_currentUserName} from likes");
                    users.Remove(_currentUserName);
                }
                else
                {
//                     DebugLogger.Log($"[ChatPanel] AddLikeAsync: Adding {_currentUserName} to likes");
                    users.Add(_currentUserName);
                }

                string updateJson = JsonConvert.SerializeObject(users);
//                 DebugLogger.Log("[ChatPanel] AddLikeAsync: Sending updated likes to Firebase");
                var content = new StringContent(updateJson, Encoding.UTF8, "application/json");
                await _http.PutAsync(url, content);

//                 DebugLogger.Log("[ChatPanel] AddLikeAsync: Refreshing chat");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ChatPanel] AddLikeAsync: Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// ShowTipDialog: Display dialog to input tip amount (1-100) for another user.
        /// Validates input and calls AddTipAsync on confirmation.
        /// </summary>
        private void ShowTipDialog(string firebaseKey, string targetUser)
        {
//             DebugLogger.Log($"[ChatPanel] ShowTipDialog: firebaseKey={firebaseKey}, targetUser={targetUser}");

            if (string.IsNullOrEmpty(firebaseKey))
            {
//                 DebugLogger.Log("[ChatPanel] ShowTipDialog: firebaseKey is empty, returning");
                return;
            }

            var form = new Form
            {
                Text = $"Tip {targetUser}",
                Width = 250,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(30, 36, 46),
                MaximizeBox = false,
                MinimizeBox = false
            };

            var label = new Label
            {
                Text = "Tip amount (1-100):",
                Location = new Point(12, 12),
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 224, 230),
                Font = new Font("Segoe UI", 9)
            };
            form.Controls.Add(label);

            var txtAmount = new TextBox
            {
                Location = new Point(12, 36),
                Width = 210,
                Text = "10",
                BackColor = Color.FromArgb(38, 44, 56),
                ForeColor = Color.FromArgb(220, 224, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };
            form.Controls.Add(txtAmount);

            var btnTip = new Button
            {
                Text = "Send Tip",
                Location = new Point(12, 65),
                Width = 100,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 127, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTip.FlatAppearance.BorderSize = 0;
            btnTip.Click += async (s, e) =>
            {
                if (int.TryParse(txtAmount.Text, out int amount) && amount >= 1 && amount <= 100)
                {
//                     DebugLogger.Log($"[ChatPanel] ShowTipDialog: User entered amount={amount}");
                    await AddTipAsync(firebaseKey, targetUser, amount);
                    form.Close();
                }
                else
                {
//                     DebugLogger.Log($"[ChatPanel] ShowTipDialog: Invalid amount entered: {txtAmount.Text}");
                    MessageBox.Show("Please enter a number between 1 and 100.");
                }
            };
            form.Controls.Add(btnTip);

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(122, 65),
                Width = 100,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(48, 54, 66),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) =>
            {
//                 DebugLogger.Log("[ChatPanel] ShowTipDialog: User cancelled");
                form.Close();
            };
            form.Controls.Add(btnCancel);

//             DebugLogger.Log("[ChatPanel] ShowTipDialog: Dialog showing");
            form.ShowDialog(this);
        }

        /// <summary>
        /// AddTipAsync: Add/accumulate tip amount for target user on message.
        /// Tips accumulate: if user tipped 20 and tips 30, total becomes 50.
        /// Updates /chat/{firebaseKey}/tips/{targetUser} in Firebase.
        /// </summary>
        private async Task AddTipAsync(string firebaseKey, string targetUser, int amount)
        {
//             DebugLogger.Log($"[ChatPanel] AddTipAsync: firebaseKey={firebaseKey}, targetUser={targetUser}, amount={amount}");

            if (string.IsNullOrEmpty(firebaseKey))
            {
//                 DebugLogger.Log("[ChatPanel] AddTipAsync: firebaseKey is empty, returning");
                return;
            }

            try
            {
                string url = _firebaseBaseUrl + $"/chat/{firebaseKey}/tips/{targetUser}.json";
//                 DebugLogger.Log($"[ChatPanel] AddTipAsync: Reading current tip from {url}");
                var response = await _http.GetAsync(url);

                int currentTip = 0;
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json) && json != "null")
                        currentTip = JsonConvert.DeserializeObject<int>(json);
//                     DebugLogger.Log($"[ChatPanel] AddTipAsync: Current tip total: {currentTip}");
                }

                // ╔ TIP ACCUMULATION ╗
                // Add the new tip amount to whatever total already exists for this user.
                int newTip = currentTip + amount;
//                 DebugLogger.Log($"[ChatPanel] AddTipAsync: New tip total: {newTip}");
                string updateJson = JsonConvert.SerializeObject(newTip);
                var content = new StringContent(updateJson, Encoding.UTF8, "application/json");
                await _http.PutAsync(url, content);

//                 DebugLogger.Log("[ChatPanel] AddTipAsync: Refreshing chat");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ChatPanel] AddTipAsync: Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// DeleteMessageAsync: Delete chat message from Firebase and local storage.
        /// Admin can delete any message; regular users can only delete their own.
        /// Deletes entire /chat/{firebaseKey} node including reactions, likes, tips.
        /// </summary>
        private async Task DeleteMessageAsync(string firebaseKey, string messageUser = null)
        {
//             DebugLogger.Log($"[ChatPanel] DeleteMessageAsync: firebaseKey={firebaseKey}, messageUser={messageUser}, currentUser={_currentUserName}");

            if (string.IsNullOrEmpty(firebaseKey))
            {
//                 DebugLogger.Log("[ChatPanel] DeleteMessageAsync: firebaseKey is empty, returning");
                return;
            }

            // Permission check: admin can delete anything, user can only delete own
            bool isOwnMessage = string.Equals(messageUser, _currentUserName, StringComparison.OrdinalIgnoreCase);
            if (!_isAdmin && !isOwnMessage)
            {
//                 DebugLogger.Log($"[ChatPanel] DeleteMessageAsync: Permission denied - not admin, not owner");
                return;
            }

//             DebugLogger.Log("[ChatPanel] DeleteMessageAsync: Permission granted, removing from local storage");
            // Remove from local storage first
            LocalChatStore.RemoveMessage(firebaseKey);

            try
            {
                // Then delete from Firebase
                string url = _firebaseBaseUrl + $"/chat/{firebaseKey}.json";
//                 DebugLogger.Log($"[ChatPanel] DeleteMessageAsync: Deleting from Firebase: {url}");
                await _http.DeleteAsync(url);
//                 DebugLogger.Log("[ChatPanel] DeleteMessageAsync: Refreshing chat");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                // Firebase unavailable — at least local is cleaned up
                DebugLogger.Log($"[ChatPanel] DeleteMessageAsync: Firebase delete failed: {ex.Message}, refreshing from local");
                _lastRenderHash = "";
                await RefreshAsync();
            }
        }

        /// <summary>
        /// EditMessageAsync: Open edit dialog and save updated message text to Firebase.
        /// Updates local message immediately, then sends to Firebase asynchronously.
        /// Sets edited flag on message.
        /// </summary>
        private async Task EditMessageAsync(string firebaseKey, string currentText)
        {
//             DebugLogger.Log($"[ChatPanel] EditMessageAsync: firebaseKey={firebaseKey}, currentText.Length={currentText?.Length ?? 0}");

            if (string.IsNullOrEmpty(firebaseKey))
            {
//                 DebugLogger.Log("[ChatPanel] EditMessageAsync: firebaseKey is empty, returning");
                return;
            }

            // Show edit dialog
            var dlg = new Form
            {
                Text = "Edit Message",
                Size = new Size(400, 180),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = _isDarkMode ? Color.FromArgb(30, 36, 48) : Color.FromArgb(248, 249, 252)
            };

            var txtEdit = new TextBox
            {
                Text = currentText,
                Location = new Point(12, 12),
                Size = new Size(360, 60),
                Multiline = true,
                Font = new Font("Segoe UI", 10),
                BackColor = _isDarkMode ? Color.FromArgb(38, 46, 60) : Color.White,
                ForeColor = _isDarkMode ? Color.White : Color.Black
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
                BackColor = _isDarkMode ? Color.FromArgb(55, 60, 75) : Color.FromArgb(200, 200, 200),
                ForeColor = _isDarkMode ? Color.White : Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnCancel);

            dlg.AcceptButton = btnSave;
            dlg.CancelButton = btnCancel;

//             DebugLogger.Log("[ChatPanel] EditMessageAsync: Showing edit dialog");
            if (dlg.ShowDialog() != DialogResult.OK)
            {
//                 DebugLogger.Log("[ChatPanel] EditMessageAsync: Edit cancelled");
                return;
            }

            string newText = txtEdit.Text.Trim();
            if (string.IsNullOrEmpty(newText) || newText == currentText)
            {
//                 DebugLogger.Log("[ChatPanel] EditMessageAsync: New text empty or unchanged");
                return;
            }

//             DebugLogger.Log($"[ChatPanel] EditMessageAsync: New text entered, length={newText.Length}");

            try
            {
                // 1. Update local message immediately (keeps clean text, set edited flag)
//                 DebugLogger.Log("[ChatPanel] EditMessageAsync: Updating local message");
                var localMsg = _messages.FirstOrDefault(m => m.FirebaseKey == firebaseKey);
                if (localMsg != null)
                {
                    localMsg.message = newText;
                    localMsg.edited = true;
                }

                // 2. Update local chat store
                if (_messagesByKey.ContainsKey(firebaseKey))
                {
                    _messagesByKey[firebaseKey].message = newText;
                    _messagesByKey[firebaseKey].edited = true;
                }
                LocalChatStore.UpdateMessage(firebaseKey, newText);

                // 3. Re-render chat locally (NO full RefreshAsync — keeps all messages)
//                 DebugLogger.Log("[ChatPanel] EditMessageAsync: Re-rendering chat");
                _lastRenderHash = "";
                RenderChat();

                // 4. Push edit to Firebase in background (message text + edited flag)
//                 DebugLogger.Log("[ChatPanel] EditMessageAsync: Pushing edit to Firebase");
                string urlMsg = _firebaseBaseUrl + $"/chat/{firebaseKey}/message.json";
                string jsonMsg = JsonConvert.SerializeObject(newText);
                var contentMsg = new StringContent(jsonMsg, Encoding.UTF8, "application/json");
                await _http.PutAsync(urlMsg, contentMsg);

                string urlEdited = _firebaseBaseUrl + $"/chat/{firebaseKey}/edited.json";
                string jsonEdited = JsonConvert.SerializeObject(true);
                var contentEdited = new StringContent(jsonEdited, Encoding.UTF8, "application/json");
                await _http.PutAsync(urlEdited, contentEdited);

//                 DebugLogger.Log("[ChatPanel] EditMessageAsync: Edit complete");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ChatPanel] EditMessageAsync: Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// BtnSend_Click: Send new chat message to Firebase.
        /// Implements local-first storage with optimistic rendering.
        /// Checks mute status before allowing send. Plays sound effect on success.
        /// </summary>
        private async void BtnSend_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[ChatPanel] BtnSend_Click: Send button clicked");

            string text = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
//                 DebugLogger.Log("[ChatPanel] BtnSend_Click: Message text is empty, returning");
                return;
            }

//             DebugLogger.Log($"[ChatPanel] BtnSend_Click: Message length={text.Length}, user={_currentUserName}");

            // ╔ MUTE CHECK ╗
            // Before allowing the send, call the _isMutedCheck callback.
            // This checks with the parent application whether this user is muted.
            // If muted, show warning and return without posting.
            if (_isMutedCheck(_currentUserName))
            {
//                 DebugLogger.Log($"[ChatPanel] BtnSend_Click: User {_currentUserName} is muted, showing warning");
                MessageBox.Show("You are muted and cannot post messages.", "Muted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var chatMsg = new ChatMessage
            {
                user = _currentUserName,
                message = text,
                timestamp = DateTime.UtcNow.ToString("o"),
                reactions = new Dictionary<string, List<string>>(),
                likes = new List<string>(),
                tips = new Dictionary<string, int>()
            };

//             DebugLogger.Log("[ChatPanel] BtnSend_Click: Clearing input and focusing");
            txtInput.Clear();
            txtInput.Focus();

            // ╔ LOCAL-FIRST: Save to local storage immediately ╗
//             DebugLogger.Log("[ChatPanel] BtnSend_Click: Saving message to local storage");
            string localKey = LocalChatStore.AddMessage(chatMsg);

            // ╔ OPTIMISTIC RENDER ╗
            // Show the message locally IMMEDIATELY so user sees it right away.
//             DebugLogger.Log("[ChatPanel] BtnSend_Click: Optimistic render (showing message immediately)");
            _messages.Add(chatMsg);
            _previousMessageCount = _messages.Count;
            _lastRenderHash = ""; // force re-render
            RenderChat();

            // ╔ SYNC TO FIREBASE (background, non-blocking) ╗
            try
            {
                string url = _firebaseBaseUrl + "/chat.json";
//                 DebugLogger.Log($"[ChatPanel] BtnSend_Click: Posting message to Firebase: {url}");
                string json = JsonConvert.SerializeObject(chatMsg);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var fbResponse = await _http.PostAsync(url, content);

                if (fbResponse.IsSuccessStatusCode)
                {
//                     DebugLogger.Log("[ChatPanel] BtnSend_Click: Firebase POST successful, playing send sound");
                    SoundManager.PlayChatSend();

                    // If Firebase returned a key, update local storage to use that key
                    string responseJson = await fbResponse.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseJson);
                    if (result != null && result.ContainsKey("name"))
                    {
                        string fbKey = result["name"];
//                         DebugLogger.Log($"[ChatPanel] BtnSend_Click: Firebase returned key: {fbKey}");
                        // Remove the local_ key and re-save with Firebase key
                        LocalChatStore.RemoveMessage(localKey);
                        chatMsg.FirebaseKey = fbKey;
                        LocalChatStore.AddMessage(chatMsg);
                    }
                }
                else
                {
                    DebugLogger.Log($"[ChatPanel] BtnSend_Click: Firebase POST failed: {fbResponse.StatusCode}");
                }

//                 DebugLogger.Log("[ChatPanel] BtnSend_Click: Waiting 300ms before refresh");
                await System.Threading.Tasks.Task.Delay(300);
//                 DebugLogger.Log("[ChatPanel] BtnSend_Click: Calling RefreshAsync");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ChatPanel] BtnSend_Click: Firebase post exception: {ex.Message}");
            }
        }

        /// <summary>
        /// ApplyTheme: Switch between dark mode, light mode, or custom theme.
        /// Updates all control colors and re-renders messages with new card background.
        /// Called by parent window when user changes theme settings.
        /// </summary>
        public void ApplyTheme(bool darkMode, CustomTheme customTheme = null)
        {
//             DebugLogger.Log($"[ChatPanel] ApplyTheme: darkMode={darkMode}, customTheme={customTheme?.Enabled ?? false}");

            _isDarkMode = darkMode;
            _customTheme = customTheme;

            if (customTheme != null && customTheme.Enabled)
            {
//                 DebugLogger.Log("[ChatPanel] ApplyTheme: Applying custom theme colors");
                this.BackColor = customTheme.GetCard();
                flowMessages.BackColor = customTheme.GetBackground();
                txtInput.BackColor = customTheme.GetInput();
                txtInput.ForeColor = customTheme.GetText();
                lblTitle.ForeColor = customTheme.GetAccent();
                btnEmoji.BackColor = customTheme.GetInput();
                foreach (Control child in btnEmoji.Controls)
                    child.BackColor = customTheme.GetInput();
            }
            else if (darkMode)
            {
//                 DebugLogger.Log("[ChatPanel] ApplyTheme: Applying dark mode colors");
                this.BackColor = ThemeConstants.Dark.BgElevated;
                flowMessages.BackColor = ThemeConstants.Dark.BgBase;
                txtInput.BackColor = ThemeConstants.Dark.BgInput;
                txtInput.ForeColor = ThemeConstants.Dark.TextPrimary;
                lblTitle.ForeColor = ThemeConstants.Dark.TextSecondary;  // calm, not coral
                btnEmoji.BackColor = ThemeConstants.Dark.BgInput;
                foreach (Control child in btnEmoji.Controls)
                    child.BackColor = ThemeConstants.Dark.BgInput;
            }
            else
            {
//                 DebugLogger.Log("[ChatPanel] ApplyTheme: Applying light mode colors");
                this.BackColor = ThemeConstants.Light.BgBase;
                flowMessages.BackColor = ThemeConstants.Light.BgSurface;
                txtInput.BackColor = ThemeConstants.Light.BgInput;
                txtInput.ForeColor = ThemeConstants.Light.TextPrimary;
                lblTitle.ForeColor = ThemeConstants.Light.TextSecondary;  // calm, not coral
                btnEmoji.BackColor = ThemeConstants.Light.BgElevated;
                foreach (Control child in btnEmoji.Controls)
                    child.BackColor = ThemeConstants.Light.BgElevated;
            }
//             DebugLogger.Log("[ChatPanel] ApplyTheme: Re-rendering chat");
            RenderChat();
        }

        /// <summary>
        /// SetChatFontSize: Change chat message font size to Small, Medium, or Big.
        /// Updates font sizes and card heights, then re-renders chat.
        /// Called from settings panel when user changes font size preference.
        /// </summary>
        public void SetChatFontSize(string sizeName)
        {
//             DebugLogger.Log($"[ChatPanel] SetChatFontSize: Changing to size={sizeName}");

            _chatFontSizeName = sizeName;
            switch (sizeName)
            {
                case "Medium":
//                     DebugLogger.Log("[ChatPanel] SetChatFontSize: Setting to Medium (10pt, height=28)");
                    _nameFontSize = 10f;
                    _msgFontSize = 10f;
                    _cardBaseHeight = 28;
                    break;
                case "Big":
//                     DebugLogger.Log("[ChatPanel] SetChatFontSize: Setting to Big (12pt, height=34)");
                    _nameFontSize = 12f;
                    _msgFontSize = 12f;
                    _cardBaseHeight = 34;
                    break;
                default: // "Small"
//                     DebugLogger.Log("[ChatPanel] SetChatFontSize: Setting to Small (8.5pt, height=22)");
                    _nameFontSize = 8.5f;
                    _msgFontSize = 8.5f;
                    _cardBaseHeight = 22;
                    break;
            }
//             DebugLogger.Log("[ChatPanel] SetChatFontSize: Re-rendering chat");
            _lastRenderHash = ""; // force re-render
            RenderChat();
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  EmojiRichTextBox — CUSTOM RICHTEXTBOX WITH COLOR EMOJI SUPPORT
    //  Attempts to use RichEdit50W from msftedit.dll (Windows 10+) for
    //  full-color emoji rendering. If the .NET Framework throws a
    //  "Class already exists" error (known issue), it automatically falls
    //  back to the default RichEdit20W control.
    // ════════════════════════════════════════════════════════════════════════════
    internal class EmojiRichTextBox : RichTextBox
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("user32.dll")]
        private static extern bool HideCaret(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // EM_SETTYPOGRAPHYOPTIONS — enables advanced typography (DirectWrite color emoji)
        private const int EM_SETTYPOGRAPHYOPTIONS = 0x04CA;
        private const int TO_ADVANCEDTYPOGRAPHY = 1;

        private const bool DisableRichEdit50W = true;
        private static bool _libLoaded;
        private static bool _richEdit50Works = false;
        internal static bool NativeColorEmojiEnabled => _richEdit50Works;

        static EmojiRichTextBox()
        {
            // Load the modern RichEdit DLL once per process
            if (!_libLoaded)
            {
                if (!DisableRichEdit50W)
                {
                    IntPtr lib = LoadLibrary("msftedit.dll");
                    _richEdit50Works = lib != IntPtr.Zero;
                }
                _libLoaded = true;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                if (_richEdit50Works)
                    cp.ClassName = "RichEdit50W"; // Color emoji support
                return cp;
            }
        }

        protected override void CreateHandle()
        {
            try
            {
                base.CreateHandle();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Fallback path for systems where RichEdit50W is unavailable or blocked.
                if (!_richEdit50Works) throw;
                _richEdit50Works = false;
                base.CreateHandle();
            }
        }

        /// <summary>
        /// After the native window handle is created, enable advanced typography
        /// so RichEdit50W uses DirectWrite to render full-color emojis natively.
        /// This makes "Segoe UI Emoji" render color emojis without needing PNG insertion.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (_richEdit50Works && IsHandleCreated)
            {
                SendMessage(Handle, EM_SETTYPOGRAPHYOPTIONS,
                    (IntPtr)TO_ADVANCEDTYPOGRAPHY, (IntPtr)TO_ADVANCEDTYPOGRAPHY);
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        //  CreateEmojiButton — REUSABLE COLOR EMOJI BUTTON
        //  Creates a Panel+EmojiRichTextBox combo that renders emojis in full color
        //  via RichEdit50W, styled as a clickable button with hover effects.
        // ════════════════════════════════════════════════════════════════════════════
        internal static Panel CreateEmojiButton(string emoji, int width, int height, float fontSize,
            Color bgColor, Color hoverColor, Action onClick, Padding? margin = null)
        {
            var panel = new Panel
            {
                Width = width,
                Height = height,
                BackColor = bgColor,
                Cursor = Cursors.Hand,
                Margin = margin ?? new Padding(2)
            };

            var rtb = new EmojiRichTextBox
            {
                Text = emoji,
                Font = new Font("Segoe UI Emoji", fontSize),
                BackColor = bgColor,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.None,
                TabStop = false,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill
            };

            // Center the emoji text
            rtb.SelectAll();
            rtb.SelectionAlignment = HorizontalAlignment.Center;
            rtb.DeselectAll();

            // Hide caret and prevent text selection visual
            rtb.GotFocus += (s, e) => { HideCaret(rtb.Handle); };
            rtb.SelectionChanged += (s, e) => { if (rtb.SelectionLength > 0) rtb.SelectionLength = 0; };

            // Hover effects on both panel and rtb
            EventHandler enterHandler = (s, e) => { panel.BackColor = hoverColor; rtb.BackColor = hoverColor; };
            EventHandler leaveHandler = (s, e) => { panel.BackColor = bgColor; rtb.BackColor = bgColor; };
            rtb.MouseEnter += enterHandler;
            rtb.MouseLeave += leaveHandler;
            panel.MouseEnter += enterHandler;
            panel.MouseLeave += leaveHandler;

            // Click handler — fires the action on mouse click
            rtb.MouseClick += (s, e) => onClick();

            panel.Controls.Add(rtb);
            return panel;
        }
    }
}
