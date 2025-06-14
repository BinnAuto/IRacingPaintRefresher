using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IRacingPaintRefresher
{
    internal static class AppConfig
    {
        private static IConfigurationRoot Configuration = new ConfigurationBuilder().Build();

        public static string? OutputPath { get; set; }

        public static int? IRacingId
        {
            get
            {
                if(_iRacingId.HasValue)
                {
                    return _iRacingId;
                }
                _iRacingId = int.TryParse(Configuration["iRacingId"], out int result)
                    ? result : null;
                return _iRacingId;
            }
            set
            {
                _iRacingId = value;
            }
        }
        private static int? _iRacingId;

        public static int RefreshRate
        {
            get
            {
                return int.TryParse(Configuration["RefreshRate"], out int result)
                    ? result : 500;
            }
        }

        public static int DownloadTimeoutMinutes
        {
            get
            {
                return int.TryParse(Configuration["DownloadTimeoutMinutes"], out int result)
                    ? result : 30;
            }
        }

        public static bool IsCustomNumber { get; set; } = false;


        public static void Load()
        {
            try
            {
                if(!File.Exists("./appsettings.json"))
                {
                    CreateDefaultConfigFile();
                }
                Configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build();
            }
            catch(Exception e)
            {
                throw new($"Error loading configuration: {e.Message}");
            }
        }


        public static Dictionary<string, string> GetModelVariants()
        {
            Dictionary<string, string> result = new();
            var modelVariants = Configuration.GetSection("ModelVariants").GetChildren();
            if(modelVariants.Count() == 0)
            {
                return result;
            }

            foreach (var v in modelVariants)
            {
                string variant = v["Variant"];
                string fileSuffix = v["FileSuffix"];
                result[variant.ToLower()] = fileSuffix;
            }
            return result;
        }


        private static void CreateDefaultConfigFile()
        {
            string[] fileLines = new string[]
            {
                "{",
                "  \"iRacingId\": 999999,",
                "  \"RefreshRate\": 500",
                "  \"DownloadTimeoutMinutes\": 30",
                "}"
            };
            File.WriteAllLines("./appsettings.json", fileLines);
        }
    }
}
