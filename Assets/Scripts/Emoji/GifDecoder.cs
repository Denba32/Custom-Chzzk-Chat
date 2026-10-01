using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Denba.Chzzk.Utility
{
    /// <summary>외부 라이브러리 없이 GIF87a/GIF89a를 프레임별 Texture2D로 디코딩한다.</summary>
    public static class GifDecoder
    {
        public struct GifFrame
        {
            public Texture2D texture;
            public Sprite sprite;
            public float delay; // 초
        }

        private const byte ExtensionIntroducer = 0x21;
        private const byte ImageSeparator = 0x2C;
        private const byte Trailer = 0x3B;
        private const byte GraphicControlLabel = 0xF9;

        private const int DefaultDelayCs = 10;
        private const int MinDelayCs = 2;
        private const int MaxLzwCodes = 4096;

        private enum Disposal { None = 0, Keep = 1, RestoreBackground = 2, RestorePrevious = 3 }

        public static List<GifFrame> Decode(byte[] data)
        {
            var frames = new List<GifFrame>();

            try
            {
                using var ms = new MemoryStream(data);
                using var r = new BinaryReader(ms);

                if (!System.Text.Encoding.ASCII.GetString(r.ReadBytes(6)).StartsWith("GIF"))
                    return frames;

                int canvasW = r.ReadUInt16();
                int canvasH = r.ReadUInt16();

                byte packed = r.ReadByte();
                bool hasGlobalTable = (packed & 0x80) != 0;
                int globalTableSize = 1 << ((packed & 0x07) + 1);

                r.ReadByte(); // 배경색 인덱스
                r.ReadByte(); // 픽셀 비율

                Color32[] globalTable = hasGlobalTable ? ReadColorTable(r, globalTableSize) : null;
                var canvas = new Color32[canvasW * canvasH];

                int delayCs = DefaultDelayCs;
                int transparentIndex = -1;
                var disposal = Disposal.None;

                while (ms.Position < ms.Length)
                {
                    byte block = r.ReadByte();
                    if (block == Trailer) break;

                    if (block == ExtensionIntroducer)
                    {
                        if (r.ReadByte() == GraphicControlLabel)
                        {
                            r.ReadByte(); // 블록 크기(4)
                            byte flags = r.ReadByte();
                            disposal = (Disposal)((flags >> 2) & 0x07);
                            delayCs = r.ReadUInt16();
                            byte transparent = r.ReadByte();
                            transparentIndex = (flags & 0x01) != 0 ? transparent : -1;
                            r.ReadByte(); // 종결자
                        }
                        else
                        {
                            SkipSubBlocks(r);
                        }
                        continue;
                    }

                    if (block == ImageSeparator)
                    {
                        int x = r.ReadUInt16();
                        int y = r.ReadUInt16();
                        int w = r.ReadUInt16();
                        int h = r.ReadUInt16();

                        byte imagePacked = r.ReadByte();
                        bool hasLocalTable = (imagePacked & 0x80) != 0;
                        bool interlaced = (imagePacked & 0x40) != 0;

                        Color32[] table = hasLocalTable
                            ? ReadColorTable(r, 1 << ((imagePacked & 0x07) + 1))
                            : globalTable ?? new Color32[256];

                        int minCodeSize = r.ReadByte();
                        byte[] indices = LzwDecode(ReadSubBlocks(r), minCodeSize, w * h);
                        if (interlaced) indices = Deinterlace(indices, w, h);

                        Color32[] snapshot = disposal == Disposal.RestorePrevious ? (Color32[])canvas.Clone() : null;

                        DrawFrame(canvas, canvasW, canvasH, indices, table, transparentIndex, x, y, w, h);
                        frames.Add(CreateFrame(canvas, canvasW, canvasH, delayCs));

                        switch (disposal)
                        {
                            case Disposal.RestoreBackground:
                                ClearRegion(canvas, canvasW, canvasH, x, y, w, h);
                                break;
                            case Disposal.RestorePrevious when snapshot != null:
                                Array.Copy(snapshot, canvas, canvas.Length);
                                break;
                        }

                        delayCs = DefaultDelayCs;
                        transparentIndex = -1;
                        disposal = Disposal.None;
                        continue;
                    }

                    SkipSubBlocks(r);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GifDecoder] 파싱 중 오류: {e.Message}");
            }

            return frames;
        }

        // GIF는 위→아래, Unity 텍스처는 아래→위이므로 Y를 뒤집어 기록한다.
        private static void DrawFrame(
            Color32[] canvas, int canvasW, int canvasH,
            byte[] indices, Color32[] table, int transparentIndex,
            int frameX, int frameY, int frameW, int frameH)
        {
            for (int py = 0; py < frameH; py++)
            {
                int cy = frameY + py;
                if (cy < 0 || cy >= canvasH) continue;

                int rowStart = (canvasH - 1 - cy) * canvasW;
                for (int px = 0; px < frameW; px++)
                {
                    int cx = frameX + px;
                    if (cx < 0 || cx >= canvasW) continue;

                    int index = indices[py * frameW + px];
                    if (index == transparentIndex) continue;

                    canvas[rowStart + cx] = table[index];
                }
            }
        }

        private static void ClearRegion(Color32[] canvas, int canvasW, int canvasH, int x, int y, int w, int h)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (int py = 0; py < h; py++)
            {
                int cy = y + py;
                if (cy < 0 || cy >= canvasH) continue;

                int rowStart = (canvasH - 1 - cy) * canvasW;
                for (int px = 0; px < w; px++)
                {
                    int cx = x + px;
                    if (cx >= 0 && cx < canvasW)
                        canvas[rowStart + cx] = clear;
                }
            }
        }

        private static GifFrame CreateFrame(Color32[] canvas, int w, int h, int delayCs)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels32(canvas);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);

            return new GifFrame
            {
                texture = tex,
                sprite = sprite,
                delay = Mathf.Max(delayCs, MinDelayCs) / 100f
            };
        }

        private static Color32[] ReadColorTable(BinaryReader r, int count)
        {
            var table = new Color32[count];
            for (int i = 0; i < count; i++)
                table[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), 255);
            return table;
        }

        private static byte[] ReadSubBlocks(BinaryReader r)
        {
            using var buffer = new MemoryStream();
            int size;
            while ((size = r.ReadByte()) > 0)
            {
                byte[] chunk = r.ReadBytes(size);
                buffer.Write(chunk, 0, chunk.Length);
            }
            return buffer.ToArray();
        }

        private static void SkipSubBlocks(BinaryReader r)
        {
            int size;
            while ((size = r.ReadByte()) > 0)
                r.BaseStream.Seek(size, SeekOrigin.Current);
        }

        private static byte[] Deinterlace(byte[] src, int w, int h)
        {
            var dst = new byte[src.Length];
            int srcRow = 0;

            // (시작 행, 간격) 4단계
            foreach (var (start, step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
            {
                for (int y = start; y < h; y += step)
                    Array.Copy(src, srcRow++ * w, dst, y * w, w);
            }

            return dst;
        }

        // 접두어/접미어 테이블 방식 LZW. 코드마다 배열을 할당하지 않는다.
        private static byte[] LzwDecode(byte[] data, int minCodeSize, int pixelCount)
        {
            var output = new byte[pixelCount];

            int clearCode = 1 << minCodeSize;
            int eofCode = clearCode + 1;
            int codeSize = minCodeSize + 1;
            int nextCode = eofCode + 1;

            var prefix = new short[MaxLzwCodes];
            var suffix = new byte[MaxLzwCodes];
            var stack = new byte[MaxLzwCodes + 1];
            for (int i = 0; i < clearCode; i++)
                suffix[i] = (byte)i;

            int bitBuffer = 0, bitCount = 0, dataPos = 0;
            int prev = -1;
            byte first = 0;
            int outPos = 0;

            while (outPos < pixelCount)
            {
                while (bitCount < codeSize && dataPos < data.Length)
                {
                    bitBuffer |= data[dataPos++] << bitCount;
                    bitCount += 8;
                }
                if (bitCount < codeSize) break;

                int code = bitBuffer & ((1 << codeSize) - 1);
                bitBuffer >>= codeSize;
                bitCount -= codeSize;

                if (code == clearCode)
                {
                    codeSize = minCodeSize + 1;
                    nextCode = eofCode + 1;
                    prev = -1;
                    continue;
                }
                if (code == eofCode) break;

                if (prev == -1)
                {
                    if (code >= clearCode) break;

                    output[outPos++] = first = (byte)code;
                    prev = code;
                    continue;
                }

                int current = code;
                int sp = 0;

                if (code >= nextCode)
                {
                    if (code > nextCode) break; // 잘못된 코드

                    stack[sp++] = first;
                    current = prev;
                }

                while (current >= clearCode)
                {
                    stack[sp++] = suffix[current];
                    current = prefix[current];
                }

                first = (byte)current;
                stack[sp++] = first;

                if (nextCode < MaxLzwCodes)
                {
                    prefix[nextCode] = (short)prev;
                    suffix[nextCode] = first;
                    nextCode++;

                    if (nextCode >= (1 << codeSize) && codeSize < 12)
                        codeSize++;
                }

                prev = code;

                while (sp > 0 && outPos < pixelCount)
                    output[outPos++] = stack[--sp];
            }

            return output;
        }
    }
}
