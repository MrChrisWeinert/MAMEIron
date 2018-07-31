using System.Security.Cryptography;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Configuration;
using System.Diagnostics;
using System;

namespace MAMEIronWPF
{
    public class GameListCreator
    {
        private string _rootDirectory;
        private string _mameExe;
        private string _listFull;
        private string _snapsDir;
        private string _gamesJson;
        private string _catver;
        private List<Game> _games;
        private Dictionary<string, string> _categories;
        private Dictionary<string, float> _versions;

        public void GenerateGameList(string mameExe, string gamesJson, string snapDir)
        {
            _rootDirectory = ConfigurationManager.AppSettings["rootDirectory"];
            _mameExe = mameExe;
            _gamesJson = gamesJson;
            _snapsDir = snapDir;
            if (!File.Exists(_gamesJson))
            {
                GenerateGamesJSON();
            }

            return;
        }
        private void GenerateGamesJSON()
        {
            _listFull = Path.Combine(_rootDirectory, "list.xml");
            if (!File.Exists(_listFull))
            {
                GenerateGamesXML();
            }

            //I included an old version of Catver from: http://www.progettosnaps.net/catver/ but there are newer versions available.
            
            //Case-sensitive
            _catver = Path.Combine(_rootDirectory, "Catver.ini");
            _gamesJson = Path.Combine(_rootDirectory, "games.json");
            _categories = new Dictionary<string, string>();
            _versions = new Dictionary<string, float>();
            LoadCategoriesAndVersions();
            ParseXMLAndFilter();

        }

        private void GenerateGamesXML()
        {
            string st = _mameExe;
            Process process = new Process();
            process.StartInfo.FileName = st;
            process.StartInfo.WorkingDirectory = _rootDirectory;
            process.StartInfo.Arguments = " -listxml";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            //Perf note: It will take MAME a few minutes to generate the list.xml file. It's roughly 170MB in size (version .161).
            process.Start();

            using (StreamReader sr = process.StandardOutput)
            {
                using (StreamWriter sw = new StreamWriter(_listFull))
                {
                    sw.Write(sr.ReadToEnd());
                    sw.Close();
                    sw.Dispose();
                }
                sr.Close();
                sr.Dispose();
                sr.DiscardBufferedData();
            }
        }

        private void LoadCategoriesAndVersions()
        {
            bool isCategory = true;
            //enum Linetype {Category, Version};
            using (StreamReader sr = new StreamReader(_catver))
            {
                string line = sr.ReadLine();
                while (line != null)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        if (line == "[Category]")
                        {
                            isCategory = true;
                            line = sr.ReadLine();
                        }
                        else if (line == "[VerAdded]")
                        {
                            isCategory = false;
                            line = sr.ReadLine();
                        }
                        if (isCategory)
                        {
                            string name = line.Substring(0, line.IndexOf("=") - 1);
                            int start = line.IndexOf("=") + 2;
                            int end = line.Length - start;
                            string category = line.Substring(start, end);
                            _categories.Add(name, category);
                        }
                        else
                        {
                            string name = line.Substring(0, line.IndexOf("=") - 1);
                            int start = line.IndexOf("=") + 2;
                            int end = line.Length - start;
                            float version = float.Parse(line.Substring(start, 4));
                            _versions.Add(name, version);
                        }
                    }
                    line = sr.ReadLine();
                }
            }
        }

        private void ParseXMLAndFilter()
        {
            _games = new List<Game>();
            XmlDocument doc = new XmlDocument();
            //Perf note: The list.xml file is roughly 170MB (version .161). Loading this into memory uses ~2GB of RAM.
            doc.Load(_listFull);
            XmlNode root = doc.SelectSingleNode("mame");
            HashSet<string> drivers = new HashSet<string>();
            HashSet<string> statuses = new HashSet<string>();
            List<string> KillList = new List<string>();
            //There are several screenshots for games that use a "default" or "image not found" or some generic varation of a blank screen. I don't want those games, so I filter them out.
            //I think I sorted the images based on file size (on disk) and saw a bunch of repeating images, and then calculated the MD5 of those images I wanted to kill and added them here.
            // Leaving the code in for now.
            //KillList.Add("e2b8f257fea66b661ee70efc73b6c84a");
            //KillList.Add("6a4ca1ab352df8af4a25c50a65bb8963");
            //KillList.Add("30ab4d58332ef5332affe5f3320c647a");
            //KillList.Add("1b7928278186f053777dea680b0a2b2d");
            //KillList.Add("5724a7dceff5b938ecd0ee7003f6a763");
            //KillList.Add("ab541cffaccbff5f9d2ad2d9031c0c48");
            //KillList.Add("a766be38df34c5db61ad5cd559919487");
            //KillList.Add("0be368e7b0f40f4287eceb539e00d0b9");
            //KillList.Add("26bdf324b11da6190f38886a3b0f7598");
            //KillList.Add("0734aca010260cee0bbf08b08e642fed");
            //KillList.Add("8f40e0665c367ea22d6ede551a8c2ed6");
            //KillList.Add("ace0c785acc73a72f52768838fb5e4fb");
            //KillList.Add("f28cffce4c580b1c28ef0c24e8e25f80");
            foreach (XmlNode node in root.SelectNodes("game"))
            {
                Game g = new Game();
                g.Name = node.Attributes["name"].Value.ToString();
                bool isClone;
                if (node.Attributes["cloneof"] != null)
                {
                    isClone = true;
                }
                else
                {
                    isClone = false;
                }
                string driverStatus = node.SelectSingleNode("driver")?.Attributes["status"].Value.ToString();
                string driverEmulation = node.SelectSingleNode("driver")?.Attributes["emulation"].Value.ToString();
                if (driverStatus == "good" && driverEmulation == "good")
                {
                    float version = 0;
                    _versions.TryGetValue(g.Name, out version);
                    if (version > 0 && version <= .161)
                    {
                        g.Description = node.SelectSingleNode("description").InnerText;
                        g.IsExcluded = false;
                        g.IsFavorite = false;

                        g.PlayCount = 0;
                        g.Year = node.SelectSingleNode("year").InnerText;
                        string category = _categories.Where(x => x.Key == g.Name).FirstOrDefault().Value;
                        if (category != null)
                        {
                            //Filter out mature games
                            if (category.Contains("* Mature *"))
                            {
                                g.IsMature = true;
                                g.IsExcluded = true;
                            }
                            else
                            {
                                g.IsMature = false;
                            }
                            if (!category.Contains("/"))
                            {
                                g.Category = category;
                                g.SubCategory = "";
                            }
                            else
                            {
                                string mainCategory = category.Substring(0, category.IndexOf("/") - 1);
                                int start = category.IndexOf("/") + 2;
                                int end = category.Length - start;
                                string subCategory = category.Substring(start, end);
                                g.Category = mainCategory;
                                g.SubCategory = subCategory;
                            }
                            g.Screenshot = g.Name + ".png";
                            
                            //Filter out games by category, subcategory, description, or if it's a clone
                            if (g.Category == "Electromechanical" || g.SubCategory == "Reels" || g.Category == "Casino" || g.SubCategory == "Mahjong" || (g.Category == "Rhythm" && (g.SubCategory == "Dance" || g.SubCategory == "Instruments")) || g.Category == "Home Systems" || g.Category == "Professional Systems" || g.Category == "System" || g.Category == "Ball & Paddle" || isClone || g.Description.Contains("DECO Cassette") || g.Category == "Multiplay" || g.Description.Contains("PlayChoice-10") || g.Category == "Quiz" || g.Description.Contains("bootleg"))
                            {
                                g.IsExcluded = true;
                            }

                            //Only add games for which we have a screenshot, and only if it's not in the Kill List
                            if (isValidScreenshot(Path.Combine(_snapsDir, g.Screenshot), KillList))
                            {
                                _games.Add(g);
                            }
                        }
                    }
                }
            }
            WriteNewGamesFile(_games, _gamesJson);
        }

        private static bool isValidScreenshot(string screenshot, List<string> killList)
        {
            if (File.Exists(screenshot))
            {
                string md5 = HashFile(screenshot);
                //Don't add it if it's in the kill list
                if (killList.Contains(md5))
                {
                    return false;
                }
                else
                {
                    //There were a bunch of screenshots that I wanted to kill and they all had similar timestamps (but varying MD5 hashes).
                    //  This code allows us to filter out based on timestamp. Again, leaving this code here "just in case" 
                    //TODO: Clean this up since it won't compile as-is.

                    //DateTime dt = File.tim(screenshot);
                    //DateTime dtStart = new DateTime(2015, 01, 16, 23, 18, 0);
                    //DateTime dtEnd = new DateTime(2015, 01, 16, 23, 20, 0);
                    //if (dt > dtStart && dt < dtEnd )
                    //{
                    //    return false;
                    //}
                    //else
                    {
                        return true;
                    }
                }
            }
            else
            {
                return false;
            }
        }

        static string HashFile(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return HashFile(fs);
            }
        }

        static string HashFile(FileStream stream)
        {
            StringBuilder sb = new StringBuilder();

            if (stream != null)
            {
                stream.Seek(0, SeekOrigin.Begin);

                MD5 md5 = MD5CryptoServiceProvider.Create();
                byte[] hash = md5.ComputeHash(stream);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));

                stream.Seek(0, SeekOrigin.Begin);
            }

            return sb.ToString();
        }
        static void WriteNewGamesFile(List<Game> _games, string _gamesJson)
        {
            using (StreamWriter sw = new StreamWriter(_gamesJson, false))
            {
                string json = JsonConvert.SerializeObject(_games);
                sw.WriteLine(json);
                sw.Close();
            }
        }
    }
}
