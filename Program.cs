namespace SatTracker
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            ClearLogFileOnStartup("error.log");
            Application.Run(new MainForm());
        }

        private static void ClearLogFileOnStartup(string fileName)
        {
            try
            {
                string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                File.WriteAllText(basePath, string.Empty);

                string currentDirectoryPath = Path.Combine(Environment.CurrentDirectory, fileName);
                if (!string.Equals(basePath, currentDirectoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(currentDirectoryPath, string.Empty);
                }
            }
            catch
            {
                // Startup should continue even if the log file is temporarily locked.
            }
        }
    }
}
