namespace UnityEngine.ProBuilder
{
    /// <summary>
    /// Information about this build of ProBuilder.
    /// </summary>
    static class Version
    {
        internal static readonly SemVer currentInfo = new SemVer("4.1.0-preview.1", "2019/04/09");

        /// <summary>
        /// Get the current version.
        /// </summary>
        /// <returns>The current version string in semantic version format.</returns>
        public static string current
        {
            get { return currentInfo.ToString(); }
        }
    }
}
