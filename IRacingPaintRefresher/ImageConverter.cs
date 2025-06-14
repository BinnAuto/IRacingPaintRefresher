using ImageMagick;
using iRacingPaintLoader;
using PaintDotNet;
using PaintDotNet.Data.PhotoshopFileType;
using Svg;
using System.Drawing;
using System.Drawing.Imaging;

namespace IRacingPaintRefresher
{
    public class ImageConverter
    {
        private const string TempPsdPath = "./_temp_.psd";

        private const string TempPngPath = "./_temp_.png";

        private readonly ImageType ImageType;

        public ImageConverter(ImageType imageType)
        {
            ImageType = imageType;
        }


        public void ConvertImage(string? imagePath, string fileSuffix)
        {
            string extension = Path.GetExtension(imagePath).ToLower();
            switch(extension)
            {
                case ".pdn":
                    ConvertPdn(imagePath);
                    break;

                case ".psd":
                    ConvertPsd(imagePath);
                    break;

                case ".svg":
                    ConvertSvg(imagePath);
                    break;
            }
            CreateOutputImage(fileSuffix);
        }


        private static void ConvertPdn(string imagePath)
        {
            File.Delete(TempPngPath);
            File.Delete(TempPsdPath);

            using (FileStream fileStream = new(imagePath, FileMode.Open))
            {
                PdnFileType pdn = new();
                var document = pdn.Load(fileStream);
                FileStream output = new(TempPsdPath, FileMode.CreateNew);
                PsdSaveConfigToken token = new(true);
                Surface surface = new(document.Width, document.Height);
                ProgressEventHandler progressHandler = new(EmptyEventHandler);
                PsdSave.Save(document, output, token, surface, progressHandler);
                output.Close();
                ConvertPsd(TempPsdPath);
                File.Delete(TempPsdPath);
            }
        }


        private static void ConvertPsd(string imagePath)
        {
            MagickImage image = new(imagePath)
            {
                Format = MagickFormat.Png32
            };
            image.Write(TempPngPath);
        }


        private static void ConvertSvg(string imagePath)
        {
            var document = SvgDocument.Open(imagePath);
            var bitmap = document.Draw(2048, 2048);
            bitmap.Save(TempPngPath);
            bitmap.Dispose();
        }


        private void CreateOutputImage(string fileSuffix)
        {
            // Convert to tga
            Bitmap bitmap = new(TempPngPath);
            Bitmap clone = new(bitmap);
            Bitmap newBitmap = clone.Clone(new Rectangle(0, 0, clone.Width, clone.Height), PixelFormat.Format32bppArgb);
            TGA tga = (TGA)newBitmap;
            clone.Dispose();
            bitmap.Dispose();

            // Build output path
            string outputFileName = $"car_";
            if(ImageType == ImageType.Paint && AppConfig.IsCustomNumber)
            {
                outputFileName += $"num_";
            }
            if(ImageType == ImageType.SpecMap)
            {
                outputFileName += $"spec_";
            }
            outputFileName += $"{AppConfig.IRacingId}{fileSuffix}.tga";
            string outputFilePath = Path.Combine(AppConfig.OutputPath, outputFileName);
            tga.Save(outputFilePath);
            newBitmap.Dispose();
            File.Delete(TempPngPath);
        }


        private static void EmptyEventHandler(object sender, PaintDotNet.ProgressEventArgs e) { }
    }
}
