using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace EasyFFmpeg
{
    public enum VideoInputType
    {
        RTP_RTSP = 0,
        CAM_DEVICE
    }

    public unsafe class VideoStreamDecoder : IDisposable
    {
        private readonly AVCodecContext* iCodecContext;
        private readonly AVFormatContext* iFormatContext;

        private readonly AVFrame* decodedFrame;
        private readonly AVFrame* receivedFrame;
        private readonly AVPacket* rawPacket;

        private readonly int dec_stream_index;
        

        public string CodecName { get; }
        public Size FrameSize { get; }
        public AVPixelFormat PixelFormat { get; }


        public VideoStreamDecoder(string url, VideoInputType inputType, AVHWDeviceType HWDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            ffmpeg.avdevice_register_all();

            iFormatContext = ffmpeg.avformat_alloc_context();
            receivedFrame = ffmpeg.av_frame_alloc();

            var _iFormatContext = iFormatContext;

            AVDictionary* avDict;
            ffmpeg.av_dict_set(&avDict, "reorder_queue_size", "1", 0);

            switch (inputType)
            {
                case VideoInputType.CAM_DEVICE:
                    AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
                    ffmpeg.avformat_open_input(&_iFormatContext, url, iformat, null).ThrowExceptionIfError();
                    break;
                case VideoInputType.RTP_RTSP:
                    ffmpeg.avformat_open_input(&_iFormatContext, url, null, &avDict).ThrowExceptionIfError();
                    break;
                default:
                    break;
            }

            ffmpeg.avformat_find_stream_info(iFormatContext, null).ThrowExceptionIfError();

            AVCodec* codec;

            dec_stream_index = ffmpeg.av_find_best_stream(iFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0).ThrowExceptionIfError();


            iCodecContext = ffmpeg.avcodec_alloc_context3(codec);

            if (HWDeviceType != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            {
                ffmpeg.av_hwdevice_ctx_create(&iCodecContext->hw_device_ctx, HWDeviceType, null, null, 0).ThrowExceptionIfError();
            }

            ffmpeg.avcodec_parameters_to_context(iCodecContext, iFormatContext->streams[dec_stream_index]->codecpar).ThrowExceptionIfError();
            ffmpeg.avcodec_open2(iCodecContext, codec, null).ThrowExceptionIfError();

            CodecName = ffmpeg.avcodec_get_name(codec->id);
            FrameSize = new Size(iCodecContext->width, iCodecContext->height);
            PixelFormat = iCodecContext->pix_fmt;

            rawPacket = ffmpeg.av_packet_alloc();
            decodedFrame = ffmpeg.av_frame_alloc();
        }

        public bool TryDecodeNextFrame(out AVFrame frame)
        {
            ffmpeg.av_frame_unref(decodedFrame);
            ffmpeg.av_frame_unref(receivedFrame);
            int error;

            do
            {
                try
                {
                    do
                    {
                        error = ffmpeg.av_read_frame(iFormatContext, rawPacket);
                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            frame = *decodedFrame;
                            return false;
                        }

                        error.ThrowExceptionIfError();
                    } while (rawPacket->stream_index != dec_stream_index);

                    ffmpeg.av_packet_rescale_ts(rawPacket, iFormatContext->streams[dec_stream_index]->time_base, iCodecContext->time_base);

                    /* Send the video frame stored in the temporary packet to the decoder.
                     * The input video stream decoder is used to do this. */
                    ffmpeg.avcodec_send_packet(iCodecContext, rawPacket).ThrowExceptionIfError();
                }
                finally
                {
                    ffmpeg.av_packet_unref(rawPacket);
                }

                //read decoded frame from input codec context
                error = ffmpeg.avcodec_receive_frame(iCodecContext, decodedFrame);

            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            error.ThrowExceptionIfError();

            if (iCodecContext->hw_device_ctx != null)
            {
                ffmpeg.av_hwframe_transfer_data(receivedFrame, decodedFrame, 0).ThrowExceptionIfError();
                frame = *receivedFrame;
            }
            else
            {
                frame = *decodedFrame;
            }

            return true;
        }

        public IReadOnlyDictionary<string, string> GetContextInfo()
        {
            AVDictionaryEntry* tag = null;
            var result = new Dictionary<string, string>();
            while ((tag = ffmpeg.av_dict_get(iFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
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

            videoInfo.SourceFrameSize = new Size(iCodecContext->width, iCodecContext->height);
            videoInfo.DestinationFrameSize = videoInfo.SourceFrameSize;
            videoInfo.SourcePixelFormat = iCodecContext->pix_fmt;
            videoInfo.DestinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
            videoInfo.Sample_aspect_ratio = iCodecContext->sample_aspect_ratio;
            videoInfo.Timebase = iCodecContext->time_base;
            videoInfo.Framerate = iCodecContext->framerate;

            return videoInfo;
        }


        #region Dispose

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ffmpeg.av_frame_unref(decodedFrame);
                    ffmpeg.av_free(decodedFrame);

                    ffmpeg.av_packet_unref(rawPacket);
                    ffmpeg.av_free(rawPacket);

                    ffmpeg.avcodec_close(iCodecContext);

                    var _iFormatContext = iFormatContext;
                    ffmpeg.avformat_close_input(&_iFormatContext);
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // 이 코드를 변경하지 마세요. 'Dispose(bool disposing)' 메서드에 정리 코드를 입력합니다.
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
