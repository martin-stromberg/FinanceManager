namespace FinanceManager.Shared.Extensions
{
    /// <summary>
    /// Extension methods for DateTime.
    /// </summary>
    public static class DateExt
    {
        /// <summary>
        /// Returns a new DateTime representing the first day of the month of the given date.
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static DateTime ToFirstOfMonth(this DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1);
        }
        /// <summary>
        /// Returns a new DateTime representing the last day of the month of the given date.
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static DateTime ToLastOfMonth(this DateTime date)
        {
            int lastDay = DateTime.DaysInMonth(date.Year, date.Month);
            return new DateTime(date.Year, date.Month, lastDay);
        }
        /// <summary>
        /// Returns a new DateTime representing the previous workday (Monday-Friday) of the given date.
        /// If the given date is a workday, it is returned unchanged. If the given date is a Saturday, the previous Friday is returned.
        /// </summary>
        /// <param name="date">The date to evaluate.</param>
        /// <returns></returns>
        public static DateTime ToPreviousWorkday(this DateTime date)
        {
            while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                date = date.AddDays(-1);
            } 
            return date;
        }
        /// <summary>
        /// Returns a new DateTime representing the next workday (Monday-Friday) of the given date.
        /// If the given date is a workday, it is returned unchanged. If the given date is a Saturday, the next Monday is returned.
        /// </summary>
        /// <param name="date">The date to evaluate.</param>
        /// <returns></returns>
        public static DateTime ToNextWorkday(this DateTime date)
        {
            while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                date = date.AddDays(1);
            }
            return date;
        }
    }
}
