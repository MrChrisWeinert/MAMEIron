using System.Security.Cryptography;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using MAMEIron.Common;

namespace MAME_XML_Parser
{
    class Program
    {
        private string _mameDir;
        private string _listFull;
        private string _snapsDir;
        private string _gamesJson;
        private string _catver;
        private List<Game> _games;
        private Dictionary<string, string> _categories;
        private Dictionary<string, float> _versions;
        static void Main(string[] args)
        {
            Program aProgram = new Program();
            aProgram._games = new List<Game>();
            aProgram._mameDir = @"C:\MAME";
            aProgram._listFull = Path.Combine(aProgram._mameDir, "list.xml");
            aProgram._snapsDir = Path.Combine(aProgram._mameDir, "snap");
            aProgram._catver = Path.Combine(aProgram._mameDir, "catver.ini");
            aProgram._gamesJson = Path.Combine(aProgram._mameDir, "games.json");
            aProgram._categories = new Dictionary<string, string>();
            aProgram._versions = new Dictionary<string, float>();
            aProgram.LoadCategoriesAndVersions();
            aProgram.ParseXMLAndFilter();
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
            XmlDocument doc = new XmlDocument();
            doc.Load(_listFull);
            XmlNode root = doc.SelectSingleNode("mame");
            HashSet<string> drivers = new HashSet<string>();
            HashSet<string> statuses = new HashSet<string>();
            List<string> KillList = new List<string>();
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
                            if (category.Contains("* Mature *"))
                            {
                                g.IsMature = true;
                                g.IsExcluded = true;
                                //category = category.Replace("* Mature *", "");
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
                            if (g.Category == "Electromechanical" || g.SubCategory == "Reels" || g.Category == "Casino" || g.SubCategory == "Mahjong" || (g.Category == "Rhythm" && (g.SubCategory == "Dance" || g.SubCategory == "Instruments")) || g.Category == "Home Systems" || g.Category == "Professional Systems" || g.Category == "System" || g.Category == "Ball & Paddle" || isClone || g.Description.Contains("DECO Cassette") || g.Category == "Multiplay" || g.Description.Contains("PlayChoice-10") || g.Category == "Quiz" || g.Description.Contains("bootleg"))
                            {
                                g.IsExcluded = true;
                            }
                            if (File.Exists(Path.Combine(@"C:\MAME\snap", g.Screenshot)) && isValidScreenshot(Path.Combine(@"C:\MAME\snap", g.Screenshot), KillList))
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
            string md5 = HashFile(screenshot);
            if (killList.Contains(md5))
            {
                return false;
            }
            else
            {
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
            StreamWriter sw = new StreamWriter(_gamesJson, false);
            string json = JsonConvert.SerializeObject(_games);
            sw.WriteLine(json);
            sw.Close();
        }
    }
}
