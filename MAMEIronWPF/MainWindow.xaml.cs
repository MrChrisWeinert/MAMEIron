﻿using System.Configuration;
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
using MAMEIron.Common;
using MadeInTheUSB.MCU;
using MadeInTheUSB.Components;
using System.Drawing;
using AForge.Video.DirectShow;
using AForge.Vision.Motion;
using AForge.Video;
using System.Threading;

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
        private string _gamesJson;
        private string _logFile;

        private int _ctrlcount = 0;
        private Dictionary<string, System.Windows.Media.ImageSource> _snapshots;
        private ObservableCollection<Game> _games { get; set; }
        private int _selectedIndex { get; set; }

        public NusbioPixel nusbioPixel;
        private int _whiteStripPWMIntensity;
        enum CabinetLights { On, Off};
        CabinetLights _cabinetLights;

        public DateTime _lastMotionDetectedTime;
        private MotionDetector detector;
        private const int MOTIONTIMEOUT = 15;// 15*60; //15 minutes
        private FilterInfoCollection _localWebCamsCollection;
        private VideoCaptureDevice _localWebCam;
        private static System.Timers.Timer _opacityTimer;

        private Utility _utility;

        public MainWindow()
        {
            InitializeComponent();
            _cabinetLights = CabinetLights.Off;
            _rootDirectory = ConfigurationManager.AppSettings["rootDirectory"];
            _logFile = Path.Combine(_rootDirectory, "log.txt");
            _mameExe = Path.Combine(_rootDirectory, "MAME64.EXE");
            _snapDirectory = Path.Combine(_rootDirectory, "snap");
            _snapshots = new Dictionary<string, System.Windows.Media.ImageSource>();
            _gamesJson = Path.Combine(_rootDirectory, "games.json");
            _utility = new Utility(_logFile);
            string errorText;
            if (!File.Exists(_mameExe))
            {
                errorText = $"{_mameExe} does not exist.";
                MessageBox.Show(errorText, "Fatal Error");
                _utility.WriteToLogFile(errorText);
                Environment.Exit(1);
            }
            else if (!File.Exists(_gamesJson))
            {
                errorText = $"{_gamesJson} does not exist.";
                MessageBox.Show(errorText, "Fatal Error");
                _utility.WriteToLogFile(errorText);
                Environment.Exit(1);
            }
            else if (!Directory.Exists(_snapDirectory))
            {
                errorText = $"{_snapDirectory} does not exist.";
                MessageBox.Show(errorText, "Fatal Error");
                _utility.WriteToLogFile(errorText);
                Environment.Exit(1);
            }

            Application.Current.MainWindow.Left = 0;
            Application.Current.MainWindow.Top = 0;
            HideMouse();
            _utility.WriteToLogFile("Starting MAME");

            Loaded += MainWindow_Loaded;

            _lastMotionDetectedTime = DateTime.Now.AddSeconds(20);
            #region lights
            _whiteStripPWMIntensity = 254;
            var rgbLedType = NusbioPixelDeviceType.Strip59;
            var MAX_LED = (int)rgbLedType;
            nusbioPixel = ConnectToMCU(null, MAX_LED);
            if (nusbioPixel != null)
            {
                _utility.WriteToLogFile("About to turn on the cabinet lights at the beginning.");
                //FadeIn();
                _utility.WriteToLogFile("Done with light initialization.");
            }
            #endregion
            //_utility.WriteToLogFile("FadeIn() complete");
            PlaySound();
            //_utility.WriteToLogFile("Intro sound was played");


            #region Load games from disk and bind to the ListView
            LoadGamesFromJSON();
            lvGames.ItemsSource = _games.OrderByDescending(x => x.IsFavorite).ThenBy(x => x.Description); ;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lvGames.ItemsSource);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("IsFavorite");
            view.GroupDescriptions.Add(groupDescription);
            lvGames.Focus();
            lvGames.SelectionMode = SelectionMode.Single;
            lvGames.SelectedIndex = 0;
#endregion


#region Turn on the motion detection & voice recognition
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
#endregion
        }
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _utility.WriteToLogFile("MainWindow Loaded");
            _localWebCamsCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (_localWebCamsCollection.Count > 0)
            {
                _localWebCam = new VideoCaptureDevice(_localWebCamsCollection[0].MonikerString);
                _localWebCam.NewFrame += new NewFrameEventHandler(Cam_NewFrame);
                //_localWebCam.Start();
            }
            ToggleCabinetLights();
        }
        void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void StartGame(Game game, string method)
        {
            nusbioPixel?.SetStrip(Color.Beige, 0);
            //videoSourcePlayer2.NewFrame -= Cam_NewFrame2;
            string st = _mameExe; // +" -video ddraw pacman";
            Process process = new Process();
            process.StartInfo.FileName = st;
            process.StartInfo.WorkingDirectory = _rootDirectory;
            process.StartInfo.Arguments = game.Name + " -autosave -skip_gameinfo -video d3d";
            process.StartInfo.UseShellExecute = true;
            process.Start();
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                game.IncrementPlayCount();
            }
            else
            {
                _utility.WriteToLogFile($"Couldn't start game: {game.Name} via {st}.");
                _games.Remove(game);
                //Rebind since we removed a game.
                lvGames.ItemsSource = _games.OrderByDescending(x => x.IsFavorite).ThenBy(x => x.Description);
                lvGames.Items.Refresh();

                _utility.WriteToLogFile($"Removed {game.Name} from games list.");
            }
            process.Close();
            PersistGameChanges();
            _utility.WriteToLogFile($"Games persisted to games.json.");
            //_lastMotionDetectedTime = DateTime.Now;
            //videoSourcePlayer2.NewFrame += Cam_NewFrame2;
            //nusbioPixel?.SetStrip(Color.RoyalBlue, 64);
            nusbioPixel?.SetStrip(70, 191, 238, 64);

            //TODO: This was previously here...not sure we need it after every "StartGame()" or if it's okay to just have it once at app startup.
            //HideMouse();
        }
        public void HideMouse()
        {
            Mouse.OverrideCursor = Cursors.None;
        }
        private void LoadGamesFromJSON()
        {
            using (StreamReader sr = new StreamReader(_gamesJson))
            {
                string json = sr.ReadToEnd();
                sr.Close();
                sr.Dispose();
                _games = new ObservableCollection<Game>();
                //_games = JsonConvert.DeserializeObject<ObservableCollection<Game>>(json);
                List<Game> tempGames = JsonConvert.DeserializeObject<List<Game>>(json); ;
                foreach (Game g in tempGames)
                {
                    if (!g.IsExcluded && !g.IsClone)
                    {
                        _games.Add(g);
                    }
                }
            }
        }
#region unused stuff from Planerator
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
            //if (lvGames.SelectedIndex == 0 && _selectedIndex > 0)
            //{
            //    lvGames.SelectedIndex = _selectedIndex;
            //}
            if (lvGames.SelectedItem != null)
            {
                //TODO: These 3 lines seem unnecessary
                Game g = (Game)lvGames.SelectedItem;
                int i = lvGames.Items.IndexOf(g);
                lvGames.SelectedIndex = i;


                Game game = (Game)lvGames.SelectedItem;
                if (game == null) return;
                string s = Path.Combine(_snapDirectory, game.Screenshot);
                System.Windows.Media.ImageSource imageSource = new BitmapImage(new Uri(s));
                snap.Width = 330;
                snap.Height = 274;
                snap.MaxWidth = 330;
                snap.MaxHeight = 274;
#if DEBUG

#else
                snap.Source = imageSource;
#endif
                GameMetadata.Content = $"Year: {game.Year}   Plays: {game.PlayCount}";
            }
        }
#region keyboard handler
        //It's "Key" to note (see what I did there?) that the lvGames_SelectionChanged is fired before the KeyDown
        private void lvGames_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                switch (e.Key)
                {
                    case Key.D1:
                        StartGame((Game)lvGames.SelectedItem, "button");
                        break;
                    case Key.LeftCtrl:
                        _ctrlcount++;
                        if (_ctrlcount == 3)
                        {
                            Game g = (Game)lvGames.SelectedItem;
                            //string desc = ((Game)lvGames.SelectedItem).Description;
                            ((Game)lvGames.SelectedItem).ToggleFavorite();
                            PersistGameChanges();

                            lvGames.ItemsSource = _games.OrderByDescending(x => x.IsFavorite).ThenBy(x => x.Description);
                            lvGames.Items.Refresh();

                            if (lvGames.SelectedIndex <= 0 && _selectedIndex > 0)
                            {
                                lvGames.SelectedIndex = _selectedIndex;
                            }
                            //int i = lvGames.Items.IndexOf(g);
                            //lvGames.SelectedIndex = i;
                            //lvGames.InvalidateProperty(ListView.ItemsSourceProperty);
                            //lvGames.ItemsSource = Games;
                            _ctrlcount = 0;
                        }
                        break;
                    case Key.LeftAlt:
                        break;
                    case Key.Space:
                        break;
                    case Key.LeftShift:
                        break;
                    case Key.Z:
                        break;
                    case Key.X:
                        break;
                    case Key.Up:
                        break;
                    case Key.Down:
                        break;
                    case Key.A:
                    //videoSourcePlayer2.NewFrame -= Cam_NewFrame2;
                    //var tasks = new List<Task<bool>>();
                    //tasks.Add(Task<bool>.Factory.StartNew(GetVoiceTextReg));
                    //Task.WaitAll(tasks.ToArray());
                    //Thread.Sleep(5000);
                    //if (_voiceGameList == null)
                    //{
                    //    _utility.WriteToLogFile("The voiceToTtext button was pressed, but no games were in the _voiceGameList.");
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
                    //    _utility.WriteToLogFile("The voiceToTtext button was pressed, but no games were in the _voiceGameList.");
                    //    videoSourcePlayer2.NewFrame += Cam_NewFrame2;
                    //}
                    //break;
                    case Key.V:
                        //ShowDialog();
                        //lvGames.Visible = false;
                        //snaps.Visible = false;
                        //favoriteList.Visible = false;
                        //this.Enabled = false;

                        //videoSourcePlayer2.NewFrame -= Cam_NewFrame2;
                        //MenuForm menuForm = new MenuForm(this);
                        //menuForm.StartPosition = FormStartPosition.CenterScreen;
                        //menuForm.Visible = true;
                        break;
                }
                //HideMouse();
            }
            catch (Exception ex)
            {
                _utility.WriteToLogFile("Exception in lvGames_KeyDown: " + ex.ToString());
            }
        }        

        private void lvGames_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            _utility.WriteToLogFile("down pressed...");
            //This will bypass the actual KeyDown event.
            //e.Handled = false;
        }

        private void lvGames_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            _utility.WriteToLogFile("down keyUp...");


            _selectedIndex = lvGames.SelectedIndex;
            Game g = (Game)lvGames.SelectedItem;

        }
#endregion
#region Lights
        void ToggleMarqueeLights()
        {
            if (nusbioPixel == null) return;
            _utility.WriteToLogFile("In ToggleMarqueeLights()");
            //initial starting brightness is 128?
            nusbioPixel.SetBrightness(64 * 2);
            _utility.WriteToLogFile("_whiteStripPWMIntensity: " + _whiteStripPWMIntensity.ToString());
            if (_whiteStripPWMIntensity < 255)
            {
                var r = nusbioPixel.AnalogWrite(Mcu.GpioPwmPin.Gpio5, _whiteStripPWMIntensity);
            }
        }
        void _opacityTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _utility.WriteToLogFile("in _opacityTimer_Elapsed()");
            System.Timers.Timer t = (System.Timers.Timer)sender;
            if (this.Opacity < 1)
            {
                ChangeOpacity();
            }
            else
            {
                _utility.WriteToLogFile("Opacity Timer Disabled.");
                t.Enabled = false;
            }
        }
        private void ChangeOpacity()
        {
            _utility.WriteToLogFile("In ChangeOpacity()");
            this.Opacity += .00568;
            if (nusbioPixel == null) return;
            nusbioPixel.SetBrightness(64 * 2);            
            if (_whiteStripPWMIntensity >= 255) return;
            if (this.Opacity > .08 && this.Opacity < .5)
            {
                _utility.WriteToLogFile("opacity > .08 and < .5)");
                _whiteStripPWMIntensity++;
                Debug.WriteLine(_whiteStripPWMIntensity);
                var r = nusbioPixel.AnalogWrite(Mcu.GpioPwmPin.Gpio5, _whiteStripPWMIntensity);
            }
            else if (this.Opacity >= .5)
            {
                _utility.WriteToLogFile("opacity > .5)");
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
            _utility.WriteToLogFile("In FadeIn()");
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
                _utility.WriteToLogFile("Cabinet lights are currently on. Turning them off.");
                //Color doesn't matter here...set the intensity to zero
                nusbioPixel?.SetStrip(70, 191, 238, 0);
                ToggleMarqueeLights();
                _cabinetLights = CabinetLights.Off;
            }
            else
            {
                _utility.WriteToLogFile("Cabinet lights are currently off. Turning them on.");
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
                Utility _utility = new Utility(Path.Combine(ConfigurationManager.AppSettings["rootDirectory"], "log.txt"));
                _utility.WriteToLogFile("Nusbio Pixel not detected.");
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
                    _utility.WriteToLogFile($"Turn Arcade Machine Off at {DateTime.Now}. _lastMotionDetectedTime: {_lastMotionDetectedTime}");
                    _cabinetLights = CabinetLights.Off;
                _utility.WriteToLogFile("Cabinet lights were toggled off.");
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
        private void PlaySound()
        {
            //if (this.InvokeRequired)
            //{
            //    PlaySoundDelegate del = new PlaySoundDelegate(PlaySound);
            //    object[] parameters = { };
            //    this.Invoke(del);
            //}
            //else
            //{
            Thread.Sleep(2000);
            string audioFile = Path.Combine(_rootDirectory, "98883_1656228-lq.wav");
            if (File.Exists(audioFile))
            {
                System.Media.SoundPlayer player = new System.Media.SoundPlayer(audioFile);
                player.Play();
            }
            //}
        }
#endregion

        private void window_Closed(object sender, EventArgs e)
        {
            //_localWebCam.Stop();
            ToggleCabinetLights();
            Application.Current.Shutdown();
        }
    }
}
