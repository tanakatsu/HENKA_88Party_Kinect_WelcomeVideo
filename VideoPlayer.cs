using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows;

namespace HENKA88KinectDemo
{
    delegate void VideoPlayerMediaOpenCallback();
    delegate void VideoPlayerMediaEndCallback(); 
    delegate void VideoPlayerPlayCallback(); 
    delegate void VideoPlayerStopCallback(); 

    class VideoPlayer
    {
        MediaPlayer player;
        public bool isPlaying; // 再生開始～再生終了完了
        public bool isStopping; // 再生終了中
        DispatcherTimer playTimer = new DispatcherTimer();
        DispatcherTimer stopTimer = new DispatcherTimer();
        DispatcherTimer fadeTimer = new DispatcherTimer();
        int stopTimerInterval = 50; // msec
        double volumeDecrement;

        int fadeTimerInterval = 50; // msec
        double fadeDecrement;
        UIElement fadeElement = null;

        internal VideoPlayerMediaOpenCallback callbackOnMediaOpen = null;
        internal VideoPlayerMediaEndCallback callbackOnMediaEnd = null;
        internal VideoPlayerPlayCallback callbackOnPlay = null;
        internal VideoPlayerStopCallback callbackOnStop = null;


        public VideoPlayer()
        {
            this.player = new MediaPlayer();
            this.player.MediaOpened += new EventHandler(player_MediaOpened);
            this.player.MediaEnded += new EventHandler(player_MediaEnded);
            isPlaying = false;
            isStopping = false;

            playTimer.Tick += new EventHandler(playTimer_Tick);

            stopTimer.Interval = new TimeSpan(0, 0, 0, 0, stopTimerInterval);
            stopTimer.Tick += new EventHandler(stopTimer_Tick);

            fadeTimer.Interval = new TimeSpan(0, 0, 0, 0, fadeTimerInterval);
            fadeTimer.Tick += new EventHandler(fadeTimer_Tick);
        }

        public MediaPlayer GetPlayer()
        {
            return player;
        }

        public void Setup(string filePath)
        {
            this.player.Open(new Uri(filePath, UriKind.RelativeOrAbsolute));
        }

        public void Start()
        {
            Start(0);
        }

        public void Start(int delay)
        {
            if (this.player.Source != null)
            {
                Log.WriteLine("Start");

                player.SpeedRatio = 1.0;
                player.Volume = 1.0;
                player.Position = new TimeSpan(0, 0, 0, 0, 0);
             
                isPlaying = true;
                isStopping = false;
//                player.Play();

                playTimer.Interval = new TimeSpan(0, 0, 0, delay);
                playTimer.Start();

                // もしタイマーが動いていたら停止させる
                stopTimer.Stop();
                fadeTimer.Stop();
            }
        }

        public void Stop()
        {
            Log.WriteLine("Stop");

            Log.WriteLine("player.Stop()");
            player.Stop();

            isPlaying = false;
            isStopping = false;

            stopTimer.Stop();

            // もしタイマーが動いていたら停止させる
            playTimer.Stop();
            fadeTimer.Stop();
        }

        public void Stop(int len)
        {
            Log.WriteLine(string.Format("Stop({0})", len));

            isStopping = true; // 終了中フラグセット
            volumeDecrement = (double)1.0 / (len * (1000 / stopTimerInterval));
            stopTimer.Start();

            // もしタイマーが動いていたら停止させる
            playTimer.Stop();
            fadeTimer.Stop();
        }

        void playTimer_Tick(object sender, EventArgs e)
        {
            if (isPlaying) // すでに再生停止されていた場合は再生しない
            {
                Log.WriteLine("player.Play()");

                player.Play();

                if (callbackOnPlay != null)
                {
                    callbackOnPlay();
                }
            }
            playTimer.Stop();
        }

        void stopTimer_Tick(object sender, EventArgs e)
        {
            player.Volume -= volumeDecrement;

            if (player.Volume < 0)
            {
                player.Volume = 0;
            }

            Log.WriteLine(string.Format("volume={0}", player.Volume));

            if (player.Volume == 0)
            {
                Stop();

                // コールバック呼ぶ
                if (callbackOnStop != null)
                {
                    callbackOnStop();
                }
            }
        }

        void player_MediaOpened(object sender, EventArgs e)
        {
            Log.WriteLine("MediaOpened");

            if (callbackOnMediaOpen != null)
            {
                callbackOnMediaOpen();
            }
        }

        void player_MediaEnded(object sender, EventArgs e)
        {
            Log.WriteLine("MediaEnded");

            if (callbackOnMediaEnd != null)
            {
                callbackOnMediaEnd();
            }
        }

        public void FadeOut(UIElement element, int duration)
        {
            fadeElement = element;
            fadeDecrement = (double)1.0 / (duration * (1000 / fadeTimerInterval));
            fadeTimer.Start();
        }

        void fadeTimer_Tick(object sender, EventArgs e)
        {
            if (fadeElement != null)
            {
                fadeElement.Opacity -= fadeDecrement;

                if (fadeElement.Opacity < 0)
                {
                    fadeElement.Opacity = 0;
                }

                if (fadeElement.Opacity == 0)
                {
                    fadeTimer.Stop();
                }
            }
        }
    }
}
