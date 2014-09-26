using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        const string OutDirectory = "tables";

        static Uri GetIdUri(int id)
        {
            return new Uri(string.Format(IdUrlFormat, id));
        }

        static Uri GetTableUri(int id)
        {
            return new Uri(string.Format(TableUrlFormat, id));
        }

        static string GetPath(int tablePageId, int idPageId)
        {
            return Path.Combine(Path.Combine(OutDirectory, idPageId + ""), tablePageId + ".txt");
        }

        /// <summary>
        /// no args: equals 1 0 134 -- download all valid pages
        /// args: 1 0 130 -- for start from id page id range [0, 130]
        /// args: 2 1234 1280 --for start from table page, id range [1234, 1280]
        /// outputs: ./tables/{pageid}/{tableid}, if start with tableid the pageid will be -1
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            List<Task> tasks = new List<Task>();
            bool wrong = true;
            int type = 1, startId = 0, endId = 134;
            if (args.Length == 0 || (args.Length == 3 && int.TryParse(args[0], out type) && int.TryParse(args[1], out startId) && int.TryParse(args[2], out endId)))
            {
                if (type > 0 && type < 3)
                    wrong = false;
            }
            if (wrong)
                Console.WriteLine("Args error\nHint:\nno args -- equals 1 0 134\nargs: 1 0 130 -- for start from id page id range [0, 130]\nargs: 2 1234 1280 --for start from table page, id range [1234, 1280]");
            else
            {
                Func<int, Task> startAction = null;
                if (type == 1)
                {
                    startAction = x => StartWithIdPageAsync(x);
                }
                else if (type == 2)
                {
                    startAction = x => StartWithTablePageAsync(x);
                }
                if (startAction != null)
                {
                    for (int i = startId; i < endId; i++)
                    {
                        tasks.Add(startAction(i));
                    }
                    Task.Factory.ContinueWhenAll(tasks.ToArray(),
                      ts =>
                      {
                          Console.WriteLine("Completed: {0}", ts.Count(t => (t.Status == TaskStatus.RanToCompletion)));
                      });
                }
            }

            Console.ReadKey();
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
            var text = HttpUtility.StripHTML(html).Trim();
            text = Regex.Replace(text, @" +", " ");
            text = Regex.Replace(text, @"\s{2,}", "\n");
            string[] tmp = text.Split('\n');
            List<string> lines = new List<string>();
            for (int i = 0; i < tmp.Length - 1; i++)
            {
                if (tmp[i].EndsWith(":"))
                {
                    lines.Add(string.Format("{0}\t{1}", tmp[i], ++i < tmp.Length ? (tmp[i].StartsWith(tmp[i - 1]) ? tmp[i].Substring(tmp[i - 1].Length) : tmp[i]) : "").Trim());
                }
            }
            return lines.ToArray();
        }

        static void WriteTable(string[] lines, string path)
        {
            if (lines != null && lines.Length != 0)
            {
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, lines);
            }
        }

        async static Task StartWithIdPageAsync(int idPageId)
        {
            Uri address = GetIdUri(idPageId);
            Task<string> idPageTask = HttpUtility.DownloadStringAsTask(address);
            int[] ids = await idPageTask.ContinueWith(x => ParseIds(x.Result));
            foreach (var id in ids)
            {
                await StartWithTablePageAsync(id, idPageId);
            }
        }

        async static Task StartWithTablePageAsync(int tablePageId, int idPageId = -1)
        {
            Uri address = GetTableUri(tablePageId);
            Task<string> tablePageTask = HttpUtility.DownloadStringAsTask(address);
            await tablePageTask.ContinueWith(x => WriteTable(ParseTable(x.Result), GetPath(tablePageId, idPageId)));
        }
    }
}
