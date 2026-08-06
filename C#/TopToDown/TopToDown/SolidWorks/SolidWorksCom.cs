using System.Runtime.InteropServices;

namespace SwFeatureDebug
{
    internal static class SolidWorksCom
    {
        public static void Release(object value)
        {
            if (value == null || !Marshal.IsComObject(value))
                return;

            try
            {
                Marshal.ReleaseComObject(value);
            }
            catch (InvalidComObjectException)
            {
            }
        }
    }
}
