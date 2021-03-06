﻿using System;
using NodaTime;

namespace SS.Common.helpers.extensions
{
    public static class DateTimeOffsetExtensions
    {
        public static DateTimeOffset TranslateDateIfDaylightSavings(this DateTimeOffset date, string timezone, int daysToShift)
        {
            var locationTimeZone = DateTimeZoneProviders.Tzdb[timezone];

            var instant = Instant.FromDateTimeOffset(date);
            var zoned = instant.InZone(locationTimeZone);
            var movedZoned = zoned.Plus(Duration.FromDays(daysToShift));

            if (movedZoned.Offset != zoned.Offset)
                movedZoned = movedZoned.PlusHours(zoned.Offset.ToTimeSpan().Hours - movedZoned.Offset.ToTimeSpan().Hours);
            return movedZoned.ToDateTimeOffset();
        }

        public static double HourDifference(this DateTimeOffset start, DateTimeOffset end, string timezone)
        {
            var locationTimeZone = DateTimeZoneProviders.Tzdb[timezone];
            var instantStart = Instant.FromDateTimeOffset(start);
            var instantEnd = Instant.FromDateTimeOffset(end);
            var zonedStart = instantStart.InZone(locationTimeZone);
            var zonedEnd = instantEnd.InZone(locationTimeZone);
            var duration =  ZonedDateTime.Subtract(zonedEnd, zonedStart);
            return duration.TotalHours;
        }

        public static DateTimeOffset ConvertToTimezone(this DateTimeOffset date, string timezone)
        {
            var locationTimeZone = DateTimeZoneProviders.Tzdb[timezone];
            var instant = Instant.FromDateTimeOffset(date);
            var zoned = instant.InZone(locationTimeZone);
            return zoned.ToDateTimeOffset();
        }

        public static string PrintFormatDateTime(this DateTimeOffset date) => date.ToString("ddd dd MMM yyyy HH:mmzz");
        public static string PrintFormatDate(this DateTimeOffset date) => date.ToString("ddd dd MMM yyyy");
        public static string PrintFormatTime(this DateTimeOffset date) => date.ToString("HH:mmzz");
    }
}
