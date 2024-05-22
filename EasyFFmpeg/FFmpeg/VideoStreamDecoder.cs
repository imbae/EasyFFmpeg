using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace EasyFFmpeg
{
    public enum VideoInputType
    {
        DEFAULT = 0,
        RTP_RTSP,
        CAM_DEVICE
    }

    public unsafe class VideoStreamDecoder : IDisposable
    {
        private readonly AVCodecContext* pCodecContext;
        private readonly AVFormatContext* pFormatContext;

        private readonly AVPacket* pPacket;
        private readonly AVFrame* pFrame;
        private readonly AVFrame* receivedFrame;

        private readonly int streamIndex;
        
        public Size FrameSize { get; }
        public AVPixelFormat PixelFormat { get; }

        public VideoStreamDecoder(string url, VideoInputType inputType, AVHWDeviceType HWDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            try
            {
                ffmpeg.avdevice_register_all();

                pFormatContext = ffmpeg.avformat_alloc_context();
                receivedFrame = ffmpeg.av_frame_alloc();

                var pFormat = pFormatContext;

                AVDictionary* avDict;
                ffmpeg.av_dict_set(&avDict, "reorder_queue_size", "1", 0);

                switch (inputType)
                {
                    case VideoInputType.CAM_DEVICE:
                        AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
                        ffmpeg.avformat_open_input(&pFormat, url, iformat, null).ThrowExceptionIfError();
                        break;
                    case VideoInputType.RTP_RTSP:
                        ffmpeg.avformat_open_input(&pFormat, url, null, &avDict).ThrowExceptionIfError();
                        break;
                    default:
                        ffmpeg.avformat_open_input(&pFormat, url, null, null).ThrowExceptionIfError();
                        break;
                }

                ffmpeg.avformat_find_stream_info(pFormatContext, null).ThrowExceptionIfError();

                AVCodec* codec = null;

                streamIndex = ffmpeg.av_find_best_stream(pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0).ThrowExceptionIfError();
                pCodecContext = ffmpeg.avcodec_alloc_context3(codec);

                if (HWDeviceType != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                {
                    ffmpeg.av_hwdevice_ctx_create(&pCodecContext->hw_device_ctx, HWDeviceType, null, null, 0).ThrowExceptionIfError();
                }

                ffmpeg.avcodec_parameters_to_context(pCodecContext, pFormatContext->streams[streamIndex]->codecpar).ThrowExceptionIfError();
                ffmpeg.avcodec_open2(pCodecContext, codec, null).ThrowExceptionIfError();

                FrameSize = new Size(pCodecContext->width, pCodecContext->height);
                PixelFormat = pCodecContext->pix_fmt;

                pPacket = ffmpeg.av_packet_alloc();
                pFrame = ffmpeg.av_frame_alloc();
            }
            catch(AccessViolationException ex)
            {
                throw new AccessViolationException("Access Violation Exception", ex);
            }
        }

        public bool TryDecodeNextFrame(out AVFrame frame)
        {
            ffmpeg.av_frame_unref(pFrame);
            ffmpeg.av_frame_unref(receivedFrame);
            int error;

            do
            {
                try
                {
                    do
                    {
                        ffmpeg.av_packet_unref(pPacket);
                        error = ffmpeg.av_read_frame(pFormatContext, pPacket);

                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            frame = *pFrame;
                            return false;
                        }

                        error.ThrowExceptionIfError();

                    } while (pPacket->stream_index != streamIndex);

                    AVRational time_base = pFormatContext->streams[streamIndex]->time_base;

                    if (time_base.num == 0 || time_base.den == 0)
                    {
                        time_base.num = 1;
                        time_base.den = 25;
                    }

                    var delay = TimeSpan.FromSeconds((double)pPacket->duration * time_base.num / time_base.den);
                    Thread.Sleep(delay);

                    /* Send the video frame stored in the temporary packet to the decoder.
                     * The input video stream decoder is used to do this. */
                    ffmpeg.avcodec_send_packet(pCodecContext, pPacket).ThrowExceptionIfError();

                }
                finally
                {
                    ffmpeg.av_packet_unref(pPacket);
                }

                //read decoded frame from input codec context
                error = ffmpeg.avcodec_receive_frame(pCodecContext, pFrame);

            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            error.ThrowExceptionIfError();

            if (pCodecContext->hw_device_ctx != null)
            {
                ffmpeg.av_hwframe_transfer_data(receivedFrame, pFrame, 0).ThrowExceptionIfError();
                frame = *receivedFrame;
            }
            else
            {
                frame = *pFrame;
            }

            return true;
        }

        public IReadOnlyDictionary<string, string> GetContextInfo()
        {
            AVDictionaryEntry* tag = null;
            var result = new Dictionary<string, string>();

            while ((tag = ffmpeg.av_dict_get(pFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
            {
                var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
                var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
                result.Add(key, value);
            }

            return result;
        }

        public VideoInfo GetVideoInfo()
        {
            VideoInfo videoInfo = new VideoInfo();

            videoInfo.FrameSize = new Size(pCodecContext->width, pCodecContext->height);
            videoInfo.GopSize = pCodecContext->gop_size;
            videoInfo.BitRate = pCodecContext->bit_rate;
            videoInfo.MaxBFrames = pCodecContext->max_b_frames;
            videoInfo.SampleAspectRatio = pCodecContext->sample_aspect_ratio;
            videoInfo.FrameRate = pFormatContext->streams[streamIndex]->avg_frame_rate;
            videoInfo.Timebase = pFormatContext->streams[streamIndex]->time_base;

            return videoInfo;
        }


        #region Dispose

        public void Dispose()
        {
            var frame = pFrame;
            ffmpeg.av_frame_free(&frame);

            var packet = pPacket;
            ffmpeg.av_packet_free(&packet);

            ffmpeg.avcodec_close(pCodecContext);

            var pFormat = pFormatContext;
            ffmpeg.avformat_close_input(&pFormat);
        }

        #endregion
    }
}
