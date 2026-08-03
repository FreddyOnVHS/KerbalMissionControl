using System;
using System.IO;
using System.Text;
using KMC.MissionControl.Rendering.Propulsion;

namespace KMC.MissionControl.Diagnostics
{
    internal static class PropulsionGraphFileLogger
    {
        private static readonly object SyncRoot =
            new object();

        public static string LogPath
        {
            get
            {
                string directory =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder
                                .LocalApplicationData),
                        "KMC",
                        "Logs");

                return Path.Combine(
                    directory,
                    "propulsion-graph.log");
            }
        }

        public static void Write(
            PropulsionRenderGraph graph)
        {
            string report =
                PropulsionRenderGraphDiagnostics
                    .CreateReport(graph);

            string path = LogPath;
            string directory =
                Path.GetDirectoryName(path);

            lock (SyncRoot)
            {
                Directory.CreateDirectory(directory);

                using (StreamWriter writer =
                    new StreamWriter(
                        path,
                        true,
                        Encoding.UTF8))
                {
                    writer.WriteLine(
                        "==================================================");
                    writer.WriteLine(
                        DateTime.Now.ToString(
                            "yyyy-MM-dd HH:mm:ss.fff"));
                    writer.Write(report);
                    writer.WriteLine();
                }
            }
        }

        public static void WriteError(
            Exception exception)
        {
            string path = LogPath;
            string directory =
                Path.GetDirectoryName(path);

            lock (SyncRoot)
            {
                Directory.CreateDirectory(directory);

                File.AppendAllText(
                    path,
                    DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss.fff") +
                    " GRAPH ERROR: " +
                    exception +
                    Environment.NewLine,
                    Encoding.UTF8);
            }
        }
    }
}
