// â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—
// â•‘                        8 BIT LAB ENGINEERING                               â•‘
// â•‘                     WORKFLOW - TEAM TIME TRACKER                            â•‘
// â•‘                                                                            â•‘
// â•‘  FILE:        OnlineUserControl.cs                                         â•‘
// â•‘  PURPOSE:     USER STATUS CARD WITH AVATAR, STATUS DOT & CONTEXT MENU      â•‘
// â•‘  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         â•‘
// â•‘  LICENSE:     OPEN SOURCE                                                  â•‘
// â•‘                                                                            â•‘
// â•‘  DESCRIPTION:                                                              â•‘
// â•‘  Custom WinForms UserControl that displays a single team member's           â•‘
// â•‘  online status in the right sidebar "Team Status" panel.                   â•‘
// â•‘                                                                            â•‘
// â•‘  VISUAL LAYOUT:                                                            â•‘
// â•‘    [AVATAR CIRCLE] [STATUS DOT]                                            â•‘
// â•‘      Username (bold)                                                       â•‘
// â•‘      Status description (italic)                                           â•‘
// â•‘      Role / Title (small)                                                  â•‘
// â•‘                                                                            â•‘
// â•‘  FEATURES:                                                                 â•‘
// â•‘  - AVATAR: Colored circle with user's first initial letter                 â•‘
// â•‘  - STATUS DOT: Green=Working, Blue=Online, Gray=Offline                    â•‘
// â•‘  - RIGHT-CLICK MENU: Send DM, Mute, Kick, Make Assistant Admin             â•‘
// â•‘  - ROLE DISPLAY: Shows job title/role under the username                   â•‘
// â•‘  - IDLE DETECTION: 7+ days offline = light blue, 14+ days = dimmed          â•‘
// â•‘                                                                            â•‘
// â•‘  GitHub: https://github.com/8BitLabEngineering                             â•‘
// â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // CLASS: OnlineUserControl
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //
    // PURPOSE:
    // This custom UserControl renders a compact STATUS CARD for ONE team member in the
    // right sidebar "Team Status" panel. It shows the user's avatar, online status,
    // and provides a context menu for admin actions (DM, mute, kick, promote).
    //
    // VISUAL COMPONENTS:
    // 1. AVATAR PANEL: 32x32 colored circle with user's first initial
    // 2. STATUS DOT: 10x10 colored dot (bottom-right of avatar) showing online state
    // 3. USERNAME LABEL: User's name (green if admin, white/gray otherwise)
    // 4. DESCRIPTION LABEL: "Working...", "Online â€” idle", or empty
    // 5. ROLE LABEL: Job title or role (e.g., "Manager", "Developer")
    //
    // VISIBILITY LOGIC:
    // - WORKING: Fully visible, bright green dot
    // - ONLINE: Visible, blue dot (idle on desktop, no active window)
    // - OFFLINE: Visible but grayed out (always shown so team list is complete)
    // - WEEK IDLE (7-14 days): Visible with light blue dot
    // - LONG IDLE (14+ days): Visible but dimmed
    //
    // SIZE & POSITION:
    // - Control dimensions: 290W x 58H pixels
    // - Avatar: 4px from top-left corner
    // - Labels: Text starts at 42px (right of avatar)
    //
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    public class OnlineUserControl : UserControl
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PROPERTIES: User Information & Permissions
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        // USERINFO: The user object containing Name, Role, Title, Avatar color, etc.
        public UserInfo UserInfo { get; private set; }

        // ISCURRENTUSER: True if this control represents the logged-in user
        // (prevents sending DMs to yourself, shows your own status differently)
        public bool IsCurrentUser { get; private set; }

        // VIEWERISADMIN: True if the person viewing this control has admin permissions
        // (enables Mute, Kick, Promote options in context menu)
        public bool ViewerIsAdmin { get; private set; }

        // VIEWERNAME: Name of the logged-in user viewing this control
        // (used for favorites management in context menu)
        public string ViewerName { get; set; }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // UI COMPONENTS: Child Panels & Labels
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        // AVATARPANEL: 32x32 transparent panel that draws the colored circle + initial
        // Uses custom Paint event handler to render with GDI+ (anti-aliased)
        private Panel avatarPanel;

        // STATUSDOT: 10x10 transparent panel that draws the colored status indicator
        // Positioned at bottom-right corner of avatar (offset 26, 26)
        // Color indicates: Green=Working, Blue=Online, Light Blue=Week Idle, Gray=Offline
        private Panel statusDot;

        // LABELNAME: Bold, 10pt username label
        // Includes "â˜… " prefix if user is admin
        private Label labelName;

        // LABELDESCRIPTION: Italic, 8.5pt status text
        // Shows "Working..." when active, "Online â€” idle" when idle, or empty
        private Label labelDescription;

        // LABELROLE: Regular, 8pt role/title label
        // Displays job title from UserInfo.Role or UserInfo.Title
        private Label labelRole;

        // CONTEXTMENU: Right-click menu with options: DM, Mute, Kick, Promote
        // Visibility of options depends on ViewerIsAdmin and IsCurrentUser flags
        private ContextMenuStrip contextMenu;

        // PROGRESS BAR: Weekly hours progress bar below user info
        private Panel progressBarBg;
        private Panel progressBarFill;
        private Panel progressBarHoliday;  // Blue section for holiday hours
        private Label labelHours;           // e.g. "7.5h / 10h"
        private Label labelLocalTime;       // e.g. "14:32 DE"
        private Timer _progressPulseTimer;
        private int _progressPulseOffset = 0;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // STATE VARIABLES: Current Status & Weekly Hours
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        // _ISONLINE: Cached flag indicating if status != "Offline"
        // Used for quick lookups and theme application
        private bool _isOnline = false;

        // _STATUS: Current status string ("Working", "Online", "Offline")
        // Drives visibility, color changes, and idle detection logic
        private string _status = "Offline";
        private bool _isInOtherProject = false; // True when user is online but in a different team

        // WEEKLY HOURS: Tracked hours and holiday hours for progress bar
        private double _weeklyHours = 0;
        private double _holidayHours = 0;
        private double _weeklyLimit = 10;
        private bool _isDarkMode = true;
        private Image _avatarImage;
        private string _avatarBase64Cache = string.Empty;
        private DateTime _lastAvatarRefresh = DateTime.MinValue;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // EVENTS: Fired by Context Menu for Form1 to Handle
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // These events are fired when user right-clicks the control and selects an
        // option. The parent Form1 subscribes to these and handles the actual action
        // (e.g., opening DM window, sending API request to mute, etc.)
        //
        // USAGE:
        // Form1 subscribes: control.OnSendDirectMessage += (name) => { ... }
        // This control fires: OnSendDirectMessage?.Invoke(UserInfo.Name);
        //
        // THREAD SAFETY:
        // Events are fired on UI thread (from Paint/Paint handlers)
        // No async/threading issues expected

        // EVENT: User clicked "Send Direct Message" - passes username
        public event Action<string> OnSendDirectMessage;

        // EVENT: User clicked "Mute User" (admin only) - passes username
        public event Action<string> OnMuteUser;

        // EVENT: User clicked "Kick User" (admin only) - passes username
        public event Action<string> OnKickUser;

        // EVENT: User clicked "User Check" (admin only) - passes username
        public event Action<string> OnSecurityCheckUser;

        // EVENT: User clicked "Make Assistant Admin" (admin only) - passes username
        public event Action<string> OnMakeAssistantAdmin;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // IDLE DETECTION: Automatic Visibility Based on Time
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // Single source of truth for when user was last seen online.
        // Updated whenever SetStatus("Offline", ...) is called.
        // Used to calculate week idle & long idle states.
        //
        public DateTime LastSeen { get; set; } = DateTime.Now;

        // ISWEEKIDLE: True if OFFLINE for 7+ days (shows light blue dot)
        // Logic: status is "Offline" AND (now - LastSeen) >= 7 days
        // Display: Control stays VISIBLE but with light blue status dot
        private bool IsWeekIdle =>
            _status == "Offline" &&
            LastSeen != default &&
            (DateTime.Now - LastSeen).TotalDays >= 7;

        // ISLONGIDLE: True if OFFLINE for 14+ days (control hidden completely)
        // Logic: status is "Offline" AND (now - LastSeen) >= 14 days
        // Display: Control is HIDDEN (this.Visible = false)
        // Purpose: Declutter UI from inactive team members over 2 weeks
        private bool IsLongIdle =>
            _status == "Offline" &&
            LastSeen != default &&
            (DateTime.Now - LastSeen).TotalDays >= 14;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // CONSTRUCTOR: Initialize Control & All Child Components
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // PARAMETERS:
        // - user: UserInfo object containing Name, Role, Title, Avatar color
        // - isCurrentUser: True if this control represents the logged-in user
        //                  (hides "Send DM" option, may show different styling)
        // - viewerIsAdmin: True if the viewer has admin permissions
        //                  (enables Mute, Kick, Promote options)
        //
        // BEHAVIOR:
        // 1. Stores user info & permission flags
        // 2. Sets control size (290x58) and transparent background
        // 3. Creates & configures all child components (panels, labels)
        // 4. Hooks Paint events for custom GDI+ rendering (avatar, status dot)
        // 5. Creates context menu with appropriate option visibility
        // 6. Initially VISIBLE (shows as "Offline" until status is refreshed)
        //
        public OnlineUserControl(UserInfo user, bool isCurrentUser = false, bool viewerIsAdmin = false)
        {
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // STORE PARAMETERS AS PROPERTIES
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            UserInfo = user;
            IsCurrentUser = isCurrentUser;
            ViewerIsAdmin = viewerIsAdmin;

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // CONFIGURE CONTROL SIZE & APPEARANCE
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Fixed size: 290 pixels wide x 82 pixels tall (more breathing room)
            // Fits nicely in right sidebar above other controls
            this.Height = 82;
            this.Width = 290;

            // Transparent background allows parent panel color to show through
            this.BackColor = Color.Transparent;

            // SHOW ALL USERS IMMEDIATELY â€” offline users are grayed out, not hidden
            // This ensures the full team list is always visible from startup
            this.Visible = true;

            // Assign context menu (created below)
            this.ContextMenuStrip = CreateContextMenu();

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // CREATE AVATAR PANEL (32x32 colored circle + initial)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Position: (4, 4) - small margin from top-left
            // Size: 32x32 pixels (standard icon size)
            // Background: Transparent (Paint event draws circle)
            avatarPanel = new Panel
            {
                Size = new Size(42, 42),
                Location = new Point(8, 6),
                BackColor = Color.Transparent
            };

            // PAINT EVENT: Render avatar circle with initial
            // Hooked here to ensure smooth anti-aliased rendering
            // SmoothingMode.AntiAlias prevents jagged edges on circle
            avatarPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                DrawAvatar(e.Graphics);
            };

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // CREATE STATUS DOT PANEL (10x10 colored indicator)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Position: (26, 26) - bottom-right corner of avatar panel
            //           Creates small badge effect overlapping the avatar
            // Size: 10x10 pixels (small, round dot)
            // Background: Transparent (Paint event draws dot)
            statusDot = new Panel
            {
                Size = new Size(11, 11),
                Location = new Point(36, 36),
                BackColor = Color.Transparent
            };

            // PAINT EVENT: Render status dot with color & border
            // Color indicates: Green=Working, Blue=Online, Light Blue=Idle, Gray=Offline
            statusDot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                DrawStatusDot(e.Graphics);
            };

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // CREATE USERNAME LABEL (Bold, 10pt)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Displays user's name with optional "â˜… " prefix for admins
            // Position: (42, 4) - to the right of avatar, top-aligned
            // Font: Segoe UI, 10pt Bold
            // Color: Green if admin, dark gray otherwise (varies by online status)
            // Determine name color: use user's chosen color if set, else admin green / default gray
            Color nameColor;
            Color userCustomColor = user.GetDrawingColor(Color.Empty);
            if (userCustomColor != Color.Empty)
                nameColor = userCustomColor;
            else if (user.IsAdmin)
                nameColor = Color.FromArgb(100, 220, 120);
            else
                nameColor = Color.FromArgb(40, 40, 60);

            labelName = new Label
            {
                // Add star symbol (â˜…) before name if user is admin
                // Add "(You)" suffix if this is the current logged-in user
                Text = (user.IsAdmin ? "\u2605 " : "") + user.Name + (isCurrentUser ? "  (You)" : ""),
                Font = ThemeConstants.FontSubtitle,
                ForeColor = nameColor,
                Location = new Point(58, 6),
                AutoSize = true
            };

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // CREATE STATUS DESCRIPTION LABEL (Italic, 8.5pt)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Displays status text like "Working...", "Online â€” idle", or custom message
            // Position: (42, 22) - below username, same horizontal alignment
            // Font: Segoe UI, 8.5pt Italic (lighter font for secondary text)
            // Color: Gray by default, changes to green/blue based on status
            // MaxSize: 240px wide - truncates long descriptions with "..."
            labelDescription = new Label
            {
                Text = "",
                Font = ThemeConstants.FontSmall,  // Regular, not Italic â€” easier to read
                ForeColor = Color.FromArgb(100, 100, 120),
                Location = new Point(58, 24),
                AutoSize = true,
                MaximumSize = new Size(230, 0)
            };

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // CREATE ROLE/TITLE LABEL (Regular, 8pt)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Displays user's job title or role (e.g., "Manager", "Developer")
            // Position: (42, 40) - below description
            // Font: Segoe UI, 8pt Regular (smallest font, subtle)
            // Color: Light gray
            // MaxSize: 240px wide - wraps if needed
            labelRole = new Label
            {
                Text = "",
                Font = ThemeConstants.FontSmall,
                ForeColor = Color.FromArgb(120, 120, 140),
                Location = new Point(58, 42),
                AutoSize = true,
                MaximumSize = new Size(230, 0)
            };

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // ADD ALL CHILD CONTROLS TO THIS CONTROL
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Order matters for z-order (avatar first, status dot on top)
            this.Controls.Add(avatarPanel);
            this.Controls.Add(statusDot);
            this.Controls.Add(labelName);
            this.Controls.Add(labelDescription);
            this.Controls.Add(labelRole);

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // CREATE LOCAL TIME LABEL (top-right corner)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            labelLocalTime = new Label
            {
                Text = "",
                Font = ThemeConstants.FontCaption,
                ForeColor = Color.FromArgb(140, 150, 170),
                Location = new Point(210, 6),
                AutoSize = true,
                TextAlign = ContentAlignment.TopRight
            };
            this.Controls.Add(labelLocalTime);

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // CREATE WEEKLY HOURS PROGRESS BAR (below labels at y=56)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var barLeft = 58;
            var barWidth = 190;
            var barTop = 60;
            var barHeight = 9;  // thicker and easier to read

            // Background bar (gray track)
            progressBarBg = new Panel
            {
                Location = new Point(barLeft, barTop),
                Size = new Size(barWidth, barHeight),
                BackColor = Color.FromArgb(36, 42, 54)  // matches BgInput
            };
            progressBarBg.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = ThemeConstants.RoundedRect(new Rectangle(0, 0, progressBarBg.Width - 1, progressBarBg.Height - 1), progressBarBg.Height / 2))
                using (var brush = new SolidBrush(progressBarBg.BackColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
            };

            // Holiday hours section (blue, drawn first)
            progressBarHoliday = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(0, barHeight),
                BackColor = Color.FromArgb(52, 152, 219) // Blue for holidays
            };
            progressBarBg.Controls.Add(progressBarHoliday);

            // Filled progress bar (gradient color based on hours)
            progressBarFill = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(0, barHeight),
                BackColor = Color.FromArgb(120, 130, 145) // Gray default
            };
            progressBarFill.Paint += (s, e) =>
            {
                if (progressBarFill.Width <= 0) return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = ThemeConstants.RoundedRect(new Rectangle(0, 0, progressBarFill.Width - 1, progressBarFill.Height - 1), progressBarFill.Height / 2))
                using (var baseBrush = new SolidBrush(progressBarFill.BackColor))
                {
                    e.Graphics.FillPath(baseBrush, path);
                    e.Graphics.SetClip(path);

                    int sheenWidth = Math.Max(10, progressBarFill.Width / 5);
                    int sheenX = _progressPulseOffset - sheenWidth;
                    using (var shineBrush = new LinearGradientBrush(
                        new Rectangle(sheenX, 0, sheenWidth, progressBarFill.Height),
                        Color.FromArgb(0, 255, 255, 255),
                        Color.FromArgb(90, 255, 255, 255),
                        LinearGradientMode.Horizontal))
                    {
                        e.Graphics.FillRectangle(shineBrush, sheenX, 0, sheenWidth, progressBarFill.Height);
                    }

                    e.Graphics.ResetClip();
                }
            };
            progressBarBg.Controls.Add(progressBarFill);

            // Hours label (right of bar)
            labelHours = new Label
            {
                Text = "",
                Font = ThemeConstants.FontCaption,
                ForeColor = Color.FromArgb(140, 150, 170),
                Location = new Point(barLeft + barWidth + 4, barTop - 1),
                AutoSize = true
            };

            this.Controls.Add(progressBarBg);
            this.Controls.Add(labelHours);

            // Initialize weekly limit from user data
            _weeklyLimit = user.WeeklyHourLimit > 0 ? user.WeeklyHourLimit : 10;

            _progressPulseTimer = new Timer { Interval = 40 };
            _progressPulseTimer.Tick += (s, e) =>
            {
                _progressPulseOffset += 4;
                int cycleWidth = Math.Max(progressBarFill.Width + 24, 60);
                if (_progressPulseOffset > cycleWidth)
                    _progressPulseOffset = 0;
                progressBarFill.Invalidate();
            };
            _progressPulseTimer.Start();

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // PAINT: Bottom separator line + current-user accent highlight
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            this.Paint += (s, e) =>
            {
                // Bottom divider line between user cards
                using (var pen = new Pen(ThemeConstants.Dark.Divider))
                    e.Graphics.DrawLine(pen, 8, this.Height - 1, this.Width - 8, this.Height - 1);

                // Current user: subtle left accent bar
                if (IsCurrentUser)
                {
                    using (var brush = new SolidBrush(ThemeConstants.Dark.AccentPrimary))
                        e.Graphics.FillRectangle(brush, 0, 8, 3, this.Height - 16);
                }
            };

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // DOUBLE-CLICK HANDLER â€” opens Direct Message chat window
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Double-clicking anywhere on the user card opens a DM chat (like WhatsApp).
            // Fires the same OnSendDirectMessage event as the context menu option.
            // Disabled for the current user (can't DM yourself).
            if (!IsCurrentUser)
            {
                this.Cursor = Cursors.Hand;
                EventHandler doubleClickHandler = (s, e) => OnSendDirectMessage?.Invoke(UserInfo.Name);
                this.DoubleClick += doubleClickHandler;
                avatarPanel.DoubleClick += doubleClickHandler;
                labelName.DoubleClick += doubleClickHandler;
                labelDescription.DoubleClick += doubleClickHandler;
                labelRole.DoubleClick += doubleClickHandler;
                statusDot.DoubleClick += doubleClickHandler;
            }

            RefreshAvatarFromTeamMeta(true);
            this.Disposed += (s, e) =>
            {
                if (_avatarImage != null)
                {
                    _avatarImage.Dispose();
                    _avatarImage = null;
                }
            };
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // METHOD: DrawAvatar
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // PURPOSE:
        // Render a 32x32 colored circle with the user's first initial letter.
        // Called by avatarPanel.Paint event on every refresh.
        //
        // HOW IT WORKS:
        // 1. DEFINE RECTANGLE: 32x32 bounding box for the circle
        // 2. GET COLOR: Call UserInfo.GetDrawingColor() for avatar background color
        //    (Each user has a unique color assigned by their hash or ID)
        // 3. DRAW CIRCLE: Use Graphics.FillEllipse() to draw colored circle
        //    (Anti-aliased by SmoothingMode.AntiAlias set in Paint handler)
        // 4. DRAW INITIAL: Measure text, center it, draw white letter on colored bg
        //    (Font: Segoe UI 14pt Bold, Color: White for contrast)
        //
        // THREAD SAFETY:
        // Called from UI thread Paint event, GDI+ is thread-safe for graphics context
        //
        // PARAMETERS:
        // - g: Graphics object from Paint event (already configured for antialiasing)
        //
        private void RefreshAvatarFromTeamMeta(bool force = false)
        {
            if (UserInfo == null || string.IsNullOrWhiteSpace(UserInfo.Name))
                return;

            if (!force && (DateTime.UtcNow - _lastAvatarRefresh).TotalSeconds < 5)
                return;

            _lastAvatarRefresh = DateTime.UtcNow;

            string avatarBase64 = string.Empty;
            try
            {
                var team = UserStorage.LoadTeam();
                if (team?.MembersMeta != null && team.MembersMeta.TryGetValue(UserInfo.Name, out var meta))
                    avatarBase64 = meta?.AvatarBase64 ?? string.Empty;
            }
            catch
            {
            }

            if (string.Equals(avatarBase64, _avatarBase64Cache, StringComparison.Ordinal))
                return;

            _avatarBase64Cache = avatarBase64 ?? string.Empty;

            Image nextImage = null;
            if (!string.IsNullOrWhiteSpace(avatarBase64))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(avatarBase64);
                    using (var ms = new MemoryStream(bytes))
                    using (var raw = Image.FromStream(ms))
                    {
                        nextImage = new Bitmap(raw);
                    }
                }
                catch
                {
                    nextImage = null;
                }
            }

            var oldImage = _avatarImage;
            _avatarImage = nextImage;
            oldImage?.Dispose();

            avatarPanel?.Invalidate();
        }

        private void DrawAvatar(Graphics g)
        {
//             DebugLogger.Log("[OnlineUser] DrawAvatar() rendering avatar circle");
            // Define bounding rectangle for the circular avatar
            int avatarW = Math.Max(1, avatarPanel.Width - 1);
            int avatarH = Math.Max(1, avatarPanel.Height - 1);
            Rectangle avatarRect = new Rectangle(0, 0, avatarW, avatarH);

            // Only draw if UserInfo is available and has a name
            if (UserInfo != null && !string.IsNullOrEmpty(UserInfo.Name))
            {
                if (_avatarImage != null)
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(avatarRect);
                        var oldClip = g.Clip;
                        g.SetClip(path);
                        g.DrawImage(_avatarImage, avatarRect);
                        g.Clip = oldClip;
                    }

                    using (var pen = new Pen(Color.FromArgb(110, 255, 255, 255), 1f))
                        g.DrawEllipse(pen, avatarRect);
                    return;
                }

                // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // DRAW COLORED BACKGROUND CIRCLE
                // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // GET COLOR: UserInfo.GetDrawingColor() returns a unique color
                // based on user's ID/hash. Falls back to gray if not available.
                // DRAW: FillEllipse() draws a filled circle (anti-aliased)
                Color bgColor = UserInfo.GetDrawingColor(Color.FromArgb(100, 100, 120));
                using (var brush = new SolidBrush(bgColor))
                {
                    g.FillEllipse(brush, avatarRect);
                }

                // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // DRAW INITIAL LETTER IN CENTER
                // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // GET INITIAL: First character of name, uppercase
                //              Fallback to "?" if name is empty
                // MEASURE: Get width/height of rendered text
                // CENTER: Calculate x,y to position letter in middle of circle
                // DRAW: DrawString() renders white text on colored background
                string initial = UserInfo.Name.Length > 0 ? UserInfo.Name[0].ToString().ToUpper() : "?";
                using (var font = new System.Drawing.Font("Segoe UI", 13.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var textBrush = new SolidBrush(Color.White))
                using (var textFormat = new StringFormat())
                {
                    textFormat.Alignment = StringAlignment.Center;
                    textFormat.LineAlignment = StringAlignment.Center;
                    textFormat.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(initial, font, textBrush, avatarRect, textFormat);
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // METHOD: DrawStatusDot
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // PURPOSE:
        // Render a 10x10 colored dot indicator showing user's online status.
        // Called by statusDot.Paint event on every refresh.
        //
        // STATUS COLORS:
        // â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
        // â”‚ STATUS        â”‚ CONDITION              â”‚ COLOR              â”‚ CODE  â”‚
        // â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
        // â”‚ WORKING       â”‚ _status == "Working"   â”‚ Bright Green       â”‚ #22C5 â”‚
        // â”‚ ONLINE        â”‚ _status == "Online"    â”‚ Bright Blue        â”‚ #3B82 â”‚
        // â”‚ WEEK IDLE     â”‚ Offline 7-14 days      â”‚ Light Blue         â”‚ #93C5 â”‚
        // â”‚ LONG IDLE     â”‚ Offline 14+ days       â”‚ Dark Gray (hidden) â”‚ #7882 â”‚
        // â”‚ OFFLINE       â”‚ Recent offline         â”‚ Dark Gray          â”‚ #7882 â”‚
        // â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
        //
        // HOW IT WORKS:
        // 1. DETERMINE COLOR: Check _status and idle flags to pick color
        // 2. DRAW CIRCLE: FillEllipse() renders 10x10 colored dot
        // 3. DRAW BORDER: DrawEllipse() adds black border for definition
        //
        // NOTE: IsWeekIdle and IsLongIdle check LastSeen timestamp
        //
        // THREAD SAFETY:
        // Called from UI thread Paint event
        //
        // PARAMETERS:
        // - g: Graphics object from Paint event (configured for antialiasing)
        //
        private void DrawStatusDot(Graphics g)
        {
//             DebugLogger.Log("[OnlineUser] DrawStatusDot() status=" + _status);
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // DETERMINE DOT COLOR BASED ON STATUS & IDLE STATE
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Color dotColor;
            if (_status == "Working")
            {
                // WORKING: Bright green - user actively working on something
                dotColor = Color.FromArgb(34, 197, 94);       // RGB: 34, 197, 94
            }
            else if (_status == "Online" && _isInOtherProject)
            {
                // ONLINE (OTHER PROJECT): Purple - user is online but in a different team
                dotColor = Color.FromArgb(168, 139, 250);     // RGB: 168, 139, 250
            }
            else if (_status == "Online")
            {
                // ONLINE: Bright blue - user online but idle (no active window)
                dotColor = Color.FromArgb(59, 130, 246);      // RGB: 59, 130, 246
            }
            else if (IsLongIdle)
            {
                // LONG IDLE (14+ days offline): Gray - shown but hidden anyway by Visible=false
                dotColor = Color.FromArgb(120, 130, 145);     // RGB: 120, 130, 145
            }
            else if (IsWeekIdle)
            {
                // WEEK IDLE (7-14 days offline): Light blue - subdued indicator
                // Shows user is inactive but was recently seen
                dotColor = Color.FromArgb(147, 197, 253);     // RGB: 147, 197, 253
            }
            else
            {
                // OFFLINE (< 7 days): Gray - standard offline color
                dotColor = Color.FromArgb(120, 130, 145);     // RGB: 120, 130, 145
            }

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // DRAW FILLED DOT (10x10 circle)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            using (var brush = new SolidBrush(dotColor))
            {
                // FillEllipse at (0,0) with 9x9 size (respects 10x10 panel bounds)
                // Anti-aliased by SmoothingMode.AntiAlias in Paint event
                g.FillEllipse(brush, 0, 0, 10, 10);
            }

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // DRAW BLACK BORDER FOR DEFINITION
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Thin black outline makes dot visible against any background
            using (var pen = new Pen(Color.Black, 1))
            {
                g.DrawEllipse(pen, 0, 0, 10, 10);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // METHOD: CreateContextMenu
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // PURPOSE:
        // Build the right-click context menu with options for:
        // - Send Direct Message
        // - Mute User (admin only)
        // - Kick User (admin only)
        // - Make Assistant Admin (admin only)
        //
        // MENU ITEMS & VISIBILITY RULES:
        // â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
        // â”‚ OPTION                    â”‚ ENABLED IF                  â”‚
        // â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
        // â”‚ ðŸ’¬ Send Direct Message    â”‚ NOT IsCurrentUser           â”‚
        // â”‚ ðŸ”‡ Mute User              â”‚ ViewerIsAdmin AND NOT self  â”‚
        // â”‚ ðŸ¦¶ Kick User              â”‚ ViewerIsAdmin AND NOT self  â”‚
        // â”‚ â­ Make Assistant Admin   â”‚ ViewerIsAdmin AND NOT self  â”‚
        // â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
        //
        // EVENTS:
        // Each menu item's Click event fires the corresponding event with username:
        // - OnSendDirectMessage?.Invoke(UserInfo.Name)
        // - OnMuteUser?.Invoke(UserInfo.Name)
        // - OnKickUser?.Invoke(UserInfo.Name)
        // - OnMakeAssistantAdmin?.Invoke(UserInfo.Name)
        //
        // Form1 subscribes to these events and handles the actual action.
        //
        // RETURNS:
        // ContextMenuStrip with all items configured and assigned to this control
        //
        private ContextMenuStrip CreateContextMenu()
        {
            // Create new context menu container
            var menu = new ContextMenuStrip();

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // MENU ITEM 1: SEND DIRECT MESSAGE
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // ENABLED: Always enabled UNLESS IsCurrentUser=true (can't DM yourself)
            // EVENT: Fires OnSendDirectMessage with username
            // ICON: ðŸ’¬ (message bubble)
            var sendDmItem = new ToolStripMenuItem("Send Direct Message");

            // Wire Click event to fire the OnSendDirectMessage event
            // Passes the username to the subscriber (Form1)
            sendDmItem.Click += (s, e) => OnSendDirectMessage?.Invoke(UserInfo.Name);

            // Disable for current user (can't message yourself)
            if (IsCurrentUser) sendDmItem.Enabled = false;

            menu.Items.Add(sendDmItem);

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // MENU ITEM: ADD / REMOVE FAVORITE
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // ENABLED: Always enabled UNLESS IsCurrentUser=true
            // ICON: â­ (star)
            // PURPOSE: Quick add/remove user from favorites list
            if (!IsCurrentUser)
            {
                var favItem = new ToolStripMenuItem("Add to Favorites");
                favItem.Click += (s, e) =>
                {
                    if (string.IsNullOrEmpty(ViewerName)) return;
                    var favs = UserStorage.GetFavoriteUsers(ViewerName);
                    if (favs.Any(f => f.Equals(UserInfo.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        favs.RemoveAll(f => f.Equals(UserInfo.Name, StringComparison.OrdinalIgnoreCase));
                        UserStorage.SaveFavoriteUsers(ViewerName, favs);
                    }
                    else
                    {
                        favs.Add(UserInfo.Name);
                        UserStorage.SaveFavoriteUsers(ViewerName, favs);
                    }
                };
                menu.Items.Add(favItem);

                // Update text dynamically when menu opens (ViewerName may not be set at construction time)
                menu.Opening += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(ViewerName))
                    {
                        var favs = UserStorage.GetFavoriteUsers(ViewerName);
                        bool isFav = favs.Any(f => f.Equals(UserInfo.Name, StringComparison.OrdinalIgnoreCase));
                        favItem.Text = isFav ? "Remove from Favorites" : "Add to Favorites";
                    }
                };
            }

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // MENU SEPARATOR
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Visual divider between regular action (DM) and admin actions
            menu.Items.Add(new ToolStripSeparator());

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // MENU ITEM 2: MUTE USER (ADMIN ONLY)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // ENABLED: ViewerIsAdmin=true AND NOT IsCurrentUser
            // EVENT: Fires OnMuteUser with username
            // ICON: ðŸ”‡ (muted speaker)
            // PURPOSE: Silences notifications/messages from this user
            var muteItem = new ToolStripMenuItem("Mute User");
            muteItem.Click += (s, e) => OnMuteUser?.Invoke(UserInfo.Name);
            muteItem.Enabled = ViewerIsAdmin && !IsCurrentUser;
            menu.Items.Add(muteItem);

            var securityItem = new ToolStripMenuItem("User Check");
            securityItem.Click += (s, e) => OnSecurityCheckUser?.Invoke(UserInfo.Name);
            securityItem.Enabled = ViewerIsAdmin && !IsCurrentUser;
            menu.Items.Add(securityItem);

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // MENU ITEM 3: KICK USER (ADMIN ONLY)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // ENABLED: ViewerIsAdmin=true AND NOT IsCurrentUser
            // EVENT: Fires OnKickUser with username
            // ICON: ðŸ¦¶ (foot/boot)
            // PURPOSE: Removes user from team/workspace
            var kickItem = new ToolStripMenuItem("Kick User");
            kickItem.Click += (s, e) => OnKickUser?.Invoke(UserInfo.Name);
            kickItem.Enabled = ViewerIsAdmin && !IsCurrentUser;
            menu.Items.Add(kickItem);

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // MENU ITEM 4: MAKE ASSISTANT ADMIN (ADMIN ONLY)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // ENABLED: ViewerIsAdmin=true AND NOT IsCurrentUser
            // EVENT: Fires OnMakeAssistantAdmin with username
            // ICON: â­ (star)
            // PURPOSE: Promotes user to admin role (grants Mute/Kick/Promote permissions)
            var adminItem = new ToolStripMenuItem("Make Assistant Admin");
            adminItem.Click += (s, e) => OnMakeAssistantAdmin?.Invoke(UserInfo.Name);
            adminItem.Enabled = ViewerIsAdmin && !IsCurrentUser;
            menu.Items.Add(adminItem);

            // Return configured menu (assigned to this.ContextMenuStrip in constructor)
            return menu;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // METHOD: SetStatus (PUBLIC)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // PURPOSE:
        // Update the user's status and refresh all visual elements.
        // Thread-safe using InvokeRequired pattern.
        //
        // CALLED FROM:
        // - Form1 when user status changes (e.g., window focus, timer ticks)
        // - Background worker threads monitoring system state
        //
        // PARAMETERS:
        // - status: "Working", "Online", or "Offline"
        // - description: Custom status text (e.g., "Working on Project X")
        //               Truncated to 50 chars with "..." if longer
        //
        // BEHAVIOR:
        // 1. Update internal _status and _isOnline flags
        // 2. Record LastSeen timestamp if going offline (for idle detection)
        // 3. Call SetStatusInternal() on UI thread (thread-safe)
        // 4. Update role display visibility
        //
        // THREAD SAFETY:
        // Checks InvokeRequired to determine if already on UI thread
        // If not, uses Invoke() to marshal call to UI thread
        // This allows background workers to update status safely
        //
        // EXAMPLE:
        // // From a background worker thread:
        // control.SetStatus("Working", "Implementing login feature");
        //
        // // From UI thread (direct call is OK, but Invoke is safe either way):
        // control.SetStatus("Online", "");
        //
        public void SetStatus(string status, string description)
        {
//             DebugLogger.Log("[OnlineUser] SetStatus() called: user=" + UserInfo.Name + " status=" + status);
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // UPDATE INTERNAL STATE
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            _status = status;
            _isOnline = status != "Offline";

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // UPDATE LASTSEEN TIMESTAMP WHEN GOING OFFLINE
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Used for idle detection (IsWeekIdle, IsLongIdle properties)
            // Records the moment user transitioned from Online/Working to Offline
            if (status == "Offline")
                LastSeen = DateTime.Now;

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // THREAD-SAFE UI UPDATE USING INVOKEREQUIRED PATTERN
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // InvokeRequired: True if called from non-UI thread
            //                 False if called from UI thread
            //
            // PATTERN:
            // if (InvokeRequired) => Not on UI thread, use Invoke() to marshal to UI
            // else => Already on UI thread, call directly
            //
            // This allows safe calls from background workers without deadlock/crashes
            if (InvokeRequired)
                Invoke(new Action(() => SetStatusInternal(status, description)));
            else
                SetStatusInternal(status, description);

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // UPDATE ROLE DISPLAY (JOB TITLE VISIBILITY)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (InvokeRequired)
                Invoke(new Action(() => UpdateRoleDisplay()));
            else
                UpdateRoleDisplay();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // METHOD: UpdateRoleDisplay (INTERNAL)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // PURPOSE:
        // Show or hide the role/title label based on whether UserInfo has role data.
        //
        // BEHAVIOR:
        // - If UserInfo.Role or UserInfo.Title is not empty: Show label
        // - Otherwise: Hide label (Visible = false)
        //
        private void UpdateRoleDisplay()
        {
//             DebugLogger.Log("[OnlineUser] UpdateRoleDisplay() updating role display");
            if (UserInfo != null && (!string.IsNullOrWhiteSpace(UserInfo.Role) || !string.IsNullOrWhiteSpace(UserInfo.Title)))
            {
                // Use Role if available, otherwise Title
                labelRole.Text = !string.IsNullOrWhiteSpace(UserInfo.Role) ? UserInfo.Role : UserInfo.Title;
                labelRole.Visible = true;
            }
            else
            {
                // No role/title available, hide label
                labelRole.Text = "";
                labelRole.Visible = false;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // METHOD: SetStatusInternal (PRIVATE)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // PURPOSE:
        // Internal method that actually updates UI elements. Called by SetStatus()
        // on the UI thread, ensuring thread-safe access to GDI+ objects.
        //
        // VISIBILITY LOGIC:
        // â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
        // â”‚ STATUS        â”‚ CONTROL VISIBLE â”‚ NAME COLOR      â”‚ DESC TEXT       â”‚
        // â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
        // â”‚ WORKING       â”‚ Visible         â”‚ White/Green*    â”‚ "Working..." +  â”‚
        // â”‚               â”‚                 â”‚                 â”‚ custom desc     â”‚
        // â”‚ ONLINE        â”‚ Visible         â”‚ Light gray/Green*â”‚ "Online â€” idle" â”‚
        // â”‚ OFFLINE       â”‚ Visible (gray)  â”‚ Dimmed gray     â”‚ "Offline"       â”‚
        // â”‚ WEEK IDLE     â”‚ Visible         â”‚ Dimmed gray     â”‚ "Offline"       â”‚
        // â”‚ (7-14 days)   â”‚                 â”‚                 â”‚                 â”‚
        // â”‚ LONG IDLE     â”‚ Visible (dim)   â”‚ Very dimmed     â”‚ "Offline"       â”‚
        // â”‚ (14+ days)    â”‚                 â”‚                 â”‚                 â”‚
        // â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
        // * Green = admin, Gray/White = regular user
        //
        // INVALIDATION:
        // Calls avatarPanel.Invalidate() and statusDot.Invalidate()
        // Forces Paint event to fire, re-rendering avatar & status dot with new colors
        //
        // PARAMETERS:
        // - status: "Working", "Online", or "Offline"
        // - description: Custom text or empty string
        //
        private void SetStatusInternal(string status, string description)
        {
//             DebugLogger.Log("[OnlineUser] SetStatusInternal() updating UI for status=" + status);
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // UPDATE INTERNAL STATE & TRIGGER REDRAWS
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            _status = status;
            _isOnline = status != "Offline";
            _isInOtherProject = false; // Reset â€” will be set in Online handler if applicable
            RefreshAvatarFromTeamMeta();

            // Force avatar and status dot to redraw (fires Paint event)
            // Necessary because avatar color may change with online state
            avatarPanel.Invalidate();
            statusDot.Invalidate();

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // DEFINE ADMIN COLOR (LIGHT GREEN) - CONSISTENT ACROSS ALL STATES
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Admins always show light green username for visibility
            Color adminGreen = Color.FromArgb(100, 220, 120);
            // User's custom color (if set via profile settings)
            Color userColor = UserInfo.GetDrawingColor(Color.Empty);
            bool hasCustomColor = userColor != Color.Empty;

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // STATUS: WORKING
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Show control, bright colors (working is positive state)
            if (status == "Working")
            {
                this.Visible = true;

                // Name color: custom user color > admin green > bright white
                labelName.ForeColor = hasCustomColor ? userColor : (UserInfo.IsAdmin ? adminGreen : Color.FromArgb(236, 240, 241));

                // Description: "Working..." + custom description (truncated to 50 chars)
                labelDescription.Text = string.IsNullOrWhiteSpace(description)
                    ? "Working..."
                    : (description.Length > 50 ? description.Substring(0, 47) + "..." : description);

                // Description color: Green to match working state
                labelDescription.ForeColor = Color.FromArgb(34, 197, 94);
                labelDescription.Visible = true;
            }
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // STATUS: ONLINE
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Show control, blue colors (online but idle)
            else if (status == "Online")
            {
                this.Visible = true;

                // Name color: custom user color > admin green > light gray
                labelName.ForeColor = hasCustomColor ? userColor : (UserInfo.IsAdmin ? adminGreen : Color.FromArgb(200, 210, 220));

                // Description: check if user is in another project (cross-team)
                string descTrimmed = (description ?? "").Trim();
                _isInOtherProject = descTrimmed.StartsWith("(in ");
                if (_isInOtherProject)
                {
                    // CROSS-TEAM: user is online but working in a different project
                    labelDescription.Text = "Online \u2014 " + descTrimmed;
                    labelDescription.ForeColor = Color.FromArgb(168, 139, 250); // Purple â€” different project
                }
                else
                {
                    // SAME TEAM: user is online and idle here
                    labelDescription.Text = string.IsNullOrWhiteSpace(description)
                        ? "Online \u2014 idle"
                        : "Online \u2014 idle" + description;
                    labelDescription.ForeColor = Color.FromArgb(59, 130, 246); // Blue
                }
                labelDescription.Visible = true;
            }
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // STATUS: OFFLINE
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // ALWAYS VISIBLE â€” grayed out so full team list is always shown
            else // Offline
            {
                // SHOW OFFLINE USERS BUT GRAYED OUT SO TEAM LIST IS ALWAYS COMPLETE
                this.Visible = true;

                // Name color: dimmed custom color > dimmed admin green > dimmed gray
                if (hasCustomColor)
                {
                    // Dim the custom color for offline state
                    labelName.ForeColor = Color.FromArgb(
                        (int)(userColor.R * 0.5), (int)(userColor.G * 0.5), (int)(userColor.B * 0.5));
                }
                else
                    labelName.ForeColor = UserInfo.IsAdmin ? Color.FromArgb(60, 130, 70) : Color.FromArgb(100, 110, 120);

                // Description: show "Offline"
                labelDescription.Text = "Offline";
                labelDescription.ForeColor = Color.FromArgb(80, 90, 100);
                labelDescription.Visible = true;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // METHOD: SetWeeklyHours (PUBLIC)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // PURPOSE:
        // Update the weekly hours progress bar with current worked hours,
        // holiday hours, and the user's weekly limit.
        //
        // COLOR GRADIENT:
        //   0h        â†’ Gray      (not started)
        //   1h-target â†’ Green     (on track)
        //   target    â†’ Dark Green (reached limit)
        //   target+2  â†’ Orange    (slightly over)
        //   target+4+ â†’ Red/Dark Red (stress alert zone)
        //
        // HOLIDAY BAR:
        //   Blue section shows pre-paid holiday hours (already "earned" time)
        //
        public void SetWeeklyHours(double workedHours, double holidayHours)
        {
//             DebugLogger.Log("[OnlineUser] SetWeeklyHours() worked=" + workedHours + " holiday=" + holidayHours);
            _weeklyHours = workedHours;
            _holidayHours = holidayHours;
            _weeklyLimit = UserInfo.WeeklyHourLimit > 0 ? UserInfo.WeeklyHourLimit : 10;

            if (InvokeRequired)
                Invoke(new Action(() => UpdateProgressBar()));
            else
                UpdateProgressBar();
        }

        private void UpdateProgressBar()
        {
//             DebugLogger.Log("[OnlineUser] UpdateProgressBar() weekly hours progress");
            var totalBarWidth = progressBarBg.Width;
            var maxDisplay = Math.Max(_weeklyLimit * 1.5, _weeklyHours + 2); // Scale bar to show overtime

            // â”€â”€ Holiday section (blue) â”€â”€
            if (_holidayHours > 0)
            {
                int holidayWidth = (int)((_holidayHours / maxDisplay) * totalBarWidth);
                holidayWidth = Math.Min(holidayWidth, totalBarWidth);
                progressBarHoliday.Width = holidayWidth;
                progressBarHoliday.Visible = true;
            }
            else
            {
                progressBarHoliday.Width = 0;
                progressBarHoliday.Visible = false;
            }

            // â”€â”€ Worked hours section â”€â”€
            int workedWidth = (int)((_weeklyHours / maxDisplay) * totalBarWidth);
            workedWidth = Math.Min(workedWidth, totalBarWidth);
            progressBarFill.Location = new Point(progressBarHoliday.Width, 0);
            progressBarFill.Width = Math.Max(0, workedWidth);

            // â”€â”€ Color based on progress â”€â”€
            progressBarFill.BackColor = GetProgressColor(_weeklyHours, _weeklyLimit);

            // â”€â”€ Hours label â”€â”€
            labelHours.Text = $"{_weeklyHours:F1}h";
            labelHours.ForeColor = GetProgressColor(_weeklyHours, _weeklyLimit);

            progressBarBg.Invalidate();
        }

        /// <summary>
        /// Returns the progress bar color based on current hours vs limit.
        /// Gray â†’ Green â†’ Dark Green â†’ Orange â†’ Red â†’ Dark Red
        /// </summary>
        private static Color GetProgressColor(double hours, double limit)
        {
            if (hours <= 0)
                return Color.FromArgb(120, 130, 145);       // Gray â€” no hours yet

            double ratio = hours / limit;

            if (ratio < 0.5)
                return Color.FromArgb(46, 204, 113);        // Light Green â€” good progress
            if (ratio < 1.0)
                return Color.FromArgb(39, 174, 96);         // Green â€” on track
            if (ratio <= 1.0 + 0.001)
                return Color.FromArgb(30, 130, 76);         // Dark Green â€” target reached!
            if (ratio < 1.2)
                return Color.FromArgb(243, 156, 18);        // Orange â€” slightly over
            if (ratio < 1.4)
                return Color.FromArgb(231, 76, 60);         // Red â€” over limit
            return Color.FromArgb(192, 57, 43);             // Dark Red â€” stress zone!
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // METHOD: UpdateLocalTime (PUBLIC)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // PURPOSE:
        // Show the user's local time based on their country setting.
        // Called periodically by Form1's refresh timer.
        //
        public void UpdateLocalTime()
        {
//             DebugLogger.Log("[OnlineUser] UpdateLocalTime() country=" + (UserInfo?.Country ?? "null"));
            string countryCode = UserInfo?.Country;

            if (string.IsNullOrWhiteSpace(countryCode) && UserInfo != null && !string.IsNullOrWhiteSpace(UserInfo.Name))
            {
                try
                {
                    var team = UserStorage.LoadTeam();
                    if (team?.MembersMeta != null && team.MembersMeta.ContainsKey(UserInfo.Name))
                    {
                        var metaCountry = team.MembersMeta[UserInfo.Name]?.Country;
                        if (!string.IsNullOrWhiteSpace(metaCountry))
                        {
                            countryCode = metaCountry.Trim().ToUpperInvariant();
                            UserInfo.Country = countryCode;
                        }
                    }
                }
                catch { }
            }

            string timeStr;
            string countryName;
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                timeStr = DateTime.Now.ToString("HH:mm");
                countryName = "";
            }
            else
            {
                timeStr = PublicHolidays.GetLocalTimeString(countryCode);
                countryName = PublicHolidays.Countries.ContainsKey(countryCode)
                    ? countryCode.ToUpper()
                    : "";
            }
            var displayText = $"{timeStr} {countryName}";

            if (InvokeRequired)
                Invoke(new Action(() => { labelLocalTime.Text = displayText; }));
            else
                labelLocalTime.Text = displayText;
        }

        /// <summary>
        /// Returns the current weekly hours worked (for Form1 to check milestones).
        /// </summary>
        public double GetWeeklyHours() => _weeklyHours;

        /// <summary>
        /// Returns the user's weekly hour limit (for Form1 to check milestones).
        /// </summary>
        public double GetWeeklyLimit() => _weeklyLimit;

        /// <summary>
        /// Returns the current holiday hours (for live progress bar updates).
        /// </summary>
        public double GetHolidayHours() => _holidayHours;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // METHOD: ApplyTheme (PUBLIC)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // PURPOSE:
        // Apply dark mode or light mode colors to all text labels.
        // Called when user toggles theme in settings or on application startup.
        //
        // DARK MODE COLORS:
        // - Name (online): Light gray (#DCE0E6)
        // - Name (offline): Medium gray (#6A6E78)
        // - Description: Subtle gray (#505864)
        // - Role: Medium gray (#787C8C)
        //
        // LIGHT MODE COLORS:
        // - Name (online): Dark blue (#1E293B)
        // - Name (offline): Medium gray (#969696)
        // - Description: Medium gray (#646478)
        // - Role: Medium gray (#787C8C)
        //
        // ADMIN OVERRIDE:
        // Admins ALWAYS show light green (#64DC78) regardless of theme
        //
        // REVALIDATION:
        // Calls avatarPanel.Invalidate() and statusDot.Invalidate()
        // Ensures avatar colors update for theme (avatar uses UserInfo.GetDrawingColor())
        //
        // PARAMETERS:
        // - darkMode: True for dark mode, False for light mode
        //
        public void ApplyTheme(bool darkMode, CustomTheme customTheme = null)
        {
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // DEFINE COLORS FOR BOTH THEMES
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            // Admin name always light green (high contrast for authority)
            Color adminGreen = Color.FromArgb(100, 220, 120);

            bool useCustomTheme = customTheme?.Enabled == true;

            // Name color when online (bright/readable)
            Color nameOnline = useCustomTheme
                ? customTheme.GetText()
                : (darkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59));

            // Name color when offline (dimmed/subtle)
            Color nameOffline = useCustomTheme
                ? customTheme.GetSecondaryText()
                : (darkMode ? Color.FromArgb(100, 110, 120) : Color.FromArgb(150, 150, 160));

            // Description color (secondary text)
            Color descColor = useCustomTheme
                ? customTheme.GetSecondaryText()
                : (darkMode ? Color.FromArgb(80, 90, 100) : Color.FromArgb(100, 100, 120));

            // Role color (tertiary text, smallest)
            Color roleColor = useCustomTheme
                ? customTheme.GetSecondaryText()
                : Color.FromArgb(120, 120, 140);

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // APPLY NAME COLOR (CUSTOM COLOR > ADMIN GREEN > DEFAULT)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Color userColor = UserInfo.GetDrawingColor(Color.Empty);
            bool hasCustomColor = userColor != Color.Empty;
            if (hasCustomColor)
            {
                labelName.ForeColor = _isOnline ? userColor
                    : Color.FromArgb((int)(userColor.R * 0.5), (int)(userColor.G * 0.5), (int)(userColor.B * 0.5));
            }
            else if (UserInfo.IsAdmin)
                labelName.ForeColor = adminGreen;
            else
                labelName.ForeColor = _isOnline ? nameOnline : nameOffline;

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // APPLY SECONDARY COLORS
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            labelDescription.ForeColor = descColor;
            labelRole.ForeColor = roleColor;

            // Maintain transparent background
            this.BackColor = Color.Transparent;

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // APPLY THEME TO PROGRESS BAR & LOCAL TIME
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            _isDarkMode = darkMode;
            progressBarBg.BackColor = useCustomTheme
                ? customTheme.GetInput()
                : (darkMode ? Color.FromArgb(50, 55, 65) : Color.FromArgb(215, 220, 228));
            labelHours.ForeColor = useCustomTheme
                ? customTheme.GetSecondaryText()
                : (darkMode ? Color.FromArgb(140, 150, 170) : Color.FromArgb(100, 110, 130));
            labelLocalTime.ForeColor = useCustomTheme
                ? customTheme.GetSecondaryText()
                : (darkMode ? Color.FromArgb(140, 150, 170) : Color.FromArgb(100, 110, 130));

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // INVALIDATE AVATAR & STATUSDOT PANELS
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Forces Paint events to fire, re-rendering with new theme
            // Avatar circle color comes from UserInfo.GetDrawingColor()
            // Status dot colors are defined in DrawStatusDot()
            avatarPanel.Invalidate();
            statusDot.Invalidate();
            RefreshAvatarFromTeamMeta();
        }
    }
}
