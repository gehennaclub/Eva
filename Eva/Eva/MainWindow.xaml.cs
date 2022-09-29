﻿using AdonisUI.Controls;
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
        private List<string> links { get; set; }
        private List<string> analyzed { get; set; }
        private UInt32 errors { get; set; }
        private UInt32 total_steps { get; set; }
        private UInt32 step = 0;
        private UInt32 count = 0;

        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            factory = new Threads.Factory();
            links = new List<string>();
            analyzed = new List<string>();

            total_steps = 2;
        }

        private void Add404(string url)
        {
            errors++;
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
                Add404($"[ {ex.Status.ToString()} ] : {url}");
                return (null);
            }
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

            buffer += Concat(tags);

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
            errors = 0;
            step = 0;
            count = 0;
            ProgressSteps.Value = 0;
            ProgressAnalyzis.Value = 0;
            FileAnalysis.Text = $"File Analysis: 0/{links.Count()}";
            FileSteps.Text = $"Total Steps: 0/{total_steps}";
            links.Clear();
            analyzed.Clear();
        }

        private void Progress()
        {
            FileSteps.Text = $"Total Steps: {step}/{total_steps}";
            ProgressSteps.Value = ((step * 100) / total_steps);
            if (links.Count() > 0)
            {
                FileAnalysis.Text = $"File Analysis: {count}/{links.Count()}";
                ProgressAnalyzis.Value = ((count * 100) / links.Count());
            }
        }

        private async Task Core()
        {
            cleanner();
            Logs.Text = "Starting scan";
            step++;
            Progress();
            await Recursive(Host.Text);
            await Display(Concat(links));
            step++;
            FileSteps.Text = $"Total Steps: {step}/{total_steps}";
            ProgressSteps.Value = ((step * 100) / total_steps);
            foreach (string link in links)
            {
                Logs.Text = $"Analysing {link}";
                await Hierarchy(link);
                count++;
                Progress();
            }
            Refresh();
            Logs.Text = "Done";
            ProgressSteps.Value = 0;
            ProgressAnalyzis.Value = 0;
        }

        private void Refresh()
        {
            ExtractedLinks.Text = $"Extracted links: {links.Count()}";
            BrokenLinks.Text = $"Broken links: {errors}";
        }

        private async Task Recursive(string url)
        {
            List<string> local = null;
            string result = null;

            if (analyzed.Contains(url) == false && analyzed.Contains($"{url}/") == false)
            {
                analyzed.Add(url);
                FileAnalysis.Text = $"File Analysis: 0/{links.Count()}";
                result = await Request(url);
                if (result != null)
                {
                    local = Extractor(result);
                    if (local != null)
                    {
                        AddAll(local);
                        Refresh();
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
                        if (line.Contains("<h") == true)
                        {
                            for (UInt32 i = 1; i < taglimit; i++)
                            {
                                if (line.Contains($"<h{i}") == true)
                                {
                                    full.Add($"<h{i}>\n\t{line}\n");
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
            Uri uri = new Uri(Host.Text);
            string path = $"Links/{uri.Host}/save_{DateTime.Now.ToString("ddMMyyyy_hhmmss")}.txt";

            if (Directory.Exists("Links") == false)
            {
                Directory.CreateDirectory("Links");
            }
            if (Directory.Exists($"Links/{uri.Host}") == false)
            {
                Directory.CreateDirectory($"Links/{uri.Host}");
            }
            File.WriteAllLines(path, links);
            Logs.Text = $"Links saved in: {path}";
        }

        private async void ClickRun(object sender, RoutedEventArgs e)
        {
            RunButton.IsEnabled = false;
            Host.IsReadOnly = true;
            if ((Uri.IsWellFormedUriString(Host.Text, UriKind.Absolute) == true))
            {
                await Core();
            } else
            {
                MessageBox.Show($"Uri '{Host.Text}' is not well formated as expected, the format must be 'http(s)://<host>.<domain>'");
            }
            Host.IsReadOnly = false;
            RunButton.IsEnabled = true;
        }

        private async void ClickSave(object sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = false;
            RunButton.IsEnabled = false;
            Host.IsReadOnly = true;
            await Save();
            SaveButton.IsEnabled = true;
            Host.IsReadOnly = false;
            RunButton.IsEnabled = true;
        }
    }
}
