using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Kinect;

namespace HENKA88KinectDemo
{
    class HumanDetector
    {
        double thRange = 3.0; // meter
        public string debugInfo;

        List<int> buf;
        int bufLen = 3; // バッファ長
        int _count = 0; // 検出人数
        bool lastStatus = false; // 前回の検出結果

        public HumanDetector()
        {
            buf = new List<int>();
        }

        public void SetBufferLength(int len)
        {
            this.bufLen = len;
            buf.Clear();
        }

        private bool Detect(Skeleton[] skeletons)
        {
            int cnt = 0;

            foreach (Skeleton skel in skeletons)
            {
                if (skel.TrackingState == SkeletonTrackingState.Tracked || skel.TrackingState == SkeletonTrackingState.PositionOnly)
                {
                    Joint joint = skel.Joints[JointType.Spine];
                    if (joint.Position.Z < thRange)
                    {
                        ++cnt;
                    }
                }
            }

            this._count = cnt;

            debugInfo = cnt.ToString();

            return cnt > 0 ? true : false;
        }

        public int Count()
        {
            return this._count;
        }

        public int DetectChange(Skeleton[] skeletons)
        {
            bool status;
            int statusChanged;

            buf.Add(Detect(skeletons) ? 1 : 0);
            if (buf.Count > bufLen)
            {
                buf.RemoveAt(0);
            }

            int cnt = 0;
            for (int i = 0; i < buf.Count; i++)
            {
                cnt += buf[i];
            }

            status = cnt > 0;

            if (lastStatus != status)
            {
                statusChanged = status ? 1 : -1;
            }
            else
            {
                statusChanged = 0;
            }

            lastStatus = status;

            return statusChanged;
        }

        public void Clear()
        {
            buf.Clear();
        }

        public Skeleton GetNearestSkeleton(Skeleton[] skeletons)
        {
            Skeleton skeleton = null;
            double depthMin = -1;

            foreach (Skeleton skel in skeletons)
            {
                if (skel.TrackingState == SkeletonTrackingState.Tracked)
                {
                    Joint joint = skel.Joints[JointType.Spine];
                    if (joint.Position.Z < thRange)
                    {
                        if (depthMin < 0 || joint.Position.Z < depthMin)
                        {
                            skeleton = skel;
                            depthMin = joint.Position.Z;
                        }
                    }
                }
            }
            return skeleton;
        }

    }
}
