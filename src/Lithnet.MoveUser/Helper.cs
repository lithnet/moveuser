using System;
using System.Collections.Generic;

namespace Lithnet.Moveuser
{
    internal static class Helper
    {
        [System.Diagnostics.DebuggerStepThrough]
        public static bool StringIsNullOrWhiteSpace(string text)
        {
            if (text == null) return true;

            text = text.Trim();
            text = text.Replace("\0", string.Empty);
            return string.IsNullOrEmpty(text);
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static bool EnumTryParse(string value, ref object enumObject)
        {
            object retval = null;

            try
            {
                retval = Enum.Parse(enumObject.GetType(), value, true);
            }
            catch (Exception)
            {
                return false;
            }

            enumObject = retval;

            return true;
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static bool EnumHasFlag(int flag, object enumObject)
        {
            return ((int)enumObject & flag) == flag;
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static bool IsNullOrWhiteSpace(this string t)
        {
            if (t == null) return true;

            t = t.Trim();
            t = t.Replace("\0", string.Empty);
            return string.IsNullOrEmpty(t);
        }
    }
}
