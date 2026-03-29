// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        PublicHolidays.cs                                            ║
// ║  PURPOSE:     BUILT-IN PUBLIC HOLIDAYS & COUNTRY/TIMEZONE DATA             ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Linq;

namespace Work_Time_Counter
{
    /// <summary>
    /// Provides built-in public holiday data for ~20 countries,
    /// country display names, and timezone mappings.
    /// Used by the weekly hours progress bar to calculate holiday hours
    /// and by the local time display feature.
    /// </summary>
    public static class PublicHolidays
    {
        // ═══════════════════════════════════════════════════════════════
        //  SUPPORTED COUNTRIES — ISO 2-letter code → Display Name
        // ═══════════════════════════════════════════════════════════════
        public static readonly Dictionary<string, string> Countries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "DE", "Germany" },
            { "US", "United States" },
            { "GB", "United Kingdom" },
            { "BG", "Bulgaria" },
            { "FR", "France" },
            { "ES", "Spain" },
            { "IT", "Italy" },
            { "NL", "Netherlands" },
            { "AT", "Austria" },
            { "CH", "Switzerland" },
            { "PL", "Poland" },
            { "CZ", "Czech Republic" },
            { "RO", "Romania" },
            { "HU", "Hungary" },
            { "SE", "Sweden" },
            { "NO", "Norway" },
            { "DK", "Denmark" },
            { "FI", "Finland" },
            { "PT", "Portugal" },
            { "JP", "Japan" },
            { "CA", "Canada" },
            { "AU", "Australia" },
            { "BR", "Brazil" },
            { "IN", "India" },
            { "TR", "Turkey" },
        };

        // ═══════════════════════════════════════════════════════════════
        //  COUNTRY → TIMEZONE ID (Windows timezone IDs)
        //  Used for displaying local time next to each user
        // ═══════════════════════════════════════════════════════════════
        public static readonly Dictionary<string, string> CountryTimezones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "DE", "W. Europe Standard Time" },
            { "US", "Eastern Standard Time" },
            { "GB", "GMT Standard Time" },
            { "BG", "FLE Standard Time" },
            { "FR", "Romance Standard Time" },
            { "ES", "Romance Standard Time" },
            { "IT", "W. Europe Standard Time" },
            { "NL", "W. Europe Standard Time" },
            { "AT", "W. Europe Standard Time" },
            { "CH", "W. Europe Standard Time" },
            { "PL", "Central European Standard Time" },
            { "CZ", "Central European Standard Time" },
            { "RO", "GTB Standard Time" },
            { "HU", "Central European Standard Time" },
            { "SE", "W. Europe Standard Time" },
            { "NO", "W. Europe Standard Time" },
            { "DK", "Romance Standard Time" },
            { "FI", "FLE Standard Time" },
            { "PT", "GMT Standard Time" },
            { "JP", "Tokyo Standard Time" },
            { "CA", "Eastern Standard Time" },
            { "AU", "AUS Eastern Standard Time" },
            { "BR", "E. South America Standard Time" },
            { "IN", "India Standard Time" },
            { "TR", "Turkey Standard Time" },
        };

        // ═══════════════════════════════════════════════════════════════
        //  COUNTRY → UTC OFFSET HOURS (fallback if TimeZoneInfo fails)
        // ═══════════════════════════════════════════════════════════════
        public static readonly Dictionary<string, double> CountryUtcOffsets = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "DE", 1 }, { "US", -5 }, { "GB", 0 }, { "BG", 2 },
            { "FR", 1 }, { "ES", 1 }, { "IT", 1 }, { "NL", 1 },
            { "AT", 1 }, { "CH", 1 }, { "PL", 1 }, { "CZ", 1 },
            { "RO", 2 }, { "HU", 1 }, { "SE", 1 }, { "NO", 1 },
            { "DK", 1 }, { "FI", 2 }, { "PT", 0 }, { "JP", 9 },
            { "CA", -5 }, { "AU", 10 }, { "BR", -3 }, { "IN", 5.5 },
            { "TR", 3 },
        };

        // ═══════════════════════════════════════════════════════════════
        //  GET LOCAL TIME FOR A COUNTRY
        // ═══════════════════════════════════════════════════════════════
        /// <summary>
        /// Returns the current local time for the given country code.
        /// Uses Windows TimeZoneInfo when available, falls back to UTC offset.
        /// </summary>
        public static DateTime GetLocalTime(string countryCode)
        {
            // [PublicHolidays] Get local time for specified country with timezone conversion
            if (string.IsNullOrEmpty(countryCode))
            {
//                 DebugLogger.Log("[PublicHolidays] Country code is null/empty - returning local machine time");
                return DateTime.Now;
            }

            // Try Windows TimeZoneInfo first (handles DST automatically)
            if (CountryTimezones.TryGetValue(countryCode, out var tzId))
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                    var localTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
//                     DebugLogger.Log($"[PublicHolidays] GetLocalTime({countryCode}): {localTime:HH:mm:ss} via TimeZoneInfo");
                    return localTime;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[PublicHolidays] TimeZoneInfo lookup failed for {countryCode}: {ex.Message} - trying fallback");
                }
            }

            // Fallback: use static UTC offset (no DST)
            if (CountryUtcOffsets.TryGetValue(countryCode, out var offset))
            {
                var localTime = DateTime.UtcNow.AddHours(offset);
//                 DebugLogger.Log($"[PublicHolidays] GetLocalTime({countryCode}): {localTime:HH:mm:ss} via UTC offset fallback");
                return localTime;
            }

//             DebugLogger.Log($"[PublicHolidays] Country code not found: {countryCode} - returning local machine time");
            return DateTime.Now; // Unknown country — use local machine time
        }

        /// <summary>
        /// Returns a formatted local time string like "14:32" for the given country.
        /// </summary>
        public static string GetLocalTimeString(string countryCode)
        {
            // [PublicHolidays] Get formatted local time string (HH:mm) for country
            var localTime = GetLocalTime(countryCode);
            var result = localTime.ToString("HH:mm");
//             DebugLogger.Log($"[PublicHolidays] GetLocalTimeString({countryCode}): {result}");
            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        //  GET HOLIDAYS FOR A COUNTRY IN A GIVEN YEAR
        // ═══════════════════════════════════════════════════════════════
        /// <summary>
        /// Returns a list of public holiday dates for the given country and year.
        /// Includes fixed-date holidays and computed Easter-based holidays.
        /// </summary>
        public static List<DateTime> GetHolidays(string countryCode, int year)
        {
            // [PublicHolidays] Get all public holidays for specified country and year
//             DebugLogger.Log($"[PublicHolidays] GetHolidays({countryCode}, {year})");
            if (string.IsNullOrEmpty(countryCode))
            {
//                 DebugLogger.Log("[PublicHolidays] Country code is null/empty - returning empty holiday list");
                return new List<DateTime>();
            }

            var holidays = new List<DateTime>();
            var easter = ComputeEaster(year);
//             DebugLogger.Log($"[PublicHolidays] Easter for {year}: {easter:yyyy-MM-dd}");

            switch (countryCode.ToUpper())
            {
                case "DE": // Germany
                    holidays.Add(new DateTime(year, 1, 1));   // Neujahr
                    holidays.Add(easter.AddDays(-2));          // Karfreitag
                    holidays.Add(easter.AddDays(1));           // Ostermontag
                    holidays.Add(new DateTime(year, 5, 1));    // Tag der Arbeit
                    holidays.Add(easter.AddDays(39));           // Christi Himmelfahrt
                    holidays.Add(easter.AddDays(50));           // Pfingstmontag
                    holidays.Add(new DateTime(year, 10, 3));   // Tag der Deutschen Einheit
                    holidays.Add(new DateTime(year, 12, 25));  // 1. Weihnachtstag
                    holidays.Add(new DateTime(year, 12, 26));  // 2. Weihnachtstag
                    break;

                case "US": // United States
                    holidays.Add(new DateTime(year, 1, 1));    // New Year's Day
                    holidays.Add(GetNthWeekday(year, 1, DayOfWeek.Monday, 3));  // MLK Day
                    holidays.Add(GetNthWeekday(year, 2, DayOfWeek.Monday, 3));  // Presidents' Day
                    holidays.Add(GetLastWeekday(year, 5, DayOfWeek.Monday));    // Memorial Day
                    holidays.Add(new DateTime(year, 6, 19));   // Juneteenth
                    holidays.Add(new DateTime(year, 7, 4));    // Independence Day
                    holidays.Add(GetNthWeekday(year, 9, DayOfWeek.Monday, 1));  // Labor Day
                    holidays.Add(GetNthWeekday(year, 11, DayOfWeek.Thursday, 4)); // Thanksgiving
                    holidays.Add(new DateTime(year, 12, 25));  // Christmas
                    break;

                case "GB": // United Kingdom
                    holidays.Add(new DateTime(year, 1, 1));    // New Year's Day
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(GetNthWeekday(year, 5, DayOfWeek.Monday, 1));  // Early May
                    holidays.Add(GetLastWeekday(year, 5, DayOfWeek.Monday));    // Spring
                    holidays.Add(GetLastWeekday(year, 8, DayOfWeek.Monday));    // Summer
                    holidays.Add(new DateTime(year, 12, 25));  // Christmas
                    holidays.Add(new DateTime(year, 12, 26));  // Boxing Day
                    break;

                case "BG": // Bulgaria
                    holidays.Add(new DateTime(year, 1, 1));    // New Year
                    holidays.Add(new DateTime(year, 3, 3));    // Liberation Day
                    holidays.Add(easter.AddDays(-2));           // Good Friday (Orthodox approx)
                    holidays.Add(easter);                       // Easter Sunday
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(new DateTime(year, 5, 6));    // St. George's Day
                    holidays.Add(new DateTime(year, 5, 24));   // Culture Day
                    holidays.Add(new DateTime(year, 9, 6));    // Unification Day
                    holidays.Add(new DateTime(year, 9, 22));   // Independence Day
                    holidays.Add(new DateTime(year, 12, 24));  // Christmas Eve
                    holidays.Add(new DateTime(year, 12, 25));  // Christmas
                    holidays.Add(new DateTime(year, 12, 26));  // 2nd Christmas
                    break;

                case "FR": // France
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(new DateTime(year, 5, 8));    // Victory Day
                    holidays.Add(easter.AddDays(39));           // Ascension
                    holidays.Add(easter.AddDays(50));           // Whit Monday
                    holidays.Add(new DateTime(year, 7, 14));   // Bastille Day
                    holidays.Add(new DateTime(year, 8, 15));   // Assumption
                    holidays.Add(new DateTime(year, 11, 1));   // All Saints
                    holidays.Add(new DateTime(year, 11, 11));  // Armistice
                    holidays.Add(new DateTime(year, 12, 25));
                    break;

                case "ES": // Spain
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(new DateTime(year, 1, 6));    // Epiphany
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(new DateTime(year, 8, 15));   // Assumption
                    holidays.Add(new DateTime(year, 10, 12));  // National Day
                    holidays.Add(new DateTime(year, 11, 1));   // All Saints
                    holidays.Add(new DateTime(year, 12, 6));   // Constitution Day
                    holidays.Add(new DateTime(year, 12, 8));   // Immaculate Conception
                    holidays.Add(new DateTime(year, 12, 25));
                    break;

                case "IT": // Italy
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(new DateTime(year, 1, 6));    // Epiphany
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 4, 25));   // Liberation Day
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(new DateTime(year, 6, 2));    // Republic Day
                    holidays.Add(new DateTime(year, 8, 15));   // Ferragosto
                    holidays.Add(new DateTime(year, 11, 1));   // All Saints
                    holidays.Add(new DateTime(year, 12, 8));   // Immaculate Conception
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));  // St. Stephen
                    break;

                case "NL": // Netherlands
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 4, 27));   // King's Day
                    holidays.Add(easter.AddDays(39));           // Ascension
                    holidays.Add(easter.AddDays(50));           // Whit Monday
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "AT": // Austria
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(new DateTime(year, 1, 6));    // Epiphany
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(easter.AddDays(39));           // Ascension
                    holidays.Add(easter.AddDays(50));           // Whit Monday
                    holidays.Add(easter.AddDays(60));           // Corpus Christi
                    holidays.Add(new DateTime(year, 8, 15));   // Assumption
                    holidays.Add(new DateTime(year, 10, 26));  // National Day
                    holidays.Add(new DateTime(year, 11, 1));   // All Saints
                    holidays.Add(new DateTime(year, 12, 8));   // Immaculate Conception
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "CH": // Switzerland
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(easter.AddDays(39));           // Ascension
                    holidays.Add(easter.AddDays(50));           // Whit Monday
                    holidays.Add(new DateTime(year, 8, 1));    // National Day
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "PL": // Poland
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(new DateTime(year, 1, 6));    // Epiphany
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(new DateTime(year, 5, 3));    // Constitution Day
                    holidays.Add(easter.AddDays(60));           // Corpus Christi
                    holidays.Add(new DateTime(year, 8, 15));   // Assumption
                    holidays.Add(new DateTime(year, 11, 1));   // All Saints
                    holidays.Add(new DateTime(year, 11, 11));  // Independence Day
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "CZ": // Czech Republic
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(new DateTime(year, 5, 8));    // Liberation Day
                    holidays.Add(new DateTime(year, 7, 5));    // Cyril & Methodius
                    holidays.Add(new DateTime(year, 7, 6));    // Jan Hus Day
                    holidays.Add(new DateTime(year, 9, 28));   // Statehood Day
                    holidays.Add(new DateTime(year, 10, 28));  // Independence Day
                    holidays.Add(new DateTime(year, 11, 17));  // Freedom Day
                    holidays.Add(new DateTime(year, 12, 24));
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "RO": // Romania
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(new DateTime(year, 1, 2));
                    holidays.Add(new DateTime(year, 1, 24));   // Unification Day
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(new DateTime(year, 6, 1));    // Children's Day
                    holidays.Add(easter.AddDays(50));           // Whit Monday
                    holidays.Add(new DateTime(year, 8, 15));   // Assumption
                    holidays.Add(new DateTime(year, 11, 30));  // St. Andrew
                    holidays.Add(new DateTime(year, 12, 1));   // National Day
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "HU": // Hungary
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(new DateTime(year, 3, 15));   // Revolution Day
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(easter.AddDays(50));           // Whit Monday
                    holidays.Add(new DateTime(year, 8, 20));   // St. Stephen
                    holidays.Add(new DateTime(year, 10, 23));  // Republic Day
                    holidays.Add(new DateTime(year, 11, 1));   // All Saints
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "SE": // Sweden
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(new DateTime(year, 1, 6));    // Epiphany
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(easter.AddDays(39));           // Ascension
                    holidays.Add(new DateTime(year, 6, 6));    // National Day
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "NO": // Norway
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(easter.AddDays(-3));           // Maundy Thursday
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(new DateTime(year, 5, 17));   // Constitution Day
                    holidays.Add(easter.AddDays(39));           // Ascension
                    holidays.Add(easter.AddDays(50));           // Whit Monday
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "DK": // Denmark
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(easter.AddDays(-3));           // Maundy Thursday
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(easter.AddDays(26));           // General Prayer Day
                    holidays.Add(easter.AddDays(39));           // Ascension
                    holidays.Add(easter.AddDays(50));           // Whit Monday
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "FI": // Finland
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(new DateTime(year, 1, 6));    // Epiphany
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 5, 1));    // May Day
                    holidays.Add(easter.AddDays(39));           // Ascension
                    holidays.Add(new DateTime(year, 12, 6));   // Independence Day
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));
                    break;

                case "PT": // Portugal
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter);                       // Easter Sunday
                    holidays.Add(new DateTime(year, 4, 25));   // Freedom Day
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(easter.AddDays(60));           // Corpus Christi
                    holidays.Add(new DateTime(year, 6, 10));   // Portugal Day
                    holidays.Add(new DateTime(year, 8, 15));   // Assumption
                    holidays.Add(new DateTime(year, 10, 5));   // Republic Day
                    holidays.Add(new DateTime(year, 11, 1));   // All Saints
                    holidays.Add(new DateTime(year, 12, 1));   // Restoration
                    holidays.Add(new DateTime(year, 12, 8));   // Immaculate Conception
                    holidays.Add(new DateTime(year, 12, 25));
                    break;

                case "JP": // Japan
                    holidays.Add(new DateTime(year, 1, 1));    // New Year
                    holidays.Add(GetNthWeekday(year, 1, DayOfWeek.Monday, 2)); // Coming of Age
                    holidays.Add(new DateTime(year, 2, 11));   // National Foundation
                    holidays.Add(new DateTime(year, 2, 23));   // Emperor's Birthday
                    holidays.Add(new DateTime(year, 4, 29));   // Showa Day
                    holidays.Add(new DateTime(year, 5, 3));    // Constitution Memorial
                    holidays.Add(new DateTime(year, 5, 4));    // Greenery Day
                    holidays.Add(new DateTime(year, 5, 5));    // Children's Day
                    holidays.Add(GetNthWeekday(year, 7, DayOfWeek.Monday, 3)); // Marine Day
                    holidays.Add(new DateTime(year, 8, 11));   // Mountain Day
                    holidays.Add(GetNthWeekday(year, 9, DayOfWeek.Monday, 3)); // Respect for Aged
                    holidays.Add(new DateTime(year, 10, 14));  // Sports Day (approx)
                    holidays.Add(new DateTime(year, 11, 3));   // Culture Day
                    holidays.Add(new DateTime(year, 11, 23));  // Labour Thanksgiving
                    break;

                case "CA": // Canada
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(GetNthWeekday(year, 2, DayOfWeek.Monday, 3)); // Family Day
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(GetLastWeekday(year, 5, DayOfWeek.Monday));   // Victoria Day (approx)
                    holidays.Add(new DateTime(year, 7, 1));    // Canada Day
                    holidays.Add(GetNthWeekday(year, 9, DayOfWeek.Monday, 1)); // Labour Day
                    holidays.Add(GetNthWeekday(year, 10, DayOfWeek.Monday, 2)); // Thanksgiving
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));  // Boxing Day
                    break;

                case "AU": // Australia
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(new DateTime(year, 1, 26));   // Australia Day
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(easter.AddDays(-1));           // Saturday before Easter
                    holidays.Add(easter.AddDays(1));            // Easter Monday
                    holidays.Add(new DateTime(year, 4, 25));   // Anzac Day
                    holidays.Add(GetNthWeekday(year, 6, DayOfWeek.Monday, 2)); // Queen's Birthday
                    holidays.Add(new DateTime(year, 12, 25));
                    holidays.Add(new DateTime(year, 12, 26));  // Boxing Day
                    break;

                case "BR": // Brazil
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(easter.AddDays(-47));          // Carnival Monday
                    holidays.Add(easter.AddDays(-46));          // Carnival Tuesday
                    holidays.Add(easter.AddDays(-2));           // Good Friday
                    holidays.Add(new DateTime(year, 4, 21));   // Tiradentes
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(easter.AddDays(60));           // Corpus Christi
                    holidays.Add(new DateTime(year, 9, 7));    // Independence Day
                    holidays.Add(new DateTime(year, 10, 12));  // Our Lady
                    holidays.Add(new DateTime(year, 11, 2));   // All Souls
                    holidays.Add(new DateTime(year, 11, 15));  // Republic Day
                    holidays.Add(new DateTime(year, 12, 25));
                    break;

                case "IN": // India
                    holidays.Add(new DateTime(year, 1, 26));   // Republic Day
                    holidays.Add(new DateTime(year, 8, 15));   // Independence Day
                    holidays.Add(new DateTime(year, 10, 2));   // Gandhi Jayanti
                    holidays.Add(new DateTime(year, 12, 25));  // Christmas
                    // Note: Many Indian holidays are lunar-based; these are the 4 national gazetted holidays
                    break;

                case "TR": // Turkey
                    holidays.Add(new DateTime(year, 1, 1));
                    holidays.Add(new DateTime(year, 4, 23));   // National Sovereignty
                    holidays.Add(new DateTime(year, 5, 1));    // Labour Day
                    holidays.Add(new DateTime(year, 5, 19));   // Youth & Sports
                    holidays.Add(new DateTime(year, 7, 15));   // Democracy Day
                    holidays.Add(new DateTime(year, 8, 30));   // Victory Day
                    holidays.Add(new DateTime(year, 10, 29));  // Republic Day
                    break;
            }

//             DebugLogger.Log($"[PublicHolidays] Total holidays for {countryCode}/{year}: {holidays.Count}");
            return holidays;
        }

        // ═══════════════════════════════════════════════════════════════
        //  GET HOLIDAY HOURS IN A WEEK
        // ═══════════════════════════════════════════════════════════════
        /// <summary>
        /// Returns the number of holiday hours (8h per holiday workday) that fall
        /// within the given week for the specified country.
        /// Only counts Mon-Fri holidays (weekends don't count).
        /// </summary>
        /// <param name="countryCode">ISO 2-letter country code</param>
        /// <param name="weekStart">Monday of the week to check</param>
        /// <param name="hoursPerDay">Hours credited per holiday (default 8)</param>
        public static double GetHolidayHoursInWeek(string countryCode, DateTime weekStart, double hoursPerDay = 8.0)
        {
            // [PublicHolidays] Calculate holiday hours in specified week
//             DebugLogger.Log($"[PublicHolidays] GetHolidayHoursInWeek({countryCode}, {weekStart:yyyy-MM-dd})");
            if (string.IsNullOrEmpty(countryCode))
            {
//                 DebugLogger.Log("[PublicHolidays] Country code is null/empty - returning 0 holiday hours");
                return 0;
            }

            var holidays = GetHolidays(countryCode, weekStart.Year);
            var weekEnd = weekStart.AddDays(4); // Mon-Fri only
//             DebugLogger.Log($"[PublicHolidays] Checking week: {weekStart:yyyy-MM-dd} to {weekEnd:yyyy-MM-dd}");

            int holidayCount = 0;
            foreach (var h in holidays)
            {
                if (h.Date >= weekStart.Date && h.Date <= weekEnd.Date &&
                    h.DayOfWeek != DayOfWeek.Saturday && h.DayOfWeek != DayOfWeek.Sunday)
                {
                    holidayCount++;
//                     DebugLogger.Log($"[PublicHolidays] Found workday holiday: {h:yyyy-MM-dd dddd}");
                }
            }

            var totalHours = holidayCount * hoursPerDay;
//             DebugLogger.Log($"[PublicHolidays] Total holiday hours this week: {totalHours}h");
            return totalHours;
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPER: Compute Easter Sunday (Western / Gregorian)
        //  Uses the Anonymous Gregorian algorithm
        // ═══════════════════════════════════════════════════════════════
        private static DateTime ComputeEaster(int year)
        {
            int a = year % 19;
            int b = year / 100;
            int c = year % 100;
            int d = b / 4;
            int e = b % 4;
            int f = (b + 8) / 25;
            int g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4;
            int k = c % 4;
            int l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31;
            int day = ((h + l - 7 * m + 114) % 31) + 1;
            return new DateTime(year, month, day);
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPER: Get Nth weekday of a month (e.g. 3rd Monday of Jan)
        // ═══════════════════════════════════════════════════════════════
        private static DateTime GetNthWeekday(int year, int month, DayOfWeek dayOfWeek, int n)
        {
            var first = new DateTime(year, month, 1);
            int daysUntil = ((int)dayOfWeek - (int)first.DayOfWeek + 7) % 7;
            return first.AddDays(daysUntil + (n - 1) * 7);
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPER: Get last weekday of a month (e.g. last Monday of May)
        // ═══════════════════════════════════════════════════════════════
        private static DateTime GetLastWeekday(int year, int month, DayOfWeek dayOfWeek)
        {
            var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            int daysBack = ((int)last.DayOfWeek - (int)dayOfWeek + 7) % 7;
            return last.AddDays(-daysBack);
        }
    }
}
