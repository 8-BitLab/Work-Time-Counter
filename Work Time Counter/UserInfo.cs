// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        UserInfo.cs                                                  ║
// ║  PURPOSE:     USER ACCOUNT MODEL WITH PASSWORD HASHING                     ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Represents a single user account in the WorkFlow system.                  ║
// ║  Handles password hashing (SHA256), admin status, and display settings.    ║
// ║                                                                            ║
// ║  SECURITY:                                                                 ║
// ║  - Passwords are NEVER stored in plain text                                ║
// ║  - SHA256 hash is computed and stored as Base64 string                     ║
// ║  - Default password "111111" must be changed on first login                ║
// ║  - The _passwordHash field is private — only accessible via methods        ║
// ║                                                                            ║
// ║  LOCAL STORAGE:  %AppData%\WorkFlow\users.bit (JSON array)                 ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;

namespace Work_Time_Counter
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  USER INFO — REPRESENTS A SINGLE USER ACCOUNT
    //  Each user has a unique name within their team, a hashed password,
    //  and display properties (color, title, role).
    //
    //  TWO CONSTRUCTORS:
    //    1. UserInfo(name)          — for NEW users (gets default password "111111")
    //    2. UserInfo(name, hash...) — for EXISTING users loaded from storage
    // ═══════════════════════════════════════════════════════════════════════════
    public class UserInfo
    {
        /// <summary>USERNAME — unique identifier within the team (case-sensitive)</summary>
        public string Name { get; set; }

        // ─────────────────────────────────────────────────────────────────
        //  PASSWORD — stored as SHA256 hash, NEVER as plain text
        //  The hash is private to prevent accidental exposure.
        //  Access only through SetPassword(), VerifyPassword(), GetPasswordHash()
        // ─────────────────────────────────────────────────────────────────
        private string _passwordHash;

        /// <summary>DEFAULT PASSWORD FLAG — true if user hasn't changed from "111111".
        /// The app forces a password change dialog when this is true.</summary>
        public bool IsDefaultPassword { get; set; }

        /// <summary>ADMIN FLAG — true if this user is the team administrator.
        /// Admins can: manage members, change settings, delete content, etc.</summary>
        public bool IsAdmin { get; set; }

        /// <summary>TEAM JOIN CODE — which team this user belongs to.
        /// Links back to TeamInfo.JoinCode for team lookup.</summary>
        public string TeamJoinCode { get; set; }

        /// <summary>DISPLAY COLOR — hex color string (e.g. "#FF7F50").
        /// Used in: avatar circle, chart bars, online status indicator, name highlighting.
        /// Set by admin in Team Settings.</summary>
        public string Color { get; set; }

        /// <summary>JOB TITLE — displayed under the user's name (e.g. "Frontend Developer")</summary>
        public string Title { get; set; }

        /// <summary>ROLE CATEGORY — used for filtering (e.g. "Developer", "QA Tester", "Manager")</summary>
        public string Role { get; set; }

        /// <summary>COUNTRY CODE — ISO 2-letter code (e.g. "DE", "US", "BG").
        /// Used for: timezone-based local time display, public holiday detection.</summary>
        public string Country { get; set; }

        /// <summary>WEEKLY HOUR LIMIT — max expected working hours per week (default 10).
        /// Used for: progress bar color gradient, motivational messages, stress alerts.</summary>
        public double WeeklyHourLimit { get; set; }

        // ─────────────────────────────────────────────────────────────────
        //  CONSTRUCTOR 1: NEW USER
        //  Called when admin adds a new team member.
        //  Sets the default password "111111" and flags it for forced change.
        // ─────────────────────────────────────────────────────────────────
        public UserInfo(string name, bool isAdmin = false, string teamJoinCode = null)
        {
            // [UserInfo] Create new user account with default password
//             DebugLogger.Log($"[UserInfo] Creating new user: {name} (isAdmin={isAdmin})");
            Name = name;
            IsAdmin = isAdmin;
            TeamJoinCode = teamJoinCode;
            Color = "";
            Title = "";
            Role = "";
            Country = "";
            WeeklyHourLimit = 10;            // DEFAULT: 10 HOURS PER WEEK
            SetPassword("111111");           // DEFAULT PASSWORD — MUST BE CHANGED ON FIRST LOGIN
            IsDefaultPassword = true;         // FLAG: FORCE PASSWORD CHANGE DIALOG
//             DebugLogger.Log("[UserInfo] New user initialized with default password");
        }

        // ─────────────────────────────────────────────────────────────────
        //  CONSTRUCTOR 2: EXISTING USER (LOADED FROM STORAGE)
        //  Called when deserializing user data from local JSON file.
        //  The password hash is already computed — we just store it directly.
        // ─────────────────────────────────────────────────────────────────
        public UserInfo(string name, string passwordHash, bool isDefault, bool isAdmin = false,
            string teamJoinCode = null, string color = "", string title = "", string role = "",
            string country = "", double weeklyHourLimit = 10)
        {
            // [UserInfo] Load existing user from stored data
//             DebugLogger.Log($"[UserInfo] Loading existing user: {name}");
            Name = name;
            _passwordHash = passwordHash;     // ALREADY HASHED — STORE DIRECTLY
            IsDefaultPassword = isDefault;
            IsAdmin = isAdmin;
            TeamJoinCode = teamJoinCode;
            Color = color ?? "";
            Title = title ?? "";
            Role = role ?? "";
            Country = country ?? "";
            WeeklyHourLimit = weeklyHourLimit > 0 ? weeklyHourLimit : 10;
//             DebugLogger.Log($"[UserInfo] User loaded: {name}, isDefault={isDefault}, isAdmin={isAdmin}");
        }

        // ─────────────────────────────────────────────────────────────────
        //  PASSWORD METHODS
        //  SetPassword:    Takes plain text, hashes it, stores the hash
        //  VerifyPassword: Takes plain text, hashes it, compares with stored hash
        //  GetPasswordHash: Returns the raw hash (for serialization to storage)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>SET PASSWORD — hashes the plain text and stores the hash</summary>
        /// <param name="plainPassword">The new password in plain text</param>
        public void SetPassword(string plainPassword)
        {
            // [UserInfo] Set and hash new password
//             DebugLogger.Log($"[UserInfo] SetPassword called for user: {Name}");
            _passwordHash = HashPassword(plainPassword);
//             DebugLogger.Log("[UserInfo] Password hashed and stored");
        }

        /// <summary>VERIFY PASSWORD — checks if the given plain text matches the stored hash</summary>
        /// <param name="plainPassword">The password attempt in plain text</param>
        /// <returns>True if the password is correct</returns>
        public bool VerifyPassword(string plainPassword)
        {
            // [UserInfo] Verify plain text password against stored hash
            var result = _passwordHash == HashPassword(plainPassword);
            DebugLogger.Log($"[UserInfo] VerifyPassword for {Name}: {(result ? "SUCCESS" : "FAILED")}");
            return result;
        }

        /// <summary>VERIFY PASSWORD HASH — compares a stored hash directly against
        /// the current password hash. Used by "Remember Password" feature to skip
        /// plain-text entry when the hash is already saved locally.</summary>
        /// <param name="hash">The stored password hash to compare</param>
        /// <returns>True if hash matches the current password hash</returns>
        public bool VerifyPasswordHash(string hash)
        {
            // [UserInfo] Verify stored password hash (for "Remember Password" feature)
            var result = _passwordHash == hash;
//             DebugLogger.Log($"[UserInfo] VerifyPasswordHash for {Name}: {(result ? "MATCH" : "MISMATCH")}");
            return result;
        }

        /// <summary>GET PASSWORD HASH — returns the raw SHA256 hash (Base64 encoded).
        /// Used only for serialization to local storage file.</summary>
        public string GetPasswordHash()
        {
            // [UserInfo] Get password hash for storage serialization
//             DebugLogger.Log($"[UserInfo] GetPasswordHash called for {Name}");
            return _passwordHash;
        }

        // ─────────────────────────────────────────────────────────────────
        //  COLOR HELPER — CONVERTS HEX STRING TO System.Drawing.Color
        //  Used by OnlineUserControl for avatar rendering and by charts
        //  for per-user color coding.
        // ─────────────────────────────────────────────────────────────────

        /// <summary>GET DRAWING COLOR — parses the hex Color string into a System.Drawing.Color.
        /// Returns the fallback color if parsing fails or Color is empty.</summary>
        /// <param name="fallback">Color to return if hex parsing fails</param>
        /// <returns>The parsed color or the fallback</returns>
        public System.Drawing.Color GetDrawingColor(System.Drawing.Color fallback)
        {
            // [UserInfo] Parse hex color string to System.Drawing.Color
            if (!string.IsNullOrEmpty(Color) && Color.StartsWith("#") && Color.Length == 7)
            {
                try
                {
                    var result = System.Drawing.ColorTranslator.FromHtml(Color);  // PARSE "#RRGGBB" FORMAT
//                     DebugLogger.Log($"[UserInfo] Parsed color for {Name}: {Color}");
                    return result;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[UserInfo] Failed to parse color '{Color}': {ex.Message}");
                }
            }
//             DebugLogger.Log($"[UserInfo] Using fallback color for {Name}");
            return fallback;
        }

        // ─────────────────────────────────────────────────────────────────
        //  SHA256 HASHING — INTERNAL PASSWORD HASHING METHOD
        //  Uses System.Security.Cryptography.SHA256 (built into .NET)
        //  Returns Base64 encoded hash string for compact storage.
        //
        //  HASH FLOW:
        //    "myPassword" → UTF8 bytes → SHA256 hash → Base64 string
        // ─────────────────────────────────────────────────────────────────
        private string HashPassword(string password)
        {
            // [UserInfo] Hash password using SHA256 algorithm
//             DebugLogger.Log("[UserInfo] HashPassword: computing SHA256 hash");
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                var result = Convert.ToBase64String(bytes);   // RETURN AS BASE64 FOR COMPACT STORAGE
//                 DebugLogger.Log($"[UserInfo] Hash computed, length={result.Length} chars");
                return result;
            }
        }

        /// <summary>TO STRING — returns the username (used in ComboBox displays, etc.)</summary>
        public override string ToString()
        {
            // [UserInfo] String representation - returns username
            return Name;
        }
    }
}
