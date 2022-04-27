using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeAPI
{
    public struct mpeg_header
    {
        public Boolean is_valid;

        public Int32 frame_size;
        public Int32 sample_rate;
        public Int32 bitrate;
        public Int32 sample_size;
        public Double duration_ms;
        public Int32 slot_size;
        public Boolean has_padding;

        public Int32 audio_layer;
        public Int32 version;

        public String get_fullname()
        {
            switch (version)
            {
                case 25:
                    switch (audio_layer)
                    {
                        case 0: return "MPEG Version 2.5 Audio Layer rsvd";
                        case 3: return "MPEG Version 2.5 Audio Layer III";
                        case 2: return "MPEG Version 2.5 Audio Layer II";
                        case 1: return "MPEG Version 2.5 Audio Layer I";
                    }
                    goto default;
                case 0:
                    switch (audio_layer)
                    {
                        case 0: return "MPEG Version rsvd Audio Layer rsvd";
                        case 3: return "MPEG Version rsvd Audio Layer III";
                        case 2: return "MPEG Version rsvd Audio Layer II";
                        case 1: return "MPEG Version rsvd Audio Layer I";
                    }
                    goto default;
                case 2:
                    switch (audio_layer)
                    {
                        case 0: return "MPEG Version 2 Audio Layer rsvd";
                        case 3: return "MPEG Version 2 Audio Layer III";
                        case 2: return "MPEG Version 2 Audio Layer II";
                        case 1: return "MPEG Version 2 Audio Layer I";
                    }
                    goto default;
                case 1:
                    switch (audio_layer)
                    {
                        case 0: return "MPEG Version 1 Audio Layer rsvd";
                        case 3: return "MPEG Version 1 Audio Layer III";
                        case 2: return "MPEG Version 1 Audio Layer II";
                        case 1: return "MPEG Version 1 Audio Layer I";
                    }
                    goto default;
                default:
                    return null;
            }
        }

    }
    public static class MPEGHelpers
    {
        // code from: "https://hydrogenaud.io/index.php?topic=85125.0" date: 04/03/2021
        // with some extra functions and modification

        // MPEG versions - use [version]
        public static readonly Byte[] mpeg_versions = { 25, 0, 2, 1 };

        // Layers - use [layer]
        public static readonly Byte[] mpeg_layers = { 0, 3, 2, 1 };

        // Bitrates - use [version][layer][bitrate]
        public static readonly UInt16[][][] mpeg_bitrates =
        {
            new UInt16[][]
            { // Version 2.5
                new UInt16[]{ 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Reserved
                new UInt16[]{ 0,   8,  16,  24,  32,  40,  48,  56,  64,  80,  96, 112, 128, 144, 160, 0 }, // Layer 3
                new UInt16[]{ 0,   8,  16,  24,  32,  40,  48,  56,  64,  80,  96, 112, 128, 144, 160, 0 }, // Layer 2
                new UInt16[]{ 0,  32,  48,  56,  64,  80,  96, 112, 128, 144, 160, 176, 192, 224, 256, 0 }, // Layer 1
            },
            new UInt16[][]
            { // Reserved
                new UInt16[]{ 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Invalid
                new UInt16[]{ 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Invalid
                new UInt16[]{ 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Invalid
                new UInt16[]{ 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Invalid
            },
            new UInt16[][]
            { // Version 2
                new UInt16[]{ 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Reserved
                new UInt16[]{ 0,   8,  16,  24,  32,  40,  48,  56,  64,  80,  96, 112, 128, 144, 160, 0 }, // Layer 3
                new UInt16[]{ 0,   8,  16,  24,  32,  40,  48,  56,  64,  80,  96, 112, 128, 144, 160, 0 }, // Layer 2
                new UInt16[]{ 0,  32,  48,  56,  64,  80,  96, 112, 128, 144, 160, 176, 192, 224, 256, 0 }, // Layer 1
            },
            new UInt16[][]
            { // Version 1
                new UInt16[]{ 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Reserved
                new UInt16[]{ 0,  32,  40,  48,  56,  64,  80,  96, 112, 128, 160, 192, 224, 256, 320, 0 }, // Layer 3
                new UInt16[]{ 0,  32,  48,  56,  64,  80,  96, 112, 128, 160, 192, 224, 256, 320, 384, 0 }, // Layer 2
                new UInt16[]{ 0,  32,  64,  96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0 }, // Layer 1
            },
        };

        // Sample rates - use [version][srate]
        public static readonly UInt16[][] mpeg_srates =
        {
            new UInt16[]{ 11025, 12000,  8000, 0 }, // MPEG 2.5
            new UInt16[]{     0,     0,     0, 0 }, // Reserved
            new UInt16[]{ 22050, 24000, 16000, 0 }, // MPEG 2
            new UInt16[]{ 44100, 48000, 32000, 0 }, // MPEG 1
        };

        // Samples per frame - use [version][layer]
        public static readonly UInt16[][] mpeg_frame_samples =
        {
            //           Rsvd     3     2     1  < Layer  v  Version
            new UInt16[]{    0,  576, 1152,  384 }, //       2.5
            new UInt16[]{    0,    0,    0,    0 }, //       Reserved
            new UInt16[]{    0,  576, 1152,  384 }, //       2
            new UInt16[]{    0, 1152, 1152,  384 }  //       1
        };

        // Slot size (MPEG unit of measurement) - use [layer]
        public static readonly Byte[] mpeg_slot_size = { 0, 1, 1, 4 }; // Rsvd, 3, 2, 1

        public static UInt16 mpg_get_frame_size(Byte[] header_chunk)
        {
            // Quick validity check
            if (mpg_is_valid_header(header_chunk))
            {
                Double fsize;
                unchecked
                {
                    // Data to be extracted from the header
                    int ver = (header_chunk[1] & 0x18) >> 3;   // Version index
                    int lyr = (header_chunk[1] & 0x06) >> 1;   // Layer index

                    // In-between calculations
                    float bps = mpeg_frame_samples[ver][lyr] / 8.0F;
                    fsize = (bps * (mpeg_bitrates[ver][lyr][(header_chunk[2] & 0xF0) >> 4] * 1000F) / mpeg_srates[ver][(header_chunk[2] & 0x0C) >> 2]) + ((((header_chunk[2] & 0x02) >> 1) == 1) ? mpeg_slot_size[lyr] : 0);
                }
                return (UInt16)fsize;
            }
            else
                return 0;
        }
        public static Double mpg_get_frame_length(Byte[] frame)
        {
            if (mpg_is_valid_header(frame))
            {
                var ver = (frame[1] & 0x18) >> 3;   // Version index
                return (double)mpeg_frame_samples[ver][(frame[1] & 0x06) >> 1] / (double)mpeg_srates[ver][(frame[2] & 0x0C) >> 2] * 1000D;
            }
            else
                return -1;
        }
        public static void mpg_seek_to_next_header(Stream stm)
        {
            if (!stm.CanSeek)
                throw new ArgumentException("stream must be able to seek!");

            Int32 b;
            Int64 Pos;
            stm.ReadByte();
            while (true)
            {
                do
                {
                    b = stm.ReadByte();
                    if (b == -1)
                        return;
                }
                while (b != 0xFF);//0
                Pos = stm.Position - 1;
                b = stm.ReadByte();//1

                if (b == -1)
                    return;
                if (((b & 0xE0) != 0xE0)   // 3 sync bits
                    || ((b & 0x18) == 0x08)   // Version rsvd
                    || ((b & 0x06) == 0x00)   // Layer rsvd
                )
                    continue;
                b = stm.ReadByte();//2
                if (b == -1)
                    return;
                if (((b & 0xF0) == 0xF0)   // Bitrate rsvd
                    || ((b & 0xF0) == 0x00)   // Bitrate rsvd
                    || ((b & 0x0C) == 0x0C)   // Samplerate rsvd
                )
                    continue;
                break;
            }
            stm.Position = Pos;
        }
        public static Boolean mpg_is_valid_header(Byte[] header_chunk)
        {
            // Quick validity check
            return ((header_chunk[0] & 0xFF) == 0xFF)
                && ((header_chunk[1] & 0xE0) == 0xE0)   // 3 sync bits
                && ((header_chunk[1] & 0x18) != 0x08)   // Version rsvd
                && ((header_chunk[1] & 0x06) != 0x00)   // Layer rsvd
                && ((header_chunk[2] & 0xF0) != 0xF0)   // Bitrate rsvd
                && ((header_chunk[2] & 0xF0) != 0x00)   // Bitrate rsvd
                && ((header_chunk[2] & 0x0C) != 0x0C)   // Samplerate rsvd
            ;
        }
        public static String mpeg_get_fullname(Byte[] header_chunk)
        {
            switch ((header_chunk[1] & 0x18) >> 3)
            {
                case 0:
                    switch ((header_chunk[1] & 0x06) >> 1)
                    {
                        case 0: return "MPEG Version 2.5 Audio Layer rsvd";
                        case 1: return "MPEG Version 2.5 Audio Layer III";
                        case 2: return "MPEG Version 2.5 Audio Layer II";
                        case 3: return "MPEG Version 2.5 Audio Layer I";
                    }
                    goto default;
                case 1:
                    switch ((header_chunk[1] & 0x06) >> 1)
                    {
                        case 0: return "MPEG Version rsvd Audio Layer rsvd";
                        case 1: return "MPEG Version rsvd Audio Layer III";
                        case 2: return "MPEG Version rsvd Audio Layer II";
                        case 3: return "MPEG Version rsvd Audio Layer I";
                    }
                    goto default;
                case 2:
                    switch ((header_chunk[1] & 0x06) >> 1)
                    {
                        case 0: return "MPEG Version 2 Audio Layer rsvd";
                        case 1: return "MPEG Version 2 Audio Layer III";
                        case 2: return "MPEG Version 2 Audio Layer II";
                        case 3: return "MPEG Version 2 Audio Layer I";
                    }
                    goto default;
                case 3:
                    switch ((header_chunk[1] & 0x06) >> 1)
                    {
                        case 0: return "MPEG Version 1 Audio Layer rsvd";
                        case 1: return "MPEG Version 1 Audio Layer III";
                        case 2: return "MPEG Version 1 Audio Layer II";
                        case 3: return "MPEG Version 1 Audio Layer I";
                    }
                    goto default;
                default:
                    return null;
            }
        }
        public static mpeg_header mpeg_parse_header(Byte[] header_chunk)
        {
            Int32 ver = (header_chunk[1] & 0x18) >> 3;   // Version index
            Int32 lyr = (header_chunk[1] & 0x06) >> 1;   // Layer index
            Int32 srx = (header_chunk[2] & 0x0C) >> 2;   // Sample rate
            Int32 btr = (header_chunk[2] & 0xF0) >> 4;
            Int32 audio_layer = mpeg_layers[lyr];
            Int32 version = mpeg_versions[ver];

            mpeg_header header = audio_layer == 0 || version == 0 || srx == 3 || btr == 0 || btr == 15 || header_chunk[0] != 0xFF || (header_chunk[1] & 0xE0) != 0xE0
                ? new mpeg_header
                {
                    sample_rate = mpeg_srates[ver][srx],
                    bitrate = mpeg_bitrates[ver][lyr][btr] * 1000,
                    sample_size = mpeg_frame_samples[ver][lyr],
                    slot_size = mpeg_slot_size[lyr],
                    has_padding = ((header_chunk[2] & 0x02) >> 1) == 1,

                    audio_layer = audio_layer,
                    version = version,
                    is_valid = false,
                }
                : new mpeg_header
                {
                    sample_rate = mpeg_srates[ver][srx],
                    bitrate = mpeg_bitrates[ver][lyr][btr] * 1000,
                    sample_size = mpeg_frame_samples[ver][lyr],
                    slot_size = mpeg_slot_size[lyr],
                    has_padding = ((header_chunk[2] & 0x02) >> 1) == 1,

                    audio_layer = audio_layer,
                    version = version,
                    is_valid = true,
                };

            // FrameSize = Bitrate * 1000/8 * SamplesPerFrame / Frequency + IsPadding * PaddingSize
            header.frame_size = (int)((header.bitrate / (8D) * header.sample_size / header.sample_rate) + (header.has_padding ? 1 : 0));

            // Frame length (in ms) = (samples per frame / sample rate (in hz)) * 1000
            header.duration_ms = (double)header.sample_size / header.sample_rate * 1000D;

            return header;
        }
    }

}
