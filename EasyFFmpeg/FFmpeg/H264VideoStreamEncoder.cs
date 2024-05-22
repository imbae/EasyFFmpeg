using FFmpeg.AutoGen;
using System;

namespace EasyFFmpeg
{
    public unsafe class H264VideoStreamEncoder : IDisposable
    {
        private AVFormatContext* oFormatContext;
        private AVCodecContext* oCodecContext;
        private AVCodec* oCodec;

        public void OpenOutputURL(string fileName, VideoInfo videoInfo)
        {
            AVStream* out_stream;

            //output file
            var _oFormatContext = oFormatContext;

            ffmpeg.avformat_alloc_output_context2(&_oFormatContext, null, null, fileName);

            oCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);

            out_stream = ffmpeg.avformat_new_stream(_oFormatContext, oCodec);

            oCodecContext = ffmpeg.avcodec_alloc_context3(oCodec);

            oCodecContext->height = videoInfo.FrameSize.Height;
            oCodecContext->width = videoInfo.FrameSize.Width;
            oCodecContext->gop_size = videoInfo.GopSize;
            oCodecContext->max_b_frames = videoInfo.MaxBFrames;
            oCodecContext->bit_rate = videoInfo.BitRate;
            oCodecContext->sample_aspect_ratio = videoInfo.SampleAspectRatio;
            oCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;      //for h.264
            oCodecContext->time_base = videoInfo.Timebase;
            oCodecContext->framerate = videoInfo.FrameRate;

            AVDictionary* codecOptions = null;
            ffmpeg.av_dict_set(&codecOptions, "profile", "high", 0);
            ffmpeg.av_dict_set(&codecOptions, "level", "4.0", 0);

            //open codecd
            ffmpeg.avcodec_open2(oCodecContext, oCodec, &codecOptions).ThrowExceptionIfError();

            ffmpeg.avcodec_parameters_from_context(out_stream->codecpar, oCodecContext);
            out_stream->time_base = oCodecContext->time_base;

            //Show some Information
            ffmpeg.av_dump_format(_oFormatContext, 0, fileName, 1);

            if (ffmpeg.avio_open(&_oFormatContext->pb, fileName, ffmpeg.AVIO_FLAG_WRITE) < 0)
            {
                Console.WriteLine("Failed to open output file! \n");
            }

            //Write File Header
            ffmpeg.avformat_write_header(_oFormatContext, null).ThrowExceptionIfError();

            oFormatContext = _oFormatContext;
        }

        public void TryEncodeNextPacket(AVFrame frame, VideoInfo info)
        {
            var packet = ffmpeg.av_packet_alloc();
            ffmpeg.av_packet_unref(packet);

            try
            {
                int error = 0;

                do
                {
                    //Supply a raw video frame to the output condec context
                    ffmpeg.avcodec_send_frame(oCodecContext, &frame).ThrowExceptionIfError();

                    //read encodeded packet from output codec context
                    error = ffmpeg.avcodec_receive_packet(oCodecContext, packet);

                    int encodedStreamIndex = packet->stream_index;

                    // Rescale packet PTS and DTS to the output time base
                    packet->pts = ffmpeg.av_rescale_q(packet->pts, oCodecContext->time_base, info.Timebase);
                    packet->dts = ffmpeg.av_rescale_q(packet->dts, oCodecContext->time_base, info.Timebase);
                    packet->duration = ffmpeg.av_rescale_q(packet->duration, oCodecContext->time_base, info.Timebase);

                    //write frame in video file
                    ffmpeg.av_interleaved_write_frame(oFormatContext, packet);

                } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN) || error == ffmpeg.AVERROR(ffmpeg.AVERROR_EOF));
            }
            finally
            {
                ffmpeg.av_packet_unref(packet);
            }
        }

        public void FlushEncode()
        {
            ffmpeg.avcodec_send_frame(oCodecContext, null);
        }

        #region Dispose

        public void Dispose()
        {
            var _oFormatContext = oFormatContext;

            //Write file trailer
            ffmpeg.av_write_trailer(_oFormatContext);
            ffmpeg.avformat_close_input(&_oFormatContext);

            //메모리 해제
            ffmpeg.avcodec_close(oCodecContext);
            ffmpeg.av_free(oCodecContext);
        }

        #endregion

    }
}
