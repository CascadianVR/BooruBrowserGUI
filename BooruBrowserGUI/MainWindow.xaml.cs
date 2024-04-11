using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using HtmlAgilityPack;

namespace BooruBrowserGUI
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        public static string gelbooru = "https://gelbooru.com/index.php?page=post&s=list&tags=";
        public string rule34 = "https://rule34.xxx/index.php?page=post&s=list&tags=";
        

        class BooruMedia
        {
            public BorruMediaType type;
            public string url = "";
            public BitmapImage? image = null;
            public List<string> tags = new List<string>();
            public string rating = "";
            public string score = "";
        }
        
        enum BorruMediaType
        {
            image,
            video,
            gif
        }
        
        enum BorruSiteType
        {
            gelbooru,
            rule34
        }

        BorruSiteType siteType = BorruSiteType.rule34;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int AllocConsole();

        public MainWindow()
        {
            InitializeComponent();

            if (Debugger.IsAttached)
                AllocConsole();

            Configure();
            
        }

        void Configure()
        {
            Style = (Style)FindResource(typeof(Window));

            tags.Focusable = true;
            tags.Focus();

            _ = AutoProgress();
        }

        async Task AutoProgress()
        {
            while (true)
            {
                if (autoProgress.IsChecked == true && _booruMedia.Count > 1)
                {
                    await Task.Delay(Convert.ToInt32(delay.Text)*1000);
                    _ = QueueItems();
                }
                await Task.Delay(100);
            }
        }

        async Task ImageSavedIndicator()
        {

            SaveIndicator.Text = "Saving...";
            await Task.Delay(200);

            SaveIndicator.Text = "Saved";
            await Task.Delay(1000);
            
            SaveIndicator.Text = "";
        }

        int mediaIndex = 0;
        int pageIndex = 0;

        List<BooruMedia> _booruMedia = new List<BooruMedia>();
        bool wantsToRun = false;
        bool isRunning = false;
        async Task PrepareMediaFromTags()
        {
            siteType = (BorruSiteType)booruSite.SelectedIndex;

            isRunning = true;

            Console.WriteLine("Getting Media...");

            HttpClient client = new();
            var splitTags = tags.Text.Replace(' ', '+');

            HtmlNodeCollection list = null;
            HtmlNodeCollection listInfo = null;
            string downloadString;
            if (siteType == BorruSiteType.gelbooru)
            {
                downloadString = await client.GetStringAsync(new Uri(gelbooru + splitTags + "+" + "&pid=" + pageIndex));

                HtmlDocument htmlSnippet = new HtmlDocument();
                htmlSnippet.LoadHtml(downloadString);

                Console.WriteLine("Getting Image Links...");
                Console.WriteLine(new Uri(gelbooru + splitTags + "+" + "&pid=" + pageIndex));

                list = htmlSnippet.DocumentNode.SelectNodes(".//article/a[@href]");
                listInfo = htmlSnippet.DocumentNode.SelectNodes(".//article/a/img[@title]");

            }
            else if (siteType == BorruSiteType.rule34)
            {
                downloadString = await client.GetStringAsync(new Uri(rule34 + splitTags + "+" + "&pid=" + pageIndex));

                HtmlDocument htmlSnippet = new HtmlDocument();
                htmlSnippet.LoadHtml(downloadString);

                Console.WriteLine("Getting Image Links...");
                Console.WriteLine(new Uri(rule34 + splitTags + "+" + "&pid=" + pageIndex));

                list = htmlSnippet.DocumentNode.SelectNodes(".//span[@class='thumb']/a[@href]");
                listInfo = htmlSnippet.DocumentNode.SelectNodes(".//span/a/img[@title]");
                Console.WriteLine(list.Count);
                Console.WriteLine(listInfo.Count);
            }

            if (list == null || list.Count < 1)
            {
                Console.WriteLine("One or more invalid tags.");
                {
                    isRunning = false;
                    wantsToRun = false;
                    return;
                }
            }

            pageIndex += list.Count;

            for (int i = 0; i < 42; i++)
            {
                if (wantsToRun)
                {
                    _booruMedia.Clear();
                    wantsToRun = false;
                    return;
                }

                if (list[i] == null) continue;

                HtmlNode? link = list[i];
                Uri pageLink = null;

                if (siteType == BorruSiteType.gelbooru) pageLink = new Uri(WebUtility.HtmlDecode(link.GetAttributeValue("href", "")));
                else if (siteType == BorruSiteType.rule34) pageLink = new Uri("https://rule34.xxx" + WebUtility.HtmlDecode(link.GetAttributeValue("href", "")));

                Console.WriteLine(pageLink);

                downloadString = null;
                try
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:124.0) Gecko/20100101 Firefox/124.0");
                    downloadString = await client.GetStringAsync(pageLink);
                }
                catch (Exception ex) 
                {
                    Console.WriteLine("ERROR!: " + ex.Message);
                }

                HtmlDocument html = new HtmlDocument();
                html.LoadHtml(downloadString);

                HtmlNodeCollection source = null;
                if (siteType == BorruSiteType.gelbooru) source = html.DocumentNode.SelectNodes(".//picture/img[@src]");
                else if (siteType == BorruSiteType.rule34) source = html.DocumentNode.SelectNodes(".//div[@class='flexi']/div/img[@src]");

                Console.WriteLine(source == null);

                if (source != null)
                {
                    var url = source[0].GetAttributeValue("src", "");

                    Console.WriteLine(url + " | " + pageIndex);

                    if (!url.EndsWith("gif"))
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(url);
                        image.EndInit();

                        var contentinfo = WebUtility.HtmlDecode(listInfo[i].GetAttributeValue("title", ""));
                        var tags = contentinfo.Split("score")[0].Split(' ').ToList();
                        var score = contentinfo.Split("score")[1].Split(" ")[0];
                        var rating = contentinfo.Split("score")[1].Split(" ")[1];

                        var borruImage = new BooruMedia()
                        {
                            type = BorruMediaType.image,
                            url = url,
                            image = image,
                            tags = tags,
                            score = score,
                            rating = rating
                        };

                        _booruMedia.Add(borruImage);

                        continue;
                    }
                    else if (url.EndsWith("gif"))
                    {
                        var contentinfo = WebUtility.HtmlDecode(listInfo[i].GetAttributeValue("title", ""));
                        var tags = contentinfo.Split("score")[0].Split(' ').ToList();
                        var score = contentinfo.Split("score")[1].Split(" ")[0];
                        var rating = contentinfo.Split("score")[1].Split(" ")[1];

                        var borruImage = new BooruMedia()
                        {
                            type = BorruMediaType.gif,
                            url = url,
                            image = null,
                            tags = tags,
                            score = score,
                            rating = rating
                        };

                        _booruMedia.Add(borruImage);

                        continue;
                    }
                }

                source = html.DocumentNode.SelectNodes(".//video[@id='gelcomVideoPlayer']/source[@src]");

                if (source != null)
                {
                    var url = source[0].GetAttributeValue("src", "");

                    Console.WriteLine(url + " | " + pageIndex);

                    var contentinfo = WebUtility.HtmlDecode(listInfo[i].GetAttributeValue("title", ""));
                    var tags = contentinfo.Split("score")[0].Split(' ').ToList();
                    var score = contentinfo.Split("score")[1].Split(' ')[0];
                    var rating = contentinfo.Split("score")[1].Split(' ')[1];

                    var borruVideo = new BooruMedia()
                    {
                        type = BorruMediaType.video,
                        url = url,
                        image = null,
                        tags = tags,
                        score = score,
                        rating = rating
                    };

                    _booruMedia.Add(borruVideo);

                    continue;
                }
            }

            wantsToRun = false;
            isRunning = false;
        }

        async void DisplayNextMediaElement()
        {
            Console.WriteLine("Displaying next media element...");
            Console.WriteLine(_booruMedia.Count);
            Console.WriteLine(mediaIndex);
            if (_booruMedia.Count < mediaIndex) return;
            Console.WriteLine(_booruMedia[mediaIndex].url);

            if (_booruMedia[mediaIndex].type == BorruMediaType.image)
            {
                imageDisplay.Source = _booruMedia[mediaIndex].image;
                videoDisplay.Source = null;
            }
            else if (_booruMedia[mediaIndex].type == BorruMediaType.video || _booruMedia[mediaIndex].type == BorruMediaType.gif)
            {
                videoDisplay.Source = new Uri(_booruMedia[mediaIndex].url);

                videoDisplay.Play();

                bool videoLoaded = false;
                RoutedEventHandler eventMediaOpened = (sender, e) =>
                {
                    // Set the flag to indicate that the MediaElement is loading a video
                    videoLoaded = true;
                };

                RoutedEventHandler eventMediaEnded = (sender, e) =>
                {
                    Console.WriteLine("Media Ended");
                    videoDisplay.Position = new TimeSpan(0, 0, 1);
                    videoDisplay.Play();
                };

                videoDisplay.MediaOpened -= eventMediaOpened;
                videoDisplay.MediaEnded -= eventMediaEnded;


                videoDisplay.MediaOpened += eventMediaOpened;
                videoDisplay.MediaEnded += eventMediaEnded;


                while (!videoLoaded)
                {
                    await Task.Delay(10);
                    if (LoadingIndicator.Visibility != Visibility.Visible) LoadingIndicator.Visibility = Visibility.Visible;
                }

                LoadingIndicator.Visibility = Visibility.Hidden;

                imageDisplay.Source = null;
            }

            string mediaInfo = "";
            var score = _booruMedia[mediaIndex].score;
            var rating = _booruMedia[mediaIndex].rating;
            if (score != null && score.Length > 1) mediaInfo += "Score: " + score.Remove(0, 1) + '\n';
            if (rating != null && rating.Length > 6) mediaInfo += "Rating: " + rating.Remove(0, 7) + '\n';
            foreach (var tag in _booruMedia[mediaIndex].tags)
            {
                mediaInfo += tag + "\n";
            }
            this.mediaInfo.Text = mediaInfo;

            mediaIndex++;
            Console.WriteLine("Media Index: " + mediaIndex);
        }

        async Task QueueItems()
        {
            Console.WriteLine(_booruMedia.Count);
            Console.WriteLine(mediaIndex);

            if (_booruMedia.Count - mediaIndex < 5)
            {
                if (isRunning) wantsToRun = true;
                _ = PrepareMediaFromTags();
            }

            if (mediaIndex >= 42)
            {
                mediaIndex -= 24;
                _booruMedia.RemoveRange(0, 24);
            }

            while (_booruMedia.Count <= 3)
            {
                await Task.Delay(10);
            }

            DisplayNextMediaElement();
        }

        private async void SaveCurrentMedia()
        {
            if (_booruMedia[mediaIndex - 1].type == BorruMediaType.image)
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_booruMedia[mediaIndex - 1].image));

                Console.WriteLine(Directory.GetCurrentDirectory() + "\\images\\");

                if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\images\\"))
                {
                    Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\images\\");
                }

                string fileType = _booruMedia[mediaIndex - 1].url.Split('.')[^1];

                using (var fileStream = new FileStream(
                    Directory.GetCurrentDirectory() + "\\images\\" + _booruMedia[mediaIndex - 1].url + mediaIndex + "." + fileType, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }
            
            if (_booruMedia[mediaIndex - 1].type == BorruMediaType.video)
            {
                HttpClient httpClient = new HttpClient();

                if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\images\\"))
                {
                    Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\images\\");
                }

                string videoUrl = _booruMedia[mediaIndex - 1].url;
                string fileType = _booruMedia[mediaIndex - 1].url.Split('.')[^1];
                string fileName = _booruMedia[mediaIndex - 1].url.Split('/')[^1].Split('.')[0];
                string fileSavePath = Directory.GetCurrentDirectory() + "\\images\\" + fileName + "." + fileType;

                Console.WriteLine(fileType);
                Console.WriteLine(fileName);
                Console.WriteLine(fileSavePath);

                using (var response = await httpClient.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    // Create or overwrite the video file
                    using (var fileStream = File.Create(fileSavePath))
                    {
                        // Copy the video stream to the file stream
                        await response.Content.CopyToAsync(fileStream);
                    }

                    Console.WriteLine($"Video downloaded successfully to: {fileSavePath}");
                }
            }
        }

        private async void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                mediaIndex = 0;
                pageIndex = 0;
                _booruMedia = new List<BooruMedia>();
                imageDisplay.Focusable = true;
                imageDisplay.Focus();

                await QueueItems();
            }

            if (tags.IsFocused) return;

            if (e.Key == Key.Right || e.Key == Key.D)
            {
                await QueueItems();
            }
            else if(e.Key == Key.Left || e.Key == Key.A)
            {
                if (mediaIndex <= 2) return;

                mediaIndex = mediaIndex - 2;
                await QueueItems();
            }
            else if(e.Key == Key.S || e.Key == Key.Space)
            {
                SaveCurrentMedia();
                await ImageSavedIndicator();
                Console.WriteLine("Image saved!");
            }
        }
        private async void Forward(object sender, EventArgs e)
        {
            await QueueItems();
        }

        private async void Backward(object sender, EventArgs e)
        {
            if (mediaIndex <= 1) return;
            mediaIndex = mediaIndex - 2;
            await QueueItems();
        }
    }
}
