﻿using Hfr.Model;
using Hfr.Utilities;
using Hfr.ViewModel;
using Hfr.Views.MainPages;
using HtmlAgilityPack;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media;
using Hfr.Models.Threads;

namespace Hfr.Helpers
{
    public static class ThreadFetcher
    {
        public static async Task GetPosts(Thread currentThread)
        {
            App.TelemetryClient.TrackEvent("Fetch topic posts");
            Debug.WriteLine("Fetching Posts");
            await Fetch(currentThread);
            Debug.WriteLine("Updating UI with new Posts list");
        }

        public static async Task Fetch(Thread currentThread)
        {
            await ThreadUI.Invoke(() =>
            {
                Loc.Thread.IsThreadLoading = true;
            });

            var html = await HttpClientHelper.Get(currentThread.ThreadUri);
            if (string.IsNullOrEmpty(html)) return;
            
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            //Refresh Current/Number of pages
            var pagesList = htmlDoc.DocumentNode.Descendants("tr")
                       .Where(x => x.GetAttributeValue("class", "") == "cBackHeader fondForum2PagesHaut")
                       .SelectMany(tr => tr.Descendants("div"))
                       .Where(x => x.GetAttributeValue("class", "") == "left")
                       .FirstOrDefault();

            int nbPage = 1;
            int currentPage = 1;
            if (pagesList != null)
            {

                var nbPageStr = pagesList.Descendants("a")
                    .LastOrDefault().InnerText;
                int.TryParse(nbPageStr, out nbPage);

                var currentPageStr = pagesList.Descendants("b")
                    .LastOrDefault().InnerText;
                int.TryParse(currentPageStr, out currentPage);

                if (currentPage > nbPage) nbPage = currentPage;
            }

            await ThreadUI.Invoke(() =>
            {
                Loc.Thread.CurrentThread.ThreadNbPage = nbPage;
                Loc.Thread.CurrentThread.ThreadCurrentPage = currentPage;
            });

            var postNodes =
                htmlDoc.DocumentNode.Descendants("table")
                    .Where(x => x.GetAttributeValue("class", "") == "messagetable")
                    .ToArray();

            if (postNodes == null || !postNodes.Any()) return;

            string TempHTMLMessagesList = "";
            string TempHTMLMessage = "";
            string TempHTMLTopic = "";

            string BodyTemplate = "";
            string MessageTemplate = "";

            // This is absolutely quick and dirty code :o
            Assembly asm = typeof(App).GetTypeInfo().Assembly;

            using (Stream stream = asm.GetManifestResourceStream(HFRRessources.Tpl_Topic))
            using (StreamReader reader = new StreamReader(stream))
            {
                BodyTemplate = reader.ReadToEnd();
            }

            using (Stream stream = asm.GetManifestResourceStream(HFRRessources.Tpl_Message))
            using (StreamReader reader = new StreamReader(stream))
            {
                MessageTemplate = reader.ReadToEnd();
            }

            var i = 0;
            foreach (var postNode in postNodes)
            {
                TempHTMLMessage = MessageTemplate;

                // Id de la réponse
                var reponseId =
                    postNode.Descendants("a")
                        .FirstOrDefault(x => x.GetAttributeValue("href", "").StartsWith("#t"))
                        .GetAttributeValue("href", "")
                        .Replace("#t", "");
                
                // Pseudo
                var pseudo = postNode.Descendants("b").FirstOrDefault(x => x.GetAttributeValue("class", "") == "s2").InnerText.CleanFromWeb();
                if (Loc.Settings.IgnoreListMembersList != null && Loc.Settings.IgnoreListMembersList.Contains(pseudo))
                {
                    Debug.WriteLine("Post form member in Ignore list, switching to next post");
                    continue;
                }
                // Mood
                var mood = postNode.Descendants("span").FirstOrDefault(x=>x.GetAttributeValue("class","") == "MoodStatus")?.InnerText.CleanFromWeb();

                // Img
                var avatarUri = "ms-appx-web:///Assets/HTML/UI/rsz_no_avatar.png";
                var avatarClass = "no_avatar";
                var divAvatarNode = postNode.Descendants("div").FirstOrDefault(x=>x.GetAttributeValue("class", "") == "avatar_center");
                if (divAvatarNode != null && divAvatarNode.ChildNodes.Any())
                {
                    var imgAvatarNode = divAvatarNode.FirstChild;
                    var uri = imgAvatarNode.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(uri))
                    {
                        avatarUri = uri;
                        avatarClass = "";
                    }
                }

                var toolbar = postNode.Descendants("div").FirstOrDefault(x => x.GetAttributeValue("class", "") == "toolbar");
                // Date
                var date = toolbar.InnerText.CleanFromWeb();
                date = date.Replace("Posté le ", "");

                // Can edit
                bool canEdit = toolbar.Descendants("img").Any(x => x.GetAttributeValue("alt", "") == "edit" && x.GetAttributeValue("title", "").Contains("Edit"));

                // Content
                var contentHtml = postNode.Descendants("div").FirstOrDefault(x => x.GetAttributeValue("id", "").Contains("para"));

                if (Loc.Settings.TubecastEnabled)
                {
                    var youtubeRegex = new Regex("youtu(?:\\.be|be\\.com)/(?:.*v(?:/|=)|(?:.*/)?)([a-zA-Z0-9-_]+)");
                    var youtubeEntries = contentHtml.Descendants("a").Where(x => x.GetAttributeValue("href", "").Contains("www.youtube.com") || x.GetAttributeValue("href", "").Contains("//youtu.be"));
                    foreach (var youtubeEntry in youtubeEntries)
                    {
                        var videoUrl = youtubeEntry.GetAttributeValue("href", "");
                        Match youtubeMatch = youtubeRegex.Match(videoUrl);

                        if (youtubeMatch.Success)
                        {
                            videoUrl = "vnd.youtube:" + youtubeMatch.Groups[1].Value;
                            youtubeEntry.SetAttributeValue("href", videoUrl);
                        }
                    }
                }

                var content = contentHtml.InnerHtml;
                int lastPostText = content.IndexOf("<div style=\"clear: both;\"> </div>", StringComparison.Ordinal);
                if (lastPostText == -1)
                {
                    lastPostText = content.Length;
                }

                content = content.Substring(0, lastPostText);
                content = content.CleanFromWeb();

                TempHTMLMessage = TempHTMLMessage.Replace("%%ID%%", i.ToString());
                TempHTMLMessage = TempHTMLMessage.Replace("%%POSTID%%", reponseId);

                if (pseudo == "Modération")
                {
                    TempHTMLMessage = TempHTMLMessage.Replace("%%moderation%%", "moderation");
                }
                else
                {
                    TempHTMLMessage = TempHTMLMessage.Replace("%%moderation%%", "");
                }

                TempHTMLMessage = TempHTMLMessage.Replace("%%no_avatar_class%%", avatarClass);

                if (Loc.Settings.SquareAvatarStylePreferred)
                {
                    TempHTMLMessage = TempHTMLMessage.Replace("%%round_avatar_class%%", "");
                }
                else
                {
                    TempHTMLMessage = TempHTMLMessage.Replace("%%round_avatar_class%%", "round");
                }
                
                if  (canEdit)
                {
                    // Post is editable
                    TempHTMLMessage = TempHTMLMessage.Replace("%%PERSONALPOST%%", "");
                }
                else
                {
                    // Post is not personal, hide personal actions
                    TempHTMLMessage = TempHTMLMessage.Replace("%%PERSONALPOST%%", "personal_post_button_hidden");
                }

                TempHTMLMessage = TempHTMLMessage.Replace("%%AUTEUR_AVATAR%%", avatarUri);
                TempHTMLMessage = TempHTMLMessage.Replace("%%AUTEUR_PSEUDO%%", pseudo);
                TempHTMLMessage = TempHTMLMessage.Replace("%%MESSAGE_DATE%%", date);

                TempHTMLMessage = TempHTMLMessage.Replace("%%MESSAGE_CONTENT%%", content);

                TempHTMLMessagesList += TempHTMLMessage;
                i++;
            }

            // Get URL of the new post form
            var url = htmlDoc.DocumentNode.Descendants("form").FirstOrDefault(x => x.GetAttributeValue("id", "") == "repondre_form").GetAttributeValue("action", "");
            currentThread.ThreadNewPostUriForm = WebUtility.HtmlDecode(url);

            TempHTMLTopic = BodyTemplate.Replace("%%MESSAGES%%", TempHTMLMessagesList);

            // Get Accent Color
            await ThreadUI.Invoke(() =>
            {
                SolidColorBrush color = (SolidColorBrush)App.Current.Resources["SystemControlHighlightAltListAccentLowBrush"];
                var colorString = $"{color.Color.R.ToString()}, {color.Color.G.ToString()}, {color.Color.B.ToString()}";

                TempHTMLTopic = TempHTMLTopic.Replace("%%ACCENTCOLOR%%", colorString);
            });

            // Get desired theme
            if (Loc.Settings.IsApplicationThemeDark)
            {
                TempHTMLTopic = TempHTMLTopic.Replace("%%COLOR-FG%%", "white").Replace("%%COLOR-BG%%", "black").Replace("%%COLOR-QUOTE%%", "#252525");
            }
            else
            {
                TempHTMLTopic = TempHTMLTopic.Replace("%%COLOR-FG%%", "black").Replace("%%COLOR-BG%%", "white").Replace("%%COLOR-QUOTE%%", "#E3E3E3");
            }

            // Get user selected font-size
            var fontSize = Loc.Settings.FontSizePreferred;
            TempHTMLTopic = TempHTMLTopic.Replace("%%FONTSIZE%%", fontSize.ToString());

            // Get user selected header transparency setting
            var postHeaderTransparency = Loc.Settings.PostHeaderTransparencyPreferred;
            TempHTMLTopic = TempHTMLTopic.Replace("%%headerPostTransparency%%", (1 - postHeaderTransparency).ToString().Replace(",", "."));            

            // Create/Open WebSite-Cache folder
            var subfolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(Strings.WebSiteCacheFolderName, CreationCollisionOption.OpenIfExists);
            var file = await subfolder.CreateFileAsync($"{Strings.WebSiteCacheFileName}", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, TempHTMLTopic);

            await ThreadUI.Invoke(() =>
            {
                Loc.Thread.UpdateThreadWebView(currentThread);

                Loc.Thread.IsThreadLoading = false;
            });
        }
    }
}
