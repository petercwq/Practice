using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ParseHtmlTables;

namespace FastaToInfobox
{
    class Program
    {
        const string TestFasta = @".\TestData\All_DAMPD.fasta";
        const string WikiUrlFormat = @"http://en.wikipedia.org/wiki/{0}";
        const string OutDirectory = "infboxes";
        const string OssFile = "OSs.txt";
        const string OssErrorFile = "OSsError.txt";

        static List<string> OSsErrors = new List<string>();

        static void Main(string[] args)
        {
            List<Task> tasks = new List<Task>();
            bool wrong = true;
            string path = TestFasta;
            if (args.Length == 0 || ((args.Length == 1) && File.Exists(args[0])))
            {
                wrong = false;
                if (args.Length > 0)
                    path = args[0];
            }
            if (wrong)
            {
                Console.WriteLine("input the .fasta file path.");
            }
            else
            {
                OSsErrors.Clear();
                ParseFastaAsync(path).ContinueWith(settask =>
                {
                    File.WriteAllLines(OssFile, settask.Result);
                    foreach (var title in settask.Result)
                        tasks.Add(StartWithWikiTitleAsync(title));
                }).Wait();

                Task.Factory.ContinueWhenAll(tasks.ToArray(),
                ts =>
                {
                    Console.WriteLine("Completed: {0}", ts.Count(t => (t.Status == TaskStatus.RanToCompletion)));
                }).Wait();

                File.WriteAllLines(OssErrorFile, OSsErrors);
            }

            Console.ReadKey();
        }

        async static Task<List<string>> ParseFastaAsync(string path)
        {
            HashSet<string> osset = new HashSet<string>();
            var st = new StreamReader(path);
            string line;
            int index1 = -1, index2 = -1;
            while ((line = await st.ReadLineAsync()) != null)
            {
                index1 = -1;
                if (!line.StartsWith(">") || (index1 = line.IndexOf("OS=")) == -1)
                {
                    continue;
                }
                index2 = line.IndexOf("=", index1 + 3);
                if (index2 == -1)
                    index2 = line.Length - 1;
                else
                {
                    int index3 = line.LastIndexOf(" ", index2);
                    if (index3 == -1)
                        index2 -= 3;
                    else
                        index2 = index3;
                }
                osset.Add(line.Substring(index1 + 3, index2 - index1 - 3).Trim());
            }
            var ret = osset.ToList();
            ret.Sort();
            return ret;
        }

        async static Task StartWithWikiTitleAsync(string title)
        {
            Uri address = GetWikiUri(title);
            Task<string> tablePageTask = HttpUtility.DownloadStringAsTask(address);
            await tablePageTask.ContinueWith(x =>
                {
                    if (x.IsCompleted && x.Status == TaskStatus.RanToCompletion)
                    {
                        WriteTable(ParseInfbox(x.Result), GetPath(title));
                    }
                    else
                    {
                        OSsErrors.Add(title);
                    }
                });
        }

        static string GetPath(string title)
        {
            return Path.Combine(OutDirectory, title + ".txt");
        }

        static string[] ParseInfbox(string text)
        {
            //throw new NotImplementedException();
            return null;
        }

        static Uri GetWikiUri(string title)
        {
            return new Uri(string.Format(WikiUrlFormat, title));
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
    }
}

