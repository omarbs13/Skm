using S22.Imap;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace ReadMail
{
    public class Program
    {
        private static readonly string _skmInfoVariable = Environment.GetEnvironmentVariable("SmkInfo", EnvironmentVariableTarget.User);
        private static LoggerAdapter _log = new LoggerAdapter();
        public static SkmInfo _smkInfo = new SkmInfo();

        static void Main(string[] args)
        {
            Console.WriteLine("Se inicia el proceso");
            if (!string.IsNullOrEmpty(_skmInfoVariable))
            {
                var info = _skmInfoVariable.Split(';').ToArray();
                _smkInfo.HostName = info[0].ToString();
                _smkInfo.Port = int.Parse(info[1].ToString());
                _smkInfo.UserName = info[2].ToString();
                _smkInfo.Password = info[3].ToString();
                _smkInfo.Subject = info[4].ToString();
                _smkInfo.Path = info[5].ToString();

                if (!Directory.Exists(_smkInfo.Path + "UnSeen"))
                    System.IO.Directory.CreateDirectory(_smkInfo.Path + "UnSeen");
                if (!Directory.Exists(_smkInfo.Path + "Upload"))
                    System.IO.Directory.CreateDirectory(_smkInfo.Path + "Upload");
                LogInLogOut();
            }
            else
            {
                Console.WriteLine("Favor de agregar la configuración de la variable de entorno SmkInfo");
            }
            Console.WriteLine("Fin del programa");
        }

        private static void ReadExcel(uint uid, string fileName)
        {
            var files = new List<string>();
            var hasHeader = true;
            try
            {
                Console.WriteLine("Se lee el excel de datos");
                foreach (string file in Directory.GetFiles(_smkInfo.Path + "UnSeen", "*.*"))
                {
                    files.Add(file);
                }

                foreach (var item in files)
                {
                    DataTable tbl = new DataTable();
                    //Se procesa el archivo
                    using (var pck = new OfficeOpenXml.ExcelPackage())
                    {
                        using (var stream = File.OpenRead(item))
                        {
                            pck.Load(stream);
                        }
                        var ws = pck.Workbook.Worksheets.First();

                        int initCell = 1;
                        for (int i = 1; i < 50; i++)
                        {
                            var res = ws.Cells[i, 1].Text;
                            if (res.Equals("Incident ID"))
                                initCell = i;
                        }

                        foreach (var firstRowCell in ws.Cells[initCell, 1, 1, ws.Dimension.End.Column])
                        {
                            tbl.Columns.Add(hasHeader ? firstRowCell.Text : string.Format("Column {0}", firstRowCell.Start.Column));
                        }
                        var startRow = hasHeader ? initCell + 1 : 1;
                        for (int rowNum = startRow; rowNum <= ws.Dimension.End.Row; rowNum++)
                        {
                            var wsRow = ws.Cells[rowNum, 1, rowNum, ws.Dimension.End.Column];
                            DataRow row = tbl.Rows.Add();
                            foreach (var cell in wsRow)
                            {
                                row[cell.Start.Column - 1] = cell.Text;
                            }
                        }
                    }


                    InsertData(tbl, uid, fileName);

                    //Se mueve el archivo
                    string destinationPath = item.Replace("UnSeen", "Upload");
                    System.IO.File.Move(item, destinationPath);
                    File.Delete(item);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex.Message, "ReadExcel<<--Lee el archivo excel para convertirlo en datatable-->>");
            }
        }

        private static void InsertData(DataTable tbl, uint uid, string fileName)
        {
            var emp = (from DataRow row in tbl.Rows

                       select new Tickets
                       {
                           AssignUserID = row["Assign User ID"].ToString(),
                           BriefDescription = row["Brief Description"].ToString(),
                           Category = row["Category"].ToString(),
                           CauseCode = row["Cause Code"].ToString(),
                           ContactEmail = row["Contact Email"].ToString(),
                           ContactFirstName = row["Contact FirstName"].ToString(),
                           ContactLastName = row["Contact Last Name"].ToString(),
                           ContactUserID = row["Contact User ID"].ToString(),
                           Impact = row["Impact"].ToString(),
                           IncidentID = row["Incident ID"].ToString(),
                           Location = row["Location"].ToString(),
                           NotifyBy = row["Notify By"].ToString(),
                           OpenTime = row["Open Time"].ToString(),
                           Priority = row["Priority"].ToString(),
                           ResolutionCode = row["Resolution Code"].ToString(),
                           ResolutionDesc = row["Resolution Desc"].ToString(),
                           ResolveGroupName = row["Resolve Group Name"].ToString(),
                           ResolveTime = row["Resolve Time"].ToString(),
                           ResolveUserID = row["Resolve User ID"].ToString(),
                           ResponsibleGroupName = row["Responsible Group Name"].ToString(),
                           Subcustomer = row["Subcustomer"].ToString(),
                           TicketStatus = row["Ticket Status"].ToString(),
                           VSLABreached = row["VSLABreached"].ToString(),

                       }).ToList();

            try
            {
                Console.WriteLine("Se almacena en BD la información del ticket ");
                InsertOrUpdate(emp, uid, fileName);              
            }
            catch (Exception ex)
            {
                _log.LogError(ex.Message, "InsertData<<--Inserta el ticket-->>");
            }

        }

        public static void LogInLogOut()
        {
            try
            {
                Mail email = new Mail();
                Console.WriteLine("Se lee el email: " + _smkInfo.UserName);
                using (ImapClient client = new ImapClient(_smkInfo.HostName, _smkInfo.Port, _smkInfo.UserName, _smkInfo.Password, AuthMethod.Login, true))
                {
                    // Returns a collection of identifiers of all mails matching the specified search criteria.
                    uint lastEmail = GetLastUid();
                    IEnumerable<uint> uids = client.Search(SearchCondition.Subject(_smkInfo.Subject).And(SearchCondition.GreaterThan(lastEmail)));
                    uids = GetUidEmail(uids.ToList());

                    List<Mail> messages = new List<Mail>();
                    foreach (var item in uids)
                    {
                        messages.Add(new Mail
                        {
                            Uid = item,
                            MailMessage = client.GetMessage(item)
                        });
                    }
                    foreach (var item in messages)
                    {
                        var date = item.MailMessage.Date();
                        int totalAttachments = item.MailMessage.Attachments.Count;

                        LogEmail logMail = new LogEmail
                        {
                            BodyMail = item.MailMessage.Body,
                            DateMail = DateTime.Parse(item.MailMessage.Date().ToString()),
                            FromMail = item.MailMessage.From.ToString(),
                            IsLoad = false,
                            NoAttachments = item.MailMessage.Attachments.Count,
                            SubjectMail = item.MailMessage.Subject,
                            UidMail = int.Parse(item.Uid.ToString())
                        };
                        saveLogMail(logMail);

                        for (int i = 0; i < totalAttachments; i++)
                        {
                            var stream = item.MailMessage.Attachments[i].ContentStream;

                            // var path = System.IO.Directory.GetCurrentDirectory();
                            var path = _smkInfo.Path + @"UnSeen\";
                            FileStream fileStream = File.Create(path + item.MailMessage.Attachments[i].Name, (int)stream.Length);
                            // Initialize the bytes array with the stream length and then fill it with data
                            byte[] bytesInStream = new byte[stream.Length];
                            stream.Read(bytesInStream, 0, bytesInStream.Length);
                            // Use write method to write to the file specified above
                            fileStream.Write(bytesInStream, 0, bytesInStream.Length);
                            //Close the filestream
                            fileStream.Close();

                            ReadExcel(item.Uid, item.MailMessage.Attachments[i].Name);
                        }


                    }

                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex.Message, "saveLogMail<<--Lee el correo inserta en BD descarga los attachments-->>");
            }
        }

        private static void saveLogMail(LogEmail logMail)
        {
            try
            {
                using (var context = new TicketDbEntities())
                {
                    context.LogEmail.Add(logMail);
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex.Message, "saveLogMail<<-- Guarda en Bd los emails -->>");

            }
        }

        private static List<uint> GetUidEmail(List<uint> uids)
        {
            try
            {
                using (var context = new TicketDbEntities())
                {
                    var ids = uids.ConvertAll(i => (int)i);
                    var mailsUids = context.LogEmail.Where(x => ids.Contains(x.UidMail)).Select(x => x.UidMail).ToList();

                    mailsUids.ForEach(x => ids.Remove(x));
                    return ids.ConvertAll(i => (uint)i);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex.Message, "GetUidEmail<<-- Elimina los emails leidos que ya han sido procesados -->>");
                return uids;
            }

        }

        private static uint GetLastUid()
        {
            try
            {
                using (var context = new TicketDbEntities())
                {
                    var mailsUids = context.LogEmail.Max(p => p.UidMail);
                    return (uint)mailsUids;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex.Message, "GetLastUid <<--Obtiene el ultimo uid del email registrado -->>");
                return 0;
            }
        }

        private static void InsertOrUpdate(List<Tickets> tickets, uint uid, string fileName)
        {
            var ticketsId = tickets.Select(x => x.IncidentID);
            
            using (var context = new TicketDbEntities())
            {
                var existTickes = context.Tickets.Where(x => ticketsId.Contains(x.IncidentID));

                if (existTickes.Any())
                {
                    foreach (var item in existTickes)
                    {
                        var ticket = tickets.FirstOrDefault(x => x.IncidentID == item.IncidentID);
                        var ticketToUpdate = context.Tickets.FirstOrDefault(x => x.IncidentID == item.IncidentID);
                        ticketToUpdate.ResolutionCode = ticket.ResolutionCode;
                        ticketToUpdate.ResolutionDesc = ticket.ResolutionDesc;
                        ticketToUpdate.ResolveUserID = ticket.ResolveUserID;
                        ticketToUpdate.TicketStatus = ticket.TicketStatus;
                        ticketToUpdate.ResolveTime = ticket.ResolveTime;
                    }
                    context.SaveChanges();
                    tickets.RemoveAll(x => existTickes.Select(y => x.IncidentID).Contains(x.IncidentID));
                }

                if (tickets.Any())
                {
                    context.Tickets.AddRange(tickets);
                    var logMail = context.LogEmail.FirstOrDefault(x => x.UidMail == uid);
                    logMail.IsLoad = true;
                    logMail.FIlesName += fileName + ", ";
                    context.SaveChanges();
                }
            }
        }
    }
}
