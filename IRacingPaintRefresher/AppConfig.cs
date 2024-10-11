using Microsoft.Extensions.Configuration;
using System;

namespace IRacingPaintRefresher
{
    internal static class AppConfig
    {
        private static IConfigurationRoot Configuration = new ConfigurationBuilder().Build();

        public static string? OutputPath { get; set; }

        public static int? iRacingId
        {
            get
            {
                return int.TryParse(Configuration["iRacingId"], out int result)
                    ? result : null;
            }
        }

        public static int RefreshRate
        {
            get
            {
                return int.TryParse(Configuration["RefreshRate"], out int result)
                    ? result : 500;
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


        private static void CreateDefaultConfigFile()
        {
            string[] fileLines = new string[]
            {
                "{",
                "  \"OutputPath\": \'\',",
                "  \"iRacingId\": 999999,",
                "  \"RefreshRate\": 500",
                "}"
            };
            File.WriteAllLines("./appsettings.json", fileLines);
        }
    }
}
