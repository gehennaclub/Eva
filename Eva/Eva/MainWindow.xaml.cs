using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Http;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Path = System.IO.Path;
using MessageBox = System.Windows.MessageBox;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Policy;

namespace Eva
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisWindow
    {
        private Threads.Factory factory { get; set; }
        private Dictionary<string, List<string>> HierarchyResult { get; set; }
        private List<string> links { get; set; }
        private List<string> analyzed { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            factory = new Threads.Factory();
            HierarchyResult = new Dictionary<string, List<string>>();
            links = new List<string>();
            analyzed = new List<string>();
        }

        private void Add404(string url)
        {
            NotFound.Document.Blocks.Add(new Paragraph(new Run(url)));
        }

        private async Task<string> Request(string url)
        {
            try
            {
                if (Uri.IsWellFormedUriString(url, UriKind.Absolute) == true)
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync();
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        Add404(url);
                    }

                    return (new StreamReader(response.GetResponseStream()).ReadToEnd());
                }
                return (null);
            } catch (WebException ex)
            {
                Logs.Text = "Server returned an exception";
                Add404($"[ {ex.Message} ] :\n{url}");
                return (null);
            }
        }

        private bool Validate(string url)
        {
            if (url != null && url != string.Empty)
            {
                if (url.StartsWith("http") == true)
                {
                    return (true);
                }
            }

            return (false);
        }

        private List<string> Extractor(string content)
        {
            List<string> result = new List<string>();
            Regex regex = new Regex("(http|https):\\/\\/([\\w_-]+(?:(?:\\.[\\w_-]+)+))([\\w.,@?^=%&:\\/~+#-]*[\\w@?^=%&\\/~+#-])", RegexOptions.Singleline);
            MatchCollection matches = regex.Matches(content);

            foreach (Match match in matches)
            {
                if ((result.Contains(match.Value) == false || result.Count <= 0) && match.Value.StartsWith(Host.Text) == true)
                {
                    result.Add(match.Value);
                }
            }

            return (result);
        }

        private string Concat(List<string> links)
        {
            string buffer = "";

            foreach (string link in links)
            {
                buffer += $"{link}\n";
            }

            return (buffer);
        }

        private string ConcatTag(string url, List<string> tags)
        {
            string buffer = $"- {url}\n";

            foreach (string tag in tags)
            {
                buffer += $"{tag}\n";
            }

            return (buffer);
        }

        private void cleanner()
        {
            List<BlockCollection> collection = new List<BlockCollection>()
            {
                Details.Document.Blocks,
                Links.Document.Blocks,
                NotFound.Document.Blocks,
                Tags.Document.Blocks
            };
            
            foreach (BlockCollection block in collection)
            {
                if (block.Count() > 0)
                    block.Clear();
            }
        }

        private async Task Core()
        {
            HierarchyResult.Clear();
            cleanner();
            Logs.Text = "Starting scan";
            await Recursive(Host.Text);
            await Display(Concat(links));
            foreach (string link in links)
            {
                Logs.Text = $"Analysing {link}";
                await Hierarchy(link);
            }
            ExtractedLinks.Text = $"Extracted links: {links.Count()}";
            Logs.Text = "Done";
        }

        private async Task Recursive(string url)
        {
            List<string> local = null;
            string result = null;

            if (analyzed.Contains(url) == false && analyzed.Contains($"{url}/") == false)
            {
                analyzed.Add(url);
                result = await Request(url);
                if (result != null)
                {
                    local = Extractor(result);
                    if (local != null)
                    {
                        AddAll(local);
                        foreach (string link in local)
                        {
                            Logs.Text = $"Searching deep links {link}";
                            await Recursive(link);
                        }
                    }
                }
            }
        }

        private void AddAll(List<string> newlinks)
        {
            foreach (string link in newlinks)
            {
                if (links.Contains(link) == false)
                {
                    links.Add(link);
                }
            }
        }

        private async Task Hierarchy(string url)
        {
            string content = await Request(url);
            string[] lines = null;
            List<string> tags = null;
            List<string> full = null;
            string copy = null;
            UInt32 taglimit = 5;

            if (content != null)
            {
                lines = content.Split('\n');
                tags = new List<string>();
                full = new List<string>();
                if (lines[0].StartsWith("<!DOCTYPE html") == true)
                {
                    foreach (string line in lines)
                    {
                        copy = line.TrimStart(' ');
                        copy = line.TrimStart('\t');
                        if (line.Contains("<h") == true)
                        {
                            for (UInt32 i = 1; i < taglimit; i++)
                            {
                                if (copy.StartsWith($"<h{i}") == true)
                                {
                                    full.Add($"<h{i}>\n\t{copy}\n");
                                    tags.Add($"<h{i}>");
                                }
                            }
                        }
                    }
                    await DisplayTags(ConcatTag(url, tags));
                    await DisplayDetails(ConcatTag(url, full));
                } else
                {
                    await DisplayTags($"- {url}: Not an HTML file");
                }
            }
        }

        private async Task Display(string content)
        {
            Links.Document.Blocks.Add(new Paragraph(new Run(content)));
        }

        private async Task DisplayTags(string content)
        {
            Tags.Document.Blocks.Add(new Paragraph(new Run(content)));
        }

        private async Task DisplayDetails(string content)
        {
            Details.Document.Blocks.Add(new Paragraph(new Run(content)));
        }

        private async Task Save()
        {
            Logs.Text = "Saving links";
            if (Directory.Exists("Links") == false)
            {
                Directory.CreateDirectory("Links");
            }
            File.WriteAllLines($"Links/{DateTime.Now.ToString("ddMMyyyy-hhmmss")}.txt", links);
            Logs.Text = "Links saved";
        }

        private async void ClickRun(object sender, RoutedEventArgs e)
        {
            RunButton.IsEnabled = false;
            await Core();
            RunButton.IsEnabled = true;
        }

        private async void ClickSave(object sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = false;
            await Save();
            SaveButton.IsEnabled = true;
        }
    }
}
