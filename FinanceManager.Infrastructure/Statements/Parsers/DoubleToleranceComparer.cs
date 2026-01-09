namespace FinanceManager.Infrastructure.Statements.Parsers
{
    /// <summary>
    /// Provides an equality comparer for double-precision floating-point values that considers two values equal if
    /// they differ by no more than a specified tolerance.
    /// </summary>
    /// <remarks>Use this comparer to perform equality checks on double values where exact equality is
    /// not appropriate due to the imprecision of floating-point arithmetic. The comparer is suitable for scenarios
    /// such as unit testing or collections where approximate equality is required. All values are considered to
    /// have the same hash code, which may affect the performance of hash-based collections such as dictionaries or
    /// hash sets.</remarks>
    public class DoubleToleranceComparer : IEqualityComparer<double>
    {
        private readonly double _tolerance;
        /// <summary>
        /// Initializes a new instance of the DoubleToleranceComparer class with the specified comparison tolerance.
        /// </summary>
        /// <remarks>Use this constructor to define the precision used when comparing double
        /// values for equality. A smaller tolerance results in stricter comparisons.</remarks>
        /// <param name="tolerance">The maximum difference between two double values for them to be considered equal. Must be greater than
        /// or equal to 0.</param>
        public DoubleToleranceComparer(double tolerance)
        {
            _tolerance = tolerance;
        }
        /// <summary>
        /// Determines whether two double-precision floating-point numbers are equal within a specified tolerance.
        /// </summary>
        /// <remarks>Use this method to compare double values for equality when exact matches are
        /// unreliable due to floating-point precision errors. The tolerance value is defined by the instance and
        /// affects the comparison result.</remarks>
        /// <param name="x">The first double-precision floating-point number to compare.</param>
        /// <param name="y">The second double-precision floating-point number to compare.</param>
        /// <returns>true if the absolute difference between x and y is less than or equal to the configured tolerance;
        /// otherwise, false.</returns>
        public bool Equals(double x, double y)
        {
            return Math.Abs(x - y) <= _tolerance;
        }
        /// <summary>
        /// Returns a hash code for the specified double-precision floating-point value.
        /// </summary>
        /// <remarks>This implementation always returns 0, which forces equality comparisons to
        /// rely solely on the Equals method. As a result, using this comparer in hash-based collections may lead to
        /// performance degradation due to hash collisions.</remarks>
        /// <param name="obj">The double-precision floating-point value for which to get a hash code.</param>
        /// <returns>A hash code for the specified value.</returns>
        public int GetHashCode(double obj)
        {
            return 0; // erzwingt Vergleich über Equals
        }
    }
}
