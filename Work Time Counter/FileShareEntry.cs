// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        FileShareEntry.cs                                            ║
// ║  PURPOSE:     DATA MODELS FOR P2P FILE SHARING SYSTEM                      ║
// ║               FIREBASE STORES ONLY TINY METADATA — NO FILE DATA!           ║
// ║               ACTUAL FILES TRANSFER DIRECTLY BETWEEN USERS VIA TCP         ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝
//
// ══════════════════════════════════════════════════════════════════════════════
// HOW P2P FILE SHARING WORKS (ZERO FIREBASE BANDWIDTH FOR FILE DATA):
// ══════════════════════════════════════════════════════════════════════════════
//
//   FIREBASE STORES ONLY METADATA (~200 BYTES PER FILE):
//     /shared_files/{key}/fileName     --> "BigDesign.psd"
//     /shared_files/{key}/fileSize     --> "12.4 MB"
//     /shared_files/{key}/fileSizeBytes --> 13003776
//     /shared_files/{key}/uploadedBy   --> "Alice"
//     /shared_files/{key}/uploadedAt   --> "2026-03-24T10:30:00Z"
//     /shared_files/{key}/fileHash     --> "a1b2c3..."
//     /shared_files/{key}/seeders/
//         "Alice"  --> "192.168.1.10:9850"
//         "Bob"    --> "192.168.1.15:9850"
//
//   ACTUAL FILE DATA FLOWS DIRECTLY BETWEEN USERS VIA TCP:
//     Alice uploads --> file saved to local SharedFiles folder
//     Alice's IP:PORT registered in Firebase as seeder
//     Bob wants file --> reads metadata from Firebase (tiny!)
//     Bob connects to Alice's IP:PORT via TCP --> file transfers directly!
//     Bob becomes a seeder too --> his IP:PORT added to Firebase
//     Charlie wants file --> can download from Alice OR Bob
//
//   THIS MEANS:
//     - Firebase bandwidth: ~200 bytes per file (metadata only!)
//     - 50 MB file? Goes directly between computers, NOT through Firebase
//     - More seeders = faster downloads, like a mini torrent
//     - Works great on same LAN / office network
//
// ══════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    // ══════════════════════════════════════════════════════════════════════════
    // FILE SHARE ENTRY — METADATA ONLY (STORED IN FIREBASE)
    // NO FILE DATA IN FIREBASE! FILES TRANSFER VIA TCP PEER-TO-PEER
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Metadata-only entry for P2P file sharing. Actual file data transferred via TCP.
    /// Stored in Firebase at: /shared_files/{key}
    /// </summary>
    public class FileShareEntry
    {
        // FILE IDENTITY
        /// <summary>Original file name with extension (e.g., "document.pdf")</summary>
        public string fileName { get; set; }

        /// <summary>Human-readable file size (e.g., "12.4 MB")</summary>
        public string fileSize { get; set; }

        /// <summary>Exact file size in bytes</summary>
        public long fileSizeBytes { get; set; }

        // UPLOAD INFO
        /// <summary>Username of the user who uploaded the file</summary>
        public string uploadedBy { get; set; }

        /// <summary>ISO 8601 UTC timestamp of upload</summary>
        public string uploadedAt { get; set; }

        // INTEGRITY CHECK
        /// <summary>SHA256 hash for verifying file integrity after transfer</summary>
        public string fileHash { get; set; }

        // SEEDER MAP — KEY: USERNAME, VALUE: "IP:PORT" FOR DIRECT TCP CONNECTION
        // EVERY USER WHO HAS THE FILE LOCALLY IS A SEEDER
        /// <summary>Dictionary mapping username to "IP:PORT" for seeder connectivity</summary>
        public Dictionary<string, string> seeders { get; set; }

        // LOCAL KEY — NOT STORED IN FIREBASE
        /// <summary>Firebase node key (not serialized to JSON)</summary>
        [JsonIgnore]
        public string Key { get; set; }

        // HELPER: HOW MANY PEOPLE HAVE THIS FILE
        /// <summary>Count of seeders who have this file</summary>
        [JsonIgnore]
        public int SeederCount => seeders != null ? seeders.Count : 0;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FILE DELETE LOG — AUDIT TRAIL (STORED IN FIREBASE)
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Audit log entry for deleted shared files.
    /// Stored in Firebase at: /file_delete_log/{key}
    /// </summary>
    public class FileDeleteLog
    {
        /// <summary>Name of the deleted file</summary>
        public string fileName { get; set; }

        /// <summary>Username of user who deleted the file</summary>
        public string deletedBy { get; set; }

        /// <summary>ISO 8601 UTC timestamp of deletion</summary>
        public string deletedAt { get; set; }
    }
}
