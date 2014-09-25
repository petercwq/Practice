using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ParseHtmlTables
{
    class Program
    {
        const string IdUrlFormat = @"http://aps.unmc.edu/AP/database/antiB.php?page={0}";
        const string TableUrlFormat = @"http://aps.unmc.edu/AP/database/query_output.php?ID={0:D5}";

        const int MaxIdPageId = 133;

        static Uri GetIdUri(int id)
        {
            return new Uri(string.Format(IdUrlFormat, id));
        }

        static Uri GetTableUri(int id)
        {
            return new Uri(string.Format(TableUrlFormat, id));
        }

        static void Main(string[] args)
        {
            Task[] tasks = new Task[MaxIdPageId];
            for (int i = 0; i <= MaxIdPageId; i++)
            {
                tasks[i] = StartWithIdPageAsync(i);
            }
            Task.Factory.ContinueWhenAll(tasks,
              ts =>
              {
                  Console.WriteLine("Completed: {0}", ts.Count(t => (t.Status == TaskStatus.RanToCompletion)));
              });

            Console.ReadKey();
        }

        static string DownloadString(Uri address)
        {
            using (WebClient client = new WebClient())
            {
                string htmlCode = client.DownloadString(address);
                return htmlCode;
            }
        }

        static Task<string> DownloadStringAsTask(Uri address)
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            WebClient client = new WebClient();
            client.DownloadStringCompleted += (sender, args) =>
            {
                if (args.Error != null) tcs.SetException(args.Error);
                else if (args.Cancelled) tcs.SetCanceled();
                else tcs.SetResult(args.Result);
            };
            client.DownloadStringAsync(address);
            return tcs.Task;
        }

        static int[] ParseIds(string html)
        {
            throw new NotImplementedException();
        }

        static string[] ParseTable(string html)
        {
            throw new NotImplementedException();
        }

        static string GetPath(int tablePageId, int idPageId)
        {
            return Path.Combine(idPageId + "", tablePageId + ".txt");
        }

        static void WriteTable(string[] lines, int tablePageId, int idPageId)
        {
            File.WriteAllLines(GetPath(tablePageId, idPageId), lines);
        }

        async static Task StartWithIdPageAsync(int idPageId)
        {
            Uri address = GetIdUri(idPageId);
            Task<string> idPageTask = DownloadStringAsTask(address);
            int[] ids = await idPageTask.ContinueWith(x => ParseIds(x.Result));
            foreach (var id in ids)
            {
                await StartWithTablePageAsync(id, idPageId);
            }
        }

        async static Task StartWithTablePageAsync(int tablePageId, int idPageId = -1)
        {
            Uri address = GetTableUri(tablePageId);
            Task<string> tablePageTask = DownloadStringAsTask(address);
            string[] table = await tablePageTask.ContinueWith(x => ParseTable(x.Result));
            WriteTable(table, tablePageId, idPageId);
        }
    }
}
