using System;

namespace TiffanyGame
{
    public static class WebDebugQuery
    {
        public const string Define = "TIFFANY_WEB_DEBUG";

        public static bool Has(string url, string key) => FindValue(url, key, out _, out _);

        public static string Get(string url, string key) => FindValue(url, key, out int start, out int length) && length > 0
            ? Uri.UnescapeDataString(url.Substring(start, length).Replace('+', ' ')) : null;

        static bool FindValue(string url, string key, out int valueStart, out int valueLength)
        {
            valueStart = valueLength = 0;
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) return false;
            int end = url.IndexOf('#');
            if (end < 0) end = url.Length;
            int query = url.IndexOf('?');
            if (query < 0 || query >= end) return false;
            int start = query + 1;
            while (start < end)
            {
                int next = url.IndexOf('&', start, end - start);
                if (next < 0) next = end;
                int equals = url.IndexOf('=', start, next - start);
                int nameEnd = equals < 0 ? next : equals;
                if (nameEnd - start == key.Length && string.CompareOrdinal(url, start, key, 0, key.Length) == 0)
                {
                    valueStart = equals < 0 ? next : equals + 1;
                    valueLength = next - valueStart;
                    return true;
                }
                start = next + 1;
            }
            return false;
        }
    }
}
