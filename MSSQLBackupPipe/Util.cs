using System;
using System.Collections.Generic;
using System.Text;

namespace MSSQLBackupPipe
{
    public static class Util
    {
        public static void WriteError(Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.GetType().FullName);
            Console.WriteLine(e.StackTrace);
            if (e.InnerException != null)
            {
                WriteError(e.InnerException);
            }
        }
    }
}
