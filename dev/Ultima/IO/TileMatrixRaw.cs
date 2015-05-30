﻿/***************************************************************************
 *                               TileMatrix.cs
 *                            -------------------
 *   begin                : May 1, 2002
 *   copyright            : (C) The RunUO Software Team
 *   email                : info@runuo.com
 *
 *   $Id: TileMatrix.cs 252 2007-09-14 07:59:32Z mark $
 *   
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 ***************************************************************************/
#region usings
using System;
using System.Collections.Generic;
using System.IO;
using UltimaXNA.Core;
using UltimaXNA.Core.Diagnostics;
#endregion

namespace UltimaXNA.Ultima.IO
{
    public class TileMatrixRaw
    {
        private static uint[] m_MapBlockHeightList = new uint[] { 512, 512, 200, 256, 181 };
        private const int m_SizeLandBlock = 196;
        private const int m_SizeLandBlockData = 192;

        private byte[] m_EmptyStaticsBlock;
        private byte[] m_InvalidLandBlock;

        private const uint m_bufferedLandBlocksMaxCount = 256; 
        private byte[][] m_bufferedLandBlocks;
        private uint[] m_bufferedLandBlocks_Keys;

        public byte[] EmptyStaticsBlock
        {
            get { return m_EmptyStaticsBlock; }
        }

        private readonly FileStream MapStream;
        private readonly FileStream StaticIndexStream;
        private readonly FileStream StaticDataStream;
        private readonly BinaryReader StaticIndexReader;

        public uint BlockHeight
        {
            get;
            private set;
        }

        public uint BlockWidth
        {
            get;
            private set;
        }

        private List<TileMatrix> m_FileShare = new List<TileMatrix>();

        public TileMatrixRaw(uint index, uint id)
        {
            MapStream = FileManager.GetFile("map{0}.mul", index);
            StaticIndexStream = FileManager.GetFile("staidx{0}.mul", index);
            StaticDataStream = FileManager.GetFile("statics{0}.mul", index);

            if (MapStream == null)
            {
                // the map we tried to load does not exist. Try alternate for felucca / trammel ?
                if (index == 1)
                {
                    index = 0;
                    MapStream = FileManager.GetFile("map{0}.mul", index);
                    StaticIndexStream = FileManager.GetFile("staidx{0}.mul", index);
                    StaticDataStream = FileManager.GetFile("statics{0}.mul", index);
                }
            }

            StaticIndexReader = new BinaryReader(StaticIndexStream);

            BlockHeight = m_MapBlockHeightList[index];
            BlockWidth = (uint)MapStream.Length / (BlockHeight * m_SizeLandBlock);

            m_EmptyStaticsBlock = new byte[0];
            m_InvalidLandBlock = new byte[m_SizeLandBlockData];
            m_bufferedLandBlocks_Keys = new uint[m_bufferedLandBlocksMaxCount];
            m_bufferedLandBlocks = new byte[m_bufferedLandBlocksMaxCount][];
            for (uint i = 0; i < m_bufferedLandBlocksMaxCount; i++)
                m_bufferedLandBlocks[i] = new byte[m_SizeLandBlockData];
        }

        public byte[] GetLandBlock(uint x, uint y)
        {
            if (MapStream == null)
            {
                return m_InvalidLandBlock;
            }
            else
            {
                return readLandBlock_Bytes(x, y);
            }
        }

        /// <summary>
        /// Retrieve the tileID and altitude of a specific land tile. N.B. VERY INEFFECIENT.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="TileID"></param>
        /// <param name="alt"></param>
        public void GetLandTile(uint x, uint y, out ushort TileID, out sbyte alt)
        {
            uint index = (((x % 8) + (y % 8) * 8) * 3);
            byte[] data = readLandBlock_Bytes(x >> 3, y >> 3);
            TileID = BitConverter.ToUInt16(data, (int)index);
            alt = (sbyte)data[index + 2];
        }

        public byte[] GetStaticBlock(uint x, uint y)
        {
            if (x >= BlockWidth || y >= BlockHeight || StaticDataStream == null || StaticIndexStream == null)
            {
                return m_EmptyStaticsBlock;
            }
            else
            {
                return readStaticBlock_Bytes(x, y);
            }
        }

        private unsafe byte[] readStaticBlock_Bytes(uint x, uint y)
        {
            try
            {
                StaticIndexStream.Seek(((x * BlockHeight) + y) * 12, SeekOrigin.Begin);

                int lookup = StaticIndexReader.ReadInt32();
                int length = StaticIndexReader.ReadInt32();

                if (lookup < 0 || length <= 0)
                {
                    return m_EmptyStaticsBlock;
                }
                else
                {
                    StaticDataStream.Seek(lookup, SeekOrigin.Begin);

                    byte[] staticTiles = new byte[length];

                    fixed (byte* pStaticTiles = staticTiles)
                    {
                        NativeMethods.Read(StaticDataStream.SafeFileHandle, pStaticTiles, length);
                    }
                    return staticTiles;
                }
            }
            catch (EndOfStreamException)
            {
                throw new Exception("End of stream in static block!");
                // return m_EmptyStaticsBlock;
            }
        }

        private unsafe byte[] readLandBlock_Bytes(uint x, uint y)
        {
            if (x >= BlockWidth) x -= BlockWidth;
            if (y >= BlockHeight) y -= BlockHeight;

            uint key = (x << 16) + y;
            uint index = x % 16 + (y % 16) * 16;
            if (m_bufferedLandBlocks_Keys[index] == key)
                return m_bufferedLandBlocks[index];

            m_bufferedLandBlocks_Keys[index] = key;

            MapStream.Seek(((x * BlockHeight) + y) * m_SizeLandBlock + 4, SeekOrigin.Begin);
            int streamStart = (int)MapStream.Position;
            fixed (byte* pData = m_bufferedLandBlocks[index])
            {
                NativeMethods.Read(MapStream.SafeFileHandle, pData, m_SizeLandBlockData);
            }
            Metrics.ReportDataRead((int)MapStream.Position - streamStart);

            return m_bufferedLandBlocks[index];
        }

        public void Dispose()
        {
            if (StaticIndexReader != null)
            {
                StaticIndexReader.Close();
            }

            if (MapStream != null)
            {
                MapStream.Close();
            }

            if (StaticDataStream != null)
            {
                StaticDataStream.Close();
            }
        }
    }
}