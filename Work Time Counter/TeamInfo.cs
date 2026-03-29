// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        TeamInfo.cs                                                  ║
// ║  PURPOSE:     TEAM DATA MODEL AND MEMBER METADATA                          ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Defines the TeamInfo and MemberMeta classes that represent a team and      ║
// ║  its members. TeamInfo is the root object stored both locally and in        ║
// ║  Firebase at /teams/{joinCode}. MemberMeta holds per-member settings        ║
// ║  like display color, job title, role, and avatar.                          ║
// ║                                                                            ║
// ║  KEY CONCEPTS:                                                             ║
// ║  - JOIN CODE: 6-character code used by new members to join the team        ║
// ║  - ADMIN: The user who created the team (full control)                     ║
// ║  - ASSISTANT ADMIN: Promoted by admin, has same powers as admin            ║
// ║  - MUTED MEMBERS: Cannot post in chat or create stickers                   ║
// ║                                                                            ║
// ║  FIREBASE STRUCTURE:                                                       ║
// ║    /teams/{joinCode}/TeamName          --> string                           ║
// ║    /teams/{joinCode}/AdminName         --> string                           ║
// ║    /teams/{joinCode}/Members           --> ["Alice", "Bob", ...]            ║
// ║    /teams/{joinCode}/MembersMeta       --> { "Alice": { MemberMeta } }      ║
// ║    /teams/{joinCode}/MutedMembers      --> ["Charlie"]                      ║
// ║    /teams/{joinCode}/AssistantAdmins   --> ["Bob"]                          ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Linq;

namespace Work_Time_Counter
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TEAM INFO — THE MAIN TEAM CONFIGURATION OBJECT
    //  This is the "heart" of the application — everything revolves around
    //  the team. It's stored both locally (for offline access) and in
    //  Firebase (for team synchronization across machines).
    //
    //  LOCAL STORAGE:  %AppData%\WorkFlow\team.bit (JSON format)
    //  FIREBASE PATH:  /teams/{JoinCode}
    // ═══════════════════════════════════════════════════════════════════════════
    public class TeamInfo
    {
        /// <summary>TEAM NAME — display name shown in the UI (e.g. "Engineering Team")</summary>
        public string TeamName { get; set; }

        /// <summary>JOIN CODE — 6-character alphanumeric code for joining the team.
        /// Generated using safe characters (no I/1/O/0 to avoid confusion).
        /// Example: "A3BK7P"</summary>
        public string JoinCode { get; set; }

        /// <summary>ADMIN NAME — the username of the team creator.
        /// The admin has full control: manage members, settings, stickers, chat, etc.</summary>
        public string AdminName { get; set; }

        /// <summary>MEMBERS LIST — all team member usernames (including the admin).
        /// This is the authoritative list of who belongs to the team.</summary>
        public List<string> Members { get; set; } = new List<string>();

        /// <summary>CREATION DATE — UTC timestamp when the team was first created (ISO 8601)</summary>
        public string CreatedAt { get; set; }

        /// <summary>CUSTOM FIREBASE URL — allows teams to use their own Firebase project.
        /// If empty/null, the default shared WorkFlow Firebase is used.
        /// Admin can set this in Team Settings to keep data on their own server.
        /// Example: "https://my-project.firebasedatabase.app"</summary>
        public string CustomFirebaseUrl { get; set; }

        /// <summary>TEAM AI ENDPOINT — admin-configured OpenRouter-compatible endpoint.</summary>
        public string TeamAiEndpoint { get; set; } = "https://openrouter.ai/api/v1/chat/completions";

        /// <summary>TEAM AI MODEL — admin-configured default model (OpenRouter format).</summary>
        public string TeamAiModel { get; set; } = "openai/gpt-4o-mini";

        /// <summary>TEAM AI API KEY — optional shared API key configured by admin.</summary>
        public string TeamAiApiKey { get; set; } = "";

        /// <summary>MEMBERS METADATA — extra info for each member (color, title, role, avatar).
        /// Keyed by member username.
        /// Example: { "Alice": { Color: "#FF7F50", Title: "Lead Dev", Role: "Developer" } }</summary>
        public Dictionary<string, MemberMeta> MembersMeta { get; set; } = new Dictionary<string, MemberMeta>();

        /// <summary>MUTED MEMBERS — usernames of members who are muted.
        /// Muted members CANNOT:
        ///   - Send messages in team chat
        ///   - Create new stickers on the board
        ///   - Post in any public channel
        /// Only admin or assistant admin can mute/unmute members.</summary>
        public List<string> MutedMembers { get; set; } = new List<string>();

        /// <summary>ASSISTANT ADMINS — usernames of members promoted to assistant admin.
        /// Assistant admins have the SAME POWERS as the main admin:
        ///   - Access to Settings panel
        ///   - Can mute/kick/promote other users
        ///   - Can delete chat messages and stickers
        ///   - Can manage wiki entries and project stages
        /// The only thing they can't do is demote the original admin.</summary>
        public List<string> AssistantAdmins { get; set; } = new List<string>();

        /// <summary>BANNED MEMBERS — usernames of members who have been kicked AND banned.
        /// Banned users CANNOT rejoin the team even with the join code.
        /// They will see a "You have been banned" message on login attempt.
        /// Only the main admin or assistant admins can ban/unban users.</summary>
        public List<string> BannedMembers { get; set; } = new List<string>();

        /// <summary>TEAM DAILY WORK LIMIT (hours) — hard cap used to block starting work after the limit is reached.</summary>
        public double DailyWorkingLimitHours { get; set; } = 6;

        /// <summary>TEAM WEEKLY WORK LIMIT (hours) — shared weekly target/limit used for warnings.</summary>
        public double WeeklyWorkingLimitHours { get; set; } = 40;

        // ─────────────────────────────────────────────────────────────────
        //  CONSTRUCTORS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>DEFAULT CONSTRUCTOR — used by JSON deserialization</summary>
        public TeamInfo() { }

        /// <summary>CREATE NEW TEAM — called when admin creates a fresh team.
        /// Automatically generates a join code and adds the admin as the first member.</summary>
        /// <param name="teamName">Display name for the team</param>
        /// <param name="adminName">Username of the team creator</param>
        public TeamInfo(string teamName, string adminName)
        {
            TeamName = teamName;
            AdminName = adminName;
            JoinCode = GenerateJoinCode();       // GENERATE A RANDOM 6-CHAR CODE
            Members.Add(adminName);               // ADMIN IS ALWAYS THE FIRST MEMBER
            CreatedAt = DateTime.UtcNow.ToString("o");  // STORE AS ISO 8601 UTC
            CustomFirebaseUrl = "";
            TeamAiEndpoint = "https://openrouter.ai/api/v1/chat/completions";
            TeamAiModel = "openai/gpt-4o-mini";
            TeamAiApiKey = "";
        }

        // ─────────────────────────────────────────────────────────────────
        //  JOIN CODE GENERATOR
        //  Creates a 6-character alphanumeric code using safe characters.
        //  Excluded: I, 1, O, 0 (too easy to confuse when reading aloud)
        // ─────────────────────────────────────────────────────────────────
        public static string GenerateJoinCode()
        {
            // [TeamInfo] Generate random 6-character join code (safe characters only)
//             DebugLogger.Log("[TeamInfo] Generating new join code");
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // SAFE CHARSET — NO I/1/O/0
            var rng = new Random();
            var code = new char[6];
            for (int i = 0; i < 6; i++)
                code[i] = chars[rng.Next(chars.Length)];
            var result = new string(code);
//             DebugLogger.Log($"[TeamInfo] Generated join code: {result}");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        //  PERMISSION CHECKS — USED THROUGHOUT THE APP TO DETERMINE
        //  WHAT A USER CAN AND CANNOT DO
        // ─────────────────────────────────────────────────────────────────

        /// <summary>CHECK ADMIN PRIVILEGES — returns true if the user is the main admin
        /// OR an assistant admin. Used to gate access to admin-only features.</summary>
        /// <param name="userName">Username to check</param>
        /// <returns>True if user has admin-level access</returns>
        public bool HasAdminPrivileges(string userName)
        {
            // [TeamInfo] Check if user has admin-level privileges
            if (string.IsNullOrEmpty(userName))
            {
//                 DebugLogger.Log("[TeamInfo] HasAdminPrivileges: null/empty username");
                return false;
            }

            // CHECK 1: Is this the original team admin?
            if (userName.Equals(AdminName, StringComparison.OrdinalIgnoreCase))
            {
//                 DebugLogger.Log($"[TeamInfo] {userName} is main admin");
                return true;
            }

            // CHECK 2: Is this user in the assistant admins list?
            if (AssistantAdmins != null && AssistantAdmins.Contains(userName))
            {
//                 DebugLogger.Log($"[TeamInfo] {userName} is assistant admin");
                return true;
            }

//             DebugLogger.Log($"[TeamInfo] {userName} does not have admin privileges");
            return false;
        }

        /// <summary>CHECK MUTE STATUS — returns true if the user is currently muted.
        /// Muted users see a warning message when they try to post.</summary>
        /// <param name="userName">Username to check</param>
        /// <returns>True if user is muted</returns>
        public bool IsMuted(string userName)
        {
            // [TeamInfo] Check if user is muted
            if (MutedMembers == null)
            {
//                 DebugLogger.Log("[TeamInfo] IsMuted: no muted members list");
                return false;
            }
            var isMuted = MutedMembers.Contains(userName);
//             DebugLogger.Log($"[TeamInfo] IsMuted({userName}): {isMuted}");
            return isMuted;
        }

        /// <summary>CHECK BAN STATUS — returns true if the user has been banned.
        /// Banned users cannot rejoin the team.</summary>
        public bool IsBanned(string userName)
        {
            // [TeamInfo] Check if user is banned
            if (BannedMembers == null)
            {
//                 DebugLogger.Log("[TeamInfo] IsBanned: no banned members list");
                return false;
            }
            var isBanned = BannedMembers.Any(b => string.Equals(b, userName, StringComparison.OrdinalIgnoreCase));
//             DebugLogger.Log($"[TeamInfo] IsBanned({userName}): {isBanned}");
            return isBanned;
        }

        /// <summary>STRING REPRESENTATION — shows team name and join code for debugging</summary>
        public override string ToString()
        {
            // [TeamInfo] String representation for debugging and display
            var result = $"{TeamName} ({JoinCode})";
//             DebugLogger.Log($"[TeamInfo] ToString: {result}");
            return result;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MEMBER META — PER-MEMBER DISPLAY SETTINGS
    //  Stored inside TeamInfo.MembersMeta dictionary, keyed by username.
    //  These settings are managed by the admin in the Team Settings panel.
    //
    //  USAGE EXAMPLE:
    //    team.MembersMeta["Alice"] = new MemberMeta("#FF7F50", "Lead Dev", "Developer");
    //
    //  AVATAR SYSTEM:
    //    Avatars are stored as Base64-encoded small images (PNG or JPG).
    //    If no avatar is set, the UI shows a colored circle with the user's
    //    first initial letter. The color comes from the Color field.
    // ═══════════════════════════════════════════════════════════════════════════
    public class MemberMeta
    {
        /// <summary>DISPLAY COLOR — hex color string used for the user's avatar circle,
        /// chart bars, and name highlighting. Example: "#FF7F50" (coral orange)</summary>
        public string Color { get; set; } = "";

        /// <summary>JOB TITLE — displayed under the user's name in the team panel.
        /// Example: "Frontend Developer", "UI/UX Designer", "Project Manager"</summary>
        public string Title { get; set; } = "";

        /// <summary>ROLE CATEGORY — used for filtering and organization.
        /// Predefined options: Developer, Frontend Dev, Backend Dev, Full Stack Dev,
        /// Designer, QA Tester, DevOps, Manager, Team Lead, Product Owner,
        /// Scrum Master, Intern, Freelancer, Other</summary>
        public string Role { get; set; } = "";

        /// <summary>AVATAR IMAGE — Base64-encoded PNG or JPG (small, < 50KB recommended).
        /// If empty, the UI renders a colored circle with the user's first initial.
        /// Stored in Firebase alongside other member metadata.</summary>
        public string AvatarBase64 { get; set; } = "";

        /// <summary>COUNTRY CODE — ISO 2-letter code (e.g. "DE", "US", "BG").
        /// Used for: timezone-based local time display visible to ALL team members,
        /// and public holiday detection for weekly progress bars.</summary>
        public string Country { get; set; } = "";

        /// <summary>WEEKLY HOUR LIMIT — max expected working hours per week (default 10).
        /// Synced to Firebase so ALL team members can see each other's progress correctly.</summary>
        public double WeeklyHourLimit { get; set; } = 10;

        /// <summary>DEFAULT CONSTRUCTOR — used by JSON deserialization</summary>
        public MemberMeta() { }

        /// <summary>CREATE WITH VALUES — used when admin sets member properties</summary>
        /// <param name="color">Hex color string (e.g. "#3498DB")</param>
        /// <param name="title">Job title text</param>
        /// <param name="role">Role category from predefined list</param>
        public MemberMeta(string color, string title, string role)
        {
            Color = color ?? "";
            Title = title ?? "";
            Role = role ?? "";
        }
    }
}
