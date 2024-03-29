﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media.Imaging;
using FFmpeg.AutoGen;

namespace EasyFFmpeg
{
    public unsafe class EasyFFmpegManager
    {
        private VideoInfo videoInfo = new VideoInfo();

        private readonly ConcurrentQueue<AVFrame> decodedFrameQueue = new ConcurrentQueue<AVFrame>();
        private AVHWDeviceType hwDeviceType;

        private AVFrame queueFrame;
        private VideoInputType videoInputType;

        private H264VideoStreamEncoder h264Encoder;
        private ManualResetEvent isDecodingEvent;
        private ManualResetEvent isEncodingEvent;

        private string url;

        private int frameNumber = 0;
        private bool isRecordComplete;        
        private bool isDecodingThreadRunning;
        private bool isEncodingThreadRunning;

        public delegate void VideoFrameReceivedHandler(BitmapImage bitmapImage);
        public event VideoFrameReceivedHandler VideoFrameReceived;
               
        public EasyFFmpegManager()
        {
            try
            {
                FFmpegBinariesHelper.RegisterFFmpegBinaries();
            }
            catch (NotSupportedException ex) { Debug.WriteLine(ex.Message); }
        }

        public void InitializeFFmpeg(string _url, VideoInputType _inputType)
        {
            url = _url;
            videoInputType = _inputType;
        }

        public void PlayVideo(string _url, VideoInputType _inputType)
        {
            url = _url;
            videoInputType = _inputType;

            isDecodingEvent = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(new WaitCallback(DecodeAllFramesToImages));
            isDecodingEvent.Set();

            isDecodingThreadRunning = true;
        }

        public void StopVideo()
        {
            if (isDecodingThreadRunning)
            {
                isDecodingThreadRunning = false;

                isDecodingEvent.Reset();
                isDecodingEvent.Dispose();
            }
        }

        public void RecordVideo(string fileName)
        {
            isEncodingEvent = new ManualResetEvent(false);
            h264Encoder = new H264VideoStreamEncoder();

            //initialize output format&codec
            h264Encoder.OpenOutputURL(fileName, videoInfo);

            ThreadPool.QueueUserWorkItem(new WaitCallback(EncodeImagesToH264));

            isEncodingEvent.Set();

            isEncodingThreadRunning = true;
            isRecordComplete = false;
        }

        public int StopRecord()
        {
            try
            {
                isEncodingThreadRunning = false;
                isEncodingEvent.Reset();

                h264Encoder.FlushEncode();
                h264Encoder.Dispose();

                frameNumber = 0;

                isRecordComplete = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return -1;
            }

            return 0;
        }

        private void ConfigureHWDecoder(bool useHwAcc, out AVHWDeviceType HWtype)
        {
            HWtype = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;

            if (useHwAcc)
            {
                var availableHWDecoders = new Dictionary<int, AVHWDeviceType>();

                Console.WriteLine("Select hardware decoder:");
                var type = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
                var number = 0;

                while ((type = ffmpeg.av_hwdevice_iterate_types(type)) != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                {
                    Console.WriteLine($"{++number}. {type}");
                    availableHWDecoders.Add(number, type);
                }
                if (availableHWDecoders.Count == 0)
                {
                    Console.WriteLine("Your system have no hardware decoders.");
                    HWtype = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
                    return;
                }

                int decoderNumber = availableHWDecoders.SingleOrDefault(t => t.Value == AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2).Key;

                if (decoderNumber == 0)
                {
                    decoderNumber = availableHWDecoders.First().Key;
                }

                Console.WriteLine($"Selected [{decoderNumber}]");

                int.TryParse(Console.ReadLine(), out var inputDecoderNumber);

                availableHWDecoders.TryGetValue(inputDecoderNumber == 0 ? decoderNumber : inputDecoderNumber, out HWtype);
            }
        }

        private void DecodeAllFramesToImages(object state)
        {
            try
            {
                ConfigureHWDecoder(false, out hwDeviceType);

                using (var decoder = new VideoStreamDecoder(url, videoInputType))
                {
                    videoInfo = decoder.GetVideoInfo();

                    var info = decoder.GetContextInfo();
                    info.ToList().ForEach(x => Console.WriteLine($"{x.Key} = {x.Value}"));

                    var sourceSize = decoder.FrameSize;
                    var sourcePixelFormat = hwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE ? decoder.PixelFormat : GetHWPixelFormat(hwDeviceType);
                    var destinationSize = sourceSize;
                    var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;

                    using (var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat))
                    {
                        while (decoder.TryDecodeNextFrame(out var frame) && isDecodingEvent.WaitOne())
                        {
                            var convertedFrame = vfc.Convert(frame);

                            Bitmap bitmap = new Bitmap(convertedFrame.width, convertedFrame.height, convertedFrame.linesize[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb, (IntPtr)convertedFrame.data[0]);

                            if (isEncodingThreadRunning)
                            {
                                decodedFrameQueue.Enqueue(convertedFrame);
                            }

                            BitmapToImageSource(bitmap);
                        }
                    }
                }
            }
            catch (ApplicationException e)
            {
                Debug.WriteLine(e.Message);
            }
            catch (ObjectDisposedException e)
            {
                Debug.WriteLine(e.Message);
            }
            catch (AccessViolationException e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private void BitmapToImageSource(Bitmap bitmap)
        {
            if (isDecodingThreadRunning)
            {
                using (var memory = new MemoryStream())
                {
                    try
                    {
                        bitmap.Save(memory, ImageFormat.Bmp);
                        memory.Position = 0;
                        BitmapImage bitmapimage = new BitmapImage();
                        bitmapimage.BeginInit();
                        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapimage.StreamSource = memory;
                        bitmapimage.EndInit();
                        bitmapimage.Freeze();

                        VideoFrameReceived?.Invoke(bitmapimage);

                        memory.Dispose();
                    }
                    catch(NullReferenceException ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }
                }
            }
        }

        private unsafe void EncodeImagesToH264(object state)
        {
            try
            {
                while (isEncodingEvent.WaitOne())
                {
                    if (decodedFrameQueue.TryDequeue(out queueFrame))
                    {
                        var sourcePixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
                        var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P; //for h.264

                        using (var vfc = new VideoFrameConverter(videoInfo.SourceFrameSize, sourcePixelFormat, videoInfo.DestinationFrameSize, destinationPixelFormat))
                        {
                            var convertedFrame = vfc.Convert(queueFrame);
                            convertedFrame.pts = frameNumber * 2;       //to do
                            h264Encoder.TryEncodeNextPacket(convertedFrame);
                        }

                        frameNumber++;
                    }
                }
            }
            catch (ObjectDisposedException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (AccessViolationException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private AVPixelFormat GetHWPixelFormat(AVHWDeviceType hWDevice)
        {
            switch (hWDevice)
            {
                case AVHWDeviceType.AV_HWDEVICE_TYPE_NONE:
                    return AVPixelFormat.AV_PIX_FMT_NONE;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU:
                    return AVPixelFormat.AV_PIX_FMT_VDPAU;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA:
                    return AVPixelFormat.AV_PIX_FMT_CUDA;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI:
                    return AVPixelFormat.AV_PIX_FMT_VAAPI;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2:
                    return AVPixelFormat.AV_PIX_FMT_NV12;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_QSV:
                    return AVPixelFormat.AV_PIX_FMT_QSV;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX:
                    return AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA:
                    return AVPixelFormat.AV_PIX_FMT_NV12;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_DRM:
                    return AVPixelFormat.AV_PIX_FMT_DRM_PRIME;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL:
                    return AVPixelFormat.AV_PIX_FMT_OPENCL;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC:
                    return AVPixelFormat.AV_PIX_FMT_MEDIACODEC;
                default:
                    return AVPixelFormat.AV_PIX_FMT_NONE;
            }
        }

        public void DisposeFFmpeg()
        {
            if (isDecodingThreadRunning)
            {
                isDecodingThreadRunning = false;

                isDecodingEvent.Reset();
                isDecodingEvent.Dispose();
            }

            if (isEncodingThreadRunning)
            {
                isEncodingThreadRunning = false;

                isEncodingEvent.Reset();
                isEncodingEvent.Dispose();

                if (!isRecordComplete)
                {
                    h264Encoder.FlushEncode();
                    h264Encoder.Dispose();
                }
            }
        }
    }
}
