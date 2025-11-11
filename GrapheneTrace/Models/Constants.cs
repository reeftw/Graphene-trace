namespace GrapheneTrace.Models
{
    /// <summary>
    /// Application-wide constants used for data validation and business logic
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Size of the pressure sensor matrix (32x32)
        /// </summary>
        public const int MATRIX_SIZE = 32;

        /// <summary>
        /// Pressure threshold (in mmHg) above which alerts are generated
        /// </summary>
        public const int ALERT_THRESHOLD = 200;

        /// <summary>
        /// Minimum pressure (in mmHg) considered for contact area calculation
        /// </summary>
        public const int MIN_CONTACT_PRESSURE = 10;
    }
}