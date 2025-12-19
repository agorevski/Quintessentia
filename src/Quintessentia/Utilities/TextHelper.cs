namespace Quintessentia.Utilities
{
    /// <summary>
    /// Provides text manipulation utilities used across the application.
    /// </summary>
    public static class TextHelper
    {
        /// <summary>
        /// Trims leading and trailing non-alphanumeric characters from a string.
        /// </summary>
        /// <param name="text">The text to trim.</param>
        /// <returns>The trimmed text, or the original text if null/empty.</returns>
        public static string TrimNonAlphanumeric(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Find first alphanumeric character from the start
            int start = 0;
            while (start < text.Length && !char.IsLetterOrDigit(text[start]))
            {
                start++;
            }

            // Find last alphanumeric character from the end
            int end = text.Length - 1;
            while (end >= start && !char.IsLetterOrDigit(text[end]))
            {
                end--;
            }

            // Return trimmed substring
            return start <= end ? text.Substring(start, end - start + 1) : string.Empty;
        }
    }
}
