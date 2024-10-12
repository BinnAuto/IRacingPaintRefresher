using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace IRacingPaintRefresher
{
    public static class TradingPaintsDownloader
    {
        private const string templateListUrl = "https://tradingpaints.com/cartemplates";
        private const string templateFileUrl = "https://ir-core-sites.iracing.com/members/member_images/misctemplates/all_iracing_templates.zip";

        public static async Task<bool> DownloadTemplates(Func<string, object> callback)
        {
            var folderMappings = BuildTemplateFolderMappings();
            byte[] templateZipData = await DownloadTemplateFile();
            callback("Template file downloaded");

            using MemoryStream dataStream = new(templateZipData);
            using ZipArchive zipArchive = new(dataStream, ZipArchiveMode.Read, true);
            var zipEntries = zipArchive.Entries;
            string iRacingPaintFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "iRacing/paint");
            if(!Directory.Exists(iRacingPaintFolder))
            {
                throw new("iRacing Paint folder does not exist");
            }

            string tempFolder = Path.Combine(iRacingPaintFolder, "_temp");
            Directory.CreateDirectory(tempFolder);
            try
            {
                foreach (var entry in zipEntries)
                {
                    string fileName = entry.Name;
                    string carIdString = fileName.Split("_").First();
                    int carId = int.Parse(carIdString);
                    if (false == folderMappings.ContainsKey(carId))
                    {
                        continue;
                    }

                    string filePath = Path.Combine(tempFolder, fileName);
                    entry.ExtractToFile(filePath, true);
                    byte[] childZipData = File.ReadAllBytes(filePath);
                    using MemoryStream childDataStream = new(childZipData);
                    using ZipArchive childZipArchive = new(childDataStream, ZipArchiveMode.Read, true);
                    var childZipEntry = childZipArchive.Entries[0];
                    string childFileName = childZipEntry.Name;
                    foreach (var folder in folderMappings[carId])
                    {
                        try
                        {
                            string extractFilePath = Path.Combine(iRacingPaintFolder, folder, childFileName);
                            childZipEntry.ExtractToFile(extractFilePath, true);
                            #if DEBUG
                            callback($"Extracted file to {folder}");
                            #endif
                        }
                        catch(Exception ex)
                        {
                            string message = $"Failed to extract to {folder}: {ex.Message}";
                            callback(message);
                        }
                    }
                }
            }
            catch(Exception e)
            {
                throw; 
            }
            finally
            {
                Directory.Delete(tempFolder, true);
            }

            return true;
        }


        private static Dictionary<int, List<string>> BuildTemplateFolderMappings()
        {
            HtmlWeb web = new();
            var document = web.Load(templateListUrl);
            var linkNodes = document.DocumentNode.SelectNodes("//div[starts-with(@id, 'car_')]/a");
            var pathNodes = document.DocumentNode.SelectNodes("//div[starts-with(@id, 'car_')]/a/div/span");
            Dictionary<int, List<string>> result = new();
            for (int i = 0; i < linkNodes.Count; i++)
            {
                var n = linkNodes[i];
                string linkUrl = n.Attributes["href"].Value;
                string zipFileName = linkUrl.Split("/").Last();
                string folderName = pathNodes[i].InnerText.Split("/").Last();
                int carId = int.Parse(zipFileName.Split("_").First());
                if(false == result.ContainsKey(carId))
                {
                    result[carId] = new();
                }
                result[carId].Add(folderName);
            }
            return result;
        }


        private async static Task<byte[]> DownloadTemplateFile()
        {
            #if DEBUG
            return File.ReadAllBytes("G:\\Development\\IRacingPaintRefresher\\IRacingPaintRefresher\\all_iracing_templates.zip");
            #endif
            using HttpClient httpClient = new();
            httpClient.Timeout = TimeSpan.FromMinutes(AppConfig.DownloadTimeoutMinutes);
            var result = await httpClient.GetByteArrayAsync(templateFileUrl);
            return result;
        }
    }
}
