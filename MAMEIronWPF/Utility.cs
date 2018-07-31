using System;
using System.IO;

namespace MAMEIronWPF
{
    public class Utility
    {
        private string _logfile;
        public Utility(string logfile)
        {
            _logfile = logfile;
        }
        public void WriteToLogFile(string text)
        {
            using (StreamWriter sw = File.AppendText(_logfile))
            {
                sw.WriteLine($"{DateTime.Now}\t{text}");
                sw.Flush();
                sw.Close();
            }
        }
    }
}
