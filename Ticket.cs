using System;
using System.IO;
using System.Net.Mail;
using System.Reflection;

namespace ReadMail
{
    public class SkmInfo
    {
        public string HostName { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Subject { get; set; }
        public string Path { get; set; }
    }

    public class Mail
    {
        public uint Uid { get; set; }
        public MailMessage MailMessage { get; set; }
    }



    public class LoggerAdapter
    {
        public void LogError(string message, string type)
        {
            LogWrite(message, type);
        }

        private void LogWrite(string logMessage, string type)
        {
            var m_exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            try
            {
                using (StreamWriter w = File.AppendText(m_exePath + "\\" + "log.txt"))
                {
                    Log(logMessage, w, type);
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void Log(string logMessage, TextWriter txtWriter, string type)
        {
            try
            {
                txtWriter.Write("\r\nLog Entry (" + type + ") : ");
                txtWriter.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(),
                    DateTime.Now.ToLongDateString());
                txtWriter.WriteLine("  :");
                txtWriter.WriteLine("  :{0}", logMessage);
                txtWriter.WriteLine("-------------------------------");
            }
            catch (Exception ex)
            {
            }
        }
    }
}
