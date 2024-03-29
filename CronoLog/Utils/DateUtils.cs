﻿using System;
using System.Globalization;

namespace CronoLog.Utils
{
    public class DateUtils
    {
        private static void getDuration(long milli, out double minutes, out double hours, out double days)
        {
            minutes = Math.Floor((double)(milli / 60000));
            minutes = (minutes == -1) ? 0 : minutes;
            hours = Math.Floor(minutes / 60);
            days = Math.Floor(hours / 24);
            minutes %= 60;
            hours %= 24;
        }

        public static string DurationString(TimeSpan duration)
        {
            string minutes = (duration.Minutes < 10) ? $"0{duration.Minutes}" : $"{duration.Minutes}";
            string hours = (duration.Hours < 10) ? $"0{duration.Hours}" : $"{duration.Hours}";
            string days = (duration.Days < 10) ? $"0{duration.Days}" : $"{duration.Days}";

            return $"{days}:{hours}:{minutes}";
        }
        public static (string, string) DurationHoursMinutes(TimeSpan duration)
        {
            string minutes = (duration.Minutes < 10) ? $"0{duration.Minutes}" : duration.Minutes.ToString();
            int days = duration.Days * 24;
            string hours = (duration.Hours + days < 10) ? $"0{duration.Hours + days}" : $"{duration.Hours + days}";
            return (hours, minutes);
        }
        public static string DurationHoursMinutesStringH(TimeSpan duration)
        {
            var (hours, minutes) = DurationHoursMinutes(duration);
            return $"{hours}h {minutes}m";
        }
        public static string HoursDuration(TimeSpan duration)
        {
            var (hours, minutes) = DurationHoursMinutes(duration);
            return $"{hours}:{minutes}:00";
        }
        public static DateTime ToBrSpTimezone(DateTime input)
        {
            if (input.Kind == DateTimeKind.Utc)
            {
                var output = TimeZoneInfo.ConvertTimeFromUtc(input, GetBrSpTimezone());
                return output;
            }
            else
                return input;

#if DEBUG
#else
#endif
        }
        public static DateTime ToDbSaveTime(DateTime date)
        {
            var tz = GetBrSpTimezone();
            if (date.Kind == DateTimeKind.Utc)
            {
                return date;
            }
            else
            {
                return TimeZoneInfo.ConvertTimeToUtc(date, GetBrSpTimezone());
            }
        }

        public static TimeZoneInfo GetBrSpTimezone()
        {
#if DEBUG
            return TimeZoneInfo.Local;
#else
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
#endif
        }
    }
}
