using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

namespace HENKA88KinectDemo
{
    public static class Log
    {
        static string fileName = "log.txt";
        static Encoding sjisEnc; 
        static StreamWriter writer;

        static Log()
        {
            sjisEnc = Encoding.GetEncoding("Shift_JIS");
        }

        public static void Start()
        {
            writer = new StreamWriter(Log.fileName, false, sjisEnc);
        }

        public static void End()
        {
            writer.Close();
        }

        public static void WriteLine(string msg)
        {
            Console.WriteLine(msg); // コンソールにも出力
            if (writer != null && writer.BaseStream != null)
            {
                writer.WriteLine(msg);
            }
        }

    }
}
