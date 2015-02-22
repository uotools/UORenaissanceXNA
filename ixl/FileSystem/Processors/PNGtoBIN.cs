﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using System.IO;

namespace InterXLib.FileSystem.Processors
{
    class PNGtoBIN : AProcessor
    {
        public override bool ExcludeThisFileFromLPK(string filepath)
        {
            return false;
        }

        public override bool TryProcess(string filename, byte[] data, bool allow_compression_of_files, out ProcessedFile processed_file)
        {
            processed_file = null;

            if (!InternalCheckExtension(filename, ".png"))
                return false;

            Texture2D texture;
            using (FileStream stream = new FileStream(filename, FileMode.Open))
            {
                texture = Texture2D.FromStream(AllProcessors.GraphicsDevice, stream);
            }

            if (texture != null)
            {
                List<byte> all_data = new List<byte>();

                uint[] pixels = new uint[texture.Width * texture.Height];
                texture.GetData<uint>(pixels);
                int i = 0;

                for (int y = 0; y < texture.Height; y++)
                {
                    for (int x = 0; x < texture.Width; x++)
                    {
                        AddValueToByteList(all_data, pixels[i++]);
                    }
                }
                processed_file = new ProcessedFile(filename, all_data.ToArray(), allow_compression_of_files);
            }

            return true;
        }
    }
}
