using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ParseHtmlTables
{
    class Program
    {
        const string IdUrlFormat = @"http://aps.unmc.edu/AP/database/antiB.php?page={0}";
        const string TableUrlFormat = @"http://aps.unmc.edu/AP/database/query_output.php?ID={0:D5}";
        static readonly Regex IdRegex = new Regex(@"query_output\.php\?ID=\d{5}", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly int SubLen = "query_output.php?ID=".Length;
        const int MaxIdPageId = 134;

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
            //Task[] tasks = new Task[MaxIdPageId];
            //for (int i = 0; i < MaxIdPageId; i++)
            //{
            //    tasks[i] = StartWithIdPageAsync(i);
            //}
            //Task.Factory.ContinueWhenAll(tasks,
            //  ts =>
            //  {
            //      Console.WriteLine("Completed: {0}", ts.Count(t => (t.Status == TaskStatus.RanToCompletion)));
            //  });

            StartWithTablePageAsync(1, 0).Wait();

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
            Match match = IdRegex.Match(html);
            List<int> ids = new List<int>();
            while (match.Success)
            {
                ids.Add(int.Parse(match.Value.Substring(SubLen)));
                match = match.NextMatch();
            }
            return ids.ToArray();
        }

        static string FindLast(string text, string tag)
        {
            int eindex = text.LastIndexOf(tag);
            var ret = text.Remove(eindex);
            eindex = ret.LastIndexOf(tag);
            ret = ret.Substring(eindex + tag.Length);
            var bsindex = ret.IndexOf('>');
            var esindex = ret.LastIndexOf('<');
            return ret.Substring(bsindex + 1, esindex - bsindex - 1);
        }

        static string[] ParseTable(string html)
        {
            List<string> rets = new List<string>();
            var tbody = FindLast(html, "table").Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("<P>", "").Replace("<p>", "").Replace(@"&nbsp;", "");
            tbody = Regex.Replace(tbody, @"<a\s+(?:[^>]*?\s+)?href=""([^""]*)""", "");
            int indexTr1 = 0, indexTr2 = -4;
            while (true)
            {
                indexTr1 = tbody.IndexOf("<tr", indexTr2 + 4);
                if (indexTr1 == -1)
                    break;
                indexTr2 = tbody.IndexOf(@"</tr", indexTr1 + 4);
                int indexTd1 = 0, indexTd2 = indexTr1 + 3 - 4;
                string line = "";
                while (true)
                {
                    indexTd1 = tbody.IndexOf("<td", indexTd2 + 4);
                    if (indexTd1 == -1)
                        break;
                    indexTd2 = tbody.IndexOf(@"</td", indexTd1);
                    int temp = tbody.IndexOf(">", indexTd1) + 1;
                    var str = tbody.Substring(temp, indexTd2 - temp).Trim();
                    if (!string.IsNullOrEmpty(str))
                        line += str + "\t";
                }
                line.TrimEnd('\t');
                if (line != "")
                    rets.Add(line);
            }
            //tbody = Regex.Replace(tbody, @"\s+", " ");
            return rets.ToArray();
        }

        static string GetPath(int tablePageId, int idPageId)
        {
            return Path.Combine(idPageId + "", tablePageId + ".txt");
        }

        static void WriteTable(string[] lines, int tablePageId, int idPageId)
        {
            if (lines != null && lines.Length != 0)
            {
                string path = GetPath(tablePageId, idPageId);
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, lines);
            }
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
            await tablePageTask.ContinueWith(x => WriteTable(ParseTable(x.Result), tablePageId, idPageId));
        }
    }
}
