using PaintDotNet;
using PaintDotNet.IO;
using PaintDotNet.Serialization;
using System;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;

namespace IRacingPaintRefresher
{
    // Port of the Paint.NET PdnFileType class
    internal sealed class PdnFileType : FileType
    {
        public PdnFileType() : base("PDN", new FileTypeOptions()
        {
            SupportsLayers = true,
            LoadExtensions = new string[] { ".pdn" },
            SaveExtensions = new string[] { ".pdn" }
        })
        {
        }

        private static long ApproximateMaxOutputOffset(Document measureMe)
        {
            return (long)measureMe.Layers.Count * measureMe.Width * measureMe.Height * 4;
        }

        public override bool IsReflexive(SaveConfigToken token)
        {
            return true;
        }

        protected override Document OnLoad(Stream stream)
        {
            // Code lifted from PaintDotNet.Data.Document.FromStream()
            long position1 = stream.Position;
            bool flag = true;
            for(int i = 0; i < Document.MagicBytes.Length; i++)
            {
                int num = stream.ReadByte();
                if(num == -1)
                {
                    throw new EndOfStreamException();
                }
                if(num != Document.MagicBytes[i])
                {
                    flag = false;
                    break;
                }
            }

            XmlDocument? xml;
            if (flag)
            {
                int num1 = stream.ReadByte();
                if (num1 == -1)
                {
                    throw new EndOfStreamException();
                }
                int num2 = stream.ReadByte();
                if (num2 == -1)
                {
                    throw new EndOfStreamException();
                }
                int num3 = stream.ReadByte();
                if (num3 == -1)
                {
                    throw new EndOfStreamException();
                }
                int count = num1 + (num2 << 8) + (num3 << 16);
                byte[] numArray = new byte[count];
                int num4 = stream.ProperRead(numArray, 0, count);
                if (num4 != count)
                {
                    throw new EndOfStreamException($"Expected {count} bytes, but only got {num4}");
                }
                string xmlString = Encoding.UTF8.GetString(numArray);
                xml = new();
                xml.LoadXml(xmlString);
            }
            else
            {
                stream.Position = position1;
            }
            long position2 = stream.Position;
            int num5 = stream.ReadByte();
            if(num5 == -1)
            {
                throw new EndOfStreamException();
            }
            int num6 = stream.ReadByte();
            if (num6 == -1)
            {
                throw new EndOfStreamException();
            }
            BinaryFormatter binaryFormatter = new();
            SerializationFallbackBinder fallbackBinder = new();
            fallbackBinder.AddAssembly(typeof(PaintDotNet.Data.AssemblyServices).Assembly);
            fallbackBinder.AddAssembly(typeof(PaintDotNet.Core.AssemblyServices).Assembly);
            fallbackBinder.AddAssembly(typeof(PaintDotNet.SystemLayer.AssemblyServices).Assembly);
            fallbackBinder.SetNextRequiredBaseType(typeof(Document));
            binaryFormatter.Binder = fallbackBinder;
            object obj;
            if(num5 == 0 && num6 == 1)
            {
                DeferredFormatter deferredFormatter = new();
                binaryFormatter.Context = new(binaryFormatter.Context.State, deferredFormatter);
                obj = binaryFormatter.Deserialize(stream);
                deferredFormatter.FinishDeserialization(stream);
            }
            else
            {
                if(num5 != 31 && num6 != 139)
                {
                    throw new FormatException("This file is not a valid Paint.NET document");
                }
                stream.Position = position2;
                using GZipStream gZip = new(stream, CompressionMode.Decompress, true);
                obj = binaryFormatter.Deserialize(gZip);
            }
            Document document = (Document)obj;
            document.Dirty = true;
            document.Invalidate();
            return document;
        }

        protected override void OnSave(Document input, Stream output, SaveConfigToken token, Surface scratchSurface, ProgressEventHandler callback)
        {
            if (callback == null)
            {
                input.SaveToStream(output);
                return;
            }
            PdnFileType.UpdateProgressTranslator upt = new(ApproximateMaxOutputOffset(input), callback);
            input.SaveToStream(output, new IOEventHandler(upt.IOEventHandler));
        }

        private sealed class UpdateProgressTranslator
        {
            private readonly long maxBytes;

            private long totalBytes;

            private readonly ProgressEventHandler callback;

            public UpdateProgressTranslator(long maxBytes, ProgressEventHandler callback)
            {
                this.maxBytes = maxBytes;
                this.callback = callback;
                totalBytes = 0;
            }

            public void IOEventHandler(object sender, IOEventArgs e)
            {
                double percent;
                lock (this)
                {
                    totalBytes += e.Count;
                    percent = Math.Max(0, Math.Min(100, (double)totalBytes * 100 / maxBytes));
                }
                callback(sender, new ProgressEventArgs(percent));
            }
        }
    }
}
