// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        ColorEmojiRenderer.cs                                        ║
// ║  PURPOSE:     COLOR EMOJI PICKER USING WEBBROWSER (HTML RENDERING)         ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  WinForms' GDI/GDI+ text rendering only produces monochrome emoji          ║
// ║  outlines. RichEdit50W can also fail due to .NET Framework bugs.            ║
// ║                                                                            ║
// ║  This class provides a WebBrowser-based emoji picker popup that renders     ║
// ║  full-color emojis using the HTML/CSS rendering engine (IE11 on Win10+),    ║
// ║  which has native color emoji support via Segoe UI Emoji font.             ║
// ║                                                                            ║
// ║  COMMUNICATION: Uses COM interop (ObjectForScripting) to pass the          ║
// ║  selected emoji index from JavaScript back to C#.                          ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    // ════════════════════════════════════════════════════════════════════════════
    //  EmojiPickerCallback — COM-VISIBLE BRIDGE FOR JAVASCRIPT → C#
    //  The WebBrowser's JavaScript calls window.external.Select(index) which
    //  invokes this class to pass the emoji selection back to WinForms.
    // ════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// COM-visible bridge class that receives emoji selection from JavaScript in WebBrowser.
    /// Allows WebBrowser control to call back to C# code via window.external.Select(index).
    /// </summary>
    [ComVisible(true)]
    public class EmojiPickerCallback
    {
        // ─── PRIVATE FIELDS ───
        private readonly string[] _emojis;      // Array of emoji strings to choose from
        private readonly Action<string> _onSelect; // Callback invoked when emoji is selected

        /// <summary>
        /// Initializes the callback bridge with emoji array and selection callback.
        /// </summary>
        public EmojiPickerCallback(string[] emojis, Action<string> onSelect)
        {
//             DebugLogger.Log("[EmojiRenderer] EmojiPickerCallback constructor — emoji count=" + (emojis?.Length ?? 0));
            _emojis = emojis;
            _onSelect = onSelect;
        }

        /// <summary>
        /// Called from JavaScript: window.external.Select(index)
        /// Validates index and invokes the selection callback with the emoji string.
        /// </summary>
        public void Select(int index)
        {
//             DebugLogger.Log("[EmojiRenderer] EmojiPickerCallback.Select called — index=" + index);
            if (index >= 0 && index < _emojis.Length)
            {
//                 DebugLogger.Log("[EmojiRenderer] Selected emoji: " + _emojis[index]);
                _onSelect?.Invoke(_emojis[index]);
            }
            else
            {
//                 DebugLogger.Log("[EmojiRenderer] Invalid emoji index: " + index);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  ColorEmojiPicker — STATIC HELPER TO SHOW COLOR EMOJI POPUP
    //  Creates a popup Form with a WebBrowser control that renders a grid of
    //  clickable color emojis using HTML/CSS. Works on Windows 10+ with IE11.
    // ════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Static helper class that creates and shows a WebBrowser-based emoji picker popup.
    /// Renders full-color emojis using HTML/CSS with Segoe UI Emoji font (Windows 10+).
    /// </summary>
    internal static class ColorEmojiPicker
    {
        /// <summary>
        /// Shows a color emoji picker popup (WebBrowser form with clickable emoji grid).
        /// Positions above the anchor button and auto-closes when losing focus.
        /// </summary>
        /// <param name="owner">Parent control for the popup</param>
        /// <param name="anchorButton">Button to position the popup near (above it)</param>
        /// <param name="emojiSet">Array of emoji strings to display in grid</param>
        /// <param name="bgColorHex">Background color as hex (e.g. "#1e242e")</param>
        /// <param name="hoverColorHex">Hover highlight color as hex (e.g. "#42a5f5")</param>
        /// <param name="popupWidth">Popup width in pixels</param>
        /// <param name="popupHeight">Popup height in pixels</param>
        /// <param name="onSelect">Callback invoked when user clicks an emoji</param>
        internal static void Show(Control owner, Control anchorButton, string[] emojiSet,
            string bgColorHex, string hoverColorHex, int popupWidth, int popupHeight,
            Action<string> onSelect)
        {
//             DebugLogger.Log("[EmojiRenderer] ColorEmojiPicker.Show called — emoji count=" + (emojiSet?.Length ?? 0) +
//                            ", size=" + popupWidth + "x" + popupHeight);

            // ─── CREATE FORM ───
            var form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                TopMost = true,
                Size = new Size(popupWidth, popupHeight),
                BackColor = ColorTranslator.FromHtml(bgColorHex)
            };

            // ─── POSITION ABOVE ANCHOR BUTTON ───
            var btnPos = anchorButton.PointToScreen(Point.Empty);
            form.Location = new Point(btnPos.X, btnPos.Y - form.Height - 2);
//             DebugLogger.Log("[EmojiRenderer] Popup positioned at " + form.Location.X + "," + form.Location.Y);

            // ─── CREATE WEBBROWSER CONTROL ───
            var wb = new WebBrowser
            {
                Dock = DockStyle.Fill,
                IsWebBrowserContextMenuEnabled = false,
                ScriptErrorsSuppressed = true,
                ScrollBarsEnabled = false,
                AllowNavigation = false,
                WebBrowserShortcutsEnabled = false
            };

            // ─── SET UP COM CALLBACK BRIDGE ───
            Form capturedForm = form; // capture for lambda closure
            wb.ObjectForScripting = new EmojiPickerCallback(emojiSet, emoji =>
            {
//                 DebugLogger.Log("[EmojiRenderer] Emoji selected from picker: " + emoji);
                onSelect(emoji);
                try { capturedForm.Close(); } catch { }
            });

            // ─── BUILD HTML CONTENT ───
            // IE=edge meta ensures IE11 rendering mode (supports Segoe UI Emoji color font)
            var sb = new StringBuilder(1024);
            sb.Append("<!DOCTYPE html><html><head>");
            sb.Append("<meta http-equiv='X-UA-Compatible' content='IE=edge'/>");
            sb.Append("<style>");
            sb.Append("*{margin:0;padding:0;box-sizing:border-box}");
            // Body: flex grid layout with background color
            sb.Append($"body{{background:{bgColorHex};display:flex;flex-wrap:wrap;padding:4px;");
            sb.Append("font-family:'Segoe UI Emoji','Apple Color Emoji','Noto Color Emoji',sans-serif;overflow:hidden}");
            // Emoji cell: 40x40px square with hover effect
            sb.Append($".e{{width:40px;height:40px;display:flex;align-items:center;justify-content:center;");
            sb.Append($"font-size:24px;border-radius:4px;cursor:pointer;-webkit-user-select:none;user-select:none}}");
            sb.Append($".e:hover{{background:{hoverColorHex}}}");
            sb.Append("</style></head><body>");

            // ─── ADD EMOJI CELLS ───
            for (int i = 0; i < emojiSet.Length; i++)
                sb.Append($"<span class='e' onclick='window.external.Select({i})'>{emojiSet[i]}</span>");

            sb.Append("</body></html>");

            wb.DocumentText = sb.ToString();
            form.Controls.Add(wb);
//             DebugLogger.Log("[EmojiRenderer] WebBrowser HTML content set");

            // ─── AUTO-CLOSE WHEN FOCUS LOST ───
            form.Deactivate += (s, e) =>
            {
//                 DebugLogger.Log("[EmojiRenderer] Emoji picker lost focus — closing");
                try { form.Close(); } catch { }
            };

            form.Show(owner);
//             DebugLogger.Log("[EmojiRenderer] Emoji picker form shown");
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  ColorEmojiCache — DOWNLOADS TWEMOJI COLOR PNG IMAGES & INSERTS INTO RICHTEXTBOX
    //  Downloads color emoji PNGs from the Twemoji CDN (Twitter's open-source emoji
    //  set, CC-BY 4.0 license) and provides a method to replace emoji characters in
    //  a RichTextBox with inline color PNG images via RTF embedding.
    //
    //  FLOW:
    //  1. Call PreloadAsync() at startup — downloads all 25 emoji PNGs in background
    //  2. Call InsertColorEmojis(rtb) after setting message text on a RichTextBox
    //     — replaces known emoji chars with inline color images
    // ════════════════════════════════════════════════════════════════════════════
    internal static class ColorEmojiCache
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 0x000B;

        // Twemoji CDN — jdecked/twemoji (maintained fork of Twitter's archived emoji set)
        private const string TwemojiCdn = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/72x72/";
        // Fallback CDN in case the primary is unreachable
        private const string TwemojiCdnFallback = "https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/72x72/";
        // PNG magic bytes: 0x89 0x50 0x4E 0x47
        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };

        // Known emoji → Twemoji hex filename (without .png)
        private static readonly Dictionary<string, string> _twemojiCodes = new Dictionary<string, string>
        {
            { "\U0001F600", "1f600" }, // 😀
            { "\U0001F602", "1f602" }, // 😂
            { "\U0001F60D", "1f60d" }, // 😍
            { "\U0001F970", "1f970" }, // 🥰
            { "\U0001F60E", "1f60e" }, // 😎
            { "\U0001F622", "1f622" }, // 😢
            { "\U0001F621", "1f621" }, // 😡
            { "\U0001F914", "1f914" }, // 🤔
            { "\U0001F44D", "1f44d" }, // 👍
            { "\U0001F44E", "1f44e" }, // 👎
            { "\u2764\uFE0F", "2764-fe0f" }, // ❤️
            { "\u2764",       "2764"       }, // ❤ (without VS16)
            { "\U0001F525", "1f525" }, // 🔥
            { "\U0001F389", "1f389" }, // 🎉
            { "\U0001F4AF", "1f4af" }, // 💯
            { "\U0001F64F", "1f64f" }, // 🙏
            { "\U0001F4AA", "1f4aa" }, // 💪
            { "\u2705",     "2705"  }, // ✅
            { "\u274C",     "274c"  }, // ❌
            { "\U0001F44B", "1f44b" }, // 👋
            { "\U0001F923", "1f923" }, // 🤣
            { "\U0001F631", "1f631" }, // 😱
            { "\U0001F495", "1f495" }, // 💕
            { "\U0001F64C", "1f64c" }, // 🙌
            { "\U0001F38A", "1f38a" }, // 🎊
            { "\U0001F4B0", "1f4b0" }, // 💰
        };

        // Sorted longest-first for correct multi-char matching (e.g. ❤️ before ❤)
        private static readonly KeyValuePair<string, string>[] _sortedEmojis;

        // Cache: emoji string → hex-encoded PNG data (ready for RTF \pict insertion)
        private static readonly Dictionary<string, string> _hexCache = new Dictionary<string, string>();
        // Cache: emoji string → Image object (for GDI+ drawing in custom Paint handlers)
        private static readonly Dictionary<string, Image> _imageCache = new Dictionary<string, Image>();
        private static readonly object _lock = new object();
        private static bool _preloading;
        private static bool _preloadCompleted;

        internal static bool IsPreloadCompleted => _preloadCompleted;
        internal static event Action CacheReady;

        static ColorEmojiCache()
        {
            _sortedEmojis = _twemojiCodes.OrderByDescending(kv => kv.Key.Length).ToArray();
        }

        /// <summary>
        /// Pre-downloads all known emoji PNGs from Twemoji CDN on a background thread.
        /// Creates both hex-encoded (for RTF) and Image caches for fast access.
        /// Safe to call multiple times — only runs once (checked by _preloading flag).
        /// If downloads fail, emojis display as text fallback (graceful degradation).
        /// </summary>
        internal static void PreloadAsync()
        {
            if (_preloadCompleted)
                return;

            if (_preloading)
            {
//                 DebugLogger.Log("[EmojiRenderer] PreloadAsync — already running, returning");
                return;
            }
            _preloading = true;
//             DebugLogger.Log("[EmojiRenderer] PreloadAsync started — will download " + _twemojiCodes.Count + " emojis");

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using (var client = new System.Net.WebClient())
                    {
                        int successCount = 0;
                        int failCount = 0;

                        foreach (var kv in _twemojiCodes)
                        {
                            // Check if already cached
                            lock (_lock) { if (_hexCache.ContainsKey(kv.Key)) continue; }

                            try
                            {
                                byte[] png = null;

                                // ─── TRY PRIMARY CDN FIRST ───
                                try
                                {
//                                     DebugLogger.Log("[EmojiRenderer] Downloading from primary CDN: " + kv.Value);
                                    png = client.DownloadData(TwemojiCdn + kv.Value + ".png");
                                }
                                catch
                                {
                                    // ─── FALLBACK TO SECONDARY CDN ───
                                    DebugLogger.Log("[EmojiRenderer] Primary CDN failed for " + kv.Value + " — trying fallback");
                                    try
                                    {
                                        png = client.DownloadData(TwemojiCdnFallback + kv.Value + ".png");
                                    }
                                    catch
                                    {
                                        DebugLogger.Log("[EmojiRenderer] Both CDNs failed for " + kv.Value);
                                    }
                                }

                                // ─── VALIDATE PNG (CHECK MAGIC BYTES) ───
                                if (png == null || png.Length < 8)
                                {
                                    DebugLogger.Log("[EmojiRenderer] PNG download failed or too small for " + kv.Value);
                                    continue;
                                }

                                bool validPng = png.Length >= PngMagic.Length;
                                for (int b = 0; b < PngMagic.Length && validPng; b++)
                                    validPng = png[b] == PngMagic[b];

                                if (!validPng)
                                {
//                                     DebugLogger.Log("[EmojiRenderer] Downloaded data for " + kv.Value + " is NOT a valid PNG — skipping");
                                    failCount++;
                                    continue;
                                }

                                // ─── CACHE HEX AND IMAGE ───
                                string hex = BitConverter.ToString(png).Replace("-", "");
                                lock (_lock)
                                {
                                    _hexCache[kv.Key] = hex;

                                    // Also create Image object for GDI+ drawing
                                    // NOTE: MemoryStream must NOT be disposed — GDI+ Image requires
                                    // the source stream to remain open for the lifetime of the Image.
                                    try
                                    {
                                        var ms = new System.IO.MemoryStream(png);
                                        _imageCache[kv.Key] = Image.FromStream(ms);
//                                         DebugLogger.Log("[EmojiRenderer] Cached emoji: " + kv.Value + " (hex=" + hex.Length + " chars)");
                                        successCount++;
                                    }
                                    catch (Exception imgEx)
                                    {
                                        DebugLogger.Log("[EmojiRenderer] Image creation failed for " + kv.Value + ": " + imgEx.Message);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.Log("[EmojiRenderer] Exception downloading emoji " + kv.Value + ": " + ex.Message);
                                failCount++;
                            }
                        }

                        DebugLogger.Log("[EmojiRenderer] PreloadAsync complete — success=" + successCount + ", failed=" + failCount);
                    }
                }
                catch (Exception networkEx)
                {
                    DebugLogger.Log("[EmojiRenderer] Network error during preload: " + networkEx.Message);
                }
                finally
                {
                    _preloading = false;
                    _preloadCompleted = true;
                    try
                    {
                        CacheReady?.Invoke();
                    }
                    catch { }
                }
            });
        }

        /// <summary>
        /// Replaces known emoji characters in a RichTextBox with inline color PNG images.
        /// Only replaces emojis whose PNGs have been successfully downloaded and cached.
        /// Call after setting rtb.Text and before adding to the parent control.
        /// Suppresses redraw during replacement to prevent flicker.
        /// </summary>
        /// <param name="rtb">The RichTextBox to modify (must have Handle created)</param>
        /// <param name="displaySizePx">Display size of each emoji in pixels (match font height)</param>
        internal static void InsertColorEmojis(RichTextBox rtb, int displaySizePx)
        {
            string text = rtb.Text;
//             DebugLogger.Log("[EmojiRenderer] InsertColorEmojis called — text length=" + (text?.Length ?? 0) +
//                            ", displaySize=" + displaySizePx + ", hexCache count=" + _hexCache.Count);
            if (string.IsNullOrEmpty(text)) return;

            // ─── CONVERT PIXELS TO TWIPS ───
            // 1px at 96 DPI ≈ 15 twips
            int goalTwips = displaySizePx * 15;

            // ─── FORWARD PASS: FIND ALL EMOJI POSITIONS ───
            // Scan text left-to-right, checking longest emoji patterns first to avoid
            // partial matches (e.g., ❤️ vs ❤)
            var replacements = new List<(int pos, int len, string hex)>();
            for (int i = 0; i < text.Length; )
            {
                bool matched = false;
                foreach (var kv in _sortedEmojis) // longest first
                {
                    string emoji = kv.Key;
                    if (i + emoji.Length <= text.Length &&
                        string.Compare(text, i, emoji, 0, emoji.Length, StringComparison.Ordinal) == 0)
                    {
                        string hex;
                        lock (_lock) { _hexCache.TryGetValue(emoji, out hex); }
                        if (hex != null)
                        {
                            replacements.Add((i, emoji.Length, hex));
//                             DebugLogger.Log("[EmojiRenderer] Found emoji at pos " + i + ": " + emoji);
                        }
                        else
                        {
//                             DebugLogger.Log("[EmojiRenderer] Found emoji '" + emoji + "' at pos " + i + " but NO hex in cache");
                        }
                        i += emoji.Length;
                        matched = true;
                        break;
                    }
                }
                if (!matched) i++;
            }

//             DebugLogger.Log("[EmojiRenderer] Found " + replacements.Count + " emoji replacements in text");
            if (replacements.Count == 0) return;

            // ─── BACKWARD PASS: APPLY REPLACEMENTS FROM END TO START ───
            // Working backward ensures indices remain valid as we modify text.
            // Suppress screen redraw to prevent flicker during multi-replacement.
            bool hasHandle = rtb.IsHandleCreated;
            if (hasHandle)
            {
//                 DebugLogger.Log("[EmojiRenderer] Suppressing redraw");
                SendMessage(rtb.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            }

            try
            {
                for (int r = replacements.Count - 1; r >= 0; r--)
                {
                    var (pos, len, hex) = replacements[r];
//                     DebugLogger.Log("[EmojiRenderer] Replacing pos=" + pos + " len=" + len + " hexLen=" +
//                                    hex.Length + " goalTwips=" + goalTwips);

                    rtb.SelectionStart = pos;
                    rtb.SelectionLength = len;

                    // ─── BUILD RTF SNIPPET ───
                    // \pict = picture
                    // \pngblip = PNG bitmap format
                    // \picw/\pich = source size in hundredths of mm (72px at 96DPI = 1905)
                    // \picwgoal/\pichgoal = display size in twips
                    // hex = hex-encoded PNG data
                    string rtfSnippet = @"{\rtf1\ansi {\pict\pngblip\picw1905\pich1905\picwgoal"
                        + goalTwips + @"\pichgoal" + goalTwips + " " + hex + "}}";
                    rtb.SelectedRtf = rtfSnippet;

//                     DebugLogger.Log("[EmojiRenderer] Replacement done — rtb.TextLength now=" + rtb.TextLength);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[EmojiRenderer] RTF insertion FAILED: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                rtb.SelectionStart = 0;
                rtb.SelectionLength = 0;

                if (hasHandle)
                {
//                     DebugLogger.Log("[EmojiRenderer] Re-enabling redraw");
                    SendMessage(rtb.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                    rtb.Invalidate();
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  TEXT SEGMENT — represents a piece of text or an emoji in a message
        // ═══════════════════════════════════════════════════════════════════════
        private struct TextSegment
        {
            public string Text;
            public bool IsEmoji;
            public Image EmojiImage; // only set when IsEmoji == true
        }

        /// <summary>
        /// Splits a string into segments of plain text and known emojis.
        /// </summary>
        private static List<TextSegment> SplitIntoSegments(string text)
        {
            var segments = new List<TextSegment>();
            int i = 0;
            int textStart = 0;

            while (i < text.Length)
            {
                bool matched = false;
                foreach (var kv in _sortedEmojis)
                {
                    string emoji = kv.Key;
                    if (i + emoji.Length <= text.Length &&
                        string.Compare(text, i, emoji, 0, emoji.Length, StringComparison.Ordinal) == 0)
                    {
                        Image img;
                        lock (_lock) { _imageCache.TryGetValue(emoji, out img); }
                        if (img != null)
                        {
                            // Flush any preceding plain text
                            if (i > textStart)
                                segments.Add(new TextSegment { Text = text.Substring(textStart, i - textStart), IsEmoji = false });
                            segments.Add(new TextSegment { Text = emoji, IsEmoji = true, EmojiImage = img });
                            i += emoji.Length;
                            textStart = i;
                            matched = true;
                            break;
                        }
                        // If no image cached, treat as plain text — fall through
                        break;
                    }
                }
                if (!matched) i++;
            }
            // Flush remaining text
            if (textStart < text.Length)
                segments.Add(new TextSegment { Text = text.Substring(textStart), IsEmoji = false });

            return segments;
        }

        /// <summary>
        /// Draws text with inline color emoji images using GDI+ Graphics.
        /// Drop-in replacement for Graphics.DrawString() that renders known emojis
        /// as color PNG images instead of monochrome glyphs.
        /// </summary>
        /// <param name="g">Graphics surface to draw on</param>
        /// <param name="text">The message text (may contain emoji characters)</param>
        /// <param name="font">Font for non-emoji text</param>
        /// <param name="textBrush">Brush for non-emoji text</param>
        /// <param name="rect">Bounding rectangle for the text layout</param>
        /// <param name="sf">StringFormat (optional, nullable)</param>
        internal static void DrawTextWithEmojis(Graphics g, string text, Font font, Brush textBrush,
            RectangleF rect, StringFormat sf)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var segments = SplitIntoSegments(text);

            // If no emojis found, just draw normally (fast path)
            bool hasEmoji = false;
            foreach (var seg in segments)
                if (seg.IsEmoji) { hasEmoji = true; break; }

            if (!hasEmoji)
            {
                g.DrawString(text, font, textBrush, rect, sf);
                return;
            }

            // ── DRAW SEGMENTS WITH WORD WRAPPING ──
            float x = rect.X;
            float y = rect.Y;
            float maxX = rect.X + rect.Width;
            float lineHeight = font.GetHeight(g);
            int emojiSize = (int)(lineHeight * 1.1f); // Slightly larger than text height
            float emojiAdvance = emojiSize + 2; // Emoji width + small gap

            foreach (var seg in segments)
            {
                if (seg.IsEmoji)
                {
                    // Wrap if emoji doesn't fit on current line
                    if (x + emojiAdvance > maxX && x > rect.X)
                    {
                        x = rect.X;
                        y += lineHeight;
                    }
                    if (y + emojiSize > rect.Y + rect.Height) break; // Out of bounds

                    // Draw the color emoji image
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(seg.EmojiImage, x, y + (lineHeight - emojiSize) / 2f, emojiSize, emojiSize);
                    x += emojiAdvance;
                }
                else
                {
                    // Plain text — draw word by word for proper wrapping
                    string remaining = seg.Text;
                    while (remaining.Length > 0)
                    {
                        // Measure how much text fits on the current line
                        float availWidth = maxX - x;
                        if (availWidth < font.Size) // Not enough space for even one char
                        {
                            x = rect.X;
                            y += lineHeight;
                            availWidth = rect.Width;
                        }
                        if (y + lineHeight > rect.Y + rect.Height) break; // Out of bounds

                        // Find how many characters fit
                        int charsFit = remaining.Length;
                        SizeF measured = g.MeasureString(remaining, font, (int)availWidth);
                        if (measured.Width > availWidth && remaining.Length > 1)
                        {
                            // Binary search for the number of chars that fit
                            int lo = 1, hi = remaining.Length;
                            while (lo < hi)
                            {
                                int mid = (lo + hi + 1) / 2;
                                SizeF sz = g.MeasureString(remaining.Substring(0, mid), font);
                                if (sz.Width <= availWidth) lo = mid;
                                else hi = mid - 1;
                            }
                            charsFit = lo;

                            // Try to break at a space for word wrapping
                            int spaceIdx = remaining.LastIndexOf(' ', charsFit - 1, charsFit);
                            if (spaceIdx > 0) charsFit = spaceIdx + 1;
                        }

                        string line = remaining.Substring(0, charsFit);
                        g.DrawString(line, font, textBrush, x, y);
                        SizeF lineSize = g.MeasureString(line, font);
                        x += lineSize.Width;

                        remaining = remaining.Substring(charsFit);
                        if (remaining.Length > 0)
                        {
                            // Wrap to next line
                            x = rect.X;
                            y += lineHeight;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a string contains any known emojis that have cached images.
        /// Useful to decide whether to use DrawTextWithEmojis or plain DrawString.
        /// </summary>
        internal static bool HasCachedEmojis(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; )
            {
                bool matched = false;
                foreach (var kv in _sortedEmojis)
                {
                    string emoji = kv.Key;
                    if (i + emoji.Length <= text.Length &&
                        string.Compare(text, i, emoji, 0, emoji.Length, StringComparison.Ordinal) == 0)
                    {
                        lock (_lock) { if (_imageCache.ContainsKey(emoji)) return true; }
                        i += emoji.Length;
                        matched = true;
                        break;
                    }
                }
                if (!matched) i++;
            }
            return false;
        }
    }
}
