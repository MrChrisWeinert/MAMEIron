using System.Configuration;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using MadeInTheUSB.MCU;
using MadeInTheUSB.Components;
using System.Drawing;
using AForge.Video.DirectShow;
using AForge.Vision.Motion;
using AForge.Video;
using System.Threading;
using System.Collections;

namespace MAMEIronWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _rootDirectory;
        private string _snapDirectory;
        private string _mameExe;
        private string _mameArgs;
        private string _gamesJson;
        private string _logFile;

        private Dictionary<string, System.Windows.Media.ImageSource> _snapshots;
        private ObservableCollection<Game> _games { get; set; }
        private int _selectedIndex { get; set; }

        private bool _supportNusbio;
        public NusbioPixel nusbioPixel;
        private int _whiteStripPWMIntensity;
        enum CabinetLights { On, Off};
        CabinetLights _cabinetLights;
        //I accidentally snapped off an LED from my 60-strip of lights :(
        private const int MAX_LED = (int)NusbioPixelDeviceType.Strip59;

        private bool _supportCamera;
        public DateTime _lastMotionDetectedTime;
        private MotionDetector detector;
        private const int MOTIONTIMEOUT = 15;// 15*60; //15 minutes
        private FilterInfoCollection _localWebCamsCollection;
        private VideoCaptureDevice _localWebCam;
        private static System.Timers.Timer _opacityTimer;

        private Logger _logger;
        public int _konamiCounter = 0;
        private Key[] KonamiCode = new Key[11];

        private DateTime _startTimeUpPress = new DateTime(0);
        private DateTime _startTimeDownPress = new DateTime(0);
        private DateTime _startTimeCPress;
        private DateTime _startTimeVPress;
        private const int LONGPRESSMILLISECONDS = 3000;
        private const int JUMPDISTANCE = 100;

        public MainWindow()
        {
            KonamiCode[0] = Key.R;
            KonamiCode[1] = Key.R;
            KonamiCode[2] = Key.F;
            KonamiCode[3] = Key.F;
            KonamiCode[4] = Key.D;
            KonamiCode[5] = Key.G;
            KonamiCode[6] = Key.D;
            KonamiCode[7] = Key.G;
            KonamiCode[8] = Key.A;
            KonamiCode[9] = Key.S;
            KonamiCode[10] = Key.C;
            InitializeComponent();
            _cabinetLights = CabinetLights.Off;
            _rootDirectory = ConfigurationManager.AppSettings["rootDirectory"];
            _mameArgs = ConfigurationManager.AppSettings["MAME_Args"];
            _logFile = Path.Combine(_rootDirectory, "log.txt");
            _mameExe = Path.Combine(_rootDirectory, "MAME64.EXE");
            _snapDirectory = Path.Combine(_rootDirectory, "snap");
            _snapshots = new Dictionary<string, System.Windows.Media.ImageSource>();
            _gamesJson = Path.Combine(_rootDirectory, "games.json");
            _supportCamera = Boolean.Parse(ConfigurationManager.AppSettings["Support_Camera"]);
            _supportNusbio = Boolean.Parse(ConfigurationManager.AppSettings["Support_Nusbio"]);
            _logger = new Logger(_logFile);

            Boolean.Parse(ConfigurationManager.AppSettings["Support_Nusbio"]);
            string errorText;
            if (!File.Exists(_mameExe))
            {
                errorText = $"{_mameExe} does not exist.";
                MessageBox.Show(errorText, "Fatal Error");
                _logger.LogException(errorText, new Exception("MAME executable not found"));
                Environment.Exit(1);
            }
            else if (!File.Exists(_gamesJson))
            {
                GameListCreator glc = new GameListCreator();
                glc.GenerateGameList(_mameExe, _gamesJson, _snapDirectory);
            }
            else if (!Directory.Exists(_snapDirectory))
            {
                errorText = $"{_snapDirectory} does not exist.";
                MessageBox.Show(errorText, "Fatal Error");
                _logger.LogException(errorText, new Exception("Snap directory not found"));
                Environment.Exit(1);
            }

            Application.Current.MainWindow.Left = 0;
            Application.Current.MainWindow.Top = 0;
            HideMouse();
            _logger.LogInfo("===============================================================================");
            _logger.LogInfo("Starting MAME");
            _logger.LogInfo("===============================================================================");
            Loaded += MainWindow_Loaded;

            _lastMotionDetectedTime = DateTime.Now.AddSeconds(20);

            if (_supportNusbio)
            {
                _whiteStripPWMIntensity = 254;
                
                nusbioPixel = ConnectToMCU(null, MAX_LED);
                if (nusbioPixel != null)
                {
                    _logger.LogVerbose("About to turn on the cabinet lights at the beginning.");
                    //FadeIn();
                    _logger.LogVerbose("Done with light initialization.");
                }
                //_logger.WriteToLogFile("FadeIn() complete");
                //PlaySound really only makes sense during the FadeIn sequence. If there are no lights, don't play the sound
                PlaySound("startup.wav");
                //_logger.WriteToLogFile("Intro sound was played");
            }


            #region Load games from disk and bind to the ListView
            LoadGamesFromJSON();
            lvGames.ItemsSource = GetUpdatedGameList();

            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lvGames.ItemsSource);
            lvGames.Focus();
            lvGames.SelectionMode = SelectionMode.Single;
            lvGames.SelectedIndex = 0;
            #endregion


            
            if (_supportCamera)
            {
                //Turn on the motion detection & voice recognition
                AForge.Controls.VideoSourcePlayer videoSourcePlayer2 = new AForge.Controls.VideoSourcePlayer();
                //            try
                //            {
                //                detector = GetDefaultMotionDetector();
                //                //videoSourcePlayer2.VideoSource = new VideoCaptureDevice(EnumerateVideoDevices().MonikerString);
                //                videoSourcePlayer2.NewFrame += Cam_NewFrame2;
                ////                videoSourcePlayer2.Start();
                //                //videoSourcePlayer2.Show();
                //                _localWebCam.Start();

                //            }
                //            catch (Exception ex)
                //            {
                //                //Do nothing. There is probably no webcam hooked up.
                //            }
                //this.LogRecognitionStart();
                //if (this.micClient == null)
                //{
                //    this.CreateMicrophoneRecoClient();
                //}
            }
        }

        private IEnumerable GetUpdatedGameList()
        {
            //Add all the games
            List<Game> gameList = _games.Where(x => x.IsExcluded == false && x.IsClone == false).OrderBy(x => x.Description).ToList();
            //Now insert the favorites at the top (this will create duplicates, which is OK)
            gameList.InsertRange(0, _games.Where(x => x.IsExcluded == false && x.IsClone == false && x.IsFavorite).OrderBy(x => x.Description).ToList());
            return gameList;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _logger.LogVerbose("MainWindow Loaded");
            if (_supportCamera)
            {
                _localWebCamsCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (_localWebCamsCollection.Count > 0)
                {
                    _localWebCam = new VideoCaptureDevice(_localWebCamsCollection[0].MonikerString);
                    _localWebCam.NewFrame += new NewFrameEventHandler(Cam_NewFrame);
                    //_localWebCam.Start();
                }
            }
            ToggleCabinetLights();
        }

        private void StartGame(Game game, string method)
        {
            _logger.LogVerbose($"StartGame {game.Name}.");
            _logger.LogVerbose("NusbioPixel.SetStrip_Begin");
            nusbioPixel?.SetStrip(Color.Beige, 0);
            _logger.LogVerbose("NusbioPixel.SetStrip_End");
            //videoSourcePlayer2.NewFrame -= Cam_NewFrame2;
            string st = _mameExe;
            Process process = new Process();
            process.StartInfo.FileName = st;
            process.StartInfo.WorkingDirectory = _rootDirectory;
            process.StartInfo.Arguments = game.Name + " " + _mameArgs;
            process.StartInfo.UseShellExecute = true;
            _logger.LogVerbose($"About to process.Start()");
            process.Start();
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                _logger.LogVerbose($"process.ExitCode==0");
                game.IncrementPlayCount();
            }
            else
            {
                _logger.LogInfo($"Couldn't start game: {game.Name} via {st}.");
                _games.First(x => x.Name == game.Name).IsExcluded = true;
                //Rebind since we excluded a game.
                lvGames.ItemsSource = GetUpdatedGameList();
                lvGames.Items.Refresh();

                _logger.LogInfo($"Excluded {game.Name} from games list.");
            }
            process.Close();
            _logger.LogVerbose($"process.Close()");
            PersistGameChanges();
            _logger.LogVerbose($"Games persisted to games.json.");
            //_lastMotionDetectedTime = DateTime.Now;
            //videoSourcePlayer2.NewFrame += Cam_NewFrame2;
            nusbioPixel?.SetStrip(70, 191, 238, 64);
        }
        public void HideMouse()
        {
            Mouse.OverrideCursor = Cursors.None;
        }
        private void LoadGamesFromJSON()
        {
            //List<Game> tempGames2 = new List<Game>();

            //using (StreamReader sr = new StreamReader(@"C:\MAME\games.json.old"))
            //{
            //    string json = sr.ReadToEnd();
            //    sr.Close();
            //    sr.Dispose();
            //    foreach (Game g in JsonConvert.DeserializeObject<List<Game>>(json))
            //    {
            //        if (g.PlayCount > 0)
            //        {
            //            tempGames2.Add(g);
            //        }
            //    }
            //}
            using (StreamReader sr = new StreamReader(_gamesJson))
            {
                string json = sr.ReadToEnd();
                sr.Close();
                sr.Dispose();
                _games = new ObservableCollection<Game>();
                List<Game> tempGames = JsonConvert.DeserializeObject<List<Game>>(json);
                foreach (Game g in tempGames)
                {
                    if (!g.IsExcluded && !g.IsClone)
                    {
                        //Game gold = tempGames2.FirstOrDefault(x => x.Name == g.Name);
                        //if (gold != null && gold.PlayCount > 0 && g.PlayCount!= gold.PlayCount)
                        //{
                        //    g.PlayCount = gold.PlayCount;
                        //}                        
                        _games.Add(g);
                    }
                }
            }
        }

        #region unused stuff from Planerator. May or not be safe to remove
        private void xSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            xx.Content = xSlider.Value;
        }
        private void ySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            yy.Content = ySlider.Value;
        }
        private void zSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            zz.Content = zSlider.Value;
        }
        private void fovSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ff.Content = fovSlider.Value;
        }

        #endregion
        private void PersistGameChanges()
        {
            //Make a backup of the old games list, just in case things go south.
            string _gamesJsonOld = _gamesJson.Replace(".json","") + DateTime.Now.ToString("yyyyMMddHHmmss") + ".json";
            File.Copy(_gamesJson, _gamesJsonOld);
            //Write out the new games list.
            using (StreamWriter sw = new StreamWriter(_gamesJson, false))
            {
                string json = JsonConvert.SerializeObject(_games);
                sw.WriteLine(json);
                sw.Close();
                sw.Dispose();
            }
        }
        private void lvGames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvGames.SelectedItem != null)
            {
                Game game = (Game)lvGames.SelectedItem;
                if (game == null) return;
                _logger.LogVerbose($"SelectionChanged: Game Name: {game.Name} Selected Index: {lvGames.SelectedIndex} Selected Item Name: {((Game)lvGames.SelectedItem).Name}");
                string s = Path.Combine(_snapDirectory, game.Screenshot);

                System.Windows.Media.ImageSource imageSource = new BitmapImage();
                if (File.Exists(s))
                {
                    imageSource = new BitmapImage(new Uri(s));
                }
                snap.Width = 330;
                snap.Height = 274;
                snap.MaxWidth = 330;
                snap.MaxHeight = 274;
                snap.Source = imageSource;
                GameMetadata.Content = $"Year: {game.Year}   Plays: {game.PlayCount}";
            }
        }

        #region keyboard handler
        //It's "Key" to note (see what I did there?) that the lvGames_SelectionChanged is fired before the KeyDown
        private void lvGames_KeyDown(object sender, KeyEventArgs e)
        {
            if (_konamiCounter > 10)
            {
                _konamiCounter = 0;
                Contra.Opacity = 0;
            }
            try
            {
                if (KonamiCode[_konamiCounter] == e.Key)
                {
                    _konamiCounter++;
                }
                else
                {
                    //reset
                    _konamiCounter = 0;
                }
                if (_konamiCounter > 10)
                {
                    //Easter egg!
                    e.Handled = true;
                    Contra.Opacity = 1;
                }
                
                if (!e.Handled)
                {
                    switch (e.Key)
                    {
                        case Key.V:
                            _logger.LogVerbose("V in KeyDown.");
                            break;
                        case Key.C:
                            _logger.LogVerbose("C in KeyDown.");
                            break;
                        case Key.System:
                            _logger.LogVerbose("System in KeyDown.");
                            break;
                        case Key.LeftCtrl:
                            _logger.LogVerbose("LeftCtrl in KeyDown.");
                            break;
                        case Key.LeftAlt:
                            _logger.LogVerbose("LeftAlt in KeyDown.");
                            break;
                        case Key.Space:
                            _logger.LogVerbose("Space in KeyDown.");
                            break;
                        case Key.LeftShift:
                            _logger.LogVerbose("LeftShift in KeyDown.");
                            break;
                        case Key.Z:
                            _logger.LogVerbose("Z in KeyDown.");
                            break;
                        case Key.X:
                            _logger.LogVerbose("X in KeyDown.");
                            break;
                        case Key.Up:
                            _logger.LogVerbose("Up in KeyDown.");
                            break;
                        case Key.Down:
                            _logger.LogVerbose("Down in KeyDown.");
                            break;
                        case Key.A:
                            _logger.LogVerbose("A in KeyDown.");
                            //videoSourcePlayer2.NewFrame -= Cam_NewFrame2;
                            //var tasks = new List<Task<bool>>();
                            //tasks.Add(Task<bool>.Factory.StartNew(GetVoiceTextReg));
                            //Task.WaitAll(tasks.ToArray());
                            //Thread.Sleep(5000);
                            //if (_voiceGameList == null)
                            //{
                            //    _logger.WriteToLogFile("The voiceToTtext button was pressed, but no games were in the _voiceGameList.");
                            //    videoSourcePlayer2.NewFrame += Cam_NewFrame2;
                            //}
                            //else if (_voiceGameList.Count == 1)
                            //{
                            //    StartGame(_voiceGameList[0], "voice");
                            //    videoSourcePlayer2.NewFrame += Cam_NewFrame2;
                            //}
                            //else if (_voiceGameList.Count > 1)
                            //{
                            //    gameList.Visible = false;
                            //    snaps.Visible = false;
                            //    favoriteList.Visible = false;
                            //    this.Enabled = false;
                            //    Form f = new FuzzyGameSelectForm(this, _voiceGameList);
                            //}
                            //else
                            //{
                            //    _logger.WriteToLogFile("The voiceToTtext button was pressed, but no games were in the _voiceGameList.");
                            //    videoSourcePlayer2.NewFrame += Cam_NewFrame2;
                            //}
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogException("Exception in lvGames_KeyDown", ex);
            }
        }

        private void ToggleFavorite()
        {
            PlaySound("pacman_cherry.wav");
            ((Game)lvGames.SelectedItem).ToggleFavorite();
            PersistGameChanges();

            lvGames.ItemsSource = GetUpdatedGameList();
            lvGames.Items.Refresh();

            if (lvGames.SelectedIndex <= 0 && _selectedIndex > 0)
            {
                lvGames.SelectedIndex = _selectedIndex;
            }
        }

        private void lvGames_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            _logger.LogVerbose($"{e.Key.ToString()} pressed in PreviewKeyDown");
            switch (e.Key)
            {
                case Key.C:
                    //If we're coming in with a new button press, save the exact time that the button was pressed.
                    if (_startTimeCPress == new DateTime(0))
                    {
                        _logger.LogVerbose("_startTimeCPress was Zero so we just set it...");
                        _startTimeCPress = DateTime.Now;
                    }

                    //If the button has been held down for < 2 seconds, do nothing. This is a Short-press and the action will happen in PreviewKeyUp
                    if (DateTime.Now < _startTimeCPress.AddMilliseconds(LONGPRESSMILLISECONDS))
                    {
                        //Short-press
                        //Do nothing...action will hapen in PreviewKeyUp
                    }
                    //If the button is being held down, but the start time is this arbitrary number I chose, then the game has already been toggled, and we're just waiting for the user to release the button.
                    else if (_startTimeCPress == new DateTime(1))
                    {
                        //Do nothing....this means the game has been toggled, but the person has not yet released the button.
                        _logger.LogVerbose("Release the Kraken!");
                    }
                    //If the button has been held down for 2 seconds or longer, Toggle the favorite.
                    //TODO: We need to provide an indicator to the user (visually, via the Pac-Man favorite icon [ooohhh...maybe audio too?), so he/she knows to let go of the button.
                    else
                    {
                        //Long-press
                        _logger.LogVerbose("C was long-pressed in PreviewkeyDown.");
                        _logger.LogVerbose("Toggling Favorite via long-press in PreviewkeyDown.");
                        ToggleFavorite();
                        _logger.LogVerbose("Reset C Start Time to zero in PreviewkeyDown.");
                        //This is where the arbitrary time value gets set.
                        _startTimeCPress = new DateTime(1);
                    }                    
                    break;
                case Key.V:
                    //If we're coming in with a new button press, save the exact time that the button was pressed.
                    if (_startTimeVPress == new DateTime(0))
                    {
                        _logger.LogVerbose("_startTimeEscPress was Zero so we just set it...");
                        _startTimeVPress = DateTime.Now;
                    }
                    //If the button has been held down for 2 seconds or longer, trigger the Exit Window
                    else
                    {
                        e.Handled = true;
                        //Long-press
                        _logger.LogVerbose("V was long-pressed in PreviewkeyDown.");
                        ExitWindow exitWindow = new ExitWindow();
                        exitWindow.Show();
                        exitWindow.ExitListView.Focus();
                        _logger.LogVerbose("Reset V Start Time to zero in PreviewkeyDown.");
                        //This is where the arbitrary time value gets set.
                        _startTimeVPress = new DateTime(1);
                    }
                    break;
                case Key.Down:
                    if (_startTimeDownPress == new DateTime(0))
                    {
                        _startTimeDownPress = DateTime.Now;
                        _logger.LogVerbose($"The Down key was pressed at {_startTimeDownPress}.");
                    }
                    else if (_startTimeDownPress.AddMilliseconds(LONGPRESSMILLISECONDS) < DateTime.Now && _startTimeDownPress != new DateTime(0))
                    {
                        _logger.LogVerbose($"Jumping down {JUMPDISTANCE} items in the games list..");
                        Game game = (Game)lvGames.SelectedItem;
                        _logger.LogVerbose($"SelectedIndex: {lvGames.SelectedIndex}. lvGames Item count: {lvGames.Items.Count}");
                        if (lvGames.SelectedIndex + JUMPDISTANCE > lvGames.Items.Count)
                        {
                            lvGames.SelectedIndex = lvGames.Items.Count;
                        }
                        else
                        {
                            lvGames.SelectedIndex += JUMPDISTANCE;
                        }
                        lvGames.SelectedItem = lvGames.Items.GetItemAt(lvGames.SelectedIndex);
                        lvGames.ScrollIntoView(lvGames.SelectedItem);
                        ListViewItem item = lvGames.ItemContainerGenerator.ContainerFromItem(lvGames.SelectedItem) as ListViewItem;
                        item.Focus();
                    }
                    break;
                case Key.Up:
                    if (_startTimeUpPress == new DateTime(0))
                    {
                        _startTimeUpPress = DateTime.Now;
                        _logger.LogVerbose($"The Up key was pressed at {_startTimeUpPress}.");
                    }
                    else if (_startTimeUpPress.AddMilliseconds(LONGPRESSMILLISECONDS) < DateTime.Now && _startTimeUpPress != new DateTime(0))
                    {
                        _logger.LogVerbose($"Jumping up {JUMPDISTANCE} items in the games list..");
                        Game game = (Game)lvGames.SelectedItem;
                        if (lvGames.SelectedIndex - JUMPDISTANCE < 0)
                        {
                            lvGames.SelectedIndex = 0;
                        }
                        else
                        {
                            lvGames.SelectedIndex -= JUMPDISTANCE;
                        }
                        _logger.LogVerbose($"SelectedIndex: {lvGames.SelectedIndex}. lvGames Item count: {lvGames.Items.Count}");
                        lvGames.SelectedItem = lvGames.Items.GetItemAt(lvGames.SelectedIndex);
                        lvGames.ScrollIntoView(lvGames.SelectedItem);
                        ListViewItem item = lvGames.ItemContainerGenerator.ContainerFromItem(lvGames.SelectedItem) as ListViewItem;
                        item.Focus();
                    }
                    break;
            }
        }

        private void lvGames_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            _logger.LogVerbose($"{e.Key.ToString()} pressed in PreviewKeyUp");
            switch (e.Key)
            {
                case Key.C:
                    //If the person lets go of the button, and this arbitrary value is set, do nothing.
                    //This means it was a long-press and the game has already been toggled in the PreviewKeyDown event.
                    if (_startTimeCPress == new DateTime(1))
                    {                        
                        _logger.LogVerbose("We just toggled a game in PreviewKeyDown, so this C PreviewkeyUp event should be ignored.");
                        _logger.LogVerbose("Reset C Start Time to zero in PreviewkeyUp: A.");
                        //Reset the button press timer back to zero.
                        _startTimeCPress = new DateTime(0);
                    }
                    //If the person lets go of the button and it's been under (2?) seconds, it was a short press. We need to start the game.
                    else if (DateTime.Now < _startTimeCPress.AddMilliseconds(LONGPRESSMILLISECONDS))
                    {
                        //Short-press
                        _logger.LogVerbose("C was short-pressed in PreviewkeyUp...");
                        _logger.LogVerbose("Starting game in PreviewkeyUp...");
                        StartGame((Game)lvGames.SelectedItem, "button");
                        _logger.LogVerbose("Reset C Start Time to zero in PreviewkeyUp: B.");
                        //Reset the button press timer back to zero.
                        _startTimeCPress = new DateTime(0);
                    }                    
                    break;
                case Key.V:
                    if (DateTime.Now < _startTimeVPress.AddMilliseconds(LONGPRESSMILLISECONDS))
                    {
                        //Short-press
                        _logger.LogVerbose("V was short-pressed in PreviewkeyUp...");
                        //Do nothing in this case.
                    }
                    else
                    {
                        //Long-press
                        //probably do nothing here.


                        //_logger.LogVerbose("V was long-pressed in PreviewkeyUp...");
                        //ExitWindow exitWindow = new ExitWindow();
                        //exitWindow.Show();                        
                        //Exit MAMEIron, or possibly Windows.
                    }
                    _startTimeVPress = new DateTime(0);
                    break;
                case Key.Down:
                    _startTimeDownPress = new DateTime(0);
                    break;
                case Key.Up:
                    _startTimeUpPress = new DateTime(0);
                    break;
            }
            _selectedIndex = lvGames.SelectedIndex;
        }
#endregion

        #region Lights
        void ToggleMarqueeLights()
        {
            if (nusbioPixel == null) return;
            _logger.LogVerbose("In ToggleMarqueeLights()");
            //initial starting brightness is 128?
            nusbioPixel.SetBrightness(64 * 2);
            _logger.LogVerbose("_whiteStripPWMIntensity: " + _whiteStripPWMIntensity.ToString());
            if (_whiteStripPWMIntensity < 255)
            {
                var r = nusbioPixel.AnalogWrite(Mcu.GpioPwmPin.Gpio5, _whiteStripPWMIntensity);
            }
        }
        void _opacityTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _logger.LogVerbose("in _opacityTimer_Elapsed()");
            System.Timers.Timer t = (System.Timers.Timer)sender;
            if (this.Opacity < 1)
            {
                ChangeOpacity();
            }
            else
            {
                _logger.LogVerbose("Opacity Timer Disabled.");
                t.Enabled = false;
            }
        }
        private void ChangeOpacity()
        {
            _logger.LogVerbose("In ChangeOpacity()");
            this.Opacity += .00568;
            if (nusbioPixel == null) return;
            nusbioPixel.SetBrightness(64 * 2);            
            if (_whiteStripPWMIntensity >= 255) return;
            if (this.Opacity > .08 && this.Opacity < .5)
            {
                _logger.LogVerbose("opacity > .08 and < .5)");
                _whiteStripPWMIntensity++;
                Debug.WriteLine(_whiteStripPWMIntensity);
                var r = nusbioPixel.AnalogWrite(Mcu.GpioPwmPin.Gpio5, _whiteStripPWMIntensity);
            }
            else if (this.Opacity >= .5)
            {
                _logger.LogVerbose("opacity > .5)");
                _whiteStripPWMIntensity += 2;
                Debug.WriteLine(_whiteStripPWMIntensity);
                if (_whiteStripPWMIntensity < 255)
                {
                    var r = nusbioPixel.AnalogWrite(Mcu.GpioPwmPin.Gpio5, _whiteStripPWMIntensity);
                }
            }
        }
        private void FadeIn()
        {
            _logger.LogVerbose("In FadeIn()");
            this.Opacity = 0;
            _opacityTimer = new System.Timers.Timer();
            _opacityTimer.Interval = 125;
            _opacityTimer.Elapsed += _opacityTimer_Elapsed;
            _opacityTimer.Enabled = true;

        }
        private void ToggleCabinetLights()
        {
            if (nusbioPixel == null) return;
            if (_cabinetLights == CabinetLights.On)
            {
                _logger.LogVerbose("Cabinet lights are currently on. Turning them off.");
                //Color doesn't matter here...set the intensity to zero
                nusbioPixel?.SetStrip(70, 191, 238, 0);
                ToggleMarqueeLights();
                _cabinetLights = CabinetLights.Off;
            }
            else
            {
                _logger.LogVerbose("Cabinet lights are currently off. Turning them on.");
                nusbioPixel?.SetStrip(70, 191, 238, 64);
                ToggleMarqueeLights();
                _cabinetLights = CabinetLights.On;
            }
        }
        private static NusbioPixel ConnectToMCU(NusbioPixel nusbioPixel, int maxLed)
        {
            if (nusbioPixel != null)
            {
                nusbioPixel.Dispose();
                nusbioPixel = null;
            }
            var comPort = new NusbioPixel().DetectMcuComPort();
            if (comPort == null)
            {
                Logger _logger = new Logger(Path.Combine(ConfigurationManager.AppSettings["rootDirectory"], "log.txt"));
                _logger.LogInfo("Nusbio Pixel not detected.");
                return null;
            }
            nusbioPixel = new NusbioPixel(maxLed, comPort);
            if (nusbioPixel.Initialize().Succeeded)
            {
                if (nusbioPixel.SetBrightness(nusbioPixel.DEFAULT_BRIGHTNESS).Succeeded)
                {
                    return nusbioPixel;
                }
            }
            return null;
        }
        private class BubbleColor
        {
            public Color color;
            public int originalPosition;
        }
        private BubbleColor[] InitRandomBubbleColors()
        {
            Random rnd = new Random();
            BubbleColor[] bubbleColors = new BubbleColor[nusbioPixel.Count];
            for (int i = 0; i < nusbioPixel.Count; i++)
            {
                var color = RGBHelper.Wheel((i * 256 / nusbioPixel.Count) + 4);
                bubbleColors[i] = new BubbleColor();
                bubbleColors[i].color = color;// r = color.R;
                bubbleColors[i].originalPosition = i;// = (int)(0.299 * bubbleColors[i].r + 0.587 * bubbleColors[i].g + 0.144 * bubbleColors[i].b);
            }
            bubbleColors = bubbleColors.OrderBy(a => Guid.NewGuid()).ToArray<BubbleColor>();
            for (int i = 0; i < nusbioPixel.Count; i++)
            {
                nusbioPixel.SetPixel(i, bubbleColors[i].color.R, bubbleColors[i].color.G, bubbleColors[i].color.B);
            }
            nusbioPixel.SetBrightness(30);
            nusbioPixel.Show();
            return bubbleColors;
        }
        private delegate void ColorBubbleSortDDelegate();
        private void ColorBubbleSortD()
        {
            //if (this.InvokeRequired)
            //{
            //    ColorBubbleSortDDelegate del = new ColorBubbleSortDDelegate(ColorBubbleSortD);
            //    object[] parameters = { };
            //    this.Invoke(del);
            //}
            //else
            //{
                BubbleColor[] bubbleColors = InitRandomBubbleColors();
                int brightness = 40;
                int n = bubbleColors.Length;
                for (int x = 0; x < n; x++)
                {
                    brightness += 2;
                    if (brightness < nusbioPixel.GetMaxBrightness())
                    {
                        nusbioPixel.SetBrightness(brightness);
                    }

                    for (int y = 0; y < n - 1; y++)
                    {
                        if (bubbleColors[y].originalPosition > bubbleColors[y + 1].originalPosition)
                        {
                            BubbleColor temp = new BubbleColor();
                            temp.color = bubbleColors[y + 1].color;
                            temp.originalPosition = bubbleColors[y + 1].originalPosition;

                            bubbleColors[y + 1].color = bubbleColors[y].color;
                            bubbleColors[y + 1].originalPosition = bubbleColors[y].originalPosition;

                            bubbleColors[y].color = temp.color;
                            bubbleColors[y].originalPosition = temp.originalPosition;

                            nusbioPixel.SetPixel(y, bubbleColors[y].color.R, bubbleColors[y].color.G, bubbleColors[y].color.B);
                            nusbioPixel.SetPixel(y + 1, bubbleColors[y + 1].color.R, bubbleColors[y + 1].color.G, bubbleColors[y + 1].color.B);
                            nusbioPixel.Show();
                        }
                    }
                }
            //}
        }
        private void ColorBubbleSort()
        {
            BubbleColor[] bubbleColors = InitRandomBubbleColors();
            int brightness = 40;
            int n = bubbleColors.Length;
            for (int x = 0; x < n; x++)
            {
                brightness += 2;
                if (brightness < nusbioPixel.GetMaxBrightness())
                {
                    nusbioPixel.SetBrightness(brightness);
                }

                for (int y = 0; y < n - 1; y++)
                {
                    if (bubbleColors[y].originalPosition > bubbleColors[y + 1].originalPosition)
                    {
                        BubbleColor temp = new BubbleColor();
                        temp.color = bubbleColors[y + 1].color;
                        temp.originalPosition = bubbleColors[y + 1].originalPosition;

                        bubbleColors[y + 1].color = bubbleColors[y].color;
                        bubbleColors[y + 1].originalPosition = bubbleColors[y].originalPosition;

                        bubbleColors[y].color = temp.color;
                        bubbleColors[y].originalPosition = temp.originalPosition;

                        nusbioPixel.SetPixel(y, bubbleColors[y].color.R, bubbleColors[y].color.G, bubbleColors[y].color.B);
                        nusbioPixel.SetPixel(y + 1, bubbleColors[y + 1].color.R, bubbleColors[y + 1].color.G, bubbleColors[y + 1].color.B);
                        nusbioPixel.Show();
                    }
                }
            }
        }
#endregion

        #region WebCam
        //private FilterInfo EnumerateVideoDevices()
        //{
        //    //enumerate video devices
        //    var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        //    if (videoDevices.Count == 1)
        //    {
        //        return videoDevices[0];
        //    }
        //    else if (videoDevices.Count != 0)
        //    {
        //        //add all devices to combo
        //        foreach (FilterInfo device in videoDevices)
        //        {
        //            if (device.Name.ToLower().Contains("hp") || device.Name.ToLower().Contains("logitech"))
        //            {
        //                return device;
        //            }
        //        }
        //    }
        //    return videoDevices[0];
        //}
        private void EnumerateVideoModes(VideoCaptureDevice device)
        {
            //get resolutions for selected video source
            try
            {
                VideoCapabilities[] videoCapabilities = device.VideoCapabilities;
                foreach (VideoCapabilities capabilty in videoCapabilities)
                {
                    System.Drawing.Size size = capabilty.FrameSize;
                }
            }
            catch
            {
            }
            finally
            {
            }
        }
#endregion

        #region Motion Detection
        void Cam_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                System.Drawing.Image img = (Bitmap)eventArgs.Frame.Clone();

                MemoryStream ms = new MemoryStream();
                img.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                ms.Seek(0, SeekOrigin.Begin);
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.EndInit();

                bi.Freeze();
                Dispatcher.BeginInvoke(new ThreadStart(delegate
                {
                    //frameHolder.Source = bi;
                    _lastMotionDetectedTime = DateTime.Now;
                    if (_cabinetLights == CabinetLights.Off)
                    {
                        ToggleCabinetLights();
                        FadeIn();
                        //PlaySound();
                    }
                }));
            }
            catch (Exception ex)
            {
            }
        }
        void Cam_NewFrame2(object sender, ref Bitmap image)
        {
            if (this.detector.ProcessFrame(image) > 0.2) //.2 was "ok"
            {
                ActivityDetected();
            }
            else
            {
                NoActivityDetected();
            }
        }
        private delegate void NoActivityDetectedDelegate();
        private void NoActivityDetected()
        {
            //if (this.InvokeRequired)
            //{
            //    NoActivityDetectedDelegate del = new NoActivityDetectedDelegate(NoActivityDetected);
            //    object[] parameters = { };
            //    this.Invoke(del);
            //}
            //else
            //{
                if (_cabinetLights == CabinetLights.On && _lastMotionDetectedTime < DateTime.Now.AddSeconds(-1 * MOTIONTIMEOUT))
                {
                    this.Opacity = 0;
                    nusbioPixel?.AnalogWrite(Mcu.GpioPwmPin.Gpio5, 0);
                    nusbioPixel?.SetStrip(Color.Beige, 20);
                    _logger.LogInfo($"Turn Arcade Machine Off at {DateTime.Now}. _lastMotionDetectedTime: {_lastMotionDetectedTime}");
                    _cabinetLights = CabinetLights.Off;
                    _logger.LogVerbose("Cabinet lights were toggled off.");
                }
            //}
        }
        private delegate void ActivityDetectedDelegate();
        private void ActivityDetected()
        {
            //if (this.InvokeRequired)
            //{
            //    ActivityDetectedDelegate del = new ActivityDetectedDelegate(ActivityDetected);
            //    object[] parameters = { };
            //    this.Invoke(del);
            //}
            //else
            //{
                _lastMotionDetectedTime = DateTime.Now;
                if (_cabinetLights == CabinetLights.Off)
                {
                    ToggleCabinetLights();
                    //FadeIn();
                    //PlaySound();
                }
            //}
        }
        public static MotionDetector GetDefaultMotionDetector()
        {
            IMotionDetector detector = null;
            IMotionProcessing processor = null;
            MotionDetector motionDetector = null;

            //detector = new AForge.Vision.Motion.TwoFramesDifferenceDetector()
            //{
            //    DifferenceThreshold = 15,
            //    SuppressNoise = true
            //};

            //detector = new AForge.Vision.Motion.CustomFrameDifferenceDetector()
            //{
            //    DifferenceThreshold = 55,
            //    KeepObjectsEdges = true,
            //    SuppressNoise = true
            //};

            detector = new SimpleBackgroundModelingDetector()
            {
                DifferenceThreshold = 10,
                FramesPerBackgroundUpdate = 10,
                KeepObjectsEdges = true,
                MillisecondsPerBackgroundUpdate = 0,
                SuppressNoise = true
            };

            //processor = new AForge.Vision.Motion.GridMotionAreaProcessing()
            //{
            //    HighlightColor = System.Drawing.Color.Red,
            //    HighlightMotionGrid = true,
            //    GridWidth = 100,
            //    GridHeight = 100,
            //    MotionAmountToHighlight = 10F
            //};

            processor = new BlobCountingObjectsProcessing()
            {
                HighlightColor = System.Drawing.Color.Red,
                HighlightMotionRegions = true,
                MinObjectsHeight = 200,
                MinObjectsWidth = 200
            };

            motionDetector = new MotionDetector(detector, processor);

            return (motionDetector);
        }
#endregion

        #region Audio
        private delegate void PlaySoundDelegate();
        private void PlaySound(string audioFile)
        {
            //if (this.InvokeRequired)
            //{
            //    PlaySoundDelegate del = new PlaySoundDelegate(PlaySound);
            //    object[] parameters = { };
            //    this.Invoke(del);
            //}
            //else
            //{
            if (audioFile == "startup.wav")
            {
                Thread.Sleep(2000);
            }            
            string fileName = Path.Combine(_rootDirectory, "Media", audioFile);
            if (File.Exists(fileName))
            {                
                System.Media.SoundPlayer player = new System.Media.SoundPlayer(fileName);
                player.Play();
            }
            //}
        }
#endregion

        private void window_Closed(object sender, EventArgs e)
        {
            //_localWebCam.Stop();
            _logger.LogInfo("Toggle Cabinet Lights_Begin");
            ToggleCabinetLights();
            _logger.LogInfo("Toggle Cabinet Lights_End");
            _logger.LogInfo("===============================================================================");
            _logger.LogInfo("Shutting down MAME");
            _logger.LogInfo("===============================================================================");
            Application.Current.Shutdown();
        }
    }
}
