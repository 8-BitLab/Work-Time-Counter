// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        StickerEntry.cs                                              ║
// ║  PURPOSE:     DATA MODELS FOR STICKERS, CHAT, DMs, AND PROJECT STAGES      ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  This file contains all the data model classes used throughout the app      ║
// ║  for serialization to/from Firebase Realtime Database (JSON format).        ║
// ║  Each class maps directly to a node in the Firebase database tree.          ║
// ║                                                                            ║
// ║  FIREBASE STRUCTURE:                                                       ║
// ║    /stickers/{key}  --> StickerEntry                                        ║
// ║    /chat/{key}      --> ChatMessage                                         ║
// ║    /dm/{key}        --> DirectMessage                                       ║
// ║    /project_stages/{key} --> ProjectStage                                   ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  STICKER ENTRY — KANBAN-STYLE TASK CARD
    //  Stored in Firebase at: /stickers/{auto-generated-key}
    //  Supports 4 types: ToDo, Reminder, Bug, Idea
    //  Each sticker can be assigned to a team member and marked as done.
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// StickerEntry represents a single task card on the team's sticker board.
    /// Data class for Firebase serialization/deserialization.
    /// </summary>
    public class StickerEntry
    {
        /// <summary>TITLE OF THE STICKER — shown as the card heading</summary>
        public string title { get; set; }

        /// <summary>DETAILED DESCRIPTION — shown when the card is expanded</summary>
        public string description { get; set; }

        /// <summary>TEXT ALIAS — same as title, saved for mobile app compatibility.
        /// Mobile apps may read "text" instead of "title".</summary>
        public string text { get; set; }

        /// <summary>CONTENT ALIAS — same as description, saved for mobile app compatibility.
        /// Mobile apps may read "content" instead of "description".</summary>
        public string content { get; set; }

        /// <summary>STICKER TYPE — determines the color and category
        /// Valid values: "ToDo", "Reminder", "Bug", "Idea"</summary>
        public string type { get; set; }

        /// <summary>PRIORITY LEVEL — affects sort order and color indicator
        /// Valid values: "High", "Medium", "Low"</summary>
        public string priority { get; set; }

        /// <summary>WHO CREATED THIS STICKER — username of the creator</summary>
        public string createdBy { get; set; }

        /// <summary>WHO IS ASSIGNED TO WORK ON THIS — username or empty</summary>
        public string assignedTo { get; set; }

        /// <summary>CREATION TIMESTAMP — ISO 8601 UTC format (e.g. "2024-01-15T10:30:00Z")</summary>
        public string createdAt { get; set; }

        /// <summary>COMPLETION STATUS — true when the task is finished</summary>
        public bool done { get; set; }

        /// <summary>FIREBASE KEY — the unique identifier in the database.
        /// This is NOT stored in Firebase itself, it's the key of the JSON node.
        /// Marked [JsonIgnore] so it won't be serialized back to the database.</summary>
        [JsonIgnore]
        public string Key { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  CHAT MESSAGE — REAL-TIME TEAM CHAT
    //  Stored in Firebase at: /chat/{auto-generated-key}
    //  Supports emoji reactions, likes, and tipping between team members.
    //
    //  HOW REACTIONS WORK:
    //    reactions = { "👍": ["Alice", "Bob"], "❤️": ["Charlie"] }
    //    Each emoji maps to a list of usernames who reacted with that emoji.
    //    Clicking the same emoji again removes your reaction (toggle behavior).
    //
    //  HOW LIKES WORK:
    //    likes = ["Alice", "Bob"]
    //    Simple list of usernames. Toggle on/off per user.
    //
    //  HOW TIPPING WORKS:
    //    tips = { "Alice": 50, "Bob": 10 }
    //    Each sender maps to a cumulative tip amount (1-100 per tip).
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// ChatMessage represents a single message in team chat.
    /// Includes support for reactions, likes, and tips.
    /// Data class for Firebase serialization/deserialization.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>USERNAME — who sent this message</summary>
        public string user { get; set; }

        /// <summary>MESSAGE TEXT — the actual chat content (supports emoji)</summary>
        public string message { get; set; }

        /// <summary>TIMESTAMP — ISO 8601 UTC format for sorting and display</summary>
        public string timestamp { get; set; }

        /// <summary>EMOJI REACTIONS — dictionary where:
        ///   KEY = emoji string (e.g. "👍", "❤️", "😀")
        ///   VALUE = list of usernames who reacted with that emoji
        /// Example: { "👍": ["Alice", "Bob"], "❤️": ["Charlie"] }</summary>
        public Dictionary<string, List<string>> reactions { get; set; }

        /// <summary>LIKES — list of usernames who liked this message
        /// Example: ["Alice", "Bob", "Charlie"]</summary>
        public List<string> likes { get; set; }

        /// <summary>TIPS — dictionary where:
        ///   KEY = sender username (who tipped)
        ///   VALUE = cumulative tip amount from that sender
        /// Example: { "Alice": 50, "Bob": 10 }</summary>
        public Dictionary<string, int> tips { get; set; }

        /// <summary>FIREBASE KEY — unique identifier for this message in the database.
        /// NOT serialized to JSON — used locally to reference the message for
        /// updates (add reaction, delete, etc.) via Firebase REST API.
        /// Pattern: _firebaseBaseUrl + "/chat/{FirebaseKey}.json"</summary>
        [JsonIgnore]
        public string FirebaseKey { get; set; }

        /// <summary>EDITED FLAG — true if this message was edited after sending.
        /// Displayed as gray "(edited)" text next to the timestamp.</summary>
        public bool edited { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DIRECT MESSAGE — PRIVATE CHAT BETWEEN TWO USERS
    //  Stored in Firebase at: /dm/{auto-generated-key}
    //
    //  CONVERSATION KEY FORMAT:
    //    To find messages between Alice and Bob, we create a "conversation key"
    //    by sorting both names alphabetically and joining with "_".
    //    Example: "Alice_Bob" (always the same regardless of who sends)
    //
    //  Firebase path: /dm/{conversationKey}/{auto-generated-key}
    //
    //  The "read" flag allows future read-receipt functionality.
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// DirectMessage represents a private message between two users.
    /// Data class for Firebase serialization/deserialization.
    /// </summary>
    public class DirectMessage
    {
        /// <summary>SENDER — username of the person who sent this DM</summary>
        public string fromUser { get; set; }

        /// <summary>RECIPIENT — username of the person this DM is addressed to</summary>
        public string toUser { get; set; }

        /// <summary>MESSAGE CONTENT — the private message text</summary>
        public string message { get; set; }

        /// <summary>TIMESTAMP — ISO 8601 UTC format for chronological ordering</summary>
        public string timestamp { get; set; }

        /// <summary>READ STATUS — true if the recipient has seen this message
        /// (reserved for future read-receipt feature)</summary>
        public bool read { get; set; }

        /// <summary>EDITED FLAG — true if the sender has edited this message after sending</summary>
        public bool edited { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PROJECT STAGE — TRACKS PROJECT PROGRESS MILESTONES
    //  Stored in Firebase at: /project_stages/{auto-generated-key}
    //
    //  Admin can define project stages (e.g. "Planning", "Development",
    //  "Testing", "Deployment") and track their status. These are displayed
    //  in the Wiki panel as a visual pipeline/roadmap.
    //
    //  The "order" field determines the display sequence (lower = earlier stage).
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// ProjectStage represents a phase or milestone in the project timeline.
    /// Displayed as pipeline steps in the Wiki panel.
    /// Data class for Firebase serialization/deserialization.
    /// </summary>
    public class ProjectStage
    {
        /// <summary>STAGE NAME — e.g. "Planning", "Development", "QA Testing"</summary>
        public string name { get; set; }

        /// <summary>STAGE DESCRIPTION — what this stage involves</summary>
        public string description { get; set; }

        /// <summary>CURRENT STATUS — one of: "Not Started", "In Progress", "Completed"</summary>
        public string status { get; set; }

        /// <summary>DISPLAY COLOR — hex color for visual differentiation (e.g. "#FF7F50")</summary>
        public string color { get; set; }

        /// <summary>SORT ORDER — lower numbers appear first (0-based sequence)</summary>
        public int order { get; set; }

        /// <summary>FIREBASE KEY — unique ID in the database (not serialized to JSON)</summary>
        [JsonIgnore]
        public string Key { get; set; }
    }
}
