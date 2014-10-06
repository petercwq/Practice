using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ParseHtmlTables;

namespace FastaToInfobox
{
    class Program
    {
        const string TestFasta = @".\TestData\All_DAMPD.fasta";
        const string WikiUrlFormat = @"http://en.wikipedia.org/wiki/{0}";
        const string OutDirectory = "infoboxes";
        const string OutTable = "Classification.txt";
        const string SpliterOfOut = "\t";
        const string OssFile = "OSs.txt";
        const string OssErrorFile = "OSsError.txt";
        const string DefaultValue = "Unknown";

        static readonly string[] Tokens = new string[] { "Kingdom", "Phylum", "Class", "Order", "Family", "Genus", "Subgenus", "Species" };

        static List<string> OSsErrors = new List<string>();
        static List<string> FinalTalbe = new List<string>();

        static Dictionary<string, string> GetUnkonwnDict()
        {
            var ret = new Dictionary<string, string>();
            foreach (var token in Tokens)
                ret.Add(token, DefaultValue);
            return ret;
        }

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
                FinalTalbe.Clear();
                FinalTalbe.Add("Name" + SpliterOfOut + string.Join(SpliterOfOut, Tokens));
                var set = ParseFastaAsync(path).Result;

                File.WriteAllLines(OssFile, set);
                foreach (var title in set)
                    tasks.Add(StartWithWikiTitleAsync(title));

                Task.Factory.ContinueWhenAll(tasks.ToArray(),
                ts =>
                {
                    Console.WriteLine("Completed: {0}", ts.Count(t => (t.Status == TaskStatus.RanToCompletion)));
                    File.WriteAllLines(OutTable, FinalTalbe);
                    File.WriteAllLines(OssErrorFile, OSsErrors);
                });
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
            Task<string> tablePageTask = HttpUtility.DownloadStringWithHttpClientAsync(address);
            await tablePageTask.ContinueWith(x =>
                {
                    if (x.IsCompleted && x.Status == TaskStatus.RanToCompletion)
                    {
                        Log(address.AbsoluteUri, "Completed");
                        FinalTalbe.Add(GetRecord(ParseInfbox(x.Result), title));
                    }
                    else
                    {
                        Log(address.AbsoluteUri, "Error");
                        System.Diagnostics.Debug.WriteLine(address.AbsoluteUri);
                        //if (x.IsFaulted)
                        //    System.Diagnostics.Debug.WriteLine(x.Exception.InnerException);
                        OSsErrors.Add(title);
                        FinalTalbe.Add(GetRecord(GetUnkonwnDict(), title));
                    }
                });
        }

        static void Log(string url, string result)
        {
            string msg = "Download from " + url + ": " + result;
            Console.WriteLine(msg);
        }

        static string GetPath(string title)
        {
            return Path.Combine(OutDirectory, title + ".txt");
        }

        static Dictionary<string, string> ParseInfbox(string text)
        {
            int index = text.IndexOf("Binomial");
            var ret = GetUnkonwnDict();
            text = HttpUtility.StripHTML(index == -1 ? text : text.Remove(index));
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool isstarted = false;
            foreach (var line in lines)
            {
                if (IsNeededLine(line))
                {
                    isstarted = true;
                    string[] keyvalue = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (keyvalue.Length > 1)
                        ret[keyvalue[0].Trim()] = keyvalue[1].Trim();
                }
                else
                {
                    if (isstarted)
                        break;
                }
            }
            return ret;
        }

        static bool IsNeededLine(string line)
        {
            foreach (var token in Tokens)
            {
                if (line.TrimStart().StartsWith(token + ":"))
                    return true;
            }
            return false;
        }

        static Uri GetWikiUri(string title)
        {
            return new Uri(string.Format(WikiUrlFormat, title));
        }

        static string GetRecord(Dictionary<string, string> dict, string title)
        {
            string ret = title + SpliterOfOut;
            if (dict != null && dict.Count > 0)
            {
                List<string> record = new List<string>();
                foreach (var token in Tokens)
                {
                    record.Add(dict[token].Replace(SpliterOfOut, " "));
                }
                ret += string.Join(SpliterOfOut, record);
            }

            System.Diagnostics.Debug.WriteLine(ret);
            return ret;
        }
    }
}

