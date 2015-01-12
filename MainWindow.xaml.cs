//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace HENKA88KinectDemo{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;

    using System.Windows.Data;
    using System.Windows.Input;
    using Microsoft.Win32;
    using System.Windows.Controls;

    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap bitmapDepth;
        private WriteableBitmap bitmapColor;

        /// <summary>
        /// Intermediate storage for the depth data received from the camera
        /// </summary>
        private DepthImagePixel[] depthPixels;

        /// <summary>
        /// Intermediate storage for the depth data converted to color
        /// </summary>
        private byte[] dataPixelsDepth;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] dataPixelsColor;

        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        private HumanDetector humanDetector = new HumanDetector();
        private VideoPlayer videoPlayer;
        private bool onDemo = false; // デモモードon
        private bool fullScreen = false;
        private bool showDebugInfo = true;
        private int playDelay = 0; // 再生開始までのディレイ(sec)
        private int stopDelay = 2; // 終了時のディレイ(sec)
        private int fadeLength = 3; // フェードアウトの時間 (sec)
        private DispatcherTimer videoCheckTimer = new DispatcherTimer();
        private DateTime? noChangeBeginAt = null;
        private int bufLen = 3; // 人検出モジュールバッファ長

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            videoPlayer = new VideoPlayer();

            this.KeyUp += MainWindow_KeyUp;
            this.Loaded += (s, e) =>
            {
                videoPlayer.Setup(@"89.mp4");
            };

            // コールバック関数をセット
            videoPlayer.callbackOnPlay = new VideoPlayerPlayCallback(OnVideoPlay);
            videoPlayer.callbackOnStop = new VideoPlayerStopCallback(OnVideoStopped);
            videoPlayer.callbackOnMediaOpen = new VideoPlayerMediaOpenCallback(OnMediaOpened);
            videoPlayer.callbackOnMediaEnd = new VideoPlayerMediaEndCallback(OnMediaEnded);

            // プレイヤーの実体のセット
            videoDrawing.Player = videoPlayer.GetPlayer();

            videoArea.LayoutUpdated += new EventHandler(videoArea_LayoutUpdated);

            videoCheckTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            videoCheckTimer.Tick += new EventHandler(videoCheckTimer_Tick);
            this.infoText.Text = "Press 's' to start demo";
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
//            ImageSkeleton.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

                // Turn on the color stream to receive color frames
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.dataPixelsDepth = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];
                this.dataPixelsColor = new byte[this.sensor.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.bitmapDepth = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.bitmapColor = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
//                this.ImageDepth.Source = this.bitmapDepth;
//                this.ImageColor.Source = this.bitmapColor;

                // Add an event handler to be called whenever there is new depth frame data
//                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;

                // Add an event handler to be called whenever there is new color frame data
//                this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
//                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        void MainWindow_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F: // フルスクリーン
                    if (fullScreen == false)
                    {
                        this.WindowStyle = System.Windows.WindowStyle.None;
                        this.WindowState = System.Windows.WindowState.Maximized;
                        this.Topmost = true;
                    }
                    else
                    {
                        this.WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
                        this.WindowState = System.Windows.WindowState.Normal;
                        this.Topmost = false;
                    }
                    fullScreen = fullScreen ? false : true;
                    break;
                case Key.A: // ファイルを交換する (画像)
                    OpenImageFile();
                    break;
                case Key.B: // ファイルを交換する（ビデオ)
                    OpenVideoFile();
                    break;
                case Key.S: // デモモードスタート/ストップ
                    onDemo = onDemo ? false : true;
                    if (onDemo)
                    {
                        this.videoPlayer.Stop(); // 即時再生終了
                        this.image.Visibility = Visibility.Visible;
                        this.videoArea.Visibility = Visibility.Collapsed;

                        // デバッグメッセージ非表示
                        showDebugInfo = false;
                        this.infoText.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // デバッグメッセージ表示
                        showDebugInfo = true;
                        this.infoText.Visibility = Visibility.Visible;

                        this.infoText.Text = "Demo Off";
                    }
                    break;
                case Key.Escape:
                    this.Close();
                    break;
                case Key.R: // スケルトン検出リセット
                    humanDetector.Clear();
                    this.sensor.SkeletonStream.Disable();
                    this.sensor.SkeletonStream.Enable();
                    break;
                case Key.D: // デバッグ表示トグル切り替え
                    showDebugInfo = showDebugInfo ? false : true;
                    if (showDebugInfo)
                    {
                        this.infoText.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        this.infoText.Visibility = Visibility.Collapsed;
                    }
                    break;
                case Key.D0:
                    playDelay = 0;
                    break;
                case Key.D1:
                    playDelay = 1;
                    break;
                case Key.D2:
                    playDelay = 2;
                    break;
                case Key.D3:
                    playDelay = 3;
                    break;
                case Key.D4:
                    playDelay = 4;
                    break;
                case Key.D5:
                    playDelay = 5;
                    break;
                case Key.L: // ログ開始
                    Log.Start();
                    MessageBox.Show("ログ開始");
                    break;
                case Key.E: // ログ書き込み
                    Log.End();
                    MessageBox.Show("ログ終了");
                    break;
                case Key.Up: // バッファ長増加
                    if (onDemo == false)
                    {
                        ++bufLen;
                        humanDetector.SetBufferLength(bufLen);

                        this.infoText.Text = string.Format("bufLen={0}", bufLen);
                    }
                    break;
                case Key.Down: // バッファ長減らす
                    if (onDemo == false)
                    {
                        if (bufLen > 1)
                        {
                            --bufLen;
                            humanDetector.SetBufferLength(bufLen);

                            this.infoText.Text = string.Format("bufLen={0}", bufLen);
                        }
                    }
                    break;
            }
        }

        private void OpenImageFile()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            // dlg.InitialDirectory = @"c:\"; // ← 適当に変更すること

            if (dlg.ShowDialog(this) == true)
            {
                BitmapImage bitmap = new BitmapImage(new Uri(dlg.FileName, UriKind.Absolute));
                image.Source = bitmap;
            }

            onDemo = false;
            this.image.Visibility = Visibility.Visible;
            this.videoArea.Visibility = Visibility.Collapsed;
        }

        private void OpenVideoFile()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            // dlg.InitialDirectory = @"c:\"; // ← 適当に変更すること

            if (dlg.ShowDialog(this) == true)
            {
                videoPlayer.Setup(dlg.FileName);
            }

            videoPlayer.Start();
            onDemo = false;
            this.image.Visibility = Visibility.Collapsed;
            this.videoArea.Visibility = Visibility.Visible;
        }

        // 描画領域をセット
        void videoArea_LayoutUpdated(object sender, EventArgs e)
        {
            videoDrawing.Rect = new Rect(0, 0, videoArea.ActualWidth, videoArea.ActualHeight);
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    // Get the min and max reliable depth for the current frame
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = depthFrame.MaxDepth;

                    // Convert the depth to RGB
                    int colorPixelIndex = 0;
                    for (int i = 0; i < this.depthPixels.Length; ++i)
                    {
                        // Get the depth for this pixel
                        short depth = depthPixels[i].Depth;

                        // To convert to a byte, we're discarding the most-significant
                        // rather than least-significant bits.
                        // We're preserving detail, although the intensity will "wrap."
                        // Values outside the reliable depth range are mapped to 0 (black).

                        // Note: Using conditionals in this loop could degrade performance.
                        // Consider using a lookup table instead when writing production code.
                        // See the KinectDepthViewer class used by the KinectExplorer sample
                        // for a lookup table example.

                        byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

                        // Write out blue byte
                        this.dataPixelsDepth[colorPixelIndex++] = intensity;

                        // Write out green byte
                        this.dataPixelsDepth[colorPixelIndex++] = intensity;

                        // Write out red byte                        
                        this.dataPixelsDepth[colorPixelIndex++] = intensity;

                        // We're outputting BGR, the last byte in the 32 bits is unused so skip it
                        // If we were outputting BGRA, we would write alpha here.
                        ++colorPixelIndex;
                    }

                    // Write the pixel data into our bitmap
                    this.bitmapDepth.WritePixels(
                        new Int32Rect(0, 0, this.bitmapDepth.PixelWidth, this.bitmapDepth.PixelHeight),
                        this.dataPixelsDepth,
                        this.bitmapDepth.PixelWidth * sizeof(int),
                        0);
                }
            }
        }


        /// <summary>
        /// Event handler for Kinect sensor's ColorFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.dataPixelsColor);

                    // Write the pixel data into our bitmap
                    this.bitmapColor.WritePixels(
                        new Int32Rect(0, 0, this.bitmapColor.PixelWidth, this.bitmapColor.PixelHeight),
                        this.dataPixelsColor,
                        this.bitmapColor.PixelWidth * sizeof(int),
                        0);
                }
            }
        }
        
        /// <summary>
        /// Handles the checking or unchecking of the near mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxNearModeChanged(object sender, RoutedEventArgs e)
        {
            /*
            if (this.sensor != null)
            {
                // will not function on non-Kinect for Windows devices
                try
                {
                    if (this.checkBoxNearMode.IsChecked.GetValueOrDefault())
                    {
                        this.sensor.DepthStream.Range = DepthRange.Near;
                    }
                    else
                    {
                        this.sensor.DepthStream.Range = DepthRange.Default;
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }
             */
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            /*
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
             */
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            if (onDemo == false) // デモモードではないとき
            {
                return;
            }

            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
//                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                int statusChange = humanDetector.DetectChange(skeletons);

                if (statusChange == 1)
                {
                    Log.WriteLine("status: 0->1");

                    if (videoPlayer.isPlaying == false) 
                    {
                        Log.WriteLine("player starting..");

                        /*
                        // 静止画像は非表示
                        image.Visibility = Visibility.Collapsed;

                        //ビデオは再生開始で表示
                        videoPlayer.Start();
                        this.mediaElement.Visibility = Visibility.Visible;
                        */

                        videoArea.Opacity = 1.0; // alphaをリセットしておく
                        videoPlayer.Start(playDelay);
                    }
                }
                else if (statusChange == -1)
                {
                    Log.WriteLine("status: 1->0");

                    if (videoPlayer.isPlaying == true && videoPlayer.isStopping == false) // 再生中かつ再生終了中でなかったら
                    {
                        Log.WriteLine("player stopping..");

                        /*
                        // ビデオを停止する
                        videoPlayer.Stop();
                        this.mediaElement.Visibility = Visibility.Collapsed;

                        // 静止画を表示
                        this.image.Visibility = Visibility.Visible;
                        */

                        videoArea.Opacity = 1.0; // alphaをリセットしておく
                        videoPlayer.Stop(stopDelay);
                    }
                }
                else
                {
                    // 人がいなくなって再生停止処理中に人がフレームインした場合の対策 (人がいるのに静止画のままになってしまう対策)
                    if (humanDetector.Count() > 0 && videoPlayer.isPlaying == false)
                    {
                        if (noChangeBeginAt == null)
                        {
                            noChangeBeginAt = DateTime.Now;
                        }
                        else
                        {
                            double elapsed = (DateTime.Now - (DateTime)noChangeBeginAt).TotalMilliseconds;

                            if (elapsed > playDelay * 1000 + 1500)
                            {
                                Log.WriteLine("status: 1->1, but video not played");

                                videoArea.Opacity = 1.0; // alphaをリセットしておく
                                videoPlayer.Start(playDelay);
                            }
                        }
                    }
                    else
                    {
                        noChangeBeginAt = null;
                    }
                }

                this.infoText.Text = humanDetector.debugInfo;

                /*
                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }
                */

                // prevent drawing outside of our render area
//                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        private void OnVideoPlay()
        {
            Log.WriteLine("on video played");

            /*
            this.videoArea.Visibility = Visibility.Visible;
            this.image.Visibility = Visibility.Collapsed;
            */
            videoCheckTimer.Start();
        }

        private void OnVideoStopped()
        {
            Log.WriteLine("on video stopped");

            this.videoArea.Visibility = Visibility.Collapsed;
            this.image.Visibility = Visibility.Visible;
        }

        private void OnMediaOpened()
        {
            Log.WriteLine("media opened");
        }

        private void OnMediaEnded()
        {
            Log.WriteLine("media ended");

            // フェードアウト
//            Log.WriteLine("fadeout starting..");
//            videoPlayer.FadeOut(this.videoArea, fadeLength);
        }

        void videoCheckTimer_Tick(object sender, EventArgs e)
        {
            if (this.videoPlayer.GetPlayer().Position.TotalMilliseconds > 100) // Blankが出ないようにするための調整値
            {
                videoCheckTimer.Stop();

                this.videoArea.Visibility = Visibility.Visible;
//                this.image.Visibility = Visibility.Collapsed;
                Dispatcher.Invoke(new Action(delegate() { this.image.Visibility = Visibility.Collapsed; }));
            }
        }

    }
}